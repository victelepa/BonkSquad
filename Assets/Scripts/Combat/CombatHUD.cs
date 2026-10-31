using UnityEngine;

// Compatibility entry point for existing Map01 scenes and minimap projection checks.
[RequireComponent(typeof(EnemyWaveController))]
public sealed class CombatHUD : MonoBehaviour
{
    private void Awake()
    {
        if (GetComponent<CombatPresentation>() == null) gameObject.AddComponent<CombatPresentation>();
    }
    public static Vector2 WorldToMinimap(Vector3 position, Bounds floorBounds, Rect map) =>
        new Vector2(map.x + Mathf.InverseLerp(floorBounds.min.x, floorBounds.max.x, position.x) * map.width,
            map.y + (1f - Mathf.InverseLerp(floorBounds.min.z, floorBounds.max.z, position.z)) * map.height);
}
