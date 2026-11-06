using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyBumper))]
public sealed class MineLayerEnemy : MonoBehaviour
{
    [Min(1f)] [SerializeField] private float dropInterval = 5f;
    [Min(1)] [SerializeField] private int activeMineCap = 3;
    private readonly List<CombatHazard> mines = new List<CombatHazard>();
    private EnemyBumper enemy;
    private float nextDrop;
    private void Awake() { enemy = GetComponent<EnemyBumper>(); nextDrop = Time.time + dropInterval; }
    private void Update()
    {
        EnemyWaveController run = enemy.Encounter;
        if (run == null || !run.IsCombatActive || enemy.IsDefeated || Time.time < nextDrop) return;
        nextDrop = Time.time + dropInterval;
        mines.RemoveAll(mine => mine == null);
        if (mines.Count >= activeMineCap) return;
        // Drop behind, rather than under, the body; clearance includes other enemies and hazards.
        Vector3 origin = transform.position - transform.forward * run.Placement.Diameter * 1.5f;
        if (run.Placement.TryNearby(origin, .3f * run.Placement.Diameter, .5f * run.Placement.Diameter,
            2f * run.Placement.Diameter, out Vector3 point)) mines.Add(run.SpawnMine(point));
    }
}
