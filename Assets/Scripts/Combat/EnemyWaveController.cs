using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WaveFailureReason { None, HealthDepleted, Timeout, Placement }

public struct WaveResultSnapshot
{
    public WaveFailureReason reason;
    public int enemiesRemaining;
    public float elapsedSeconds, health;
}

public enum EnemyWaveState { Introduction, Active, Cleared, Failed, Victory }

public sealed class EnemyWaveController : MonoBehaviour
{
    [Serializable]
    public sealed class EnemyGroup
    {
        public EnemyBumper prefab;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public string title;
        [TextArea] public string introduction;
        public EnemyBumper prefab;
        [Tooltip("-1 uses the encounter default; 0 allows specialist-only waves.")]
        public int basicMinions = -1;
        [Min(0f)] public float minionSpeedInDiameters = 1.2f;
        [Min(0)] public int count;
        [Min(1f)] public float seconds = 180f;
        public EnemyGroup[] additionalGroups = Array.Empty<EnemyGroup>();

        public List<EnemyBumper> SpawnList()
        {
            var result = new List<EnemyBumper>();
            for (int i = 0; i < count; i++) result.Add(prefab);
            if (additionalGroups != null)
                foreach (EnemyGroup group in additionalGroups)
                    for (int i = 0; i < group.count; i++) result.Add(group.prefab);
            return result;
        }
    }
    [SerializeField] private BonkRobotController player;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private CombatPlacement placement;
    [SerializeField] private WaveDefinition[] waves;
    [SerializeField] private EnemyBumper minionPrefab;
    [Min(0)] [SerializeField] private int basicMinionsPerWave = 12;
    [SerializeField] private PinballScore scoring = new PinballScore();
    public PinballScore Scoring => scoring;
    public int BasicMinionsPerWave => CurrentWave.basicMinions >= 0 ? CurrentWave.basicMinions : basicMinionsPerWave;
    private PinballScore.Checkpoint scoreAtWaveStart;
    private BonkRobotController.DashCheckpoint dashAtWaveStart;
    private bool retryingWave;
    [SerializeField] private CombatHazard minePrefab;
    [SerializeField] private CombatHazard shellPrefab;
    [SerializeField] private HealthPickup healthPrefab;
    [SerializeField] private Material markerMaterial;
    [Min(1)] [SerializeField] private int globalMinionCap = 6;
    [Range(0f, 1f)] [SerializeField] private float healthDropChance = .05f;
    [SerializeField] private Vector3 playerStart;
    [SerializeField] private Quaternion playerRotation = Quaternion.identity;
    private readonly HashSet<EnemyBumper> enemies = new HashSet<EnemyBumper>();
    private readonly HashSet<EnemyBumper> minions = new HashSet<EnemyBumper>();
    private Transform enemyRoot, transientRoot;
    private System.Random random = new System.Random();
    private bool tryingAllTypes;
    private int nextMinionPatrol;
    // Injectable for deterministic drop tests, independent of placement randomness.
    public Func<double> DropRandom { get; set; }
    public EnemyWaveState State { get; private set; } = EnemyWaveState.Introduction;
    public int WaveIndex { get; private set; }
    public int WaveCount => waves.Length;
    public WaveDefinition CurrentWave => waves[WaveIndex];
    public float TimeRemaining { get; private set; }
    public string ResultMessage { get; private set; }
    public WaveResultSnapshot Result { get; private set; }
    public bool PresentationPaused { get; private set; }
    public bool IsCombatActive => State == EnemyWaveState.Active && !PresentationPaused;
    public EnemyBumper MinionPrefab => minionPrefab;
    public void SetPresentationPaused(bool paused)
    {
        PresentationPaused = paused;
        if (player != null) player.SetEncounterLocked(paused || State != EnemyWaveState.Active, false);
        if (health != null) health.CombatEnabled = !paused && State == EnemyWaveState.Active;
    }
    public int LivingEnemies => enemies.Count;
    public IReadOnlyCollection<EnemyBumper> LivingEnemyObjects => enemies;
    public IEnumerable<Transform> WarningMarkers => transientRoot != null ? transientRoot.Cast<Transform>() : Array.Empty<Transform>();
    public bool CanSpawnMinion => IsCombatActive && minions.Count < globalMinionCap;
    public BonkRobotController Player => player;
    public PlayerHealth Health => health;
    public CombatPlacement Placement => placement;
    public float MinionRadius => minionPrefab.GetComponent<CapsuleCollider>().radius;
    public float MinionHeight => minionPrefab.GetComponent<CapsuleCollider>().height;

