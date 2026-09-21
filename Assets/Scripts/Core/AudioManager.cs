using UnityEngine;

// Central sound-effect player. Clips are assigned via the Inspector on the
// GameController prefab in Resources — a clip left empty simply makes that
// effect silent, so every Play method is safe to call with no clip. All of
// them honor the SoundOn setting from SettingScene.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource _source;
    [SerializeField] private AudioClip _tileTapClip;
    [SerializeField] private AudioClip _syllableCompleteClip;
    [SerializeField] private AudioClip _wordSuccessClip;
    [SerializeField] private AudioClip _wordErrorClip;
    [SerializeField] private AudioClip _hintClip;
    [SerializeField] private AudioClip _resetClip;

    void Awake()
    {
        // A duplicate AudioManager (often one with no clips assigned)
        // silently steals the singleton and mutes every sound effect.
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"AudioManager: duplicate instance on '{gameObject.name}' — destroying it. Look for a second GameController in the scene.");
            Destroy(this);
            return;
        }
        Instance = this;
        if (_source == null)
        {
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
        }
        _source.playOnAwake = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayTileTap() => Play(_tileTapClip);

    public void PlaySyllableComplete() => Play(_syllableCompleteClip);

    public void PlayWordSuccess() => Play(_wordSuccessClip);

    public void PlayWordError() => Play(_wordErrorClip);

    public void PlayHint() => Play(_hintClip);

    public void PlayReset() => Play(_resetClip);

    private void Play(AudioClip clip)
    {
        if (clip == null || _source == null) return;
        if (!GameSettings.SoundOn) return;
        _source.PlayOneShot(clip);
    }

    // Convenience for callers: no-ops when no AudioManager exists
    // (e.g. in unit test scenes).
    public static void TryPlayTileTap() { if (Instance != null) Instance.PlayTileTap(); }
    public static void TryPlaySyllableComplete() { if (Instance != null) Instance.PlaySyllableComplete(); }
    public static void TryPlayWordSuccess() { if (Instance != null) Instance.PlayWordSuccess(); }
    public static void TryPlayWordError() { if (Instance != null) Instance.PlayWordError(); }
    public static void TryPlayHint() { if (Instance != null) Instance.PlayHint(); }
    public static void TryPlayReset() { if (Instance != null) Instance.PlayReset(); }
}
