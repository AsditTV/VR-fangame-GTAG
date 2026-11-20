"""
Textures procédurales de la map (papiers peints pastel, carrelage, parquet, béton, rouille...)
et textures uniques qui racontent le lore (portrait aux yeux grattés, tableau noir, panneaux...).
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "Character"))
import textures as tx  # noqa: E402

S = 256
FONT_SERIF = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"
FONT_SANS = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONT_OBL = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf"
if not os.path.exists(FONT_OBL):
    FONT_OBL = FONT_SANS


def _rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


def _img(arr):
    return Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8), "RGB")


def wallpaper(c1, c2, motif, seed=1, stripes=6, kind="lune"):
    """Papier peint à rayures pastel avec un petit motif (lunes ou étoiles), jauni et taché."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    band = (np.floor(x / S * stripes * 2) % 2)[..., None]
    col = _rgb(c1) * (1 - band) + _rgb(c2) * band
    img = _img(col)
    d = ImageDraw.Draw(img)
    mc = tuple(int(v * 255) for v in _rgb(motif))
    step = S // stripes
    for i in range(stripes):
        for j in range(3):
            cx = i * step + step * 0.5 + (step * 0.25 if j % 2 else 0)
            cy = j * S / 3 + S / 6
            cx %= S
            if kind == "lune":
                d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=mc)
                bc = tuple(int(v * 255) for v in (_rgb(c1) if (int(cx / S * stripes * 2) % 2 == 0) else _rgb(c2)))
                d.ellipse([cx - 3, cy - 9, cx + 9, cy + 5], fill=bc)
            else:
                pts = []
                for k in range(10):
                    a = k * math.pi / 5 - math.pi / 2
                    r = 7 if k % 2 == 0 else 3
                    pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
                d.polygon(pts, fill=mc)
    arr = np.asarray(img).astype(np.float32) / 255
    stain = tx.fractal(S, rng, ((3, 0.6), (8, 0.4)))
    yellow = np.array([0.82, 0.74, 0.55])
    t = np.clip((stain - 0.5) * 1.6, 0, 0.45)[..., None]
    arr = arr * (1 - t) + arr * yellow * t
    grain = tx.tile_noise(S, 64, rng)
    arr *= (0.95 + 0.05 * grain)[..., None]
    h = grain * 0.3 + stain * 0.2
    return _img(arr), tx.height_to_normal(h, 0.8)