    public void Configure(BonkRobotController owner, PlayerHealth playerHealth, CombatPlacement positions,
        WaveDefinition[] definitions, EnemyBumper minion, CombatHazard mine, CombatHazard shell,
        HealthPickup pickup, Material marker, Vector3 start, Quaternion rotation)
    {
        player = owner; health = playerHealth; placement = positions; waves = definitions;
        minionPrefab = minion; minePrefab = mine; shellPrefab = shell; healthPrefab = pickup;
        markerMaterial = marker; playerStart = start; playerRotation = rotation;
    }

    private void Start()
    {
        enemyRoot = new GameObject("Wave Enemies").transform;
        enemyRoot.SetParent(transform, false);
        transientRoot = new GameObject("Wave Hazards and Drops").transform;
        transientRoot.SetParent(transform, false);
        health.Damaged += OnPlayerDamaged;
        player.ConfigureEncounter(this);
        player.GetComponentInChildren<BouncingBallController>(true).ConfigureEncounter(this);
        SetState(EnemyWaveState.Introduction);
        TimeRemaining = CurrentWave.seconds;
    }

    private void OnPlayerDamaged()
    {
        if (IsCombatActive) scoring.BreakCombo(Time.time);
    }

    public void RegisterRicochet(float speed)
    {
        if (IsCombatActive && player.IsBallForm && speed >= player.SpeedSettings.SmallEnemyKillSpeed)
            scoring.RegisterBounce(Time.time);
    }

    public EnemyHitResult TryBonk(EnemyBumper enemy, float speed, PlayerSpeedSettings settings)
    {
        if (enemy == null || enemy.Encounter != this || !IsCombatActive) return EnemyHitResult.Rejected;
        if (enemy.IsDefeated) return EnemyHitResult.Defeated;
        if (!settings.CanKill(speed, enemy.IsSmallEnemy)) return EnemyHitResult.Rejected;
        int damage = scoring.ConsumeImpactCharge() ? 2 : 1;
        return enemy.TryHitFromBall(speed, settings, damage);
    }

    public void StartWave()
    {
        if (IsCombatActive) return;
        Cleanup();
        player.ResetForEncounter(playerStart, playerRotation);
        List<EnemyBumper> spawnList = CurrentWave.SpawnList();
        float radius = 0f, height = 0f;
        foreach (EnemyBumper prefab in spawnList)
        {
            CapsuleCollider shape = prefab.GetComponent<CapsuleCollider>();
            radius = Mathf.Max(radius, shape.radius);
            height = Mathf.Max(height, shape.height);
        }
        Physics.SyncTransforms();
        if (!placement.TryWavePositions(spawnList.Count, playerStart, radius, height,
            random, out List<Vector3> points))
        {
            ResultMessage = "No safe enemy positions. Check the arena spawn regions.";
            CaptureResult(WaveFailureReason.Placement);
            SetState(EnemyWaveState.Failed);
            return;
        }
        if (!placement.TryDistributedMinions(BasicMinionsPerWave, playerStart, points, MinionRadius, MinionHeight,
            random, out List<Vector3> basicPoints))
        {
            ResultMessage = "Not enough clear floor to distribute the basic minions.";
            CaptureResult(WaveFailureReason.Placement);
            SetState(EnemyWaveState.Failed);
            return;
        }
        if (!retryingWave)
        {
            scoreAtWaveStart = scoring.Save();
            dashAtWaveStart = player.SaveDashCheckpoint();
        }
        retryingWave = false;
        scoring.BreakCombo(Time.time, false);
        TimeRemaining = CurrentWave.seconds;
        SetState(EnemyWaveState.Active);
        for (int i = 0; i < spawnList.Count; i++) SpawnEnemy(spawnList[i], points[i], false);
        foreach (Vector3 point in basicPoints)
        {
            EnemyBumper minion = SpawnEnemy(minionPrefab, point, false);
            minion.ConfigureCombat(1, true, placement.Diameter * CurrentWave.minionSpeedInDiameters, true, placement.ArenaMin, placement.ArenaMax);
        }
    }

