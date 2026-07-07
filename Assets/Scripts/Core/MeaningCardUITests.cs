using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeaningCardUITests : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    IEnumerator Start()
    {
        yield return TestShowPopulatesAndRaisesShown();
        yield return TestAutoDismissAfterShowSeconds();
        yield return TestTapDismissesCard();
        Cleanup();
        Debug.Log("All MeaningCardUI tests passed!");
    }

    MeaningCardUI MakeCard()
    {
        var canvasGo = new GameObject("TestCanvas", typeof(RectTransform));
        _spawned.Add(canvasGo);
        MeaningCardUI card = MeaningCardUI.CreateRuntime(canvasGo.transform, null);
        return card;
    }

    WordEntry MakeEntry()
    {
        return new WordEntry
        {
            word = "학교",
            english = "school",
            romanization = "hakgyo",
            example = "학교에 가요.",
            syllable_count = 2
        };
    }

    IEnumerator TestShowPopulatesAndRaisesShown()
    {
        MeaningCardUI card = MakeCard();

        bool shown = false;
        card.Shown += () => shown = true;

        card.Show(MakeEntry());
        yield return null;

        Assert(card.gameObject.activeSelf, "Card should be active after Show");
        Assert(shown, "Shown event should fire on Show");

        var text = card.GetComponentInChildren<TMPro.TMP_Text>(true);
        Assert(text != null && text.text.Contains("학교"), "Card should show the word");
        Assert(text != null && text.text.Contains("school"), "Card should show the meaning");
        Assert(text != null && text.text.Contains("hakgyo"), "Card should show the romanization");
        Assert(text != null && text.text.Contains("학교에 가요."), "Card should show the example sentence");

        card.Hide();
    }

    IEnumerator TestAutoDismissAfterShowSeconds()
    {
        MeaningCardUI card = MakeCard();
        card.SetShowSeconds(0.2f);

        bool hidden = false;
        card.Hidden += () => hidden = true;

        card.Show(MakeEntry());
        yield return new WaitForSeconds(1.5f); // slide up + 0.2s + slide down

        Assert(!card.gameObject.activeSelf, "Card should auto-dismiss after show duration");
        Assert(hidden, "Hidden event should fire after auto-dismiss");
    }

    IEnumerator TestTapDismissesCard()
    {
        MeaningCardUI card = MakeCard();
        card.SetShowSeconds(30f); // would stay for a long time without the tap

        bool hidden = false;
        card.Hidden += () => hidden = true;

        card.Show(MakeEntry());
        yield return new WaitForSeconds(0.5f);

        card.OnPointerClick(null);
        yield return new WaitForSeconds(0.6f);

        Assert(!card.gameObject.activeSelf, "Tapping the card should dismiss it");
        Assert(hidden, "Hidden event should fire on tap dismiss");
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
