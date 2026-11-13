using UnityEngine;
using UnityEngine.UIElements;

// Shared binding for the Dash template in the arena and practice scene.
internal sealed class DashHud
{
    private readonly BonkRobotController player;
    private readonly PinballScore scoring;
    private readonly VisualElement panel;
    private readonly Button button;
    private readonly Label status;
    private readonly VisualElement useIndicators;
    private readonly VisualTreeAsset useIndicatorTemplate;

    public DashHud(VisualElement root, BonkRobotController player, PinballScore scoring = null)
    {
        this.player = player;
        this.scoring = scoring;
        panel = root.Q("dash-panel");
        button = panel.Q<Button>("dash-button");
        status = panel.Q<Label>("dash-status");
        useIndicators = root.Q("dash-uses");
        if (useIndicators != null)
            useIndicatorTemplate = Resources.Load<VisualTreeAsset>("CombatUI/DashUse");
        button.clicked += () => player.TryUseDash();
    }

    public void Refresh(bool visible)
    {
        panel.EnableInClassList("hidden", !visible);
        if (!visible) return;

        int remaining = player.DashUsesRemaining;
        float cooldown = player.DashCooldownRemaining;
        bool ready = player.CanDash;
        button.SetEnabled(ready);
        string state = remaining <= 0 ? (scoring != null && scoring.DashProgress > 0
                ? $"Charging {Mathf.RoundToInt(scoring.DashRecharge01 * 100f)}%" : "Empty")
            : player.DizzinessPhase == PlayerDizzinessPhase.Stunned ? "Stunned"
            : !player.IsBallForm ? "Ball only"
            : cooldown > 0f ? $"{cooldown:0.0}s"
            : ready ? "Ready" : "No target";
        status.text = useIndicators == null ? $"{remaining}/{player.DashUsesPerRun}  ·  {state}" : state;
        if (useIndicators != null) RefreshUseIndicators(remaining);
    }

    private void RefreshUseIndicators(int remaining)
    {
        int total = player.DashUsesPerRun;
        while (useIndicators.childCount > total) useIndicators.RemoveAt(useIndicators.childCount - 1);
        while (useIndicators.childCount < total)
        {
            var indicator = useIndicatorTemplate.Instantiate();
            indicator.pickingMode = PickingMode.Ignore;
            useIndicators.Add(indicator);
        }

        float recharge = scoring != null ? scoring.DashRecharge01 : 0f;
        for (int i = 0; i < total; i++)
        {
            float fill = i < remaining ? 1f : i == remaining ? recharge : 0f;
            useIndicators[i].Q<VisualElement>(className: "orb-progress").style.height = Length.Percent(fill * 100f);
        }
    }
}
