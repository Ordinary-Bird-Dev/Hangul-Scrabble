using UnityEngine;

// Central sound-effect player. Clips are assigned via the Inspector and
// are intentionally left unassigned for now (audio assets not ready);
// every Play method is safe to call with no clip and honors the
// SoundOn setting from SettingScene.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource _source;
    [SerializeField] private AudioClip _tileTapClip;
    [SerializeField] private AudioClip _syllableCompleteClip;
    [SerializeField] private AudioClip _wordSuccessClip;
    [SerializeField] private AudioClip _wordErrorClip;

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
}
