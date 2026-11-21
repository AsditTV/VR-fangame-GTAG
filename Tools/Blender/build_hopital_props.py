"""L'infirmerie du sous-sol (là où le Docteur « testait » les enfants) : mobilier et textures. Version Blender.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_hopital_props.py -- DOSSIER_SORTIE DOSSIER_TEXTURES

Sortie (mètres réels, base en 0, avant = +Z Unity) : LitHopital, Rideau (1,6 m, cache la vue du Docteur),
Plateau (chariot + plateau d'instruments qui tombe : objet bruyant), Perfusion (pied + poche : bruyant),
Bocaux (étagère de bocaux : bruyant), ChaiseAttente, FauteuilRoulant, LampeOperatoire, TableOperation,
Classeur ; textures hopital_carrelage.png, hopital_mur.png, hopital_plafond.png.
Les morceaux qui tombent sont des noeuds à part (« Chute_* ») : InstitutBuilder les rend physiques.
DA : pastel d'hôpital d'enfants (menthe, rose fané, crème), tout un peu de travers, sangles en cuir.
"""
import math
import os
import shutil
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
OUTDIR = argv[0]
os.makedirs(OUTDIR, exist_ok=True)

from poupee_utils import *  # noqa: E402,F401,F403
import poupee_utils as pu  # noqa: E402

pu.init(argv[1], seed=101)


def start(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    pu._tex_cache.clear()
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    return root


def part(root, name, bm, mats, uv=0.1):
    ob = bpy.data.objects.new(name, bpy.data.meshes.new(name))
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = root
    return finish(ob, bm, mats, uv)


def node(root, name):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.parent = root
    return e


def export(name):
    out = os.path.join(OUTDIR, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True, export_cameras=False,
                              export_lights=False, export_extras=False)
    pu.strip_scene_name(out)
    print("EXPORT ->", out)


def box(bm, lo, hi, bevel=0.0):
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    c, s = (Vector(lo) + Vector(hi)) / 2, Vector(hi) - Vector(lo)
    for v in tmp.verts:
        v.co = Vector((c.x + v.co.x * s.x, c.y + v.co.y * s.y, c.z + v.co.z * s.z))
    if bevel > 0:
        bmesh.ops.bevel(tmp, geom=tmp.edges[:], offset=bevel, segments=1, affect="EDGES")
    me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


def rod(bm, a, b, r, segs=8):
    tmp = bmesh.new()
    L = (Vector(b) - Vector(a)).length
    tube(tmp, 0.0, L, lambda t, ang: r, segs, 1, 0.0, 10, 0, True, True)
    q = (Vector(b) - Vector(a)).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(Vector(a)) @ q, verts=tmp.verts)
    me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


# ================================================================== textures de l'infirmerie
def tiles_png(name, base, grout, n=4, size=1024, seed=1, half=None):
    y, x = np.mgrid[0:size, 0:size] / size
    wob = (pu.tile_noise(size, [(3, 1.0), (7, 0.4)], seed) - 0.5) * 0.012
    gx = np.abs(((x + wob) * n) % 1.0 - 0.5)
    gy = np.abs(((y - wob) * n) % 1.0 - 0.5)
    joint = np.clip((np.maximum(gx, gy) - 0.47) / 0.02, 0, 1)
    tile_id = (np.floor((x + wob) * n) * 7 + np.floor((y - wob) * n) * 13) % 5
    vary = 0.93 + 0.03 * tile_id + 0.05 * (pu.tile_noise(size, [(2, 1.0), (6, 0.5)], seed + 3) - 0.5)
    stain = np.clip((pu.tile_noise(size, [(1, 1.0), (3, 0.6)], seed + 5) - 0.55) / 0.45, 0, 1)
    rust = np.clip((pu.tile_noise(size, [(4, 1.0), (11, 0.5)], seed + 7) - 0.72) / 0.28, 0, 1)   # traînées rouille
    col = np.zeros((size, size, 4))
    for i in range(3):
        c = base[i] * vary * (1 - 0.3 * stain) * (1 - 0.25 * rust * (1 if i else 0.4))
        col[..., i] = c * (1 - joint) + grout[i] * joint
    if half is not None:                                   # haut du mur : enduit crème taché (pas de carrelage)
        top = y < 0.5
        plaster = 0.9 + 0.08 * (pu.tile_noise(size, [(2, 1.0), (8, 0.4)], seed + 9) - 0.5)
        for i in range(3):
            col[..., i] = np.where(top, half[i] * plaster * (1 - 0.35 * stain), col[..., i])
        band = (np.abs(y - 0.5) < 0.012)                    # frise rose entre les deux
        for i, v in enumerate((0.86, 0.62, 0.68)):
            col[..., i] = np.where(band, v, col[..., i])
    col[..., 3] = 1
    pu.save_rgba(name, col)


