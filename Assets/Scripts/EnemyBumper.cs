using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum EnemyType { Large = 0, Small = 1 }

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class EnemyBumper : MonoBehaviour
{
    private static readonly List<EnemyBumper> activeEnemies = new List<EnemyBumper>();
    public static int ActiveCount { get; private set; }
    public static IReadOnlyList<EnemyBumper> ActiveEnemies => activeEnemies;

    [Header("Enemy Identity")]
    [Tooltip("Small enemies are faster and use the lower instant-kill threshold. Large enemies have more health and require a faster impact.")]
    [FormerlySerializedAs("destroyedByBall")]
    [SerializeField] private EnemyType enemyType = EnemyType.Small;

    [Header("Patrol Route")]
    [Tooltip("Optional reusable scene route. When empty, Patrol Offsets define a route relative to the starting position.")]
    [SerializeField] private EnemyPatrolPath patrolPath;
    [SerializeField] private Vector3[] patrolOffsets =
    {
        new Vector3(-3f, 0f, -2f), new Vector3(3f, 0f, -2f),
        new Vector3(3f, 0f, 2f), new Vector3(-3f, 0f, 2f)
    };
    [Min(0f)] [SerializeField] private float moveSpeed = 2.5f;
    [Min(0.05f)] [SerializeField] private float waypointTolerance = 0.35f;

    [Header("Melee Attack")]
    [Min(0f)] [SerializeField] private float attackDetectionRange = 2.25f;
    [Tooltip("The player must remain in range for this long before taking damage.")]
    [Min(0f)] [SerializeField] private float attackWindup = 1.1f;
    [Min(1)] [SerializeField] private int attackDamage = 1;
    [Min(0f)] [SerializeField] private float attackCooldown = 0.8f;

    [Header("Ball Damage")]
    [Tooltip("Zero uses 30 health for small enemies and 80 for large enemies.")]
    [Min(0)] [SerializeField] private int maximumHealth;
    [Tooltip("Zero uses the global small/large instant-kill speed.")]
    [Min(0f)] [SerializeField] private float instantKillSpeed;
    [Tooltip("Below the threshold, impact speed is multiplied by this value and removed from health.")]
    [Min(0f)] [SerializeField] private float damagePerSpeed = 3f;
    [Tooltip("Zero uses the global enemy defeat speed reward.")]
    [Min(0f)] [SerializeField] private float defeatSpeedReward;

    [Header("Arena Bounds")]
    [SerializeField] private Vector2 arenaMin = new Vector2(-22f, -22f);
    [SerializeField] private Vector2 arenaMax = new Vector2(22f, 22f);

    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private Rigidbody body;
    private CapsuleCollider capsule;
    private BonkGameTuning tuning;
    private Renderer[] renderers;
    private MaterialPropertyBlock feedbackProperties;
    private Vector3 spawnPosition;
    private Vector3 direction;
    private Vector3 knockbackVelocity;
    private int waypointIndex;
    private int currentHealth;
    private bool isDefeated;
    private BonkRobotController attackTarget;
    private float attackTimer;
    private float nextAttackTime;

    public bool IsDefeated => isDefeated;
    public EnemyType Type => enemyType;
    public int CurrentHealth => currentHealth;
    public int MaximumHealth => maximumHealth > 0
        ? maximumHealth : enemyType == EnemyType.Small ? 30 : 80;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveCount()
    {
        ActiveCount = 0;
        activeEnemies.Clear();
    }

    private void Awake()
    {
        feedbackProperties = new MaterialPropertyBlock();
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        BonkRobotController player = FindAnyObjectByType<BonkRobotController>();
        tuning = player != null ? player.Tuning : BonkGameTuning.CreateRuntimeDefaults();
        renderers = GetComponentsInChildren<Renderer>();
        spawnPosition = transform.position;
        currentHealth = MaximumHealth;
    }

    public void Configure(EnemyType type, EnemyPatrolPath route)
    {
        enemyType = type;
        patrolPath = route;
        currentHealth = MaximumHealth;
    }

    private void OnEnable()
    {
        ActiveCount++;
        if (!activeEnemies.Contains(this)) activeEnemies.Add(this);
    }

    private void OnDisable()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
        activeEnemies.Remove(this);
    }

    private void FixedUpdate()
    {
        if (isDefeated) return;
        UpdateMeleeAttack();

        float activeSpeed = moveSpeed;
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            direction = knockbackVelocity.normalized;
            activeSpeed = knockbackVelocity.magnitude;
            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity, Vector3.zero, 12f * Time.fixedDeltaTime);
        }
        else if (!TryChoosePatrolDirection(out direction))
        {
            return;
        }
        MoveInDirection(direction, activeSpeed);
    }

    private bool TryChoosePatrolDirection(out Vector3 patrolDirection)
    {
        patrolDirection = Vector3.zero;
        int pointCount = patrolPath != null
            ? patrolPath.PointCount : patrolOffsets != null ? patrolOffsets.Length : 0;
        if (pointCount == 0) return false;

        waypointIndex = Mathf.Clamp(waypointIndex, 0, pointCount - 1);
        Vector3 target = GetPatrolPoint(waypointIndex);
        Vector3 offset = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
        if (offset.magnitude <= waypointTolerance)
        {
            waypointIndex = (waypointIndex + 1) % pointCount;
            offset = Vector3.ProjectOnPlane(
                GetPatrolPoint(waypointIndex) - body.position, Vector3.up);
        }
        patrolDirection = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector3.zero;
        return patrolDirection.sqrMagnitude > 0f;
    }

    private Vector3 GetPatrolPoint(int index)
    {
        return patrolPath != null
            ? patrolPath.GetPoint(index) : spawnPosition + patrolOffsets[index];
    }

    private void MoveInDirection(Vector3 moveDirection, float speed)
    {
        float distance = Mathf.Max(0f, speed) * Time.fixedDeltaTime;
        Vector3 destination = body.position + moveDirection * distance;
        destination.x = Mathf.Clamp(destination.x, arenaMin.x, arenaMax.x);
        destination.z = Mathf.Clamp(destination.z, arenaMin.y, arenaMax.y);

        Vector3 scale = transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfSegment = Mathf.Max(
            0f, capsule.height * Mathf.Abs(scale.y) * 0.5f - radius);
        Vector3 center = transform.TransformPoint(capsule.center) + Vector3.up * 0.05f;
        int count = Physics.CapsuleCastNonAlloc(
            center + Vector3.up * halfSegment, center - Vector3.up * halfSegment,
            radius, moveDirection, obstacleHits, distance + 0.1f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (obstacleHits[i].collider.attachedRigidbody == body) continue;
            int points = patrolPath != null
                ? patrolPath.PointCount : patrolOffsets != null ? patrolOffsets.Length : 0;
            if (points > 0) waypointIndex = (waypointIndex + 1) % points;
            return;
        }
        body.MovePosition(destination);
    }

    private void UpdateMeleeAttack()
    {
        BonkRobotController closest = FindClosestVulnerablePlayer(attackDetectionRange);
        if (closest == null)
        {
            attackTarget = null;
            attackTimer = 0f;
            return;
        }
        if (closest != attackTarget)
        {
            attackTarget = closest;
            attackTimer = 0f;
        }
        if (Time.time < nextAttackTime) return;

        attackTimer += Time.fixedDeltaTime;
        if (attackTimer < attackWindup) return;
        attackTarget.TryTakeDamage(transform.position, attackDamage);
        attackTimer = 0f;
        nextAttackTime = Time.time + attackCooldown;
    }

    private BonkRobotController FindClosestVulnerablePlayer(float maximumDistance)
    {
        BonkRobotController closest = null;
        float closestDistance = maximumDistance * maximumDistance;
        foreach (BonkRobotController player in BonkRobotController.Players)
        {
            if (player == null || player.IsBallForm || player.StandingTransform == null) continue;
            float distance =
                (player.StandingTransform.position - transform.position).sqrMagnitude;
            if (distance <= closestDistance)
            {
                closestDistance = distance;
                closest = player;
            }
        }
        return closest;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDefeated || collision.rigidbody == null ||
            !collision.rigidbody.TryGetComponent(out BouncingBallController ball) ||
            !ball.isActiveAndEnabled) return;

        float impactSpeed = Vector3.ProjectOnPlane(
            collision.relativeVelocity, Vector3.up).magnitude;
        float requiredSpeed = instantKillSpeed > 0f
            ? instantKillSpeed
            : enemyType == EnemyType.Small
                ? tuning.smallEnemyKillSpeed : tuning.heavyEnemyKillSpeed;
        Vector3 awayFromBall = Vector3.ProjectOnPlane(
            transform.position - ball.transform.position, Vector3.up).normalized;
        if (awayFromBall.sqrMagnitude < 0.01f)
            awayFromBall = -ball.PlanarVelocity.normalized;

        if (impactSpeed >= requiredSpeed)
        {
            Defeat(ball, awayFromBall);
            return;
        }

        int impactDamage = Mathf.Max(1, Mathf.RoundToInt(impactSpeed * damagePerSpeed));
        currentHealth = Mathf.Max(0, currentHealth - impactDamage);
        knockbackVelocity = awayFromBall * tuning.weakHitKnockback;
        if (enemyType == EnemyType.Large)
            ball.AddSpeedBoost(tuning.heavyWeakHitSpeedReward);

        if (currentHealth <= 0) Defeat(ball, awayFromBall);
        else
        {
            StopAllCoroutines();
            StartCoroutine(ShowDamageFeedback());
        }
    }

    private void Defeat(BouncingBallController ball, Vector3 awayFromBall)
    {
        isDefeated = true;
        knockbackVelocity = awayFromBall * tuning.strongHitKnockback;
        ball.AddSpeedBoost(defeatSpeedReward > 0f
            ? defeatSpeedReward : tuning.enemyDefeatSpeedReward);
        Destroy(gameObject);
    }

    private IEnumerator ShowDamageFeedback()
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
            enemyRenderer.SetPropertyBlock(null);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackDetectionRange);
        if (patrolPath != null || patrolOffsets == null || patrolOffsets.Length < 2) return;
        Gizmos.color = Color.yellow;
        Vector3 origin = Application.isPlaying ? spawnPosition : transform.position;
        for (int i = 0; i < patrolOffsets.Length; i++)
        {
            Vector3 from = origin + patrolOffsets[i];
            Vector3 to = origin + patrolOffsets[(i + 1) % patrolOffsets.Length];
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(from, 0.2f);
        }
    }
}
