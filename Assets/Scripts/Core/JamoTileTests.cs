using System.Collections.Generic;
using UnityEngine;

public class JamoTileTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestSetJamo();
        TestSelectEnforcesSingleSelection();
        TestTapTogglesSelection();
        TestConsumeClearsSelectionAndBlocksReselect();
        TestClearSelection();
        TestSetJamoRevivesConsumedTile();
        TestTryPlaceIntoComposesSyllable();
        TestTryPlaceIntoRejectsInvalidJamo();
        Cleanup();
        Debug.Log("All JamoTile tests passed!");
    }

    JamoTile MakeTile(string jamo)
    {
        var go = new GameObject($"TestTile_{jamo}");
        _spawned.Add(go);
        var tile = go.AddComponent<JamoTile>();
        tile.SetJamo(jamo);
        return tile;
    }

    void TestSetJamo()
    {
        JamoTile.ClearSelection();
        JamoTile tile = MakeTile("ㄱ");

        Assert(tile.Jamo == "ㄱ", "SetJamo should store the jamo string");
        Assert(tile.State == JamoTile.TileState.Normal, "A freshly dealt tile should be in Normal state");
    }

    void TestSelectEnforcesSingleSelection()
    {
        JamoTile.ClearSelection();
        JamoTile a = MakeTile("ㄴ");
        JamoTile b = MakeTile("ㅏ");

        a.Select();
        Assert(JamoTile.GetSelectedTile() == a, "Tile A should be the selected tile after Select()");

        b.Select();
        Assert(JamoTile.GetSelectedTile() == b, "Selecting tile B should replace tile A as selection");
        Assert(a.State == JamoTile.TileState.Normal, "Tile A should return to Normal when B is selected");
        Assert(b.State == JamoTile.TileState.Selected, "Tile B should be in Selected state");
    }

    void TestTapTogglesSelection()
    {
        JamoTile.ClearSelection();
        JamoTile tile = MakeTile("ㄷ");

        tile.OnPointerClick(null);
        Assert(tile.State == JamoTile.TileState.Selected, "First tap should select the tile");

        tile.OnPointerClick(null);
        Assert(tile.State == JamoTile.TileState.Normal, "Second tap should deselect the tile");
        Assert(JamoTile.GetSelectedTile() == null, "No tile should be selected after toggling off");
    }

    void TestConsumeClearsSelectionAndBlocksReselect()
    {
        JamoTile.ClearSelection();
        JamoTile tile = MakeTile("ㄹ");

        tile.Select();
        tile.Consume();

        Assert(tile.State == JamoTile.TileState.Consumed, "Consume should set Consumed state");
        Assert(JamoTile.GetSelectedTile() == null, "Consuming the selected tile should clear the selection");

        tile.Select();
        Assert(tile.State == JamoTile.TileState.Consumed, "A consumed tile should refuse Select()");

        tile.OnPointerClick(null);
        Assert(JamoTile.GetSelectedTile() == null, "Tapping a consumed tile should not select it");
    }

    void TestClearSelection()
    {
        JamoTile.ClearSelection();
        JamoTile tile = MakeTile("ㅁ");

        tile.Select();
        JamoTile.ClearSelection();

        Assert(JamoTile.GetSelectedTile() == null, "ClearSelection should leave no tile selected");
        Assert(tile.State == JamoTile.TileState.Normal, "ClearSelection should return the tile to Normal");
    }

    void TestSetJamoRevivesConsumedTile()
    {
        JamoTile.ClearSelection();
        JamoTile tile = MakeTile("ㅂ");

        tile.Consume();
        tile.SetJamo("ㅅ");

        Assert(tile.Jamo == "ㅅ", "SetJamo should replace the jamo on a consumed tile");
        Assert(tile.State == JamoTile.TileState.Normal, "SetJamo should revive a consumed tile to Normal");
    }

    void TestTryPlaceIntoComposesSyllable()
    {
        JamoTile.ClearSelection();
        var slotGo = new GameObject("TestSlot");
        _spawned.Add(slotGo);
        SyllableSlot slot = slotGo.AddComponent<SyllableSlot>();

        JamoTile cho = MakeTile("ㅎ");
        JamoTile jung = MakeTile("ㅏ");
        JamoTile jong = MakeTile("ㄱ");

        Assert(cho.TryPlaceInto(slot), "Placing ㅎ into an empty slot should succeed as choseong");
        Assert(cho.State == JamoTile.TileState.Consumed, "A placed tile should be consumed");
        Assert(jung.TryPlaceInto(slot), "Placing ㅏ after cho should succeed as jungseong");
        Assert(jong.TryPlaceInto(slot), "Placing ㄱ after cho+jung should succeed as jongseong");
        Assert(slot.CurrentSyllable == "학", $"Slot should compose 학, got {slot.CurrentSyllable}");

        Assert(!jong.TryPlaceInto(slot), "A consumed tile should refuse to place again");
    }

    void TestTryPlaceIntoRejectsInvalidJamo()
    {
        JamoTile.ClearSelection();
        var slotGo = new GameObject("TestSlot2");
        _spawned.Add(slotGo);
        SyllableSlot slot = slotGo.AddComponent<SyllableSlot>();

        JamoTile vowel = MakeTile("ㅏ");
        Assert(!vowel.TryPlaceInto(slot), "A vowel should be rejected as choseong in an empty slot");
        Assert(vowel.State == JamoTile.TileState.Normal, "A rejected tile should not be consumed");
        Assert(slot.State == SyllableSlot.SlotState.Empty, "Slot should stay Empty after rejected placement");
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
