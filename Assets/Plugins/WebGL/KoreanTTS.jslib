// Korean text-to-speech for the WebGL build, via the browser's built-in
// Web Speech API. Called from SpeechManager.cs.
//
// Unity auto-detects a .jslib under Assets/Plugins/WebGL/ as a WebGL
// plugin, so no importer settings are needed. Outside a WebGL build none
// of this is compiled in at all.
mergeInto(LibraryManager.library, {

  TTS_IsSupported: function () {
    return (typeof window !== 'undefined'
      && 'speechSynthesis' in window
      && typeof window.SpeechSynthesisUtterance !== 'undefined') ? 1 : 0;
  },

  // Whether a Korean voice is actually installed, which is a separate
  // question from whether the API exists. A Windows machine without the
  // Korean language pack, or Firefox on many Linux builds, has the API
  // and no Korean voice — it would either stay silent or read Hangul
  // with an English engine. Callers use this to decide on a fallback.
  TTS_HasKoreanVoice: function () {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return 0;
    var voices = window.speechSynthesis.getVoices();
    for (var i = 0; i < voices.length; i++) {
      var lang = voices[i].lang ? voices[i].lang.toLowerCase().replace('_', '-') : '';
      if (lang.indexOf('ko') === 0) return 1;
    }
    return 0;
  },

  // iOS/WebKit will only start speaking if the very first speak() of the
  // page happens SYNCHRONOUSLY inside a real user gesture. Anything later
  // — our success read-back waits 0.45s for the chime, and the cancel path
  // below defers by 60ms — is silently dropped until that has happened.
  //
  // So burn the requirement off early: on the first tile tap, speak a
  // silent utterance from inside the tap handler. After that the engine is
  // unlocked for the rest of the session and deferred speech works.
  TTS_Unlock: function () {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
    if (window.__hangulTTSUnlocked) return;
    window.__hangulTTSUnlocked = true;

    try {
      // A space rather than an empty string: some engines reject "".
      var u = new SpeechSynthesisUtterance(' ');
      u.volume = 0;
      u.lang = 'ko-KR';
      window.speechSynthesis.speak(u);
    } catch (e) {
      // An engine that refuses the primer is no worse off than before.
    }
  },

  TTS_Speak: function (textPtr, rate) {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;

    var text = UTF8ToString(textPtr);
    if (!text) return;

    var synth = window.speechSynthesis;

    var say = function () {
      var u = new SpeechSynthesisUtterance(text);
      // Set lang even when no voice object matches: some engines pick a
      // suitable voice from the tag alone.
      u.lang = 'ko-KR';
      u.rate = rate;

      var voices = synth.getVoices();
      for (var i = 0; i < voices.length; i++) {
        var lang = voices[i].lang ? voices[i].lang.toLowerCase().replace('_', '-') : '';
        if (lang.indexOf('ko') === 0) { u.voice = voices[i]; break; }
      }

      synth.speak(u);
    };

    // Drop anything still queued so two quick hints don't stack up and
    // talk over each other seconds later. Chrome can swallow a speak()
    // issued in the same tick as a cancel(), hence the deferral.
    if (synth.speaking || synth.pending) {
      synth.cancel();
      setTimeout(say, 60);
    } else {
      say();
    }
  },

  TTS_Cancel: function () {
    if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
      window.speechSynthesis.cancel();
    }
  }
});
