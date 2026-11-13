using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BouncingBallController : MonoBehaviour
{
    [Header("Ball Movement")]
    [Tooltip("Speed gained per second while input is within the shared angle tolerance.")]
    [Min(0f)]
    [SerializeField] private float acceleration = 10f;

    [Tooltip("Sideways acceleration used to gently steer the ball without changing its speed.")]
    [Min(0f)]
    [SerializeField] private float steeringAcceleration = 3f;

    [Header("Pinball assistance")]
    [Range(0f, .5f)] [SerializeField] private float wallSpeedBonus = .15f;
    [Min(0f)] [SerializeField] private float minimumWallImpactSpeed = 2f;
    [Min(0f)] [SerializeField] private float wallBoostCooldown = .12f;
    [Min(0f)] [SerializeField] private float minionHaloExtraDiameters = .3f;
    [Range(0f, 1f)] [SerializeField] private float wallSoundVolume = .12f;
    private float nextWallBoost, boostFlashUntil;
    private readonly Collider[] haloOverlaps = new Collider[64];
    private readonly RaycastHit[] haloSweep = new RaycastHit[64];
    private LineRenderer attackRing;
    private Material assistanceMaterial;
    private AudioSource boostAudio;
    private AudioClip boostClip;
    private SphereCollider ballShape;
    public float AttackHaloRadius => BallRadius + minionHaloExtraDiameters * BallRadius * 2f;
    private float BallRadius => ballShape != null ? ballShape.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z)) : .5f;
    public float WallBoostFlashUntil => boostFlashUntil;
    public event System.Action WallBounced;
    public event System.Action<EnemyBumper> DashHitEnemy;

    private Rigidbody rb;
    private Transform cameraTransform;
    private BonkRobotController speedSystem;

    private Vector3 simulationVelocity;
    private PhysicsMaterial frictionlessMaterial;
    private EnemyWaveController encounter;
    private readonly HashSet<Collider> contactSurfaces = new HashSet<Collider>();
    public void ConfigureEncounter(EnemyWaveController owner) => encounter = owner;

    public void StopForSnare()
    {
        InitializeBody();
        dashInProgress = false;
        simulationVelocity = Vector3.zero;
        SetPlanarVelocity(Vector3.zero);
    }

    private bool isBallMode;
    private bool dashInProgress;
    private float inputTime;
    private float matchingDirectionTime;

    private readonly HashSet<EnemyBumper> contactEnemies = new HashSet<EnemyBumper>();
    private readonly HashSet<EnemyBumper> resolvedEnemies =
        new HashSet<EnemyBumper>();

    public PlayerSpeedSettings SpeedSettings =>
        speedSystem != null
            ? speedSystem.SpeedSettings
            : PlayerSpeedSettings.Default;

    public Vector3 PlanarVelocity =>
        rb != null
            ? PlayerMovementMath.Planar(rb.linearVelocity)
            : Vector3.zero;
    
    public float AccelerationEfficiency01 { get; private set; }
    
    public float DirectionMatchRatio01 =>
        inputTime > 0f
            ? Mathf.Clamp01(matchingDirectionTime / inputTime)
            : 0f;
    
    public float AccelerationSuccessRatio01 => DirectionMatchRatio01;

    private void Awake()
    {
        InitializeBody();
        ballShape = GetComponent<SphereCollider>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (speedSystem == null)
            speedSystem = GetComponentInParent<BonkRobotController>();
    }

    private void InitializeBody()
    {
        if (rb != null)
            return;

        rb = GetComponent<Rigidbody>();
        
        rb.linearDamping = 0f;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        frictionlessMaterial = new PhysicsMaterial("Ball Speed System")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,

            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        foreach (Collider collider in
                 GetComponentsInChildren<Collider>(true))
        {
            if (collider.attachedRigidbody == rb && !collider.isTrigger)
                collider.sharedMaterial = frictionlessMaterial;
        }
    }

    public void Configure(BonkRobotController owner)
    {
        speedSystem = owner;
    }

    public void ApplyKnockback(Vector3 direction, float strength)
    {
        InitializeBody();
        simulationVelocity = PlayerMovementMath.KnockbackVelocity(simulationVelocity, direction, strength, SpeedSettings.MaximumSpeed);
        SetPlanarVelocity(simulationVelocity);
        rb.WakeUp();
    }

    public bool DashTowards(Vector3 direction)
    {
        if (!isBallMode || (speedSystem != null && speedSystem.IsEncounterLocked)) return false;
        direction = PlayerMovementMath.Planar(direction);
        if (direction.sqrMagnitude < 0.0001f) return false;

        InitializeBody();
        dashInProgress = true;
        simulationVelocity = direction.normalized * SpeedSettings.MaximumSpeed;
        SetPlanarVelocity(simulationVelocity);
        rb.WakeUp();
        return true;
    }

    public void EnterBallMode(Vector3 inheritedVelocity)
    {
        InitializeBody();

        rb.position = transform.position;
        rb.rotation = transform.rotation;

        simulationVelocity =
            PlayerMovementMath.Planar(inheritedVelocity);

        rb.linearVelocity = simulationVelocity;
        rb.angularVelocity = Vector3.zero;

        AccelerationEfficiency01 = 0f;
        
        inputTime = 0f;
        matchingDirectionTime = 0f;
        isBallMode = true;
        dashInProgress = false;

        resolvedEnemies.Clear();
        contactEnemies.Clear();
        contactSurfaces.Clear();
        nextWallBoost = boostFlashUntil = float.NegativeInfinity;
        rb.WakeUp();
    }

    public void ExitBallMode()
    {
        isBallMode = false;
        dashInProgress = false;
        AccelerationEfficiency01 = 0f;
        
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        simulationVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (speedSystem != null && (speedSystem.IsEncounterLocked || speedSystem.IsSnared))
        {
            SetPlanarVelocity(Vector3.zero);
            simulationVelocity = Vector3.zero;
            return;
        }
        if (!isBallMode)
            return;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector3 velocity = PlanarVelocity;
        float speed = velocity.magnitude;

        Vector3 inputDirection = speedSystem != null && speedSystem.IsKnockedBack
            ? Vector3.zero : PlayerMovementMath.ReadInputDirection(cameraTransform);

        bool hasInput = inputDirection.sqrMagnitude > 0f;

        Vector3 travelDirection = speed > 0f
            ? velocity / speed
            : hasInput
                ? inputDirection
                : PlayerMovementMath.Planar(transform.forward).normalized;
        
        bool canAccelerate = PlayerMovementMath.AllowsAcceleration(
            travelDirection,
            inputDirection,
            SpeedSettings.AccelerationAngleTolerance);
        
        if (hasInput)
        {
            inputTime += Time.fixedDeltaTime;

            if (canAccelerate)
                matchingDirectionTime += Time.fixedDeltaTime;
        }

        if (speed > 0f && hasInput)
        {
            Vector3 sidewaysInput =
                inputDirection -
                travelDirection *
                Vector3.Dot(inputDirection, travelDirection);

            Vector3 steeredDirection =
                travelDirection +
                sidewaysInput *
                (steeringAcceleration * Time.fixedDeltaTime / speed);

            if (steeredDirection.sqrMagnitude > 0f)
                travelDirection = steeredDirection.normalized;
        }

        float previousSpeed = speed;
        float expectedSpeedGain =
            acceleration * Time.fixedDeltaTime;

        if (speed < SpeedSettings.MaximumSpeed && canAccelerate)
        {
            speed = Mathf.Min(
                SpeedSettings.MaximumSpeed,
                speed + expectedSpeedGain);
        }

        AccelerationEfficiency01 = expectedSpeedGain > 0f
            ? Mathf.Clamp01(
                (speed - previousSpeed) / expectedSpeedGain)
            : 0f;

        velocity = travelDirection * speed;
        SetPlanarVelocity(velocity);
        
        simulationVelocity = velocity;
        resolvedEnemies.Clear();
        ResolveAttackHalo();
    }

    private void SetPlanarVelocity(Vector3 velocity)
    {
        Vector3 current = rb.linearVelocity;

        rb.linearVelocity = new Vector3(
            velocity.x,
            current.y,
            velocity.z);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ResolveCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        EnemyBumper enemy = collision.collider.GetComponentInParent<EnemyBumper>();
        if (enemy != null) contactEnemies.Remove(enemy);
        contactSurfaces.Remove(collision.collider);
    }

    private void OnDisable()
    {
        dashInProgress = false;
        if (attackRing != null) attackRing.enabled = false;
        if (boostAudio != null) boostAudio.Stop();
        contactEnemies.Clear();
        contactSurfaces.Clear();
    }

    private void OnCollisionStay(Collision collision)
    {
        ResolveCollision(collision);
    }

    private void ResolveCollision(Collision collision)
    {
        EnemyBumper enemy =
            collision.collider.GetComponentInParent<EnemyBumper>();

        if (enemy != null)
        {
            if (!resolvedEnemies.Add(enemy)) return;
            if (contactEnemies.Add(enemy))
            {
                if (dashInProgress)
                {
                    dashInProgress = false;
                    if (!enemy.IsDefeated) DashHitEnemy?.Invoke(enemy);
                }

                EnemyHitResult hit = enemy.Encounter != null
                    ? enemy.Encounter.TryBonk(enemy, simulationVelocity.magnitude, SpeedSettings)
                    : enemy.TryHitFromBall(simulationVelocity.magnitude, SpeedSettings);
                if (hit == EnemyHitResult.Defeated)
                {
                    SetPlanarVelocity(simulationVelocity);
                    return;
                }
            }
        }
        if (enemy != null) enemy.ContactPlayer(GetComponentInParent<PlayerHealth>());
        bool freshSurface = enemy == null && contactSurfaces.Add(collision.collider);
        bool reflected = false;
        bool meaningfulWall = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 surfaceNormal = collision.GetContact(i).normal;
            Vector3 normal = PlayerMovementMath.Planar(surfaceNormal);
            
            if (normal.sqrMagnitude < 0.0001f)
                continue;

            if (Vector3.Dot(simulationVelocity, normal) < 0f)
            {
                meaningfulWall |= Mathf.Abs(surfaceNormal.y) < .4f && simulationVelocity.magnitude >= minimumWallImpactSpeed &&
                    -Vector3.Dot(simulationVelocity.normalized, normal.normalized) >= .2f;
                reflected = true;
                simulationVelocity =
                    PlayerMovementMath.ReflectPlanar(
                        simulationVelocity,
                        normal);
            }
        }

        if (enemy == null && reflected) dashInProgress = false;
        if (freshSurface && meaningfulWall && isBallMode) WallBounced?.Invoke();
        if (freshSurface && reflected && encounter != null && encounter.IsCombatActive)
        {
            if (meaningfulWall) TryWallBoost();
            encounter.RegisterRicochet(simulationVelocity.magnitude);
        }
        if (enemy != null || simulationVelocity.sqrMagnitude > 0f)
            SetPlanarVelocity(simulationVelocity);
    }

    // Called only for a newly separated, meaningful wall contact, never for enemy hits or floor contact.
    private bool TryWallBoost()
    {
        if (!isBallMode || Time.time < nextWallBoost || simulationVelocity.magnitude < minimumWallImpactSpeed) return false;
        float speed = simulationVelocity.magnitude;
        float boosted = Mathf.Min(SpeedSettings.MaximumSpeed, speed * (1f + wallSpeedBonus));
        if (boosted <= speed) return false;
        simulationVelocity = simulationVelocity.normalized * boosted;
        nextWallBoost = Time.time + wallBoostCooldown;
        boostFlashUntil = Time.time + .3f;
        PlayBoostSound();
        return true;
    }

    private void ResolveAttackHalo()
    {
        if (encounter == null || !encounter.IsCombatActive || !isBallMode || minionHaloExtraDiameters <= 0f ||
            simulationVelocity.magnitude < SpeedSettings.SmallEnemyKillSpeed) return;
        Vector3 origin = ballShape != null ? transform.TransformPoint(ballShape.center) : rb.position;
        int count = Physics.OverlapSphereNonAlloc(origin, AttackHaloRadius, haloOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) TryHaloTarget(haloOverlaps[i], origin);
        float distance = simulationVelocity.magnitude * Time.fixedDeltaTime;
        count = Physics.SphereCastNonAlloc(origin, AttackHaloRadius, simulationVelocity.normalized, haloSweep,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) TryHaloTarget(haloSweep[i].collider, origin);
    }
    private void TryHaloTarget(Collider target, Vector3 origin)
    {
        EnemyBumper enemy = target.GetComponentInParent<EnemyBumper>();
        if (enemy == null || enemy.IsDefeated || !enemy.IsBasicMinion || enemy.Encounter != encounter || resolvedEnemies.Contains(enemy)) return;
        // Assistance never reaches through walls or solid obstacles.
        if (Physics.Linecast(origin, target.bounds.center, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<EnemyBumper>() != enemy) return;
        resolvedEnemies.Add(enemy);
        encounter.TryBonk(enemy, simulationVelocity.magnitude, SpeedSettings);
    }
    private void LateUpdate()
    {
        bool attackReady = PlanarVelocity.magnitude >= SpeedSettings.SmallEnemyKillSpeed;
        bool active = isBallMode && encounter != null && encounter.IsCombatActive && (attackReady || Time.time < boostFlashUntil);
        if (active && attackRing == null) BuildAssistanceVisuals();
        if (attackRing == null) return;
        attackRing.enabled = active && attackReady;
        Vector3 center = transform.position;
        center.y = encounter.Placement.Floor.bounds.max.y + .06f;
        for (int i = 0; i < attackRing.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / attackRing.positionCount;
            attackRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * AttackHaloRadius);
        }
        Color color = Time.time < boostFlashUntil ? new Color(1f, .85f, .15f) : new Color(.2f, 1f, 1f);
        attackRing.startColor = attackRing.endColor = color;
    }
    private void BuildAssistanceVisuals()
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        assistanceMaterial = new Material(shader) { name = "Pinball assistance glow" };
        var ring = new GameObject("Attack halo", typeof(LineRenderer)); ring.transform.SetParent(transform, false);
        attackRing = ring.GetComponent<LineRenderer>(); attackRing.sharedMaterial = assistanceMaterial;
        attackRing.useWorldSpace = true; attackRing.loop = true; attackRing.positionCount = 48; attackRing.widthMultiplier = BallRadius * .12f;
    }
    private void PlayBoostSound()
    {
        if (wallSoundVolume <= 0) return;
        if (boostAudio == null)
        {
            boostAudio = gameObject.AddComponent<AudioSource>(); boostAudio.playOnAwake = false; boostAudio.spatialBlend = 0;
            const int samples = 3528;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / 44100f, decay = 1f - (float)i / samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * (320f * t - 1400f * t * t)) * decay * decay;
            }
            boostClip = AudioClip.Create("Wall boost", samples, 1, 44100, false); boostClip.SetData(data, 0);
        }
        boostAudio.PlayOneShot(boostClip, wallSoundVolume);
    }

    private void OnDestroy()
    {
        if (assistanceMaterial != null) Destroy(assistanceMaterial);
        if (boostClip != null) Destroy(boostClip);
        if (frictionlessMaterial != null)
            Destroy(frictionlessMaterial);
    }
}
