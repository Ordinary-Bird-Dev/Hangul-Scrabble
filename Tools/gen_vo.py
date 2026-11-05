#!/usr/bin/env python3
"""
gen_vo.py -- pre-generate ElevenLabs audio for the Classic hint button.

    set ELEVENLABS_API_KEY=...            (PowerShell: $env:ELEVENLABS_API_KEY="...")
    python Tools\\gen_vo.py --list-voices
    python Tools\\gen_vo.py --level 1 --voice <id> --limit 20
    python Tools\\gen_vo.py --level 1 --voice <id>

Why pre-generate at all: the hint currently speaks through the browser's
Web Speech API, which needs a Korean voice installed on the player's
machine. Plenty of Windows installs have none, so the player pays the
50-point hint and hears silence -- and since those points count against
the help budget, a silent hint can now lose them the round.

Only words the hint can actually reach are generated. UseHint speaks
Target.word, and Target comes from PickTarget, which requires 2-3
syllables and a non-empty prompt. Level 1 is 1,446 such words.

Filenames are the word's UTF-16 code points in lowercase hex -- 학교
becomes d559ad50.mp3. That keeps every filename pure ASCII, so nothing
depends on the web server handling UTF-8 filenames or on Unicode
normalisation matching between Windows, the zip and the browser. The
same four lines of JavaScript reproduce it at runtime, so no manifest
lookup is needed to find a clip.

Stdlib only -- no pip install. Resumable: a word whose file already
exists is skipped, so a failed or interrupted run is just re-run.
"""

import argparse
import concurrent.futures
import json
import os
import random
import sys
import threading
import time
import urllib.error
import urllib.request

API = 'https://api.elevenlabs.io/v1'
LEVEL_FILES = {1: 'topik1_words.json', 2: 'vocabB_words.json', 3: 'vocabC_words.json'}

# Paths are resolved against the project root -- the folder holding this
# script's Tools/ directory -- not the shell's working directory. Running
# it from C:\Windows\system32 should still find Assets/Resources.
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Pinned so a word generated in six months matches one generated today.
# Change these and you must regenerate everything, or the set will drift
# mid-vocabulary the way the sprite sheets did before the palette file.
DEFAULT_MODEL = 'eleven_multilingual_v2'
DEFAULT_SETTINGS = {
    'stability': 0.5,
    'similarity_boost': 0.75,
    'style': 0.0,
    'use_speaker_boost': True,
    # Slower than conversational. A learner meeting a word for the first
    # time, not a news reader -- this replaces the old SpeechRate = 0.85
    # that was applied to the browser engine.
    'speed': 0.85,
}

_print_lock = threading.Lock()


def clean_key(raw):
    """A pasted key picks up stray whitespace, and sometimes the quote
    characters themselves. Either makes the auth header malformed, and the
    server answers 400 or 401 saying nothing about why."""
    return (raw or '').strip().strip('"\'').strip()


def key():
    """The API key, from the environment or by asking.

    Asking is the default path on purpose. Setting an environment variable
    on Windows means either a $env: assignment that dies with the window,
    or a SetEnvironmentVariable call long enough to get mangled when
    pasted. Neither is worth making someone fight to run a script.
    """
    k = clean_key(os.environ.get('ELEVENLABS_API_KEY'))
    if k:
        return k

    if not sys.stdin.isatty():
        sys.exit('No API key. Set ELEVENLABS_API_KEY, or run this from a '
                 'terminal where it can prompt you.')

    print('ElevenLabs API key needed. Get one at elevenlabs.io -> your '
          'avatar -> API Keys.')
    try:
        import getpass
        raw = getpass.getpass('Paste it here and press Enter (nothing will appear as you type): ')
    except Exception:                                   # noqa: BLE001
        raw = input('Paste it here and press Enter: ')

    k = clean_key(raw)
    if not k:
        sys.exit('Nothing entered.')
    # Cached in-process so the worker threads do not each re-prompt.
    os.environ['ELEVENLABS_API_KEY'] = k
    return k


