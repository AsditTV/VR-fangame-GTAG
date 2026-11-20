"""Cosmétiques de la boutique, version Blender (même style que la poupée : papier, tissu, laine, feutre).

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_cosmetiques.py -- DOSSIER_SORTIE DOSSIER_TEXTURES

Sortie : paper_crown.glb, party_hat.glb, ribbon_bow.glb, yarn_scarf.glb, moon_badge.glb
Chaque fichier garde l'origine au point d'attache (haut du crâne, cou, poitrine) et à peu près le même
encombrement que l'ancien : les réglages offset / rotation / échelle des CosmeticItem restent valables.
Repère Blender : avant du personnage = -Y, haut = +Z.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
OUTDIR = argv[0]
os.makedirs(OUTDIR, exist_ok=True)

from poupee_utils import *  # noqa: E402,F401,F403
import poupee_utils as pu  # noqa: E402

pu.init(argv[1], seed=41)


def start(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    pu._tex_cache.clear()
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    return root


def part(root, name, bm, mats, uv=0.05):
    ob = bpy.data.objects.new(name, bpy.data.meshes.new(name))
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = root
    return finish(ob, bm, mats, uv)


def export(name):
    out = os.path.join(OUTDIR, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True, export_cameras=False,
                              export_lights=False, export_extras=False)
    pu.strip_scene_name(out)
    print("EXPORT ->", out, sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH"), "faces")


def merge(dst, src):
    me = bpy.data.meshes.new("tmp"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)


# ================================================================== couronne en papier (prestige)
root = start("paper_crown")
M_PAPIER = material("Papier_Couronne", srgb("#F2E8CD"), "feutre", 0.95, seed=51)
M_ROSE, M_BLEU, M_VERT = (material("Boule_" + n, srgb(c), "feutre", 0.9, seed=52)
                          for n, c in (("Rose", "#E6A0B4"), ("Bleu", "#A0C8E6"), ("Vert", "#AAD7B4")))
M_SCOTCH = material("Scotch", srgb("#F3EBC8"), None, 0.3)
bm = bmesh.new()
N, R = 96, 0.100
pts_top = []
low, high = [], []
for j in range(N):
    a = 2 * math.pi * j / N
    k = (j % 12) / 12.0
    tooth = 1 - abs(k * 2 - 1)                       # pointe en zigzag (7 pointes + une plus petite)
    top = 0.030 + (0.068 + 0.012 * noise.noise(Vector((a * 3, 1, 1)))) * tooth * (0.7 if j >= 84 else 1.0)
    r = R * (1 + 0.025 * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, 2))))
    low.append(bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, -0.02))))
    high.append(bm.verts.new(Vector((math.cos(a) * r * 0.985, math.sin(a) * r * 0.985, top + 0.0025 * noise.noise(Vector((a * 9, 3, 3)))))))
    if j % 12 == 6:
        pts_top.append(high[-1].co.copy())
for j in range(N):
    bm.faces.new((low[j], low[(j + 1) % N], high[(j + 1) % N], high[j]))
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0018)
part(root, "paper_crown_0", bm, [M_PAPIER], 0.06)
bm_list = [bmesh.new(), bmesh.new(), bmesh.new()]
for i, p in enumerate(pts_top):                       # boules de papier froissé collées sur les pointes
    ellipsoid(bm_list[i % 3], p + Vector((0, 0, 0.006)), (0.011, 0.011, 0.011), 0.004, 120, i, 12, 8)
for i, (b, m) in enumerate(zip(bm_list, (M_ROSE, M_BLEU, M_VERT))):
    part(root, f"paper_crown_{i + 1}", b, [m], 0.03)
bm = bmesh.new()                                      # le bout de scotch qui ferme la couronne
a = 2 * math.pi * 90 / N
thin_box(bm, Vector((math.cos(a) * R * 1.01, math.sin(a) * R * 1.01, 0.01)), Vector((0, 0, 1)),
         Vector((math.cos(a), math.sin(a), 0)), 0.035, 0.02, 0.0008)
part(root, "paper_crown_4", bm, [M_SCOTCH], 0.03)
export("paper_crown")

# ================================================================== chapeau pointu de fête
root = start("party_hat")
M_A = material("Chapeau_Rose", srgb("#E9B3C2"), "feutre", 0.9, seed=53)
M_B = material("Chapeau_Creme", srgb("#F3E9D2"), "feutre", 0.9, seed=53)
M_POMPON = material("Pompon_Laine", srgb("#A9D2C1"), "laine", 1.0, seed=54)
M_ELAST = material("Elastique", srgb("#3A3240"), None, 0.6)
bm = bmesh.new()
SEG, RINGS, H = 40, 24, 0.19
grid = []
for i in range(RINGS + 1):
    t = i / RINGS
    ring = []
    for j in range(SEG):
        a = 2 * math.pi * j / SEG
        r = 0.064 * (1 - t) + 0.004
        lean = Vector((0.018 * t * t, 0.006 * t * t, 0))           # pointe un peu de travers
        ring.append(bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, -0.018 + H * t)) + lean))
    grid.append(ring)
for i in range(RINGS):
    for j in range(SEG):
        f = bm.faces.new((grid[i][j], grid[i][(j + 1) % SEG], grid[i + 1][(j + 1) % SEG], grid[i + 1][j]))
        f.material_index = int((j / SEG * 6 + i / RINGS * 3)) % 2   # rayures en spirale
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0015)
part(root, "party_hat_0", bm, [M_A, M_B], 0.05)
bm = bmesh.new()
ellipsoid(bm, Vector((0.018, 0.006, -0.018 + H + 0.012)), (0.024, 0.024, 0.022), 0.008, 90, 3, 16, 12)
part(root, "party_hat_1", bm, [M_POMPON], 0.03)
bm = curve_mesh([[(0.06, 0.0, -0.016), (0.05, -0.04, -0.07), (0.0, -0.06, -0.10), (-0.05, -0.04, -0.07), (-0.06, 0.0, -0.016)]],
                0.0015, "elastique")
part(root, "party_hat_2", bm, [M_ELAST], 0.03)
export("party_hat")

# ================================================================== nœud en ruban (sur le côté de la tête)
root = start("ribbon_bow")
M_RUBAN = material("Ruban_Satin", srgb("#E7A9BC"), "tissu", 0.45, seed=55)
M_FILN = material("Fil_Noeud", srgb("#3A3240"), None, 0.7)
C = Vector((0.093, -0.02, 0.036))                   # centre du nœud (même place que l'ancien)
bm = bmesh.new()


def ribbon(bm, centers, widths, ups):
    """Ruban plat : une suite de points centraux, une largeur et une direction de largeur par point."""
    rows = [[bm.verts.new(c - u * w / 2), bm.verts.new(c + u * w / 2)] for c, w, u in zip(centers, widths, ups)]
    for i in range(len(rows) - 1):
        bm.faces.new((rows[i][0], rows[i + 1][0], rows[i + 1][1], rows[i][1]))


for sign in (1, -1):
    # une boucle : le ruban part sur le côté et revient, en faisant le tour d'avant en arrière ;
    # sa largeur est verticale, donc on voit bien la boucle de face (un peu affaissée vers le bas)
    cs, ws, us = [], [], []
    for s in range(30):
        t = s / 29 * 2 * math.pi
        x = sign * (0.010 + 0.042 * (1 - math.cos(t)) * 0.5)
        y = 0.011 * math.sin(t)
        z = -0.006 * (1 - math.cos(t)) * 0.5 + 0.004 * math.sin(2 * t)
        cs.append(C + Vector((x, y, z)))
        ws.append(0.026 + 0.012 * (1 - math.cos(t)) * 0.5)
        us.append(Vector((0, 0, 1)))
    ribbon(bm, cs, ws, us)
for sign, lean in ((1, 0.35), (-1, -0.25)):        # les deux pans qui pendent, bouts coupés en V
    cs, ws, us = [], [], []
    for s in range(12):
        t = s / 11
        cs.append(C + Vector((sign * (0.006 + 0.028 * t), -0.004 - 0.004 * t, -0.006 - 0.062 * t)))
        ws.append(0.019 * (1 - 0.15 * t))
        us.append(Vector((math.cos(lean), 0, math.sin(lean))))
    ribbon(bm, cs, ws, us)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0015)
ellipsoid(bm, C, (0.012, 0.013, 0.014), 0.002, 60, 2, 14, 10)                    # le nœud central
part(root, "ribbon_bow_0", bm, [M_RUBAN], 0.04)
bm = bmesh.new()
for k in range(3):
    thin_box(bm, C + Vector((0, -0.0135, -0.008 + k * 0.008)), Vector((1, 0, 0.3)), Vector((0, -1, 0)), 0.012, 0.0018, 0.0015)
part(root, "ribbon_bow_1", bm, [M_FILN], 0.03)
export("ribbon_bow")

# ================================================================== écharpe en laine tricotée
root = start("yarn_scarf")
M_LAINE_A = material("Laine_Menthe", srgb("#A9D2C1"), "laine", 1.0, seed=56)
M_LAINE_B = material("Laine_Creme", srgb("#F1E6D2"), "laine", 1.0, seed=56)
bm = bmesh.new()
SEG, RG = 64, 14
grid = []
for i in range(SEG + 1):
    a = 2 * math.pi * i / SEG
    ring = []
    for j in range(RG):
        b = 2 * math.pi * j / RG
        rib = 1 + 0.12 * math.sin(i * 1.0) * math.cos(b)          # côtes du tricot
        r_tube = 0.020 * rib
        R0 = 0.085 + 0.004 * math.sin(3 * a)
        p = Vector((math.cos(a) * (R0 + r_tube * math.cos(b)), math.sin(a) * (R0 + r_tube * math.cos(b)) * 0.95,
                    r_tube * 1.4 * math.sin(b) + 0.004 * math.sin(2 * a)))
        ring.append(bm.verts.new(p))
    grid.append(ring)
for i in range(SEG):
    for j in range(RG):
        f = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][(j + 1) % RG], grid[i][(j + 1) % RG]))
        f.material_index = (i // 6) % 2                            # rayures
# les deux pans qui pendent devant, un peu croisés
for sign in (1, -1):
    rows = []
    for s in range(16):
        t = s / 15
        x = sign * (0.03 - 0.02 * t) + 0.01 * math.sin(t * 3)
        y = -0.088 - 0.012 * t
        z = -0.01 - 0.17 * t
        rows.append([bm.verts.new(Vector((x + dx, y - 0.006 * abs(dx) / 0.025, z))) for dx in (-0.025, 0, 0.025)])
    for i in range(len(rows) - 1):
        for k in range(2):
            f = bm.faces.new((rows[i][k], rows[i][k + 1], rows[i + 1][k + 1], rows[i + 1][k]))
            f.material_index = (i // 3) % 2
bmesh.ops.solidify(bm, geom=[f for f in bm.faces if len(f.verts) == 4 and f.calc_area() < 0.0005], thickness=0.008)
part(root, "yarn_scarf_0", bm, [M_LAINE_A, M_LAINE_B], 0.03)
fringe = []
for sign in (1, -1):                                               # franges au bout des pans
    for k in range(5):
        x = sign * 0.01 + 0.025 * (k / 4 - 0.5) + 0.01 * math.sin(3)
        fringe.append([(x, -0.1, -0.18), (x + 0.004 * (k - 2), -0.102, -0.205), (x + 0.006 * (k - 2), -0.1, -0.225)])
part(root, "yarn_scarf_1", curve_mesh(fringe, 0.0022, "franges"), [M_LAINE_B], 0.02)
export("yarn_scarf")

# ================================================================== badge lune en feutre (poitrine)
root = start("moon_badge")
M_FEUTRE = material("Feutre_Lune", srgb("#EAD9A0"), "feutre", 1.0, seed=57)
M_FOND = material("Feutre_Nuit", srgb("#7F86B8"), "feutre", 1.0, seed=58)
M_EPINGLE = material("Epingle", srgb("#C9C9CF"), None, 0.25)
M_EPINGLE.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 1.0
M_FILB = material("Fil_Badge", srgb("#3A3240"), None, 0.7)


def flat_shape(bm, outline, y0, depth):
    """Extrude une forme plate (contour dans le plan XZ) vers l'arrière (+Y)."""
    front = [bm.verts.new(Vector((x, y0, z))) for x, z in outline]
    back = [bm.verts.new(Vector((x, y0 + depth, z))) for x, z in outline]
    bm.faces.new(front[::-1])
    bm.faces.new(back)
    n = len(outline)
    for i in range(n):
        bm.faces.new((front[i], front[(i + 1) % n], back[(i + 1) % n], back[i]))


