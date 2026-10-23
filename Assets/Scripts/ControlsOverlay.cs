using UnityEngine;
using UnityEngine.UI;

public class ControlsOverlay : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private BonkRobotController player;

    [Header("HUD Appearance")]
    [SerializeField] private Vector2 screenPadding = new Vector2(20f, 20f);
    [SerializeField] private Vector2 barSize = new Vector2(500f, 28f);
    [SerializeField] private Vector2 controlsSize = new Vector2(560f, 60f);
    [SerializeField] private Color trackColor = new Color(0.12f, 0.17f, 0.23f, 1f);
    [SerializeField] private Color fillColor = new Color(0.1f, 0.8f, 0.95f, 1f);
    [SerializeField] private Color tickColor = Color.white;
    [SerializeField] private Color textColor = Color.white;
    [Range(2f, 8f)] [SerializeField] private float tickWidth = 3f;
    [Header("Dizziness Effects (fixed colors, animated opacity and offset)")]
    [Range(0f, 1f)] [SerializeField] private float mediumEffectThreshold = 0.2f;
    [Range(0f, 1f)] [SerializeField] private float highEffectThreshold = 0.62f;
    [Min(0f)] [SerializeField] private float maximumGhostOffset = 13f;
    [Range(0f, 1f)] [SerializeField] private float maximumGhostOpacity = 0.85f;
    [Range(0f, 1f)] [SerializeField] private float maximumRippleOpacity = 0.95f;
    [Min(1f)] [SerializeField] private float ghostStrokeWidth = 4f;
    [Min(0f)] [SerializeField] private float slowPulseFrequency = 1.1f;
    [Min(0f)] [SerializeField] private float fastPulseFrequency = 4.2f;
    [TextArea(2, 3)] [SerializeField] private string movementHint =
        "BALL: WASD / Arrows = steer; align to accelerate\nROBOT: WASD / Arrows = steer | SPACE: switch form";

    private GameObject canvasObject;
    private RectTransform ghostFrameA;
    private RectTransform ghostFrameB;
    private RectTransform[] ghostEdgesA;
    private RectTransform[] ghostEdgesB;
    private RectTransform[] rippleEdges;
    private RectTransform track;
    private RectTransform fill;
    private RectTransform smallTick;
    private RectTransform largeTick;
    private Text smallLabel;
    private Text largeLabel;
    private Text warningText;
    private Text lockText;
    private Text hint;
    private Font font;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<BonkRobotController>();
        if (player == null) player = FindFirstObjectByType<BonkRobotController>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvasObject = new GameObject("Speed HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        ghostFrameA = MakeContainer("Dizziness Ghost A", canvasObject.transform);
        ghostEdgesA = MakeBorder("Ghost A", ghostFrameA);
        ghostFrameB = MakeContainer("Dizziness Ghost B", canvasObject.transform);
        ghostEdgesB = MakeBorder("Ghost B", ghostFrameB);
        rippleEdges = MakeBorder("Dizziness Ripple", canvasObject.transform);
        track = MakeImage("Speed Track", canvasObject.transform, trackColor);
        fill = MakeImage("Speed Fill", track, fillColor);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.zero;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        // Ticks are siblings of the fill. Filling never moves or clips them.
        smallTick = MakeImage("Small Enemy Threshold", track, tickColor);
        largeTick = MakeImage("Large Enemy Threshold", track, tickColor);
        smallLabel = MakeText("Small Enemy Label", canvasObject.transform, "", 15, TextAnchor.MiddleCenter);
        largeLabel = MakeText("Large Enemy Label", canvasObject.transform, "", 15, TextAnchor.MiddleCenter);
        warningText = MakeText("Dizziness Warning", canvasObject.transform,
            "!  SWITCH TO ROBOT  !", 21, TextAnchor.UpperRight);
        lockText = MakeText("Stun Status", canvasObject.transform,
            "STUNNED", 16, TextAnchor.MiddleCenter);
        hint = MakeText("Controls", canvasObject.transform, movementHint, 15, TextAnchor.UpperRight);
        AddTextOutline(smallLabel);
        AddTextOutline(largeLabel);
        AddTextOutline(warningText);
        AddTextOutline(lockText);
        AddTextOutline(hint);
        Refresh();
    }

    private static RectTransform MakeContainer(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private RectTransform[] MakeBorder(string name, Transform parent)
    {
        return new[]
        {
            MakeImage(name + " Top", parent, fillColor),
            MakeImage(name + " Bottom", parent, fillColor),
            MakeImage(name + " Left", parent, fillColor),
            MakeImage(name + " Right", parent, fillColor)
        };
    }

    private static void PlaceBorder(RectTransform[] edges, float x, float y,
        float width, float height, float stroke)
    {
        Place(edges[0], x - stroke, y + height, width + stroke * 2f, stroke);
        Place(edges[1], x - stroke, y - stroke, width + stroke * 2f, stroke);
        Place(edges[2], x - stroke, y, stroke, height);
        Place(edges[3], x + width, y, stroke, height);
    }

    private static void SetBorderOpacity(RectTransform[] edges, Color color, float opacity)
    {
        foreach (RectTransform edge in edges)
            SetImageOpacity(edge, color, opacity);
    }

    private static void AddTextOutline(Text text)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private RectTransform MakeImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private Text MakeText(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = textColor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void PlaceTopRight(RectTransform rect, float right, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        PlayerSpeedSettings settings = player != null ? player.SpeedSettings : PlayerSpeedSettings.Default;
        float speed = player != null ? player.CurrentSpeed : 0f;
        PlayerDizzinessPhase phase = player != null ? player.DizzinessPhase : PlayerDizzinessPhase.Normal;
        float dizzinessRatio = player != null ? player.DizzinessRatio : 0f;
        float barX = screenPadding.x;
        float barY = screenPadding.y + 28f;
        float width = Mathf.Max(140f, barSize.x);
        float height = Mathf.Max(4f, barSize.y);
        Place(track, barX, barY, width, height);
        PlaceTopRight(hint.rectTransform, screenPadding.x, screenPadding.y,
            Mathf.Max(100f, controlsSize.x), Mathf.Max(40f, controlsSize.y));
        PlaceTopRight(warningText.rectTransform, screenPadding.x, screenPadding.y + 65f,
            Mathf.Max(100f, controlsSize.x), 28f);

        float smallRatio = settings.SmallEnemyKillSpeed / settings.MaximumSpeed;
        float largeRatio = settings.LargeEnemyKillSpeed / settings.MaximumSpeed;
        SetTick(smallTick, smallRatio);
        SetTick(largeTick, largeRatio);
        float fillRatio = phase == PlayerDizzinessPhase.Stunned
            ? 0f : Mathf.Clamp01(speed / settings.MaximumSpeed);
        fill.anchorMax = new Vector2(fillRatio, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        Place(smallLabel.rectTransform, Mathf.Clamp(barX + smallRatio * width - 70f,
                barX, barX + width - 140f),
            barY + height + 4f, 140f, 22f);
        Place(largeLabel.rectTransform, Mathf.Clamp(barX + largeRatio * width - 70f,
                barX, barX + width - 140f),
            barY - 25f, 140f, 22f);
        smallLabel.text = $"SMALL >= {settings.SmallEnemyKillSpeed:0.##}";
        largeLabel.text = $"LARGE >= {settings.LargeEnemyKillSpeed:0.##}";
        Place(lockText.rectTransform, barX, barY, width, height);
        lockText.gameObject.SetActive(phase == PlayerDizzinessPhase.Stunned);
        warningText.gameObject.SetActive(phase == PlayerDizzinessPhase.Warning);
        UpdateDizzinessEffects(phase, dizzinessRatio, barX, barY, width, height);
        hint.text = movementHint;

        track.GetComponent<Image>().color = trackColor;
        fill.GetComponent<Image>().color = fillColor;
        smallTick.GetComponent<Image>().color = largeTick.GetComponent<Image>().color = tickColor;
        smallLabel.color = largeLabel.color = lockText.color = hint.color = textColor;
    }

    private void UpdateDizzinessEffects(PlayerDizzinessPhase phase, float ratio,
        float barX, float barY, float width, float height)
    {
        float medium = Mathf.Clamp01(mediumEffectThreshold);
        float high = Mathf.Max(medium + 0.01f, highEffectThreshold);
        float strength = phase == PlayerDizzinessPhase.Stunned
            ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(medium, high, ratio));
        float highStrength = phase == PlayerDizzinessPhase.Stunned
            ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(high, 1f, ratio));
        float frequency = Mathf.Lerp(slowPulseFrequency, fastPulseFrequency, ratio);
        float wave = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f);
        float pulse = 0.7f + 0.3f * wave;
        float offset = maximumGhostOffset * strength * wave;
        float stroke = Mathf.Max(1f, ghostStrokeWidth);

        Place(ghostFrameA, barX + offset, barY + offset * 0.35f, width, height);
        Place(ghostFrameB, barX - offset, barY - offset * 0.35f, width, height);
        PlaceBorder(ghostEdgesA, 0f, 0f, width, height, stroke);
        PlaceBorder(ghostEdgesB, 0f, 0f, width, height, stroke);
        SetBorderOpacity(ghostEdgesA, fillColor, maximumGhostOpacity * strength * pulse);
        SetBorderOpacity(ghostEdgesB, fillColor, maximumGhostOpacity * highStrength * pulse);

        float rippleExpansion = 4f + 9f * strength * pulse;
        PlaceBorder(rippleEdges, barX - rippleExpansion, barY - rippleExpansion,
            width + rippleExpansion * 2f, height + rippleExpansion * 2f,
            2f + 2f * highStrength);
        SetBorderOpacity(rippleEdges, fillColor, maximumRippleOpacity * strength * pulse);

        float warningPulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 5f * Mathf.PI * 2f);
        Color warningColor = textColor;
        warningColor.a *= warningPulse;
        warningText.color = warningColor;
    }

    private static void SetImageOpacity(RectTransform rect, Color color, float opacity)
    {
        color.a *= Mathf.Clamp01(opacity);
        rect.GetComponent<Image>().color = color;
    }

    private void SetTick(RectTransform tick, float ratio)
    {
        tick.anchorMin = new Vector2(ratio, 0f);
        tick.anchorMax = new Vector2(ratio, 1f);
        tick.pivot = new Vector2(ratio, 0.5f); // stays inside the track even at 100%
        tick.sizeDelta = new Vector2(tickWidth, 0f);
        tick.anchoredPosition = Vector2.zero;
    }

    private void OnEnable()
    {
        if (canvasObject != null) canvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (canvasObject != null) canvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }
}
