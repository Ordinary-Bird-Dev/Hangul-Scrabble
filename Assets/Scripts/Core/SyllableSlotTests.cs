using UnityEngine;

public class SyllableSlotTests : MonoBehaviour
{
    void Start()
    {
        TestBasicComposition();
        TestInvalidOrdering();
        TestReset();
        TestNoJongComplete();
        TestInvalidJamoRejected();
        Debug.Log("All SyllableSlot tests passed!");
    }

    void TestBasicComposition()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        bool r1 = slot.TryPlaceCho("ㅎ");
        bool r2 = slot.TryPlaceJung("ㅏ");
        bool r3 = slot.TryPlaceJong("ㄱ");

        Debug.Assert(r1, "TryPlaceCho(ㅎ) should succeed");
        Debug.Assert(r2, "TryPlaceJung(ㅏ) should succeed");
        Debug.Assert(r3, "TryPlaceJong(ㄱ) should succeed");
        Debug.Assert(slot.State == SyllableSlot.SlotState.Complete,
            "State should be Complete after cho+jung+jong");
        Debug.Assert(slot.CurrentSyllable == "학",
            $"Expected 학, got {slot.CurrentSyllable}");

        Destroy(slot);
    }

    void TestInvalidOrdering()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        bool result = slot.TryPlaceJung("ㅏ");
        Debug.Assert(!result,
            "Placing jung before cho should fail");
        Debug.Assert(slot.State == SyllableSlot.SlotState.Empty,
            "State should stay Empty after failed placement");

        Destroy(slot);
    }

    void TestReset()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        slot.TryPlaceCho("ㅅ");
        slot.TryPlaceJung("ㅏ");
        slot.Reset();

        Debug.Assert(slot.State == SyllableSlot.SlotState.Empty,
            "State should be Empty after reset");
        Debug.Assert(slot.CurrentSyllable == null,
            "CurrentSyllable should be null after reset");

        Destroy(slot);
    }

    void TestNoJongComplete()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        slot.TryPlaceCho("ㅅ");
        slot.TryPlaceJung("ㅏ");
        slot.TryComplete();

        Debug.Assert(slot.State == SyllableSlot.SlotState.Complete,
            "State should be Complete for syllable with no jongseong");
        Debug.Assert(slot.CurrentSyllable == "사",
            $"Expected 사, got {slot.CurrentSyllable}");

        Destroy(slot);
    }

    void TestInvalidJamoRejected()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        bool result = slot.TryPlaceCho("ㅏ");
        Debug.Assert(!result,
            "Placing a vowel into cho slot should fail");
        Debug.Assert(slot.State == SyllableSlot.SlotState.Empty,
            "State should stay Empty after invalid jamo");

        Destroy(slot);
    }
}