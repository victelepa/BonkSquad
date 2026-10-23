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

    private Rigidbody rb;
    private Transform cameraTransform;
    private BonkRobotController speedSystem;

    private Vector3 simulationVelocity;
    private PhysicsMaterial frictionlessMaterial;

    private bool isBallMode;
    private float inputTime;
    private float matchingDirectionTime;

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

        resolvedEnemies.Clear();
        rb.WakeUp();
    }

    public void ExitBallMode()
    {
        isBallMode = false;
        AccelerationEfficiency01 = 0f;
        
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        simulationVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!isBallMode)
            return;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector3 velocity = PlanarVelocity;
        float speed = velocity.magnitude;

        Vector3 inputDirection =
            PlayerMovementMath.ReadInputDirection(cameraTransform);

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
            if (!resolvedEnemies.Add(enemy))
                return;

            if (enemy.TryDefeat(
                    simulationVelocity.magnitude,
                    SpeedSettings))
            {
                SetPlanarVelocity(simulationVelocity);
                return;
            }
        }
        
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = PlayerMovementMath.Planar(
                collision.GetContact(i).normal);
            
            if (normal.sqrMagnitude < 0.0001f)
                continue;

            if (Vector3.Dot(simulationVelocity, normal) < 0f)
            {
                simulationVelocity =
                    PlayerMovementMath.ReflectPlanar(
                        simulationVelocity,
                        normal);
            }
        }

        if (enemy != null || simulationVelocity.sqrMagnitude > 0f)
            SetPlanarVelocity(simulationVelocity);
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
            Destroy(frictionlessMaterial);
    }
}