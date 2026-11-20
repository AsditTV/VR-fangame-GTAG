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
import build_cosmetics as cos  # noqa: E402

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
    M["mur_creme"] = (pbr(*mt.wallpaper("#F1DCCB", "#F7EBDD", "#E6B9A6", seed=5, kind="etoile")), 1.4)
    M["plafond"] = (pbr(color=hexc("#D9D1CC"), rough=1.0), None)
    M["bois_moulure"] = (pbr(*mt.wood("#B9A6A0", seed=21, planks=2)), 1.2)
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


# ---------------------------------------------------------------- formes "faites main"
_wrng = np.random.default_rng(77)


def rounded_box(extents, radius, wonk=0.0, subdiv=2):
    """Boîte aux arêtes arrondies, un peu de travers (style meuble de dessin animé) au lieu d'un cube parfait.
    wonk = déformation relative (0.03 = le dessus est 3 % plus large ou décalé)."""
    ex = np.array(extents, float)
    half = ex / 2
    r = float(min(radius, half.min() * 0.95))
    m = trimesh.creation.box(extents=ex)
    for _ in range(subdiv):
        m = m.subdivide()
    v = m.vertices.copy()
    inner = np.maximum(half - r, 1e-5)
    q = np.clip(v, -inner, inner)
    d = v - q
    n = np.linalg.norm(d, axis=1, keepdims=True)
    v = q + np.where(n > 1e-9, d / np.maximum(n, 1e-9) * r, d)
    if wonk > 0:
        t = (v[:, 1] / max(half[1], 1e-5) + 1) / 2              # 0 en bas, 1 en haut
        top_scale = 1 + _wrng.uniform(-wonk, wonk * 1.5)
        shear = _wrng.uniform(-wonk, wonk, 2) * ex[1]
        v[:, 0] *= 1 + (top_scale - 1) * t
        v[:, 2] *= 1 + (top_scale - 1) * t
        v[:, 0] += shear[0] * t
        v[:, 2] += shear[1] * t
        bulge = _wrng.uniform(0, wonk * 0.6)                   # flancs légèrement bombés
        side = np.sin(np.pi * t)[:, None]
        v[:, [0, 2]] += v[:, [0, 2]] / np.maximum(half[[0, 2]], 1e-5) * bulge * min(half[0], half[2]) * side
    return trimesh.Trimesh(vertices=v, faces=m.faces, process=True)


