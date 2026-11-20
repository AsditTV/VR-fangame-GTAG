"""Rendu d'aperçu d'un modèle .glb avec Blender (sans ouvrir l'interface).

Usage :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/preview_glb.py -- modele.glb sortie.png [angle_degres]

Fait un rendu 3/4 face sur fond neutre, lumière douce (pas l'éclairage du jeu : c'est pour juger la forme
et les matières). Plusieurs angles : appeler plusieurs fois avec un angle différent.
"""
import math
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
angle = float(argv[2]) if len(argv) > 2 else 25.0

# Scène vide
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

# Boîte englobante de tout ce qui est visible
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
center, size = (lo + hi) / 2, (hi - lo)
radius = max(size) * 0.5

scene = bpy.context.scene
world = bpy.data.worlds.new("Fond")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.15, 0.18, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
scene.world = world

# Caméra : 3/4 face. glTF : l'avant du modèle (+Z glTF) devient -Y dans Blender.
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
scene.collection.objects.link(cam)
cam.data.lens = 60
dist = radius / math.tan(math.radians(14)) * 1.05
a = math.radians(angle)
cam.location = center + Vector((math.sin(a) * dist, -math.cos(a) * dist, radius * 0.25))
cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = cam

def add_light(name, kind, energy, loc, size=1.0):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, kind))
    l.data.energy = energy
    if kind == "AREA":
        l.data.size = size
    l.location = loc
    l.rotation_euler = (center - l.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(l)

add_light("Cle", "AREA", 110 * radius * radius * 4, center + Vector((-2, -2.5, 2.5)) * radius * 1.6, radius * 2)
add_light("Contre", "AREA", 70 * radius * radius * 4, center + Vector((2.5, 2.0, 1.5)) * radius * 1.6, radius * 2)
add_light("Bouche", "AREA", 25 * radius * radius * 4, center + Vector((1.5, -2.5, -0.5)) * radius * 1.6, radius * 3)

# Rendu : Eevee si possible (rapide), sinon Cycles sur le processeur
scene.render.resolution_x, scene.render.resolution_y = 900, 1100
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.filepath = out
try:
    bpy.ops.render.render(write_still=True)
except Exception as e:  # pas de GPU utilisable : on passe en Cycles CPU
    print("Eevee indisponible :", e)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    bpy.ops.render.render(write_still=True)
print("APERCU ->", out, "taille", tuple(round(v, 3) for v in size))
