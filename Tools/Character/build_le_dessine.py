"""
Génère le modèle 3D du personnage "Le Dessiné" (poupée de tissu + masque en papier dessiné)
au format .glb, à partir des dessins Assets/Horror/Art/Face/face.png et mouth.png.

Usage (depuis la racine du dépôt) :
    pip install trimesh pillow numpy
    python3 Tools/Character/build_le_dessine.py

Sortie : Assets/Horror/Art/Character/LeDessine.glb
Dans Unity, il faut le package officiel "glTFast" (com.unity.cloud.gltfast) pour importer un .glb.

Version 3 "détaillée" : tissu tissé avec relief, formes bosselées comme une poupée rembourrée
à la main, coutures, déchirure avec rembourrage qui dépasse, boutons dépareillés, fil qui pend,
cheveux en laine, masque en papier froissé.
Les noeuds gardent les mêmes noms (Tete, Masque, Bouche, Epaule_G/D, Coude_G/D, Main_G/D,
Doigt_*) pour que les scripts Unity (MicMouth, HandFingerAnimator, FaceScribble) marchent.
"""
import math
import os
import sys

import numpy as np
import trimesh
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from trimesh.visual.material import PBRMaterial
from trimesh.visual import TextureVisuals

sys.path.insert(0, os.path.dirname(__file__))
import textures as tx  # noqa: E402

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
SEMELLE = rgb("#B48F82")
MENTHE = rgb("#A6CBB6")       # pièces rapiécées
ROSE = rgb("#D9A7AE")
LAINE = rgb("#5B4766")        # cheveux en laine
RAMBOURRAGE = rgb("#F1ECE2")  # rembourrage blanc cassé
TROU = rgb("#2A2230")
FIL = rgb("#3A3240")          # coutures
PAPIER = [0.95, 0.93, 0.86, 1.0]  # teinte "papier vieilli" appliquée au masque


def mat(color, rough=0.92, metal=0.0, name=None):
    return PBRMaterial(name=name, baseColorFactor=[c / 255 for c in color],
                       roughnessFactor=rough, metallicFactor=metal)


def colored(mesh, color, **kw):
    """Matière unie (pour les petits détails : fils, boutons, métal)."""
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
def ellipsoid(rx, ry, rz, color, subdiv=None, kind="fabric", lump=0.0, seed=0, texel=0.1):
    """Ellipsoïde avec matière détaillée. lump = amplitude des bosses (en mètres)."""
    if subdiv is None:  # moins de triangles pour les petites pièces (budget VR)
        subdiv = 3 if max(rx, ry, rz) > 0.1 else 2
    m = trimesh.creation.icosphere(subdivisions=subdiv, radius=1.0)
    m.apply_transform(S(rx, ry, rz))
    if lump > 0:
        tx.lumpy(m, lump, freq=2 * math.pi / (max(rx, ry, rz) * 0.7), seed=seed)
    return tx.dress_up(m, color, kind, texel=texel)


def capsule_y(length, radius, color, kind="fabric", lump=0.0, seed=0, texel=0.06):
    """Capsule verticale, de y=0 à y=-length (pend vers le bas depuis son origine)."""
    count = [14, 10] if radius > 0.02 else [8, 5]  # doigts : moins de détails
    m = trimesh.creation.capsule(height=length, radius=radius, count=count)
    m.apply_transform(R(90, X))        # l'axe de trimesh est Z -> on le met sur Y
    m.apply_transform(T(0, -length / 2, 0))
    if lump > 0:
        tx.lumpy(m, lump, freq=40.0, seed=seed)
    return tx.dress_up(m, color, kind, texel=texel)


def box(sx, sy, sz, color, kind="fabric", texel=0.05):
    return tx.dress_up(trimesh.creation.box(extents=[sx, sy, sz]), color, kind, texel=texel)


def cross_stitch(size=0.022, thick=0.0045, color=FIL):
    a = trimesh.creation.box(extents=[size, thick, thick])
    b = a.copy()
    a.apply_transform(R(40, Z))
    b.apply_transform(R(-40, Z))
    return colored(trimesh.util.concatenate([a, b]), color)


def running_stitch(length=0.018, thick=0.004, color=FIL):
    """Un point droit (couture en pointillés)."""
    return colored(trimesh.creation.box(extents=[thick, length, thick]), color)


