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

        // Multi-source: words exclusive to the vocabB / vocabC sets are
        // valid once those sets load (the development default).
        TestValid("가격", true);      // vocabB only
        TestValid("가난", true);      // vocabC only

        // Homonyms: 사과 is apple (topik1) and apology (vocabC). GetEntry
        // returns the first-loaded sense; GetEntries exposes them all.
        WordEntry sagwa = WordValidator.GetEntry("사과");
        Debug.Assert(sagwa != null && sagwa.english == "apple",
            "사과 primary sense should be topik1's 'apple' (load-order priority)");
        var senses = WordValidator.GetEntries("사과");
        Debug.Assert(senses.Count >= 2, $"사과 should have at least 2 senses, got {senses.Count}");

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