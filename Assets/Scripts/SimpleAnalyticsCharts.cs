using UnityEngine;
using UnityEngine.UI;
using XCharts.Runtime;

public sealed class SimpleAnalyticsCharts : MonoBehaviour
{
    private GameObject canvasRoot;
    private RectTransform content;

    private readonly RectTransform[] cards = new RectTransform[4];
    private readonly BaseChart[] charts = new BaseChart[4];
    private readonly Text[] notes = new Text[4];

    private float previousTimeScale = 1f;

    public bool IsVisible =>
        canvasRoot != null && canvasRoot.activeSelf;

    public void Show(SimpleGameAnalytics analytics)
    {
        if (canvasRoot == null)
            CreateUI();

        if (!IsVisible)
        {
            previousTimeScale = Time.timeScale;
            canvasRoot.SetActive(true);
        }

        Canvas.ForceUpdateCanvases();

        LayoutCharts();
        RefreshCharts(analytics);

        Time.timeScale = 0f;
    }

    public void Hide()
    {
        if (!IsVisible)
            return;

        canvasRoot.SetActive(false);
        Time.timeScale = previousTimeScale;
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        Hide();

        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    private void CreateUI()
    {
        canvasRoot = new GameObject(
            "RuntimeAnalyticsCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        var canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        
        var background = NewRect(
            "Background", canvasRoot.transform);

        Stretch(background, Vector2.zero, Vector2.zero);

        var backgroundImage =
            background.gameObject.AddComponent<Image>();

        backgroundImage.color =
            new Color(0.06f, 0.07f, 0.09f, 1f);

        backgroundImage.raycastTarget = false;
        
        var header = CreateText(
            canvasRoot.transform,
            "Header",
            "SESSION ANALYTICS  |  Press R to close",
            26,
            Color.white);

        var headerRect = header.rectTransform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(-48f, 56f);
        headerRect.anchoredPosition = new Vector2(0f, -10f);
        
        content = NewRect("Charts", canvasRoot.transform);

        Stretch(
            content,
            new Vector2(20f, 20f),
            new Vector2(-20f, -80f));

        string[] headings =
        {
            "1. Form Switching Frequency",
            "2. Acceleration Effectiveness (%)",
            "3. Speed Progression",
            "4. Post-Stun Death Rate (%)"
        };

        for (int i = 0; i < 4; i++)
        {
            cards[i] = NewRect("Card" + i, content);
            Center(cards[i]);

            var cardImage =
                cards[i].gameObject.AddComponent<Image>();

            cardImage.color = Color.white;
            cardImage.raycastTarget = false;
            
            var title = CreateText(
                cards[i],
                "Title",
                headings[i],
                20,
                new Color(0.12f, 0.14f, 0.18f));

            PositionText(title.rectTransform, 12f, 28f);

            notes[i] = CreateText(
                cards[i],
                "Description",
                "",
                14,
                new Color(0.35f, 0.37f, 0.4f));

            PositionText(notes[i].rectTransform, 42f, 32f);
        }

        charts[0] = CreateChart<BarChart>(0);
        charts[1] = CreateChart<BarChart>(1);
        charts[2] = CreateChart<LineChart>(2);
        charts[3] = CreateChart<BarChart>(3);

        for (int i = 0; i < charts.Length; i++)
            SetupChart(charts[i], i == 1 || i == 3);

        charts[0].AddSerie<Bar>("Switches / min");
        charts[1].AddSerie<Bar>("Percent");

        charts[2].AddSerie<Line>("Player speed");
        charts[2].AddSerie<Line>("Small enemy threshold");

        charts[3].AddSerie<Bar>("Percent");
        
        foreach (var chart in charts)
            chart.AnimationEnable(false);

        canvasRoot.SetActive(false);
    }

    private static RectTransform NewRect(
        string objectName, Transform parent)
    {
        var go = new GameObject(
            objectName, typeof(RectTransform));

        go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(
        RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimum;
        rect.offsetMax = maximum;
        rect.localScale = Vector3.one;
    }

    private static void Center(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        string text,
        int fontSize,
        Color color)
    {
        var rect = NewRect(objectName, parent);
        var label = rect.gameObject.AddComponent<Text>();

        label.font =
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;

        return label;
    }

    private static void PositionText(
        RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-32f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
    }

    private T CreateChart<T>(int index) where T : BaseChart
    {
        var rect = NewRect("Chart", cards[index]);
        var chart = rect.gameObject.AddComponent<T>();

        chart.Init();
        
        Center(rect);
        rect.anchoredPosition = new Vector2(0f, -33f);

        chart.SetSize(600f, 300f);

        return chart;
    }

    private void LayoutCharts()
    {
        const float gap = 16f;

        float contentWidth = content.rect.width;
        float contentHeight = content.rect.height;

        float width = (contentWidth - gap) * 0.5f;
        float height = (contentHeight - gap) * 0.5f;

        for (int i = 0; i < 4; i++)
        {
            int column = i % 2;
            int row = i / 2;

            float x =
                -contentWidth * 0.5f +
                width * 0.5f +
                column * (width + gap);

            float y =
                contentHeight * 0.5f -
                height * 0.5f -
                row * (height + gap);

            Center(cards[i]);

            cards[i].sizeDelta = new Vector2(width, height);
            cards[i].anchoredPosition = new Vector2(x, y);

            var chartRect =
                charts[i].GetComponent<RectTransform>();

            Center(chartRect);
            chartRect.anchoredPosition = new Vector2(0f, -33f);
            
            charts[i].SetSize(width - 12f, height - 86f);
            charts[i].RefreshAllComponent();
        }
    }

    private static void SetupChart(
        BaseChart chart, bool percentage)
    {
        chart.RemoveData();

        chart.EnsureChartComponent<Title>().show = false;
        chart.EnsureChartComponent<Tooltip>().show = false;
        chart.EnsureChartComponent<Legend>().show = false;

        var x = chart.EnsureChartComponent<XAxis>();
        x.show = true;
        x.type = Axis.AxisType.Category;
        x.boundaryGap = true;
        x.splitNumber = 5;

        var y = chart.EnsureChartComponent<YAxis>();
        y.show = true;
        y.type = Axis.AxisType.Value;

        if (percentage)
        {
            y.minMaxType = Axis.AxisMinMaxType.Custom;
            y.min = 0;
            y.max = 100;
        }

        var grid = chart.EnsureChartComponent<GridCoord>();
        grid.top = 14;
        grid.bottom = 38;
        grid.left = 48;
        grid.right = 20;
    }

    private void AddBar(
        int index, string category, float value)
    {
        charts[index].AddXAxisData(category);
        charts[index].AddData(0, value);
    }

    private void RefreshCharts(SimpleGameAnalytics analytics)
    {
        foreach (var chart in charts)
            chart.ClearData();

        if (!analytics.HasSession)
        {
            notes[0].text = "No session yet";
            notes[1].text = "No directional input: N/A";
            notes[2].text = "No session yet";
            notes[3].text = "No stun events: N/A";

            Redraw();
            return;
        }

        // Metric 1
        if (analytics.ElapsedSeconds > 0f)
        {
            AddBar(0, "Actual", analytics.SwitchesPerMinute);
        }

        notes[0].text = string.Format(
            "{0} switches / {1:F0}s = {2:F1} per minute",
            analytics.SwitchCount,
            analytics.ElapsedSeconds,
            analytics.SwitchesPerMinute);

        // Metric 2
        if (analytics.InputSeconds > 0f)
        {
            AddBar(1, "Actual", analytics.AccelerationPercent);

            notes[1].text = string.Format(
                "{0:F1}s successful / {1:F1}s input = {2:F1}%",
                analytics.AccelerationSeconds,
                analytics.InputSeconds,
                analytics.AccelerationPercent);
        }
        else
        {
            notes[1].text = "No directional input: N/A";
        }

        // Metric 3
        foreach (var point in analytics.SpeedHistory)
        {
            charts[2].AddXAxisData(
                point.time.ToString("F0") + "s");

            charts[2].AddData(0, point.speed);
            charts[2].AddData(1, analytics.KillSpeedThreshold);
        }

        string reached =
            analytics.FirstThresholdTime >= 0f
                ? string.Format(
                    "First reached at {0:F1}s",
                    analytics.FirstThresholdTime)
                : "Not reached";

        notes[2].text = string.Format(
            "Enemy threshold: {0:F1} units/s | {1}",
            analytics.KillSpeedThreshold,
            reached);

        // Metric 4
        if (analytics.StunCount > 0)
        {
            AddBar(3, "Actual", analytics.PostStunDeathPercent);

            notes[3].text = string.Format(
                "{0}/{1} stuns = {2:F1}% | Pending: {3}",
                analytics.PostStunDeaths,
                analytics.StunCount,
                analytics.PostStunDeathPercent,
                analytics.PendingStuns);
        }
        else
        {
            notes[3].text = "No stun events: N/A";
        }

        Redraw();
    }

    private void Redraw()
    {
        foreach (var chart in charts)
        {
            chart.AnimationEnable(false);
            chart.RefreshChart();
        }
    }
}