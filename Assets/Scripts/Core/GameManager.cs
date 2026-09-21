using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Round controller for the timed modes (Classic and Word Hunt):
// 3-minute countdown in the TopBar, scoring with the combo multiplier,
// and transition to ResultScene. Zen runs without a countdown.
public class GameManager : MonoBehaviour
{
    public const float RoundSeconds = 180f;

    // Words that fill the TopBar gauge end to end. Purely a presentation
    // target: reaching it does not end the round or change scoring, it just
    // means the bar is full and the player can keep going. Tune it against
    // real play — if the bar caps out at the one-minute mark it is too low,
    // and if it never leaves the left edge it is too high.
    public const int WordsTarget = 10;

    // How far into the red a round may go. Penalties (currently only
    // Classic's hint) stop biting at this floor, and whoever charges them
    // is expected to stop offering the purchase once Score reaches it —
    // otherwise the penalty silently becomes free, which is exactly what
    // a clamp at 0 used to do. Score resets to 0 each round, so this
    // doubles as a per-round hint budget of two hints.
    public const int MinScore = -100;

    // False once the round has spent its full penalty budget.
    public bool CanAffordPenalty => Score > MinScore;

    public static GameManager Instance { get; private set; }

    // Carried across the scene load so ResultScene can display them.
    public static int LastFinalScore { get; private set; }
    public static int LastWordsCompleted { get; private set; }

    public static float? SavedTimeRemaining { get; private set; }
    public static int? SavedScore { get; private set; }
    public static int? SavedWordsCompleted { get; private set; }
    public static GameMode? SavedMode { get; private set; }

    public void SaveSessionState()
    {
        if (!RoundActive) return;

        SavedTimeRemaining = TimeRemaining;
        SavedScore = Score;
        SavedWordsCompleted = WordsCompleted;
        SavedMode = Mode;
    }

    public static void ClearSavedSession()
    {
        SavedTimeRemaining = null;
        SavedScore = null;
        SavedWordsCompleted = null;
        SavedMode = null;
    }

    public event System.Action<int> RoundEnded;

    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private TMP_Text _scoreText;

    private WordBuilder _wordBuilder;
    private Scrollbar _progressScrollbar;
    private Slider _progressSlider;
    private Image _progressFill;
    private bool _loadResultSceneOnEnd = true;
    private float _lastWordTime = float.NegativeInfinity;
    private bool _initialized;

    public int Score { get; private set; }
    public int WordsCompleted { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool RoundActive { get; private set; }

    // Set false by SceneBootstrap for the tutorial. Scoring and word
    // completion still work; only the clock stops. Defaults true so every
    // ordinary round is unaffected.
    public bool CountdownEnabled { get; set; } = true;
    public GameMode Mode { get; private set; } = GameMode.Classic;

    void Awake()
    {
        // A duplicate GameManager silently steals the singleton and runs a
        // second round timer; fail loudly instead.
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"GameManager: duplicate instance on '{gameObject.name}' — destroying it. Look for a second GameController in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        Initialize();
        StartRound();
    }

    void Update()
    {
        Tick(Time.deltaTime);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_wordBuilder != null)
            _wordBuilder.WordCompleted -= OnWordCompleted;
    }

    // Tests disable the scene transition so EndRound can run headless.
    public void Configure(bool loadResultSceneOnEnd)
    {
        _loadResultSceneOnEnd = loadResultSceneOnEnd;
    }

    public void Initialize()
    {
        if (_initialized) return;

        _wordBuilder = FindAnyObjectByType<WordBuilder>();
        if (_wordBuilder != null)
            _wordBuilder.WordCompleted += OnWordCompleted;

        ResolveTopBarUI();
        _initialized = true;
    }

    public void StartRound()
    {
        Mode = GameSettings.Mode;

        if (SavedTimeRemaining.HasValue && SavedMode.HasValue && SavedMode.Value == Mode)
        {
            Score = SavedScore ?? 0;
            WordsCompleted = SavedWordsCompleted ?? 0;
            TimeRemaining = SavedTimeRemaining.Value;
        }
        else
        {
            Score = 0;
            WordsCompleted = 0;
            TimeRemaining = RoundSeconds;
        }

        ClearSavedSession();

        _lastWordTime = float.NegativeInfinity;
        RoundActive = true;
        UpdateScoreUI();
        UpdateTimerUI();
        UpdateProgressUI();
    }

