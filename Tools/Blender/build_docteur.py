"""Le Docteur : le médecin de l'Institut, celui qui « testait » les enfants au sous-sol. Version Blender.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_docteur.py -- SORTIE.glb DOSSIER_TEXTURES

Grande marionnette (2,5 m) dans la DA du jeu (tissu, feutre, coutures) : long manteau prune rapiécé, capuche,
masque à bec de corbeau en cuir crème cousu, deux verres ronds rouge sombre qui luisent à peine, bras trop longs,
doigts fins. Il tient une lanterne au verre verdâtre : c'est avec elle qu'il voit (DoctorAI).
Pieds en 0, regard vers l'avant (-Y Blender = +Z Unity).
Noeuds utiles : LeDocteur > Manteau, Epaule_G/D > Bras_G/D, Main_D > Lanterne > Lanterne_Verre ;
Tete > Crane, Capuche, Masque (visé par le screamer), Yeux.
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

pu.init(argv[1], seed=91)
bpy.ops.wm.read_factory_settings(use_empty=True)

M_MANTEAU = material("Manteau_Prune", srgb("#3E2B3D"), "feutre", 0.85, seed=92)
M_RAPIECE = material("Rapiece_Lilas", srgb("#8E7AA6"), "tissu", 0.9, seed=93)
M_CUIR = material("Masque_Cuir", srgb("#E4D6BC"), "tissu", 0.7, seed=94)
M_FIL = material("Fil_Noir", srgb("#1E1A22"), None, 0.8)
M_VERRE = material("Verres_Rouges", srgb("#4A1218"), None, 0.08)
M_VERRE.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = srgb("#B0303A")
M_VERRE.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.6
M_LAITON = material("Laiton_Lanterne", srgb("#9C8456"), None, 0.35)
M_LAITON.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.85
M_LUEUR = material("Lueur_Lanterne", srgb("#D8F0A8"), None, 0.2)
M_LUEUR.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = srgb("#C8F090")
M_LUEUR.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 4.0
M_GANT = material("Gants", srgb("#2A2230"), "feutre", 0.6, seed=95)


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
    """Un tube de a vers b (tube() fabrique le long de Z, on l'oriente)."""
    tmp = bmesh.new()
    L = (b - a).length
    tube(tmp, 0.0, L, radius_fn, segs, rings, amp, freq, seed, cap0, cap1)
    q = (b - a).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(a) @ q, verts=tmp.verts)
    me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)


root = bpy.data.objects.new("LeDocteur", None)
bpy.context.scene.collection.objects.link(root)

# ------------------------------------------------ le manteau : long, évasé, ourlet effiloché, un peu de travers
def coat_r(t, a):
    z = t * 1.78
    if z < 1.05:
        r = 0.44 - 0.21 * (z / 1.05) ** 0.8
    elif z < 1.58:
        r = 0.23 + 0.05 * ((z - 1.05) / 0.53)
    else:
        r = 0.28 - 0.2 * ((z - 1.58) / 0.2) ** 1.5
    fray = 0.03 * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, 0.5))) if z < 0.12 else 0.0
    pleats = (0.02 * math.sin(11 * a) * max(0.0, 1 - z / 1.0))
    return max(0.05, r + fray + pleats)


bm = bmesh.new()
tube(bm, 0.03, 1.78, coat_r, 40, 34, 0.006, 6, 2, cap0=False, cap1=True)
for v in bm.verts:
    v.co.y *= 0.82
    v.co.x += 0.03 * (v.co.z / 1.78) ** 2            # il penche, comme quelqu'un qui se courbe vers un lit
add("Manteau", root, bm, [M_MANTEAU], 0.15)

bm = bmesh.new()                                      # pièces rapiécées, cousues de travers
for (x, z, s) in ((0.2, 0.55, 0.11), (-0.16, 1.25, 0.08), (0.05, 0.25, 0.09)):
    thin_box(bm, Vector((x, -0.36 + z * 0.12, z)), Vector((1, 0, 0.2)), Vector((0, -1, 0.15)), s, s * 1.2, 0.01)
add("Rapieces", root, bm, [M_RAPIECE], 0.05)
bm = bmesh.new()
for k in range(6):                                    # boutons de manteau
    z = 1.5 - k * 0.16
    disc(bm, Vector((0.02, -0.2 - 0.02 * (1.5 - z), z)), Vector((0, -1, 0.1)), 0.018, 0.012, 10)
