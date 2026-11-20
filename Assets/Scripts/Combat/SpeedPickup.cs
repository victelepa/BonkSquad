using UnityEngine;

public sealed class SpeedPickup : MonoBehaviour
{
    [Min(0.1f)] [SerializeField] private float speedAmount = 5f;
    private bool collected;
    public float SpeedAmount => speedAmount;

    private void Update()
    {
        transform.Rotate(0f, 100f * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other) => Collect(other);
    private void OnTriggerStay(Collider other) => Collect(other);

    private void Collect(Collider other)
    {
        if (collected) return;
        BonkRobotController player = other.GetComponentInParent<BonkRobotController>();
        if (player == null || player.IsEncounterLocked || !player.AddMomentum(speedAmount)) return;
        collected = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
