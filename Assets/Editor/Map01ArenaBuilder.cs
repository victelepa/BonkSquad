using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Creates persistent scene assets. No map generation or boost logic runs in Play mode.</summary>
public static class Map01ArenaBuilder
{
    private const string Folder = "Assets/Scenes/levels";
    private const string ScenePath = Folder + "/Map01_RefractionArena.unity";
    private const string AssetFolder = Folder + "/Map01Assets";
    private static float diameter;
    private static PhysicsMaterial wallPhysics;
    private static Material floor, centralFloor, obstacle, outline, blue, blueFill, teal, tealFill;
    private static Transform geometry, decoration;

    [MenuItem("Tools/BonkSquad/Create Map01 Arena")]
    public static void Build()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Map01 already exists; edit the scene directly. Refusing to overwrite it.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Directory.CreateDirectory(AssetFolder);
        AssetDatabase.Refresh();
        // Open a separate copy; the original SampleScene is never saved or overwritten.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var ball = UnityEngine.Object.FindFirstObjectByType<BouncingBallController>(FindObjectsInactive.Include);
        if (ball == null || !ball.TryGetComponent<SphereCollider>(out var ballCollider))
            throw new InvalidOperationException("SampleScene must contain the existing player ball and SphereCollider.");
        diameter = ballCollider.radius * 2f * Mathf.Max(Mathf.Abs(ball.transform.lossyScale.x), Mathf.Abs(ball.transform.lossyScale.z));
        if (diameter <= 0f) throw new InvalidOperationException("Invalid player ball diameter.");

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "GreyboxLevel" || root.name == "Enemies")
                UnityEngine.Object.DestroyImmediate(root);

        wallPhysics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Materials/Wall.physicMaterial");
        if (wallPhysics == null) throw new InvalidOperationException("Missing shared wall physics material.");
        floor = MaterialAsset("Floor_Charcoal", new Color(.075f, .085f, .10f), false);
        centralFloor = MaterialAsset("Floor_Central", new Color(.095f, .11f, .13f), false);
        obstacle = MaterialAsset("Obstacles_Graphite", new Color(.24f, .27f, .30f), false);
        outline = MaterialAsset("Edges_Ivory", new Color(.83f, .88f, .91f), true);
        blue = MaterialAsset("Boost_Omnidirectional_Edge", new Color(.12f, .40f, 1f), true);
        blueFill = MaterialAsset("Boost_Omnidirectional_Surface", new Color(.045f, .13f, .28f), true);
        teal = MaterialAsset("Boost_Directional_Edge", new Color(.13f, .84f, .77f), true);
        tealFill = MaterialAsset("Boost_Directional_Surface", new Color(.035f, .23f, .23f), true);

        var map = new GameObject("Map01_RefractionArena");
        geometry = Child(map.transform, "Collision Geometry");
        decoration = Child(map.transform, "Floor Markings");
        Box("Arena Floor", geometry, new Vector3(0, -.15f, 0), new Vector3(64 * diameter, .3f, 64 * diameter), floor, true);
        FlatPolygon("Central Floor Inset", Rectangle(-20, -20, 40, 40), .006f, centralFloor);

        float wallHeight = 3f * diameter, wallThickness = .75f * diameter;
        // Interior wall faces are exactly at +/-32D; their thickness extends outward.
        Box("Boundary North", geometry, new Vector3(0, wallHeight / 2, 32 * diameter + wallThickness / 2), new Vector3(64 * diameter + 2 * wallThickness, wallHeight, wallThickness), obstacle, true);
        Box("Boundary South", geometry, new Vector3(0, wallHeight / 2, -32 * diameter - wallThickness / 2), new Vector3(64 * diameter + 2 * wallThickness, wallHeight, wallThickness), obstacle, true);
        Box("Boundary East", geometry, new Vector3(32 * diameter + wallThickness / 2, wallHeight / 2, 0), new Vector3(wallThickness, wallHeight, 64 * diameter), obstacle, true);
        Box("Boundary West", geometry, new Vector3(-32 * diameter - wallThickness / 2, wallHeight / 2, 0), new Vector3(wallThickness, wallHeight, 64 * diameter), obstacle, true);
        Rim("Arena Boundary Rim", Rectangle(-32, -32, 64, 64), wallHeight + .01f, .055f * diameter, outline);

        Prism("C01_ChamferedBlock_NW", Points(-16,12, -8,12, -6,14, -6,16, -16,16));
        Prism("C02_ShortWall_NW", Rectangle(-16, 5, 3, 4));
        Prism("C04_RoundPillar_NE", Circle(new Vector2(17, 5.5f), 1.5f, 40));
        Prism("C05_ChamferedBlock_SE", Points(7,-7, 16,-7, 16,-11, 9,-11, 7,-9));
        Prism("C06_ShortWall_SE", Rectangle(13, -17.5f, 4, 3));
        Prism("C07_RoundedDeflector_SW", Capsule(new Vector2(-11, -12), 8, 2, 45));
        Prism("C08_RoundPillar_SW", Circle(new Vector2(-6, -17), 1.5f, 40));
        Prism("C09_RoundPillar_SW", Circle(new Vector2(-17, -5.5f), 1.5f, 40));

        OmniPad("U1_OmniBoost", new Vector2(-23, 0));
        OmniPad("U2_OmniBoost", new Vector2(0, -23));
        OmniPad("U3_OmniBoost", new Vector2(23, 1));
        DirectionalPad();

        // Subtle grid matches the plan without filling the central corridor with geometry.
        var gridMaterial = MaterialAsset("Floor_Grid", new Color(.135f, .16f, .19f), true);
        for (int n = -20; n <= 20; n += 4)
        {
            Segment("Grid X " + n, new Vector2(n, -20), new Vector2(n, 20), .012f, .018f * diameter, gridMaterial);
            Segment("Grid Z " + n, new Vector2(-20, n), new Vector2(20, n), .012f, .018f * diameter, gridMaterial);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.62f, .67f, .74f);
        RenderSettings.skybox = null;
        var camera = Camera.main;
        if (camera != null)
        {
            camera.backgroundColor = new Color(.055f, .065f, .08f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
        Map01HierarchyOrganizer.Organize(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        ValidateAndPreview();
        Debug.Log("MAP01_BUILD_OK " + ScenePath + " D=" + diameter);
    }

    private static Material MaterialAsset(string name, Color color, bool unlit)
    {
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP shader missing.");
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .12f);
        AssetDatabase.CreateAsset(material, AssetFolder + "/" + name + ".mat");
        return material;
    }

    private static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collide)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        var collider = go.GetComponent<BoxCollider>();
        if (collide) collider.sharedMaterial = name == "Arena Floor" ? null : wallPhysics;
        else UnityEngine.Object.DestroyImmediate(collider);
    }

    private static Vector2[] Points(params float[] coordinates)
    {
        var points = new Vector2[coordinates.Length / 2];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
        return points;
    }

    private static Vector2[] Rectangle(float x, float z, float width, float length)
        => Points(x,z, x+width,z, x+width,z+length, x,z+length);

    private static Vector2[] Circle(Vector2 center, float radius, int segments)
    {
        var points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return points;
    }

    private static Vector2[] Capsule(Vector2 center, float length, float width, float degrees)
    {
        var points = new List<Vector2>();
        float radius = width / 2, halfSegment = (length - width) / 2;
        for (int end = 0; end < 2; end++)
            for (int i = 0; i <= 12; i++)
            {
                float angle = (-90 + end * 180 + i * 15) * Mathf.Deg2Rad;
                var p = new Vector2((end == 0 ? halfSegment : -halfSegment) + radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
                float rotation = degrees * Mathf.Deg2Rad;
                points.Add(center + new Vector2(p.x * Mathf.Cos(rotation) - p.y * Mathf.Sin(rotation), p.x * Mathf.Sin(rotation) + p.y * Mathf.Cos(rotation)));
            }
        return points.ToArray();
    }

    private static void EnsureCounterClockwise(Vector2[] points)
    {
        float area = 0;
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Length];
            area += a.x * b.y - b.x * a.y;
        }
        if (area < 0) Array.Reverse(points);
    }

    private static void Prism(string name, Vector2[] points)
    {
        EnsureCounterClockwise(points);
        int n = points.Length;
        float height = 2 * diameter;
        var vertices = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            vertices[i] = new Vector3(points[i].x * diameter, 0, points[i].y * diameter);
            vertices[i + n] = vertices[i] + Vector3.up * height;
        }
        var triangles = new List<int>();
        for (int i = 1; i < n - 1; i++)
        {
            triangles.AddRange(new[] {0,i,i+1, n,n+i+1,n+i});
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            triangles.AddRange(new[] {i,i+n,j+n, i,j+n,j});
        }
        var go = MeshObject(name, geometry, vertices, triangles.ToArray(), obstacle);
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = true;
        collider.sharedMaterial = wallPhysics;
        Rim(name + " Top Outline", points, height + .008f, .045f * diameter, outline);
    }

    private static GameObject MeshObject(string name, Transform parent, Vector3[] vertices, int[] triangles, Material material)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, AssetFolder + "/" + name.Replace(" ", "_") + ".asset");
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (parent == decoration)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.GetComponent<MeshRenderer>().receiveShadows = false;
        }
        return go;
    }

    private static void FlatPolygon(string name, Vector2[] points, float height, Material material)
    {
        EnsureCounterClockwise(points);
        var vertices = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++) vertices[i] = new Vector3(points[i].x * diameter, height, points[i].y * diameter);
        var triangles = new List<int>();
        for (int i = 1; i < points.Length - 1; i++) triangles.AddRange(new[] {0,i+1,i});
        MeshObject(name, decoration, vertices, triangles.ToArray(), material);
    }

    private static void Segment(string name, Vector2 from, Vector2 to, float height, float worldWidth, Material material)
    {
        var normal = new Vector2(-(to - from).y, (to - from).x).normalized * worldWidth / (2 * diameter);
        FlatPolygon(name, new[] {from+normal, from-normal, to-normal, to+normal}, height, material);
    }

    private static void Rim(string name, Vector2[] points, float height, float worldWidth, Material material)
    {
        // A single non-colliding mesh for the complete outline.
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[i]; var b = points[(i+1)%points.Length];
            var normal = new Vector2(-(b-a).y, (b-a).x).normalized * worldWidth / (2 * diameter);
            int n = vertices.Count;
            foreach (var p in new[] {a+normal, a-normal, b-normal, b+normal})
                vertices.Add(new Vector3(p.x*diameter, height, p.y*diameter));
            triangles.AddRange(new[] {n,n+2,n+1, n,n+3,n+2});
        }
        MeshObject(name, decoration, vertices.ToArray(), triangles.ToArray(), material);
    }

    private static void OmniPad(string name, Vector2 center)
    {
        FlatPolygon(name + " Surface", Circle(center, 2.5f, 64), .025f, blueFill);
        Rim(name + " Ring", Circle(center, 2.5f, 64), .033f, .075f*diameter, blue);
        // Four small outward chevrons convey all-direction use without a preferred heading.
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI / 2;
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-d.y, d.x);
            var tip = center + d*1.65f;
            Segment(name + " Chevron " + i + " L", tip-d*.48f+side*.35f, tip, .04f, .09f*diameter, blue);
            Segment(name + " Chevron " + i + " R", tip-d*.48f-side*.35f, tip, .04f, .09f*diameter, blue);
        }
    }

    private static void DirectionalPad()
    {
        const string name = "A1_DirectionalBoost";
        var rectangle = Rectangle(9.5f, 20, 3, 6);
        FlatPolygon(name + " Surface", rectangle, .025f, tealFill);
        Rim(name + " Border", rectangle, .033f, .075f*diameter, teal);
        Segment(name + " Arrow Shaft", new Vector2(11,24.7f), new Vector2(11,21.5f), .04f, .14f*diameter, outline);
        FlatPolygon(name + " Arrow Head", Points(10.25f,22.2f, 11,21.1f, 11.75f,22.2f), .041f, outline);
    }

    public static void ValidateAndPreview()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var map = GameObject.Find("Map01_RefractionArena");
        if (map == null) throw new InvalidOperationException("Map root missing after scene reload.");
        var ball = UnityEngine.Object.FindFirstObjectByType<BouncingBallController>(FindObjectsInactive.Include);
        var sphere = ball.GetComponent<SphereCollider>();
        diameter = sphere.radius * 2 * Mathf.Max(Mathf.Abs(ball.transform.lossyScale.x), Mathf.Abs(ball.transform.lossyScale.z));
        int solidCount = 0;
        foreach (var collider in map.GetComponentsInChildren<MeshCollider>())
        {
            if (!collider.convex || collider.sharedMesh == null) throw new InvalidOperationException("Invalid obstacle collider.");
            solidCount++;
        }
        if (solidCount != 8) throw new InvalidOperationException("Expected eight central colliders.");
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    throw new InvalidOperationException("Missing script: " + transform.name);
        var visuals = map.transform.Find("Floor Markings");
        if (visuals.GetComponentsInChildren<Collider>().Length != 0 || map.GetComponentsInChildren<MonoBehaviour>().Length != 0)
            throw new InvalidOperationException("Map visuals must not contain colliders or runtime logic.");
        Physics.SyncTransforms();
        foreach (var p in new[] {new Vector2(0,3), new Vector2(11,12), new Vector2(-23,0), new Vector2(0,-23), new Vector2(23,1), new Vector2(11,23)})
            if (!Physics.Raycast(new Vector3(p.x*diameter, 10, p.y*diameter), Vector3.down, out var hit, 12) || hit.collider.name != "Arena Floor")
                throw new InvalidOperationException("Unexpected obstruction at " + p);
        foreach (var p in new[] {new Vector2(-14.5f,7), new Vector2(17,5.5f), new Vector2(15,-16), new Vector2(-11,-12)})
            if (!Physics.Raycast(new Vector3(p.x*diameter,10,p.y*diameter), Vector3.down, out var hit, 12) || !hit.collider.name.StartsWith("C", StringComparison.Ordinal))
                throw new InvalidOperationException("Obstacle collision missing at " + p);

        var preview = new GameObject("Temporary Map Preview Camera", typeof(Camera));
        var camera = preview.GetComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(0, 80, 0), Quaternion.Euler(90, 0, 0));
        camera.orthographic = true;
        camera.orthographicSize = 35 * diameter;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 120;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.045f,.055f,.07f);
        var target = new RenderTexture(1600, 1600, 24);
        camera.targetTexture = target;
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        camera.Render();
        camera.Render();
        ShaderUtil.allowAsyncCompilation = asyncCompilation;
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(1600, 1600, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0,0,1600,1600),0,0);
        image.Apply();
        Directory.CreateDirectory("output/Map01");
        File.WriteAllBytes("output/Map01/Map01-Unity-Preview.png", image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(preview);
        File.WriteAllText("output/Map01/validation.txt", "PASS: scene reload, 8 convex central colliders, 4 boundary colliders, no map runtime scripts, no boost colliders, clear center and C03 location, clear 4 boost pads, obstacle raycasts, no missing scripts. D=" + diameter);
        Debug.Log("MAP01_VALIDATION_OK");
    }
}
