"""Les Doux (mascottes du théâtre), version Blender : un .glb par animal (lapin, ours, chat, chien).

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_doux.py -- DOSSIER_SORTIE DOSSIER_TEXTURES

Sortie : DOSSIER_SORTIE/Doux_Lapin.glb, Doux_Ours.glb, Doux_Chat.glb, Doux_Chien.glb

Mêmes proportions que les Doux de la maquette (pieds en 0, centre de la tête à 1,70 m, regard vers
l'avant), pour que InstitutBuilder les pose à la place des anciens.
Hiérarchie (les scripts s'en servent) :
  Doux_<Animal>            pivot aux pieds (le Doux qui marche se déplace avec)
    Corps, Corps_Creme, Coutures, Fermeture
    Tete                   pivot au cou (SoftieWatcher tourne la tête autour de ce point)
      Tete_Fourrure, Tete_Creme, Tete_Details
La matière « Fourrure » est claire et neutre : InstitutBuilder la teinte (rose, menthe, lilas...).
Détails : yeux-boutons dépareillés (l'un pend à son fil), sourire cousu trop grand, fermeture éclair
entrouverte dans le dos (du noir dedans), coutures qui lâchent avec du rembourrage.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
OUTDIR = argv[0]
os.makedirs(OUTDIR, exist_ok=True)

from poupee_utils import *  # noqa: E402,F401,F403
import poupee_utils as pu  # noqa: E402

pu.init(argv[1], seed=23)

FRONT = Vector((0, -1, 0))          # l'avant dans Blender (= +Z glTF = avant dans Unity)
HY, NECK = 1.70, 1.40               # centre de la tête, base du cou


def empty(name, parent, loc):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.location = loc
    return ob


def merge(bm_dst, bm_src):
    me = bpy.data.meshes.new("tmp")
    bm_src.to_mesh(me); bm_src.free()
    bm_dst.from_mesh(me)
    bpy.data.meshes.remove(me)


def limb(bm, a, b, r0, r1, seed, segs=18, rings=10, amp=0.012):
    """Boudin rembourré de a à b (coordonnées monde)."""
    a, b = Vector(a), Vector(b)
    L = (b - a).length
    tmp = bmesh.new()
    tube(tmp, 0.0, -L, lambda t, ang: (r0 + (r1 - r0) * t) * (1 + 0.06 * math.sin(math.pi * t)), segs, rings, amp, 9, seed)
    rot = (a - b).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(a) @ rot, verts=tmp.verts)
    merge(bm, tmp)


def head_part(name, tete, bm, mats, uv):
    """Pièce de la tête : construite en coordonnées monde, ramenée dans le repère du cou (pivot à NECK)."""
    bmesh.ops.translate(bm, vec=Vector((0, 0, -NECK)), verts=bm.verts)
    ob = bpy.data.objects.new(name, bpy.data.meshes.new(name))
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = tete
    return finish(ob, bm, mats, uv)


def build(kind, seed):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    pu._tex_cache.clear()
    M_FUR = material("Fourrure", srgb("#F4F0EA"), "feutre", 1.0, seed=31)
    M_CREME = material("Creme", srgb("#F1E6D2"), "feutre", 1.0, seed=32)
    M_NOIR = material("Fil_Noir", srgb("#221C26"), None, 0.6)
    M_BOUTON_A = material("Bouton_Rose", srgb("#C9868F"), None, 0.3)
    M_BOUTON_B = material("Bouton_Noir", srgb("#1A161D"), None, 0.25)
    M_REMB = material("Rembourrage", srgb("#F3EEE6"), "feutre", 1.0, seed=33)

    root = empty("Doux_" + kind.capitalize(), None, (0, 0, 0))
    rs = pu.rng

    # ------------------------------------------------ corps : poire rembourrée, jambes courtes, bras qui pendent
    bm = bmesh.new()
    ellipsoid(bm, Vector((0, 0, 0.98)), (0.34, 0.28, 0.43), 0.025, 4.5, seed, 40, 28)
    for sx in (1, -1):
        limb(bm, (0.14 * sx, 0, 0.62), (0.15 * sx, -0.01, 0.12), 0.125, 0.115, seed + 1)
        ellipsoid(bm, Vector((0.15 * sx, -0.05, 0.07)), (0.12, 0.17, 0.08), 0.01, 8, seed + 2, 20, 12)
        limb(bm, (0.30 * sx, 0, 1.24), (0.46 * sx, -0.07, 0.79), 0.095, 0.085, seed + 3)
        ellipsoid(bm, Vector((0.475 * sx, -0.08, 0.72)), (0.11, 0.10, 0.11), 0.012, 9, seed + 4, 18, 12)
    limb(bm, (0, 0, NECK + 0.06), (0, 0, NECK - 0.06), 0.16, 0.165, seed + 5, 20, 4, 0.006)
    corps = bpy.data.objects.new("Corps", bpy.data.meshes.new("Corps"))
    bpy.context.scene.collection.objects.link(corps); corps.parent = root
    finish(corps, bm, [M_FUR], 0.12)

    # ventre + semelles crème
    bm = bmesh.new()
    ellipsoid(bm, Vector((0, -0.24, 0.93)), (0.22, 0.08, 0.28), 0.008, 10, seed + 6, 28, 18)
    for sx in (1, -1):
        disc(bm, Vector((0.15 * sx, -0.06, 0.004)), Vector((0, 0, -1)), 0.10, 0.012, 18, 0.1, seed + sx)
        ellipsoid(bm, Vector((0.475 * sx, -0.16, 0.69)), (0.06, 0.03, 0.06), 0.004, 20, seed + 7, 12, 8)   # coussinets
    ob = bpy.data.objects.new("Corps_Creme", bpy.data.meshes.new("c")); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root; finish(ob, bm, [M_CREME], 0.1)

    # coutures du ventre (X) + couture qui lâche sur le flanc, rembourrage qui sort
    bm = bmesh.new()
    for k in range(18):
        a = 2 * math.pi * k / 18
        p = Vector((0.21 * math.cos(a), -0.29 + 0.03 * abs(math.cos(a)), 0.93 + 0.27 * math.sin(a)))
        stitch_x(bm, p, Vector((0, -1, 0.0)), Vector((0, 0, 1)), 0.022)
    side = 1 if seed % 2 else -1
    for k in range(5):
        p = Vector((0.33 * side, -0.05, 1.05 - k * 0.05))
        thin_box(bm, p, Vector((0, 0, 1)), Vector((side, 0, 0)), 0.03, 0.006, 0.004)
    ob = bpy.data.objects.new("Coutures", bpy.data.meshes.new("s")); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root; finish(ob, bm, [M_NOIR], 0.05)
    bm = bmesh.new()
    for k in range(4):
        ellipsoid(bm, Vector((0.335 * side, -0.07 + rs.uniform(-0.02, 0.02), 0.90 + k * 0.025)), (0.035, 0.03, 0.03), 0.01, 40, k, 12, 8)
    ob = bpy.data.objects.new("Rembourrage", bpy.data.meshes.new("r")); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root; finish(ob, bm, [M_REMB], 0.05)

    # fermeture éclair dans le dos, entrouverte : dents, curseur, et du noir à l'intérieur
    bm = bmesh.new()
    for k in range(34):
        z = 1.32 - k * 0.022
        open_ = max(0.0, (z - 1.0) / 0.32) * 0.035          # ouverte en haut
        for sx in (1, -1):
            thin_box(bm, Vector((sx * (0.008 + open_), 0.282 - 0.02 * abs(z - 1.0), z)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.012, 0.008, 0.006)
    disc(bm, Vector((0, 0.29, 1.0)), Vector((0, 1, 0)), 0.018, 0.012, 10)
    ob = bpy.data.objects.new("Fermeture", bpy.data.meshes.new("z")); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root; finish(ob, bm, [M_BOUTON_B], 0.05)
    bm = bmesh.new()   # l'ouverture : une fente noire
    c = bm.verts.new(Vector((0, 0.279, 1.17)))
    ring = []
    for k in range(16):
        a = 2 * math.pi * k / 16
        ring.append(bm.verts.new(Vector((0.03 * math.cos(a), 0.279 - 0.004, 1.17 + 0.15 * math.sin(a)))))
    for k in range(16):
        bm.faces.new((c, ring[(k + 1) % 16], ring[k]))
    merge_bm = bm
    ob = bpy.data.objects.new("Fermeture_Trou", bpy.data.meshes.new("t")); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root; finish(ob, merge_bm, [M_NOIR], 0.05)

    # ------------------------------------------------ tête (pivot au cou)
    tete = empty("Tete", root, (0, 0, NECK))
    hc = Vector((0, 0, HY))
    bm = bmesh.new()
    ellipsoid(bm, hc, (0.33, 0.30, 0.31), 0.03, 4.0, seed + 10, 40, 28)
    ears_creme = bmesh.new()
    for sx in (1, -1):
        if kind == "lapin":                       # longues oreilles, une pliée
            ang = math.radians(10 * sx if sx > 0 else -55)
            m = Matrix.Translation(hc + Vector((0.12 * sx, 0.02, 0.36))) @ Matrix.Rotation(-ang, 4, "Y")
            ellipsoid(bm, Vector((0, 0, 0.16)), (0.075, 0.045, 0.24), 0.01, 8, seed + 11 + sx, 18, 14, m)
            ellipsoid(ears_creme, Vector((0, -0.03, 0.16)), (0.045, 0.02, 0.18), 0.004, 8, seed, 14, 10, m)
        elif kind == "ours":
            ellipsoid(bm, hc + Vector((0.24 * sx, 0.02, 0.23)), (0.10, 0.06, 0.10), 0.01, 8, seed + 11 + sx, 18, 12)
            ellipsoid(ears_creme, hc + Vector((0.24 * sx, -0.03, 0.23)), (0.06, 0.02, 0.06), 0.003, 8, seed, 12, 8)
        elif kind == "chat":
            tmp = bmesh.new()
            ret = bmesh.ops.create_cone(tmp, cap_ends=True, segments=10, radius1=0.11, radius2=0.01, depth=0.22)
            bmesh.ops.transform(tmp, matrix=Matrix.Translation(hc + Vector((0.18 * sx, 0.0, 0.25))) @ Matrix.Rotation(-0.35 * sx, 4, "Y")
                                @ Matrix.Diagonal((1, 0.45, 1, 1)), verts=ret["verts"])
            merge(bm, tmp)
            ellipsoid(ears_creme, hc + Vector((0.18 * sx, -0.045, 0.23)), (0.05, 0.015, 0.07), 0.003, 8, seed, 12, 8)
        else:                                     # chien : oreilles tombantes
            m = Matrix.Translation(hc + Vector((0.31 * sx, 0.0, 0.05))) @ Matrix.Rotation(0.3 * sx, 4, "Y")
            ellipsoid(bm, Vector((0, 0, -0.08)), (0.07, 0.05, 0.20), 0.01, 8, seed + 11 + sx, 18, 12, m)
    head_part("Tete_Fourrure", tete, bm, [M_FUR], 0.12)

    ellipsoid(ears_creme, hc + Vector((0, -0.265, -0.10)), (0.16, 0.08, 0.11), 0.006, 12, seed + 12, 24, 16)   # museau
    head_part("Tete_Creme", tete, ears_creme, [M_CREME], 0.1)

    # yeux-boutons dépareillés : le gauche pend au bout de son fil, nez, sourire cousu trop grand
    bm = bmesh.new()
    eye_r = hc + Vector((0.12, -0.285, 0.07))
    eye_l = hc + Vector((-0.215, -0.265, -0.05))          # descendu sur la joue, pendu à son fil
    disc(bm, eye_r, Vector((0.25, -1, 0.15)), 0.058, 0.022, 20, 0.05, 1)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        disc(bm, eye_r + Vector((math.cos(a) * 0.016, -0.012, math.sin(a) * 0.016)), Vector((0.25, -1, 0.15)), 0.006, 0.004, 8)
    btn_a = bmesh.new()
    disc(btn_a, eye_l, Vector((-0.2, -1, 0.3)), 0.05, 0.02, 20, 0.08, 2)
    tmp = bpy.data.meshes.new("tmp"); btn_a.to_mesh(tmp); btn_a.free()
    start = len(bm.faces)
    bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    bm.faces.ensure_lookup_table()
    for f in bm.faces[start:]:
        f.material_index = 1
    thread = curve_mesh([[tuple(hc + Vector((-0.13, -0.285, 0.07))), tuple(hc + Vector((-0.19, -0.30, 0.02))),
                          tuple(eye_l + Vector((0, 0.0, 0.02)))]], 0.003, "fil_oeil")
    for k in range(2):                                      # le trou laissé par l'œil : deux points de fil
        thin_box(bm, hc + Vector((-0.13, -0.29, 0.07)), Vector((1, 0, 1 - 2 * k)), Vector((0, -1, 0.1)), 0.04, 0.006, 0.004)
    merge(bm, thread)
    nose = bmesh.new()
    ellipsoid(nose, hc + Vector((0, -0.345, -0.05)), (0.045, 0.03, 0.032), 0.004, 20, 3, 12, 8)
    merge(bm, nose)
    pts = []
    for i in range(11):
        x = -0.2 + 0.4 * i / 10
        z = HY - 0.17 + 0.07 * (x / 0.2) ** 2
        y_head = -math.sqrt(max(0.0, 1 - (x / 0.33) ** 2 - ((z - HY) / 0.31) ** 2)) * 0.30
        y_muz = -0.265 - 0.08 * math.sqrt(max(0.0, 1 - (x / 0.16) ** 2 - ((z - HY + 0.10) / 0.11) ** 2))
        y = min(y_head, y_muz) - 0.012                       # par-dessus le museau puis les joues (trop large)
        pts.append(Vector((x, y, z)))
    merge(bm, curve_mesh([[tuple(p) for p in pts]], 0.006, "sourire"))
    for p in pts[1:-1]:
        thin_box(bm, p + Vector((0, -0.004, 0)), Vector((0, 0, 1)), Vector((0, -1, 0)), 0.05, 0.007, 0.006)
    head_part("Tete_Details", tete, bm, [M_NOIR, M_BOUTON_A], 0.05)

    # col froncé à la base de la tête
    bm = bmesh.new()
    grid = []
    for i, (r, z) in enumerate(((0.15, NECK + 0.04), (0.24, NECK + 0.02), (0.30, NECK - 0.01))):
        ring = []
        for j in range(64):
            a = 2 * math.pi * j / 64
            rr = r * (1 + 0.08 * i * math.sin(12 * a))
            ring.append(bm.verts.new(Vector((math.cos(a) * rr, math.sin(a) * rr * 0.9, z - 0.012 * i * math.cos(12 * a)))))
        grid.append(ring)
    for i in range(2):
        for j in range(64):
            bm.faces.new((grid[i][j], grid[i][(j + 1) % 64], grid[i + 1][(j + 1) % 64], grid[i + 1][j]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
    ob = bpy.data.objects.new("Col", bpy.data.meshes.new("col")); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root; finish(ob, bm, [M_CREME], 0.1)

    out = os.path.join(OUTDIR, f"Doux_{kind.capitalize()}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True, export_cameras=False,
                              export_lights=False, export_extras=False)
    pu.strip_scene_name(out)
    tris = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    print(f"EXPORT -> {out} : {tris} faces")


for i, k in enumerate(("lapin", "ours", "chat", "chien")):
    build(k, 300 + 10 * i)
