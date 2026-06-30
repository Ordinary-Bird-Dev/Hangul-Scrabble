using System.Collections.Generic;

public static class HangulComposer
{
    private static readonly string[] Choseong = {
        "ㄱ","ㄲ","ㄴ","ㄷ","ㄸ","ㄹ","ㅁ","ㅂ","ㅃ",
        "ㅅ","ㅆ","ㅇ","ㅈ","ㅉ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
    };

    private static readonly string[] Jungseong = {
        "ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ",
        "ㅘ","ㅙ","ㅚ","ㅛ","ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"
    };

    private static readonly string[] Jongseong = {
        "","ㄱ","ㄲ","ㄳ","ㄴ","ㄵ","ㄶ","ㄷ","ㄹ",
        "ㄺ","ㄻ","ㄼ","ㄽ","ㄾ","ㄿ","ㅀ","ㅁ","ㅂ",
        "ㅄ","ㅅ","ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
    };

    // O(1) lookup tables: jamo string -> its index in the array above
    private static readonly Dictionary<string, int> ChoseongIndex = BuildIndex(Choseong);
    private static readonly Dictionary<string, int> JungseongIndex = BuildIndex(Jungseong);
    private static readonly Dictionary<string, int> JongseongIndex = BuildIndex(Jongseong);

    // Valid consonants for 초성 position only
    public static readonly HashSet<string> ValidChoseong = new HashSet<string>(Choseong);

    // Valid consonants for 종성 position only
    public static readonly HashSet<string> ValidJongseong = new HashSet<string>(Jongseong);

    private static Dictionary<string, int> BuildIndex(string[] source)
    {
        var dict = new Dictionary<string, int>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            dict[source[i]] = i;
        }
        return dict;
    }

    public static string Compose(string cho, string jung, string jong = "")
    {
        if (!ChoseongIndex.TryGetValue(cho, out int c)) return null;
        if (!JungseongIndex.TryGetValue(jung, out int v)) return null;
        if (!JongseongIndex.TryGetValue(jong, out int f)) return null;

        int code = 0xAC00 + (c * 21 * 28) + (v * 28) + f;
        return System.Char.ConvertFromUtf32(code);
    }

    public static bool IsValidChoseong(string jamo) =>
        ChoseongIndex.ContainsKey(jamo);

    public static bool IsValidJungseong(string jamo) =>
        JungseongIndex.ContainsKey(jamo);

    public static bool IsValidJongseong(string jamo) =>
        JongseongIndex.ContainsKey(jamo);
}