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
import textures as tx  # noqa: E402
from trimesh.visual.material import PBRMaterial  # noqa: E402

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
    wobbly_line(d, [(118, 215), (163, 200), (203, 212)], rnd)
    wobbly_line(d, [(279, 214), (324, 198), (369, 210)], rnd)
    wobbly_line(d, [(240, 250), (232, 330), (222, 372), (246, 378)], rnd, width=2)
    wobbly_line(d, [(150, 400), (170, 430)], rnd, width=2)
    wobbly_line(d, [(330, 400), (310, 430)], rnd, width=2)

    # Yeux GOMMÉS : frottis gris de gomme + papier troué au centre
    for ex, ey in ((160, 262), (322, 262)):
        smear = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        smd = ImageDraw.Draw(smear)
        for _ in range(60):  # coups de gomme dans tous les sens
            a = rnd.uniform(-0.5, 0.5)
            L = rnd.uniform(25, 60)
            x0, y0 = ex + rnd.uniform(-35, 35), ey + rnd.uniform(-22, 22)
            smd.line([(x0 - L * math.cos(a), y0 - L * math.sin(a)), (x0 + L * math.cos(a), y0 + L * math.sin(a))],
                     fill=(120, 110, 122, rnd.randint(18, 40)), width=rnd.randint(6, 14))
        smear = smear.filter(ImageFilter.GaussianBlur(3))
        img.alpha_composite(smear)
        img.putalpha(mask)
        # trou déchiré (alpha 0) : on voit le noir de la tête à travers
        hole = [(ex + 31 * math.cos(a) * rnd.uniform(0.65, 1.15), ey + 22 * math.sin(a) * rnd.uniform(0.65, 1.2))
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
    # Larmes d'encre qui coulent des trous des yeux
    for ex in (160, 322):
        for k in range(3):
            x = ex + rnd.uniform(-22, 22)
            y0 = 262 + rnd.uniform(12, 20)
            L = rnd.uniform(60, 190)
            pts = [(x, y0)]
            for j in range(10):
                pts.append((pts[-1][0] + rnd.uniform(-2, 2), y0 + L * (j + 1) / 10))
            d.line(pts, fill=(14, 10, 18, 235), width=int(rnd.uniform(3, 7)))
            ex_, ey_ = pts[-1]
            r = rnd.uniform(4, 7)
            d.ellipse([ex_ - r, ey_ - r * 0.8, ex_ + r, ey_ + r * 1.3], fill=(14, 10, 18, 235))
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
    """Cornet acoustique : tube fin qui s'évase en pavillon (axe +Y), cuivre vert-de-gris."""
    profile = [(0.0, 0.0), (0.013, 0.0), (0.014, 0.07), (0.019, 0.13), (0.033, 0.18),
               (0.062, 0.215), (0.085, 0.23), (0.080, 0.232), (0.0, 0.20)]
    m = trimesh.creation.revolve(np.array(profile) * scale, sections=24)
    m.apply_transform(R(-90, X))  # revolve tourne autour de Z -> on met l'axe sur Y
    face_outward(m)
    return tx.dress_up(m, CUIVRE, kind="patina", rough=0.55, metal=0.6, texel=0.06)


def dress_texture():
    """Texture unique de la robe : tissu tissé mauve, taches d'eau, et encre qui remonte du bas
    avec de longues coulures. Canal de rugosité : l'encre est mouillée (brillante)."""
    W, H = 1024, 512
    fab, _ = tx.fabric(seed=5, threads=20)
    fab = np.asarray(fab.convert("L")).astype(np.float32) / 255
    tiled = np.tile(fab, (H // fab.shape[0] * 2, W // fab.shape[1] * 2))
    tiled = np.asarray(Image.fromarray((tiled * 255).astype(np.uint8)).resize((W, H))).astype(np.float32) / 255
    robe = np.array(ROBE[:3], np.float32) / 255
    col = tiled[..., None] * robe[None, None, :] * 1.08
    rng = np.random.default_rng(8)
    stains = np.asarray(Image.fromarray((rng.random((8, 16)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)).astype(np.float32) / 255
    col *= (1 - 0.12 * np.clip((stains - 0.55) * 3, 0, 1))[..., None]
    # encre : v = 0 en haut de l'image = bas de la robe (on retourne plus bas)
    ink = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(ink)
    base = np.asarray(Image.fromarray((rng.random((1, 24)) * 255).astype(np.uint8)).resize((W, 1), Image.BICUBIC))[0] / 255
    for x in range(W):
        top = int(H * (0.13 + 0.12 * base[x]))
        d.line([(x, 0), (x, top)], fill=255)
    for _ in range(46):  # coulures
        x = rng.uniform(0, W)
        L = rng.uniform(0.15, 0.55) * H
        wdt = rng.uniform(3, 10)
        d.line([(x, H * 0.15), (x + rng.uniform(-6, 6), L)], fill=255, width=int(wdt))
        d.ellipse([x - wdt, L - wdt * 0.8, x + wdt, L + wdt * 1.4], fill=255)
    ink_a = np.asarray(ink.filter(ImageFilter.GaussianBlur(2))).astype(np.float32) / 255
    encre = np.array(ENCRE[:3], np.float32) / 255
    col = col * (1 - ink_a[..., None]) + encre[None, None, :] * ink_a[..., None]
    col = col[::-1]  # bas de la robe en bas de l'image
    ink_a = ink_a[::-1]
    albedo = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGB")
    rough = 0.95 - 0.75 * ink_a
    mr = np.dstack([np.zeros_like(rough), rough, np.zeros_like(rough)])  # G = rugosité, B = métal
    mr_img = Image.fromarray((mr * 255).astype(np.uint8), "RGB")
    normal = tx.height_to_normal(np.tile(np.asarray(tx.fabric(seed=5, threads=20)[0].convert("L")).astype(np.float32) / 255, (2, 4)), 1.4)
    return albedo, mr_img, normal


def dress():
    """Robe longue évasée, plissée, bas en lambeaux et trempé d'encre (maillage fait main pour
    avoir un UV cylindrique propre : u = tour de la robe, v = hauteur)."""
    prof_y = np.array([0.0, 0.04, 0.25, 0.6, 0.95, 1.18, 1.30, 1.36])
    prof_r = np.array([0.42, 0.44, 0.37, 0.27, 0.20, 0.17, 0.16, 0.12])
    sections, rows = 96, 40
    rng = np.random.default_rng(3)
    tat_phase = rng.uniform(0, math.tau, 3)
    verts, uvs, faces = [], [], []
    for j in range(rows + 1):
        t = j / rows
        y = 1.36 * t
        r = np.interp(y, prof_y, prof_r)
        for i in range(sections + 1):
            u = i / sections
            a = u * math.tau
            pleat = 1 + 0.045 * math.sin(a * 22) * (1 - t) ** 0.7     # plis plus marqués en bas
            yy = y
            if j == 0:  # bas en lambeaux
                yy += 0.03 * (math.sin(a * 7 + tat_phase[0]) + 0.6 * math.sin(a * 13 + tat_phase[1])) + 0.02
            verts.append([r * pleat * math.cos(a), yy, r * pleat * math.sin(a)])
            uvs.append([u * 2.0, t])  # texture répétée 2 fois autour
    n = sections + 1
    for j in range(rows):
        for i in range(sections):
            a_, b_ = j * n + i, j * n + i + 1
            c_, d_ = (j + 1) * n + i + 1, (j + 1) * n + i
            faces += [[a_, c_, b_], [a_, d_, c_]]
    m = trimesh.Trimesh(vertices=np.array(verts), faces=np.array(faces), process=False)
    face_outward(m)
    albedo, mr, normal = dress_texture()
    m.visual = TextureVisuals(uv=np.array(uvs), material=PBRMaterial(
        baseColorTexture=albedo, metallicRoughnessTexture=mr, normalTexture=normal,
        roughnessFactor=1.0, metallicFactor=0.0, doubleSided=True))
    return m


def apron_texture():
    W, H = 256, 384
    rng = np.random.default_rng(12)
    fab, _ = tx.fabric(seed=9, threads=30)
    img = fab.resize((W, H)).convert("RGBA")
    arr = np.asarray(img).astype(np.float32)
    arr[..., :3] *= np.array([0.93, 0.89, 0.82])
    stains = np.asarray(Image.fromarray((rng.random((5, 4)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)).astype(np.float32) / 255
    arr[..., :3] *= (1 - 0.18 * np.clip((stains - 0.5) * 2.5, 0, 1))[..., None]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    # empreinte de main d'enfant à l'encre, à moitié effacée
    cx, cy = 150, 250
    d.ellipse([cx - 22, cy - 18, cx + 22, cy + 24], fill=(18, 14, 22, 150))
    for k, a in enumerate((-50, -22, 0, 22, 55)):
        ra = math.radians(a)
        L = 34 if k in (1, 2) else 28
        d.line([(cx + 14 * math.sin(ra), cy - 10), (cx + L * 1.3 * math.sin(ra), cy - 14 - L * math.cos(ra))],
               fill=(18, 14, 22, 150), width=10)
    return img.filter(ImageFilter.GaussianBlur(0.8))


def apron():
    """Tablier de gouvernante (portion de cylindre devant la robe)."""
    rows, seg = 12, 16
    verts, uvs, faces = [], [], []
    for j in range(rows + 1):
        t = j / rows
        y = 0.55 + t * 0.72
        r = np.interp(y, [0.55, 0.95, 1.27], [0.29, 0.215, 0.18]) + 0.012
        for i in range(seg + 1):
            u = i / seg
            a = math.radians(90 - 48 + u * 96)
            verts.append([r * math.cos(a), y, r * math.sin(a) + 0.004])
            uvs.append([1 - u, t])
    n = seg + 1
    for j in range(rows):
        for i in range(seg):
            a_, b_ = j * n + i, j * n + i + 1
            c_, d_ = (j + 1) * n + i + 1, (j + 1) * n + i
            faces += [[a_, b_, c_], [a_, c_, d_]]
    m = trimesh.Trimesh(vertices=np.array(verts), faces=np.array(faces), process=False)
    m.visual = TextureVisuals(uv=np.array(uvs), material=PBRMaterial(
        baseColorTexture=apron_texture(), roughnessFactor=0.95, metallicFactor=0.0, doubleSided=True))
    return m


def paper_sheet_texture(seed):
    """Petit papier plié (les notes de la directrice dans sa poche)."""
    rnd = random.Random(seed)
    img = Image.new("RGBA", (128, 160), (236, 229, 212, 255))
    d = ImageDraw.Draw(img)
    for k in range(9):
        y = 20 + k * 14
        d.line([(12, y), (12 + rnd.uniform(60, 104), y + rnd.uniform(-1, 1))], fill=(70, 60, 80, 200), width=2)
    return img


def key_mesh():
    """Vieille clé : anneau + tige + panneton."""
    bow = trimesh.creation.torus(major_radius=0.011, minor_radius=0.0025, major_sections=16, minor_sections=6)
    bow.apply_transform(R(90, Y) @ T(0, 0, 0))
    shaft = trimesh.creation.cylinder(radius=0.0028, height=0.05, sections=8)
    shaft.apply_transform(R(90, X) @ T(0, 0, 0))
    shaft.apply_transform(T(0, -0.036, 0))
    bit = trimesh.creation.box(extents=[0.003, 0.012, 0.012])
    bit.apply_transform(T(0, -0.055, 0.007))
    return trimesh.util.concatenate([bow, shaft, bit])


def build():
    scene = trimesh.Scene()

    def node(name, parent, matrix):
        scene.graph.update(frame_from=parent, frame_to=name, matrix=matrix)

    def add(mesh, name, parent, matrix=None):
        scene.add_geometry(mesh, node_name=name, geom_name=name, parent_node_name=parent,
                           transform=np.eye(4) if matrix is None else matrix)

    node("LEcoute", scene.graph.base_frame, np.eye(4))
    add(dress(), "Robe", "LEcoute")
    add(apron(), "Tablier", "LEcoute")

    # Poche du tablier avec des papiers pliés qui dépassent (ses notes : un papier de lore !)
    add(tx.dress_up(trimesh.creation.box(extents=[0.11, 0.08, 0.012]), rgb("#DDD3C3"), texel=0.05),
        "Poche", "LEcoute", T(-0.08, 0.86, 0.255) @ R(-18, Y) @ R(-8, X))
    for k in range(2):
        pv, puv, pf = bent_quad(0.06, 0.075, 1.0, 0.0, seg=2)
        add(textured(pv, puv, pf, paper_sheet_texture(k), name="Papier_Poche_%d" % k), "Papier_Poche_%d" % k,
            "LEcoute", T(-0.085 + k * 0.022, 0.915, 0.25 - k * 0.004) @ R(-18, Y) @ R(-10 + k * 14, Z))

    # Trousseau de clés à la ceinture
    node("Trousseau", "LEcoute", T(0.17, 1.22, 0.06) @ R(20, Z))
    ring = trimesh.creation.torus(major_radius=0.035, minor_radius=0.004)
    add(tx.dress_up(ring, CUIVRE, kind="patina", rough=0.5, metal=0.7, texel=0.03), "Trousseau_Anneau", "Trousseau", R(90, Y))
    for k in range(5):
        add(tx.dress_up(key_mesh(), CUIVRE, kind="patina", rough=0.5, metal=0.7, texel=0.03), "Cle_%d" % k,
            "Trousseau", R(-30 + k * 15, X) @ T(0, -0.04, 0))

    # Buste maigre, penché en avant, rangée de petits boutons noirs
    node("Buste", "LEcoute", T(0, 1.30, 0) @ R(26, X))
    add(ellipsoid(0.14, 0.30, 0.10, ROBE, lump=0.003, seed=31, texel=0.07), "Buste_Mesh", "Buste", T(0, 0.27, 0))
    for k in range(8):
        y = 0.08 + k * 0.058
        dy = (y - 0.27) / 0.30
        z = 0.10 * math.sqrt(max(0, 1 - dy * dy)) + 0.002
        add(ellipsoid(0.008, 0.008, 0.005, ENCRE, kind="plain", subdiv=1), "Bouton_%d" % k, "Buste", T(0, y, z))
    # Col en dentelle : festons tout autour du cou
    for k in range(16):
        a = k / 16 * math.tau
        add(ellipsoid(0.026, 0.016, 0.008, COL, subdiv=1, texel=0.03), "Dentelle_%d" % k, "Buste",
            T(0.075 * math.sin(a), 0.555, 0.06 * math.cos(a)) @ R(math.degrees(a), Y) @ R(-35, X))
    collar = trimesh.creation.torus(major_radius=0.07, minor_radius=0.018, major_sections=32, minor_sections=8)
    add(tx.dress_up(collar, COL, texel=0.03), "Col", "Buste", T(0, 0.56, 0) @ R(90, X))

    # Cou trop long, cassé vers l'avant (peau)
    node("Cou", "Buste", T(0, 0.57, 0) @ R(22, X))
    neck = capsule_y(0.26, 0.032, PEAU, kind="skin", lump=0.0015, seed=33, texel=0.05)
    neck.apply_transform(R(180, X))  # vers le haut
    add(neck, "Cou_Mesh", "Cou")

    # Tête : penchée sur le côté (elle écoute). Le noeud "Tete" est celui à animer.
    # Le buste (26°) et le cou (22°) penchent déjà : on redresse la tête pour que le visage
    # regarde devant, un peu vers le bas (vers toi).
    node("Tete", "Cou", T(0, 0.30, 0) @ R(26, Z) @ R(-36, X))
    add(ellipsoid(0.15, 0.22, 0.15, NUIT, kind="plain"), "Tete_Nuit", "Tete", T(0, 0.06, 0))
    add(ellipsoid(0.085, 0.075, 0.075, CHIGNON, lump=0.006, seed=35, texel=0.025), "Chignon", "Tete", T(0, 0.16, -0.13))
    for k, a in enumerate((-30, 25)):
        pin = trimesh.creation.cylinder(radius=0.003, height=0.16, sections=8)
        add(colored(pin, rgb("#C9C2B5"), rough=0.4, metal=0.7), "Epingle_%d" % k, "Tete",
            T(0, 0.17, -0.13) @ R(a, Z) @ R(90, Y))

    # Portrait en papier froissé = le visage
    portrait = portrait_texture()
    height, shade = tx.paper_layer(portrait.size, seed=21, creases=12)
    arr = np.asarray(portrait).astype(np.float32)
    arr[..., :3] *= shade[..., None]
    portrait = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    pv, puv, pf = bent_quad(0.30, 0.40, 0.17, 0.0, seg=28, rows=18, crumple=0.003, seed=6)
    add(textured(pv, puv, pf, portrait, name="Portrait", normal=tx.normal_from_height_clamped(height, 1.4)),
        "Portrait", "Tete", T(0, 0.055, 0.166))  # devant le crâne (sinon il perce le papier)

    # Cornets acoustiques plantés dans le crâne (elle n'a plus que l'ouïe)
    add(horn(0.8), "Cornet_G", "Tete", T(0.135, 0.10, -0.03) @ R(-72, Z) @ R(-28, X))
    add(horn(0.65), "Cornet_D", "Tete", T(-0.135, 0.04, -0.02) @ R(78, Z) @ R(-22, X))
    add(horn(0.55), "Cornet_Haut", "Tete", T(0.02, 0.25, -0.07) @ R(-12, Z) @ R(-42, X))

    # Câbles de téléphone qui pendent des cornets dans son dos (reliés au Standard)
    for c, (sx_, sy_) in enumerate(((0.10, 0.02), (-0.10, -0.02))):
        parent = "Tete"
        name0 = "Cable_%d" % c
        node(name0, parent, T(sx_, sy_, -0.12) @ R(28, X) @ R(12 * (1 if c else -1), Z))  # pend vers le bas, dans le dos
        parent = name0
        for k in range(9):
            nm = "Cable_%d_%d" % (c, k)
            node(nm, parent, (T(0, -0.06, 0) if k else np.eye(4)) @ R(-9 if k % 2 else 7, X) @ R(4, Z))
            add(capsule_y(0.06, 0.006, rgb("#2A2430"), kind="plain"), nm + "_Mesh", nm)
            parent = nm

    # Bras démesurés, tendus en avant pour tâtonner
    UP, FORE = 0.56, 0.56
    for side, sx in (("G", 1), ("D", -1)):
        sh = "Epaule_" + side
        node(sh, "Buste", T(0.16 * sx, 0.48, 0) @ R(8 * sx, Z) @ R(-34, X))
        add(ellipsoid(0.05, 0.045, 0.045, ROBE, texel=0.07), "Epaule_Boule_" + side, sh)
        add(capsule_y(UP, 0.034, ROBE, lump=0.002, seed=40, texel=0.07), "BrasHaut_" + side, sh)       # manche
        cuff = trimesh.creation.torus(major_radius=0.036, minor_radius=0.009, major_sections=20, minor_sections=6)
        add(tx.dress_up(cuff, COL, texel=0.03), "Manchette_" + side, sh, T(0, -UP + 0.02, 0) @ R(90, X))
        el = "Coude_" + side
        node(el, sh, T(0, -UP, 0) @ R(-48, X) @ R(-6 * sx, Z))
        add(ellipsoid(0.03, 0.03, 0.03, PEAU, kind="skin", texel=0.04), "Coude_Os_" + side, el)
        add(capsule_y(FORE, 0.026, PEAU, kind="skin", lump=0.0025, seed=41, texel=0.05), "AvantBras_" + side, el)
        hd = "Main_" + side
        node(hd, el, T(0, -FORE - 0.01, 0) @ R(-10, X))
        add(ellipsoid(0.036, 0.048, 0.017, PEAU, kind="skin", lump=0.002, seed=42, texel=0.04), "Paume_" + side, hd, T(0, -0.04, 0))
        for k, fx in enumerate((-0.027, -0.009, 0.009, 0.027)):
            j1 = "Doigt_%d_1_%s" % (k, side)
            j2 = "Doigt_%d_2_%s" % (k, side)
            L1 = 0.10 + 0.015 * (1.5 - abs(k - 1.5))
            node(j1, hd, T(fx * sx, -0.08, 0) @ R(-14, X) @ R(fx * 180 * sx, Z))
            add(ellipsoid(0.011, 0.011, 0.011, PEAU, kind="skin", subdiv=1, texel=0.03), j1 + "_Jointure", j1)
            add(capsule_y(L1, 0.0075, PEAU, kind="skin", texel=0.03), j1 + "_Mesh", j1)
            node(j2, j1, T(0, -L1 - 0.006, 0) @ R(-22, X))
            add(ellipsoid(0.0095, 0.0095, 0.0095, PEAU, kind="skin", subdiv=1, texel=0.03), j2 + "_Jointure", j2)
            # bouts trempés d'encre encore humide (brillants) + ongle pointu
            add(colored(capsule_y(0.085, 0.0065, ENCRE, kind="plain"), ENCRE, rough=0.2), j2 + "_Mesh", j2)
            nail = trimesh.creation.cone(radius=0.006, height=0.03, sections=8)
            nail.apply_transform(R(90, X))
            add(colored(nail, ENCRE, rough=0.15), j2 + "_Ongle", j2, T(0, -0.09, 0))
        th = "Doigt_Pouce_" + side
        node(th, hd, T(0.034 * sx, -0.03, 0.01) @ R(40 * sx, Z) @ R(-20, X))
        add(capsule_y(0.08, 0.0075, PEAU, kind="skin", texel=0.03), th + "_Mesh", th)

    return scene


if __name__ == "__main__":
    scene = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    scene.export(OUT)
    tx.dedupe_glb(OUT)
    tris = sum(len(g.faces) for g in scene.geometry.values())
    print("OK ->", OUT, "| triangles :", tris, "| taille :", os.path.getsize(OUT) // 1024, "Ko")
