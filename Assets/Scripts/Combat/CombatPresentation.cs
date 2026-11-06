using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum CombatPage { MainMenu, Tutorial, WavePreview, Combat, Results }

[RequireComponent(typeof(EnemyWaveController))]
public sealed class CombatPresentation : MonoBehaviour
{
    [SerializeField] private EnemyPresentationProfile[] enemyProfiles = EnemyPresentationProfile.Defaults();
    private EnemyWaveController run;
    private Font font;
    private RectTransform root, modal, hud, map;
    private Text objective, healthLabel, formLabel, speedLabel, dizzyLabel, chargeLabel, scoreLabel, comboLabel, playerHint;
    private Button dashButton;
    private Text dashButtonLabel, dashCountLabel;
    private Image healthFill, speedFill, dizzyFill, damageTint;
    private RectTransform comboBanner;
    private readonly List<Image> damageEdges = new List<Image>();
    private readonly List<GameObject> highlights = new List<GameObject>();
    private readonly List<Text> worldLabels = new List<Text>();
    private sealed class MapPin { public RectTransform root; public Text label; public Image fill; }
    private readonly List<MapPin> mapMarkers = new List<MapPin>();
    private readonly List<Text> legendLabels = new List<Text>();
    private readonly List<EnemyBumper> roster = new List<EnemyBumper>();
    private readonly List<int> rosterCounts = new List<int>();
    private Canvas canvas;
    private GameObject ownedEventSystem;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private Transform previewModel;
    private readonly Dictionary<Transform, Transform> previewPaths = new Dictionary<Transform, Transform>();
    private float savedTimeScale = 1f;
    private bool ownsPause, tourFromHelp, tourFromMenu;
    private CombatPage returnPage;
    private EnemyWaveState lastState;

    public CombatPage Page { get; private set; }
    public int TutorialIndex { get; private set; }
    public int PreviewIndex { get; private set; }
    public int PreviewCount => roster.Count;
    public bool IsTourOpen => Page == CombatPage.Tutorial;
    private static readonly Color Ink = new Color(.035f, .055f, .09f, .98f);
    private static readonly Color Gold = new Color(1f, .84f, .2f);
    private static readonly Color Cyan = new Color(.25f, .9f, 1f);
    private static readonly string[] TourTitles = {
        "One player. Two forms.", "Build speed. Bonk in ball form.", "Robot form: control and recovery.",
        "Read the arena at a glance.", "Chain bonks. Earn stronger hits."
    };
    private static readonly string[] TourCopy = {
        "Clear every enemy before the timer ends. Move with WASD or arrow keys. Press Space to switch between robot and ball.",
        "Ball form is for speed and bonking. Press Q for a limited dash toward the nearest enemy. Bounce off walls for a speed boost. When READY TO BONK lights up, your cyan halo also catches minion near misses.",
        "Robot form gives better direction control and recovers dizziness. Use it to line up your next run. Switch before the dizziness warning expires to avoid a brief stun.",
        "Match enemy icons to the minimap. Leave blast warning rings. Pick up a green + to restore health; minion contact hurts robot form.",
        "Chain kills or bounce off a wall before a kill for bonus points. Every {0} points earns an Impact Charge: your next fast hit automatically deals double damage."
    };

