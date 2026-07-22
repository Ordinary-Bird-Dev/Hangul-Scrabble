using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Word Hunt Mode: an English meaning clue is shown and the player must
// build the matching Korean word. The tray is dealt so the target word
// is always buildable. One hint per word (romanization) costs 50 points.
// The meaning card still appears via WordBuilder on success.
public class WordHuntController : MonoBehaviour
{
    public const int HintPenalty = 50;

    private WordBuilder _builder;
    private SyllableBuilderUI _syllableBuilder;
    private TMP_Text _clueText;
    private System.Random _rng = new System.Random();
    private bool _hintUsed;

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
    }

    void OnDestroy()
    {
        if (_builder != null)
            _builder.WordCompleted -= HandleWordCompleted;
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
            _builder.WordCompleted += HandleWordCompleted;

        BuildUI();
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
    public static List<string> RequiredJamoFor(string word)
    {
        var required = new List<string>();
        if (string.IsNullOrEmpty(word)) return required;

        foreach (char syllable in word)
        {
            if (!HangulComposer.TryDecompose(syllable.ToString(), out string cho, out string jung, out string jong))
                continue;
            required.Add(cho);
            required.Add(jung);
            if (jong != "") required.Add(jong);
        }
        return required;
    }

    public void NextTarget()
    {
        Target = PickTarget(WordValidator.AllEntries, _rng, Target != null ? Target.word : null);
        _hintUsed = false;
        UpdateClue();

        if (Target != null && TileManager.Instance != null)
            TileManager.Instance.DealAllWithRequired(RequiredJamoFor(Target.word));
    }

    // Word Hunt promises the target is always buildable, but consumed
    // tiles refill with random jamo — so a slot/chain reset or a completed
    // non-target word can leak a required jamo out of the tray for good.
    // Jamo the player is still holding (in the syllable slot or the word
    // chain) count as available; when the tray + held jamo can no longer
    // form the target, the tray is re-dealt with the required jamo.
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
        TileManager.Instance.DealAllWithRequired(required);
    }

    // Multiset check: every jamo in required (with duplicates) must be
    // matched by a distinct jamo in available.
    public static bool ContainsAll(IReadOnlyList<string> required, List<string> available)
    {
        var pool = new List<string>(available);
        foreach (string jamo in required)
            if (!pool.Remove(jamo)) return false;
        return true;
    }

    // Hint button: reveal the romanization at a 50-point penalty.
    public void UseHint()
    {
        if (_hintUsed || Target == null) return;

        _hintUsed = true;
        if (GameManager.Instance != null)
            GameManager.Instance.AddPoints(-HintPenalty);
        UpdateClue();
    }

    public void HandleWordCompleted(WordEntry entry)
    {
        if (Target == null || entry == null) return;
        if (entry.word != Target.word) return;

        RoundsCompleted++;
        NextTarget();
    }

    private void UpdateClue()
    {
        if (_clueText == null || Target == null) return;

        string hint = _hintUsed ? $"\n<size=70%><i>{Target.romanization}</i></size>" : "";
        _clueText.text = $"Find: <b>{Target.english}</b>{hint}";
    }

    // Clue banner under the TopBar plus a hint button.
    private void BuildUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        TMP_FontAsset font = donor != null ? donor.font : null;

        var banner = new GameObject("WordHuntClue", typeof(RectTransform));
        banner.transform.SetParent(canvas.transform, false);
        var bannerRect = (RectTransform)banner.transform;
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -215f);
        bannerRect.sizeDelta = new Vector2(920f, 100f);

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
        _clueText.fontSize = 38f;
        _clueText.alignment = TextAlignmentOptions.Center;
        _clueText.raycastTarget = false;
        if (font != null) _clueText.font = font;

        var hintGo = new GameObject("HintButton", typeof(RectTransform));
        hintGo.transform.SetParent(canvas.transform, false);
        var hintRect = (RectTransform)hintGo.transform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.anchoredPosition = new Vector2(-130f, -330f);
        hintRect.sizeDelta = new Vector2(220f, 70f);

        Image hintBg = hintGo.AddComponent<Image>();
        hintBg.color = new Color(0.5f, 0.4f, 0.1f, 0.9f);
        Button hintButton = hintGo.AddComponent<Button>();
        hintButton.onClick.AddListener(UseHint);

        var hintLabelGo = new GameObject("Label", typeof(RectTransform));
        hintLabelGo.transform.SetParent(hintGo.transform, false);
        var hintLabelRect = (RectTransform)hintLabelGo.transform;
        hintLabelRect.anchorMin = Vector2.zero;
        hintLabelRect.anchorMax = Vector2.one;
        hintLabelRect.offsetMin = Vector2.zero;
        hintLabelRect.offsetMax = Vector2.zero;

        var hintLabel = hintLabelGo.AddComponent<TextMeshProUGUI>();
        hintLabel.text = $"Hint (-{HintPenalty})";
        hintLabel.fontSize = 30f;
        hintLabel.alignment = TextAlignmentOptions.Center;
        hintLabel.raycastTarget = false;
        if (font != null) hintLabel.font = font;
    }
}
