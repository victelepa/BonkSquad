using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Wanders independently of the player; contact response comes from the wall material.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class EnemyBumper : MonoBehaviour
{
    private static readonly List<EnemyBumper> activeEnemies = new List<EnemyBumper>();
    public static int ActiveCount { get; private set; }
    public static IReadOnlyList<EnemyBumper> ActiveEnemies => activeEnemies;
    public bool IsDefeated => isDefeated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveCount()
    {
        ActiveCount = 0;
        activeEnemies.Clear();
    }

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
    private Vector3 knockbackVelocity;
    private float nextDirectionChange;
    private float avoidUntil;
    private bool isDefeated;
    private BonkRobotController robot;
    private BonkGameTuning tuning;
    private Renderer[] renderers;
    private MaterialPropertyBlock feedbackProperties;

    private void Awake()
    {
        feedbackProperties = new MaterialPropertyBlock();
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        robot = FindAnyObjectByType<BonkRobotController>();
        tuning = robot != null ? robot.Tuning : BonkGameTuning.CreateRuntimeDefaults();
        moveSpeed = destroyedByBall ? tuning.chaserSpeed : tuning.heavyChaserSpeed;
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        ActiveCount++;
        if (!activeEnemies.Contains(this))
        {
            activeEnemies.Add(this);
        }
        ChooseDirection();
    }

    private void OnDisable()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
        activeEnemies.Remove(this);
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

        robot = FindClosestVulnerablePlayer();

        if (Time.time >= avoidUntil &&
            robot != null && !robot.IsBallForm && robot.StandingTransform != null)
        {
            Vector3 toPlayer = Vector3.ProjectOnPlane(
                robot.StandingTransform.position - body.position,
                Vector3.up);
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                direction = toPlayer.normalized;
            }
        }

        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            direction = knockbackVelocity.normalized;
            moveSpeed = knockbackVelocity.magnitude;
            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity, Vector3.zero, 12f * Time.fixedDeltaTime);
        }
        else
        {
            moveSpeed = destroyedByBall ? tuning.chaserSpeed : tuning.heavyChaserSpeed;
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
            avoidUntil = Time.time + 0.5f;
            return;
        }

        body.MovePosition(body.position + direction * distance);
    }

    private BonkRobotController FindClosestVulnerablePlayer()
    {
        BonkRobotController closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (BonkRobotController player in BonkRobotController.Players)
        {
            if (player == null || player.IsBallForm || player.StandingTransform == null)
            {
                continue;
            }

            float distance = (player.StandingTransform.position - transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = player;
            }
        }
        return closest;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDefeated || collision.rigidbody == null)
        {
            return;
        }

        if (collision.rigidbody.TryGetComponent(out BouncingBallController ball) &&
            ball.isActiveAndEnabled)
        {
            float impactSpeed = Vector3.ProjectOnPlane(
                collision.relativeVelocity, Vector3.up).magnitude;
            float requiredSpeed = destroyedByBall
                ? tuning.smallEnemyKillSpeed
                : tuning.heavyEnemyKillSpeed;

            Vector3 awayFromBall = Vector3.ProjectOnPlane(
                transform.position - ball.transform.position,
                Vector3.up).normalized;
            if (awayFromBall.sqrMagnitude < 0.01f)
            {
                awayFromBall = -ball.PlanarVelocity.normalized;
            }

            if (impactSpeed >= requiredSpeed)
            {
                isDefeated = true;
                knockbackVelocity = awayFromBall * tuning.strongHitKnockback;
                Destroy(gameObject);
            }
            else
            {
                knockbackVelocity = awayFromBall * tuning.weakHitKnockback;
                StopAllCoroutines();
                StartCoroutine(ShowWeakHitFeedback());
            }
        }
    }

    private IEnumerator ShowWeakHitFeedback()
    {
        SetFeedbackColor(new Color(0.35f, 0.35f, 0.35f));
        yield return new WaitForSeconds(0.15f);
        ClearFeedbackColor();
    }

    private void SetFeedbackColor(Color color)
    {
        foreach (Renderer enemyRenderer in renderers)
        {
            enemyRenderer.GetPropertyBlock(feedbackProperties);
            feedbackProperties.SetColor("_BaseColor", color);
            feedbackProperties.SetColor("_Color", color);
            enemyRenderer.SetPropertyBlock(feedbackProperties);
        }
    }

    private void ClearFeedbackColor()
    {
        foreach (Renderer enemyRenderer in renderers)
        {
            enemyRenderer.SetPropertyBlock(null);
        }
    }
}
