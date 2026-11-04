using UnityEngine;

public class WordValidatorTests : MonoBehaviour
{
    void Start()
    {
        WordValidator.Load();

        TestValid("학교", true);
        TestValid("사람", true);
        TestValid("집", true);
        TestValid("asdf", false);
        TestValid("없는단어", false);

        WordEntry entry = WordValidator.GetEntry("학교");
        Debug.Assert(entry != null, "GetEntry: 학교 should return an entry");
        Debug.Assert(entry.english == "school", "학교 meaning should be 'school'");
        Debug.Assert(entry.source == "topik1", "학교 should come from the topik1 set");

        // Multi-source: words outside the level-1 set are still valid once
        // the later sets load (the development default).
        TestValid("가격", true);      // topik1 + vocabC
        TestValid("가난", true);      // vocabB only

        // Homonyms: 사과 is apple (topik1) and apology (vocabB). GetEntry
        // returns the first-loaded sense; GetEntries exposes them all.
        WordEntry sagwa = WordValidator.GetEntry("사과");
        Debug.Assert(sagwa != null && sagwa.english == "apple",
            "사과 primary sense should be topik1's 'apple' (load-order priority)");
        var senses = WordValidator.GetEntries("사과");
        Debug.Assert(senses.Count >= 2, $"사과 should have at least 2 senses, got {senses.Count}");

        // The clue invariant. ClassicModeController renders `prompt`, so a
        // clue-eligible entry without one would show "Find: " and nothing
        // else. The importer guarantees it; this is what catches a
        // reimport that silently stopped doing so.
        int cluable = 0, blank = 0;
        foreach (WordEntry e in WordValidator.AllEntries)
        {
            if (e.syllable_count < 2 || e.syllable_count > 3) continue;
            if (string.IsNullOrEmpty(e.prompt)) blank++; else cluable++;
        }
        Debug.Assert(cluable > 1000, $"expected a few thousand clue-eligible entries, got {cluable}");
        Debug.Assert(blank < cluable / 100,
            $"{blank} clue-eligible entries have no prompt — the import dropped them");

        // Source scoping: EntriesFor returns only that set's entries.
        var topikOnly = WordValidator.EntriesFor("topik1");
        Debug.Assert(topikOnly.Count > 0, "EntriesFor(topik1) should not be empty");
        bool allTopik = true;
        foreach (WordEntry e in topikOnly)
            if (e.source != "topik1") allTopik = false;
        Debug.Assert(allTopik, "EntriesFor(topik1) should only contain topik1 entries");

        Debug.Log("All WordValidator tests passed!");
    }

    void TestValid(string word, bool expected)
    {
        bool result = WordValidator.IsValid(word);
        Debug.Assert(result == expected,
            $"IsValid({word}) = {result}, expected {expected}");
    }
}