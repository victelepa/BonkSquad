using System;
using UnityEngine;

// Explicit timestamps make combo boundaries deterministic and independent of physics callbacks.
[Serializable]
public sealed class PinballScore
{
    [Min(1)] [SerializeField] private int minionPoints = 10;
    [Min(1)] [SerializeField] private int specialistPoints = 50;
    [Min(1)] [SerializeField] private int pointsPerCharge = 100;
    [Min(1)] [SerializeField] private int pointsPerDashUse = 100;
    [Min(.1f)] [SerializeField] private float comboWindow = 3f;
    [Min(0)] [SerializeField] private int ricochetBonus = 25;
    [Min(0)] [SerializeField] private int doubleBonus = 30;
    [Min(0)] [SerializeField] private int tripleBonus = 50;
    public int Points { get; private set; }
    public int Progress { get; private set; }
    public int DashProgress { get; private set; }
    public int ImpactCharges { get; private set; }
    public event Action<int> DashUsesEarned;
    public int ChainCount { get; private set; }
    public int ChargeThreshold => pointsPerCharge;
    public int DashThreshold => Mathf.Max(1, pointsPerDashUse);
    public float DashRecharge01 => Mathf.Clamp01((float)DashProgress / DashThreshold);
    public string Feedback { get; private set; } = "";
    public float FeedbackUntil { get; private set; }
    public float FeedbackStartedAt { get; private set; }
    public bool FeedbackIsReward { get; private set; }
    private float lastKill = float.NegativeInfinity;
    private float bounceUntil = float.NegativeInfinity;

    public struct Checkpoint
    {
        public int points, progress, dashProgress, charges;
    }
    public Checkpoint Save() => new Checkpoint { points = Points, progress = Progress, dashProgress = DashProgress, charges = ImpactCharges };
    public void Restore(Checkpoint checkpoint)
    {
        Points = checkpoint.points; Progress = checkpoint.progress; DashProgress = checkpoint.dashProgress;
        ImpactCharges = checkpoint.charges;
        BreakCombo(0f, false);
    }
    public void Reset() => Restore(default);
    public void RegisterBounce(float now) => bounceUntil = now + comboWindow;
    public void Tick(float now)
    {
        if (now - lastKill > comboWindow) ChainCount = 0;
    }
    public void BreakCombo(float now, bool showFeedback = true)
    {
        bool hadCombo = ChainCount > 0 || now <= bounceUntil;
        ChainCount = 0;
        lastKill = bounceUntil = float.NegativeInfinity;
        Feedback = showFeedback && hadCombo ? "Combo interrupted" : "";
        FeedbackStartedAt = now;
        FeedbackIsReward = false;
        FeedbackUntil = now + 1.5f;
    }
    public int RecordKill(bool minion, float now)
    {
        ChainCount = now - lastKill <= comboWindow ? ChainCount + 1 : 1;
        lastKill = now;
        int earned = minion ? minionPoints : specialistPoints;
        Feedback = "";
        if (now <= bounceUntil)
        {
            earned += ricochetBonus;
            Feedback = $"Ricochet Shot +{ricochetBonus}";
        }
        bounceUntil = float.NegativeInfinity; // A bounce rewards only the next defeat.
        if (ChainCount == 2)
        {
            earned += doubleBonus;
            Feedback += (Feedback.Length > 0 ? " · " : "") + $"Double Bonk +{doubleBonus}";
        }
        else if (ChainCount == 3)
        {
            earned += tripleBonus;
            Feedback += (Feedback.Length > 0 ? " · " : "") + $"Triple Bonk +{tripleBonus}";
        }
        Points += earned;
        Progress += earned;
        int chargeThreshold = Mathf.Max(1, pointsPerCharge);
        int granted = Progress / chargeThreshold;
        Progress %= chargeThreshold;
        ImpactCharges += granted;
        if (granted > 0) Feedback += (Feedback.Length > 0 ? " · " : "") + "Impact Charge ready!";
        DashProgress += earned;
        int dashUsesEarned = DashProgress / DashThreshold;
        DashProgress %= DashThreshold;
        if (dashUsesEarned > 0) DashUsesEarned?.Invoke(dashUsesEarned);
        FeedbackStartedAt = now;
        FeedbackIsReward = Feedback.Length > 0;
        FeedbackUntil = now + 2f;
        return earned;
    }
    public bool ConsumeImpactCharge()
    {
        if (ImpactCharges <= 0) return false;
        ImpactCharges--;
        return true;
    }
}
