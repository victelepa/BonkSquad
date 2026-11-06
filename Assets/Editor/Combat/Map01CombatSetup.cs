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
        encounter.AddComponent<CombatPresentation>();
        encounter.AddComponent<CombatHUD>();
        Physics.SyncTransforms();
        ValidatePlacement(placement, start);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MAP01_COMBAT_SETUP_OK: four waves including all enemy types, reusable prefabs, random placement and 5% healing drops.");
    }

    public static EnemyWaveController.WaveDefinition[] TeachingWaves(EnemyBumper spawner, EnemyBumper layer, EnemyBumper mortar) => new[] {
        new EnemyWaveController.WaveDefinition { title = "Learn to bonk", count = 0, basicMinions = 4, minionSpeedInDiameters = .65f },
        new EnemyWaveController.WaveDefinition { title = "Meet the spawner", prefab = spawner, count = 1, basicMinions = 3 },
        new EnemyWaveController.WaveDefinition { title = "Watch for mines", prefab = layer, count = 1, basicMinions = 4 },
        new EnemyWaveController.WaveDefinition { title = "Keep moving", prefab = mortar, count = 1, basicMinions = 4 },
        new EnemyWaveController.WaveDefinition { title = "All Types", prefab = spawner, count = 1, basicMinions = 4, additionalGroups = new[] {
                new EnemyWaveController.EnemyGroup { prefab = layer, count = 1 },
                new EnemyWaveController.EnemyGroup { prefab = mortar, count = 1 }
            } }
    };

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
        capsule.height = (kind == 3 ? 1f : 1.6f) * d;
        capsule.radius = (kind == 3 ? .5f : .55f) * d;
        capsule.center = Vector3.up * capsule.height * .5f;
        var enemy = go.AddComponent<EnemyBumper>();
        enemy.ConfigureCombat(kind == 3 ? 1 : 2, true, kind == 1 ? d * 1.5f : kind == 3 ? d * 2f : 0f,
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
