using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Meaning card shown when a word is completed: slides up from below the
// screen, shows word / meaning / romanization / example, then dismisses
// after a few seconds or when tapped. Built at runtime so no scene
// wiring is needed (the MeaningCard object in GameScene is an empty
// non-UI placeholder).
public class MeaningCardUI : MonoBehaviour, IPointerClickHandler
{
    public event System.Action Shown;
    public event System.Action Hidden;

    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _showSeconds = 3f;
    [SerializeField] private float _slideSeconds = 0.35f;

    private RectTransform _rect;
    private Vector2 _homePosition;
    private bool _homeCaptured;
    private Coroutine _routine;

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
        bg.color = Palette.Surface;

        var textGo = new GameObject("CardText", typeof(RectTransform));
        textGo.transform.SetParent(cardGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 20f);
        textRect.offsetMax = new Vector2(-30f, -20f);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        // Explicit: the card is now a white Surface, so TMP's default white
        // would render the whole card blank.
        text.color = Palette.Ink;
        text.fontSize = 44f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; // taps land on the card background

        MeaningCardUI card = cardGo.AddComponent<MeaningCardUI>();
        card._text = text;
        cardGo.SetActive(false);
        return card;
    }

    void Awake()
    {
        _rect = transform as RectTransform;
    }

    public void SetShowSeconds(float seconds)
    {
        _showSeconds = seconds;
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

        if (_rect != null)
        {
            if (!_homeCaptured)
            {
                _homePosition = _rect.anchoredPosition;
                _homeCaptured = true;
            }

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine());
        }
        else
        {
            Shown?.Invoke();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HideAnimated();
    }

    // Slides the card back down, then deactivates it.
    public void HideAnimated()
    {
        if (!gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideRoutine());
    }

    // Immediate hide, no animation.
    public void Hide()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        if (_homeCaptured && _rect != null) _rect.anchoredPosition = _homePosition;

        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(false);
        if (wasActive) Hidden?.Invoke();
    }

    private IEnumerator ShowRoutine()
    {
        _rect.anchoredPosition = OffScreenPosition();
        Shown?.Invoke();

        yield return UITween.SlideTo(_rect, _homePosition, _slideSeconds);
        yield return new WaitForSeconds(_showSeconds);
        yield return HideRoutine();
    }

    private IEnumerator HideRoutine()
    {
        yield return UITween.SlideTo(_rect, OffScreenPosition(), _slideSeconds * 0.8f);

        _rect.anchoredPosition = _homePosition;
        _routine = null;
        gameObject.SetActive(false);
        Hidden?.Invoke();
    }

    private Vector2 OffScreenPosition()
    {
        float drop = _rect != null ? _rect.rect.height + 500f : 900f;
        return _homePosition - new Vector2(0f, drop);
    }
}
