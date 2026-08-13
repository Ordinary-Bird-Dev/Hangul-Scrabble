using UnityEngine;

public class SyllableSlot : MonoBehaviour
{
    public enum SlotState
    {
        Empty,
        ChoPlaced,
        ChoJungPlaced,
        Complete
    }

    public SlotState State { get; private set; } = SlotState.Empty;

    private string _cho = "";
    private string _jung = "";
    private string _jong = "";

    public string CurrentSyllable { get; private set; } = null;

    // Read-only access to the placed jamo so UI previews can render
    // each position live as tiles are placed.
    public string Cho => _cho;
    public string Jung => _jung;
    public string Jong => _jong;

    public bool TryPlaceCho(string jamo)
    {
        if (State != SlotState.Empty)
        {
            Debug.LogWarning("SyllableSlot: cannot place cho — slot not empty.");
            return false;
        }
        if (!HangulComposer.IsValidChoseong(jamo))
        {
            Debug.LogWarning($"SyllableSlot: '{jamo}' is not a valid choseong.");
            return false;
        }
        _cho = jamo;
        State = SlotState.ChoPlaced;
        UpdatePreview();
        return true;
    }

    public bool TryPlaceJung(string jamo)
    {
        if (State != SlotState.ChoPlaced)
        {
            Debug.LogWarning("SyllableSlot: cannot place jung — cho not placed yet.");
            return false;
        }
        if (!HangulComposer.IsValidJungseong(jamo))
        {
            Debug.LogWarning($"SyllableSlot: '{jamo}' is not a valid jungseong.");
            return false;
        }
        _jung = jamo;
        State = SlotState.ChoJungPlaced;
        UpdatePreview();
        return true;
    }

    public bool TryPlaceJong(string jamo)
    {
        if (State != SlotState.ChoJungPlaced)
        {
            Debug.LogWarning("SyllableSlot: cannot place jong — cho+jung not placed yet.");
            return false;
        }
        // The jongseong table legitimately contains "" (no final consonant),
        // but an undealt tile's empty jamo must not count as a placement.
        if (string.IsNullOrEmpty(jamo) || !HangulComposer.IsValidJongseong(jamo))
        {
            Debug.LogWarning($"SyllableSlot: '{jamo}' is not a valid jongseong.");
            return false;
        }
        _jong = jamo;
        State = SlotState.Complete;
        UpdatePreview();
        return true;
    }

    public bool TryRemoveJong()
    {
        if (State != SlotState.Complete) return false;
        _jong = "";
        State = SlotState.ChoJungPlaced;
        UpdatePreview();
        return true;
    }

    public bool TryRemoveJung()
    {
        if (State != SlotState.ChoJungPlaced) return false;
        _jung = "";
        CurrentSyllable = null;
        State = SlotState.ChoPlaced;
        return true;
    }

    public bool TryRemoveCho()
    {
        if (State != SlotState.ChoPlaced) return false;
        _cho = "";
        CurrentSyllable = null;
        State = SlotState.Empty;
        return true;
    }

    public bool TryComplete()
    {
        if (State != SlotState.ChoJungPlaced)
        {
            Debug.LogWarning("SyllableSlot: cannot complete — need cho+jung first.");
            return false;
        }
        State = SlotState.Complete;
        UpdatePreview();
        return true;
    }

    public void Reset()
    {
        _cho = "";
        _jung = "";
        _jong = "";
        CurrentSyllable = null;
        State = SlotState.Empty;
    }

    private void UpdatePreview()
    {
        if (_cho != "" && _jung != "")
            CurrentSyllable = HangulComposer.Compose(_cho, _jung, _jong);
    }
}