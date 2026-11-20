"""
Génère le modèle 3D du personnage "Le Dessiné" (poupée de tissu + masque en papier dessiné)
au format .glb, à partir des dessins Assets/Horror/Art/Face/face.png et mouth.png.

Usage (depuis la racine du dépôt) :
    pip install trimesh pillow numpy
    python3 Tools/Character/build_le_dessine.py

Sortie : Assets/Horror/Art/Character/LeDessine.glb
Dans Unity, il faut le package officiel "glTFast" (com.unity.cloud.gltfast) pour importer un .glb.

C'est un modèle "low-poly stylisé" fait par code : il sert à valider la silhouette et le style.
Les noeuds sont séparés (Tete, Masque, Bouche, Epaule_G/D, Coude_G/D, Main_G/D...) pour que
Unity puisse placer les bras sur les manettes et animer la bouche avec MicMouth.
"""
import math
import os
import sys

import numpy as np
import trimesh
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from trimesh.visual.material import PBRMaterial
from trimesh.visual import TextureVisuals

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FACE_PNG = os.path.join(ROOT, "Assets/Horror/Art/Face/face.png")
MOUTH_PNG = os.path.join(ROOT, "Assets/Horror/Art/Face/mouth.png")
OUT = os.path.join(ROOT, "Assets/Horror/Art/Character/LeDessine.glb")

# ---------------------------------------------------------------- palette (pastel inquiétant)
def rgb(h, a=255):
    h = h.lstrip("#")
    return [int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a]

LILAS = rgb("#B3A0C6")        # corps (tissu)
LILAS_FONCE = rgb("#9583A9")  # jambes
TISSU_TETE = rgb("#D6C2BC")   # tête en tissu rose-beige délavé
PECHE = rgb("#E6BFAE")        # mains et pieds
MENTHE = rgb("#A6CBB6")       # pièce rapiécée
FIL = rgb("#3A3240")          # coutures
PAPIER = [0.95, 0.93, 0.86, 1.0]  # teinte "papier vieilli" appliquée au masque


def mat(color, rough=0.92, metal=0.0, name=None):
    return PBRMaterial(name=name, baseColorFactor=[c / 255 for c in color],
                       roughnessFactor=rough, metallicFactor=metal)


def colored(mesh, color, **kw):
    mesh.visual = TextureVisuals(material=mat(color, **kw))
    return mesh


# ---------------------------------------------------------------- transforms
def T(x=0.0, y=0.0, z=0.0):
    return trimesh.transformations.translation_matrix([x, y, z])


def R(deg, axis):
    return trimesh.transformations.rotation_matrix(math.radians(deg), axis)


def S(x, y, z):
    m = np.eye(4)
    m[0, 0], m[1, 1], m[2, 2] = x, y, z
    return m


X, Y, Z = [1, 0, 0], [0, 1, 0], [0, 0, 1]

# ---------------------------------------------------------------- primitives
def ellipsoid(rx, ry, rz, color, subdiv=None):
    if subdiv is None:  # moins de triangles pour les petites pièces (budget VR)
        subdiv = 3 if max(rx, ry, rz) > 0.1 else 2
    m = trimesh.creation.icosphere(subdivisions=subdiv, radius=1.0)
    m.apply_transform(S(rx, ry, rz))
    return colored(m, color)


def capsule_y(length, radius, color):
    """Capsule verticale, de y=0 à y=-length (pend vers le bas depuis son origine)."""
    count = [14, 10] if radius > 0.02 else [8, 5]  # doigts : moins de détails
    m = trimesh.creation.capsule(height=length, radius=radius, count=count)
    m.apply_transform(R(90, X))        # l'axe de trimesh est Z -> on le met sur Y
    m.apply_transform(T(0, -length / 2, 0))
    return colored(m, color)


def box(sx, sy, sz, color):
    return colored(trimesh.creation.box(extents=[sx, sy, sz]), color)


def cross_stitch(size=0.022, thick=0.0045, color=FIL):
    a = trimesh.creation.box(extents=[size, thick, thick])
    b = a.copy()
    a.apply_transform(R(40, Z))
    b.apply_transform(R(-40, Z))
    return colored(trimesh.util.concatenate([a, b]), color)


