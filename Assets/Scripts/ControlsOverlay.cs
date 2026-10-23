using UnityEngine;
using UnityEngine.UI;

public class ControlsOverlay : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private BonkRobotController player;

    [Header("Fixed HUD Appearance")]
    [SerializeField] private Vector2 screenPadding = new Vector2(20f, 20f);
    [SerializeField] private Vector2 panelSize = new Vector2(540f, 180f);
    [SerializeField] private Color panelColor = new Color(0.04f, 0.06f, 0.09f, 0.92f);
    [SerializeField] private Color trackColor = new Color(0.12f, 0.17f, 0.23f, 1f);
    [SerializeField] private Color fillColor = new Color(0.1f, 0.8f, 0.95f, 1f);
    [SerializeField] private Color tickColor = Color.white;
    [SerializeField] private Color textColor = Color.white;
    [Range(2f, 8f)] [SerializeField] private float tickWidth = 3f;
    [TextArea(2, 3)] [SerializeField] private string movementHint =
        "BALL: WASD along travel = accelerate | ROBOT: WASD = steer\nSPACE: switch form | Kills give no speed boost";

    private GameObject canvasObject;
    private RectTransform panel;
    private RectTransform track;
    private RectTransform fill;
    private RectTransform smallTick;
    private RectTransform largeTick;
    private Text speedLabel;
    private Text title;
    private Text smallLabel;
    private Text largeLabel;
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

        panel = MakeImage("Speed Panel", canvasObject.transform, panelColor);
        title = MakeText("Title", panel, "SPEED", 22, TextAnchor.MiddleLeft);
        speedLabel = MakeText("Current Speed", panel, "", 22, TextAnchor.MiddleRight);
        track = MakeImage("Speed Track", panel, trackColor);
        fill = MakeImage("Speed Fill", track, fillColor);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.zero;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        // Ticks are siblings of the fill. Filling never moves or clips them.
        smallTick = MakeImage("Small Enemy Threshold", track, tickColor);
        largeTick = MakeImage("Large Enemy Threshold", track, tickColor);
        smallLabel = MakeText("Small Enemy Label", panel, "", 16, TextAnchor.MiddleCenter);
        largeLabel = MakeText("Large Enemy Label", panel, "", 16, TextAnchor.MiddleCenter);
        hint = MakeText("Controls", panel, movementHint, 15, TextAnchor.MiddleLeft);
        Refresh();
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

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        PlayerSpeedSettings settings = player != null ? player.SpeedSettings : PlayerSpeedSettings.Default;
        float speed = player != null ? player.CurrentSpeed : 0f;
        float width = Mathf.Max(340f, panelSize.x);
        float height = Mathf.Max(180f, panelSize.y);
        Place(panel, screenPadding.x, screenPadding.y, width, height);
        float barWidth = width - 40f;
        Place(track, 20f, height - 95f, barWidth, 28f);
        Place(title.rectTransform, 20f, height - 36f, 130f, 28f);
        Place(speedLabel.rectTransform, 150f, height - 36f, width - 170f, 28f);
        Place(hint.rectTransform, 20f, 8f, barWidth, 40f);

        float smallRatio = settings.SmallEnemyKillSpeed / settings.MaximumSpeed;
        float largeRatio = settings.LargeEnemyKillSpeed / settings.MaximumSpeed;
        SetTick(smallTick, smallRatio);
        SetTick(largeTick, largeRatio);
        // Separate rows keep labels readable even when thresholds are close.
        Place(smallLabel.rectTransform, Mathf.Clamp(20f + smallRatio * barWidth - 70f, 20f, width - 160f),
            height - 65f, 140f, 21f);
        Place(largeLabel.rectTransform, Mathf.Clamp(20f + largeRatio * barWidth - 70f, 20f, width - 160f),
            height - 118f, 140f, 21f);
        fill.anchorMax = new Vector2(Mathf.Clamp01(speed / settings.MaximumSpeed), 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        speedLabel.text = $"{speed:0.0} / {settings.MaximumSpeed:0.0}";
        smallLabel.text = $"SMALL >= {settings.SmallEnemyKillSpeed:0.##}";
        largeLabel.text = $"LARGE >= {settings.LargeEnemyKillSpeed:0.##}";
        hint.text = movementHint;

        // All colors are fixed Inspector values; no speed/stun tint or HUD shake.
        panel.GetComponent<Image>().color = panelColor;
        track.GetComponent<Image>().color = trackColor;
        fill.GetComponent<Image>().color = fillColor;
        smallTick.GetComponent<Image>().color = largeTick.GetComponent<Image>().color = tickColor;
        title.color = speedLabel.color = smallLabel.color = largeLabel.color = hint.color = textColor;
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
