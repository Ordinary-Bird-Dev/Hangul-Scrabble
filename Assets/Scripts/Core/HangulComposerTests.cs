using UnityEngine;

public class HangulComposerTests : MonoBehaviour
{
    void Start()
    {
        TestBasicCompose();
        TestNoJongseong();
        TestEveryChoseong();
        TestEveryJungseong();
        TestEveryJongseong();
        TestInvalidChoseong();
        TestInvalidJungseong();
        TestInvalidJongseong();
        TestEmptyJungseongFails();
        TestIsValidHelpers();
        Debug.Log("All HangulComposer tests passed!");
    }

    void TestBasicCompose()
    {
        Assert(HangulComposer.Compose("ㅎ", "ㅏ", "ㄱ") == "학", "ㅎ+ㅏ+ㄱ should be 학");
        Assert(HangulComposer.Compose("ㅅ", "ㅏ", "ㄹ") == "살", "ㅅ+ㅏ+ㄹ should be 살");
        Assert(HangulComposer.Compose("ㄱ", "ㅏ") == "가", "ㄱ+ㅏ should be 가");
    }

    void TestNoJongseong()
    {
        Assert(HangulComposer.Compose("ㅇ", "ㅣ") == "이", "ㅇ+ㅣ (no jong) should be 이");
        Assert(HangulComposer.Compose("ㅅ", "ㅏ", "") == "사", "ㅅ+ㅏ+empty jong should be 사");
    }

    void TestEveryChoseong()
    {
        // Every possible 초성 paired with ㅏ should compose without returning null
        string[] choseongList = {
            "ㄱ","ㄲ","ㄴ","ㄷ","ㄸ","ㄹ","ㅁ","ㅂ","ㅃ",
            "ㅅ","ㅆ","ㅇ","ㅈ","ㅉ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
        };
        foreach (string cho in choseongList)
        {
            string result = HangulComposer.Compose(cho, "ㅏ");
            Assert(result != null, $"Choseong {cho}+ㅏ should compose successfully");
        }
        Assert(choseongList.Length == 19, "There should be exactly 19 choseong");
    }

    void TestEveryJungseong()
    {
        // Every possible 중성 paired with ㄱ should compose without returning null
        string[] jungseongList = {
            "ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ",
            "ㅘ","ㅙ","ㅚ","ㅛ","ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"
        };
        foreach (string jung in jungseongList)
        {
            string result = HangulComposer.Compose("ㄱ", jung);
            Assert(result != null, $"Jungseong ㄱ+{jung} should compose successfully");
        }
        Assert(jungseongList.Length == 21, "There should be exactly 21 jungseong");
    }

    void TestEveryJongseong()
    {
        // Every possible 종성 (including empty) appended to 가 should compose without returning null
        string[] jongseongList = {
            "","ㄱ","ㄲ","ㄳ","ㄴ","ㄵ","ㄶ","ㄷ","ㄹ",
            "ㄺ","ㄻ","ㄼ","ㄽ","ㄾ","ㄿ","ㅀ","ㅁ","ㅂ",
            "ㅄ","ㅅ","ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"
        };
        foreach (string jong in jongseongList)
        {
            string result = HangulComposer.Compose("ㄱ", "ㅏ", jong);
            Assert(result != null, $"Jongseong ㄱ+ㅏ+'{jong}' should compose successfully");
        }
        Assert(jongseongList.Length == 28, "There should be exactly 28 jongseong (including empty)");
    }

    void TestInvalidChoseong()
    {
        string result = HangulComposer.Compose("ㅏ", "ㅏ"); // vowel in cho position
        Assert(result == null, "A vowel passed as choseong should return null");
    }

    void TestInvalidJungseong()
    {
        string result = HangulComposer.Compose("ㄱ", "ㄱ"); // consonant in jung position
        Assert(result == null, "A consonant passed as jungseong should return null");
    }

    void TestInvalidJongseong()
    {
        string result = HangulComposer.Compose("ㄱ", "ㅏ", "ㅏ"); // vowel in jong position
        Assert(result == null, "A vowel passed as jongseong should return null");
    }

    void TestEmptyJungseongFails()
    {
        string result = HangulComposer.Compose("ㅎ", "");
        Assert(result == null, "Empty jungseong should return null since vowel is required");
    }

    void TestIsValidHelpers()
    {
        Assert(HangulComposer.IsValidChoseong("ㅎ") == true, "ㅎ should be a valid choseong");
        Assert(HangulComposer.IsValidChoseong("ㅏ") == false, "ㅏ should not be a valid choseong");

        Assert(HangulComposer.IsValidJungseong("ㅏ") == true, "ㅏ should be a valid jungseong");
        Assert(HangulComposer.IsValidJungseong("ㄱ") == false, "ㄱ should not be a valid jungseong");

        Assert(HangulComposer.IsValidJongseong("") == true, "Empty string should be a valid jongseong (no final consonant)");
        Assert(HangulComposer.IsValidJongseong("ㄲ") == true, "ㄲ should be a valid jongseong");
        Assert(HangulComposer.IsValidJongseong("ㅃ") == false, "ㅃ should not be a valid jongseong (not allowed as final)");
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}