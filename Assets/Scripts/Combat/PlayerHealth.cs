using System;
using UnityEngine;

// Lives on the persistent player root, never on an individual form.
public sealed class PlayerHealth : MonoBehaviour
{
    [Min(1f)] [SerializeField] private float maximumHealth = 100f;
    [Min(0f)] [SerializeField] private float attackDamage = 20f;
    [Min(0f)] [SerializeField] private float protectionSeconds = 1f;
    public float Current { get; private set; }
    public float Maximum => maximumHealth;
    public bool IsDead => Current <= 0f;
    public string LastDamageLabel { get; private set; } = "";
    public float LastHitTime { get; private set; } = float.NegativeInfinity;
    public bool CombatEnabled { get; set; } = true;
    public event Action Changed;
    public event Action Died;
    public event Action Damaged;
    private float protectedUntil;

    private void Awake()
    {
        ResetHealth();
        if (GetComponent<PlayerDamageFeedback>() == null) gameObject.AddComponent<PlayerDamageFeedback>();
    }
    public void ResetHealth()
    {
        Current = maximumHealth;
        protectedUntil = float.NegativeInfinity;
        LastHitTime = float.NegativeInfinity;
        LastDamageLabel = "";
        GetComponent<PlayerDamageFeedback>()?.Clear();
        Changed?.Invoke();
    }

    public bool TryDamage() => TryDamage(attackDamage);
    public bool TryDamage(float damage) => ApplyDamage(damage, null);
    public bool TryDamageFrom(Vector3 sourcePosition) => TryDamageFrom(sourcePosition, "Enemy hit");
    public bool TryDamageFrom(Vector3 sourcePosition, string cause)
    {
        if (!ApplyDamage(attackDamage, sourcePosition)) return false;
        LastDamageLabel = $"{cause} −{attackDamage:0}"; return true;
    }

    private bool ApplyDamage(float damage, Vector3? sourcePosition)
    {
        if (!CombatEnabled || IsDead || damage <= 0f || Time.time < protectedUntil) return false;
        Current = Mathf.Max(0f, Current - damage);
        protectedUntil = Time.time + protectionSeconds;
        LastHitTime = Time.time;
        if (!IsDead && sourcePosition.HasValue)
            GetComponent<BonkRobotController>()?.ApplyDamageKnockback(sourcePosition.Value);
        Changed?.Invoke();
        Damaged?.Invoke();
        if (IsDead) Died?.Invoke();
        return true;
    }

    public bool TryHeal(float amount)
    {
        if (!CombatEnabled || IsDead || amount <= 0f || Current >= maximumHealth) return false;
        Current = Mathf.Min(maximumHealth, Current + amount);
        Changed?.Invoke();
        return true;
    }
}
