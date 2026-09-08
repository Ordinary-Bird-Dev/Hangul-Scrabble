using System.Collections.Generic;
using TMPro;
using UnityEngine;

// NOTE: MonoBehaviour tests, not NUnit — they only run when this component is
// attached to a GameObject in a scene that is played.
//
// These exercise the direct-tap model: a jamo reaches a slot by tapping the
// TILE (TryAutoPlace), and tapping a SLOT only ever retracts what is already
// in it. There is no select-then-place step.
public class SyllableBuilderUITests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestAutoPlaceRoutesToChoSlot();
        TestInvalidJamoRejectedAndTileKept();
        TestTappingEmptySlotDoesNothing();
        TestTappingFilledSlotRetractsTile();
        TestConfirmFlowRaisesEventAndResets();
        TestConfirmWithoutJongseong();
        TestAutoConfirmAdvancesOnJong();
        Cleanup();
        Debug.Log("All SyllableBuilderUI tests passed!");
    }

    SyllableBuilderUI MakeBuilder()
    {
        GameObject cho = MakeSlotObject("TestChoSlot");
        GameObject jung = MakeSlotObject("TestJungSlot");
        GameObject jong = MakeSlotObject("TestJongSlot");

        var builderGo = new GameObject("TestSyllableBuilder");
        _spawned.Add(builderGo);
        var builder = builderGo.AddComponent<SyllableBuilderUI>();
        builder.Configure(cho, jung, jong);
        builder.Initialize();
        return builder;
    }

    GameObject MakeSlotObject(string name)
    {
        var root = new GameObject(name);
        _spawned.Add(root);
        var preview = new GameObject("PreviewText");
        preview.transform.SetParent(root.transform);
        preview.AddComponent<TextMeshProUGUI>();
        return root;
    }

    JamoTile MakeTile(string jamo)
    {
        var go = new GameObject($"TestTile_{jamo}");
        _spawned.Add(go);
        var tile = go.AddComponent<JamoTile>();
        tile.SetJamo(jamo);
        return tile;
    }

    TMP_Text Preview(GameObject slotRoot) =>
        slotRoot.transform.Find("PreviewText").GetComponent<TMP_Text>();

    void TestAutoPlaceRoutesToChoSlot()
    {
        SyllableBuilderUI builder = MakeBuilder();
        JamoTile tile = MakeTile("ㅎ");

        Assert(builder.TryAutoPlace(tile), "A consonant into an empty slot should be accepted");
        Assert(builder.Slot.State == SyllableSlot.SlotState.ChoPlaced,
            "ㅎ should land in the cho slot");
        Assert(tile.State == JamoTile.TileState.Consumed,
            "The placed tile should be consumed");
        Assert(builder.Slot.Cho == "ㅎ", "Slot should hold ㅎ as choseong");
    }

    void TestInvalidJamoRejectedAndTileKept()
    {
        SyllableBuilderUI builder = MakeBuilder();
        JamoTile tile = MakeTile("ㅏ");

        Assert(!builder.TryAutoPlace(tile), "A vowel has no legal home in an empty slot");
        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "A rejected placement should leave the slot Empty");
        Assert(tile.State == JamoTile.TileState.Normal,
            "A rejected tile should stay available, not be consumed");
    }

    void TestTappingEmptySlotDoesNothing()
    {
        SyllableBuilderUI builder = MakeBuilder();

        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);

        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Tapping an empty slot should do nothing — jamo arrive via tile taps");
    }

    void TestTappingFilledSlotRetractsTile()
    {
        SyllableBuilderUI builder = MakeBuilder();
        JamoTile tile = MakeTile("ㅎ");
        builder.TryAutoPlace(tile);

        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);

        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Tapping a filled slot should empty it");
        Assert(tile.State == JamoTile.TileState.Normal,
            "The retracted tile should return to the tray as Normal");
        Assert(tile.Jamo == "ㅎ", "The retracted tile should keep its jamo");
    }

    void TestConfirmFlowRaisesEventAndResets()
    {
        SyllableBuilderUI builder = MakeBuilder();

        string confirmed = null;
        builder.SyllableConfirmed += s => confirmed = s;

        builder.TryAutoPlace(MakeTile("ㅎ"));
        builder.TryAutoPlace(MakeTile("ㅏ"));
        builder.TryAutoPlace(MakeTile("ㄱ"));

        Assert(confirmed == null, "Syllable should not be confirmed before ConfirmSyllable (auto-confirm off)");

        builder.ConfirmSyllable();

        Assert(confirmed == "학", $"Confirmed syllable should be 학, got {confirmed}");
        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Slot should reset after confirming");
    }

    void TestConfirmWithoutJongseong()
    {
        SyllableBuilderUI builder = MakeBuilder();

        string confirmed = null;
        builder.SyllableConfirmed += s => confirmed = s;

        builder.TryAutoPlace(MakeTile("ㅇ"));
        builder.TryAutoPlace(MakeTile("ㅣ"));
        builder.ConfirmSyllable();

        Assert(confirmed == "이", $"Cho+jung confirm should produce 이, got {confirmed}");
    }

    void TestAutoConfirmAdvancesOnJong()
    {
        bool originalAutoConfirm = GameSettings.AutoConfirm;
        GameSettings.AutoConfirm = true;

        SyllableBuilderUI builder = MakeBuilder();
        string confirmed = null;
        builder.SyllableConfirmed += s => confirmed = s;

        builder.TryAutoPlace(MakeTile("ㅅ"));
        builder.TryAutoPlace(MakeTile("ㅏ"));
        builder.TryAutoPlace(MakeTile("ㄴ"));

        Assert(confirmed == "산", $"Auto-confirm should advance 산 on jong placement, got {confirmed}");
        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Slot should reset after auto-confirm");

        GameSettings.AutoConfirm = originalAutoConfirm;
    }

    void Cleanup()
    {
        foreach (GameObject go in _spawned)
            Destroy(go);
        _spawned.Clear();
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
