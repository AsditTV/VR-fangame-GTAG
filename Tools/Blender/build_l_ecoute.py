"""L'Écoute (Céleste Marivaux), version Blender : même silhouette et mêmes noeuds, matières et formes refaites.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_l_ecoute.py -- ANCIEN.glb SORTIE.glb DOSSIER_TEXTURES

Noeuds gardés (MonsterAnimator, BlindMonsterAI les cherchent) : LEcoute, Buste, Cou, Tete, Epaule_G/D,
Coude_G/D, Main_G/D, Doigt_* (doigts animés), Cable_* (fils qui pendent), Trousseau (clés), Cornet_*.

Ce qui change :
 - robe refaite : vrais plis, bas déchiré irrégulier, encre qui remonte en coulures (texture dédiée)
   et gouttes d'encre qui pendent sous l'ourlet ;
 - manches bouffantes d'époque, avant-bras maigres et noueux ;
 - cornets acoustiques en cuivre vert-de-gris avec pavillon et rebord ;
 - chignon enroulé de laine, col de dentelle festonné en une pièce, boutons regroupés ;
 - portrait, tablier, buste, tête : gardés (dessins et UV) mais froissés / cabossés.
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
SRC, OUT = argv[0], argv[1]

from poupee_utils import *  # noqa: E402,F401,F403  (outils communs)
import poupee_utils as pu  # noqa: E402

pu.init(argv[2], seed=11)
rng = pu.rng

ROBE, ENCRE, PEAU = srgb("#7D7088"), srgb("#120E16"), srgb("#CEC4CF")
COLC, CHIGNON, CUIVRE = srgb("#E6DDCF"), srgb("#4A4052"), srgb("#86A396")


# ============================================================== texture de la robe (encre qui remonte)
def robe_texture(size=1024):
    """u = tour de la robe (0..1), v = du bas (0) à la taille (1). Raccordable en u."""
    h = pu.weave_height(size, 340, 21) * 0.5 + 0.25
    y, x = np.mgrid[0:size, 0:size] / size          # y : 0 en haut de l'image = taille
    v = 1 - y
    # longueur des coulures selon la colonne (bruit raccordable en u)
    cols = pu.tile_noise(size, [(3, 1.0), (9, 0.6), (27, 0.35)], 5)[0]
    drip = 0.10 + 0.45 * cols ** 2.2                 # hauteur atteinte par l'encre
    fine = pu.tile_noise(size, [(40, 1.0), (80, 0.6)], 6)[0]
    drip = drip + 0.12 * np.clip(fine - 0.55, 0, 1) * 3   # quelques coulures fines plus hautes
    wet = np.clip((drip[None, :] - v) / 0.04, 0, 1)        # 1 = imbibé d'encre
    stain = pu.tile_noise(size, [(4, 1.0), (12, 0.5)], 7)
    lum = (0.78 + 0.16 * h + 0.06 * (stain - 0.5)) * (1 - wet) + 0.06 * wet
    col = np.dstack([lum, lum * 0.98, lum * 1.0, np.ones_like(lum)])
    # l'encre est lisse (relief du tissu effacé), donc moins de relief là où c'est mouillé
    hh = h * (1 - wet * 0.8)
    gx = (np.roll(hh, -1, 1) - np.roll(hh, 1, 1)) * 2.4
    gy = (np.roll(hh, -1, 0) - np.roll(hh, 1, 0)) * 2.4
    n = np.dstack([-gx, gy, np.ones_like(hh)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return (pu.save_rgba("robe_couleur", col), pu.save_rgba("robe_normale", np.dstack([n * 0.5 + 0.5, np.ones_like(hh)])))


def robe_material():
    m = bpy.data.materials.new("Robe_Encre")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.9
    col_img, nor_img = robe_texture()
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = col_img
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs[7].default_value = ROBE
    nt.links.new(tex.outputs["Color"], mix.inputs[6])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    ntex = nt.nodes.new("ShaderNodeTexImage"); ntex.image = nor_img
    ntex.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def wrinkle_existing(obj, amp, freq, cuts=1, seed=0):
    """Garde UV et matières d'un objet, le subdivise et le froisse le long des normales."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    bm.normal_update()
    for v in bm.verts:
        p = v.co
        crease = 0.5 - abs(noise.noise(p * freq + Vector((seed, 2, 3))))
        v.co += v.normal * amp * (crease + 0.4 * noise.noise(p * freq * 2.3 + Vector((5, seed, 1))))
    me = bpy.data.meshes.new(obj.name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    for m in obj.data.materials:
        me.materials.append(m)
    obj.data = me


# ============================================================== import
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
O = bpy.data.objects
ROOT, BUSTE, TETE = O["LEcoute"], O["Buste"], O["Tete"]

M_ROBE = robe_material()
M_ENCRE = material("Encre", ENCRE, None, 0.12)
M_PEAU = material("Peau", PEAU, "feutre", 0.7, seed=12)
M_DENTELLE = material("Dentelle", COLC, "feutre", 0.95, seed=13)
M_LAINE = material("Chignon_Laine", CHIGNON, "laine", 1.0, seed=14)
M_CUIVRE = material("Cuivre", CUIVRE, "feutre", 0.45, seed=15)
M_CUIVRE.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.65
M_BOUTON = material("Bouton_Jais", srgb("#2B2430"), None, 0.25)
M_MANCHE = O["Buste_Mesh"].data.materials[0]          # même tissu que le buste

# ============================================================== robe (cloche plissée, bas déchiré)
SEGS, RINGS = 120, 70
R_T = [(0.0, 0.150), (0.10, 0.200), (0.25, 0.262), (0.50, 0.335), (0.75, 0.400), (1.0, 0.455)]
TT, RR = np.array([a for a, _ in R_T]), np.array([b for _, b in R_T])


def hem_z(th):
    c = Vector((math.cos(th) * 2, math.sin(th) * 2, 0))
    return 0.015 + 0.07 * noise.noise(c * 1.5) ** 2 + 0.035 * abs(noise.noise(c * 6 + Vector((0, 0, 4))))


bm = bmesh.new()
uvl = bm.loops.layers.uv.verify()
grid = []
for i in range(RINGS + 1):
    t = i / RINGS
    ring = []
    for j in range(SEGS + 1):                        # colonne en double pour la couture des UV
        th = 2 * math.pi * (j % SEGS) / SEGS
        z = 1.36 + (hem_z(th) - 1.36) * t
        fold = (0.07 * math.sin(13 * th + 0.8) + 0.035 * math.sin(23 * th + 2.0)
                + 0.03 * noise.noise(Vector((math.cos(th) * 4, math.sin(th) * 4, z * 3)))) * min(1, t * 1.6)
        r = float(np.interp(t, TT, RR)) * (1 + fold)
        ring.append(bm.verts.new(Vector((math.cos(th) * r, math.sin(th) * r * 0.92, z))))
    grid.append(ring)
for i in range(RINGS):
    for j in range(SEGS):
        f = bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
        for l, (ii, jj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uvl].uv = (jj / SEGS, 1 - ii / RINGS)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.006)
