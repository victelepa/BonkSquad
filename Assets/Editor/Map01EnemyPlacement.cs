using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Places the existing wandering enemies in Map01; no new runtime movement rules.</summary>
public static class Map01EnemyPlacement
{
    private const string Destination = "Assets/Scenes/levels/Map01_RefractionArena.unity";

    private readonly struct Group
    {
        public readonly string Name;
        public readonly Vector2 Center;
        public readonly int Count;
        public readonly bool HasBig;
        public Group(string name, Vector2 center, int count, bool hasBig)
        {
            Name = name; Center = center; Count = count; HasBig = hasBig;
        }
    }

    // Coordinates in ball diameters. These are initial loose clusters, not patrol bounds.
    private static readonly Group[] Groups =
    {
        new Group("01_NorthWest_4", new Vector2(-23,18), 4, true),
        new Group("02_NorthInner_3", new Vector2(-1,16), 3, false),
        new Group("03_NorthEast_4", new Vector2(23,18), 4, true),
        new Group("04_EastFlank_2", new Vector2(25,-8), 2, false),
        new Group("05_SouthEast_3", new Vector2(8,-24), 3, true),
        new Group("06_WestInner_2", new Vector2(-8,1), 2, false),
        new Group("07_SouthWest_3", new Vector2(-24,-19), 3, true),
        new Group("08_EastInner_1", new Vector2(8,7), 1, false)
    };

    private static readonly Vector2[] Offsets =
    {
        new Vector2(-.9f,.2f), new Vector2(1.3f,1.2f),
        new Vector2(.7f,-1.6f), new Vector2(-1.8f,-2.0f)
    };

    [MenuItem("Tools/BonkSquad/Add Map01 Enemy Groups")]
    public static void Place()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var target = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
        if (target.GetRootGameObjects().Any(root => root.GetComponentsInChildren<EnemyBumper>(true).Length > 0))
            throw new InvalidOperationException("Map01 already has enemies; refusing to duplicate or replace them.");

