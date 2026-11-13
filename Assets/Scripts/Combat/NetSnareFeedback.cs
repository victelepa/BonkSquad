using UnityEngine;

[RequireComponent(typeof(BonkRobotController))]
public sealed class NetSnareFeedback : MonoBehaviour
{
    private BonkRobotController player;
    private LineRenderer cage;
    private Material material;
    private void Awake()
    {
        player = GetComponent<BonkRobotController>();
        var go = new GameObject("Snare net", typeof(LineRenderer)); go.transform.SetParent(transform, false);
        cage = go.GetComponent<LineRenderer>(); cage.useWorldSpace = true; cage.positionCount = 33;
        material = new Material(Shader.Find("Sprites/Default")); cage.sharedMaterial = material;
        cage.widthMultiplier = .055f; cage.startColor = cage.endColor = Color.cyan; cage.enabled = false;
    }
    private void LateUpdate()
    {
        cage.enabled = player.IsSnared;
        if (!cage.enabled) return;
        for (int i = 0; i < cage.positionCount; i++)
        {
            float angle = i * Mathf.PI / 4;
            cage.SetPosition(i, player.ActivePosition + new Vector3(Mathf.Cos(angle)*.8f, i%2 == 0 ? -.4f : 1.1f, Mathf.Sin(angle)*.8f));
        }
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