me = bpy.data.meshes.new("Robe"); bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = True
me.materials.append(M_ROBE)
O["Robe"].data = me



def robe_r(th, z):
    """Rayon extérieur de la robe à l'angle th et à la hauteur z (mêmes formules que le maillage)."""
    t = (1.36 - z) / max(1e-4, 1.36 - hem_z(th))
    t = min(1.0, max(0.0, t))
    fold = (0.07 * math.sin(13 * th + 0.8) + 0.035 * math.sin(23 * th + 2.0)
            + 0.03 * noise.noise(Vector((math.cos(th) * 4, math.sin(th) * 4, z * 3)))) * min(1, t * 1.6)
    return float(np.interp(t, TT, RR)) * (1 + fold) + 0.006


def stick_on_robe(obj, gap):
    """Pousse les sommets d'un objet (tablier, poche) juste devant la surface de la robe."""
    mw, mi = obj.matrix_world, obj.matrix_world.inverted()
    for v in obj.data.vertices:
        w = mw @ v.co
        th = math.atan2(w.y / 0.92, w.x)
        r_now = math.hypot(w.x, w.y / 0.92)
        r_min = robe_r(th, w.z) + gap
        if w.z < 1.36 and r_now < r_min:
            k = r_min / max(r_now, 1e-4)
            v.co = mi @ Vector((w.x * k, w.y * k, w.z))


# gouttes d'encre qui pendent sous l'ourlet
bm = bmesh.new()
for k in range(26):
    th = rng.uniform(0, 2 * math.pi)
    t = 1.0
    r = float(np.interp(t, TT, RR)) * (1 + 0.07 * math.sin(13 * th + 0.8))
    z = hem_z(th)
    L = rng.uniform(0.02, 0.07)
    ellipsoid(bm, Vector((math.cos(th) * r, math.sin(th) * r * 0.92, z - L * 0.5)), (0.006, 0.006, L * 0.55),
              0.0, 10, k, 10, 8)
    ellipsoid(bm, Vector((math.cos(th) * r, math.sin(th) * r * 0.92, z - L)), (0.009, 0.009, 0.011), 0.0, 10, k, 10, 8)
