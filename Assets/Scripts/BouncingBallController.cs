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

    [Header("Aim Assistance")]
    [Tooltip("Maximum angle in which nearby enemies gently attract the trajectory.")]
    [Range(0f, 30f)]
    [SerializeField] private float aimAssistAngle = 15f;
    [Min(0f)]
    [SerializeField] private float aimAssistRange = 14f;
    [Min(0f)]
    [SerializeField] private float aimAssistTurnSpeed = 24f;

    [Header("Speed Feedback")]
    [SerializeField] private Color lowSpeedColor = new Color(0.15f, 0.45f, 1f);
    [SerializeField] private Color killSpeedColor = new Color(1f, 0.75f, 0.1f);
    [SerializeField] private Color heavySpeedColor = new Color(1f, 0.15f, 0.05f);

    private Rigidbody rb;
    private Transform cameraTransform;
    private Vector3 lastMovingVelocity;
    private float lastMovingVelocityTime = -10f;
    private BonkGameTuning tuning;
    private Renderer ballRenderer;
    private MaterialPropertyBlock materialProperties;
    private BonkRobotController owner;

    public float Speed => PlanarVelocity.magnitude;
    public Vector3 LastPlanarVelocity => lastMovingVelocity;
    public float SmallKillSpeed => tuning != null ? tuning.smallEnemyKillSpeed : 9f;
    public float HeavyKillSpeed => tuning != null ? tuning.heavyEnemyKillSpeed : 16f;

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
        ballRenderer = GetComponent<Renderer>();
        materialProperties = new MaterialPropertyBlock();
    }

    public void Configure(
        BonkGameTuning gameTuning,
        BonkRobotController playerOwner = null)
    {
        tuning = gameTuning;
        owner = playerOwner;
        if (owner != null)
        {
            cameraTransform = owner.CameraTransform;
            if (owner.PlayerNumber == 2)
            {
                lowSpeedColor = new Color(0.7f, 0.25f, 1f);
            }
        }
        acceleration = tuning.ballAcceleration;
        steeringAcceleration = tuning.ballSteeringAcceleration;
        brakingAcceleration = tuning.ballBrakingAcceleration;
        maxSpeed = tuning.ballNormalSpeedCap;
    }

    public void AddSpeedBoost(float addedSpeed)
    {
        Vector3 planar = PlanarVelocity;
        Vector3 direction = planar.sqrMagnitude > 0.01f
            ? planar.normalized
            : transform.forward;
        float boostedSpeed = Mathf.Min(
            tuning != null ? tuning.ballAbsoluteSpeedCap : maxSpeed,
            planar.magnitude + Mathf.Max(0f, addedSpeed));
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = direction * boostedSpeed + Vector3.up * velocity.y;
    }

    public void ApplyBoostedRicochet(
        Vector3 incomingVelocity,
        Vector3 surfaceNormal,
        float addedSpeed)
    {
        Vector3 incoming = Vector3.ProjectOnPlane(incomingVelocity, Vector3.up);
        Vector3 planarNormal = Vector3.ProjectOnPlane(
            surfaceNormal, Vector3.up);
        if (incoming.sqrMagnitude < 0.01f || planarNormal.sqrMagnitude < 0.01f)
        {
            AddSpeedBoost(addedSpeed);
            return;
        }

        // Wall boosts are a horizontal mirror-angle ricochet. Ignoring the
        // vertical contact component prevents seams and bevels launching the ball.
        Vector3 reflected = Vector3.Reflect(
            incoming.normalized, planarNormal.normalized);
        float absoluteCap = tuning != null ? tuning.ballAbsoluteSpeedCap : maxSpeed;
        float targetSpeed = Mathf.Min(
            absoluteCap,
            incoming.magnitude + Mathf.Max(0f, addedSpeed));
        rb.linearVelocity = reflected.normalized * targetSpeed;
        rb.angularVelocity = Vector3.zero;
        lastMovingVelocity = rb.linearVelocity;
        lastMovingVelocityTime = Time.time;
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
        ApplyAimAssist();
        ApplyNaturalSpeedLoss();
        LimitSpeed();
        UpdateSpeedFeedback();
    }

    private void ApplyNaturalSpeedLoss()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
        float loss = tuning != null ? tuning.ballNaturalSpeedLoss : 0.75f;
        if (planar.magnitude > maxSpeed)
        {
            loss = tuning != null ? tuning.overcapSpeedLoss : 3f;
        }

        float nextSpeed = Mathf.MoveTowards(
            planar.magnitude, 0f, loss * Time.fixedDeltaTime);
        Vector3 nextPlanar = planar.sqrMagnitude > 0.001f
            ? planar.normalized * nextSpeed
            : Vector3.zero;
        rb.linearVelocity = new Vector3(nextPlanar.x, velocity.y, nextPlanar.z);
    }

    private void ApplyLimitedControl()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        bool playerTwo = owner != null && owner.PlayerNumber == 2;
        Vector2 input = playerTwo
            ? new Vector2(
                (Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) -
                (Keyboard.current.leftArrowKey.isPressed ? 1f : 0f),
                (Keyboard.current.upArrowKey.isPressed ? 1f : 0f) -
                (Keyboard.current.downArrowKey.isPressed ? 1f : 0f))
            : new Vector2(
                (Keyboard.current.dKey.isPressed ? 1f : 0f) -
                (Keyboard.current.aKey.isPressed ? 1f : 0f),
                (Keyboard.current.wKey.isPressed ? 1f : 0f) -
                (Keyboard.current.sKey.isPressed ? 1f : 0f));

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

        if (alignment > 0f && speed < maxSpeed)
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

        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed));
        float steeringMultiplier = Mathf.Lerp(2.4f, 0.85f, speedRatio);
        rb.AddForce(
            steering * steeringAcceleration * steeringMultiplier,
            ForceMode.Acceleration);
    }

    private void ApplyAimAssist()
    {
        Vector3 velocity = PlanarVelocity;
        float speed = velocity.magnitude;
        if (speed < 3f || EnemyBumper.ActiveCount == 0)
        {
            return;
        }

        Vector3 travelDirection = velocity / speed;
        float minimumDot = Mathf.Cos(aimAssistAngle * Mathf.Deg2Rad);
        EnemyBumper bestTarget = null;
        Vector3 bestDirection = Vector3.zero;
        float bestScore = float.NegativeInfinity;

        foreach (EnemyBumper enemy in EnemyBumper.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDefeated)
            {
                continue;
            }

            Vector3 offset = Vector3.ProjectOnPlane(
                enemy.transform.position - transform.position, Vector3.up);
            float distance = offset.magnitude;
            if (distance < 0.5f || distance > aimAssistRange)
            {
                continue;
            }

            Vector3 direction = offset / distance;
            float alignment = Vector3.Dot(travelDirection, direction);
            if (alignment < minimumDot)
            {
                continue;
            }

            if (Physics.Linecast(
                    transform.position,
                    enemy.transform.position,
                    out RaycastHit hit,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<EnemyBumper>() != enemy)
            {
                continue;
            }

            float score = alignment * 2f - distance / aimAssistRange;
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
                bestDirection = direction;
            }
        }

        if (bestTarget == null)
        {
            return;
        }

        float speedRatio = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed));
        float turnRate = aimAssistTurnSpeed * Mathf.Lerp(1f, 0.45f, speedRatio);
        Vector3 assistedDirection = Vector3.RotateTowards(
            travelDirection,
            bestDirection,
            turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f);
        rb.linearVelocity = assistedDirection.normalized * speed +
            Vector3.up * rb.linearVelocity.y;
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        float absoluteCap = tuning != null ? tuning.ballAbsoluteSpeedCap : maxSpeed;
        if (horizontalVelocity.sqrMagnitude > absoluteCap * absoluteCap)
        {
            horizontalVelocity =
                horizontalVelocity.normalized * absoluteCap;

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );
        }
    }

    private void UpdateSpeedFeedback()
    {
        if (ballRenderer == null)
        {
            return;
        }

        Color color = Speed >= HeavyKillSpeed
            ? heavySpeedColor
            : Speed >= SmallKillSpeed
                ? killSpeedColor
                : lowSpeedColor;
        ballRenderer.GetPropertyBlock(materialProperties);
        materialProperties.SetColor("_BaseColor", color);
        materialProperties.SetColor("_Color", color);
        ballRenderer.SetPropertyBlock(materialProperties);
    }
}
