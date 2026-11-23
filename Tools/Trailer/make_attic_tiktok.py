"""
TikTok « STAY IN THE LIGHT » (vertical 1080 x 1920, 30 i/s), en vrai jeu au grenier : la poupée, derrière les barreaux de
la cage-refuge, sous une ampoule ; La Veilleuse attend au bord du halo ; la poupée annonce la wishlist ; l'ampoule grésille,
s'éteint, La Veilleuse charge. Voix : Zira, vitesse 1, b1..b4.

Usage : python Tools/Trailer/make_attic_tiktok.py <dossier des images + evenements.txt> <dossier des voix> <sortie.mp4>
evenements.txt : image <TAB> b1..b4, buzz (l'ampoule grésille), charge, fin.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile
from scipy.signal import resample_poly

import common as C

SRC, VOIXDIR, OUT_MP4 = sys.argv[1], sys.argv[2], sys.argv[3]
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_attic")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR
KEYS = ["b1", "b2", "b3", "b4"]

LINES = {
    "b1": "rule number one in the attic. stay under the light.",
    "b2": "she can't step into it. she just waits.",
    "b3": "calm faces is coming to meta quest. wishlist it now.",
    "b4": "no, no, no. not the bulb.",
}

EV = {}
for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
    f, what = line.rstrip("\n").split("\t", 1)
    EV[what.strip()] = int(f)
FIN = EV["fin"]
CAP = 10 ** 9   # pas de casquette dans cette vidéo


def load(path, gain=1.0):
    sr, a = wavfile.read(path if os.path.isabs(path) else os.path.join(AUDIO, path))
    a = a.astype(np.float32)
    if a.ndim > 1:
        a = a.mean(axis=1)
    a /= (np.max(np.abs(a)) + 1e-9)
    if sr != SR:
        g = np.gcd(sr, SR)
        a = resample_poly(a, SR // g, sr // g)
    return a * gain


def loop(clip, dur):
    n = max(1, int(dur * SR))
    return np.tile(clip, n // len(clip) + 1)[:n]


def wrap(text, fnt, size, maxw=960):
    f = C.font(fnt, size)
    out, cur = [], ""
    for w in text.split(" "):
        test = (cur + " " + w).strip()
        if cur and f.getlength(test) > maxw:
            out.append(cur); cur = w
        else:
            cur = test
    out.append(cur)
    return out


def subs():
    """(début, fin, lignes) en images : chaque phrase reste jusqu'à la suivante."""
    out = []
    for i, k in enumerate(KEYS):
        a = EV[k]
        dur = int(len(load(os.path.join(VOIXDIR, k + ".wav"))) / SR * FPS) + 8
        b = min(a + dur, EV[KEYS[i + 1]] if i + 1 < len(KEYS) else FIN)
        out.append((a, b, wrap(LINES[k], C.F_KID, 64)))
    return out


