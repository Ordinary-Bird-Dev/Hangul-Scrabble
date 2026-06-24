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

    private int _choIndex = -1;
    private int _jungIndex = -1;
    private int _jongIndex = 0;

    public char CurrentSyllable { get; private set; } = '\0';

    public bool TryPlaceCho(int choIndex)
    {
        if (State != SlotState.Empty)
        {
            Debug.LogWarning("SyllableSlot: cannot place cho — slot not empty.");
            return false;
        }
        _choIndex = choIndex;
        State = SlotState.ChoPlaced;
        UpdatePreview();
        return true;
    }

    public bool TryPlaceJung(int jungIndex)
    {
        if (State != SlotState.ChoPlaced)
        {
            Debug.LogWarning("SyllableSlot: cannot place jung — cho not placed yet.");
            return false;
        }
        _jungIndex = jungIndex;
        State = SlotState.ChoJungPlaced;
        UpdatePreview();
        return true;
    }

    public bool TryPlaceJong(int jongIndex)
    {
        if (State != SlotState.ChoJungPlaced)
        {
            Debug.LogWarning("SyllableSlot: cannot place jong — cho+jung not placed yet.");
            return false;
        }
        _jongIndex = jongIndex;
        State = SlotState.Complete;
        UpdatePreview();
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
        _choIndex = -1;
        _jungIndex = -1;
        _jongIndex = 0;
        CurrentSyllable = '\0';
        State = SlotState.Empty;
    }

    private void UpdatePreview()
    {
        if (_choIndex >= 0 && _jungIndex >= 0)
        {
            CurrentSyllable = HangulComposer.Compose(_choIndex, _jungIndex, _jongIndex);
        }
    }
}