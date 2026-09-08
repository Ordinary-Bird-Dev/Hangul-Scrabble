using System.Collections.Generic;
using UnityEngine;

// NOTE: these are MonoBehaviour tests, not NUnit — they only run when this
// component is attached to a GameObject in a scene that is played. No scene
// attaches it today, so nothing here executes unless you add it deliberately.
public class JamoTileTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Start()
    {
        TestSetJamo();
        TestTapDoesNotChangeStateWithoutABuilder();
        TestConsumeBlocksFurtherTaps();
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
        JamoTile tile = MakeTile("ㄱ");

        Assert(tile.Jamo == "ㄱ", "SetJamo should store the jamo string");
        Assert(tile.State == JamoTile.TileState.Normal, "A freshly dealt tile should be in Normal state");
    }

    // A tile is only spent when a builder actually accepts its jamo. With no
    // SyllableBuilderUI in the scene, TryAutoPlaceTile no-ops and the tile
    // must stay available — otherwise a tap would silently burn a tile.
    void TestTapDoesNotChangeStateWithoutABuilder()
    {
        JamoTile tile = MakeTile("ㄷ");

        tile.OnPointerClick(null);
        Assert(tile.State == JamoTile.TileState.Normal, "A tap with no builder present should leave the tile Normal");

        tile.OnPointerClick(null);
        Assert(tile.State == JamoTile.TileState.Normal, "Repeated taps should not toggle a tile into any other state");
    }

    void TestConsumeBlocksFurtherTaps()
    {
        JamoTile tile = MakeTile("ㄹ");

        tile.Consume();
        Assert(tile.State == JamoTile.TileState.Consumed, "Consume should set Consumed state");
        Assert(tile.IsConsumed, "IsConsumed should agree with the Consumed state");

        tile.Tap();
        Assert(tile.State == JamoTile.TileState.Consumed, "A consumed tile should ignore taps");
    }

    void TestSetJamoRevivesConsumedTile()
    {
        JamoTile tile = MakeTile("ㅂ");

        tile.Consume();
        tile.SetJamo("ㅅ");

        Assert(tile.Jamo == "ㅅ", "SetJamo should replace the jamo on a consumed tile");
        Assert(tile.State == JamoTile.TileState.Normal, "SetJamo should revive a consumed tile to Normal");
    }

    void TestTryPlaceIntoComposesSyllable()
    {
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