def offer_to_remember():
    """After a key works, offer to persist it so this is a one-time step."""
    if os.name != 'nt' or not sys.stdin.isatty():
        return
    try:
        answer = input('\nRemember this key for future runs? [y/N] ').strip().lower()
    except Exception:                                   # noqa: BLE001
        return
    if answer not in ('y', 'yes'):
        print('Not saved. It will ask again next time.')
        return
    import subprocess
    # setx writes to the user environment; new terminals pick it up.
    r = subprocess.run(['setx', 'ELEVENLABS_API_KEY', os.environ['ELEVENLABS_API_KEY']],
                       capture_output=True, text=True)
    if r.returncode == 0:
        print('Saved to your Windows user profile. New terminal windows will\n'
              'find it automatically; this one still needs the prompt.\n'
              'To remove it later:  setx ELEVENLABS_API_KEY ""')
    else:
        print('Could not save it (%s). No harm done -- it will just ask again.'
              % (r.stderr or r.stdout).strip()[:120])


def stem(word):
    """학교 -> 'd559ad50'. Mirrored by four lines of JS at runtime."""
    return ''.join('%04x' % ord(c) for c in word)


def key_fingerprint():
    """Enough to spot a truncated or stale paste, without printing the key.

    Reports the CLEANED value -- the bytes actually sent -- so the length
    can be compared against the key on the website directly.
    """
    raw = os.environ.get('ELEVENLABS_API_KEY') or ''
    k = clean_key(raw)
    note = '' if k == raw else '  (stripped %d stray chars)' % (len(raw) - len(k))
    return 'length %d, ends "%s"%s' % (len(k), k[-4:] if len(k) >= 4 else '?', note)


def request(path, method='GET', body=None, raw=False):
    req = urllib.request.Request(API + path, method=method)
    req.add_header('xi-api-key', key())
    data = None
    if body is not None:
        data = json.dumps(body).encode('utf-8')
        req.add_header('Content-Type', 'application/json')
    with urllib.request.urlopen(req, data, timeout=120) as r:
        return r.read() if raw else json.load(r)


def request_or_explain(path, **kw):
    """request(), but turn an auth failure into something actionable.

    A raw traceback for a 401 tells you nothing you can act on, and this
    is the one failure every first run hits.
    """
    try:
        return request(path, **kw)
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            detail = e.read()[:300].decode('utf-8', 'replace')
            sys.exit(
                'HTTP %d from %s -- ElevenLabs rejected the key.\n'
                '  the key in this shell: %s\n'
                '  server said: %s\n\n'
                'Usual causes, in order:\n'
                '  1. The key was rotated and this shell still holds the old one.\n'
                '     Re-set it:  $env:ELEVENLABS_API_KEY = "the-current-key"\n'
                '  2. It was pasted short. Compare the length above against the\n'
                '     key on elevenlabs.io -- they are normally 51 characters.\n'
                '  3. The key is permission-scoped and lacks access to this\n'
                '     endpoint. On elevenlabs.io the key needs at least\n'
                '     "Voices: Read" and "Text to Speech" enabled, or no\n'
                '     restrictions at all.\n'
                % (e.code, path, key_fingerprint(), detail))
        raise


def check():
    """Validate the key and print the quota, so the first call is cheap."""
    sub = request_or_explain('/user/subscription')
    used = sub.get('character_count')
    limit = sub.get('character_limit')
    print('key OK (%s)' % key_fingerprint())
    print('  tier           : %s' % sub.get('tier'))
    if used is not None and limit:
        print('  characters used: %s / %s  (%.1f%%)'
              % (format(used, ','), format(limit, ','), 100 * used / limit))
        print('  remaining      : %s' % format(limit - used, ','))
    reset = sub.get('next_character_count_reset_unix')
    if reset:
        print('  resets         : %s UTC' % time.strftime('%Y-%m-%d', time.gmtime(reset)))
    print('\nRun this again after the probe -- the difference is exactly what\n'
          '20 words cost, which settles the per-request-minimum question.')
    offer_to_remember()


