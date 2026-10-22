using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyPatrolPath : MonoBehaviour
{
    [Tooltip("Ordered world-space points followed in a loop. Move these transforms directly in the Scene view.")]
    [SerializeField] private List<Transform> points = new List<Transform>();

    public int PointCount => points.Count;

    public Vector3 GetPoint(int index)
    {
        if (points.Count == 0) return transform.position;
        Transform point = points[Mathf.Abs(index) % points.Count];
        return point != null ? point.position : transform.position;
    }

    private void OnDrawGizmos()
    {
        if (points.Count < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < points.Count; i++)
        {
            Transform from = points[i];
            Transform to = points[(i + 1) % points.Count];
            if (from == null || to == null) continue;
            Gizmos.DrawLine(from.position, to.position);
            Gizmos.DrawWireSphere(from.position, 0.25f);
        }
    }
}
