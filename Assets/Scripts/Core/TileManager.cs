using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    public static TileManager Instance { get; private set; }

    [SerializeField] private Transform _tileContainer;
    [SerializeField] private JamoTile _tilePrefab;
    [SerializeField] private int _traySize = 14;
    [SerializeField] private int _refillThreshold = 6;

    private readonly List<JamoTile> _tiles = new List<JamoTile>();
    private TileDealer _dealer = new TileDealer();
    private bool _initialized;

    public IReadOnlyList<JamoTile> Tiles => _tiles;

    public int ActiveTileCount
    {
        get
        {
            int count = 0;
            foreach (JamoTile tile in _tiles)
                if (!tile.IsConsumed) count++;
            return count;
        }
    }

    void Awake()
    {
        // A second TileManager (duplicate GameController, scene-authored
        // copy) silently steals the singleton; fail loudly instead.
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"TileManager: duplicate instance on '{gameObject.name}' — destroying it. Look for a second GameController in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        Initialize();
    }

    void LateUpdate()
    {
        if (_initialized) RefillIfNeeded();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Allows tests and runtime bootstrap code to wire references
    // without serialized scene fields.
    public void Configure(Transform container, JamoTile prefab, int traySize, int refillThreshold)
    {
        _tileContainer = container;
        _tilePrefab = prefab;
        _traySize = traySize;
        _refillThreshold = refillThreshold;
    }

    public void SetSeed(int seed)
    {
        _dealer = new TileDealer(seed);
    }

    public void Initialize()
    {
        if (_initialized) return;

        // The first DealAll validates against the word list; Load() is
        // idempotent and may already have run in WordBuilder/WordHunt.
        WordValidator.Load();

        if (_tileContainer == null)
        {
            GameObject tray = GameObject.Find("TileTray");
            if (tray != null)
            {
                Transform content = tray.transform.Find("Viewport/Content");
                _tileContainer = content != null ? content : tray.transform;
            }
        }

        if (_tileContainer == null)
        {
            Debug.LogError("TileManager: no tile container assigned and TileTray not found.");
            return;
        }

        _tiles.Clear();
        _tiles.AddRange(_tileContainer.GetComponentsInChildren<JamoTile>(true));

        while (_tiles.Count < _traySize && _tilePrefab != null)
            _tiles.Add(Instantiate(_tilePrefab, _tileContainer));

        if (_tiles.Count == 0)
        {
            Debug.LogError("TileManager: no tiles found in container and no prefab to instantiate.");
            return;
        }

        _initialized = true;

        // Zen wants tiles replaced the instant they're used, rather than
        // waiting for a batch of several to be consumed first. Word Hunt
        // never refills — it deals an exact puzzle set via DealExact.
        if (GameSettings.Mode == GameMode.Zen)
            _refillThreshold = _tiles.Count;

        DealAll();
    }

    // A fresh tray failing to contain any of the ~900 dictionary words
    // is already rare with corpus-weighted deals; a handful of redeals
    // makes it effectively impossible before the injection fallback.
    private const int MaxDealAttempts = 5;

    // Deals a fresh balanced hand to every tile in the tray, guaranteed
    // to have at least one dictionary word buildable (when the word list
    // is loaded — without it the plain balanced deal is used).
    public void DealAll()
    {
        List<string> jamos = DealBuildable();
        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].SetJamo(jamos[i]);
    }

    private List<string> DealBuildable()
    {
        List<string> jamos = _dealer.Deal(_tiles.Count);

        IReadOnlyCollection<WordEntry> entries = WordValidator.AllEntries;
        if (entries.Count == 0) return jamos;

        for (int attempt = 1; attempt < MaxDealAttempts; attempt++)
        {
            if (TrayValidator.AnyWordBuildable(jamos, entries)) return jamos;
            jamos = _dealer.Deal(_tiles.Count);
        }

        if (!TrayValidator.AnyWordBuildable(jamos, entries))
            InjectRandomWord(jamos, entries);
        return jamos;
    }

    // Last-resort guarantee: overwrite random tray positions with the
    // jamo of a randomly chosen dealable word (mirrors what Word Hunt
    // does for its target). Draws only from the dealer's RNG, so seeded
    // deals stay reproducible.
    private void InjectRandomWord(List<string> jamos, IReadOnlyCollection<WordEntry> entries)
    {
        var candidates = new List<List<string>>();
        foreach (WordEntry entry in entries)
        {
            List<string> required = TrayValidator.RequiredJamoFor(entry.word);
            if (required.Count == 0 || required.Count > jamos.Count) continue;
            if (!TrayValidator.AreAllJamoDealable(required)) continue;
            candidates.Add(required);
        }
        if (candidates.Count == 0) return;

        List<string> chosen = candidates[_dealer.NextIndex(candidates.Count)];

        var indices = new List<int>(jamos.Count);
        for (int i = 0; i < jamos.Count; i++) indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = _dealer.NextIndex(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        for (int i = 0; i < chosen.Count; i++)
            jamos[indices[i]] = chosen[i];
    }

    // Deals a fresh hand guaranteed to contain the given jamo (Word Hunt:
    // the target word must be buildable). Required jamo overwrite random
    // tray positions after the normal balanced deal.
    public void DealAllWithRequired(IReadOnlyList<string> requiredJamo)
    {
        DealAll();
        if (requiredJamo == null || requiredJamo.Count == 0 || _tiles.Count == 0) return;

        var indices = new List<int>(_tiles.Count);
        for (int i = 0; i < _tiles.Count; i++) indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = _dealer.NextIndex(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = Mathf.Min(requiredJamo.Count, _tiles.Count);
        for (int i = 0; i < count; i++)
            _tiles[indices[i]].SetJamo(requiredJamo[i]);
    }

    // Word Hunt: deals exactly the given jamo, shuffled, and blanks every
    // remaining tile — the puzzle is sequencing the set, not searching a
    // full tray. Shuffles with UnityEngine.Random rather than the seeded
    // dealer, so these deals are not seed-reproducible.
    public void DealExact(IReadOnlyList<string> requiredJamo)
    {
        if (_tiles.Count == 0) return;

        var jamos = new List<string>();
        if (requiredJamo != null) jamos.AddRange(requiredJamo);

        for (int i = jamos.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (jamos[i], jamos[j]) = (jamos[j], jamos[i]);
        }

        int count = Mathf.Min(jamos.Count, _tiles.Count);
        for (int i = 0; i < count; i++)
            _tiles[i].SetJamo(jamos[i]);
        for (int i = count; i < _tiles.Count; i++)
            _tiles[i].SetEmpty();
    }

    // When fewer than _refillThreshold tiles remain playable,
    // consumed tiles are re-dealt so the tray returns to full.
    // Replacements are dealt against the surviving tiles' composition so
    // the full tray always lands back on the balanced consonant/vowel
    // split instead of drifting vowel-starved over many refills.
    public void RefillIfNeeded()
    {
        // Word Hunt blanks most of the tray, so ActiveTileCount sits below
        // any threshold from the first deal — refilling here would
        // immediately bury the exact puzzle set under random tiles.
        if (GameSettings.Mode == GameMode.WordHunt) return;

        if (ActiveTileCount >= _refillThreshold) return;

        var consumed = new List<JamoTile>();
        int activeConsonants = 0, activeVowels = 0;
        foreach (JamoTile tile in _tiles)
        {
            if (tile.IsConsumed) consumed.Add(tile);
            else if (HangulComposer.IsValidChoseong(tile.Jamo)) activeConsonants++;
            else if (HangulComposer.IsValidJungseong(tile.Jamo)) activeVowels++;
        }
        if (consumed.Count == 0) return;

        List<string> jamos = _dealer.DealRefill(consumed.Count, activeConsonants, activeVowels);
        for (int i = 0; i < consumed.Count; i++)
            consumed[i].SetJamo(jamos[i]);
    }
}
