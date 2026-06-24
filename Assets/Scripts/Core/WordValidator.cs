using System.Collections.Generic;
using UnityEngine;

public static class WordValidator
{
    private static Dictionary<string, WordEntry> _wordMap;
    private static HashSet<string> _wordSet;
    private static bool _loaded = false;

    public static void Load()
    {
        if (_loaded) return;

        TextAsset json = Resources.Load<TextAsset>("topik_level1");
        if (json == null)
        {
            Debug.LogError("WordValidator: topik_level1.json not found in Resources!");
            return;
        }

        WordList list = JsonUtility.FromJson<WordList>(json.text);

        _wordMap = new Dictionary<string, WordEntry>();
        _wordSet = new HashSet<string>();

        foreach (WordEntry entry in list.entries)
        {
            _wordMap[entry.word] = entry;
            _wordSet.Add(entry.word);
        }

        _loaded = true;
        Debug.Log($"WordValidator: loaded {_wordSet.Count} words.");
    }

    public static bool IsValid(string word)
    {
        return _wordSet.Contains(word);
    }

    public static WordEntry GetEntry(string word)
    {
        _wordMap.TryGetValue(word, out WordEntry entry);
        return entry;
    }
}