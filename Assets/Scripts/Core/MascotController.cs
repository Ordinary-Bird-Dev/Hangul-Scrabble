using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Swaps the mascot between the idle monkey (during play) and the
// reading monkey (while the meaning card is up after a correct word).
// Sprites are assigned in the scene on the MascotImage object.
public class MascotController : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private Sprite _idleSprite;
    [SerializeField] private Sprite _readingSprite;

    private MeaningCardUI _card;

    void Start()
    {
        if (_image == null) _image = GetComponent<Image>();
        ShowIdle();
        StartCoroutine(WireRoutine());
    }

    void OnDestroy()
    {
        if (_card != null)
        {
            _card.Shown -= ShowReading;
            _card.Hidden -= ShowIdle;
        }
    }

    public void Configure(Image image, Sprite idle, Sprite reading)
    {
        _image = image;
        _idleSprite = idle;
        _readingSprite = reading;
    }

    // The meaning card is built at runtime by WordBuilder during Start,
    // so retry for a couple of seconds until it exists.
    private IEnumerator WireRoutine()
    {
        for (int i = 0; i < 120 && _card == null; i++)
        {
            TryWire();
            yield return null;
        }
    }

    public void WireTo(MeaningCardUI card)
    {
        if (card == null || _card == card) return;

        if (_card != null)
        {
            _card.Shown -= ShowReading;
            _card.Hidden -= ShowIdle;
        }

        _card = card;
        _card.Shown += ShowReading;
        _card.Hidden += ShowIdle;
    }

    private void TryWire()
    {
        MeaningCardUI card = FindAnyObjectByType<MeaningCardUI>(FindObjectsInactive.Include);
        if (card != null) WireTo(card);
    }

    public void ShowIdle()
    {
        ApplySprite(_idleSprite);
    }

    public void ShowReading()
    {
        ApplySprite(_readingSprite);
    }

    private void ApplySprite(Sprite sprite)
    {
        if (_image == null || sprite == null) return;
        _image.color = Color.white; // clear the grey placeholder tint
        _image.preserveAspect = true;
    }
}
