using UnityEngine;
using UnityEngine.UI;

// Makes a plain UI.Toggle look like an iOS-style pill switch: the track
// changes colour and the knob slides between two ends.
//
// Unity's Toggle can only show or hide one graphic for its on state, which
// is enough for a checkbox and not enough for a switch — a switch has to
// show something in BOTH states. So the knob stays visible and this drives
// its position and the track colour instead.
//
// Purely presentational: it never writes GameSettings. SettingsSceneController
// still owns what the toggle means. Attach it to the same GameObject as the
// Toggle and wire _track and _knob in the Inspector.
[RequireComponent(typeof(Toggle))]
public class PillToggleView : MonoBehaviour
{
    [SerializeField] private Image _track;
    [SerializeField] private RectTransform _knob;

    // Inset from the track's edge to the knob's edge, both ends.
    [SerializeField] private float _knobInset = 8f;

    [SerializeField] private Color _onColor = Palette.Action;
    [SerializeField] private Color _offColor = Palette.TrackOff;

    private Toggle _toggle;

    void Awake()
    {
        _toggle = GetComponent<Toggle>();
        if (_track == null) Debug.LogWarning($"PillToggleView on '{name}': _track is unassigned — the switch will not change colour.");
        if (_knob == null) Debug.LogWarning($"PillToggleView on '{name}': _knob is unassigned — the switch will not slide.");
    }

    void OnEnable()
    {
        if (_toggle == null) return;
        _toggle.onValueChanged.AddListener(Apply);
        // Apply on enable, not only on change: SettingsSceneController sets
        // isOn from PlayerPrefs during Start, and a value that was already
        // correct raises no onValueChanged, which would leave the knob
        // sitting at the wrong end.
        Apply(_toggle.isOn);
    }

    void OnDisable()
    {
        if (_toggle != null) _toggle.onValueChanged.RemoveListener(Apply);
    }

    public void Apply(bool isOn)
    {
        if (_track != null) _track.color = isOn ? _onColor : _offColor;

        if (_knob == null) return;

        // Anchor the knob to the track's left or right edge and inset it, so
        // the travel distance follows the track's width instead of being a
        // hardcoded number that breaks when the track is resized.
        float pivotX = isOn ? 1f : 0f;
        _knob.anchorMin = new Vector2(pivotX, 0.5f);
        _knob.anchorMax = new Vector2(pivotX, 0.5f);
        _knob.pivot = new Vector2(pivotX, 0.5f);
        _knob.anchoredPosition = new Vector2(isOn ? -_knobInset : _knobInset, 0f);
    }

    // Lets the Inspector preview both states without entering Play mode.
    void OnValidate()
    {
        Toggle toggle = GetComponent<Toggle>();
        if (toggle != null) Apply(toggle.isOn);
    }
}
