"""
Fabrique les images des papiers de lore (en anglais, comme dans le jeu) et des dessins d'enfants
accrochés aux murs. Chaque document a son propre support et son écriture :
brochure glacée, coupures de journal, règlement tapé à la machine, lettres d'enfants sur papier
ligné, carnet gondolé, journal intime, rapport officiel, registre, dessin aux crayons...

Usage (depuis la racine du dépôt) : python3 Tools/Papers/make_papers.py
Sorties :
  Assets/Horror/Art/Papers/<id>.jpg        (recto, 1024 px de large : lisible en VR de près)
  Assets/Horror/Art/Papers/<id>_back.jpg   (verso quand il y a quelque chose derrière)
  Assets/Horror/Art/Papers/papers.json     (liste : id, titre, ordre, zone, secret)
  Assets/Horror/Art/Drawings/*.png         (dessins d'enfants pour les murs)
"""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(__file__)
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets/Horror/Art/Papers")
OUT_DRAW = os.path.join(ROOT, "Assets/Horror/Art/Drawings")
FACE = os.path.join(ROOT, "Assets/Horror/Art/Face/face.png")
F = os.path.join(HERE, "fonts")

W, H = 1024, 1448      # format A5 (rapport 1:1,414)
rng = np.random.default_rng(1985)


def font(name, size):
    files = {
        "child": "gloria-hallelujah-latin-400-normal.woff",
        "hand": "caveat-latin-400-normal.woff",
        "hand_bold": "caveat-latin-700-normal.woff",
        "cursive": "homemade-apple-latin-400-normal.woff",
        "type": "special-elite-latin-400-normal.woff",
        "news": "old-standard-tt-latin-400-normal.woff",
        "news_bold": "old-standard-tt-latin-700-normal.woff",
        "old": "im-fell-english-latin-400-normal.woff",
    }
    return ImageFont.truetype(os.path.join(F, files[name]), size)


