"""
TikTok « WISHLIST NOW + MODERATORS WANTED » (vertical 1080 x 1920, 30 i/s), en vrai jeu : la poupée du joueur annonce
la page du jeu sur le store Meta Quest (wishlist), puis le recrutement des modos ; la casquette du veilleur apparaît,
L'Écoute passe derrière puis approche. Voix : synthèse vocale Windows (Zira, vitesse 1), fichiers w1..w5.

Usage : python Tools/Trailer/make_wishlist_modos.py <dossier des images + evenements.txt> <dossier des voix> <sortie.mp4>
evenements.txt : image <TAB> w1..w5, cap, derriere, fin. L'Écoute passe dès w3.
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
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_wishlist")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR
KEYS = ["w1", "w2", "w3", "w4", "w5"]

LINES = {
    "w1": "big news. calm faces has its page on the meta quest store.",
    "w2": "you can wishlist it right now. link in my bio.",
    "w3": "and i'm still looking for moderators.",
    "w4": "join the discord. moderators get the night watch cap.",
    "w5": "wait. did you hear that?",
}

EV = {}
for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
    f, what = line.rstrip("\n").split("\t", 1)
    EV[what.strip()] = int(f)
FIN = EV["fin"]
CAP = EV["cap"]


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
    punches = [EV[k] for k in KEYS] + [EV["derriere"]]
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
        if i >= EV["derriere"]:
            shake = max(shake, 0.004 + 0.012 * min(1.0, (i - EV["derriere"]) / 100.0))
        img = C.zoom(img, zoom, cy=0.45, dx=rng.uniform(-shake, shake), dy=rng.uniform(-shake, shake))
        arr = C.to_arr(img)
        arr = np.clip(arr * 1.12, 0, 1)                                      # un peu plus clair (écran de téléphone)
        if 0 <= i - CAP < 6:
            arr = arr + (1 - arr) * (0.85 * (1 - (i - CAP) / 6))           # flash blanc
        if EV["derriere"] <= i < EV["derriere"] + 3:
            arr = C.glitch(arr, 0.5, seed=i)
        W3 = EV["w3"]
        if i < W3:   # 1re partie : la wishlist (le titre claque quand la poupée dit « wishlist »)
            pop = 1.0 + 0.18 * np.exp(-i / 3.0) + (0.35 * np.exp(-(i - EV["w2"]) / 3.0) if i >= EV["w2"] else 0.0)
            arr = C.over(arr, C.text_layer(["WISHLIST", "NOW"], size=int(128 * pop), seed=i // 6, y=250, jitter=2, stroke=8,
                                           color=(245, 235, 215)), min(1.0, i / 4))
            arr = C.over(arr, C.text_layer(["on the Meta Quest store"], size=54, seed=i // 6, y=530, fnt=C.F_TYPE, jitter=1,
                                           stroke=5, color=(240, 70, 80)), min(1.0, max(0.0, (i - 6) / 8)))
        else:        # 2e partie : le recrutement
            pop = 1.0 + 0.35 * np.exp(-(i - W3) / 3.0)
            arr = C.over(arr, C.text_layer(["MODERATORS", "WANTED"], size=int(128 * pop), seed=i // 6, y=250, jitter=2, stroke=8,
                                           color=(240, 70, 80)), min(1.0, (i - W3) / 2))
        if W3 <= i < CAP:
            arr = C.over(arr, C.text_layer(["join the discord"], size=54, seed=i // 6, y=530, fnt=C.F_TYPE, jitter=1,
                                           stroke=5, color=(245, 235, 215)), min(1.0, (i - W3) / 4))
        elif i >= CAP:
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
        arr = C.over(arr, C.text_layer(["on Meta Quest · link in bio", "mods wanted: join the discord"], size=50, seed=j // 3,
                                       y=1560, fnt=C.F_TYPE, jitter=1), min(1.0, max(0.0, (j - 12) / 8)))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    return k


def sound(n):
    T = n / FPS + 1.0
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T)); vo = np.zeros(int(SR * T))
    t = lambda f: f / FPS                                                    # noqa: E731
    C.place(bed, loop(load("Ambience/drone_loop.wav"), t(FIN)) * 0.45, 0)
    for k in KEYS:
        C.place(vo, load(os.path.join(VOIXDIR, k + ".wav")), t(EV[k]) + 0.05)
    p0, p1 = EV["w3"], EV["derriere"]
    C.place(fx, loop(load("Monster/keys_loop.wav"), t(FIN) - t(p0)) * np.linspace(0.15, 0.7, int((t(FIN) - t(p0)) * SR)), t(p0))
    for s in np.arange(t(p0), t(FIN), 0.55):
        C.place(fx, load("Monster/step_0%d.wav" % (1 + int(s * 7) % 4), 0.35), s)
    C.place(fx, load("Monster/whisper_03.wav", 0.5), t(p1) + 0.4)
    C.place(fx, C.boom(1.4, 50) * 0.9, t(CAP))                               # la casquette : coup sourd
    C.place(fx, C.boom(1.0, 60) * 0.6, t(EV["w2"]))                          # « wishlist » : le titre claque
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
