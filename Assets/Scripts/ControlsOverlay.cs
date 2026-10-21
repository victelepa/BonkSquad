using UnityEngine;
using UnityEngine.UI;

public class ControlsOverlay : MonoBehaviour
{
    [TextArea(3, 5)]
    [SerializeField] private string instructions =
        "WASD - Move / Accelerate   SPACE - Switch form\nRed pillars boost ball speed. Fast hits defeat enemies. Human form takes damage.";

    private Text statusLabel;
    private Text playerTwoStatusLabel;
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
        statusRect.sizeDelta = new Vector2(440f, 130f);

        statusLabel = statusObject.GetComponent<Text>();
        statusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusLabel.fontSize = 24;
        statusLabel.color = Color.white;
        statusLabel.alignment = TextAnchor.UpperLeft;
        statusLabel.raycastTarget = false;

        GameObject playerTwoStatusObject = new GameObject("Player 2 Status",
            typeof(RectTransform), typeof(Text));
        playerTwoStatusObject.transform.SetParent(canvasObject.transform, false);
        RectTransform playerTwoStatusRect =
            playerTwoStatusObject.GetComponent<RectTransform>();
        playerTwoStatusRect.anchorMin = new Vector2(1f, 1f);
        playerTwoStatusRect.anchorMax = new Vector2(1f, 1f);
        playerTwoStatusRect.pivot = new Vector2(1f, 1f);
        playerTwoStatusRect.anchoredPosition = new Vector2(-18f, -18f);
        playerTwoStatusRect.sizeDelta = new Vector2(440f, 130f);

        playerTwoStatusLabel = playerTwoStatusObject.GetComponent<Text>();
        playerTwoStatusLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        playerTwoStatusLabel.fontSize = 24;
        playerTwoStatusLabel.color = new Color(0.8f, 0.5f, 1f);
        playerTwoStatusLabel.alignment = TextAnchor.UpperRight;
        playerTwoStatusLabel.raycastTarget = false;

        robot = FindAnyObjectByType<BonkRobotController>();
    }

    private void Update()
    {
        if (robot == null)
        {
            robot = FindAnyObjectByType<BonkRobotController>();
        }

        if (robot == null || statusLabel == null)
        {
            return;
        }

        BonkRobotController playerTwo = null;
        foreach (BonkRobotController player in BonkRobotController.Players)
        {
            if (player != null && player.PlayerNumber == 1)
            {
                robot = player;
            }
            else if (player != null && player.PlayerNumber == 2)
            {
                playerTwo = player;
            }
        }

        statusLabel.text = BuildPlayerStatus(robot, "P1");
        playerTwoStatusLabel.text = playerTwo != null
            ? BuildPlayerStatus(playerTwo, "P2")
            : string.Empty;
    }

    private string BuildPlayerStatus(BonkRobotController player, string label)
    {
        float speed = player.BallController != null
            ? player.BallController.Speed
            : 0f;
        string tier = speed >= player.Tuning.heavyEnemyKillSpeed
            ? "HEAVY KILL"
            : speed >= player.Tuning.smallEnemyKillSpeed
                ? "SMALL KILL"
                : "TOO SLOW";
        string targetHint = BuildTargetHint(player);
        return
            $"{label}   HEALTH  {player.CurrentHealth}/{player.MaxHealth}\n" +
            $"SPEED   {speed:0.0}   {tier}\n" +
            $"MONSTERS   {EnemyBumper.ActiveCount}" +
            (player.BallController != null && player.BallController.BounceChain > 1
                ? $"   BOUNCE x{player.BallController.BounceChain}"
                : string.Empty) +
            (player.StoredSpeedBoost > 0f
                ? $"   BOOST +{player.StoredSpeedBoost:0}"
                : string.Empty) + targetHint;
    }

    private string BuildTargetHint(BonkRobotController player)
    {
        Transform playerTransform = player.IsBallForm
            ? player.BallTransform
            : player.StandingTransform;
        if (EnemyBumper.ActiveCount > 3 || playerTransform == null)
        {
            return string.Empty;
        }

        EnemyBumper nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyBumper enemy in EnemyBumper.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDefeated)
            {
                continue;
            }

            float distance = Vector3.Distance(
                playerTransform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
        {
            return string.Empty;
        }

        Vector3 offset = nearest.transform.position - playerTransform.position;
        string horizontal = Mathf.Abs(offset.x) > 2f
            ? (offset.x > 0f ? "E" : "W")
            : string.Empty;
        string vertical = Mathf.Abs(offset.z) > 2f
            ? (offset.z > 0f ? "N" : "S")
            : string.Empty;
        string direction = horizontal + vertical;
        return $"\nTARGET   {nearestDistance:0}m {direction}";
    }
}
