#!/usr/bin/env python3
"""
Re-frame a sheet so the character is the same size in every frame and sits on
a common baseline, then repack and rewrite the .meta.

Only worth doing on a sheet whose character actually drifts in size.
monkey_jump_grid_1024 does: its content grows 359 -> 434 px across four
frames, in width and height together, which is the drawing getting bigger
rather than a jump arc. Every other sheet varies by <= 2% and is left alone --
rescaling a sheet whose size changes are deliberate would flatten the
animation.

The on-screen size of a sprite under Preserve Aspect in a fixed box is
220 * (content_height / rect_height) when the frame is taller than 1.36:1,
which all of these are. So matching that ratio across sheets is what makes
the mascot stop changing size between states.
"""

import os
import re
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sprite_cleanup import (RIG_PALETTE, INK, bleed_alpha, smooth_flats,
                            snap_palette, resample_alpha_sdf)
from batch_sheets import parse_sprites, MAX_TEX, GUTTER

TARGET_H = 400      # character height in frame pixels after normalising
K = 0.955           # content_height / frame_height -> ~210 px on screen
BOTTOM = 0.012      # gap under the feet, as a fraction of frame height


def debleed(frame):
    """Only the halo fix here. The palette snap has to wait until after the
    rescale below -- Lanczos re-blends ink against fill, so snapping first
    just gets undone and the ink comes back out with a spread of ~11 instead
    of the 0 every other sheet has."""
    rgba = np.array(frame.convert('RGBA'))
    rgb, a = rgba[..., :3], rgba[..., 3]
    return Image.fromarray(np.dstack([bleed_alpha(rgb, a), a]), 'RGBA')


def finish(rgb, a):
    rgb = smooth_flats(rgb, a, 2)
    rgb, _ = snap_palette(rgb, a, RIG_PALETTE, INK, strength=0.5)
    return rgb


def content_box(im):
    a = np.array(im)[..., 3]
    ys, xs = np.where(a > 16)
    return (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1) if len(xs) else None


def normalise(png, meta, out_png, out_meta):
    img = Image.open(png).convert('RGBA')
    txt = open(meta, encoding='utf-8').read()
    sprites = parse_sprites(txt)

    # pass 1 -- clean each frame, scale its content to TARGET_H
    pieces = []
    for s in sprites:
        rx, ry, rw, rh = s['rect']
        box = (int(rx), int(img.height - ry - rh), int(rx + rw), int(img.height - ry))
        f = debleed(img.crop(box))
        cb = content_box(f)
        c = f.crop(cb)
        k = TARGET_H / c.height
        nw, nh = max(1, int(round(c.width * k))), TARGET_H
        a = resample_alpha_sdf(np.array(c)[..., 3], (nw, nh))
        rgbb = np.array(c.convert('RGBA').resize((nw, nh), Image.LANCZOS))[..., :3]
        rgbb = finish(rgbb, a)
        pieces.append((s, Image.fromarray(np.dstack([rgbb, a]), 'RGBA')))

    Hf = int(round(TARGET_H / K))
    Wf = max(p.width for _, p in pieces) + 2 * GUTTER
    gap = int(round(BOTTOM * Hf))

    # pass 2 -- pack, every frame the same size, feet on the same line
    n = len(pieces)
    best = None
    for cols in range(1, n + 1):
        rows = (n + cols - 1) // cols
        w, h = cols * (Wf + GUTTER), rows * (Hf + GUTTER)
        if w <= MAX_TEX and h <= MAX_TEX:
            cand = (w * h, max(w, h) / min(w, h), cols, w, h)
            if best is None or cand < best:
                best = cand
    assert best, 'will not fit a 2048 atlas'
    _, _, cols, W, H = best
    atlas = Image.new('RGBA', (W, H), (0, 0, 0, 0))

    placed = []
    for i, (s, p) in enumerate(pieces):
        fx = (i % cols) * (Wf + GUTTER) + GUTTER // 2
        fy = (i // cols) * (Hf + GUTTER) + GUTTER // 2
        atlas.alpha_composite(p, (fx + (Wf - p.width) // 2, fy + Hf - gap - p.height))
        placed.append((s, fx, fy))
    atlas.save(out_png)

    new = txt
    for s, fx, fy in placed:
        uy = H - fy - Hf
        upd = re.sub(
            r'(rect:\s*\n\s*serializedVersion: \d+\s*\n\s*)x: -?[\d.]+(\s*\n\s*)y: -?[\d.]+'
            r'(\s*\n\s*)width: -?[\d.]+(\s*\n\s*)height: -?[\d.]+',
            lambda m: '%sx: %d%sy: %d%swidth: %d%sheight: %d' % (
                m.group(1), fx, m.group(2), uy, m.group(3), Wf, m.group(4), Hf),
            s['raw'], count=1)
        new = new.replace(s['raw'], upd, 1)
    new = re.sub(r'(\n    filterMode: )-?\d+', r'\g<1>1', new, count=1)
    open(out_meta, 'w', encoding='utf-8', newline='\n').write(new)
    return dict(frames=n, frame=(Wf, Hf), atlas=(W, H), ratio=TARGET_H / Hf)


if __name__ == '__main__':
    src, m, op, om = sys.argv[1:5]
    print(normalise(src, m, op, om))
