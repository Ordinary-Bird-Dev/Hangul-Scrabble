using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Chains confirmed syllables into the word bar, validates the chain
// against the TOPIK word list, and shows the meaning card on success.
public class WordBuilder : MonoBehaviour
{
    public event System.Action<WordEntry> WordCompleted;
    public event System.Action<string> WordRejected;

    [SerializeField] private SyllableBuilderUI _syllableBuilder;
    [SerializeField] private TMP_Text _wordText;
    [SerializeField] private Button _wordConfirmButton;
    [SerializeField] private Button _wordResetButton;
    [SerializeField] private MeaningCardUI _meaningCard;

    private readonly List<string> _syllables = new List<string>();
    private bool _initialized;

    public string CurrentWord => string.Concat(_syllables);
    public int SyllableCount => _syllables.Count;

    void Start()
    {
        Initialize();
    }

    void OnDestroy()
    {
        if (_syllableBuilder != null)
            _syllableBuilder.SyllableConfirmed -= AppendSyllable;
    }

    public void Configure(SyllableBuilderUI builder, TMP_Text wordText,
        Button confirmButton = null, Button resetButton = null, MeaningCardUI meaningCard = null)
    {
        _syllableBuilder = builder;
        _wordText = wordText;
        _wordConfirmButton = confirmButton;
        _wordResetButton = resetButton;
        _meaningCard = meaningCard;
    }

    public void Initialize()
    {
        if (_initialized) return;

        WordValidator.Load();

        if (_syllableBuilder == null)
            _syllableBuilder = FindAnyObjectByType<SyllableBuilderUI>();
        if (_syllableBuilder != null)
            _syllableBuilder.SyllableConfirmed += AppendSyllable;

        if (_wordText == null)
        {
            GameObject wordTextGo = GameObject.Find("WordText");
            if (wordTextGo != null) _wordText = wordTextGo.GetComponent<TMP_Text>();
        }

        if (_wordConfirmButton == null) _wordConfirmButton = FindButton("WordConfirmButton");
        if (_wordConfirmButton != null) _wordConfirmButton.onClick.AddListener(OnWordConfirmPressed);

        if (_wordResetButton == null) _wordResetButton = FindButton("WordResetButton");
        if (_wordResetButton != null) _wordResetButton.onClick.AddListener(ResetChain);

        _initialized = true;
        UpdateWordText();
    }

    public void AppendSyllable(string syllable)
    {
        if (string.IsNullOrEmpty(syllable)) return;
        _syllables.Add(syllable);
        UpdateWordText();
    }

    // WordConfirmButton handler. Returns true when the chain formed a
    // valid dictionary word.
    public bool ConfirmWord()
    {
        string word = CurrentWord;
        if (word.Length == 0) return false;

        WordValidator.Load();
        if (!WordValidator.IsValid(word))
        {
            WordRejected?.Invoke(word);
            return false;
        }

        WordEntry entry = WordValidator.GetEntry(word);
        ClearChain();

        if (_meaningCard != null) _meaningCard.Show(entry);
        WordCompleted?.Invoke(entry);
        return true;
    }

    // WordResetButton handler: throws away the current chain.
    public void ResetChain()
    {
        ClearChain();
    }

    public void ClearChain()
    {
        _syllables.Clear();
        UpdateWordText();
    }

    private void OnWordConfirmPressed()
    {
        ConfirmWord();
    }

    private void UpdateWordText()
    {
        if (_wordText != null) _wordText.text = CurrentWord;
    }

    private static Button FindButton(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }
}
