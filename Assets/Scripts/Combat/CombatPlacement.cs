using System;
using System.Collections.Generic;
using UnityEngine;

// All positions are floor-level roots. Regions are authored connected, open parts of Map01.
public sealed class CombatPlacement : MonoBehaviour
{
    [SerializeField] private Collider arenaFloor;
    [SerializeField] private Rect[] spawnRegions;
    [SerializeField] private Vector3[] fallbackPositions;
    [Min(1)] [SerializeField] private int randomAttempts = 100;
    public float Diameter = .8f;
    public Collider Floor => arenaFloor;
    public Vector3[] FallbackPositions => fallbackPositions;
    public Vector2 ArenaMin => new Vector2(arenaFloor.bounds.min.x + Diameter, arenaFloor.bounds.min.z + Diameter);
    public Vector2 ArenaMax => new Vector2(arenaFloor.bounds.max.x - Diameter, arenaFloor.bounds.max.z - Diameter);

    public void Configure(Collider floor, float diameter, Rect[] regions, Vector3[] fallbacks)
    {
        arenaFloor = floor;
        Diameter = diameter;
        spawnRegions = regions;
        fallbackPositions = fallbacks;
    }

    public bool IsClear(Vector3 point, float radius, float height, bool avoidHazards = true)
    {
        if (arenaFloor == null) return false;
        Bounds bounds = arenaFloor.bounds;
        if (point.x - radius < bounds.min.x || point.x + radius > bounds.max.x ||
            point.z - radius < bounds.min.z || point.z + radius > bounds.max.z) return false;
        // Check support under the center and the full cardinal footprint.
        foreach (Vector3 offset in new[] { Vector3.zero, Vector3.left * radius, Vector3.right * radius,
            Vector3.forward * radius, Vector3.back * radius })
        {
            if (!arenaFloor.Raycast(new Ray(point + offset + Vector3.up * 3f, Vector3.down), out RaycastHit hit, 6f) ||
                Mathf.Abs(hit.point.y - point.y) > .1f) return false;
        }
        Vector3 bottom = point + Vector3.up * (radius + .04f);
        Vector3 top = point + Vector3.up * Mathf.Max(radius + .04f, height - radius);
        foreach (Collider obstacle in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Collide))
        {
            if (obstacle == arenaFloor) continue;
            if (!obstacle.isTrigger || (avoidHazards && obstacle.GetComponentInParent<CombatHazard>() != null)) return false;
        }
        // Mortar markers have no colliders. Reserve their full blast footprint too.
        if (avoidHazards)
            foreach (CombatHazard hazard in FindObjectsByType<CombatHazard>(FindObjectsSortMode.None))
                if (hazard.gameObject.activeInHierarchy && hazard.Contains(point, radius)) return false;
        return true;
    }

    public bool TryWavePositions(int count, Vector3 playerStart, float radius, float height,
        System.Random random, out List<Vector3> positions)
    {
        positions = new List<Vector3>();
        for (int i = 0; i < randomAttempts && positions.Count < count; i++)
        {
            Rect region = spawnRegions[random.Next(spawnRegions.Length)];
            Vector3 point = new Vector3(Mathf.Lerp(region.xMin, region.xMax, (float)random.NextDouble()),
                arenaFloor.bounds.max.y, Mathf.Lerp(region.yMin, region.yMax, (float)random.NextDouble()));
            if (ValidWavePoint(point, playerStart, radius, height, positions)) positions.Add(point);
        }
        if (positions.Count == count) return true;
        // Rebuild the entire selection from the guaranteed fallback set, not around random leftovers.
        positions.Clear();
        var candidates = new List<Vector3>(fallbackPositions);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        foreach (Vector3 point in candidates)
        {
            if (ValidWavePoint(point, playerStart, radius, height, positions)) positions.Add(point);
            if (positions.Count == count) return true;
        }
        positions.Clear();
        return false;
    }

    private bool ValidWavePoint(Vector3 point, Vector3 playerStart, float radius, float height, List<Vector3> existing)
    {
        if (PlayerMovementMath.Planar(point - playerStart).magnitude < 6f * Diameter) return false;
        foreach (Vector3 other in existing)
            if (PlayerMovementMath.Planar(other - point).magnitude < 4f * Diameter) return false;
        // A broad clearance envelope prevents specialists from occupying narrow routes.
        return IsClear(point, Mathf.Max(radius, 1.5f * Diameter), Mathf.Max(height, 3f * Diameter));
    }

    public bool TryDistributedMinions(int count, Vector3 playerStart, List<Vector3> specialists,
        float radius, float height, System.Random random, out List<Vector3> positions)
    {
        positions = new List<Vector3>();
        if (count <= 0) return true;
        Bounds floor = arenaFloor.bounds;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / columns);
        var cells = new List<Rect>();
        float minX = floor.min.x + Diameter, minZ = floor.min.z + Diameter;
        float width = (floor.size.x - 2f * Diameter) / columns;
        float depth = (floor.size.z - 2f * Diameter) / rows;
        for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++) cells.Add(new Rect(minX + x * width, minZ + z * depth, width, depth));
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        foreach (Rect cell in cells)
        {
            bool placed = false;
            for (int attempt = 0; attempt < randomAttempts && !placed; attempt++)
            {
                Vector3 point = new Vector3(Mathf.Lerp(cell.xMin, cell.xMax, (float)random.NextDouble()),
                    floor.max.y, Mathf.Lerp(cell.yMin, cell.yMax, (float)random.NextDouble()));
                placed = TryMinionPoint(point, playerStart, specialists, radius, height, positions);
            }
            // Deterministic, cell-local fallback keeps distribution even if random attempts fail.
            for (float x = cell.xMin + .25f * Diameter; x < cell.xMax && !placed; x += .5f * Diameter)
                for (float z = cell.yMin + .25f * Diameter; z < cell.yMax && !placed; z += .5f * Diameter)
                    placed = TryMinionPoint(new Vector3(x, floor.max.y, z), playerStart, specialists, radius, height, positions);
            if (positions.Count == count) return true;
        }
        positions.Clear();
        return false;
    }

    private bool TryMinionPoint(Vector3 point, Vector3 playerStart, List<Vector3> specialists,
        float radius, float height, List<Vector3> existing)
    {
        if (PlayerMovementMath.Planar(point - playerStart).magnitude < 6f * Diameter ||
            !IsClear(point, radius + .15f * Diameter, height)) return false;
        foreach (Vector3 specialist in specialists)
            if (PlayerMovementMath.Planar(point - specialist).magnitude < 4f * Diameter) return false;
        foreach (Vector3 other in existing)
            if (PlayerMovementMath.Planar(point - other).magnitude < 1.4f * Diameter) return false;
        existing.Add(point);
        return true;
    }

    public bool TryNearby(Vector3 origin, float radius, float height, float maximumDistance, out Vector3 position)
    {
        origin.y = arenaFloor.bounds.max.y;
        if (IsClear(origin, radius, height)) { position = origin; return true; }
        // Deterministic nearest-first rings keep drops near the defeat and spawns near the parent.
        for (float distance = Diameter * .5f; distance <= maximumDistance; distance += Diameter * .5f)
            for (int angle = 0; angle < 16; angle++)
            {
                float radians = angle * Mathf.PI / 8f;
                Vector3 point = origin + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * distance;
                if (IsClear(point, radius, height)) { position = point; return true; }
            }
        position = default;
        return false;
    }
}