new_object("Gouttes_Encre", ROOT, bm, [M_ENCRE], 0.05)

# ============================================================== tablier, poche, buste, tête, portrait : froissés
wrinkle_existing(O["Tablier"], 0.006, 9, cuts=2, seed=1)
stick_on_robe(O["Tablier"], 0.008)
for _n in ("Poche", "Papier_Poche_0", "Papier_Poche_1"):
    if _n in O:
        stick_on_robe(O[_n], 0.016)
wrinkle_existing(O["Buste_Mesh"], 0.003, 14, cuts=1, seed=2)
wrinkle_existing(O["Tete_Nuit"], 0.004, 7, cuts=0, seed=3)
wrinkle_existing(O["Portrait"], 0.0035, 20, cuts=1, seed=4)

# ============================================================== chignon enroulé de laine
chig = O["Chignon"]
c, h = local_bbox(chig)
bm = bmesh.new()
ellipsoid(bm, c, (h.x * 0.8, h.y * 0.8, h.z * 0.8), 0.004, 12, 5, 24, 16)
finish(chig, bm, [M_LAINE], 0.04)
wc = chig.matrix_world @ c
ax = (chig.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()
u = ax.orthogonal().normalized(); w = ax.cross(u)
R = max(h.x, h.y) * 0.82
spiral = []
for s in range(160):
    a = s / 159 * 9 * math.pi
    k = -1 + 2 * s / 159
    rr = R * math.sqrt(max(0.05, 1 - k * k)) + 0.004
    spiral.append(tuple(wc + u * math.cos(a) * rr + w * math.sin(a) * rr + ax * k * h.z * 0.75))
new_object("Chignon_Laine", TETE, curve_mesh([spiral], 0.007, "spirale"), [M_LAINE], 0.03)

# ============================================================== cornets acoustiques
tete_w = TETE.matrix_world.translation
for name in ("Cornet_G", "Cornet_D", "Cornet_Haut"):
    ob = O[name]
    c, h = local_bbox(ob)
    z0, z1 = c.z - h.z, c.z + h.z
    far_top = (ob.matrix_world @ Vector((c.x, c.y, z1)) - tete_w).length > (ob.matrix_world @ Vector((c.x, c.y, z0)) - tete_w).length
    bell = max(h.x, h.y) * 0.95

    def horn_r(t, a, bell=bell, far_top=far_top):
        tt = t if far_top else 1 - t
        return 0.011 + 0.004 * tt + (bell - 0.015) * (tt ** 4.5) + 0.0008 * math.sin(6 * a)
    bm = bmesh.new()
    grid = tube(bm, z0, z1, horn_r, 28, 26, 0.0, 10, 0, cap0=not far_top, cap1=far_top is False)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.003)
    # rebord roulé du pavillon
    zr = z1 if far_top else z0
    rim = [Vector((math.cos(2 * math.pi * k / 40) * bell, math.sin(2 * math.pi * k / 40) * bell, zr)) for k in range(41)]
    rbm = curve_mesh([[tuple(c2 + Vector((c.x * 0, 0, 0))) for c2 in rim]], 0.004, "rebord")
    tmp = bpy.data.meshes.new("tmp"); rbm.to_mesh(tmp); bm.from_mesh(tmp); rbm.free(); bpy.data.meshes.remove(tmp)
    bmesh.ops.translate(bm, vec=Vector((c.x, c.y, 0)), verts=bm.verts)
    finish(ob, bm, [M_CUIVRE], 0.05)

