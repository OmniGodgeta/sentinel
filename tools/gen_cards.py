import math, random
from PIL import Image, ImageDraw, ImageFilter

W, H = 784, 1168

def hexrgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

def lerp(a, b, t):
    return tuple(int(a[i] + (b[i]-a[i])*t) for i in range(3))

def bg(accent, seed):
    rnd = random.Random(seed)
    dark = (6, 7, 12)
    im = Image.new('RGB', (W, H), dark)
    px = im.load()
    cx, cy = W*0.5, H*0.38
    maxr = math.hypot(W, H) * 0.65
    for y in range(0, H, 2):
        for x in range(0, W, 2):
            d = math.hypot(x-cx, y-cy) / maxr
            t = max(0.0, 1.0 - d)
            t = t ** 1.6
            col = lerp(dark, accent, t*0.55)
            px[x, y] = col
            if x+1 < W: px[x+1, y] = col
            if y+1 < H: px[x, y+1] = col
            if x+1 < W and y+1 < H: px[x+1, y+1] = col
    im = im.filter(ImageFilter.GaussianBlur(3))
    draw = ImageDraw.Draw(im, 'RGBA')
    # starfield
    for _ in range(220):
        x = rnd.uniform(0, W); y = rnd.uniform(0, H)
        r = rnd.uniform(0.5, 1.8)
        a = rnd.randint(40, 160)
        draw.ellipse([x-r, y-r, x+r, y+r], fill=(255, 255, 255, a))
    # radial energy rings
    for i in range(4):
        r = maxr * (0.18 + i*0.14)
        a = max(0, 90 - i*20)
        bbox = [cx-r, cy-r, cx+r, cy+r]
        draw.ellipse(bbox, outline=(*accent, a), width=3)
    # vignette
    vign = Image.new('L', (W, H), 0)
    vd = ImageDraw.Draw(vign)
    vd.ellipse([-W*0.3, -H*0.2, W*1.3, H*1.15], fill=255)
    vign = vign.filter(ImageFilter.GaussianBlur(120))
    black = Image.new('RGB', (W, H), (2, 2, 4))
    im = Image.composite(im, black, vign)
    return im

def glyph(draw, kind, accent, cx, cy, s):
    col = (*accent, 235)
    glow = (*accent, 70)
    def line(a, b, w, c=col):
        draw.line([a, b], fill=c, width=w)
    if kind == "waterdrop":
        pts = [(cx, cy-s), (cx-s*0.62, cy+s*0.35), (cx-s*0.32, cy+s*0.95),
               (cx+s*0.32, cy+s*0.95), (cx+s*0.62, cy+s*0.35)]
        draw.polygon(pts, fill=glow)
        draw.polygon(pts, outline=col, width=6)
        draw.ellipse([cx-s*0.16, cy-s*0.05, cx+s*0.16, cy+s*0.27], fill=(255,255,255,220))
    elif kind == "space_bomb":
        r = s*0.62
        draw.ellipse([cx-r, cy-r, cx+r, cy+r], fill=glow, outline=col, width=8)
        for i in range(8):
            a = i*math.pi/4
            x1, y1 = cx+math.cos(a)*r*1.05, cy+math.sin(a)*r*1.05
            x2, y2 = cx+math.cos(a)*r*1.55, cy+math.sin(a)*r*1.55
            line((x1,y1),(x2,y2), 7)
        draw.ellipse([cx-s*0.14, cy-s*0.14, cx+s*0.14, cy+s*0.14], fill=(255,255,255,230))
    elif kind == "force_field":
        for i, rr in enumerate([s*0.95, s*0.65, s*0.35]):
            a = 220 - i*40
            draw.ellipse([cx-rr, cy-rr, cx+rr, cy+rr], outline=(*accent, a), width=7)
        draw.ellipse([cx-s*0.14, cy-s*0.14, cx+s*0.14, cy+s*0.14], fill=(255,255,255,230))
    elif kind == "sweep_laser":
        for i in range(5):
            t = i/4
            ang = math.radians(-35 + 70*t)
            x2 = cx + math.sin(ang)*s*1.15
            y2 = cy - math.cos(ang)*s*1.15
            a = 90 + int(120*(1-abs(t-0.5)*2))
            line((cx, cy), (x2, y2), 5, (*accent, a))
        draw.ellipse([cx-s*0.12, cy-s*0.12, cx+s*0.12, cy+s*0.12], fill=(255,255,255,230))
    draw.ellipse([cx-s*1.05, cy-s*1.05, cx+s*1.05, cy+s*1.05], outline=(*accent, 90), width=2)

cards = [
    ("waterdrop", "#2ec6e8", 1),
    ("space_bomb", "#c44fe0", 2),
    ("force_field", "#9b5de5", 3),
    ("sweep_laser", "#ff5c8a", 4),
]

import os
out_dir = os.path.join(os.path.dirname(__file__), "..", "assets", "game", "cards")
for kind, hexcol, seed in cards:
    accent = hexrgb(hexcol)
    im = bg(accent, seed).convert('RGBA')
    draw = ImageDraw.Draw(im, 'RGBA')
    glyph(draw, kind, accent, W*0.5, H*0.40, 150)
    im = im.convert('RGB')
    im.save(f"{out_dir}/{kind}.jpg", quality=90)
    print("saved", kind)
