"""La Veilleuse : la surveillante de nuit de l'Institut, celle qui éteignait les lumières à 21 h. Version Blender.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_veilleuse.py -- SORTIE.glb DOSSIER_TEXTURES

Grande marionnette (2,45 m) dans la DA du jeu (tissu, feutre, coutures) : longue chemise de nuit en lin jauni,
bras interminables, doigts gris. Sa tête est cachée sous un vieil ABAT-JOUR rose fané à franges : de dessous, on ne
voit que deux petits yeux pâles. Elle tient un ÉTEIGNOIR (une cloche de laiton au bout d'une longue tige) : c'est
avec lui qu'elle « souffle » les ampoules du grenier (VeilleuseAI).
Pieds en 0, regard vers l'avant (-Y Blender = +Z Unity).
Noeuds utiles : LaVeilleuse > Robe, Epaule_G/D > Bras_G/D, Main_G/D, Main_D > Eteignoir ;
Tete > Visage (visé par le screamer), Yeux, Abat_Jour, Franges.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
OUT = argv[0]

from poupee_utils import *  # noqa: E402,F401,F403
import poupee_utils as pu  # noqa: E402

pu.init(argv[1], seed=131)
bpy.ops.wm.read_factory_settings(use_empty=True)

M_LIN = material("Robe_Lin_Jauni", srgb("#CFC3A6"), "tissu", 0.9, seed=132)
M_TACHE = material("Robe_Taches", srgb("#9E8F72"), "tissu", 0.95, seed=133)
M_PEAU = material("Peau_Grise", srgb("#7E7A78"), "feutre", 0.8, seed=134)
M_ABAT = material("Abat_Jour_Rose", srgb("#A87D80"), "tissu", 0.85, seed=135)
M_FRANGE = material("Franges", srgb("#6E4A52"), None, 0.9)
M_VISAGE = material("Visage_Ombre", srgb("#1A1418"), "feutre", 0.9, seed=136)
M_YEUX = material("Yeux_Pales", srgb("#E8E2D0"), None, 0.3)
M_YEUX.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = srgb("#F2ECD8")
M_YEUX.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 1.2
M_LAITON = material("Laiton_Eteignoir", srgb("#8F7A4E"), None, 0.35)
M_LAITON.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.85
M_FIL = material("Fil_Noir", srgb("#1E1A22"), None, 0.8)


def empty(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    e.parent = parent
    e.location = loc - (parent.matrix_world.translation if parent else Vector())
    bpy.context.view_layer.update()
    return e


def add(name, parent, bm, mats, uv=0.1):
    return new_object(name, parent, bm, mats, uv)


def tube_between(bm, a, b, radius_fn, segs=14, rings=10, amp=0.0, freq=20.0, seed=0, cap0=True, cap1=True):
    tmp = bmesh.new()
    L = (b - a).length
    tube(tmp, 0.0, L, radius_fn, segs, rings, amp, freq, seed, cap0, cap1)
    q = (b - a).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(a) @ q, verts=tmp.verts)
    me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


root = bpy.data.objects.new("LaVeilleuse", None)
bpy.context.scene.collection.objects.link(root)

# ------------------------------------------------ la chemise de nuit : longue, étroite en haut, ourlet qui traîne
def robe_r(t, a):
    z = t * 1.92
    if z < 1.2:
        r = 0.36 - 0.2 * (z / 1.2) ** 0.7
    else:
        r = 0.16 + 0.03 * math.sin((z - 1.2) / 0.72 * math.pi) - 0.08 * ((z - 1.2) / 0.72) ** 2
    hem = 0.035 * noise.noise(Vector((math.cos(a) * 4, math.sin(a) * 4, 0.3))) if z < 0.15 else 0.0
    plis = 0.018 * math.sin(13 * a) * max(0.0, 1 - z / 1.3)
    return max(0.05, r + hem + plis)


bm = bmesh.new()
tube(bm, 0.0, 1.92, robe_r, 40, 36, 0.005, 7, 3, cap0=False, cap1=True)
for v in bm.verts:
    v.co.y *= 0.8
    v.co.y -= 0.06 * (v.co.z / 1.92) ** 2           # elle se penche en avant, vers un lit
add("Robe", root, bm, [M_LIN], 0.15)

bm = bmesh.new()                                     # taches de cire et d'encre sur le lin
for (x, z, s) in ((0.16, 0.5, 0.1), (-0.1, 1.1, 0.07), (0.04, 0.25, 0.08), (-0.18, 0.7, 0.06)):
    thin_box(bm, Vector((x, -0.3 + z * 0.1, z)), Vector((1, 0, 0.15)), Vector((0, -1, 0.1)), s, s * 1.3, 0.008)
add("Taches", root, bm, [M_TACHE], 0.05)

# ------------------------------------------------ cou interminable et tête sous l'abat-jour
tete = empty("Tete", root, Vector((0.0, -0.14, 2.1)))
bm = bmesh.new()
tube_between(bm, Vector((0, -0.06, 1.88)), Vector((0, -0.13, 2.08)), lambda t, a: 0.045 - 0.01 * t, 12, 6, 0.002, 12, 5)
add("Cou", root, bm, [M_PEAU], 0.05)
bm = bmesh.new()
ellipsoid(bm, Vector((0, -0.15, 2.12)), (0.11, 0.1, 0.13), 0.004, 10, 6, 20, 14)   # le visage : dans l'ombre
add("Visage", tete, bm, [M_VISAGE], 0.06)
bm = bmesh.new()
for x in (-0.035, 0.035):
    ellipsoid(bm, Vector((x, -0.245, 2.11)), (0.012, 0.006, 0.009), 0.0, 1, 0, 10, 6)
add("Yeux", tete, bm, [M_YEUX], 0.03)


def abat_r(t, a):
    return 0.16 + 0.17 * t + 0.008 * math.sin(18 * a)   # plissé


bm = bmesh.new()
tube(bm, 0.0, 0.36, abat_r, 36, 6, 0.003, 8, 6, cap0=False, cap1=False)
for v in bm.verts:                                   # renversé (large en bas) et de travers
    z = v.co.z
    v.co.z = -z
q = Matrix.Rotation(math.radians(9), 4, "X") @ Matrix.Rotation(math.radians(-6), 4, "Y")
bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector((0.0, -0.15, 2.36))) @ q, verts=bm.verts)
add("Abat_Jour", tete, bm, [M_ABAT], 0.08)
bm = bmesh.new()                                     # franges qui pendent tout autour
for k in range(26):
    a = 2 * math.pi * k / 26
    p = Matrix.Translation(Vector((0.0, -0.15, 2.36))) @ q @ Vector((math.cos(a) * 0.335, math.sin(a) * 0.335, -0.36))
    tube_between(bm, p, p + Vector((0, 0, -0.07 - 0.02 * math.sin(3 * a))), lambda t, aa: 0.006, 5, 2)
add("Franges", tete, bm, [M_FRANGE], 0.03)

# ------------------------------------------------ bras trop longs, doigts gris
for side, sx in (("G", -1), ("D", 1)):
    sh = Vector((sx * 0.19, -0.04, 1.84))
    ep = empty("Epaule_" + side, root, sh)
    elbow = Vector((sx * 0.3, 0.04, 1.28))
    hand = Vector((sx * 0.3, -0.12, 0.7)) if side == "G" else Vector((sx * 0.28, -0.22, 1.0))
    bm = bmesh.new()
    tube_between(bm, sh, elbow, lambda t, a: 0.06 - 0.008 * t + 0.006 * math.sin(7 * a), 14, 10, 0.004, 15, 4)
    ellipsoid(bm, elbow, (0.055, 0.055, 0.055), 0.003, 15, 4, 12, 8)
    tube_between(bm, elbow, hand, lambda t, a: 0.05 - 0.016 * t + 0.005 * math.sin(7 * a), 14, 10, 0.004, 15, 5)
    add("Bras_" + side, ep, bm, [M_LIN], 0.1)
    mn = empty("Main_" + side, ep, hand)
    bm = bmesh.new()
    ellipsoid(bm, hand + Vector((0, 0, -0.035)), (0.04, 0.026, 0.05), 0.002, 20, 5, 12, 8)
    add("Paume_" + side, mn, bm, [M_PEAU], 0.05)
    fingers = []
    for f in range(4):
        base = hand + Vector((sx * (-0.022 + f * 0.015), -0.01, -0.075))
        fingers.append([tuple(base), tuple(base + Vector((sx * 0.01, -0.02, -0.08))),
                        tuple(base + Vector((sx * 0.012, -0.05, -0.17))), tuple(base + Vector((sx * 0.004, -0.08, -0.22)))])
    add("Doigts_" + side, mn, curve_mesh(fingers, 0.006, "doigts", res=3, bevel_res=1), [M_PEAU], 0.04)

    if side == "D":                                  # l'éteignoir, tenu comme un bâton : la cloche en l'air
        et = empty("Eteignoir", mn, hand)
        bm = bmesh.new()
        tube_between(bm, hand + Vector((0, 0, -0.7)), hand + Vector((0, -0.02, 0.85)), lambda t, a: 0.011, 10, 6)
        tip = hand + Vector((0, -0.02, 0.85))
        tube_between(bm, tip, tip + Vector((0, -0.12, 0.02)), lambda t, a: 0.009, 8, 3)
        cloche = tip + Vector((0, -0.14, -0.02))
        add("Tige_Eteignoir", et, bm, [M_LAITON], 0.05)
        bm = bmesh.new()
        tube(bm, 0.0, 0.11, lambda t, a: 0.055 - 0.045 * t, 18, 5, 0.0, 1, 0, cap0=False, cap1=True)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(cloche - Vector((0, 0, 0.1))), verts=bm.verts)
        add("Cloche_Eteignoir", et, bm, [M_LAITON], 0.05)

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True, export_cameras=False,
                          export_lights=False, export_extras=False)
pu.strip_scene_name(OUT)
n = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
print("EXPORT ->", OUT, ":", n, "faces")
