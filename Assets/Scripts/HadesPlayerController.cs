using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class HadesPlayerController : MonoBehaviour
{
    [Header("Normal Form Movement")]
    [Tooltip("Maximum movement speed when not carrying ball momentum.")]
    [Min(0f)]
    [SerializeField] float moveSpeed = 6f;
    [Tooltip("How quickly the model turns toward its actual travel direction.")]
    [Min(0f)]
    [SerializeField] float turnSpeed = 14f;
    [Tooltip("Acceleration while the player is giving movement input.")]
    [Min(0f)]
    [SerializeField] float groundAcceleration = 28f;
    [Tooltip("Deceleration after releasing movement input outside drift.")]
    [Min(0f)]
    [SerializeField] float groundDeceleration = 20f;

    [Header("Drift")]
    [Tooltip("How quickly input can bend the inherited momentum direction, in degrees per second.")]
    [Range(0f, 360f)]
    [SerializeField] float driftTurnSpeed = 120f;
    [Tooltip("Speed lost per second while carrying momentum from ball form.")]
    [Min(0f)]
    [SerializeField] float driftSpeedLoss = 2f;

    CharacterController controller;
    Transform cameraTransform;
    float verticalVelocity;
    Vector3 planarVelocity;
    bool isDrifting;
    BonkRobotController robot;

    public Vector3 CurrentPlanarVelocity { get; private set; }

    public void Configure(BonkRobotController owner)
    {
        robot = owner;
        moveSpeed = robot.Tuning.humanMoveSpeed;
        cameraTransform = robot.CameraTransform;
    }

    public void ApplyKnockback(Vector3 velocity)
    {
        planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        CurrentPlanarVelocity = planarVelocity;
        isDrifting = true;
    }

    public void EnterStandingMode(Vector3 inheritedVelocity)
    {
        planarVelocity = Vector3.ProjectOnPlane(inheritedVelocity, Vector3.up);
        CurrentPlanarVelocity = planarVelocity;
        isDrifting = planarVelocity.sqrMagnitude > 0.0625f;
        verticalVelocity = -2f;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (robot == null)
        {
            robot = GetComponentInParent<BonkRobotController>();
        }

        if (robot != null && hit.collider.TryGetComponent(out EnemyBumper enemy))
        {
            robot.TryTakeDamage(enemy.transform.position);
        }
    }

    void Update()
    {
        Vector2 input = Vector2.zero;
        
        if (Keyboard.current != null)
        {
            bool playerTwo = robot != null && robot.PlayerNumber == 2;
            input.x = playerTwo
                ? (Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) -
                  (Keyboard.current.leftArrowKey.isPressed ? 1f : 0f)
                : (Keyboard.current.dKey.isPressed ? 1f : 0f) -
                  (Keyboard.current.aKey.isPressed ? 1f : 0f);
            input.y = playerTwo
                ? (Keyboard.current.upArrowKey.isPressed ? 1f : 0f) -
                  (Keyboard.current.downArrowKey.isPressed ? 1f : 0f)
                : (Keyboard.current.wKey.isPressed ? 1f : 0f) -
                  (Keyboard.current.sKey.isPressed ? 1f : 0f);
        }
        
        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 inputDirection = forward * input.y + right * input.x;
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        float speed = planarVelocity.magnitude;
        if (isDrifting)
        {
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 driftDirection = Vector3.RotateTowards(
                    planarVelocity.normalized,
                    inputDirection,
                    driftTurnSpeed * Mathf.Deg2Rad * Time.deltaTime,
                    0f);
                planarVelocity = driftDirection * speed;
            }

            speed = Mathf.MoveTowards(
                speed,
                0f,
                driftSpeedLoss * Time.deltaTime);
            planarVelocity = speed > 0.01f
                ? planarVelocity.normalized * speed
                : Vector3.zero;

            if (speed <= 0.25f)
            {
                isDrifting = false;
            }
            else if (speed <= moveSpeed + 0.05f &&
                     inputDirection.sqrMagnitude > 0.01f &&
                     Vector3.Dot(planarVelocity.normalized, inputDirection) > 0.95f)
            {
                isDrifting = false;
            }
        }
        else
        {
            Vector3 targetVelocity = inputDirection * moveSpeed;
            float acceleration = inputDirection.sqrMagnitude > 0.01f
                ? groundAcceleration
                : groundDeceleration;
            planarVelocity = Vector3.MoveTowards(
                planarVelocity,
                targetVelocity,
                acceleration * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;

        CollisionFlags collisionFlags = controller.Move(
            (planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        if ((collisionFlags & CollisionFlags.Sides) != 0)
        {
            Vector3 actualVelocity = controller.velocity;
            planarVelocity = new Vector3(actualVelocity.x, 0f, actualVelocity.z);
            if (planarVelocity.sqrMagnitude < 0.0625f)
            {
                isDrifting = false;
            }
        }

        CurrentPlanarVelocity = planarVelocity;

        Vector3 facingDirection = planarVelocity.sqrMagnitude > 0.01f
            ? planarVelocity.normalized
            : inputDirection;

        if (facingDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facingDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }
}
