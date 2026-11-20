using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Robot Steering")]
    [Min(0f)] [SerializeField] private float turnSpeed = 14f;
    [Min(0f)] [SerializeField] private float groundAcceleration = 28f;
    [Min(0f)] [SerializeField] private float groundDeceleration = 20f;
    [Header("High-Speed Drift")]
    [FormerlySerializedAs("driftTurnSpeed")]
    [Range(0f, 720f)] [SerializeField] private float momentumTurnSpeed = 240f;
    [Min(0f)] [SerializeField] private float driftHoldSeconds = .4f;
    [Min(0f)] [SerializeField] private float driftHoldDeceleration = 1.5f;
    [Min(0f)] [SerializeField] private float driftFacingTurnSpeed = 540f;
    [Range(0f, 180f)] [SerializeField] private float driftMaxSlipAngle = 55f;

    private CharacterController controller;
    private Transform cameraTransform;
    private BonkRobotController speedSystem;
    private float verticalVelocity;
    private Vector3 planarVelocity;
    private bool carryingMomentum;
    private float driftHoldRemaining;
    private bool resolvedSideHit;
    private bool controlLocked;

    public Vector3 CurrentPlanarVelocity => planarVelocity;
    public PlayerSpeedSettings SpeedSettings => speedSystem != null ? speedSystem.SpeedSettings : PlayerSpeedSettings.Default;

    public void Configure(BonkRobotController owner) => speedSystem = owner;

    public void StopForSnare()
    {
        planarVelocity = Vector3.zero;
        carryingMomentum = false;
        driftHoldRemaining = 0f;
    }

    public void ApplyKnockback(Vector3 direction, float strength)
    {
        planarVelocity = PlayerMovementMath.KnockbackVelocity(planarVelocity, direction, strength, SpeedSettings.MaximumSpeed);
        carryingMomentum = true;
        driftHoldRemaining = 0f;
    }

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;
        if (!locked) return;
        planarVelocity = Vector3.zero;
        carryingMomentum = false;
        driftHoldRemaining = 0f;
        resolvedSideHit = false;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = wasEnabled;
    }

    public void EnterStandingMode(Vector3 inheritedVelocity)
    {
        planarVelocity = PlayerMovementMath.Planar(inheritedVelocity);
        // Preserve inherited speed even when below base; passive decay never boosts it.
        carryingMomentum = planarVelocity.sqrMagnitude > 0f;
        driftHoldRemaining = planarVelocity.magnitude > SpeedSettings.RobotBaseSpeed
            ? driftHoldSeconds : 0f;
        verticalVelocity = -2f;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
        if (speedSystem == null) speedSystem = GetComponentInParent<BonkRobotController>();
    }

    private void Update()
    {
        if (speedSystem != null && (speedSystem.IsEncounterLocked || speedSystem.IsSnared)) return;
        if (controlLocked && (speedSystem == null || !speedSystem.IsKnockedBack))
        {
            // Gravity continues, but no horizontal movement or facing change is allowed.
            planarVelocity = Vector3.zero;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return;
        }
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        Vector3 inputDirection = speedSystem != null && speedSystem.IsKnockedBack
            ? Vector3.zero : PlayerMovementMath.ReadInputDirection(cameraTransform);
        float speed = planarVelocity.magnitude;
        if (speed > SpeedSettings.RobotBaseSpeed) carryingMomentum = true;
        bool wasCarryingMomentum = carryingMomentum;
        if (carryingMomentum)
        {
            Vector3 direction = speed > 0f ? planarVelocity / speed : transform.forward;
            if (inputDirection.sqrMagnitude > 0f)
                direction = Vector3.RotateTowards(direction, inputDirection,
                    momentumTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
            float holdTime = Mathf.Min(Time.deltaTime, driftHoldRemaining);
            driftHoldRemaining -= holdTime;
            if (holdTime > 0f)
                speed = Mathf.Max(SpeedSettings.RobotBaseSpeed,
                    speed - driftHoldDeceleration * holdTime);
            speed = SpeedSettings.DecelerateRobot(speed, Time.deltaTime - holdTime);
            planarVelocity = direction * speed;
            // Keep coasting at base speed without input. Active input can return to
            // normal robot locomotion once inherited excess speed is fully gone.
            if (speed <= SpeedSettings.RobotBaseSpeed && inputDirection.sqrMagnitude > 0f)
                carryingMomentum = false;
        }
        else
        {
            driftHoldRemaining = 0f;
            planarVelocity = Vector3.MoveTowards(planarVelocity,
                inputDirection * SpeedSettings.RobotBaseSpeed,
                (inputDirection.sqrMagnitude > 0f ? groundAcceleration : groundDeceleration) * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;
        resolvedSideHit = false;
        // OnControllerColliderHit runs inside Move and updates planarVelocity.
        // Do not replace the reflected velocity with controller.velocity afterward.
        controller.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        Vector3 facing = planarVelocity.sqrMagnitude > 0f ? planarVelocity.normalized : inputDirection;
        if (wasCarryingMomentum && speed > SpeedSettings.RobotBaseSpeed &&
            inputDirection.sqrMagnitude > 0f)
        {
            // Face into the turn while the inherited velocity follows behind.
            Vector3 driftFacing = Vector3.RotateTowards(facing, inputDirection,
                driftMaxSlipAngle * Mathf.Deg2Rad, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(driftFacing), driftFacingTurnSpeed * Time.deltaTime);
        }
        else if (facing.sqrMagnitude > 0f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(facing), turnSpeed * Time.deltaTime);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (controlLocked && (speedSystem == null || !speedSystem.IsKnockedBack)) return;
        hit.collider.GetComponentInParent<EnemyBumper>()?.ContactPlayer(GetComponentInParent<PlayerHealth>());
        // Mesh-floor contacts can have small sideways normals at triangle edges.
        // Walkable ground must not reflect momentum as if the robot hit a wall.
        if (hit.normal.y > 0f && hit.normal.y >= Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad)) return;
        Vector3 normal = PlayerMovementMath.Planar(hit.normal);
        if (normal.sqrMagnitude < 0.0001f || Vector3.Dot(planarVelocity, normal) >= 0f) return;
        // CharacterController can report several contacts during one Move. Resolve
        // only the first blocking surface to avoid reflecting the same impact twice.
        if (resolvedSideHit) return;
        resolvedSideHit = true;
        planarVelocity = PlayerMovementMath.ReflectPlanar(planarVelocity, normal);
        carryingMomentum = true;
    }
}