tiles_png("hopital_carrelage", (0.70, 0.84, 0.79), (0.30, 0.35, 0.34), 5, seed=21)
tiles_png("hopital_mur", (0.72, 0.86, 0.82), (0.36, 0.42, 0.42), 8, seed=22, half=(0.88, 0.84, 0.76))
tiles_png("hopital_plafond", (0.84, 0.82, 0.78), (0.6, 0.58, 0.55), 3, seed=23)
for f in ("hopital_carrelage.png", "hopital_mur.png", "hopital_plafond.png"):
    shutil.copy(os.path.join(pu.TEXDIR, f), os.path.join(OUTDIR, f))


def mats():
    m = {}
    m["metal"] = material("Metal_Menthe", srgb("#A9CFC2"), None, 0.45)
    m["metal"].node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.4
    m["chrome"] = material("Chrome_Terne", srgb("#B9BCC2"), None, 0.3)
    m["chrome"].node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.9
    m["drap"] = material("Drap_Creme", srgb("#EDE4D3"), "tissu", 0.9, seed=31)
    m["rose"] = material("Rideau_Rose", srgb("#D9A7B3"), "tissu", 0.95, seed=32)
    m["cuir"] = material("Sangle_Cuir", srgb("#6B4A3E"), "feutre", 0.6, seed=33)
    m["bois"] = material("Bois_Clair", srgb("#C9A98A"), "feutre", 0.7, seed=34)
    m["verre"] = material("Verre_Bocal", srgb("#C8DCD4"), None, 0.08)
    m["liquide"] = material("Liquide_Trouble", srgb("#8FA36A"), None, 0.2)
    m["tache"] = material("Tache_Ancienne", srgb("#8A5A4E"), None, 0.8)
    return m


# ================================================================== lit d'enfant à barreaux, avec sangles
root = start("LitHopital"); M = mats()
bm = bmesh.new()
for x in (-0.42, 0.42):
    for y in (-0.82, 0.82):
        rod(bm, (x, y, 0), (x, y, 0.62 if y > 0 else 0.5), 0.02)
for y in (-0.82, 0.82):
    h = 0.62 if y > 0 else 0.5
    rod(bm, (-0.42, y, h), (0.42, y, h), 0.02)
    for k in range(5):
        x = -0.32 + k * 0.16
        rod(bm, (x, y, 0.36), (x, y, h), 0.012)
for x in (-0.42, 0.42):
    rod(bm, (x, -0.82, 0.34), (x, 0.82, 0.34), 0.018)
part(root, "Cadre", bm, [M["metal"]], 0.05)
bm = bmesh.new()
box(bm, (-0.4, -0.8, 0.34), (0.4, 0.8, 0.46), 0.03)
part(root, "Matelas", bm, [M["drap"]], 0.2)
bm = bmesh.new()
ellipsoid(bm, Vector((0, 0.62, 0.5)), (0.25, 0.12, 0.06), 0.008, 10, 3, 16, 8)
part(root, "Oreiller", bm, [M["drap"]], 0.15)
bm = bmesh.new()
for y in (-0.45, 0.15):                                    # sangles (poignets, chevilles)
    box(bm, (-0.43, y - 0.035, 0.455), (0.43, y + 0.035, 0.475))
