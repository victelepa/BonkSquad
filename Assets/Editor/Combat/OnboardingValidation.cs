using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Drives the actual UI buttons and presentation flow in the saved Map01 scene.
[InitializeOnLoad]
public static class OnboardingValidation
{
    private const string Running = "BonkSquad.OnboardingValidation";
    private static CombatPresentation ui;
    private static EnemyWaveController run;
    private static Keyboard practiceKeyboard;
    private static InputSettings originalInputSettings;
    private static UIDocument practiceDocument;
    private static BonkRobotController practicePlayer;
    private static TrailRenderer[][] practiceTrails;
    private static bool sawPracticeDashTrail;
    private static Vector3 practiceDashStart;
    private static float practiceDashAttemptedAt;
    private static EnemyBumper practiceDashTarget;
    private static EnemyBumper collisionTarget;
    private static float completionObservedAt;
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
    private static void Check(bool value, string message) { if (!value) throw new Exception("ONBOARDING_TEST_FAILED: " + message); }
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static VisualElement Root => ui.GetComponentInChildren<UIDocument>().rootVisualElement;

    private static bool Visible(VisualElement element)
    {
        for (var current = element; current != null; current = current.parent)
            if (current.ClassListContains("hidden")) return false;
        return true;
    }

    private static void Click(string name)
    {
        var button = Root.Query<Button>().ToList().SingleOrDefault(b => b.text == name && Visible(b));
        Check(button != null, "button available: " + name);
        Submit(button);
    }

    private static void Submit(Button button)
    {
        using var submit = NavigationSubmitEvent.GetPooled();
        submit.target = button;
        button.SendEvent(submit);
    }

    private static bool HasText(string value) => Root.Query<Label>().ToList().Any(t => Visible(t) && t.text.Contains(value));