    private void Start()
    {
        run = GetComponent<EnemyWaveController>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
        lastState = run.State;
        ShowMainMenu();
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    private RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var rect = Rect(name, parent, x, y, w, h);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }
    private Text Label(string name, Transform parent, string value, float x, float y, float w, float h, int size = 20, Color? color = null)
    {
        var rect = Rect(name, parent, x, y, w, h);
        var t = rect.gameObject.AddComponent<Text>(); t.font = font; t.text = value; t.fontSize = size;
        t.color = color ?? Color.white; t.raycastTarget = false; t.supportRichText = false;
        t.alignment = TextAnchor.MiddleLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }
    private Button Button(string text, Transform parent, float x, float y, float w, System.Action action)
    {
        var rect = Box(text, parent, x, y, w, 44, new Color(.12f, .23f, .32f));
        var image = rect.GetComponent<Image>(); image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(.5f, .9f, 1f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
        var label = Label("Label", rect, text, 6, 0, w - 12, 44, 20); label.alignment = TextAnchor.MiddleCenter;
        button.onClick.AddListener(() => action());
        return button;
    }
    private Image Bar(Transform parent, string name, float x, float y, float width, Color color)
    {
        var track = Box(name, parent, x, y, width, 12, new Color(.15f, .2f, .26f));
        var fill = Box("Fill", track, 0, 0, width, 12, color).GetComponent<Image>();
        // Highlight belongs to the track, so it does not shrink with the meter.
        var outline = track.gameObject.AddComponent<Outline>(); outline.effectColor = Color.clear;
        return fill;
    }
    private void BuildCanvas()
    {
        var go = new GameObject("Combat UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root = Box("Safe layout", go.transform, 0, 0, 1280, 720, Color.clear);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            ownedEventSystem = new GameObject("Combat UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownedEventSystem.transform.SetParent(transform, false);
        }
        hud = Box("HUD", root, 0, 0, 1280, 720, Color.clear);
        var objectivePanel = Box("Objective", hud, 350, 14, 580, 72, Ink);
        objective = Label("Objective text", objectivePanel, "", 12, 4, 556, 64, 24); objective.alignment = TextAnchor.MiddleCenter;
        var bottom = Box("Player status", hud, 18, 580, 972, 124, Ink);
        healthLabel = Label("Health", bottom, "", 16, 8, 192, 28, 19); healthFill = Bar(bottom, "Health meter", 16, 44, 180, new Color(.3f, 1f, .55f));
        formLabel = Label("Form", bottom, "", 16, 64, 190, 46, 18);
        speedLabel = Label("Speed", bottom, "", 218, 8, 244, 28, 18); speedFill = Bar(bottom, "Speed meter", 218, 44, 228, Cyan);
        Label("Speed hint", bottom, "Ball + speed = attack", 218, 65, 230, 40, 17);
        dizzyLabel = Label("Dizziness", bottom, "", 470, 8, 228, 28, 18); dizzyFill = Bar(bottom, "Dizziness meter", 470, 44, 214, new Color(1f, .6f, .2f));
        Label("Recovery hint", bottom, "Robot = recover", 470, 65, 214, 40, 17);
        chargeLabel = Label("Impact charge", bottom, "", 716, 8, 240, 64, 18, Gold);
        scoreLabel = Label("Score", bottom, "", 716, 76, 240, 30, 17);
        var dashPanel = Box("Ball dash", hud, 18, 500, 298, 70, Ink);
        dashButton = Button("Q  DASH", dashPanel, 10, 13, 178, () => { run.Player.TryUseDash(); });
        dashButtonLabel = dashButton.GetComponentInChildren<Text>();
        dashCountLabel = Label("Dash uses", dashPanel, "", 196, 13, 94, 44, 19, Cyan);
        dashCountLabel.alignment = TextAnchor.MiddleCenter;
        var dashColors = dashButton.colors;
        dashColors.disabledColor = new Color(.3f, .33f, .37f, 1f);
        dashButton.colors = dashColors;
        comboBanner = Box("Combo banner", hud, 350, 96, 580, 82, Gold);
        Box("Backing", comboBanner, 3, 3, 574, 76, Ink);
        comboLabel = Label("Combo text", comboBanner, "", 12, 3, 556, 76, 26, Gold); comboLabel.alignment = TextAnchor.MiddleCenter;
        comboLabel.fontStyle = FontStyle.Bold; var shadow = comboLabel.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(2, -2);
        playerHint = Label("Player warning", hud, "", 420, 390, 440, 58, 23, Gold); playerHint.alignment = TextAnchor.MiddleCenter;
        var mapPanel = Box("Minimap", hud, 1010, 366, 252, 338, Ink);
        Label("Map title", mapPanel, "ARENA                       N ↑", 12, 4, 228, 30, 17);
        map = Box("Map floor", mapPanel, 16, 38, 220, 220, new Color(.13f, .19f, .24f));
        for (int i = 0; i < 6; i++)
            legendLabels.Add(Label("Map legend", mapPanel, "", 10 + (i % 2) * 118, 264 + (i / 2) * 22, 118, 22, 14));
        BuildMapGeometry();
        Button("Help", hud, 1128, 18, 130, OpenHelp);
        AddHighlight(objectivePanel); AddHighlight(speedFill.transform.parent as RectTransform);
        AddHighlight(dizzyFill.transform.parent as RectTransform); AddHighlight(mapPanel); AddHighlight(chargeLabel.rectTransform);
        damageTint = Box("Damage tint", root, 0, 0, 1280, 720, Color.clear).GetComponent<Image>();
        damageEdges.Add(Box("Top damage edge", root, 0, 0, 1280, 20, Color.clear).GetComponent<Image>());
        damageEdges.Add(Box("Bottom damage edge", root, 0, 700, 1280, 20, Color.clear).GetComponent<Image>());
        damageEdges.Add(Box("Left damage edge", root, 0, 20, 20, 680, Color.clear).GetComponent<Image>());
        damageEdges.Add(Box("Right damage edge", root, 1260, 20, 20, 680, Color.clear).GetComponent<Image>());
        modal = Box("Modal", root, 0, 0, 1280, 720, Color.clear);
    }
    private void AddHighlight(RectTransform target)
    {
        var rect = Box("Tutorial highlight", target, -6, -6, target.rect.width + 12, target.rect.height + 12, Color.clear);
        var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = Gold; outline.effectDistance = new Vector2(4, 4);
        // Draw four actual rim images; an Outline alone cannot outline a transparent graphic.
        Box("Top", rect, 0, 0, rect.rect.width, 3, Gold); Box("Bottom", rect, 0, rect.rect.height - 3, rect.rect.width, 3, Gold);
        Box("Left", rect, 0, 0, 3, rect.rect.height, Gold); Box("Right", rect, rect.rect.width - 3, 0, 3, rect.rect.height, Gold);
        var overlay = rect.gameObject.AddComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 31;
        highlights.Add(rect.gameObject); rect.gameObject.SetActive(false);
    }
    private RectTransform Card(string heading, string eyebrow)
    {
        ClearModal();
        var dim = Box("Dim background", modal, 0, 0, 1280, 720, new Color(0, .015f, .035f, .63f));
        dim.GetComponent<Image>().raycastTarget = true;
        var card = Box("Card", modal, 160, 180, 960, 360, Ink);
        Box("Accent", card, 0, 0, 5, 360, Cyan);
        Label("Eyebrow", card, eyebrow.ToUpperInvariant(), 30, 16, 900, 28, 16, Cyan);
        Label("Heading", card, heading, 30, 50, 900, 64, 30).fontStyle = FontStyle.Bold;
        return card;
    }
    private void ClearModal()
    {
        foreach (Transform child in modal) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (GameObject highlight in highlights) highlight.SetActive(false);
        if (previewModel != null) { previewModel.gameObject.SetActive(false); Destroy(previewModel.gameObject); previewModel = null; }
        if (previewCamera != null) previewCamera.enabled = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void Pause()
    {
        if (!ownsPause) { savedTimeScale = Time.timeScale; ownsPause = true; }
        Time.timeScale = 0f; run.SetPresentationPaused(true);
    }
    private void Resume()
    {
        if (ownsPause) { Time.timeScale = savedTimeScale; ownsPause = false; }
        run.SetPresentationPaused(false);
    }
    public void ShowMainMenu()
    {
        Resume(); run.RestartRun(); Pause(); Page = CombatPage.MainMenu; hud.gameObject.SetActive(false);
        var card = Card("BONK SQUAD", "Single-player arena pinball");
        Label("Promise", card, "One player. Two forms.\nClear every enemy before time runs out.", 30, 122, 900, 100, 28);
        Button("Play", card, 30, 276, 260, PlayRun);
        Button("How to Play", card, 320, 276, 280, () => BeginTour(false, true));
        Button("Quit", card, 630, 276, 260, Quit);
        lastState = run.State;
    }
    public void PlayRun()
    {
        Resume(); run.RestartRun(); BeginTour(false, false);
    }
    private void BeginTour(bool fromHelp, bool fromMenu)
    {
        returnPage = Page; tourFromHelp = fromHelp; tourFromMenu = fromMenu; TutorialIndex = 0;
        Pause(); Page = CombatPage.Tutorial; hud.gameObject.SetActive(true); DrawTour();
    }
    public void OpenHelp()
    {
        if (Page == CombatPage.Tutorial || Page == CombatPage.MainMenu) return;
        BeginTour(true, false);
    }
    public void NextTutorial()
    {
        if (!IsTourOpen) return;
        if (TutorialIndex < TourTitles.Length - 1) { TutorialIndex++; DrawTour(); }
        else CloseTour();
    }
    public void PreviousTutorial() { if (IsTourOpen && TutorialIndex > 0) { TutorialIndex--; DrawTour(); } }
    public void SkipTutorial() { if (IsTourOpen) CloseTour(); }
    private void DrawTour()
    {
        var card = Card(TourTitles[TutorialIndex], $"Quick guide   {TutorialIndex + 1} / {TourTitles.Length}");
        card.anchoredPosition = new Vector2(70, -180); card.sizeDelta = new Vector2(900, 360);
        Label("Instruction", card, string.Format(TourCopy[TutorialIndex], run.Scoring.ChargeThreshold), 30, 122, 840, 110, 25);
        if (TutorialIndex > 0) Button("Back", card, 30, 280, 180, PreviousTutorial);
        Button(tourFromHelp ? "Close Help" : "Skip Tutorial", card, 240, 280, 270, SkipTutorial);
        Button(TutorialIndex == 4 ? (tourFromHelp || tourFromMenu ? "Done" : "Meet the enemies") : "Next", card, 570, 280, 300, NextTutorial);
        highlights[TutorialIndex].SetActive(true);
    }
    private void CloseTour()
    {
        if (tourFromMenu) { ShowMainMenu(); return; }
        if (tourFromHelp)
        {
            Resume(); Page = returnPage;
            if (Page == CombatPage.Combat) ClearModal();
            else if (Page == CombatPage.WavePreview) ShowWavePreview();
            else ShowResults();
            return;
        }
        ShowWavePreview();
    }
    public void ShowWavePreview()
    {
        Pause(); Page = CombatPage.WavePreview; hud.gameObject.SetActive(true);
        roster.Clear(); rosterCounts.Clear();
        foreach (EnemyBumper prefab in run.CurrentWave.SpawnList())
        {
            int index = roster.IndexOf(prefab);
            if (index < 0) { roster.Add(prefab); rosterCounts.Add(1); } else rosterCounts[index]++;
        }
        if (run.BasicMinionsPerWave > 0) { roster.Add(run.MinionPrefab); rosterCounts.Add(run.BasicMinionsPerWave); }
        PreviewIndex = 0; DrawPreview(); lastState = run.State;
    }
    public void BrowseEnemy(int direction)
    {
        if (Page != CombatPage.WavePreview || roster.Count == 0) return;
        PreviewIndex = (PreviewIndex + direction + roster.Count) % roster.Count; DrawPreview();
    }
    private EnemyPresentationProfile Profile(EnemyBumper enemy)
    {
        var kind = EnemyPresentationProfile.Kind(enemy);
        return enemyProfiles.FirstOrDefault(p => p != null && p.kind == kind) ?? EnemyPresentationProfile.Defaults().First(p => p.kind == kind);
    }
    private void DrawPreview()
    {
        EnemyBumper enemy = roster[PreviewIndex]; var profile = Profile(enemy);
        var card = Card($"{profile.symbol}  {profile.displayName}  ×{rosterCounts[PreviewIndex]}", $"Wave {run.WaveIndex + 1} / {run.WaveCount}   •   {run.CurrentWave.title}   •   {run.CurrentWave.seconds:0}s to clear");
        Label("Enemy ability", card, profile.ability, 320, 122, 610, 64, 21, profile.color);
        int hits = enemy.MaximumHitPoints;
        Label("Kill instruction", card, $"{hits} fast ball hit{(hits == 1 ? "" : "s")} • speed ≥ {run.Player.SpeedSettings.KillThreshold(enemy.IsSmallEnemy):0.#}\n{profile.killTip}", 320, 192, 610, 82, 20);
        Button("←", card, 30, 286, 70, () => BrowseEnemy(-1));
        Label("Card count", card, $"{PreviewIndex + 1} / {roster.Count}", 110, 286, 110, 44, 19).alignment = TextAnchor.MiddleCenter;
        Button("→", card, 230, 286, 70, () => BrowseEnemy(1));
        Button("Help", card, 340, 286, 160, OpenHelp);
        Button("Start Wave", card, 610, 286, 310, StartPresentedWave);
        BuildPreviewModel(enemy, card);
    }
    public void StartPresentedWave()
    {
        if (Page != CombatPage.WavePreview) return;
        ClearModal(); Resume(); run.StartWave(); Page = CombatPage.Combat; lastState = EnemyWaveState.Active;
    }
    private void ShowResults()
    {
        Pause(); Page = CombatPage.Results;
        bool failed = run.State == EnemyWaveState.Failed;
        string heading = run.State == EnemyWaveState.Victory ? "Arena cleared!" : failed ?
            run.Result.reason == WaveFailureReason.Timeout ? $"Time ran out — {run.Result.enemiesRemaining} enemies remained" :
            run.Result.reason == WaveFailureReason.HealthDepleted ? "Health reached zero" : "Arena could not start" : "Wave cleared!";
        var card = Card(heading, $"Wave {run.WaveIndex + 1} result");
        string detail = failed ? run.Result.reason == WaveFailureReason.Timeout ? "Find remaining targets on the minimap. Keep moving between fast bonks." :
            run.Result.reason == WaveFailureReason.HealthDepleted ? "Leave blast rings. Recover in robot form away from minions." : run.ResultMessage :
            run.State == EnemyWaveState.Victory ? "You cleared every wave. Ready for another run?" : "All enemies defeated. Your remaining health and charges carry forward.";
        Label("Result detail", card, detail + $"\n{run.Result.elapsedSeconds:0}s elapsed  •  {run.Result.health:0} health  •  Score {run.Scoring.Points}" +
            (failed ? "\nRetry restores full health, resets this wave and its score checkpoint, and reshuffles enemies." : ""), 30, 120, 900, 136, 22);
        Button("Main Menu", card, 30, 282, 260, ShowMainMenu);
        Button(failed ? "Retry Wave" : run.State == EnemyWaveState.Victory ? "Restart Run" : "Next Wave", card, 540, 282, 380, () => {
            if (failed) { ClearModal(); Resume(); run.RetryWave(); Page = CombatPage.Combat; lastState = EnemyWaveState.Active; }
            else if (run.State == EnemyWaveState.Victory) PlayRun();
            else { Resume(); run.NextWave(); ShowWavePreview(); }
        });
    }
    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void BuildPreviewModel(EnemyBumper source, RectTransform card)
    {
        var frame = Box("Enemy model", card, 30, 122, 266, 150, new Color(.08f, .13f, .19f));
        var image = Rect("Preview image", frame, 0, 0, 266, 150).gameObject.AddComponent<RawImage>(); image.raycastTarget = false;
        if (previewCamera == null)
        {
            previewTexture = new RenderTexture(512, 320, 16) { name = "Enemy preview texture" };
            var cameraObject = new GameObject("Isolated preview camera", typeof(Camera)); cameraObject.transform.SetParent(transform, false);
            previewCamera = cameraObject.GetComponent<Camera>(); previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.08f, .13f, .19f);
            previewCamera.cullingMask = 1 << 31; previewCamera.targetTexture = previewTexture;
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 20;
            var lightObject = new GameObject("Preview light", typeof(Light)); lightObject.transform.SetParent(cameraObject.transform, false);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.cullingMask = 1 << 31; light.intensity = 1.5f;
        }
        image.texture = previewTexture; previewCamera.enabled = true;
        previewPaths.Clear();
        previewModel = new GameObject("Preview meshes only").transform; previewModel.SetParent(transform, false);
        previewModel.position = new Vector3(10000, 10000, 10000);
        // Copy only mesh data. No Instantiate(prefab), Awake, rigidbodies, colliders, or attacks.
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>(); if (renderer == null || filter.sharedMesh == null) continue;
            Transform parent = ClonePreviewPath(filter.transform, source.transform, previewModel);
            parent.gameObject.layer = 31;
            parent.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            parent.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        Renderer[] renderers = previewModel.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(previewModel.position, Vector3.one);
        if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds); }
        Vector3 center = bounds.center;
        previewCamera.transform.position = center + new Vector3(2, 1.4f, -3).normalized * 8f;
        previewCamera.transform.LookAt(center);
        previewCamera.orthographicSize = Mathf.Max(.5f, bounds.size.magnitude * .65f);
    }
    private Transform ClonePreviewPath(Transform source, Transform sourceRoot, Transform destinationRoot)
    {
        if (source == sourceRoot) return destinationRoot;
        Transform parent = ClonePreviewPath(source.parent, sourceRoot, destinationRoot);
        if (previewPaths.TryGetValue(source, out Transform existing)) return existing;
        var clone = new GameObject(source.name).transform; clone.SetParent(parent, false);
        previewPaths[source] = clone;
        clone.localPosition = source.localPosition; clone.localRotation = source.localRotation; clone.localScale = source.localScale;
        return clone;
    }
    private void BuildMapGeometry()
    {
        if (run.Placement.Floor == null) return;
        Bounds floor = run.Placement.Floor.bounds;
        foreach (Collider solid in run.Placement.Floor.transform.parent.GetComponentsInChildren<Collider>())
        {
            if (solid == run.Placement.Floor || solid.isTrigger || !solid.enabled) continue;
            Vector2 a = CombatHUD.WorldToMinimap(new Vector3(solid.bounds.min.x, 0, solid.bounds.max.z), floor, new Rect(0, 0, 220, 220));
            Vector2 b = CombatHUD.WorldToMinimap(new Vector3(solid.bounds.max.x, 0, solid.bounds.min.z), floor, new Rect(0, 0, 220, 220));
            Box("Obstacle", map, Mathf.Clamp(a.x, 0, 220), Mathf.Clamp(a.y, 0, 220), Mathf.Max(1, Mathf.Min(220, b.x) - Mathf.Max(0, a.x)),
                Mathf.Max(1, Mathf.Min(220, b.y) - Mathf.Max(0, a.y)), new Color(.4f, .47f, .53f));
        }
    }
    private Text Pooled(List<Text> pool, int index, Transform parent, string name, int size)
    {
        while (pool.Count <= index)
        {
            var label = Label(name, parent, "", 0, 0, 180, 30, size);
            label.alignment = TextAnchor.MiddleCenter;
            var outline = label.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, -1);
            pool.Add(label);
        }
        pool[index].gameObject.SetActive(true); return pool[index];
    }
    private void MapMarker(int index, Vector3 position, string symbol, Color color)
    {
        while (mapMarkers.Count <= index)
        {
            var rect = Box("Map pin", map, 0, 0, 18, 18, color);
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(2, -2);
            var label = Label("Map symbol", rect, "", -3, -5, 24, 28, 18, Color.black);
            label.alignment = TextAnchor.MiddleCenter; label.fontStyle = FontStyle.Bold;
            mapMarkers.Add(new MapPin { root = rect, label = label, fill = rect.GetComponent<Image>() });
        }
        MapPin pin = mapMarkers[index]; pin.root.gameObject.SetActive(true);
        bool player = symbol == "P", minion = symbol == "•";
        float size = player ? 24 : minion ? 12 : 18;
        pin.root.sizeDelta = new Vector2(size, size);
        pin.fill.color = player ? Cyan : color;
        pin.label.text = minion ? "" : symbol;
        pin.label.rectTransform.anchoredPosition = new Vector2((size - 24) * .5f, (28 - size) * .5f);
        Vector2 point = CombatHUD.WorldToMinimap(position, run.Placement.Floor.bounds, new Rect(10, 10, 200, 200));
        pin.root.anchoredPosition = new Vector2(point.x - size * .5f, -point.y + size * .5f);
        pin.root.SetAsLastSibling(); // Player is drawn last, above enemies and arena geometry.
    }
    private void UpdateLegend()
    {
        var entries = new List<EnemyPresentationProfile>();
        foreach (EnemyBumper prefab in run.CurrentWave.SpawnList())
        {
            var profile = Profile(prefab);
            if (!entries.Any(p => p.kind == profile.kind)) entries.Add(profile);
        }
        bool minions = run.BasicMinionsPerWave > 0 || entries.Any(p => p.kind == PresentedEnemyKind.Spawner);
        foreach (EnemyBumper enemy in run.LivingEnemyObjects)
        {
            var profile = Profile(enemy);
            if (!entries.Any(p => p.kind == profile.kind)) entries.Add(profile);
        }
        if (minions && !entries.Any(p => p.kind == PresentedEnemyKind.Minion)) entries.Insert(0, Profile(run.MinionPrefab));
        legendLabels[0].text = "P  You"; legendLabels[0].color = Cyan;
        int index = 1;
        foreach (var profile in entries)
        {
            legendLabels[index].text = profile.symbol + "  " + profile.displayName;
            legendLabels[index++].color = profile.color;
        }
        legendLabels[index].text = "+  Health"; legendLabels[index++].color = Color.green;
        while (index < legendLabels.Count) legendLabels[index++].text = "";
    }
    private void WorldLabel(int index, Vector3 position, string text, Color color, float height = 28f)
    {
        Text label = Pooled(worldLabels, index, hud, "World cue", 16);
        Camera camera = Camera.main;
        if (camera == null) { label.gameObject.SetActive(false); return; }
        Vector3 screen = camera.WorldToScreenPoint(position);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) { label.gameObject.SetActive(false); return; }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        label.rectTransform.pivot = new Vector2(.5f, 0);
        label.rectTransform.anchoredPosition = local;
        label.rectTransform.sizeDelta = new Vector2(200, height); label.text = text; label.color = color;
    }
    private static void HideUnused(List<Text> pool, int used)
    {
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }
    private void UpdateWorldCues()
    {
        int world = 0, markers = 0;
        if (Page == CombatPage.Combat)
        {
            foreach (EnemyBumper enemy in run.LivingEnemyObjects)
            {
                if (enemy == null || enemy.IsDefeated) continue;
                EnemyPresentationProfile profile = Profile(enemy);
                MapMarker(markers++, enemy.transform.position, profile.symbol, profile.color);
                WorldLabel(world++, enemy.transform.position + Vector3.up * (enemy.IsBasicMinion ? 1.4f : 2f) * run.Placement.Diameter,
                    profile.symbol + " " + profile.displayName + (enemy.UsesStraightPatrol ? " " + enemy.PatrolArrow : ""), profile.color);
            }
            foreach (HealthPickup pickup in FindObjectsByType<HealthPickup>(FindObjectsSortMode.None))
            {
                MapMarker(markers++, pickup.transform.position, "+", Color.green);
                WorldLabel(world++, pickup.transform.position + Vector3.up * run.Placement.Diameter,
                    Time.time < pickup.FullHealthFeedbackUntil ? "Health full" : $"+{pickup.HealAmount:0} health", Color.green);
            }
            foreach (CombatHazard hazard in FindObjectsByType<CombatHazard>(FindObjectsSortMode.None))
                WorldLabel(world++, hazard.transform.position + Vector3.up * run.Placement.Diameter, hazard.WarningLabel, Gold);
            foreach (Transform marker in run.WarningMarkers)
                if (marker != null && marker.gameObject.activeInHierarchy && marker.name == "Minion incoming")
                    WorldLabel(world++, marker.position + Vector3.up * run.Placement.Diameter, "S  MINION INCOMING", Color.green);
        }
        if (run.Placement.Floor != null) MapMarker(markers++, run.Player.ActivePosition, "P", Color.white);
        HideUnused(worldLabels, world);
        for (int i = markers; i < mapMarkers.Count; i++) mapMarkers[i].root.gameObject.SetActive(false);
        UpdateLegend();
    }
    private void Update()
    {
        if (run == null || root == null) return;
        // Direct gameplay APIs remain usable by editor validation; adopt their state transitions.
        if (run.State != lastState)
        {
            lastState = run.State;
            if (run.State == EnemyWaveState.Active) { Resume(); ClearModal(); Page = CombatPage.Combat; hud.gameObject.SetActive(true); }
            else if (run.State == EnemyWaveState.Introduction && Page == CombatPage.Combat) ShowWavePreview();
            else if (run.State != EnemyWaveState.Introduction) ShowResults();
        }
        float threshold = run.Player.SpeedSettings.SmallEnemyKillSpeed;
        bool ready = run.Player.IsBallForm && run.Player.CurrentSpeed >= threshold;
        objective.text = $"CLEAR ALL ENEMIES  •  Wave {run.WaveIndex + 1}/{run.WaveCount}\n" +
            (run.State == EnemyWaveState.Introduction ? $"{run.CurrentWave.SpawnList().Count + run.BasicMinionsPerWave} starting enemies     {run.CurrentWave.seconds:0}s limit" :
                $"{run.LivingEnemies} left     {Mathf.CeilToInt(run.TimeRemaining)}s remaining");
        objective.color = run.TimeRemaining <= 30 ? Gold : Color.white;
        healthLabel.text = $"HEALTH  {run.Health.Current:0}/{run.Health.Maximum:0}";
        SetBar(healthFill, run.Health.Current / run.Health.Maximum, 180);
        formLabel.text = run.Player.IsBallForm ? "BALL   •   Space: robot" : "ROBOT   •   Space: ball";
        dashCountLabel.text = $"{run.Player.DashUsesRemaining}/{run.Player.DashUsesPerRun}";
        float dashCooldown = run.Player.DashCooldownRemaining;
        dashButton.interactable = Page == CombatPage.Combat && run.Player.CanDash;
        dashButtonLabel.text = run.Player.DashUsesRemaining == 0 ? "Q  DASH  EMPTY" :
            !run.Player.IsBallForm ? "Q  DASH  BALL ONLY" :
            dashCooldown > 0f ? $"Q  DASH  {dashCooldown:0.0}s" :
            !run.IsCombatActive ? "Q  DASH  WAIT" :
            !run.Player.CanDash ? "Q  DASH  NO TARGET" : "Q  DASH";
        speedLabel.text = ready ? "SPEED  •  READY TO BONK" : "SPEED  •  Too slow"; speedLabel.color = ready ? Cyan : Color.white;
        SetBar(speedFill, run.Player.CurrentSpeed / run.Player.SpeedSettings.MaximumSpeed, 228);
        dizzyLabel.text = $"DIZZINESS  {Mathf.RoundToInt(run.Player.DizzinessRatio * 100)}%";
        SetBar(dizzyFill, run.Player.DizzinessRatio, 214);
        chargeLabel.text = run.Scoring.ImpactCharges > 0 ? $"IMPACT ×{run.Scoring.ImpactCharges}\nNext fast hit: double damage" : $"IMPACT  {run.Scoring.Progress}/{run.Scoring.ChargeThreshold}\nEarn points to charge";
        scoreLabel.text = $"SCORE  {run.Scoring.Points}";
        var score = run.Scoring;
        bool reward = Page == CombatPage.Combat && score.FeedbackIsReward && Time.time <= score.FeedbackUntil;
        comboLabel.text = Page == CombatPage.Combat && Time.time <= score.FeedbackUntil ? score.Feedback : "";
        comboBanner.gameObject.SetActive(!string.IsNullOrEmpty(comboLabel.text));
        comboBanner.GetComponent<Image>().color = reward ? Gold : new Color(.45f, .5f, .6f);
        float age = Mathf.Max(0, Time.time - score.FeedbackStartedAt);
        float fade = Mathf.Clamp01((score.FeedbackUntil - Time.time) / .35f);
        comboLabel.color = reward ? Color.Lerp(Gold, Color.white, Mathf.Clamp01(1 - age / .22f)) * new Color(1, 1, 1, fade) : Color.white;
        comboBanner.localScale = Vector3.one * (reward ? 1 + .12f * Mathf.Sin(Mathf.Clamp01(age / .35f) * Mathf.PI) : 1);
        var phase = run.Player.DizzinessPhase;
        playerHint.text = Page != CombatPage.Combat ? "" : phase == PlayerDizzinessPhase.Warning ? "Space: robot to recover" :
            phase == PlayerDizzinessPhase.Stunned ? $"Stunned — recovering  {run.Player.PhaseTimeRemaining:0.0}s" : "";
        if (!string.IsNullOrEmpty(playerHint.text) && Camera.main != null)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(run.Player.ActivePosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 point);
            playerHint.rectTransform.anchorMin = playerHint.rectTransform.anchorMax = new Vector2(.5f, .5f);
            playerHint.rectTransform.pivot = new Vector2(.5f, 0);
            playerHint.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(point.x, -390, 390), Mathf.Clamp(point.y + 45, -170, 160));
        }
        var feedback = run.Health.GetComponent<PlayerDamageFeedback>(); float intensity = feedback != null ? feedback.Intensity : 0;
        damageTint.color = new Color(1, .08f, .05f, .08f * intensity);
        foreach (Image edge in damageEdges) edge.color = new Color(1, .08f, .05f, .55f * intensity);
        UpdateWorldCues();
        // Keep modal above pooled world labels, and tutorial highlights above the dimming layer.
        if (IsTourOpen)
            foreach (GameObject highlight in highlights) if (highlight.activeSelf) highlight.transform.localScale = Vector3.one * (1 + .015f * Mathf.Sin(Time.unscaledTime * 4));
    }
    private static void SetBar(Image fill, float value, float width) => fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp01(value) * width);
    public void DismissForValidation()
    {
        Resume(); ClearModal(); Page = CombatPage.Combat; hud.gameObject.SetActive(true); lastState = run.State;
    }
    private void OnDisable()
    {
        if (ownsPause) { Time.timeScale = savedTimeScale; ownsPause = false; }
        if (run != null) run.SetPresentationPaused(false);
    }
    private void OnDestroy()
    {
        if (previewTexture != null) { previewTexture.Release(); Destroy(previewTexture); }
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
    }
}