    public void Tick(float deltaSeconds)
    {
        if (!RoundActive) return;

        // Zen Mode has no countdown: play continues until the player leaves.
        // Neither does the tutorial, which sets CountdownEnabled false —
        // without it a lesson would be cut off mid-word at the 3:00 mark and
        // dumped into ResultScene.
        if (Mode == GameMode.Zen || !CountdownEnabled)
        {
            UpdateTimerUI();
            return;
        }

        TimeRemaining -= deltaSeconds;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            UpdateTimerUI();
            EndRound();
            return;
        }
        UpdateTimerUI();
    }

    // Awards points for a completed word at the given time and returns
    // the points granted. Split out from the event handler so combo
    // timing is unit-testable with injected clocks.
    public int RegisterWord(string word, float now)
    {
        if (!RoundActive || string.IsNullOrEmpty(word)) return 0;

        int points = ScoreCalculator.WordScore(word);
        if (ScoreCalculator.IsCombo(_lastWordTime, now))
            points = ScoreCalculator.ApplyCombo(points);

        _lastWordTime = now;
        Score += points;
        WordsCompleted++;
        UpdateScoreUI();
        UpdateProgressUI();
        return points;
    }

    // Direct score adjustment (e.g. Classic's hint penalty). Never below
    // MinScore. A caller that keeps spending past the floor gets charged
    // nothing, so callers must gate on CanAffordPenalty.
    public void AddPoints(int delta)
    {
        Score = Mathf.Max(MinScore, Score + delta);
        UpdateScoreUI();
    }

    public void EndRound()
    {
        if (!RoundActive) return;

        RoundActive = false;
        LastFinalScore = Score;
        LastWordsCompleted = WordsCompleted;
        RoundEnded?.Invoke(Score);

        if (_loadResultSceneOnEnd)
            SceneManager.LoadScene("ResultScene");
    }

    private void OnWordCompleted(WordEntry entry)
    {
        RegisterWord(entry.word, Time.time);
    }

    // Finds or creates the timer and score labels in the TopBar, and
    // hooks the ProgressBar (scrollbar or slider) as the time gauge.
    private void ResolveTopBarUI()
    {
        // Every lookup here is inactive-inclusive, and that is load-bearing,
        // not defensive. SceneBootstrap switches TimerText, ScoreText and
        // ProgressBar OFF for the tutorial during sceneLoaded, which runs
        // before this does. With GameObject.Find these would all read as
        // missing: the gauge would log a bogus warning, and FindOrCreateLabel
        // would build duplicate runtime labels on top of the hidden authored
        // ones — visible, in the very mode that hid them.
        GameObject topBar = FindInSceneIncludingInactive("TopBar");
        if (topBar == null)
            Debug.LogWarning("GameManager: TopBar not found — timer/score labels cannot be created if the scene lacks them.");

        GameObject progressGo = FindInSceneIncludingInactive("ProgressBar");
        if (progressGo != null)
        {
            _progressScrollbar = progressGo.GetComponentInChildren<Scrollbar>(true);
            _progressSlider = progressGo.GetComponentInChildren<Slider>(true);

            // A Slider or Scrollbar is overkill for a gauge nobody drags. The
            // simplest rig is a track Image with one Filled child called
            // "Fill", so accept that too. Looked up by name rather than by
            // GetComponentInChildren<Image>, which would return the track
            // itself and silently do nothing visible.
            if (_progressScrollbar == null && _progressSlider == null)
            {
                Transform fill = progressGo.transform.Find("Fill");
                _progressFill = fill != null ? fill.GetComponent<Image>() : null;

                if (_progressFill == null)
                    Debug.LogWarning("GameManager: ProgressBar has no Scrollbar, Slider, or 'Fill' Image child — the time gauge will sit still.");
                else if (_progressFill.type != Image.Type.Filled)
                    Debug.LogWarning("GameManager: ProgressBar/Fill is not an Image of type Filled — fillAmount has no effect, so the gauge will sit still.");
            }
        }
        else
        {
            Debug.LogWarning("GameManager: ProgressBar not found — the time gauge is disabled.");
        }

        if (_timerText == null)
            _timerText = FindOrCreateLabel(topBar, "TimerText", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f));
        if (_scoreText == null)
            _scoreText = FindOrCreateLabel(topBar, "ScoreText", new Vector2(0.5f, 0.5f), new Vector2(0f, -110f));
    }

    // Searches every transform in the active scene, switched on or off.
    // GameObject.Find skips inactive objects, which is the single most
    // repeated source of dead wiring in this project.
    private static GameObject FindInSceneIncludingInactive(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    private static TMP_Text FindOrCreateLabel(GameObject topBar, string name, Vector2 anchor, Vector2 position)
    {
        // Inactive-inclusive: a hidden authored label must be REUSED, not
        // duplicated. Returning an inactive label is fine — writing .text to
        // it is legal and simply renders nothing until it is switched on.
        GameObject existing = FindInSceneIncludingInactive(name);
        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null) return existingText;
        }

        if (topBar == null) return null;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(topBar.transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(500f, 70f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 48f;

        // Reuse the Korean font from any existing TMP label in the scene.
        TMP_Text donor = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (donor != null && donor != text && donor.font != null)
            text.font = donor.font;

        return text;
    }

    private void UpdateTimerUI()
    {
        if (_timerText != null)
        {
            if (Mode == GameMode.Zen)
            {
                _timerText.text = "∞";
            }
            else
            {
                int total = Mathf.CeilToInt(TimeRemaining);
                _timerText.text = $"{total / 60}:{total % 60:00}";
            }
        }
    }

    // The TopBar gauge fills toward WordsTarget. It deliberately does NOT
    // track the countdown: the timer already reads out as "2:07" directly
    // above it, and a second thing draining in sync says nothing new. It
    // also does not track Score, which has a floor (MinScore) but no
    // ceiling, so there is no honest denominator to divide by.
    //
    // Every mode counts words, Zen included, so there is no special case.
    private void UpdateProgressUI()
    {
        float fraction = WordsTarget > 0
            ? Mathf.Clamp01((float)WordsCompleted / WordsTarget)
            : 0f;

        if (_progressScrollbar != null) _progressScrollbar.size = fraction;
        if (_progressSlider != null) _progressSlider.value = fraction;
        if (_progressFill != null) _progressFill.fillAmount = fraction;
    }

    private void UpdateScoreUI()
    {
        if (_scoreText != null)
            _scoreText.text = Score.ToString("N0");
    }
}
