using System.Runtime.InteropServices;
using UnityEngine;

// Speaks Korean vocabulary aloud using the browser's own speech engine,
// reached through Plugins/WebGL/KoreanTTS.jslib.
//
// Why the browser and not audio files: the corpus is 5,543 unique words
// across topik1, vocabB and vocabC. A clip per word would mean thousands
// of assets in a build that has to finish downloading before anyone can
// play it on itch.io. The browser already has a Korean voice on most
// machines, it costs nothing, and it covers words added later for free.
//
// The catch is that "most machines" is not "all". The Web Speech API is
// everywhere, but a Korean VOICE is not: a Windows install without the
// Korean language pack has none, and neither do many Firefox-on-Linux
// setups. HasKoreanVoice is the honest answer to that, and callers are
// expected to degrade rather than assume — ClassicModeController uses it
// to decide whether the hint still needs to print the romanization.
//
// Outside a WebGL build (the Editor, a desktop player) there is no
// speechSynthesis to call, so every method is a no-op and IsSupported is
// false. Speak logs what it would have said, so Editor testing still
// shows the trigger firing at the right moment.
public static class SpeechManager
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int TTS_IsSupported();
    [DllImport("__Internal")] private static extern int TTS_HasKoreanVoice();
    [DllImport("__Internal")] private static extern void TTS_Speak(string text, float rate);
    [DllImport("__Internal")] private static extern void TTS_Cancel();
    [DllImport("__Internal")] private static extern void TTS_Unlock();
#endif

    // Slower than conversational. This is a learner meeting a word for the
    // first time, not a news reader. 1.0 is the browser default.
    public const float SpeechRate = 0.85f;

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

    // Queried live rather than cached at startup. Browsers populate the
    // voice list asynchronously, so an answer taken during the first frame
    // is usually a false negative — by the time a player reaches the hint
    // button the list has long since loaded.
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
    public static void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!GameSettings.SoundOn) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        TTS_Speak(text, SpeechRate);
#else
        Debug.Log($"SpeechManager: no speech engine outside WebGL — would have said \"{text}\".");
#endif
    }

    // MUST be called from inside a real input handler, on the same frame as
    // the tap — JamoTile.Tap does it. iOS and other WebKit browsers refuse
    // to speak at all until one speak() has run synchronously inside a user
    // gesture, which would otherwise silently kill the success read-back
    // (it waits for the chime) for any player who never presses Hint.
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