        var ball = target.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BouncingBallController>(true)).Single();
        var sphere = ball.GetComponent<SphereCollider>();
        float diameter = sphere.radius * 2f * Mathf.Max(Mathf.Abs(ball.transform.lossyScale.x), Mathf.Abs(ball.transform.lossyScale.z));
        var source = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        var templates = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyBumper>(true)).ToArray();
        var bigTemplate = templates.First(e => e.name.StartsWith("Big Enemy", StringComparison.Ordinal));
        var smallTemplate = templates.First(e => e.name.StartsWith("Small Enemy", StringComparison.Ordinal));
        SceneManager.SetActiveScene(target);

        var container = new GameObject("Enemies");
        var physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Wall.physicMaterial");
        int bigIndex = 0, smallIndex = 0;
        foreach (var group in Groups)
        {
            var groupObject = new GameObject("Group_" + group.Name);
            groupObject.transform.SetParent(container.transform, false);
            groupObject.transform.localPosition = new Vector3(group.Center.x * diameter, 0, group.Center.y * diameter);
            for (int i = 0; i < group.Count; i++)
            {
                bool big = group.HasBig && i == 0;
                var enemy = UnityEngine.Object.Instantiate(big ? bigTemplate.gameObject : smallTemplate.gameObject);
                enemy.name = big ? "Big Enemy " + (++bigIndex).ToString("00") : "Small Enemy " + (++smallIndex).ToString("00");
                if (enemy.scene != target) SceneManager.MoveGameObjectToScene(enemy, target);
                enemy.transform.SetParent(groupObject.transform, false);
                var capsule = enemy.GetComponent<CapsuleCollider>();
                float height = Mathf.Max(capsule.height * Mathf.Abs(enemy.transform.lossyScale.y),
                    2f * capsule.radius * Mathf.Max(Mathf.Abs(enemy.transform.lossyScale.x), Mathf.Abs(enemy.transform.lossyScale.z)));
                var offset = group.Count == 1 ? Vector2.zero : Offsets[i];
                enemy.transform.localPosition = new Vector3(offset.x * diameter, height / 2, offset.y * diameter);
                enemy.transform.localRotation = Quaternion.identity;
                capsule.sharedMaterial = physicsMaterial;

                float radius = capsule.radius * Mathf.Max(Mathf.Abs(enemy.transform.lossyScale.x), Mathf.Abs(enemy.transform.lossyScale.z));
                float centerLimit = 32f * diameter - radius - .25f;
                var settings = new SerializedObject(enemy.GetComponent<EnemyBumper>());
                settings.FindProperty("arenaMin").vector2Value = new Vector2(-centerLimit, -centerLimit);
                settings.FindProperty("arenaMax").vector2Value = new Vector2(centerLimit, centerLimit);
                settings.ApplyModifiedPropertiesWithoutUndo();
                enemy.SetActive(true);
            }
        }
        EditorSceneManager.CloseScene(source, true);
        Physics.SyncTransforms();
        Validate(target, diameter);
        EditorSceneManager.SaveScene(target);
        AssetDatabase.SaveAssets();
        // Reopens the persisted scene and refreshes the existing top-down preview.
        Map01ArenaBuilder.ValidateAndPreview();
        var reloaded = SceneManager.GetSceneByPath(Destination);
        Validate(reloaded, diameter);
        Debug.Log("MAP01_ENEMIES_OK: 4 big + 18 small; 8 loose groups; existing random wandering preserved.");
    }

    private static void Validate(Scene scene, float diameter)
    {
        var enemies = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyBumper>(true)).ToArray();
        if (enemies.Length != 22 || enemies.Count(e => e.name.StartsWith("Big Enemy", StringComparison.Ordinal)) != 4 ||
            enemies.Count(e => e.name.StartsWith("Small Enemy", StringComparison.Ordinal)) != 18)
            throw new InvalidOperationException("Enemy count must be 4 big and 18 small.");

        var container = scene.GetRootGameObjects().Single(root => root.name == "Enemies");
        if (container.transform.childCount != 8) throw new InvalidOperationException("Expected eight groups.");
        foreach (Transform group in container.transform)
            if (group.childCount < 1 || group.childCount > 4) throw new InvalidOperationException("Groups must contain 1-4 enemies.");

        foreach (var enemy in enemies)
        {
            bool big = enemy.name.StartsWith("Big Enemy", StringComparison.Ordinal);
            var settings = new SerializedObject(enemy);
            if (!Mathf.Approximately(settings.FindProperty("moveSpeed").floatValue, big ? 1 : 2.5f) ||
                settings.FindProperty("directionChangeInterval").vector2Value != new Vector2(2,5) ||
                settings.FindProperty("destroyedByBall").boolValue != !big)
                throw new InvalidOperationException("Original enemy behavior changed: " + enemy.name);
            var capsule = enemy.GetComponent<CapsuleCollider>();
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(enemy.transform.lossyScale.x), Mathf.Abs(enemy.transform.lossyScale.z));
            float halfSegment = Mathf.Max(0, capsule.height * Mathf.Abs(enemy.transform.lossyScale.y) / 2 - radius);
            Vector3 center = enemy.transform.TransformPoint(capsule.center);
            foreach (var other in Physics.OverlapCapsule(center + Vector3.up*halfSegment, center - Vector3.up*halfSegment,
                         radius + .12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (other == capsule || other.name == "Arena Floor") continue;
                throw new InvalidOperationException("Enemy starts too close to another collider: " + enemy.name + " / " + other.name);
            }
            float centerLimit = settings.FindProperty("arenaMax").vector2Value.x;
            if (Mathf.Abs(center.x) > centerLimit || Mathf.Abs(center.z) > centerLimit)
                throw new InvalidOperationException("Enemy outside wandering bounds: " + enemy.name);
            if (new Vector2(center.x,center.z).magnitude < 6 * diameter)
                throw new InvalidOperationException("Enemy too close to player spawn: " + enemy.name);
        }
        Directory.CreateDirectory("output/Map01");
        File.WriteAllText("output/Map01/enemy-validation.txt", "PASS: 4 big + 18 small; eight groups (4,3,4,2,3,2,3,1); inherited speeds 1/2.5 and random direction interval 2-5 seconds; bounds adapted to new arena; no initial overlap with obstacles, enemies, walls, or player; 6D player-spawn clearance.\n" +
            string.Join("\n", enemies.Select(e => e.transform.parent.name + " / " + e.name + " / " + e.transform.position)));
    }
}
