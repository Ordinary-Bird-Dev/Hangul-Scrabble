#!/usr/bin/env python3
"""
batch_sheets.py -- upscale and clean a Unity sprite sheet IN PLACE, rewriting
its .meta so every existing animation keeps working.

Why this is not just "run sprite_cleanup.py on the png":

  * A multi-sprite sheet's .meta stores each frame's rect in PIXELS. Replace
    the png with a 4x one and every rect points at the wrong place -- the
    animations break silently.
  * Several of these sheets are already WIDER than the importer's
    maxTextureSize (2432 and 3072 vs 2048), so Unity is quietly downsampling
    them on import. Scaling the strip up makes that worse. Frames have to be
    repacked into a squarer atlas that fits.
  * Frames in these sheets are tight-packed with zero padding. Once the
    filter mode is Bilinear, neighbouring frames bleed into each other. The
    repack adds a gutter.

What is preserved exactly, so references survive: every sprite's name,
spriteID, internalID, alignment, pivot and the internalIDToNameTable.
Rects and borders are recomputed; spritePixelsToUnits is multiplied by the
same scale so anything calling SetNativeSize is unchanged.
"""

import os
import re
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sprite_cleanup import (RIG_PALETTE, INK, bleed_alpha, resample_alpha_sdf,
                            smooth_flats, snap_palette)

MAX_TEX = 2048
GUTTER = 8          # transparent px between frames, so bilinear cannot bleed


def parse_sprites(meta):
    """Pull one dict per sprite out of the meta's `sprites:` block."""
    block = meta.split('    sprites:', 1)
    if len(block) < 2:
        return []
    out = []
    for chunk in re.split(r'\n    - serializedVersion: \d+\n', block[1])[1:]:
        m = re.search(r'x: (-?[\d.]+)\s*\n\s*y: (-?[\d.]+)\s*\n\s*'
                      r'width: (-?[\d.]+)\s*\n\s*height: (-?[\d.]+)', chunk)
        if not m:
            continue
        out.append({
            'name': re.search(r'name: (.*)', chunk).group(1).strip(),
            'rect': [float(v) for v in m.groups()],
            'border': [float(v) for v in re.search(
                r'border: \{x: (-?[\d.]+), y: (-?[\d.]+), z: (-?[\d.]+), w: (-?[\d.]+)\}',
                chunk).groups()],
            'raw': chunk,
        })
    return out


def choose_scale(sprites, want=4.0):
    """Biggest scale that fits MAX_TEX, and the column count that wastes least.

    Taking the first column count that fits gives a 1-wide tower; picking the
    smallest total area instead keeps the atlas squarish, which is both less
    texture memory and friendlier to the importer.
    """
    # Continuous search, not a fixed ladder: sheet3_strip's three 1024px
    # frames need 0.99 to fit a 2x2 grid once the gutter is counted, and a
    # 0.8% shrink is nothing next to the 0.67x downsample the importer is
    # applying to it today.
    scale = want
    scales = []
    while scale >= 0.3:
        scales.append(round(scale, 4))
        scale *= 0.98
    for scale in scales:
        fits = []
        for cols in range(1, len(sprites) + 1):
            w, h = pack_size(sprites, scale, cols)
            if w <= MAX_TEX and h <= MAX_TEX:
                fits.append((w * h, max(w, h) / min(w, h), cols))
        if fits:
            fits.sort()
            return scale, fits[0][2]
    return 1.0, len(sprites)


def pack_size(sprites, scale, cols):
    rows, cw, ch = [], 0, 0
    W = H = 0
    for i, s in enumerate(sprites):
        w = int(round(s['rect'][2] * scale)) + GUTTER
        h = int(round(s['rect'][3] * scale)) + GUTTER
        cw += w
        ch = max(ch, h)
        if (i + 1) % cols == 0 or i == len(sprites) - 1:
            W = max(W, cw)
            H += ch
            cw, ch = 0, 0
    return W, H


