using System.Collections.Generic;

// Pure logic (no UnityEngine): decides whether a tray of jamo tiles can
// build dictionary words. Used by TileManager to guarantee every fresh
// deal has at least one buildable word, and by Word Hunt to keep its
// target reachable.
public static class TrayValidator
{
    // Every jamo needed to compose the word, in order.
    public static List<string> RequiredJamoFor(string word)
    {
        var required = new List<string>();
        if (string.IsNullOrEmpty(word)) return required;

        foreach (char syllable in word)
        {
            if (!HangulComposer.TryDecompose(syllable.ToString(), out string cho, out string jung, out string jong))
                continue;
            required.Add(cho);
            required.Add(jung);
            if (jong != "") required.Add(jong);
        }
        return required;
    }

    // Multiset check: every jamo in required (with duplicates) must be
    // matched by a distinct jamo in available.
    public static bool ContainsAll(IReadOnlyList<string> required, IReadOnlyList<string> available)
    {
        var pool = new List<string>(available);
        foreach (string jamo in required)
            if (!pool.Remove(jamo)) return false;
        return true;
    }

    // True when at least one dictionary word can be assembled from the
    // tray. Words needing compound finals (없다, 읽다...) simply fail the
    // multiset check against random trays, which is the correct outcome.
    public static bool AnyWordBuildable(IReadOnlyList<string> tray, IEnumerable<WordEntry> entries)
    {
        foreach (WordEntry entry in entries)
        {
            List<string> required = RequiredJamoFor(entry.word);
            if (required.Count == 0 || required.Count > tray.Count) continue;
            if (ContainsAll(required, tray)) return true;
        }
        return false;
    }

    // True when every jamo is one the random dealer can produce — i.e.
    // no compound finals, which exist only as Word Hunt required tiles.
    public static bool AreAllJamoDealable(IReadOnlyList<string> jamos)
    {
        foreach (string jamo in jamos)
            if (!HangulComposer.IsValidChoseong(jamo) && !HangulComposer.IsValidJungseong(jamo))
                return false;
        return true;
    }
}
