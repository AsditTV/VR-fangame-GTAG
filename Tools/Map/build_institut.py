"""
Génère une MAQUETTE 3D de l'Institut Marivaux (pour visualiser l'agencement et l'ambiance).
Ce n'est pas la map finale : les vrais modèles viendront de packs d'assets. Mais l'agencement,
les ouvertures, les hauteurs et les objets qui racontent le lore sont ceux du design.

Zones : Hall (départ), Couloir des chambres, École + dortoir aux 23 lits, le Standard,
escalier vers le sous-sol, Salle des machines, Piscine d'encre.

Usage (depuis la racine du dépôt) : python3 Tools/Map/build_institut.py
Sorties :
  Assets/Horror/Art/Map/Institut_Maquette.glb   (géométrie, un noeud par zone et par matière)
  Assets/Horror/Art/Map/Institut_Maquette.json  (lumières de nuit, étiquettes, repères de lore)
"""
import json
import math
import os
import sys

import numpy as np
import trimesh
from PIL import Image
from trimesh.visual import TextureVisuals
from trimesh.visual.material import PBRMaterial

HERE = os.path.dirname(__file__)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "Character"))
import map_textures as mt  # noqa: E402
import textures as tx  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_GLB = os.path.join(ROOT, "Assets/Horror/Art/Map/Institut_Maquette.glb")
OUT_JSON = os.path.join(ROOT, "Assets/Horror/Art/Map/Institut_Maquette.json")
FACE_DIR = os.path.join(ROOT, "Assets/Horror/Art/Face")

WALL = 0.15   # épaisseur d'un mur (chaque pièce a ses propres murs, côté intérieur)


# ---------------------------------------------------------------- matières
def pbr(albedo=None, normal=None, color=(1.0, 1.0, 1.0, 1.0), rough=0.9, metal=0.0, emissive=None,
        alpha=None, double=False):
    # floats obligatoires : trimesh lit des entiers comme des valeurs 0-255 (1 -> presque noir)
    kw = dict(baseColorFactor=[float(c) for c in color], roughnessFactor=rough, metallicFactor=metal, doubleSided=double)
    if albedo is not None:
        kw["baseColorTexture"] = albedo
    if normal is not None:
        kw["normalTexture"] = normal
    if emissive is not None:
        kw["emissiveFactor"] = list(emissive)
    if alpha:
        kw["alphaMode"] = alpha
        kw["alphaCutoff"] = 0.5 if alpha == "MASK" else None
    return PBRMaterial(**kw)


def hexc(h, a=1.0):
    h = h.lstrip("#")
    return (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)


def make_materials():
    M = {}
    M["mur_lilas"] = (pbr(*mt.wallpaper("#CDBFD9", "#E6DCE8", "#A99BC0", seed=1, kind="lune")), 1.6)
    M["mur_menthe"] = (pbr(*mt.wallpaper("#BFD9CC", "#E2EEE6", "#8FB5A6", seed=2, kind="etoile")), 1.4)
    M["mur_bleu"] = (pbr(*mt.wallpaper("#C6D3E3", "#E6ECF2", "#9AAACB", seed=3, kind="lune")), 1.6)
    M["mur_vert_sombre"] = (pbr(*mt.wallpaper("#4E6158", "#56695F", "#6F8579", seed=4, kind="etoile")), 1.2)
    M["sol_damier"] = (pbr(*mt.checker("#E3B9C1", "#EFE6DA")), 1.6)
    M["parquet"] = (pbr(*mt.wood("#A08272")), 1.8)
    M["moquette_rose"] = (pbr(*mt.fabric_colored("#B98C96", seed=8)), 0.6)
    M["beton"] = (pbr(*mt.concrete(), rough=0.95), 2.0)
    M["carrelage_piscine"] = (pbr(*mt.small_tiles("#A9D2C6")), 1.2)
    M["carrelage_creme"] = (pbr(*mt.small_tiles("#E8E0CC", seed=9)), 1.2)
    M["rouille"] = (pbr(*mt.rust(), rough=0.7, metal=0.4), 1.0)
    M["tissu_creme"] = (pbr(*mt.fabric_colored("#E8E0D2", seed=10)), 0.4)
    M["tissu_rose"] = (pbr(*mt.fabric_colored("#D9A9B2", seed=11)), 0.4)
    M["tissu_menthe"] = (pbr(*mt.fabric_colored("#A9CDBD", seed=12)), 0.4)
    M["lino_sombre"] = (pbr(*mt.checker("#3E4440", "#56574F", tiles=6, seed=13)), 1.4)
    M["plafond"] = (pbr(color=hexc("#D9D1CC"), rough=1.0), None)
    M["peinture_blanche"] = (pbr(color=hexc("#E9E4DA"), rough=0.6), None)
    M["bois_peint_rose"] = (pbr(color=hexc("#D6A3AE"), rough=0.7), None)
    M["bois_peint_bleu"] = (pbr(color=hexc("#9FB3CF"), rough=0.7), None)
    M["bois_sombre"] = (pbr(color=hexc("#5A463F"), rough=0.6), None)
    M["laiton"] = (pbr(color=hexc("#B69A62"), rough=0.35, metal=0.9), None)
    M["metal_gris"] = (pbr(color=hexc("#8E9497"), rough=0.4, metal=0.8), None)
    M["noir_cable"] = (pbr(color=hexc("#211C25"), rough=0.6), None)
    M["encre"] = (pbr(color=hexc("#0D0A12"), rough=0.04, metal=0.0), None)
    M["papier"] = (pbr(color=hexc("#EEE7D6"), rough=0.95), None)
    M["jaune_bouee"] = (pbr(color=hexc("#E9CF6A"), rough=0.5), None)
    M["minitel"] = (pbr(color=hexc("#D9CDB4"), rough=0.6), None)
    M["lum_rose"] = (pbr(color=hexc("#F2B8C6"), emissive=(0.95, 0.55, 0.65)), None)
    M["lum_menthe"] = (pbr(color=hexc("#B7EAD4"), emissive=(0.5, 0.9, 0.7)), None)
    M["lum_orange"] = (pbr(color=hexc("#F2B37A"), emissive=(1.0, 0.55, 0.2)), None)
    M["lum_chaud"] = (pbr(color=hexc("#F7E3B5"), emissive=(1.0, 0.85, 0.55)), None)
    M["ecran_minitel"] = (pbr(color=hexc("#1D3B2E"), emissive=(0.25, 0.75, 0.45), rough=0.2), None)
    return M


