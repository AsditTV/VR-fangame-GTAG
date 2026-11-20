"""Ajoute les normales manquantes dans des fichiers .glb (sans dépendance, Python pur).

Pourquoi : les modèles générés par trimesh contiennent des morceaux qui n'ont que des positions
(pas de NORMAL). glTFast (l'importeur .glb de Unity) refuse alors le fichier avec l'erreur
« Invalid mainBufferType Position, Tangent ». On calcule des normales lissées (pondérées par
l'aire des triangles) pour chaque morceau qui n'en a pas, et on réécrit le .glb.

Usage :  python Tools/fix_glb_normals.py chemin1.glb chemin2.glb ...
Le fichier est modifié sur place : faites une copie avant (le dépôt Git garde aussi l'original).
"""
import json
import math
import struct
import sys

COMPONENT_FORMATS = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
TYPE_SIZES = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def read_glb(path):
    data = open(path, "rb").read()
    magic, version, _length = struct.unpack_from("<III", data, 0)
    assert magic == 0x46546C67 and version == 2, "pas un GLB 2.0"
    offset, gltf, binary = 12, None, b""
    while offset < len(data):
        chunk_len, chunk_type = struct.unpack_from("<II", data, offset)
        chunk = data[offset + 8: offset + 8 + chunk_len]
        if chunk_type == 0x4E4F534A:  # JSON
            gltf = json.loads(chunk.decode("utf-8"))
        elif chunk_type == 0x004E4942:  # BIN
            binary = chunk
        offset += 8 + chunk_len
    return gltf, bytearray(binary)


def write_glb(path, gltf, binary):
    js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    js += b" " * ((4 - len(js) % 4) % 4)
    binary = bytes(binary) + b"\x00" * ((4 - len(binary) % 4) % 4)
    total = 12 + 8 + len(js) + 8 + len(binary)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        f.write(struct.pack("<II", len(binary), 0x004E4942) + binary)


def read_accessor(gltf, binary, index):
    acc = gltf["accessors"][index]
    view = gltf["bufferViews"][acc["bufferView"]]
    fmt = COMPONENT_FORMATS[acc["componentType"]]
    n = TYPE_SIZES[acc["type"]]
    item = struct.calcsize("<" + fmt) * n
    stride = view.get("byteStride", item)
    base = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    return [struct.unpack_from("<" + fmt * n, binary, base + i * stride) for i in range(acc["count"])]


def smooth_normals(positions, indices):
    normals = [[0.0, 0.0, 0.0] for _ in positions]
    for t in range(0, len(indices) - 2, 3):
        a, b, c = indices[t], indices[t + 1], indices[t + 2]
        pa, pb, pc = positions[a], positions[b], positions[c]
        u = (pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2])
        v = (pc[0] - pa[0], pc[1] - pa[1], pc[2] - pa[2])
        # produit vectoriel : sa longueur = 2 x aire, donc pondération par l'aire
        n = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
        for k in (a, b, c):
            normals[k][0] += n[0]
            normals[k][1] += n[1]
            normals[k][2] += n[2]
    out = []
    for n in normals:
        length = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2)
        out.append((n[0] / length, n[1] / length, n[2] / length) if length > 1e-12 else (0.0, 1.0, 0.0))
    return out


def fix(path):
    gltf, binary = read_glb(path)
    fixed = 0
    for mesh in gltf.get("meshes", []):
        for prim in mesh["primitives"]:
            attrs = prim["attributes"]
            if "NORMAL" in attrs or prim.get("mode", 4) != 4:
                continue  # déjà des normales, ou pas des triangles
            positions = read_accessor(gltf, binary, attrs["POSITION"])
            if "indices" in prim:
                indices = [i[0] for i in read_accessor(gltf, binary, prim["indices"])]
            else:
                indices = list(range(len(positions)))
            normals = smooth_normals(positions, indices)

            # Nouveau morceau de données à la fin du buffer binaire (aligné sur 4 octets).
            binary += b"\x00" * ((4 - len(binary) % 4) % 4)
            start = len(binary)
            for n in normals:
                binary += struct.pack("<fff", *n)
            gltf["bufferViews"].append({"buffer": 0, "byteOffset": start, "byteLength": len(normals) * 12, "target": 34962})
            gltf["accessors"].append({"bufferView": len(gltf["bufferViews"]) - 1, "componentType": 5126,
                                      "count": len(normals), "type": "VEC3"})
            attrs["NORMAL"] = len(gltf["accessors"]) - 1
            fixed += 1
    gltf["buffers"][0]["byteLength"] = len(binary)
    write_glb(path, gltf, binary)
    print(f"{path} : {fixed} morceau(x) corrigé(s)")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        fix(p)
