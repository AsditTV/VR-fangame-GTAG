"""Version allégée d'un modèle (pour les PNJ et le décor sur Quest) : mêmes noeuds, mêmes noms, mêmes matières,
maillages simplifiés (Decimate). Les scripts Unity qui cherchent « Tete », « Epaule_G »... marchent pareil.

Usage (depuis la racine du dépôt) :
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background --factory-startup \
      --python Tools/Blender/make_light.py -- ENTREE.glb SORTIE.glb [RATIO=0.2] [MIN_TRIS=400]
Les petites pièces (moins de MIN_TRIS triangles) ne sont pas touchées : elles portent les détails (yeux, coutures).
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import poupee_utils as pu  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
ratio = float(argv[2]) if len(argv) > 2 else 0.2
min_tris = int(argv[3]) if len(argv) > 3 else 400

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
before = after = 0
for ob in bpy.data.objects:
    if ob.type != "MESH":
        continue
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    before += tris
    if tris >= min_tris:
        mod = ob.modifiers.new("allege", "DECIMATE")
        mod.ratio = max(ratio, min_tris / tris)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
    after += sum(len(p.vertices) - 2 for p in ob.data.polygons)
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True, export_cameras=False,
                          export_lights=False, export_extras=False)
pu.strip_scene_name(out)
print(f"ALLEGE -> {out} : {before} -> {after} triangles")