def list_voices():
    voices = request_or_explain('/voices')['voices']
    print('%-24s %-28s %s' % ('voice_id', 'name', 'labels'))
    print('-' * 90)
    for v in voices:
        labels = ', '.join('%s=%s' % kv for kv in (v.get('labels') or {}).items())
        print('%-24s %-28s %s' % (v['voice_id'], v['name'][:28], labels[:40]))
    print('\n%d voices. Preview them at elevenlabs.io/app/voice-library before choosing;'
          ' pick one tagged Korean or multilingual.' % len(voices))


def resolve_voice(given):
    """Accept either a voice_id or the display name the website shows.

    The site shows names like "Eunha - Elegant Korean Female", so that is
    what someone will reach for first. Matching on it costs one API call
    and removes a step that is easy to get wrong.
    """
    voices = request_or_explain('/voices')['voices']
    by_id = {v['voice_id']: v for v in voices}
    if given in by_id:
        return given, by_id[given]['name']

    needle = given.strip().lower()
    # "Eunha - Elegant Korean Female" should match the voice named "Eunha",
    # so compare against the part before any dash as well as the whole.
    head = needle.split(' - ')[0].strip()
    hits = [v for v in voices
            if needle in v['name'].lower() or v['name'].lower() in needle
            or head == v['name'].lower()]
    if len(hits) == 1:
        print('voice "%s" -> %s (%s)' % (given, hits[0]['voice_id'], hits[0]['name']))
        return hits[0]['voice_id'], hits[0]['name']
    if not hits:
        sys.exit('No voice matches %r. Run --list-voices to see what is on your account.'
                 % given)
    sys.exit('%r matches %d voices: %s. Use the voice_id instead.'
             % (given, len(hits), ', '.join(v['name'] for v in hits[:6])))


def speakable(resources, level):
    path = os.path.join(resources, LEVEL_FILES[level])
    rows = json.load(open(path, encoding='utf-8'))['words']
    # Exactly PickTarget's gate.
    words = {r['word'] for r in rows if 2 <= r['syllable_count'] <= 3 and r['prompt']}
    return sorted(words)


# Strip near-silence from both ends, then pad a hair so the first consonant
# is not clipped. Measured on the probe clips: 5-52% of each file was dead
# air before the word, which is both wasted bytes and a delay the player
# hears after tapping Hint.
TRIM_FILTER = ('silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.03,'
               'areverse,'
               'silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.03,'
               'areverse,apad=pad_dur=0.04')


def have_ffmpeg():
    import shutil
    return shutil.which('ffmpeg') is not None


