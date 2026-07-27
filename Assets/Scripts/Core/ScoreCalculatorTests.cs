using System.Collections.Generic;
using UnityEngine;

public class ScoreCalculatorTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestBaseScoreTable();
        TestCountSyllables();
        TestCountJongseong();
        TestWordScore();
        TestComboWindow();
        TestGameManagerScoringAndCombo();
        TestGameManagerRoundEnd();
        TestGameManagerSaveAndRestore();
        Cleanup();
        Debug.Log("All ScoreCalculator tests passed!");
    }

    void TestBaseScoreTable()
    {
        Assert(ScoreCalculator.BaseScore(2) == 100, "2-syllable word should score 100");
        Assert(ScoreCalculator.BaseScore(3) == 200, "3-syllable word should score 200");
        Assert(ScoreCalculator.BaseScore(4) == 350, "4-syllable word should score 350");
        Assert(ScoreCalculator.BaseScore(5) == 350, "5-syllable word should score 350");
        Assert(ScoreCalculator.BaseScore(1) == 50, "1-syllable word should score 50");
        Assert(ScoreCalculator.BaseScore(0) == 0, "0 syllables should score 0");
    }

    void TestCountSyllables()
    {
        Assert(ScoreCalculator.CountSyllables("학교") == 2, "학교 has 2 syllables");
        Assert(ScoreCalculator.CountSyllables("바나나") == 3, "바나나 has 3 syllables");
        Assert(ScoreCalculator.CountSyllables("") == 0, "Empty string has 0 syllables");
    }

    void TestCountJongseong()
    {
        Assert(ScoreCalculator.CountJongseong("학교") == 1, "학교 has 1 받침 (학)");
        Assert(ScoreCalculator.CountJongseong("친구") == 1, "친구 has 1 받침 (친)");
        Assert(ScoreCalculator.CountJongseong("바나나") == 0, "바나나 has no 받침");
        Assert(ScoreCalculator.CountJongseong("물") == 1, "물 has 1 받침");
        Assert(ScoreCalculator.CountJongseong("한국말") == 3, "한국말 has 3 받침");
    }

    void TestWordScore()
    {
        Assert(ScoreCalculator.WordScore("학교") == 150, "학교 = 100 base + 50 받침 = 150");
        Assert(ScoreCalculator.WordScore("바나나") == 200, "바나나 = 200 base + 0 받침 = 200");
        Assert(ScoreCalculator.WordScore("한국말") == 350, "한국말 = 200 base + 150 받침 = 350");
        Assert(ScoreCalculator.WordScore("물") == 100, "물 = 50 base + 50 받침 = 100");
    }

    void TestComboWindow()
    {
        Assert(ScoreCalculator.IsCombo(10f, 30f), "20s gap should be a combo");
        Assert(ScoreCalculator.IsCombo(10f, 40f), "Exactly 30s gap should be a combo");
        Assert(!ScoreCalculator.IsCombo(10f, 41f), "31s gap should not be a combo");
        Assert(!ScoreCalculator.IsCombo(float.NegativeInfinity, 5f), "First word of the round is never a combo");
        Assert(ScoreCalculator.ApplyCombo(100) == 150, "Combo should multiply 100 into 150");
    }

    GameManager MakeManager()
    {
        var go = new GameObject("TestGameManager");
        _spawned.Add(go);
        var manager = go.AddComponent<GameManager>();
        manager.Configure(loadResultSceneOnEnd: false);
        manager.StartRound();
        return manager;
    }

    void TestGameManagerScoringAndCombo()
    {
        GameManager manager = MakeManager();

        int first = manager.RegisterWord("학교", 10f);
        Assert(first == 150, $"First word 학교 should award 150, got {first}");

        int second = manager.RegisterWord("친구", 25f);
        Assert(second == 225, $"친구 within 15s should combo: 150 x 1.5 = 225, got {second}");

        int third = manager.RegisterWord("바나나", 60f);
        Assert(third == 200, $"바나나 35s later should not combo, got {third}");

        Assert(manager.Score == 575, $"Total score should be 575, got {manager.Score}");
        Assert(manager.WordsCompleted == 3, "Three words should be recorded");
    }

    void TestGameManagerRoundEnd()
    {
        GameManager manager = MakeManager();

        bool ended = false;
        int finalScore = -1;
        manager.RoundEnded += s => { ended = true; finalScore = s; };

        manager.RegisterWord("학교", 5f);
        manager.Tick(GameManager.RoundSeconds + 1f);

        Assert(ended, "Ticking past 180s should end the round");
        Assert(!manager.RoundActive, "Round should be inactive after ending");
        Assert(finalScore == 150, $"RoundEnded should carry the final score 150, got {finalScore}");
        Assert(GameManager.LastFinalScore == 150, "LastFinalScore should persist for ResultScene");

        Assert(manager.RegisterWord("친구", 200f) == 0, "Words after round end should score 0");
    }

    void TestGameManagerSaveAndRestore()
    {
        GameManager manager = MakeManager();
        
        manager.RegisterWord("학교", 5f); // score = 150, words completed = 1
        manager.Tick(10f); // TimeRemaining becomes GameManager.RoundSeconds - 10f
        
        float expectedTimeRemaining = manager.TimeRemaining;
        int expectedScore = manager.Score;
        int expectedWordsCompleted = manager.WordsCompleted;
        GameMode expectedMode = manager.Mode;

        // Save session state
        manager.SaveSessionState();

        Assert(GameManager.SavedTimeRemaining.HasValue && GameManager.SavedTimeRemaining.Value == expectedTimeRemaining, "SavedTimeRemaining should match actual TimeRemaining");
        Assert(GameManager.SavedScore.HasValue && GameManager.SavedScore.Value == expectedScore, "SavedScore should match actual Score");
        Assert(GameManager.SavedWordsCompleted.HasValue && GameManager.SavedWordsCompleted.Value == expectedWordsCompleted, "SavedWordsCompleted should match actual WordsCompleted");
        Assert(GameManager.SavedMode.HasValue && GameManager.SavedMode.Value == expectedMode, "SavedMode should match actual Mode");

        // Now destroy/cleanup current manager and create a new one to simulate scene reload
        Cleanup();
        
        // Ensure the static saved state survives cleanup/destruction
        Assert(GameManager.SavedTimeRemaining.HasValue && GameManager.SavedTimeRemaining.Value == expectedTimeRemaining, "Saved state must survive scene reload/destruction");

        // Create new GameManager and start round with the same mode (Classic by default in GameSettings.Mode)
        GameSettings.Mode = expectedMode;
        GameManager newManager = MakeManager(); // MakeManager calls StartRound which consumes and clears the saved session

        Assert(newManager.TimeRemaining == expectedTimeRemaining, $"Restored TimeRemaining should match, expected {expectedTimeRemaining}, got {newManager.TimeRemaining}");
        Assert(newManager.Score == expectedScore, $"Restored Score should match, expected {expectedScore}, got {newManager.Score}");
        Assert(newManager.WordsCompleted == expectedWordsCompleted, $"Restored WordsCompleted should match, expected {expectedWordsCompleted}, got {newManager.WordsCompleted}");
        Assert(newManager.Mode == expectedMode, "Restored Mode should match");

        // Verify saved session is cleared after being consumed
        Assert(!GameManager.SavedTimeRemaining.HasValue, "Saved session must be cleared after being consumed");

        // Test mode change in settings (should reset round)
        GameManager manager2 = MakeManager();
        manager2.RegisterWord("학교", 5f);
        manager2.Tick(10f);
        manager2.SaveSessionState();

        // Change mode
        GameSettings.Mode = GameMode.Zen;
        GameManager manager3 = MakeManager(); // different mode

        Assert(manager3.Score == 0, "Changing mode should start a fresh round with 0 score");
        Assert(manager3.WordsCompleted == 0, "Changing mode should start a fresh round with 0 words");
        Assert(manager3.Mode == GameMode.Zen, "New manager should use the new mode");
        Assert(!GameManager.SavedTimeRemaining.HasValue, "Saved session must be cleared even if mode changed");

        // Restore GameSettings.Mode back to default (Classic)
        GameSettings.Mode = GameMode.Classic;
    }

    void Cleanup()
    {
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