def bent_quad(width, height, bend_radius, x_offset_global, uv_rect=(0, 0, 1, 1), seg=24,
              rows=1, crumple=0.0, seed=0):
    """Rectangle courbé autour de l'axe Y (comme une feuille posée sur un visage).
    L'origine du maillage est au centre du rectangle ; x_offset_global sert à suivre la courbe
    d'un rectangle parent (pour la bouche posée sur le masque).
    rows/crumple : plus de lignes et des bosses en profondeur = papier froissé."""
    u0, v0, u1, v1 = uv_rect
    rng = np.random.default_rng(seed)
    waves = [(rng.normal(size=2) * rng.uniform(25, 60), rng.uniform(0, math.tau)) for _ in range(6)]
    verts, uvs, faces = [], [], []
    z_ref = bend_radius * math.cos(x_offset_global / bend_radius)
    for j in range(rows + 1):
        for i in range(seg + 1):
            t, s = i / seg, j / rows
            xl = -width / 2 + t * width
            xg = x_offset_global + xl
            ang = xg / bend_radius
            x = bend_radius * math.sin(ang) - x_offset_global
            z = bend_radius * math.cos(ang) - z_ref
            y = -height / 2 + s * height
            if crumple > 0:
                z += crumple * sum(math.sin(k[0] * xl + k[1] * y + ph) for k, ph in waves) / len(waves)
            verts.append([x, y, z])
            uvs.append([u0 + t * (u1 - u0), v0 + s * (v1 - v0)])
    n = seg + 1
    for j in range(rows):
        for i in range(seg):
            a, b = j * n + i, j * n + i + 1
            c, d = (j + 1) * n + i + 1, (j + 1) * n + i
            faces += [[a, b, c], [a, c, d]]
    return np.array(verts), np.array(uvs), np.array(faces)


def textured(verts, uvs, faces, image, tint=(1.0, 1.0, 1.0, 1.0), name=None, alpha_mode="MASK",
             normal=None):
    # Attention : des entiers (1, 1, 1, 1) seraient lus comme 1/255 par trimesh -> invisible.
    m = trimesh.Trimesh(vertices=verts, faces=faces, process=False)
    material = PBRMaterial(name=name, baseColorTexture=image, baseColorFactor=list(tint),
                           normalTexture=normal, roughnessFactor=0.95, metallicFactor=0.0,
                           alphaMode=alpha_mode, alphaCutoff=0.5 if alpha_mode == "MASK" else None,
                           doubleSided=True)
    m.visual = TextureVisuals(uv=uvs, material=material)
    return m


def thicken_line(img, size=5):
    """Épaissit un trait fin (copie pour le modèle 3D uniquement, l'original n'est pas touché).
    Sans ça, la bouche écrasée par MicMouth (échelle 0,35) devient plus fine qu'un pixel et disparaît."""
    alpha = img.getchannel("A").filter(ImageFilter.MaxFilter(size))
    out = Image.new("RGBA", img.size, (24, 20, 26, 0))
    out.putalpha(alpha)
    return out


def paper_face(face_img):
    """Ton dessin sur du vrai papier : grain + plis dans la couleur et dans le relief."""
    w, h = face_img.size
    height, shade = tx.paper_layer((w, h), seed=11)
    arr = np.asarray(face_img).astype(np.float32)
    arr[..., :3] *= shade[..., None]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")
    return img, tx.normal_from_height_clamped(height, 1.3)


def body_surface(x, y, out=0.004, center_y=0.47, a=0.19, b=0.25, c=0.15):
    """Transform pour poser un objet À PLAT sur le ventre (ellipsoïde du corps), face vers l'extérieur."""
    dy = (y - center_y) / b
    z2 = 1 - dy * dy - (x / a) ** 2
    z = c * math.sqrt(max(0.0, z2))
    n = np.array([x / a ** 2, (y - center_y) / b ** 2, z / c ** 2])
    n /= np.linalg.norm(n)
    yaw = math.degrees(math.atan2(n[0], n[2]))
    pitch = -math.degrees(math.asin(n[1]))
    return T(*(np.array([x, y, z]) + n * out)) @ R(yaw, Y) @ R(pitch, X)


def side_surface(y, side, out=0.003, center_y=0.47, a=0.19, b=0.25, angle_deg=90):
    """Point sur le flanc du corps (angle autour de l'axe vertical, 90 = côté gauche +X)."""
    dy = (y - center_y) / b
    r = math.sqrt(max(0.0, 1 - dy * dy))
    ang = math.radians(angle_deg) * side
    x, z = a * r * math.sin(ang), 0.15 * r * math.cos(ang)
    return T(x + math.sin(ang) * out, y, z + math.cos(ang) * out) @ R(math.degrees(ang), Y)


