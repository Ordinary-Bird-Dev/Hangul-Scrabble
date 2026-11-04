# Hangulnoree monkey palette

Measured 2026-10-21 from the 14 live sprite sheets (3.5 million opaque pixels),
not chosen by hand. These are the colours actually shipping.

## The seven colours

| Role | Hex | RGB | HSV (H°, S%, V%) | Unity (0–1) | Share | Where it appears |
|---|---|---|---|---|---|---|
| Ink | `#502E1C` | 80, 46, 28 | 21, 65%, 31% | 0.314, 0.181, 0.110 | 23.2% | every outline, eyes, nose, mouth |
| Fur shade | `#9F765B` | 159, 118, 91 | 24, 43%, 62% | 0.624, 0.464, 0.359 | 12.8% | shadowed side of the fur |
| Fur base | `#B98C6A` | 185, 140, 106 | 26, 43%, 73% | 0.725, 0.549, 0.416 | 23.2% | head, body, limbs, tail |
| Fur light | `#BF967B` | 191, 150, 123 | 24, 36%, 75% | 0.749, 0.590, 0.484 | 8.1% | lit side of the fur |
| Skin shade | `#D5B092` | 213, 176, 146 | 27, 31%, 84% | 0.836, 0.690, 0.574 | 4.4% | underside of muzzle and belly |
| Skin base | `#E9C5A0` | 233, 197, 160 | 30, 31%, 91% | 0.912, 0.774, 0.626 | 17.7% | muzzle, belly, inner ears, hands, feet |
| Skin highlight | `#EFD7C0` | 239, 215, 192 | 29, 20%, 94% | 0.937, 0.842, 0.752 | 3.9% | small catch-lights |

HSV is the "colour wheel" form: H in degrees around the wheel, S and V as
percentages, the same numbers Photoshop, Krita, Aseprite and Figma show. Unity's
own picker takes the 0–1 floats in the last column; its HSV sliders are also
0–1, so divide H by 360 there.

## The one rule that keeps new art consistent

**Every colour sits between H 21° and H 30° — a ten degree band.** The palette
is a single warm brown varied only by saturation and value:

```
          V%  ^
          94  |                                   EFD7C0  (S 20)
          91  |                            E9C5A0 (S 31)
          84  |                     D5B092 (S 31)
          75  |              BF967B (S 36)
          73  |       B98C6A (S 43)
          62  |  9F765B (S 43)
          31  | 502E1C (S 65)
              +------------------------------------------>
                            all at H 21-30
```

When you add a tone, move along S and V. Moving around the wheel is what makes
a frame look like it came from somewhere else — that is exactly the failure the
old Win and Wrong sheets had, sitting at a different hue from everything else.

## Not a palette colour

`#936C53` turns up at 6.7% of pixels but **85% of its pixels touch a different
colour**, so it is anti-aliasing between the ink and the fur rather than
anything drawn. Don't add it to a swatch set; it will reappear on its own
wherever ink meets fur.

## How consistent the sheets are right now

Mean distance from each pixel to its nearest palette colour:

| Sheet | Mean | p95 | Within 8 |
|---|---|---|---|
| monkey_idle | 3.9 | 13.3 | 89.8% |
| monkey_frown | 3.8 | 12.7 | 90.9% |
| monkey_jump_grid_1024 | 4.1 | 11.8 | 88.1% |
| monkey_laugh_clap_sheet | 4.6 | 12.4 | 86.2% |
| monkey_sleep | 4.7 | 15.2 | 88.3% |
| confusion frames (×8) | 4.9–5.5 | 15–19 | 81–85% |
| monkey_idle_sit_sheet | 6.0 | 15.7 | 71.7% |

The ink is exact everywhere — spread 0.4, effectively one value. The fills sit
within a few units of their centres, which is anti-aliasing, not drift.
`monkey_idle_sit_sheet` is the loosest of the set and the one to redo first if
you ever want them perfectly uniform.

## Files

| File | Use |
|---|---|
| `monkey_palette.gpl` | load in Aseprite, Krita, GIMP, Inkscape |
| `monkey_palette.png` | printable swatch card with all the numbers |
| `MonkeyPalette.cs` | `MonkeyPalette.FurBase` etc. in Unity code |
| `palette.json` | machine-readable source of truth |
| `palette_check.py` | audit any sprite or folder against the palette |

## Checking new art

```
python Tools/palette/palette_check.py Assets/Sprites/monkey_idle.png
python Tools/palette/palette_check.py Assets/Sprites          # whole tree
python Tools/palette/palette_check.py new_sheet.png --rewrite # snap it
```

It prints mean and p95 distance, the percentage on-palette, and names any
colour that is both common and off-palette. It exits non-zero when a file's
mean exceeds 6.0, so it works as a pre-commit check.

A worked example — the same sheet with its fur pushed 18 units toward green:

```
drift_test.png        8.3   19.3   59.4%  DRIFTED (expected mean <= 6.0)
                      off-palette #B89F6D  13.2% of pixels  HSV 40,41%,72%
```

It catches the hue sliding from 26° to 40° and tells you how much of the frame
is affected.

## Keeping this file honest

The numbers above are generated, not typed. If the art changes, re-measure
rather than editing the table by hand — otherwise the reference and the sprites
drift apart, which is the problem it exists to prevent.
