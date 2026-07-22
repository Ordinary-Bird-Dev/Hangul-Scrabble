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
    private Coroutine _flashRoutine;
    private Color _wordTextColor = Color.white;
    private Animator _mascotAnimator;

    private const string MascotCelebrateTrigger = "Celebrate";
    private const string MascotWrongTrigger = "Wrong";

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

        if (_wordText != null) _wordTextColor = _wordText.color;

        if (_wordConfirmButton == null) _wordConfirmButton = FindButton("WordConfirmButton");
        if (_wordConfirmButton != null) _wordConfirmButton.onClick.AddListener(OnWordConfirmPressed);

        if (_wordResetButton == null) _wordResetButton = FindButton("WordResetButton");
        if (_wordResetButton != null) _wordResetButton.onClick.AddListener(ResetChain);

        if (_meaningCard == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
                _meaningCard = MeaningCardUI.CreateRuntime(canvas.transform,
                    donor != null ? donor.font : null);
            }
        }

        GameObject mascot = GameObject.Find("MascotImage");
        if (mascot != null) _mascotAnimator = mascot.GetComponent<Animator>();

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
            FlashRejection();
            AudioManager.TryPlayWordError();
            if (_mascotAnimator != null && _mascotAnimator.runtimeAnimatorController != null)
                _mascotAnimator.SetTrigger(MascotWrongTrigger);
            WordRejected?.Invoke(word);
            return false;
        }

        WordEntry entry = WordValidator.GetEntry(word);
        PlayWordBurst(word);
        AudioManager.TryPlayWordSuccess();
        ClearChain();

        // Fires alongside _meaningCard.Show, whose Shown event swaps the
        // mascot to the reading sprite in the same call stack.
        if (_mascotAnimator != null && _mascotAnimator.runtimeAnimatorController != null)
            _mascotAnimator.SetTrigger(MascotCelebrateTrigger);

        if (_meaningCard != null) _meaningCard.Show(entry);
        WordCompleted?.Invoke(entry);
        return true;
    }

    // Success feedback: a ghost copy of the completed word scales out
    // and fades away while the real word bar clears for the next word.
    private void PlayWordBurst(string word)
    {
        if (_wordText == null || !isActiveAndEnabled) return;

        GameObject ghost = Instantiate(_wordText.gameObject, _wordText.transform.parent);
        ghost.name = "WordBurstGhost";

        TMP_Text ghostText = ghost.GetComponent<TMP_Text>();
        if (ghostText != null)
        {
            ghostText.text = word;
            ghostText.raycastTarget = false;
        }

        StartCoroutine(UITween.BurstAndDestroy(ghost, 1.8f, 0.45f));
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

    private void ResetWord()
    {
        _syllables.Clear();
        UpdateWordText();
    }

    private void UpdateWordText()
    {
        if (_wordText != null) _wordText.text = CurrentWord;
    }

    private void FlashRejection()
    {
        if (_wordText == null || !isActiveAndEnabled) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        _wordText.color = new Color(0.9f, 0.25f, 0.25f);
        yield return new WaitForSeconds(0.4f);
        _wordText.color = _wordTextColor;
        _flashRoutine = null;
    }

    private static Button FindButton(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }
}
