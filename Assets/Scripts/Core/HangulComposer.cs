using System.Collections.Generic;

public static class HangulComposer
{
    public static readonly string[] Choseong = {
        "ㄱ","ㄲ","ㄴ","ㄷ","ㄸ","ㄹ","ㅁ","ㅂ","ㅃ",
        "ㅅ","ㅆ","ㅇ","ㅈ","ㅉ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
    };
    
    public static readonly string[] Jungseong = {
        "ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ","ㅘ",
        "ㅙ","ㅚ","ㅛ","ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"
    };

    public static readonly string[] Jongseong = {
        "","ㄱ","ㄲ","ㄳ","ㄴ","ㄵ","ㄶ","ㄷ","ㄹ","ㄺ",
        "ㄻ","ㄼ","ㄽ","ㄾ","ㄿ","ㅀ","ㅁ","ㅂ","ㅄ","ㅅ",
        "ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
    };

    public static char Compose(int cho, int jung, int jong = 0)
    {
        int cp = 0xAC00 + (cho * 21 * 28) + (jung * 28) + jong;
        return (char)cp;
    }
    public static (int cho, int jung, int jong) Decompose(char syllable)
    {
        int offset = syllable - 0xAC00;
        int jong = offset % 28;
        int jung = (offset / 28) % 21;
        int cho = offset / (21 * 28);
        return (cho, jung, jong);
    }

    public static bool IsSyllable(char c) =>
        c >= 0xAC00 && c <= 0xD7A3;

    public static bool IsJamo(char c) =>
        (c >= 0x3131 && c <= 0x318E);
}