def process_sheet(png_path, meta_path, out_png, out_meta, want_scale=4.0,
                  snap=0.5, smooth=2):
    img = Image.open(png_path).convert('RGBA')
    meta = open(meta_path, encoding='utf-8').read()
    sprites = parse_sprites(meta)
    if not sprites:
        return None

    scale, cols = choose_scale(sprites, want_scale)
    W, H = pack_size(sprites, scale, cols)
    atlas = Image.new('RGBA', (W, H), (0, 0, 0, 0))

    # Unity rects are measured from the BOTTOM-LEFT of the texture; PIL from
    # the top-left. Getting this backwards silently shuffles the frames.
    placed, x, y, row_h = [], 0, 0, 0
    for i, s in enumerate(sprites):
        rx, ry, rw, rh = s['rect']
        box = (int(rx), int(img.height - ry - rh), int(rx + rw), int(img.height - ry))
        frame = img.crop(box)

        rgba = np.array(frame)
        rgb, alpha = rgba[..., :3], rgba[..., 3]
        rgb = bleed_alpha(rgb, alpha)
        nw, nh = int(round(rw * scale)), int(round(rh * scale))
        big_a = resample_alpha_sdf(alpha, (nw, nh))
        big_c = np.array(Image.fromarray(rgb).resize((nw, nh), Image.LANCZOS))
        big_c = smooth_flats(big_c, big_a, smooth)
        big_c, _ = snap_palette(big_c, big_a, RIG_PALETTE, INK, strength=snap)
        clean = Image.fromarray(np.dstack([big_c, big_a]), 'RGBA')

        if i % cols == 0 and i:
            y += row_h
            x, row_h = 0, 0
        atlas.paste(clean, (x + GUTTER // 2, y + GUTTER // 2))
        placed.append((s, x + GUTTER // 2, y + GUTTER // 2, nw, nh))
        x += nw + GUTTER
        row_h = max(row_h, nh + GUTTER)

    atlas.save(out_png)

    # --- rewrite the meta ------------------------------------------------
    new = meta
    for s, px, py, nw, nh in placed:
        uy = H - py - nh                       # back to bottom-left origin
        old = s['raw']
        upd = re.sub(
            r'(rect:\s*\n\s*serializedVersion: \d+\s*\n\s*)x: -?[\d.]+(\s*\n\s*)y: -?[\d.]+'
            r'(\s*\n\s*)width: -?[\d.]+(\s*\n\s*)height: -?[\d.]+',
            lambda m: '%sx: %d%sy: %d%swidth: %d%sheight: %d' % (
                m.group(1), px, m.group(2), uy, m.group(3), nw, m.group(4), nh),
            old, count=1)
        b = [int(round(v * scale)) for v in s['border']]
        upd = re.sub(r'border: \{x: -?[\d.]+, y: -?[\d.]+, z: -?[\d.]+, w: -?[\d.]+\}',
                     'border: {x: %d, y: %d, z: %d, w: %d}' % tuple(b), upd, count=1)
        assert old in new, 'sprite block not found: ' + s['name']
        new = new.replace(old, upd, 1)

    new = re.sub(r'(\n    filterMode: )-?\d+', r'\g<1>1', new, count=1)
    new = re.sub(r'(\n  spritePixelsToUnits: )[\d.]+',
                 lambda m: '%s%g' % (m.group(1), 100 * scale), new, count=1)
    open(out_meta, 'w', encoding='utf-8', newline='\n').write(new)

    return dict(scale=scale, cols=cols, old=img.size, new=(W, H),
                frames=len(sprites))


def verify(src_meta, dst_meta, dst_png):
    """The whole job is worthless if a reference moved. Check that none did."""
    a, b = open(src_meta, encoding='utf-8').read(), open(dst_meta, encoding='utf-8').read()
    errs = []
    for field in ('spriteID', 'internalID', 'name', 'guid'):
        ia = re.findall(r'%s: (\S+)' % field, a)
        ib = re.findall(r'%s: (\S+)' % field, b)
        if ia != ib:
            errs.append('%s list changed (%d -> %d)' % (field, len(ia), len(ib)))
    if re.findall(r'alignment: (\d+)', a) != re.findall(r'alignment: (\d+)', b):
        errs.append('alignment changed')
    if re.findall(r'pivot: \{[^}]*\}', a) != re.findall(r'pivot: \{[^}]*\}', b):
        errs.append('pivot changed')

    W, H = Image.open(dst_png).size
    if W > MAX_TEX or H > MAX_TEX:
        errs.append('atlas %dx%d exceeds maxTextureSize %d' % (W, H, MAX_TEX))
    boxes = []
    for s in parse_sprites(b):
        x, y, w, h = s['rect']
        if x < 0 or y < 0 or x + w > W or y + h > H:
            errs.append('%s rect falls outside the atlas' % s['name'])
        boxes.append((x, y, w, h, s['name']))
    for i in range(len(boxes)):
        for j in range(i + 1, len(boxes)):
            ax, ay, aw, ah, an = boxes[i]
            bx, by, bw, bh, bn = boxes[j]
            if ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah:
                errs.append('rects overlap: %s / %s' % (an, bn))
    if re.search(r'\n    filterMode: 1', b) is None:
        errs.append('filterMode not set to Bilinear')
    return errs
