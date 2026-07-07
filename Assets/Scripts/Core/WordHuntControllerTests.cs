using System.Collections.Generic;
using UnityEngine;

public class WordHuntControllerTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestRequiredJamoFor();
        TestPickTargetFiltersAndIsSeeded();
        TestHintPenaltyAndScoreFloor();
        TestDealWithRequiredGuaranteesJamo();
        TestTargetAdvancesOnMatch();
        Cleanup();
        Debug.Log("All WordHuntController tests passed!");
    }

    void TestRequiredJamoFor()
    {
        List<string> jamo = WordHuntController.RequiredJamoFor("학교");
        Assert(jamo.Count == 5, $"학교 should need 5 jamo (ㅎㅏㄱ + ㄱㅛ), got {jamo.Count}");
        Assert(jamo[0] == "ㅎ" && jamo[1] == "ㅏ" && jamo[2] == "ㄱ", "학 should decompose to ㅎㅏㄱ");
        Assert(jamo[3] == "ㄱ" && jamo[4] == "ㅛ", "교 should decompose to ㄱㅛ with no jongseong");

        Assert(WordHuntController.RequiredJamoFor("").Count == 0, "Empty word needs no jamo");
    }

    void TestPickTargetFiltersAndIsSeeded()
    {
        var entries = new List<WordEntry>
        {
            new WordEntry { word = "물", english = "water", syllable_count = 1 },
            new WordEntry { word = "학교", english = "school", syllable_count = 2 },
            new WordEntry { word = "바나나", english = "banana", syllable_count = 3 },
            new WordEntry { word = "무엇인가", english = "something", syllable_count = 4 },
            new WordEntry { word = "친구", english = "", syllable_count = 2 },
        };

        for (int i = 0; i < 20; i++)
        {
            WordEntry pick = WordHuntController.PickTarget(entries, new System.Random(i));
            Assert(pick != null && (pick.word == "학교" || pick.word == "바나나"),
                $"Only 2-3 syllable words with meanings qualify, got {pick?.word}");
        }

        WordEntry a = WordHuntController.PickTarget(entries, new System.Random(7));
        WordEntry b = WordHuntController.PickTarget(entries, new System.Random(7));
        Assert(a.word == b.word, "Same seed should pick the same target");

        Assert(WordHuntController.PickTarget(new List<WordEntry>(), new System.Random(1)) == null,
            "No candidates should yield a null target");
    }

    void TestHintPenaltyAndScoreFloor()
    {
        var go = new GameObject("TestGameManagerHunt");
        _spawned.Add(go);
        var manager = go.AddComponent<GameManager>();
        manager.Configure(loadResultSceneOnEnd: false);
        manager.StartRound();

        manager.AddPoints(-WordHuntController.HintPenalty);
        Assert(manager.Score == 0, "Score should never go below zero from a hint");

        manager.RegisterWord("학교", 10f); // 150 points
        manager.AddPoints(-WordHuntController.HintPenalty);
        Assert(manager.Score == 100, $"150 - 50 hint should leave 100, got {manager.Score}");
    }

    void TestDealWithRequiredGuaranteesJamo()
    {
        var containerGo = new GameObject("TestHuntTray");
        _spawned.Add(containerGo);
        for (int i = 0; i < 14; i++)
        {
            var tileGo = new GameObject($"Tile{i}");
            tileGo.transform.SetParent(containerGo.transform);
            tileGo.AddComponent<JamoTile>();
        }

        var managerGo = new GameObject("TestHuntTileManager");
        _spawned.Add(managerGo);
        var manager = managerGo.AddComponent<TileManager>();
        manager.Configure(containerGo.transform, null, 14, 6);
        manager.SetSeed(11);
        manager.Initialize();

        List<string> required = WordHuntController.RequiredJamoFor("학교");
        manager.DealAllWithRequired(required);

        var trayCounts = new Dictionary<string, int>();
        foreach (JamoTile tile in manager.Tiles)
        {
            trayCounts.TryGetValue(tile.Jamo, out int n);
            trayCounts[tile.Jamo] = n + 1;
        }

        var requiredCounts = new Dictionary<string, int>();
        foreach (string jamo in required)
        {
            requiredCounts.TryGetValue(jamo, out int n);
            requiredCounts[jamo] = n + 1;
        }

        foreach (KeyValuePair<string, int> pair in requiredCounts)
        {
            trayCounts.TryGetValue(pair.Key, out int have);
            Assert(have >= pair.Value,
                $"Tray must contain at least {pair.Value}x {pair.Key} for 학교, got {have}");
        }
    }

    void TestTargetAdvancesOnMatch()
    {
        WordValidator.Load();

        var go = new GameObject("TestHuntController");
        _spawned.Add(go);
        var hunt = go.AddComponent<WordHuntController>();
        hunt.SetSeed(3);
        hunt.Initialize();

        Assert(hunt.Target != null, "Word Hunt should pick a target from the dictionary");
        Assert(hunt.Target.syllable_count >= 2 && hunt.Target.syllable_count <= 3,
            "Target should be a 2-3 syllable word");

        WordEntry oldTarget = hunt.Target;

        hunt.HandleWordCompleted(new WordEntry { word = "다른단어" });
        Assert(hunt.RoundsCompleted == 0, "A non-matching word should not advance the hunt");
        Assert(hunt.Target == oldTarget, "Target should stay the same after a non-match");

        hunt.UseHint();
        Assert(hunt.HintUsed, "UseHint should mark the hint as used");

        hunt.HandleWordCompleted(oldTarget);
        Assert(hunt.RoundsCompleted == 1, "Matching the target should complete the round");
        Assert(!hunt.HintUsed, "Hint should reset for the next target");
        Assert(hunt.Target != null, "A new target should be picked after a match");
    }

    void Cleanup()
    {
        foreach (GameObject go in _spawned)
            if (go != null) Destroy(go);
        _spawned.Clear();
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