# ============================================================== bras : manches bouffantes, avant-bras noueux
for S in ("G", "D"):
    ep, coude, main = O["Epaule_" + S], O["Coude_" + S], O["Main_" + S]
    l1 = (coude.matrix_world.translation - ep.matrix_world.translation).length
    l2 = (main.matrix_world.translation - coude.matrix_world.translation).length

    def sleeve_r(t, a):
        if t < 0.38:
            k = t / 0.38
            return 0.048 + 0.032 * math.sin(math.pi * k) * (1 + 0.12 * math.sin(10 * a))
        return 0.046 - 0.012 * (t - 0.38) / 0.62 + 0.002 * math.sin(5 * a)
    bm = bmesh.new()
    tube(bm, 0.03, -l1 + 0.01, sleeve_r, 28, 30, 0.003, 18, 3)
    finish(O["BrasHaut_" + S], bm, [M_MANCHE], 0.08)

    def fore_r(t, a):
        r = 0.026 - 0.007 * t
        r += 0.011 * math.exp(-((t - 0.02) / 0.05) ** 2)       # coude pointu
        r += 0.007 * math.exp(-((t - 0.93) / 0.04) ** 2)       # os du poignet
        return r * (1 + 0.08 * math.sin(2 * a))                 # un peu aplati
    bm = bmesh.new()
    tube(bm, 0.02, -l2 + 0.005, fore_r, 18, 34, 0.0025, 30, 4)
    finish(O["AvantBras_" + S], bm, [M_PEAU], 0.05)

    paume = O["Paume_" + S]
    c, h = local_bbox(paume)
    bm = bmesh.new()
    ellipsoid(bm, c, (h.x * 0.95, h.y * 0.95, h.z), 0.004, 45, 7, 24, 16)   # jointures qui saillent
    finish(paume, bm, [M_PEAU], 0.05)

# ============================================================== cou trop long, vertèbres qui saillent
cou_mesh = O["Cou_Mesh"]
bm = bmesh.new()
tube(bm, -0.03, 0.29, lambda t, a: 0.023 + 0.007 * max(0.0, math.sin(t * math.pi * 7)) ** 3 * (0.6 + 0.4 * math.cos(a - math.pi / 2))
     + 0.003 * math.sin(2 * a), 20, 40, 0.0015, 40, 8)
finish(cou_mesh, bm, [M_PEAU], 0.05)

# ============================================================== doigts : longs, noueux, ongles d'encre en griffes
for ob in [o for o in O if o.name.startswith("Doigt_") and o.name.endswith("_Mesh")]:
    zs = [v.co.z for v in ob.data.vertices]
    L = -min(zs)
    r0 = 0.0072 if "Pouce" not in ob.name else 0.0078

    def finger_r(t, a, r0=r0):
        knob = 0.0022 * (math.exp(-(t / 0.08) ** 2) + math.exp(-((t - 1) / 0.08) ** 2))   # jointures
        return (r0 * (1 - 0.25 * t) + knob) * (1 + 0.1 * math.sin(2 * a))
    bm = bmesh.new()
    tube(bm, 0.004, -L, finger_r, 10, 16, 0.0008, 90, hash(ob.name) % 89)
    finish(ob, bm, [M_PEAU], 0.03)
for ob in [o for o in O if o.name.endswith("_Ongle")]:
    bm = bmesh.new()
    tube(bm, 0.004, -0.04, lambda t, a: 0.0062 * (1 - t) ** 1.4 + 0.0004, 8, 10, 0.0, 10, 0)
    for v in bm.verts:                     # griffe recourbée vers la paume
        v.co.y -= 0.012 * (v.co.z / -0.04) ** 2
    finish(ob, bm, [M_ENCRE], 0.03)

# ============================================================== portrait épinglé sur la tête
por = O["Portrait"]
wverts = [por.matrix_world @ v.co for v in por.data.vertices]
tw = TETE.matrix_world.translation
top = max(wverts, key=lambda p: p.z)
left = min(wverts, key=lambda p: p.x)
right = max(wverts, key=lambda p: p.x)
bm_pin, bm_head = bmesh.new(), bmesh.new()
for p in (top, left, right):
    d = (tw - p).normalized()
    pts = [tuple(p - d * 0.012), tuple(p + d * 0.05)]
    merge_bm = curve_mesh([pts], 0.0012, "epingle")
    me = bpy.data.meshes.new("tmp"); merge_bm.to_mesh(me); merge_bm.free(); bm_pin.from_mesh(me); bpy.data.meshes.remove(me)
    ellipsoid(bm_head, p - d * 0.014, (0.006, 0.006, 0.006), 0.0, 10, 1, 10, 8)
M_PIN = material("Epingle_Metal", srgb("#C9C9CF"), None, 0.25)
M_PIN.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 1.0
M_PIN_TETE = material("Tete_Epingle", srgb("#E6A0B4"), None, 0.3)
new_object("Epingles", TETE, bm_pin, [M_PIN], 0.03)
new_object("Tetes_Epingles", TETE, bm_head, [M_PIN_TETE], 0.03)

