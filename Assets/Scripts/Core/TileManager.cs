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
        DealAll();
    }

    // Deals a fresh balanced hand to every tile in the tray.
    public void DealAll()
    {
        List<string> jamos = _dealer.Deal(_tiles.Count);
        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].SetJamo(jamos[i]);
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
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = Mathf.Min(requiredJamo.Count, _tiles.Count);
        for (int i = 0; i < count; i++)
            _tiles[indices[i]].SetJamo(requiredJamo[i]);
    }

    // When fewer than _refillThreshold tiles remain playable,
    // consumed tiles are re-dealt so the tray returns to full.
    public void RefillIfNeeded()
    {
        if (ActiveTileCount >= _refillThreshold) return;

        foreach (JamoTile tile in _tiles)
            if (tile.IsConsumed)
                tile.SetJamo(_dealer.NextJamo());
    }
}
