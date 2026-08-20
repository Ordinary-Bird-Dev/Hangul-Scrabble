using System.Collections.Generic;
using UnityEngine;

public static class WordValidator
{
    // Word-set registry: set id -> Resources file. Load order is
    // priority: the first-loaded sense of a word is what GetEntry
    // returns, so topik1's gloss wins where sets share a word.
    private static readonly (string id, string resource)[] Registry =
    {
        ("topik1", "topik1_words"),
        ("vocabB", "vocabB_words"),
        ("vocabC", "vocabC_words"),
    };

    // TODO(DLC): every set loads by default so development exercises the
    // full corpus. Before any paid-DLC release this must flip to just
    // { "topik1" }, with the extra sets enabled by ownership/settings.
    private static readonly string[] DefaultSources = { "topik1", "vocabB", "vocabC" };

    // word -> senses. Homonyms (같은 word, different english) are kept as
    // separate entries under one key rather than overwriting each other.
    private static Dictionary<string, List<WordEntry>> _wordMap;
    private static List<WordEntry> _allEntries;
    private static string _loadedKey;

    public static void Load() => Load(DefaultSources);

    // Loads the given word sets into one runtime lookup. Re-calling with
    // the same sources is a no-op (Load() runs opportunistically all over
    // the codebase); different sources rebuild the lookup.
    public static void Load(IReadOnlyList<string> sources)
    {
        string key = string.Join(",", sources);
        if (_wordMap != null && key == _loadedKey) return;

        var map = new Dictionary<string, List<WordEntry>>();
        var all = new List<WordEntry>();
        int setsLoaded = 0;

        foreach (string id in sources)
        {
            string resource = ResourceFor(id);
            if (resource == null)
            {
                Debug.LogError($"WordValidator: unknown word set '{id}' — not in the registry.");
                continue;
            }

            TextAsset json = Resources.Load<TextAsset>(resource);
            if (json == null)
            {
                Debug.LogError($"WordValidator: {resource}.json not found in Resources!");
                continue;
            }

            WordList list = JsonUtility.FromJson<WordList>(json.text);
            if (list == null || list.words == null)
            {
                Debug.LogError($"WordValidator: {resource}.json is not in the {{\"words\": [...]}} schema — run Tools/import_vocab.ps1 on it.");
                continue;
            }

            foreach (WordEntry entry in list.words)
            {
                entry.source = id;

                if (!map.TryGetValue(entry.word, out List<WordEntry> senses))
                {
                    senses = new List<WordEntry>(1);
                    map[entry.word] = senses;
                }

                // The same word with the same gloss arriving from a later
                // set is a cross-set duplicate, not a homonym — drop it.
                if (HasGloss(senses, entry.english)) continue;

                senses.Add(entry);
                all.Add(entry);
            }
            setsLoaded++;
        }

        // All sets failed to load: keep whatever was loaded before (null
        // on first call, so IsValid stays false) and let a later Load
        // retry rather than caching the failure.
        if (setsLoaded == 0) return;

        _wordMap = map;
        _allEntries = all;
        _loadedKey = key;
        Debug.Log($"WordValidator: loaded {all.Count} entries ({map.Count} distinct words) from {setsLoaded} set(s): {key}.");
    }

    public static bool IsValid(string word)
    {
        // _wordMap stays null if Load() failed (missing Resources asset);
        // treat every word as invalid rather than throwing on each confirm.
        return _wordMap != null && word != null && _wordMap.ContainsKey(word);
    }

    // The primary sense of a word: the first one loaded, i.e. from the
    // earliest set in the load order that defines it.
    public static WordEntry GetEntry(string word)
    {
        if (_wordMap == null || word == null) return null;
        return _wordMap.TryGetValue(word, out List<WordEntry> senses) ? senses[0] : null;
    }

    // Every sense of a word, in load order. Empty when unknown.
    public static IReadOnlyList<WordEntry> GetEntries(string word)
    {
        if (_wordMap == null || word == null) return System.Array.Empty<WordEntry>();
        return _wordMap.TryGetValue(word, out List<WordEntry> senses)
            ? (IReadOnlyList<WordEntry>)senses
            : System.Array.Empty<WordEntry>();
    }

    // All loaded entries (every sense of every word), for modes that pick
    // target words (Classic).
    public static IReadOnlyCollection<WordEntry> AllEntries =>
        _allEntries != null ? (IReadOnlyCollection<WordEntry>)_allEntries : System.Array.Empty<WordEntry>();

    // Entries belonging to one word set — the hook for difficulty/DLC
    // scoping of the Classic target pool.
    public static IReadOnlyList<WordEntry> EntriesFor(string source)
    {
        if (_allEntries == null) return System.Array.Empty<WordEntry>();
        var result = new List<WordEntry>();
        foreach (WordEntry entry in _allEntries)
            if (entry.source == source) result.Add(entry);
        return result;
    }

    private static string ResourceFor(string id)
    {
        foreach ((string regId, string resource) in Registry)
            if (regId == id) return resource;
        return null;
    }

    private static bool HasGloss(List<WordEntry> senses, string english)
    {
        foreach (WordEntry sense in senses)
            if (sense.english == english) return true;
        return false;
    }
}
