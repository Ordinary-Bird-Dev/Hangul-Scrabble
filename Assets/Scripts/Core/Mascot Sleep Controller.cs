using UnityEngine;

// Idle-timeout mascot behaviour: after SleepAfterSeconds with no tile taps,
// the mascot plays its fall-asleep/sleeping-loop clip. The next tile tap
// wakes it back up. Lives on the same GameObject as the mascot's Animator.
public class MascotSleepController : MonoBehaviour
{
    private const float SleepAfterSeconds = 20f;
    private const string SleepTrigger = "Sleep";
    private const string WakeUpTrigger = "WakeUp";

    public static MascotSleepController Instance { get; private set; }

    private Animator _animator;
    private float _idleTimer;
    private bool _isAsleep;

    void Awake()
    {
        Instance = this;
        _animator = GetComponent<Animator>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_isAsleep) return;

        _idleTimer += Time.deltaTime;
        if (_idleTimer >= SleepAfterSeconds)
            FallAsleep();
    }

    private void FallAsleep()
    {
        _isAsleep = true;
        if (_animator != null && _animator.runtimeAnimatorController != null)
            _animator.SetTrigger(SleepTrigger);
    }

    // Called on every tile tap. Resets the idle countdown, and wakes
    // the mascot only if it was actually asleep.
    public void RegisterTileTap()
    {
        _idleTimer = 0f;
        if (!_isAsleep) return;

        _isAsleep = false;
        if (_animator != null && _animator.runtimeAnimatorController != null)
            _animator.SetTrigger(WakeUpTrigger);
    }

    // Convenience for callers: no-ops if this hasn't run its Awake yet
    // (e.g. very first frame) or the scene has no mascot.
    public static void TryRegisterTileTap()
    {
        if (Instance != null) Instance.RegisterTileTap();
    }
}