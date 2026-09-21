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

    // Optional extra gate a mode can install on top of the dictionary
    // check. Null (Zen, Word Hunt) means every valid word is accepted;
    // Classic sets it so only the clue's answer counts. See ConfirmWord
    // for why the gate has to run here rather than in a WordCompleted
    // subscriber.
    public System.Func<WordEntry, bool> AcceptWord { get; set; }

    [SerializeField] private SyllableBuilderUI _syllableBuilder;
    [SerializeField] private TMP_Text _wordText;
    [SerializeField] private Button _wordConfirmButton;
    [SerializeField] private Button _wordResetButton;
    [SerializeField] private MeaningCardUI _meaningCard;

    private readonly List<string> _syllables = new List<string>();
    private bool _initialized;
    private Coroutine _resetPunchRoutine;
    private Color _wordTextColor = Color.white;

    // True while the bar is showing a word the dictionary refused. Stays
    // true until the chain changes, so the warning cannot go stale.
    private bool _rejectedWordShowing;

    // Lets a mode ask whether the bar is currently holding a refused word.
    public bool RejectedWordShowing => _rejectedWordShowing;
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

        // Name-based lookups fail loudly: GameObject.Find cannot see
        // inactive objects, so a deactivated button silently unwires — an
        // inactive WordConfirmButton once masked a dead confirm for weeks.
        if (_wordText == null)
        {
            GameObject wordTextGo = GameObject.Find("WordText");
            if (wordTextGo != null) _wordText = wordTextGo.GetComponent<TMP_Text>();
        }
        if (_wordText == null)
            Debug.LogWarning("WordBuilder: WordText not found (or inactive) — the word bar display is disabled.");

        if (_wordText != null) _wordTextColor = _wordText.color;

        if (_wordConfirmButton == null) _wordConfirmButton = FindButton("WordConfirmButton");
        if (_wordConfirmButton != null)
            _wordConfirmButton.onClick.AddListener(OnWordConfirmPressed);
        else
            Debug.LogWarning("WordBuilder: WordConfirmButton not found (or inactive) — word confirm is disabled.");

        if (_wordResetButton == null) _wordResetButton = FindButton("WordResetButton");
        if (_wordResetButton != null)
            _wordResetButton.onClick.AddListener(ResetChain);
        else
            Debug.LogWarning("WordBuilder: WordResetButton not found (or inactive) — word reset is disabled.");

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
        if (mascot != null)
            _mascotAnimator = mascot.GetComponent<Animator>();
        else
            Debug.LogWarning("WordBuilder: MascotImage not found (or inactive) — celebrate/wrong animations are disabled.");

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
            return Reject(word);

        WordEntry entry = WordValidator.GetEntry(word);

        // A guided mode's gate runs BEFORE WordCompleted fires, not in a
        // subscriber. GameManager scores off that event, and it cannot
        // re-check the target afterwards: ClassicModeController advances to
        // the next target inside its own handler, and the two subscribe in
        // Start order that is not guaranteed — so a post-hoc check would
        // compare the answer against whichever target happened to be
        // current and could reject a correct word.
        if (AcceptWord != null && !AcceptWord(entry))
            return Reject(word);

        PlayWordBurst(word);
        AudioManager.TryPlayWordSuccess();

        // Read the word back in every mode. This is the one success path
        // the whole game funnels through — Classic, Word Hunt and Zen all
        // land here — so the read-back belongs here rather than in any one
        // mode's handler. Delayed so the success chime above has room to
        // finish first, and given `word` by value because ClearChain and
        // the guided modes both move on while this is still waiting.
        StartCoroutine(SpeakWordAfterChime(word));

        ClearChain();

        // Fires alongside _meaningCard.Show, whose Shown event swaps the
        // mascot to the reading sprite in the same call stack.
        if (_mascotAnimator != null && _mascotAnimator.runtimeAnimatorController != null)
            _mascotAnimator.SetTrigger(MascotCelebrateTrigger);

        if (_meaningCard != null) _meaningCard.Show(entry);
        WordCompleted?.Invoke(entry);
        return true;
    }

    // Long enough for the success chime to clear, short enough that the
    // spoken word still reads as part of the same beat.
    private const float SuccessSpeechDelay = 0.45f;

    private System.Collections.IEnumerator SpeakWordAfterChime(string word)
    {
        yield return new WaitForSeconds(SuccessSpeechDelay);
        SpeechManager.Speak(word);
    }

    // The single rejection path: wrong dictionary word, or a real word a
    // guided mode did not ask for. Both look identical to the player.
    private bool Reject(string word)
    {
        MarkWordRejected();
        AudioManager.TryPlayWordError();
        if (_mascotAnimator != null && _mascotAnimator.runtimeAnimatorController != null)
            _mascotAnimator.SetTrigger(MascotWrongTrigger);
        WordRejected?.Invoke(word);
        return false;
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
    //
    // The sound lives here and NOT in ClearChain, which is also called on a
    // successful word and by ClassicModeController's re-deal after a wrong
    // answer. Both of those already have their own sound, and adding one to
    // ClearChain would double up on every success.
    public void ResetChain()
    {
        AudioManager.TryPlayReset();
        ClearChain();
    }

    public void ClearChain()
    {
        _syllables.Clear();
        UpdateWordText();
    }

    public void OnWordConfirmPressed()
    {
        ConfirmWord();
    }

    // Every change to the chain clears the rejected mark, whoever made it:
    // the player appending a syllable, the player pressing Reset, or
    // Classic's ResetPuzzle. That is the whole state machine — there is no
    // path that alters the word and leaves a stale warning behind.
    private void UpdateWordText()
    {
        _rejectedWordShowing = false;
        if (_wordText != null) _wordText.text = CurrentWord;
        ApplyWordColor();
    }

    // Marks the chain as refused and LEAVES it marked.
    //
    // The old version was a 0.4s red blink that then restored the normal
    // colour, which was the wrong signal for Zen and Word Hunt: those modes
    // keep the chain after a rejection (deliberately — see WordBuilderTests
    // and the fact that their tiles are already consumed and would be lost),
    // so the bar went back to looking perfectly normal while holding a word
    // the game had already refused. Confirming another syllable then
    // appended to it, and 교학 quietly became 교학교.
    //
    // Classic is unaffected in practice: it clears the chain inside the same
    // WordRejected call, so UpdateWordText unmarks it immediately and the
    // player keeps getting the sound, the mascot and the re-deal.
    private void MarkWordRejected()
    {
        _rejectedWordShowing = true;
        ApplyWordColor();
        PunchResetButton();
    }

    private void ApplyWordColor()
    {
        if (_wordText == null) return;
        _wordText.color = _rejectedWordShowing ? Palette.Reject : _wordTextColor;
    }

    // Draws the eye to the way out. The word bar says "this is wrong"; this
    // says "here is what to press".
    private void PunchResetButton()
    {
        if (_wordResetButton == null || !isActiveAndEnabled) return;

        Transform target = _wordResetButton.transform;
        target.localScale = Vector3.one;
        if (_resetPunchRoutine != null) StopCoroutine(_resetPunchRoutine);
        _resetPunchRoutine = StartCoroutine(UITween.PunchScale(target, 0.18f, 0.28f));
    }

    private static Button FindButton(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }
}
