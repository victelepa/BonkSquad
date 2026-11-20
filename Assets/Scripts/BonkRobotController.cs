using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BonkRobotController : MonoBehaviour
{
    [Header("Speed System (shared by both forms and HUD)")]
    [SerializeField] private PlayerSpeedSettings speedSettings = new PlayerSpeedSettings();
    public PlayerSpeedSettings SpeedSettings => speedSettings;
    public Vector3 CurrentPlanarVelocity => IsBallForm
        ? (ballController != null ? ballController.PlanarVelocity : Vector3.zero)
        : (standingController != null ? standingController.CurrentPlanarVelocity : Vector3.zero);
    public float CurrentSpeed => CurrentPlanarVelocity.magnitude;

    [Header("Dizziness System")]
    [SerializeField] private PlayerDizzinessSettings dizzinessSettings = new PlayerDizzinessSettings();
    public PlayerDizzinessSettings DizzinessSettings => dizzinessSettings;
    public PlayerDizzinessPhase DizzinessPhase { get; private set; }
    public float DizzinessValue { get; private set; }
    public float DizzinessRatio => DizzinessValue / dizzinessSettings.MaximumDizziness;
    public float PhaseTimeRemaining { get; private set; }
    public event System.Action SwitchedToRobotManually;
    public bool IsEncounterLocked { get; private set; }
    public bool CanAcceptPlayerInput => !IsEncounterLocked && !IsSnared && DizzinessPhase != PlayerDizzinessPhase.Stunned;
    public Vector3 ActivePosition => IsBallForm ? ballForm.transform.position : standingForm.transform.position;

    [Header("Damage Knockback")]
    [Min(0f)] [SerializeField] private float knockbackStrength = 6f;
    [Min(0f)] [SerializeField] private float knockbackRecoverySeconds = .2f;
    private float knockbackUntil;
    public bool IsKnockedBack => Time.time < knockbackUntil;

    [Header("Ball Dash")]
    [Min(0)] [SerializeField] private int dashUsesPerRun = 4;
    [Min(0f)] [SerializeField] private float dashCooldownSeconds = 5f;
    private int dashUsesRemaining;
    private float nextDashReadyTime;
    private EnemyWaveController encounter;
    private float snaredUntil, netImmuneUntil;
    public bool IsSnared => Time.time < snaredUntil;
    public float SnareRemaining => Mathf.Max(0f, snaredUntil - Time.time);
    public int DashUsesPerRun => dashUsesPerRun;
    public int DashUsesRemaining => dashUsesRemaining;
    public float DashCooldownRemaining => Mathf.Max(0f, nextDashReadyTime - Time.time);
    internal bool DashEnabled { get; set; } = true;
    public bool CanDash => DashAllowedByState && TryFindDashDirection(out _);

    public readonly struct DashCheckpoint
    {
        public readonly int usesRemaining;
        public readonly float cooldownRemaining;
        public DashCheckpoint(int uses, float cooldown)
        {
            usesRemaining = uses;
            cooldownRemaining = cooldown;
        }
    }

    public void ConfigureEncounter(EnemyWaveController owner) => encounter = owner;

    public void ResetDashForRun()
    {
        dashUsesRemaining = Mathf.Min(4, dashUsesPerRun);
        nextDashReadyTime = Time.time;
    }

    public int RestoreDashUses(int count)
    {
        int before = dashUsesRemaining;
        dashUsesRemaining = Mathf.Min(Mathf.Min(4, dashUsesPerRun), dashUsesRemaining + Mathf.Max(0, count));
        return dashUsesRemaining - before;
    }

    public bool TrySnare(float duration, float immunity)
    {
        if (IsEncounterLocked || IsSnared || DizzinessPhase == PlayerDizzinessPhase.Stunned ||
            Time.time < netImmuneUntil || (encounter != null && !encounter.IsCombatActive)) return false;
        if (GetComponent<NetSnareFeedback>() == null) gameObject.AddComponent<NetSnareFeedback>();
        snaredUntil = Time.time + Mathf.Max(0f, duration);
        netImmuneUntil = snaredUntil + Mathf.Max(0f, immunity);
        ballController.StopForSnare();
        standingController.StopForSnare();
        encounter?.Scoring.BreakCombo(Time.time);
        return true;
    }

    public DashCheckpoint SaveDashCheckpoint() => new DashCheckpoint(dashUsesRemaining, DashCooldownRemaining);

    public void RestoreDashCheckpoint(DashCheckpoint checkpoint)
    {
        dashUsesRemaining = Mathf.Clamp(checkpoint.usesRemaining, 0, Mathf.Min(4, dashUsesPerRun));
        nextDashReadyTime = Time.time + Mathf.Max(0f, checkpoint.cooldownRemaining);
    }

    private bool DashAllowedByState => DashEnabled && isActiveAndEnabled && IsBallForm && CanAcceptPlayerInput &&
        (encounter == null || encounter.IsCombatActive) && dashUsesRemaining > 0 && DashCooldownRemaining <= 0f;

    private bool TryFindDashDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        IEnumerable<EnemyBumper> candidates = encounter != null
            ? encounter.LivingEnemyObjects
            : FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None);
        float nearestDistance = float.PositiveInfinity;
        Vector3 origin = ballForm.transform.position;
        foreach (EnemyBumper enemy in candidates)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDefeated) continue;
            Collider shape = enemy.GetComponent<Collider>();
            Vector3 center = shape != null ? shape.bounds.center : enemy.transform.position;
            Vector3 offset = PlayerMovementMath.Planar(center - origin);
            float distance = offset.sqrMagnitude;
            if (distance < 0.0001f || distance >= nearestDistance) continue;
            nearestDistance = distance;
            direction = offset;
        }
        return nearestDistance < float.PositiveInfinity;
    }

    public bool TryUseDash()
    {
        if (!DashAllowedByState || !TryFindDashDirection(out Vector3 direction) ||
            ballController == null || !ballController.DashTowards(direction)) return false;
        dashUsesRemaining--;
        nextDashReadyTime = Time.time + dashCooldownSeconds;
        return true;
    }

    public void SetEncounterLocked(bool locked, bool clearKnockback = true)
    {
        IsEncounterLocked = locked;
        if (locked && clearKnockback) knockbackUntil = 0f;
    }

    public void ApplyDamageKnockback(Vector3 sourcePosition)
    {
        if (IsEncounterLocked || knockbackStrength <= 0f) return;
        Vector3 direction = PlayerMovementMath.Planar(ActivePosition - sourcePosition);
        if (direction.sqrMagnitude < .0001f)
        {
            Vector3 velocity = CurrentPlanarVelocity;
            direction = velocity.sqrMagnitude > .0001f ? -velocity :
                -(IsBallForm ? ballForm.transform.forward : standingForm.transform.forward);
        }
        direction = PlayerMovementMath.Planar(direction).normalized;
        if (direction.sqrMagnitude < .0001f) direction = Vector3.back;
        knockbackUntil = Time.time + Mathf.Max(0f, knockbackRecoverySeconds);
        if (IsBallForm) ballController.ApplyKnockback(direction, knockbackStrength);
        else standingController.ApplyKnockback(direction, knockbackStrength);
    }

    public void ResetForEncounter(Vector3 position, Quaternion rotation)
    {
        snaredUntil = netImmuneUntil = float.NegativeInfinity;
        knockbackUntil = 0f;
        DizzinessPhase = PlayerDizzinessPhase.Normal;
        DizzinessValue = 0f;
        PhaseTimeRemaining = 0f;
        ballController.ExitBallMode();
        ballForm.SetActive(false);
        standingForm.SetActive(true);
        IsBallForm = false;
        standingController.Teleport(position, rotation);
        standingController.SetControlLocked(false);
        standingController.EnterStandingMode(Vector3.zero);
        ballForm.transform.SetPositionAndRotation(position, rotation);
        cameraTarget = standingForm.transform;
        cameraVelocity = Vector3.zero;
    }

    [Header("Forms")]
    [SerializeField] private GameObject standingForm;
    [SerializeField] private GameObject ballForm;
    [SerializeField] private BouncingBallController ballController;
    [SerializeField] private PlayerController standingController;
    [Tooltip("Start the scene in ball form instead of normal form.")]
    [SerializeField] private bool startAsBall;

    [Header("Input")]
    [Tooltip("Input action used to switch between normal and ball forms.")]
    [SerializeField] private InputActionReference switchFormAction;

    [Header("Camera Follow")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Camera offset from the currently active form.")]
    [SerializeField] private Vector3 cameraOffset =
        new Vector3(0f, 6f, -8f);

    [Tooltip("Camera follow smoothing time. Lower values respond faster.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float cameraSmoothTime = 0.2f;

    public bool IsBallForm { get; private set; }

    private Transform cameraTarget;
    private Vector3 cameraVelocity;
    private void OnValidate()
    {
        if (speedSettings == null) speedSettings = new PlayerSpeedSettings();
        speedSettings.Validate();
        if (dizzinessSettings == null) dizzinessSettings = new PlayerDizzinessSettings();
        dizzinessSettings.Validate();
        DizzinessValue = Mathf.Clamp(DizzinessValue, 0f, dizzinessSettings.MaximumDizziness);
        dashUsesPerRun = Mathf.Clamp(dashUsesPerRun, 0, 4);
        dashCooldownSeconds = Mathf.Max(0f, dashCooldownSeconds);
    }

    private void Awake()
    {
        OnValidate();
        ResetDashForRun();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (ballController == null && ballForm != null)
        {
            ballController = ballForm.GetComponent<BouncingBallController>();
        }

        if (standingController == null && standingForm != null)
        {
            standingController = standingForm.GetComponent<PlayerController>();
        }

        if (standingForm == null || ballForm == null ||
            ballController == null || standingController == null)
        {
            Debug.LogError("BonkRobotController requires both forms and their movement controllers.", this);
            enabled = false;
            return;
        }

        ballController.Configure(this);
        standingController.Configure(this);
        standingController.SetControlLocked(false);
        IsBallForm = startAsBall;

        standingForm.SetActive(!IsBallForm);
        ballForm.SetActive(IsBallForm);
        
        if (IsBallForm) ballController.EnterBallMode(Vector3.zero);
        else standingController.EnterStandingMode(Vector3.zero);

        cameraTarget = IsBallForm
            ? ballForm.transform
            : standingForm.transform;
    }

    private void OnEnable()
    {
        if (switchFormAction == null)
        {
            return;
        }

        switchFormAction.action.performed += OnSwitchForm;
        switchFormAction.action.Enable();
    }

    private void OnDisable()
    {
        if (switchFormAction == null)
        {
            return;
        }

        switchFormAction.action.performed -= OnSwitchForm;
        switchFormAction.action.Disable();
    }

    private void OnSwitchForm(
        InputAction.CallbackContext context)
    {
        ToggleForm();
    }

    public void ToggleForm()
    {
        if (!isActiveAndEnabled || !CanAcceptPlayerInput) return;
        if (!IsBallForm)
        {
            Vector3 inheritedVelocity = standingController != null
                ? standingController.CurrentPlanarVelocity
                : Vector3.zero;

            ballForm.transform.SetPositionAndRotation(
                standingForm.transform.position,
                standingForm.transform.rotation);

            standingForm.SetActive(false);
            ballForm.SetActive(true);
            IsBallForm = true;
            cameraTarget = ballForm.transform;

            if (ballController != null)
            {
                ballController.EnterBallMode(inheritedVelocity);
            }
        }
        else
        {
            Vector3 ballVelocity = ballController != null
                ? ballController.PlanarVelocity
                : Vector3.zero;
            SwitchToRobot(ballVelocity);
            if (DizzinessPhase == PlayerDizzinessPhase.Warning)
                CancelWarning();
            SwitchedToRobotManually?.Invoke();
        }
    }

    private void SwitchToRobot(Vector3 inheritedVelocity)
    {
        Vector3 standingPosition = ballForm.transform.position;
        Quaternion standingRotation = inheritedVelocity.sqrMagnitude > 0f
            ? Quaternion.LookRotation(inheritedVelocity.normalized)
            : ballForm.transform.rotation;

        ballController.ExitBallMode();
        ballForm.SetActive(false);
        standingForm.SetActive(true);
        IsBallForm = false;
        standingController.Teleport(standingPosition, standingRotation);
        standingController.EnterStandingMode(inheritedVelocity);
        cameraTarget = standingForm.transform;
    }

    private void Update()
    {
        if (IsEncounterLocked || IsSnared) return;
        bool dashPressed = Keyboard.current != null
            ? Keyboard.current.qKey.wasPressedThisFrame
            : UnityEngine.Input.GetKeyDown(KeyCode.Q);
        if (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame)
            dashPressed = true;
        if (dashPressed) TryUseDash();
        if (Keyboard.current == null && UnityEngine.Input.GetKeyDown(KeyCode.Space))
            ToggleForm();

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        switch (DizzinessPhase)
        {
            case PlayerDizzinessPhase.Normal:
                UpdateNormalDizziness(deltaTime);
                break;
            case PlayerDizzinessPhase.Warning:
                PhaseTimeRemaining = Mathf.Max(0f, PhaseTimeRemaining - deltaTime);
                if (PhaseTimeRemaining <= 0f) EnterStun();
                break;
            case PlayerDizzinessPhase.Stunned:
                RecoverAsRobot(deltaTime);
                PhaseTimeRemaining = Mathf.Max(0f, PhaseTimeRemaining - deltaTime);
                if (PhaseTimeRemaining <= 0f)
                {
                    DizzinessPhase = PlayerDizzinessPhase.Normal;
                    standingController.SetControlLocked(false);
                }
                break;
        }
    }

    private void UpdateNormalDizziness(float deltaTime)
    {
        if (!IsBallForm)
        {
            RecoverAsRobot(deltaTime);
            return;
        }

        float speed = CurrentSpeed;
        float threshold = dizzinessSettings.EffectiveHighSpeedThreshold(speedSettings);
        if (speed >= threshold)
        {
            DizzinessValue = Mathf.Min(dizzinessSettings.MaximumDizziness,
                DizzinessValue + dizzinessSettings.GainRate(speed, speedSettings) * deltaTime);
            if (DizzinessValue >= dizzinessSettings.MaximumDizziness)
            {
                DizzinessPhase = PlayerDizzinessPhase.Warning;
                PhaseTimeRemaining = dizzinessSettings.WarningDuration;
            }
        }
        else
        {
            DizzinessValue = Mathf.Max(0f,
                DizzinessValue - dizzinessSettings.BallLowSpeedRecovery * deltaTime);
        }
    }

    private void RecoverAsRobot(float deltaTime)
    {
        DizzinessValue = Mathf.Max(0f,
            DizzinessValue - dizzinessSettings.RobotRecovery * deltaTime);
    }

    private void CancelWarning()
    {
        DizzinessPhase = PlayerDizzinessPhase.Normal;
        PhaseTimeRemaining = 0f;
    }

    private void EnterStun()
    {
        DizzinessPhase = PlayerDizzinessPhase.Stunned;
        PhaseTimeRemaining = dizzinessSettings.StunDuration;
        // The forced transition discards momentum instead of using normal robot deceleration.
        SwitchToRobot(Vector3.zero);
        standingController.SetControlLocked(true);
    }

    private void LateUpdate()
    {
        FollowActiveForm();
    }

    private void FollowActiveForm()
    {
        if (cameraTransform == null ||
            cameraTarget == null)
        {
            return;
        }

        Vector3 desiredPosition =
            cameraTarget.position + cameraOffset;

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref cameraVelocity,
            cameraSmoothTime
        );
        
    }
}
