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
        Debug.Assert(entry.meaning_en == "school", "학교 meaning should be 'school'");

        Debug.Log("All WordValidator tests passed!");
    }

    void TestValid(string word, bool expected)
    {
        bool result = WordValidator.IsValid(word);
        Debug.Assert(result == expected,
            $"IsValid({word}) = {result}, expected {expected}");
    }
}