using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyBumper))]
public sealed class SpawnerEnemy : MonoBehaviour
{
    [Min(1f)] [SerializeField] private float spawnInterval = 8f;
    [Min(.1f)] [SerializeField] private float warningSeconds = 1f;
    [Min(1)] [SerializeField] private int livingMinionCap = 2;
    [Min(0)] [SerializeField] private int totalSpawnBudget = 3;
    private int totalSpawned;
    public int TotalSpawned => totalSpawned;
    public int SpawnBudget => totalSpawnBudget;
    private readonly List<EnemyBumper> children = new List<EnemyBumper>();
    private EnemyBumper enemy;
    private float nextSpawn;
    private float warningEnd;
    private GameObject marker;
    private bool warningPending;
    private Vector3 spawnPoint;
    private void Awake() { enemy = GetComponent<EnemyBumper>(); nextSpawn = Time.time + spawnInterval; }
    private void OnEnable()
    {
        if (enemy == null) enemy = GetComponent<EnemyBumper>();
        enemy.Defeated += StopSpawning;
    }
    private void StopSpawning(EnemyBumper _)
    {
        warningPending = false;
        if (marker != null) { marker.SetActive(false); Destroy(marker); marker = null; }
    }
    private void OnDisable() { if (enemy != null) enemy.Defeated -= StopSpawning; StopSpawning(enemy); }
    private void Update()
    {
        EnemyWaveController run = enemy.Encounter;
        if (run == null || !run.IsCombatActive || enemy.IsDefeated || totalSpawned >= totalSpawnBudget) return;
        children.RemoveAll(child => child == null || child.IsDefeated);
        if (warningPending)
        {
            if (Time.time < warningEnd) return;
            if (marker != null) Destroy(marker);
            marker = null; warningPending = false;
            bool spawned = false;
            // A moving player or minion may have entered the marked location.
            if (children.Count < livingMinionCap && run.CanSpawnMinion &&
                run.IsMinionSpawnSafe(spawnPoint))
            {
                EnemyBumper child = run.SpawnMinion(spawnPoint);
                if (child != null) { children.Add(child); totalSpawned++; spawned = true; }
            }
            nextSpawn = Time.time + (spawned ? Mathf.Max(.1f, spawnInterval - warningSeconds) : 1f);
        }
        else if (Time.time >= nextSpawn)
        {
            nextSpawn = Time.time + spawnInterval;
            if (children.Count >= livingMinionCap || !run.CanSpawnMinion) return;
            if (!run.TryFindMinionSpawn(out spawnPoint)) { nextSpawn = Time.time + 1f; return; }
            marker = run.CreateMarker("Minion incoming", spawnPoint, run.Placement.Diameter, Color.green);
            warningPending = true;
            warningEnd = Time.time + warningSeconds;
        }
    }
    private void OnDestroy() { if (marker != null) Destroy(marker); }
}
