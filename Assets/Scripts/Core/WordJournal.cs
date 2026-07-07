using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Zen Mode's running word journal: every found word is listed with its
// meaning, newest first. Builds its own panel at runtime.
public class WordJournal : MonoBehaviour
{
    public const int MaxVisibleEntries = 8;

    [SerializeField] private TMP_Text _text;

    private readonly List<string> _lines = new List<string>();
    private WordBuilder _builder;
    private bool _initialized;

    // Total words found this session (not capped like the visible list).
    public int Count { get; private set; }
    public IReadOnlyList<string> Lines => _lines;

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

        _initialized = true;
        UpdateText();
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
    }

    private void UpdateText()
    {
        if (_text == null) return;
        _text.text = _lines.Count == 0 ? "" : string.Join("\n", _lines);
    }

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
        bg.color = new Color(0f, 0f, 0f, 0.25f);
        bg.raycastTarget = false;

        var textGo = new GameObject("JournalText", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (donor != null && donor != text && donor.font != null)
            text.font = donor.font;

        return text;
    }
}
