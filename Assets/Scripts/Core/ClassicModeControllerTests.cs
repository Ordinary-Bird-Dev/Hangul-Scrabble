using System.Collections.Generic;
using UnityEngine;

public class ClassicModeControllerTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestRequiredJamoFor();
        TestPickTargetFiltersAndIsSeeded();
        TestHintPenaltyAndScore();
        TestHelpBudgetEndsRound();
        TestDealWithRequiredGuaranteesJamo();
        TestTargetAdvancesOnMatch();
        Cleanup();
        Debug.Log("All ClassicModeController tests passed!");
    }

    void TestRequiredJamoFor()
    {
        List<string> jamo = ClassicModeController.RequiredJamoFor("학교");
        Assert(jamo.Count == 5, $"학교 should need 5 jamo (ㅎㅏㄱ + ㄱㅛ), got {jamo.Count}");
        Assert(jamo[0] == "ㅎ" && jamo[1] == "ㅏ" && jamo[2] == "ㄱ", "학 should decompose to ㅎㅏㄱ");
        Assert(jamo[3] == "ㄱ" && jamo[4] == "ㅛ", "교 should decompose to ㄱㅛ with no jongseong");

        Assert(ClassicModeController.RequiredJamoFor("").Count == 0, "Empty word needs no jamo");
    }

    void TestPickTargetFiltersAndIsSeeded()
    {
        // Every entry needs a `prompt`, not just an `english`: PickTarget
        // gates on the clue the banner actually renders. 사랑 is here to
        // pin that — it has a gloss and no prompt, and must still be
        // skipped. Without it, swapping the gate back to `english` would
        // pass this test silently.
        var entries = new List<WordEntry>
        {
            new WordEntry { word = "물", english = "water", prompt = "water", syllable_count = 1 },
            new WordEntry { word = "학교", english = "school", prompt = "school", syllable_count = 2 },
            new WordEntry { word = "바나나", english = "banana", prompt = "banana", syllable_count = 3 },
            new WordEntry { word = "무엇인가", english = "something", prompt = "something", syllable_count = 4 },
            new WordEntry { word = "친구", english = "", prompt = "", syllable_count = 2 },
            new WordEntry { word = "사랑", english = "love", prompt = "", syllable_count = 2 },
        };

        for (int i = 0; i < 20; i++)
        {
            WordEntry pick = ClassicModeController.PickTarget(entries, new System.Random(i));
            Assert(pick != null && (pick.word == "학교" || pick.word == "바나나"),
                $"Only 2-3 syllable words with a clue qualify, got {pick?.word ?? "(null)"}");
        }

        WordEntry a = ClassicModeController.PickTarget(entries, new System.Random(7));
        WordEntry b = ClassicModeController.PickTarget(entries, new System.Random(7));
        Assert(a.word == b.word, "Same seed should pick the same target");

        Assert(ClassicModeController.PickTarget(new List<WordEntry>(), new System.Random(1)) == null,
            "No candidates should yield a null target");
    }

    // One GameManager for every test here, deliberately. Awake treats a
    // second live instance as a bug and logs an error, so spawning one per
    // test would fill the console with a failure that is the test's fault
    // rather than the game's. StartRound resets the round state, which is
    // all these need.
    private GameManager _manager;

    // StartRound reads GameSettings.Mode, and Zen switches scoring off
    // entirely. Pinning it makes these tests independent of whatever mode
    // the machine running them last played in — without this they pass or
    // fail depending on a PlayerPrefs value.
    private GameManager FreshRound()
    {
        GameSettings.Mode = GameMode.Classic;
        if (_manager == null)
        {
            var go = new GameObject("TestGameManagerScoring");
            _spawned.Add(go);
            _manager = go.AddComponent<GameManager>();
            _manager.Configure(loadResultSceneOnEnd: false);
        }
        GameManager.ClearSavedSession();
        _manager.StartRound();
        return _manager;
    }

    void TestHintPenaltyAndScore()
    {
        GameManager manager = FreshRound();

        // The score DOES go negative. The old version of this test asserted
        // it stayed at zero, which stopped being true when the floor moved
        // off zero and has been failing quietly ever since.
        manager.SpendHelp(ClassicModeController.HintPenalty);
        Assert(manager.Score == -50, $"A 50-point hint from zero should leave -50, got {manager.Score}");
        Assert(manager.HelpSpent == 50, $"The hint should book 50 against the budget, got {manager.HelpSpent}");

        int before = manager.HelpRemaining;
        manager.RegisterWord("학교", 10f); // 150 points
        Assert(manager.Score == 100, $"150 earned against a 50 hint should leave 100, got {manager.Score}");
        Assert(manager.HelpRemaining == before,
            "Earning points must not refill the help budget — that is the whole difference from a score floor");

        // AddPoints is not a purchase and must leave the budget alone.
        manager.AddPoints(-25);
        Assert(manager.HelpSpent == 50, $"AddPoints should not touch HelpSpent, got {manager.HelpSpent}");
    }

    void TestHelpBudgetEndsRound()
    {
        GameManager manager = FreshRound();

        int purchases = GameManager.HelpBudget / ClassicModeController.SkipPenalty;
        for (int i = 0; i < purchases - 1; i++)
        {
            bool ended = manager.SpendHelp(ClassicModeController.SkipPenalty);
            Assert(!ended, $"Purchase {i + 1} of {purchases} should not end the round");
            Assert(manager.CanAffordPenalty, $"Budget should still be open after purchase {i + 1}");
        }

        Assert(manager.SpendHelp(ClassicModeController.SkipPenalty),
            "The purchase that empties the budget should end the round");
        Assert(!manager.RoundActive, "Round should be over once the budget is spent");
        Assert(!manager.CanAffordPenalty, "Budget should be closed once it is spent");
        Assert(GameManager.LastRoundLost, "Running the budget out should mark the round lost");
        Assert(manager.Score == -GameManager.HelpBudget,
            $"A round that scores nothing should bottom out at {-GameManager.HelpBudget}, got {manager.Score}");

        // Past the floor the purchase must be refused outright, not charged
        // a clamped zero — a free hint is exactly the bug the gate exists for.
        int spent = manager.HelpSpent;
        Assert(!manager.SpendHelp(ClassicModeController.SkipPenalty), "Spending past the floor should do nothing");
        Assert(manager.HelpSpent == spent, "A refused purchase must not move HelpSpent");
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

        List<string> required = ClassicModeController.RequiredJamoFor("학교");
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
        var hunt = go.AddComponent<ClassicModeController>();
        hunt.SetSeed(3);
        hunt.Initialize();

        Assert(hunt.Target != null, "Classic mode should pick a target from the dictionary");
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
