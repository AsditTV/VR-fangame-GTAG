"""Objets de la Laverie (DA pastel fait main), version Blender.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_laverie_props.py -- DOSSIER_SORTIE DOSSIER_TEXTURES

Sortie (unités de la maquette, avant WORLD_SCALE ; base posée en 0, avant = +Z Unity) :
  Machine.glb (0,84 x 0,95 x 0,8), SecheLinge.glb (0,8 x 0,9 x 0,76), Table.glb (6,2 m), DrapPendu.glb,
  et laverie_carrelage.png / laverie_mur.png (textures dessinées à la main).
La matière « Email » est claire : InstitutBuilder la teinte (menthe, rose, crème, lilas) machine par machine.
Rien n'est parfaitement carré : arêtes arrondies, tôle cabossée, plateau gondolé.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
OUTDIR = argv[0]
os.makedirs(OUTDIR, exist_ok=True)

from poupee_utils import *  # noqa: E402,F401,F403
import poupee_utils as pu  # noqa: E402

pu.init(argv[1], seed=71)


def start(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    pu._tex_cache.clear()
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    return root


def part(root, name, bm, mats, uv=0.15):
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


def rbox(dst, cx, cy, z0, w, d, h, r, dent=0.0, seed=0):
    """Boîte aux arêtes arrondies (Blender : x largeur, y profondeur, z hauteur), un peu cabossée.
    Construite à part puis fusionnée (sinon on subdiviserait aussi ce qui est déjà dans dst)."""
    bm = bmesh.new()
    ret = bmesh.ops.create_cube(bm, size=1.0)
    vs = ret["verts"]
    bmesh.ops.scale(bm, vec=Vector((w, d, h)), verts=vs)
    bmesh.ops.translate(bm, vec=Vector((cx, cy, z0 + h / 2)), verts=vs)
    edges = list({e for v in vs for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=vs + edges, offset=r, segments=3, affect="EDGES")
    sub = list({e for v in bm.verts for e in v.link_edges})
    bmesh.ops.subdivide_edges(bm, edges=sub, cuts=2, use_grid_fill=True)
    bm.normal_update()
    if dent:
        for v in bm.verts:
            v.co += v.normal * dent * noise.noise(v.co * 6 + Vector((seed, 1, 2))) if v.normal.length > 0 else Vector()
    bm.normal_update()
    me = bpy.data.meshes.new("tmp"); bm.to_mesh(me); bm.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)


def merge(dst, src):
    me = bpy.data.meshes.new("tmp"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)


# ================================================================== textures de la salle (carrelage et mur)
def tiles_png(name, base, grout, n=4, size=1024, seed=1):
    y, x = np.mgrid[0:size, 0:size] / size
    wob = (pu.tile_noise(size, [(3, 1.0), (7, 0.4)], seed) - 0.5) * 0.012          # joints tremblés (faits main)
    gx = np.abs(((x + wob) * n) % 1.0 - 0.5)
    gy = np.abs(((y - wob) * n) % 1.0 - 0.5)
    joint = np.clip((np.maximum(gx, gy) - 0.47) / 0.02, 0, 1)
    tile_id = (np.floor((x + wob) * n) * 7 + np.floor((y - wob) * n) * 13) % 5
    vary = 0.94 + 0.03 * tile_id + 0.05 * (pu.tile_noise(size, [(2, 1.0), (6, 0.5)], seed + 3) - 0.5)
    stain = np.clip((pu.tile_noise(size, [(1, 1.0), (3, 0.6)], seed + 5) - 0.6) / 0.4, 0, 1)   # taches d'humidité
    col = np.zeros((size, size, 4))
    for i in range(3):
        c = base[i] * vary * (1 - 0.25 * stain)
        col[..., i] = c * (1 - joint) + grout[i] * joint
    col[..., 3] = 1
    pu.save_rgba(name, col)


tiles_png("laverie_carrelage", (0.76, 0.86, 0.82), (0.32, 0.36, 0.36), 4, seed=11)
tiles_png("laverie_mur", (0.80, 0.84, 0.88), (0.42, 0.46, 0.5), 6, seed=12)
import shutil  # noqa: E402
for f in ("laverie_carrelage.png", "laverie_mur.png"):
    shutil.copy(os.path.join(pu.TEXDIR, f), os.path.join(OUTDIR, f))

# ================================================================== machine à laver
root = start("Machine")
M_EMAIL = material("Email", srgb("#F2F0EA"), "feutre", 0.35, seed=72)
M_CHROME = material("Chrome", srgb("#C9CCD2"), None, 0.2)
M_CHROME.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 1.0
M_VITRE = material("Vitre", srgb("#1E2A2E"), None, 0.05)
M_EAU = material("Eau_Trouble", srgb("#7C8F8C"), None, 0.1)
M_BOUTON = material("Boutons", srgb("#3A3240"), None, 0.4)
W, D, H = 0.84, 0.8, 0.95
bm = bmesh.new()
rbox(bm, 0, 0, 0, W, D, H, 0.05, 0.004, 1)
part(root, "Corps", bm, [M_EMAIL], 0.3)
bm = bmesh.new()                                   # bandeau de commandes + deux boutons
rbox(bm, 0, 0.25, H, W * 0.95, 0.22, 0.12, 0.03)
part(root, "Bandeau", bm, [M_EMAIL], 0.3)
bm = bmesh.new()
for x in (-0.22, 0.18):
    disc(bm, Vector((x, 0.13, H + 0.06)), Vector((0, -1, 0)), 0.035, 0.03, 14)
part(root, "Boutons", bm, [M_BOUTON], 0.05)
ring = [(math.cos(a) * 0.235, -D / 2 - 0.012, 0.5 + math.sin(a) * 0.235) for a in [2 * math.pi * k / 40 for k in range(41)]]
part(root, "Hublot_Cercle", curve_mesh([ring], 0.03, "cercle"), [M_CHROME], 0.05)
bm = bmesh.new()
disc(bm, Vector((0, -D / 2 - 0.004, 0.5)), Vector((0, -1, 0)), 0.215, 0.01, 32)
part(root, "Hublot_Vitre", bm, [M_VITRE], 0.05)
bm = bmesh.new()                                   # l'eau trouble à mi-hauteur derrière la vitre
disc(bm, Vector((0, -D / 2 + 0.01, 0.43)), Vector((0, -1, 0)), 0.2, 0.004, 24)
part(root, "Hublot_Eau", bm, [M_EAU], 0.05)
export("Machine")

# ================================================================== sèche-linge
root = start("SecheLinge")
M_EMAIL = material("Email", srgb("#F2F0EA"), "feutre", 0.35, seed=73)
M_CHROME = material("Chrome", srgb("#C9CCD2"), None, 0.2)
M_CHROME.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 1.0
M_VITRE = material("Vitre", srgb("#2B2530"), None, 0.05)
W, D, H = 0.8, 0.76, 0.9
bm = bmesh.new()
rbox(bm, 0, 0, 0, W, D, H, 0.05, 0.004, 2)
part(root, "Corps", bm, [M_EMAIL], 0.3)
bm = bmesh.new()                                   # porte carrée aux coins très arrondis
rbox(bm, 0, -D / 2 - 0.015, 0.22, 0.5, 0.03, 0.46, 0.1)
part(root, "Porte", bm, [M_CHROME], 0.1)
bm = bmesh.new()
rbox(bm, 0, -D / 2 - 0.032, 0.27, 0.4, 0.01, 0.36, 0.08)
part(root, "Porte_Vitre", bm, [M_VITRE], 0.1)
export("SecheLinge")

# ================================================================== table à plier (plateau gondolé, pieds tordus, linge plié)
root = start("Table")
M_BOIS = material("Bois_Table", srgb("#B79A86"), "feutre", 0.8, seed=74)
M_METAL = material("Pieds", srgb("#7E8C88"), None, 0.5)
M_LINGE = [material("Linge_" + n, srgb(c), "tissu", 0.9, seed=75) for n, c in (("Rose", "#E8C2CB"), ("Menthe", "#C2DCCD"), ("Creme", "#F1E8D6"))]
bm = bmesh.new()
rbox(bm, 0, 0, 0.82, 6.2, 1.1, 0.08, 0.02)
for v in bm.verts:                                  # plateau gondolé par l'humidité
    v.co.z += 0.012 * math.sin(v.co.x * 1.7) * math.cos(v.co.y * 2.0)
part(root, "Plateau", bm, [M_BOIS], 0.4)
legs = []
for x in (-2.9, 0.0, 2.9):
    for y in (-0.45, 0.45):
        legs.append([(x, y, 0.82), (x + 0.02 * (1 if y > 0 else -1), y, 0.4), (x - 0.01, y, 0.0)])
part(root, "Pieds", curve_mesh(legs, 0.035, "pieds"), [M_METAL], 0.1)
for i, (x, n) in enumerate(((-2.2, 4), (-0.6, 2), (1.4, 5), (2.5, 3))):
    bm = bmesh.new()
    for k in range(n):
        rbox(bm, x + 0.02 * k, 0.05 * (k % 2), 0.9 + k * 0.06, 0.42, 0.34, 0.055, 0.02, 0.003, k)
    part(root, f"Linge_{i}", bm, [M_LINGE[i % 3]], 0.1)
export("Table")

# ================================================================== drap pendu (plis, bas ondulé, pinces)
root = start("DrapPendu")
M_DRAP = material("Drap_Pendu", srgb("#E9ECEE"), "tissu", 0.85, seed=76)
M_PINCE = material("Pinces", srgb("#E6A9B8"), "feutre", 0.7, seed=77)
bm = bmesh.new()
COLS, ROWS = 36, 20
grid = []
for i in range(ROWS + 1):
    t = i / ROWS
    row = []
    for j in range(COLS + 1):
        u = j / COLS
        y = (u - 0.5) * 1.1
        z = 1.17 - t * 1.17 + (0.04 * math.sin(u * 9 + 1) * t if i == ROWS else 0)
        x = 0.05 * math.sin(u * 14 + t * 2) * t + 0.015 * noise.noise(Vector((u * 5, t * 5, 3)))
        row.append(bm.verts.new(Vector((x, y, z))))
    grid.append(row)
for i in range(ROWS):
    for j in range(COLS):
        bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
part(root, "Drap", bm, [M_DRAP], 0.25)
bm = bmesh.new()
for y in (-0.42, 0.0, 0.43):
    for off in (-0.007, 0.007):
        thin_box(bm, Vector((off, y, 1.17)), Vector((0, 0, 1)), Vector((1, 0, 0)), 0.07, 0.014, 0.012)
part(root, "Pinces", bm, [M_PINCE], 0.05)
export("DrapPendu")