    private static int PinCount() => Root.Query<VisualElement>(className: "map-pin").ToList().Count(Visible);
    private static void Wait(int next)
    {
        waitUntil = EditorApplication.timeSinceStartup + .3; stage = next;
    }
    private static void KillAll()
    {
        foreach (EnemyBumper enemy in run.LivingEnemyObjects.ToArray())
            while (!enemy.IsDefeated) enemy.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
    }
    private static void CheckHud()
    {
        var healthRow = Root.Q("health");
        Check(Root.Q("score") == null, "combat score label is gone");
        Check(healthRow.parent.IndexOf(Root.Q("combat-dash")) < healthRow.parent.IndexOf(healthRow)
            && Root.Q("combat-dash").parent == healthRow.parent, "Dash is above the health row");
        var dashUses = Root.Q("dash-uses");
        Check(healthRow.childCount == 5 && dashUses.parent == healthRow.parent
            && dashUses.parent.IndexOf(dashUses) > healthRow.parent.IndexOf(healthRow), "Dash circles are below five hearts");
        Check(dashUses.childCount == run.Player.DashUsesPerRun && dashUses.childCount == 4,
            "Dash circle count matches the configured total");
        Check(Root.Q("map").Query<Label>().ToList().Count == 0, "minimap has no labels");
        Check(Root.Q<Label>("timer").text.EndsWith("s remaining") && !HasText("starting enemies"), "objective shows timer without enemy count");
        Check(!HasText("DIZZINESS") && !HasText("READY TO BONK"), "old bottom status text is absent");
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
    private static TrailRenderer[][] SpeedTrails(BouncingBallController ball)
    {
        var controller = ball.GetComponent<PlayerSpeedTrailController>();
        Check(controller != null, "player has the speed trail controller");
        var settings = new SerializedObject(controller);
        return new[] { "lowSpeedTrail", "mediumSpeedTrail", "highSpeedTrail" }.Select(field =>
        {
            var root = settings.FindProperty(field).objectReferenceValue as GameObject;
            Check(root != null && root.transform.IsChildOf(ball.transform), field + " attached to ball");
            var renderers = root.GetComponentsInChildren<TrailRenderer>(true);
            Check(renderers.Length > 0 && renderers.All(trail => trail.sharedMaterial != null), field + " has renderers and materials");
            return renderers;
        }).ToArray();
    }

    private static void ValidateSpeedTrails(BouncingBallController ball)
    {
        var groups = SpeedTrails(ball);
        float[] speeds = { 0f, ball.SpeedSettings.SmallEnemyKillSpeed / 2f,
            ball.SpeedSettings.SmallEnemyKillSpeed, ball.SpeedSettings.MaximumSpeed };
        for (int i = 0; i < speeds.Length; i++)
        {
            ball.EnterBallMode(Vector3.right * speeds[i]);
            ball.SendMessage("LateUpdate");
            for (int tier = 0; tier < groups.Length; tier++)
                Check(groups[tier].All(trail => trail.emitting == (tier == i - 1)), "trail tier matches speed " + speeds[i]);
        }
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
        ValidateSpeedTrails(ball);
        run.Player.ResetForEncounter(start, Quaternion.identity);
        Check(SpeedTrails(ball).SelectMany(group => group).All(trail => !trail.emitting), "robot form clears all trail emitters");
        Check(!ball.GetComponentsInChildren<LineRenderer>(true).Any(r => r.name == "Attack halo" && r.enabled), "robot form clears halo");
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
            if (EditorApplication.timeSinceStartup >= deadline)
                Check(false, $"timeout at stage {stage}; practice speed={practicePlayer?.CurrentSpeed:0.00}, ball={practicePlayer?.IsBallForm}, W={practiceKeyboard?.wKey.isPressed}");
            if (stage >= 7) { TickPractice(); return; }
            if (ui == null) ui = UnityEngine.Object.FindFirstObjectByType<CombatPresentation>();
            if (ui == null || frames++ < 6) return;
            run = ui.GetComponent<EnemyWaveController>();
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            switch (stage)
            {
                case 0:
                    Check(ui.Page == CombatPage.MainMenu && Time.timeScale == 0 && run.Player.IsEncounterLocked, "launch menu locks game");
                    var menu = Root.Q("menu");
                    Check(menu.parent == Root.Q("modal").parent && !Visible(Root.Q("modal")), "menu is separate from the modal");
                    Check(Mathf.Approximately(menu.layout.width, menu.parent.layout.width)
                        && Mathf.Approximately(menu.layout.height, menu.parent.layout.height), "menu fills the Game view");
                    Check(menu.Query<Label>().ToList().Select(label => label.text).SequenceEqual(new[] { "BONK SQUAD" }), "main menu only has the game title and buttons");
                    var menuButtons = menu.Query<Button>().ToList().Select(button => button.text);
                    Check(menuButtons.SequenceEqual(new[] { "Start", "Tutorial" }), "main menu has Start and Tutorial");
                    Click("Start");
                    Check(ui.Page == CombatPage.Combat && run.IsCombatActive, "Start enters combat directly");
                    Wait(1); break;
                case 1:
                    Click("Help");
                    Check(ui.IsTourOpen && ui.TutorialIndex == 0, "Help opens the controls guide");
                    for (int i = 0; i < 4; i++) Click("Next");
                    Check(ui.TutorialIndex == 4 && HasText("automatically"), "charge needs no activation key");
                    Click("Back"); Check(ui.TutorialIndex == 3, "guide back"); Click("Next"); Click("Done");
                    Check(ui.Page == CombatPage.Combat && run.IsCombatActive, "Done resumes combat");
                    ValidatePredictablePatrols();
                    Check(run.WaveCount == 5 && run.BasicMinionsPerWave == 4 && run.CurrentWave.count == 0, "gentle first-wave roster");
                    Check(run.MinionRadius >= .49f * run.Placement.Diameter, "wider minion hit area");
                    var settings = new PlayerDizzinessSettings();
                    Check(Mathf.Abs(settings.GainRate(run.Player.SpeedSettings.MaximumSpeed, run.Player.SpeedSettings) - 10f) < .01f, "dizziness gain halved");
                    Check(run.IsCombatActive && run.LivingEnemies == 4 && ui.Page == CombatPage.Combat, "start releases locks");
                    Check(run.LivingEnemyObjects.All(e => e.UsesStraightPatrol) && run.LivingEnemyObjects.Select(e => e.PatrolDirection).Distinct().Count() == 4,
                        "first wave gets four distinct readable patrol axes");
                    ui.SendMessage("Update");
                    CheckHud();
                    var dashButton = Root.Q<Button>("dash-button");
                    Check(Visible(dashButton) && !dashButton.enabledSelf, "Dash is visible but disabled in robot form");
                    run.Player.ToggleForm();
                    ui.SendMessage("Update");
                    Check(dashButton.enabledSelf, "Dash is available with a target in ball form");
                    int uses = run.Player.DashUsesRemaining;
                    Click(dashButton.text);
                    ui.SendMessage("Update");
                    Check(run.Player.DashUsesRemaining == uses - 1 && !dashButton.enabledSelf
                        && run.Player.DashCooldownRemaining > 0f, "HUD Dash consumes a use and starts cooldown");
                    var dashUses = Root.Q("dash-uses");
                    Check(dashUses.Children().Count(indicator => indicator.ClassListContains("dash-use-spent")) == 1,
                        "Dash empties one circle without removing its slot");
                    Check(!Root.Q<Label>("dash-status").text.Contains("/"), "Dash status has no numeric use counter");
                    run.Player.ResetDashForRun();
                    run.Player.ToggleForm();
                    Check(PinCount() == 5, "player and all starting minions have visible map pins");
                    run.DropRandom = () => .99d;
                    Check(run.TryFindMinionSpawn(out Vector3 childPoint), "safe reinforcement location");
                    var child = run.SpawnMinion(childPoint); Check(child.UsesStraightPatrol, "reinforcements use predictable patrols"); ui.SendMessage("Update");
                    Check(PinCount() == 6, "spawned minion appears on map immediately");
                    child.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed, run.Player.SpeedSettings);
                    ui.SendMessage("Update");
                    Check(PinCount() == 5, "defeated reinforcement leaves map");
                    ValidatePinballAssistance();
                    Check(Mathf.Abs(run.Player.DizzinessSettings.GainRate(run.Player.SpeedSettings.MaximumSpeed, run.Player.SpeedSettings) - 10f) < .01f, "scene dizziness gain halved");
                    run.Health.TryDamageFrom(run.Player.ActivePosition - Vector3.right);
                    ui.SendMessage("Update");
                    Check(Root.Q("health").Query<VisualElement>(className: "empty").ToList().Count == 1, "damage empties a heart");
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
                    ui.SendMessage("Update");
                    Check(run.Scoring.Progress == 50 && run.Scoring.ImpactCharges == 0, "partial Impact still accumulates");
                    run.Scoring.RegisterBounce(Time.time); run.Scoring.RecordKill(false, Time.time);
                    run.Scoring.RegisterBounce(Time.time); run.Scoring.RecordKill(false, Time.time);
                    ui.SendMessage("Update");
                    Check(run.Scoring.ImpactCharges == 2 && Root.Q("dash-uses").Children().All(indicator => !indicator.ClassListContains("dash-use-spent")),
                        "earning Impact charges does not affect Dash circles");
                    Property(run.Player, "DizzinessPhase", PlayerDizzinessPhase.Normal); run.Health.ResetHealth();
                    Property(run, "TimeRemaining", 0f); Wait(3); break;
                case 3:
                    Check(ui.Page == CombatPage.Results && run.Result.reason == WaveFailureReason.Timeout && run.Result.enemiesRemaining == 4, "timeout snapshot");
                    Check(HasText("Time ran out — 4 enemies remained"), "explicit timeout reason");
                    Click("Retry Wave"); Check(ui.Page == CombatPage.Combat && run.Health.Current == 100 && run.IsCombatActive, "retry bypasses tutorial");
                    KillAll(); Wait(4); break;
                case 4:
                    Check(run.State == EnemyWaveState.Cleared && ui.Page == CombatPage.Results, "clear results");
                    Click("Next Wave");
                    Check(ui.Page == CombatPage.Combat && run.IsCombatActive && run.WaveIndex == 1
                        && run.LivingEnemyObjects.Any(e => e.GetComponent<SpawnerEnemy>() != null), "next wave starts immediately with its specialist");
                    run.Health.TryDamage(1000); Wait(5); break;
                case 5:
                    Check(run.Result.reason == WaveFailureReason.HealthDepleted && HasText("Health reached zero"), "explicit death reason");
                    Click("Main Menu"); Check(ui.Page == CombatPage.MainMenu && run.LivingEnemies == 0, "menu cleans encounter");
                    Click("Start");
                    Check(ui.Page == CombatPage.Combat && run.IsCombatActive, "new run starts combat directly");
                    // Jump to the saved mixed definition to check the final-wave result.
                    run.RestartRun();
                    Field(run, "<WaveIndex>k__BackingField", run.WaveCount - 1);
                    run.StartWave();
                    Check(run.LivingEnemyObjects.Select(EnemyPresentationProfile.Kind).Distinct().Count() == 4, "mixed wave has all four enemy types");
                    KillAll(); Wait(6); break;
                case 6:
                    Check(ui.Page == CombatPage.Results && run.State == EnemyWaveState.Victory, "victory screen");
                    Click("Restart Run"); Check(ui.Page == CombatPage.Combat && run.WaveIndex == 0, "victory restart starts wave one");
                    ui.ShowMainMenu();
                    Click("Tutorial");
                    stage = 7; break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }
    private static void Press(params Key[] keys) => InputSystem.QueueStateEvent(practiceKeyboard, new KeyboardState(keys));

    private static bool CheckTutorialKeycaps(VisualElement page, params string[] expected)
    {
        var hints = page.Q(className: "key-hint-row");
        Check(hints != null, "lesson has an input hint template");
        if (!(hints.layout.width > 0f)) return false;

        var labels = hints.Query<VisualElement>(className: "key-hint").ToList()
            .Where(group => group.resolvedStyle.display != DisplayStyle.None)
            .SelectMany(group => group.Query<Label>(className: "keycap").ToList())
            .Select(key => key.text);
        Check(labels.SequenceEqual(expected), "lesson shows only its supported keycaps: " + page.name);
        return true;
    }

    private static void TickPractice()
    {
        if (stage == 27)
        {
            if (SceneManager.GetActiveScene().name != "Map01_RefractionArena") return;
            var arena = UnityEngine.Object.FindFirstObjectByType<CombatPresentation>();
            if (arena == null || arena.GetComponentInChildren<UIDocument>() == null) return;
            Check(arena.Page == CombatPage.MainMenu, "tutorial returns to main menu");
            Debug.Log("ONBOARDING_VALIDATION_OK: arena HUD, Dash, direct waves, and all tutorial steps with real keyboard input and wall collision.");
            Finish(0);
            return;
        }

        if (SceneManager.GetActiveScene().name != TutorialLevel.SceneName) return;
        if (practiceDocument == null)
        {
            practiceDocument = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            practicePlayer = UnityEngine.Object.FindFirstObjectByType<BonkRobotController>();
        }
        var root = practiceDocument?.rootVisualElement;
        if (root == null) return;

        switch (stage)
        {
            case 7:
                Check(Visible(root.Q("move")), "tutorial starts on movement");
                if (!CheckTutorialKeycaps(root.Q("move"), "Space", "W", "A", "S", "D")) return;
                practiceTrails = SpeedTrails(practicePlayer.GetComponentInChildren<BouncingBallController>(true));
                var pad = GameObject.Find("Directional Boost Pad").GetComponentInChildren<MeshRenderer>().bounds;
                var floorCollider = GameObject.Find("Arena Floor").GetComponent<Collider>();
                Check(floorCollider.sharedMaterial == null || floorCollider.sharedMaterial.bounciness == 0f, "tutorial floor is non-bouncy");
                var floor = floorCollider.bounds;
                Check(pad.min.x >= floor.min.x && pad.max.x <= floor.max.x && pad.min.z >= floor.min.z
                    && pad.max.z <= floor.max.z, "directional pad is inside the arena");
                originalInputSettings = InputSystem.settings;
                var testSettings = UnityEngine.Object.Instantiate(originalInputSettings);
                testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings = testSettings;
                practiceKeyboard = InputSystem.AddDevice<Keyboard>();
                practiceKeyboard.MakeCurrent();
                Press(Key.Space, Key.W);
                stage++; break;
            case 8:
                if (!Visible(root.Q("practice-completed"))) return;
                Check(Visible(root.Q("move")) && practicePlayer.CanAcceptPlayerInput && Time.timeScale > 0f,
                    "movement completion keeps the current lesson visible and leaves controls active");
                completionObservedAt = Time.unscaledTime;
                stage++; break;
            case 9:
                if (!Visible(root.Q("bounce"))) return;
                Check(Time.unscaledTime - completionObservedAt >= 4.9f && !Visible(root.Q("practice-completed")),
                    "the next lesson opens after the five-second checkmark");
                Check(practicePlayer.IsBallForm, "Space and W advance ball acceleration lesson");
                practicePlayer.ResetForEncounter(new Vector3(0f, .5f, 0f), Quaternion.identity);
                practicePlayer.ToggleForm();
                Press(Key.W);
                stage++; break;
            case 10:
                if (!Visible(root.Q("bounce")) || !Visible(root.Q("practice-completed"))) return;
                Check(practicePlayer.CurrentPlanarVelocity.z < 0, "wall collision reflected the ball");
                var body = practicePlayer.GetComponentInChildren<BouncingBallController>().GetComponent<Rigidbody>();
                Check(Mathf.Abs(body.linearVelocity.y) < .2f, "ball rolls on the ground after hitting the wall");
                Press();
                stage++; break;
            case 11:
                if (!Visible(root.Q("low-speed-hit"))) return;
                var demo = root.Q("tutorial-demo");
                var demoFrame = demo.Q<Image>();
                if (demoFrame.image == null || demo.Q<Label>("tutorial-demo-caption").text != "Low-speed bounce") return;
                Check(Visible(demo) && demoFrame.image.name == "Practice-3-Ice", "the second low-speed clip plays in the demo HUD");
                collisionTarget = StartCollisionPractice(4f);
                stage++; break;
            case 12:
                if (!Visible(root.Q("practice-completed"))) return;
                Check(collisionTarget != null && collisionTarget.HitPoints == collisionTarget.MaximumHitPoints
                    && practicePlayer.CurrentPlanarVelocity.z < 0f, "low-speed contact reflects without damage");
                stage++; break;
            case 13:
                if (!Visible(root.Q("high-speed-hit"))) return;
                var highSpeedDemo = root.Q("tutorial-demo");
                if (highSpeedDemo.Q<Label>("tutorial-demo-caption").text != "High-speed breakthrough") return;
                Check(highSpeedDemo.Q<Image>().image?.name == "Practice-4-Cosmos", "the second high-speed clip plays in the demo HUD");
                var trailHint = root.Q<Label>("enemy-trail-hints").text;
                Check(trailHint.Contains("Cosmos or Dark for small enemies") && trailHint.Contains("Large enemies need the faster Dark trail"),
                    "high-speed practice describes the trails for small and large enemies");
                StartCollisionPractice(practicePlayer.SpeedSettings.SmallEnemyKillSpeed + 1f);
                stage++; break;
            case 14:
                if (!Visible(root.Q("high-speed-hit")) || !Visible(root.Q("practice-completed"))) return;
                Check(practicePlayer.CurrentPlanarVelocity.z > 0f
                    && UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None).All(enemy => enemy.IsDefeated),
                    "high-speed contact defeats the target and preserves forward momentum");
                stage++; break;
            case 15:
                if (!Visible(root.Q("stun"))) return;
                if (practicePlayer.DizzinessPhase == PlayerDizzinessPhase.Normal && practicePlayer.CurrentSpeed < practicePlayer.SpeedSettings.LargeEnemyKillSpeed)
                    practicePlayer.GetComponentInChildren<BouncingBallController>()
                        .EnterBallMode(practicePlayer.CurrentPlanarVelocity.normalized * practicePlayer.SpeedSettings.MaximumSpeed);
                if (!Visible(root.Q("stunned"))) return;
                Check(!practicePlayer.IsBallForm && !practicePlayer.CanAcceptPlayerInput, "actual stun switches form and locks controls");
                stage++; break;
            case 16:
                if (!Visible(root.Q("recover")) || !CheckTutorialKeycaps(root.Q("recover"), "Space")) return;
                Press(Key.Space, Key.W);
                stage++; break;
            case 17:
                if (!practicePlayer.IsBallForm) return;
                if (practicePlayer.DizzinessRatio < .15f)
                {
                    var direction = practicePlayer.CurrentPlanarVelocity.normalized;
                    if (direction == Vector3.zero) direction = Vector3.forward;
                    if (practicePlayer.CurrentSpeed < practicePlayer.SpeedSettings.SmallEnemyKillSpeed)
                        practicePlayer.GetComponentInChildren<BouncingBallController>()
                            .EnterBallMode(direction * practicePlayer.SpeedSettings.MaximumSpeed);
                    return;
                }
                Check(practicePlayer.DizzinessPhase == PlayerDizzinessPhase.Normal && !Visible(root.Q("dizziness-countdown")), "partial dizziness does not require the warning countdown");
                Press(Key.W);
                stage++; break;
            case 18:
                Press(Key.Space, Key.D);
                stage++; break;
            case 19:
                if (!Visible(root.Q("dash")) || !CheckTutorialKeycaps(root.Q("dash"), "Space", "Q")) return;
                Check(!practicePlayer.IsBallForm && practicePlayer.DizzinessPhase == PlayerDizzinessPhase.Normal, "switching with partial dizziness advances to Dash after the checkmark");
                Check(root.Q("tutorial-demo").Q<Image>().image?.name == "Practice-7-Dash", "Dash plays its own demo clip");
                Check(root.Q("dash-uses").childCount == practicePlayer.DashUsesPerRun
                    && !root.Q<Label>("dash-status").text.Contains("/"), "tutorial Dash HUD uses the arena's circles and status");
                var enemies = UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None);
                Check(enemies.Length == 2 && enemies.Count(e => e.IsSmallEnemy) == 1, "one basic small and one basic large enemy");
                practiceDashStart = new Vector3(0f, practicePlayer.ActivePosition.y, -5f);
                practicePlayer.ResetForEncounter(practiceDashStart, Quaternion.identity);
                practicePlayer.RestoreDashCheckpoint(new BonkRobotController.DashCheckpoint(1, 0f));
                // Put both targets behind the practice wall so the first Dash cannot reach them.
                for (int i = 0; i < enemies.Length; i++)
                    enemies[i].transform.position = new Vector3(0f, enemies[i].transform.position.y, 11f + i * 2f);
                Physics.SyncTransforms();
                Press();
                stage++; break;
            case 20:
                Press(Key.Space);
                stage++; break;
            case 21:
                if (!root.Q<Button>("dash-button").enabledSelf) return;
                practiceDashAttemptedAt = Time.time;
                Press(Key.Q);
                stage++; break;
            case 22:
                if (Time.time - practiceDashAttemptedAt < 1f) return;
                Check(practicePlayer.DashUsesRemaining == 0 && Visible(root.Q("dash")), "a Dash blocked by the wall does not finish practice");
                practicePlayer.ResetForEncounter(practiceDashStart, Quaternion.identity);
                practiceDashTarget = UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None).First(e => e.IsSmallEnemy);
                practiceDashTarget.transform.position = new Vector3(0f, practiceDashTarget.transform.position.y, 2f);
                Physics.SyncTransforms();
                Press();
                stage++; break;
            case 23:
                if (practicePlayer.DashUsesRemaining == 0) return;
                Check(practicePlayer.DashUsesRemaining == practicePlayer.DashUsesPerRun, "practice refills spent Dash uses");
                Press(Key.Space);
                stage++; break;
            case 24:
                if (!root.Q<Button>("dash-button").enabledSelf) return;
                Press(Key.Q);
                stage++; break;
            case 25:
                if (practicePlayer.DashUsesRemaining == practicePlayer.DashUsesPerRun) return;
                Check(Visible(root.Q("dash")) && practiceDashTarget != null && !practiceDashTarget.IsDefeated, "releasing Dash alone does not finish practice");
                stage++; break;
            case 26:
                sawPracticeDashTrail |= practiceTrails[2].Any(trail => trail.emitting);
                if (!Visible(root.Q("complete"))) return;
                Check(sawPracticeDashTrail, "tutorial dash plays the high-speed trail before completion");
                Check(practiceDashTarget == null || practiceDashTarget.IsDefeated, "Dash physically hits the practice target before completion");
                Press();
                Submit(root.Q<Button>("tutorial-done"));
                stage++; break;
        }
    }

    private static EnemyBumper StartCollisionPractice(float speed)
    {
        var target = UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None).Single();
        target.transform.position = new Vector3(0f, target.transform.position.y, 0f);
        practicePlayer.ResetForEncounter(new Vector3(0f, .5f, -2f), Quaternion.identity);
        practicePlayer.ToggleForm();
        practicePlayer.GetComponentInChildren<BouncingBallController>().EnterBallMode(Vector3.forward * speed);
        Physics.SyncTransforms();
        return target;
    }

    private static void Finish(int code)
    {
        if (practiceKeyboard != null) InputSystem.RemoveDevice(practiceKeyboard);
        if (originalInputSettings != null)
        {
            var testSettings = InputSystem.settings;
            InputSystem.settings = originalInputSettings;
            UnityEngine.Object.Destroy(testSettings);
        }
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick;
        if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
