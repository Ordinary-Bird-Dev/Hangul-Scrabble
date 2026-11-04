// One sense of one word, as stored in Assets/Resources/*_words.json.
//
// Field names are the JSON keys: Unity's JsonUtility matches by name and
// silently leaves anything it cannot match at its default, so renaming a
// member here breaks the data load with no error. Add a field to the
// importer and to this class together or not at all.
[System.Serializable]
public class WordEntry
{
    public string word;

    // The full gloss, every sense joined by "; ". Shown on the meaning
    // card after a solve, where the length is the point — it is the
    // teaching moment. Too long for the clue banner, hence `prompt`.
    public string english;        // was meaning_en

    // The one-or-two-word clue. Split out from `english` because krdict
    // glosses run to hundreds of characters ("go; travel; head for; be
    // bound for; enter; join; …"), which made an unreadable clue.
    //
    // Empty means the source had nothing usable — krdict writes
    // "(no equivalent expression)" for words with no English equivalent.
    // PickTarget skips those, so the word stays buildable and valid but
    // is never set as a target.
    public string prompt;

    public string example;
    public int syllable_count;

    // Jamo the word costs to build, counting a compound final as its
    // parts. Pre-computed by the vocabulary source; the tray holds 14.
    public int tile_count;

    // "tutorial" (2-4 tiles), "core" (5-6) or "challenge" (7-8), banded by
    // the vocabulary source. Nothing reads it yet — it is here so the
    // difficulty ramp has something to sort on without a reimport.
    public string band;

    // Word-set id ("topik1", "vocabB", ...) stamped by WordValidator at
    // load time — not present in the JSON files.
    public string source;
}
