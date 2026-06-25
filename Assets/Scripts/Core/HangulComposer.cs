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

    // Valid consonants for 초성 position only
    public static readonly HashSet<string> ValidChoseong = new HashSet<string>(Choseong);

    // Valid consonants for 종성 position only
    public static readonly HashSet<string> ValidJongseong = new HashSet<string>(Jongseong);

    public static string Compose(string cho, string jung, string jong = "")
    {
        int c = System.Array.IndexOf(Choseong, cho);
        int v = System.Array.IndexOf(Jungseong, jung);
        int f = System.Array.IndexOf(Jongseong, jong);

        if (c == -1 || v == -1 || f == -1) return null; // invalid combination

        int code = 0xAC00 + (c * 21 * 28) + (v * 28) + f;
        return System.Char.ConvertFromUtf32(code);
    }

    public static bool IsValidChoseong(string jamo) => 
        System.Array.IndexOf(Choseong, jamo) != -1;

    public static bool IsValidJungseong(string jamo) => 
        System.Array.IndexOf(Jungseong, jamo) != -1;

    public static bool IsValidJongseong(string jamo) => 
        System.Array.IndexOf(Jongseong, jamo) != -1;
}