    private EnemyBumper SpawnEnemy(EnemyBumper prefab, Vector3 point, bool minion)
    {
        // Inactive staging ensures registration/binding precede OnEnable and the first simulation step.
        var staging = new GameObject("Spawn staging");
        staging.SetActive(false);
        EnemyBumper enemy = Instantiate(prefab, point, Quaternion.identity, staging.transform);
        enemy.Bind(this);
        if (enemy.IsBasicMinion) enemy.ConfigurePatrol((MinionPatrolAxis)(nextMinionPatrol++ % 4));
        enemies.Add(enemy);
        if (minion) minions.Add(enemy);
        enemy.Defeated += OnEnemyDefeated;
        enemy.transform.SetParent(enemyRoot, true);
        enemy.gameObject.SetActive(true);
        Destroy(staging);
        return enemy;
    }

    public bool TryFindMinionSpawn(out Vector3 position)
    {
        var specialists = new List<Vector3>();
        foreach (EnemyBumper enemy in enemies)
            if (enemy != null && !enemy.IsDefeated && !enemy.IsBasicMinion) specialists.Add(enemy.transform.position);
        if (placement.TryDistributedMinions(1, player.ActivePosition, specialists, MinionRadius, MinionHeight,
            random, out List<Vector3> points))
        {
            position = points[0];
            return true;
        }
        position = default;
        return false;
    }

    public bool IsMinionSpawnSafe(Vector3 position) =>
        PlayerMovementMath.Planar(position - player.ActivePosition).magnitude >= 6f * placement.Diameter &&
        placement.IsClear(position, MinionRadius, MinionHeight);

    public EnemyBumper SpawnMinion(Vector3 point) => CanSpawnMinion ? SpawnEnemy(minionPrefab, point, true) : null;

    public static bool ShouldDrop(double roll, float chance = .05f) => roll >= 0d && roll < Math.Round(chance, 6);

    private void OnEnemyDefeated(EnemyBumper enemy)
    {
        if (!enemies.Remove(enemy)) return;
        minions.Remove(enemy);
        enemy.Defeated -= OnEnemyDefeated;
        if (!IsCombatActive) return;
        scoring.RecordKill(enemy.IsBasicMinion, Time.time);
        double roll = DropRandom != null ? DropRandom() : random.NextDouble();
        if (ShouldDrop(roll, healthDropChance) &&
            placement.TryNearby(enemy.transform.position, placement.Diameter * .35f,
                placement.Diameter, placement.Diameter * 4f, out Vector3 point))
            Instantiate(healthPrefab, point, Quaternion.identity, transientRoot);
    }

    private void LateUpdate()
    {
        if (!IsCombatActive) return;
        scoring.Tick(Time.time);
        // Resolve after all collision and attack callbacks. Death wins over a simultaneous clear.
        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
        if (health.IsDead) Finish(EnemyWaveState.Failed, "Robot destroyed");
        else if (enemies.Count == 0)
            Finish(tryingAllTypes || WaveIndex == waves.Length - 1 ? EnemyWaveState.Victory : EnemyWaveState.Cleared, "Wave cleared!");
        else if (TimeRemaining <= 0f) Finish(EnemyWaveState.Failed, "Time ran out");
    }

    private void Finish(EnemyWaveState state, string message)
    {
        ResultMessage = message;
        CaptureResult(state == EnemyWaveState.Failed ? (health.IsDead ? WaveFailureReason.HealthDepleted : WaveFailureReason.Timeout) : WaveFailureReason.None);
        scoring.BreakCombo(Time.time, false);
        SetState(state);
        ClearChildren(transientRoot);
    }

    private void CaptureResult(WaveFailureReason reason)
    {
        Result = new WaveResultSnapshot { reason = reason, enemiesRemaining = enemies.Count,
            elapsedSeconds = Mathf.Max(0f, CurrentWave.seconds - TimeRemaining), health = health.Current };
    }

    private void SetState(EnemyWaveState state)
    {
        State = state;
        player.SetEncounterLocked(!IsCombatActive);
        health.CombatEnabled = IsCombatActive;
    }

    public void NextWave()
    {
        if (State != EnemyWaveState.Cleared) return;
        Cleanup();
        WaveIndex++;
        TimeRemaining = CurrentWave.seconds;
        SetState(EnemyWaveState.Introduction);
    }

    public void RetryWave()
    {
        if (State != EnemyWaveState.Failed) return;
        Cleanup();
        player.ResetForEncounter(playerStart, playerRotation);
        health.ResetHealth();
        scoring.Restore(scoreAtWaveStart);
        player.RestoreDashCheckpoint(dashAtWaveStart);
        retryingWave = true;
        StartWave();
    }

