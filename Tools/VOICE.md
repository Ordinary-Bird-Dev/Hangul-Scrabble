# Hint voice — state, decisions, and how to add a level

Everything the Classic hint button speaks. Written 2026-10-29, after
level 1 shipped. Read this before touching `Tools/gen_vo.py` or
`Tools/pack_vo.py`.

## Where it stands

| | state |
|---|---|
| Level 1 (topik1, 초급) | **done** — 1,446 clips, generated, trimmed, packed |
| Level 2 (vocabB, 중급) | not generated — 4,587 words, falls back to browser speech |
| Level 3 (vocabC, 고급) | not generated — 16,957 words, falls back to browser speech |

Shipped level-1 audio is **8.0 MB** in `Assets/StreamingAssets/vo/`,
packed into 128 bank files plus `vo_index.json`.

## The pinned voice — do not change casually

Recorded in `Assets/StreamingAssets/vo/vo_manifest.json`, and repeated
here because the manifest ships and this does not:

```
voice_id   cBOtnpVZNlQ5VJygXGB8     ("Eunha - Elegant Korean Female")
model_id   eleven_multilingual_v2
speed 0.85, stability 0.5, similarity_boost 0.75, style 0.0,
use_speaker_boost true
```

Generate level 2 with a different voice and the vocabulary changes
speaker halfway up the difficulty ladder. Same discipline as
`Tools/palette/monkey_palette.md` for the sprites: the settings are the
asset, not an incidental of the run that made them.

## Adding level 2 or 3

**The order matters.** `pack_vo.py` refuses to pack on top of an existing
pack, because doing so would overwrite the banks with only the loose
files and throw away every level already packed.

```
python Tools\pack_vo.py --unpack              # banks -> loose clips again
python Tools\gen_vo.py --level 2 --voice "Eunha"
python Tools\gen_vo.py --post 48              # trim silence, re-encode
python Tools\pack_vo.py --banks 256           # see the table below
```

`gen_vo.py` skips any word whose clip already exists, so the unpacked
level-1 clips are left alone and only the new level is fetched. `--post`
re-encodes from the untouched masters in `VoiceMasters/`, so running it
again never compounds the loss.

### Choosing `--banks`

itch.io rejects an HTML5 zip with more than **1000 files**. The Unity
build is about 22 of them. Each bank is one file; the index is one more.

| levels packed | clips | suggested banks | files in zip | bank size |
|---|---|---|---|---|
| 1 | 1,446 | 128 | ~151 | 64 KB |
| 1+2 | 6,033 | 256 | ~279 | 115 KB |
| 1+2+3 | 22,990 | 512 | ~535 | 220 KB |

Bigger banks mean a bigger download per hint; more banks mean more files.
A player buys at most four hints a round (the help budget), so the worst
round costs four bank fetches.

## What the numbers actually were

Measured, not estimated — useful for predicting levels 2 and 3.

- **Billing: 20 words cost 20 credits.** Roughly one credit per request,
  *less* than the character count. All three levels is about 22,400
  credits, under 4% of a month on the Pro plan.
- **Generation: 1,446 clips in 73 seconds** at concurrency 8, zero
  failures. Level 3 should be around 15 minutes.
- **Clips arrive ~1.1 s long at 18 KB**, of which 5–52% is silence.
  Trimming plus 48 kbps took level 1 from 25.2 MB to 8.0 MB (68%).
- **48 kbps was chosen over 32** because this is a pronunciation tool —
  the player is straining to hear ㅂ vs ㅍ vs ㅃ. Changing your mind is
  `--post 32`, which costs no credits and takes seconds.

## Things that will bite

**Only clue-eligible words get clips.** `gen_vo.py` filters to 2–3
syllables with a non-empty `prompt`, which is exactly
`ClassicModeController.PickTarget`'s gate. Change that gate and the two
fall out of step — words become targets with no audio.

**Filenames are the word's UTF-16 code points in hex.** 학교 →
`d559ad50`. Python builds it in `gen_vo.py`, JavaScript rebuilds it in
`KoreanTTS.jslib`; both were checked against all 1,446 level-1 words.
Change one, change both.

**Masters live outside `Assets/`** at `VoiceMasters/`, deliberately.
Everything under `StreamingAssets` is copied into the build, so masters
kept there would ship twice. They are the 128 kbps originals and exist so
a future re-encode needs no new credits.

**No fallback off the web.** `KoreanTTS.jslib` only compiles into WebGL
builds, and Web Speech is a browser feature. On a native port a word
without a clip is simply silent, so levels 2 and 3 stop being optional.

**The API key is never an argument.** `gen_vo.py` reads
`ELEVENLABS_API_KEY`, and prompts for it if unset. Do not put it on a
command line or in a screenshot.

## Licensing

The clips are speech synthesised from krdict headwords, so they inherit
the unresolved ShareAlike question in `CREDITS.md`. Worth settling before
any commercial release.

## Files

| path | what |
|---|---|
| `Tools/gen_vo.py` | generate, `--check`, `--list-voices`, `--post` |
| `Tools/pack_vo.py` | pack into banks, `--unpack` |
| `Assets/StreamingAssets/vo/` | shipped banks + `vo_index.json` + `vo_manifest.json` |
| `VoiceMasters/` | 128 kbps originals, not shipped |
| `Assets/Plugins/WebGL/KoreanTTS.jslib` | runtime playback, browser fallback |
| `Assets/Scripts/Core/SpeechManager.cs` | the C# surface |
