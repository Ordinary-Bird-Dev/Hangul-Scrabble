using UnityEngine.EventSystems;

public class EventSystemWebGL : EventSystem
{
#if UNITY_WEBGL
    protected override void OnApplicationFocus(bool hasFocus)
    {
        // Do nothing — prevents Safari's focus bug from disabling input permanently.
    }
#endif
}