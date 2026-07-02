using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SyllableBuilderUITests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestTapPlacesSelectedTile();
        TestInvalidJamoRejectedAndTileKept();
        TestNoSelectionDoesNothing();
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

    JamoTile MakeSelectedTile(string jamo)
    {
        var go = new GameObject($"TestTile_{jamo}");
        _spawned.Add(go);
        var tile = go.AddComponent<JamoTile>();
        tile.SetJamo(jamo);
        tile.Select();
        return tile;
    }

    TMP_Text Preview(GameObject slotRoot) =>
        slotRoot.transform.Find("PreviewText").GetComponent<TMP_Text>();

    void TestTapPlacesSelectedTile()
    {
        JamoTile.ClearSelection();
        SyllableBuilderUI builder = MakeBuilder();
        JamoTile tile = MakeSelectedTile("ㅎ");

        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);

        Assert(builder.Slot.State == SyllableSlot.SlotState.ChoPlaced,
            "Tapping the cho slot with ㅎ selected should place it");
        Assert(tile.State == JamoTile.TileState.Consumed,
            "The placed tile should be consumed");
        Assert(builder.Slot.Cho == "ㅎ", "Slot should hold ㅎ as choseong");
    }

    void TestInvalidJamoRejectedAndTileKept()
    {
        JamoTile.ClearSelection();
        SyllableBuilderUI builder = MakeBuilder();
        JamoTile tile = MakeSelectedTile("ㅏ");

        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);

        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "A vowel tapped into the cho slot should be rejected");
        Assert(tile.State == JamoTile.TileState.Selected,
            "A rejected tile should stay selected, not consumed");
    }

    void TestNoSelectionDoesNothing()
    {
        JamoTile.ClearSelection();
        SyllableBuilderUI builder = MakeBuilder();

        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);

        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Tapping a slot with no tile selected should do nothing");
    }

    void TestConfirmFlowRaisesEventAndResets()
    {
        JamoTile.ClearSelection();
        SyllableBuilderUI builder = MakeBuilder();

        string confirmed = null;
        builder.SyllableConfirmed += s => confirmed = s;

        MakeSelectedTile("ㅎ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);
        MakeSelectedTile("ㅏ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Jung);
        MakeSelectedTile("ㄱ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Jong);

        Assert(confirmed == null, "Syllable should not be confirmed before ConfirmSyllable (auto-confirm off)");

        builder.ConfirmSyllable();

        Assert(confirmed == "학", $"Confirmed syllable should be 학, got {confirmed}");
        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Slot should reset after confirming");
    }

    void TestConfirmWithoutJongseong()
    {
        JamoTile.ClearSelection();
        SyllableBuilderUI builder = MakeBuilder();

        string confirmed = null;
        builder.SyllableConfirmed += s => confirmed = s;

        MakeSelectedTile("ㅇ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);
        MakeSelectedTile("ㅣ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Jung);
        builder.ConfirmSyllable();

        Assert(confirmed == "이", $"Cho+jung confirm should produce 이, got {confirmed}");
    }

    void TestAutoConfirmAdvancesOnJong()
    {
        JamoTile.ClearSelection();
        bool originalAutoConfirm = GameSettings.AutoConfirm;
        GameSettings.AutoConfirm = true;

        SyllableBuilderUI builder = MakeBuilder();
        string confirmed = null;
        builder.SyllableConfirmed += s => confirmed = s;

        MakeSelectedTile("ㅅ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Cho);
        MakeSelectedTile("ㅏ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Jung);
        MakeSelectedTile("ㄴ");
        builder.OnSlotTapped(SyllableBuilderUI.SlotRole.Jong);

        Assert(confirmed == "산", $"Auto-confirm should advance 산 on jong placement, got {confirmed}");
        Assert(builder.Slot.State == SyllableSlot.SlotState.Empty,
            "Slot should reset after auto-confirm");

        GameSettings.AutoConfirm = originalAutoConfirm;
    }

    void Cleanup()
    {
        JamoTile.ClearSelection();
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
