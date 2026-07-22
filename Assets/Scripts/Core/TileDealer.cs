using System.Collections.Generic;

// Pure logic (no UnityEngine) so it can be unit tested and, later,
// seeded deterministically for the Phase 4 daily puzzle.
public class TileDealer
{
    // Weights roughly follow jamo frequency in TOPIK Level 1 vocabulary:
    // common jamo (ㅇ, ㄱ, ㅏ, ㅣ...) appear far more often than tense
    // consonants (ㅃ, ㅉ) or compound vowels (ㅙ, ㅞ).
    private static readonly (string jamo, int weight)[] ChoseongWeights =
    {
        ("ㄱ", 10), ("ㄲ", 2), ("ㄴ", 8), ("ㄷ", 7), ("ㄸ", 2),
        ("ㄹ", 7), ("ㅁ", 6), ("ㅂ", 6), ("ㅃ", 1), ("ㅅ", 8),
        ("ㅆ", 2), ("ㅇ", 12), ("ㅈ", 6), ("ㅉ", 1), ("ㅊ", 4),
        ("ㅋ", 3), ("ㅌ", 3), ("ㅍ", 3), ("ㅎ", 7)
    };

    private static readonly (string jamo, int weight)[] JungseongWeights =
    {
        ("ㅏ", 12), ("ㅐ", 5), ("ㅑ", 3), ("ㅒ", 1), ("ㅓ", 8),
        ("ㅔ", 5), ("ㅕ", 4), ("ㅖ", 2), ("ㅗ", 8), ("ㅘ", 2),
        ("ㅙ", 1), ("ㅚ", 2), ("ㅛ", 3), ("ㅜ", 7), ("ㅝ", 1),
        ("ㅞ", 1), ("ㅟ", 2), ("ㅠ", 3), ("ㅡ", 7), ("ㅢ", 2),
        ("ㅣ", 10)
    };

    // Fraction of dealt tiles that are consonants; the rest are vowels.
    private const double ConsonantRatio = 0.55;

    private readonly System.Random _random;

    public TileDealer() : this(System.Environment.TickCount) { }

    public TileDealer(int seed)
    {
        _random = new System.Random(seed);
    }

    public string NextChoseong() => WeightedPick(ChoseongWeights);

    public string NextJungseong() => WeightedPick(JungseongWeights);

    public string NextJamo() =>
        _random.NextDouble() < ConsonantRatio ? NextChoseong() : NextJungseong();

    // Exposes the dealer's RNG so tray shuffles stay deterministic under
    // a seed (Phase 4 daily puzzle).
    public int NextIndex(int maxExclusive) => _random.Next(maxExclusive);

    // Deals a balanced hand: ~55% consonants, ~45% vowels, shuffled,
    // so a full tray always contains enough vowels to build syllables.
    public List<string> Deal(int count)
    {
        int consonants = (int)System.Math.Round(count * ConsonantRatio);
        var result = new List<string>(count);

        for (int i = 0; i < consonants && i < count; i++)
            result.Add(NextChoseong());
        while (result.Count < count)
            result.Add(NextJungseong());

        Shuffle(result);
        return result;
    }

    private void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private string WeightedPick((string jamo, int weight)[] pool)
    {
        int total = 0;
        foreach (var entry in pool)
            total += entry.weight;

        int roll = _random.Next(total);
        foreach (var entry in pool)
        {
            roll -= entry.weight;
            if (roll < 0) return entry.jamo;
        }
        return pool[pool.Length - 1].jamo;
    }
}
