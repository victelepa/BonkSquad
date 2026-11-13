using System.Linq;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class TopplingPillar : MonoBehaviour
{
    [SerializeField] private Material warningMaterial;
    [SerializeField, Min(.1f)] private float fallSeconds = .55f;
    private BoxCollider body;
    private LineRenderer warning;
    private Vector3 uprightPosition, landingPosition, pivot;
    private Quaternion uprightRotation, landingRotation;
    private EnemyWaveController encounter;
    private float progress;

    private void Awake()
    {
        body = GetComponent<BoxCollider>();
        uprightPosition = transform.position;
        uprightRotation = transform.rotation;
    }

    public void TryTopple(EnemyWaveController run, Vector3 surfaceNormal)
    {
        if (encounter != null || !run.IsCombatActive) return;
        Vector3 inward = -PlayerMovementMath.Planar(surfaceNormal);
        if (inward.sqrMagnitude < .01f) return;

        Vector3 local = Quaternion.Inverse(uprightRotation) * inward;
        Vector3 face = Mathf.Abs(local.x) > Mathf.Abs(local.z)
            ? Vector3.right * Mathf.Sign(local.x) : Vector3.forward * Mathf.Sign(local.z);
        Vector3 direction = uprightRotation * face;
        Vector3 size = Vector3.Scale(body.size, transform.lossyScale);
        float depth = face.x != 0f ? size.x : size.z;
        pivot = uprightPosition - Vector3.up * size.y * .5f + direction * depth * .5f;
        landingRotation = Quaternion.AngleAxis(90f, Vector3.Cross(Vector3.up, direction));
        landingPosition = pivot + landingRotation * (uprightPosition - pivot);

        encounter = run;
        progress = 0f;
        ShowWarning(direction, size.y, face.x != 0f ? size.z : size.x);
    }

    private void ShowWarning(Vector3 direction, float length, float width)
    {
        if (warning == null)
        {
            warning = new GameObject("Pillar landing outline", typeof(LineRenderer)).GetComponent<LineRenderer>();
            warning.transform.SetParent(transform, false);
            warning.sharedMaterial = warningMaterial;
            warning.startColor = warning.endColor = new Color(1f, .78f, .12f);
            warning.widthMultiplier = .09f;
            warning.loop = true;
            warning.positionCount = 4;
        }
        Vector3 start = pivot + Vector3.up * .06f;
        Vector3 side = Vector3.Cross(Vector3.up, direction) * width * .5f;
        Vector3 end = start + direction * length;
        warning.SetPositions(new[] { start - side, start + side, end + side, end - side });
        warning.enabled = true;
    }

    private void Update()
    {
        if (encounter == null || !encounter.IsCombatActive || progress >= 1f) return;
        progress = Mathf.Min(1f, progress + Time.deltaTime / fallSeconds);
        Quaternion rotation = Quaternion.Slerp(Quaternion.identity, landingRotation, progress * progress);
        transform.SetPositionAndRotation(pivot + rotation * (uprightPosition - pivot), rotation * uprightRotation);
        if (progress < 1f) return;

        transform.position = landingPosition;
        warning.enabled = false;
        Physics.SyncTransforms();
        Vector3 halfSize = Vector3.Scale(body.size, transform.lossyScale) * .5f;
        foreach (var enemy in encounter.LivingEnemyObjects.ToArray())
        {
            if (enemy == null || enemy.IsDefeated) continue;
            var shape = enemy.GetComponent<CapsuleCollider>();
            Vector3 center = transform.InverseTransformPoint(shape.bounds.center);
            Vector3 offset = Vector3.Scale(center - body.center, transform.lossyScale);
            float x = Mathf.Max(0f, Mathf.Abs(offset.x) - halfSize.x);
            float z = Mathf.Max(0f, Mathf.Abs(offset.z) - halfSize.z);
            // At landing the pillar's local Y is the length along the floor.
            float along = Mathf.Max(0f, Mathf.Abs(offset.y) - halfSize.y);
            float across = Mathf.Abs(Vector3.Dot(transform.right, Vector3.up)) > .5f ? z : x;
            if (along * along + across * across <= shape.radius * shape.radius)
                enemy.TryCrush();
        }
        encounter.NotifyObstacleMoved();
    }

    public void ResetPillar()
    {
        if (encounter == null) return;
        encounter = null;
        transform.SetPositionAndRotation(uprightPosition, uprightRotation);
        warning.enabled = false;
    }
}
