#!/usr/bin/env python3
"""Build a full-size card jpg for a weapon out of PDTD's own card chrome.

The generated-art cards in assets/game/cards/ are 784x1168 portrait jpgs. Two of them
(waterdrop, force_field) never got real art and shipped as flat procedural gradients.
Rather than invent something, this composes them the same way the in-run draft popup now
does — PDTD's card plate, its art in the frame's window, its frame and outline over the
top — so the standalone card and the draft card are the same object at two sizes.

Run after tools/extract_pdtd_sprites.py has populated assets/game/pdtd/:

    python tools/gen_pdtd_cards.py

Needs Pillow. Fonts come from assets/fonts/ (Orbitron for the title, Exo2 for body),
the same pair UiTheme uses, so the text matches the rest of the game.
"""

import os

from PIL import Image, ImageDraw, ImageFont

REPO = os.path.join(os.path.dirname(__file__), "..")
PDTD = os.path.join(REPO, "assets", "game", "pdtd")
CARDS = os.path.join(REPO, "assets", "game", "cards")
FONTS = os.path.join(REPO, "assets", "fonts")

W, H = 784, 1168
# The frame's art window, measured off card_front_normal's alpha channel (see
# extract_pdtd_sprites.py) — the same numbers Hud.cs anchors the draft card's art to.
WIN = (0.045, 0.125, 0.955, 0.576)

CARDS_TO_BUILD = [
    {
        "id": "waterdrop",
        "title": "WATERDROP",
        "subtitle": "PLANET DEFENSE SENTINEL",
        "art": "skillicon/waterdrop",
        "art_fill": True,
        "tier": "normal",
        "accent": (90, 200, 235),
        "stats": [("BOUNCE COUNT", "+0.4 / lv"), ("IMPACT DAMAGE", "+60%"), ("BOUNCE RADIUS", "260")],
        "text": "Fires an aqua bolt that ricochets between\nenemies, losing power with each bounce.",
    },
    {
        "id": "force_field",
        "title": "FORCE FIELD",
        "subtitle": "PLANET DEFENSE SENTINEL",
        "art": "icons/force_field",
        "art_fill": False,
        "tier": "super",
        "accent": (150, 130, 245),
        "stats": [("FIELD RADIUS", "+12 / lv"), ("DAMAGE ABSORBED", "+45%"), ("DURATION", "+1.0s / lv")],
        "text": "Projects a gravity barrier that halts and\ncrushes everything caught inside it.",
    },
]


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def layer(path, size):
    im = Image.open(os.path.join(PDTD, path + ".png")).convert("RGBA")
    return im.resize(size, Image.LANCZOS)


def fit(im, box, fill):
    """Scale `im` into `box` (w, h) — cover-and-crop when fill, else letterbox."""
    bw, bh = box
    s = max(bw / im.width, bh / im.height) if fill else min(bw / im.width, bh / im.height)
    im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
    if fill:
        left = (im.width - bw) // 2
        top = (im.height - bh) // 2
        im = im.crop((left, top, left + bw, top + bh))
    return im


def centered(draw, y, text, f, fill):
    w = draw.textbbox((0, 0), text, font=f)[2]
    draw.text(((W - w) // 2, y), text, font=f, fill=fill)


def build(spec):
    card = Image.new("RGBA", (W, H), (8, 10, 16, 255))
    card.alpha_composite(layer("cardui/card_back", (W, H)))

    # art into the frame's window
    x0, y0, x1, y1 = (int(WIN[0] * W), int(WIN[1] * H), int(WIN[2] * W), int(WIN[3] * H))
    art = Image.open(os.path.join(PDTD, spec["art"] + ".png")).convert("RGBA")
    # Trim the transparent margin first — PDTD's emblems and platform renders sit in a
    # lot of empty space, so fitting the raw canvas leaves the subject looking tiny in
    # the middle of the window.
    bbox = art.getbbox()
    if bbox:
        art = art.crop(bbox)
    art = fit(art, (x1 - x0, y1 - y0), spec["art_fill"])
    card.alpha_composite(art, (x0 + ((x1 - x0) - art.width) // 2,
                               y0 + ((y1 - y0) - art.height) // 2))

    card.alpha_composite(layer(f"cardui/card_front_{spec['tier']}", (W, H)))
    card.alpha_composite(layer(f"cardui/card_outline_{spec['tier']}", (W, H)))

    # Draw onto a separate overlay and composite it: ImageDraw writes RGBA channels
    # directly rather than blending, so a translucent fill drawn straight onto `card`
    # comes out opaque (the stat-bar track rendered as solid white the first time).
    over = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    accent = spec["accent"]

    centered(d, int(H * 0.045), spec["title"], font("Orbitron.ttf", 58), (240, 246, 255, 255))
    centered(d, int(H * 0.098), spec["subtitle"], font("Exo2.ttf", 22), accent + (215,))

    # stat rows in the frame's lower band
    fl, fv = font("Exo2.ttf", 26), font("Orbitron.ttf", 26)
    y = int(H * 0.635)
    for label, value in spec["stats"]:
        d.text((int(W * 0.10), y), label, font=fl, fill=(205, 214, 230, 235))
        vw = d.textbbox((0, 0), value, font=fv)[2]
        d.text((int(W * 0.90) - vw, y - 2), value, font=fv, fill=accent + (255,))
        y += 34
        d.rectangle([int(W * 0.10), y, int(W * 0.90), y + 7], fill=(255, 255, 255, 40))
        d.rectangle([int(W * 0.10), y, int(W * 0.10) + int(W * 0.62), y + 7], fill=accent + (220,))
        y += 40

    ft = font("Exo2.ttf", 23)
    for i, line in enumerate(spec["text"].split("\n")):
        centered(d, int(H * 0.878) + i * 30, line, ft, (188, 198, 216, 225))

    card.alpha_composite(over)
    out = os.path.join(CARDS, spec["id"] + ".jpg")
    card.convert("RGB").save(out, quality=92)
    print(f"wrote {out}  {W}x{H}")


if __name__ == "__main__":
    for spec in CARDS_TO_BUILD:
        build(spec)
