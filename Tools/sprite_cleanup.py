#!/usr/bin/env python3
"""
sprite_cleanup.py -- make Hangulnoree's monkey sprite sheets crisp and
consistent with each other.

    pip install pillow numpy scipy

    # one sheet, 4 frames across
    python sprite_cleanup.py in/monkey_idle_sit_sheet.png -o out/ --cols 4 --scale 4

    # a folder of single frames
    python sprite_cleanup.py in/monkey_confusion_frames -o out/ --scale 2

WHAT THIS CAN AND CANNOT FIX
----------------------------
It cannot invent detail. A 144x143 frame shown ~560px tall on a 1080x1920
canvas is a ~4x upscale, and no filter recovers line work that was never
drawn. If you can regenerate the sheets at 512px or more per frame, do that
first -- it beats every option below. This script exists for the frames you
already have, and for keeping newly generated ones consistent with each
other.

What it does fix, in order of how much it matters:

1. STAIR-STEPPED SILHOUETTES.  Naive upscaling of a small sprite either
   blocks up (Point) or goes mushy (Bilinear).  This resamples the
   silhouette as a signed distance field instead of as pixels, so curves
   come out smooth at any scale and the edge stays about one pixel wide.

2. INCONSISTENT LINE COLOUR.  Across the existing sheets the "black"
   outline measures anywhere from #46321E to #5A3D2A, with a per-channel
   spread of 20-28.  Every dark pixel is snapped to one canonical colour,
   so the line reads as the same ink in every frame.

3. DRIFTING COLOUR.  Flat areas are median-filtered and then pulled toward
   a fixed palette (the colours measured off the rig art), which stops
   frame 3 being slightly greener than frame 2.  --snap 0.5 was measured
   best on these sheets: higher values quantise the generator's per-pixel
   noise as well as its colour and the fur goes blotchy.

4. HALOES.  Transparent pixels keep whatever RGB the generator left in
   them, which bleeds as a dark fringe once the GPU filters the texture.
   Their colour is replaced with the nearest opaque pixel's.

5. JITTER BETWEEN FRAMES.  --align shifts every frame so its content sits
   on a common horizontal centre and a common baseline, which removes the
   wobble you get when a generator re-centres each frame slightly
   differently.

Deliberately NOT done: redrawing the interior line work at uniform width.
The taper in those lines is the drawing, and flattening it makes the
monkey look like clip art.  Only the outer silhouette gets a guaranteed
uniform stroke, via --stroke.

UNITY IMPORT SETTINGS, which matter as much as this script
----------------------------------------------------------
On the cleaned sheet, set:
  Filter Mode        Bilinear   (currently Point -- that is what makes the
                                existing outlines look chewed when scaled up)
  Compression        None, or High Quality
  Max Size           >= the cleaned sheet's width
  Mip Maps           off, for UI sprites
Point filtering only looks right if you are going for deliberate pixel art,
which this art is not.
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

try:
    from scipy import ndimage
except ImportError:
    sys.exit("needs scipy:  pip install scipy")


# Measured from the 14 live sprite sheets on 2026-10-21, weighted by area.
# NOT the old rig palette this once held -- the rig is gone, and the sheets
# had drifted a few units away from it. Regenerate with
# Tools/palette/palette_check.py rather than editing by hand.
#
# Every entry sits between H 21 and H 30; new tones vary S and V, not hue.
MONKEY_PALETTE = [
    (0x9F, 0x76, 0x5B),   # Fur shade       shadowed side of the fur
    (0xB9, 0x8C, 0x6A),   # Fur base        head, body, limbs, tail
    (0xBF, 0x96, 0x7B),   # Fur light       lit side of the fur
    (0xD5, 0xB0, 0x92),   # Skin shade      underside of muzzle and belly
    (0xE9, 0xC5, 0xA0),   # Skin base       muzzle, belly, inner ears, hands, feet
    (0xEF, 0xD7, 0xC0),   # Skin highlight  small catch-lights
]
RIG_PALETTE = MONKEY_PALETTE   # old name, kept so existing scripts import
INK = (0x50, 0x2E, 0x1C)


def bleed_alpha(rgb, alpha, iterations=12):
    """Give transparent pixels their nearest opaque neighbour's colour.

    Without this the generator's leftover RGB in transparent pixels bleeds
    into the edge as a dark halo the moment the texture is filtered or
    mip-mapped. Costs nothing at runtime and is invisible until it isn't.
    """
    out = rgb.astype(np.float32).copy()
    known = alpha > 8
    if not known.any():
        return rgb
    idx = ndimage.distance_transform_edt(
        ~known, return_distances=False, return_indices=True)
    return out[tuple(idx)].astype(np.uint8)


def resample_alpha_sdf(alpha, size):
    """Resize a silhouette through a signed distance field.

    Resizing alpha directly resamples a step function: you get stair-steps
    (nearest) or a wide grey ramp (bilinear). The distance field is smooth
    everywhere, so it interpolates cleanly, and re-thresholding it afterwards
    puts the edge back at roughly one pixel wide however far you scale.
    """
    binary = alpha > 127
    if not binary.any():
        return np.zeros(size[::-1], np.uint8)
    d_in = ndimage.distance_transform_edt(binary)
    d_out = ndimage.distance_transform_edt(~binary)
    sdf = d_in - d_out

    scale = size[0] / alpha.shape[1]
    big = np.array(Image.fromarray(sdf.astype(np.float32), mode='F')
                   .resize(size, Image.BICUBIC)) * scale
    # 0.5px of feather either side of the zero crossing
    return (np.clip(big + 0.5, 0, 1) * 255).astype(np.uint8)


def smooth_flats(rgb, alpha, radius):
    """Median-filter the fill areas, leaving the ink alone.

    Snapping straight to a palette quantises the generator's per-pixel noise
    as well as the colour, which comes out as blotches across the fur. A
    median pass first makes each flat area actually flat, so the snap has
    something clean to round. Median rather than blur because it does not
    drag colour across the line work.
    """
    if radius <= 0:
        return rgb
    lum = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
    protect = (lum < 110) | (alpha <= 128)      # ink and edges keep their pixels
    out = rgb.copy()
    for c in range(3):
        med = ndimage.median_filter(rgb[..., c], size=int(radius) * 2 + 1)
        out[..., c] = np.where(protect, rgb[..., c], med)
    return out


def snap_palette(rgb, alpha, palette, ink, ink_threshold=100, strength=1.0):
    """Quantise to a fixed palette; force dark pixels to one ink colour."""
    pal = np.array(palette, np.float32)
    flat = rgb.reshape(-1, 3).astype(np.float32)
    nearest = pal[((flat[:, None, :] - pal[None]) ** 2).sum(2).argmin(1)]
    out = (flat * (1 - strength) + nearest * strength).reshape(rgb.shape)

    lum = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
    is_ink = (lum < ink_threshold) & (alpha > 128)
    out[is_ink] = ink
    return out.astype(np.uint8), is_ink


def add_stroke(rgb, alpha, width, ink):
    """Draw a uniform outline just inside the silhouette.

    Inside, not outside: an outward stroke grows the sprite and breaks any
    alignment you already have.
    """
    if width <= 0:
        return rgb, alpha
    solid = alpha > 127
    inner = ndimage.binary_erosion(solid, iterations=int(round(width)))
    band = solid & ~inner
    out = rgb.copy()
    out[band] = ink
    return out, alpha


def stroke_stats(rgb, alpha):
    """Mean and spread of horizontal dark-run lengths -- the consistency number."""
    lum = 0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]
    dark = (lum < 100) & (alpha > 128)
    runs = []
    for row in dark:
        n = 0
        for v in row:
            if v:
                n += 1
            elif n:
                runs.append(n)
                n = 0
        if n:
            runs.append(n)
    if not runs:
        return 0.0, 0.0, 0
    r = np.array(runs)
    return float(r.mean()), float(r.std()), len(r)


def process(frame, scale, palette, ink, stroke, snap_strength, smooth):
    rgba = np.array(frame.convert('RGBA'))
    rgb, alpha = rgba[..., :3], rgba[..., 3]

    rgb = bleed_alpha(rgb, alpha)

    size = (int(round(frame.width * scale)), int(round(frame.height * scale)))
    if scale != 1:
        big_alpha = resample_alpha_sdf(alpha, size)
        big_rgb = np.array(Image.fromarray(rgb).resize(size, Image.LANCZOS))
    else:
        big_alpha, big_rgb = alpha, rgb

    big_rgb = smooth_flats(big_rgb, big_alpha, smooth)
    big_rgb, _ = snap_palette(big_rgb, big_alpha, palette, ink,
                              strength=snap_strength)
    big_rgb, big_alpha = add_stroke(big_rgb, big_alpha, stroke, ink)

    out = np.dstack([big_rgb, big_alpha])
    return Image.fromarray(out, 'RGBA')


def content_box(im):
    a = np.array(im)[..., 3]
    ys, xs = np.where(a > 16)
    if not len(xs):
        return None
    return xs.min(), ys.min(), xs.max(), ys.max()


def align_frames(frames):
    """Shift each frame to a shared horizontal centre and baseline.

    Generators re-centre every frame a little differently; played back that
    reads as the character twitching. Aligning on the baseline rather than
    the bounding box keeps a raised arm from dragging the whole body down.
    """
    boxes = [content_box(f) for f in frames]
    good = [(f, b) for f, b in zip(frames, boxes) if b]
    if not good:
        return frames
    cx = np.mean([(b[0] + b[2]) / 2 for _, b in good])
    base = np.mean([b[3] for _, b in good])
    out = []
    for f, b in zip(frames, boxes):
        if not b:
            out.append(f)
            continue
        dx = int(round(cx - (b[0] + b[2]) / 2))
        dy = int(round(base - b[3]))
        shifted = Image.new('RGBA', f.size, (0, 0, 0, 0))
        shifted.paste(f, (dx, dy))
        out.append(shifted)
    return out


def slice_sheet(im, cols, rows):
    w, h = im.width // cols, im.height // rows
    return [im.crop((c * w, r * h, (c + 1) * w, (r + 1) * h))
            for r in range(rows) for c in range(cols)]


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('input', help='a sheet .png, or a folder of frames')
    p.add_argument('-o', '--out', required=True)
    p.add_argument('--cols', type=int, default=1, help='frames across a sheet')
    p.add_argument('--rows', type=int, default=1)
    p.add_argument('--scale', type=float, default=4.0)
    p.add_argument('--stroke', type=float, default=0.0,
                   help='uniform silhouette stroke in OUTPUT px (0 = off)')
    p.add_argument('--snap', type=float, default=0.5,
                   help='palette snap strength 0..1. 0.5 measured best on these '
                        'sheets; 0.85+ starts banding the fur')
    p.add_argument('--smooth', type=int, default=2,
                   help='median radius on fill areas before the snap, in output px')
    p.add_argument('--align', action='store_true',
                   help='share a horizontal centre and baseline across frames')
    p.add_argument('--sheet', action='store_true',
                   help='write one sheet back out instead of numbered frames')
    a = p.parse_args()

    os.makedirs(a.out, exist_ok=True)
    if os.path.isdir(a.input):
        names = sorted(f for f in os.listdir(a.input) if f.lower().endswith('.png'))
        frames = [Image.open(os.path.join(a.input, f)).convert('RGBA') for f in names]
        stem = os.path.basename(a.input.rstrip('/\\'))
    else:
        im = Image.open(a.input).convert('RGBA')
        frames = slice_sheet(im, a.cols, a.rows)
        names = ['%s_%02d.png' % (os.path.splitext(os.path.basename(a.input))[0], i)
                 for i in range(len(frames))]
        stem = os.path.splitext(os.path.basename(a.input))[0]

    print('%-26s %-9s %-22s %s' % ('frame', 'size', 'stroke mean/sd before', 'after'))
    done = []
    for f, n in zip(frames, names):
        src = np.array(f)
        m0, s0, _ = stroke_stats(src[..., :3], src[..., 3])
        g = process(f, a.scale, RIG_PALETTE, INK, a.stroke, a.snap, a.smooth)
        dst = np.array(g)
        m1, s1, _ = stroke_stats(dst[..., :3], dst[..., 3])
        print('%-26s %-9s %6.2f / %-6.2f        %6.2f / %.2f'
              % (n, '%dx%d' % g.size, m0, s0, m1, s1))
        done.append(g)

    if a.align:
        done = align_frames(done)

    if a.sheet and len(done) > 1:
        w, h = done[0].size
        cols = a.cols if a.cols > 1 else len(done)
        rows = (len(done) + cols - 1) // cols
        out = Image.new('RGBA', (w * cols, h * rows), (0, 0, 0, 0))
        for i, g in enumerate(done):
            out.paste(g, ((i % cols) * w, (i // cols) * h))
        path = os.path.join(a.out, stem + '_clean.png')
        out.save(path)
        print('\nwrote %s  (%dx%d, %d frames of %dx%d)'
              % (path, out.width, out.height, len(done), w, h))
    else:
        for g, n in zip(done, names):
            g.save(os.path.join(a.out, n))
        print('\nwrote %d frames to %s' % (len(done), a.out))


if __name__ == '__main__':
    main()
