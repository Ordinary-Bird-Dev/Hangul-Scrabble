using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Classic Mode: an English meaning clue is shown and the player must
// build the matching Korean word. The tray is dealt so the target word
// is always buildable. One hint per word (romanization) costs 50 points.
// The meaning card still appears via WordBuilder on success.
public class ClassicModeController : MonoBehaviour
{
    public const int HintPenalty = 50;

    private WordBuilder _builder;
    private SyllableBuilderUI _syllableBuilder;
    private MeaningCardUI _meaningCard;
    private TMP_Text _clueText;
    private System.Random _rng = new System.Random();
    private bool _hintUsed;

    private Button _hintButton;
    private Image _hintBackground;
    private bool? _hintShownEnabled;

    private static readonly Color HintEnabledColor = new Color(0.5f, 0.4f, 0.1f, 0.9f);
    private static readonly Color HintDisabledColor = new Color(0.3f, 0.28f, 0.2f, 0.45f);

    // Sizing for the RUNTIME FALLBACK banner only — the one BuildClueBanner
    // creates when GameScene has no ClueBanner of its own. Once the banner
    // is authored in the scene, the Inspector owns all of this and these
    // constants stop applying — the hint line is styled relative to
    // ClueText, so it follows the Inspector too.
    // The fallback banner grows downward from a fixed top edge, so raising
    // BannerHeight never pushes it into the TopBar.
    private const float BannerTopEdge = -165f;
    private const float BannerHeight = 160f;
    private const float BannerWidth = 920f;
    private const float ClueFontSize = 55f;
    // The hint line is styled RELATIVE to whatever ClueText is set to in
    // the Inspector, and inherits its colour. An absolute size and a fixed
    // colour here would fight the scene: a 72pt scene clue made a 36pt
    // hint look like a footnote, and a pale hint colour vanished against a
    // light banner. Bold plus a size step is enough to separate it.
    private const int HintSizePercent = 85;

    // A hint is offered only when this word has not had one AND the round
    // still has penalty budget left. Without the second half the penalty
    // is free at the floor, which is the bug this replaced.
    public bool CanUseHint =>
        !_hintUsed
        && Target != null
        && (GameManager.Instance == null || GameManager.Instance.CanAffordPenalty);

    // Word-set id to draw targets from (see WordValidator's registry);
    // null draws from every loaded set. The hook for difficulty/DLC
    // scoping of the target pool.
    public string TargetSource { get; set; }

    public WordEntry Target { get; private set; }
    public int RoundsCompleted { get; private set; }
    public bool HintUsed => _hintUsed;

    void Start()
    {
        Initialize();
    }

    void Update()
    {
        EnsureTargetBuildable();
        RefreshHintButton();
    }

    void OnDestroy()
    {
        if (_builder != null)
        {
            _builder.WordCompleted -= HandleWordCompleted;
            _builder.WordRejected -= HandleWordRejected;
            // Leaving Classic must restore the free-form modes: the gate
            // lives on WordBuilder, which outlives this controller.
            if (_builder.AcceptWord == (System.Func<WordEntry, bool>)IsTarget)
                _builder.AcceptWord = null;
        }
    }

    public void Configure(WordBuilder builder)
    {
        _builder = builder;
    }

    public void SetSeed(int seed)
    {
        _rng = new System.Random(seed);
    }

    public void Initialize()
    {
        WordValidator.Load();

        if (_builder == null)
            _builder = FindAnyObjectByType<WordBuilder>();
        if (_builder != null)
        {
            _builder.WordCompleted += HandleWordCompleted;
            _builder.WordRejected += HandleWordRejected;
            _builder.AcceptWord = IsTarget;
        }

        if (_syllableBuilder == null)
            _syllableBuilder = FindAnyObjectByType<SyllableBuilderUI>();

        ResolveUI();
        NextTarget();
    }

