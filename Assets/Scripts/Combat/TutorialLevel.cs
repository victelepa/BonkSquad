using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class TutorialLevel : MonoBehaviour
{
    public const string SceneName = "TutorialArena";
    private const string MenuSceneName = "Map01_RefractionArena";

    [SerializeField] private BonkRobotController player;
    [SerializeField] private BouncingBallController ball;
    [SerializeField] private EnemyBumper[] practiceTargets;

    private enum Step { Move, Bounce, LowSpeedHit, HighSpeedHit, Stun, Recover, Dash, Complete }

    private Step step;
    private VisualElement root;
    private VisualElement[] pages;
    private Label progress, dizzinessCountdown;
    private VisualElement dizzinessMeter, dizzinessFill;
    private DashHud dash;
    private TutorialDemoHud demo;
    private float accelerationTime;
    private const float CompletionHoldSeconds = 5f;
    private bool stunned, showingCompletion;
    private float nextStepTime;
    private VisualElement completionMark;
    private EnemyBumper[] liveTargets;

    private void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        pages = root.Q("tutorial-stages").Children().ToArray();
        progress = root.Q<Label>("tutorial-progress");
        dizzinessCountdown = root.Q<Label>("dizziness-countdown");
        dizzinessMeter = root.Q("practice-dizziness");
        dizzinessFill = root.Q("practice-fill");
        completionMark = root.Q("practice-completed");
        dash = new DashHud(root, player);
        demo = new TutorialDemoHud(root);
        root.Q<Button>("tutorial-exit").clicked += ReturnToMenu;
        root.Q<Button>("tutorial-done").clicked += ReturnToMenu;
        liveTargets = new EnemyBumper[practiceTargets.Length];
        ball.WallBounced += OnWallBounced;
        ball.DashHitEnemy += OnDashHitEnemy;
        ball.EnemyImpact += OnEnemyImpact;
        player.SwitchedToRobotManually += OnSwitchedToRobotManually;
        ShowStep(Step.Move);
    }

    private void OnDestroy()
    {
        demo?.Stop();
        if (ball != null)
        {
            ball.WallBounced -= OnWallBounced;
            ball.DashHitEnemy -= OnDashHitEnemy;
            ball.EnemyImpact -= OnEnemyImpact;
        }
        if (player != null) player.SwitchedToRobotManually -= OnSwitchedToRobotManually;
    }

    private void OnWallBounced()
    {
        if (step == Step.Bounce) CompleteStep();
    }

    private void OnDashHitEnemy(EnemyBumper enemy)
    {
        if (step == Step.Dash && liveTargets.Contains(enemy)) CompleteStep();
    }

    private void OnSwitchedToRobotManually()
    {
        if (step == Step.Recover && player.DizzinessValue > 0f) CompleteStep();
    }

    private void OnEnemyImpact(EnemyBumper enemy, EnemyHitResult hit)
    {
        if (!liveTargets.Contains(enemy)) return;
        if ((step == Step.LowSpeedHit && hit == EnemyHitResult.Rejected)
            || (step == Step.HighSpeedHit && hit == EnemyHitResult.Defeated))
            CompleteStep();
    }

    private void CompleteStep()
    {
        if (showingCompletion) return;

        showingCompletion = true;
        nextStepTime = Time.unscaledTime + CompletionHoldSeconds;
        Show(completionMark, true);
        dash.Refresh(step == Step.Dash);
    }

    private void ReturnToMenu()
    {
        SceneManager.LoadScene(MenuSceneName);
    }

    private static void Show(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);

    private void ShowStep(Step next)
    {
        step = next;
        demo.Show(next switch
        {
            Step.LowSpeedHit => TutorialDemoHud.Sequence.LowSpeed,
            Step.HighSpeedHit => TutorialDemoHud.Sequence.HighSpeed,
            Step.Dash => TutorialDemoHud.Sequence.Dash,
            _ => TutorialDemoHud.Sequence.None
        });
        showingCompletion = false;
        Show(completionMark, false);
        player.DashEnabled = step == Step.Dash;
        for (int i = 0; i < pages.Length; i++) Show(pages[i], i == (int)step);
        Show(progress, step != Step.Complete);
        progress.text = $"Practice  {(int)step + 1} / {(int)Step.Complete}";
        Show(dizzinessMeter, step == Step.Stun || step == Step.Recover);

        ClearPracticeTargets();
        if (step == Step.LowSpeedHit || step == Step.HighSpeedHit || step == Step.Dash)
        {
            SpawnPracticeTargets();
        }
        dash.Refresh(step == Step.Dash);
    }

    private void ClearPracticeTargets()
    {
        for (int i = 0; i < liveTargets.Length; i++)
        {
            if (liveTargets[i] != null)
            {
                liveTargets[i].gameObject.SetActive(false);
                Destroy(liveTargets[i].gameObject);
            }
            liveTargets[i] = null;
        }
    }

    private void SpawnPracticeTargets()
    {
        for (int i = 0; i < practiceTargets.Length; i++)
        {
            if (step != Step.Dash && !practiceTargets[i].IsSmallEnemy) continue;
            if (liveTargets[i] != null && !liveTargets[i].IsDefeated) continue;
            // Keep a target available if the player bonks it before trying Dash.
            var target = Instantiate(practiceTargets[i], practiceTargets[i].transform.parent);
            target.name = practiceTargets[i].name;
            target.gameObject.SetActive(true);
            liveTargets[i] = target;
        }
    }

    private void Update()
    {
        if (root == null) return;
        dizzinessFill.style.width = Length.Percent(player.DizzinessRatio * 100f);
        dash.Refresh(step == Step.Dash);

        bool showCountdown = (step == Step.Stun || step == Step.Recover)
            && player.DizzinessPhase == PlayerDizzinessPhase.Warning;
        Show(dizzinessCountdown, showCountdown);
        if (showCountdown)
            dizzinessCountdown.text = $"Warning: {Mathf.Ceil(player.PhaseTimeRemaining * 10f) / 10f:0.0}s";

        if (showingCompletion)
        {
            if (Time.unscaledTime < nextStepTime) return;
            ShowStep((Step)((int)step + 1));
            return;
        }

        switch (step)
        {
            case Step.Move:
                if (player.IsBallForm && ball.AccelerationEfficiency01 > .1f)
                    accelerationTime += Time.deltaTime;
                if (accelerationTime >= .2f) CompleteStep();
                break;
            case Step.Stun:
                UpdateStunPractice();
                break;
            case Step.Recover:
                UpdateRecoveryPractice();
                break;
            case Step.LowSpeedHit:
            case Step.HighSpeedHit:
                SpawnPracticeTargets();
                break;
            case Step.Dash:
                SpawnPracticeTargets();
                if (player.DashUsesRemaining == 0 && player.DashCooldownRemaining <= 0f)
                    player.ResetDashForRun();
                break;
        }
    }

    private void UpdateStunPractice()
    {
        bool warning = player.DizzinessPhase == PlayerDizzinessPhase.Warning;
        bool inStun = player.DizzinessPhase == PlayerDizzinessPhase.Stunned;
        stunned |= inStun;
        Show(root.Q("stun-build"), !warning && !inStun);
        Show(root.Q("stun-warning"), warning);
        Show(root.Q("stunned"), inStun);
        if (stunned && !inStun) CompleteStep();
    }

    private void UpdateRecoveryPractice()
    {
        var phase = player.DizzinessPhase;
        Show(root.Q("recover-build"), phase == PlayerDizzinessPhase.Normal);
        Show(root.Q("recover-switch"), phase == PlayerDizzinessPhase.Warning);
        Show(root.Q("recover-missed"), phase == PlayerDizzinessPhase.Stunned);
    }
}
