using UnityEngine;
using UnityEngine.Serialization;

public enum EnemyHitResult { Rejected, Damaged, Defeated }

// Size selects the shared player-speed threshold; hit points count qualifying impacts.
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class EnemyBumper : MonoBehaviour
{
    [Header("Enemy Size")]
    [Tooltip("Checked = small enemy, unchecked = large enemy. Only the ball can defeat enemies at the matching speed threshold.")]
    [FormerlySerializedAs("destroyedByBall")]
    [SerializeField] private bool isSmallEnemy;
    public bool IsSmallEnemy => isSmallEnemy;

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
    [Header("Combat")]
    [Min(1)] [SerializeField] private int maximumHitPoints = 1;
    [SerializeField] private bool contactDamage;
    [SerializeField] private bool showHealthBar;
    public int HitPoints { get; private set; }
    public int MaximumHitPoints => maximumHitPoints;
    public bool IsDefeated => isDefeated;
    public bool IsBasicMinion => contactDamage && maximumHitPoints == 1;
    public EnemyWaveController Encounter { get; private set; }
    public event System.Action<EnemyBumper> HealthChanged;
    public event System.Action<EnemyBumper> Defeated;
    private bool isDefeated;
    private float hitFlashUntil;

    public void ConfigureCombat(int hitPoints, bool small, float wanderingSpeed, bool damagesOnContact,
        Vector2 minimum, Vector2 maximum)
    {
        maximumHitPoints = Mathf.Max(1, hitPoints);
        HitPoints = maximumHitPoints;
        isSmallEnemy = small;
        moveSpeed = wanderingSpeed;
        contactDamage = damagesOnContact;
        showHealthBar = true;
        arenaMin = minimum;
        arenaMax = maximum;
    }

    public void Bind(EnemyWaveController encounter) => Encounter = encounter;

    public void ContactPlayer(PlayerHealth health)
    {
        if (!contactDamage || isDefeated || health == null || (Encounter != null && !Encounter.IsCombatActive)) return;
        BonkRobotController player = health.GetComponent<BonkRobotController>();
        if (player != null && player.IsBallForm) return; // The ball is safe from minion contact, not explosions.
        health.TryDamageFrom(transform.position);
    }

    private void OnCollisionStay(Collision collision)
    {
        // Ball contacts are handled after its speed-qualified hit has been resolved.
        if (collision.collider.GetComponentInParent<BouncingBallController>() == null)
            ContactPlayer(collision.collider.GetComponentInParent<PlayerHealth>());
    }

    private void OnGUI()
    {
        if (!showHealthBar || isDefeated || Camera.main == null) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * (capsule.height + .3f));
        if (screen.z <= 0f) return;
        Color old = GUI.color;
        float width = 52f / maximumHitPoints;
        for (int i = 0; i < maximumHitPoints; i++)
        {
            GUI.color = i < HitPoints ? (Time.time < hitFlashUntil ? Color.white : new Color(.3f, 1f, .5f)) : Color.gray;
            GUI.DrawTexture(new Rect(screen.x - 26 + i * width, Screen.height - screen.y, width - 2, 6), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }

    private void Awake()
    {
        HitPoints = Mathf.Max(1, maximumHitPoints);
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
        if (isDefeated || (Encounter != null && !Encounter.IsCombatActive) || moveSpeed <= 0f)
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
    
    // Compatibility wrapper for existing callers; true still means the enemy was destroyed.
    public bool TryDefeatFromBall(float incomingPlayerSpeed, PlayerSpeedSettings settings)
        => TryHitFromBall(incomingPlayerSpeed, settings) == EnemyHitResult.Defeated;

    public EnemyHitResult TryHitFromBall(float incomingPlayerSpeed, PlayerSpeedSettings settings, int damage = 1)
    {
        if (isDefeated) return EnemyHitResult.Defeated;
        if ((Encounter != null && !Encounter.IsCombatActive) ||
            !settings.CanKill(incomingPlayerSpeed, isSmallEnemy)) return EnemyHitResult.Rejected;
        HitPoints = Mathf.Max(0, HitPoints - Mathf.Max(1, damage));
        hitFlashUntil = Time.time + .18f;
        HealthChanged?.Invoke(this);
        if (HitPoints > 0) return EnemyHitResult.Damaged;
        isDefeated = true;
        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>()) enemyCollider.enabled = false;
        enabled = false;
        Defeated?.Invoke(this);
        Destroy(gameObject);
        return EnemyHitResult.Defeated;
    }
}
