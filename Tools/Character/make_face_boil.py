"""
Fabrique les images du visage "qui gribouille" à partir de ton dessin (face.png / mouth.png).
Ton dessin original n'est JAMAIS modifié : on écrit des copies dans Assets/Horror/Art/Face/Boil/.

 - face_parle_0..2 : le trait "tremble" (comme un dessin animé fait main) quand on parle
 - face_cri_0..2   : trait très agité + gribouillis rageurs sur les yeux quand on crie
 - mouth_parle_0..2: la bouche qui tremble (trait épaissi, pour le modèle 3D)

Usage (depuis la racine du dépôt) : python3 Tools/Character/make_face_boil.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FACE = os.path.join(ROOT, "Assets/Horror/Art/Face/face.png")
MOUTH = os.path.join(ROOT, "Assets/Horror/Art/Face/mouth.png")
OUT_DIR = os.path.join(ROOT, "Assets/Horror/Art/Face/Boil")

INK = (24, 20, 26)


def noise_field(w, h, cells, amp, rng):
    """Champ de déplacement doux (bruit basse résolution agrandi)."""
    def channel():
        small = rng.uniform(-1, 1, size=(cells[1], cells[0])).astype(np.float32)
        img = Image.fromarray(small, mode="F").resize((w, h), Image.BICUBIC)
        return np.asarray(img) * amp
    return channel(), channel()


def warp(rgba, amp, rng, cells=(10, 8)):
    """Déforme une image RGBA avec un petit champ de bruit (échantillonnage bilinéaire)."""
    arr = np.asarray(rgba).astype(np.float32)
    h, w = arr.shape[:2]
    dx, dy = noise_field(w, h, cells, amp, rng)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    sx = np.clip(xx + dx, 0, w - 1.001)
    sy = np.clip(yy + dy, 0, h - 1.001)
    x0, y0 = sx.astype(int), sy.astype(int)
    fx, fy = (sx - x0)[..., None], (sy - y0)[..., None]
    a = arr[y0, x0]; b = arr[y0, x0 + 1]; c = arr[y0 + 1, x0]; d = arr[y0 + 1, x0 + 1]
    out = a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


def split_face(face):
    """Sépare le dessin en 'remplissage blanc' et 'traits noirs'."""
    arr = np.asarray(face).astype(np.int32)
    lum = arr[..., :3].mean(axis=2)
    alpha = arr[..., 3]
    ink_amount = np.clip((200 - lum) / 200.0, 0, 1) * (alpha / 255.0)
    lines = np.zeros_like(arr)
    lines[..., 0], lines[..., 1], lines[..., 2] = INK
    lines[..., 3] = (ink_amount * 255).astype(np.int32)
    fill = arr.copy()
    fill[..., :3] = np.where(alpha[..., None] > 0, 255, fill[..., :3])  # intérieur blanc, sans traits
    return Image.fromarray(fill.astype(np.uint8), "RGBA"), Image.fromarray(lines.astype(np.uint8), "RGBA")


def scribble(draw, cx, cy, rx, ry, rng, loops=7, width=3):
    """Gribouillis d'enfant énervé : une boucle qui tourne en tous sens dans une zone."""
    pts = []
    ang = rng.uniform(0, math.tau)
    phase = rng.uniform(0, math.tau)
    ox, oy = 0.0, 0.0  # le centre de la boucle dérive, comme une main qui s'énerve
    for i in range(loops * 26):
        ang += rng.uniform(0.18, 0.42)
        r = 0.2 + 0.8 * abs(math.sin(i * 0.11 + phase)) * rng.uniform(0.8, 1.0)
        ox = max(-0.35, min(0.35, ox + rng.uniform(-0.06, 0.06)))
        oy = max(-0.35, min(0.35, oy + rng.uniform(-0.06, 0.06)))
        pts.append((cx + (math.cos(ang) * r + ox) * rx, cy + (math.sin(ang) * r + oy) * ry))
    draw.line(pts, fill=INK + (235,), width=width, joint="curve")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    face = Image.open(FACE).convert("RGBA")
    mouth = Image.open(MOUTH).convert("RGBA")
    fill, lines = split_face(face)

    # Zones des yeux mesurées sur ton croquis (en pixels de face.png)
    eyes = [(198, 128, 30, 36), (296, 150, 42, 44)]

    for i in range(3):
        rng = np.random.default_rng(100 + i)
        # Parler : trait qui tremble légèrement
        talk = fill.copy()
        talk.alpha_composite(warp(lines, 1.6, rng))
        talk.save(os.path.join(OUT_DIR, "face_parle_%d.png" % i))

        # Crier : trait très agité + gribouillis sur les yeux
        rng2 = np.random.default_rng(200 + i)
        cry = warp(fill, 2.0, rng2)
        cry.alpha_composite(warp(lines, 4.5, rng2, cells=(14, 11)))
        mask = cry.getchannel("A")
        scrib = Image.new("RGBA", cry.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(scrib)
        pr = random.Random(300 + i)
        for (cx, cy, rx, ry) in eyes:
            scribble(d, cx + pr.uniform(-4, 4), cy + pr.uniform(-4, 4), rx * 1.2, ry * 1.2, pr, width=2)
        # quelques traits nerveux qui partent des yeux vers le bas (comme des larmes rayées)
        for (cx, cy, rx, ry) in eyes:
            for _ in range(2):
                x0 = cx + pr.uniform(-rx * 0.6, rx * 0.6)
                pts = [(x0, cy + ry * 0.6)]
                for k in range(5):
                    pts.append((pts[-1][0] + pr.uniform(-6, 6), pts[-1][1] + pr.uniform(10, 18)))
                d.line(pts, fill=INK + (255,), width=2)
        # le gribouillis ne déborde pas du visage
        scrib.putalpha(Image.fromarray(np.minimum(np.asarray(scrib.getchannel("A")), np.asarray(mask))))
        cry.alpha_composite(scrib)
        cry.save(os.path.join(OUT_DIR, "face_cri_%d.png" % i))

        # Bouche : trait épaissi (lisible en VR) qui tremble
        thick = Image.new("RGBA", mouth.size, INK + (0,))
        thick.putalpha(mouth.getchannel("A").filter(ImageFilter.MaxFilter(5)))
        pad = Image.new("RGBA", (mouth.width, mouth.height), INK + (0,))
        pad.alpha_composite(thick)
        warp(pad, 1.4, np.random.default_rng(400 + i), cells=(8, 3)).save(
            os.path.join(OUT_DIR, "mouth_parle_%d.png" % i))

    print("OK ->", OUT_DIR, sorted(os.listdir(OUT_DIR)))


if __name__ == "__main__":
    main()
