using UnityEngine;

[RequireComponent(typeof(EnemyBumper))]
public sealed class MortarEnemy : MonoBehaviour
{
    [Min(1f)] [SerializeField] private float fireInterval = 5f;
    private EnemyBumper enemy;
    private float nextFire;
    private void Awake() { enemy = GetComponent<EnemyBumper>(); nextFire = Time.time + fireInterval; }
    internal void DelayFirstShot(float seconds) => nextFire += seconds;
    private void Update()
    {
        EnemyWaveController run = enemy.Encounter;
        if (run == null || !run.IsCombatActive || enemy.IsDefeated || Time.time < nextFire) return;
        nextFire = Time.time + fireInterval;
        run.SpawnShell(transform.position + Vector3.up * run.Placement.Diameter * 1.5f, run.Player.ActivePosition);
    }
}
