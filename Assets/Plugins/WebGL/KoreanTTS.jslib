// Korean text-to-speech for the WebGL build.
//
// Two engines, in order of preference:
//
//   1. Pre-generated clips, made by Tools/gen_vo.py and packed by
//      Tools/pack_vo.py into StreamingAssets/vo/. This is the normal path.
//   2. The browser's own Web Speech API, for any word with no clip --
//      levels whose audio has not been generated, or vocabulary added
//      since the last run.
//
// Why clips at all, when the browser has a speech engine: a Korean VOICE
// is not installed everywhere. A Windows box without the Korean language
// pack has the Web Speech API and nothing to speak with, so the player
// paid 50 points for the hint and heard silence -- and those points count
// against the help budget that can end the round.
//
// Why a pack rather than one file per word: itch.io rejects an HTML5 zip
// with more than 1000 files, and a Unity build plus 1,446 clips is 1,468.
// pack_vo.py concatenates them into ~128 bank files and writes
// vo_index.json mapping each word to {bank, offset, length}. We fetch the
// one bank a word lives in, slice those bytes, and play them as a Blob.
// A player still downloads only the audio they actually hear.
//
// Word -> index key is the UTF-16 code points in lowercase hex, so 학교
// is d559ad50. Pure ASCII, independent of server filename handling and of
// Unicode normalisation. gen_vo.py builds the same key in Python; the two
// were checked against all 1,446 level-1 words.
mergeInto(LibraryManager.library, {

  $HangulVO: {
    base: null,
    index: null,          // {banks:[name], clips:{stem:[bank,off,len]}}
    indexFetch: null,     // in-flight promise, so we fetch it once
    banks: {},            // bank number -> ArrayBuffer
    bankFetch: {},        // in-flight promises
    urls: {},             // stem -> blob: URL, built once per word
    player: null,         // ONE audio element, reused -- see TTS_Unlock
    unlocked: false,

    stem: function (text) {
      var s = '';
      for (var i = 0; i < text.length; i++) {
        s += ('000' + text.charCodeAt(i).toString(16)).slice(-4);
      }
      return s;
    },

    getPlayer: function () {
      if (!this.player) {
        this.player = new Audio();
        this.player.preload = 'auto';
      }
      return this.player;
    },

    loadIndex: function () {
      if (this.index) return Promise.resolve(this.index);
      if (this.indexFetch) return this.indexFetch;
      var self = this;
      this.indexFetch = fetch(this.base + 'vo_index.json')
        .then(function (r) { if (!r.ok) throw 0; return r.json(); })
        .then(function (j) { self.index = j; return j; })
        .catch(function () { self.index = { banks: [], clips: {} }; return self.index; });
      return this.indexFetch;
    },

    loadBank: function (n) {
      if (this.banks[n]) return Promise.resolve(this.banks[n]);
      if (this.bankFetch[n]) return this.bankFetch[n];
      var self = this;
      this.bankFetch[n] = fetch(this.base + this.index.banks[n])
        .then(function (r) { if (!r.ok) throw 0; return r.arrayBuffer(); })
        .then(function (buf) { self.banks[n] = buf; return buf; });
      return this.bankFetch[n];
    },

    // Resolves to a blob: URL for the word, or null if it has no clip.
    urlFor: function (text) {
      var self = this;
      var st = this.stem(text);
      if (this.urls[st]) return Promise.resolve(this.urls[st]);
      if (!this.base) return Promise.resolve(null);

      return this.loadIndex().then(function (index) {
        var e = index.clips[st];
        if (!e) return null;
        return self.loadBank(e[0]).then(function (buf) {
          if (self.urls[st]) return self.urls[st];
          var slice = buf.slice(e[1], e[1] + e[2]);
          var url = URL.createObjectURL(new Blob([slice], { type: 'audio/mpeg' }));
          self.urls[st] = url;
          return url;
        });
      }).catch(function () { return null; });
    },

    stop: function () {
      if (this.player) {
        try { this.player.pause(); this.player.currentTime = 0; } catch (e) {}
      }
      if (typeof window !== 'undefined' && 'speechSynthesis' in window) {
        window.speechSynthesis.cancel();
      }
    },

    // The original browser path, unchanged, now only a fallback.
    speakWithBrowser: function (text, rate) {
      if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;
      var synth = window.speechSynthesis;
      var say = function () {
        var u = new SpeechSynthesisUtterance(text);
        u.lang = 'ko-KR';
        u.rate = rate;
        var voices = synth.getVoices();
        for (var i = 0; i < voices.length; i++) {
          var lang = voices[i].lang ? voices[i].lang.toLowerCase().replace('_', '-') : '';
          if (lang.indexOf('ko') === 0) { u.voice = voices[i]; break; }
        }
        synth.speak(u);
      };
      // Chrome can swallow a speak() issued in the same tick as a cancel().
      if (synth.speaking || synth.pending) { synth.cancel(); setTimeout(say, 60); }
      else { say(); }
    }
  },

  TTS_IsSupported: function () {
    return (typeof window !== 'undefined' && typeof Audio !== 'undefined') ? 1 : 0;
  },

  // Whether the BROWSER has a Korean voice. Only describes the fallback's
  // health now -- a word with a clip speaks on any machine.
  TTS_HasKoreanVoice: function () {
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return 0;
    var voices = window.speechSynthesis.getVoices();
    for (var i = 0; i < voices.length; i++) {
      var lang = voices[i].lang ? voices[i].lang.toLowerCase().replace('_', '-') : '';
      if (lang.indexOf('ko') === 0) return 1;
    }
    return 0;
  },

  // iOS/WebKit refuses to play audio until one play() has run
  // synchronously inside a real user gesture. JamoTile.Tap calls this on
  // the first tile tap.
  //
  // The unlock attaches to the ELEMENT, not to the page, which is why
  // there is one reusable player rather than a new Audio per clip. Later
  // hints just swap its src, so they inherit this gesture and are not
  // blocked for arriving after an await.
  TTS_Unlock__deps: ['$HangulVO'],
  TTS_Unlock: function () {
    if (typeof window === 'undefined' || HangulVO.unlocked) return;
    HangulVO.unlocked = true;

    try {
      if ('speechSynthesis' in window) {
        var u = new SpeechSynthesisUtterance(' ');   // a space: some engines reject ''
        u.volume = 0;
        u.lang = 'ko-KR';
        window.speechSynthesis.speak(u);
      }
    } catch (e) { /* an engine that refuses the primer is no worse off */ }

    try {
      var p = HangulVO.getPlayer();
      p.src = 'data:audio/mpeg;base64,SUQzBAAAAAAAI1RTU0UAAAAPAAADTGF2ZjYwLjE2LjEwMAAAAAAAAAAAAAAA//NwwAAAAAAAAAAAAEluZm8AAAAPAAAABAAAAR8Aubm5ubm5ubm5ubm5ubm5ubm5ubm5ubm50dHR0dHR0dHR0dHR0dHR0dHR0dHR0dHR0ejo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Ojo6Oj/////////////////////////////////AAAAAExhdmM2MC4zMQAAAAAAAAAAAAAAACQCcQAAAAAAAAEfOUAvBwAAAAAAAAAAAAAAAAD/8xDEAAAAA0gAAAAATEFNRTMuMTAwVVVVVf/zEsQNAAADSAAAAABVVVVVVVVVVVVVVVVVVf/zEMQbAAADSAAAAABVVVVVVVVVVVVVVVVV//MQxCgAAANIAAAAAFVVVVVVVVVVVVVVVVU=';   // 50ms of silence, inline
      var played = p.play();
      if (played && played.catch) played.catch(function () {});
    } catch (e) { /* same */ }
  },

  TTS_Speak__deps: ['$HangulVO'],
  TTS_Speak: function (textPtr, basePtr, rate) {
    if (typeof window === 'undefined') return;
    var text = UTF8ToString(textPtr);
    if (!text) return;
    HangulVO.base = UTF8ToString(basePtr) || HangulVO.base;

    // Drop whatever is still going, so two quick hints do not stack up.
    HangulVO.stop();

    // A token so a slow lookup cannot start playing after a newer request.
    var token = (HangulVO.token = (HangulVO.token || 0) + 1);

    HangulVO.urlFor(text).then(function (url) {
      if (token !== HangulVO.token) return;        // superseded
      if (!url) { HangulVO.speakWithBrowser(text, rate); return; }
      var p = HangulVO.getPlayer();
      p.src = url;
      var played = p.play();
      if (played && played.catch) {
        played.catch(function () { HangulVO.speakWithBrowser(text, rate); });
      }
    });
  },

  // Warms the index, the bank and the blob for a word before the player
  // asks, so the Hint press plays immediately instead of waiting on a
  // fetch. ClassicModeController calls this when a target is dealt.
  TTS_Preload__deps: ['$HangulVO'],
  TTS_Preload: function (textPtr, basePtr) {
    if (typeof window === 'undefined') return;
    var text = UTF8ToString(textPtr);
    if (!text) return;
    HangulVO.base = UTF8ToString(basePtr) || HangulVO.base;
    HangulVO.urlFor(text);
  },

  TTS_Cancel__deps: ['$HangulVO'],
  TTS_Cancel: function () {
    if (typeof window === 'undefined') return;
    HangulVO.token = (HangulVO.token || 0) + 1;    // invalidate any pending play
    HangulVO.stop();
  }
});
