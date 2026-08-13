using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Bridges the SyllableSlot state machine to the on-screen
// ChoSlot / Jungslot / Jongslot objects in SyllableBuilder.
// Tapping a slot places the currently selected JamoTile into that
// position, and each slot's PreviewText updates live.
public class SyllableBuilderUI : MonoBehaviour
{
    public enum SlotRole { Cho, Jung, Jong }

    public event System.Action<string> SyllableConfirmed;

    [SerializeField] private GameObject _choSlotRoot;
    [SerializeField] private GameObject _jungSlotRoot;
    [SerializeField] private GameObject _jongSlotRoot;
    [SerializeField] private Button _confirmButton;

    private TMP_Text _choPreview;
    private TMP_Text _jungPreview;
    private TMP_Text _jongPreview;

    private struct PlacedTile
    {
        public JamoTile Tile;
        public string Jamo;
    }

    private PlacedTile? _choPlaced;
    private PlacedTile? _jungPlaced;
    private PlacedTile? _jongPlaced;

    public SyllableSlot Slot { get; private set; }

    private bool _initialized;

    void Awake()
    {
        Slot = GetComponent<SyllableSlot>();
        if (Slot == null) Slot = gameObject.AddComponent<SyllableSlot>();
    }

    void Start()
    {
        Initialize();
    }

    public void Configure(GameObject choRoot, GameObject jungRoot, GameObject jongRoot, Button confirmButton = null)
    {
        _choSlotRoot = choRoot;
        _jungSlotRoot = jungRoot;
        _jongSlotRoot = jongRoot;
        _confirmButton = confirmButton;
    }

    public void Initialize()
    {
        if (_initialized) return;

        if (_choSlotRoot == null) _choSlotRoot = FindSlotObject("ChoSlot");
        if (_jungSlotRoot == null) _jungSlotRoot = FindSlotObject("Jungslot", "JungSlot");
        if (_jongSlotRoot == null) _jongSlotRoot = FindSlotObject("Jongslot", "JongSlot");

        _choPreview = FindPreview(_choSlotRoot);
        _jungPreview = FindPreview(_jungSlotRoot);
        _jongPreview = FindPreview(_jongSlotRoot);

        AttachTapTarget(_choSlotRoot, SlotRole.Cho);
        AttachTapTarget(_jungSlotRoot, SlotRole.Jung);
        AttachTapTarget(_jongSlotRoot, SlotRole.Jong);

        // No silent name-based fallback: an unassigned button previously
        // grabbed WordConfirmButton and collided with WordBuilder's
        // confirm. A misconfiguration should fail loudly instead.
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(ConfirmSyllable);
        else
            Debug.LogWarning("SyllableBuilderUI: _confirmButton is not assigned — syllable confirm is disabled until a Button is wired via the Inspector or Configure().");

        _initialized = true;
        UpdatePreviews();
    }

    // Places the currently selected tile into whichever slot comes next
    // (cho -> jung -> jong), so the player doesn't need to tap the
    // specific slot box directly. Not wired to any button right now —
    // kept for a future tile-place button. (The scene object once named
    // TileSelectorButton was really the word confirm button and has been
    // renamed WordConfirmButton.)
    public void PlaceSelectedTile()
    {
        // Loud no-ops: a press that does nothing should say why.
        if (JamoTile.GetSelectedTile() == null)
        {
            Debug.LogWarning("SyllableBuilderUI: PlaceSelectedTile called with no tile selected.");
            return;
        }
        if (Slot.State == SyllableSlot.SlotState.Complete)
        {
            Debug.LogWarning("SyllableBuilderUI: PlaceSelectedTile called but the syllable is already complete — confirm or reset it first.");
            return;
        }

        SlotRole nextRole = Slot.State switch
        {
            SyllableSlot.SlotState.Empty => SlotRole.Cho,
            SyllableSlot.SlotState.ChoPlaced => SlotRole.Jung,
            SyllableSlot.SlotState.ChoJungPlaced => SlotRole.Jong,
            _ => SlotRole.Cho
        };

        OnSlotTapped(nextRole);
    }

    public void OnSlotTapped(SlotRole role)
    {
        if (IsRoleFilled(role))
        {
            RetractRole(role);
            return;
        }

        JamoTile tile = JamoTile.GetSelectedTile();
        if (tile == null) return;

        bool placed;
        switch (role)
        {
            case SlotRole.Cho:
                placed = Slot.TryPlaceCho(tile.Jamo);
                break;
            case SlotRole.Jung:
                placed = Slot.TryPlaceJung(tile.Jamo);
                break;
            case SlotRole.Jong:
                placed = Slot.TryPlaceJong(tile.Jamo);
                break;
            default:
                placed = false;
                break;
        }

        if (!placed) return;

        var placedTile = new PlacedTile { Tile = tile, Jamo = tile.Jamo };
        switch (role)
        {
            case SlotRole.Cho: _choPlaced = placedTile; break;
            case SlotRole.Jung: _jungPlaced = placedTile; break;
            case SlotRole.Jong: _jongPlaced = placedTile; break;
        }

        tile.Consume();
        UpdatePreviews();
        PulseSlot(role);

        if (GameSettings.AutoConfirm && Slot.State == SyllableSlot.SlotState.Complete)
            ConfirmSyllable();
    }

