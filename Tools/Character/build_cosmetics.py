"""
Génère les cosmétiques de la boutique (même style que la poupée : papier, tissu, laine).
Chaque fichier a son origine au point d'attache (haut du crâne, cou, poitrine).

Usage : python3 Tools/Character/build_cosmetics.py
Sortie : Assets/Horror/Art/Cosmetics/<id>.glb
"""
import math
import os
import sys

import numpy as np
import trimesh
from PIL import Image, ImageDraw
from trimesh.visual import TextureVisuals
from trimesh.visual.material import PBRMaterial

sys.path.insert(0, os.path.dirname(__file__))
import textures as tx  # noqa: E402
from build_le_dessine import R, S, T, X, Y, Z, colored, ellipsoid, rgb  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets/Horror/Art/Cosmetics")

# id, nom anglais, prix, emplacement (pour le menu Unity et la maquette)
CATALOG = [
    ("paper_crown", "Paper Crown", 5000, "Tete"),   # objet de prestige (voir Docs/ECONOMIE.md)
    ("party_hat", "Party Hat", 1000, "Tete"),
    ("ribbon_bow", "Ribbon Bow", 600, "Tete"),
    ("yarn_scarf", "Yarn Scarf", 1500, "Cou"),
    ("moon_badge", "Moon Badge", 300, "Poitrine"),
]


def crown_texture():
    img = Image.new("RGB", (512, 128), (242, 232, 205))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(4)
    cols = [(230, 160, 180), (160, 200, 230), (170, 215, 180), (240, 210, 120)]
    for k in range(14):  # étoiles et ronds dessinés au crayon par un enfant
        x, y = rng.uniform(10, 500), rng.uniform(40, 110)
        c = cols[k % 4]
        if k % 2:
            pts = [(x + 12 * math.cos(a) * (1 if i % 2 == 0 else 0.45), y + 12 * math.sin(a) * (1 if i % 2 == 0 else 0.45))
                   for i, a in enumerate(np.linspace(-math.pi / 2, 1.5 * math.pi, 11)[:-1])]
            d.polygon(pts, outline=c, width=3)
        else:
            d.ellipse([x - 8, y - 8, x + 8, y + 8], outline=c, width=3)
    return img


def paper_crown():
    """Couronne en papier découpé, en zigzag, posée de travers sur le crâne."""
    n, r, h0, h1 = 14, 0.1, 0.035, 0.085
    verts, uvs, faces = [], [], []
    for i in range(n * 2 + 1):
        a = i / (n * 2) * math.tau
        top = h1 if i % 2 == 0 else h0 + 0.012
        x, z = r * math.cos(a), r * math.sin(a)
        verts += [[x, 0, z], [x, top, z]]
        u = i / (n * 2)
        uvs += [[u, 0], [u, top / h1]]
    for i in range(n * 2):
        a, b, c, d = 2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2
        faces += [[a, b, c], [a, c, d]]
    m = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    m.visual = TextureVisuals(uv=np.array(uvs), material=PBRMaterial(
        baseColorTexture=crown_texture(), roughnessFactor=0.95, metallicFactor=0.0, doubleSided=True))
    m.apply_transform(R(-12, Z))
    return [m]


def stripes(c1, c2, n=8):
    img = Image.new("RGB", (256, 256), c1)
    d = ImageDraw.Draw(img)
    for k in range(n):
        if k % 2:
            d.rectangle([k * 256 / n, 0, (k + 1) * 256 / n, 256], fill=c2)
    return img


def party_hat():
    """Chapeau pointu à rayures pastel, un peu écrasé, avec un pompon de laine."""
    profile = np.array([[0, 0], [0.065, 0], [0.002, 0.17], [0, 0.17]])
    m = trimesh.creation.revolve(profile, sections=28)
    m.apply_transform(R(-90, X))
    v = m.vertices
    ang = np.arctan2(v[:, 2], v[:, 0])
    uv = np.stack([(ang / math.tau) % 1.0, v[:, 1] / 0.17], axis=1)
    m.visual = TextureVisuals(uv=uv, material=PBRMaterial(
        baseColorTexture=stripes((236, 190, 200), (190, 220, 210)), roughnessFactor=0.9, metallicFactor=0.0, doubleSided=True))
    m.apply_transform(R(-14, X) @ R(8, Z))
    pom = ellipsoid(0.025, 0.025, 0.025, rgb("#F2E8D8"), lump=0.004, seed=3, texel=0.02)
    pom.apply_transform(R(-14, X) @ R(8, Z) @ T(0, 0.175, 0))
    return [m, pom]


