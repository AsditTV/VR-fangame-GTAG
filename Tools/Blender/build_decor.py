"""Décor de l'Institut (DA pastel fait main) : cadres de dessins d'enfants, guirlande de fanions,
lapin en peluche oublié, cubes de jouets. Version Blender.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/build_decor.py -- DOSSIER_SORTIE DOSSIER_TEXTURES

Sortie (mètres réels, base en 0, avant = +Z Unity) : Cadre_family/friends/lady/pool.glb (les vrais dessins
d'Assets/Horror/Art/Drawings), Guirlande.glb (1 m de long sur l'axe X, étirée par InstitutBuilder),
Lapin.glb, Cubes.glb. Rien de droit : cadres de travers, fanions inégaux, peluche affaissée.
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
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DRAWINGS = os.path.join(ROOT_DIR, "Assets", "Horror", "Art", "Drawings")

from poupee_utils import *  # noqa: E402,F401,F403
import poupee_utils as pu  # noqa: E402

pu.init(argv[1], seed=81)


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


def export(name):
    out = os.path.join(OUTDIR, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True, export_cameras=False,
                              export_lights=False, export_extras=False)
    pu.strip_scene_name(out)
    print("EXPORT ->", out)


def image_material(name, path):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.95
    tex = nt.nodes.new("ShaderNodeTexImage")
    img = bpy.data.images.load(path)
    # les dessins ont un fond transparent (exporté en noir) : on les pose sur du papier crème
    import numpy as np
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    paper = np.array([0.95, 0.92, 0.84])
    a = px[..., 3:4]
    rgb = px[..., :3] * a + paper * (1 - a)
    out = np.dstack([rgb, np.ones((h, w, 1), dtype=np.float32)])
    tex.image = pu.save_rgba(name + "_papier", np.flipud(out))
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


# ------------------------------------------------ cadres de travers avec les dessins des enfants
for key in ("family", "friends", "lady", "pool"):
    root = start("Cadre_" + key)
    M_BOIS = material("Cadre_Bois", srgb(["#C9A27E", "#B9C9D6", "#D7B3C0", "#B8D3C2"][["family", "friends", "lady", "pool"].index(key)]),
                      "feutre", 0.8, seed=82)
    M_DESSIN = image_material("Dessin_" + key, os.path.join(DRAWINGS, "drawing_" + key + ".png"))
    W, H = 0.42, 0.34
    bm = bmesh.new()                                      # le papier du dessin (UV 0..1 sur la feuille)
    uv = bm.loops.layers.uv.verify()
    vs = [bm.verts.new(Vector((x, 0.0, z))) for x, z in ((-W / 2, 0), (W / 2, 0), (W / 2, H), (-W / 2, H))]
    f = bm.faces.new(vs[::-1])
    for l, (u, v) in zip(f.loops, ((0, 0), (0, 1), (1, 1), (1, 0))[::-1]):
        l[uv].uv = (u, v)
    ob = bpy.data.objects.new("Dessin", bpy.data.meshes.new("Dessin"))
    bpy.context.scene.collection.objects.link(ob); ob.parent = root
    me = ob.data; bm.to_mesh(me); bm.free(); me.materials.append(M_DESSIN)
    bm = bmesh.new()                                      # le cadre : 4 baguettes inégales
    t = 0.035
    for (x0, z0, x1, z1) in ((-W / 2 - t, -t, W / 2 + t, 0), (-W / 2 - t, H, W / 2 + t, H + t * 1.2),
                             (-W / 2 - t, 0, -W / 2, H), (W / 2, 0, W / 2 + t * 0.8, H)):
        ret = bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector((x1 - x0, 0.03, z1 - z0)), verts=ret["verts"])
        bmesh.ops.translate(bm, vec=Vector(((x0 + x1) / 2, 0.012, (z0 + z1) / 2)), verts=ret["verts"])
    part(root, "Cadre", bm, [M_BOIS], 0.05)
    for o in root.children:
        o.location.y -= 0.0                               # rien : le cadre est déjà devant le papier
    root.rotation_euler = (0, math.radians(4 * (1 if key in ("family", "pool") else -1)), 0)   # de travers
    export("Cadre_" + key)

# ------------------------------------------------ guirlande de fanions en papier (1 m, étirée dans Unity)
root = start("Guirlande")
COLORS = ["#E6A9B8", "#A9D2C1", "#C7B6DC", "#E9D7A0", "#B2C5E1"]
mats = [material("Fanion_" + str(i), srgb(c), "feutre", 0.9, seed=83) for i, c in enumerate(COLORS)]
M_FIL = material("Fil_Guirlande", srgb("#3A3240"), None, 0.7)
pts = [(x, 0.0, -0.08 * math.sin(math.pi * (x + 0.5))) for x in [(-0.5 + k / 20) for k in range(21)]]
part(root, "Fil", curve_mesh([pts], 0.003, "fil"), [M_FIL], 0.05)
bm = bmesh.new()
for k in range(9):
    x = -0.45 + k * 0.1125
    z = -0.08 * math.sin(math.pi * (x + 0.5))
    h = 0.075 + 0.02 * noise.noise(Vector((k, 1, 1)))
    a = bm.verts.new(Vector((x - 0.04, 0, z))); b = bm.verts.new(Vector((x + 0.04, 0, z)))
    c = bm.verts.new(Vector((x + 0.008 * noise.noise(Vector((k, 2, 2))), 0.01, z - h)))
    f = bm.faces.new((a, b, c)); f.material_index = k % len(COLORS)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.002)
part(root, "Fanions", bm, mats, 0.05)
export("Guirlande")

# ------------------------------------------------ lapin en peluche oublié (affaissé, un œil-bouton en moins)
root = start("Lapin")
M_PELUCHE = material("Peluche", srgb("#E8D6DA"), "feutre", 1.0, seed=84)
M_NOIR = material("Bouton", srgb("#221C26"), None, 0.4)
bm = bmesh.new()
ellipsoid(bm, Vector((0, 0, 0.09)), (0.09, 0.08, 0.1), 0.008, 18, 1, 20, 14)                  # corps
ellipsoid(bm, Vector((0.01, -0.01, 0.22)), (0.07, 0.065, 0.065), 0.006, 18, 2, 20, 14)        # tête penchée
for sx, ang in ((1, -0.3), (-1, 1.1)):                                                      # oreilles, une tombée
    m = Matrix.Translation(Vector((0.03 * sx, 0, 0.27))) @ Matrix.Rotation(ang * sx, 4, "Y")
    ellipsoid(bm, Vector((0, 0, 0.06)), (0.022, 0.012, 0.07), 0.003, 18, 3, 12, 8, m)
for sx in (1, -1):
    ellipsoid(bm, Vector((0.05 * sx, -0.06, 0.03)), (0.035, 0.05, 0.03), 0.004, 18, 4, 12, 8)  # pattes
part(root, "Peluche", bm, [M_PELUCHE], 0.06)
bm = bmesh.new()
disc(bm, Vector((0.03, -0.068, 0.235)), Vector((0, -1, 0)), 0.01, 0.006, 10)                # un seul œil
part(root, "Oeil", bm, [M_NOIR], 0.03)
export("Lapin")

# ------------------------------------------------ cubes de jouets empilés de travers
root = start("Cubes")
mats = [material("Cube_" + str(i), srgb(c), "feutre", 0.8, seed=85) for i, c in enumerate(["#E6A9B8", "#A9D2C1", "#E9D7A0"])]
for i, (x, y, z, rot) in enumerate(((0, 0, 0, 8), (0.02, 0.01, 0.12, -14), (0.12, -0.02, 0, 25))):
    bm = bmesh.new()
    ret = bmesh.ops.create_cube(bm, size=0.12)
    edges = list({e for v in ret["verts"] for e in v.link_edges})
    bmesh.ops.bevel(bm, geom=ret["verts"] + edges, offset=0.012, segments=2, affect="EDGES")
    bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(rot), 3, "Z"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((x, y, z + 0.06)), verts=bm.verts)
    part(root, "Cube_" + str(i), bm, [mats[i]], 0.06)
export("Cubes")

# ------------------------------------------------ applique murale (abat-jour de tissu de travers) : la lampe des pièces
root = start("Applique")
M_LAITON = material("Laiton", srgb("#B89A6A"), None, 0.35)
M_LAITON.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.8
M_ABAT = material("Abat_Jour", srgb("#EBC7CF"), "tissu", 0.9, seed=86)
M_AMPOULE = material("Ampoule", srgb("#FFE9C4"), None, 0.2)
M_AMPOULE.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = srgb("#FFD9A0")
M_AMPOULE.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 3.0
bm = bmesh.new()
disc(bm, Vector((0, 0.01, 0)), Vector((0, -1, 0)), 0.05, 0.02, 16)                          # platine au mur
part(root, "Platine", bm, [M_LAITON], 0.05)
part(root, "Bras", curve_mesh([[(0, 0, 0), (0, -0.08, 0.02), (0, -0.14, 0.08)]], 0.008, "bras"), [M_LAITON], 0.05)
bm = bmesh.new()
tube(bm, 0.0, 0.16, lambda t, a: 0.09 - 0.045 * t + 0.004 * math.sin(6 * a), 20, 6, 0.002, 30, 1, cap0=False, cap1=False)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.004)
bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(9), 3, "X"), verts=bm.verts)   # de travers
bmesh.ops.translate(bm, vec=Vector((0, -0.15, 0.04)), verts=bm.verts)
part(root, "Abat_Jour", bm, [M_ABAT], 0.06)
bm = bmesh.new()
ellipsoid(bm, Vector((0, -0.15, 0.1)), (0.025, 0.025, 0.032), 0.0, 10, 1, 12, 8)
part(root, "Ampoule", bm, [M_AMPOULE], 0.05)
export("Applique")

# ------------------------------------------------ vieux téléphone à cadran (le Standard)
root = start("Telephone")
M_BAKE = material("Bakelite", srgb("#C7B6DC"), "feutre", 0.35, seed=87)
M_CADRAN = material("Cadran", srgb("#F1E6D2"), None, 0.4)
M_FILT = material("Cordon", srgb("#3A3240"), None, 0.6)
bm = bmesh.new()
ellipsoid(bm, Vector((0, 0, 0.06)), (0.11, 0.09, 0.065), 0.003, 20, 1, 24, 14)
for v in bm.verts:
    v.co.z = max(v.co.z, 0.0)
part(root, "Socle", bm, [M_BAKE], 0.06)
bm = bmesh.new()
disc(bm, Vector((0, -0.07, 0.085)), Vector((0, -0.8, 0.6)), 0.045, 0.008, 24)
part(root, "Cadran", bm, [M_CADRAN], 0.05)
hand = [(-0.1, 0.0, 0.135), (-0.05, 0.0, 0.15), (0.05, 0.0, 0.15), (0.1, 0.0, 0.135)]
part(root, "Combine", curve_mesh([hand], 0.022, "combine"), [M_BAKE], 0.06)
part(root, "Cordon", curve_mesh([[(0.1, 0.0, 0.12), (0.15, 0.04, 0.05), (0.12, 0.08, 0.01), (0.06, 0.06, 0.02)]], 0.004, "cordon"), [M_FILT], 0.05)
export("Telephone")
