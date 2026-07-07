using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MascotControllerTests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    IEnumerator Start()
    {
        TestSpriteSwapMethods();
        yield return TestCardEventsDriveMascot();
        Cleanup();
        Debug.Log("All MascotController tests passed!");
    }

    Sprite MakeSprite()
    {
        var texture = new Texture2D(4, 4);
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    MascotController MakeMascot(out Image image, out Sprite idle, out Sprite reading)
    {
        var go = new GameObject("TestMascot");
        _spawned.Add(go);
        image = go.AddComponent<Image>();
        image.color = new Color(0.5f, 0.5f, 0.5f, 1f);

        idle = MakeSprite();
        reading = MakeSprite();

        var mascot = go.AddComponent<MascotController>();
        mascot.Configure(image, idle, reading);
        return mascot;
    }

    void TestSpriteSwapMethods()
    {
        MascotController mascot = MakeMascot(out Image image, out Sprite idle, out Sprite reading);

        mascot.ShowIdle();
        Assert(image.sprite == idle, "ShowIdle should apply the idle sprite");
        Assert(image.color == Color.white, "Applying a sprite should clear the placeholder tint");

        mascot.ShowReading();
        Assert(image.sprite == reading, "ShowReading should apply the reading sprite");
    }

    IEnumerator TestCardEventsDriveMascot()
    {
        MascotController mascot = MakeMascot(out Image image, out Sprite idle, out Sprite reading);

        var canvasGo = new GameObject("TestCanvas", typeof(RectTransform));
        _spawned.Add(canvasGo);
        MeaningCardUI card = MeaningCardUI.CreateRuntime(canvasGo.transform, null);
        mascot.WireTo(card);

        var entry = new WordEntry { word = "물", english = "water", romanization = "mul", example = "" };
        card.Show(entry);
        yield return null;

        Assert(image.sprite == reading, "Mascot should switch to reading while the card is shown");

        card.Hide();
        yield return null;

        Assert(image.sprite == idle, "Mascot should return to idle when the card hides");
    }

    void Cleanup()
    {
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
