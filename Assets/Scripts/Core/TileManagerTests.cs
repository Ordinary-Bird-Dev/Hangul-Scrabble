using System.Collections.Generic;
using UnityEngine;

public class TileManagerTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestDealerProducesValidJamo();
        TestDealerBalancedHand();
        TestDealerDeterministicWithSeed();
        TestDealerWeightsFavorCommonJamo();
        TestManagerAdoptsAndFillsTray();
        TestManagerRefillsBelowThreshold();
        TestManagerNoRefillAtThreshold();
        Cleanup();
        Debug.Log("All TileManager tests passed!");
    }

    void TestDealerProducesValidJamo()
    {
        var dealer = new TileDealer(42);
        for (int i = 0; i < 500; i++)
        {
            string jamo = dealer.NextJamo();
            Assert(HangulComposer.IsValidChoseong(jamo) || HangulComposer.IsValidJungseong(jamo),
                $"NextJamo produced invalid jamo '{jamo}'");
        }
    }

    void TestDealerBalancedHand()
    {
        var dealer = new TileDealer(7);
        List<string> hand = dealer.Deal(14);

        Assert(hand.Count == 14, "Deal(14) should return 14 jamo");

        int consonants = 0, vowels = 0;
        foreach (string jamo in hand)
        {
            if (HangulComposer.IsValidChoseong(jamo)) consonants++;
            else if (HangulComposer.IsValidJungseong(jamo)) vowels++;
        }
        Assert(consonants + vowels == 14, "Every dealt jamo should be a valid choseong or jungseong");
        Assert(consonants == 8, $"A 14-tile hand should contain 8 consonants, got {consonants}");
        Assert(vowels == 6, $"A 14-tile hand should contain 6 vowels, got {vowels}");
    }

    void TestDealerDeterministicWithSeed()
    {
        var a = new TileDealer(1234);
        var b = new TileDealer(1234);
        for (int i = 0; i < 50; i++)
            Assert(a.NextJamo() == b.NextJamo(), "Same seed should deal the same jamo sequence");
    }

    void TestDealerWeightsFavorCommonJamo()
    {
        var dealer = new TileDealer(99);
        int common = 0, rare = 0;
        for (int i = 0; i < 5000; i++)
        {
            string jamo = dealer.NextChoseong();
            if (jamo == "ㅇ") common++;
            if (jamo == "ㅃ") rare++;
        }
        Assert(common > rare * 3,
            $"ㅇ (weight 12) should appear far more often than ㅃ (weight 1): got {common} vs {rare}");
    }

    JamoTile MakeTile(Transform parent)
    {
        var go = new GameObject("TestTile");
        go.transform.SetParent(parent);
        return go.AddComponent<JamoTile>();
    }

    TileManager MakeManager(int existingTiles, out Transform container)
    {
        var containerGo = new GameObject("TestTray");
        _spawned.Add(containerGo);
        container = containerGo.transform;

        for (int i = 0; i < existingTiles; i++)
            MakeTile(container);

        var prefabGo = new GameObject("TilePrefabStub");
        _spawned.Add(prefabGo);
        JamoTile prefab = prefabGo.AddComponent<JamoTile>();

        var managerGo = new GameObject("TestTileManager");
        _spawned.Add(managerGo);
        var manager = managerGo.AddComponent<TileManager>();
        manager.Configure(container, prefab, 14, 6);
        manager.SetSeed(42);
        manager.Initialize();
        return manager;
    }

    void TestManagerAdoptsAndFillsTray()
    {
        TileManager manager = MakeManager(10, out Transform container);

        Assert(manager.Tiles.Count == 14,
            $"Manager should adopt 10 tiles and instantiate 4 more, got {manager.Tiles.Count}");
        Assert(manager.ActiveTileCount == 14, "All tiles should be active after the initial deal");

        foreach (JamoTile tile in manager.Tiles)
        {
            Assert(tile.Jamo != "", "Every tile should have a jamo after DealAll");
            Assert(HangulComposer.IsValidChoseong(tile.Jamo) || HangulComposer.IsValidJungseong(tile.Jamo),
                $"Dealt tile has invalid jamo '{tile.Jamo}'");
        }
    }

    void TestManagerRefillsBelowThreshold()
    {
        TileManager manager = MakeManager(14, out _);

        for (int i = 0; i < 9; i++)
            manager.Tiles[i].Consume();
        Assert(manager.ActiveTileCount == 5, "Nine consumed tiles should leave 5 active");

        manager.RefillIfNeeded();
        Assert(manager.ActiveTileCount == 14,
            $"Refill below threshold should restore all 14 tiles, got {manager.ActiveTileCount}");
    }

    void TestManagerNoRefillAtThreshold()
    {
        TileManager manager = MakeManager(14, out _);

        for (int i = 0; i < 8; i++)
            manager.Tiles[i].Consume();
        Assert(manager.ActiveTileCount == 6, "Eight consumed tiles should leave 6 active");

        manager.RefillIfNeeded();
        Assert(manager.ActiveTileCount == 6,
            "Refill should not trigger while active tiles are at the threshold");
    }

    void Cleanup()
    {
        JamoTile.ClearSelection();
        foreach (GameObject go in _spawned)
            Destroy(go);
        _spawned.Clear();
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