    // Candidates are short words (2-3 syllables) with an English meaning.
    // excludeWord keeps the same target from being picked twice in a row.
    public static WordEntry PickTarget(IEnumerable<WordEntry> entries, System.Random rng, string excludeWord = null)
    {
        var candidates = new List<WordEntry>();
        WordEntry excluded = null;
        foreach (WordEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.english)) continue;
            if (entry.syllable_count < 2 || entry.syllable_count > 3) continue;
            if (excludeWord != null && entry.word == excludeWord)
            {
                excluded = entry;
                continue;
            }
            candidates.Add(entry);
        }

        // Re-allow the excluded word only when it is the sole candidate.
        if (candidates.Count == 0) return excluded;
        return candidates[rng.Next(candidates.Count)];
    }

    // Every jamo needed to compose the word, in order.
    public static List<string> RequiredJamoFor(string word) =>
        TrayValidator.RequiredJamoFor(word);

    public void NextTarget()
    {
        IEnumerable<WordEntry> pool = TargetSource == null
            ? WordValidator.AllEntries
            : WordValidator.EntriesFor(TargetSource);
        Target = PickTarget(pool, _rng, Target != null ? Target.word : null);
        _hintUsed = false;
        UpdateClue();
        RefreshHintButton();

        if (Target != null && TileManager.Instance != null)
        {
            // Start order between this controller and TileManager (both on
            // the GameController object) is not guaranteed; Initialize() is
            // idempotent and ensures the tray exists before the exact deal.
            TileManager.Instance.Initialize();
            TileManager.Instance.DealExact(RequiredJamoFor(Target.word));
        }
    }

    // Classic deals exactly the target's jamo, but a required jamo can
    // still leak for good: WordResetButton throws away the chain without
    // restoring its consumed tiles, and refill is disabled in this mode.
    // Jamo the player is still holding (in the syllable slot or the word
    // chain) count as available; when the tray + held jamo can no longer
    // form the target, the exact set is re-dealt.
    private void EnsureTargetBuildable()
    {
        if (Target == null || TileManager.Instance == null) return;

        List<string> required = RequiredJamoFor(Target.word);
        if (required.Count == 0) return;

        var available = new List<string>();
        foreach (JamoTile tile in TileManager.Instance.Tiles)
            if (!tile.IsConsumed && !string.IsNullOrEmpty(tile.Jamo))
                available.Add(tile.Jamo);

        if (_builder != null)
            available.AddRange(RequiredJamoFor(_builder.CurrentWord));

        if (_syllableBuilder == null)
            _syllableBuilder = FindAnyObjectByType<SyllableBuilderUI>();
        if (_syllableBuilder != null && _syllableBuilder.Slot != null)
        {
            if (_syllableBuilder.Slot.Cho != "") available.Add(_syllableBuilder.Slot.Cho);
            if (_syllableBuilder.Slot.Jung != "") available.Add(_syllableBuilder.Slot.Jung);
            if (_syllableBuilder.Slot.Jong != "") available.Add(_syllableBuilder.Slot.Jong);
        }

        if (ContainsAll(required, available)) return;
        TileManager.Instance.DealExact(required);
    }

    // Multiset check: every jamo in required (with duplicates) must be
    // matched by a distinct jamo in available.
    public static bool ContainsAll(IReadOnlyList<string> required, List<string> available) =>
        TrayValidator.ContainsAll(required, available);

    // Hint button: reveal the romanization at a 50-point penalty, while
    // the round can still pay for it.
    public void UseHint()
    {
        if (!CanUseHint) return;

        _hintUsed = true;
        if (GameManager.Instance != null)
            GameManager.Instance.AddPoints(-HintPenalty);
        UpdateClue();
        RefreshHintButton();
    }

    // Greys the button out rather than hiding it, so the hint stays
    // discoverable and its absence reads as "spent", not "missing".
    //
    // interactable is the only thing touched on a scene-authored button —
    // its ColorBlock's Disabled Color, set in the Inspector, does the
    // greying. _hintBackground is non-null only for the runtime fallback,
    // which has no Inspector to configure.
    //
    // Cached: this runs every frame, and reassigning an unchanged colour
    // dirties the Graphic.
    private void RefreshHintButton()
    {
        if (_hintButton == null) return;

        bool enabled = CanUseHint;
        if (_hintShownEnabled == enabled) return;
        _hintShownEnabled = enabled;

        _hintButton.interactable = enabled;
        if (_hintBackground != null)
            _hintBackground.color = enabled ? HintEnabledColor : HintDisabledColor;
    }

    // The scoring gate for guided mode: only the word the clue asked for
    // is accepted, so a real dictionary word built from the same tiles no
    // longer earns points or counts toward WordsCompleted. Fails open when
    // no target has been picked yet, so a setup gap cannot make the game
    // unwinnable. Compares by word, not reference, so a homonym entry for
    // the same spelling still counts.
    public bool IsTarget(WordEntry entry) =>
        Target == null || (entry != null && entry.word == Target.word);

    public void HandleWordCompleted(WordEntry entry)
    {
        if (Target == null || entry == null) return;
        if (entry.word != Target.word) return;

        // WordBuilder shows the card for the word's primary sense; when
        // the clue was a different homonym sense (사과 "apology" vs the
        // primary "apple"), re-show the card with the Target so it
        // matches what the player was asked to find.
        if (!ReferenceEquals(entry, Target))
            ShowTargetCard();

        RoundsCompleted++;
        NextTarget();
    }

    private void ShowTargetCard()
    {
        // The card is runtime-built by WordBuilder and inactive while
        // hidden, so the lookup must include inactive objects.
        if (_meaningCard == null)
            _meaningCard = FindAnyObjectByType<MeaningCardUI>(FindObjectsInactive.Include);
        if (_meaningCard != null) _meaningCard.Show(Target);
    }

    // A wrong submission wipes the attempt and re-deals the same exact
    // tile set, so the player retries the sequencing puzzle from scratch.
    public void HandleWordRejected(string word)
    {
        ResetPuzzle();
    }

    // Chain first, slot second: ResetSlot returns tiles still held
    // mid-syllable to the tray before the re-deal overwrites everything.
    private void ResetPuzzle()
    {
        if (Target == null) return;

        if (_builder != null) _builder.ClearChain();
        if (_syllableBuilder == null)
            _syllableBuilder = FindAnyObjectByType<SyllableBuilderUI>();
        if (_syllableBuilder != null) _syllableBuilder.ResetSlot();

        if (TileManager.Instance != null)
            TileManager.Instance.DealExact(RequiredJamoFor(Target.word));
    }

    private void UpdateClue()
    {
        if (_clueText == null || Target == null) return;

        string hint = _hintUsed
            ? $"\n<size={HintSizePercent}%><b>{Target.romanization}</b></size>"
            : "";
        _clueText.text = $"Find: <b>{Target.english}</b>{hint}";
    }

    // Scene objects win over the runtime fallbacks below. Author these in
    // GameScene to control size, colour, font and placement from the
    // Inspector:
    //
    //   ClueBanner        Image (the panel background)
    //   └── ClueText      TextMeshProUGUI — the script only sets .text
    //   HintButton        Image + Button (Inspector's Disabled Color is
    //   └── Label         what greys it out; leave the label text alone,
    //                     the script does not overwrite it)
    //
    // Do NOT add UseHint to HintButton's OnClick list in the Inspector —
    // this wires it in code, and both would fire (harmless, but it makes
    // the penalty look doubled the first time you read the score).
    //
    // Anything missing is built at runtime instead, so a scene without
    // them still plays. Search includes inactive objects: GameObject.Find
    // cannot see them, which is how WordConfirmButton hid for months.
    private void ResolveUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();

        // Switched off by SceneBootstrap outside Classic, so switch them
        // back on here rather than relying on load order. Self-sufficient:
        // this controller only exists in Classic, so anything it finds
        // should be visible.
        GameObject bannerGo = FindInScene("ClueBanner");
        if (bannerGo != null) bannerGo.SetActive(true);

        GameObject clueGo = FindInScene("ClueText");
        _clueText = clueGo != null ? clueGo.GetComponent<TMP_Text>() : null;

        GameObject hintGo = FindInScene("HintButton");
        if (hintGo != null) hintGo.SetActive(true);
        _hintButton = hintGo != null ? hintGo.GetComponent<Button>() : null;
        if (hintGo != null && _hintButton == null)
            Debug.LogWarning("ClassicModeController: 'HintButton' exists but has no Button component — the hint is not clickable.");
        if (_hintButton != null)
        {
            _hintBackground = null;   // scene-authored: its ColorBlock owns the tint
            _hintButton.onClick.AddListener(UseHint);
        }

        if (canvas == null)
        {
            if (_clueText == null || _hintButton == null)
                Debug.LogWarning("ClassicModeController: no Canvas in GameScene — missing clue/hint UI cannot be built.");
            return;
        }

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        TMP_FontAsset font = donor != null ? donor.font : null;

        if (_clueText == null) BuildClueBanner(canvas, font);
        if (_hintButton == null) BuildHintButton(canvas, font);
    }

    private static GameObject FindInScene(string name)
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    // Runtime fallback: clue banner under the TopBar.
    private void BuildClueBanner(Canvas canvas, TMP_FontAsset font)
    {
        var banner = new GameObject("ClueBanner", typeof(RectTransform));
        banner.transform.SetParent(canvas.transform, false);
        var bannerRect = (RectTransform)banner.transform;
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        // Centre pivot, so the offset is derived from the fixed top edge.
        bannerRect.anchoredPosition = new Vector2(0f, BannerTopEdge - BannerHeight * 0.5f);
        bannerRect.sizeDelta = new Vector2(BannerWidth, BannerHeight);

        Image bannerBg = banner.AddComponent<Image>();
        bannerBg.color = new Color(0.1f, 0.3f, 0.2f, 0.85f);
        bannerBg.raycastTarget = false;

        var clueGo = new GameObject("ClueText", typeof(RectTransform));
        clueGo.transform.SetParent(banner.transform, false);
        var clueRect = (RectTransform)clueGo.transform;
        clueRect.anchorMin = Vector2.zero;
        clueRect.anchorMax = Vector2.one;
        clueRect.offsetMin = new Vector2(20f, 8f);
        clueRect.offsetMax = new Vector2(-20f, -8f);

        _clueText = clueGo.AddComponent<TextMeshProUGUI>();
        _clueText.fontSize = ClueFontSize;
        _clueText.alignment = TextAlignmentOptions.Center;
        _clueText.raycastTarget = false;
        // Deliberately NOT auto-sized: the hint line sets an absolute
        // <size=> and TMP's auto-sizing interacts badly with absolute size
        // tags. The banner is instead tall enough for a wrapped clue plus
        // the hint line — raise BannerHeight if a gloss ever overflows.
        if (font != null) _clueText.font = font;
    }

    // Runtime fallback: hint button in the top-right.
    private void BuildHintButton(Canvas canvas, TMP_FontAsset font)
    {
        var hintGo = new GameObject("HintButton", typeof(RectTransform));
        hintGo.transform.SetParent(canvas.transform, false);
        var hintRect = (RectTransform)hintGo.transform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.anchoredPosition = new Vector2(-140f, -370f);
        hintRect.sizeDelta = new Vector2(250f, 84f);

        _hintBackground = hintGo.AddComponent<Image>();
        _hintBackground.color = HintEnabledColor;
        _hintButton = hintGo.AddComponent<Button>();
        // AddComponent does not assign targetGraphic the way the Inspector
        // does, so without this the button's tint never changes.
        _hintButton.targetGraphic = _hintBackground;
        _hintButton.onClick.AddListener(UseHint);

        var hintLabelGo = new GameObject("Label", typeof(RectTransform));
        hintLabelGo.transform.SetParent(hintGo.transform, false);
        var hintLabelRect = (RectTransform)hintLabelGo.transform;
        hintLabelRect.anchorMin = Vector2.zero;
        hintLabelRect.anchorMax = Vector2.one;
        hintLabelRect.offsetMin = Vector2.zero;
        hintLabelRect.offsetMax = Vector2.zero;

        var hintLabel = hintLabelGo.AddComponent<TextMeshProUGUI>();
        hintLabel.text = $"Hint (-{HintPenalty})";
        hintLabel.fontSize = 34f;
        hintLabel.alignment = TextAlignmentOptions.Center;
        hintLabel.raycastTarget = false;
        if (font != null) hintLabel.font = font;
    }
}