add("Boutons", root, bm, [M_LAITON], 0.03)
bm = bmesh.new()
for x in (-0.12, 0.12):                               # bouts de bottines qui dépassent
    ellipsoid(bm, Vector((x, -0.3, 0.04)), (0.07, 0.13, 0.05), 0.002, 20, 3, 14, 8)
add("Bottines", root, bm, [M_GANT], 0.05)

# ------------------------------------------------ bras trop longs, mains gantées aux doigts fins
for side, sx in (("G", -1), ("D", 1)):
    sh = Vector((sx * 0.27, -0.02, 1.62))
    ep = empty("Epaule_" + side, root, sh)
    hand = Vector((sx * 0.36, -0.16, 0.78))
    elbow = Vector((sx * 0.4, 0.06, 1.17))             # coude en arrière et vers l'extérieur : une posture de mante
    bm = bmesh.new()
    tube_between(bm, sh, elbow, lambda t, a: 0.075 - 0.01 * t + 0.008 * math.sin(7 * a), 14, 10, 0.004, 15, 4)
    ellipsoid(bm, elbow, (0.07, 0.07, 0.07), 0.003, 15, 4, 12, 8)
    tube_between(bm, elbow, hand, lambda t, a: 0.066 - 0.022 * t + 0.006 * math.sin(7 * a), 14, 10, 0.004, 15, 5)
    add("Bras_" + side, ep, bm, [M_MANTEAU], 0.1)
    mn = empty("Main_" + side, ep, hand)
    bm = bmesh.new()
    ellipsoid(bm, hand + Vector((0, 0, -0.04)), (0.045, 0.03, 0.055), 0.002, 20, 5, 12, 8)
    add("Paume_" + side, mn, bm, [M_GANT], 0.05)
    fingers = []
    for f in range(4):
        base = hand + Vector((sx * (-0.025 + f * 0.017), -0.01, -0.08))
        fingers.append([tuple(base), tuple(base + Vector((sx * 0.01, -0.02, -0.07))),
                        tuple(base + Vector((sx * 0.012, -0.05, -0.15))), tuple(base + Vector((sx * 0.005, -0.075, -0.19)))])
    add("Doigts_" + side, mn, curve_mesh(fingers, 0.007, "doigts", res=3, bevel_res=1), [M_GANT], 0.04)

    if side == "D":                                   # la lanterne, tenue par l'anneau
        lt = empty("Lanterne", mn, hand + Vector((0.0, -0.04, -0.3)))
        c = hand + Vector((0.0, -0.04, -0.34))
        bm = bmesh.new()
        disc(bm, c + Vector((0, 0, -0.1)), Vector((0, 0, 1)), 0.075, 0.02, 12)
        disc(bm, c + Vector((0, 0, 0.1)), Vector((0, 0, 1)), 0.06, 0.03, 12)
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            p = c + Vector((math.cos(a) * 0.068, math.sin(a) * 0.068, 0))
            thin_box(bm, p, Vector((0, 0, 1)), Vector((math.cos(a), math.sin(a), 0)), 0.2, 0.012, 0.012)
        add("Lanterne_Cage", lt, bm, [M_LAITON], 0.04)
        add("Lanterne_Anse", lt, curve_mesh([[tuple(c + Vector((-0.04, 0, 0.12))), tuple(c + Vector((0, 0, 0.2))),
                                              tuple(c + Vector((0.04, 0, 0.12)))]], 0.006, "anse", res=3, bevel_res=1), [M_LAITON], 0.04)
        bm = bmesh.new()
        tube_between(bm, c + Vector((0, 0, -0.09)), c + Vector((0, 0, 0.09)), lambda t, a: 0.052, 12, 2)
        add("Lanterne_Verre", lt, bm, [M_LUEUR], 0.05)

# ------------------------------------------------ la tête : capuche, crâne, masque à bec cousu, verres rouges
bm = bmesh.new()                                      # col montant : la tête ne flotte plus au-dessus du manteau
tube(bm, 1.68, 1.9, lambda t, a: 0.115 - 0.02 * t + 0.006 * math.sin(9 * a), 22, 6, 0.003, 10, 6, cap0=False, cap1=False)
for v in bm.verts:
    v.co.x += 0.04 * ((v.co.z - 1.68) / 0.22)
