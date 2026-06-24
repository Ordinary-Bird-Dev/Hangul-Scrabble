using UnityEngine;

public class SyllableSlotTests : MonoBehaviour
{
    void Start()
    {
        TestBasicComposition();
        TestInvalidOrdering();
        TestReset();
        TestNoJongComplete();
        Debug.Log("All SyllableSlot tests passed!");
    }

    void TestBasicComposition()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        bool r1 = slot.TryPlaceCho(18);   // ㅎ
        bool r2 = slot.TryPlaceJung(0);   // ㅏ
        bool r3 = slot.TryPlaceJong(1);   // ㄱ

        Debug.Assert(r1, "TryPlaceCho should succeed");
        Debug.Assert(r2, "TryPlaceJung should succeed");
        Debug.Assert(r3, "TryPlaceJong should succeed");
        Debug.Assert(slot.State == SyllableSlot.SlotState.Complete, "State should be Complete");
        Debug.Assert(slot.CurrentSyllable == '학',
            $"Expected 학, got {slot.CurrentSyllable}");

        Destroy(slot);
    }

    void TestInvalidOrdering()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        bool result = slot.TryPlaceJung(0);
        Debug.Assert(!result, "Placing jung before cho should fail");
        Debug.Assert(slot.State == SyllableSlot.SlotState.Empty,
            "State should stay Empty after failed placement");

        Destroy(slot);
    }

    void TestReset()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        slot.TryPlaceCho(9);    // ㅅ
        slot.TryPlaceJung(0);   // ㅏ
        slot.Reset();

        Debug.Assert(slot.State == SyllableSlot.SlotState.Empty,
            "State should be Empty after reset");
        Debug.Assert(slot.CurrentSyllable == '\0',
            "CurrentSyllable should be null char after reset");

        Destroy(slot);
    }

    void TestNoJongComplete()
    {
        SyllableSlot slot = gameObject.AddComponent<SyllableSlot>();

        slot.TryPlaceCho(9);    // ㅅ
        slot.TryPlaceJung(0);   // ㅏ
        slot.TryComplete();     // 사 — no jongseong

        Debug.Assert(slot.State == SyllableSlot.SlotState.Complete,
            "State should be Complete");
        Debug.Assert(slot.CurrentSyllable == '사',
            $"Expected 사, got {slot.CurrentSyllable}");

        Destroy(slot);
    }
}