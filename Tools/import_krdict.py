#!/usr/bin/env python3
"""
import_krdict.py -- convert a krdict "game ready" export into the schema
WordValidator expects.

    python3 -I import_krdict.py <in.json> <out.json>

The krdict exports are bare JSON arrays with a dozen fields per row.
Unity's JsonUtility cannot deserialize a top-level array at all -- it needs
the {"words": [...]} wrapper that WordList declares -- and it silently drops
any field with no matching member on WordEntry. So this does two jobs:
wrap, and keep only the fields the game actually reads.

Dropping the rest is not tidiness. Everything under Assets/Resources is
baked into the WebGL build and parsed at startup whether or not anything
reads it, so `id`, `part_of_speech`, `level`, `senses`, `example_source`,
`example_register` and `flags` would cost download and parse time for
nothing. They stay in the source exports, which are the record.

Two fixes applied on the way through:
  - an empty `prompt` is filled from the first segment of `english`, so the
    clue banner always has something short to show (63 rows across the
    three sets are flagged no_english_prompt)
  - a row with neither `prompt` nor `english` is dropped, because
    ClassicModeController.PickTarget skips empty-english entries anyway and
    an unclued word can never be a target
"""

import json
import os
import re
import sys

# What the game reads. `english` is the full gloss shown on the meaning
# card after a solve; `prompt` is the short form the clue banner uses.
KEEP = ('word', 'prompt', 'english', 'example', 'syllable_count',
        'tile_count', 'band')


# krdict writes this where a Korean word has no English equivalent. It is a
# dictionary note, not a meaning, so it can never serve as a clue -- "Find:
# (no equivalent expression)" is unanswerable.
JUNK = re.compile(r'no equivalent expression', re.I)


def usable(text):
    return bool(text) and not JUNK.search(text)


def short_gloss(english):
    """First usable sense of a multi-sense gloss.

    'go; travel; head for' -> 'go'
    '(no equivalent expression); the second day' -> 'the second day'
    """
    for part in (english or '').split(';'):
        part = part.strip()
        if usable(part):
            return part
    return ''


def convert(src, dst):
    rows = json.load(open(src, encoding='utf-8'))
    if not isinstance(rows, list):
        raise SystemExit('%s: expected a bare JSON array' % src)

    out, filled, unclued, dropped = [], 0, 0, 0
    for r in rows:
        english = (r.get('english') or '').strip()
        prompt = (r.get('prompt') or '').strip()
        if not usable(prompt):
            replacement = short_gloss(english)
            if replacement:
                filled += 1
            else:
                # Nothing in the row can serve as a clue. The word is still
                # a real word -- it stays loaded so the player can build it
                # and still validates -- but PickTarget skips a blank
                # prompt, so it will never be set as a target.
                unclued += 1
            prompt = replacement
        if not prompt and not english:
            dropped += 1
            continue
        entry = {k: r.get(k) for k in KEEP}
        entry['english'] = english
        entry['prompt'] = prompt
        out.append(entry)

    text = json.dumps({'words': out}, ensure_ascii=False, indent=2) + '\n'
    with open(dst, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)

    return dict(name=os.path.basename(dst), rows_in=len(rows), rows_out=len(out),
                prompts_filled=filled, unclued=unclued, dropped=dropped,
                mb=round(len(text.encode('utf-8')) / 1048576, 2))


if __name__ == '__main__':
    print(convert(sys.argv[1], sys.argv[2]))
