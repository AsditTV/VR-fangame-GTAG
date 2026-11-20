"""
Génère le modèle 3D du monstre aveugle "L'Écoute" (Céleste Marivaux) au format .glb.

Silhouette pensée pour faire peur en VR, même dans le noir :
 - 2,3 m, penchée en avant, tête cassée sur le côté comme quelqu'un qui écoute ;
 - longue robe de gouvernante dont le bas est trempé d'encre qui coule ;
 - visage = un portrait en papier dessiné dont les YEUX ONT ÉTÉ GOMMÉS jusqu'à trouer le papier
   (on voit du noir à travers) et la bouche cousue d'une oreille à l'autre ;
 - des cornets acoustiques (vieilles "oreilles" en cuivre) plantés dans le crâne ;
 - bras beaucoup trop longs, doigts fins tachés d'encre, tendus en avant pour tâtonner ;
 - un trousseau de clés à la ceinture (les portes à boutons... et un bruit qui la trahit).

Usage (depuis la racine du dépôt) : python3 Tools/Character/build_l_ecoute.py
Sortie : Assets/Horror/Art/Monsters/LEcoute.glb
"""
import math
import os
import random
import sys

import numpy as np
import trimesh
from PIL import Image, ImageDraw, ImageFilter
from trimesh.visual import TextureVisuals

sys.path.insert(0, os.path.dirname(__file__))
from build_le_dessine import (R, S, T, X, Y, Z, bent_quad, capsule_y, colored, ellipsoid,  # noqa: E402
                              rgb, textured)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets/Horror/Art/Monsters/LEcoute.glb")

ROBE = rgb("#7D7088")       # mauve poussiéreux
ENCRE = rgb("#120E16")      # encre presque noire
PEAU = rgb("#CEC4CF")       # gris-lilas maladif
COL = rgb("#E6DDCF")        # col blanc cassé
CHIGNON = rgb("#4A4052")
CUIVRE = rgb("#86A396")     # cuivre vert-de-gris, pastel
NUIT = rgb("#0B090D")       # intérieur de la tête (visible par les trous des yeux)
FIL_ROUGE = (92, 40, 48)


# ---------------------------------------------------------------- texture du portrait gommé
def wobbly_line(draw, pts, rnd, width=3, color=(28, 22, 30, 255), jitter=1.6):
    out = []
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        n = max(2, int(math.hypot(x1 - x0, y1 - y0) / 6))
        for k in range(n):
            t = k / n
            out.append((x0 + (x1 - x0) * t + rnd.uniform(-jitter, jitter),
                        y0 + (y1 - y0) * t + rnd.uniform(-jitter, jitter)))
    out.append(pts[-1])
    draw.line(out, fill=color, width=width, joint="curve")


