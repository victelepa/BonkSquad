using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyEncounterSpawner : MonoBehaviour
{
    [Serializable]
    public class SpawnGroup
    {
        public string designerLabel = "Enemy Group";
        public EnemyBumper enemyPrefab;
        public EnemyType enemyType = EnemyType.Small;
        [Min(0)] public int quantity = 1;
        [Tooltip("Enemies use these in order. If quantity is larger, locations repeat with Spawn Radius offsets.")]
        public Transform[] spawnLocations;
        public EnemyPatrolPath patrolPath;
        [Min(0f)] public float spawnRadius = 1.5f;
    }

    [Tooltip("Placed scene enemies can coexist with groups spawned here.")]
    [SerializeField] private List<SpawnGroup> groups = new List<SpawnGroup>();
    [SerializeField] private bool spawnOnStart = true;

    private void Start()
    {
        if (spawnOnStart) SpawnAll();
    }

    [ContextMenu("Spawn All Groups")]
    public void SpawnAll()
    {
        foreach (SpawnGroup group in groups)
        {
            if (group.enemyPrefab == null || group.quantity <= 0 ||
                group.spawnLocations == null || group.spawnLocations.Length == 0)
                continue;

            for (int i = 0; i < group.quantity; i++)
            {
                Transform spawn = group.spawnLocations[i % group.spawnLocations.Length];
                if (spawn == null) continue;
                int repeat = i / group.spawnLocations.Length;
                float angle = i * 137.5f * Mathf.Deg2Rad;
                Vector3 offset = repeat == 0 ? Vector3.zero : new Vector3(
                    Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * group.spawnRadius * repeat;
                EnemyBumper enemy = Instantiate(
                    group.enemyPrefab, spawn.position + offset, spawn.rotation, transform);
                enemy.Configure(group.enemyType, group.patrolPath);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.8f);
        foreach (SpawnGroup group in groups)
        {
            if (group.spawnLocations == null) continue;
            foreach (Transform spawn in group.spawnLocations)
                if (spawn != null) Gizmos.DrawWireSphere(spawn.position, 0.45f);
        }
    }
}