def checker(c1, c2, tiles=4, seed=2):
    """Carrelage en damier avec joints."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    u, v = x / S * tiles, y / S * tiles
    cell = ((np.floor(u) + np.floor(v)) % 2)[..., None]
    col = _rgb(c1) * (1 - cell) + _rgb(c2) * cell
    fu, fv = u - np.floor(u), v - np.floor(v)
    grout = ((fu < 0.035) | (fu > 0.965) | (fv < 0.035) | (fv > 0.965)).astype(np.float32)
    col = col * (1 - grout[..., None] * 0.45)
    dirt = tx.fractal(S, rng)
    col *= (0.88 + 0.12 * dirt)[..., None]
    h = 1 - grout * 0.8 + dirt * 0.1
    return _img(col), tx.height_to_normal(h, 2.0)


def small_tiles(c, tiles=12, seed=3):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    u, v = x / S * tiles, y / S * tiles
    fu, fv = u - np.floor(u), v - np.floor(v)
    grout = ((fu < 0.06) | (fu > 0.94) | (fv < 0.06) | (fv > 0.94)).astype(np.float32)
    var = tx.tile_noise(S, tiles, rng)
    col = _rgb(c)[None, None, :] * (0.92 + 0.08 * var[..., None])
    col = col * (1 - grout[..., None] * 0.35)
    return _img(col), tx.height_to_normal(1 - grout, 2.2)


def wood(c="#9C7B6A", seed=4, planks=5):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    p = np.floor(x / S * planks)
    fx = x / S * planks - p
    offs = rng.uniform(0, 1, planks + 1)
    grain_n = tx.tile_noise(S, 8, rng)
    rings = 0.5 + 0.5 * np.sin((y / S * 6 + offs[p.astype(int)] * 10 + grain_n * 3) * math.pi * 2)
    shade = (rng.uniform(0.85, 1.05, planks + 1))[p.astype(int)]
    seam = ((fx < 0.03) | (fx > 0.97)).astype(np.float32)
    endseam = (np.abs(((y / S + offs[p.astype(int)]) % 0.5) - 0.0) < 0.006).astype(np.float32)
    col = _rgb(c)[None, None, :] * (0.82 + 0.18 * rings[..., None]) * shade[..., None]
    col *= (1 - 0.5 * np.maximum(seam, endseam))[..., None]
    h = rings * 0.2 + (1 - np.maximum(seam, endseam)) * 0.8
    return _img(col), tx.height_to_normal(h, 1.6)


def concrete(c="#7F8784", seed=5):
    rng = np.random.default_rng(seed)
    n = tx.fractal(S, rng)
    pits = (tx.tile_noise(S, 80, rng) > 0.8).astype(np.float32)
    stains = tx.fractal(S, rng, ((3, 0.7), (6, 0.3)))
    col = _rgb(c)[None, None, :] * (0.8 + 0.25 * n[..., None]) * (1 - 0.25 * np.clip((stains - 0.55) * 3, 0, 1))[..., None]
    col *= (1 - 0.3 * pits)[..., None]
    return _img(col), tx.height_to_normal(n * 0.6 - pits * 0.4, 2.0)


def rust(seed=6):
    rng = np.random.default_rng(seed)
    n = tx.fractal(S, rng)
    r = tx.fractal(S, rng, ((4, 0.5), (16, 0.5)))
    base = _rgb("#5E6E6B")
    orange = _rgb("#8A5A3C")
    t = np.clip((r - 0.45) * 2.5, 0, 1)[..., None]
    col = base * (1 - t) + orange * t
    col *= (0.85 + 0.2 * n[..., None])
    return _img(col), tx.height_to_normal(n * 0.5 + t[..., 0] * 0.5, 2.2)


def fabric_colored(c, seed=7, threads=20):
    alb, nrm = tx.fabric(seed=seed, threads=threads)
    arr = np.asarray(alb).astype(np.float32) / 255 * _rgb(c)[None, None, :] * 1.1
    return _img(arr), nrm


# ---------------------------------------------------------------- textures uniques (lore)
def portrait_scratched():
    """Grand portrait de Céleste : peinture pastel, yeux grattés au couteau."""
    w, h = 320, 420
    img = Image.new("RGB", (w, h), (120, 96, 84))
    d = ImageDraw.Draw(img)
    d.rectangle([14, 14, w - 15, h - 15], fill=(196, 186, 205))           # fond lilas
    d.ellipse([90, 70, 230, 250], fill=(232, 214, 200))                    # visage
    d.ellipse([70, 40, 250, 140], fill=(92, 78, 98))                       # cheveux / chignon
    d.ellipse([120, 20, 200, 80], fill=(92, 78, 98))
    d.rectangle([110, 240, 210, 300], fill=(232, 214, 200))                # cou
    d.polygon([(40, h - 15), (90, 290), (230, 290), (280, h - 15)], fill=(125, 112, 136))  # robe
    d.polygon([(130, 290), (160, 330), (190, 290)], fill=(236, 228, 214))  # col
    d.arc([135, 190, 185, 215], 10, 170, fill=(150, 90, 100), width=3)     # sourire peint
    # yeux grattés : griffures claires dans tous les sens
    rnd = np.random.default_rng(13)
    for ex in (135, 185):
        for _ in range(26):
            a = rnd.uniform(-0.6, 0.6)
            L = rnd.uniform(14, 30)
            x0, y0 = ex + rnd.uniform(-10, 10), 150 + rnd.uniform(-8, 8)
            d.line([(x0 - L * math.cos(a), y0 - L * math.sin(a)), (x0 + L * math.cos(a), y0 + L * math.sin(a))],
                   fill=(240, 232, 225) if rnd.random() < 0.7 else (70, 55, 60), width=int(rnd.uniform(1, 3)))
    d.rectangle([6, 6, w - 7, h - 7], outline=(176, 150, 98), width=10)   # cadre doré
    plate = Image.new("RGB", (w, h))
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    d = ImageDraw.Draw(img)
    d.rectangle([110, h - 42, 210, h - 22], fill=(176, 150, 98))
    d.text((160, h - 32), "C. Marivaux", fill=(60, 45, 30), font=ImageFont.truetype(FONT_SERIF, 13), anchor="mm")
    return img


def chalkboard():
    w, h = 768, 320
    img = Image.new("RGB", (w, h), (46, 62, 56))
    rng = np.random.default_rng(14)
    arr = np.asarray(img).astype(np.float32)
    smudge = np.asarray(Image.fromarray((rng.random((8, 20)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(np.float32)
    arr += (smudge[..., None] - 128) * 0.08
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(FONT_OBL, 26)
    for k in range(9):
        y = 22 + k * 32
        x = 20 + rng.uniform(-4, 4)
        text = "I will not scream again. I will not scream again."
        if k >= 7:
            text = "I will not scream again. I will not scr"     # l'écriture s'arrête net...
        d.text((x, y), text, fill=(222, 222, 210), font=font)
    d.rectangle([0, 0, w - 1, h - 1], outline=(120, 96, 70), width=12)
    return img


def sign(lines, bg=(236, 226, 206), fg=(90, 60, 70), w=512, h=192, font_size=44, border=(150, 110, 90)):
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    d.rectangle([6, 6, w - 7, h - 7], outline=border, width=8)
    font = ImageFont.truetype(FONT_SERIF, font_size)
    n = len(lines)
    for i, line in enumerate(lines):
        d.text((w / 2, h / 2 + (i - (n - 1) / 2) * (font_size + 10)), line, fill=fg, font=font, anchor="mm")
    return img


def happy_sign_covered():
    """Ancien panneau joyeux de l'hôtel, avec le panneau de l'institut cloué de travers par-dessus."""
    old = sign(["Children laugh here!"], bg=(250, 214, 220), fg=(200, 110, 140), font_size=40)
    new = sign(["Here, we keep quiet."], bg=(222, 222, 216), fg=(40, 36, 44), w=380, h=120, font_size=40, border=(80, 80, 86))
    new = new.rotate(-6, expand=True, fillcolor=(0, 0, 0))
    mask = new.convert("L").point(lambda v: 255 if v > 8 else 0)
    old.paste(new, (70, 40), mask)
    d = ImageDraw.Draw(old)
    for x, y in ((100, 62), (420, 92), (95, 150), (410, 172)):
        d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=(120, 120, 126))  # clous
    return old


def number_atlas(numbers, chalk=False, cols=4):
    """Atlas de numéros : plaques de porte, ou chiffres à la craie de plus en plus tremblés."""
    rows = math.ceil(len(numbers) / cols)
    cw, ch = 128, 128
    img = Image.new("RGBA", (cw * cols, ch * rows), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(15)
    for i, n in enumerate(numbers):
        cx, cy = (i % cols) * cw, (i // cols) * ch
        if chalk:
            shake = 1 + i * 0.8
            font = ImageFont.truetype(FONT_OBL, 70)
            for k in range(3):
                d.text((cx + 64 + rng.uniform(-shake, shake), cy + 64 + rng.uniform(-shake, shake)), str(n),
                       fill=(235, 235, 228, 150), font=font, anchor="mm")
        else:
            d.rounded_rectangle([cx + 14, cy + 30, cx + cw - 14, cy + ch - 30], radius=12, fill=(214, 190, 140, 255),
                                outline=(120, 95, 60, 255), width=4)
            d.text((cx + 64, cy + 64), str(n), fill=(70, 50, 40, 255), font=ImageFont.truetype(FONT_SERIF, 48), anchor="mm")
    return img, cols, rows


def growth_chart():
    """Toise : les tailles des enfants, toutes arrêtées en novembre 1985."""
    w, h = 128, 512
    img = Image.new("RGB", (w, h), (236, 228, 208))
    d = ImageDraw.Draw(img)
    for k in range(0, 18):
        y = h - 20 - k * 28
        d.line([(0, y), (30 if k % 2 == 0 else 18, y)], fill=(110, 90, 80), width=2)
        if k % 2 == 0:
            d.text((34, y - 8), "%d" % (k * 10), fill=(110, 90, 80), font=ImageFont.truetype(FONT_SANS, 12))
    rng = np.random.default_rng(16)
    names = ["Theo", "Lucie", "Nour", "Ines", "Malo", "Bastien"]
    font = ImageFont.truetype(FONT_OBL, 12)
    for i, n in enumerate(names):
        y = int(h - 20 - rng.uniform(9, 14) * 28)
        d.line([(60, y), (124, y)], fill=(160, 70, 80), width=2)
        d.text((62, y - 15), n + " Nov 85", fill=(160, 70, 80), font=font)
    return img


def pump_plate():
    return sign(["PUMP No.2", "Special filling", "Nov 12, 1985"], bg=(170, 160, 140), fg=(50, 40, 36),
                w=320, h=200, font_size=28, border=(90, 80, 70))


def pool_sign():
    return sign(["NO SCREAMING", "NEAR THE POOL"], bg=(220, 236, 230), fg=(150, 60, 70), font_size=40)


def socket_labels():
    """Étiquettes du Standard : un micro par pièce."""
    rooms = ["Dormitory", "Pool", "Room 3", "Room 7", "Dining Hall", "Infirmary",
             "Laundry", "Hallway", "Lobby", "Classroom", "Playroom", "Room 23"]
    w, h = 1024, 96
    img = Image.new("RGB", (w, h), (196, 186, 160))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(FONT_SANS, 15)
    cw = w / 6
    for i, r in enumerate(rooms):
        x, y = (i % 6) * cw, (i // 6) * 48
        d.rectangle([x + 4, y + 6, x + cw - 4, y + 42], outline=(90, 80, 60), width=2)
        d.text((x + cw / 2, y + 24), r, fill=(50, 40, 30), font=font, anchor="mm")
        if r == "Playroom":   # la seule pièce sans micro (la zone sûre)
            d.line([(x + 10, y + 24), (x + cw - 10, y + 24)], fill=(170, 40, 50), width=3)
            d.text((x + cw / 2, y + 38), "DISCONNECTED", fill=(170, 40, 50), font=ImageFont.truetype(FONT_SANS, 11), anchor="mm")
    return img
