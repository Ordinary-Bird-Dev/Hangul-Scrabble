[System.Serializable]
public class WordEntry
{
    public string word;
    public string english;        // was meaning_en
    public string romanization;
    public string example;
    public int syllable_count;

    // Word-set id ("topik1", "vocabB", ...) stamped by WordValidator at
    // load time — not present in the JSON files.
    public string source;
}