add("Col", root, bm, [M_MANTEAU], 0.08)

tete = empty("Tete", root, Vector((0.05, -0.02, 1.8)))
bm = bmesh.new()
ellipsoid(bm, Vector((0.05, 0.0, 1.97)), (0.13, 0.14, 0.15), 0.004, 12, 6, 20, 14)
add("Crane", tete, bm, [M_MANTEAU], 0.08)
bm = bmesh.new()
ellipsoid(bm, Vector((0.05, 0.035, 1.99)), (0.155, 0.15, 0.17), 0.012, 8, 7, 22, 14)
for v in bm.verts:                                    # capuche ouverte devant (on aplatit la face avant)
    if v.co.y < -0.06:
        v.co.y = -0.06 + (v.co.y + 0.06) * 0.2
add("Capuche", tete, bm, [M_MANTEAU], 0.1)

bm = bmesh.new()
ellipsoid(bm, Vector((0.05, -0.115, 1.96)), (0.12, 0.045, 0.125), 0.003, 15, 8, 20, 12)     # la plaque du visage


def beak_r(t, a):
    return max(0.006, 0.085 * (1 - t) ** 1.25 + 0.004 * math.sin(5 * a))


BEAK_DIR = Vector((0, -0.93, -0.36)).normalized()
BEAK_BASE = Vector((0.05, -0.15, 1.92))
tmp = bmesh.new()
tube(tmp, 0.0, 0.46, beak_r, 18, 16, 0.002, 12, 9, cap0=True, cap1=True)
for v in tmp.verts:                                   # le bec se courbe vers le bas, comme un vrai bec de corbeau
    t = v.co.z / 0.46
    v.co.y += -0.06 * t * t
q = BEAK_DIR.to_track_quat("Z", "Y").to_matrix().to_4x4()
bmesh.ops.transform(tmp, matrix=Matrix.Translation(BEAK_BASE) @ q, verts=tmp.verts)
me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); tmp.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
add("Masque", tete, bm, [M_CUIR], 0.06)

bm = bmesh.new()                                      # coutures sur le dessus du bec (même repère que le bec)
for k in range(8):
    t = 0.06 + k * 0.1
    local = Vector((0.0, beak_r(t, math.pi / 2) * 0.98 - 0.06 * t * t, t * 0.46))
    p = Matrix.Translation(BEAK_BASE) @ q @ local
    nrm = (q.to_3x3() @ Vector((0, 1, 0))).normalized()
    stitch_x(bm, p, nrm, (q.to_3x3() @ Vector((0, 0, 1))).normalized(), 0.022)
add("Coutures_Bec", tete, bm, [M_FIL], 0.03)

bm = bmesh.new()                                      # chapeau à large bord, de travers (mince : le crâne reste la plus grosse pièce)
disc(bm, Vector((0.05, 0.02, 2.1)), Vector((0.12, 0.05, 1)).normalized(), 0.27, 0.018, 28, 0.06, 3)
tube(bm, 0.0, 0.13, lambda t, a: 0.13 - 0.015 * t + 0.004 * math.sin(6 * a), 20, 4, 0.003, 10, 8, cap0=False, cap1=True)
for v in bm.verts:
    if v.co.z < 0.2:                                  # la calotte (construite en 0) remonte sur le bord
        v.co = Vector((v.co.x + 0.05 + 0.12 * v.co.z, v.co.y + 0.02, v.co.z + 2.1))
add("Chapeau", tete, bm, [M_MANTEAU], 0.1)
bm = bmesh.new()
for x in (-0.002, 0.102):
    disc(bm, Vector((x, -0.158, 1.995)), Vector((0, -1, 0.05)), 0.036, 0.02, 18)
add("Yeux", tete, bm, [M_VERRE], 0.04)
bm = bmesh.new()
for x in (-0.002, 0.102):
    tube_between(bm, Vector((x, -0.15, 1.995)), Vector((x, -0.172, 1.995)), lambda t, a: 0.042, 18, 2, cap0=False, cap1=False)
add("Cerclages", tete, bm, [M_LAITON], 0.04)

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True, export_cameras=False,
                          export_lights=False, export_extras=False)
pu.strip_scene_name(OUT)
n = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
print("EXPORT ->", OUT, ":", n, "faces")