def post(vo_dir, master_dir, bitrate):
    """Trim silence and re-encode every clip, keeping the originals.

    Masters move OUTSIDE Assets/ on purpose: everything under
    StreamingAssets is copied into the build, so leaving 128k originals
    there would ship both versions. Keeping them at all means a future
    change of bitrate costs no credits and no regeneration.
    """
    import shutil
    import subprocess

    if not have_ffmpeg():
        sys.exit('ffmpeg is not on PATH, and the trim/re-encode needs it.\n'
                 '  Install:  winget install ffmpeg\n'
                 '  then open a NEW terminal so PATH refreshes.\n'
                 'Alternatively skip this step and generate at a smaller size\n'
                 'directly:  --format mp3_22050_32')

    clips = sorted(n for n in os.listdir(vo_dir) if n.endswith('.mp3'))
    if not clips:
        sys.exit('No .mp3 files in %s' % vo_dir)
    os.makedirs(master_dir, exist_ok=True)

    before = after = 0
    failed = []
    for i, name in enumerate(clips, 1):
        live = os.path.join(vo_dir, name)
        master = os.path.join(master_dir, name)
        # First pass moves the pristine file aside; later passes re-encode
        # from that master rather than from already-lossy output.
        if not os.path.exists(master):
            shutil.move(live, master)
        before += os.path.getsize(master)

        r = subprocess.run(
            ['ffmpeg', '-v', 'error', '-y', '-i', master, '-af', TRIM_FILTER,
             '-c:a', 'libmp3lame', '-b:a', '%dk' % bitrate, '-ac', '1', live],
            capture_output=True, text=True)
        if r.returncode != 0 or not os.path.exists(live):
            failed.append(name)
            shutil.copy(master, live)          # never leave a gap in the set
            continue
        after += os.path.getsize(live)
        if i % 200 == 0 or i == len(clips):
            print('  %d/%d' % (i, len(clips)))

    print('\n%d clips: %.1f MB -> %.1f MB at %dk trimmed  (%.0f%% smaller)'
          % (len(clips), before / 1048576, after / 1048576, bitrate,
             100 * (1 - after / before) if before else 0))
    print('masters kept outside Assets/ at %s' % os.path.abspath(master_dir))
    if failed:
        print('%d clip(s) could not be re-encoded and were left at full quality: %s'
              % (len(failed), ', '.join(failed[:5])))

    mpath = os.path.join(vo_dir, 'vo_manifest.json')
    if os.path.exists(mpath):
        man = json.load(open(mpath, encoding='utf-8'))
        man['shipped_bitrate_kbps'] = bitrate
        man['shipped_trimmed'] = True
        man['master_format'] = man.get('output_format')
        man['master_dir'] = os.path.abspath(master_dir)
        json.dump(man, open(mpath, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)


# Spoken-text corrections. Some words are mispronounced the same way on
# every take -- 닷새 came back as 닷사이, the voice reading 새 as the short
# form of 사이 -- so re-rolling does not help. For those, ElevenLabs is sent
# a respelling of how the word should SOUND, while the clip keeps the
# original word's filename so the game still finds it. Lives next to this
# script and is tracked in git, so every fix is on record and survives a
# full regeneration.
FIXES_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          'vo_pronunciations.json')


def load_fixes():
    if not os.path.exists(FIXES_PATH):
        return {}
    raw = json.load(open(FIXES_PATH, encoding='utf-8'))
    return {w: (v['say'] if isinstance(v, dict) else v)
            for w, v in raw.items() if not w.startswith('_')}


FIXES = load_fixes()


def synth(word, out_dir, voice, model, fmt, settings, retries=5):
    dst = os.path.join(out_dir, stem(word) + '.mp3')
    if os.path.exists(dst) and os.path.getsize(dst) > 0:
        return word, 'skip', 0

    body = {'text': FIXES.get(word, word), 'model_id': model, 'voice_settings': settings}
    delay = 0.5
    for attempt in range(retries + 1):
        try:
            audio = request('/text-to-speech/%s?output_format=%s' % (voice, fmt),
                            method='POST', body=body, raw=True)
            tmp = dst + '.part'
            with open(tmp, 'wb') as f:
                f.write(audio)
            os.replace(tmp, dst)
            return word, 'ok', len(audio)
        except urllib.error.HTTPError as e:
            # 429 is concurrency or quota; 5xx is transient. Anything else
            # (401 bad key, 422 bad request) will not improve on a retry.
            if e.code not in (429, 500, 502, 503, 504) or attempt == retries:
                detail = e.read()[:200].decode('utf-8', 'replace')
                return word, 'HTTP %d %s' % (e.code, detail), 0
        except Exception as e:                      # noqa: BLE001 - network flake
            if attempt == retries:
                return word, repr(e)[:120], 0
        time.sleep(delay + random.random() * delay)  # backoff with full jitter
        delay = min(delay * 2, 8.0)
    return word, 'gave up', 0


