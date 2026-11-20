"""
Textures procédurales pour les modèles prototypes : tissu tissé, papier, cuivre vert-de-gris, peau.
Chaque texture se raccorde sur elle-même (on peut la répéter sans couture visible) et vient avec
sa "normal map" (le relief : fils du tissu, grain du papier...), pour que les surfaces ne soient
plus lisses comme du plastique.

Les textures de couleur sont en NIVEAUX DE GRIS : la vraie couleur vient du "baseColorFactor" du
matériau. Une seule texture de tissu sert donc pour le lilas, la pêche, le menthe...
"""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import trimesh
from trimesh.visual import TextureVisuals
from trimesh.visual.material import PBRMaterial

SIZE = 256  # assez pour du tissu répété, et léger pour la VR


# ---------------------------------------------------------------- bruits raccordables
def tile_noise(size, cells, rng):
    """Bruit doux qui se raccorde sur ses bords (valeurs 0..1)."""
    grid = rng.random((cells, cells)).astype(np.float32)
    big = np.tile(grid, (3, 3))
    img = Image.fromarray(big, mode="F").resize((size * 3, size * 3), Image.BICUBIC)
    return np.asarray(img)[size:2 * size, size:2 * size]


def fractal(size, rng, octaves=((4, 0.5), (8, 0.25), (16, 0.15), (32, 0.1))):
    out = np.zeros((size, size), np.float32)
    total = 0.0
    for cells, amp in octaves:
        out += tile_noise(size, cells, rng) * amp
        total += amp
    out /= total
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


