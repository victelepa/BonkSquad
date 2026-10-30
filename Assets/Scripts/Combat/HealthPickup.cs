using UnityEngine;

public sealed class HealthPickup : MonoBehaviour
{
    [Min(1f)] [SerializeField] private float healAmount = 25f;
    private bool collected;
    private void OnTriggerEnter(Collider other) => Collect(other);
    private void OnTriggerStay(Collider other) => Collect(other);
    private void Collect(Collider other)
    {
        if (collected) return;
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health != null && health.TryHeal(healAmount))
        {
            collected = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
