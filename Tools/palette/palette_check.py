#!/usr/bin/env python3
"""
palette_check.py -- audit monkey sprites against the canonical palette.

    pip install pillow numpy
    python Tools/palette/palette_check.py Assets/Sprites/monkey_idle.png
    python Tools/palette/palette_check.py Assets/Sprites            # whole tree

Run it on anything new before it goes in the project. It reports, per file,
how far each opaque pixel sits from the nearest canonical colour, and names
any colour that is both common and off-palette -- which is how a sheet from a
new generation session gets caught before it ships next to the old ones.

    --rewrite   snap the file to the palette and save it (keeps alpha intact)
    --tol N     distance that still counts as on-palette (default 8)

Why distance rather than exact match: the sheets are anti-aliased, so most
pixels are legitimately part-way between two palette entries. What matters is
that no pixel is far from ALL of them. A sheet drifting as a whole shows up as
a raised mean; a stray new colour shows up in the offenders list.
"""

import argparse
import colorsys
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PALETTE = json.load(open(os.path.join(HERE, 'palette.json'), encoding='utf-8'))
C = np.array([p['rgb'] for p in PALETTE], float)
NAMES = [p['role'] for p in PALETTE]

# Measured 2026-10-21 across the 14 live sheets: mean drift 3.8-6.0, so a sheet
# reporting much above 6 has either been regenerated or edited in another tool.
EXPECT_MEAN = 6.0


def audit(path, tol):
    a = np.array(Image.open(path).convert('RGBA'))
    op = a[..., 3] > 250
    if not op.any():
        return None
    px = a[op][:, :3].astype(float)
    d2 = ((px[:, None, :] - C[None]) ** 2).sum(2)
    d = np.sqrt(d2.min(1))
    nearest = d2.argmin(1)

    off = d > tol
    offenders = []
    if off.any():
        vals, counts = np.unique(px[off].astype(np.uint8), axis=0, return_counts=True)
        for v, n in sorted(zip(vals, counts), key=lambda t: -t[1])[:5]:
            if n / len(px) < 0.002:        # ignore one-off AA pixels
                break
            r, g, b = v / 255
            h, s, vv = colorsys.rgb_to_hsv(r, g, b)
            offenders.append(('#%02X%02X%02X' % tuple(v), 100 * n / len(px),
                              (round(h * 360), round(s * 100), round(vv * 100))))
    usage = {NAMES[k]: 100 * (nearest == k).mean() for k in range(len(C))}
    return dict(px=len(px), mean=d.mean(), p95=float(np.percentile(d, 95)),
                within=100 * (d <= tol).mean(), offenders=offenders, usage=usage)


def rewrite(path, tol):
    """Pull every pixel onto its nearest palette colour. Alpha is untouched."""
    im = Image.open(path).convert('RGBA')
    a = np.array(im)
    rgb, al = a[..., :3].astype(float), a[..., 3]
    flat = rgb.reshape(-1, 3)
    snapped = C[((flat[:, None, :] - C[None]) ** 2).sum(2).argmin(1)]
    out = snapped.reshape(rgb.shape).astype(np.uint8)
    # leave partly transparent edge pixels alone -- snapping them hardens the
    # silhouette and undoes the anti-aliasing the sheets rely on
    keep = al <= 250
    out[keep] = a[..., :3][keep]
    Image.fromarray(np.dstack([out, al]), 'RGBA').save(path)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('path')
    p.add_argument('--tol', type=float, default=8.0)
    p.add_argument('--rewrite', action='store_true')
    args = p.parse_args()

    files = []
    if os.path.isdir(args.path):
        for root, _, names in os.walk(args.path):
            files += [os.path.join(root, n) for n in sorted(names)
                      if n.lower().endswith('.png')]
    else:
        files = [args.path]

    print('%-40s %8s %8s %9s  %s' % ('file', 'mean', 'p95', 'on-pal', 'notes'))
    worst = 0.0
    for f in files:
        r = audit(f, args.tol)
        if r is None:
            continue
        worst = max(worst, r['mean'])
        note = ''
        if r['mean'] > EXPECT_MEAN:
            note = 'DRIFTED (expected mean <= %.1f)' % EXPECT_MEAN
        print('%-40s %8.1f %8.1f %8.1f%%  %s' % (os.path.basename(f)[:40],
                                                 r['mean'], r['p95'], r['within'], note))
        for hexv, share, hsv in r['offenders']:
            print('%44s off-palette %s  %.1f%% of pixels  HSV %d,%d%%,%d%%'
                  % ('', hexv, share, *hsv))
        if args.rewrite:
            rewrite(f, args.tol)
            print('%44s rewritten to palette' % '')
    print('\nworst mean drift: %.1f' % worst)
    return 1 if worst > EXPECT_MEAN else 0


if __name__ == '__main__':
    sys.exit(main())
