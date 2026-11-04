# Hint voice — state, decisions, and how to add a level

Everything the Classic hint button speaks. Written 2026-10-29, after
level 1 shipped; updated 2026-11-03, after level 2. Read this before
touching `Tools/gen_vo.py` or `Tools/pack_vo.py`.

## Where it stands

| | state |
|---|---|
| Level 1 (topik1, 초급) | **done** — 1,446 clips, Eunha |
| Level 2 (vocabB, 중급) | **done** — 4,587 hint words: 4,488 new clips in Soo, 99 shared with level 1 (Eunha) |
| Level 3 (vocabC, 고급) | not generated — 16,957 hint words (16,477 new), falls back to browser speech |

Shipped audio is **34.7 MB** in `Assets/StreamingAssets/vo/`: 5,934
clips packed into 256 bank files (~140 KB each) plus `vo_index.json`.
Checked 2026-11-03: every clue-eligible level 1 and 2 word is in the
index, no extras, byte ranges contiguous, sampled banks decode as
48 kbps mono.

## The pinned voices — one per level, do not change casually

Recorded per level under `voices_by_level` in
`Assets/StreamingAssets/vo/vo_manifest.json`, and repeated here because
the manifest ships and this does not:

```
level 1  cBOtnpVZNlQ5VJygXGB8   "Eunha - Elegant Korean Female"
level 2  UmVsCJauYwXB4cGF0OlH   "Soo - Warm Korean Teacher"
both     eleven_multilingual_v2
         speed 0.85, stability 0.5, similarity_boost 0.75, style 0.0,
         use_speaker_boost true
```

Level 2 changes speaker on purpose (decided 2026-11-03). Within a level
the voice is fixed: regenerate a level-2 word with anything but Soo and
that level drifts. Same discipline as `Tools/palette/monkey_palette.md`
for the sprites: the settings are the asset, not an incidental of the
run that made them. Pass the voice_id, not a name, when regenerating.
Level 3's voice is not chosen yet.

**One clip per word, not per level.** Filenames come from the word
alone, so a word in two levels has one clip, in the voice of whichever
level generated it first. 99 level-2 hint words are level-1 words and
speak in Eunha. Level 3 has 480 such overlaps with levels 1–2, which
will keep their existing voice. Giving them per-level clips would mean
adding the level to the filename in both `gen_vo.py` and
`KoreanTTS.jslib`.

## Adding level 3

**The order matters.** `pack_vo.py` refuses to pack on top of an existing
pack, because doing so would overwrite the banks with only the loose
files and throw away every level already packed. Close Unity first, or
it imports thousands of loose clips mid-run.

```
python Tools\pack_vo.py --unpack              # banks -> loose clips again
python Tools\gen_vo.py --list-voices          # get the voice_id
python Tools\gen_vo.py --level 3 --voice <voice_id> --limit 20   # listen first
python Tools\gen_vo.py --level 3 --voice <voice_id>
python Tools\gen_vo.py --post 48              # trim silence, re-encode
python Tools\pack_vo.py --banks 512           # see the table below
```

A voice from the ElevenLabs Voice Library must be added to "My voices"
before `--list-voices` or `--voice` can see it.

`gen_vo.py` skips any word whose clip already exists, so the unpacked
clips are left alone and only the new level is fetched — including the
`--limit` probe clips, so delete those first if you change settings.
`--post` re-encodes from the untouched masters in `VoiceMasters/`, so
running it again never compounds the loss. The manifest records each
level's voice separately; a `--limit` probe is not recorded.

### Choosing `--banks`

itch.io rejects an HTML5 zip with more than **1000 files**. The Unity
build is about 22 of them. Each bank is one file; the index is one more.

| levels packed | clips | banks | files in zip | bank size |
|---|---|---|---|---|
| 1 | 1,446 | 128 | ~151 | 64 KB |
| 1+2 (current) | 5,934 | 256 | ~279 | 139 KB measured |
| 1+2+3 | 22,411 | 512 | ~535 | ~265 KB projected |

Bigger banks mean a bigger download per hint; more banks mean more files.
A player buys at most four hints a round (the help budget), so the worst
round costs four bank fetches.

## What the numbers actually were

Measured, not estimated — useful for predicting level 3.

- **Billing: 20 words cost 20 credits.** Roughly one credit per request,
  *less* than the character count. Level 3 is about 16,500 credits.
- **Generation: 1,446 clips in 73 seconds** at concurrency 8, zero
  failures. Level 3 should be around 15 minutes.
- **Clips arrive ~1.1 s long at 18 KB**, of which 5–52% is silence.
  Trimming plus 48 kbps took level 1 from 25.2 MB to 8.0 MB (68%).
- **Shipped clip size:** level-1 average 5.8 KB (Eunha), level-2-only
  average 6.3 KB (Soo, slightly longer words and voice). At 6.3 KB,
  level 3 adds about 98 MB, for a total near 133 MB.
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
without a clip is simply silent, so level 3 stops being optional.

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

## Open items, carried over

Not blockers for generating audio, but a new session should know they exist.

- `InfoPageController.VocabCredit` holds the required krdict attribution
  but needs a `VocabCreditText` label placed in the How to Play or Hangul
  Basics scene. It logs a warning until then.
- `ClassicModeControllerTests` and `WordValidatorTests` are components on
  GameController in GameScene, so they run in builds. Remove them in the
  Inspector before shipping.
- The header comment in `SpeechManager.cs` still says only level 1 has
  audio.
- Level 3 is the only remaining audio work. It would be about 74% of the
  total payload (~98 MB of ~133 MB) for words only a level-3 player sees,
  so whether it earns its size is a real decision, not a formality.
