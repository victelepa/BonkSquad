using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Drives the actual UI buttons and presentation flow in the saved Map01 scene.
[InitializeOnLoad]
public static class OnboardingValidation
{
    private const string Running = "BonkSquad.OnboardingValidation";
    private static CombatPresentation ui;
    private static EnemyWaveController run;
    private static int stage, frames;
    private static double waitUntil, deadline;
    private static float timer, hitTime, stunTime, dizziness, gameTime;
    private static Vector3 position, velocity;
    static OnboardingValidation() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
    [MenuItem("Tools/BonkSquad/Validate Onboarding (Play Mode)")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(Map01CombatSetup.ScenePath);
        SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode();
    }
    [MenuItem("Tools/BonkSquad/Preview Wave Cards (Play Mode)")]
    public static void PreviewCards()
    {
        if (!EditorApplication.isPlaying) { Debug.Log("Enter Play Mode to preview the wave cards."); return; }
        var presentation = UnityEngine.Object.FindFirstObjectByType<CombatPresentation>();
        if (presentation != null) { presentation.PlayRun(); presentation.SkipTutorial(); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception("ONBOARDING_TEST_FAILED: " + message); }
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name && b.isActiveAndEnabled);
        Check(button != null, "button available: " + name); button.onClick.Invoke();
    }
    private static bool HasText(string value) => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.text.Contains(value));
    private static void Wait(int next)
    {
        waitUntil = EditorApplication.timeSinceStartup + .3; stage = next;
    }
    private static void KillAll()
    {
        foreach (EnemyBumper enemy in run.LivingEnemyObjects.ToArray())
            while (!enemy.IsDefeated) enemy.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
    }
    private static void CheckPreviewSafety()
    {
        Transform model = run.transform.Find("Preview meshes only"); Check(model != null, "actual enemy model preview exists");
        Check(model.GetComponentsInChildren<Renderer>().Length > 0, "preview has meshes");
        Check(model.GetComponentsInChildren<MonoBehaviour>().Length == 0 && model.GetComponentsInChildren<Collider>().Length == 0 &&
            model.GetComponentsInChildren<Rigidbody>().Length == 0, "preview contains no gameplay components");
        Check(run.LivingEnemies == 0, "preview never registers enemies");
    }
    private static void CheckTextLayout()
    {
        Canvas.ForceUpdateCanvases();
        foreach (Text text in ui.GetComponentsInChildren<Text>())
        {
            if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text) || text.name == "World cue" || text.name == "Map marker") continue;
            float preferred = text.preferredHeight;
            Check(preferred <= text.rectTransform.rect.height + 2, "text fits: " + text.name + " height=" + preferred + " available=" + text.rectTransform.rect.height);
        }
        // ScreenMatchMode.Expand keeps this reference layout inside all requested aspect ratios.
        RectTransform layout = ui.GetComponentsInChildren<RectTransform>().First(r => r.name == "Safe layout");
        Check(layout.rect.width == 1280 && layout.rect.height == 720, "fixed safe layout for 720p, 1080p, and 16:10");
        foreach (Vector2 size in new[] { new Vector2(1280,720), new Vector2(1920,1080), new Vector2(1920,1200) })
        {
            float scale = Mathf.Min(size.x/1280f, size.y/720f);
            Check(1280*scale <= size.x + .01f && 720*scale <= size.y + .01f, "reference fits aspect " + size);
        }
    }
    private static void ValidatePredictablePatrols()
    {
        var fixture = new GameObject("Patrol check");
        fixture.transform.position = new Vector3(1000, 1000, 1000);
        fixture.AddComponent<Rigidbody>();
        var capsule = fixture.AddComponent<CapsuleCollider>(); capsule.radius = .2f; capsule.height = .5f;
        var enemy = fixture.AddComponent<EnemyBumper>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var step = typeof(EnemyBumper).GetMethod("FixedUpdate", flags);
        foreach (MinionPatrolAxis axis in Enum.GetValues(typeof(MinionPatrolAxis)))
        {
            enemy.ConfigureCombat(1, true, 1f, true, new Vector2(990, 990), new Vector2(1010, 1010));
            enemy.ConfigurePatrol(axis);
            Vector3 direction = enemy.PatrolDirection;
            Field(enemy, "nextDirectionChange", float.NegativeInfinity); step.Invoke(enemy, null);
            Check(Vector3.Distance(enemy.PatrolDirection, direction) < .001f, "patrol ignores random direction timer: " + axis);
            enemy.ConfigureCombat(1, true, 1f, true, new Vector2(999.999f, 999.999f), new Vector2(1000.001f, 1000.001f));
            step.Invoke(enemy, null);
            Check(Vector3.Distance(enemy.PatrolDirection, -direction) < .001f, "patrol reverses along same route at edge: " + axis);
        }
        enemy.ConfigureCombat(1, true, 1f, true, new Vector2(990, 990), new Vector2(1010, 1010));
        enemy.ConfigurePatrol(MinionPatrolAxis.Horizontal);
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = fixture.transform.position + Vector3.right * .4f; obstacle.transform.localScale = Vector3.one * .2f;
        Physics.SyncTransforms(); step.Invoke(enemy, null);
        Check(enemy.PatrolDirection.x < 0, "solid obstacle reverses patrol without choosing a random heading");
        UnityEngine.Object.DestroyImmediate(obstacle); UnityEngine.Object.DestroyImmediate(fixture);
    }
    private static void ValidatePinballAssistance()
    {
        Vector3 start = run.Player.ActivePosition;
        run.Player.ToggleForm();
        var ball = run.Player.GetComponentInChildren<BouncingBallController>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var boost = typeof(BouncingBallController).GetMethod("TryWallBoost", flags);
        var halo = typeof(BouncingBallController).GetMethod("ResolveAttackHalo", flags);
        float threshold = run.Player.SpeedSettings.SmallEnemyKillSpeed;
        ball.EnterBallMode(Vector3.right * threshold);
        Check((bool)boost.Invoke(ball, null), "wall boost accepted");
        Vector3 boosted = (Vector3)typeof(BouncingBallController).GetField("simulationVelocity", flags).GetValue(ball);
        Check(Mathf.Abs(boosted.magnitude - threshold * 1.15f) < .01f, "wall adds 15 percent speed");
        Check(!(bool)boost.Invoke(ball, null), "wall cooldown prevents duplicate boost");
        ball.EnterBallMode(Vector3.right * run.Player.SpeedSettings.MaximumSpeed);
        Check(!(bool)boost.Invoke(ball, null), "wall cannot exceed speed cap");
        float d = run.Placement.Diameter;
        Vector3 origin = ball.transform.TransformPoint(ball.GetComponent<SphereCollider>().center);
        Vector3 point = default; bool found = false;
        for (int i = 0; i < 16 && !found; i++)
        {
            float angle = i * Mathf.PI / 8;
            point = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.15f * d;
            point.y = run.Placement.Floor.bounds.max.y;
            found = run.Placement.IsClear(point, run.MinionRadius, run.MinionHeight);
        }
        Check(found, "near-miss test position clear");
        var minion = run.SpawnMinion(point); Physics.SyncTransforms();
        ball.EnterBallMode(Vector3.right * (threshold - .01f)); halo.Invoke(ball, null);
        Check(!minion.IsDefeated, "halo respects speed threshold");
        ball.EnterBallMode(Vector3.right * threshold);
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Halo test obstacle";
        blocker.transform.position = Vector3.Lerp(origin, minion.GetComponent<Collider>().bounds.center, .55f);
        blocker.transform.localScale = new Vector3(.12f, 3f, .12f) * d;
        Physics.SyncTransforms(); halo.Invoke(ball, null);
        Check(!minion.IsDefeated, "halo cannot hit through obstacle");
        UnityEngine.Object.DestroyImmediate(blocker); Physics.SyncTransforms();
        halo.Invoke(ball, null); Check(minion.IsDefeated, "halo catches near miss outside physical contact");
        var waves = (EnemyWaveController.WaveDefinition[])typeof(EnemyWaveController).GetField("waves", flags).GetValue(run);
        var specialistPrefab = waves.SelectMany(w => w.SpawnList()).First();
        var specialist = (EnemyBumper)typeof(EnemyWaveController).GetMethod("SpawnEnemy", flags).Invoke(run, new object[] { specialistPrefab, point, false });
        Physics.SyncTransforms(); halo.Invoke(ball, null);
        Check(specialist.HitPoints == specialist.MaximumHitPoints, "halo leaves specialists to physical bonks");
        while (!specialist.IsDefeated) specialist.TryHitFromBall(threshold, run.Player.SpeedSettings);
        ball.SendMessage("LateUpdate");
        Check(ball.GetComponentsInChildren<LineRenderer>().Any(r => r.name == "Attack halo" && r.enabled), "attack-ready halo is visible");
        run.Player.ResetForEncounter(start, Quaternion.identity);
        Check(!ball.GetComponentsInChildren<LineRenderer>(true).Any(r => r.name == "Attack halo" && r.enabled), "robot form clears halo");
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
            Check(EditorApplication.timeSinceStartup < deadline, "timeout at stage " + stage);
            if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<CombatPresentation>();
            if (ui == null || frames++ < 6) return;
            run = ui.GetComponent<EnemyWaveController>();
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            switch (stage)
            {
                case 0:
                    Check(ui.Page == CombatPage.MainMenu && Time.timeScale == 0 && run.Player.IsEncounterLocked, "launch menu locks game");
                    Check(HasText("One player. Two forms."), "single-player goal stated"); CheckTextLayout();
                    Click("How to Play"); Check(ui.IsTourOpen, "menu help opens tour"); Click("Skip Tutorial");
                    Check(ui.Page == CombatPage.MainMenu, "menu help returns to menu"); Click("Play");
                    Check(ui.IsTourOpen && ui.TutorialIndex == 0, "Play starts tour");
                    timer = run.TimeRemaining; gameTime = Time.time; position = run.Player.ActivePosition; Wait(1); break;
                case 1:
                    Check(Time.time == gameTime && run.TimeRemaining == timer && run.LivingEnemies == 0 && run.Player.ActivePosition == position, "tour freezes simulation");
                    for (int i = 0; i < 4; i++) { CheckTextLayout(); Click("Next"); }
                    Check(ui.TutorialIndex == 4 && HasText("automatically"), "charge needs no activation key"); CheckTextLayout();
                    Click("Back"); Check(ui.TutorialIndex == 3, "tour back"); Click("Next"); Click("Meet the enemies");
                    Check(ui.Page == CombatPage.WavePreview && ui.PreviewCount == 1, "first wave only has basic minions"); CheckPreviewSafety(); CheckTextLayout();
                    Click("→"); Check(ui.PreviewIndex == 0 && HasText("1 fast ball hit"), "minion card one hit"); CheckTextLayout();
                    Click("→"); Check(ui.PreviewIndex == 0, "single-card carousel wraps");
                    ValidatePredictablePatrols();
                    Check(run.WaveCount == 5 && run.BasicMinionsPerWave == 4 && run.CurrentWave.count == 0, "gentle first-wave roster");
                    Check(run.MinionRadius >= .49f * run.Placement.Diameter, "wider minion hit area");
                    var settings = new PlayerDizzinessSettings();
                    Check(Mathf.Abs(settings.GainRate(run.Player.SpeedSettings.MaximumSpeed, run.Player.SpeedSettings) - 10f) < .01f, "dizziness gain halved");
                    Click("Start Wave"); Check(run.IsCombatActive && run.LivingEnemies == 4 && ui.Page == CombatPage.Combat, "start releases locks");
                    Check(run.LivingEnemyObjects.All(e => e.UsesStraightPatrol) && run.LivingEnemyObjects.Select(e => e.PatrolDirection).Distinct().Count() == 4,
                        "first wave gets four distinct readable patrol axes");
                    ui.SendMessage("Update");
                    var pins = ui.GetComponentsInChildren<RectTransform>().Where(r => r.name == "Map pin").ToArray();
                    Check(pins.Length == 5 && pins.Any(p => p.rect.width == 24) && pins.Count(p => p.rect.width == 12) == 4, "player and all starting minions have visible map pins");
                    run.DropRandom = () => .99d;
                    Check(run.TryFindMinionSpawn(out Vector3 childPoint), "safe reinforcement location");
                    var child = run.SpawnMinion(childPoint); Check(child.UsesStraightPatrol, "reinforcements use predictable patrols"); ui.SendMessage("Update");
                    Check(ui.GetComponentsInChildren<RectTransform>().Count(r => r.name == "Map pin") == 6, "spawned minion appears on map immediately");
                    child.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
                    ui.SendMessage("Update");
                    Check(ui.GetComponentsInChildren<RectTransform>().Count(r => r.name == "Map pin") == 5, "defeated reinforcement leaves map");
                    ValidatePinballAssistance();
                    Check(Mathf.Abs(run.Player.DizzinessSettings.GainRate(run.Player.SpeedSettings.MaximumSpeed, run.Player.SpeedSettings) - 10f) < .01f, "scene dizziness gain halved");
                    run.Health.TryDamageFrom(run.Player.ActivePosition - Vector3.right);
                    run.Player.ToggleForm();
                    Property(run.Player, "DizzinessPhase", PlayerDizzinessPhase.Stunned);
                    Property(run.Player, "PhaseTimeRemaining", 1.7f);
                    Property(run.Player, "DizzinessValue", 75f);
                    timer = run.TimeRemaining; hitTime = run.Health.LastHitTime; stunTime = run.Player.PhaseTimeRemaining; dizziness = run.Player.DizzinessValue;
                    position = run.Player.ActivePosition; velocity = run.Player.CurrentPlanarVelocity; gameTime = Time.time;
                    Click("Help"); Check(ui.IsTourOpen, "combat help"); Wait(2); break;
                case 2:
                    Check(Time.time == gameTime && run.TimeRemaining == timer && run.Player.PhaseTimeRemaining == stunTime && run.Player.DizzinessValue == dizziness &&
                        run.Player.ActivePosition == position && run.Player.CurrentPlanarVelocity == velocity, "help preserves timer, stun, dizziness, position and velocity");
                    Check(!run.Health.TryDamage() && run.Health.LastHitTime == hitTime, "help blocks damage");
                    Click("Close Help"); Check(run.IsCombatActive && run.Player.PhaseTimeRemaining == stunTime && run.Health.LastHitTime == hitTime, "help resumes saved state");
                    Check(!run.Health.TryDamage(), "protection remains after help");
                    run.Scoring.Reset();
                    run.Scoring.RecordKill(false, Time.time);
                    run.Scoring.RegisterBounce(Time.time); run.Scoring.RecordKill(false, Time.time);
                    ui.SendMessage("Update"); CheckTextLayout();
                    run.Scoring.RegisterBounce(Time.time); run.Scoring.RecordKill(false, Time.time);
                    ui.SendMessage("Update"); CheckTextLayout();
                    Property(run.Player, "DizzinessPhase", PlayerDizzinessPhase.Normal); run.Health.ResetHealth();
                    Property(run, "TimeRemaining", 0f); Wait(3); break;
                case 3:
                    Check(ui.Page == CombatPage.Results && run.Result.reason == WaveFailureReason.Timeout && run.Result.enemiesRemaining == 4, "timeout snapshot");
                    Check(HasText("Time ran out — 4 enemies remained"), "explicit timeout reason"); CheckTextLayout();
                    Click("Retry Wave"); Check(ui.Page == CombatPage.Combat && run.Health.Current == 100 && run.IsCombatActive, "retry bypasses tutorial");
                    KillAll(); Wait(4); break;
                case 4:
                    Check(run.State == EnemyWaveState.Cleared && ui.Page == CombatPage.Results, "clear results");
                    Click("Next Wave"); Check(ui.Page == CombatPage.WavePreview && ui.PreviewCount == 2 && HasText("Spawner") && HasText("2 fast ball hits"), "next wave adds one specialist");
                    ui.SendMessage("Update"); Check(HasText("S  Spawner"), "legend introduces specialist with its wave");
                    CheckTextLayout(); Click("Start Wave");
                    run.Health.TryDamage(1000); Wait(5); break;
                case 5:
                    Check(run.Result.reason == WaveFailureReason.HealthDepleted && HasText("Health reached zero"), "explicit death reason");
                    Click("Main Menu"); Check(ui.Page == CombatPage.MainMenu && run.LivingEnemies == 0, "menu cleans encounter");
                    Click("Play"); Check(ui.IsTourOpen, "new run repeats tour"); Click("Skip Tutorial");
                    Check(ui.Page == CombatPage.WavePreview, "skip goes to preview");
                    ui.DismissForValidation(); run.TryAllEnemyTypes(); run.RestartRun();
                    // Use the actual mixed definition while keeping the developer shortcut off the player UI.
                    Field(run, "<WaveIndex>k__BackingField", run.WaveCount - 1); ui.ShowWavePreview();
                    Check(ui.PreviewCount == 4, "mixed wave has all four enemy cards");
                    for (int i = 0; i < 4; i++) { CheckPreviewSafety(); CheckTextLayout(); ui.BrowseEnemy(1); }
                    Check(!UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "Try All Enemy Types"), "developer shortcut hidden");
                    Click("Start Wave"); KillAll(); Wait(6); break;
                case 6:
                    Check(ui.Page == CombatPage.Results && run.State == EnemyWaveState.Victory, "victory screen");
                    Click("Restart Run"); Check(ui.IsTourOpen && run.WaveIndex == 0, "victory restart repeats tutorial");
                    Debug.Log("ONBOARDING_VALIDATION_OK: fixed minion patrol axes, no timer randomness, edge/obstacle reversals and reinforcement routes; wall boost/cap/cooldown, speed-qualified minion halo, obstacle rejection, specialist exclusion and form visuals; launch/menu, five cards, skip/back, model-only roster carousel, gameplay start, paused help with stun/protection preservation, timeout/death reasons, retries, next wave, mixed roster, victory/restart, safe aspect layouts and text fit.");
                    Finish(0); break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
