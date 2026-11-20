"""Le Drap (Odette, la lingère) : un grand drap mouillé sur une silhouette trop grande. Version Blender.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_drap.py -- SORTIE.glb DOSSIER_TEXTURES

Pieds (bas du drap) en 0, environ 2,3 m de haut, regard vers l'avant (-Y Blender = +Z Unity).
Noeuds : LeDrap (racine, se déplace sous l'eau) > Drap, Bras, Pinces, Gouttes ; Tete > Visage
(la tête est copiée pour le jumpscare). Style : pastel délavé, visage gribouillé au marqueur à linge.
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

pu.init(argv[1], seed=61)
bpy.ops.wm.read_factory_settings(use_empty=True)

M_DRAP = material("Drap_Mouille", srgb("#DCE6EC"), "tissu", 0.5, seed=62)
M_MARQUEUR = material("Marqueur_A_Linge", srgb("#2D3E78"), None, 0.6)
M_ROSE = material("Pince_Rose", srgb("#E6A9B8"), "feutre", 0.7, seed=63)
M_MENTHE = material("Pince_Menthe", srgb("#A9D2C1"), "feutre", 0.7, seed=63)
M_EAU = material("Gouttes", srgb("#C8D8E0"), None, 0.08)


def obj(name, parent, bm, mats, uv=0.12):
    ob = bpy.data.objects.new(name, bpy.data.meshes.new(name))
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    return finish(ob, bm, mats, uv)


def merge(dst, src):
    me = bpy.data.meshes.new("tmp"); src.to_mesh(me); src.free(); dst.from_mesh(me); bpy.data.meshes.remove(me)


root = bpy.data.objects.new("LeDrap", None)
bpy.context.scene.collection.objects.link(root)

# ------------------------------------------------ le drap : une silhouette trop grande sous le tissu
PROF = [(0.00, 0.78), (0.10, 0.62), (0.30, 0.46), (0.70, 0.36), (1.10, 0.31), (1.45, 0.30), (1.58, 0.33),
        (1.70, 0.24), (1.82, 0.19), (1.95, 0.215), (2.10, 0.20), (2.22, 0.13), (2.30, 0.0)]
SEG, RINGS = 72, 70
bm = bmesh.new()
grid = []
for i in range(RINGS + 1):
    z = 2.30 * i / RINGS
    zs = [p[0] for p in PROF]
    k = max(j for j in range(len(zs)) if zs[j] <= z) if z < 2.30 else len(zs) - 2
    k = min(k, len(PROF) - 2)
    (z0, r0), (z1, r1) = PROF[k], PROF[k + 1]
    r_base = r0 + (r1 - r0) * ((z - z0) / max(1e-6, z1 - z0))
    ring = []
    for j in range(SEG):
        th = 2 * math.pi * j / SEG
        low = max(0.0, 1 - z / 1.2)                                   # plis de plus en plus marqués vers le bas
        fold = 1 + low * (0.16 * math.sin(9 * th + 0.4) + 0.07 * math.sin(17 * th + 1.3)) \
            + 0.05 * noise.noise(Vector((math.cos(th) * 2, math.sin(th) * 2, z * 3)))
        r = r_base * fold
        zz = z
        if i == 0:                                                    # ourlet déchiré, étalé dans l'eau
            zz = 0.02 * noise.noise(Vector((math.cos(th) * 4, math.sin(th) * 4, 1.0)))
        p = Vector((math.cos(th) * r, math.sin(th) * r * 0.85, zz))
        if 1.85 < z < 2.25:                                           # la tête penche, comme L'Écoute qui écoute
            p.x += (z - 1.85) * 0.25
        ring.append(bm.verts.new(p))
    grid.append(ring)
for i in range(RINGS):
    for j in range(SEG):
        bm.faces.new((grid[i][j], grid[i][(j + 1) % SEG], grid[i + 1][(j + 1) % SEG], grid[i + 1][j]))
top = bm.verts.new(Vector((0.1, 0, 2.31)))
for j in range(SEG):
    bm.faces.new((grid[-1][j], grid[-1][(j + 1) % SEG], top))
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.008)
obj("Drap", root, bm, [M_DRAP], 0.07)

# ------------------------------------------------ les bras, trop longs, tendus pour tâtonner (le tissu pend dessous)
bm = bmesh.new()
for sx in (1, -1):
    a, b = Vector((0.30 * sx, 0.0, 1.55)), Vector((0.42 * sx, -0.95, 1.05))
    L = (b - a).length
    tmp = bmesh.new()
    tube(tmp, 0.0, -L, lambda t, ang: 0.075 * (1 - 0.35 * t) * (1 + 0.5 * max(0.0, -math.sin(ang)) * (1 - t)), 20, 16, 0.006, 8, 3 + sx)
    rot = (a - b).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(a) @ rot, verts=tmp.verts)
    merge(bm, tmp)
    # le pan de tissu qui pend sous le bras (fait voile)
    rows = []
    for s in range(12):
        t = s / 11
        c = a.lerp(b, t)
        hang = 0.45 * (1 - t) + 0.12
        rows.append([bm.verts.new(c + Vector((0, 0, -0.03))), bm.verts.new(c + Vector((0.03 * sx, 0.02, -hang - 0.04 * math.sin(t * 9))))])
    for i in range(11):
        bm.faces.new((rows[i][0], rows[i + 1][0], rows[i + 1][1], rows[i][1]))
    # longs doigts qui pointent sous le drap
    for f in range(4):
        tip = b + Vector((sx * (f - 1.5) * 0.03, -0.16 - 0.02 * (f % 2), -0.06))
        merge(bm, curve_mesh([[tuple(b), tuple(b.lerp(tip, 0.5) + Vector((0, 0, 0.02))), tuple(tip)]], 0.014, "doigt"))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)   # tissu à deux faces : jamais noir de dos
obj("Bras", root, bm, [M_DRAP], 0.07)

# ------------------------------------------------ la tête (pivot) et le visage gribouillé au marqueur
tete = bpy.data.objects.new("Tete", None)
bpy.context.scene.collection.objects.link(tete)
tete.parent = root
tete.location = (0.05, 0, 2.0)
HC = Vector((0.0, 0, 2.03))   # centre de la tête AVANT penchement (on_face ajoute le penchement)
bm = bmesh.new()


def on_face(x, z):
    """Point sur l'avant de la tête (le drap la recouvre)."""
    rr = 0.215
    dz = (z - HC.z) / 0.25
    tilt = max(0.0, z - 1.85) * 0.25                     # même penchement que la tête du drap
    y = -math.sqrt(max(0.0, 1 - ((x - HC.x) / rr) ** 2 - dz * dz)) * rr * 0.85 - 0.028
    return Vector((x + tilt, y, z))


