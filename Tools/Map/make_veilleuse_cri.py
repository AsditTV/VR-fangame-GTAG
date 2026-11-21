# Le visage de cri de La Veilleuse (montré seulement pendant le screamer) : visage de feutre pâle cousu,
# orbites noires creusées et de travers, pupilles d'épingle, coulures sous les yeux, bouche ouverte trop longue.
# Usage : python make_veilleuse_cri.py <fichier png>
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]
N = 512
rng = np.random.default_rng(23)

def blob(cx, cy, rx, ry, rot, jitter, n=40):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        r = 1 + jitter * (rng.random() - 0.5)
        x, y = rx * r * math.cos(a), ry * r * math.sin(a)
        c, s = math.cos(rot), math.sin(rot)
        pts.append((cx + x * c - y * s, cy + x * s + y * c))
    return pts

base = Image.new("RGBA", (N, N), (0, 0, 0, 0))
d = ImageDraw.Draw(base)
d.polygon(blob(256, 262, 182, 232, 0.0, 0.05, 80), fill=(214, 205, 188, 255))
a = np.array(base).astype(np.float32)
a[..., :3] += (rng.random((N, N)) * 24 - 12)[..., None]
a[..., :3] *= (0.92 - 0.25 * np.linspace(0, 1, N) ** 2)[:, None, None]
base = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")

# ombres creusées autour des orbites et de la bouche (floues)
shade = Image.new("L", (N, N), 0)
sd = ImageDraw.Draw(shade)
sd.polygon(blob(178, 222, 92, 104, -0.25, 0.1), fill=170)
sd.polygon(blob(336, 230, 84, 96, 0.3, 0.1), fill=170)
sd.polygon(blob(258, 410, 66, 96, 0.05, 0.1), fill=150)
shade = shade.filter(ImageFilter.GaussianBlur(22))
dark = Image.new("RGBA", (N, N), (25, 12, 16, 255))
mask = Image.fromarray((np.array(shade) * (np.array(base)[..., 3] > 0)).astype(np.uint8))
base.paste(dark, (0, 0), mask)

d = ImageDraw.Draw(base)
d.polygon(blob(178, 222, 64, 78, -0.25, 0.12), fill=(8, 5, 7, 255))          # orbite gauche, plus grande
d.polygon(blob(336, 232, 56, 68, 0.3, 0.12), fill=(8, 5, 7, 255))            # orbite droite, plus basse
d.ellipse((171, 226, 181, 238), fill=(240, 236, 222, 255))                  # pupilles d'épingle, qui ne regardent
d.ellipse((341, 222, 350, 233), fill=(240, 236, 222, 255))                  # pas au même endroit
for (x0, y0, ln) in ((160, 290, 120), (196, 296, 70), (330, 296, 140), (350, 292, 60)):   # coulures
    for k in range(ln):
        y = y0 + k; x = x0 + 3 * math.sin(k / 9.0)
        w = max(1, 6 - k // 25)
        d.ellipse((x - w, y - w, x + w, y + w), fill=(20, 10, 12, 230))
d.polygon(blob(258, 412, 44, 78, 0.04, 0.18), fill=(14, 4, 6, 255))           # bouche ouverte, trop longue
for x in range(176, 346, 17):                                               # la couture arrachée de la bouche
    d.line((x, 318 + (x % 3) * 3, x + 8, 340), fill=(90, 46, 44, 255), width=4)
d.line((256, 36, 252, 120), fill=(110, 90, 82, 255), width=3)               # couture du front
for y in range(44, 120, 15):
    d.line((242, y, 264, y + 7), fill=(110, 90, 82, 255), width=3)
base = base.filter(ImageFilter.GaussianBlur(0.7))
base.save(out)
print(out)
