using UnityEngine;
using UnityEngine.UI;

public class ControlsOverlay : MonoBehaviour
{
    [TextArea(3, 5)]
    [SerializeField] private string instructions =
        "WASD - Move / Accelerate   SPACE - Switch form\nRed pillars boost ball speed. Fast hits defeat enemies. Human form takes damage.";

    private Text statusLabel;
    private BonkRobotController robot;

    private void Awake()
    {
        GameObject canvasObject = new GameObject("Controls Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Controls Background",
            typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.offsetMin = new Vector2(16f, 16f);
        panel.offsetMax = new Vector2(-16f, 120f);
        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.09f, 0.8f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Controls Text",
            typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        Text label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = instructions;
        label.fontSize = 22;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = 22;
        label.raycastTarget = false;

        GameObject statusObject = new GameObject("Phase 1 Status",
            typeof(RectTransform), typeof(Text));
        statusObject.transform.SetParent(canvasObject.transform, false);
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(18f, -18f);
        statusRect.sizeDelta = new Vector2(440f, 100f);

        statusLabel = statusObject.GetComponent<Text>();
        statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusLabel.fontSize = 24;
        statusLabel.color = Color.white;
        statusLabel.alignment = TextAnchor.UpperLeft;
        statusLabel.raycastTarget = false;

        robot = FindFirstObjectByType<BonkRobotController>();
    }

    private void Update()
    {
        if (robot == null)
        {
            robot = FindFirstObjectByType<BonkRobotController>();
        }

        if (robot == null || statusLabel == null)
        {
            return;
        }

        float speed = robot.BallController != null
            ? robot.BallController.Speed
            : 0f;
        string tier = speed >= robot.Tuning.heavyEnemyKillSpeed
            ? "HEAVY KILL"
            : speed >= robot.Tuning.smallEnemyKillSpeed
                ? "SMALL KILL"
                : "TOO SLOW";
        statusLabel.text =
            $"HEALTH  {robot.CurrentHealth}/{robot.MaxHealth}\n" +
            $"SPEED   {speed:0.0}   {tier}";
    }
}
