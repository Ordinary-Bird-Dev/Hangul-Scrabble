using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The screen between the last tutorial lesson and a real round.
//
// Before this existed, finishing lesson 5 loaded GameScene on the same
// frame, which meant the reward for completing the tutorial was having the
// tutorial yanked away mid-celebration. This holds the moment and lets the
// player choose where to go.
//
// An overlay inside GameScene rather than a scene of its own, for the same
// reason the tutorial itself lives here: a second scene would need its own
// copy of the background, the mascot and the layout, and this project
// already has the scar from ResultScene's hand-copied mascot drifting out
// of sync for months. The board stays on screen behind a scrim, dimmed.
//
// Works two ways, deliberately:
//   * If GameScene contains an object named TutorialCompletePanel, this
//     uses it and wires its named children — so the panel can be styled in
//     the Inspector with real sprites and rounded corners.
//   * If it does not, one is built at runtime from Palette colours. Plain
//     rectangles, like MeaningCardUI and WordJournal, but nothing to wire
//     and nothing to forget.
public class TutorialCompletePanel : MonoBehaviour
{
    public const string PanelName = "TutorialCompletePanel";
    public const string PlayButtonName = "TutorialPlayButton";
    public const string TitleButtonName = "TutorialTitleButton";
    public const string HeadlineName = "TutorialCompleteHeadline";
    public const string BodyName = "TutorialCompleteBody";

    private const string DefaultHeadline = "Nicely done";
    private const string DefaultBody =
        "You built five Korean words from their pieces.\nReady for a real round?";

    [SerializeField] private float _fadeSeconds = 0.28f;

    private CanvasGroup _group;
    private RectTransform _card;
    private Coroutine _routine;
    private bool _wired;

    // ---- entry point ---------------------------------------------------

    // Finds or builds the panel, wires the two routes, and shows it.
    // Safe to call more than once; the second call is a no-op because the
    // panel is already on screen and TutorialController guards as well.
    public static TutorialCompletePanel Show(System.Action onPlay, System.Action onTitle)
    {
        TutorialCompletePanel panel = Resolve();
        if (panel == null) return null;

        panel.Wire(onPlay, onTitle);
        panel.Appear();
        return panel;
    }

