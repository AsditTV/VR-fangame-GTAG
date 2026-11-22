# L'insigne de la casquette du veilleur de nuit (cosmétique réservé aux modérateurs) : écusson de laiton usé,
# « NIGHT WATCH » gravé, une clé au centre (Armand ferme les portes), « M.I. » en bas (Institut Marivaux).
# Fond transparent (matériau à découpe alpha).
# Usage : python make_insigne_veilleur.py <fichier png>
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

out = sys.argv[1]
W, H = 256, 320
rng = np.random.default_rng(85)


def police(taille):
    for nom in ("C:/Windows/Fonts/georgiab.ttf", "C:/Windows/Fonts/timesbd.ttf", "C:/Windows/Fonts/arialbd.ttf"):
        try:
            return ImageFont.truetype(nom, taille)
        except OSError:
            continue
    return ImageFont.load_default()


# forme d'écusson : haut droit à pans coupés, bas en pointe
ecusson = [(28, 18), (228, 18), (244, 40), (244, 190), (128, 302), (12, 190), (12, 40)]
masque = Image.new("L", (W, H), 0)
ImageDraw.Draw(masque).polygon(ecusson, fill=255)

# laiton : dégradé vertical + bruit + usure
y = np.linspace(0, 1, H)[:, None]
base = np.zeros((H, W, 3), np.float32)
base[..., 0] = 196 - 60 * y
base[..., 1] = 150 - 55 * y
base[..., 2] = 72 - 30 * y
base += (rng.random((H, W)) * 30 - 15)[..., None]
img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
d = ImageDraw.Draw(img)

# bord en relief (plus clair) puis filet gravé (sombre)
d.line(ecusson + [ecusson[0]], fill=(236, 204, 128, 255), width=7)
interieur = [(40, 34), (216, 34), (228, 50), (228, 184), (128, 280), (28, 184), (28, 50)]
d.line(interieur + [interieur[0]], fill=(70, 46, 18, 255), width=3)

# textes gravés
sombre = (58, 36, 14, 255)
f1, f2 = police(34), police(30)
for texte, yy, f in (("NIGHT", 52, f1), ("WATCH", 90, f1)):
    w = d.textlength(texte, font=f)
    d.text(((W - w) / 2, yy), texte, font=f, fill=sombre)
w = d.textlength("M.I.", font=f2)
d.text(((W - w) / 2, 222), "M.I.", font=f2, fill=sombre)

# la clé (anneau + tige + dents)
cx, cy = 128, 170
d.ellipse((cx - 50, cy - 16, cx - 18, cy + 16), outline=sombre, width=7)
d.rectangle((cx - 20, cy - 4, cx + 46, cy + 4), fill=sombre)
d.rectangle((cx + 30, cy + 4, cx + 36, cy + 18), fill=sombre)
d.rectangle((cx + 40, cy + 4, cx + 46, cy + 14), fill=sombre)

# usure : rayures claires et taches sombres, sur un calque fondu par-dessus (sinon les taches sont opaques)
usure = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(usure)
for _ in range(70):
    x0, y0 = rng.integers(20, W - 20), rng.integers(20, H - 40)
    L = rng.integers(6, 26)
    a = rng.uniform(0, np.pi)
    d.line((x0, y0, x0 + L * np.cos(a), y0 + L * np.sin(a)), fill=(240, 214, 150, 70), width=1)
for _ in range(25):
    x0, y0, r = rng.integers(20, W - 20), rng.integers(20, H - 40), rng.integers(4, 12)
    d.ellipse((x0 - r, y0 - r, x0 + r, y0 + r), fill=(60, 44, 20, 35))

img = Image.alpha_composite(img, usure.filter(ImageFilter.GaussianBlur(1.2)))
img = img.filter(ImageFilter.GaussianBlur(0.6))
img.putalpha(masque)
img.save(out)
print("insigne ->", out)