# ---------------------------------------------------------------- papier
def paper(color=(236, 228, 208), size=(W, H), age=0.5, creases=4, stains=3, lined=None, grid=False, margin_line=False):
    """Papier vieilli : grain, taches, plis, bords assombris, lignes de cahier en option."""
    w, h = size
    base = np.ones((h, w, 3), np.float32) * np.array(color, np.float32) / 255
    grain = np.asarray(Image.fromarray((rng.random((h // 4, w // 4)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(np.float32) / 255
    base *= (0.95 + 0.07 * grain)[..., None]
    # bords plus sombres (vieillissement)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    edge = np.minimum(np.minimum(xx, w - xx), np.minimum(yy, h - yy)) / (min(w, h) * 0.18)
    base *= (1 - age * 0.22 * np.clip(1 - edge, 0, 1) ** 2)[..., None]
    img = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(img, "RGBA")
    if lined:
        for y in range(lined[0], h - 60, lined[1]):
            d.line([(40, y), (w - 40, y)], fill=(120, 150, 200, 110), width=2)
    if margin_line:
        d.line([(120, 40), (120, h - 40)], fill=(220, 120, 130, 140), width=2)
    if grid:
        for x in range(30, w, 38):
            d.line([(x, 0), (x, h)], fill=(130, 160, 190, 70), width=1)
        for y in range(30, h, 38):
            d.line([(0, y), (w, y)], fill=(130, 160, 190, 70), width=1)
    # taches (café, eau)
    st = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(st)
    for _ in range(stains):
        x, y, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(40, 180)
        sd.ellipse([x - r, y - r, x + r, y + r], fill=(120, 90, 50, int(rng.uniform(14, 34))))
        if rng.random() < 0.4:   # anneau de tasse
            sd.ellipse([x - r * 0.6, y - r * 0.6, x + r * 0.6, y + r * 0.6], outline=(110, 80, 45, 50), width=6)
    img.paste(Image.alpha_composite(img.convert("RGBA"), st.filter(ImageFilter.GaussianBlur(10))).convert("RGB"))
    d = ImageDraw.Draw(img, "RGBA")
    for _ in range(creases):   # plis
        if rng.random() < 0.5:
            x = rng.uniform(w * 0.2, w * 0.8)
            d.line([(x, 0), (x + rng.uniform(-40, 40), h)], fill=(90, 80, 70, 40), width=3)
            d.line([(x + 3, 0), (x + 3 + rng.uniform(-40, 40), h)], fill=(255, 255, 255, 40), width=2)
        else:
            y = rng.uniform(h * 0.2, h * 0.8)
            d.line([(0, y), (w, y + rng.uniform(-40, 40))], fill=(90, 80, 70, 40), width=3)
            d.line([(0, y + 3), (w, y + 3 + rng.uniform(-40, 40))], fill=(255, 255, 255, 40), width=2)
    return img


def wrap(text, fnt, max_w, draw):
    """Coupe un texte en lignes qui tiennent dans max_w pixels."""
    lines = []
    for para in text.split("\n"):
        words, cur = para.split(" "), ""
        for wd in words:
            test = (cur + " " + wd).strip()
            if draw.textlength(test, font=fnt) <= max_w:
                cur = test
            else:
                lines.append(cur)
                cur = wd
        lines.append(cur)
    return lines


def write(img, text, fnt, x, y, max_w, color=(40, 34, 44), spacing=1.25, jitter=0.0, align="left"):
    """Écrit un texte (avec une légère irrégularité pour l'écriture à la main). Renvoie le y final."""
    d = ImageDraw.Draw(img, "RGBA")
    size = fnt.size
    for line in wrap(text, fnt, max_w, d):
        dx = rng.uniform(-jitter, jitter) * size * 0.3
        lx = x + dx
        if align == "center":
            lx = x + (max_w - d.textlength(line, font=fnt)) / 2
        d.text((lx, y + rng.uniform(-jitter, jitter) * size * 0.1), line, fill=color, font=fnt)
        y += size * spacing
    return y


def stamp(img, text, xy, color=(170, 40, 50), size=56, angle=-12):
    """Tampon encreur rouge, un peu effacé."""
    fnt = font("type", size)
    tmp = Image.new("RGBA", (900, 200), (0, 0, 0, 0))
    d = ImageDraw.Draw(tmp)
    tw = d.textlength(text, font=fnt)
    d.rectangle([10, 10, tw + 50, size + 50], outline=color + (220,), width=6)
    d.text((30, 25), text, fill=color + (220,), font=fnt)
    a = np.asarray(tmp).copy()
    holes = rng.random(a.shape[:2]) < 0.18          # encre irrégulière
    a[holes, 3] = (a[holes, 3] * 0.3).astype(np.uint8)
    tmp = Image.fromarray(a).rotate(angle, expand=True, resample=Image.BICUBIC)
    img.paste(tmp, xy, tmp)


def ink_blot(img, x, y, r, color=(20, 16, 24)):
    d = ImageDraw.Draw(img, "RGBA")
    for _ in range(12):
        ox, oy, rr = rng.normal(0, r * 0.4), rng.normal(0, r * 0.4), rng.uniform(r * 0.3, r * 0.8)
        d.ellipse([x + ox - rr, y + oy - rr, x + ox + rr, y + oy + rr], fill=color + (230,))


def staple(img, x, y):
    d = ImageDraw.Draw(img)
    d.line([(x, y), (x + 70, y + 8)], fill=(150, 150, 155), width=6)
    d.line([(x, y + 3), (x + 70, y + 11)], fill=(90, 90, 95), width=2)


def halftone_photo(w, h, kind="pool"):
    """Fausse photo de journal en trame."""
    img = Image.new("L", (w, h), 200)
    d = ImageDraw.Draw(img)
    if kind == "pool":
        d.rectangle([0, int(h * 0.55), w, h], fill=90)
        d.polygon([(0, int(h * 0.55)), (w, int(h * 0.4)), (w, int(h * 0.55))], fill=150)
        for k in range(6):
            d.rectangle([20 + k * w // 6, int(h * 0.2), 40 + k * w // 6, int(h * 0.55)], fill=60)
    else:
        d.rectangle([0, 0, w, h], fill=170)
        d.polygon([(w * 0.1, h), (w * 0.5, h * 0.15), (w * 0.9, h)], fill=80)
        for k in range(8):
            d.rectangle([w * 0.2 + k * w * 0.075, h * 0.55, w * 0.24 + k * w * 0.075, h * 0.7], fill=40)
    img = img.filter(ImageFilter.GaussianBlur(2))
    out = Image.new("L", (w, h), 235)
    od = ImageDraw.Draw(out)
    a = np.asarray(img)
    for y in range(0, h, 7):
        for x in range(0, w, 7):
            r = (255 - a[y, x]) / 255 * 4.2
            od.ellipse([x - r, y - r, x + r, y + r], fill=30)
    return out.convert("RGB")


# ---------------------------------------------------------------- les documents
DOCS = []


def doc(did, title, zone, order, secret=False):
    def deco(fn):
        DOCS.append({"id": did, "title": title, "zone": zone, "order": order, "secret": secret, "fn": fn})
        return fn
    return deco


@doc("p01_brochure", "Brochure, 1974", "Lobby", 1)
def p01():
    img = paper((246, 232, 236), age=0.35, creases=2, stains=1)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W, 330], fill=(232, 186, 200))
    write(img, "Céleste's Gardens", font("news_bold", 92), 60, 60, W - 120, color=(255, 250, 245), align="center")
    write(img, "the hotel where children laugh!", font("hand_bold", 64), 60, 190, W - 120, color=(120, 70, 100), align="center")
    # les Doux : petites mascottes rondes dessinées
    for k, (cx, col) in enumerate(((250, (190, 220, 230)), (512, (240, 210, 150)), (774, (200, 225, 190)))):
        d.ellipse([cx - 90, 400, cx + 90, 600], fill=col, outline=(120, 100, 110), width=5)
        d.ellipse([cx - 40, 460, cx - 20, 485], fill=(60, 50, 60))
        d.ellipse([cx + 20, 460, cx + 40, 485], fill=(60, 50, 60))
        d.arc([cx - 40, 490, cx + 40, 550], 20, 160, fill=(60, 50, 60), width=5)
        d.ellipse([cx - 110, 390, cx - 60, 440], fill=col, outline=(120, 100, 110), width=4)
        d.ellipse([cx + 60, 390, cx + 110, 440], fill=col, outline=(120, 100, 110), width=4)
    write(img, "Come and meet the Softies, our adorable mascots! Carousel, bubble garden, and soon our "
               "brand-new indoor swimming pool. Here, children's laughter is our sweetest music.",
          font("news", 46), 90, 680, W - 180, color=(70, 55, 70), spacing=1.4)
    write(img, "— Céleste Marivaux, creator of The Softies", font("old", 40), 90, 1180, W - 180, color=(120, 80, 100))
    return img


@doc("p02_news1977", "Newspaper, August 16, 1977", "Lobby", 2)
def p02():
    img = paper((228, 222, 200), age=0.6, creases=3, stains=2)
    d = ImageDraw.Draw(img)
    write(img, "THE LAKESIDE COURIER", font("news_bold", 70), 50, 40, W - 100, color=(30, 28, 30), align="center")
    d.line([(50, 140), (W - 50, 140)], fill=(30, 28, 30), width=4)
    write(img, "Tuesday, August 16, 1977", font("news", 30), 50, 152, W - 100, color=(60, 55, 55), align="center")
    write(img, "TRAGEDY AT CÉLESTE'S GARDENS", font("news_bold", 64), 50, 220, W - 100, color=(20, 18, 20), align="center", spacing=1.1)
    img.paste(halftone_photo(W - 140, 380, "pool"), (70, 400))
    write(img, "The new indoor pool, photographed the day before the party.", font("old", 28), 70, 790, W - 140, color=(60, 55, 55))
    body = ("A six-year-old girl, Mila Marivaux, drowned on Saturday night during the pool's opening party. "
            "\"The music drowned out everything. Nobody heard a thing,\" said a waiter. "
            "The resort is closed until further notice. Mrs Marivaux, the creator of the beloved Softies books, "
            "could not be reached for comment.")
    col_w = (W - 160) // 2
    lines = wrap(body, font("news", 34), col_w, d)
    half = (len(lines) + 1) // 2
    y = 850
    for i, line in enumerate(lines):
        x = 60 if i < half else 100 + col_w
        yy = y + (i if i < half else i - half) * 44
        d.text((x, yy), line, fill=(35, 32, 35), font=font("news", 34))
    return img


@doc("p03_rules", "Rules of the Marivaux Institute, 1978", "School wing", 3)
def p03():
    img = paper((238, 234, 222), age=0.4, creases=1, stains=1)
    d = ImageDraw.Draw(img)
    write(img, "MARIVAUX INSTITUTE", font("news_bold", 60), 60, 60, W - 120, color=(90, 70, 100), align="center")
    write(img, "for Calm Children", font("old", 42), 60, 135, W - 120, color=(110, 90, 120), align="center")
    d.line([(160, 205), (W - 160, 205)], fill=(150, 130, 160), width=3)
    write(img, "HOUSE RULES", font("type", 54), 60, 250, W - 120, color=(40, 36, 44), align="center")
    rules = ["1. We do not scream.", "2. We do not run in the hallways.",
             "3. We wear our calm face from waking until bedtime.",
             "4. At 9 p.m., every wing's doors lock (Lock Protocol). The buttons are for staff only."]
    y = 380
    for r in rules:
        y = write(img, r, font("type", 44), 90, y, W - 180, color=(40, 36, 44), spacing=1.35) + 30
    write(img, "5. CALM IS LISTENING TO YOU.", font("type", 56), 90, y + 30, W - 180, color=(120, 40, 60))
    write(img, "— The Headmistress", font("old", 40), 90, 1250, W - 180, color=(90, 70, 100))
    return img


@doc("p04_theo", "Theo's note", "School wing", 4)
def p04():
    img = paper((240, 238, 230), age=0.3, lined=(180, 64), margin_line=True, creases=2)
    write(img, "Today we drew our calm face. Madam says we have to wear it until it fits. Mine itches.",
          font("child", 50), 140, 168, W - 200, color=(50, 60, 120), spacing=1.27, jitter=0.6)
    write(img, "Theo, age 8, No.3", font("child", 46), 520, 560, 460, color=(50, 60, 120), jitter=0.5)
    face = Image.open(FACE).convert("RGBA").resize((560, 420))
    bg = Image.new("RGBA", face.size, (252, 250, 245, 255))
    bg.alpha_composite(face)
    bg = bg.rotate(5, expand=True, fillcolor=(0, 0, 0, 0))
    img.paste(bg, (230, 760), bg)
    staple(img, 260, 750)
    return img


@doc("p05_ines", "Letter never sent", "School wing", 5)
def p05():
    img = paper((241, 239, 232), age=0.35, lined=(180, 64), margin_line=True, creases=3)
    write(img, "Mom, Dad,", font("child", 52), 140, 166, W - 200, color=(60, 50, 110), jitter=0.6)
    write(img, "Lucie hasn't said a word in two weeks. But at night, the mouth on her mask moves by itself. "
               "I'm scared to put mine on. Please come and get me.",
          font("child", 50), 140, 294, W - 200, color=(60, 50, 110), spacing=1.27, jitter=0.6)
    write(img, "Ines, No.18", font("child", 50), 600, 870, 400, color=(60, 50, 110), jitter=0.5)
    stamp(img, "NOT APPROVED - HEADMISTRESS", (80, 1000), size=50, angle=-9)
    return img


@doc("p06_entertainer", "Note in a dressing room", "Closed park", 6)
def p06():
    img = paper((233, 226, 214), size=(W, H), age=0.6, creases=5, stains=3)
    d = ImageDraw.Draw(img)
    d.ellipse([W - 260, 60, W - 80, 240], outline=(200, 140, 160), width=8)   # logo des Doux
    write(img, "Softies", font("hand_bold", 52), W - 250, 120, 160, color=(200, 140, 160), align="center")
    text = ("Madam's orders: never take the Softies' heads off in front of the children.\n"
            "I haven't taken mine off in three weeks. This morning I tried. I couldn't.\n"
            "The little ones laugh when we play \"Red Light, Green Light\": we only move when they're not looking.\n"
            "I don't think I know how to do anything else anymore.")
    write(img, text, font("hand", 60), 80, 300, W - 160, color=(40, 30, 40), spacing=1.18, jitter=0.9)
    return img


@doc("p07_lifeguard", "Lifeguard's notebook", "Pool", 7)
def p07():
    img = paper((232, 230, 215), age=0.7, grid=True, creases=4, stains=5)
    y = 90
    write(img, "August 14, 1977.", font("hand_bold", 56), 80, y, W - 160, color=(30, 40, 90))
    y += 90
    for k in range(9):
        y = write(img, "I didn't hear anything.", font("hand", 52 + k * 2), 80 + k * 6, y, W - 160,
                  color=(30, 40, 90), jitter=0.4 + k * 0.25) + 6
    write(img, "...", font("hand", 60), 80, y, 200, color=(30, 40, 90))
    y += 110
    y = write(img, "November 12, 1985.", font("hand_bold", 56), 80, y, W - 160, color=(30, 40, 90)) + 10
    write(img, "Madam had the pool drained. She's having it filled with ink. She says: \"The water took her "
               "from me. The ink will give her back.\"", font("hand", 54), 80, y, W - 160, color=(30, 40, 90), jitter=0.8)
    ink_blot(img, 860, 1360, 55)   # dans le coin : ne cache pas la phrase clé
    ink_blot(img, 200, 640, 35)
    # pages gondolées par l'eau
    a = np.asarray(img).astype(np.float32)
    yy = np.arange(H)[:, None]
    wave = (np.sin(yy / 47.0) * 0.04 + 0.96)
    return Image.fromarray(np.clip(a * wave[..., None], 0, 255).astype(np.uint8))


def diary(text, date):
    img = paper((240, 232, 214), age=0.5, lined=(230, 70), creases=2, stains=2)
    write(img, date, font("old", 48), 90, 120, W - 180, color=(80, 50, 70))
    write(img, text, font("old", 54), 90, 250, W - 180, color=(50, 30, 50), spacing=1.29, jitter=0.2)
    return img


@doc("p08_diary_a", "Céleste's diary, page A", "Manor", 8)
def p08():
    return diary("Nobody heard Mila. If the house had been quiet, I would have heard her.\n\n"
                 "No child will ever scream again without me hearing it. Never again.", "September 3, 1977")


@doc("p09_diary_b", "Céleste's diary, page B", "Manor", 9)
def p09():
    return diary("The faces work. The ink keeps whatever you put into it. The children are calm, and their voices "
                 "are safe inside the paper, where nothing can lose them.", "March 21, 1980")


@doc("p10_inspection", "Inspection report, October 1985", "Manor", 10)
def p10():
    img = paper((236, 236, 230), age=0.3, creases=2, stains=1)
    d = ImageDraw.Draw(img)
    write(img, "ACADEMY INSPECTORATE", font("type", 52), 60, 70, W - 120, color=(30, 30, 40), align="center")
    write(img, "Inspection report - Ref. 85/1142", font("type", 34), 60, 140, W - 120, color=(60, 60, 70), align="center")
    d.line([(60, 200), (W - 60, 200)], fill=(40, 40, 50), width=3)
    write(img, "Visit to the Marivaux Institute.\n\nOf the 23 pupils, not one spoke a single word during the visit. "
               "The paper masks are worn at all times, including at meals. The headmistress refuses to let us "
               "remove them.\n\nWe recommend the closure of the institution on November 15, 1985.",
          font("type", 42), 80, 260, W - 160, color=(30, 30, 40), spacing=1.4)
    stamp(img, "CONFIDENTIAL", (520, 1130), size=54, angle=8)
    write(img, "R. Dumont, inspector", font("hand", 50), 90, 1250, 500, color=(30, 40, 90))
    return img


@doc("p11_watchman", "Night watchman's logbook", "Manor", 11)
def p11():
    img = paper((226, 222, 204), age=0.6, creases=3, stains=3)
    d = ImageDraw.Draw(img)
    cols = [60, 260, W - 60]
    for y in range(160, H - 80, 72):
        d.line([(60, y), (W - 60, y)], fill=(90, 80, 70), width=2)
    for x in cols:
        d.line([(x, 90), (x, H - 80)], fill=(90, 80, 70), width=2)
    write(img, "DATE", font("type", 36), 80, 106, 180, color=(60, 50, 40))
    write(img, "ROUND / REMARKS", font("type", 36), 280, 106, 500, color=(60, 50, 40))
    rows = [("11/11", "All quiet. Doors locked 9 p.m."), ("12/11", "Pump running all night. Smell of ink."),
            ("13/11", "Madam cut the power to the whole building.")]
    y = 178
    for a, b in rows:
        d.text((80, y), a, fill=(40, 40, 90), font=font("hand", 48))
        d.text((280, y), b, fill=(40, 40, 90), font=font("hand", 44))
        y += 72
    y = write(img, "She ordered the Lock Protocol in every wing, even the pool. She said: \"In the dark, you hear better.\"",
              font("hand", 46), 280, y, W - 360, color=(40, 40, 90), spacing=1.56, jitter=0.6)
    y += 20
    write(img, "I'm counting the doors. 1, 2, 3... 4... I don't know how many there are anymore.",
          font("hand", 50), 280, y, W - 360, color=(40, 40, 90), spacing=1.4, jitter=1.6)
    return img


@doc("p12_news1985", "Newspaper, November 20, 1985", "Manor", 12)
def p12():
    img = paper((226, 222, 204), age=0.55, creases=3, stains=2)
    d = ImageDraw.Draw(img)
    write(img, "THE LAKESIDE COURIER", font("news_bold", 70), 50, 40, W - 100, color=(30, 28, 30), align="center")
    d.line([(50, 140), (W - 50, 140)], fill=(30, 28, 30), width=4)
    write(img, "Wednesday, November 20, 1985", font("news", 30), 50, 152, W - 100, color=(60, 55, 55), align="center")
    write(img, "MYSTERIOUS CLOSURE OF THE MARIVAUX INSTITUTE", font("news_bold", 58), 50, 220, W - 100, color=(20, 18, 20), align="center", spacing=1.1)
    img.paste(halftone_photo(W - 140, 360, "building"), (70, 450))
    write(img, "23 children and 9 staff members remain missing. Police found only drawings: 23 children's faces "
               "pinned to the dormitory walls. The headmistress, Céleste Marivaux, is also missing.",
          font("news", 38), 70, 850, W - 140, color=(35, 32, 35), spacing=1.35)
    return img


@doc("p13_pencil", "Pencil note", "Darkest area", 13)
def p13():
    img = paper((232, 230, 222), age=0.7, creases=9, stains=1)
    write(img, "If you're reading this, you're a face too.\n\nDon't talk.\nDon't run.\nDon't drop things.\n\n"
               "She comes to the noise. She doesn't want to hurt you, she just wants you to be quiet.\n\nForever.",
          font("child", 54), 90, 130, W - 180, color=(95, 95, 100), spacing=1.32, jitter=1.2)
    return img


@doc("s1_mila", "Mila's last drawing", "Secret", 14, secret=True)
def s1():
    img = paper((248, 246, 238), age=0.25, creases=1, stains=0)
    d = ImageDraw.Draw(img)
    write(img, "MOMMY", font("child", 120), 0, 90, W, color=(220, 90, 120), align="center")
    cx, cy = W // 2, 760
    for k in range(3):   # coloriage au crayon : traits qui débordent
        d.ellipse([cx - 300 + k * 4, cy - 340 + k * 3, cx + 300 - k * 3, cy + 340 - k * 2], outline=(240, 180, 120), width=10)
    for _ in range(140):
        x = rng.uniform(cx - 260, cx + 260)
        y = rng.uniform(cy - 300, cy + 300)
        if ((x - cx) / 280) ** 2 + ((y - cy) / 320) ** 2 < 1:
            d.line([(x, y), (x + rng.uniform(20, 60), y + rng.uniform(-10, 10))], fill=(250, 220, 170), width=6)
    d.ellipse([cx - 150, cy - 120, cx - 70, cy - 40], fill=(90, 150, 220))
    d.ellipse([cx + 70, cy - 120, cx + 150, cy - 40], fill=(90, 150, 220))
    d.ellipse([cx - 110, cy + 60, cx + 110, cy + 230], fill=(200, 60, 80))     # bouche grande ouverte : elle appelle
    d.ellipse([cx - 70, cy + 100, cx + 70, cy + 200], fill=(120, 30, 50))
    for k in range(9):   # cheveux
        a = math.pi * (0.1 + 0.8 * k / 8)
        d.line([(cx - math.cos(a) * 280, cy - math.sin(a) * 330), (cx - math.cos(a) * 380, cy - math.sin(a) * 430)],
               fill=(160, 110, 70), width=10)
    write(img, "Mila", font("child", 60), 680, 1250, 300, color=(90, 150, 220))
    return img


@doc("s2_cassette", "Note in the music box", "Secret", 15, secret=True)
def s2():
    img = paper((240, 236, 226), size=(W, 760), age=0.3, creases=1, stains=1)
    d = ImageDraw.Draw(img)
    d.rectangle([90, 60, W - 90, 300], fill=(60, 56, 64))
    d.rectangle([130, 90, W - 130, 200], fill=(236, 226, 206))
    d.text((150, 105), "Mila - birthday 1976", fill=(200, 80, 110), font=font("hand_bold", 64))
    for x in (330, W - 330):
        d.ellipse([x - 50, 210, x + 50, 290], fill=(30, 28, 32), outline=(200, 200, 200), width=4)
    write(img, "Madam listens to this tape every night. It's the only thing that calms her.",
          font("hand", 56), 90, 380, W - 180, color=(40, 40, 80), jitter=0.6)
    return img


@doc("s3_payroll", "Staff payroll, 1985", "Secret", 16, secret=True)
def s3():
    img = paper((236, 234, 224), age=0.4, creases=2, stains=1)
    d = ImageDraw.Draw(img)
    write(img, "MARIVAUX INSTITUTE - STAFF PAYROLL - OCT. 1985", font("type", 38), 60, 70, W - 120, color=(30, 30, 40), align="center")
    staff = [("Céleste Marivaux", "Headmistress"), ("Armand Leroy", "Night watchman"), ("Gérard Morel", "Lifeguard"),
             ("Josiane Petit", "Laundry"), ("Paul Garnier", "Mechanic"), ("Sylvie Roux", "Park entertainer"),
             ("Marc Roux", "Park entertainer"), ("Lise Bonnet", "Park entertainer"), ("Hélène Faure", "Switchboard")]
    y = 220
    for name, job in staff:
        d.text((90, y), name, fill=(30, 30, 40), font=font("type", 40))
        d.text((600, y), job, fill=(30, 30, 40), font=font("type", 40))
        d.line([(80, y + 62), (W - 80, y + 62)], fill=(150, 140, 130), width=1)
        y += 100
    d.line([(85, 220 + 50), (520, 220 + 30)], fill=(30, 30, 80), width=4)   # son nom barré à la main
    return img


@doc("s4_portrait", "The 24th face", "Secret", 17, secret=True)
def s4():
    img = paper((236, 228, 210), age=0.5, creases=3, stains=2)
    d = ImageDraw.Draw(img)
    cx, cy = W // 2, 700
    d.ellipse([cx - 280, cy - 420, cx + 280, cy + 420], outline=(40, 34, 40), width=5)
    d.ellipse([cx - 320, cy - 520, cx + 320, cy - 300], fill=(80, 70, 85))       # chignon
    d.line([(cx, cy - 60), (cx - 20, cy + 120), (cx + 15, cy + 130)], fill=(40, 34, 40), width=4)
    d.arc([cx - 140, cy + 180, cx + 140, cy + 280], 15, 165, fill=(40, 34, 40), width=5)
    for ex in (cx - 130, cx + 130):   # yeux gommés jusqu'à trouer le papier
        for _ in range(40):
            a = rng.uniform(-0.5, 0.5)
            L = rng.uniform(40, 90)
            x0, y0 = ex + rng.uniform(-40, 40), cy - 150 + rng.uniform(-25, 25)
            d.line([(x0 - L * math.cos(a), y0 - L * math.sin(a)), (x0 + L * math.cos(a), y0 + L * math.sin(a))],
                   fill=(170, 160, 165), width=int(rng.uniform(6, 14)))
        hole = [(ex + 55 * math.cos(t) * rng.uniform(0.6, 1.1), cy - 150 + 38 * math.sin(t) * rng.uniform(0.6, 1.2))
                for t in np.linspace(0, math.tau, 20)]
        d.polygon(hole, fill=(12, 10, 14))
    write(img, "C. M. - self-portrait", font("old", 40), 60, 1300, W - 120, color=(80, 60, 70), align="center")
    return img


def s4_back():
    img = paper((236, 228, 210), age=0.5, creases=3, stains=2)
    write(img, "I saw too much. I could see their eyes watching me through the paper.\n\n"
               "Tonight I erased mine. Since then, I hear everything.",
          font("old", 60), 100, 720, W - 200, color=(50, 30, 50), spacing=1.32, jitter=0.4)  # sous les trous des yeux
    d = ImageDraw.Draw(img, "RGBA")
    for ex in (W // 2 - 130, W // 2 + 130):   # les trous des yeux se voient aussi au verso (miroir)
        X = W - ex
        d.ellipse([X - 50, 550 - 36, X + 50, 550 + 36], fill=(12, 10, 14, 255))
    return img


# ---------------------------------------------------------------- dessins d'enfants (murs)
def crayon_line(d, pts, color, width=8):
    for _ in range(2):
        jit = [(x + rng.uniform(-2, 2), y + rng.uniform(-2, 2)) for x, y in pts]
        d.line(jit, fill=color, width=width, joint="curve")


def drawings():
    os.makedirs(OUT_DRAW, exist_ok=True)
    out = {}
    # 1) "La dame qui écoute" : grande silhouette sans yeux avec des cornets
    img = paper((250, 248, 240), size=(768, 1024), age=0.3, creases=1, stains=1)
    d = ImageDraw.Draw(img)
    crayon_line(d, [(384, 180), (384, 760)], (60, 60, 70), 10)
    d.ellipse([300, 90, 470, 300], outline=(60, 60, 70), width=8)
    crayon_line(d, [(300, 160), (220, 110)], (180, 140, 90), 10)
    crayon_line(d, [(470, 160), (550, 110)], (180, 140, 90), 10)
    d.polygon([(300, 330), (468, 330), (600, 900), (170, 900)], outline=(140, 110, 160), width=8)
    for _ in range(60):
        y = rng.uniform(760, 900)
        crayon_line(d, [(rng.uniform(190, 560), y), (rng.uniform(190, 560), y)], (30, 25, 35), 6)
    crayon_line(d, [(300, 380), (130, 640)], (60, 60, 70), 8)
    crayon_line(d, [(468, 380), (640, 640)], (60, 60, 70), 8)
    d.line([(330, 250), (440, 250)], fill=(200, 60, 70), width=6)
    write(img, "the lady who listens", font("child", 48), 40, 930, 700, color=(90, 90, 160), align="center")
    out["drawing_lady"] = img
    # 2) "Mes 23 amis" : rangées de visages ronds, un sans bouche
    img = paper((250, 248, 240), size=(1024, 768), age=0.3, creases=1, stains=1)
    d = ImageDraw.Draw(img)
    cols = [(230, 150, 170), (150, 190, 230), (160, 210, 170), (240, 200, 120)]
    k = 0
    for r in range(3):
        for c in range(8):
            if k >= 23:
                break
            cx, cy = 90 + c * 120, 140 + r * 190
            d.ellipse([cx - 45, cy - 55, cx + 45, cy + 55], outline=cols[k % 4], width=7)
            d.ellipse([cx - 20, cy - 15, cx - 10, cy - 5], fill=(50, 50, 60))
            d.ellipse([cx + 10, cy - 15, cx + 20, cy - 5], fill=(50, 50, 60))
            if k != 2:
                d.arc([cx - 22, cy, cx + 22, cy + 30], 20, 160, fill=(50, 50, 60), width=5)
            k += 1
    write(img, "my 23 friends", font("child", 56), 40, 650, 940, color=(90, 90, 160), align="center")
    out["drawing_friends"] = img
    # 3) "La piscine noire"
    img = paper((250, 248, 240), size=(1024, 768), age=0.35, creases=2, stains=1)
    d = ImageDraw.Draw(img)
    d.rectangle([120, 300, 900, 620], outline=(120, 190, 200), width=10)
    for _ in range(220):
        y = rng.uniform(310, 610)
        crayon_line(d, [(rng.uniform(130, 890), y), (rng.uniform(130, 890), y + rng.uniform(-8, 8))], (20, 18, 26), 7)
    d.ellipse([440, 230, 520, 310], outline=(240, 200, 80), width=8)    # petite tête qui dépasse
    crayon_line(d, [(480, 310), (480, 330)], (240, 200, 80), 8)
    write(img, "dont go in the pool", font("child", 56), 40, 650, 940, color=(160, 60, 80), align="center")
    out["drawing_pool"] = img
    # 4) "Ma famille" barré
    img = paper((250, 248, 240), size=(768, 1024), age=0.3, creases=2, stains=1)
    d = ImageDraw.Draw(img)
    for i, x in enumerate((200, 384, 568)):
        h0 = 300 if i != 1 else 420
        d.ellipse([x - 50, h0 - 100, x + 50, h0], outline=(70, 70, 90), width=7)
        crayon_line(d, [(x, h0), (x, h0 + 260)], (70, 70, 90), 7)
        crayon_line(d, [(x - 80, h0 + 90), (x + 80, h0 + 90)], (70, 70, 90), 7)
        crayon_line(d, [(x, h0 + 260), (x - 60, h0 + 400)], (70, 70, 90), 7)
        crayon_line(d, [(x, h0 + 260), (x + 60, h0 + 400)], (70, 70, 90), 7)
    for _ in range(40):   # rageusement barré
        crayon_line(d, [(rng.uniform(80, 690), rng.uniform(150, 800)), (rng.uniform(80, 690), rng.uniform(150, 800))], (20, 16, 24), 5)
    write(img, "my family", font("child", 56), 40, 880, 700, color=(90, 90, 160), align="center")
    out["drawing_family"] = img
    for name, im in out.items():
        im.save(os.path.join(OUT_DRAW, name + ".png"), optimize=True)
    return list(out)


def main():
    os.makedirs(OUT, exist_ok=True)
    meta = []
    for dct in DOCS:
        img = dct["fn"]()
        img.save(os.path.join(OUT, dct["id"] + ".jpg"), quality=86)
        entry = {k: v for k, v in dct.items() if k != "fn"}
        entry["size"] = list(img.size)
        if dct["id"] == "s4_portrait":
            s4_back().save(os.path.join(OUT, dct["id"] + "_back.jpg"), quality=86)
            entry["back"] = True
        meta.append(entry)
    with open(os.path.join(OUT, "papers.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=1)
    names = drawings()
    print("OK ->", len(meta), "papiers,", len(names), "dessins")


if __name__ == "__main__":
    main()
