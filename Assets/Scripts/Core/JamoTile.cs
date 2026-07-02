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

    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Color _normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color _consumedColor = new Color(0.6f, 0.6f, 0.6f, 0.35f);

    public string Jamo { get; private set; } = "";
    public TileState State { get; private set; } = TileState.Normal;
    public bool IsConsumed => State == TileState.Consumed;

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