def frames():
    """Dynamisme : coups de zoom à chaque phrase, flash blanc et gros coup quand la casquette apparaît, textes qui rebondissent."""
    shutil.rmtree(FR, ignore_errors=True)
    os.makedirs(FR)
    S = subs()
    rng = np.random.default_rng(11)
    punches = [EV[k] for k in KEYS] + [EV["charge"]]
    k = 0
    for i in range(0, FIN):
        img = Image.open(os.path.join(SRC, f"f{i:05d}.jpg"))
        zoom = 1.04
        for p in punches:
            if 0 <= i - p < 14:
                zoom += 0.09 * np.exp(-(i - p) / 3.5)
        if 0 <= i - CAP < 20:
            zoom += 0.22 * np.exp(-(i - CAP) / 4.0)                          # le gros coup de la casquette
        shake = 0.0
        if 0 <= i - CAP < 10:
            shake = 0.012 * (1 - (i - CAP) / 10)
        if i >= EV["buzz"]:
            shake = max(shake, 0.004 + 0.014 * min(1.0, (i - EV["buzz"]) / 60.0))
        img = C.zoom(img, zoom, cy=0.45, dx=rng.uniform(-shake, shake), dy=rng.uniform(-shake, shake))
        arr = C.to_arr(img)
        arr = np.clip(arr * 1.12, 0, 1)                                      # un peu plus clair (écran de téléphone)
        if 0 <= i - CAP < 6:
            arr = arr + (1 - arr) * (0.85 * (1 - (i - CAP) / 6))           # flash blanc
        if EV["buzz"] <= i < EV["charge"]:
            # l'ampoule grésille puis meurt : l'image clignote et s'assombrit
            fade = (i - EV["buzz"]) / max(1, EV["charge"] - EV["buzz"])
            flick = 0.35 if rng.random() < 0.35 else 1.0
            arr = arr * (flick * (1.0 - 0.55 * fade))
        if i >= EV["charge"]:
            arr = arr * 0.5
        if EV["charge"] <= i < EV["charge"] + 3:
            arr = C.glitch(arr, 0.55, seed=i)
        W3 = EV["b3"]
        if i < W3:   # 1re partie : la règle du grenier
            pop = 1.0 + 0.18 * np.exp(-i / 3.0)
            arr = C.over(arr, C.text_layer(["STAY IN", "THE LIGHT"], size=int(124 * pop), seed=i // 6, y=250, jitter=2, stroke=8,
                                           color=(245, 225, 170)), min(1.0, i / 4))
            arr = C.over(arr, C.text_layer(["she can't step into it"], size=52, seed=i // 6, y=530, fnt=C.F_TYPE, jitter=1,
                                           stroke=5, color=(240, 70, 80)), min(1.0, max(0.0, (i - 6) / 8)))
        else:        # 2e partie : la wishlist (le titre claque)
            pop = 1.0 + 0.35 * np.exp(-(i - W3) / 3.0)
            arr = C.over(arr, C.text_layer(["WISHLIST", "NOW"], size=int(128 * pop), seed=i // 6, y=250, jitter=2, stroke=8,
                                           color=(245, 235, 215)), min(1.0, (i - W3) / 2))
            arr = C.over(arr, C.text_layer(["on the Meta Quest store"], size=54, seed=i // 6, y=530, fnt=C.F_TYPE, jitter=1,
                                           stroke=5, color=(240, 70, 80)), min(1.0, (i - W3) / 4))
        if False:
            s2 = 1.0 + 0.45 * np.exp(-(i - CAP) / 3.0)                       # « MOD-ONLY » claque avec la casquette
            arr = C.over(arr, C.text_layer(["MOD-ONLY IN-GAME CAP"], size=int(60 * s2), seed=i // 6, y=530, fnt=C.F_TYPE,
                                           jitter=1, stroke=6, color=(217, 164, 65)), min(1.0, (i - CAP) / 2))
        for a_, b_, lines in S:
            if a_ <= i < b_:
                al = min(1.0, (i - a_) / 2) * min(1.0, (b_ - i) / 4)
                size = int(64 * (1.0 + 0.22 * np.exp(-(i - a_) / 2.5)))
                arr = C.over(arr, C.text_layer(lines, size=size, seed=i // 5, y=1500, jitter=2, stroke=5, color=(245, 245, 245)), al)
        C.to_img(C.grain(arr, 0.03, seed=i)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    last = Image.open(os.path.join(SRC, f"f{FIN - 1:05d}.jpg"))
    for j in range(6):                                                       # coupure sèche, image qui se déchire
        arr = C.glitch(C.to_arr(C.zoom(last, 1.15 + 0.07 * j, cy=0.42)), 0.95 - j * 0.12, seed=60 + j)
        C.to_img(arr).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    for j in range(8):
        C.to_img(np.zeros((C.H, C.W, 3), np.float32)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    for j in range(66):                                                      # logo + appel
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 10)))
        if j >= 6:
            s1 = 1.0 + 0.3 * np.exp(-(j - 6) / 3.0)
            arr = C.over(arr, C.text_layer(["WISHLIST NOW"], size=int(84 * s1), seed=j // 3, y=1430, jitter=2, color=(245, 235, 215)))
        arr = C.over(arr, C.text_layer(["on Meta Quest · link in bio", "stay in the light"], size=50, seed=j // 3,
                                       y=1560, fnt=C.F_TYPE, jitter=1), min(1.0, max(0.0, (j - 12) / 8)))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    return k


def sound(n):
    T = n / FPS + 1.0
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T)); vo = np.zeros(int(SR * T))
    t = lambda f: f / FPS                                                    # noqa: E731
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), t(FIN)) * 0.5, 0)
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), t(EV["buzz"])) * 0.25, 0)   # le bourdonnement de l'ampoule
    for k in KEYS:
        C.place(vo, load(os.path.join(VOIXDIR, k + ".wav")), t(EV[k]) + 0.05)
    C.place(fx, load("Grenier/ampoule_gresille.wav", 0.8), t(EV["buzz"]))
    C.place(fx, load("Grenier/ampoule_souffle.wav", 0.9), t(EV["buzz"]) + 1.5)
    C.place(fx, load("Grenier/veilleuse_clochette.wav", 0.9), t(EV["charge"]) - 0.5)
    C.place(fx, C.boom(1.0, 60) * 0.6, t(EV["b3"]))                          # « wishlist » : le titre claque
    C.place(fx, C.tick() * 0.9, t(CAP))
    bed *= 2.0; fx *= 2.0; vo *= 2.4
    sc, bm = load("Monster/scream_chase.wav"), C.boom(1.2, 45) * 0.8
    n_ = max(len(sc), len(bm))
    hit = np.tanh(3 * (np.pad(sc, (0, n_ - len(sc))) + np.pad(bm, (0, n_ - len(bm)))))
    C.place(fx, hit * 1.6, t(FIN))
    C.place(fx, C.boom(1.0, 60) * 0.35, t(FIN) + 20 / FPS)
    for k in KEYS:
        C.place(fx, C.tick() * 0.5, t(EV[k]))
    return C.mixdown([(bed, 0.0), (fx, 0.0), (vo, 0.0)], n / FPS)


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n = frames()
    wav = os.path.join(WORK, "son.wav")
    C.write_wav(wav, sound(n))
    C.encode(FR, wav, OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
