using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameFlowScreen : MonoBehaviour
{
    private enum ScreenState
    {
        Title,
        Instructions,
        Playing
    }

    private ScreenState state;
    private GameObject screenRoot;
    private Text heading;
    private Text body;
    private Text prompt;
    private int selectedPlayerCount = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<GameFlowScreen>() == null)
        {
            new GameObject("Game Flow Screen").AddComponent<GameFlowScreen>();
        }
    }

    private void Awake()
    {
        Time.timeScale = 0f;
        BuildInterface();
        ShowTitle();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            if (state == ScreenState.Title)
            {
                ShowInstructions();
            }
            else if (state == ScreenState.Instructions)
            {
                StartGame();
            }
        }
        else if (state == ScreenState.Title &&
                 (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                  Keyboard.current.aKey.wasPressedThisFrame))
        {
            selectedPlayerCount = 1;
            RefreshTitleSelection();
        }
        else if (state == ScreenState.Title &&
                 (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                  Keyboard.current.dKey.wasPressedThisFrame))
        {
            selectedPlayerCount = 2;
            RefreshTitleSelection();
        }
        else if (state == ScreenState.Instructions &&
                 Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ShowTitle();
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void BuildInterface()
    {
        screenRoot = new GameObject(
            "Front End Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        screenRoot.transform.SetParent(transform, false);

        Canvas canvas = screenRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = screenRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = CreateUiObject(
            "Backdrop", screenRoot.transform, typeof(Image));
        Stretch(backdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        backdrop.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.055f, 0.98f);

        GameObject frame = CreateUiObject(
            "Content Frame", backdrop.transform, typeof(Image));
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.12f, 0.1f);
        frameRect.anchorMax = new Vector2(0.88f, 0.9f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        frame.GetComponent<Image>().color = new Color(0.055f, 0.08f, 0.14f, 1f);

        heading = CreateText(
            "Heading", frame.transform, 52, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetAnchors(heading.rectTransform, new Vector2(0.06f, 0.78f),
            new Vector2(0.94f, 0.96f));
        heading.color = new Color(1f, 0.72f, 0.12f);

        body = CreateText(
            "Body", frame.transform, 22, FontStyle.Normal, TextAnchor.MiddleLeft);
        SetAnchors(body.rectTransform, new Vector2(0.08f, 0.16f),
            new Vector2(0.92f, 0.79f));
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Truncate;
        body.resizeTextForBestFit = true;
        body.resizeTextMinSize = 15;
        body.resizeTextMaxSize = 28;

        prompt = CreateText(
            "Prompt", frame.transform, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetAnchors(prompt.rectTransform, new Vector2(0.08f, 0.03f),
            new Vector2(0.92f, 0.14f));
        prompt.color = new Color(0.35f, 0.9f, 1f);
    }

    private void ShowTitle()
    {
        state = ScreenState.Title;
        heading.text = "BONK SQUAD";
        heading.fontSize = 64;
        body.alignment = TextAnchor.MiddleCenter;
        body.fontSize = 28;
        body.text =
            "BUILD MOMENTUM AND BONK EVERY MONSTER OUT OF THE ARENA.\n\n" +
            "CHOOSE GAME MODE";
        RefreshTitleSelection();
    }

    private void RefreshTitleSelection()
    {
        string onePlayer = selectedPlayerCount == 1 ? "[  1 PLAYER  ]" : "   1 PLAYER   ";
        string twoPlayer = selectedPlayerCount == 2 ? "[  2 PLAYERS  ]" : "   2 PLAYERS   ";
        prompt.text =
            $"{onePlayer}          {twoPlayer}\n" +
            "A / D OR ← / → TO CHOOSE     •     ENTER TO CONTINUE";
    }

    private void ShowInstructions()
    {
        state = ScreenState.Instructions;
        heading.text = "HOW TO PLAY";
        heading.fontSize = 46;
        body.alignment = TextAnchor.UpperLeft;
        body.fontSize = 20;
        string controls = selectedPlayerCount == 1
            ? "W / A / S / D  —  Move and steer     SPACE  —  Switch form\n"
            : "PLAYER 1: W / A / S / D  —  Move and steer     Q  —  Switch form\n" +
              "PLAYER 2: ARROW KEYS  —  Move and steer     SPACE  —  Switch form\n";
        body.text =
            "CONTROLS\n" +
            controls +
            "ENTER  —  Continue / start the game\n" +
            "ESC  —  Return to the title screen\n\n" +
            "GOAL\nDefeat every monster. Build enough speed to destroy small enemies, " +
            "then reach maximum impact speed to defeat the large enemies.\n\n" +
            "FORMS\n" +
            "Robot form turns precisely but can take damage. Ball form carries momentum, " +
            "ricochets, and defeats enemies.\n\n" +
            "SPEED & SURFACES\nGray walls and barriers bounce you. Red bumpers bounce you faster. " +
            "Yellow floor tiles accelerate you forward. Blue ramps can be driven over to launch. " +
            "Chain ricochets within three seconds to build pinball momentum. " +
            "Your ball changes color as it reaches stronger impact tiers.\n\n" +
            "POWER-UPS\nCyan capsule  Speed boost\nGreen sphere  Restore one health\n" +
            "Power-ups appear at different locations over time. A collected speed boost is saved " +
            "if you are currently in robot form.\n\n" +
            "HUNTING\nNearby enemies receive gentle aim assistance. When three monsters remain, " +
            "the HUD shows the direction and distance to the nearest target.";
        prompt.text = "PRESS ENTER TO PLAY     •     ESC TO GO BACK";
    }

    private void StartGame()
    {
        LocalCoopBootstrap.ConfigurePlayerCount(selectedPlayerCount);
        state = ScreenState.Playing;
        Time.timeScale = 1f;
        screenRoot.SetActive(false);
    }

    private static GameObject CreateUiObject(
        string objectName, Transform parent, params System.Type[] components)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        foreach (System.Type component in components)
        {
            if (component != typeof(RectTransform))
            {
                uiObject.AddComponent(component);
            }
        }
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static Text CreateText(
        string objectName, Transform parent, int size,
        FontStyle style, TextAnchor alignment)
    {
        Text text = CreateUiObject(objectName, parent, typeof(Text)).GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(
        RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(
        RectTransform rect, Vector2 minimumOffset, Vector2 maximumOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minimumOffset;
        rect.offsetMax = maximumOffset;
    }
}
