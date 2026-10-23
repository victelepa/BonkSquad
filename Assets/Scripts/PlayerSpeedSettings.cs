using System;
using UnityEngine;
using UnityEngine.InputSystem;

// One instance lives on BonkRobotController. Both forms, enemies and the HUD read it.
[Serializable]
public sealed class PlayerSpeedSettings
{
    [Header("Shared speed scale (world units / second)")]
    [Min(0.1f)] [SerializeField] private float maximumSpeed = 18f;
    [Min(0f)] [SerializeField] private float robotBaseSpeed = 2f;
    [Min(0.01f)] [SerializeField] private float smallEnemyKillSpeed = 8f;
    [Min(0.02f)] [SerializeField] private float largeEnemyKillSpeed = 14f;
    [Tooltip("Constant speed lost per second above the robot base speed.")]
    [Min(0.01f)] [SerializeField] private float robotDeceleration = 2f;
    [Tooltip("Maximum inclusive angle between input and ball travel direction.")]
    [Range(0f, 180f)] [SerializeField] private float accelerationAngleTolerance = 30f;

    public float MaximumSpeed => maximumSpeed;
    public float RobotBaseSpeed => robotBaseSpeed;
    public float SmallEnemyKillSpeed => smallEnemyKillSpeed;
    public float LargeEnemyKillSpeed => largeEnemyKillSpeed;
    public float RobotDeceleration => robotDeceleration;
    public float AccelerationAngleTolerance => accelerationAngleTolerance;

    // Shared fallback for testing a form without a form-switching parent.
    public static PlayerSpeedSettings Default { get; } = new PlayerSpeedSettings();

    public void Validate()
    {
        maximumSpeed = Mathf.Max(0.1f, FiniteOr(maximumSpeed, 18f));
        robotBaseSpeed = Mathf.Clamp(FiniteOr(robotBaseSpeed, 2f), 0f, maximumSpeed);
        smallEnemyKillSpeed = Mathf.Clamp(FiniteOr(smallEnemyKillSpeed, 8f), 0.01f, maximumSpeed - 0.01f);
        largeEnemyKillSpeed = Mathf.Clamp(FiniteOr(largeEnemyKillSpeed, 14f), smallEnemyKillSpeed + 0.01f, maximumSpeed);
        robotDeceleration = Mathf.Max(0.01f, FiniteOr(robotDeceleration, 2f));
        accelerationAngleTolerance = Mathf.Clamp(FiniteOr(accelerationAngleTolerance, 30f), 0f, 180f);
    }

    private static float FiniteOr(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    public float KillThreshold(bool isSmallEnemy)
    {
        return isSmallEnemy ? smallEnemyKillSpeed : largeEnemyKillSpeed;
    }

    public bool CanKill(float playerSpeed, bool isSmallEnemy)
    {
        return playerSpeed >= KillThreshold(isSmallEnemy);
    }

    public float DecelerateRobot(float speed, float deltaTime)
    {
        return speed > robotBaseSpeed
            ? Mathf.Max(robotBaseSpeed, speed - robotDeceleration * deltaTime)
            : speed;
    }
}

public static class PlayerMovementMath
{
    public static Vector3 Planar(Vector3 velocity)
    {
        return new Vector3(velocity.x, 0f, velocity.z);
    }

    public static Vector3 ReadInputDirection(Transform cameraTransform)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector3.zero;
        float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
            - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
            - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
        Vector3 forward = cameraTransform != null ? Planar(cameraTransform.forward).normalized : Vector3.forward;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return (right * x + forward * z).normalized;
    }

    public static bool AllowsAcceleration(Vector3 travelDirection, Vector3 inputDirection, float tolerance)
    {
        // Angle comparison is inclusive. No input never accelerates.
        return travelDirection.sqrMagnitude > 0f && inputDirection.sqrMagnitude > 0f &&
            Vector3.Angle(travelDirection, inputDirection) <= tolerance + 0.0001f;
    }

    public static Vector3 ReflectPlanar(Vector3 velocity, Vector3 normal)
    {
        Vector3 planarNormal = Planar(normal).normalized;
        if (planarNormal.sqrMagnitude < 0.0001f) return velocity;
        return Vector3.Reflect(velocity, planarNormal).normalized * velocity.magnitude;
    }
}
