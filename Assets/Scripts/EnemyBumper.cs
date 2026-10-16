using UnityEngine;

// Wanders independently of the player; contact response comes from the wall material.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class EnemyBumper : MonoBehaviour
{
    [Header("Ball Impact")]
    [Tooltip("Small enemies are defeated when hit by the player in ball form.")]
    [SerializeField] private bool destroyedByBall;

    [Header("Wandering")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 1f;
    [Tooltip("Seconds between random changes of direction.")]
    [SerializeField] private Vector2 directionChangeInterval = new Vector2(2f, 5f);
    [Tooltip("Allowed center positions on the X/Z plane, inside the arena walls.")]
    [SerializeField] private Vector2 arenaMin = new Vector2(-22f, -22f);
    [SerializeField] private Vector2 arenaMax = new Vector2(22f, 22f);

    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private Rigidbody body;
    private CapsuleCollider capsule;
    private Vector3 direction;
    private float nextDirectionChange;
    private bool isDefeated;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void OnEnable()
    {
        ChooseDirection();
    }

    private void ChooseDirection()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        nextDirectionChange = Time.time + Random.Range(
            Mathf.Max(0.1f, directionChangeInterval.x),
            Mathf.Max(0.1f, directionChangeInterval.x, directionChangeInterval.y));
    }

    private void FixedUpdate()
    {
        if (moveSpeed <= 0f)
        {
            return;
        }

        if (Time.time >= nextDirectionChange)
        {
            ChooseDirection();
        }

        float distance = moveSpeed * Time.fixedDeltaTime;
        Vector3 destination = body.position + direction * distance;
        if (destination.x < arenaMin.x || destination.x > arenaMax.x)
            direction.x = -direction.x;
        if (destination.z < arenaMin.y || destination.z > arenaMax.y)
            direction.z = -direction.z;

        // Raise the sweep slightly so the floor does not count as an obstacle.
        Vector3 scale = transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfSegment = Mathf.Max(0f, capsule.height * Mathf.Abs(scale.y) * 0.5f - radius);
        Vector3 center = transform.TransformPoint(capsule.center) + Vector3.up * 0.05f;
        int count = Physics.CapsuleCastNonAlloc(
            center + Vector3.up * halfSegment, center - Vector3.up * halfSegment,
            radius, direction, obstacleHits, distance + 0.15f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (obstacleHits[i].collider.attachedRigidbody == body)
                continue;

            ChooseDirection();
            return;
        }

        body.MovePosition(body.position + direction * distance);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!destroyedByBall || isDefeated || collision.rigidbody == null)
        {
            return;
        }

        if (collision.rigidbody.TryGetComponent(out BouncingBallController ball) &&
            ball.isActiveAndEnabled)
        {
            isDefeated = true;
            // Collision callbacks run after the solver has applied the wall-material
            // rebound. Defer removal to the end of the frame to preserve that response.
            Destroy(gameObject);
        }
    }
}
