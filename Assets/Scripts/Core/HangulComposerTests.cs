using UnityEngine;

public class HangulComposerTests : MonoBehaviour
{
    void Start()
    {
        TestCompose("ㅎ", "ㅏ", "ㄱ", "학");
        TestCompose("ㅅ", "ㅏ", "", "사");
        TestCompose("ㅇ", "ㅣ", "", "이");
        TestInvalid("ㅎ", "", "");
        Debug.Log("All HangulComposer tests passed!");
    }

    void TestCompose(string cho, string jung, string jong, string expected)
    {
        string result = HangulComposer.Compose(cho, jung, jong);
        Debug.Assert(result == expected,
            $"Compose({cho},{jung},{jong}) = {result}, expected {expected}");
    }

    void TestInvalid(string cho, string jung, string jong)
    {
        string result = HangulComposer.Compose(cho, jung, jong);
        Debug.Assert(result == null,
            $"Compose({cho},{jung},{jong}) should return null for invalid input");
    }
}