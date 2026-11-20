"""Outils communs aux constructeurs Blender de CALM FACES (poupées de tissu, papier, coutures).

Importé par build_le_dessine.py, build_l_ecoute.py... (Blender --background --python).
Appeler init(dossier_textures) avant de créer des matières.
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise

TEXDIR = None
rng = random.Random(7)


def init(texdir, seed=7):
    """Dossier où écrire les textures générées (elles sont ensuite embarquées dans le .glb)."""
    global TEXDIR, rng
    TEXDIR = texdir
    os.makedirs(texdir, exist_ok=True)
    rng = random.Random(seed)


# ============================================================== couleurs
def srgb(hexa):
    h = hexa.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]


LILAS, TISSU_TETE, PECHE = srgb("#B3A0C6"), srgb("#D6C2BC"), srgb("#E6BFAE")
MENTHE, ROSE, LAINE = srgb("#A6CBB6"), srgb("#D9A7AE"), srgb("#5B4766")
REMBOURRAGE, TROU, FIL = srgb("#F1ECE2"), srgb("#2A2230"), srgb("#3A3240")
IVOIRE, BLEU_BOUTON = srgb("#E9D9A6"), srgb("#9FB8D0")


# ============================================================== textures (numpy, raccordables)
def tile_noise(size, freqs, seed):
    """Bruit qui se raccorde sur les bords (somme de sinus à fréquences entières)."""
    r = np.random.default_rng(seed)
    y, x = np.mgrid[0:size, 0:size] / size * 2 * np.pi
    acc = np.zeros((size, size))
    for f, amp in freqs:
        for _ in range(7):
            kx, ky = r.integers(-f, f + 1, 2)
            acc += amp * np.sin(kx * x + ky * y + r.uniform(0, 2 * np.pi))
    acc -= acc.min()
    return acc / max(acc.max(), 1e-6)


def weave_height(size=512, threads=40, seed=1):
    """Relief d'un tissu tissé : fils qui passent dessus / dessous, un peu irréguliers."""
    y, x = np.mgrid[0:size, 0:size] / size
    wob = (tile_noise(size, [(2, 1.0), (5, 0.4)], seed) - 0.5) * 0.35
    u, v = x * threads + wob, y * threads - wob
    fu, fv = u - np.floor(u), v - np.floor(v)
    over = ((np.floor(u) + np.floor(v)) % 2) == 0
    warp, weft = np.sin(np.pi * fu), np.sin(np.pi * fv)
    h = np.where(over, warp * (0.65 + 0.35 * weft), weft * (0.65 + 0.35 * warp))
    return np.clip(h * 0.85 + tile_noise(size, [(24, 1.0), (48, 0.6)], seed + 1) * 0.15, 0, 1)


def felt_height(size=512, seed=3):
    return tile_noise(size, [(16, 1.0), (40, 0.7), (90, 0.5)], seed)