part(root, "Sangles", bm, [M["cuir"]], 0.05)
bm = bmesh.new()
disc(bm, Vector((0.08, -0.1, 0.462)), Vector((0, 0, 1)), 0.13, 0.003, 18, 0.25, 4)
part(root, "Tache", bm, [M["tache"]], 0.05)
export("LitHopital")

# ================================================================== rideau sur rail (cache la vue)
root = start("Rideau"); M = mats()
bm = bmesh.new()
rod(bm, (-0.8, 0, 1.75), (0.8, 0, 1.75), 0.012)
for x in (-0.8, 0.8):
    rod(bm, (x, 0, 0), (x, 0, 1.75), 0.015)
part(root, "Rail", bm, [M["chrome"]], 0.05)
bm = bmesh.new()
COLS, ROWS = 26, 6
grid = []
for i in range(ROWS + 1):
    t = i / ROWS
    row = []
    for j in range(COLS + 1):
        u = j / COLS
        x = -0.78 + u * 1.56
        z = 1.72 - t * 1.62 + (0.03 * math.sin(u * 11) if i == ROWS else 0)
        y = 0.05 * math.sin(u * 2 * math.pi * 6)
        row.append(bm.verts.new(Vector((x, y, z))))
    grid.append(row)
for i in range(ROWS):
    for j in range(COLS):
        bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
part(root, "Toile", bm, [M["rose"]], 0.3)
export("Rideau")

# ================================================================== chariot + plateau d'instruments (le plateau tombe)
root = start("Plateau"); M = mats()
bm = bmesh.new()
for x in (-0.22, 0.22):
    for y in (-0.15, 0.15):
        rod(bm, (x, y, 0.03), (x, y, 0.72), 0.012)
        ellipsoid(bm, Vector((x, y, 0.025)), (0.025, 0.025, 0.025), 0.0, 10, 0, 8, 6)
box(bm, (-0.24, -0.17, 0.3), (0.24, 0.17, 0.315))
part(root, "Chariot", bm, [M["chrome"]], 0.05)
chute = node(root, "Chute_Plateau")
bm = bmesh.new()
box(bm, (-0.25, -0.18, 0.72), (0.25, 0.18, 0.74))
for x in (-0.25, 0.25):
    box(bm, (x - 0.005, -0.18, 0.74), (x + 0.005, 0.18, 0.77))
for y in (-0.18, 0.18):
    box(bm, (-0.25, y - 0.005, 0.74), (0.25, y + 0.005, 0.77))
part(chute, "Plateau_Metal", bm, [M["chrome"]], 0.05)
bm = bmesh.new()
for k in range(5):                                         # instruments : tiges, pinces, une seringue géante
    a = k * 0.6 - 1.2
    rod(bm, (-0.15 + k * 0.07, -0.1, 0.75), (-0.12 + k * 0.07 + 0.05 * math.sin(a), 0.1, 0.752), 0.006)
rod(bm, (0.08, -0.12, 0.765), (0.08, 0.12, 0.765), 0.022)
part(chute, "Instruments", bm, [M["chrome"]], 0.04)
bm = bmesh.new()
for (x, y) in ((-0.17, 0.11), (0.18, -0.1)):
    tube(bm, 0.0, 0.08, lambda t, a: 0.03, 12, 1, 0, 10, 0, True, False)
    for v in bm.verts:
        if v.co.z <= 0.081 and abs(v.co.x) < 0.04 and abs(v.co.y) < 0.04:
            v.co += Vector((x, y, 0.74))
part(chute, "Fioles", bm, [M["verre"]], 0.04)
export("Plateau")

# ================================================================== pied à perfusion (tout tombe)
root = start("Perfusion"); M = mats()
chute = node(root, "Chute_Perfusion")
bm = bmesh.new()
for k in range(5):
    a = k * 2 * math.pi / 5
    rod(bm, (0, 0, 0.06), (math.cos(a) * 0.26, math.sin(a) * 0.26, 0.03), 0.012)