def redo(words, a):
    """Throw away specific clips and generate them again, each in the voice
    of the level that owns it.

    Both copies must go. --post re-encodes every clip from its master in
    VoiceMasters/, so deleting only the live clip would bring the bad take
    straight back on the next --post.
    """
    if os.path.exists(os.path.join(a.out, 'vo_index.json')):
        sys.exit('Clips are packed. Unpack first:\n  python Tools\\pack_vo.py --unpack')
    if not words:
        words = sorted(FIXES)
        if not words:
            sys.exit('No words given and %s is empty.' % FIXES_PATH)
        print('No words given: redoing everything in vo_pronunciations.json (%d).'
              % len(words))

    manifest = json.load(open(os.path.join(a.out, 'vo_manifest.json'), encoding='utf-8'))
    voices = manifest.get('voices_by_level') or {}

    # A word belongs to the lowest level that can ask for it -- that is the
    # level that generated it first, so that is the voice it must keep.
    owner = {}
    for lv in (1, 2, 3):
        for w in speakable(a.resources, lv):
            owner.setdefault(w, lv)

    done = failed = 0
    for word in words:
        lv = owner.get(word)
        if lv is None:
            print('  SKIP %s -- not a hint word in any level' % word)
            continue
        v = voices.get(str(lv))
        if not v:
            print('  SKIP %s -- manifest has no voice for level %d' % (word, lv))
            continue
        name = stem(word) + '.mp3'
        for d in (a.out, a.masters):
            path = os.path.join(d, name)
            if os.path.exists(path):
                os.remove(path)
        said = FIXES.get(word, word)
        _, status, _ = synth(word, a.out, v['voice_id'], v.get('model_id', a.model),
                             a.format, v.get('voice_settings', DEFAULT_SETTINGS))
        note = '' if said == word else '  (sent as %s)' % said
        print('  %-4s %s  level %d, %s%s' % (word, status, lv, v.get('voice_name'), note))
        if status == 'ok':
            done += 1
        else:
            failed += 1

    print('\nregenerated %d, failed %d. Next:\n'
          '  python Tools\\gen_vo.py --post 48\n'
          '  python Tools\\pack_vo.py --banks 512' % (done, failed))
    return 1 if failed else 0


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--level', type=int, choices=(1, 2, 3), default=1)
    p.add_argument('--voice', help='voice_id, or the voice name in quotes '
                                   '(see --list-voices)')
    p.add_argument('--model', default=DEFAULT_MODEL)
    p.add_argument('--format', default='mp3_44100_128',
                   help='mp3_44100_128 for masters, mp3_22050_32 to ship directly')
    p.add_argument('--limit', type=int, help='generate only N words, for a probe')
    p.add_argument('--concurrency', type=int, default=8,
                   help='Pro allows 10; stay under it')
    p.add_argument('--resources', default=os.path.join(PROJECT, 'Assets', 'Resources'))
    p.add_argument('--out', default=os.path.join(PROJECT, 'Assets', 'StreamingAssets', 'vo'))
    p.add_argument('--list-voices', action='store_true')
    p.add_argument('--check', action='store_true',
                   help='validate the key and show the quota, then exit')
    p.add_argument('--post', type=int, metavar='KBPS', nargs='?', const=48,
                   help='trim silence and re-encode existing clips at KBPS '
                        '(default 48); keeps originals outside Assets/')
    p.add_argument('--masters', default=os.path.join(PROJECT, 'VoiceMasters'),
                   help='where untouched originals are kept (outside Assets/)')
    p.add_argument('--redo', nargs='*', metavar='WORD',
                   help='delete and regenerate these words (clip and master), each '
                        'in its own level\'s voice; with no words, every word in '
                        'Tools/vo_pronunciations.json')
    a = p.parse_args()

    if a.redo is not None:
        return redo(a.redo, a)
    if a.post is not None:
        post(a.out, a.masters, a.post)
        return 0
    if a.check:
        check()
        return 0
    if a.list_voices:
        list_voices()
        return 0
    if not a.voice:
        sys.exit('--voice is required. Run --list-voices first, then pass the id\n'
                 '  or the name in quotes:  --voice "Eunha"')
    voice_id, voice_name = resolve_voice(a.voice)

    words = speakable(a.resources, a.level)
    if a.limit:
        # A seeded spread rather than the first N, so the probe covers
        # short and long words instead of everything starting in ㄱ.
        words = random.Random(20261029).sample(words, min(a.limit, len(words)))
        words.sort()

    os.makedirs(a.out, exist_ok=True)
    print('level %d: %d words -> %s' % (a.level, len(words), os.path.abspath(a.out)))
    print('voice %s  model %s  format %s  concurrency %d\n'
          % (voice_name, a.model, a.format, a.concurrency))

    done = {'ok': 0, 'skip': 0, 'fail': 0}
    total_bytes = 0
    started = time.time()

    with concurrent.futures.ThreadPoolExecutor(a.concurrency) as pool:
        futures = [pool.submit(synth, w, a.out, voice_id, a.model, a.format, DEFAULT_SETTINGS)
                   for w in words]
        for i, f in enumerate(concurrent.futures.as_completed(futures), 1):
            word, status, size = f.result()
            total_bytes += size
            if status == 'ok':
                done['ok'] += 1
            elif status == 'skip':
                done['skip'] += 1
            else:
                done['fail'] += 1
                with _print_lock:
                    print('  FAIL %-6s %s' % (word, status))
            if i % 50 == 0 or i == len(words):
                with _print_lock:
                    print('  %d/%d  ok=%d skip=%d fail=%d  %.1f MB  %.0fs'
                          % (i, len(words), done['ok'], done['skip'], done['fail'],
                             total_bytes / 1048576, time.time() - started))

    # The record of how this set was made. Without it, words added later
    # come back in a different voice and the vocabulary drifts audibly.
    #
    # Voices are recorded PER LEVEL. Level 1 is Eunha and level 2 is Soo by
    # choice (2026-11-03), so a single top-level voice_id would be wrong for
    # one of them, and whichever level ran last would erase the other's
    # record. A --limit probe is not recorded: it is a listening test, not
    # a level.
    manifest_path = os.path.join(a.out, 'vo_manifest.json')
    manifest = {}
    if os.path.exists(manifest_path):
        manifest = json.load(open(manifest_path, encoding='utf-8'))

    by_level = manifest.get('voices_by_level') or {}
    if not by_level and manifest.get('voice_id'):
        # Migrate the old single-voice manifest: every level it lists was
        # made with the voice it names.
        for lv in manifest.get('levels', []):
            by_level[str(lv)] = {
                'voice_id': manifest['voice_id'],
                'voice_name': manifest.get('voice_name'),
                'model_id': manifest.get('model_id'),
                'voice_settings': manifest.get('voice_settings'),
                'generated_utc': manifest.get('generated_utc'),
            }
    for k in ('voice_id', 'voice_name', 'model_id', 'voice_settings', 'generated_utc'):
        manifest.pop(k, None)

    if a.limit:
        print('\n--limit probe: manifest not updated.')
    else:
        by_level[str(a.level)] = {
            'voice_id': voice_id, 'voice_name': voice_name,
            'model_id': a.model, 'voice_settings': DEFAULT_SETTINGS,
            'generated_utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
        }
        manifest['voices_by_level'] = dict(sorted(by_level.items()))
        manifest['output_format'] = a.format
        manifest['filename_scheme'] = 'lowercase hex of each UTF-16 code point, .mp3'
        manifest['shared_words_note'] = (
            'One clip per word, not per level. A word in more than one level '
            'keeps the voice of the level that generated it first.')
        manifest['levels'] = sorted(set(manifest.get('levels', [])) | {a.level})
        manifest['clip_count'] = len([n for n in os.listdir(a.out) if n.endswith('.mp3')])
        json.dump(manifest, open(manifest_path, 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=2)

    print('\nok=%d skip=%d fail=%d   %.1f MB   %.0fs'
          % (done['ok'], done['skip'], done['fail'], total_bytes / 1048576,
             time.time() - started))
    print('manifest: %s' % os.path.abspath(manifest_path))
    if done['ok']:
        avg = total_bytes / done['ok']
        print('average clip %.1f KB  -> full level 1 (1,446) would be %.1f MB'
              % (avg / 1024, avg * 1446 / 1048576))
    return 1 if done['fail'] else 0


if __name__ == '__main__':
    sys.exit(main())
