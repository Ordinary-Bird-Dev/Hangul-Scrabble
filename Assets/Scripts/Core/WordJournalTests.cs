using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WordJournalTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestFormatEntry();
        TestAddWordNewestFirstAndCapped();
        TestPanelHiddenUntilFirstWord();
        TestJournalSubscribesToWordBuilder();
        TestGameModeSettingRoundtrip();
        TestZenModeRoundNeverEnds();
        Cleanup();
        Debug.Log("All WordJournal tests passed!");
    }

    WordEntry Entry(string word, string english) =>
        new WordEntry { word = word, english = english, prompt = english, example = "" };

    void TestFormatEntry()
    {
        Assert(WordJournal.FormatEntry(Entry("학교", "school")) == "학교 — school",
            "FormatEntry should join word and meaning with an em dash");
        Assert(WordJournal.FormatEntry(Entry("물", "")) == "물",
            "FormatEntry should show just the word when the meaning is empty");
        Assert(WordJournal.FormatEntry(null) == "", "FormatEntry(null) should be empty");
    }

    WordJournal MakeJournal(out TMP_Text text, WordBuilder builder = null)
    {
        var textGo = new GameObject("TestJournalText");
        _spawned.Add(textGo);
        text = textGo.AddComponent<TextMeshProUGUI>();

        var journalGo = new GameObject("TestJournal");
        _spawned.Add(journalGo);
        var journal = journalGo.AddComponent<WordJournal>();
        journal.Configure(text, builder);
        journal.Initialize();
        return journal;
    }

    void TestAddWordNewestFirstAndCapped()
    {
        WordJournal journal = MakeJournal(out TMP_Text text);

        for (int i = 1; i <= 10; i++)
            journal.AddWord(Entry($"단어{i}", $"word{i}"));

        Assert(journal.Count == 10, $"Count should track all 10 words, got {journal.Count}");
        Assert(journal.Lines.Count == WordJournal.MaxVisibleEntries,
            $"Visible list should cap at {WordJournal.MaxVisibleEntries}, got {journal.Lines.Count}");
        Assert(journal.Lines[0] == "단어10 — word10", "Newest word should be listed first");
        Assert(text.text.StartsWith("단어10"), "Journal text should start with the newest word");
        Assert(!text.text.Contains("단어1 —"), "Oldest overflow entries should drop off the visible list");
    }

    // The journal panel starts hidden and appears with the first word.
    //
    // Built with a real parent, unlike MakeJournal: the panel is the text's
    // parent, and a journal whose text is a root object has no panel to
    // hide — which is exactly the case the other tests exercise.
    void TestPanelHiddenUntilFirstWord()
    {
        var panelGo = new GameObject("TestJournalPanel", typeof(RectTransform));
        _spawned.Add(panelGo);

        var textGo = new GameObject("TestJournalText", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();

        var journalGo = new GameObject("TestPanelJournal");
        _spawned.Add(journalGo);
        var journal = journalGo.AddComponent<WordJournal>();
        journal.Configure(text, null);
        journal.Initialize();

        Assert(!panelGo.activeSelf,
            "Journal panel should be hidden before the first word — an empty panel reads as a bug");
        Assert(!journal.PanelVisible, "PanelVisible should be false with no words");

        journal.AddWord(Entry("물", "water"));

        Assert(panelGo.activeSelf, "Journal panel should appear once a word is found");
        Assert(journal.PanelVisible, "PanelVisible should be true after the first word");

        journal.AddWord(Entry("별", "star"));

        Assert(panelGo.activeSelf, "Journal panel should stay visible for later words");
    }

    void TestJournalSubscribesToWordBuilder()
    {
        var builderGo = new GameObject("TestWordBuilder");
        _spawned.Add(builderGo);
        var syllableBuilder = builderGo.AddComponent<SyllableBuilderUI>();
        var wordBuilder = builderGo.AddComponent<WordBuilder>();
        wordBuilder.Configure(syllableBuilder, null);
        wordBuilder.Initialize();

        WordJournal journal = MakeJournal(out _, wordBuilder);

        wordBuilder.AppendSyllable("학");
        wordBuilder.AppendSyllable("교");
        wordBuilder.ConfirmWord();

        Assert(journal.Count == 1, "Completing a word should add a journal entry");
        Assert(journal.Lines[0].StartsWith("학교"), $"Journal should record 학교, got '{journal.Lines[0]}'");
    }

    void TestGameModeSettingRoundtrip()
    {
        GameMode original = GameSettings.Mode;

        GameSettings.Mode = GameMode.Zen;
        Assert(GameSettings.Mode == GameMode.Zen, "Mode should persist Zen");
        GameSettings.Mode = GameMode.WordHunt;
        Assert(GameSettings.Mode == GameMode.WordHunt, "Mode should persist WordHunt");
        GameSettings.Mode = GameMode.Classic;
        Assert(GameSettings.Mode == GameMode.Classic, "Mode should persist Classic");

        GameSettings.Mode = original;
    }

    void TestZenModeRoundNeverEnds()
    {
        GameMode original = GameSettings.Mode;
        GameSettings.Mode = GameMode.Zen;

        var go = new GameObject("TestZenGameManager");
        _spawned.Add(go);
        var manager = go.AddComponent<GameManager>();
        manager.Configure(loadResultSceneOnEnd: false);
        manager.StartRound();

        manager.Tick(GameManager.RoundSeconds * 3f);

        Assert(manager.RoundActive, "Zen Mode round should not end from elapsed time");
        Assert(manager.Mode == GameMode.Zen, "GameManager should report Zen mode");
        // Reversed deliberately. This used to assert that Zen scored, which
        // contradicted the How to Play copy; Zen is the casual mode and now
        // awards nothing. The word still has to COUNT, though — the journal
        // and the gauge both read WordsCompleted.
        Assert(manager.RegisterWord("학교", 10f) == 0, "Zen Mode should award no points");
        Assert(manager.Score == 0, "Zen Mode should leave the score at zero");
        Assert(manager.WordsCompleted == 1, "Zen Mode should still count the word");
        Assert(!manager.ScoringEnabled, "ScoringEnabled should be false in Zen Mode");

        GameSettings.Mode = original;
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