rod(bm, (0, 0, 0.03), (0, 0, 1.75), 0.013)
rod(bm, (-0.14, 0, 1.75), (0.14, 0, 1.75), 0.01)
part(chute, "Pied", bm, [M["chrome"]], 0.05)
bm = bmesh.new()
ellipsoid(bm, Vector((0.12, 0, 1.58)), (0.07, 0.03, 0.12), 0.004, 12, 6, 12, 8)
part(chute, "Poche", bm, [M["liquide"]], 0.05)
part(chute, "Tuyau", curve_mesh([[(0.12, 0, 1.46), (0.16, -0.05, 1.1), (0.08, -0.12, 0.6), (0.2, -0.1, 0.25)]], 0.004, "tuyau", res=3, bevel_res=1), [M["verre"]], 0.04)
export("Perfusion")

# ================================================================== étagère de bocaux (les bocaux tombent)
root = start("Bocaux"); M = mats()
bm = bmesh.new()
box(bm, (-0.45, -0.15, 0.0), (0.45, 0.15, 0.03))
for z in (0.55, 1.05):
    box(bm, (-0.45, -0.15, z), (0.45, 0.15, z + 0.03))
for x in (-0.44, 0.44):
    box(bm, (x - 0.015, -0.15, 0), (x + 0.015, 0.15, 1.3))
box(bm, (-0.45, 0.14, 0), (0.45, 0.16, 1.3))
part(root, "Etagere", bm, [M["bois"]], 0.1)
chute = node(root, "Chute_Bocaux")
bm_v, bm_l = bmesh.new(), bmesh.new()
for shelf, z in enumerate((0.58, 1.08)):
    for k in range(4):
        x = -0.33 + k * 0.22 + 0.02 * math.sin(k * 3 + shelf)
        h = 0.2 + 0.06 * ((k + shelf) % 3)
        tmp = bmesh.new()
        tube(tmp, 0.0, h, lambda t, a: 0.065, 14, 1, 0, 10, 0, True, True)
        bmesh.ops.translate(tmp, vec=Vector((x, 0, z)), verts=tmp.verts)
        me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free(); bm_v.from_mesh(me); bpy.data.meshes.remove(me)
        ellipsoid(bm_l, Vector((x, 0, z + h * 0.4)), (0.05, 0.05, h * 0.36), 0.01, 6, k, 10, 8)
part(chute, "Bocaux_Verre", bm_v, [M["verre"]], 0.05)
part(chute, "Bocaux_Contenu", bm_l, [M["liquide"]], 0.05)
export("Bocaux")

# ================================================================== chaise d'attente d'enfant
root = start("ChaiseAttente"); M = mats()
bm = bmesh.new()
for x in (-0.15, 0.15):
    for y in (-0.15, 0.15):
        rod(bm, (x, y, 0), (x, y, 0.32), 0.012)
    rod(bm, (x, 0.15, 0.32), (x, 0.17, 0.62), 0.012)
part(root, "Pieds", bm, [M["metal"]], 0.05)
bm = bmesh.new()
box(bm, (-0.18, -0.18, 0.32), (0.18, 0.18, 0.36), 0.01)
box(bm, (-0.18, 0.15, 0.42), (0.18, 0.18, 0.62), 0.01)
part(root, "Assise", bm, [M["rose"]], 0.1)
export("ChaiseAttente")

# ================================================================== fauteuil roulant d'enfant
root = start("FauteuilRoulant"); M = mats()
bm = bmesh.new()
for x in (-0.27, 0.27):
    tmp = bmesh.new()
    tube(tmp, -0.015, 0.015, lambda t, a: 0.26, 24, 1, 0, 10, 0, True, True)
    bmesh.ops.rotate(tmp, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=tmp.verts)
    bmesh.ops.translate(tmp, vec=Vector((x, 0.05, 0.26)), verts=tmp.verts)
    me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
