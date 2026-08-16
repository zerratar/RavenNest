#!/usr/bin/env python3
"""
Builds the dashboard art band assets, and refuses to ship one that cannot be
read over.

    python art-source/build-art.py

Masters live in art-source/dashboard/*.png and are not deployed. This writes
src/RavenNest.Blazor/wwwroot/imgs/dashboard/*.jpg, which are.

Why this is a script rather than a note
---------------------------------------
The tint in ravenfall-tokens.css is 0.65 because that is the lowest alpha at
which the page title and subtitle both clear 4.5:1 across the current set, and
it clears it by about 1%. There is no margin for an image that happens to be
brighter than the ones already here. A note saying "measure new images" gets
skipped; a build step that fails does not.

The three parameters below are not taste
-----------------------------------------
CROP   The band is drawn `background-size: cover; background-position: center
       30%` in a box of BAND_W x BAND_H. Cropping to exactly that geometry means
       no pixel ships that is never drawn, which is most of the file size.

BLUR   Measured: across 0 to 2.5px the worst pixel behind the title moved 2.27
       to 2.31, so blur does almost nothing for legibility. It is here for
       softness and because it roughly halves the encoded size. Pre-blurring
       also keeps `filter: blur()` off a 420px-tall element during scroll.

WIDTH  1200 covers a wide .main without upscaling artefacts that survive the
       blur. Larger buys nothing you can see.
"""

import os
import sys
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "dashboard")
OUT = os.path.join(HERE, "..", "src", "RavenNest.Blazor", "wwwroot", "imgs", "dashboard")

BAND_W, BAND_H = 1064, 420          # the band at a 1280 viewport
OUT_W = 1200
BLUR = 1.2
QUALITY = 72

TINT = 0.65                          # keep in step with --rf-art-tint
BG = (0x12, 0x15, 0x1c)              # --rf-bg, the tint colour
INK = (0xec, 0xe4, 0xd6)             # --rf-ink; the subtitle is promoted to this

# Title is 2rem bold, so it counts as large text. The subtitle is small.
TITLE_MIN, SUB_MIN = 3.0, 4.5


def _lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4


def _lum(p):
    return 0.2126 * _lin(p[0]) + 0.7152 * _lin(p[1]) + 0.0722 * _lin(p[2])


def _ratio(a, b):
    hi, lo = max(a, b), min(a, b)
    return (hi + 0.05) / (lo + 0.05)


def band_crop(im):
    """Reproduce cover + center 30% so the asset is exactly what gets drawn."""
    scale = max(BAND_W / im.width, BAND_H / im.height)
    offset_y = (BAND_H - im.height * scale) * 0.30
    top = max(0, int(round(-offset_y / scale)))
    bottom = min(im.height, int(round((-offset_y + BAND_H) / scale)))
    return im.crop((0, top, im.width, bottom))


def worst_contrast(pixels, ink):
    ink_l = _lum(ink)
    return min(
        _ratio(ink_l, _lum((TINT * BG[0] + (1 - TINT) * p[0],
                            TINT * BG[1] + (1 - TINT) * p[1],
                            TINT * BG[2] + (1 - TINT) * p[2])))
        for p in pixels
    )


def main():
    if not os.path.isdir(SRC):
        print("no masters in " + SRC)
        return 1

    masters = sorted(f for f in os.listdir(SRC) if f.lower().endswith(".png"))
    if not masters:
        print("no .png masters found")
        return 1

    os.makedirs(OUT, exist_ok=True)
    failures = []

    print("%-14s %8s %9s %9s" % ("image", "size", "title", "subtitle"))
    print("-" * 44)

    for name in masters:
        stem = os.path.splitext(name)[0]
        im = Image.open(os.path.join(SRC, name)).convert("RGB")
        band = band_crop(im).resize((OUT_W, int(round(OUT_W * BAND_H / BAND_W))), Image.LANCZOS)
        band = band.filter(ImageFilter.GaussianBlur(BLUR))

        dest = os.path.join(OUT, stem + ".jpg")
        band.save(dest, "JPEG", quality=QUALITY, optimize=True, progressive=True)

        # The title and subtitle sit in the top 126px of the 420px band.
        h = band.height
        title = list(band.crop((0, int(h * 32 / 420), band.width, int(h * 78 / 420)))
                     .resize((160, 26)).getdata())
        sub = list(band.crop((0, int(h * 78 / 420), band.width, int(h * 126 / 420)))
                   .resize((160, 26)).getdata())

        t, s = worst_contrast(title, INK), worst_contrast(sub, INK)
        ok = t >= TITLE_MIN and s >= SUB_MIN
        if not ok:
            failures.append(stem)

        print("%-14s %7.0fKB %8.2f%s %8.2f%s" % (
            stem, os.path.getsize(dest) / 1024,
            t, " " if t >= TITLE_MIN else "!",
            s, " " if s >= SUB_MIN else "!"))

    print("-" * 44)
    if failures:
        print("FAIL: %s cannot be read over at %d%% tint." % (", ".join(failures), TINT * 100))
        print("Either darken the master, or raise --rf-art-tint and TINT together")
        print("and re-run. Do not ship it as it is.")
        return 1

    print("all %d clear %.1f:1 title and %.1f:1 subtitle at %d%% tint"
          % (len(masters), TITLE_MIN, SUB_MIN, TINT * 100))
    return 0


if __name__ == "__main__":
    sys.exit(main())
