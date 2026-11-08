using System;
using UnityEngine;

public sealed class NetProjectile : MonoBehaviour
{
    [Min(.1f)] [SerializeField] private float speedInDiameters = 7f;
    [Min(.1f)] [SerializeField] private float lifetime = 3f;
    [Min(0f)] [SerializeField] private float snareSeconds = .6f;
    [Min(0f)] [SerializeField] private float immunitySeconds = 1.5f;
    private EnemyWaveController run;
    private EnemyBumper shooter;
    private Vector3 direction;
    private float expires;
    private Material material;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    public static NetProjectile Create(EnemyWaveController owner, EnemyBumper source, Vector3 origin, Vector3 target, float speed = 7f, float duration = 3f, float snare = .6f, float immunity = 1.5f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Net projectile";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(owner.TransientRoot, true); go.transform.position = origin;
        go.transform.localScale = Vector3.one * owner.Placement.Diameter * .4f;
        var projectile = go.AddComponent<NetProjectile>(); projectile.run = owner; projectile.shooter = source;
        projectile.speedInDiameters = Mathf.Max(.1f,speed); projectile.lifetime = Mathf.Max(.1f,duration);
        projectile.snareSeconds = Mathf.Max(0,snare); projectile.immunitySeconds = Mathf.Max(0,immunity);
        projectile.direction = (target - origin).normalized; projectile.expires = Time.time + projectile.lifetime;
        projectile.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); projectile.material.color = Color.cyan;
        go.GetComponent<Renderer>().sharedMaterial = projectile.material;
        return projectile;
    }
    private void FixedUpdate()
    {
        if (run == null || !run.IsCombatActive) return;
        if (Time.time >= expires) { Destroy(gameObject); return; }
        float distance = speedInDiameters * run.Placement.Diameter * Time.fixedDeltaTime;
        int count = Physics.SphereCastNonAlloc(transform.position, .2f * run.Placement.Diameter, direction,
            hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, 0, count, HitComparer.Instance);
        for (int i = 0; i < count; i++)
        {
            Collider solid = hits[i].collider;
            if (shooter != null && solid.GetComponentInParent<EnemyBumper>() == shooter) continue;
            var player = solid.GetComponentInParent<BonkRobotController>();
            if (player == run.Player) player.TrySnare(snareSeconds, immunitySeconds);
            Destroy(gameObject); return;
        }
        transform.position += direction * distance;
    }
    private sealed class HitComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitComparer Instance = new HitComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