# ---------------------------------------------------------------- constructeur
class Builder:
    def __init__(self, materials):
        self.M = materials
        self.parts = {}       # (zone, matière) -> [verts, faces, normals, uvs]
        self.unique = []      # maillages avec leur propre texture (panneaux, portrait...)
        self.rooms = []       # pièces (pour les ambiances sonores et la navigation dans Unity)
        self.doors = {}       # ouvertures entre pièces (pour les portes du Protocole Verrou)

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

    def unique_mesh(self, zone, name, key, mesh):
        """Objet à part (son propre noeud dans le fichier) : Unity peut le détacher et l'animer
        (chaises volantes...). Même matière et même projection d'UV que le décor."""
        texel = self.M[key][1] or 1.0
        out, uv = tx.box_uv(mesh, texel)
        out.visual = TextureVisuals(uv=uv, material=self.M[key][0])
        self.unique.append((zone, name, out))

    def box(self, zone, key, x0, x1, y0, y1, z0, z1, rot=None, style="prop"):
        """Boîte arrondie. style="prop" : meuble de travers et bombé ; "arch" : mur/sol presque droit ;
        "sharp" : vraie boîte nette (rare : marches, passerelles métalliques)."""
        ext = [x1 - x0, y1 - y0, z1 - z0]
        if style == "sharp":
            m = trimesh.creation.box(extents=ext)
            flat = True
        elif style == "arch":
            m = rounded_box(ext, radius=0.03, wonk=0.006, subdiv=1)
            flat = False
        else:
            m = rounded_box(ext, radius=min(0.06, 0.3 * min(ext)), wonk=0.035, subdiv=2)
            m.apply_transform(trimesh.transformations.rotation_matrix(math.radians(_wrng.uniform(-2.5, 2.5)), [0, 1, 0]))
            flat = False
        if rot is not None:
            m.apply_transform(rot)
        m.apply_translation([(x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2])
        self.add_mesh(zone, key, m, flat=flat)

    def boxc(self, zone, key, center, size, rot=None, style="prop"):
        cx, cy, cz = center
        sx, sy, sz = size
        self.box(zone, key, cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2, cz - sz / 2, cz + sz / 2, rot, style)

    def cyl(self, zone, key, p0, p1, r, sections=14):
        m = trimesh.creation.cylinder(radius=r, segment=[p0, p1], sections=sections)
        self.add_mesh(zone, key, m)

    def sphere(self, zone, key, c, r, scale=(1, 1, 1), subdiv=1):
        m = trimesh.creation.icosphere(subdivisions=subdiv, radius=r)
        m.apply_transform(np.diag([scale[0], scale[1], scale[2], 1]))
        m.apply_translation(c)
        self.add_mesh(zone, key, m)

    def quad(self, zone, name, image, center, w, h, facing, alpha=None, rough=0.8, uv=(0, 0, 1, 1), emissive=None, spin=0.0):
        """Panneau plat texturé (portrait, tableau, panneau...). facing = direction vers laquelle il regarde.
        spin = rotation dans son plan (degrés), pour poser un papier de travers."""
        f = np.array(facing, float)
        f /= np.linalg.norm(f)
        up = np.array([0, 1, 0.0]) if abs(f[1]) < 0.9 else np.array([0, 0, -1.0])
        right = np.cross(up, f)
        right /= np.linalg.norm(right)
        up = np.cross(f, right)
        if spin:
            a = math.radians(spin)
            right, up = right * math.cos(a) + up * math.sin(a), -right * math.sin(a) + up * math.cos(a)
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
    def room(self, zone, x0, x1, z0, z1, y0, height, wall, floor, openings=(), ceiling=True, nav_center=None, name=None):
        """Pièce : sol, plafond (noeud Plafonds), 4 murs avec ouvertures.
        openings : (côté 'N','S','E','O', début, fin, hauteur) ; N = z1, S = z0, E = x1, O = x0.
        nav_center : point de passage de la pièce pour le monstre (par défaut le centre au sol)."""
        y1 = y0 + height
        room_id = len(self.rooms)
        center = nav_center if nav_center is not None else ((x0 + x1) / 2, y0, (z0 + z1) / 2)
        self.rooms.append({"zone": zone, "nom": name or zone, "min": [x0, y0, z0], "max": [x1, y1, z1],
                           "centre": [float(c) for c in center], "portes": []})
        for (side, a, b, h) in openings:
            if side in "NS":
                pos, axis = ((a + b) / 2, (z1 if side == "N" else z0)), "z"
            else:
                pos, axis = ((x1 if side == "E" else x0), (a + b) / 2), "x"
            key = (round(pos[0], 1), round(pos[1], 1))
            d = self.doors.setdefault(key, {"x": float(pos[0]), "z": float(pos[1]), "axe": axis, "largeur": float(b - a),
                                            "bas": float(y0), "haut": float(y0 + h), "pieces": []})
            d["bas"] = max(d["bas"], float(y0))          # entre deux étages : la porte est au niveau le plus haut
            d["haut"] = min(d["haut"], float(y0 + h))
            d["pieces"].append(room_id)
            self.rooms[room_id]["portes"].append(key)
        if floor:
            self.box(zone, floor, x0, x1, y0 - 0.1, y0, z0, z1, style="arch")
        if ceiling:
            self.box("Plafonds", "plafond", x0, x1, y1, y1 + 0.1, z0, z1, style="arch")
        for side in "NSEO":
            ops = sorted([(a, b, h) for (s, a, b, h) in openings if s == side])
            if side in "NS":
                lo, hi = x0, x1
                zz = (z1 - WALL, z1) if side == "N" else (z0, z0 + WALL)
            else:
                lo, hi = z0, z1
                xx = (x1 - WALL, x1) if side == "E" else (x0, x0 + WALL)
            cursor = lo
            thick = zz if side in "NS" else xx
            for a, b, h in ops + [(hi, hi, 0)]:
                if a > cursor:
                    self._wall_seg(zone, wall, side, cursor, a, y0, y1, thick)
                    if floor:   # plinthe en bois le long du mur
                        self._trim(zone, side, cursor + 0.02, a - 0.02, y0, y0 + 0.13, thick, 0.035)
                if h > 0 and b > a:   # linteau au-dessus de l'ouverture + encadrement de porte
                    self._wall_seg(zone, wall, side, a, b, y0 + h, y1, thick)
                    self._trim(zone, side, a - 0.09, a + 0.01, y0, y0 + h + 0.08, thick, 0.05)
                    self._trim(zone, side, b - 0.01, b + 0.09, y0, y0 + h + 0.08, thick, 0.05)
                    self._trim(zone, side, a - 0.09, b + 0.09, y0 + h - 0.01, y0 + h + 0.1, thick, 0.05)
                cursor = max(cursor, b)

    def _wall_seg(self, zone, key, side, a, b, y0, y1, thick):
        if side in "NS":
            self.box(zone, key, a, b, y0, y1, thick[0], thick[1], style="arch")
        else:
            self.box(zone, key, thick[0], thick[1], y0, y1, a, b, style="arch")

    def _trim(self, zone, side, a, b, y0, y1, thick, depth):
        """Moulure (plinthe, chambranle) collée sur la face intérieure du mur."""
        if b - a < 0.05:
            return
        key = "bois_moulure"
        if side == "N":
            self.box(zone, key, a, b, y0, y1, thick[0] - depth, thick[0], style="arch")
        elif side == "S":
            self.box(zone, key, a, b, y0, y1, thick[1], thick[1] + depth, style="arch")
        elif side == "E":
            self.box(zone, key, thick[0] - depth, thick[0], y0, y1, a, b, style="arch")
        else:
            self.box(zone, key, thick[1], thick[1] + depth, y0, y1, a, b, style="arch")

    # -------------------------------------------------------- export
    def scene(self):
        sc = trimesh.Scene()
        sc.graph.update(frame_from=sc.graph.base_frame, frame_to="Institut", matrix=np.eye(4))
        zones = sorted({z for z, _ in self.parts} | {z for z, _, _ in self.unique})
        for z in zones:
            sc.graph.update(frame_from="Institut", frame_to=z, matrix=np.eye(4))
        for (zone, key), (V, F, N, U, _) in self.parts.items():
            V, F, N, U = np.vstack(V), np.vstack(F), np.vstack(N), np.vstack(U)
            # Fusion exacte des sommets identiques (position + normale + UV) : même rendu, fichier bien plus léger
            k = np.round(np.hstack([V * 1e4, N * 1e3, U * 1e4])).astype(np.int64)
            _, first, inverse = np.unique(k, axis=0, return_index=True, return_inverse=True)
            inverse = inverse.reshape(-1)
            V, N, U, F = V[first], N[first], U[first], inverse[F]
            m = trimesh.Trimesh(vertices=V, faces=F, vertex_normals=N, process=False)
            m.visual = TextureVisuals(uv=U, material=self.M[key][0])
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
           openings=[("E", -1.2, 1.2, 3), ("E", 3.2, 4.8, 2.6), ("N", -1.2, 1.2, 3), ("O", -1.2, 1.2, 3)])
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
    B.room(Z, -17.5, -14.5, -10, -5, -3, 6.2, "beton", None, openings=[("N", -17.2, -14.8, 5.6), ("S", -17.2, -14.8, 3)], nav_center=(-16, -1.5, -7.5),
           ceiling=False)
    ang = math.atan2(3, 5)
    rot = trimesh.transformations.rotation_matrix(-ang, [1, 0, 0])
    B.boxc(Z, "beton", (-16, -1.55, -7.5), (2.7, 0.2, math.hypot(3, 5)), rot=rot, style="arch")
    for k in range(10):    # marches visibles
        zz = -5.25 - k * 0.5
        yy = -0.15 - k * 0.3
        B.box(Z, "metal_gris", -17.3, -14.7, yy, yy + 0.04, zz - 0.25, zz + 0.25, style="sharp")

    # ============================ SALLE DES MACHINES (grande, en boucle) ============================
    # Hall des chaudières (haut de 7 m, passerelles) -> galerie des portes -> grand ventilateur
    # -> citerne d'encre -> couloir des douches -> piscine -> retour au hall. Une boucle : on peut fuir en rond.
    Z = "Machines"
    B.room(Z, -32, -8, -24, -10, -3, 7, "beton", "beton",
           openings=[("N", -17.2, -14.8, 3), ("E", -17, -14, 3), ("S", -26.7, -24.3, 3)],
           nav_center=(-17, -3, -20), name="Salle des machines")
    labels.append({"nom": "Salle des machines (sous-sol)", "pos": [-20, 2.5, -17]})
    for x in (-21, -27.5):                       # quatre chaudières, plus hautes que toi
        for z in (-13.5, -18.5):
            B.cyl(Z, "rouille", [x, -3, z], [x, 1.2, z], 1.2, 20)
            m = trimesh.creation.icosphere(subdivisions=2, radius=1.2)
            m.apply_scale([1, 0.45, 1])
            m.apply_translation([x, 1.2, z])
            B.add_mesh(Z, "rouille", m)          # dôme
            B.cyl(Z, "rouille", [x, 1.6, z], [x, 4.0, z], 0.25, 10)       # cheminée jusqu'au plafond
            for yy in (-2.2, -0.6, 0.8):         # cerclages rivetés
                B.cyl(Z, "metal_gris", [x, yy, z], [x, yy + 0.08, z], 1.24, 20)
            B.sphere(Z, "lum_orange", (x + 1.18, -1.6, z), 0.07)
        light((x + 1.6, -1.6, -16), "#ff9a50", 1.0, 5)
    B.boxc(Z, "rouille", (-12, -2.4, -19.5), (2.4, 1.2, 1.6))       # pompe de la piscine
    B.cyl(Z, "rouille", [-12, -1.8, -19.5], [-12, -1.0, -19.5], 0.5, 16)
    B.quad(Z, "Plaque_Pompe", mt.pump_plate(), (-12, -2.3, -18.69), 0.8, 0.5, (0, 0, 1))
    # le grand moteur : un volant d'inertie arrêté net
    fw = trimesh.creation.torus(major_radius=1.6, minor_radius=0.12, major_sections=32, minor_sections=8)
    fw.apply_transform(trimesh.transformations.rotation_matrix(math.pi / 2, [0, 1, 0]))
    fw.apply_translation([-11.5, -0.9, -12.6])
    B.add_mesh(Z, "metal_gris", fw)
    for k in range(6):
        a = k / 6 * math.tau
        B.cyl(Z, "metal_gris", [-11.5, -0.9, -12.6], [-11.5, -0.9 + 1.55 * math.sin(a), -12.6 + 1.55 * math.cos(a)], 0.06, 6)
    B.cyl(Z, "rouille", [-12.3, -0.9, -12.6], [-10.7, -0.9, -12.6], 0.25, 12)          # moyeu
    B.boxc(Z, "rouille", (-9.4, -2.1, -12.6), (2.0, 1.8, 1.4))                          # carter du moteur
    for k, y in enumerate((-0.4, 0.1, 0.5, 2.6, 3.3)):   # tuyaux au plafond, dont ceux d'encre
        key = "encre" if k in (1, 3) else "rouille"
        B.cyl(Z, key, [-31.5, y, -11 - (k % 3) * 0.5], [-8.2, y, -11 - (k % 3) * 0.5], 0.16 if key == "encre" else 0.12, 12)
    B.cyl(Z, "encre", [-12, -1.0, -19.5], [-12, -0.1, -19.5], 0.14, 10)
    B.cyl(Z, "encre", [-12, -0.1, -19.5], [-8.2, -0.1, -16], 0.14, 10)
    B.box(Z, "metal_gris", -31.8, -9, -1.0, -0.92, -21.8, -20.6, style="sharp")        # passerelle basse
    for x in np.arange(-31.5, -9, 1.5):
        B.cyl(Z, "metal_gris", [x, -0.92, -20.6], [x, -0.1, -20.6], 0.025, 6)
        B.cyl(Z, "metal_gris", [x, -3, -21.2], [x, -1.0, -21.2], 0.04, 6)
    B.cyl(Z, "metal_gris", [-31.5, -0.15, -20.6], [-9, -0.15, -20.6], 0.025, 6)
    B.box(Z, "metal_gris", -31.8, -9, 1.5, 1.58, -11.6, -10.2, style="sharp")          # passerelle haute (on y grimpe aux bras)
    for x in np.arange(-31.5, -9, 1.5):
        B.cyl(Z, "metal_gris", [x, 1.58, -11.6], [x, 2.4, -11.6], 0.025, 6)
    B.cyl(Z, "metal_gris", [-31.5, 2.35, -11.6], [-9, 2.35, -11.6], 0.025, 6)
    for k in range(15):                                                                   # échelle
        B.cyl(Z, "metal_gris", [-31.75, -2.7 + k * 0.3, -11.3], [-31.75, -2.7 + k * 0.3, -10.5], 0.02, 6)
    for z in (-11.3, -10.5):
        B.cyl(Z, "metal_gris", [-31.75, -3, z], [-31.75, 1.6, z], 0.03, 6)
    for x, z, r in ((-15, -15, 0.9), (-10.5, -16.5, 0.6), (-18, -20, 0.5), (-25, -22.5, 0.8), (-30, -15, 0.5)):   # flaques d'encre
        B.sphere(Z, "encre", (x, -2.995, z), r, scale=(1, 0.01, 0.7))
    B.sphere(Z, "lum_orange", (-25.5, 0.4, -23.8), 0.1, scale=(1.6, 0.6, 0.6))
    light((-25.5, 0.2, -23.3), "#ff4a3a", 0.7, 4)          # lampe de secours rouge au-dessus de la galerie
    mark("La pompe n°2", "« Remplissage spécial, 12/11/1985 ». La veille de la Nuit Longue. Les tuyaux noirs partent vers la piscine.",
         (-12, -2.3, -18.7), (-13.5, -1.4, -15.5))
    mark("Le grand moteur", "Un volant d'inertie de trois mètres, arrêté net. Il y a encore des traces de petites mains sur les rayons.",
         (-11.5, -0.9, -12.6), (-15.5, 0.2, -15.5))

    # ---- la galerie des portes : un couloir bas, des tuyaux, et des petites portes partout
    Z = "Galerie"
    B.room(Z, -27, -24, -40, -24, -3, 3.2, "beton", "beton",
           openings=[("N", -26.7, -24.3, 3), ("S", -26.7, -24.3, 3)], name="Galerie des portes")
    labels.append({"nom": "Galerie des portes", "pos": [-25.5, 0.6, -32]})
    for x, sd in ((-26.75, 1), (-24.25, -1)):
        B.cyl(Z, "rouille", [x + 0.05 * sd, -0.5, -39.8], [x + 0.05 * sd, -0.5, -24.2], 0.1, 10)
        B.cyl(Z, "encre", [x + 0.12 * sd, -0.15, -39.8], [x + 0.12 * sd, -0.15, -24.2], 0.08, 10)
    for z, r in ((-29, 0.4), (-35.5, 0.6)):
        B.sphere(Z, "encre", (-25.5, -2.995, z), r, scale=(1, 0.01, 1.6))
    light((-25.5, -0.3, -29), "#9fd8b8", 0.35, 4)
    light((-25.5, -0.3, -36.5), "#9fd8b8", 0.3, 4)

    # ---- la salle du grand ventilateur
    Z = "Ventilateur"
    B.room(Z, -36, -18, -54, -40, -3, 8, "beton", "beton",
           openings=[("N", -26.7, -24.3, 3), ("E", -49, -46, 3)], name="Salle du grand ventilateur")
    labels.append({"nom": "Le grand ventilateur", "pos": [-28, 3.5, -47]})
    fan_c = np.array([-35.55, 1.6, -47.0])
    ring = trimesh.creation.torus(major_radius=3.3, minor_radius=0.28, major_sections=40, minor_sections=10)
    ring.apply_transform(trimesh.transformations.rotation_matrix(math.pi / 2, [0, 1, 0]))
    ring.apply_translation(fan_c)
    B.add_mesh(Z, "rouille", ring)
    for k in range(9):                                     # grille devant les pales
        zz = -47.0 + (k - 4) * 0.72
        hh = math.sqrt(max(3.3 ** 2 - (zz + 47.0) ** 2, 0.1))
        B.cyl(Z, "metal_gris", [-35.2, fan_c[1] - hh, zz], [-35.2, fan_c[1] + hh, zz], 0.03, 6)
    blades = []
    for k in range(5):                                     # pales (objet à part : Unity les fait tourner)
        bl = rounded_box([0.08, 2.9, 0.9], 0.04, wonk=0.04, subdiv=1)
        bl.apply_translation([0, 1.6, 0])
        bl.apply_transform(trimesh.transformations.rotation_matrix(math.radians(14), [0, 1, 0]))
        bl.apply_transform(trimesh.transformations.rotation_matrix(k / 5 * math.tau, [1, 0, 0]))
        blades.append(bl)
    hub = trimesh.creation.icosphere(subdivisions=2, radius=0.45)
    hub.apply_scale([0.7, 1, 1])
    blades.append(hub)
    fan = trimesh.util.concatenate(blades)
    fan.apply_translation(fan_c + [0.15, 0, 0])
    B.unique_mesh(Z, "Pales_Ventilateur", "rouille", fan)
    light((-35.0, 1.6, -47.0), "#ffb070", 1.3, 10)          # la lumière passe entre les pales
    light((-21, 4.5, -44), "#8fa0b8", 0.3, 9)
    for k in range(4):                                     # caisses de crayons empilées, éventrées
        B.boxc(Z, "jaune_bouee" if k % 2 else "bois_peint_bleu", (-20.0, -2.6 + k * 0.78 if k < 3 else -2.6, -52.6 + (1.0 if k == 3 else 0)),
               (1.0, 0.75, 0.8), rot=trimesh.transformations.rotation_matrix(math.radians(6 * k - 8), [0, 1, 0]))
    mark("Le grand ventilateur", "Il tourne tout seul, très lentement. Le souffle fait voler les chaises de la salle de classe.",
         tuple(fan_c), (-24.5, 1.4, -47.5))

    # ---- la citerne d'encre
    Z = "Citerne"
    B.room(Z, -18, -2, -54, -40, -3, 5, "beton", "beton",
           openings=[("O", -49, -46, 3), ("N", -5.7, -3.3, 3)], nav_center=(-10, -3, -47.2), name="La citerne d'encre")
    labels.append({"nom": "La citerne d'encre", "pos": [-10, 2.5, -47]})
    for k, (x, z, r) in enumerate(((-14, -51.6, 1.4), (-8, -52.2, 1.3), (-14, -42.4, 1.2), (-4.2, -51.0, 1.0))):
        B.cyl(Z, "rouille", [x, -3, z], [x, 1.4, z], r, 22)
        m = trimesh.creation.icosphere(subdivisions=2, radius=r)
        m.apply_scale([1, 0.35, 1])
        m.apply_translation([x, 1.4, z])
        B.add_mesh(Z, "rouille", m)
        for yy in (-1.8, 0.2):
            B.cyl(Z, "metal_gris", [x, yy, z], [x, yy + 0.07, z], r + 0.03, 22)
        B.cyl(Z, "encre", [x, 1.7, z], [x, 1.7, -40.2], 0.12, 10)            # tuyau d'encre vers la piscine
        B.cyl(Z, "encre", [x, 1.6, z], [x, 1.9, z], 0.12, 10)
    B.cyl(Z, "encre", [-12.65, -0.2, -51.3], [-12.6, -2.99, -50.9], 0.05, 8)    # la fuite
    B.sphere(Z, "encre", (-12.0, -2.995, -50.0), 1.5, scale=(1, 0.01, 0.8))
    vw = trimesh.creation.torus(major_radius=0.35, minor_radius=0.04, major_sections=20, minor_sections=6)
    vw.apply_transform(trimesh.transformations.rotation_matrix(math.pi / 2, [0, 0, 1]))
    vw.apply_translation([-12.4, -1.0, -42.4])
    B.add_mesh(Z, "laiton", vw)                                                  # vanne
    light((-11, 0.8, -49.5), "#ffa060", 0.6, 6)
    mark("La citerne d'encre", "Des cuves pleines d'encre, reliées à la piscine. L'une fuit encore. Quelqu'un a gratté « 23 » sur la vanne.",
         (-12.0, -1.0, -47.0), (-4.2, 1.2, -42.6))

    # ---- le couloir des douches (retour vers la piscine)
    Z = "Douches"
    B.room(Z, -6, -3, -40, -26, -3, 3.2, "carrelage_creme", "carrelage_piscine",
           openings=[("S", -5.7, -3.3, 3), ("N", -5.7, -3.3, 3)], name="Couloir des douches")
    labels.append({"nom": "Couloir des douches", "pos": [-4.5, 0.4, -33]})
    for k, z in enumerate((-29, -32, -35, -38)):
        x = -5.85 if k % 2 == 0 else -3.15
        sd = 1 if k % 2 == 0 else -1
        B.cyl(Z, "metal_gris", [x, -0.6, z], [x + 0.35 * sd, -0.6, z], 0.025, 6)
        B.cyl(Z, "metal_gris", [x + 0.35 * sd, -0.6, z], [x + 0.35 * sd, -0.75, z], 0.06, 10)   # pommeau
        B.sphere(Z, "encre", (x + 0.35 * sd, -2.995, z), 0.35, scale=(1, 0.01, 1))         # elles coulent noir
    B.cyl(Z, "metal_gris", [-4.5, -3.0, -33], [-4.5, -2.99, -33], 0.18, 12)                  # bonde
    light((-4.5, 0.0, -33), "#cfe8ff", 0.35, 5)

    # ============================ PISCINE D'ENCRE ============================
    Z = "Piscine"
    B.room(Z, -8, 12, -26, -8, -3, 6, "carrelage_creme", None, openings=[("O", -17, -14, 3), ("S", -5.7, -3.3, 3)],
           nav_center=(-5.5, -3, -15.5))
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

    # ============================ SALLE DE JEUX (zone sûre + boutique) ============================
    Z = "SalleDeJeux"
    B.room(Z, 7, 15, 2.5, 10, 0, 3.4, "mur_creme", "parquet", openings=[("O", 3.2, 4.8, 2.6)])
    labels.append({"nom": "Salle de jeux (zone sûre)", "pos": [11, 2.6, 6.2]})
    B.cyl(Z, "tissu_menthe", [11, 0, 5.2], [11, 0.02, 5.2], 2.2, 32)                 # grand tapis rond
    B.cyl(Z, "tissu_rose", [11, 0.02, 5.2], [11, 0.03, 5.2], 0.8, 24)                # point de réapparition
    B.quad(Z, "Panneau_Playroom", mt.sign(["PLAYROOM"], bg=(247, 235, 221), fg=(190, 120, 120), font_size=56),
           (11, 2.7, 2.67), 2.2, 0.8, (0, 0, 1))
    B.quad(Z, "Panneau_Shop", mt.sign(["SHOP"], bg=(232, 244, 238), fg=(90, 140, 120), font_size=60, w=384),
           (11, 2.55, 9.83), 1.6, 0.8, (0, 0, -1))
    B.quad(Z, "Panneau_Pieces", mt.sign(["CRAYONS: 100"], bg=(60, 50, 70), fg=(240, 210, 130), font_size=52, w=448, border=(120, 100, 70)),
           (14.83, 1.7, 6.0), 1.4, 0.6, (-1, 0, 0))
    for i, (cid, name, price, slot) in enumerate(cos.CATALOG):   # présentoirs avec les vrais cosmétiques
        x = 8.6 + i * 1.2
        B.cyl(Z, "bois_peint_bleu", [x, 0, 9.0], [x, 0.9, 9.0], 0.32, 18)
        B.cyl(Z, "peinture_blanche", [x, 0.9, 9.0], [x, 0.95, 9.0], 0.36, 18)
        y_top = 0.95
        lift = 0.0
        if slot == "Cou":     # l'écharpe est présentée sur un petit support, comme un cou
            lift = 0.46
            B.cyl(Z, "bois_sombre", [x, y_top, 9.0], [x, y_top + lift, 9.0], 0.05, 10)
        elif slot == "Poitrine":
            lift = 0.02
        for k, part in enumerate(cos.BUILDERS[cid]()):
            part = part.copy()
            part.apply_scale(2.2)
            part.apply_translation([x, y_top + lift, 9.0])
            B.unique.append((Z, "Cosmetique_%s_%d" % (cid, k), part))
        B.quad(Z, "Prix_%s" % cid, mt.sign([name, "%d crayons" % price], bg=(247, 240, 228), fg=(90, 70, 80), font_size=34, w=384),
               (x, 0.6, 8.66), 0.5, 0.25, (0, 0, -1))
        B.boxc(Z, "lum_rose", (x, 0.45, 8.67), (0.12, 0.12, 0.04))                  # bouton "acheter"
    for k, (x, z, c) in enumerate(((8.2, 3.4, "tissu_rose"), (8.6, 3.3, "tissu_menthe"), (8.4, 3.75, "bois_peint_bleu"),
                                    (13.6, 3.6, "bois_peint_rose"), (13.9, 3.3, "tissu_creme"))):   # cubes de jouets
        B.boxc(Z, c, (x, 0.15 + (0.3 if k == 2 else 0), z), (0.3, 0.3, 0.3))
    # Boîte de crayons géante (récompense : 100 crayons toutes les 7 h), contre le mur est
    B.boxc(Z, "jaune_bouee", (14.45, 0.4, 7.9), (0.7, 0.8, 1.0),
           rot=trimesh.transformations.rotation_matrix(math.radians(4), [0, 1, 0]))       # carton un peu de travers
    B.boxc(Z, "bois_peint_rose", (14.1, 0.7, 7.9), (0.04, 0.1, 0.9))                 # bande rose sur la face
    for k, (dz, h, tilt, c) in enumerate(((-0.36, 0.42, -9, "lum_rose"), (-0.18, 0.55, 4, "tissu_menthe"),
                                          (0.0, 0.36, -3, "bois_peint_bleu"), (0.17, 0.6, 7, "jaune_bouee"),
                                          (0.34, 0.47, -6, "bois_peint_rose"))):          # crayons qui dépassent
        z = 7.9 + dz
        top = (14.45 + 0.02 * k, 0.8 + h, z + tilt * 0.004)
        B.cyl(Z, c, [14.45, 0.7, z], list(top), 0.055, 7)                               # 7 pans : crayon de cire
        B.cyl(Z, c, list(top), [top[0], top[1] + 0.09, top[2]], 0.02, 7)                 # pointe usée
    B.quad(Z, "Panneau_Crayons", mt.sign(["FREE CRAYONS", "+100 / 7H"], bg=(250, 238, 200), fg=(150, 100, 60), font_size=36, w=384),
           (14.08, 0.42, 7.9), 0.6, 0.3, (-1, 0, 0))
    B.boxc(Z, "lum_rose", (14.08, 0.15, 7.9), (0.04, 0.12, 0.12))                    # bouton "récolter"
    B.box(Z, "bois_sombre", 7.3, 7.9, 0, 0.6, 7.6, 8.4)                                  # tabouret + boîte à musique
    B.boxc(Z, "laiton", (7.6, 0.7, 8.0), (0.3, 0.2, 0.2))
    light((11, 2.8, 5.5), "#ffd9b0", 2.6, 9)          # la seule lumière chaude et franche du jeu
    light((7.8, 1.0, 8.0), "#ffe2b8", 0.8, 3)
    mark("La Salle de jeux (zone sûre)",
         "La seule pièce sans micro : elle ne t'entend pas ici et n'y entre jamais. On y apparaît, on y revient après s'être fait attraper. La boutique de cosmétiques est contre le mur.",
         (11, 1.0, 9.0), (10.2, 1.9, 4.6))
    mark("Le micro débranché",
         "Au Standard, l'étiquette « Playroom » est barrée : DISCONNECTED. C'est pour ça que la Salle de jeux est sûre.",
         (-19.5, 2.3, 2.1), (-16.5, 2.2, 3.0))
    mark("La boîte de crayons",
         "Toutes les 7 heures, on appuie sur le bouton rose : +100 crayons gratuits. Au-dessus, un compte à rebours dit quand revenir.",
         (14.4, 0.8, 7.9), (11.6, 1.6, 7.4))

    # ============================ CHAISES QUI VOLENT ============================
    def kid_chair(seed):
        """Petite chaise d'école en bois peint, un peu tordue (assise, dossier, 4 pieds)."""
        parts = [rounded_box([0.38, 0.045, 0.36], 0.02, wonk=0.05, subdiv=1)]
        back = rounded_box([0.38, 0.26, 0.035], 0.015, wonk=0.06, subdiv=1)
        back.apply_translation([0, 0.3, -0.17])
        parts.append(back)
        for dx in (-0.16, 0.16):
            parts.append(trimesh.creation.cylinder(radius=0.016, segment=[[dx, 0, -0.165], [dx, 0.2, -0.17]], sections=6))
            for dz in (-0.15, 0.15):
                leg = trimesh.creation.cylinder(radius=0.018, segment=[[dx, 0, dz], [dx * 1.08, -0.36, dz * 1.08]], sections=6)
                parts.append(leg)
        m = trimesh.util.concatenate(parts)
        return m

    chairs = (("Ecole", (0.0, 2.2, 18.0), (25, 40, 10)), ("Ecole", (-2.6, 2.9, 22.0), (-160, 15, 30)),
              ("Ecole", (2.3, 1.6, 25.4), (10, -70, -20)), ("Hall", (-1.6, 3.6, -2.0), (70, 20, 0)),
              ("Hall", (3.4, 2.3, 3.4), (-20, 120, 35)), ("Couloir", (20.0, 1.7, 0.0), (180, 30, 0)),
              ("Ventilateur", (-31.0, 2.6, -45.0), (40, 0, 80)), ("Ventilateur", (-29.0, 1.0, -49.4), (-30, 60, 15)),
              ("Ventilateur", (-27.6, 3.9, -44.2), (120, -20, 40)), ("Ventilateur", (-32.6, 0.4, -50.2), (15, 200, -60)))
    for k, (zone, pos, (rx, ry, rz)) in enumerate(chairs):
        m = kid_chair(k)
        rot = (trimesh.transformations.euler_matrix(math.radians(rx), math.radians(ry), math.radians(rz), "sxyz"))
        m.apply_transform(rot)
        m.apply_translation(pos)
        key = ("bois_peint_rose", "bois_peint_bleu", "peinture_blanche")[k % 3]
        B.unique_mesh(zone, "ChaiseVolante_%d" % k, key, m)
    mark("Les chaises qui volent", "Des petites chaises d'école flottent dans le noir. Si tu t'approches trop, elles tombent... et le bruit l'appelle.",
         (0.0, 2.2, 18.0), (2.5, 1.8, 15.0))

    # ============================ PETITES PORTES D'ENFANTS ============================
    # 22 petites portes avec un prénom (+ la porte 23 du couloir = les « 23 friends » du dessin).
    # Certaines sont normales, d'autres incrustées de travers, à moitié avalées par le sol, en hauteur,
    # à l'envers... ou au plafond. Elles ne s'ouvrent pas. Parfois, on y frappe de l'intérieur.
    def child_door(zone, k, name, center, normal, spin=0.0, scratched=False, w=0.72, h=1.12):
        n = np.array(normal, float)
        up = np.array([0, 1, 0.0]) if abs(n[1]) < 0.9 else np.array([0, 0, -1.0])
        right = np.cross(up, n)
        right /= np.linalg.norm(right)
        up = np.cross(n, right)
        a = math.radians(spin)
        right, up = right * math.cos(a) + up * math.sin(a), -right * math.sin(a) + up * math.cos(a)
        c = np.array(center, float)
        frame = rounded_box([w + 0.12, h + 0.1, 0.05], 0.025, wonk=0.04, subdiv=1)
        T = np.eye(4)
        T[:3, 0], T[:3, 1], T[:3, 2], T[:3, 3] = right, up, n, c + n * 0.025
        frame.apply_transform(T)
        B.add_mesh(zone, "bois_sombre", frame)
        img = mt.child_door(name, k, scratched=scratched).resize((128, 192))
        B.quad(zone, "PorteEnfant_%d" % k, img, tuple(c + n * 0.052), w, h, tuple(n), spin=spin, rough=0.75)
        B.sphere(zone, "laiton", tuple(c + n * 0.08 + right * w * 0.32 - up * h * 0.08), 0.035)
        return {"id": k, "name": name, "pos": [float(v) for v in c + n * 0.06], "face": [float(v) for v in n]}

    kid_doors = []
    names = ["Theo", "Lucie", "???", "Nour", "Ines", "Malo", "Bastien", "Mila", "Jade", "Sacha", "Lena",
             "Remi", "Adele", "Yanis", "Rose", "Elsa", "Hugo", "Nina", "Louis", "Maya", "Noe", "Clara"]
    W2 = WALL
    spots_doors = [   # zone, centre (sur la face du mur), normale vers la pièce, rotation dans le plan
        ("Hall", (-2.4, 0.56, 6 - W2), (0, 0, -1), 0),
        ("Hall", (2.6, 1.9, 6 - W2), (0, 0, -1), -4),                  # en hauteur
        ("Hall", (-7 + W2, 0.56, -3.5), (1, 0, 0), 180),               # à l'envers
        ("Ecole", (-5.5, 0.56, 12 + W2), (0, 0, 1), 3),
        ("Ecole", (-3.4, 0.2, 12 + W2), (0, 0, 1), -8),                # à moitié dans le sol
        ("Ecole", (4.0, 0.56, 12 + W2), (0, 0, 1), 0),
        ("Ecole", (6.2, 2.6, 12 + W2), (0, 0, 1), 12),                 # en hauteur, de travers
        ("Ecole", (5.6, 3.2, 30 - W2), (0, 0, -1), 180),               # à l'envers près du plafond
    ]
    gz = [-25.4, -26.9, -28.4, -29.9, -31.4, -32.9, -34.4, -35.9, -37.4, -38.9]
    for i, z in enumerate(gz):          # galerie : des portes des deux côtés, de plus en plus folles
        left = i % 2 == 0
        x = -27 + W2 if left else -24 - W2
        nrm = (1, 0, 0) if left else (-1, 0, 0)
        y = (-2.44, -2.0, -2.44, -1.2, -2.85, -2.44, -0.9, -2.44, -2.3, -1.6)[i]
        spin = (0, 6, -3, 25, 0, 90, -15, 180, 40, -100)[i]
        spots_doors.append(("Galerie", (x, y, z), nrm, spin))
    spots_doors.append(("Galerie", (-25.5, 0.2 - 0.02, -33.6), (0, -1, 0), 0))   # au plafond
    spots_doors.append(("Ventilateur", (-33.0, 2.2, -40 - W2), (0, 0, -1), 8))     # en hauteur dans la salle du ventilateur
    spots_doors.append(("Ventilateur", (-21.0, -2.44, -40 - W2), (0, 0, -1), 0))
    spots_doors.append(("Douches", (-6 + W2, -2.44, -36.5), (1, 0, 0), -5))      # entre deux douches
    for k, (zone, c, nrm, spin) in enumerate(spots_doors):
        kid_doors.append(child_door(zone, k, names[k], c, nrm, spin, scratched=(names[k] == "Malo")))
    mark("La galerie des portes", "Un couloir bas plein de petites portes avec des prénoms. Aucune ne s'ouvre. Parfois, quelqu'un frappe de l'autre côté.",
         (-25.4, -2.0, -31.4), (-25.5, -1.4, -26.0))

    # ============================ PAPIERS DE LORE + DESSINS D'ENFANTS ============================
    papers_dir = os.path.join(ROOT, "Assets/Horror/Art/Papers")
    meta = {m["id"]: m for m in json.load(open(os.path.join(papers_dir, "papers.json"), encoding="utf-8"))}
    # id : (zone, position, à plat ou au mur (direction), rotation dans le plan, caméra pour le regarder)
    spots = {
        "p01_brochure":   ("Hall", (-5.3, 1.172, 3.0), (0, 1, 0), 15, (-4.2, 2.3, 1.9)),
        "p02_news1977":   ("Hall", (2.45, 0.462, -0.5), (0, 1, 0), -20, (1.5, 1.7, -1.6)),
        "p03_rules":      ("Ecole", (-1.33, 1.6, 9.0), (1, 0, 0), 2, (0.6, 1.7, 9.0)),
        "p04_theo":       ("Ecole", (-2.5, 0.78, 26.85), (0, 1, 0), 8, (-2.2, 1.9, 25.4)),
        "p05_ines":       ("Ecole", (5.0, 0.012, 19.3), (0, 1, 0), 40, (3.5, 1.6, 18.2)),
        "p06_entertainer": ("Couloir", (17.0, 0.02, -0.25), (0, 1, 0), -30, (15.6, 1.7, 0.6)),
        "p07_lifeguard":  ("Piscine", (-3.0, -1.235, -8.6), (0, 1, 0), 12, (-2.0, 0.2, -10.6)),
        "p08_diary_a":    ("Standard", (-18.25, 0.81, -0.35), (0, 1, 0), -10, (-16.9, 1.9, -1.2)),
        "p09_diary_b":    ("Standard", (-17.95, 0.81, 0.25), (0, 1, 0), 22, (-16.9, 1.9, 1.3)),
        "p10_inspection": ("Standard", (-14.0, 0.012, 3.5), (0, 1, 0), 35, (-13.2, 1.5, 2.2)),
        "p11_watchman":   ("Couloir", (29.4, 0.02, 0.55), (0, 1, 0), -15, (27.6, 1.6, -0.4)),
        "p12_news1985":   ("Hall", (0.9, 0.012, -5.2), (0, 1, 0), 25, (0.4, 1.7, -3.0)),
        "p13_pencil":     ("Machines", (-23.2, -2.99, -21.4), (0, 1, 0), 5, (-21.6, -1.5, -19.8)),
        "s1_mila":        ("Piscine", (9.0, -2.99, -21.7), (0, 1, 0), -25, (9.6, -1.6, -19.6)),
        "s2_cassette":    ("SalleDeJeux", (7.6, 0.805, 8.0), (0, 1, 0), 30, (8.6, 1.7, 7.0)),
        "s3_payroll":     ("Standard", (-18.05, 0.012, -0.1), (0, 1, 0), 60, (-16.8, 1.2, -0.9)),
        "s4_portrait":    ("Couloir", (30.78, 0.02, 0.05), (0, 1, 0), 90, (29.4, 1.5, 0.6)),
    }
    papers = []
    for pid, (zone, pos, facing, spin, cam) in spots.items():
        m = meta[pid]
        img = Image.open(os.path.join(papers_dir, pid + ".jpg")).convert("RGB")
        img = img.resize((224, int(224 * img.height / img.width)))          # léger : on lit le papier en grand dans le lecteur
        w = 0.17
        h = w * img.height / img.width
        B.quad(zone, "Papier_" + pid, img, pos, w, h, facing, spin=spin, rough=0.95)
        papers.append({"id": pid, "titre": m["title"], "secret": m["secret"], "verso": bool(m.get("back")),
                       "pos": list(map(float, pos)), "cam": list(map(float, cam)),
                       "face": list(map(float, facing)), "spin": float(spin)})
    drawings_dir = os.path.join(ROOT, "Assets/Horror/Art/Drawings")
    for name, pos, facing, spin in (("drawing_lady", (-7.83, 1.55, 17.6), (1, 0, 0), 3),
                                    ("drawing_friends", (-7.83, 1.45, 22.4), (1, 0, 0), -4),
                                    ("drawing_pool", (7.83, 1.5, 18.8), (-1, 0, 0), 5),
                                    ("drawing_family", (7.83, 1.6, 23.6), (-1, 0, 0), -3)):
        img = Image.open(os.path.join(drawings_dir, name + ".png")).convert("RGB")
        img = img.resize((256, int(256 * img.height / img.width)))
        w = 0.62 if img.width > img.height else 0.46
        B.quad("Ecole", "Dessin_" + name, img, pos, w, w * img.height / img.width, facing, spin=spin)
    mark("Les dessins aux murs", "Des dessins d'enfants punaisés au dortoir : « the lady who listens », « my 23 friends » (un visage n'a pas de bouche), « dont go in the pool »...",
         (-7.8, 1.5, 20), (-4.5, 1.8, 20))

    # Portes du Protocole Verrou : seulement les ouvertures entre deux pièces (pas la porte 23, qui donne sur le noir)
    door_keys = [k for k, d in B.doors.items() if len(set(d["pieces"])) >= 2]
    doors = []
    for k in door_keys:
        d = dict(B.doors[k])
        d["pieces"] = sorted(set(d["pieces"]))
        doors.append(d)
    # Réseau de passage du monstre : un point par pièce + un point par porte, reliés
    nodes, edges = [], []
    for r in B.rooms:
        nodes.append(r["centre"])
    door_index = {}
    for k in door_keys:
        d = B.doors[k]
        door_index[k] = len(nodes)
        nodes.append([d["x"], d["bas"], d["z"]])
    for i, r in enumerate(B.rooms):
        for k in r["portes"]:
            if k in door_index:
                edges.append([i, door_index[k]])
    rooms = [{"zone": r["zone"], "nom": r["nom"], "min": r["min"], "max": r["max"], "centre": r["centre"]} for r in B.rooms]

    data = {"spawn": [11, 0, 5.2], "spawn_yaw": 1.5708, "lumieres": lights, "zones": labels, "lore": lore, "papiers": papers,
            "portes": doors, "pieces": rooms, "nav": {"points": nodes, "liens": edges}, "portes_enfants": kid_doors}
    return B.scene(), data


