using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Map01CombatSetup
{
    public const string ScenePath = "Assets/Scenes/levels/Map01_RefractionArena.unity";
    public const string Folder = "Assets/Prefabs/Combat";

    [MenuItem("Tools/BonkSquad/Setup Map01 Enemy Waves")]
    public static void Setup()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = UnityEngine.Object.FindFirstObjectByType<BonkRobotController>();
        var ball = player.GetComponentInChildren<BouncingBallController>(true);
        var sphere = ball.GetComponent<SphereCollider>();
        float d = sphere.radius * 2f * Mathf.Max(Mathf.Abs(ball.transform.lossyScale.x), Mathf.Abs(ball.transform.lossyScale.y), Mathf.Abs(ball.transform.lossyScale.z));
        Collider floor = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Single(c => c.name == "Arena Floor");
        foreach (var existing in UnityEngine.Object.FindObjectsByType<EnemyWaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        foreach (var existing in UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        var oldEnemies = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Enemies");
        if (oldEnemies != null) UnityEngine.Object.DestroyImmediate(oldEnemies);
        foreach (var oldPickup in UnityEngine.Object.FindObjectsByType<HealthPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(oldPickup.gameObject);
        foreach (var oldPickup in UnityEngine.Object.FindObjectsByType<SpeedPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(oldPickup.gameObject);

        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        Material green = Material("Spawner Green", new Color(.28f, .8f, .2f));
        Material orange = Material("Mine Layer Orange", new Color(1f, .4f, .04f));
        Material purple = Material("Mortar Purple", new Color(.65f, .22f, .95f));
        Material minionMaterial = Material("Minion Lime", new Color(.8f, .9f, .18f));
        Material dark = Material("Combat Dark", new Color(.07f, .1f, .13f));
        Material white = Material("Healing White", Color.white);
        Material heal = Material("Healing Teal", new Color(.05f, 1f, .65f));
        Material marker = Material("Warning Ring", Color.white, true);
        Vector2 minimum = new Vector2(floor.bounds.min.x + d, floor.bounds.min.z + d);
        Vector2 maximum = new Vector2(floor.bounds.max.x - d, floor.bounds.max.z - d);
        EnemyBumper spawner = MakeEnemy("Spawner", d, green, dark, 0, minimum, maximum);
        EnemyBumper layer = MakeEnemy("MineLayer", d, orange, dark, 1, minimum, maximum);
        EnemyBumper mortar = MakeEnemy("Mortar", d, purple, dark, 2, minimum, maximum);
        EnemyBumper minion = MakeEnemy("Minion", d, minionMaterial, dark, 3, minimum, maximum);
        MakeEnemy("NetShooter", d, Material("Net Cyan", Color.cyan), dark, 4, minimum, maximum);
        MakeEnemy("Runner", d, Material("Runner Red", new Color(1f,.2f,.35f)), dark, 5, minimum, maximum);
        CombatHazard mine = MakeHazard("Mine", d, orange, true);
        CombatHazard shell = MakeHazard("MortarShell", d, purple, false);
        var pickupObject = new GameObject("HealthPickup");
        var trigger = pickupObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true; trigger.radius = .35f * d; trigger.center = Vector3.up * d * .5f;
        var body = pickupObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        Model(pickupObject.transform, "Healing capsule", PrimitiveType.Sphere, Vector3.up * .5f * d, Vector3.one * .65f * d, heal);
        Model(pickupObject.transform, "Plus horizontal", PrimitiveType.Cube, new Vector3(0, .85f, 0) * d, new Vector3(.8f, .1f, .2f) * d, white);
        Model(pickupObject.transform, "Plus vertical", PrimitiveType.Cube, new Vector3(0, .85f, 0) * d, new Vector3(.2f, .1f, .8f) * d, white);
        pickupObject.AddComponent<HealthPickup>();
        HealthPickup pickup = Save(pickupObject, "HealthPickup").GetComponent<HealthPickup>();
        SpeedPickup speedPickup = MakeSpeedPickup(d);
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null) health = player.gameObject.AddComponent<PlayerHealth>();
        var encounter = new GameObject("Map01 Enemy Waves");
        if (player.GetComponent<PlayerDamageFeedback>() == null) player.gameObject.AddComponent<PlayerDamageFeedback>();
        var placement = encounter.AddComponent<CombatPlacement>();
        // These four broad strips form a connected ring outside the central obstacle groups.
        // SphereCollider uses the largest world scale, including Y. Derive the strips from
        // the existing floor bounds so a nonuniform player scale never resizes the arena.
        Bounds bounds = floor.bounds;
        float halfX = bounds.extents.x, halfZ = bounds.extents.z;
        float cx = bounds.center.x, cz = bounds.center.z;
        Rect[] regions = {
            new Rect(cx-.875f*halfX, cz-.875f*halfZ, .25f*halfX, 1.75f*halfZ),
            new Rect(cx+.625f*halfX, cz-.875f*halfZ, .25f*halfX, 1.75f*halfZ),
            new Rect(cx-.625f*halfX, cz+.625f*halfZ, 1.25f*halfX, .25f*halfZ),
            new Rect(cx-.625f*halfX, cz-.875f*halfZ, 1.25f*halfX, .25f*halfZ)
        };
        Vector3[] fallbacks = {
            new Vector3(cx-.78125f*halfX, 0, cz-.78125f*halfZ),
            new Vector3(cx+.78125f*halfX, 0, cz-.78125f*halfZ),
            new Vector3(cx-.78125f*halfX, 0, cz+.78125f*halfZ),
            new Vector3(cx+.78125f*halfX, 0, cz+.78125f*halfZ)
        };
        for (int i = 0; i < fallbacks.Length; i++) fallbacks[i].y = floor.bounds.max.y;
        placement.Configure(floor, d, regions, fallbacks);
        var run = encounter.AddComponent<EnemyWaveController>();
        Vector3 start = player.GetComponentInChildren<PlayerController>(true).transform.position;
        Quaternion rotation = player.GetComponentInChildren<PlayerController>(true).transform.rotation;
        run.Configure(player, health, placement, TeachingWaves(spawner, layer, mortar),
            minion, mine, shell, pickup, marker, start, rotation);
        run.ConfigurePowerups(speedPickup);
        encounter.AddComponent<CombatPresentation>();
        encounter.AddComponent<CombatHUD>();
        InstallBats(run, dark);
        Physics.SyncTransforms();
        ValidatePlacement(placement, start);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MAP01_COMBAT_SETUP_OK: four waves including all enemy types, reusable prefabs, random placement and 5% healing drops.");
    }

    public static EnemyWaveController.WaveDefinition[] TeachingWaves(EnemyBumper spawner, EnemyBumper layer, EnemyBumper mortar,
        EnemyBumper net = null, EnemyBumper runner = null)
    {
        net = net != null ? net : AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/NetShooter.prefab");
        runner = runner != null ? runner : AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Runner.prefab");
        return new[] {
            new EnemyWaveController.WaveDefinition { title = "Mortar", prefab = mortar, count = 1, basicMinions = 3, minionSpeedInDiameters = .9f },
            new EnemyWaveController.WaveDefinition { title = "Blasts and mines", prefab = mortar, count = 1, basicMinions = 3, additionalGroups = new[] { new EnemyWaveController.EnemyGroup { prefab = layer } } },
            new EnemyWaveController.WaveDefinition { title = "Spawn and snare", prefab = spawner, count = 1, basicMinions = 4, additionalGroups = new[] { new EnemyWaveController.EnemyGroup { prefab = net } } },
            new EnemyWaveController.WaveDefinition { title = "Moving targets", prefab = mortar, count = 1, basicMinions = 4, additionalGroups = new[] {
                new EnemyWaveController.EnemyGroup { prefab = layer }, new EnemyWaveController.EnemyGroup { prefab = runner } } },
            new EnemyWaveController.WaveDefinition { title = "Final pursuit", prefab = spawner, count = 1, basicMinions = 4, additionalGroups = new[] {
                new EnemyWaveController.EnemyGroup { prefab = net }, new EnemyWaveController.EnemyGroup { prefab = runner } } }
        };
    }

    [MenuItem("Tools/BonkSquad/Install Pinball Expansion")]
    public static void InstallPinballExpansion()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var run = UnityEngine.Object.FindFirstObjectByType<EnemyWaveController>();
        if (run == null) throw new InvalidOperationException("Set up enemy waves first.");
        float d = run.Placement.Diameter;
        var dark = Material("Combat Dark", new Color(.07f,.1f,.13f));
        var net = MakeEnemy("NetShooter", d, Material("Net Cyan", Color.cyan), dark, 4, run.Placement.ArenaMin, run.Placement.ArenaMax);
        var runner = MakeEnemy("Runner", d, Material("Runner Red", new Color(1f,.2f,.35f)), dark, 5, run.Placement.ArenaMin, run.Placement.ArenaMax);
        var spawner = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Spawner.prefab");
        var layer = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/MineLayer.prefab");
        var mortar = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Mortar.prefab");
        typeof(EnemyWaveController).GetField("waves", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(run, TeachingWaves(spawner, layer, mortar, net, runner));
        var shell = PrefabUtility.LoadPrefabContents(Folder + "/MortarShell.prefab");
        var shellData = new SerializedObject(shell.GetComponent<CombatHazard>());
        shellData.FindProperty("radiusInDiameters").floatValue = 2.75f;
        shellData.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(shell, Folder + "/MortarShell.prefab"); PrefabUtility.UnloadPrefabContents(shell);
        var playerData = new SerializedObject(run.Player); playerData.FindProperty("initialDashUses").intValue = 4;
        playerData.ApplyModifiedPropertiesWithoutUndo();
        InstallBats(run, dark);
        EditorUtility.SetDirty(run); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("PINBALL_EXPANSION_INSTALLED");
    }
    private static void InstallBats(EnemyWaveController run, Material dark)
    {
        foreach (var bat in run.GetComponentsInChildren<RotatingPinballBat>()) UnityEngine.Object.DestroyImmediate(bat.gameObject);
        Physics.SyncTransforms();
        float d = run.Placement.Diameter; Bounds floor = run.Placement.Floor.bounds;
        var candidates = new List<Vector3>();
        for (float x = floor.min.x + 5*d; x < floor.max.x-5*d; x += 3*d)
            for (float z = floor.min.z + 5*d; z < floor.max.z-5*d; z += 3*d)
                candidates.Add(new Vector3(x, floor.max.y, z));
        candidates = candidates.OrderBy(p => Mathf.Abs(Mathf.Abs(p.x-floor.center.x)-floor.extents.x*.55f) + Mathf.Abs(p.z-floor.center.z)).ToList();
        int placed = 0;
        var batPositions = new List<Vector3>();
        var cyan = Material("Bat Cyan", new Color(.15f,1f,1f));
        foreach (Vector3 point in candidates)
        {
            if (PlayerMovementMath.Planar(point-run.Player.ActivePosition).magnitude < 7*d ||
                batPositions.Any(p => PlayerMovementMath.Planar(p-point).magnitude < 14*d) ||
                !run.Placement.IsClear(point, 4*d, 3*d)) continue;
            var go = new GameObject("Rotating bat " + (placed+1)); go.transform.SetParent(run.transform, false); go.transform.position = point;
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var collider = go.AddComponent<BoxCollider>(); collider.center = Vector3.up*.6f*d; collider.size = new Vector3(5*d, 1.2f*d, .45f*d);
            Model(go.transform, "Rod", PrimitiveType.Cube, collider.center, collider.size, cyan);
            Model(go.transform, "Pivot", PrimitiveType.Cylinder, Vector3.up*.6f*d, new Vector3(.7f, .65f, .7f)*d, dark);
            Model(go.transform, "Launch arrow shaft", PrimitiveType.Cube, new Vector3(0,.7f,1.1f)*d, new Vector3(.14f,.14f,1.6f)*d, cyan);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var wing = Model(go.transform, "Arrow head", PrimitiveType.Cube, new Vector3(sign*.3f,.7f,1.65f)*d, new Vector3(.14f,.14f,.9f)*d, cyan);
                wing.localRotation = Quaternion.Euler(0, sign*45, 0);
            }
            var bat = go.AddComponent<RotatingPinballBat>(); bat.Configure(run); bat.SetReservedRadius(3.4f*d);
            batPositions.Add(point); Physics.SyncTransforms(); placed++;
            if (placed == 4) break;
        }
        if (placed != 4) throw new InvalidOperationException("Could not place four bats with clear sweep space.");
    }

    [MenuItem("Tools/BonkSquad/Apply Expanded Pinball Arena")]
    public static void ApplyExpandedPinballArena()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var run = UnityEngine.Object.FindFirstObjectByType<EnemyWaveController>();
        if (run == null) throw new InvalidOperationException("Map01 needs an EnemyWaveController.");
        GameObject arena = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Map01_RefractionArena");
        if (arena == null) throw new InvalidOperationException("Map01 arena root was not found.");

        arena.transform.localScale = new Vector3(1.35f, 1f, 1.35f);
        Physics.SyncTransforms();
        run.Placement.SetAuthoredCoordinateScale(1.35f);
        SpeedPickup speedPickup = AssetDatabase.LoadAssetAtPath<SpeedPickup>(Folder + "/SpeedPickup.prefab");
        if (speedPickup == null) speedPickup = MakeSpeedPickup(run.Placement.Diameter);
        run.ConfigurePowerups(speedPickup);

        var runData = new SerializedObject(run);
        runData.FindProperty("scoreGoalPerWave").intValue = 750;
        runData.FindProperty("healthDropChance").floatValue = .12f;
        runData.FindProperty("speedDropChance").floatValue = .18f;
        SerializedProperty scoring = runData.FindProperty("scoring");
        scoring.FindPropertyRelative("minionPoints").intValue = 25;
        scoring.FindPropertyRelative("specialistPoints").intValue = 100;
        runData.ApplyModifiedPropertiesWithoutUndo();

        InstallBats(run, Material("Combat Dark", new Color(.07f, .1f, .13f)));
        EditorUtility.SetDirty(run);
        EditorUtility.SetDirty(run.Placement);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("EXPANDED_PINBALL_ARENA_OK: 35% larger arena, four blades, 750-point waves, health and speed drops.");
    }

    [MenuItem("Tools/BonkSquad/Apply Escalating Enemy Waves")]
    public static void ApplyEscalatingEnemyWaves()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var run = UnityEngine.Object.FindFirstObjectByType<EnemyWaveController>();
        if (run == null) throw new InvalidOperationException("Map01 needs an EnemyWaveController.");

        EnemyBumper runner = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Runner.prefab");
        EnemyBumper net = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/NetShooter.prefab");
        EnemyBumper mortar = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Mortar.prefab");
        EnemyBumper layer = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/MineLayer.prefab");
        EnemyBumper spawner = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Spawner.prefab");

        var runnerRoot = PrefabUtility.LoadPrefabContents(Folder + "/Runner.prefab");
        var runnerData = new SerializedObject(runnerRoot.GetComponent<RunnerEnemy>());
        runnerData.FindProperty("baseSpeedInDiameters").floatValue = 1.25f;
        runnerData.FindProperty("speedPerLostSegment").floatValue = 1f;
        runnerData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(runnerRoot, Folder + "/Runner.prefab");
        PrefabUtility.UnloadPrefabContents(runnerRoot);

        var field = typeof(EnemyWaveController).GetField("waves",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var existing = (EnemyWaveController.WaveDefinition[])field.GetValue(run);
        var waves = existing.Take(2).Concat(new[]
        {
            new EnemyWaveController.WaveDefinition
            {
                title = "Pressure circuit", prefab = runner, count = 4, basicMinions = 16,
                minionSpeedInDiameters = 1.35f, seconds = 150f,
                additionalGroups = new[]
                {
                    new EnemyWaveController.EnemyGroup { prefab = net, count = 3 },
                    new EnemyWaveController.EnemyGroup { prefab = mortar, count = 3 },
                    new EnemyWaveController.EnemyGroup { prefab = layer, count = 3 },
                    new EnemyWaveController.EnemyGroup { prefab = spawner, count = 1 }
                }
            },
            new EnemyWaveController.WaveDefinition
            {
                title = "Final overdrive", prefab = runner, count = 6, basicMinions = 18,
                minionSpeedInDiameters = 1.5f, seconds = 120f,
                additionalGroups = new[]
                {
                    new EnemyWaveController.EnemyGroup { prefab = net, count = 4 },
                    new EnemyWaveController.EnemyGroup { prefab = mortar, count = 4 },
                    new EnemyWaveController.EnemyGroup { prefab = layer, count = 4 },
                    new EnemyWaveController.EnemyGroup { prefab = spawner, count = 2 }
                }
            },
            new EnemyWaveController.WaveDefinition
            {
                title = "Crossfire maze", prefab = spawner, count = 3, basicMinions = 20,
                minionSpeedInDiameters = 1.6f, seconds = 115f,
                additionalGroups = new[]
                {
                    new EnemyWaveController.EnemyGroup { prefab = runner, count = 5 },
                    new EnemyWaveController.EnemyGroup { prefab = net, count = 5 },
                    new EnemyWaveController.EnemyGroup { prefab = mortar, count = 5 },
                    new EnemyWaveController.EnemyGroup { prefab = layer, count = 4 }
                }
            },
            new EnemyWaveController.WaveDefinition
            {
                title = "Pinwheel siege", prefab = mortar, count = 6, basicMinions = 22,
                minionSpeedInDiameters = 1.75f, seconds = 100f,
                additionalGroups = new[]
                {
                    new EnemyWaveController.EnemyGroup { prefab = runner, count = 7 },
                    new EnemyWaveController.EnemyGroup { prefab = net, count = 6 },
                    new EnemyWaveController.EnemyGroup { prefab = layer, count = 5 },
                    new EnemyWaveController.EnemyGroup { prefab = spawner, count = 3 }
                }
            }
        }).ToArray();
        field.SetValue(run, waves);
        WaveArenaLayoutController layoutController = InstallWaveLayouts(run);
        var placementData = new SerializedObject(run.Placement);
        placementData.FindProperty("randomAttempts").intValue = 800;
        placementData.ApplyModifiedPropertiesWithoutUndo();
        ValidateWaveLayouts(run, layoutController, waves);
        EditorUtility.SetDirty(run);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ESCALATING_WAVES_OK: six waves, six obstacle layouts, shorter timers and escalating pressure movement.");
    }

    private static WaveArenaLayoutController InstallWaveLayouts(EnemyWaveController run)
    {
        WaveArenaLayoutController oldController = run.GetComponent<WaveArenaLayoutController>();
        if (oldController != null) UnityEngine.Object.DestroyImmediate(oldController);
        Transform oldRoot = run.transform.Find("Wave Layout Obstacles");
        if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

        Material steel = Material("Arena Modular Steel", new Color(.36f, .4f, .46f));
        var root = new GameObject("Wave Layout Obstacles").transform;
        root.SetParent(run.transform, false);
        var blocks = new Transform[8];
        for (int i = 0; i < blocks.Length; i++)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Layout block " + (i + 1);
            block.transform.SetParent(root, false);
            block.GetComponent<MeshRenderer>().sharedMaterial = steel;
            blocks[i] = block.transform;
            block.SetActive(false);
        }

        WaveArenaLayoutController.ArenaLayout Layout(string name, params WaveArenaLayoutController.BlockPose[] poses)
            => new WaveArenaLayoutController.ArenaLayout { name = name, blocks = poses };
        WaveArenaLayoutController.BlockPose B(float x, float z, float width, float depth, float yaw = 0f)
            => new WaveArenaLayoutController.BlockPose(x, z, width, depth, yaw);

        var layouts = new[]
        {
            Layout("Split gates", B(-12, 9, 9, 1.4f), B(12, -9, 9, 1.4f), B(-18, -15, 1.4f, 7), B(18, 15, 1.4f, 7)),
            Layout("Slalom", B(-18, -10, 1.4f, 8), B(-7, 10, 1.4f, 8), B(7, -10, 1.4f, 8), B(18, 10, 1.4f, 8)),
            Layout("Diamond lanes", B(-11, 11, 8, 1.3f, 45), B(11, 11, 8, 1.3f, -45), B(-11, -11, 8, 1.3f, -45), B(11, -11, 8, 1.3f, 45)),
            Layout("Crossfire", B(0, 16, 11, 1.3f), B(0, -16, 11, 1.3f), B(-16, 0, 1.3f, 11), B(16, 0, 1.3f, 11), B(-20, 20, 5, 1.2f, 45), B(20, -20, 5, 1.2f, 45)),
            Layout("Corner traps", B(-18, 18, 7, 1.3f, 45), B(18, 18, 7, 1.3f, -45), B(-18, -18, 7, 1.3f, -45), B(18, -18, 7, 1.3f, 45), B(-10, 0, 5, 1.2f), B(10, 0, 5, 1.2f)),
            Layout("Pinwheel", B(-8, 5, 9, 1.3f, 25), B(8, -5, 9, 1.3f, 25), B(-5, -8, 1.3f, 9, 25), B(5, 8, 1.3f, 9, 25), B(-19, 0, 1.2f, 6), B(19, 0, 1.2f, 6))
        };
        var controller = run.gameObject.AddComponent<WaveArenaLayoutController>();
        controller.Configure(run, blocks, layouts);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ValidateWaveLayouts(EnemyWaveController run, WaveArenaLayoutController controller,
        EnemyWaveController.WaveDefinition[] waves)
    {
        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            controller.ApplyLayout(waveIndex);
            List<EnemyBumper> roster = waves[waveIndex].SpawnList();
            float radius = 0f, height = 0f;
            foreach (EnemyBumper prefab in roster)
            {
                CapsuleCollider shape = prefab.GetComponent<CapsuleCollider>();
                radius = Mathf.Max(radius, shape.radius);
                height = Mathf.Max(height, shape.height);
            }
            var random = new System.Random(4700 + waveIndex);
            if (!run.Placement.TryWavePositions(roster.Count, run.Player.ActivePosition, radius, height,
                    random, out List<Vector3> specialists) ||
                !run.Placement.TryDistributedMinions(waves[waveIndex].basicMinions,
                    run.Player.ActivePosition, specialists, run.MinionRadius, run.MinionHeight,
                    random, out List<Vector3> minions))
                throw new InvalidOperationException($"Wave {waveIndex + 1} layout '{controller.CurrentLayoutName}' has insufficient safe spawn space.");
        }
        controller.ApplyLayout(0);
    }

    [MenuItem("Tools/BonkSquad/Apply Enemy Visual Language")]
    public static void ApplyEnemyVisualLanguage()
    {
        Material minionRed = Material("Minion Light Red", EnemyPalette.HostileCrimson);
        Material gunmetal = Material("Enemy Gunmetal", new Color(.28f, .32f, .37f));
        Material mortarMetal = Material("Mortar Metal Black", EnemyPalette.MortarMetal);
        Material spawnerArmor = Material("Spawner Burgundy", new Color(.48f, .05f, .25f));
        Material mineBrown = Material("Mine Brown", EnemyPalette.MineBrown);
        Material netDarkBlue = Material("Net Dark Blue", EnemyPalette.NetDarkBlue);
        Material spawnerSignal = Material("Spawner Signal", EnemyPalette.SpawnerSignal);
        Material explosion = Material("Explosion Red Orange", EnemyPalette.Explosion);
        Material mineDanger = Material("Mine Danger Red", EnemyPalette.MineDanger);
        Material netSignal = Material("Net Signal Cyan", EnemyPalette.Net);
        Material runnerSignal = Material("Runner Yellow", EnemyPalette.Runner);
        Material hazardYellow = Material("Hazard Stripe Yellow", new Color(1f, .72f, .04f));

        EditEnemyVisual("Spawner", root =>
        {
            SetModelMaterial(root, "Body", spawnerArmor);
            SetAllModelMaterials(root, "Spawn pod", spawnerSignal);
        });
        EditEnemyVisual("MineLayer", root =>
        {
            SetModelMaterial(root, "Body", mineBrown);
            SetModelMaterial(root, "Rear dispenser", explosion);
            RemoveChildren(root, "Role Accent");
            Model(root, "Role Accent", PrimitiveType.Cube, new Vector3(0, .72f, .51f), new Vector3(.85f, .12f, .08f), hazardYellow);
            Model(root, "Role Accent", PrimitiveType.Cube, new Vector3(0, .48f, .51f), new Vector3(.85f, .12f, .08f), mineDanger);
        });
        EditEnemyVisual("Mortar", root =>
        {
            SetModelMaterial(root, "Body", mortarMetal);
            SetModelMaterial(root, "Mortar barrel", mortarMetal);
            RemoveChildren(root, "Role Accent");
            Transform tip = Model(root, "Role Accent", PrimitiveType.Cylinder,
                new Vector3(0, 1.72f, .43f), new Vector3(.34f, .12f, .34f), explosion);
            tip.localRotation = Quaternion.Euler(35f, 0f, 0f);
            Transform piston = Model(root, "Role Accent", PrimitiveType.Cylinder,
                new Vector3(0, 1.2f, -.03f), new Vector3(.16f, .48f, .16f), explosion);
            piston.localRotation = Quaternion.Euler(35f, 0f, 0f);
        });
        EditEnemyVisual("NetShooter", root =>
        {
            SetModelMaterial(root, "Body", netDarkBlue);
            SetModelMaterial(root, "Net crossbar", netSignal);
        });
        EditEnemyVisual("Minion", root => SetModelMaterial(root, "Body", minionRed));
        EditEnemyVisual("Runner", root =>
        {
            SetModelMaterial(root, "Body", runnerSignal);
            SetAllModelMaterials(root, "Runner leg", gunmetal);
            Transform body = root.Find("Body");
            if (body != null) body.localScale = new Vector3(.85f, 1f, .95f);
            foreach (Transform child in root.Cast<Transform>().Where(child => child.name == "Runner leg"))
            {
                child.localPosition = new Vector3(Mathf.Sign(child.localPosition.x) * .45f, .3f, 0f);
                child.localScale = new Vector3(.2f, .45f, 1.05f);
            }
            CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
            collider.radius = .55f;
            var runnerData = new SerializedObject(root.GetComponent<RunnerEnemy>());
            runnerData.FindProperty("baseSpeedInDiameters").floatValue = 2.25f;
            runnerData.FindProperty("speedPerLostSegment").floatValue = 1.5f;
            runnerData.ApplyModifiedPropertiesWithoutUndo();
        });

        EditEnemyVisual("Mine", root => SetModelMaterial(root, "Visual", mineDanger));
        EditEnemyVisual("MortarShell", root => SetModelMaterial(root, "Visual", explosion));

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CombatPresentation presentation = UnityEngine.Object.FindFirstObjectByType<CombatPresentation>();
        if (presentation != null)
        {
            var field = typeof(CombatPresentation).GetField("enemyProfiles",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(presentation, EnemyPresentationProfile.Defaults());
            EditorUtility.SetDirty(presentation);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY_VISUAL_LANGUAGE_OK: crimson hostile family, role accents, slimmer faster Runner and matching action colors.");
    }

    private static void EditEnemyVisual(string prefabName, Action<Transform> edit)
    {
        string path = Folder + "/" + prefabName + ".prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        edit(root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void SetModelMaterial(Transform root, string childName, Material material)
    {
        Transform child = root.Find(childName);
        if (child != null && child.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;
    }

    private static void SetAllModelMaterials(Transform root, string childName, Material material)
    {
        foreach (Transform child in root.Cast<Transform>().Where(child => child.name == childName))
            if (child.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;
    }

    private static void RemoveChildren(Transform root, string childName)
    {
        foreach (Transform child in root.Cast<Transform>().Where(child => child.name == childName).ToArray())
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static SpeedPickup MakeSpeedPickup(float d)
    {
        var existing = AssetDatabase.LoadAssetAtPath<SpeedPickup>(Folder + "/SpeedPickup.prefab");
        if (existing != null) return existing;
        Directory.CreateDirectory(Folder);
        Material yellow = Material("Speed Yellow", new Color(1f, .82f, .05f));
        Material cyan = Material("Speed Cyan", new Color(.05f, .9f, 1f));
        var pickupObject = new GameObject("SpeedPickup");
        var trigger = pickupObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = .4f * d;
        trigger.center = Vector3.up * .55f * d;
        var body = pickupObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        Transform core = Model(pickupObject.transform, "Speed core", PrimitiveType.Cube,
            Vector3.up * .55f * d, new Vector3(.55f, .55f, .55f) * d, yellow);
        core.localRotation = Quaternion.Euler(0f, 45f, 45f);
        Model(pickupObject.transform, "Speed trail", PrimitiveType.Cube,
            new Vector3(0f, .55f, -.42f) * d, new Vector3(.18f, .18f, .65f) * d, cyan);
        pickupObject.AddComponent<SpeedPickup>();
        return Save(pickupObject, "SpeedPickup").GetComponent<SpeedPickup>();
    }

    [MenuItem("Tools/BonkSquad/Apply Map01 Teaching Balance")]
    public static void ApplyTeachingBalance()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var run = UnityEngine.Object.FindFirstObjectByType<EnemyWaveController>();
        var spawner = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Spawner.prefab");
        var layer = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/MineLayer.prefab");
        var mortar = AssetDatabase.LoadAssetAtPath<EnemyBumper>(Folder + "/Mortar.prefab");
        typeof(EnemyWaveController).GetField("waves", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(run, TeachingWaves(spawner, layer, mortar));
        var minion = PrefabUtility.LoadPrefabContents(Folder + "/Minion.prefab");
        float d = run.Placement.Diameter;
        minion.GetComponent<CapsuleCollider>().radius = .5f * d;
        Transform body = minion.transform.Find("Body");
        body.localScale = new Vector3(.95f * d, body.localScale.y, .95f * d);
        PrefabUtility.SaveAsPrefabAsset(minion, Folder + "/Minion.prefab"); PrefabUtility.UnloadPrefabContents(minion);
        var spawn = PrefabUtility.LoadPrefabContents(Folder + "/Spawner.prefab");
        var spawnData = new SerializedObject(spawn.GetComponent<SpawnerEnemy>());
        spawnData.FindProperty("livingMinionCap").intValue = 2;
        spawnData.FindProperty("totalSpawnBudget").intValue = 3;
        spawnData.FindProperty("spawnInterval").floatValue = 8f;
        spawnData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(spawn, Folder + "/Spawner.prefab"); PrefabUtility.UnloadPrefabContents(spawn);
        EditorUtility.SetDirty(run);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("TEACHING_BALANCE_APPLIED: four slow wider minions, then one specialist per wave, twice the dizziness buildup time.");
    }

    [MenuItem("Tools/BonkSquad/Install Map01 Combat UI")]
    public static void InstallPresentation()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var run = UnityEngine.Object.FindFirstObjectByType<EnemyWaveController>();
        if (run == null) throw new InvalidOperationException("Set up Map01 enemy waves first.");
        if (run.GetComponent<CombatPresentation>() == null) run.gameObject.AddComponent<CombatPresentation>();
        if (run.GetComponent<CombatHUD>() == null) run.gameObject.AddComponent<CombatHUD>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MAP01_ONBOARDING_INSTALLED");
    }

    public static void ValidatePlacement(CombatPlacement placement, Vector3 start)
    {
        float d = placement.Diameter;
        foreach (Vector3 point in placement.FallbackPositions)
            if (!placement.IsClear(point, 1.5f*d, 3f*d)) throw new InvalidOperationException("Invalid fallback: " + point);
        for (int seed = 0; seed < 250; seed++)
        {
            if (!placement.TryWavePositions(3, start, .55f*d, 1.6f*d, new System.Random(seed), out List<Vector3> points) || points.Count != 3)
                throw new InvalidOperationException("Placement failed for seed " + seed);
            if (!placement.TryDistributedMinions(12, start, points, .35f*d, d, new System.Random(seed), out List<Vector3> minions) || minions.Count != 12)
                throw new InvalidOperationException("Distributed minion placement failed for seed " + seed);
            Bounds floor = placement.Floor.bounds;
            for (int side = 0; side < 4; side++)
            {
                int quadrant = side;
                if (minions.FindAll(p => (p.x >= floor.center.x ? 1 : 0) + (p.z >= floor.center.z ? 2 : 0) == quadrant).Count < 2)
                    throw new InvalidOperationException("Minions not distributed throughout arena.");
            }
            foreach (Vector3 minion in minions)
            {
                if (!placement.IsClear(minion, .5f*d, d) || PlayerMovementMath.Planar(minion-start).magnitude < 6*d)
                    throw new InvalidOperationException("Invalid minion floor clearance.");
                foreach (Vector3 specialist in points)
                    if (PlayerMovementMath.Planar(minion-specialist).magnitude < 4*d) throw new InvalidOperationException("Minion overlaps specialist route.");
                foreach (Vector3 other in minions)
                    if (minion != other && Vector3.Distance(minion, other) < 1.399f*d) throw new InvalidOperationException("Minions overlap.");
            }
            foreach (Vector3 point in points)
            {
                if (!placement.IsClear(point, 1.5f*d, 3f*d) || PlayerMovementMath.Planar(point-start).magnitude < 6*d)
                    throw new InvalidOperationException("Invalid random placement.");
                foreach (Vector3 other in points)
                    if (point != other && Vector3.Distance(point, other) < 4*d) throw new InvalidOperationException("Enemy spacing failed.");
            }
        }
    }

    private static EnemyBumper MakeEnemy(string name, float d, Material color, Material dark, int kind, Vector2 minimum, Vector2 maximum)
    {
        var go = new GameObject(name);
        go.AddComponent<Rigidbody>();
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.height = (kind == 3 ? 1f : kind == 5 ? 2f : 1.6f) * d;
        capsule.radius = (kind == 3 ? .5f : kind == 5 ? .8f : .55f) * d;
        capsule.center = Vector3.up * capsule.height * .5f;
        var enemy = go.AddComponent<EnemyBumper>();
        enemy.ConfigureCombat(kind == 3 ? 1 : kind == 5 ? 3 : 2, true, kind == 1 ? d * 1.5f : kind == 3 ? d * 2f : 0f,
            kind == 3, minimum, maximum);
        Model(go.transform, "Body", kind == 0 ? PrimitiveType.Cylinder : kind == 1 ? PrimitiveType.Cube : PrimitiveType.Capsule,
            capsule.center, new Vector3(kind == 3 ? .95f : 1f, kind == 0 || kind == 2 || kind == 3 ? capsule.height/d*.5f : 1.2f, kind == 3 ? .95f : 1f)*d, color);
        if (kind == 0)
        {
            go.AddComponent<SpawnerEnemy>();
            for (int i = -1; i <= 1; i += 2)
                Model(go.transform, "Spawn pod", PrimitiveType.Sphere, new Vector3(i*.5f, 1.2f, 0)*d, Vector3.one*.5f*d, dark);
        }
        if (kind == 1)
        {
            go.AddComponent<MineLayerEnemy>();
            Model(go.transform, "Rear dispenser", PrimitiveType.Cube, new Vector3(0, .45f, -.6f)*d, new Vector3(.7f, .5f, .4f)*d, dark);
        }
        if (kind == 2)
        {
            go.AddComponent<MortarEnemy>();
            Transform barrel = Model(go.transform, "Mortar barrel", PrimitiveType.Cylinder, new Vector3(0, 1.4f, .2f)*d, new Vector3(.4f,.6f,.4f)*d, dark);
            barrel.localRotation = Quaternion.Euler(35, 0, 0);
        }
        if (kind == 4)
        {
            go.AddComponent<NetShooterEnemy>();
            Model(go.transform, "Net crossbar", PrimitiveType.Cube, Vector3.up*d, new Vector3(1.7f,.18f,.22f)*d, dark);
        }
        if (kind == 5)
        {
            enemy.ConfigurePatrol(MinionPatrolAxis.DiagonalUp);
            go.transform.Find("Body").localScale = new Vector3(1.5f, 1f, 1.5f)*d;
            go.AddComponent<RunnerEnemy>();
            for (int sign = -1; sign <= 1; sign += 2)
                Model(go.transform, "Runner leg", PrimitiveType.Cube, new Vector3(sign*.7f,.3f,0)*d, new Vector3(.3f,.5f,1.2f)*d, dark);
        }
        return Save(go, name).GetComponent<EnemyBumper>();
    }

    private static CombatHazard MakeHazard(string name, float d, Material material, bool mine)
    {
        var go = new GameObject(name);
        Transform visual = Model(go.transform, "Visual", mine ? PrimitiveType.Cylinder : PrimitiveType.Sphere,
            Vector3.up * .15f*d, mine ? new Vector3(.6f,.12f,.6f)*d : Vector3.one*.4f*d, material);
        var hazard = go.AddComponent<CombatHazard>();
        hazard.ConfigurePrefab(mine, visual);
        return Save(go, name).GetComponent<CombatHazard>();
    }

    private static Transform Model(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
    {
        var model = GameObject.CreatePrimitive(primitive);
        model.name = name; model.transform.SetParent(parent, false);
        model.transform.localPosition = position; model.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
        model.GetComponent<Renderer>().sharedMaterial = material;
        return model.transform;
    }
    private static GameObject Save(GameObject go, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }
    private static Material Material(string name, Color color, bool marker = false)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(marker ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .15f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
