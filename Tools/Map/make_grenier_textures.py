# Textures du grenier (bois de plancher, poutres, planches des murs) : procédurales, raccordables, 512 px.
# Usage : python make_grenier_textures.py <dossier de sortie>
import sys, os
import numpy as np
from PIL import Image

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(7)
N = 512

def bruit(scale, octaves=4):
    """Bruit de valeur raccordable (somme d'octaves)."""
    acc = np.zeros((N, N))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        g = max(2, scale * 2 ** o)
        grid = rng.random((g, g))
        x = np.linspace(0, g, N, endpoint=False)
        xi = x.astype(int); xf = x - xi; xf = xf * xf * (3 - 2 * xf)
        a = grid[np.ix_(xi % g, xi % g)]; b = grid[np.ix_(xi % g, (xi + 1) % g)]
        c = grid[np.ix_((xi + 1) % g, xi % g)]; d = grid[np.ix_((xi + 1) % g, (xi + 1) % g)]
        fy = xf[:, None]; fx = xf[None, :]
        acc += amp * ((a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy)
        tot += amp; amp *= 0.5
    return acc / tot

def planches(base, n_planks, grain_scale, dark):
    img = np.zeros((N, N, 3))
    y = np.arange(N)[:, None]; x = np.arange(N)[None, :]
    h = N // n_planks
    grain = bruit(grain_scale, 5)
    fibres = np.sin((y * 0.9 + grain * 60) * 0.55) * 0.5 + 0.5
    for p in range(n_planks):
        tint = 0.78 + rng.random() * 0.35
        rows = slice(p * h, (p + 1) * h)
        img[rows] = np.array(base) * tint
    img *= (0.82 + 0.18 * fibres[..., None])
    img *= (0.85 + 0.3 * bruit(3, 3)[..., None])
    for p in range(n_planks + 1):                                   # joints sombres entre les planches
        r = (p * h) % N
        img[max(0, r - 2):r + 2] *= dark
    # quelques clous / noeuds
    for _ in range(n_planks * 3):
        cy, cx = rng.integers(0, N, 2)
        rr = (y - cy) ** 2 + (x - cx) ** 2
        img[rr < 9] *= 0.45
    return np.clip(img, 0, 1)

def save(arr, name):
    Image.fromarray((arr * 255).astype(np.uint8)).save(os.path.join(out, name))
    print(name)

save(planches((0.42, 0.29, 0.18), 8, 4, 0.35), "grenier_plancher.png")
save(planches((0.33, 0.24, 0.16), 2, 6, 0.6), "grenier_poutre.png")
save(planches((0.36, 0.30, 0.24), 6, 3, 0.4), "grenier_mur.png")
# le drap tendu du projecteur : toile écrue, un peu tachée
toile = np.ones((N, N, 3)) * np.array([0.86, 0.83, 0.76])
toile *= (0.9 + 0.1 * bruit(8, 4)[..., None])
toile *= (0.92 + 0.08 * bruit(2, 2)[..., None])
save(np.clip(toile, 0, 1), "grenier_drap.png")