# ---------------------------------------------------------------- constructeur
class Builder:
    def __init__(self, materials):
        self.M = materials
        self.parts = {}       # (zone, matière) -> [verts, faces, normals, uvs]
        self.unique = []      # maillages avec leur propre texture (panneaux, portrait...)

    def _push(self, zone, key, mesh, uv):
        p = self.parts.setdefault((zone, key), [[], [], [], [], 0])
        p[0].append(mesh.vertices)
        p[1].append(mesh.faces + p[4])
        p[2].append(mesh.vertex_normals)
        p[3].append(uv)
        p[4] += len(mesh.vertices)

    def add_mesh(self, zone, key, mesh, flat=False):
        """Ajoute un maillage (UV projetées en coordonnées monde, pour que les textures se suivent
        d'un mur à l'autre). flat=True : arêtes nettes (murs, meubles), sinon normales lisses."""
        texel = self.M[key][1] or 1.0
        out, uv = tx.box_uv(mesh, texel)
        if flat:
            fn = np.repeat(mesh.face_normals, 3, axis=0)
            out = trimesh.Trimesh(vertices=out.vertices, faces=out.faces, vertex_normals=fn, process=False)
        self._push(zone, key, out, uv)

    def box(self, zone, key, x0, x1, y0, y1, z0, z1, rot=None):
        m = trimesh.creation.box(extents=[x1 - x0, y1 - y0, z1 - z0])
        if rot is not None:
            m.apply_transform(rot)
        m.apply_translation([(x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2])
        self.add_mesh(zone, key, m, flat=True)

    def boxc(self, zone, key, center, size, rot=None):
        cx, cy, cz = center
        sx, sy, sz = size
        self.box(zone, key, cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2, cz - sz / 2, cz + sz / 2, rot)

    def cyl(self, zone, key, p0, p1, r, sections=14):
        m = trimesh.creation.cylinder(radius=r, segment=[p0, p1], sections=sections)
        self.add_mesh(zone, key, m)

    def sphere(self, zone, key, c, r, scale=(1, 1, 1), subdiv=1):
        m = trimesh.creation.icosphere(subdivisions=subdiv, radius=r)
        m.apply_transform(np.diag([scale[0], scale[1], scale[2], 1]))
        m.apply_translation(c)
        self.add_mesh(zone, key, m)

    def quad(self, zone, name, image, center, w, h, facing, alpha=None, rough=0.8, uv=(0, 0, 1, 1), emissive=None):
        """Panneau plat texturé (portrait, tableau, panneau...). facing = direction vers laquelle il regarde."""
        f = np.array(facing, float)
        f /= np.linalg.norm(f)
        up = np.array([0, 1, 0.0]) if abs(f[1]) < 0.9 else np.array([0, 0, -1.0])
        right = np.cross(up, f)
        right /= np.linalg.norm(right)
        up = np.cross(f, right)
        c = np.array(center, float)
        v = np.array([c - right * w / 2 - up * h / 2, c + right * w / 2 - up * h / 2,
                      c + right * w / 2 + up * h / 2, c - right * w / 2 + up * h / 2])
        u0, v0, u1, v1 = uv
        # uv donnés en coordonnées image (v=0 en haut) ; trimesh attend v=0 en bas (il retourne à l'export)
        uvs = np.array([[u0, 1 - v1], [u1, 1 - v1], [u1, 1 - v0], [u0, 1 - v0]])
        m = trimesh.Trimesh(vertices=v, faces=[[0, 1, 2], [0, 2, 3]], vertex_normals=[f] * 4, process=False)
        mat = pbr(albedo=image, rough=rough, alpha=alpha, double=True, emissive=emissive)
        m.visual = TextureVisuals(uv=uvs, material=mat)
        self.unique.append((zone, name, m))

    # -------------------------------------------------------- pièces
    def room(self, zone, x0, x1, z0, z1, y0, height, wall, floor, openings=(), ceiling=True):
        """Pièce : sol, plafond (noeud Plafonds), 4 murs avec ouvertures.
        openings : (côté 'N','S','E','O', début, fin, hauteur) ; N = z1, S = z0, E = x1, O = x0."""
        y1 = y0 + height
        if floor:
            self.box(zone, floor, x0, x1, y0 - 0.1, y0, z0, z1)
        if ceiling:
            self.box("Plafonds", "plafond", x0, x1, y1, y1 + 0.1, z0, z1)
        for side in "NSEO":
            ops = sorted([(a, b, h) for (s, a, b, h) in openings if s == side])
            if side in "NS":
                lo, hi = x0, x1
                zz = (z1 - WALL, z1) if side == "N" else (z0, z0 + WALL)
            else:
                lo, hi = z0, z1
                xx = (x1 - WALL, x1) if side == "E" else (x0, x0 + WALL)
            cursor = lo
            for a, b, h in ops + [(hi, hi, 0)]:
                if a > cursor:
                    self._wall_seg(zone, wall, side, cursor, a, y0, y1, zz if side in "NS" else xx)
                if h > 0 and b > a:   # linteau au-dessus de l'ouverture
                    self._wall_seg(zone, wall, side, a, b, y0 + h, y1, zz if side in "NS" else xx)
                cursor = max(cursor, b)

    def _wall_seg(self, zone, key, side, a, b, y0, y1, thick):
        if side in "NS":
            self.box(zone, key, a, b, y0, y1, thick[0], thick[1])
        else:
            self.box(zone, key, thick[0], thick[1], y0, y1, a, b)

    # -------------------------------------------------------- export
    def scene(self):
        sc = trimesh.Scene()
        sc.graph.update(frame_from=sc.graph.base_frame, frame_to="Institut", matrix=np.eye(4))
        zones = sorted({z for z, _ in self.parts} | {z for z, _, _ in self.unique})
        for z in zones:
            sc.graph.update(frame_from="Institut", frame_to=z, matrix=np.eye(4))
        for (zone, key), (V, F, N, U, _) in self.parts.items():
            m = trimesh.Trimesh(vertices=np.vstack(V), faces=np.vstack(F), vertex_normals=np.vstack(N), process=False)
            m.visual = TextureVisuals(uv=np.vstack(U), material=self.M[key][0])
            name = "%s_%s" % (zone, key)
            sc.add_geometry(m, node_name=name, geom_name=name, parent_node_name=zone)
        for zone, name, m in self.unique:
            sc.add_geometry(m, node_name=zone + "_" + name, geom_name=zone + "_" + name, parent_node_name=zone)
        return sc


# ---------------------------------------------------------------- l'Institut
def build():
    M = make_materials()
    B = Builder(M)
    lights, labels, lore = [], [], []

    def light(pos, color, intensity, distance):
        lights.append({"pos": list(map(float, pos)), "color": color, "intensity": intensity, "distance": distance})

    def mark(title, text, pos, cam):
        lore.append({"titre": title, "texte": text, "pos": list(map(float, pos)), "cam": list(map(float, cam))})

    # ============================ HALL (départ) ============================
    Z = "Hall"
    B.room(Z, -7, 7, -6, 6, 0, 7, "mur_lilas", "sol_damier",
           openings=[("E", -1.2, 1.2, 3), ("N", -1.2, 1.2, 3), ("O", -1.2, 1.2, 3)])
    labels.append({"nom": "Hall", "pos": [0, 3.5, 0]})
    # grandes portes d'entrée condamnées (chaîne)
    B.box(Z, "bois_sombre", -1.6, 1.6, 0, 3.6, -5.9, -5.8)
    B.cyl(Z, "metal_gris", [-1.4, 1.5, -5.75], [1.4, 1.9, -5.75], 0.04)
    B.cyl(Z, "metal_gris", [-1.4, 1.9, -5.75], [1.4, 1.5, -5.75], 0.04)
    # colonnes menthe
    for x in (-4.5, 4.5):
        for z in (-3, 3):
            B.cyl(Z, "carrelage_piscine", [x, 0, z], [x, 7, z], 0.35, 16)
    # tapis central + canapés pastel
    B.box(Z, "moquette_rose", -3, 3, 0, 0.02, -2.5, 2.5)
    for x, rot in ((-2.6, 0), (2.6, 0)):
        B.box(Z, "tissu_rose", x - 0.5, x + 0.5, 0, 0.45, -1.2, 1.2)
        B.box(Z, "tissu_rose", x - 0.5 + (0.75 if x > 0 else 0), x - 0.25 + (0.75 if x > 0 else 0), 0.45, 1.0, -1.2, 1.2)
    # comptoir d'accueil + Minitel + sonnette
    B.box(Z, "bois_peint_rose", -6.0, -3.0, 0, 1.1, 2.6, 3.4)
    B.box(Z, "bois_sombre", -6.1, -2.9, 1.1, 1.16, 2.5, 3.5)
    B.boxc(Z, "minitel", (-4.2, 1.36, 3.0), (0.42, 0.4, 0.38))
    B.quad(Z, "Ecran_Minitel", _flat((40, 120, 80)), (-4.2, 1.4, 2.805), 0.3, 0.22, (0, 0, -1),
           emissive=(0.2, 0.7, 0.4))
    B.boxc(Z, "minitel", (-4.2, 1.18, 2.75), (0.46, 0.04, 0.22))     # clavier
    B.sphere(Z, "laiton", (-3.5, 1.2, 3.0), 0.07, scale=(1, 0.6, 1))
    light((-4.2, 1.5, 2.6), "#7fe0a8", 0.8, 3)
    # portrait aux yeux grattés + panneau cloué
    B.quad(Z, "Portrait", mt.portrait_scratched(), (-4.0, 4.3, 5.82), 2.0, 2.6, (0, 0, -1))
    B.quad(Z, "Panneau_On_Se_Tait", mt.happy_sign_covered(), (4.0, 3.2, 5.82), 2.4, 0.9, (0, 0, -1))
    # lustre éteint (une seule bougie électrique allumée)
    B.cyl(Z, "metal_gris", [0, 7, 0], [0, 5.6, 0], 0.02)
    m = trimesh.creation.torus(major_radius=0.9, minor_radius=0.05, major_sections=24, minor_sections=6)
    m.apply_translation([0, 5.5, 0])
    B.add_mesh(Z, "laiton", m)
    for k in range(8):
        a = k / 8 * math.tau
        B.sphere(Z, "lum_chaud" if k == 3 else "peinture_blanche", (0.9 * math.cos(a), 5.62, 0.9 * math.sin(a)), 0.07)
    light((0.9 * math.cos(3 / 8 * math.tau), 5.5, 0.9 * math.sin(3 / 8 * math.tau)), "#ffd9a0", 0.8, 10)
    # plantes mortes
    for x, z in ((-6.2, -5.2), (6.2, -5.2)):
        B.cyl(Z, "bois_peint_bleu", [x, 0, z], [x, 0.6, z], 0.3)
        for k in range(5):
            a = k / 5 * math.tau
            B.cyl(Z, "bois_sombre", [x, 0.6, z], [x + 0.35 * math.cos(a), 1.6 + 0.2 * k % 3, z + 0.35 * math.sin(a)], 0.02, 6)
    mark("Le portrait aux yeux grattés", "Céleste Marivaux, peinte en pastel. Quelqu'un a gratté ses yeux au couteau.",
         (-4.0, 4.3, 5.8), (-2.5, 2.2, 1.5))
    mark("« Ici, on se tait. »", "L'ancien panneau joyeux de l'hôtel, recouvert par celui de l'institut, cloué de travers.",
         (4.0, 3.2, 5.8), (2.5, 2.0, 1.5))
    mark("Le Minitel de l'accueil", "L'ordinateur des réglages : salles privées, micro, couleur. Son écran vert est la seule lumière du hall.",
         (-4.2, 1.4, 3.0), (-3.2, 1.9, 1.2))

    # ============================ COULOIR DES CHAMBRES ============================
    Z = "Couloir"
    B.room(Z, 7, 31, -1.5, 1.5, 0, 3.2, "mur_menthe", "parquet",
           openings=[("O", -1.2, 1.2, 3), ("E", -0.5, 0.5, 2.2)])
    labels.append({"nom": "Couloir des chambres", "pos": [19, 2.5, 0]})
    B.box(Z, "moquette_rose", 7.2, 30.8, 0, 0.015, -0.7, 0.7)
    numbers = list(range(1, 13))
    plates, cols, rows = mt.number_atlas(numbers)
    chalk, ccols, crows = mt.number_atlas(numbers, chalk=True)
    for i, n in enumerate(numbers):
        side = 1 if i % 2 == 0 else -1
        x = 9.5 + (i // 2) * 3.6
        zw = 1.5 * side - 0.16 * side
        B.box(Z, "bois_peint_rose" if i % 3 else "bois_peint_bleu", x - 0.5, x + 0.5, 0, 2.2,
              min(zw, zw + 0.06 * side), max(zw, zw + 0.06 * side))
        B.sphere(Z, "laiton", (x + 0.35, 1.05, zw - 0.05 * side), 0.04)
        cu, cv = (i % cols) / cols, (i // cols) / rows
        B.quad(Z, "Plaque_%d" % n, plates, (x, 1.85, zw - 0.02 * side), 0.32, 0.32, (0, 0, -side), alpha="MASK",
               uv=(cu, cv, cu + 1 / cols, cv + 1 / rows))
        # panneau à boutons du Protocole Verrou
        B.boxc(Z, "metal_gris", (x + 0.8, 1.3, zw - 0.03 * side), (0.16, 0.24, 0.04))
        B.sphere(Z, "lum_rose", (x + 0.8, 1.33, zw - 0.06 * side), 0.03)
        # numéro à la craie au sol, de plus en plus tremblé
        B.quad(Z, "Craie_%d" % n, chalk, (x, 0.02, 0.85 * side), 0.45, 0.45, (0, 1, 0), alpha="BLEND",
               uv=(cu, cv, cu + 1 / ccols, cv + 1 / crows))
        if i % 4 == 0:
            light((x + 0.8, 1.35, zw - 0.2 * side), "#f2a7b9", 0.5, 2.2)
    # porte 23 entrouverte au fond : derrière, le noir
    B.box(Z, "bois_peint_bleu", 30.75, 30.8, 0, 2.2, 0.5, 1.3, rot=None)
    B.box(Z, "noir_cable", 30.9, 31.2, 0, 2.2, -0.5, 0.5)
    B.quad(Z, "Plaque_23", mt.number_atlas([23])[0], (30.83, 1.85, 0.9), 0.3, 0.3, (-1, 0, 0), alpha="MASK", uv=(0, 0, 0.25, 1))
    mark("Les numéros à la craie", "Le veilleur comptait les portes. Plus on avance, plus les chiffres tremblent.",
         (20, 0.05, 0.8), (17, 2.2, -0.6))
    mark("La porte 23", "Au bout du couloir, une porte entrouverte. Il n'y avait que 22 chambres à l'hôtel.",
         (30.8, 1.2, 0), (27.5, 1.7, 0))

    # ============================ ÉCOLE / DORTOIR ============================
    Z = "Ecole"
    B.room(Z, -1.5, 1.5, 6, 12, 0, 3.2, "mur_bleu", "parquet", openings=[("S", -1.2, 1.2, 3), ("N", -1.2, 1.2, 3)])
    B.room(Z, -8, 8, 12, 30, 0, 4, "mur_bleu", "parquet", openings=[("S", -1.2, 1.2, 3)])
    labels.append({"nom": "Dortoir aux 23 lits", "pos": [0, 3, 21]})
    faces = [Image.open(os.path.join(FACE_DIR, f)).convert("RGBA") for f in
             ("face.png", "Boil/face_parle_0.png", "Boil/face_parle_1.png", "Boil/face_parle_2.png")]
    empty_bed = 5
    bed = 0
    for side in (-1, 1):
        for k in range(12):
            if side == 1 and k == 11:
                continue  # 23 lits : 12 d'un côté, 11 de l'autre
            x = side * 6.3
            z = 13.6 + k * 1.35
            B.box(Z, "peinture_blanche", x - 0.95, x + 0.95, 0.25, 0.32, z - 0.45, z + 0.45)     # sommier
            for dx in (-0.95, 0.95):
                for dz in (-0.45, 0.45):
                    B.cyl(Z, "peinture_blanche", [x + dx, 0, z + dz], [x + dx, 0.45 if dx * side > 0 else 0.8, z + dz], 0.03, 8)
            B.box(Z, "tissu_creme", x - 0.92, x + 0.92, 0.32, 0.5, z - 0.42, z + 0.42)            # matelas
            px = x + side * 0.65
            B.box(Z, "tissu_creme" if bed % 3 else "tissu_menthe", px - 0.2, px + 0.2, 0.5, 0.62, z - 0.3, z + 0.3)  # oreiller
            if bed != empty_bed:
                B.quad(Z, "Masque_Lit_%d" % bed, faces[bed % 4], (px, 0.64, z), 0.5, 0.37, (0, 1, 0), alpha="MASK")
            else:
                light((x, 2.2, z), "#c9d8ff", 0.9, 3.5)  # un rayon de lune sur LE lit sans masque
                mark("Le lit sans masque", "23 lits, un masque en papier sur chaque oreiller. Celui-ci n'en a pas : c'est le tien.",
                     (x, 0.6, z), (x - side * 2.5, 2.2, z - 1.5))
            if k % 3 == 1:   # veilleuse en forme de lune entre deux lits
                B.sphere(Z, "lum_rose", (x - side * 0.2, 0.75, z + 0.68), 0.09, scale=(1, 1, 0.5))
                if k % 6 == 1:
                    light((x - side * 0.4, 0.9, z + 0.68), "#f4b6c8", 0.7, 3.2)
            bed += 1
    B.quad(Z, "Tableau", mt.chalkboard(), (0, 2.1, 29.83), 6.0, 2.5, (0, 0, -1))
    B.quad(Z, "Toise", mt.growth_chart(), (-7.83, 1.2, 15.0), 0.4, 1.6, (1, 0, 0))
    for k, x in enumerate((-2.5, 0, 2.5)):  # pupitres
        B.box(Z, "bois_peint_bleu", x - 0.5, x + 0.5, 0.7, 0.76, 26.5, 27.2)
        for dx in (-0.45, 0.45):
            B.cyl(Z, "metal_gris", [x + dx, 0, 26.6], [x + dx, 0.7, 26.6], 0.025, 6)
            B.cyl(Z, "metal_gris", [x + dx, 0, 27.1], [x + dx, 0.7, 27.1], 0.025, 6)
        B.box(Z, "papier", x - 0.15, x + 0.15, 0.765, 0.77, 26.7, 27.1)
    mark("« Je ne crierai plus. »", "La même phrase recopiée en punition, ligne après ligne. La dernière s'arrête au milieu d'un mot.",
         (0, 2.1, 29.8), (0, 2.0, 24.5))
    mark("La toise", "Les tailles des enfants. Toutes les mesures s'arrêtent en novembre 1985.",
         (-7.8, 1.2, 15), (-5.0, 1.6, 15.5))

    # ============================ LE STANDARD ============================
    Z = "Standard"
    B.room(Z, -12, -7, -1.5, 1.5, 0, 3, "mur_vert_sombre", "lino_sombre", openings=[("E", -1.2, 1.2, 2.6), ("O", -1.2, 1.2, 2.6)])
    B.room(Z, -20, -12, -5, 5, 0, 3.5, "mur_vert_sombre", "lino_sombre",
           openings=[("E", -1.2, 1.2, 2.6), ("S", -17.2, -14.8, 2.6)])
    labels.append({"nom": "Le Standard", "pos": [-16, 3, 0]})
    B.box(Z, "bois_sombre", -19.85, -19.5, 0, 2.6, -3.5, 3.5)                              # panneau du standard
    B.quad(Z, "Etiquettes", mt.socket_labels(), (-19.49, 2.3, 0), 6.6, 0.6, (1, 0, 0))
    rng = np.random.default_rng(3)
    for i in range(12):
        for j in range(5):
            zz = -3.1 + i * 0.56
            yy = 0.8 + j * 0.28
            B.cyl(Z, "laiton", [-19.5, yy, zz], [-19.44, yy, zz], 0.035, 8)
            if rng.random() < 0.3:   # câbles branchés qui pendent en U
                z2 = zz + rng.uniform(-1.2, 1.2)
                mid = (zz + z2) / 2
                B.cyl(Z, "noir_cable", [-19.44, yy, zz], [-19.3, yy - 0.5, mid], 0.012, 5)
                B.cyl(Z, "noir_cable", [-19.3, yy - 0.5, mid], [-19.44, yy - 0.05, z2], 0.012, 5)
    B.box(Z, "bois_sombre", -18.6, -17.4, 0.75, 0.8, -0.8, 0.8)                               # bureau
    for dx in (-0.55, 0.55):
        for dz in (-0.7, 0.7):
            B.cyl(Z, "bois_sombre", [-18 + dx, 0, dz], [-18 + dx, 0.75, dz], 0.03, 6)
    B.box(Z, "tissu_rose", -17.2, -16.7, 0.45, 0.52, -0.25, 0.25)                             # chaise
    B.box(Z, "tissu_rose", -16.75, -16.68, 0.5, 1.1, -0.25, 0.25)
    m = trimesh.creation.torus(major_radius=0.1, minor_radius=0.012, major_sections=16, minor_sections=5)
    m.apply_transform(trimesh.transformations.rotation_matrix(math.pi / 2, [0, 0, 1]))
    m.apply_translation([-16.95, 0.62, 0])
    B.add_mesh(Z, "noir_cable", m)                                                              # casque posé sur la chaise
    for dz in (-0.1, 0.1):
        B.cyl(Z, "noir_cable", [-16.95, 0.56, dz], [-16.95, 0.62, dz], 0.045, 10)
    B.cyl(Z, "laiton", [-18.2, 0.8, 0.5], [-18.2, 1.25, 0.5], 0.015, 6)                       # lampe de bureau
    B.sphere(Z, "lum_chaud", (-18.1, 1.25, 0.5), 0.08, scale=(1.4, 0.7, 1.4))
    light((-18.0, 1.3, 0.5), "#ffd49a", 1.2, 5)
    light((-19.2, 1.5, 0), "#e6c47a", 0.5, 3)
    mark("Le Standard", "Un micro par pièce de l'institut, tous reliés ici. C'est comme ça qu'elle entend tout.",
         (-19.5, 1.5, 0), (-15.5, 2.0, 1.5))
    mark("Le casque sur la chaise", "Son casque d'écoute est encore posé là. Il grésille.",
         (-16.95, 0.6, 0), (-15.8, 1.6, -1.4))

    # ============================ ESCALIER VERS LE SOUS-SOL ============================
    Z = "Escalier"
    B.room(Z, -17.5, -14.5, -10, -5, -3, 6.2, "beton", None, openings=[("N", -17.2, -14.8, 5.6), ("S", -17.2, -14.8, 3)],
           ceiling=False)
    ang = math.atan2(3, 5)
    rot = trimesh.transformations.rotation_matrix(-ang, [1, 0, 0])
    B.boxc(Z, "beton", (-16, -1.55, -7.5), (2.7, 0.2, math.hypot(3, 5)), rot=rot)
    for k in range(10):    # marches visibles
        zz = -5.25 - k * 0.5
        yy = -0.15 - k * 0.3
        B.box(Z, "metal_gris", -17.3, -14.7, yy, yy + 0.04, zz - 0.25, zz + 0.25)

    # ============================ SALLE DES MACHINES ============================
    Z = "Machines"
    B.room(Z, -24, -8, -22, -10, -3, 4, "beton", "beton", openings=[("N", -17.2, -14.8, 3), ("E", -17, -14, 3)])
    labels.append({"nom": "Salle des machines (sous-sol)", "pos": [-16, 0.5, -16]})
    for x, z in ((-21, -13.5), (-21, -18.5)):    # chaudières
        B.cyl(Z, "rouille", [x, -3, z], [x, 0.2, z], 1.2, 20)
        B.cyl(Z, "rouille", [x, 0.2, z], [x, 0.9, z], 0.25, 10)
        B.sphere(Z, "lum_orange", (x + 1.18, -1.6, z), 0.07)
        light((x + 1.5, -1.6, z), "#ff9a50", 0.9, 4)
    B.boxc(Z, "rouille", (-12, -2.4, -19.5), (2.4, 1.2, 1.6))       # pompe de la piscine
    B.cyl(Z, "rouille", [-12, -1.8, -19.5], [-12, -1.0, -19.5], 0.5, 16)
    B.quad(Z, "Plaque_Pompe", mt.pump_plate(), (-12, -2.3, -18.69), 0.8, 0.5, (0, 0, 1))
    for k, y in enumerate((-0.4, 0.1, 0.5)):   # tuyaux au plafond, dont ceux d'encre
        key = "encre" if k == 1 else "rouille"
        B.cyl(Z, key, [-23.5, y, -11 - k * 0.5], [-8.2, y, -11 - k * 0.5], 0.16 if k == 1 else 0.12, 12)
    B.cyl(Z, "encre", [-12, -1.0, -19.5], [-12, -0.1, -19.5], 0.14, 10)
    B.cyl(Z, "encre", [-12, -0.1, -19.5], [-8.2, -0.1, -16], 0.14, 10)
    B.box(Z, "metal_gris", -23.8, -9, -1.0, -0.92, -21.8, -20.6)     # passerelle
    for x in np.arange(-23.5, -9, 1.5):
        B.cyl(Z, "metal_gris", [x, -0.92, -20.6], [x, -0.1, -20.6], 0.025, 6)
        B.cyl(Z, "metal_gris", [x, -3, -21.2], [x, -1.0, -21.2], 0.04, 6)
    B.cyl(Z, "metal_gris", [-23.5, -0.15, -20.6], [-9, -0.15, -20.6], 0.025, 6)
    for x, z, r in ((-15, -15, 0.9), (-10.5, -13, 0.6), (-18, -20, 0.5)):   # flaques d'encre
        B.sphere(Z, "encre", (x, -2.995, z), r, scale=(1, 0.01, 0.7))
    mark("La pompe n°2", "« Remplissage spécial, 12/11/1985 ». La veille de la Nuit Longue. Les tuyaux noirs partent vers la piscine.",
         (-12, -2.3, -18.7), (-13.5, -1.4, -15.5))

    # ============================ PISCINE D'ENCRE ============================
    Z = "Piscine"
    B.room(Z, -8, 12, -26, -8, -3, 6, "carrelage_creme", None, openings=[("O", -17, -14, 3)])
    labels.append({"nom": "Piscine d'encre (sous-sol)", "pos": [2, 1.5, -17]})
    bx0, bx1, bz0, bz1 = -4, 8, -22, -12
    for (x0, x1, z0, z1) in ((-8, 12, -26, bz0), (-8, 12, bz1, -8), (-8, bx0, bz0, bz1), (bx1, 12, bz0, bz1)):
        B.box(Z, "carrelage_piscine", x0, x1, -3.1, -3, z0, z1)
    B.box(Z, "carrelage_piscine", bx0, bx1, -5.2, -5.1, bz0, bz1)               # fond
    for (x0, x1, z0, z1) in ((bx0, bx1, bz0, bz0 + 0.1), (bx0, bx1, bz1 - 0.1, bz1), (bx0, bx0 + 0.1, bz0, bz1), (bx1 - 0.1, bx1, bz0, bz1)):
        B.box(Z, "carrelage_piscine", x0, x1, -5.1, -3.0, z0, z1)
    B.box(Z, "encre", bx0 + 0.1, bx1 - 0.1, -3.42, -3.4, bz0 + 0.1, bz1 - 0.1)   # surface d'encre
    B.box(Z, "peinture_blanche", 7.0, 9.6, -2.55, -2.45, -17.4, -16.6)          # plongeoir
    B.box(Z, "metal_gris", 9.2, 9.6, -3, -2.55, -17.3, -16.7)
    for k in range(4):   # gradins
        B.box(Z, "bois_peint_bleu", -7.8, 11.8, -3, -2.6 + k * 0.45, -9.0 - (3 - k) * 0.7, -8.2 - (3 - k) * 0.7)
    B.boxc(Z, "noir_cable", (5.5, -1.45, -9.3), (0.7, 0.5, 0.45),
           rot=trimesh.transformations.rotation_matrix(1.2, [0, 0, 1]))            # enceinte renversée
    for k in range(18):  # guirlande de fanions pastel
        x = -7 + k * 1.05
        sag = -0.5 * math.sin(k / 17 * math.pi)
        key = ("tissu_rose", "tissu_menthe", "tissu_creme")[k % 3]
        B.boxc(Z, key, (x, 2.3 + sag, -17), (0.3, 0.35, 0.02),
               rot=trimesh.transformations.rotation_matrix(math.pi / 4, [0, 0, 1]))
    B.cyl(Z, "noir_cable", [-7.5, 2.55, -17], [11.5, 2.55, -17], 0.01, 4)
    m = trimesh.creation.torus(major_radius=0.35, minor_radius=0.12, major_sections=18, minor_sections=8)
    m.apply_transform(np.diag([1, 0.35, 1, 1]))
    m.apply_translation([-5.2, -2.98, -11.2])
    B.add_mesh(Z, "jaune_bouee", m)                                             # bouée-canard dégonflée
    B.sphere(Z, "jaune_bouee", (-5.2, -2.85, -10.75), 0.13, scale=(1, 0.8, 1))
    B.sphere(Z, "lum_orange", (-5.2, -2.86, -10.6), 0.04, scale=(1.4, 0.5, 1))
    for k in range(6):   # échelle qui descend dans l'encre au grand bain
        y = -3.2 - k * 0.3
        B.cyl(Z, "metal_gris", [7.6, y, -21.6], [7.6, y, -21.0], 0.02, 6)
    for z in (-21.6, -21.0):
        B.cyl(Z, "metal_gris", [7.6, -2.4, z], [7.6, -5.0, z], 0.03, 6)
    B.quad(Z, "Panneau_Interdit", mt.pool_sign(), (2, 0.2, -25.83), 2.6, 1.0, (0, 0, 1))
    light((2, 2.6, -17), "#9fc4d8", 0.8, 14)       # lueur froide des hautes fenêtres
    light((-5.2, -2.6, -10.8), "#ffd08a", 0.35, 2)
    light((11.2, 1.5, -8.6), "#8fe3b0", 0.5, 3)    # panneau de sortie vert
    B.boxc(Z, "lum_menthe", (11.2, 1.5, -8.18), (0.5, 0.2, 0.04))
    mark("La piscine d'encre", "Le bassin a été rempli d'encre la nuit du 13 novembre 1985. On ne voit pas le fond.",
         (2, -3.4, -17), (-2, 0.5, -10.5))
    mark("La bouée de Mila", "Une petite bouée-canard dégonflée, au bord du bassin.",
         (-5.2, -2.9, -11.2), (-4.0, -1.8, -9.6))
    mark("L'enceinte renversée", "Celle de la fête de 1977. La musique était si forte que personne n'a entendu Mila.",
         (5.5, -1.2, -9.3), (4.0, -1.0, -12.5))
    mark("L'échelle dans l'encre", "Au grand bain, une échelle descend dans l'encre... et continue. Vers l'Envers ?",
         (7.6, -3.6, -21.3), (5.0, -1.5, -19.0))

    data = {"spawn": [0, 0, -2], "lumieres": lights, "zones": labels, "lore": lore}
    return B.scene(), data


def _flat(rgb):
    return Image.new("RGB", (8, 8), rgb)


if __name__ == "__main__":
    scene, data = build()
    os.makedirs(os.path.dirname(OUT_GLB), exist_ok=True)
    scene.export(OUT_GLB)
    tx.dedupe_glb(OUT_GLB)
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    tris = sum(len(g.faces) for g in scene.geometry.values())
    print("OK ->", OUT_GLB, "| triangles :", tris, "| taille :", os.path.getsize(OUT_GLB) // 1024, "Ko",
          "| lumières :", len(data["lumieres"]), "| repères :", len(data["lore"]))