def portrait_texture():
    w, h = 480, 640
    rnd = random.Random(7)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Papier : ovale allongé, un peu de travers, couleur papier vieilli
    cx, cy, rx, ry = 240, 330, 205, 290
    oval = [(cx + rx * math.cos(a) * (1 + 0.03 * math.sin(3 * a)),
             cy + ry * math.sin(a) * (1 + 0.02 * math.cos(5 * a)))
            for a in np.linspace(0, math.tau, 120)]
    d.polygon(oval, fill=(232, 224, 207, 255))
    # taches et jaunissement
    stains = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(stains)
    for _ in range(14):
        x, y, r = rnd.uniform(80, 400), rnd.uniform(80, 580), rnd.uniform(15, 60)
        sd.ellipse([x - r, y - r, x + r, y + r], fill=(150, 125, 95, rnd.randint(18, 40)))
    stains = stains.filter(ImageFilter.GaussianBlur(14))
    mask = img.getchannel("A")
    img.alpha_composite(stains)
    img.putalpha(mask)
    d = ImageDraw.Draw(img)
    wobbly_line(d, oval, rnd, width=3)

    # Traits du visage : sourcils sévères, nez long, rides
    wobbly_line(d, [(130, 215), (175, 200), (215, 212)], rnd)
    wobbly_line(d, [(265, 214), (310, 198), (355, 210)], rnd)
    wobbly_line(d, [(240, 250), (232, 330), (222, 372), (246, 378)], rnd, width=2)
    wobbly_line(d, [(150, 400), (170, 430)], rnd, width=2)
    wobbly_line(d, [(330, 400), (310, 430)], rnd, width=2)

    # Yeux GOMMÉS : frottis gris de gomme + papier troué au centre
    for ex, ey in ((172, 262), (308, 262)):
        smear = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        smd = ImageDraw.Draw(smear)
        for _ in range(60):  # coups de gomme dans tous les sens
            a = rnd.uniform(-0.5, 0.5)
            L = rnd.uniform(25, 60)
            x0, y0 = ex + rnd.uniform(-35, 35), ey + rnd.uniform(-22, 22)
            smd.line([(x0 - L * math.cos(a), y0 - L * math.sin(a)), (x0 + L * math.cos(a), y0 + L * math.sin(a))],
                     fill=(80, 72, 86, rnd.randint(35, 75)), width=rnd.randint(6, 14))
        smear = smear.filter(ImageFilter.GaussianBlur(3))
        img.alpha_composite(smear)
        img.putalpha(mask)
        # trou déchiré (alpha 0) : on voit le noir de la tête à travers
        hole = [(ex + 36 * math.cos(a) * rnd.uniform(0.65, 1.15), ey + 25 * math.sin(a) * rnd.uniform(0.65, 1.2))
                for a in np.linspace(0, math.tau, 22)]
        hole_mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(hole_mask).polygon(hole, fill=255)
        a_ch = np.asarray(img.getchannel("A")).copy()
        a_ch[np.asarray(hole_mask) > 0] = 0
        mask = Image.fromarray(a_ch)  # on garde le trou pour l'oeil suivant
        img.putalpha(mask)
        d = ImageDraw.Draw(img)
        d.line(hole + [hole[0]], fill=(70, 60, 55, 255), width=2)  # bord du papier déchiré

    # Bouche cousue d'une joue à l'autre
    mouth = [(110, 470), (170, 482), (240, 486), (310, 482), (372, 468)]
    wobbly_line(d, mouth, rnd, width=4)
    for k in range(11):
        t = k / 10
        x = 118 + t * 246
        y = 470 + 16 * math.sin(math.pi * t)
        d.line([(x - 7, y - 16), (x + 7, y + 16)], fill=FIL_ROUGE + (255,), width=3)
        d.line([(x + 7, y - 16), (x - 7, y + 16)], fill=FIL_ROUGE + (255,), width=3)
    return img


# ---------------------------------------------------------------- pièces
def face_outward(m):
    """Pour une forme tournée autour de l'axe Y : retourne les faces si elles regardent vers l'intérieur."""
    c = m.triangles_center
    radial = c.copy()
    radial[:, 1] = 0.0
    if np.sum(np.einsum("ij,ij->i", m.face_normals, radial)) < 0:
        m.invert()

def horn(scale=1.0):
    """Cornet acoustique : tube fin qui s'évase en pavillon (axe +Y)."""
    profile = [(0.0, 0.0), (0.013, 0.0), (0.014, 0.07), (0.019, 0.13), (0.033, 0.18),
               (0.062, 0.215), (0.085, 0.23), (0.080, 0.232), (0.0, 0.20)]
    m = trimesh.creation.revolve(np.array(profile) * scale, sections=24)
    m.apply_transform(R(-90, X))  # revolve tourne autour de Z -> on met l'axe sur Y
    face_outward(m)
    return colored(m, CUIVRE, rough=0.45, metal=0.65)