def ribbon_bow():
    """Gros noeud en tissu rose, cousu sur le côté du crâne."""
    parts = []
    for s in (1, -1):
        lobe = ellipsoid(0.05, 0.032, 0.016, rgb("#D99AAA"), lump=0.003, seed=5 + s, texel=0.03)
        lobe.apply_transform(T(0.045 * s, 0, 0) @ R(18 * s, Z))
        parts.append(lobe)
        tail = trimesh.creation.box(extents=[0.022, 0.06, 0.006])
        tail = tx.dress_up(tail, rgb("#C98597"), texel=0.03)
        tail.apply_transform(T(0.018 * s, -0.04, 0.004) @ R(-14 * s, Z))
        parts.append(tail)
    parts.append(ellipsoid(0.018, 0.02, 0.016, rgb("#C98597"), texel=0.03))
    for p in parts:
        p.apply_transform(T(0.09, 0.04, 0.02) @ R(-30, Z))  # sur le côté de la tête
    return parts


def yarn_scarf():
    """Écharpe en grosse laine menthe autour du cou, avec deux pans à franges."""
    ring = trimesh.creation.torus(major_radius=0.065, minor_radius=0.024, major_sections=28, minor_sections=10)
    ring.apply_transform(R(90, X))
    tx.lumpy(ring, 0.004, freq=90, seed=8)
    parts = [tx.dress_up(ring, rgb("#9CC9B6"), texel=0.025)]
    for k, (x, rz) in enumerate(((0.03, 6), (0.055, -4))):
        tail = trimesh.creation.box(extents=[0.04, 0.16, 0.014])
        tail.apply_transform(T(x, -0.09, 0.06) @ R(rz, Z))
        parts.append(tx.dress_up(tail, rgb("#9CC9B6" if k == 0 else "#B8D9C9"), texel=0.025))
        for f in range(4):  # franges
            fr = trimesh.creation.cylinder(radius=0.0025, height=0.03, sections=6)
            fr.apply_transform(R(90, X))
            fr.apply_transform(T(x - 0.015 + f * 0.01, -0.185, 0.06))
            parts.append(colored(fr, rgb("#8BB8A5")))
    return parts


def moon_badge():
    """Broche en forme de lune (comme les veilleuses du dortoir)."""
    disc = trimesh.creation.cylinder(radius=0.03, height=0.008, sections=28)
    cut = trimesh.creation.cylinder(radius=0.026, height=0.02, sections=28)
    cut.apply_translation([0.016, 0.01, 0])
    # croissant simple : un disque + un disque "creux" plus sombre pour donner la forme
    disc.apply_transform(R(90, X))
    cut.apply_transform(R(90, X))
    moon = colored(disc, rgb("#F0DC9A"), rough=0.4, metal=0.3)
    shadow = colored(cut, rgb("#B3A0C6"), rough=0.8)
    shadow.apply_transform(S(1, 1, 0.5))
    shadow.apply_translation([0, 0, 0.004])
    pin = trimesh.creation.torus(major_radius=0.034, minor_radius=0.0025)
    return [moon, shadow, colored(pin, rgb("#B69A62"), rough=0.35, metal=0.9)]


BUILDERS = {"paper_crown": paper_crown, "party_hat": party_hat, "ribbon_bow": ribbon_bow,
            "yarn_scarf": yarn_scarf, "moon_badge": moon_badge}


def build_all():
    os.makedirs(OUT, exist_ok=True)
    for cid, name, price, slot in CATALOG:
        sc = trimesh.Scene()
        sc.graph.update(frame_from=sc.graph.base_frame, frame_to=cid, matrix=np.eye(4))
        for i, part in enumerate(BUILDERS[cid]()):
            sc.add_geometry(part, node_name="%s_%d" % (cid, i), geom_name="%s_%d" % (cid, i), parent_node_name=cid)
        path = os.path.join(OUT, cid + ".glb")
        sc.export(path)
        tx.dedupe_glb(path)
        print("OK ->", path, os.path.getsize(path) // 1024, "Ko")


if __name__ == "__main__":
    build_all()
