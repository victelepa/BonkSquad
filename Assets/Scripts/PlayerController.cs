using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Robot Steering")]
    [Min(0f)] [SerializeField] private float turnSpeed = 14f;
    [Min(0f)] [SerializeField] private float groundAcceleration = 28f;
    [Min(0f)] [SerializeField] private float groundDeceleration = 20f;
    [FormerlySerializedAs("driftTurnSpeed")]
    [Range(0f, 360f)] [SerializeField] private float momentumTurnSpeed = 120f;

    private CharacterController controller;
    private Transform cameraTransform;
    private BonkRobotController speedSystem;
    private float verticalVelocity;
    private Vector3 planarVelocity;
    private bool carryingMomentum;
    private bool resolvedSideHit;
    private bool controlLocked;

    public Vector3 CurrentPlanarVelocity => planarVelocity;
    public PlayerSpeedSettings SpeedSettings => speedSystem != null ? speedSystem.SpeedSettings : PlayerSpeedSettings.Default;

    public void Configure(BonkRobotController owner) => speedSystem = owner;

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;
        if (!locked) return;
        planarVelocity = Vector3.zero;
        carryingMomentum = false;
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
        if (controlLocked)
        {
            // Gravity continues, but no horizontal movement or facing change is allowed.
            planarVelocity = Vector3.zero;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            return;
        }
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        Vector3 inputDirection = PlayerMovementMath.ReadInputDirection(cameraTransform);
        float speed = planarVelocity.magnitude;
        if (speed > SpeedSettings.RobotBaseSpeed) carryingMomentum = true;
        if (carryingMomentum)
        {
            Vector3 direction = speed > 0f ? planarVelocity / speed : transform.forward;
            if (inputDirection.sqrMagnitude > 0f)
                direction = Vector3.RotateTowards(direction, inputDirection,
                    momentumTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;
            speed = SpeedSettings.DecelerateRobot(speed, Time.deltaTime);
            planarVelocity = direction * speed;
            // Keep coasting at base speed without input. Active input can return to
            // normal robot locomotion once inherited excess speed is fully gone.
            if (speed <= SpeedSettings.RobotBaseSpeed && inputDirection.sqrMagnitude > 0f)
                carryingMomentum = false;
        }
        else
        {
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
        if (facing.sqrMagnitude > 0f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(facing), turnSpeed * Time.deltaTime);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (controlLocked) return;
        EnemyBumper enemy = hit.collider.GetComponentInParent<EnemyBumper>();
        if (enemy != null && enemy.TryDefeat(planarVelocity.magnitude, SpeedSettings)) return;
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