def save_rgba(name, arr):
    """arr : (h, w, 4) en 0..1 -> image PNG sur disque, rechargée (le glb l'embarquera)."""
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(np.flipud(arr).astype(np.float32).ravel())
    path = os.path.join(TEXDIR, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return bpy.data.images.load(path)


def textures(kind, seed):
    """Renvoie (image couleur claire et neutre, image normal map) pour un type de matière."""
    size = 512
    if kind == "tissu":
        h = weave_height(size, 40, seed)
        mottle = tile_noise(size, [(3, 1.0), (7, 0.5)], seed + 9)
        lum = 0.80 + 0.14 * h + 0.06 * (mottle - 0.5)
        strength = 2.2
    elif kind == "feutre":
        h = felt_height(size, seed)
        lum = 0.86 + 0.12 * h
        strength = 1.4
    else:  # laine : fibres
        y, x = np.mgrid[0:size, 0:size] / size
        h = 0.5 + 0.5 * np.sin((x * 30 + tile_noise(size, [(3, 1.0)], seed) * 2) * 2 * np.pi)
        h = h * 0.7 + felt_height(size, seed) * 0.3
        lum = 0.75 + 0.25 * h
        strength = 2.5
    col = np.dstack([lum, lum * 0.985, lum * 0.97, np.ones_like(lum)])
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.dstack([-gx, gy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    nor = np.dstack([n * 0.5 + 0.5, np.ones_like(h)])
    return save_rgba(f"{kind}_{seed}_couleur", col), save_rgba(f"{kind}_{seed}_normale", nor)


_tex_cache = {}


def material(name, color, kind=None, rough=0.92, seed=1):
    """Matière Principled exportable en glTF : texture x couleur (Mix Multiply = baseColorFactor)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Base Color"].default_value = color
    if kind:
        key = (kind, seed)
        if key not in _tex_cache:
            _tex_cache[key] = textures(kind, seed)
        col_img, nor_img = _tex_cache[key]
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = col_img
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = color          # B = couleur
        nt.links.new(tex.outputs["Color"], mix.inputs[6])   # A = texture
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        ntex = nt.nodes.new("ShaderNodeTexImage"); ntex.image = nor_img
        ntex.image.colorspace_settings.name = "Non-Color"
        nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.inputs["Strength"].default_value = 0.8
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return m


# ============================================================== géométrie (bmesh)
def lump(p, amp, freq, seed):
    return amp * noise.noise(p * freq + Vector((seed * 1.7, seed * 3.1, seed * 0.7)))


def box_uv(bm, scale):
    """UV en projection 'boîte' à l'échelle du monde : la trame du tissu garde la même taille partout."""
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for l in f.loops:
            co = l.vert.co
            l[uv].uv = (co[a] / scale, co[b] / scale)


def ellipsoid(bm, center, radii, amp=0.0, freq=20.0, seed=0, segs=32, rings=20, matrix=None):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    verts = ret["verts"]
    for v in verts:
        d = v.co.copy()
        p = Vector((d.x * radii[0], d.y * radii[1], d.z * radii[2]))
        p += d.normalized() * lump(p, amp, freq, seed)
        v.co = center + p
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=verts)
    return verts


def tube(bm, z0, z1, radius_fn, segs=28, rings=24, amp=0.0, freq=25.0, seed=0, cap0=True, cap1=True):
    """Tube le long de l'axe Z local ; radius_fn(t, angle) donne le rayon (t = 0 en z0, 1 en z1)."""
    grid = []
    for i in range(rings + 1):
        t = i / rings
        z = z0 + (z1 - z0) * t
        ring = []
        for j in range(segs):
            a = 2 * math.pi * j / segs
            r = radius_fn(t, a)
            p = Vector((math.cos(a) * r, math.sin(a) * r, z))
            p += Vector((math.cos(a), math.sin(a), 0)) * lump(p, amp, freq, seed)
            ring.append(bm.verts.new(p))
        grid.append(ring)
    for i in range(rings):
        for j in range(segs):
            bm.faces.new((grid[i][j], grid[i][(j + 1) % segs], grid[i + 1][(j + 1) % segs], grid[i + 1][j]))
    for ring, cap, sign in ((grid[0], cap0, -1), (grid[-1], cap1, 1)):
        if cap:
            c = bm.verts.new(sum((v.co for v in ring), Vector()) / len(ring) + Vector((0, 0, sign * 0.004)))
            for j in range(segs):
                f = (ring[j], ring[(j + 1) % segs], c) if sign > 0 else (ring[(j + 1) % segs], ring[j], c)
                bm.faces.new(f)
    return grid


def thin_box(bm, center, along, normal, length, width, thick):
    """Petit pavé (un point de couture) posé sur une surface."""
    along = along.normalized()
    normal = normal.normalized()
    side = normal.cross(along).normalized()
    vs = []
    for dz in (-0.5, 0.5):
        for dy in (-0.5, 0.5):
            for dx in (-0.5, 0.5):
                vs.append(bm.verts.new(center + along * dx * length + side * dy * width + normal * dz * thick))
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        bm.faces.new([vs[i] for i in f])


def stitch_x(bm, center, normal, up, size=0.014):
    a = (up + normal.cross(up)).normalized()
    b = (up - normal.cross(up)).normalized()
    thin_box(bm, center, a, normal, size, size * 0.18, 0.003)
    thin_box(bm, center, b, normal, size, size * 0.18, 0.003)


def disc(bm, center, normal, radius, depth, segs=20, wobble=0.0, seed=0):
    m = normal.to_track_quat("Z", "Y").to_matrix().to_4x4()
    ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=radius, radius2=radius * 0.92, depth=depth)
    for v in ret["verts"]:
        v.co.x *= 1 + wobble * noise.noise(Vector((v.co.x * 40, v.co.y * 40, seed)))
        v.co.y *= 1 + wobble * noise.noise(Vector((v.co.y * 40, seed, v.co.x * 40)))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ m, verts=ret["verts"])


def curve_mesh(points_list, radius, name):
    """Fils / laine : courbes avec épaisseur, converties en maillage (bmesh renvoyé)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 2
    for pts in points_list:
        sp = cu.splines.new("NURBS")
        sp.points.add(len(pts) - 1)
        for p, co in zip(sp.points, pts):
            p.co = (co[0], co[1], co[2], 1)
        sp.use_endpoint_u = True
        sp.order_u = 3
        sp.resolution_u = 6
    ob = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(ob)
    deps = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(deps))
    bm = bmesh.new(); bm.from_mesh(me)
    bpy.data.objects.remove(ob); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
    return bm


def finish(obj, bm, mats, uv_scale=0.1, world_space=False):
    """Écrit le bmesh dans l'objet (en gardant son nom, son parent et son pivot)."""
    if world_space:
        bmesh.ops.transform(bm, matrix=obj.matrix_world.inverted(), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    box_uv(bm, uv_scale)
    me = bpy.data.meshes.new(obj.name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    for m in mats:
        me.materials.append(m)
    old = obj.data if obj.type == "MESH" else None
    obj.data = me
    if old is not None and old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def new_object(name, parent, bm, mats, uv_scale=0.1):
    """Nouvel objet décoratif, construit en coordonnées monde puis rattaché au parent."""
    obj = bpy.data.objects.new(name, bpy.data.meshes.new(name))
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.matrix_world = parent.matrix_world.copy()
    return finish(obj, bm, mats, uv_scale, world_space=True)


def local_bbox(obj):
    xs = [v.co for v in obj.data.vertices]
    lo = Vector((min(v.x for v in xs), min(v.y for v in xs), min(v.z for v in xs)))
    hi = Vector((max(v.x for v in xs), max(v.y for v in xs), max(v.z for v in xs)))
    return (lo + hi) / 2, (hi - lo) / 2




def strip_scene_name(path):
    """Retire le nom de la scène glTF ("Scene") après l'export Blender.

    glTFast identifie la racine du modèle par « scenes/<nom de scène> ». Les anciens modèles
    (trimesh) n'avaient pas de nom : sans ce nettoyage, la racine change d'identifiant et les
    scènes Unity perdent le lien vers le modèle (« Missing Prefab »).
    """
    import json
    import struct
    data = open(path, "rb").read()
    jlen = struct.unpack_from("<I", data, 12)[0]
    gltf = json.loads(data[20:20 + jlen])
    for sc in gltf.get("scenes", []):
        sc.pop("name", None)
    js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    rest = data[20 + jlen:]
    total = 12 + 8 + len(js) + len(rest)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        f.write(rest)
