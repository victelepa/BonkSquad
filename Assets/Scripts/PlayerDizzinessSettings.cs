using System;
using UnityEngine;

public enum PlayerDizzinessPhase
{
    Normal,
    Warning,
    Stunned
}

[Serializable]
public sealed class PlayerDizzinessSettings
{
    [Header("Dizziness Meter")]
    [Min(1f)] [SerializeField] private float maximumDizziness = 100f;
    [Tooltip("Use the small-enemy kill speed as the high-speed threshold.")]
    [SerializeField] private bool useSmallEnemyKillSpeed = true;
    [Min(0.01f)] [SerializeField] private float highSpeedThreshold = 8f;
    [Tooltip("Dizziness gained per second. X is speed from threshold (0) to maximum speed (1); Y is gain per second.")]
    [SerializeField] private AnimationCurve gainRateBySpeed = new AnimationCurve(
        new Keyframe(0f, 8f), new Keyframe(1f, 20f));
    [Min(0f)] [SerializeField] private float ballLowSpeedRecovery = 3f;
    [Min(0f)] [SerializeField] private float robotRecovery = 25f;

    [Header("Durations (game seconds)")]
    [Min(0.01f)] [SerializeField] private float warningDuration = 2.5f;
    [Min(0.01f)] [SerializeField] private float stunDuration = 2.5f;

    public float MaximumDizziness => maximumDizziness;
    public float BallLowSpeedRecovery => ballLowSpeedRecovery;
    public float RobotRecovery => robotRecovery;
    public float WarningDuration => warningDuration;
    public float StunDuration => stunDuration;

    public float EffectiveHighSpeedThreshold(PlayerSpeedSettings speedSettings)
    {
        return useSmallEnemyKillSpeed
            ? speedSettings.SmallEnemyKillSpeed
            : highSpeedThreshold;
    }

    public float GainRate(float speed, PlayerSpeedSettings speedSettings)
    {
        float threshold = EffectiveHighSpeedThreshold(speedSettings);
        if (speed < threshold) return 0f;
        float speedRange = Mathf.Max(0.01f, speedSettings.MaximumSpeed - threshold);
        float normalizedSpeed = Mathf.Clamp01((speed - threshold) / speedRange);
        return Mathf.Max(0f, gainRateBySpeed.Evaluate(normalizedSpeed));
    }

    public void Validate()
    {
        maximumDizziness = Mathf.Max(1f, FiniteOr(maximumDizziness, 100f));
        highSpeedThreshold = Mathf.Max(0.01f, FiniteOr(highSpeedThreshold, 8f));
        ballLowSpeedRecovery = Mathf.Max(0f, FiniteOr(ballLowSpeedRecovery, 3f));
        robotRecovery = Mathf.Max(0f, FiniteOr(robotRecovery, 25f));
        warningDuration = Mathf.Max(0.01f, FiniteOr(warningDuration, 2.5f));
        stunDuration = Mathf.Max(0.01f, FiniteOr(stunDuration, 2.5f));
        if (gainRateBySpeed == null || gainRateBySpeed.length == 0)
            gainRateBySpeed = new AnimationCurve(new Keyframe(0f, 8f), new Keyframe(1f, 20f));
    }

    private static float FiniteOr(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }
}
