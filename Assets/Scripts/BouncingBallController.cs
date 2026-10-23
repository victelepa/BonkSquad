using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BouncingBallController : MonoBehaviour
{
    [Header("Ball Movement")]
    [Tooltip("Speed gained per second while input is within the shared angle tolerance.")]
    [Min(0f)] [SerializeField] private float acceleration = 10f;

    private Rigidbody rb;
    private Transform cameraTransform;
    private BonkRobotController speedSystem;
    private Vector3 simulationVelocity;
    private PhysicsMaterial frictionlessMaterial;
    private readonly HashSet<EnemyBumper> resolvedEnemies = new HashSet<EnemyBumper>();

    public PlayerSpeedSettings SpeedSettings => speedSystem != null ? speedSystem.SpeedSettings : PlayerSpeedSettings.Default;
    public Vector3 PlanarVelocity => rb != null ? PlayerMovementMath.Planar(rb.linearVelocity) : Vector3.zero;

    private void Awake()
    {
        InitializeBody();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
        if (speedSystem == null) speedSystem = GetComponentInParent<BonkRobotController>();
    }

    private void InitializeBody()
    {
        if (rb != null) return;
        rb = GetComponent<Rigidbody>();
        // Horizontal speed is controlled explicitly: floor friction and damping must
        // not quietly change the speed used by the HUD or enemy impact rules.
        rb.linearDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        frictionlessMaterial = new PhysicsMaterial("Ball Speed System")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            // Horizontal rebounds are explicit; do not launch the ball off floors.
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
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
        simulationVelocity = PlayerMovementMath.Planar(inheritedVelocity);
        rb.linearVelocity = simulationVelocity;
        rb.angularVelocity = Vector3.zero;
        resolvedEnemies.Clear();
        rb.WakeUp();
    }

    public void ExitBallMode()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        simulationVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        Vector3 velocity = PlanarVelocity;
        float speed = velocity.magnitude;
        Vector3 travelDirection = speed > 0f
            ? velocity / speed
            : PlayerMovementMath.Planar(transform.forward).normalized;
        // At rest, the facing established in robot form is the initial direction.
        // Input never supplies a new direction, even at very low speed.
        Vector3 inputDirection = PlayerMovementMath.ReadInputDirection(cameraTransform);
        if (speed < SpeedSettings.MaximumSpeed && PlayerMovementMath.AllowsAcceleration(
            travelDirection, inputDirection, SpeedSettings.AccelerationAngleTolerance))
        {
            speed = Mathf.Min(SpeedSettings.MaximumSpeed, speed + acceleration * Time.fixedDeltaTime);
            velocity = travelDirection * speed;
        }
        SetPlanarVelocity(velocity);
        // Capture AFTER acceleration and BEFORE the physics solver changes contact
        // velocity. Never use a recent-history/stale velocity for a kill decision.
        simulationVelocity = velocity;
        resolvedEnemies.Clear();
    }

    private void SetPlanarVelocity(Vector3 velocity)
    {
        Vector3 current = rb.linearVelocity;
        rb.linearVelocity = new Vector3(velocity.x, current.y, velocity.z);
    }

    private void OnCollisionEnter(Collision collision) => ResolveCollision(collision);
    private void OnCollisionStay(Collision collision) => ResolveCollision(collision);

    private void ResolveCollision(Collision collision)
    {
        EnemyBumper enemy = collision.collider.GetComponentInParent<EnemyBumper>();
        if (enemy != null)
        {
            if (!resolvedEnemies.Add(enemy)) return;
            if (enemy.TryDefeat(simulationVelocity.magnitude, SpeedSettings))
            {
                // Ignore the solver's rebound on a defeated enemy. Killing changes
                // neither the incoming direction nor the speed.
                SetPlanarVelocity(simulationVelocity);
                return;
            }
        }

        // A wall or surviving enemy reflects direction without adding/removing speed.
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = PlayerMovementMath.Planar(collision.GetContact(i).normal);
            if (normal.sqrMagnitude < 0.0001f) continue; // floor/ceiling
            if (Vector3.Dot(simulationVelocity, normal) < 0f)
                simulationVelocity = PlayerMovementMath.ReflectPlanar(simulationVelocity, normal);
        }
        if (enemy != null || simulationVelocity.sqrMagnitude > 0f)
            SetPlanarVelocity(simulationVelocity);
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null) Destroy(frictionlessMaterial);
    }
}
