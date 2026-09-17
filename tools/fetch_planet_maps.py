#!/usr/bin/env python3
"""Fetch and prepare the equirectangular colour maps the Shop's worlds spin on.

The Shop used to sell five worlds that were flat, static sprites while the home planet
was a real rotating sphere. These are the actual bodies' maps, so Mars/Europa/Titan/
Triton/Pluto render through assets/game/planet.gdshader exactly the way Earth renders
through earth.gdshader.

    python tools/fetch_planet_maps.py            # download + process + write assets

Sources (all free to use; recorded in assets/game/CREDITS.txt):
  Mars    solarsystemscope.com texture pack            CC BY 4.0
  Europa  Voyager / Galileo SSI global mosaic          NASA/JPL/USGS, public domain
  Titan   Cassini PIA22770 global surface mosaic       NASA/JPL-Caltech, public domain
  Triton  Voyager 2 global map (no grid)               NASA/JPL, public domain
  Pluto   New Horizons global colour mosaic            NASA/JHUAPL/SwRI, public domain

PROCESSING, AND WHY IT'S NEEDED
-------------------------------
Two of these are honest partial maps: Voyager 2 and New Horizons each flew past and
only imaged one hemisphere, so the source files carry a large black unimaged region.
Mapped onto a sphere that shows up as a hard black cap, which reads as a broken texture
rather than as missing data. `fill_unimaged` replaces those rows with a vertically
mirrored copy of the imaged terrain nearby, so the globe is continuous. That half of
Triton and Pluto is therefore *invented* — plausible, derived from the real surface, but
not observation. Nothing in the game depends on it being accurate.

Europa's and Titan's mosaics are greyscale (clear-filter and radar products). The `tint`
uniform in planet.gdshader puts their real colour back at render time rather than baking
it in, so the maps on disk stay the unmodified source data.
"""

import io
import os
import sys
import urllib.parse
import urllib.request

REPO = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(REPO, "assets", "game", "planets")
UA = "beyond-game/1.0 (personal sideload build; asset fetch)"
SIZE = (2048, 1024)

COMMONS = "https://commons.wikimedia.org/wiki/Special:FilePath/"

MAPS = {
    # id -> (url, fill_unimaged)
    "mars":   ("https://www.solarsystemscope.com/textures/download/2k_mars.jpg", False),
    "europa": (COMMONS + urllib.parse.quote("Europa Voyager GalileoSSI global mosaic.jpg") + "?width=2048", True),
    "titan":  (COMMONS + urllib.parse.quote("PIA22770-SaturnMoon-Titan-Surface-20181206.jpg") + "?width=2048", True),
    "triton": (COMMONS + urllib.parse.quote("Triton map no grid.jpg") + "?width=2048", True),
    "pluto":  (COMMONS + urllib.parse.quote("Pluto color mapmosaic.jpg") + "?width=2048", True),
}


def get(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=120) as r:
        return r.read()


def fill_unimaged(im, dark=30):
    """Replace the near-black unimaged region with mirrored imaged terrain.

    Done per COLUMN, not per row: the terminator of a flyby mosaic is a curve, not a
    straight line (Triton's imaged region bulges across the middle, Pluto's tapers), so
    a row-based test either leaves black wedges behind or eats real data. For each
    column we find the imaged span and reflect terrain outward from its ends, bouncing
    back and forth inside the span so a short span can still fill a long gap.
    """
    import numpy as np

    a = np.asarray(im.convert("RGB")).astype(np.int16)
    h, w, _ = a.shape
    lit = a.sum(axis=2) > dark * 3          # (h, w) bool
    if lit.all():
        return im

    out = a.copy()
    for x in range(w):
        col = np.flatnonzero(lit[:, x])
        if col.size == 0:
            continue
        top, bot = int(col[0]), int(col[-1])
        span = bot - top
        if span <= 0:
            out[:, x, :] = a[top, x, :]
            continue
        for y in range(h):
            if lit[y, x]:
                continue
            # reflect y into [top, bot] — a triangle wave, so repeated reflection
            # keeps producing plausible terrain instead of a single repeated row
            period = 2 * span
            t = (y - top) % period
            if t < 0:
                t += period
            src = top + (t if t <= span else period - t)
            out[y, x, :] = a[src, x, :]

    from PIL import Image
    return Image.fromarray(out.astype("uint8"), "RGB")


def main():
    from PIL import Image

    os.makedirs(OUT, exist_ok=True)
    for pid, (url, fill) in MAPS.items():
        try:
            raw = get(url)
        except Exception as ex:
            print(f"  !! {pid}: {ex}")
            continue
        im = Image.open(io.BytesIO(raw)).convert("RGB")
        if fill:
            im = fill_unimaged(im)
        im = im.resize(SIZE, Image.LANCZOS)
        path = os.path.join(OUT, f"{pid}_map.jpg")
        # opaque photographic maps: JPEG is ~5x smaller than PNG and identical on a sphere
        im.save(path, quality=88, optimize=True)
        print(f"  {pid:8s} {SIZE[0]}x{SIZE[1]}  <- {url.split('/')[-1][:60]}")


if __name__ == "__main__":
    sys.exit(main())
