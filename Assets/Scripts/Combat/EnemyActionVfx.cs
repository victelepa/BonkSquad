using UnityEngine;

public static class EnemyPalette
{
    public static readonly Color HostileCrimson = new Color(1f, .32f, .3f);
    public static readonly Color SpawnerSignal = new Color(.95f, .08f, .48f);
    public static readonly Color Explosion = new Color(1f, .22f, .05f);
    public static readonly Color MineDanger = new Color(1f, .34f, .04f);
    public static readonly Color Net = new Color(.05f, .85f, 1f);
    public static readonly Color Runner = new Color(1f, .78f, .04f);
    public static readonly Color MortarMetal = new Color(.12f, .14f, .17f);
    public static readonly Color MineBrown = new Color(.42f, .2f, .07f);
    public static readonly Color NetDarkBlue = new Color(.04f, .14f, .34f);

    public static Color For(PresentedEnemyKind kind) => kind switch
    {
        PresentedEnemyKind.Spawner => SpawnerSignal,
        PresentedEnemyKind.MineLayer => MineDanger,
        PresentedEnemyKind.Mortar => Explosion,
        PresentedEnemyKind.NetShooter => NetDarkBlue,
        PresentedEnemyKind.Runner => Runner,
        _ => HostileCrimson
    };
}

public static class EnemyActionVfx
{
    public static void Glimmer(Transform parent, Vector3 position, Color color, float size = 1f)
        => Burst(parent, position, color, 28, size, .8f, .35f, false);

    public static void Explosion(Transform parent, Vector3 position, float size)
    {
        Burst(parent, position, EnemyPalette.Explosion, 34, size, 1.1f, .55f, true);
        Burst(parent, position, new Color(.42f, .32f, .25f), 24, size * .8f, .7f, .8f, true);
    }

    public static void NetPatch(Transform parent, Vector3 position, float diameter)
    {
        var root = new GameObject("Fallen net visual");
        root.transform.SetParent(parent, true);
        root.transform.position = position + Vector3.up * .035f;
        var material = NewMaterial(EnemyPalette.Net);
        float half = diameter * 1.15f;
        for (int i = -2; i <= 2; i++)
        {
            float offset = i * half * .25f;
            Line(root.transform, material, new Vector3(-half, 0, offset), new Vector3(half, 0, offset), diameter * .045f);
            Line(root.transform, material, new Vector3(offset, 0, -half), new Vector3(offset, 0, half), diameter * .045f);
        }
        var lifetime = root.AddComponent<TransientEnemyVfx>();
        lifetime.Configure(1.5f, material);
    }

    private static void Burst(Transform parent, Vector3 position, Color color, int count,
        float size, float speed, float lifetime, bool gravity)
    {
        var go = new GameObject("Enemy action burst");
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        var particles = go.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = .1f;
        main.startLifetime = lifetime;
        main.startSpeed = speed * size;
        main.startSize = Mathf.Max(.04f, size * .12f);
        main.startColor = color;
        main.gravityModifier = gravity ? 1.2f : -.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = size * .45f;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        var material = NewMaterial(color);
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var cleanup = go.AddComponent<TransientEnemyVfx>();
        cleanup.Configure(lifetime + .3f, material);
        particles.Emit(count);
        particles.Play();
    }

    private static void Line(Transform parent, Material material, Vector3 a, Vector3 b, float width)
    {
        var go = new GameObject("Net strand");
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = EnemyPalette.Net;
    }

    private static Material NewMaterial(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = color;
        return material;
    }
}

public sealed class TransientEnemyVfx : MonoBehaviour
{
    private float destroyAt;
    private Material ownedMaterial;
    public void Configure(float seconds, Material material)
    {
        destroyAt = Time.time + seconds;
        ownedMaterial = material;
    }
    private void Update()
    {
        if (Time.time >= destroyAt) Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