def bent_quad(width, height, bend_radius, x_offset_global, uv_rect=(0, 0, 1, 1), seg=24):
    """Rectangle courbé autour de l'axe Y (comme une feuille posée sur un visage).
    L'origine du maillage est au centre du rectangle ; x_offset_global sert à suivre la courbe
    d'un rectangle parent (pour la bouche posée sur le masque)."""
    u0, v0, u1, v1 = uv_rect
    verts, uvs, faces = [], [], []
    z_ref = bend_radius * math.cos(x_offset_global / bend_radius)
    for j in range(2):
        for i in range(seg + 1):
            t = i / seg
            xl = -width / 2 + t * width
            xg = x_offset_global + xl
            ang = xg / bend_radius
            x = bend_radius * math.sin(ang) - x_offset_global
            z = bend_radius * math.cos(ang) - z_ref
            y = -height / 2 + j * height
            verts.append([x, y, z])
            uvs.append([u0 + t * (u1 - u0), v0 + j * (v1 - v0)])
    n = seg + 1
    for i in range(seg):
        a, b, c, d = i, i + 1, n + i + 1, n + i
        faces += [[a, b, c], [a, c, d]]
    return np.array(verts), np.array(uvs), np.array(faces)


def textured(verts, uvs, faces, image, tint=(1.0, 1.0, 1.0, 1.0), name=None, alpha_mode="MASK"):
    # Attention : des entiers (1, 1, 1, 1) seraient lus comme 1/255 par trimesh -> invisible.
    m = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    material = PBRMaterial(name=name, baseColorTexture=image, baseColorFactor=list(tint),
                           roughnessFactor=0.95, metallicFactor=0.0, alphaMode=alpha_mode,
                           alphaCutoff=0.5 if alpha_mode == "MASK" else None, doubleSided=True)
    m.visual = TextureVisuals(uv=uvs, material=material)
    return m


def thicken_line(img, size=5):
    """Épaissit un trait fin (copie pour le modèle 3D uniquement, l'original n'est pas touché).
    Sans ça, la bouche écrasée par MicMouth (échelle 0,35) devient plus fine qu'un pixel et disparaît."""
    alpha = img.getchannel("A").filter(ImageFilter.MaxFilter(size))
    out = Image.new("RGBA", img.size, (24, 20, 26, 0))
    out.putalpha(alpha)
    return out


def body_surface(x, y, out=0.004, center_y=0.47, a=0.19, b=0.25, c=0.15):
    """Transform pour poser un objet À PLAT sur le ventre (ellipsoïde du corps), face vers l'extérieur."""
    dy = (y - center_y) / b
    z = c * math.sqrt(max(0.0, 1 - dy * dy - (x / a) ** 2))
    n = np.array([x / a ** 2, (y - center_y) / b ** 2, z / c ** 2])
    n /= np.linalg.norm(n)
    yaw = math.degrees(math.atan2(n[0], n[2]))
    pitch = -math.degrees(math.asin(n[1]))
    return T(*(np.array([x, y, z]) + n * out)) @ R(yaw, Y) @ R(pitch, X)


def tag_texture(number="3", name="Théo"):
    """Étiquette en carton épinglée sur la poitrine (numéro de l'enfant, cf. lore)."""
    w, h = 256, 176
    img = Image.new("RGBA", (w, h), (236, 228, 210, 255))
    d = ImageDraw.Draw(img)
    d.rectangle([5, 5, w - 6, h - 6], outline=(150, 70, 70, 255), width=5)
    font_big = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", 78)
    font_small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 30)
    d.text((w / 2, 74), "n°" + number, fill=(60, 45, 55, 255), font=font_big, anchor="mm")
    d.text((w / 2, 140), name, fill=(110, 80, 90, 255), font=font_small, anchor="mm")
    return img


