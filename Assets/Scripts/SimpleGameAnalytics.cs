using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(SimpleAnalyticsCharts))]
public sealed class SimpleGameAnalytics : MonoBehaviour
{
    [SerializeField] private BonkRobotController player;
    [SerializeField] private BouncingBallController ball;
    [SerializeField] private PlayerHealth health;

    [SerializeField] private float movementThreshold = 0.1f;
    [SerializeField] private float sampleInterval = 1f;

    public struct SpeedPoint
    {
        public float time;
        public float speed;

        public SpeedPoint(float time, float speed)
        {
            this.time = time;
            this.speed = speed;
        }
    }

    public bool HasSession { get; private set; }
    public bool Recording { get; private set; }

    public float ElapsedSeconds { get; private set; }
    public int SwitchCount { get; private set; }

    public float InputSeconds { get; private set; }
    public float AccelerationSeconds { get; private set; }

    public int StunCount { get; private set; }
    public int PostStunDeaths { get; private set; }

    public float KillSpeedThreshold { get; private set; }
    public float FirstThresholdTime { get; private set; } = -1f;

    public float SwitchesPerMinute =>
        ElapsedSeconds > 0f
            ? SwitchCount * 60f / ElapsedSeconds
            : 0f;

    public float AccelerationPercent =>
        InputSeconds > 0f
            ? AccelerationSeconds * 100f / InputSeconds
            : 0f;

    public float PostStunDeathPercent =>
        StunCount > 0
            ? PostStunDeaths * 100f / StunCount
            : 0f;

    public int PendingStuns => recentStuns.Count;

    public readonly List<SpeedPoint> SpeedHistory =
        new List<SpeedPoint>();

    private readonly List<float> recentStuns = new List<float>();

    private SimpleAnalyticsCharts charts;
    private Transform cameraTransform;

    private bool waitingForMovement = true;
    private bool previousBallForm;
    private bool previousStunned;
    private float nextSampleTime;

    private bool CanRecord =>
        player != null &&
        player.isActiveAndEnabled &&
        !player.IsEncounterLocked &&
        Time.timeScale > 0f &&
        (health == null || (!health.IsDead && health.CombatEnabled));

    private void Awake()
    {
        if (player == null)
            player = GetComponent<BonkRobotController>();

        if (ball == null)
            ball = GetComponentInChildren<BouncingBallController>(true);

        if (health == null)
            health = GetComponent<PlayerHealth>();

        charts = GetComponent<SimpleAnalyticsCharts>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        if (health != null)
            health.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= OnPlayerDied;
    }

    private void Update()
    {
        bool pressedR = Keyboard.current != null
            ? Keyboard.current.rKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.R);

        if (!pressedR)
            return;

        if (charts.IsVisible)
        {
            charts.Hide();
            waitingForMovement = true;
        }
        else
        {
            EndSession();
            charts.Show(this);
        }
    }

    private void TryBeginOnMovement()
    {
        if (waitingForMovement &&
            !charts.IsVisible &&
            CanRecord &&
            player.CurrentSpeed > movementThreshold)
        {
            BeginSession();
        }
    }

    public void BeginSession()
    {
        waitingForMovement = false;
        HasSession = true;
        Recording = true;

        ElapsedSeconds = 0f;
        SwitchCount = 0;
        InputSeconds = 0f;
        AccelerationSeconds = 0f;
        StunCount = 0;
        PostStunDeaths = 0;
        FirstThresholdTime = -1f;

        KillSpeedThreshold =
            player.SpeedSettings.SmallEnemyKillSpeed;

        recentStuns.Clear();
        SpeedHistory.Clear();

        CacheState();
        RecordSpeed();

        nextSampleTime = Mathf.Max(0.2f, sampleInterval);
    }

    public void EndSession()
    {
        if (!Recording)
            return;

        ObserveStateChanges();
        RecordSpeed();

        Recording = false;
    }

    private void LateUpdate()
    {
        TryBeginOnMovement();

        if (!Recording)
            return;

        if (!CanRecord)
        {
            CacheState();
            return;
        }

        ElapsedSeconds += Time.deltaTime;

        ObserveStateChanges();

        if (FirstThresholdTime < 0f &&
            player.CurrentSpeed >= KillSpeedThreshold)
        {
            FirstThresholdTime = ElapsedSeconds;
        }

        for (int i = recentStuns.Count - 1; i >= 0; i--)
        {
            if (ElapsedSeconds - recentStuns[i] > 5f)
                recentStuns.RemoveAt(i);
        }

        if (ElapsedSeconds >= nextSampleTime)
        {
            RecordSpeed();

            nextSampleTime =
                ElapsedSeconds + Mathf.Max(0.2f, sampleInterval);
        }
    }

    private void FixedUpdate()
    {
        TryBeginOnMovement();

        if (!Recording || !CanRecord ||
            !player.IsBallForm || player.IsKnockedBack ||
            ball == null || !ball.isActiveAndEnabled)
        {
            return;
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector3 input =
            PlayerMovementMath.ReadInputDirection(cameraTransform);

        if (input.sqrMagnitude <= 0f)
            return;

        InputSeconds += Time.fixedDeltaTime;

        if (ball.AccelerationEfficiency01 > 0f)
            AccelerationSeconds += Time.fixedDeltaTime;
    }

    private void ObserveStateChanges()
    {
        bool ballForm = player.IsBallForm;
        bool stunned =
            player.DizzinessPhase == PlayerDizzinessPhase.Stunned;
        
        if (ballForm != previousBallForm &&
            !stunned && !previousStunned)
        {
            SwitchCount++;
        }

        if (stunned && !previousStunned)
        {
            StunCount++;
            recentStuns.Add(ElapsedSeconds);
        }

        previousBallForm = ballForm;
        previousStunned = stunned;
    }

    private void CacheState()
    {
        previousBallForm = player.IsBallForm;

        previousStunned =
            player.DizzinessPhase == PlayerDizzinessPhase.Stunned;
    }

    private void RecordSpeed()
    {
        float speed = player.CurrentSpeed;

        if (FirstThresholdTime < 0f &&
            speed >= KillSpeedThreshold)
        {
            FirstThresholdTime = ElapsedSeconds;
        }

        var point = new SpeedPoint(ElapsedSeconds, speed);
        int count = SpeedHistory.Count;

        if (count > 0 &&
            Mathf.Abs(SpeedHistory[count - 1].time -
                      ElapsedSeconds) < 0.001f)
        {
            SpeedHistory[count - 1] = point;
        }
        else
        {
            SpeedHistory.Add(point);
        }

        if (SpeedHistory.Count > 300)
            SpeedHistory.RemoveAt(0);
    }

    private void OnPlayerDied()
    {
        if (!Recording)
            return;

        ObserveStateChanges();

        foreach (float stunTime in recentStuns)
        {
            if (ElapsedSeconds - stunTime <= 5f)
                PostStunDeaths++;
        }

        recentStuns.Clear();
        EndSession();
    }
}