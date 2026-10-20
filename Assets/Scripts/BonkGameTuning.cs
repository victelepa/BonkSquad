using UnityEngine;

[CreateAssetMenu(fileName = "BonkGameTuning", menuName = "BonkSquad/Game Tuning")]
public class BonkGameTuning : ScriptableObject
{
    [Header("Player Health")]
    [Min(1)] public int playerHealth = 3;
    [Min(0f)] public float damageInvulnerability = 1.1f;
    [Min(0f)] public float damageKnockback = 5f;

    [Header("Human Movement")]
    [Min(0f)] public float humanMoveSpeed = 6f;

    [Header("Ball Movement")]
    [Min(0f)] public float ballAcceleration = 8f;
    [Min(0f)] public float ballSteeringAcceleration = 3f;
    [Min(0f)] public float ballBrakingAcceleration = 4f;
    [Min(0f)] public float ballNaturalSpeedLoss = 0.35f;
    [Min(0f)] public float ballNormalSpeedCap = 18f;
    [Min(0f)] public float ballAbsoluteSpeedCap = 24f;
    [Min(0f)] public float overcapSpeedLoss = 3f;
    [Min(1f)] public float launchMomentumMultiplier = 1.35f;

    [Header("Powerups")]
    [Min(0f)] public float speedPowerupBoost = 6f;
    [Min(0f)] public float powerupMinimumDelay = 5f;
    [Min(0f)] public float powerupMaximumDelay = 10f;
    [Min(1)] public int maximumActivePowerups = 3;

    [Header("Impact Tiers")]
    [Min(0f)] public float smallEnemyKillSpeed = 9f;
    [Min(0f)] public float heavyEnemyKillSpeed = 19f;
    [Min(0f)] public float weakHitKnockback = 5f;
    [Min(0f)] public float strongHitKnockback = 10f;

    [Header("Enemies")]
    [Min(0f)] public float chaserSpeed = 4f;
    [Min(0f)] public float heavyChaserSpeed = 2.5f;

    public static BonkGameTuning CreateRuntimeDefaults()
    {
        BonkGameTuning tuning = CreateInstance<BonkGameTuning>();
        tuning.name = "Runtime BonkGameTuning";
        tuning.hideFlags = HideFlags.HideAndDontSave;
        return tuning;
    }
}