for sx in (1, -1):                                                    # yeux : des spirales griffonnées
    pts = []
    for s in range(30):
        t = s / 29 * 3.2 * math.pi
        rr = 0.012 + 0.03 * s / 29
        pts.append(tuple(on_face(HC.x + sx * 0.075 + math.cos(t) * rr, HC.z + 0.05 + math.sin(t) * rr * 1.2)))
    merge(bm, curve_mesh([pts], 0.0035, "oeil"))
mouth = [tuple(on_face(HC.x + x, HC.z - 0.09 + 0.02 * math.sin(x * 60) + 0.25 * x * x)) for x in [(-0.13 + 0.26 * s / 24) for s in range(25)]]
merge(bm, curve_mesh([mouth], 0.004, "bouche"))
for sx in (1, -1):                                                    # traînées de marqueur qui coulent (mouillé)
    merge(bm, curve_mesh([[tuple(on_face(HC.x + sx * 0.075, HC.z + 0.01)), tuple(on_face(HC.x + sx * 0.08, HC.z - 0.06)),
                           tuple(on_face(HC.x + sx * 0.085, HC.z - 0.14))]], 0.0025, "coulure"))
bmesh.ops.translate(bm, vec=-Vector(tete.location), verts=bm.verts)
obj("Visage", tete, bm, [M_MARQUEUR], 0.05)

