using UnityEngine;
using UnityEngine.UIElements;

// Shared binding for the Dash template in the arena and practice scene.
internal sealed class DashHud
{
    private readonly BonkRobotController player;
    private readonly VisualElement panel;
    private readonly Button button;
    private readonly Label status;
    private readonly VisualElement useIndicators;
    private readonly VisualTreeAsset useIndicatorTemplate;

    public DashHud(VisualElement root, BonkRobotController player)
    {
        this.player = player;
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
        string state = remaining <= 0 ? "Empty"
            : player.DizzinessPhase == PlayerDizzinessPhase.Stunned ? "Stunned"
            : !player.IsBallForm ? "Ball only"
            : cooldown > 0f ? $"{cooldown:0.0}s"
            : ready ? "Ready" : "No target";
        status.text = useIndicators == null ? $"{remaining}/{player.DashUsesPerRun}  ·  {state}" : state;
        if (useIndicators != null) RefreshUseIndicators(remaining);
    }

    private void RefreshUseIndicators(int remaining)
    {
        int total = Mathf.Max(player.DashUsesPerRun, remaining);
        while (useIndicators.childCount > total) useIndicators.RemoveAt(useIndicators.childCount - 1);
        while (useIndicators.childCount < total)
        {
            var indicator = useIndicatorTemplate.Instantiate();
            indicator.pickingMode = PickingMode.Ignore;
            useIndicators.Add(indicator);
        }

        for (int i = 0; i < total; i++)
            useIndicators[i].EnableInClassList("dash-use-spent", i >= remaining);
    }
}