    private static TutorialCompletePanel Resolve()
    {
        // Inactive-inclusive: a scene-authored panel is switched off until
        // it is needed, and GameObject.Find cannot see inactive objects —
        // the single most repeated bug in this codebase.
        TutorialCompletePanel existing =
            FindAnyObjectByType<TutorialCompletePanel>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        GameObject authored = FindIncludingInactive(PanelName);
        if (authored != null) return authored.AddComponent<TutorialCompletePanel>();

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("TutorialCompletePanel: no Canvas in the scene — the tutorial cannot show its finish screen, so it will fall straight through to the game.");
            return null;
        }

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return CreateRuntime(canvas.transform, donor != null ? donor.font : null);
    }

    // ---- wiring --------------------------------------------------------

    private void Wire(System.Action onPlay, System.Action onTitle)
    {
        if (_wired) return;
        _wired = true;

        WireButton(PlayButtonName, onPlay);
        WireButton(TitleButtonName, onTitle);
    }

    private void WireButton(string name, System.Action action)
    {
        Transform found = FindInChildren(transform, name);
        Button button = found != null ? found.GetComponent<Button>() : null;

        if (button == null)
        {
            Debug.LogError($"TutorialCompletePanel: '{name}' not found under the panel, or it has no Button — that route out of the finish screen is dead. A player could be stuck here.");
            return;
        }

        // RemoveAllListeners first: a scene-authored button may carry an
        // onClick set in the Inspector, and two handlers would mean two
        // scene loads racing each other.
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action?.Invoke());
    }

    // ---- show ----------------------------------------------------------

    private void Appear()
    {
        gameObject.SetActive(true);

        // Last sibling so the scrim is above everything the scene and the
        // other runtime builders have added — including MeaningCardUI,
        // which is created at runtime too and would otherwise draw on top.
        transform.SetAsLastSibling();

        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        // By name first, then first child. A scene-authored panel can put
        // anything in slot 0, and scale-punching a full-screen element
        // would look like the whole screen lurching.
        if (_card == null)
        {
            Transform named = FindInChildren(transform, "Card");
            if (named != null && named != transform) _card = named as RectTransform;
            else if (transform.childCount > 0) _card = transform.GetChild(0) as RectTransform;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        _group.alpha = 0f;
        // Blocks the board underneath from the first frame, before the fade
        // has finished, so a tap during the fade cannot reach a tile.
        _group.blocksRaycasts = true;

        if (_card != null) _card.localScale = Vector3.one * 0.94f;

        if (_card != null) StartCoroutine(UITween.ScaleTo(_card, Vector3.one, _fadeSeconds));
        yield return UITween.FadeTo(_group, 1f, _fadeSeconds);

        _routine = null;
    }

    // ---- runtime construction ------------------------------------------

    public static TutorialCompletePanel CreateRuntime(Transform canvasParent, TMP_FontAsset font)
    {
        var panelGo = new GameObject(PanelName, typeof(RectTransform));
        panelGo.transform.SetParent(canvasParent, false);

        var rect = (RectTransform)panelGo.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // The scrim is the input blocker as well as the dimmer, so it keeps
        // raycastTarget on — unlike WordJournal's panel, which sits over
        // live play and must not swallow taps.
        Image scrim = panelGo.AddComponent<Image>();
        scrim.color = Palette.Scrim;
        scrim.raycastTarget = true;

        panelGo.AddComponent<CanvasGroup>();

        // Card. First child, which is what Appear() picks up to scale.
        var cardGo = new GameObject("Card", typeof(RectTransform));
        cardGo.transform.SetParent(panelGo.transform, false);
        var cardRect = (RectTransform)cardGo.transform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 140f);
        cardRect.sizeDelta = new Vector2(840f, 620f);
        cardGo.AddComponent<Image>().color = Palette.Surface;

        Label(cardGo.transform, HeadlineName, DefaultHeadline, font,
            Palette.Ink, 68f, FontStyles.Bold,
            topOffset: 56f, height: 96f);

        Label(cardGo.transform, BodyName, DefaultBody, font,
            Palette.InkSoft, 38f, FontStyles.Normal,
            topOffset: 172f, height: 130f);

        // Action colour on Play only. Two accent buttons would make the
        // choice read as equally weighted, and it is not — the tutorial
        // exists to get someone playing.
        Button(cardGo.transform, PlayButtonName, "Start Playing", font,
            Palette.Action, Palette.ActionEdge, Palette.OnAction,
            bottomOffset: 176f, height: 132f, fontSize: 46f);

        Button(cardGo.transform, TitleButtonName, "Back to Title", font,
            Palette.SurfaceSunken, Palette.SurfaceEdge, Palette.Ink,
            bottomOffset: 48f, height: 112f, fontSize: 40f);

        TutorialCompletePanel panel = panelGo.AddComponent<TutorialCompletePanel>();
        panelGo.SetActive(false);
        return panel;
    }

    private static void Label(Transform parent, string name, string content,
        TMP_FontAsset font, Color color, float size, FontStyles style,
        float topOffset, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -topOffset);
        rect.sizeDelta = new Vector2(720f, height);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.color = color;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Top;
        text.raycastTarget = false;
    }

    // The button object IS the drop edge; a Fill child inset 6 from the
    // bottom is the face, and the face is the Button's targetGraphic. So a
    // press darkens the face and leaves the edge, which is how the
    // scene-authored buttons in this game behave.
    private static void Button(Transform parent, string name, string label,
        TMP_FontAsset font, Color face, Color edge, Color onFace,
        float bottomOffset, float height, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottomOffset);
        rect.sizeDelta = new Vector2(720f, height);
        go.AddComponent<Image>().color = edge;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(go.transform, false);
        var fillRect = (RectTransform)fillGo.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(0f, 6f);
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillGo.AddComponent<Image>();
        fill.color = face;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(fillGo.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = label;
        text.color = onFace;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        var button = go.AddComponent<Button>();
        // targetGraphic is the face, and the default ColorBlock's normal
        // colour is white — which multiplies, so the face keeps the colour
        // set above and only darkens on hover and press. Setting the tint
        // here instead of on the Image is the part that actually works: a
        // Selectable overwrites its target graphic's rendered colour on the
        // first frame regardless of what the Image says.
        button.targetGraphic = fill;
        button.colors = ColorBlock.defaultColorBlock;
    }

    // ---- lookup helpers ------------------------------------------------

    private static Transform FindInChildren(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject root in
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
}