# ------------------------------------------------ la calotte de tissu de la tête (copiée avec le visage pour le jumpscare)
bm = bmesh.new()
verts = ellipsoid(bm, Vector((0, 0, 2.03)), (0.222, 0.192, 0.262), 0.004, 12, 9, 32, 20)
for v in verts:
    v.co.x += max(0.0, v.co.z - 1.85) * 0.25          # même penchement que le drap
bmesh.ops.translate(bm, vec=-Vector(tete.location), verts=bm.verts)
obj("Tete_Tissu", tete, bm, [M_DRAP], 0.07)

# ------------------------------------------------ le visage de CRI (caché ; montré quand il surgit et pour le jumpscare)
M_TROU = material("Trou_Noir", srgb("#0B0A0F"), None, 1.0)
bm = bmesh.new()


def hole(cx, cz, rx, rz, jag, seed):
    """Un trou noir sur le tissu (bord déchiré), posé sur l'avant de la tête."""
    c = bm.verts.new(on_face(cx, cz) + Vector((0, -0.004, 0)))
    ring = []
    for k in range(24):
        a = 2 * math.pi * k / 24
        j = 1 + jag * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, seed)))
        ring.append(bm.verts.new(on_face(cx + math.cos(a) * rx * j, cz + math.sin(a) * rz * j) + Vector((0, -0.004, 0))))
    for k in range(24):
        bm.faces.new((c, ring[(k + 1) % 24], ring[k]))


hole(HC.x - 0.075, HC.z + 0.06, 0.032, 0.045, 0.25, 1)     # yeux : deux trous qui coulent vers le bas
hole(HC.x + 0.075, HC.z + 0.06, 0.03, 0.05, 0.25, 2)
hole(HC.x, HC.z - 0.10, 0.075, 0.105, 0.35, 3)              # la bouche béante, trop grande
bmesh.ops.translate(bm, vec=-Vector(tete.location), verts=bm.verts)
obj("Visage_Cri", tete, bm, [M_TROU], 0.05)

# ------------------------------------------------ pinces à linge pastel, accrochées au drap
bm_r, bm_m = bmesh.new(), bmesh.new()
for i, (p, n) in enumerate(((Vector((0.1, 0, 2.29)), Vector((0, 0, 1))), (Vector((0.30, -0.05, 1.6)), Vector((0.7, -0.3, 0.6))),
                            (Vector((-0.30, 0.05, 1.6)), Vector((-0.7, 0.3, 0.6))), (Vector((0.16, 0.18, 2.12)), Vector((0.4, 0.8, 0.4))))):
    target = bm_r if i % 2 == 0 else bm_m
    side = n.cross(Vector((0, 1, 0)) if abs(n.y) < 0.9 else Vector((1, 0, 0))).normalized()
    for off in (-0.008, 0.008):
        thin_box(target, p + n.normalized() * 0.03 + side * off, n, side, 0.075, 0.014, 0.012)
obj("Pinces_Roses", root, bm_r, [M_ROSE], 0.05)
obj("Pinces_Menthe", root, bm_m, [M_MENTHE], 0.05)

# ------------------------------------------------ gouttes qui tombent des mains et du bas
bm = bmesh.new()
for sx in (1, -1):
    hand = Vector((0.42 * sx, -1.1, 1.0))
    for k in range(3):
        ellipsoid(bm, hand + Vector((0, 0, -0.06 - k * 0.05)), (0.008, 0.008, 0.014), 0.0, 10, k, 8, 6)
for k in range(10):
    a = 2 * math.pi * k / 10 + 0.3
    ellipsoid(bm, Vector((math.cos(a) * 0.66, math.sin(a) * 0.56, 0.06)), (0.01, 0.01, 0.02), 0.0, 10, k, 8, 6)
obj("Gouttes", root, bm, [M_EAU], 0.05)

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True, export_cameras=False,
                          export_lights=False, export_extras=False)
pu.strip_scene_name(OUT)
print("EXPORT ->", OUT, sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH"), "faces")
