using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Repeatable integration checks without adding test-only assembly references to gameplay scripts.
[InitializeOnLoad]
public static class CombatValidation
{
    private const string Running = "BonkSquad.CombatValidation";
    private static int stage;
    private static float started;
    private static double deadline;
    private static EnemyWaveController run;
    private static int drops;
    private static int warmupFrames;
    private static Vector3 knockbackStart;
    private static Vector3 encounterStart;
    private static bool checkedSpawnBudget;
    private static System.Collections.Generic.Dictionary<EnemyBumper, Vector3> initialMinions;
    static CombatValidation()
    {
        if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/BonkSquad/Validate Combat (Play Mode)")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(Map01CombatSetup.ScenePath);
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    // Batch entry point also verifies setup is safe to repeat without duplicating the encounter.
    public static void SetupAndRun()
    {
        Map01CombatSetup.Setup();
        Check(UnityEngine.Object.FindObjectsByType<EnemyWaveController>(FindObjectsSortMode.None).Length == 1,
            "rerunning setup leaves exactly one encounter");
        Run();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("COMBAT_TEST_FAILED: " + message);
    }
    private static void Field(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static int Count<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
    private static EnemyBumper[] Enemies() => UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None).Where(e => !e.IsDefeated).ToArray();
    private static void KillAll()
    {
        run.DropRandom = () => .99d;
        foreach (EnemyBumper enemy in Enemies())
            while (!enemy.IsDefeated) enemy.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
    }

    private static void ValidateScoring()
    {
        var score = new PinballScore();
        score.RegisterBounce(1f);
        Check(score.RecordKill(true, 2f) == 35, "ricochet adds 25 to minion base score");
        Check(score.RecordKill(true, 4f) == 40, "double bonk bonus");
        Check(score.RecordKill(true, 6f) == 60, "triple bonk bonus");
        Check(score.Points == 135 && score.Progress == 35 && score.ImpactCharges == 1, "meter overflow and charge award");
        Check(score.ConsumeImpactCharge() && !score.ConsumeImpactCharge(), "single-use charge");
        Check(score.RecordKill(true, 7f) == 10, "fourth kill cannot repeat triple bonus");
        int earned = score.Points;
        score.BreakCombo(7.5f);
        Check(score.Points == earned && score.RecordKill(true, 8f) == 10, "damage breaks combo without deducting points");
        score.RegisterBounce(9f);
        Check(score.RecordKill(true, 12f) == 35, "inclusive three-second ricochet boundary");
        Check(score.RecordKill(true, 15.001f) == 10, "expired chain starts over");
        var checkpoint = score.Save();
        score.RecordKill(false, 16f);
        score.Restore(checkpoint);
        Check(score.Points == checkpoint.points && score.ImpactCharges == checkpoint.charges && score.ChainCount == 0,
            "retry checkpoint restores score and charges without a live combo");
        score.Reset();
        score.RegisterBounce(0f);
        Check(score.RecordKill(true, 3.001f) == 10, "expired bounce grants no bonus");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (deadline == 0d) deadline = EditorApplication.timeSinceStartup + 90d;
            Check(EditorApplication.timeSinceStartup < deadline, "integration check timed out at stage " + stage);
            if (run == null) run = UnityEngine.Object.FindFirstObjectByType<EnemyWaveController>();
            if (run == null) return;
            switch (stage)
            {
                case 0:
                    if (!run.Player.IsEncounterLocked) return; // Wait for Start.
                    if (warmupFrames++ < 4) return;
                    Check(run.State == EnemyWaveState.Introduction && run.WaveCount == 4, "initial intro");
                    Check(run.Health.Current == 100 && !run.Health.CombatEnabled, "initial health and combat lock");
                    Check(EnemyWaveController.ShouldDrop(0d) && EnemyWaveController.ShouldDrop(.049999d), "5% positive boundary");
                    Check(!EnemyWaveController.ShouldDrop(.05d) && !EnemyWaveController.ShouldDrop(.99d), "5% negative boundary");
                    Map01CombatSetup.ValidatePlacement(run.Placement, run.Player.ActivePosition);
                    // Force fallback and verify the same guarantees without random attempts.
                    Field(run.Placement, "randomAttempts", 0);
                    Map01CombatSetup.ValidatePlacement(run.Placement, run.Player.ActivePosition);
                    Field(run.Placement, "randomAttempts", 100);
                    ValidateScoring();
                    run.StartWave();
                    Check(run.LivingEnemies == 2 + run.BasicMinionsPerWave && !run.Player.IsEncounterLocked, "spawner wave start");
                    initialMinions = Enemies().Where(e => e.IsBasicMinion).ToDictionary(e => e, e => e.transform.position);
                    EnemyBumper basic = Enemies().First(e => e.IsBasicMinion);
                    basic.ContactPlayer(run.Health);
                    Check(run.Health.Current == 80, "basic minion damages robot form");
                    var feedback = run.Health.GetComponent<PlayerDamageFeedback>();
                    Check(feedback != null && feedback.IsFlashing, "accepted damage flashes player");
                    run.Player.ToggleForm();
                    run.Health.ResetHealth();
                    basic.ContactPlayer(run.Health);
                    Check(run.Health.Current == 100, "basic minion contact cannot damage ball form");
                    Check(!feedback.IsFlashing, "reset clears flash and immune contact does not trigger it");
                    run.Player.ToggleForm();
                    run.Player.ResetForEncounter(run.Player.ActivePosition, Quaternion.identity);
                    Check(run.Health.TryDamage() && run.Health.Current == 80, "20 damage");
                    Check(!run.Health.TryDamage(), "one-second protection");
                    Check(run.Health.TryHeal(50) && run.Health.Current == 100 && !run.Health.TryHeal(25), "healing clamp and full-health rejection");
                    run.Health.ResetHealth();
                    encounterStart = run.Player.ActivePosition;
                    knockbackStart = run.Player.ActivePosition;
                    Check(run.Health.TryDamageFrom(knockbackStart - Vector3.right * 2f), "directional robot damage");
                    Vector3 pushed = run.Player.CurrentPlanarVelocity;
                    Check(pushed.x > 0f && run.Player.IsKnockedBack, "robot pushed away from damage source");
                    Check(!run.Health.TryDamageFrom(knockbackStart + Vector3.right * 2f) &&
                        run.Player.CurrentPlanarVelocity == pushed, "invulnerability blocks repeated knockback");
                    started = Time.time;
                    stage = 100;
                    break;
                case 100:
                    if (Time.time - started < .3f) return;
                    Check(run.Player.ActivePosition.x > knockbackStart.x + .1f, "robot physically moves under knockback");
                    Check(!run.Player.IsKnockedBack, "knockback recovery expires");
                    Vector3 inherited = run.Player.CurrentPlanarVelocity;
                    run.Player.ToggleForm();
                    Check(run.Player.IsBallForm && Vector3.Distance(run.Player.CurrentPlanarVelocity, inherited) < .01f,
                        "form switch preserves knockback momentum");
                    run.Health.ResetHealth();
                    knockbackStart = run.Player.ActivePosition;
                    Check(run.Health.TryDamageFrom(knockbackStart + Vector3.right * 2f), "directional ball damage");
                    Check(run.Player.CurrentPlanarVelocity.x < 0f && run.Player.CurrentSpeed <= run.Player.SpeedSettings.MaximumSpeed,
                        "ball pushed away and speed capped");
                    started = Time.time;
                    stage = 101;
                    break;
                case 101:
                    if (Time.time - started < .3f) return;
                    Check(run.Player.ActivePosition.x < knockbackStart.x - .1f, "ball physically moves under knockback");
                    run.Health.ResetHealth();
                    Check(run.Health.TryDamageFrom(run.Player.ActivePosition) && run.Player.CurrentPlanarVelocity.sqrMagnitude > 0f,
                        "exact-center blast has a nonzero fallback direction");
                    run.Player.ResetForEncounter(encounterStart, Quaternion.identity);
                    Check(!run.Player.IsKnockedBack && run.Player.CurrentSpeed == 0, "encounter reset clears knockback");
                    run.Health.ResetHealth();
                    run.Health.CombatEnabled = false;
                    Time.timeScale = 10f;
                    started = Time.time;
                    stage = 1;
                    break;
                case 1:
                    if (Time.time - started < 22f) return;
                    Check(initialMinions.Count(p => p.Key != null && Vector3.Distance(p.Key.transform.position, p.Value) > run.Placement.Diameter) >= 6,
                        "common minions actively roam away from starting positions");
                    Check(!run.Health.GetComponent<PlayerDamageFeedback>().IsFlashing, "damage flash fades out");
                    EnemyBumper[] all = Enemies();
                    int minions = all.Count(e => e.GetComponent<SpawnerEnemy>() == null);
                    Check((checkedSpawnBudget ? minions == run.BasicMinionsPerWave : minions > run.BasicMinionsPerWave) &&
                        minions <= run.BasicMinionsPerWave + 6, "spawners create capped minions");
                    Check(Count<CombatHazard>() == 0, "no wrong-wave hazards");
                    if (!checkedSpawnBudget)
                    {
                        var spawned = (System.Collections.Generic.HashSet<EnemyBumper>)typeof(EnemyWaveController)
                            .GetField("minions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
                        run.DropRandom = () => .99d;
                        foreach (EnemyBumper child in spawned.ToArray()) child.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
                        started = Time.time;
                        stage = 150;
                        break;
                    }
                    EnemyBumper parent = all.First(e => e.GetComponent<SpawnerEnemy>() != null);
                    int notifications = 0;
                    parent.Defeated += _ => notifications++;
                    float speed = run.Player.SpeedSettings.SmallEnemyKillSpeed;
                    Check(parent.TryHitFromBall(speed - .001f, run.Player.SpeedSettings) == EnemyHitResult.Rejected && parent.HitPoints == 2, "below threshold");
                    Check(parent.TryHitFromBall(speed, run.Player.SpeedSettings) == EnemyHitResult.Damaged && parent.HitPoints == 1, "threshold damages");
                    drops = Count<HealthPickup>();
                    run.DropRandom = () => .049999d;
                    Check(parent.TryHitFromBall(speed, run.Player.SpeedSettings) == EnemyHitResult.Defeated, "second hit defeats");
                    parent.TryHitFromBall(speed, run.Player.SpeedSettings);
                    Check(notifications == 1 && Count<HealthPickup>() == drops + 1, "one death and one drop roll");
                    Check(run.LivingEnemies >= minions + 1, "minions survive parent death");
                    EnemyBumper second = all.First(e => e != parent && e.GetComponent<SpawnerEnemy>() != null);
                    run.DropRandom = () => .05d;
                    second.TryHitFromBall(speed, run.Player.SpeedSettings);
                    second.TryHitFromBall(speed, run.Player.SpeedSettings);
                    Check(Count<HealthPickup>() == drops + 1, "exactly .05 does not drop");
                    Check(run.IsCombatActive && run.LivingEnemies == minions, "pending minions prevent clear");
                    foreach (EnemyBumper child in Enemies())
                        child.ConfigureCombat(1, true, 0f, false, run.Placement.ArenaMin, run.Placement.ArenaMax);
                    run.Health.CombatEnabled = true;
                    run.Health.ResetHealth();
                    HealthPickup pickup = UnityEngine.Object.FindFirstObjectByType<HealthPickup>();
                    Vector3 pickupPosition = run.Player.ActivePosition;
                    pickupPosition.y = run.Placement.Floor.bounds.max.y;
                    pickup.transform.position = pickupPosition;
                    Physics.SyncTransforms();
                    started = Time.time;
                    stage = 10;
                    break;
                case 10:
                    if (Time.time - started < .3f) return;
                    Check(Count<HealthPickup>() == drops + 1, "pickup remains at full health");
                    run.Player.ToggleForm();
                    Check(run.Player.IsBallForm && run.Health.Current == 100, "health shared with ball form");
                    run.Health.TryDamage();
                    started = Time.time;
                    stage = 11;
                    break;
                case 11:
                    if (Time.time - started < 2f && run.Health.Current != 100) return;
                    Check(run.Health.Current == 100 && Count<HealthPickup>() == drops, "ball collects healing once: health=" + run.Health.Current + " pickups=" + Count<HealthPickup>() + " player=" + run.Player.ActivePosition + " pickup=" + (UnityEngine.Object.FindFirstObjectByType<HealthPickup>() != null ? UnityEngine.Object.FindFirstObjectByType<HealthPickup>().transform.position.ToString() : "none"));
                    run.Player.ToggleForm();
                    Check(!run.Player.IsBallForm && run.Health.Current == 100, "health shared with robot form");
                    run.Health.ResetHealth();
                    run.Health.TryDamage();
                    KillAll();
                    stage = 2;
                    break;
                case 150:
                    if (Time.time - started < 20f) return;
                    if (Time.time - started < 60f && UnityEngine.Object.FindObjectsByType<SpawnerEnemy>(FindObjectsSortMode.None)
                        .Any(s => s.TotalSpawned < s.SpawnBudget)) return;
                    foreach (SpawnerEnemy spawner in UnityEngine.Object.FindObjectsByType<SpawnerEnemy>(FindObjectsSortMode.None))
                        Check(spawner.TotalSpawned == spawner.SpawnBudget, "spawner uses its finite budget");
                    var remainingSpawned = (System.Collections.Generic.HashSet<EnemyBumper>)typeof(EnemyWaveController)
                        .GetField("minions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
                    foreach (EnemyBumper child in remainingSpawned.ToArray()) child.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
                    started = Time.time;
                    stage = 151;
                    break;
                case 151:
                    if (Time.time - started < 9f) return;
                    Check(run.LivingEnemies == run.BasicMinionsPerWave + 2, "exhausted spawners cannot farm more minions");
                    checkedSpawnBudget = true;
                    started = Time.time - 22f;
                    stage = 1;
                    break;
                case 2:
                    if (run.State != EnemyWaveState.Cleared) return;
                    Check(run.Health.Current == 80 && !run.Health.CombatEnabled, "health preserved after clear");
                    Check(Count<HealthPickup>() == 0, "drops cleaned after clear");
                    run.NextWave();
                    Check(run.WaveIndex == 1 && run.State == EnemyWaveState.Introduction, "second wave introduction");
                    run.StartWave();
                    Check(run.Health.Current == 80 && run.LivingEnemies == 3 + run.BasicMinionsPerWave, "mine wave and carried health");
                    run.Health.CombatEnabled = false;
                    started = Time.time; stage++;
                    break;
                case 3:
                    if (Time.time - started < 17f) return;
                    Check(Count<CombatHazard>() > 0 && Count<CombatHazard>() <= 9, "mines created and capped");
                    // A mine must not damage while arming; it must explode once after arming.
                    foreach (CombatHazard existing in UnityEngine.Object.FindObjectsByType<CombatHazard>(FindObjectsSortMode.None))
                        UnityEngine.Object.Destroy(existing.gameObject);
                    run.Health.CombatEnabled = true;
                    run.SpawnMine(run.Player.ActivePosition);
                    run.Health.ResetHealth();
                    started = Time.time; stage++;
                    break;
                case 4:
                    if (Time.time - started < 1f) { Check(run.Health.Current == 100, "mine arming grace"); return; }
                    if (Time.time - started < 2f) return;
                    Check(run.Health.Current == 80, "armed mine damages once");
                    run.Health.ResetHealth();
                    KillAll(); stage++;
                    break;
                case 5:
                    if (run.State != EnemyWaveState.Cleared) return;
                    Check(Count<CombatHazard>() == 0, "hazards cleaned on clear");
                    run.NextWave(); run.StartWave();
                    Check(run.LivingEnemies == 3 + run.BasicMinionsPerWave && run.WaveIndex == 2, "mortar wave start");
                    run.Health.ResetHealth();
                    run.SpawnShell(run.Player.ActivePosition + Vector3.forward * 5f, run.Player.ActivePosition);
                    started = Time.time; stage++;
                    break;
                case 6:
                    if (Time.time - started < 1.5f) { Check(run.Health.Current == 100, "mortar warning grace"); return; }
                    if (Time.time - started < 2.3f) return;
                    Check(run.Health.Current == 80, "mortar explosion damages once");
                    // Force death and final defeat in the same tick: death must win.
                    run.Health.ResetHealth(); run.Health.TryDamage(100);
                    KillAll(); stage++;
                    break;
                case 7:
                    if (run.State != EnemyWaveState.Failed) return;
                    Check(run.Health.IsDead && run.Player.IsEncounterLocked, "death wins over clear");
                    run.RetryWave();
                    Check(run.WaveIndex == 2 && run.Health.Current == 100 && run.TimeRemaining == 180 && run.LivingEnemies == 3 + run.BasicMinionsPerWave, "retry resets current wave");
                    Check(run.Player.CurrentSpeed == 0 && run.Player.DizzinessValue == 0, "retry resets movement and dizziness");
                    run.Scoring.RecordKill(false, Time.time);
                    Check(run.Scoring.Points > 0, "failed attempt can earn score before retry");
                    // Expire the timer without clearing enemies.
                    typeof(EnemyWaveController).GetProperty("TimeRemaining").SetValue(run, 0f);
                    stage++;
                    break;
                case 8:
                    if (run.State != EnemyWaveState.Failed) return;
                    Check(!run.Health.IsDead, "timeout without death");
                    run.RetryWave();
                    var scoreCheckpoint = (PinballScore.Checkpoint)typeof(EnemyWaveController)
                        .GetField("scoreAtWaveStart", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
                    Check(run.Scoring.Points == scoreCheckpoint.points && run.Scoring.ImpactCharges == scoreCheckpoint.charges,
                        "retry cannot farm score or charges");
                    // Clear and timeout together: clear must win.
                    KillAll();
                    typeof(EnemyWaveController).GetProperty("TimeRemaining").SetValue(run, 0f);
                    stage++;
                    break;
                case 9:
                    if (run.State != EnemyWaveState.Cleared) return;
                    run.NextWave(); run.StartWave();
                    Check(run.WaveIndex == 3 && run.TimeRemaining == 180 && run.LivingEnemies == 3 + run.BasicMinionsPerWave, "mixed wave and longer timer");
                    Check(Count<SpawnerEnemy>() == 1 && Count<MineLayerEnemy>() == 1 && Count<MortarEnemy>() == 1,
                        "mixed wave includes all three specialists");
                    Check(run.LivingEnemyObjects.Count == run.LivingEnemies, "minimap shares live enemy registry");
                    Bounds bounds = run.Placement.Floor.bounds;
                    Rect map = new Rect(0, 0, 200, 200);
                    Check(Vector2.Distance(CombatHUD.WorldToMinimap(bounds.center, bounds, map), new Vector2(100, 100)) < .01f,
                        "minimap projects arena center");
                    Check(Vector2.Distance(CombatHUD.WorldToMinimap(bounds.max, bounds, map), new Vector2(200, 0)) < .01f,
                        "minimap north-east orientation");
                    Check(Vector2.Distance(CombatHUD.WorldToMinimap(bounds.min, bounds, map), new Vector2(0, 200)) < .01f,
                        "minimap south-west orientation");
                    run.Scoring.Reset();
                    for (int i = 0; i < 10; i++) run.Scoring.RecordKill(true, Time.time + i * 4f);
                    Check(run.Scoring.ImpactCharges == 1, "100 points grants Impact Charge");
                    EnemyBumper target = Enemies().First(e => e.GetComponent<MortarEnemy>() != null);
                    Check(run.TryBonk(target, run.Player.SpeedSettings.SmallEnemyKillSpeed - .01f, run.Player.SpeedSettings) == EnemyHitResult.Rejected &&
                        run.Scoring.ImpactCharges == 1, "under-speed hit does not consume charge");
                    Check(run.TryBonk(target, run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings) == EnemyHitResult.Defeated &&
                        run.Scoring.ImpactCharges == 0, "charged qualifying hit removes both specialist segments");
                    KillAll();
                    Check(run.LivingEnemyObjects.Count == 0, "defeated enemies disappear from minimap registry");
                    stage = 12;
                    break;
                case 12:
                    if (run.State != EnemyWaveState.Victory) return;
                    run.RestartRun();
                    Check(run.WaveIndex == 0 && run.State == EnemyWaveState.Introduction && run.Health.Current == 100, "restart run");
                    Check(Count<CombatHazard>() == 0 && Count<HealthPickup>() == 0, "restart cleanup");
                    // Reproduce a loaded scene with only the original three waves (Mortars last).
                    var originalWaves = (EnemyWaveController.WaveDefinition[])typeof(EnemyWaveController)
                        .GetField("waves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
                    Field(run, "waves", originalWaves.Take(3).ToArray());
                    run.TryAllEnemyTypes();
                    Check(run.WaveIndex == 3 && run.IsCombatActive && run.LivingEnemies == 3 + run.BasicMinionsPerWave && run.TimeRemaining == 180,
                        "try-all repairs older three-wave configuration");
                    Check(Count<SpawnerEnemy>() == 1 && Count<MineLayerEnemy>() == 1 && Count<MortarEnemy>() == 1,
                        "try-all spawns actual types, not merely three enemies");
                    KillAll();
                    stage = 13;
                    break;
                case 13:
                    if (run.State != EnemyWaveState.Victory) return;
                    run.RestartRun();
                    var definitions = (EnemyWaveController.WaveDefinition[])typeof(EnemyWaveController)
                        .GetField("waves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
                    // Put Mortars last again, this time with a valid mixed wave earlier in the array.
                    (definitions[2], definitions[3]) = (definitions[3], definitions[2]);
                    run.TryAllEnemyTypes();
                    Check(run.WaveIndex == 2 && Count<SpawnerEnemy>() == 1 && Count<MineLayerEnemy>() == 1 && Count<MortarEnemy>() == 1,
                        "try-all finds a mixed roster independently of wave order");
                    KillAll();
                    stage = 14;
                    break;
                case 14:
                    if (run.State != EnemyWaveState.Victory) return;
                    Debug.Log("COMBAT_VALIDATION_OK: arena-wide minion distribution and roaming, damage flash trigger/reset/fade, robot-only contact damage, ricochet/double/triple scoring, Impact Charge and retry checkpoints; robot/ball knockback movement, direction, protection, form switching, recovery and reset; 500 placement cases, health, drops, enemy attacks, four waves, 180-second timers, mixed roster, minimap projection/registry, try-all with legacy/reordered scenes, death, timeout, retry and restart.");
                    Stop(0); break;
            }
        }
        catch (Exception error) { Debug.LogException(error); Stop(1); }
    }

    private static void Stop(int code)
    {
        Time.timeScale = 1f;
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.ExitPlaymode();
    }
}
