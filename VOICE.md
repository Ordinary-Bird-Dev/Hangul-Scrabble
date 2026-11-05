# Hint voice — state, decisions, and how to add a level

Everything the Classic hint button speaks. Written 2026-10-29, after
level 1 shipped; updated 2026-11-03, after levels 2 and 3. Read this before
touching `Tools/gen_vo.py` or `Tools/pack_vo.py`.

## Where it stands

| | state |
|---|---|
| Level 1 (topik1, 초급) | **done** — 1,446 clips, Eunha |
| Level 2 (vocabB, 중급) | **done** — 4,587 hint words: 4,488 new clips in Soo, 99 shared with level 1 (Eunha) |
| Level 3 (vocabC, 고급) | **done** — 16,957 hint words: 16,477 new clips in Jini, 480 shared with levels 1–2 |

Shipped audio is **124.5 MB** in `Assets/StreamingAssets/vo/`: 22,411
clips packed into 512 bank files (~250 KB each) plus `vo_index.json`
(657 KB). Checked 2026-11-03: every clue-eligible word in all three
levels is in the index, no extras, byte ranges contiguous, bank sizes
match the index, sampled banks decode as 48 kbps mono in all three
voices. All audio work is done.

## The pinned voices — one per level, do not change casually

Recorded per level under `voices_by_level` in
`Assets/StreamingAssets/vo/vo_manifest.json`, and repeated here because
the manifest ships and this does not:

```
level 1  cBOtnpVZNlQ5VJygXGB8   "Eunha - Elegant Korean Female"
level 2  UmVsCJauYwXB4cGF0OlH   "Soo - Warm Korean Teacher"
level 3  0oqpliV6dVSr9XomngOW   "Jini - Warm & Intelligent Korean Female"
all      eleven_multilingual_v2
         speed 0.85, stability 0.5, similarity_boost 0.75, style 0.0,
         use_speaker_boost true
```

Each level has its own speaker on purpose (decided 2026-11-03). Within
a level the voice is fixed: regenerate a level-2 word with anything but
Soo, or a level-3 word with anything but Jini, and that level drifts.
Same discipline as `Tools/palette/monkey_palette.md` for the sprites:
the settings are the asset, not an incidental of the run that made them. Pass the voice_id, not a name, when regenerating.

**One clip per word, not per level.** Filenames come from the word
alone, so a word in two levels has one clip, in the voice of whichever
level generated it first. 99 level-2 hint words are level-1 words and
speak in Eunha; 480 level-3 hint words are level-1 or level-2 words and
speak in Eunha or Soo. Giving them per-level clips would mean
adding the level to the filename in both `gen_vo.py` and
`KoreanTTS.jslib`.

## Regenerating or adding a level

**The order matters.** `pack_vo.py` refuses to pack on top of an existing
pack, because doing so would overwrite the banks with only the loose
files and throw away every level already packed. Close Unity first, or
it imports thousands of loose clips mid-run.

```
python Tools\pack_vo.py --unpack              # banks -> loose clips again
python Tools\gen_vo.py --list-voices          # get the voice_id
python Tools\gen_vo.py --level N --voice <voice_id> --limit 20   # listen first
python Tools\gen_vo.py --level N --voice <voice_id>
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
| 1+2 | 5,934 | 256 | ~279 | 139 KB |
| 1+2+3 (current) | 22,411 | 512 | ~535 | 249 KB |

Bigger banks mean a bigger download per hint; more banks mean more files.
A player buys at most four hints a round (the help budget), so the worst
round costs four bank fetches.

## What the numbers actually were

Measured, not estimated.

- **Billing: 20 words cost 20 credits.** Roughly one credit per request,
  *less* than the character count. Level 3 was about 16,500 credits.
- **Generation: 1,446 clips in 73 seconds** at concurrency 8, zero
  failures. Scales linearly: level 3 (16,477 clips) is ~15 minutes.
- **Clips arrive ~1.1 s long at 18 KB**, of which 5–52% is silence.
  Trimming plus 48 kbps took level 1 from 25.2 MB to 8.0 MB (68%).
- **Shipped clip size:** level-1 average 5.8 KB (Eunha), level-2-only
  average 6.3 KB (Soo), level-3-only average 5.7 KB (Jini). Level 3
  added 89.8 MB.
- **48 kbps was chosen over 32** because this is a pronunciation tool —
  the player is straining to hear ㅂ vs ㅍ vs ㅃ. Changing your mind is
  `--post 32`, which costs no credits and takes seconds.

## Fixing a mispronounced word

Corrections live in `Tools/vo_pronunciations.json`: the word as the game
knows it, and a `say` respelling of how it should sound, which is what
ElevenLabs is sent. The clip keeps the original word's filename. Every
generation run uses the file, so fixes survive a full regeneration.

```
python Tools\pack_vo.py --unpack
python Tools\gen_vo.py --redo 닷새 엿새       # or --redo alone: every word in the file
python Tools\gen_vo.py --post 48
python Tools\pack_vo.py --banks 512
```

`--redo` deletes the clip **and its master**, then regenerates it in the
voice of the lowest level containing the word (from `voices_by_level`).
Deleting only the live clip does nothing: `--post` would re-encode the
bad master straight back. A word with no entry in the file is simply
re-rolled, which is enough for one-off glitches; a word wrong on every
take needs a respelling.

Respell with the standard pronunciation (표준 발음), and check that the
respelling cannot itself be misread. Spelling patterns are only a guide
to which words share a problem: 밭일 is [반닐], not [바칠], and tensing
(김밥 [김빱] vs 불고기 [불고기]) cannot be read off the spelling at all.

**Known so far (2026-11-05).** 닷새 was read as 닷사이: with no context,
the voice took 새 as the short form of 사이. Fixed with 닷새 → 닫쌔, plus
엿새 and 댓새 pre-emptively. Words most at risk of the same misreading,
because their 새 really is short for 사이, still to check by ear: 그새,
고새, 요새, 어느새, 밤새, 이새, 잇새, and 얘기 (short for 이야기). Clip
length does not catch this: 닷새's clip was a normal length for its
level.

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
without a clip is simply silent, so the clips are the only audio there.

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
| `Tools/gen_vo.py` | generate, `--check`, `--list-voices`, `--post`, `--redo` |
| `Tools/vo_pronunciations.json` | pronunciation corrections (respellings sent to ElevenLabs) |
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
- **First-hint cost grew.** `KoreanTTS.jslib` fetches the whole
  `vo_index.json` on the first hint, before the bank. That is now 657 KB
  (211 KB if the host gzips JSON), up from 42 KB with level 1 alone.
  Fixes, if it matters: fetch the index at scene load instead of on the
  first hint, or split it into one small index per bank.
- Level 3 is 72% of the audio (89.8 MB of 124.5 MB) for words only a
  level-3 player sees. It ships because a player only downloads banks
  they hear, but it is most of the build's size on disk and in the zip.
