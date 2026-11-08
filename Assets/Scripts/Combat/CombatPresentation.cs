using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public enum CombatPage { MainMenu, Tutorial, Combat, Results, WavePreview, Skills }

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(EnemyWaveController))]
public sealed class CombatPresentation : MonoBehaviour
{
    [SerializeField] private EnemyPresentationProfile[] enemyProfiles = EnemyPresentationProfile.Defaults();

    private EnemyWaveController run;
    private UIDocument document;
    private VisualElement root, hud, modal, mapMarkers, mapObstacles, worldCues, combo, damage;
    private Label wave, timer, comboText, playerWarning, guideProgress, guideImpact, resultWave, resultTitle, resultDetail;
    private string impactGuideFormat;
    private VisualElement[] guidePages, hearts;
    private VisualTreeAsset worldCueTemplate;
    private readonly List<VisualElement> pins = new();
    private readonly List<WorldCue> cues = new();
    private PlayerDamageFeedback damageFeedback;
    private Camera worldCamera;
    private float savedTimeScale;
    private bool ownsPause;
    private DashHud dash;
    private EnemyWaveState lastState;
    private readonly List<EnemyModelPreview> enemyPreviews = new();
    private float tokenPopupUntil = float.NegativeInfinity;
    public bool IsSkillChoiceOpen => Page == CombatPage.Skills;

    private sealed class WorldCue
    {
        public VisualElement element, health;
        public Label caption;
    }

    public CombatPage Page { get; private set; }
    public int TutorialIndex { get; private set; }
    public bool IsTourOpen => Page == CombatPage.Tutorial;

    private void Start()
    {
        run = GetComponent<EnemyWaveController>();
        damageFeedback = run.Health.GetComponent<PlayerDamageFeedback>();
        var ui = new GameObject("Combat UI");
        ui.SetActive(false);
        ui.transform.SetParent(transform, false);
        document = ui.AddComponent<UIDocument>();
        document.panelSettings = Resources.Load<PanelSettings>("CombatUI/CombatPanel");
        document.visualTreeAsset = Resources.Load<VisualTreeAsset>("CombatUI/Combat");
        worldCueTemplate = Resources.Load<VisualTreeAsset>("CombatUI/WorldCue");
        ui.SetActive(true);

        root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        hud = root.Q("hud");
        modal = root.Q("modal");
        mapMarkers = root.Q("map-markers");
        mapObstacles = root.Q("map-obstacles");
        worldCues = root.Q("world-cues");
        combo = root.Q("combo");
        damage = root.Q("damage");
        wave = root.Q<Label>("wave");
        timer = root.Q<Label>("timer");
        comboText = root.Q<Label>("combo-text");
        playerWarning = root.Q<Label>("player-warning");
        guideProgress = root.Q<Label>("guide-progress");
        guideImpact = root.Q<Label>("guide-impact");
        impactGuideFormat = guideImpact.text;
        resultWave = root.Q<Label>("result-wave");
        resultTitle = root.Q<Label>("result-title");
        resultDetail = root.Q<Label>("result-detail");
        guidePages = root.Q("guide-pages").Children().ToArray();
        hearts = BindMeter("health");

        Button("start").clicked += PlayRun;
        Button("tutorial").clicked += OpenTutorialLevel;
        Button("help-button").clicked += OpenHelp;
        Button("back").clicked += PreviousTutorial;
        Button("next").clicked += NextTutorial;
        Button("done").clicked += CloseTour;
        Button("close-help").clicked += CloseTour;
        Button("main-menu").clicked += ShowMainMenu;
        Button("retry-wave").clicked += () => BeginCombat(run.RetryWave);
        Button("next-wave").clicked += AdvanceWave;
        Button("restart-run").clicked += PlayRun;

        Button("start-wave").clicked += StartPresentedWave;
        Button("skills-button").clicked += OpenSkills;
        Button("buy-impact").clicked += () => BuySkill(false);
        Button("buy-dash").clicked += () => BuySkill(true);
        Button("close-skills").clicked += CloseSkills;
        run.Scoring.TokensEarned += OnTokensEarned;
        dash = new DashHud(root, run.Player);
        BuildMapGeometry();
        ShowMainMenu();
    }

