using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Classic Mode round controller: 3-minute countdown in the TopBar,
// scoring with combo multiplier, and transition to ResultScene.
public class GameManager : MonoBehaviour
{
    public const float RoundSeconds = 180f;

    public static GameManager Instance { get; private set; }

    // Carried across the scene load so ResultScene can display them.
    public static int LastFinalScore { get; private set; }
    public static int LastWordsCompleted { get; private set; }
    public static WordEntry LastWordEntry { get; private set; }

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
    private bool _loadResultSceneOnEnd = true;
    private float _lastWordTime = float.NegativeInfinity;
    private bool _initialized;

    public int Score { get; private set; }
    public int WordsCompleted { get; private set; }
    public float TimeRemaining { get; private set; }
    public bool RoundActive { get; private set; }
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

        LastWordEntry = null;
        _lastWordTime = float.NegativeInfinity;
        RoundActive = true;
        UpdateScoreUI();
        UpdateTimerUI();
    }

    public void Tick(float deltaSeconds)
    {
        if (!RoundActive) return;

        // Zen Mode has no countdown: play continues until the player leaves.
        if (Mode == GameMode.Zen)
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
        return points;
    }

    // Direct score adjustment (e.g. Word Hunt hint penalty). Never below 0.
    public void AddPoints(int delta)
    {
        Score = Mathf.Max(0, Score + delta);
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
        LastWordEntry = entry;
        RegisterWord(entry.word, Time.time);
    }

    // Finds or creates the timer and score labels in the TopBar, and
    // hooks the ProgressBar (scrollbar or slider) as the time gauge.
    private void ResolveTopBarUI()
    {
        GameObject topBar = GameObject.Find("TopBar");
        if (topBar == null)
            Debug.LogWarning("GameManager: TopBar not found (or inactive) — timer/score labels cannot be created if the scene lacks them.");

        GameObject progressGo = GameObject.Find("ProgressBar");
        if (progressGo != null)
        {
            _progressScrollbar = progressGo.GetComponentInChildren<Scrollbar>(true);
            _progressSlider = progressGo.GetComponentInChildren<Slider>(true);
        }
        else
        {
            Debug.LogWarning("GameManager: ProgressBar not found (or inactive) — the time gauge is disabled.");
        }

        if (_timerText == null)
            _timerText = FindOrCreateLabel(topBar, "TimerText", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f));
        if (_scoreText == null)
            _scoreText = FindOrCreateLabel(topBar, "ScoreText", new Vector2(0.5f, 0.5f), new Vector2(0f, -110f));
    }

    private static TMP_Text FindOrCreateLabel(GameObject topBar, string name, Vector2 anchor, Vector2 position)
    {
        GameObject existing = GameObject.Find(name);
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

        float fraction = Mode == GameMode.Zen ? 1f : Mathf.Clamp01(TimeRemaining / RoundSeconds);
        if (_progressScrollbar != null) _progressScrollbar.size = fraction;
        if (_progressSlider != null) _progressSlider.value = fraction;
    }

    private void UpdateScoreUI()
    {
        if (_scoreText != null)
            _scoreText.text = Score.ToString("N0");
    }
}