    private bool IsRoleFilled(SlotRole role)
    {
        switch (role)
        {
            case SlotRole.Cho: return Slot.Cho != "";
            case SlotRole.Jung: return Slot.Jung != "";
            case SlotRole.Jong: return Slot.Jong != "";
            default: return false;
        }
    }

    // Only the topmost placed jamo can be retracted — SyllableSlot's
    // TryRemove* methods enforce that same ordering, so an out-of-order
    // tap (e.g. Cho while Jong is still placed) is just a silent no-op.
    private void RetractRole(SlotRole role)
    {
        bool removed;
        PlacedTile? placed;
        switch (role)
        {
            case SlotRole.Jong:
                removed = Slot.TryRemoveJong();
                placed = _jongPlaced;
                _jongPlaced = null;
                break;
            case SlotRole.Jung:
                removed = Slot.TryRemoveJung();
                placed = _jungPlaced;
                _jungPlaced = null;
                break;
            case SlotRole.Cho:
                removed = Slot.TryRemoveCho();
                placed = _choPlaced;
                _choPlaced = null;
                break;
            default:
                removed = false;
                placed = null;
                break;
        }

        if (!removed) return;

        if (placed.HasValue && placed.Value.Tile != null)
            placed.Value.Tile.SetJamo(placed.Value.Jamo);

        UpdatePreviews();
        PulseSlot(role);
    }

    // ConfirmButton handler: completes a cho+jung syllable if needed,
    // hands the composed syllable to listeners, and clears the slot.
    public void ConfirmSyllable()
    {
        if (Slot.State == SyllableSlot.SlotState.ChoJungPlaced)
            Slot.TryComplete();

        if (Slot.State != SyllableSlot.SlotState.Complete) return;

        string syllable = Slot.CurrentSyllable;
        Slot.Reset();
        _choPlaced = null;
        _jungPlaced = null;
        _jongPlaced = null;
        UpdatePreviews();
        AudioManager.TryPlaySyllableComplete();
        SyllableConfirmed?.Invoke(syllable);
    }

    public void ResetSlot()
    {
        RestorePlaced(ref _choPlaced);
        RestorePlaced(ref _jungPlaced);
        RestorePlaced(ref _jongPlaced);
        Slot.Reset();
        UpdatePreviews();
    }

    private void RestorePlaced(ref PlacedTile? placed)
    {
        if (placed.HasValue && placed.Value.Tile != null)
            placed.Value.Tile.SetJamo(placed.Value.Jamo);
        placed = null;
    }

    public void UpdatePreviews()
    {
        if (_choPreview != null) _choPreview.text = Slot.Cho;
        if (_jungPreview != null) _jungPreview.text = Slot.Jung;
        if (_jongPreview != null) _jongPreview.text = Slot.Jong;
    }

    // Compose feedback: the slot that received a jamo pulses briefly.
    private void PulseSlot(SlotRole role)
    {
        if (!isActiveAndEnabled) return;

        GameObject root;
        switch (role)
        {
            case SlotRole.Cho: root = _choSlotRoot; break;
            case SlotRole.Jung: root = _jungSlotRoot; break;
            default: root = _jongSlotRoot; break;
        }
        if (root == null) return;

        root.transform.localScale = Vector3.one;
        StartCoroutine(UITween.PunchScale(root.transform, 0.12f, 0.22f));
    }

    private void AttachTapTarget(GameObject root, SlotRole role)
    {
        if (root == null)
        {
            Debug.LogWarning($"SyllableBuilderUI: no GameObject wired for {role} slot.");
            return;
        }

        SlotTapTarget target = root.GetComponent<SlotTapTarget>();
        if (target == null) target = root.AddComponent<SlotTapTarget>();
        target.Bind(this, role);
    }

    private static GameObject FindSlotObject(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) return found;
        }
        return null;
    }

    private static TMP_Text FindPreview(GameObject root)
    {
        if (root == null) return null;

        Transform preview = root.transform.Find("PreviewText");
        if (preview != null) return preview.GetComponent<TMP_Text>();

        return root.GetComponentInChildren<TMP_Text>(true);
    }
}

// Attached at runtime to each slot GameObject; forwards taps to the builder.
public class SlotTapTarget : MonoBehaviour, IPointerClickHandler
{
    private SyllableBuilderUI _owner;
    private SyllableBuilderUI.SlotRole _role;

    public void Bind(SyllableBuilderUI owner, SyllableBuilderUI.SlotRole role)
    {
        _owner = owner;
        _role = role;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_owner != null) _owner.OnSlotTapped(_role);
    }
}
