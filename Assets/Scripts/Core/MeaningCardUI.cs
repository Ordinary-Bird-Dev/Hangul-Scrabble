using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Simple meaning card shown when a word is completed. Built at runtime
// so no scene wiring is needed; Phase 3 replaces this with the animated
// slide-up panel.
public class MeaningCardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _showSeconds = 3f;

    private Coroutine _hideRoutine;

    // Builds a card as a child of the given canvas transform, reusing the
    // font of an existing TMP text in the scene so Korean glyphs render.
    public static MeaningCardUI CreateRuntime(Transform canvasParent, TMP_FontAsset font)
    {
        var cardGo = new GameObject("MeaningCard", typeof(RectTransform));
        cardGo.transform.SetParent(canvasParent, false);

        var rect = (RectTransform)cardGo.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 340f);
        rect.sizeDelta = new Vector2(900f, 320f);

        Image bg = cardGo.AddComponent<Image>();
        bg.color = new Color(0.13f, 0.13f, 0.2f, 0.95f);

        var textGo = new GameObject("CardText", typeof(RectTransform));
        textGo.transform.SetParent(cardGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 20f);
        textRect.offsetMax = new Vector2(-30f, -20f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = 44f;
        text.alignment = TextAlignmentOptions.Center;

        MeaningCardUI card = cardGo.AddComponent<MeaningCardUI>();
        card._text = text;
        cardGo.SetActive(false);
        return card;
    }

    public void Show(WordEntry entry)
    {
        if (entry == null) return;

        if (_text != null)
        {
            string example = string.IsNullOrEmpty(entry.example) ? "" : $"\n<i><size=70%>{entry.example}</size></i>";
            _text.text = $"<b>{entry.word}</b>  <size=60%>({entry.romanization})</size>\n{entry.english}{example}";
        }

        gameObject.SetActive(true);

        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public void Hide()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
        gameObject.SetActive(false);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(_showSeconds);
        _hideRoutine = null;
        gameObject.SetActive(false);
    }
}
