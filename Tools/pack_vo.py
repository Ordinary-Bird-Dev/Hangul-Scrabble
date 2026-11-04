#!/usr/bin/env python3
"""
pack_vo.py -- pack the per-word clips into a few bank files.

    python Tools\\pack_vo.py            # 128 banks, the default
    python Tools\\pack_vo.py --banks 64
    python Tools\\pack_vo.py --unpack   # put the loose clips back

Why: itch.io refuses an HTML5 zip containing more than 1000 files, and a
Unity build plus 1,446 one-word clips is 1,468. The clips are only 8 MB
in total -- the byte count was never the problem, the file count was.

So the clips are concatenated into N bank files with an index recording
where each one starts. At runtime KoreanTTS.jslib fetches the single bank
holding the word it needs, slices those bytes out and plays them as a
Blob. A player still downloads only the audio they actually hear, which
is the whole reason this lives in StreamingAssets rather than being
baked into the build.

Banks are filled round-robin over the clips sorted by name, so every bank
ends up about the same size. There is no locality to exploit -- Classic
picks its target at random, so any grouping is as good as any other.

Reversible: --unpack rebuilds the loose .mp3 files from the banks, so a
regeneration run can skip-what-exists as before.
"""

import argparse
import json
import os
import sys

INDEX_NAME = 'vo_index.json'
BANK_FMT = 'bank%02d.bin'


def pack(vo_dir, banks):
    # Packing on top of an existing pack would overwrite bank00..bankNN and
    # the index with only the loose clips -- silently throwing away every
    # level already packed. Adding a level means unpack, generate, re-pack.
    if os.path.exists(os.path.join(vo_dir, INDEX_NAME)):
        existing = json.load(open(os.path.join(vo_dir, INDEX_NAME), encoding='utf-8'))
        sys.exit(
            'Already packed: %s holds %d clips in %d banks.\n'
            'Packing again would overwrite them with just the loose files.\n'
            'To add a level:\n'
            '  python Tools\\pack_vo.py --unpack\n'
            '  python Tools\\gen_vo.py --level N --voice "Eunha"\n'
            '  python Tools\\gen_vo.py --post 48\n'
            '  python Tools\\pack_vo.py --banks %d'
            % (INDEX_NAME, len(existing.get('clips', {})),
               len(existing.get('banks', [])), banks))

    clips = sorted(n for n in os.listdir(vo_dir) if n.endswith('.mp3'))
    if not clips:
        sys.exit('No .mp3 files in %s -- already packed?' % vo_dir)
    banks = max(1, min(banks, len(clips)))

    # stem -> [bank, offset, length]
    entry = {}
    buffers = [[] for _ in range(banks)]
    offsets = [0] * banks

    for i, name in enumerate(clips):
        b = i % banks
        with open(os.path.join(vo_dir, name), 'rb') as f:
            data = f.read()
        if not data:
            print('  skipping empty %s' % name)
            continue
        entry[name[:-4]] = [b, offsets[b], len(data)]
        buffers[b].append(data)
        offsets[b] += len(data)

    written = []
    for b in range(banks):
        path = os.path.join(vo_dir, BANK_FMT % b)
        with open(path, 'wb') as f:
            for chunk in buffers[b]:
                f.write(chunk)
        written.append((BANK_FMT % b, offsets[b]))

    index = {
        'banks': [n for n, _ in written],
        'clips': entry,
        'note': 'offsets are byte ranges inside the named bank; see KoreanTTS.jslib',
    }
    with open(os.path.join(vo_dir, INDEX_NAME), 'w', encoding='utf-8') as f:
        json.dump(index, f, separators=(',', ':'))     # compact: this ships

    for name in clips:
        os.remove(os.path.join(vo_dir, name))

    total = sum(s for _, s in written)
    isize = os.path.getsize(os.path.join(vo_dir, INDEX_NAME))
    print('packed %d clips into %d banks' % (len(entry), banks))
    print('  audio      %.2f MB' % (total / 1048576))
    print('  index      %.0f KB' % (isize / 1024))
    print('  bank size  %.0f KB average, %.0f KB largest'
          % (total / banks / 1024, max(s for _, s in written) / 1024))
    print('  files in this folder now: %d  (was %d)' % (banks + 1, len(clips) + 1))


def unpack(vo_dir):
    ipath = os.path.join(vo_dir, INDEX_NAME)
    if not os.path.exists(ipath):
        sys.exit('No %s in %s' % (INDEX_NAME, vo_dir))
    index = json.load(open(ipath, encoding='utf-8'))
    data = []
    for name in index['banks']:
        with open(os.path.join(vo_dir, name), 'rb') as f:
            data.append(f.read())

    for st, (b, off, length) in index['clips'].items():
        with open(os.path.join(vo_dir, st + '.mp3'), 'wb') as f:
            f.write(data[b][off:off + length])
    for name in index['banks']:
        os.remove(os.path.join(vo_dir, name))
    os.remove(ipath)
    print('unpacked %d clips; banks and index removed' % len(index['clips']))


if __name__ == '__main__':
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    p = argparse.ArgumentParser()
    p.add_argument('--banks', type=int, default=128)
    p.add_argument('--unpack', action='store_true')
    p.add_argument('--dir', default=os.path.join(here, 'Assets', 'StreamingAssets', 'vo'))
    a = p.parse_args()
    if a.unpack:
        unpack(a.dir)
    else:
        pack(a.dir, a.banks)
