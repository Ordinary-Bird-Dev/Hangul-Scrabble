using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JamoTile : MonoBehaviour, IPointerClickHandler
{
    public enum TileState
    {
        Normal,
        Selected,
        Consumed
    }

    // Tap-to-select: only one tile may be selected at a time.
    private static JamoTile _currentlySelected;
    private static Animator _mascotAnimator;
    private const string MascotTileSelectTrigger = "TileSelect";

    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Color _normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _consumedColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);

    public string Jamo { get; private set; } = "";
    public TileState State { get; private set; } = TileState.Normal;
    public bool IsConsumed => State == TileState.Consumed;

    private Coroutine _bounceRoutine;

    void Awake()
    {
        if (_background == null) _background = GetComponent<Image>();
        if (_label == null) _label = GetComponentInChildren<TextMeshProUGUI>();
        ApplyVisuals();
    }

    void OnDestroy()
    {
        if (_currentlySelected == this)
            _currentlySelected = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (State == TileState.Consumed) return;

        if (State == TileState.Selected)
            Deselect();
        else
            Select();
    }

    public void Select()
    {
        if (State == TileState.Consumed) return;

        if (_currentlySelected != null && _currentlySelected != this)
            _currentlySelected.Deselect();

        _currentlySelected = this;
        State = TileState.Selected;
        ApplyVisuals();
        PlayBounce();
        AudioManager.TryPlayTileTap();
        TriggerMascotTileSelect();
        MascotSleepController.TryRegisterTileTap(); // add this line
    }

    

    public void Deselect()
    {
        if (_currentlySelected == this)
            _currentlySelected = null;

        if (State == TileState.Selected)
        {
            State = TileState.Normal;
            ApplyVisuals();
        }
    }

    public void Consume()
    {
        if (_currentlySelected == this)
            _currentlySelected = null;

        State = TileState.Consumed;
        ApplyVisuals();
    }

    // Classic (guided): blanks an unused tray slot. The tile keeps its place in
    // the fixed 7x2 grid but renders greyed with no label and ignores taps.
    public void SetEmpty()
    {
        Jamo = "";

        if (_currentlySelected == this)
            _currentlySelected = null;

        State = TileState.Consumed;
        if (_label != null) _label.text = "";
        ApplyVisuals();
    }

    public void SetJamo(string jamo, TextMeshProUGUI label)
    {
        _label = label;
        SetJamo(jamo);
    }

    // Dealing a jamo also revives a consumed tile back to Normal,
    // so TileManager can reuse the same tile instances when refilling.
    public void SetJamo(string jamo)
    {
        Jamo = jamo;

        if (_currentlySelected == this)
            _currentlySelected = null;

        State = TileState.Normal;
        if (_label != null) _label.text = jamo;
        ApplyVisuals();
    }

    // Places this tile's jamo into the next open position of the slot's
    // state machine (cho -> jung -> jong) and consumes the tile on success.
    public bool TryPlaceInto(SyllableSlot slot)
    {
        if (slot == null || State == TileState.Consumed) return false;

        bool placed;
        switch (slot.State)
        {
            case SyllableSlot.SlotState.Empty:
                placed = slot.TryPlaceCho(Jamo);
                break;
            case SyllableSlot.SlotState.ChoPlaced:
                placed = slot.TryPlaceJung(Jamo);
                break;
            case SyllableSlot.SlotState.ChoJungPlaced:
                placed = slot.TryPlaceJong(Jamo);
                break;
            default:
                placed = false;
                break;
        }

        if (placed) Consume();
        return placed;
    }

    public static JamoTile GetSelectedTile() => _currentlySelected;

    public static void ClearSelection()
    {
        if (_currentlySelected != null)
            _currentlySelected.Deselect();
    }

    // Tap-to-select bounce. Scale is reset first so rapid taps never drift.
    private void PlayBounce()
    {
        if (!isActiveAndEnabled) return;
        if (_bounceRoutine != null) StopCoroutine(_bounceRoutine);
        transform.localScale = Vector3.one;
        _bounceRoutine = StartCoroutine(UITween.PunchScale(transform, 0.15f, 0.2f));
    }

    private static void TriggerMascotTileSelect()
    {
        if (_mascotAnimator == null)
        {
            GameObject mascot = GameObject.Find("MascotImage");
            if (mascot != null) _mascotAnimator = mascot.GetComponent<Animator>();
        }
        if (_mascotAnimator != null && _mascotAnimator.runtimeAnimatorController != null)
            _mascotAnimator.SetTrigger(MascotTileSelectTrigger);
    }

    private void ApplyVisuals()
    {
        if (_background == null) return;

        switch (State)
        {
            case TileState.Selected:
                _background.color = _selectedColor;
                break;
            case TileState.Consumed:
                _background.color = _consumedColor;
                break;
            default:
                _background.color = _normalColor;
                break;
        }

        _background.raycastTarget = State != TileState.Consumed;
    }
}