def tag_texture(number="3", name="Theo"):
    """Étiquette en carton épinglée sur la poitrine (numéro de l'enfant, cf. lore)."""
    w, h = 256, 176
    img = Image.new("RGBA", (w, h), (236, 228, 210, 255))
    d = ImageDraw.Draw(img)
    d.rectangle([5, 5, w - 6, h - 6], outline=(150, 70, 70, 255), width=5)
    font_big = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", 78)
    font_small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 30)
    d.text((w / 2, 74), "No." + number, fill=(60, 45, 55, 255), font=font_big, anchor="mm")
    d.text((w / 2, 140), name, fill=(110, 80, 90, 255), font=font_small, anchor="mm")
    # carton taché et un coin corné
    rng = np.random.default_rng(5)
    stain = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(stain)
    for _ in range(6):
        x, y, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(10, 40)
        sd.ellipse([x - r, y - r, x + r, y + r], fill=(120, 90, 60, int(rng.uniform(15, 35))))
    img.alpha_composite(stain.filter(ImageFilter.GaussianBlur(8)))
    d = ImageDraw.Draw(img)
    d.polygon([(w - 6, h - 40), (w - 6, h - 6), (w - 40, h - 6)], fill=(0, 0, 0, 0))
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

    # --- Jambes courtes, pieds avec semelle cousue
    for side, sx in (("G", 1), ("D", -1)):  # G = gauche du personnage (+X, il regarde vers +Z)
        add(capsule_y(0.17, 0.055, LILAS_FONCE, lump=0.003, seed=2 if sx > 0 else 3),
            "Jambe_" + side, "LeDessine", T(0.085 * sx, 0.26, 0))
        add(ellipsoid(0.065, 0.04, 0.085, PECHE, lump=0.003, seed=4, texel=0.05),
            "Pied_" + side, "LeDessine", T(0.085 * sx, 0.045, 0.025))
        add(ellipsoid(0.068, 0.014, 0.088, SEMELLE, texel=0.05), "Semelle_" + side, "LeDessine",
            T(0.085 * sx, 0.012, 0.025))
        for k in range(6):  # points autour du pied
            a = math.radians(-70 + k * 28)
            add(running_stitch(0.012, 0.0035), "Pied_Point_%s_%d" % (side, k), "LeDessine",
                T(0.085 * sx + 0.066 * math.sin(a), 0.03, 0.025 + 0.087 * math.cos(a)) @ R(math.degrees(a), Y) @ R(90, Z))
    # pièce rose sur le genou gauche
    add(box(0.05, 0.05, 0.006, ROSE), "Piece_Genou", "LeDessine", T(0.085, 0.2, 0.054) @ R(-6, Z))
    for k, (dx, dy) in enumerate([(-0.022, 0.022), (0.022, 0.022), (-0.022, -0.022), (0.022, -0.022)]):
        add(cross_stitch(0.01, 0.0028), "Genou_Point_%d" % k, "LeDessine", T(0.085 + dx, 0.2 + dy, 0.058))

    # --- Corps : un sac de tissu bombé et bosselé
    add(ellipsoid(0.19, 0.25, 0.15, LILAS, lump=0.006, seed=1), "Corps", "LeDessine", T(0, 0.47, 0))

    # Couture centrale (points en croix) + coutures de côté (pointillés)
    for k in range(8):
        y = 0.30 + k * 0.05
        add(cross_stitch(), "Couture_Ventre_%d" % k, "LeDessine", body_surface(0.0, y, 0.004))
    for side in (1, -1):
        for k in range(12):
            y = 0.27 + k * 0.035
            add(running_stitch(), "Couture_Cote_%d_%d" % (side, k), "LeDessine",
                side_surface(y, side, 0.004))

    # Boutons dépareillés
    for k, (y, r, col) in enumerate(((0.63, 0.016, MENTHE), (0.545, 0.013, ROSE))):
        btn = trimesh.creation.cylinder(radius=r, height=0.006, sections=18)
        add(colored(btn, col, rough=0.5), "Bouton_%d" % k, "LeDessine", body_surface(-0.07, y, 0.006))
        add(cross_stitch(r * 1.1, 0.003), "Bouton_Fil_%d" % k, "LeDessine", body_surface(-0.07, y, 0.0095))

    # Déchirure sur le flanc droit, rembourrage qui dépasse, couture abandonnée + fil qui pend
    tear = body_surface(-0.135, 0.40, -0.002)
    add(ellipsoid(0.038, 0.016, 0.01, TROU, kind="plain"), "Dechirure", "LeDessine", tear @ R(-30, Z))
    rng = np.random.default_rng(9)
    for k in range(6):
        stuff = ellipsoid(0.013 + rng.uniform(0, 0.007), 0.012 + rng.uniform(0, 0.006), 0.012, RAMBOURRAGE,
                          lump=0.002, seed=20 + k, texel=0.03)
        add(stuff, "Rembourrage_%d" % k, "LeDessine",
            tear @ R(-30, Z) @ T(rng.uniform(-0.025, 0.025), rng.uniform(-0.006, 0.006), 0.008 + rng.uniform(0, 0.008)))
    for k in range(3):
        add(cross_stitch(0.016, 0.0035), "Dechirure_Point_%d" % k, "LeDessine",
            tear @ R(-30, Z) @ T(0.03 + k * 0.016, 0, 0.006))
    node("Fil_Pendant", "LeDessine", tear @ R(-30, Z) @ T(0.078, 0, 0.008))
    parent = "Fil_Pendant"
    for k in range(5):  # chaîne de petits bouts de fil qui pendouille
        seg = capsule_y(0.022, 0.0018, FIL, kind="plain")
        name = "Fil_%d" % k
        node(name, parent, (T(0, -0.024, 0) if k else np.eye(4)) @ R(12 if k % 2 else -9, Z))
        add(seg, name + "_Mesh", name)
        parent = name

    # Étiquette numérotée + épingle à nourrice
    tag = tag_texture()
    tv, tuv, tf = bent_quad(0.085, 0.058, 1.0, 0.0)
    add(textured(tv, tuv, tf, tag, name="Etiquette"), "Etiquette", "LeDessine", body_surface(0.075, 0.585, 0.006) @ R(-8, Z))
    pin = trimesh.creation.torus(major_radius=0.016, minor_radius=0.0022)
    pin.apply_transform(S(1.0, 0.45, 1.0))
    colored(pin, rgb("#B9BEC4"), rough=0.35, metal=0.9)
    add(pin, "Epingle", "LeDessine", body_surface(0.072, 0.62, 0.012) @ R(-8, Z) @ R(90, X))

    # --- Cou
    neck = trimesh.creation.cylinder(radius=0.045, height=0.07, sections=16)
    neck.apply_transform(R(90, X))
    add(tx.dress_up(neck, TISSU_TETE, texel=0.05), "Cou", "LeDessine", T(0, 0.745, 0))

    # --- Tête (tissu bosselé) : noeud "Tete" = centre de la tête
    node("Tete", "LeDessine", T(0, 0.90, 0))
    add(ellipsoid(0.15, 0.145, 0.138, TISSU_TETE, lump=0.004, seed=7), "Tete_Tissu", "Tete")
    for k in range(7):  # couture à l'arrière du crâne
        a = math.radians(-60 + k * 20)
        y, z = 0.145 * math.sin(a), -0.138 * math.cos(a) - 0.004
        add(cross_stitch(0.02), "Couture_Crane_%d" % k, "Tete", T(0, y, z) @ R(-math.degrees(a), X))
    # cheveux en laine : quelques boucles cousues sur le dessus
    for k in range(7):
        loop = trimesh.creation.torus(major_radius=0.022, minor_radius=0.006, major_sections=16, minor_sections=6)
        a = -0.9 + k * 0.3
        add(tx.dress_up(loop, LAINE, texel=0.02), "Laine_%d" % k, "Tete",
            T(0.13 * math.sin(a), 0.135 * math.cos(a * 0.6) + 0.005, -0.03 - 0.02 * math.cos(a)) @ R(math.degrees(a) * 0.8, Z) @ R(70, X))

    # Élastique qui tient le masque (aplati devant : passe DERRIÈRE le masque)
    band = trimesh.creation.torus(major_radius=1.0, minor_radius=0.05, major_sections=40, minor_sections=8)
    band.apply_transform(S(0.152, 0.152, 0.152))
    band.apply_transform(R(90, X))
    band.apply_transform(S(1.0, 0.142 / 0.152, 0.135 / 0.152))
    add(tx.dress_up(band, FIL, texel=0.02), "Elastique", "Tete", T(0, 0.01, -0.004))

    # --- Masque en papier froissé = ton dessin
    BEND = 0.20
    oval_px_width = 270.0                 # largeur de l'ovale du visage dans face.png
    mask_w = 0.27 * fw / oval_px_width    # l'ovale fait ~27 cm de large
    mask_h = mask_w * fh / fw
    px = mask_w / fw                      # mètres par pixel
    mask_x = (fw / 2 - 250) * px          # recentre l'ovale (centre ~x=250 px)
    node("Masque", "Tete", T(mask_x, -0.003, 0.150))
    paper_img, paper_normal = paper_face(face_img)
    mv, muv, mf = bent_quad(mask_w, mask_h, BEND, 0.0, seg=32, rows=20, crumple=0.0022, seed=3)
    add(textured(mv, muv, mf, paper_img, tint=PAPIER, name="Masque_Papier", normal=paper_normal),
        "Masque_Papier", "Masque")

    # --- Bouche : noeud séparé, origine au centre de la bouche (MicMouth étire son échelle Y)
    mouth_x = -24.5 * px
    mouth_y = -69.0 * px
    mouth_z = BEND * math.cos(mouth_x / BEND) - BEND + 0.0045
    node("Bouche", "Masque", T(mouth_x, mouth_y, mouth_z))
    bv, buv, bf = bent_quad(mw * px, mh * px, BEND, mouth_x)
    add(textured(bv, buv, bf, thicken_line(mouth_img), name="Bouche_Dessin", alpha_mode="BLEND"),
        "Bouche_Dessin", "Bouche")

    # --- Bras longs (style locomotion aux bras), en pose "A"
    UPPER, FORE = 0.25, 0.25
    for side, sx in (("G", 1), ("D", -1)):
        shoulder = "Epaule_" + side
        node(shoulder, "LeDessine", T(0.19 * sx, 0.66, 0) @ R(38 * sx, Z) @ R(-8, X))
        add(ellipsoid(0.055, 0.055, 0.055, LILAS, lump=0.002, seed=11), "Epaule_Boule_" + side, shoulder)
        for k in range(8):  # couture d'épaule
            a = math.radians(k * 45)
            add(running_stitch(0.012, 0.0035), "Epaule_Point_%s_%d" % (side, k), shoulder,
                T(0.047 * math.cos(a), -0.028, 0.047 * math.sin(a)) @ R(-math.degrees(a), Y) @ R(90, X))
        add(capsule_y(UPPER, 0.042, LILAS, lump=0.002, seed=12), "BrasHaut_" + side, shoulder)

        elbow = "Coude_" + side
        node(elbow, shoulder, T(0, -UPPER, 0) @ R(-12 * sx, Z) @ R(-14, X))
        add(capsule_y(FORE, 0.038, LILAS, lump=0.002, seed=13), "AvantBras_" + side, elbow)

        hand = "Main_" + side
        node(hand, elbow, T(0, -FORE - 0.02, 0))
        add(ellipsoid(0.048, 0.05, 0.027, PECHE, lump=0.0015, seed=14, texel=0.04), "Paume_" + side, hand, T(0, -0.045, 0))

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
            add(capsule_y(l1, 0.0115, PECHE, texel=0.03), j1 + "_Mesh", j1)
            node(j2, j1, T(0, -l1 - 0.004, 0))
            add(capsule_y(l2, 0.0105, PECHE, texel=0.03), j2 + "_Mesh", j2)

        t1 = "Doigt_Pouce_1_" + side
        t2 = "Doigt_Pouce_2_" + side
        node(t1, hand, T(0.040 * sx, -0.038, 0.012) @ R(38 * sx, Z) @ R(-18, X))
        add(capsule_y(0.028, 0.012, PECHE, texel=0.03), t1 + "_Mesh", t1)
        node(t2, t1, T(0, -0.032, 0))
        add(capsule_y(0.024, 0.011, PECHE, texel=0.03), t2 + "_Mesh", t2)

    # Pièce rapiécée menthe sur le bras droit, avec ses points
    add(box(0.07, 0.07, 0.008, MENTHE), "Piece_Rapiecee", "Epaule_D", T(0, -0.13, 0.04) @ R(8, Z))
    for k, (dx, dy) in enumerate([(-0.03, 0.03), (0.03, 0.03), (-0.03, -0.03), (0.03, -0.03)]):
        add(cross_stitch(0.012, 0.003), "Piece_Point_%d" % k, "Epaule_D",
            T(dx, -0.13 + dy, 0.0455) @ R(8, Z))

    return scene


if __name__ == "__main__":
    scene = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    scene.export(OUT)
    tx.dedupe_glb(OUT)
    tris = sum(len(g.faces) for g in scene.geometry.values())
    print("OK ->", OUT, "| triangles :", tris, "| taille :", os.path.getsize(OUT) // 1024, "Ko")
