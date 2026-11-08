using UnityEngine;

[RequireComponent(typeof(EnemyBumper))]
public sealed class NetShooterEnemy : MonoBehaviour
{
    [Min(1f)] [SerializeField] private float fireInterval = 6f;
    [Min(.1f)] [SerializeField] private float warningSeconds = 1f;
    [Min(.1f)] [SerializeField] private float projectileSpeedInDiameters = 7f;
    [Min(.1f)] [SerializeField] private float projectileLifetime = 3f;
    [Min(0f)] [SerializeField] private float snareDuration = .6f;
    [Min(0f)] [SerializeField] private float releaseImmunity = 1.5f;
    private EnemyBumper enemy;
    private float nextFire, warningEnd;
    private Vector3 target;
    private GameObject marker;
    private void Awake() { enemy = GetComponent<EnemyBumper>(); nextFire = Time.time + fireInterval; }
    private void Update()
    {
        var run = enemy.Encounter;
        if (run == null || !run.IsCombatActive || enemy.IsDefeated) return;
        if (marker != null)
        {
            if (Time.time < warningEnd) return;
            Destroy(marker); marker = null;
            Vector3 origin = transform.position + Vector3.up * run.Placement.Diameter;
            NetProjectile.Create(run, enemy, origin, target, projectileSpeedInDiameters, projectileLifetime, snareDuration, releaseImmunity);
            nextFire = Time.time + Mathf.Max(.1f, fireInterval - warningSeconds);
        }
        else if (Time.time >= nextFire)
        {
            target = run.Player.ActivePosition + Vector3.up * .5f * run.Placement.Diameter;
            Vector3 warningPoint = run.Player.ActivePosition; warningPoint.y = run.Placement.Floor.bounds.max.y;
            marker = run.CreateMarker("NET SHOT — DODGE", warningPoint, run.Placement.Diameter, Color.cyan);
            warningEnd = Time.time + warningSeconds;
        }
    }
    private void OnDisable() { if (marker != null) { marker.SetActive(false); Destroy(marker); } }
}
