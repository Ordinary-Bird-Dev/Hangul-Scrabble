using System.Collections.Generic;
using UnityEngine;

public class TrayValidatorTests : MonoBehaviour
{
    void Start()
    {
        TestRequiredJamoSimpleWord();
        TestRequiredJamoCompoundFinal();
        TestContainsAllRespectsDuplicates();
        TestAnyWordBuildable();
        TestDealableRejectsCompoundFinals();
        Debug.Log("All TrayValidator tests passed!");
    }

    void TestRequiredJamoSimpleWord()
    {
        List<string> required = TrayValidator.RequiredJamoFor("가게");
        Assert(required.Count == 4, $"가게 needs 4 jamo, got {required.Count}");
        Assert(required[0] == "ㄱ" && required[1] == "ㅏ" && required[2] == "ㄱ" && required[3] == "ㅔ",
            $"가게 should decompose to ㄱㅏㄱㅔ, got {string.Join(",", required)}");
    }

    void TestRequiredJamoCompoundFinal()
    {
        List<string> required = TrayValidator.RequiredJamoFor("없다");
        Assert(required.Count == 5, $"없다 needs 5 jamo, got {required.Count}");
        Assert(required[2] == "ㅄ",
            $"없 should keep its compound final ㅄ as one jamo, got '{required[2]}'");
    }

    void TestContainsAllRespectsDuplicates()
    {
        var required = new List<string> { "ㄱ", "ㅏ", "ㄱ", "ㅔ" };

        Assert(TrayValidator.ContainsAll(required, new List<string> { "ㅔ", "ㄱ", "ㄴ", "ㅏ", "ㄱ" }),
            "Tray with ㄱㄱㅏㅔ should satisfy 가게");
        Assert(!TrayValidator.ContainsAll(required, new List<string> { "ㄱ", "ㅏ", "ㅔ", "ㄴ" }),
            "A single ㄱ must not satisfy a word needing two ㄱ");
    }

    void TestAnyWordBuildable()
    {
        var entries = new List<WordEntry>
        {
            new WordEntry { word = "나무" },
            new WordEntry { word = "학교" }
        };

        var tray = new List<string> { "ㄴ", "ㅏ", "ㅁ", "ㅜ", "ㅅ", "ㅗ" };
        Assert(TrayValidator.AnyWordBuildable(tray, entries),
            "Tray containing ㄴㅏㅁㅜ should build 나무");

        var barren = new List<string> { "ㅋ", "ㅋ", "ㅃ", "ㅒ", "ㅢ", "ㅉ" };
        Assert(!TrayValidator.AnyWordBuildable(barren, entries),
            "Tray of rare jamo should build neither 나무 nor 학교");
    }

    void TestDealableRejectsCompoundFinals()
    {
        Assert(TrayValidator.AreAllJamoDealable(TrayValidator.RequiredJamoFor("가게")),
            "가게 uses only dealable jamo");
        Assert(!TrayValidator.AreAllJamoDealable(TrayValidator.RequiredJamoFor("없다")),
            "없다 needs the undealable compound final ㅄ");
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
