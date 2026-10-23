using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Groups the saved map parts without changing their world-space placement.</summary>
public static class Map01HierarchyOrganizer
{
    private const string ScenePath = "Assets/Scenes/levels/Map01_RefractionArena.unity";

    [MenuItem("Tools/BonkSquad/Organize Map01 Hierarchy")]
    public static void OrganizeSavedMap()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Organize(scene);
        EditorSceneManager.SaveScene(scene);
        Map01ArenaBuilder.ValidateAndPreview();
        Debug.Log("MAP01_HIERARCHY_OK: boosts, obstacles, boundary and grid grouped; world transforms preserved.");
    }

    public static void Organize(Scene scene)
    {
        var root = scene.GetRootGameObjects().Single(go => go.name == "Map01_RefractionArena").transform;
        var geometry = root.Find("Collision Geometry");
        var markings = root.Find("Floor Markings");
        if (geometry == null || markings == null) throw new InvalidOperationException("Missing map containers.");
        var transforms = root.GetComponentsInChildren<Transform>(true);
        var poses = transforms.ToDictionary(t => t, t => (t.position, t.rotation, t.lossyScale));
        int colliderCount = root.GetComponentsInChildren<Collider>(true).Length;

        foreach (string id in new[] {"U1_OmniBoost", "U2_OmniBoost", "U3_OmniBoost", "A1_DirectionalBoost"})
        {
            var parts = Children(markings).Where(t => t.name.StartsWith(id + " ", StringComparison.Ordinal)).ToArray();
            if (parts.Length == 0) continue; // Already organized.
            var surface = parts.Single(t => t.name == id + " Surface").GetComponent<Renderer>();
            var pivot = surface.bounds.center;
            pivot.y = 0;
            var group = NewGroup(markings, id, pivot);
            foreach (var part in parts)
            {
                part.SetParent(group, true);
                part.name = part.name.Substring(id.Length + 1);
            }
        }

        var obstacles = Children(geometry).Where(t => t.GetComponent<MeshCollider>() != null && t.name.StartsWith("C", StringComparison.Ordinal)).ToArray();
        foreach (var body in obstacles)
        {
            string originalName = body.name;
            var outline = markings.Find(originalName + " Top Outline");
            var pivot = body.GetComponent<Renderer>().bounds.center;
            pivot.y = 0;
            var group = NewGroup(geometry, originalName, pivot);
            body.SetParent(group, true);
            body.name = originalName.Substring(0, 3) + " Body";
            if (outline != null)
            {
                outline.SetParent(group, true);
                outline.name = "Top Outline";
            }
        }

        var boundaryParts = Children(geometry).Where(t => t.name.StartsWith("Boundary ", StringComparison.Ordinal)).ToArray();
        if (boundaryParts.Length > 0)
        {
            var group = NewGroup(geometry, "Arena Boundary", root.position);
            foreach (var part in boundaryParts) part.SetParent(group, true);
            var rim = markings.Find("Arena Boundary Rim");
            if (rim != null) rim.SetParent(group, true);
        }
        var gridParts = Children(markings).Where(t => t.name.StartsWith("Grid ", StringComparison.Ordinal)).ToArray();
        if (gridParts.Length > 0)
        {
            var group = NewGroup(markings, "Grid", root.position);
            foreach (var part in gridParts) part.SetParent(group, true);
        }

        foreach (var pair in poses)
            if (Vector3.Distance(pair.Key.position, pair.Value.position) > .0001f ||
                Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .001f ||
                Vector3.Distance(pair.Key.lossyScale, pair.Value.lossyScale) > .0001f)
                throw new InvalidOperationException("Grouping changed world transform: " + pair.Key.name);
        if (root.GetComponentsInChildren<Collider>(true).Length != colliderCount)
            throw new InvalidOperationException("Grouping changed collision components.");
        foreach (string id in new[] {"U1_OmniBoost", "U2_OmniBoost", "U3_OmniBoost", "A1_DirectionalBoost"})
        {
            var group = markings.Find(id);
            int expected = id.StartsWith("A1", StringComparison.Ordinal) ? 4 : 10;
            if (group == null || group.childCount != expected || group.GetComponentsInChildren<Collider>().Length != 0)
                throw new InvalidOperationException("Incomplete boost group: " + id);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Transform[] Children(Transform parent)
        => parent.Cast<Transform>().ToArray();

    private static Transform NewGroup(Transform parent, string name, Vector3 position)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.position = position;
        return group;
    }
}