def write_unity_json(data):
    """Même contenu, au format que JsonUtility (Unity) sait lire : des objets {x,y,z} au lieu de listes."""
    v3 = lambda p: {"x": p[0], "y": p[1], "z": p[2]}
    out = {
        "spawn": v3(data["spawn"]), "spawnYaw": math.degrees(data["spawn_yaw"]),
        "lights": [{"pos": v3(l["pos"]), "color": l["color"], "intensity": l["intensity"], "distance": l["distance"]} for l in data["lumieres"]],
        "doors": [{"x": d["x"], "z": d["z"], "axis": d["axe"], "width": d["largeur"], "bottom": d["bas"], "top": d["haut"]} for d in data["portes"]],
        "rooms": [{"zone": r["zone"], "name": r["nom"], "min": v3(r["min"]), "max": v3(r["max"]), "center": v3(r["centre"])} for r in data["pieces"]],
        "navPoints": [v3(p) for p in data["nav"]["points"]],
        "navLinks": [{"x": a, "y": b} for a, b in data["nav"]["liens"]],
        "papers": [{"id": p["id"], "pos": v3(p["pos"]), "face": v3(p["face"]), "spin": p["spin"]} for p in data["papiers"]],
        "childDoors": [{"id": d["id"], "name": d["name"], "pos": v3(d["pos"]), "face": v3(d["face"])} for d in data["portes_enfants"]],
    }
    with open(OUT_JSON.replace(".json", "_Unity.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)


def _flat(rgb):
    return Image.new("RGB", (8, 8), rgb)


if __name__ == "__main__":
    scene, data = build()
    os.makedirs(os.path.dirname(OUT_GLB), exist_ok=True)
    scene.export(OUT_GLB)
    tx.dedupe_glb(OUT_GLB)
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    write_unity_json(data)
    tris = sum(len(g.faces) for g in scene.geometry.values())
    print("OK ->", OUT_GLB, "| triangles :", tris, "| taille :", os.path.getsize(OUT_GLB) // 1024, "Ko",
          "| lumières :", len(data["lumieres"]), "| repères :", len(data["lore"]))
