using UnityEngine;

// Pure scoring rules for Classic Mode:
//   2-syllable word = 100, 3-syllable = 200, 4+ = 350
//   +50 per 받침 (jongseong) used
//   x1.5 combo when two words land within 30 seconds
public static class ScoreCalculator
{
    public const float ComboWindowSeconds = 30f;
    public const float ComboMultiplier = 1.5f;

    private const char HangulBase = (char)0xAC00;
    private const char HangulLast = (char)0xD7A3;

    public static int BaseScore(int syllableCount)
    {
        if (syllableCount <= 0) return 0;
        if (syllableCount == 1) return 50; // below the design table; half of a 2-syllable word
        if (syllableCount == 2) return 100;
        if (syllableCount == 3) return 200;
        return 350;
    }

    // Counts composed syllables that carry a final consonant (받침).
    public static int CountJongseong(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;

        int count = 0;
        foreach (char ch in word)
        {
            if (ch < HangulBase || ch > HangulLast) continue;
            if ((ch - HangulBase) % 28 != 0) count++;
        }
        return count;
    }

    public static int CountSyllables(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;

        int count = 0;
        foreach (char ch in word)
            if (ch >= HangulBase && ch <= HangulLast) count++;
        return count;
    }

    public static int WordScore(string word) =>
        BaseScore(CountSyllables(word)) + 50 * CountJongseong(word);

    public static int ApplyCombo(int score) =>
        Mathf.RoundToInt(score * ComboMultiplier);

    public static bool IsCombo(float previousWordTime, float now) =>
        now - previousWordTime <= ComboWindowSeconds;
}
