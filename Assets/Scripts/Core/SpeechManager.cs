using System.Runtime.InteropServices;
using UnityEngine;

// Speaks Korean vocabulary aloud, through Plugins/WebGL/KoreanTTS.jslib.
//
// Two engines, preferred in this order:
//
//   1. A pre-generated clip from StreamingAssets/vo/, one per word, made
//      by Tools/gen_vo.py against ElevenLabs. Level 1 has all 1,446 of
//      its clue-eligible words; levels 2 and 3 do not yet.
//   2. The browser's own Web Speech API, for any word with no clip.
//
// It used to be the browser alone, on the reasoning that thousands of
// clips would bloat the build. That reasoning had two holes. The corpus
// is short words, so the whole of level 1 is 8 MB at 48 kbps; and
// StreamingAssets is fetched on demand rather than bundled into the
// initial download, so none of it delays the first frame.
//
// The hole that actually mattered: the Web Speech API is everywhere but a
// Korean VOICE is not. A Windows install without the Korean language pack
// has the API and nothing to speak with. The player paid 50 points for the
// hint, heard silence, and -- since the help budget turns spent points
// into a lose condition -- could be eliminated by a feature that made no
// sound. A clip plays on any machine.
//
// Outside a WebGL build (the Editor, a desktop player) there is no audio
// path at all, so every method is a no-op and IsSupported is false. Speak
// logs what it would have said, so Editor testing still shows the trigger
// firing at the right moment.
public static class SpeechManager
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int TTS_IsSupported();
    [DllImport("__Internal")] private static extern int TTS_HasKoreanVoice();
    [DllImport("__Internal")] private static extern void TTS_Speak(string text, string clipBase, float rate);
    [DllImport("__Internal")] private static extern void TTS_Preload(string text, string clipBase);
    [DllImport("__Internal")] private static extern void TTS_Cancel();
    [DllImport("__Internal")] private static extern void TTS_Unlock();
#endif

    // Only reached by the fallback engine now. The clips are generated at
    // this rate already -- baked in rather than applied at playback,
    // because resampling a finished MP3 adds artefacts.
    public const float SpeechRate = 0.85f;

    // Where the clips live, relative to the build. Resolved once:
    // Application.streamingAssetsPath is a full URL on WebGL and does not
    // change, and the JS side needs it on every call.
    private static string _clipBase;

    private static string ClipBase
    {
        get
        {
            if (_clipBase == null)
                _clipBase = Application.streamingAssetsPath + "/vo/";
            return _clipBase;
        }
    }

    public static bool IsSupported
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TTS_IsSupported() == 1;
#else
            return false;
#endif
        }
    }

    // Whether the BROWSER has a Korean voice. This no longer decides
    // whether a word can be spoken -- a word with a clip speaks on any
    // machine -- so it now only describes the fallback's health, for a
    // word whose audio has not been generated.
    //
    // Queried live rather than cached at startup. Browsers populate the
    // voice list asynchronously, so an answer taken during the first frame
    // is usually a false negative.
    public static bool HasKoreanVoice
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return TTS_HasKoreanVoice() == 1;
#else
            return false;
#endif
        }
    }

    // Honors the same SoundOn setting as AudioManager, so the Settings
    // toggle silences the whole game rather than most of it.
    //
    // The JS side picks the engine: it tries the clip, and falls back to
    // the browser on a 404 or a decode failure. Nothing here needs to know
    // which words have audio, which is what keeps this from going stale
    // every time more clips are generated.
    public static void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!GameSettings.SoundOn) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        TTS_Speak(text, ClipBase, SpeechRate);
#else
        Debug.Log($"SpeechManager: no speech engine outside WebGL — would have said \"{text}\".");
#endif
    }

    // Fetches a word's clip ahead of time so the first Hint press plays
    // instantly instead of waiting on a round trip. Optional: skipping it
    // costs one short fetch, not correctness. Deliberately ignores
    // SoundOn — warming the cache makes no noise, and a player who turns
    // sound on mid-round should not then pay the latency.
    public static void Preload(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        TTS_Preload(text, ClipBase);
#endif
    }

    // MUST be called from inside a real input handler, on the same frame as
    // the tap — JamoTile.Tap does it. iOS and other WebKit browsers refuse
    // to play audio of ANY kind until one play() has run synchronously
    // inside a user gesture. That applies to the <audio> element just as it
    // did to speech synthesis, so the JS side primes both.
    //
    // Cheap and idempotent: the JS side no-ops after the first call.
    public static void Unlock()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TTS_Unlock();
#endif
    }

    public static void Cancel()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TTS_Cancel();
#endif
    }
}
