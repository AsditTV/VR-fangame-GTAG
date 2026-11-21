"""Images de la fiche Meta Horizon Store (PNG 24 bits, tailles exigées par Meta).

Usage (racine du dépôt) : python Tools/Marketing/make_store_assets.py
Entrées : Marketing/calm_faces_logo_banniere.png, Marketing/calm_faces_profil.png, Marketing/Store/*.png (rendus du jeu)
Sorties : Marketing/Store/meta/ (base, couvertures paysage/carrée/portrait, couverture principale 3000x900, icône, logo)
Le logo est détouré (fond nuit retiré, traits gardés) puis posé sur une vraie image du jeu, assombrie.
"""
import os
from PIL import Image, ImageEnhance, ImageFilter, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
M = os.path.join(ROOT, "Marketing")
OUT = os.path.join(M, "Store", "meta")
os.makedirs(OUT, exist_ok=True)
NUIT = (8, 7, 11)


def detoure(img, seuil=22):
    """Rend transparent le fond nuit du logo (les traits sombres du visage restent : ils sont plus clairs que le fond)."""
    img = img.convert("RGBA")
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            d = abs(r - NUIT[0]) + abs(g - NUIT[1]) + abs(b - NUIT[2])
            if d < seuil:
                px[x, y] = (r, g, b, 0)
            elif d < seuil * 2:
                px[x, y] = (r, g, b, int(255 * (d - seuil) / seuil))
    return img


def fond(src, w, h, sombre=0.42, focus=(0.5, 0.5)):
    """Une capture du jeu recadrée (en gardant le point d'intérêt), assombrie, avec un vignettage."""
    im = Image.open(src).convert("RGB")
    s = max(w / im.width, h / im.height)
    im = im.resize((int(im.width * s + 0.5), int(im.height * s + 0.5)), Image.LANCZOS)
    cx = int(im.width * focus[0] - w / 2); cy = int(im.height * focus[1] - h / 2)
    cx = max(0, min(im.width - w, cx)); cy = max(0, min(im.height - h, cy))
    im = im.crop((cx, cy, cx + w, cy + h))
    im = ImageEnhance.Color(im).enhance(0.75)
    im = ImageEnhance.Brightness(im).enhance(sombre)
    vig = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(vig)
    d.ellipse((-w * 0.25, -h * 0.35, w * 1.25, h * 1.35), fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(min(w, h) // 6))
    noir = Image.new("RGB", (w, h), NUIT)
    return Image.composite(im, noir, vig)


def pose(base, logo, largeur, centre):
    l = logo.resize((int(largeur), int(logo.height * largeur / logo.width)), Image.LANCZOS)
    x = int(centre[0] * base.width - l.width / 2); y = int(centre[1] * base.height - l.height / 2)
    base.paste(l, (x, y), l)
    return base


banniere = Image.open(os.path.join(M, "calm_faces_logo_banniere.png"))
logo = detoure(banniere)
logo = logo.crop(logo.getbbox())
logo.save(os.path.join(OUT, "logo_transparent.png"))                      # le seul PNG avec transparence (logo facultatif)

S = os.path.join(M, "Store")
doc, ecoute, salle = (os.path.join(S, f) for f in ("01_docteur.png", "02_ecoute.png", "05_salle_de_jeux.png"))

# le logo dans la zone vide, le monstre visible ; hauteur du logo bornée pour les formats larges
pose(fond(doc, 2560, 1440, sombre=0.62), logo, 980, (0.77, 0.5)).save(os.path.join(OUT, "base_2560x1440.png"))
pose(fond(doc, 2560, 1440, sombre=0.62), logo, 980, (0.77, 0.5)).save(os.path.join(OUT, "couverture_paysage_2560x1440.png"))
pose(fond(ecoute, 1440, 1440, sombre=0.6, focus=(0.6, 0.45)), logo, 760, (0.5, 0.7)).save(os.path.join(OUT, "couverture_carree_1440x1440.png"))
pose(fond(ecoute, 1008, 1440, sombre=0.6, focus=(0.62, 0.45)), logo, 640, (0.5, 0.74)).save(os.path.join(OUT, "couverture_portrait_1008x1440.png"))
pose(fond(doc, 3000, 900, sombre=0.62, focus=(0.45, 0.42)), logo, int(780 * 938 / 892), (0.8, 0.5)).save(os.path.join(OUT, "couverture_principale_3000x900.png"))

icone = Image.open(os.path.join(M, "calm_faces_profil.png")).convert("RGB").resize((512, 512), Image.LANCZOS)
icone.save(os.path.join(OUT, "icone_512.png"))
for f in sorted(os.listdir(OUT)):
    im = Image.open(os.path.join(OUT, f)); print(f, im.size, im.mode)