def height_to_normal(h, strength):
    """Relief (hauteur 0..1) -> normal map RGB (convention glTF), raccordable."""
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    n = np.dstack([-dx, dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB")


def gray(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "L").convert("RGB")


# ---------------------------------------------------------------- matières
def fabric(seed=1, threads=26):
    """Toile tissée (dessus/dessous), fibres, petites bouloches et taches d'usure."""
    rng = np.random.default_rng(seed)
    s = SIZE
    y, x = np.mgrid[0:s, 0:s].astype(np.float32)
    u, v = x * threads / s, y * threads / s
    warp = np.abs(np.sin(np.pi * u)) ** 0.6       # fils verticaux
    weft = np.abs(np.sin(np.pi * v)) ** 0.6       # fils horizontaux
    over = (np.floor(u) + np.floor(v)) % 2        # armure toile : un dessus, un dessous
    h = np.where(over > 0, 0.55 + 0.45 * warp * (0.6 + 0.4 * weft), 0.55 + 0.45 * weft * (0.6 + 0.4 * warp))
    fibers = tile_noise(s, 64, rng)
    lumps = fractal(s, rng)
    h = h * 0.75 + fibers * 0.15 + lumps * 0.10
    wear = fractal(s, rng, ((3, 0.6), (6, 0.4)))
    albedo = 0.80 + 0.16 * h - 0.05 * wear  # usure très douce (sinon on voit la répétition)
    return gray(albedo), height_to_normal(h, 1.8)


def skin(seed=2):
    """Peau maladive : marbrures et rides fines étirées."""
    rng = np.random.default_rng(seed)
    s = SIZE
    mottle = fractal(s, rng)
    wr = tile_noise(s, 48, rng)
    wr = np.asarray(Image.fromarray(wr, "F").resize((s, s // 4)).resize((s, s), Image.BICUBIC))
    h = mottle * 0.6 + wr * 0.4
    albedo = 0.86 + 0.12 * mottle - 0.08 * wr
    return gray(albedo), height_to_normal(h, 1.6)


def patina(seed=3):
    """Cuivre vert-de-gris : couleur déjà incluse (pas en gris), piqûres en relief."""
    rng = np.random.default_rng(seed)
    s = SIZE
    n = fractal(s, rng)
    pits = (tile_noise(s, 96, rng) > 0.78).astype(np.float32)
    copper = np.array([0.62, 0.45, 0.33])
    verdigris = np.array([0.55, 0.70, 0.62])
    t = np.clip((n - 0.35) * 2.2, 0, 1)[..., None]
    col = copper * (1 - t) + verdigris * t
    col *= (1 - 0.35 * pits)[..., None]
    img = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8), "RGB")
    return img, height_to_normal(n * 0.7 - pits * 0.3, 2.0)


def paper_layer(size_xy, seed=4, creases=9):
    """Grain de papier + plis de papier froissé (non raccordable : pour un masque/portrait)."""
    w, h = size_xy
    rng = np.random.default_rng(seed)
    grain = np.asarray(Image.fromarray(rng.random((h // 2, w // 2)).astype(np.float32), "F").resize((w, h), Image.BICUBIC))
    height = grain * 0.06
    crease_img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(crease_img)
    for _ in range(creases):
        x0, y0 = rng.uniform(0, w), rng.uniform(0, h)
        a = rng.uniform(0, math.pi)
        L = rng.uniform(0.4, 1.0) * max(w, h)
        d.line([(x0 - math.cos(a) * L, y0 - math.sin(a) * L), (x0 + math.cos(a) * L, y0 + math.sin(a) * L)],
               fill=int(rng.uniform(130, 255)), width=2)
    crease = np.asarray(crease_img.filter(ImageFilter.GaussianBlur(3))).astype(np.float32) / 255.0
    height = height + crease * 0.9
    shade = 1.0 - 0.10 * crease - 0.05 * (grain - 0.5)
    return height, shade


def normal_from_height_clamped(h, strength):
    """Comme height_to_normal mais sans raccord (bords clampés)."""
    hp = np.pad(h, 1, mode="edge")
    dx = (hp[1:-1, 2:] - hp[1:-1, :-2]) * strength
    dy = (hp[2:, 1:-1] - hp[:-2, 1:-1]) * strength
    n = np.dstack([-dx, dy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB")


# ---------------------------------------------------------------- application sur les maillages
_CACHE = {}


def library():
    if not _CACHE:
        _CACHE["fabric"] = fabric()
        _CACHE["skin"] = skin()
        _CACHE["patina"] = patina()
    return _CACHE


def lumpy(mesh, amp, freq=18.0, seed=0):
    """Bosselle une forme (comme une poupée rembourrée à la main) : déplace les sommets le long
    de leur normale avec un bruit 3D doux."""
    rng = np.random.default_rng(seed)
    v = mesh.vertices
    n = mesh.vertex_normals
    disp = np.zeros(len(v))
    for _ in range(7):
        k = rng.normal(size=3)
        k = k / np.linalg.norm(k) * freq * rng.uniform(0.6, 1.4)
        disp += np.sin(v @ k + rng.uniform(0, math.tau))
    disp /= 7
    mesh.vertices = v + n * (disp * amp)[:, None]
    return mesh


def box_uv(mesh, texel=0.08):
    """UV par projection sur les 3 axes (selon l'orientation de chaque face), en gardant
    les normales lisses. texel = taille en mètres d'une répétition de texture."""
    vn = mesh.vertex_normals.copy()
    faces = mesh.faces.copy()
    fn = mesh.face_normals
    verts = mesh.vertices[faces.reshape(-1)]
    normals = vn[faces.reshape(-1)]
    axis = np.argmax(np.abs(fn), axis=1).repeat(3)
    uv = np.zeros((len(verts), 2))
    for a, (i, j) in enumerate(((1, 2), (0, 2), (0, 1))):
        sel = axis == a
        uv[sel] = verts[sel][:, [i, j]] / texel
    new_faces = np.arange(len(verts)).reshape(-1, 3)
    out = trimesh.Trimesh(vertices=verts, faces=new_faces, vertex_normals=normals, process=False)
    return out, uv


def dress_up(mesh, color, kind="fabric", rough=0.95, metal=0.0, texel=0.08, normal_scale=1.0):
    """Donne une matière détaillée à un maillage : 'fabric', 'skin', 'patina' ou 'plain'."""
    if kind == "plain":
        mesh.visual = TextureVisuals(material=PBRMaterial(
            baseColorFactor=[c / 255 for c in color], roughnessFactor=rough, metallicFactor=metal))
        return mesh
    albedo, normal = library()[kind]
    out, uv = box_uv(mesh, texel)
    factor = [1.0, 1.0, 1.0, 1.0] if kind == "patina" else [c / 255 for c in color]
    out.visual = TextureVisuals(uv=uv, material=PBRMaterial(
        baseColorTexture=albedo, baseColorFactor=factor, normalTexture=normal,
        roughnessFactor=rough, metallicFactor=metal))
    # refusionne les sommets identiques (même position, même normale, même UV) : fichier 3x plus léger
    # (les normales lisses d'origine sont restaurées par position après la fusion)
    smooth = {tuple(np.round(p, 6)): n for p, n in zip(out.vertices, out.vertex_normals)}
    out.merge_vertices(merge_tex=False, merge_norm=False)
    out.vertex_normals = np.array([smooth[tuple(np.round(p, 6))] for p in out.vertices])
    return out


# ---------------------------------------------------------------- nettoyage du .glb
def dedupe_glb(path):
    """trimesh copie la même image une fois par matériau : on ne garde qu'un exemplaire
    de chaque image identique (le fichier passe de ~2 Mo à quelques centaines de Ko)."""
    import hashlib
    import json
    import struct

    data = open(path, "rb").read()
    off, js, binary = 12, None, b""
    while off < len(data):
        ln, typ = struct.unpack_from("<I4s", data, off)
        chunk = data[off + 8:off + 8 + ln]
        off += 8 + ln
        if typ == b"JSON":
            js = json.loads(chunk)
        elif typ == b"BIN\x00":
            binary = chunk
    views = js["bufferViews"]

    def view_bytes(i):
        v = views[i]
        o = v.get("byteOffset", 0)
        return binary[o:o + v["byteLength"]]

    # images identiques -> une seule
    first_by_hash, image_map, dropped_views = {}, {}, set()
    for idx, img in enumerate(js.get("images", [])):
        h = hashlib.sha1(view_bytes(img["bufferView"])).hexdigest()
        if h in first_by_hash:
            image_map[idx] = first_by_hash[h]
            dropped_views.add(img["bufferView"])
        else:
            first_by_hash[h] = idx
            image_map[idx] = idx
    for tex in js.get("textures", []):
        tex["source"] = image_map[tex["source"]]

    # on renumérote les images gardées
    kept_images = sorted(set(image_map.values()))
    new_img_index = {old: new for new, old in enumerate(kept_images)}
    for tex in js.get("textures", []):
        tex["source"] = new_img_index[tex["source"]]
    js["images"] = [js["images"][i] for i in kept_images]
    # une vue peut être partagée par une image gardée : on ne la supprime pas
    dropped_views -= {img["bufferView"] for img in js["images"]}

    # on reconstruit le buffer binaire sans les vues supprimées
    out, view_map, new_views = bytearray(), {}, []
    for i, v in enumerate(views):
        if i in dropped_views:
            continue
        while len(out) % 4:
            out += b"\x00"
        nv = dict(v)
        nv["byteOffset"] = len(out)
        out += view_bytes(i)
        view_map[i] = len(new_views)
        new_views.append(nv)
    js["bufferViews"] = new_views
    for acc in js.get("accessors", []):
        if "bufferView" in acc:
            acc["bufferView"] = view_map[acc["bufferView"]]
    for img in js["images"]:
        img["bufferView"] = view_map[img["bufferView"]]
    while len(out) % 4:
        out += b"\x00"
    js["buffers"] = [{"byteLength": len(out)}]

    jbytes = json.dumps(js, separators=(",", ":")).encode()
    while len(jbytes) % 4:
        jbytes += b" "
    total = 12 + 8 + len(jbytes) + 8 + len(out)
    with open(path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(jbytes), b"JSON") + jbytes)
        f.write(struct.pack("<I4s", len(out), b"BIN\x00") + bytes(out))