part(root, "Roues", bm, [M["chrome"]], 0.05)
bm = bmesh.new()
box(bm, (-0.22, -0.2, 0.38), (0.22, 0.2, 0.42))
box(bm, (-0.22, 0.18, 0.42), (0.22, 0.22, 0.78))
part(root, "Siege", bm, [M["cuir"]], 0.08)
bm = bmesh.new()
for x in (-0.22, 0.22):
    rod(bm, (x, 0.22, 0.4), (x, 0.24, 0.92), 0.012)
    rod(bm, (x, -0.2, 0.38), (x, -0.32, 0.06), 0.01)
part(root, "Cadre", bm, [M["metal"]], 0.05)
export("FauteuilRoulant")

# ================================================================== lampe opératoire (accrochée au plafond, base en 0 = haut)
root = start("LampeOperatoire"); M = mats()
lum = material("Lampe_Lumiere", srgb("#FFF6DA"), None, 0.2)
lum.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = srgb("#FFF0C8")
lum.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 2.5
bm = bmesh.new()
rod(bm, (0, 0, 0), (0, 0, -0.6), 0.02)
tmp = bmesh.new()
tube(tmp, 0.0, 0.18, lambda t, a: 0.12 + 0.3 * t, 24, 3, 0, 10, 0, True, False)
bmesh.ops.translate(tmp, vec=Vector((0, 0, -0.78)), verts=tmp.verts)
me = bpy.data.meshes.new("t"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
part(root, "Coupole", bm, [M["metal"]], 0.05)
bm = bmesh.new()
for k in range(5):
    a = k * 2 * math.pi / 5
    disc(bm, Vector((math.cos(a) * 0.24, math.sin(a) * 0.24, -0.6)), Vector((0, 0, -1)), 0.07, 0.01, 14)
disc(bm, Vector((0, 0, -0.6)), Vector((0, 0, -1)), 0.08, 0.01, 14)
part(root, "Ampoules", bm, [lum], 0.05)
export("LampeOperatoire")

# ================================================================== table d'opération (avec sangles)
root = start("TableOperation"); M = mats()
bm = bmesh.new()
rod(bm, (0, 0, 0), (0, 0, 0.7), 0.06, 12)
box(bm, (-0.3, -0.3, 0.0), (0.3, 0.3, 0.05), 0.01)
part(root, "Pied", bm, [M["chrome"]], 0.05)
bm = bmesh.new()
box(bm, (-0.32, -0.85, 0.7), (0.32, 0.85, 0.8), 0.03)
part(root, "Plateau_Table", bm, [M["drap"]], 0.15)
bm = bmesh.new()
for y in (-0.5, 0.0, 0.5):
    box(bm, (-0.33, y - 0.04, 0.795), (0.33, y + 0.04, 0.815))
part(root, "Sangles", bm, [M["cuir"]], 0.05)
export("TableOperation")

# ================================================================== classeur des dossiers (tiroir entrouvert)
root = start("Classeur"); M = mats()
bm = bmesh.new()
box(bm, (-0.25, -0.3, 0), (0.25, 0.3, 1.3), 0.01)
part(root, "Caisson", bm, [M["metal"]], 0.1)
bm = bmesh.new()
for k in range(4):
    z = 0.1 + k * 0.31
    out = 0.18 if k == 2 else 0.0
    box(bm, (-0.22, -0.31 - out, z), (0.22, -0.29 - out, z + 0.27))
    box(bm, (-0.06, -0.335 - out, z + 0.17), (0.06, -0.31 - out, z + 0.2))
part(root, "Tiroirs", bm, [M["chrome"]], 0.05)
bm = bmesh.new()
for k in range(8):                                         # dossiers qui dépassent du tiroir ouvert
    box(bm, (-0.2 + k * 0.05, -0.47, 0.75), (-0.17 + k * 0.05, -0.31, 0.95 + 0.02 * (k % 3)))
part(root, "Dossiers", bm, [M["drap"]], 0.05)
export("Classeur")