    private Button Button(string name) => root.Q<Button>(name);

    private static void Show(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);

    private VisualElement[] BindMeter(string name)
    {
        var fills = root.Q(name).Query<VisualElement>(className: "fill").ToList();
        foreach (var fill in fills)
            fill.parent.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                fill.style.backgroundSize = new BackgroundSize(
                    Length.Pixels(evt.newRect.width), Length.Pixels(evt.newRect.height));
            });
        return fills.ToArray();
    }

    private static void SetMeter(VisualElement[] fills, float value)
    {
        for (int i = 0; i < fills.Length; i++)
        {
            float fraction = Mathf.Clamp01(value - i);
            fills[i].EnableInClassList("empty", fraction <= 0f);
            fills[i].style.height = Length.Percent(fraction * 100f);
        }
    }

    private void Pause()
    {
        if (!ownsPause)
        {
            savedTimeScale = Time.timeScale;
            ownsPause = true;
        }
        Time.timeScale = 0f;
        run.SetPresentationPaused(true);
    }

    private void Resume()
    {
        if (ownsPause)
        {
            Time.timeScale = savedTimeScale;
            ownsPause = false;
        }
        run.SetPresentationPaused(false);
    }

    private void SetPage(CombatPage page)
    {
        Page = page;
        Show(hud, page != CombatPage.MainMenu);
        Show(modal, page == CombatPage.Tutorial || page == CombatPage.Results || page == CombatPage.WavePreview || page == CombatPage.Skills);
        Show(root.Q("wave-preview"), page == CombatPage.WavePreview);
        Show(root.Q("skill-choice"), page == CombatPage.Skills);
        if (page != CombatPage.WavePreview) ClearPreviews();
        Show(root.Q("menu"), page == CombatPage.MainMenu);
        Show(root.Q("guide"), page == CombatPage.Tutorial);
        Show(root.Q("results"), page == CombatPage.Results);
        Show(Button("help-button"), page == CombatPage.Combat);
        dash.Refresh(page == CombatPage.Combat);
    }

    public void ShowMainMenu()
    {
        Resume();
        run.RestartRun();
        Pause();
        lastState = run.State;
        tokenPopupUntil = float.NegativeInfinity;
        SetPage(CombatPage.MainMenu);
    }

    public void PlayRun()
    {
        Resume();
        run.RestartRun();
        tokenPopupUntil = float.NegativeInfinity;
        ShowWavePreview();
    }

    private void OpenTutorialLevel()
    {
        Resume();
        SceneManager.LoadScene(TutorialLevel.SceneName);
    }

    public void OpenHelp()
    {
        if (Page != CombatPage.Combat) return;
        TutorialIndex = 0;
        Pause();
        SetPage(CombatPage.Tutorial);
        DrawTour();
    }
    public void NextTutorial()
    {
        if (!IsTourOpen) return;
        if (TutorialIndex == guidePages.Length - 1) CloseTour();
        else
        {
            TutorialIndex++;
            DrawTour();
        }
    }

    public void PreviousTutorial()
    {
        if (!IsTourOpen || TutorialIndex == 0) return;
        TutorialIndex--;
        DrawTour();
    }

    public void SkipTutorial()
    {
        if (IsTourOpen) CloseTour();
    }

    private void DrawTour()
    {
        for (int i = 0; i < guidePages.Length; i++) Show(guidePages[i], i == TutorialIndex);
        guideProgress.text = $"Quick guide   {TutorialIndex + 1} / {guidePages.Length}";
        guideImpact.text = string.Format(impactGuideFormat, run.Scoring.ChargeThreshold);
        bool last = TutorialIndex == guidePages.Length - 1;
        Show(Button("back"), TutorialIndex > 0);
        Show(Button("next"), !last);
        Show(Button("done"), last);
    }

    private void CloseTour()
    {
        if (!IsTourOpen) return;
        Resume();
        SetPage(CombatPage.Combat);
    }
    private void AdvanceWave()
    {
        Resume();
        run.NextWave();
        ShowWavePreview();
    }

    private void BeginCombat(System.Action start)
    {
        tokenPopupUntil = float.NegativeInfinity;
        Resume();
        start();
        SyncRunState();
    }

    private void SyncRunState()
    {
        lastState = run.State;
        switch (run.State)
        {
            case EnemyWaveState.Introduction:
                if (Page == CombatPage.Combat) ShowWavePreview();
                break;
            case EnemyWaveState.Active:
                Resume();
                SetPage(CombatPage.Combat);
                break;
            default:
                ShowResults();
                break;
        }
    }

    private void ClearPreviews()
    {
        foreach (var preview in enemyPreviews) preview.Dispose();
        enemyPreviews.Clear();
    }
    public void ShowWavePreview()
    {
        Pause(); ClearPreviews(); SetPage(CombatPage.WavePreview); lastState = run.State;
        root.Q<Label>("preview-heading").text = $"Wave {run.WaveIndex+1}: {run.CurrentWave.title}";
        root.Q<Label>("preview-summary").text = $"Clear every enemy before time runs out.";
        var cards = root.Q("preview-enemies"); cards.Clear();
        foreach (var group in run.CurrentWave.SpawnList().GroupBy(p => p))
        {
            var profile = Profile(group.Key);
            var card = new VisualElement(); card.AddToClassList("enemy-card");
            var name = new Label($"{profile.symbol} {profile.displayName}"); name.AddToClassList("enemy-name"); name.style.color = profile.color; card.Add(name);
            var preview = new EnemyModelPreview(group.Key, enemyPreviews.Count); enemyPreviews.Add(preview);
            var image = new Image { image = preview.Texture, scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList("enemy-model"); card.Add(image);
            card.Add(new Label(profile.ability));
            var tip = new Label($"How to Kill: {group.Key.MaximumHitPoints} ball hits. {profile.killTip}"); tip.AddToClassList("enemy-tip"); card.Add(tip);
            cards.Add(card);
        }
    }
    public void StartPresentedWave()
    {
        if (Page == CombatPage.WavePreview) BeginCombat(run.StartWave);
    }
    private void OnTokensEarned(int count)
    {
        tokenPopupUntil = Time.time + 3f;
        root.Q<Label>("token-popup").text = count == 1 ? "+1 green charge — Q to Dash!" : $"+{count} green charges — Q to Dash!";
    }
    // Rewards are added directly to the green Q charges. No separate purchase screen.
    public void OpenSkills() { }
    public bool BuySkill(bool dashSkill)
    {
        if (Page != CombatPage.Skills) return false;
        bool bought = dashSkill ? run.BuyDash() : run.BuyImpact();
        root.Q<Label>("purchase-feedback").text = bought ? (dashSkill ? "Dash use restored!" : "Impact ready — next fast hit deals double damage!") : "No token available, or Dash is already full.";
        RefreshSkills(); return bought;
    }
    private void RefreshSkills()
    {
        root.Q<Label>("skill-balance").text = $"{run.Scoring.SkillTokens} skill tokens • Impact ×{run.Scoring.ImpactCharges} • Dash {run.Player.DashUsesRemaining}/{run.Player.DashUsesPerRun}";
        Button("buy-impact").SetEnabled(run.Scoring.SkillTokens > 0);
        Button("buy-dash").SetEnabled(run.Scoring.SkillTokens > 0 && run.Player.DashUsesRemaining < run.Player.DashUsesPerRun);
    }
    public void CloseSkills()
    {
        if (Page != CombatPage.Skills) return;
        Resume(); SetPage(CombatPage.Combat);
    }

    private void ShowResults()
    {
        Pause();
        SetPage(CombatPage.Results);
        bool failed = run.State == EnemyWaveState.Failed;
        bool victory = run.State == EnemyWaveState.Victory;
        resultWave.text = $"Wave {run.WaveIndex + 1} result";
        resultTitle.text = run.State switch
        {
            EnemyWaveState.Victory => "Arena cleared!",
            EnemyWaveState.Cleared => "Wave cleared!",
            _ => run.Result.reason switch
            {
                WaveFailureReason.Timeout => $"Time ran out — {run.Result.enemiesRemaining} enemies remained",
                WaveFailureReason.HealthDepleted => "Health reached zero",
                _ => "Arena could not start"
            }
        };
        string summary = $"{run.Result.elapsedSeconds:0}s elapsed  •  {run.Result.health:0} health  •  Score {run.Scoring.Points}";
        resultDetail.text = failed
            ? summary + "\nRetry restores full health and resets score and Dash to the start of this wave."
            : summary;
        Show(Button("retry-wave"), failed);
        Show(Button("next-wave"), !failed && !victory);
        Show(Button("restart-run"), victory);
    }

    private EnemyPresentationProfile Profile(EnemyBumper enemy)
    {
        var kind = EnemyPresentationProfile.Kind(enemy);
        var configured = System.Array.Find(enemyProfiles, profile => profile != null && profile.kind == kind);
        var defaults = EnemyPresentationProfile.Defaults()[(int)kind];
        if (configured == null) return defaults;
        if (string.IsNullOrEmpty(configured.ability)) configured.ability = defaults.ability;
        if (string.IsNullOrEmpty(configured.killTip)) configured.killTip = defaults.killTip;
        return configured;
    }

    private VisualElement AddDecoration(VisualElement parent, string className)
    {
        var element = new VisualElement { pickingMode = PickingMode.Ignore };
        element.AddToClassList(className);
        parent.Add(element);
        return element;
    }

    private void BuildMapGeometry()
    {
        var floor = run.Placement.Floor;
        if (floor == null) return;
        var unitRect = new Rect(0, 0, 1, 1);
        foreach (Collider solid in floor.transform.parent.GetComponentsInChildren<Collider>())
        {
            if (solid == floor || solid.isTrigger || !solid.enabled) continue;
            Bounds bounds = solid.bounds;
            Vector2 min = CombatHUD.WorldToMinimap(new Vector3(bounds.min.x, 0, bounds.max.z), floor.bounds, unitRect);
            Vector2 max = CombatHUD.WorldToMinimap(new Vector3(bounds.max.x, 0, bounds.min.z), floor.bounds, unitRect);
            var obstacle = AddDecoration(mapObstacles, "map-obstacle");
            SetMapPosition(obstacle, min);
            obstacle.style.width = Length.Percent((max.x - min.x) * 100f);
            obstacle.style.height = Length.Percent((max.y - min.y) * 100f);
        }
    }

    private static void SetMapPosition(VisualElement element, Vector2 point)
    {
        element.style.left = Length.Percent(point.x * 100f);
        element.style.top = Length.Percent(point.y * 100f);
    }

    private void MapMarker(int index, Vector3 position, string variant, Color? color = null, string symbol = "")
    {
        if (index == pins.Count)
        {
            var added = AddDecoration(mapMarkers, "map-pin");
            var letter = new Label(); letter.AddToClassList("map-letter"); letter.pickingMode = PickingMode.Ignore; added.Add(letter);
            pins.Add(added);
        }
        var pin = pins[index];
        pin.EnableInClassList("map-minion", variant == "minion");
        pin.EnableInClassList("map-health", variant == "health");
        pin.EnableInClassList("map-player", variant == "player");
        pin.style.backgroundColor = color.HasValue ? new StyleColor(color.Value) : new StyleColor(StyleKeyword.Null);
        pin.Q<Label>().text = variant == "player" ? "P" : variant == "health" ? "+" : variant == "minion" ? "" : symbol;
        SetMapPosition(pin, CombatHUD.WorldToMinimap(position, run.Placement.Floor.bounds, new Rect(.06f, .06f, .88f, .88f)));
        Show(pin, true);
    }

    private bool TryWorldPosition(Vector3 worldPosition, out Vector2 position)
    {
        position = default;
        if (worldCamera == null || root.panel == null) return false;
        Vector3 screen = worldCamera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0 || !worldCamera.pixelRect.Contains(screen)) return false;
        position = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
        return true;
    }

    private void UpdateCue(int index, Vector3 position, string text, Color color, EnemyBumper enemy = null)
    {
        if (index == cues.Count)
        {
            var element = worldCueTemplate.Instantiate().Q(className: "cue");
            worldCues.Add(element);
            cues.Add(new WorldCue { element = element, health = element.Q("enemy-health"), caption = element.Q<Label>("caption") });
        }

        WorldCue cue = cues[index];
        bool visible = TryWorldPosition(position, out Vector2 point);
        Show(cue.element, visible);
        if (!visible) return;
        cue.element.style.left = point.x;
        cue.element.style.top = point.y;
        cue.caption.text = text;
        cue.caption.style.color = color;
        bool showHealth = enemy != null && enemy.ShowHealthBar;
        Show(cue.health, showHealth);
        if (!showHealth) return;

        while (cue.health.childCount < enemy.MaximumHitPoints) AddDecoration(cue.health, "health-pip");
        for (int i = 0; i < cue.health.childCount; i++)
        {
            var pip = cue.health[i];
            Show(pip, i < enemy.MaximumHitPoints);
            pip.EnableInClassList("health-pip-lost", i >= enemy.HitPoints);
            pip.EnableInClassList("health-pip-flash", i < enemy.HitPoints && enemy.IsHitFlashing);
        }
    }

    private void UpdateWorldCues()
    {
        int usedCues = 0, usedPins = 0;
        bool hasMap = run.Placement.Floor != null;
        bool liveView = Page == CombatPage.Combat || IsTourOpen || Page == CombatPage.Skills;
        if (liveView)
        {
            foreach (EnemyBumper enemy in run.LivingEnemyObjects)
            {
                if (enemy == null || enemy.IsDefeated) continue;
                var profile = Profile(enemy);
                if (hasMap) MapMarker(usedPins++, enemy.transform.position, enemy.IsBasicMinion ? "minion" : "", profile.color, profile.symbol);
                UpdateCue(usedCues++, enemy.transform.position + Vector3.up * (enemy.IsBasicMinion ? 1.4f : 2f) * run.Placement.Diameter,
                    profile.symbol + " " + profile.displayName + (enemy.UsesStraightPatrol ? " " + enemy.PatrolArrow : ""), profile.color, enemy);
            }
            foreach (HealthPickup pickup in FindObjectsByType<HealthPickup>(FindObjectsSortMode.None))
            {
                if (hasMap) MapMarker(usedPins++, pickup.transform.position, "health");
                UpdateCue(usedCues++, pickup.transform.position + Vector3.up * run.Placement.Diameter,
                    Time.time < pickup.FullHealthFeedbackUntil ? "Health full" : $"+{pickup.HealAmount:0} health", Color.green);
            }
            foreach (var runner in run.LivingEnemyObjects.Where(e => e.GetComponent<RunnerEnemy>() != null && e.HitPoints < e.MaximumHitPoints))
                UpdateCue(usedCues++, runner.transform.position + Vector3.up*3f*run.Placement.Diameter, $"FASTER! {runner.MoveSpeed/run.Placement.Diameter:0.0}×", new Color(1f,.4f,.4f));
            foreach (CombatHazard hazard in FindObjectsByType<CombatHazard>(FindObjectsSortMode.None))
                UpdateCue(usedCues++, hazard.transform.position + Vector3.up * run.Placement.Diameter, hazard.WarningLabel, Color.yellow);
            foreach (Transform marker in run.WarningMarkers)
                if (marker != null && marker.gameObject.activeInHierarchy)
                {
                    if (marker.name == "Minion incoming") UpdateCue(usedCues++, marker.position + Vector3.up * run.Placement.Diameter, "S MINION INCOMING", Color.green);
                    else if (marker.name == "NET SHOT — DODGE") UpdateCue(usedCues++, marker.position + Vector3.up * run.Placement.Diameter, "N NET SHOT — DODGE", Color.cyan);
                }
        }
        if (hasMap)
        {
            foreach (var bat in run.GetComponentsInChildren<RotatingPinballBat>()) MapMarker(usedPins++, bat.transform.position, "", Color.cyan, "B");
            MapMarker(usedPins++, run.Player.ActivePosition, "player");
        }
        for (int i = usedPins; i < pins.Count; i++) Show(pins[i], false);
        for (int i = usedCues; i < cues.Count; i++) Show(cues[i].element, false);
    }

    private void Update()
    {
        if (root == null) return;
        if (run.State != lastState) SyncRunState();
        if (Page == CombatPage.MainMenu) return;

        dash.Refresh(Page == CombatPage.Combat);
        worldCamera = Camera.main;
        wave.text = $"Wave {run.WaveIndex + 1}/{run.WaveCount}";
        timer.text = $"{Mathf.CeilToInt(run.TimeRemaining)}s remaining";
        timer.EnableInClassList("timer-low", run.TimeRemaining <= 30f);
        var scoring = run.Scoring;
        root.Q<Label>("score-status").text = $"Score {scoring.Points} · {scoring.Progress}/{scoring.ChargeThreshold} to next charge";
        Show(Button("skills-button"), false);
        Show(root.Q("impact-status"), false);
        Show(root.Q("token-popup"), Time.time < tokenPopupUntil && Page == CombatPage.Combat);
        var reason = root.Q<Label>("damage-reason"); reason.text = run.Health.LastDamageLabel;
        Show(reason, Page == CombatPage.Combat && Time.time-run.Health.LastHitTime < 1f && !string.IsNullOrEmpty(reason.text));
        var kinds = run.CurrentWave.SpawnList().Select(Profile).Concat(run.LivingEnemyObjects.Where(e => e != null).Select(Profile)).GroupBy(p => p.kind).Select(g => g.First()).ToList();
        if (run.BasicMinionsPerWave > 0 && !kinds.Any(p => p.kind == PresentedEnemyKind.Minion)) kinds.Add(Profile(run.MinionPrefab));
        root.Q<Label>("map-legend").text = "P You · + Health · B Bat\n" + string.Join(" · ", kinds.Select(p => p.symbol + " " + p.displayName));
        SetMeter(hearts, hearts.Length * run.Health.Current / run.Health.Maximum);

        bool showCombo = Page == CombatPage.Combat && Time.time <= scoring.FeedbackUntil && !string.IsNullOrEmpty(scoring.Feedback);
        Show(combo, showCombo);
        if (showCombo)
        {
            comboText.text = scoring.Feedback;
            combo.EnableInClassList("reward", scoring.FeedbackIsReward);
            combo.style.opacity = Mathf.Clamp01((scoring.FeedbackUntil - Time.time) / .35f);
        }

        playerWarning.text = run.Player.IsSnared ? $"NETTED — release in {run.Player.SnareRemaining:0.0}s" : run.Player.DizzinessPhase switch
        {
            PlayerDizzinessPhase.Warning => "Space: robot to recover",
            PlayerDizzinessPhase.Stunned => $"Stunned — recovering  {run.Player.PhaseTimeRemaining:0.0}s",
            _ => ""
        };
        bool warningVisible = TryWorldPosition(run.Player.ActivePosition, out Vector2 warningPosition)
            && Page == CombatPage.Combat && playerWarning.text.Length > 0;
        Show(playerWarning, warningVisible);
        if (warningVisible)
        {
            playerWarning.style.left = warningPosition.x;
            playerWarning.style.top = warningPosition.y;
        }
        damage.style.opacity = damageFeedback != null ? damageFeedback.Intensity : 0f;
        UpdateWorldCues();
    }

    public void DismissForValidation()
    {
        Resume();
        SetPage(CombatPage.Combat);
        lastState = run.State;
    }

    private void OnDisable()
    {
        if (run != null) Resume();
        if (root != null) root.style.display = DisplayStyle.None;
    }

    private void OnEnable()
    {
        if (root != null) root.style.display = DisplayStyle.Flex;
    }

    private void OnDestroy()
    {
        ClearPreviews();
        if (run != null) run.Scoring.TokensEarned -= OnTokensEarned;
        if (document != null) Destroy(document.gameObject);
    }
}
