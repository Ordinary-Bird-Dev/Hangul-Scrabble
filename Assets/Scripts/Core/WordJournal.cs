using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Zen Mode's running word journal: every found word is listed with its
// meaning, newest first. Builds its own panel at runtime.
//
// The panel is hidden until the first word lands. An empty journal is a
// blank scrim rectangle sitting over the board from the moment Zen starts —
// it reads as a loading placeholder or a bug, not as a container waiting to
// be filled. Once it has something to show it fades in and stays for the
// rest of the session; Count only ever grows, so there is no path back to
// hidden.
public class WordJournal : MonoBehaviour
{
    public const int MaxVisibleEntries = 8;

    // Short enough not to delay the next word, long enough to read as an
    // arrival rather than a pop.
    private const float RevealSeconds = 0.3f;
    private const float RevealSlide = 60f;

    [SerializeField] private TMP_Text _text;

    private readonly List<string> _lines = new List<string>();
    private WordBuilder _builder;
    private bool _initialized;

    // The panel behind the text, when there is one. Set by BuildPanel, or
    // inferred from the text's parent for a journal that was Configure()d
    // with a scene-authored panel. Stays null when the text has no parent
    // at all — which is how the tests build it — and every visibility call
    // then no-ops rather than hiding the caller's own object.
    private GameObject _panel;
    private RectTransform _panelRect;
    private CanvasGroup _panelGroup;
    private Vector2 _panelHome;
    private bool _revealed;
    private Coroutine _revealRoutine;

    // Total words found this session (not capped like the visible list).
    public int Count { get; private set; }
    public IReadOnlyList<string> Lines => _lines;

    // Whether the panel is currently on screen. Hidden before the first
    // word, shown from then on.
    public bool PanelVisible => _panel == null || (_revealed && _panel.activeSelf);

    void Start()
    {
        Initialize();
    }

    void OnDestroy()
    {
        if (_builder != null)
            _builder.WordCompleted -= AddWord;
    }

    public void Configure(TMP_Text text, WordBuilder builder)
    {
        _text = text;
        _builder = builder;
    }

    public void Initialize()
    {
        if (_initialized) return;

        if (_builder == null)
            _builder = FindAnyObjectByType<WordBuilder>();
        if (_builder != null)
            _builder.WordCompleted += AddWord;

        if (_text == null)
            _text = BuildPanel();

        ResolvePanel();

        _initialized = true;
        UpdateText();

        // Instant, not faded: this runs on the first frame of the scene, and
        // there is nothing to animate away from.
        ApplyVisibility(instant: true);
    }

    public static string FormatEntry(WordEntry entry)
    {
        if (entry == null) return "";
        return string.IsNullOrEmpty(entry.english) ? entry.word : $"{entry.word} — {entry.english}";
    }

    public void AddWord(WordEntry entry)
    {
        if (entry == null) return;

        _lines.Insert(0, FormatEntry(entry));
        while (_lines.Count > MaxVisibleEntries)
            _lines.RemoveAt(_lines.Count - 1);

        Count++;
        UpdateText();
        ApplyVisibility(instant: false);
    }

    private void UpdateText()
    {
        if (_text == null) return;
        _text.text = _lines.Count == 0 ? "" : string.Join("\n", _lines);
    }

    // ---- visibility ----------------------------------------------------

    // BuildPanel sets _panel directly. This covers the other route in:
    // a journal handed an existing TMP_Text, whose panel is its parent.
    private void ResolvePanel()
    {
        if (_panel != null || _text == null) return;

        Transform parent = _text.transform.parent;
        if (parent == null) return;

        _panel = parent.gameObject;
        _panelRect = parent as RectTransform;
        _panelGroup = _panel.GetComponent<CanvasGroup>();
        if (_panelGroup == null) _panelGroup = _panel.AddComponent<CanvasGroup>();
        if (_panelRect != null) _panelHome = _panelRect.anchoredPosition;
    }

    private void ApplyVisibility(bool instant)
    {
        if (_panel == null) return;

        if (Count <= 0)
        {
            _panel.SetActive(false);
            _revealed = false;
            return;
        }

        if (_revealed) return;
        _revealed = true;
        _panel.SetActive(true);

        // isActiveAndEnabled: StartCoroutine throws on a disabled component,
        // and this can be called from Initialize before the object is fully
        // live. Falling back to an instant show is correct either way — the
        // journal is visible, just without the flourish.
        if (instant || !isActiveAndEnabled)
        {
            if (_panelGroup != null) _panelGroup.alpha = 1f;
            if (_panelRect != null) _panelRect.anchoredPosition = _panelHome;
            return;
        }

        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(RevealRoutine());
    }

    // Fades up while sliding in from the left edge it is anchored to, so the
    // journal reads as arriving from off-screen rather than materialising on
    // top of the board.
    private IEnumerator RevealRoutine()
    {
        if (_panelGroup != null) _panelGroup.alpha = 0f;

        if (_panelRect != null)
        {
            _panelRect.anchoredPosition = _panelHome - new Vector2(RevealSlide, 0f);
            StartCoroutine(UITween.SlideTo(_panelRect, _panelHome, RevealSeconds));
        }

        if (_panelGroup != null)
            yield return UITween.FadeTo(_panelGroup, 1f, RevealSeconds);

        _revealRoutine = null;
    }

    // ---- construction --------------------------------------------------

    // Semi-transparent journal panel on the left side, under the TopBar.
    private TMP_Text BuildPanel()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        var panelGo = new GameObject("WordJournal", typeof(RectTransform));
        panelGo.transform.SetParent(canvas.transform, false);

        var rect = (RectTransform)panelGo.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -180f);
        rect.sizeDelta = new Vector2(400f, 560f);

        Image bg = panelGo.AddComponent<Image>();
        // Stays a scrim, not a Surface: the journal overlays live gameplay,
        // so a solid white panel would hide the board behind it.
        bg.color = Palette.Scrim;
        bg.raycastTarget = false;

        var textGo = new GameObject("JournalText", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.color = Palette.OnJungle;
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (donor != null && donor != text && donor.font != null)
            text.font = donor.font;

        // Recorded here rather than left to ResolvePanel: the panel is
        // switched off a moment later, and its home position has to be
        // captured before the reveal slide moves it.
        _panel = panelGo;
        _panelRect = rect;
        _panelGroup = panelGo.AddComponent<CanvasGroup>();
        _panelHome = rect.anchoredPosition;

        return text;
    }
}
