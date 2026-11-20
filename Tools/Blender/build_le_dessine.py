"""Le Dessiné, version Blender : poupée de tissu sans jambes, plus détaillée que la version trimesh.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_le_dessine.py -- ANCIEN.glb SORTIE.glb DOSSIER_TEXTURES

Principe : on importe l'ancien modèle pour garder EXACTEMENT les mêmes noeuds, pivots et noms
(Tete, Masque, Masque_Papier, Bouche, Bouche_Dessin, Cou, Corps, Epaule_G/D, Epaule_Boule_G/D,
BrasHaut_G/D, Coude_G/D, AvantBras_G/D, Main_G/D, Paume_G/D, Doigt_*), car les scripts Unity les
cherchent par leur nom. On remplace seulement la géométrie et les matières, et on refait les
décorations (coutures, boutons, pièce, déchirure, cordon, laine, col).

Règles respectées :
 - les pièces teintes par DollColorizer (Corps, BrasHaut_, AvantBras_, Epaule_Boule_) ont une texture
   claire et neutre ; aucune décoration n'est leur enfant (sinon elle prendrait la couleur du joueur) ;
 - le masque garde ton dessin et ses UV ; la bouche (Bouche_Dessin) n'est pas touchée (animation micro) ;
 - rien de parfaitement rond : bosses, plis, bords de papier irréguliers.
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT, TEXDIR = argv[0], argv[1], argv[2]


from poupee_utils import *  # noqa: F401,F403  (outils communs)
import poupee_utils as pu
pu.init(argv[2])
rng = pu.rng

# ============================================================== import de l'ancien modèle
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
O = bpy.data.objects
ROOT = O["LeDessine"]

# on retire les anciennes décorations (refaites plus bas)
for ob in list(O):
    if ob.name.startswith(("Couture_", "Bouton_", "Dechirure", "Rembourrage_", "Fil_", "Laine_",
                           "Epaule_Point_", "Piece_")):
        bpy.data.objects.remove(ob, do_unlink=True)

# matières
M_CORPS = material("Tissu_Corps", LILAS, "tissu", 0.95, seed=1)
M_TETE = material("Tissu_Tete", TISSU_TETE, "tissu", 0.95, seed=1)
M_MAINS = material("Feutre_Mains", PECHE, "feutre", 0.9, seed=3)
M_MENTHE = material("Tissu_Piece", MENTHE, "tissu", 0.95, seed=1)
M_COL = material("Tissu_Col", ROSE, "tissu", 0.95, seed=1)
M_LAINE = material("Laine", LAINE, "laine", 1.0, seed=6)
M_FIL = material("Fil", FIL, None, 0.8)
M_REMB = material("Rembourrage", REMBOURRAGE, "feutre", 1.0, seed=7)
M_TROU = material("Trou", TROU, None, 1.0)
M_IVOIRE = material("Bouton_Ivoire", IVOIRE, None, 0.35)
M_BLEU = material("Bouton_Bleu", BLEU_BOUTON, None, 0.35)

# ============================================================== le corps (sac sans jambes)
SQ = 0.76                 # le sac est ovale (plus plat d'avant en arrière)
PROFILE = [(0.200, 0.040), (0.212, 0.085), (0.228, 0.140), (0.255, 0.178), (0.300, 0.200), (0.380, 0.207),
           (0.460, 0.201), (0.540, 0.188), (0.610, 0.168), (0.665, 0.140), (0.705, 0.105), (0.735, 0.072),
           (0.760, 0.050)]
PZ = np.array([p[0] for p in PROFILE]); PR = np.array([p[1] for p in PROFILE])


def body_radius(z):
    return float(np.interp(z, PZ, PR))


def gather(theta, z):
    """Plis du sac : très froncé en bas (cordon), un peu sous le cou."""
    low = max(0.0, 1 - (z - 0.20) / 0.13)
    top = max(0.0, 1 - (0.765 - z) / 0.06)
    return 1 + 0.16 * low * math.sin(11 * theta + 0.6) + 0.05 * top * math.sin(9 * theta)


BULGE_TH, BULGE_Z = -math.pi / 2 - 0.6, 0.33   # rembourrage tassé en bas à gauche


def body_point(theta, z, extra=0.0):
    d = math.atan2(math.sin(theta - BULGE_TH), math.cos(theta - BULGE_TH))
    bulge = 0.014 * math.exp(-(d * d) / 0.18 - (z - BULGE_Z) ** 2 / 0.006)
    r = body_radius(z) * gather(theta, z) + extra + bulge
    lean = 0.014 * (0.76 - z)                       # le sac s'affaisse un peu sur le côté
    return Vector((math.cos(theta) * r + lean, math.sin(theta) * r * SQ, z))


def body_normal(theta, z):
    p0, pz = body_point(theta, z), body_point(theta, z + 0.004)
    pt = body_point(theta + 0.01, z)
    n = (pt - p0).cross(pz - p0).normalized()
    return n if n.dot(Vector((p0.x, p0.y, 0))) >= 0 else -n


FRONT = -math.pi / 2       # -Y = l'avant du personnage dans Blender
bm = bmesh.new()
SEGS, RINGS = 72, 64
grid = []
for i in range(RINGS + 1):
    z = 0.200 + (0.762 - 0.200) * i / RINGS
    ring = []
    for j in range(SEGS):
        th = 2 * math.pi * j / SEGS
        p = body_point(th, z)
        n = Vector((p.x, p.y / SQ, 0)).normalized()
        p += n * lump(p, 0.006, 9.0, 1) + n * lump(p, 0.0025, 30.0, 2)
        ring.append(bm.verts.new(p))
    grid.append(ring)
for i in range(RINGS):
    for j in range(SEGS):
        bm.faces.new((grid[i][j], grid[i][(j + 1) % SEGS], grid[i + 1][(j + 1) % SEGS], grid[i + 1][j]))
bottom = bm.verts.new(Vector((0, 0, 0.192)))
for j in range(SEGS):
    bm.faces.new((grid[0][(j + 1) % SEGS], grid[0][j], bottom))
finish(O["Corps"], bm, [M_CORPS], 0.09, world_space=True)

# --- coutures du corps : couture verticale devant (points en X) + coutures de côté (points droits)
bm = bmesh.new()
for k in range(14):
    z = 0.31 + k * 0.03
    th = FRONT + 0.03 * math.sin(k * 1.3)          # couture un peu de travers, faite main
    stitch_x(bm, body_point(th, z, 0.004), body_normal(th, z), Vector((0, 0, 1)), 0.016)
for side in (0.0, math.pi):
    for k in range(18):
        z = 0.27 + k * 0.026
        p, n = body_point(side, z, 0.003), body_normal(side, z)
        thin_box(bm, p, Vector((0, 0, 1)), n, 0.014, 0.0035, 0.003)
# couture de la pièce et autour de la déchirure (plus bas)
PATCH_TH, PATCH_Z = FRONT + 0.62, 0.43
for k in range(16):
    a = 2 * math.pi * k / 16
    th, z = PATCH_TH + 0.27 * math.cos(a), PATCH_Z + 0.045 * math.sin(a)
    stitch_x(bm, body_point(th, z, 0.007), body_normal(th, z), Vector((0, 0, 1)), 0.009)
# les fils qui tiennent les boutons
BUTTONS = [(FRONT - 0.42, 0.635, 0.017, M_IVOIRE), (FRONT - 0.36, 0.545, 0.014, M_BLEU)]
for th, z, r, _ in BUTTONS:
    stitch_x(bm, body_point(th, z, 0.010), body_normal(th, z), Vector((0, 0, 1)), r * 0.9)
new_object("Coutures", ROOT, bm, [M_FIL], 0.05)

# --- boutons dépareillés (deux matières dans un seul objet)
bm = bmesh.new()
for idx, (th, z, r, _) in enumerate(BUTTONS):
    disc(bm, body_point(th, z, 0.004), body_normal(th, z), r, 0.006, 18, 0.08, idx)
    for f in bm.faces:
        if f.material_index == 0 and not f.tag:
            f.material_index = idx
            f.tag = True
new_object("Boutons", ROOT, bm, [M_IVOIRE, M_BLEU], 0.05)

# --- pièce rapiécée (menthe), bord irrégulier, épousant le ventre
bm = bmesh.new()
N = 28
rim, inner = [], []
center = bm.verts.new(body_point(PATCH_TH, PATCH_Z, 0.006))
for k in range(N):
    a = 2 * math.pi * k / N
    jag = 1 + 0.12 * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, 5.0)))
    th, z = PATCH_TH + 0.32 * jag * math.cos(a), PATCH_Z + 0.055 * jag * math.sin(a)
    rim.append(bm.verts.new(body_point(th, z, 0.006)))
for k in range(N):
    bm.faces.new((center, rim[k], rim[(k + 1) % N]))
new_object("Piece_Rapiecee", ROOT, bm, [M_MENTHE], 0.07)
bm = bmesh.new()                       # une deuxième pièce, rose, dans le dos, cousue de travers
BACK_TH, BACK_Z = math.pi / 2 + 0.35, 0.52
center = bm.verts.new(body_point(BACK_TH, BACK_Z, 0.006))
rim = []
for k in range(N):
    a = 2 * math.pi * k / N + 0.3
    sq = 1 + 0.15 * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, 8.0)))
    rim.append(bm.verts.new(body_point(BACK_TH + 0.24 * sq * math.cos(a), BACK_Z + 0.05 * sq * math.sin(a), 0.006)))
for k in range(N):
    bm.faces.new((center, rim[(k + 1) % N], rim[k]))
new_object("Piece_Dos", ROOT, bm, [M_COL], 0.07)
bm = bmesh.new()
for k in range(12):
    a = 2 * math.pi * k / 12 + 0.3
    th, z = BACK_TH + 0.2 * math.cos(a), BACK_Z + 0.042 * math.sin(a)
    thin_box(bm, body_point(th, z, 0.0085), Vector((-math.sin(a), 0, math.cos(a))), body_normal(th, z), 0.012, 0.003, 0.003)
new_object("Coutures_Dos", ROOT, bm, [M_FIL], 0.05)

# --- déchirure sur le flanc gauche, avec du rembourrage qui sort
TEAR_TH, TEAR_Z = FRONT - 0.95, 0.40
bm = bmesh.new()
c = bm.verts.new(body_point(TEAR_TH, TEAR_Z, 0.002))
ring = []
for k in range(20):
    a = 2 * math.pi * k / 20
    jag = 1 + 0.35 * abs(noise.noise(Vector((math.cos(a) * 4, math.sin(a) * 4, 9.0))))
    ring.append(bm.verts.new(body_point(TEAR_TH + 0.20 * jag * math.cos(a), TEAR_Z + 0.03 * jag * math.sin(a), 0.002)))
for k in range(20):
    bm.faces.new((c, ring[k], ring[(k + 1) % 20]))
new_object("Dechirure", ROOT, bm, [M_TROU], 0.05)
bm = bmesh.new()
for k in range(6):
    th = TEAR_TH + rng.uniform(-0.12, 0.12)
    z = TEAR_Z + rng.uniform(-0.015, 0.015)
    ellipsoid(bm, body_point(th, z, 0.008), (0.016, 0.016, 0.013), 0.004, 60, k, 14, 10)
new_object("Rembourrage", ROOT, bm, [M_REMB], 0.03)

# --- cordon qui ferme le bas du sac + petit bout de tissu froncé + deux fils qui pendent
bm = bmesh.new()
pts = [body_point(2 * math.pi * k / 24, 0.214, 0.004) for k in range(25)]
cord = curve_mesh([[tuple(p) for p in pts]], 0.0045, "cordon")
knot = body_point(FRONT + 0.25, 0.214, 0.008)
tails = [[tuple(knot), tuple(knot + Vector((0.01, -0.02, -0.03))), tuple(knot + Vector((0.02, -0.025, -0.07))),
          tuple(knot + Vector((0.015, -0.02, -0.10)))],
         [tuple(knot), tuple(knot + Vector((-0.012, -0.02, -0.025))), tuple(knot + Vector((-0.02, -0.03, -0.06)))]]
cord2 = curve_mesh(tails, 0.0035, "bouts")
tmp = bpy.data.meshes.new("tmp")
for b in (cord, cord2):
    b.to_mesh(tmp); bm.from_mesh(tmp); b.free()
bpy.data.meshes.remove(tmp)
ellipsoid(bm, knot, (0.011, 0.009, 0.009), 0.002, 80, 3, 12, 8)
new_object("Cordon", ROOT, bm, [M_FIL], 0.04)
bm = bmesh.new()   # la "queue" froncée sous le sac (il flotte, pas de jambes)
tube(bm, 0.196, 0.150, lambda t, a: (0.040 * (1 - t) + 0.012) * (1 + 0.25 * math.sin(7 * a)), 28, 10, 0.003, 50, 4,
     cap0=False, cap1=True)
new_object("Corps_Fronce", ROOT, bm, [M_CORPS], 0.06)  # commence par « Corps » : teint comme le sac

# ============================================================== cou + col froncé
cou = O["Cou"]
bm = bmesh.new()
tube(bm, 0.700, 0.800, lambda t, a: 0.046 + 0.004 * math.sin(3 * a + t * 4), 24, 8, 0.002, 40, 5)
finish(cou, bm, [M_TETE], 0.06, world_space=True)
bm = bmesh.new()
grid = []
for i, (r, z) in enumerate(((0.050, 0.758), (0.088, 0.748), (0.112, 0.732))):
    ring = []
    for j in range(64):
        a = 2 * math.pi * j / 64
        rr = r * (1 + (0.10 * i) * math.sin(14 * a))
        ring.append(bm.verts.new(Vector((math.cos(a) * rr, math.sin(a) * rr * 0.9, z - 0.006 * i * math.cos(14 * a)))))
    grid.append(ring)
for i in range(2):
    for j in range(64):
        bm.faces.new((grid[i][j], grid[i][(j + 1) % 64], grid[i + 1][(j + 1) % 64], grid[i + 1][j]))
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.004)
new_object("Col", ROOT, bm, [M_COL], 0.05)

# ============================================================== tête en tissu, couture, laine
tete = O["Tete"]
tc = tete.matrix_world.translation.copy()
bm = bmesh.new()
verts = ellipsoid(bm, tc, (0.150, 0.138, 0.146), 0.007, 8, 11, 48, 32)
for v in verts:                      # la couture du crâne creuse un peu le tissu
    d = v.co - tc
    if d.z > 0 and abs(d.x) < 0.012:
        v.co -= d.normalized() * 0.004 * (1 - abs(d.x) / 0.012)
finish(O["Tete_Tissu"], bm, [M_TETE], 0.09, world_space=True)

bm = bmesh.new()
for k in range(13):
    a = math.radians(-60 + k * 12)
    p = tc + Vector((0, math.sin(a) * 0.140, math.cos(a) * 0.148))
    stitch_x(bm, p + (p - tc).normalized() * 0.003, (p - tc).normalized(), Vector((0, 1, 0)), 0.013)
new_object("Couture_Crane", tete, bm, [M_FIL], 0.04)

loops = []
for k in range(12):                  # boucles de laine sur le dessus et l'arrière
    a0 = rng.uniform(-1.1, 1.1)
    base = tc + Vector((math.sin(a0) * 0.09, 0.03 + rng.uniform(-0.02, 0.07), 0.125))
    pts = []
    for s in range(14):
        t = s / 13 * 2.3 * math.pi
        pts.append(tuple(base + Vector((math.cos(t) * 0.018, s * 0.004, math.sin(t) * 0.022 + 0.012))))
    loops.append(pts)
new_object("Laine", tete, curve_mesh(loops, 0.0065, "laine"), [M_LAINE], 0.03)

# ============================================================== masque en papier froissé (UV gardés)
mask = O["Masque_Papier"]
bm = bmesh.new(); bm.from_mesh(mask.data)
bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
uv = bm.loops.layers.uv.active
for v in bm.verts:
    p = v.co
    crease = abs(noise.noise(p * 22 + Vector((3, 1, 2))))           # plis nets
    crumple = noise.noise(p * 45 + Vector((7, 7, 7)))
    v.co.y -= 0.0035 * (0.5 - crease) + 0.0012 * crumple            # -Y local = vers l'avant
    if v.is_boundary:                                              # bord découpé à la main
        v.co.x += 0.004 * noise.noise(p * 70 + Vector((1, 2, 3)))
        v.co.z += 0.004 * noise.noise(p * 70 + Vector((4, 5, 6)))
me = bpy.data.meshes.new("Masque_Papier")
bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = True
for m in mask.data.materials:
    me.materials.append(m)
mask.data = me

# ============================================================== bras, épaules, mains
for S, sign in (("G", 1), ("D", -1)):
    ep, coude, main = O["Epaule_" + S], O["Coude_" + S], O["Main_" + S]
    # épaule : boule de tissu + gros bouton d'articulation de poupée côté extérieur
    bm = bmesh.new()
    ellipsoid(bm, Vector(), (0.072, 0.066, 0.070), 0.005, 14, 20 + sign, 28, 18)
    finish(O["Epaule_Boule_" + S], bm, [M_CORPS], 0.09)
    out_dir = ep.matrix_world.to_3x3().inverted() @ Vector((sign, 0, 0))
    bm = bmesh.new()
    disc(bm, out_dir.normalized() * 0.071, out_dir, 0.026, 0.008, 20, 0.06, 30 + sign)
    obj = bpy.data.objects.new("Epaule_Bouton_" + S, bpy.data.meshes.new("b"))
    bpy.context.scene.collection.objects.link(obj); obj.parent = ep; obj.matrix_parent_inverse = Matrix()
    finish(obj, bm, [M_IVOIRE], 0.05)
    bm = bmesh.new()
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        q = out_dir.normalized().to_track_quat("Z", "Y").to_matrix()
        pt = out_dir.normalized() * 0.0755 + q @ Vector((math.cos(a) * 0.009, math.sin(a) * 0.009, 0))
        disc(bm, pt, out_dir, 0.003, 0.002, 8)
    stitch_x(bm, out_dir.normalized() * 0.077, out_dir.normalized(), q @ Vector((0, 1, 0)), 0.02)
    obj = bpy.data.objects.new("Epaule_Fil_" + S, bpy.data.meshes.new("f"))
    bpy.context.scene.collection.objects.link(obj); obj.parent = ep; obj.matrix_parent_inverse = Matrix()
    finish(obj, bm, [M_FIL], 0.05)

    # haut du bras : boudin rembourré, un peu cabossé (le long de -Z local), couture creusée derrière
    def seam_angle(obj):
        d = obj.matrix_world.to_3x3().inverted() @ Vector((0, 1, 0))
        return math.atan2(d.y, d.x)

    def groove(a, a0):
        da = math.atan2(math.sin(a - a0), math.cos(a - a0))
        return 0.0035 * math.exp(-(da / 0.10) ** 2)
    a_up = seam_angle(ep)
    bm = bmesh.new()
    tube(bm, 0.015, -0.255, lambda t, a: 0.047 - 0.006 * t + 0.004 * math.sin(math.pi * t) + 0.002 * math.sin(3 * a)
         - groove(a, a_up), 32, 18, 0.004, 22, 40 + sign)
    finish(O["BrasHaut_" + S], bm, [M_CORPS], 0.09)

    # avant-bras + manchette évasée (« cornet ») au poignet, ourlet ondulé
    a_fore = seam_angle(coude)

    def fore_r(t, a):
        if t < 0.78:
            return 0.043 - 0.005 * t + 0.003 * math.sin(math.pi * t / 0.78) - groove(a, a_fore)
        k = (t - 0.78) / 0.22
        return 0.040 + 0.022 * k * k + 0.006 * k * math.sin(9 * a)
    bm = bmesh.new()
    tube(bm, 0.02, -0.255, fore_r, 28, 22, 0.003, 24, 50 + sign, cap0=True, cap1=False)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.003)
    finish(O["AvantBras_" + S], bm, [M_CORPS], 0.09)

    # main en moufle de feutre, doigts dodus (même boîte que l'ancien modèle, plus ronds)
    paume = O["Paume_" + S]
    c, h = local_bbox(paume)
    bm = bmesh.new()
    ellipsoid(bm, c, (h.x * 1.0, h.y * 1.25, h.z * 0.95), 0.004, 30, 60 + sign, 28, 18)
    finish(paume, bm, [M_MAINS], 0.06)
    for f in ("Index", "Majeur", "Annulaire", "Pouce"):      # doigts de moufle : saucisses de feutre dodues
        j1, j2 = O[f"Doigt_{f}_1_{S}"], O[f"Doigt_{f}_2_{S}"]
        L1 = (j2.matrix_world.translation - j1.matrix_world.translation).length
        for seg, L, r in ((1, L1, 0.0175), (2, L1 * 0.95, 0.0165)):
            ob = O[f"Doigt_{f}_{seg}_{S}_Mesh"]

            def fr(t, a, r=r, seg=seg):
                k = r * (1 - 0.1 * t) * (1 + 0.07 * math.sin(2 * a))
                if seg == 2 and t > 0.7:                    # bout bien rond
                    k *= math.sqrt(max(0.08, 1 - ((t - 0.7) / 0.3) ** 2))
                return k
            bm = bmesh.new()
            tube(bm, 0.007, -L - 0.003, fr, 14, 12, 0.0015, 60, hash(ob.name) % 97)
            finish(ob, bm, [M_MAINS], 0.05)

# ============================================================== étiquette « No.3 » : recollée sur le sac
for name in ("Etiquette", "Epingle"):
    ob = O[name]
    x, z = ob.matrix_world.translation.x, ob.matrix_world.translation.z
    th = -math.acos(max(-1, min(1, x / body_radius(z))))
    target_y = body_point(th, z).y - 0.004
    pts = [ob.matrix_world @ Vector(cc) for cc in ob.bound_box]
    back_y = max(p.y for p in pts)
    ob.matrix_world = Matrix.Translation((0, target_y - back_y + 0.006, 0)) @ ob.matrix_world

# ============================================================== export
for ob in O:
    ob.select_set(False)
tris = sum(len(o.data.polygons) for o in O if o.type == "MESH")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True, export_apply=False,
                          export_cameras=False, export_lights=False, export_extras=False)
pu.strip_scene_name(OUT)   # garde le même identifiant de racine que l'ancien modèle
print(f"EXPORT -> {OUT} : {len([o for o in O if o.type == 'MESH'])} maillages, {tris} faces")