bm = bmesh.new()                                   # rond bleu nuit, bord irrégulier découpé aux ciseaux
circ = [(0.036 * (1 + 0.04 * noise.noise(Vector((math.cos(a) * 4, math.sin(a) * 4, 1)))) * math.cos(a),
         0.036 * (1 + 0.04 * noise.noise(Vector((math.cos(a) * 4, math.sin(a) * 4, 1)))) * math.sin(a))
        for a in [2 * math.pi * k / 40 for k in range(40)]]
flat_shape(bm, circ, -0.004, 0.006)
part(root, "moon_badge_0", bm, [M_FOND], 0.04)
bm = bmesh.new()                                   # croissant de lune en feutre, cousu par-dessus
moon = []
for k in range(30):                                # bord extérieur : grand arc à gauche
    ang = math.radians(55 + 250 * k / 29)
    moon.append((0.026 * math.cos(ang), 0.026 * math.sin(ang)))
for k in range(24):                                # le creux : arc d'un cercle décalé à droite
    ang = math.radians(285 - 210 * k / 23)
    moon.append((0.013 + 0.02 * math.cos(ang), 0.02 * math.sin(ang)))
flat_shape(bm, moon, -0.009, 0.005)
part(root, "moon_badge_1", bm, [M_FEUTRE], 0.04)
bm = bmesh.new()                                   # points de couture autour de la lune + œil fermé dessiné au fil
for k in range(14):
    a = math.radians(-110 + 220 * k / 13)
    a = math.radians(70 + 220 * k / 13)
    p = Vector((0.022 * math.cos(a), -0.0098, 0.022 * math.sin(a)))
    thin_box(bm, p, Vector((-math.sin(a), 0, math.cos(a))), Vector((0, -1, 0)), 0.005, 0.0012, 0.001)
for k in range(5):
    t = k / 4
    thin_box(bm, Vector((-0.016 + 0.008 * t, -0.0098, 0.004 - 0.004 * math.sin(math.pi * t))), Vector((1, 0, 0)), Vector((0, -1, 0)), 0.003, 0.001, 0.001)
part(root, "moon_badge_2", bm, [M_FILB], 0.03)
bm = curve_mesh([[(-0.028, 0.004, 0.0), (0.0, 0.0045, 0.002), (0.03, 0.004, 0.0), (0.04, 0.004, 0.004), (0.03, 0.004, 0.006),
                  (0.0, 0.0045, 0.006)]], 0.0012, "epingle")
part(root, "moon_badge_3", bm, [M_EPINGLE], 0.02)
export("moon_badge")
