using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WordBuilderTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestAppendBuildsChain();
        TestConfirmValidWordRaisesEventAndClears();
        TestConfirmSingleSyllableDictionaryWord();
        TestConfirmInvalidWordRejectedAndChainKept();
        TestConfirmEmptyChainFails();
        TestResetClearsChain();
        TestSyllableConfirmedEventFeedsChain();
        Cleanup();
        Debug.Log("All WordBuilder tests passed!");
    }

    WordBuilder MakeBuilder(out TMP_Text wordText)
    {
        var textGo = new GameObject("TestWordText");
        _spawned.Add(textGo);
        wordText = textGo.AddComponent<TextMeshProUGUI>();

        var builderGo = new GameObject("TestWordBuilder");
        _spawned.Add(builderGo);
        var syllableBuilder = builderGo.AddComponent<SyllableBuilderUI>();

        var wordBuilderGo = new GameObject("TestWordBuilderHost");
        _spawned.Add(wordBuilderGo);
        var wordBuilder = wordBuilderGo.AddComponent<WordBuilder>();
        wordBuilder.Configure(syllableBuilder, wordText);
        wordBuilder.Initialize();
        return wordBuilder;
    }

    void TestAppendBuildsChain()
    {
        WordBuilder builder = MakeBuilder(out TMP_Text wordText);

        builder.AppendSyllable("학");
        builder.AppendSyllable("교");

        Assert(builder.CurrentWord == "학교", $"Chain should read 학교, got {builder.CurrentWord}");
        Assert(builder.SyllableCount == 2, "Chain should hold 2 syllables");
        Assert(wordText.text == "학교", "WordText should mirror the chain");
    }

    void TestConfirmValidWordRaisesEventAndClears()
    {
        WordBuilder builder = MakeBuilder(out TMP_Text wordText);

        WordEntry completed = null;
        builder.WordCompleted += e => completed = e;

        builder.AppendSyllable("학");
        builder.AppendSyllable("교");
        bool result = builder.ConfirmWord();

        Assert(result, "학교 should validate as a TOPIK Level 1 word");
        Assert(completed != null && completed.word == "학교", "WordCompleted should carry the 학교 entry");
        Assert(completed != null && completed.meaning == "school", $"학교 meaning should be school, got {completed?.meaning}");
        Assert(builder.CurrentWord == "", "Chain should clear after a successful word");
        Assert(wordText.text == "", "WordText should clear after a successful word");
    }

    void TestConfirmSingleSyllableDictionaryWord()
    {
        WordBuilder builder = MakeBuilder(out _);

        WordEntry completed = null;
        builder.WordCompleted += e => completed = e;

        builder.AppendSyllable("물");
        bool result = builder.ConfirmWord();

        Assert(result, "물 is in the dictionary and should be accepted");
        Assert(completed != null && completed.word == "물", "WordCompleted should carry the 물 entry");
    }

    void TestConfirmInvalidWordRejectedAndChainKept()
    {
        WordBuilder builder = MakeBuilder(out _);

        string rejected = null;
        builder.WordRejected += w => rejected = w;

        builder.AppendSyllable("교");
        builder.AppendSyllable("학");
        bool result = builder.ConfirmWord();

        Assert(!result, "교학 should not validate");
        Assert(rejected == "교학", $"WordRejected should carry 교학, got {rejected}");
        Assert(builder.CurrentWord == "교학", "Chain should be kept after a rejected word so the player can reset");
    }

    void TestConfirmEmptyChainFails()
    {
        WordBuilder builder = MakeBuilder(out _);
        Assert(!builder.ConfirmWord(), "Confirming an empty chain should fail");
    }

    void TestResetClearsChain()
    {
        WordBuilder builder = MakeBuilder(out TMP_Text wordText);

        builder.AppendSyllable("친");
        builder.AppendSyllable("구");
        builder.ResetChain();

        Assert(builder.CurrentWord == "", "ResetChain should empty the chain");
        Assert(wordText.text == "", "WordText should clear on reset");
    }

    void TestSyllableConfirmedEventFeedsChain()
    {
        var builderGo = new GameObject("TestSyllableBuilder2");
        _spawned.Add(builderGo);
        var syllableBuilder = builderGo.AddComponent<SyllableBuilderUI>();

        var wordBuilderGo = new GameObject("TestWordBuilderHost2");
        _spawned.Add(wordBuilderGo);
        var wordBuilder = wordBuilderGo.AddComponent<WordBuilder>();
        wordBuilder.Configure(syllableBuilder, null);
        wordBuilder.Initialize();

        syllableBuilder.Slot.TryPlaceCho("ㅊ");
        syllableBuilder.Slot.TryPlaceJung("ㅣ");
        syllableBuilder.Slot.TryPlaceJong("ㄴ");
        syllableBuilder.ConfirmSyllable();

        Assert(wordBuilder.CurrentWord == "친",
            $"Confirming a syllable in the builder should append it to the chain, got '{wordBuilder.CurrentWord}'");
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
