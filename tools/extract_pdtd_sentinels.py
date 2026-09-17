#!/usr/bin/env python3
"""Pull PDTD's canonical per-sentinel art out of its Unity bundles.

Companion to extract_pdtd_sprites.py. That one walks a flat list of
bundle -> output-name pairs; this one exists because the sentinel art needs
*compositing*, which that table can't express.

PDTD keeps one directory per sentinel under assets/Res/skins/sentinel/, named
`000NN_<weapon>`, and inside its `ui/` folder:

    <name>_mainicon.png    the craft on transparent alpha  (in-world model)
    <name>_bg.png          the sky/planet plate behind it  (card backdrop)
    <name>_itemicon.png    a small square badge
    <name>_skindisplay.png a large promo render

The in-run tile and the draft card in PDTD are `bg` with `mainicon` composited
over it — that's why the card art reads as "a ship photographed over a planet".
This script writes both halves:

    assets/game/pdtd/icons/<kind>.png   mainicon alone, for the orbiting sentinel
    assets/game/pdtd/tiles/<kind>.png   bg + mainicon, for cards and the HUD bar

WHY THIS REPLACES THE OLD icons/ SET. The previous pass sourced the orbiting
models ad hoc from whatever bundle looked right, which left the mapping
unverifiable — several were plausible but unconfirmed, and there was no source
at all for chain lightning / ball lightning / radiation zone / force field. The
`000NN_<weapon>` directories are PDTD's own authoritative naming, so taking all
eleven from there makes every kind correct by construction instead of by eye.

(Note: PDTD's Waterdrop sentinel really is a teardrop-shaped craft rather than a
winged ship — that's its actual model, not a card illustration leaking through.)

Standing CLAUDE.md §4 user exception: sideload-only personal build, PDTD assets
are fine, credited in assets/game/CREDITS.txt regardless.

    python tools/extract_pdtd_sentinels.py --apply
"""

import argparse
import io
import os
import zipfile

XAPK = os.path.expanduser("~/Downloads/Planet+Defense_+Space+TD.xapk")
INNER = "UnityDataAssetPack.apk"
REPO = os.path.join(os.path.dirname(__file__), "..")
OUT_ROOT = os.path.join(REPO, "assets", "game", "pdtd")

# Beyond's OrbitalWeaponDef.Kind -> PDTD's skins/sentinel directory.
# "missile" and "railgun" have no orbital kind in Beyond right now; they're kept
# because the hero missile barrage and the Codex both want the art.
KINDS = {
    "missile": "00001_missile",
    "waterdrop": "00002_waterdrop",
    "railgun": "00003_railgun",
    "laser": "00004_laser",
    "beam_laser": "00005_beam",
    "rad_line": "00006_radiationlink",
    "rad_zone": "00007_radiationzone",
    "space_bomb": "00008_spacebomb",
    "force_field": "00009_gravitynova",
    "lightning": "00010_chainlightning",
    "shock_orb": "00011_balllightning",
}

MODEL_MAX = 384    # in-world sprite; drawn at ~62-100 px, so this is plenty
TILE = 512         # square card/HUD tile


def open_inner():
    with zipfile.ZipFile(XAPK) as outer, outer.open(INNER) as f:
        return zipfile.ZipFile(io.BytesIO(f.read()))


def load_texture(zf, member):
    import UnityPy
    UnityPy.config.FALLBACK_UNITY_VERSION = "6000.0.80f1"
    env = UnityPy.load(zf.read(member))
    best = None
    for obj in env.objects:
        if obj.type.name != "Texture2D":
            continue
        img = obj.read().image
        if best is None or img.width * img.height > best.width * best.height:
            best = img
    return best


def cover(im, size):
    """Scale-and-centre-crop `im` to a square of `size` — PDTD's bg plates are
    4:3-ish and the tile is square, so letterboxing would show seams."""
    from PIL import Image
    s = max(size / im.width, size / im.height)
    im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    left = (im.width - size) // 2
    top = (im.height - size) // 2
    return im.crop((left, top, left + size, top + size))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if not args.apply:
        ap.print_help()
        return

    from PIL import Image

    zf = open_inner()
    names = set(zf.namelist())

    def find(sdir, suffix):
        """`ui/<sdir>_<suffix>` as .png.ab, falling back to .jpg.ab — a couple of
        the bg plates ship as jpg (radiationline's skin bg, for one)."""
        for ext in ("png", "jpg"):
            m = f"assets/Res/skins/sentinel/{sdir}/ui/{sdir}_{suffix}.{ext}.ab"
            if m in names:
                return m
        return None

    icons_dir = os.path.join(OUT_ROOT, "icons")
    tiles_dir = os.path.join(OUT_ROOT, "tiles")
    os.makedirs(icons_dir, exist_ok=True)
    os.makedirs(tiles_dir, exist_ok=True)

    for kind, sdir in KINDS.items():
        mi = find(sdir, "mainicon")
        if mi is None:
            print(f"  !! {kind}: no mainicon under {sdir}")
            continue
        model = load_texture(zf, mi)
        if model is None:
            print(f"  !! {kind}: no Texture2D in {mi}")
            continue
        model = model.convert("RGBA")

        # --- in-world model: the craft alone, trimmed of its transparent margin so
        # every kind draws at a consistent on-screen size regardless of how much
        # empty canvas PDTD left around it.
        out = model.crop(model.getbbox() or (0, 0, model.width, model.height))
        if max(out.size) > MODEL_MAX:
            s = MODEL_MAX / max(out.size)
            out = out.resize((max(1, round(out.width * s)), max(1, round(out.height * s))), Image.LANCZOS)
        out.save(os.path.join(icons_dir, kind + ".png"))

        # --- card / HUD tile: bg plate with the craft over it, the way PDTD builds it
        bg_member = find(sdir, "bg")
        tile = Image.new("RGBA", (TILE, TILE), (10, 14, 24, 255))
        if bg_member is not None:
            bg = load_texture(zf, bg_member)
            if bg is not None:
                tile.alpha_composite(cover(bg.convert("RGBA"), TILE))
        craft = model.crop(model.getbbox() or (0, 0, model.width, model.height))
        # ~78% of the tile, matching how much of the frame the ship fills in PDTD
        s = (TILE * 0.78) / max(craft.size)
        craft = craft.resize((max(1, round(craft.width * s)), max(1, round(craft.height * s))), Image.LANCZOS)
        tile.alpha_composite(craft, ((TILE - craft.width) // 2, (TILE - craft.height) // 2))
        tile.save(os.path.join(tiles_dir, kind + ".png"))

        print(f"  {kind:12s} <- {sdir}   model {out.size}   tile {TILE}x{TILE}")


if __name__ == "__main__":
    main()