def dress():
    """Robe longue évasée, bas en lambeaux et trempé d'encre qui coule (couleurs par sommet)."""
    profile = [(0.0, 1.36), (0.12, 1.36), (0.16, 1.30), (0.17, 1.18), (0.20, 0.95),
               (0.27, 0.6), (0.37, 0.25), (0.44, 0.04), (0.42, 0.0), (0.0, 0.0)]
    m = trimesh.creation.revolve(np.array(profile), sections=48)
    m = m.subdivide()
    m.apply_transform(R(-90, X))
    v = m.vertices.copy()
    rnd = np.random.default_rng(3)
    ang = np.arctan2(v[:, 2], v[:, 0])
    # bas en lambeaux
    tatter = 0.035 * (np.sin(ang * 7) + 0.6 * np.sin(ang * 13 + 1.3)) + rnd.uniform(-0.01, 0.01, len(v))
    low = v[:, 1] < 0.06
    v[low, 1] += np.clip(tatter[low], -0.03, 0.05) + 0.02
    m.vertices = v
    # encre : remonte depuis le bas avec des coulures de longueurs différentes
    drip = 0.18 + 0.22 * np.abs(np.sin(ang * 5 + 0.7)) * np.abs(np.sin(ang * 11))
    t = np.clip((drip - v[:, 1]) / 0.12 + 0.5, 0, 1)
    robe, encre = np.array(ROBE[:3], float), np.array(ENCRE[:3], float)
    cols = (robe[None, :] * (1 - t[:, None]) + encre[None, :] * t[:, None]).astype(np.uint8)
    m.visual = trimesh.visual.ColorVisuals(m, vertex_colors=np.hstack([cols, np.full((len(v), 1), 255, np.uint8)]))
    face_outward(m)  # faces tournées vers l'extérieur (sinon la robe paraît noire)
    return m