    // Locate by actual roster, never by array position: an older loaded scene may end in Mortars.
    public void TryAllEnemyTypes()
    {
        if (State != EnemyWaveState.Introduction) return;
        int mixedIndex = FindAllTypesWave();
        if (mixedIndex < 0)
        {
            ResultMessage = "All Types needs spawner, mine-layer, and mortar prefabs. Run Setup Map01 Enemy Waves.";
            CaptureResult(WaveFailureReason.Placement);
            SetState(EnemyWaveState.Failed);
            return;
        }
        RestartRun();
        tryingAllTypes = true;
        WaveIndex = mixedIndex;
        StartWave();
    }

    private int FindAllTypesWave()
    {
        EnemyBumper spawner = null, layer = null, mortar = null;
        for (int i = 0; i < waves.Length; i++)
        {
            bool hasSpawner = false, hasLayer = false, hasMortar = false;
            foreach (EnemyBumper prefab in waves[i].SpawnList())
            {
                if (prefab == null) continue;
                if (prefab.TryGetComponent<SpawnerEnemy>(out _)) { spawner = prefab; hasSpawner = true; }
                if (prefab.TryGetComponent<MineLayerEnemy>(out _)) { layer = prefab; hasLayer = true; }
                if (prefab.TryGetComponent<MortarEnemy>(out _)) { mortar = prefab; hasMortar = true; }
            }
            if (hasSpawner && hasLayer && hasMortar) return i;
        }
        if (spawner == null || layer == null || mortar == null) return -1;
        // Support scene data already loaded before the mixed wave was added to the saved scene.
        var mixed = new WaveDefinition {
            title = "All Types", prefab = spawner, count = 1, seconds = 180f,
            introduction = "Spawners, mine layers, and mortars together. Use the minimap to choose your targets.",
            additionalGroups = new[] {
                new EnemyGroup { prefab = layer, count = 1 },
                new EnemyGroup { prefab = mortar, count = 1 }
            }
        };
        Array.Resize(ref waves, waves.Length + 1);
        waves[waves.Length - 1] = mixed;
        return waves.Length - 1;
    }

    public void RestartRun()
    {
        PresentationPaused = false;
        Result = default;
        tryingAllTypes = false;
        retryingWave = false;
        scoring.Reset();
        player.ResetDashForRun();
        Cleanup();
        WaveIndex = 0;
        player.ResetForEncounter(playerStart, playerRotation);
        health.ResetHealth();
        TimeRemaining = CurrentWave.seconds;
        SetState(EnemyWaveState.Introduction);
    }

    private void Cleanup()
    {
        foreach (EnemyBumper enemy in enemies) if (enemy != null) enemy.Defeated -= OnEnemyDefeated;
        enemies.Clear(); minions.Clear();
        nextMinionPatrol = 0;
        ClearChildren(enemyRoot); ClearChildren(transientRoot);
    }
    private static void ClearChildren(Transform root)
    {
        if (root == null) return;
        foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }

    public CombatHazard SpawnMine(Vector3 point)
    {
        CombatHazard mine = Instantiate(minePrefab, point, Quaternion.identity, transientRoot);
        mine.Initialize(this, point);
        return mine;
    }
    public void SpawnShell(Vector3 launch, Vector3 target)
    {
        target.y = placement.Floor.bounds.max.y;
        CombatHazard shell = Instantiate(shellPrefab, target, Quaternion.identity, transientRoot);
        shell.Initialize(this, launch);
    }
    public GameObject CreateMarker(string label, Vector3 position, float radius, Color color)
    {
        var marker = new GameObject(label, typeof(LineRenderer));
        marker.transform.SetParent(transientRoot, false);
        marker.transform.position = position + Vector3.up * .04f;
        LineRenderer ring = marker.GetComponent<LineRenderer>();
        ring.sharedMaterial = markerMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 48;
        ring.widthMultiplier = placement.Diameter * .1f;
        ring.startColor = ring.endColor = color;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
        }
        return marker;
    }

    private void OnDestroy()
    {
        foreach (EnemyBumper enemy in enemies) if (enemy != null) enemy.Defeated -= OnEnemyDefeated;
        if (player != null) player.SetEncounterLocked(false);
        if (health != null)
        {
            health.Damaged -= OnPlayerDamaged;
            health.CombatEnabled = true;
        }
    }
}
