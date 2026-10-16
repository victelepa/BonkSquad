using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BouncingBallController : MonoBehaviour
{
    [Header("Ball Movement")]
    [Tooltip("Acceleration when input points along the current travel direction.")]
    [Min(0f)]
    [SerializeField] private float acceleration = 10f;
    [Tooltip("Sideways acceleration available for steering. Keep this lower than acceleration for an inertial feel.")]
    [Min(0f)]
    [SerializeField] private float steeringAcceleration = 3f;
    [Tooltip("Deceleration strength when input points against travel direction.")]
    [Min(0f)]
    [SerializeField] private float brakingAcceleration = 4f;
    [Tooltip("Maximum horizontal speed in ball form.")]
    [Min(0f)]
    [SerializeField] private float maxSpeed = 18f;

    private Rigidbody rb;
    private Transform cameraTransform;
    private Vector3 lastMovingVelocity;
    private float lastMovingVelocityTime = -10f;

    public Vector3 PlanarVelocity
    {
        get
        {
            if (rb == null)
            {
                return Vector3.zero;
            }

            Vector3 velocity = rb.linearVelocity;
            Vector3 currentVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (currentVelocity.sqrMagnitude > 0.0625f)
            {
                return currentVelocity;
            }

            return Time.time - lastMovingVelocityTime <= 0.2f
                ? lastMovingVelocity
                : Vector3.zero;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    public void EnterBallMode(Vector3 inheritedVelocity)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        rb.linearVelocity = new Vector3(
            inheritedVelocity.x,
            0f,
            inheritedVelocity.z);
        lastMovingVelocity = rb.linearVelocity;
        lastMovingVelocityTime = Time.time;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
    }

    public void ExitBallMode()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (planarVelocity.sqrMagnitude > 0.0625f)
        {
            lastMovingVelocity = planarVelocity;
            lastMovingVelocityTime = Time.time;
        }

        ApplyLimitedControl();
        LimitSpeed();
    }

    private void ApplyLimitedControl()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        Vector2 input = new Vector2(
            (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f)
            - (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f),
            (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f)
            - (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f));

        if (input.sqrMagnitude < 0.01f)
        {
            return;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        Vector3 forward = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : Vector3.forward;
        Vector3 right = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
            : Vector3.right;
        Vector3 inputDirection = (forward * input.y + right * input.x).normalized;

        Vector3 velocity = PlanarVelocity;
        float speed = velocity.magnitude;

        if (speed < 0.25f)
        {
            rb.AddForce(inputDirection * acceleration * 0.5f, ForceMode.Acceleration);
            return;
        }

        Vector3 travelDirection = velocity / speed;
        float alignment = Vector3.Dot(inputDirection, travelDirection);
        Vector3 steering = inputDirection - travelDirection * alignment;

        if (alignment > 0f)
        {
            rb.AddForce(
                travelDirection * alignment * acceleration,
                ForceMode.Acceleration);
        }
        else if (alignment < 0f)
        {
            rb.AddForce(
                travelDirection * alignment * brakingAcceleration,
                ForceMode.Acceleration);
        }

        rb.AddForce(steering * steeringAcceleration, ForceMode.Acceleration);
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontalVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized * maxSpeed;

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
        }
    }
}