# ============================================================== nœud du tablier dans le dos + pièces sur la robe
bm = bmesh.new()
bow_c = Vector((0.0, robe_r(math.pi / 2, 1.30) * 0.92 + 0.004, 1.30))
loops2 = []
for sign in (1, -1):
    loops2.append([tuple(bow_c + Vector((sign * (0.11 * (1 - math.cos(t)) * 0.5), 0.016 * math.sin(t) + 0.012, 0.05 * math.sin(t))))
                   for t in [s / 20 * 2 * math.pi for s in range(21)]])
    loops2.append([tuple(bow_c), tuple(bow_c + Vector((sign * 0.04, 0.016, -0.15))), tuple(bow_c + Vector((sign * 0.07, 0.012, -0.36)))])
new_object("Noeud_Tablier", ROOT, curve_mesh(loops2, 0.009, "noeud"), [M_DENTELLE], 0.03)
M_PIECE = material("Robe_Piece", srgb("#968AA6"), "tissu", 0.9, seed=16)
bm_p, bm_s = bmesh.new(), bmesh.new()
for th0, z0, w, h in ((2.4, 0.92, 0.22, 0.08), (-0.5, 0.62, 0.18, 0.07)):
    c = bm_p.verts.new(Vector((math.cos(th0) * (robe_r(th0, z0) + 0.006), math.sin(th0) * (robe_r(th0, z0) + 0.006) * 0.92, z0)))
    ring = []
    for k in range(20):
        a = 2 * math.pi * k / 20
        jag = 1 + 0.12 * noise.noise(Vector((math.cos(a) * 3, math.sin(a) * 3, th0)))
        th, z = th0 + w * jag * math.cos(a), z0 + h * jag * math.sin(a)
        rr = robe_r(th, z) + 0.006
        ring.append(bm_p.verts.new(Vector((math.cos(th) * rr, math.sin(th) * rr * 0.92, z))))
        if k % 2 == 0:
            rr2 = rr + 0.003
            n = Vector((math.cos(th), math.sin(th) * 0.92, 0)).normalized()
            stitch_x(bm_s, Vector((math.cos(th) * rr2, math.sin(th) * rr2 * 0.92, z)), n, Vector((0, 0, 1)), 0.016)
    for k in range(20):
        bm_p.faces.new((c, ring[k], ring[(k + 1) % 20]))
new_object("Robe_Pieces", ROOT, bm_p, [M_PIECE], 0.08)
M_FILR = material("Fil_Robe", srgb("#2A2230"), None, 0.7)
new_object("Robe_Coutures", ROOT, bm_s, [M_FILR], 0.04)

# ============================================================== dentelle (une pièce) et boutons regroupés
dents = [o for o in O if o.name.startswith("Dentelle_")]
if dents:
    center = sum((o.matrix_world.translation for o in dents), Vector()) / len(dents)
    nrm = Vector((0, -1, 0.35)).normalized()
    q = nrm.to_track_quat("Z", "Y").to_matrix()
    bm = bmesh.new()
    rings = []
    for i, r in enumerate((0.030, 0.062, 0.085)):
        ring = []
        for j in range(72):
            a = 2 * math.pi * j / 72
            rr = r * (1 + (0.10 if i == 2 else 0.02) * math.sin(9 * a) ** 2)   # festons arrondis
            ring.append(bm.verts.new(center + q @ Vector((math.cos(a) * rr, math.sin(a) * rr, 0.004 * i * math.sin(18 * a)))))
        rings.append(ring)
    for i in range(2):
        for j in range(72):
            bm.faces.new((rings[i][j], rings[i][(j + 1) % 72], rings[i + 1][(j + 1) % 72], rings[i + 1][j]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.002)
    new_object("Dentelle", BUSTE, bm, [M_DENTELLE], 0.04)
    for o in dents:
        bpy.data.objects.remove(o, do_unlink=True)

buttons = [o for o in O if o.name.startswith("Bouton_")]
bm = bmesh.new()
for o in buttons:
    nrm = (o.matrix_world.to_3x3() @ Vector((0, 0, 1))).normalized()
    disc(bm, o.matrix_world.translation, nrm, 0.0085, 0.006, 14, 0.05, hash(o.name) % 13)
new_object("Boutons", BUSTE, bm, [M_BOUTON], 0.03)
for o in buttons:
    bpy.data.objects.remove(o, do_unlink=True)

# ============================================================== export
for ob in O:
    ob.select_set(False)
tris = sum(len(o.data.polygons) for o in O if o.type == "MESH")
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", export_yup=True, export_apply=False,
                          export_cameras=False, export_lights=False, export_extras=False)
pu.strip_scene_name(OUT)   # garde le même identifiant de racine que l'ancien modèle
print(f"EXPORT -> {OUT} : {len([o for o in O if o.type == 'MESH'])} maillages, {tris} faces")