# ---------------------------------------------------------------- construction
def build():
    for p in (FACE_PNG, MOUTH_PNG):
        if not os.path.exists(p):
            sys.exit("Fichier introuvable : " + p)

    face_img = Image.open(FACE_PNG).convert("RGBA")
    mouth_img = Image.open(MOUTH_PNG).convert("RGBA")
    fw, fh = face_img.size      # 522 x 391
    mw, mh = mouth_img.size     # 197 x 65

    scene = trimesh.Scene()
    base = scene.graph.base_frame

    def node(name, parent, matrix):
        scene.graph.update(frame_from=parent, frame_to=name, matrix=matrix)

    def add(mesh, name, parent, matrix=None):
        scene.add_geometry(mesh, node_name=name, geom_name=name, parent_node_name=parent,
                           transform=np.eye(4) if matrix is None else matrix)

    node("LeDessine", base, np.eye(4))

    # --- Jambes courtes et pieds (poupée)
    for side, sx in (("G", 1), ("D", -1)):  # G = gauche du personnage (+X, il regarde vers +Z)
        add(capsule_y(0.17, 0.055, LILAS_FONCE), "Jambe_" + side, "LeDessine", T(0.085 * sx, 0.26, 0))
        foot = ellipsoid(0.065, 0.04, 0.085, PECHE)
        add(foot, "Pied_" + side, "LeDessine", T(0.085 * sx, 0.04, 0.025))

    # --- Corps : un sac de tissu bombé
    add(ellipsoid(0.19, 0.25, 0.15, LILAS), "Corps", "LeDessine", T(0, 0.47, 0))

    # Couture verticale sur le ventre (points en croix)
    for k in range(8):
        y = 0.30 + k * 0.05
        add(cross_stitch(), "Couture_Ventre_%d" % k, "LeDessine", body_surface(0.0, y, 0.002))

    # Étiquette numérotée + épingle à nourrice
    tag = tag_texture()
    tv, tuv, tf = bent_quad(0.085, 0.058, 1.0, 0.0)
    tag_mesh = textured(tv, tuv, tf, tag, name="Etiquette")
    add(tag_mesh, "Etiquette", "LeDessine", body_surface(0.075, 0.585, 0.006) @ R(-8, Z))
    pin = trimesh.creation.torus(major_radius=0.016, minor_radius=0.0022)
    pin.apply_transform(S(1.0, 0.45, 1.0))
    colored(pin, rgb("#B9BEC4"), rough=0.35, metal=0.9)
    add(pin, "Epingle", "LeDessine", body_surface(0.072, 0.62, 0.012) @ R(-8, Z) @ R(90, X))

    # --- Cou
    neck = trimesh.creation.cylinder(radius=0.045, height=0.07, sections=16)
    neck.apply_transform(R(90, X))
    add(colored(neck, TISSU_TETE), "Cou", "LeDessine", T(0, 0.745, 0))

    # --- Tête (tissu) : noeud "Tete" = centre de la tête
    node("Tete", "LeDessine", T(0, 0.90, 0))
    add(ellipsoid(0.15, 0.145, 0.138, TISSU_TETE), "Tete_Tissu", "Tete")

    # Couture à l'arrière du crâne
    for k in range(7):
        a = math.radians(-60 + k * 20)
        y, z = 0.145 * math.sin(a), -0.138 * math.cos(a) - 0.002
        add(cross_stitch(0.02), "Couture_Crane_%d" % k, "Tete", T(0, y, z) @ R(-math.degrees(a), X))

    # Élastique noir qui tient le masque
    band = trimesh.creation.torus(major_radius=1.0, minor_radius=0.05, major_sections=40, minor_sections=8)
    band.apply_transform(S(0.152, 0.152, 0.152))
    band.apply_transform(R(90, X))
    band.apply_transform(S(1.0, 0.142 / 0.152, 0.135 / 0.152))  # aplati devant : passe DERRIÈRE le masque
    add(colored(band, FIL), "Elastique", "Tete", T(0, 0.01, -0.004))

    # --- Masque en papier = ton dessin, légèrement courbé
    BEND = 0.20
    oval_px_width = 270.0                 # largeur de l'ovale du visage dans face.png
    mask_w = 0.27 * fw / oval_px_width    # l'ovale fait ~27 cm de large
    mask_h = mask_w * fh / fw
    px = mask_w / fw                      # mètres par pixel
    mask_x = (fw / 2 - 250) * px          # recentre l'ovale (centre ~x=250 px)
    node("Masque", "Tete", T(mask_x, -0.003, 0.150))
    mv, muv, mf = bent_quad(mask_w, mask_h, BEND, 0.0)
    add(textured(mv, muv, mf, face_img, tint=PAPIER, name="Masque_Papier"), "Masque_Papier", "Masque")

    # --- Bouche : noeud séparé, origine au centre de la bouche (MicMouth étire son échelle Y)
    # Position mesurée sur ton croquis : 24,5 px à gauche et 69 px sous le centre du dessin.
    mouth_x = -24.5 * px
    mouth_y = -69.0 * px
    mouth_z = BEND * math.cos(mouth_x / BEND) - BEND + 0.0025
    node("Bouche", "Masque", T(mouth_x, mouth_y, mouth_z))
    bv, buv, bf = bent_quad(mw * px, mh * px, BEND, mouth_x)
    add(textured(bv, buv, bf, thicken_line(mouth_img), name="Bouche_Dessin", alpha_mode="BLEND"),
        "Bouche_Dessin", "Bouche")

    # --- Bras longs (style locomotion aux bras), en pose "A"
    UPPER, FORE = 0.25, 0.25
    for side, sx in (("G", 1), ("D", -1)):
        shoulder = "Epaule_" + side
        node(shoulder, "LeDessine", T(0.19 * sx, 0.66, 0) @ R(38 * sx, Z) @ R(-8, X))
        add(ellipsoid(0.055, 0.055, 0.055, LILAS), "Epaule_Boule_" + side, shoulder)
        add(capsule_y(UPPER, 0.042, LILAS), "BrasHaut_" + side, shoulder)

        elbow = "Coude_" + side
        node(elbow, shoulder, T(0, -UPPER, 0) @ R(-12 * sx, Z) @ R(-14, X))
        add(capsule_y(FORE, 0.038, LILAS), "AvantBras_" + side, elbow)

        hand = "Main_" + side
        node(hand, elbow, T(0, -FORE - 0.02, 0))
        # Paume (la face avant de la main = +Z local du noeud Main)
        add(ellipsoid(0.048, 0.05, 0.027, PECHE), "Paume_" + side, hand, T(0, -0.045, 0))

        # 3 doigts de dessin animé + 1 pouce, 2 articulations chacun.
        # Plier un doigt = tourner ses noeuds Doigt_*_1 et Doigt_*_2 autour de leur axe X local
        # (angle NÉGATIF = vers la paume). Le script HandFingerAnimator fait ça dans Unity.
        fingers = (("Index", 0.024, 0.034, 0.028),
                   ("Majeur", 0.0, 0.038, 0.030),
                   ("Annulaire", -0.024, 0.031, 0.025))
        for fname, fx, l1, l2 in fingers:
            j1 = "Doigt_%s_1_%s" % (fname, side)
            j2 = "Doigt_%s_2_%s" % (fname, side)
            node(j1, hand, T(fx * sx, -0.086, 0.002))
            add(capsule_y(l1, 0.0115, PECHE), j1 + "_Mesh", j1)
            node(j2, j1, T(0, -l1 - 0.004, 0))
            add(capsule_y(l2, 0.0105, PECHE), j2 + "_Mesh", j2)

        t1 = "Doigt_Pouce_1_" + side
        t2 = "Doigt_Pouce_2_" + side
        node(t1, hand, T(0.040 * sx, -0.038, 0.012) @ R(38 * sx, Z) @ R(-18, X))
        add(capsule_y(0.028, 0.012, PECHE), t1 + "_Mesh", t1)
        node(t2, t1, T(0, -0.032, 0))
        add(capsule_y(0.024, 0.011, PECHE), t2 + "_Mesh", t2)

    # Pièce rapiécée menthe sur le bras droit, avec ses points
    patch = box(0.07, 0.07, 0.008, MENTHE)
    add(patch, "Piece_Rapiecee", "Epaule_D", T(0, -0.13, 0.04) @ R(8, Z))
    for k, (dx, dy) in enumerate([(-0.03, 0.03), (0.03, 0.03), (-0.03, -0.03), (0.03, -0.03)]):
        add(cross_stitch(0.012, 0.003), "Piece_Point_%d" % k, "Epaule_D",
            T(dx, -0.13 + dy, 0.0455) @ R(8, Z))

    return scene


if __name__ == "__main__":
    scene = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    scene.export(OUT)
    tris = sum(len(g.faces) for g in scene.geometry.values())
    print("OK ->", OUT, "| triangles :", tris, "| taille :", os.path.getsize(OUT) // 1024, "Ko")
