# Voice clip pipeline

How the Hint button's Korean audio is made and shipped. Written 2026-10-29,
after level 1 shipped. Read this before generating levels 2 or 3.

## State

| level | set | speakable words | clips generated | shipped |
|---|---|---|---|---|
| 1 | topik1 (krdict 초급) | 1,446 | yes | yes, 8.0 MB |
| 2 | vocabB (krdict 중급) | 4,587 | **no** | no |
| 3 | vocabC (krdict 고급) | 16,957 | **no** | no |

Levels 2 and 3 currently fall through to the browser's Web Speech API,
which is what the whole game did before. No worse than it was, just not
better.

## What "speakable" means

The hint speaks `Target.word`, and `Target` only ever comes from
`ClassicModeController.PickTarget`, which requires **2–3 syllables and a
non-empty `prompt`**. Everything else in the word list can be built and
validated but never asked for, so it never needs audio. That is why the
counts above are smaller than the sets.

`gen_vo.py` applies exactly that filter. If `PickTarget`'s gate ever
changes, change `speakable()` in the script to match or clips will be
generated for words that are never spoken, and missed for words that are.

## The three commands

```powershell
# 1. generate (prompts for the ElevenLabs key; ~2.5 min for level 1)
python C:\Users\hyunc\HangulPuzzle\Tools\gen_vo.py --level 2 --voice "Eunha"

# 2. trim silence and re-encode for shipping (needs ffmpeg on PATH)
python C:\Users\hyunc\HangulPuzzle\Tools\gen_vo.py --post 48

# 3. pack into bank files, because itch.io caps a zip at 1000 files
python C:\Users\hyunc\HangulPuzzle\Tools\pack_vo.py --banks 128
```

`pack_vo.py --unpack` reverses step 3. **Run it before generating another
level**, otherwise `gen_vo.py` cannot see which clips already exist and
will regenerate everything.

## Settings that must not drift

Pinned in `gen_vo.py` and recorded in `StreamingAssets/vo/vo_manifest.json`:

```
voice_id   cBOtnpVZNlQ5VJygXGB8   ("Eunha - Elegant Korean Female")
model_id   eleven_multilingual_v2
speed      0.85     stability 0.5     similarity_boost 0.75
master     mp3_44100_128   ->   shipped at 48 kbps, silence-trimmed
```

Change any of these and the new words will audibly not match the old ones.
This is the same failure the sprite sheets had before `Tools/palette/`
existed. If a change is wanted, regenerate **everything**, not the delta.

## Measured numbers

- **Billing:** 20 words cost 20 credits. Roughly one credit per request,
  not per character. All three levels ≈ 22,400 credits, about 3.7% of one
  month on the Pro plan. Cost is not a constraint.
- **Concurrency:** Pro allows 10; the script uses 8. 1,446 clips took 73 s.
- **Size, level 1:** 25.2 MB at 128 kbps → **8.0 MB** trimmed at 48 kbps.
  Trimming alone removed 5–52% per clip, which was pure leading/trailing
  silence — it also made the hint start speaking immediately.
- **Projected:** level 2 ≈ 21 MB, level 3 ≈ 78 MB at the same settings.

## Why banks

itch.io rejects an HTML5 zip with more than 1000 files. Unity's build is
~22 files, so 1,446 loose clips made 1,468 and the upload was refused —
the 8 MB was never the issue. `pack_vo.py` concatenates clips into N
banks plus `vo_index.json` mapping each word to `{bank, offset, length}`.

At runtime `KoreanTTS.jslib` fetches the one bank a word lives in, slices
those bytes and plays them as a Blob, so a player still downloads only
the audio they hear.

**Budget the file count before generating level 3.** At 128 banks per
level the total is ~400 files, fine — but level 3's banks would be ~600 KB
each, which is a chunky fetch for one hint. Raise `--banks` for level 3
(say 512) and check the total stays well under 1000.

## Filename scheme

A word's key is its UTF-16 code points in lowercase hex: 학교 → `d559ad50`.
Pure ASCII, so nothing depends on the server handling UTF-8 filenames or
on Unicode normalisation surviving Windows → zip → browser.

`gen_vo.py` (Python) and `KoreanTTS.jslib` (JavaScript) each build this
independently. They were checked against all 1,446 level-1 words with zero
mismatches. **If you touch one, re-check the other.**

## Gotchas already paid for

- `$env:VAR` in PowerShell dies with the window. `gen_vo.py` prompts for
  the key instead; say yes to "remember" if you are tired of pasting it.
- Multi-line pastes into PowerShell glue lines together. One command per
  line.
- Paths resolve against the script's own location, not the shell's working
  directory, so it runs from anywhere.
- Anything under `Assets/StreamingAssets/` ships. Masters live in
  `VoiceMasters/` at the project root, deliberately outside `Assets/`.

## Other platforms

The MP3s are platform-neutral and carry over unchanged. The *playback* is
not: `KoreanTTS.jslib` only compiles into WebGL builds, and every method in
`SpeechManager` is behind `#if UNITY_WEBGL && !UNITY_EDITOR`. A native
build would be silent until a second path is added, roughly
`UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG)` + an
AudioSource. Use UnityWebRequest even on desktop — Android keeps
StreamingAssets inside the APK, where ordinary file reads fail.

Note that native platforms have **no Web Speech fallback**, so clips stop
being optional there: levels 2 and 3 would be silent rather than degraded.

## Licensing

The words come from krdict under CC BY-SA 2.0 KR — see `CREDITS.md`. The
generated audio is derived from those headwords, so it inherits the same
unresolved ShareAlike question. Not a blocker for generating, but it is
still open.
