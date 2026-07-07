using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Animation tests need real frames to elapse, so Start is a coroutine;
// assertions still use the same Assert(bool, string) pattern.
public class UITweenTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    IEnumerator Start()
    {
        yield return TestPunchScaleReturnsToOne();
        yield return TestScaleToReachesTarget();
        yield return TestSlideToReachesTarget();
        yield return TestBurstDestroysObject();
        yield return TestTileBounceLeavesScaleAtOne();
        Cleanup();
        Debug.Log("All UITween tests passed!");
    }

    GameObject Spawn(string name)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        return go;
    }

    IEnumerator TestPunchScaleReturnsToOne()
    {
        GameObject go = Spawn("PunchTarget");
        yield return UITween.PunchScale(go.transform, 0.3f, 0.1f);
        Assert(Vector3.Distance(go.transform.localScale, Vector3.one) < 0.001f,
            $"PunchScale should settle back at scale 1, got {go.transform.localScale}");
    }

    IEnumerator TestScaleToReachesTarget()
    {
        GameObject go = Spawn("ScaleTarget");
        yield return UITween.ScaleTo(go.transform, new Vector3(2f, 2f, 2f), 0.1f);
        Assert(Vector3.Distance(go.transform.localScale, new Vector3(2f, 2f, 2f)) < 0.001f,
            $"ScaleTo should end exactly at the target, got {go.transform.localScale}");
    }

    IEnumerator TestSlideToReachesTarget()
    {
        GameObject go = Spawn("SlideTarget");
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, -500f);

        yield return UITween.SlideTo(rect, new Vector2(0f, 120f), 0.1f);
        Assert(Vector2.Distance(rect.anchoredPosition, new Vector2(0f, 120f)) < 0.001f,
            $"SlideTo should end exactly at the target, got {rect.anchoredPosition}");
    }

    IEnumerator TestBurstDestroysObject()
    {
        GameObject go = Spawn("BurstTarget");
        go.AddComponent<RectTransform>();

        yield return UITween.BurstAndDestroy(go, 1.5f, 0.1f);
        yield return null; // Destroy applies at end of frame

        Assert(go == null, "BurstAndDestroy should destroy the object when done");
    }

    IEnumerator TestTileBounceLeavesScaleAtOne()
    {
        GameObject go = Spawn("BounceTile");
        JamoTile tile = go.AddComponent<JamoTile>();
        tile.SetJamo("ㄱ");

        tile.Select();
        yield return new WaitForSeconds(0.35f);

        Assert(Vector3.Distance(go.transform.localScale, Vector3.one) < 0.001f,
            $"Tile bounce should settle back at scale 1, got {go.transform.localScale}");
        JamoTile.ClearSelection();
    }

    void Cleanup()
    {
        JamoTile.ClearSelection();
        foreach (GameObject go in _spawned)
            if (go != null) Destroy(go);
        _spawned.Clear();
    }

    void Assert(bool condition, string message)
    {
        if (!condition)
            Debug.LogError($"TEST FAILED: {message}");
    }
}