def build():
    scene = trimesh.Scene()

    def node(name, parent, matrix):
        scene.graph.update(frame_from=parent, frame_to=name, matrix=matrix)

    def add(mesh, name, parent, matrix=None):
        scene.add_geometry(mesh, node_name=name, geom_name=name, parent_node_name=parent,
                           transform=np.eye(4) if matrix is None else matrix)

    node("LEcoute", scene.graph.base_frame, np.eye(4))
    add(dress(), "Robe", "LEcoute")

    # Trousseau de clés à la ceinture
    node("Trousseau", "LEcoute", T(0.17, 1.22, 0.06) @ R(20, Z))
    ring = trimesh.creation.torus(major_radius=0.035, minor_radius=0.004)
    add(colored(ring, CUIVRE, rough=0.4, metal=0.8), "Trousseau_Anneau", "Trousseau", R(90, Y))
    for k in range(4):
        key = trimesh.creation.box(extents=[0.008, 0.07, 0.016])
        add(colored(key, rgb("#9AA59E"), rough=0.4, metal=0.8), "Cle_%d" % k, "Trousseau",
            R(-25 + k * 17, X) @ T(0, -0.06, 0))

    # Buste maigre, penché en avant
    node("Buste", "LEcoute", T(0, 1.30, 0) @ R(26, X))
    add(ellipsoid(0.14, 0.30, 0.10, ROBE), "Buste_Mesh", "Buste", T(0, 0.27, 0))
    collar = trimesh.creation.torus(major_radius=0.07, minor_radius=0.022, major_sections=32, minor_sections=8)
    add(colored(collar, COL), "Col", "Buste", T(0, 0.56, 0) @ R(90, X))

    # Cou trop long, cassé vers l'avant
    node("Cou", "Buste", T(0, 0.57, 0) @ R(22, X))
    neck = capsule_y(0.26, 0.032, PEAU)
    neck.apply_transform(R(180, X))  # vers le haut
    add(neck, "Cou_Mesh", "Cou")

    # Tête : penchée sur le côté (elle écoute). Le noeud "Tete" est celui à animer.
    # Le buste (26°) et le cou (22°) penchent déjà : on redresse la tête pour que le visage
    # regarde devant, un peu vers le bas (vers toi).
    node("Tete", "Cou", T(0, 0.30, 0) @ R(26, Z) @ R(-36, X))
    add(ellipsoid(0.15, 0.22, 0.15, NUIT), "Tete_Nuit", "Tete", T(0, 0.06, 0))
    add(ellipsoid(0.085, 0.075, 0.075, CHIGNON), "Chignon", "Tete", T(0, 0.16, -0.13))
    for k, a in enumerate((-30, 25)):
        pin = trimesh.creation.cylinder(radius=0.003, height=0.16, sections=8)
        add(colored(pin, rgb("#C9C2B5"), rough=0.4, metal=0.7), "Epingle_%d" % k, "Tete",
            T(0, 0.17, -0.13) @ R(a, Z) @ R(90, Y))

    # Portrait en papier = le visage
    portrait = portrait_texture()
    pv, puv, pf = bent_quad(0.30, 0.40, 0.17, 0.0, seg=28)
    add(textured(pv, puv, pf, portrait, name="Portrait"), "Portrait", "Tete", T(0, 0.055, 0.148))

    # Cornets acoustiques plantés dans le crâne (elle n'a plus que l'ouïe)
    add(horn(0.8), "Cornet_G", "Tete", T(0.135, 0.10, -0.03) @ R(-72, Z) @ R(-28, X))
    add(horn(0.65), "Cornet_D", "Tete", T(-0.135, 0.04, -0.02) @ R(78, Z) @ R(-22, X))
    add(horn(0.55), "Cornet_Haut", "Tete", T(0.02, 0.25, -0.07) @ R(-12, Z) @ R(-42, X))

    # Bras démesurés, tendus en avant pour tâtonner
    UP, FORE = 0.56, 0.56
    for side, sx in (("G", 1), ("D", -1)):
        sh = "Epaule_" + side
        node(sh, "Buste", T(0.16 * sx, 0.48, 0) @ R(8 * sx, Z) @ R(-34, X))
        add(ellipsoid(0.05, 0.045, 0.045, ROBE), "Epaule_Boule_" + side, sh)
        add(capsule_y(UP, 0.034, ROBE), "BrasHaut_" + side, sh)       # manche
        el = "Coude_" + side
        node(el, sh, T(0, -UP, 0) @ R(-48, X) @ R(-6 * sx, Z))
        add(capsule_y(FORE, 0.026, PEAU), "AvantBras_" + side, el)
        hd = "Main_" + side
        node(hd, el, T(0, -FORE - 0.01, 0) @ R(-10, X))
        add(ellipsoid(0.036, 0.048, 0.017, PEAU), "Paume_" + side, hd, T(0, -0.04, 0))
        for k, fx in enumerate((-0.027, -0.009, 0.009, 0.027)):
            j1 = "Doigt_%d_1_%s" % (k, side)
            j2 = "Doigt_%d_2_%s" % (k, side)
            L1 = 0.10 + 0.015 * (1.5 - abs(k - 1.5))
            node(j1, hd, T(fx * sx, -0.08, 0) @ R(-14, X) @ R(fx * 180 * sx, Z))
            add(capsule_y(L1, 0.0075, PEAU), j1 + "_Mesh", j1)
            node(j2, j1, T(0, -L1 - 0.006, 0) @ R(-22, X))
            add(capsule_y(0.085, 0.0065, ENCRE), j2 + "_Mesh", j2)     # bouts trempés d'encre
        th = "Doigt_Pouce_" + side
        node(th, hd, T(0.034 * sx, -0.03, 0.01) @ R(40 * sx, Z) @ R(-20, X))
        add(capsule_y(0.08, 0.0075, PEAU), th + "_Mesh", th)

    return scene


if __name__ == "__main__":
    scene = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    scene.export(OUT)
    tris = sum(len(g.faces) for g in scene.geometry.values())
    print("OK ->", OUT, "| triangles :", tris, "| taille :", os.path.getsize(OUT) // 1024, "Ko")
