using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XCharts.Runtime;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class BallAccelerationEfficiencyChart : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private BouncingBallController player;

    [Header("Display")]
    [SerializeField] private bool initiallyVisible = false;

    private GameObject uiRoot;
    private GameObject chartObject;
    private BarChart chart;
    private Text toggleButtonText;

    private bool chartVisible;
    private float lastPercent = -1f;

    private void Start()
    {
        CreateUI();
        SetChartVisible(initiallyVisible);
    }

    private void Update()
    {
        if (chartVisible)
            RefreshChart();
    }

    private void CreateUI()
    {
        // 创建独立的屏幕 UI。
        uiRoot = new GameObject(
            "Ball Acceleration Efficiency UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();

        CreateToggleButton();
        CreateChart();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem));

        eventSystemObject.transform.SetParent(uiRoot.transform, false);

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private void CreateToggleButton()
    {
        GameObject buttonObject = new GameObject(
            "Toggle Chart Button",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(uiRoot.transform, false);

        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();

        SetTopLeftRect(
            buttonRect,
            new Vector2(220f, 48f),
            new Vector2(24f, -24f));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.22f, 0.32f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(ToggleChart);

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(Text));

        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        toggleButtonText = textObject.GetComponent<Text>();
        toggleButtonText.font =
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        toggleButtonText.fontSize = 20;
        toggleButtonText.alignment = TextAnchor.MiddleCenter;
        toggleButtonText.color = Color.white;
        toggleButtonText.raycastTarget = false;

        // 使用英文避免运行时默认字体缺少中文字形。
        toggleButtonText.text = "Show Acceleration";
    }

    private void CreateChart()
    {
        chartObject = new GameObject(
            "Acceleration Efficiency Chart",
            typeof(RectTransform),
            typeof(CanvasRenderer));

        chartObject.transform.SetParent(uiRoot.transform, false);

        RectTransform chartRect =
            chartObject.GetComponent<RectTransform>();

        SetTopLeftRect(
            chartRect,
            new Vector2(440f, 300f),
            new Vector2(24f, -88f));

        chart = chartObject.AddComponent<BarChart>();

        var title = chart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "Acceleration Success (%)";

        var legend = chart.EnsureChartComponent<Legend>();
        legend.show = false;

        var xAxis = chart.EnsureChartComponent<XAxis>();
        xAxis.type = Axis.AxisType.Category;

        var yAxis = chart.EnsureChartComponent<YAxis>();
        yAxis.type = Axis.AxisType.Value;
        yAxis.minMaxType = Axis.AxisMinMaxType.Custom;
        yAxis.min = 0f;
        yAxis.max = 100f;

        // 移除组件可能自带的示例数据和系列。
        chart.RemoveData();

        var serie = chart.AddSerie<Bar>("Success time");
        serie.animation.enable = false;

        var label = serie.EnsureComponent<LabelStyle>();
        label.show = true;
        label.formatter = "{c}%";

        chart.AddXAxisData("Ball mode");
        chart.AddData(0, 0f);
    }

    private static void SetTopLeftRect(
        RectTransform rect,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    public void ToggleChart()
    {
        SetChartVisible(!chartVisible);
    }

    public void SetChartVisible(bool visible)
    {
        chartVisible = visible;

        if (chartObject != null)
            chartObject.SetActive(visible);

        if (toggleButtonText != null)
        {
            toggleButtonText.text = visible
                ? "Hide Acceleration"
                : "Show Acceleration";
        }

        if (visible)
        {
            // 重新打开时立即显示最新值。
            lastPercent = -1f;
            RefreshChart();
        }
    }

    private void RefreshChart()
    {
        if (chart == null)
            return;
        
        float percent = player != null
            ? player.AccelerationSuccessRatio01 * 100f
            : 0f;

        percent = Mathf.Round(percent * 10f) / 10f;

        if (Mathf.Approximately(percent, lastPercent))
            return;

        chart.UpdateData(0, 0, percent);
        lastPercent = percent;
    }

    private void OnDestroy()
    {
        if (uiRoot != null)
            Destroy(uiRoot);
    }
}