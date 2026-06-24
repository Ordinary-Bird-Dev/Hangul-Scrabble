using UnityEngine;

public class HangulComposerTests : MonoBehaviour
{
    void Start()
    {
        TestCompose('학', 18, 0, 1);   // ㅎ+ㅏ+ㄱ
        TestCompose('교', 1, 8, 0);   // ㄲ... wait, 교 = ㄱ(0)+ㅛ(12)+0
        TestCompose('사', 9, 0, 0);   // ㅅ+ㅏ (no jong)
        TestCompose('람', 5, 0, 16);  // ㄹ+ㅏ+ㅁ
        TestDecompose('학', 18, 0, 1);
        TestDecompose('사', 9, 0, 0);
        Debug.Log("All HangulComposer tests passed!");
    }

    void TestCompose(char expected, int cho, int jung, int jong)
    {
        char result = HangulComposer.Compose(cho, jung, jong);
        Debug.Assert(result == expected,
            $"Compose({cho},{jung},{jong}) = {result}, expected {expected}");
    }

    void TestDecompose(char input, int eCho, int eJung, int eJong)
    {
        var (cho, jung, jong) = HangulComposer.Decompose(input);
        Debug.Assert(cho == eCho && jung == eJung && jong == eJong,
            $"Decompose({input}): got ({cho},{jung},{jong}), expected ({eCho},{eJung},{eJong})");
    }
}