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

        if (_confirmButton == null)
        {
            GameObject buttonGo = GameObject.Find("WordConfirmButton");
            if (buttonGo != null) _confirmButton = buttonGo.GetComponent<Button>();
        }
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(ConfirmSyllable);

        _initialized = true;
        UpdatePreviews();
    }

    public void OnSlotTapped(SlotRole role)
    {
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

        tile.Consume();
        UpdatePreviews();

        // With auto-confirm on, a fully completed syllable (jongseong placed)
        // advances immediately. A cho+jung syllable still waits for the
        // ConfirmButton, since the player may want to add a jongseong.
        if (GameSettings.AutoConfirm && Slot.State == SyllableSlot.SlotState.Complete)
            ConfirmSyllable();
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
        UpdatePreviews();
        SyllableConfirmed?.Invoke(syllable);
    }

    public void ResetSlot()
    {
        Slot.Reset();
        UpdatePreviews();
    }

    public void UpdatePreviews()
    {
        if (_choPreview != null) _choPreview.text = Slot.Cho;
        if (_jungPreview != null) _jungPreview.text = Slot.Jung;
        if (_jongPreview != null) _jongPreview.text = Slot.Jong;
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
