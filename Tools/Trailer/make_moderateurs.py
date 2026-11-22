"""
TikTok « MODERATORS WANTED » (vertical 1080 x 1920, 30 i/s), en vrai jeu : la poupée du joueur parle face caméra
au dortoir (Horror.Tests.TiktokActor, bouche calée sur la voix), L'Écoute passe derrière elle, puis s'arrête dans
son dos à la dernière phrase. Titre « MODERATORS WANTED » affiché tout du long, sous-titres, fin sur le logo.
Voix du joueur : synthèse vocale Windows (Tools/Audio/tts_windows.ps1), fichiers m1..m5.

Usage : python Tools/Trailer/make_moderateurs.py <dossier des images + evenements.txt> <dossier des voix> <sortie.mp4>
evenements.txt : image <TAB> m1..m5 (début de chaque phrase), passage, derriere, fin.
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
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_moderateurs")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR

LINES = {
    "m1": "hey. quick thing. keep your voice down.",
    "m2": "calm faces is coming out soon on meta quest, and i need your help.",
    "m3": "i'm looking for moderators for the discord.",
    "m4": "if you're chill, mature, and you know how to stay calm, join the discord. link + form in my bio.",
    "m5": "wait. why are you looking behind me?",
}

EV = {}
for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
    f, what = line.rstrip("\n").split("\t", 1)
    EV[what] = int(f)
FIN = EV["fin"]


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
    keys = ["m1", "m2", "m3", "m4", "m5"]
    out = []
    for i, k in enumerate(keys):
        a = EV[k]
        dur = int(len(load(os.path.join(VOIXDIR, k + ".wav"))) / SR * FPS) + 8
        b = min(a + dur, EV[keys[i + 1]] if i + 1 < len(keys) else FIN)
        out.append((a, b, wrap(LINES[k], C.F_KID, 64)))
    return out


def title_layer(i):
    """« MODERATORS WANTED » : gros titre fixe en haut, qui tremble à peine (comme le reste du jeu)."""
    lay = C.text_layer(["MODERATORS", "WANTED"], size=128, seed=i // 6, y=250, jitter=2, stroke=8, color=(240, 70, 80))
    return lay


def frames():
    """Dynamisme : petit zoom brusque à chaque phrase, tremblement quand elle arrive, textes qui rebondissent."""
    shutil.rmtree(FR, ignore_errors=True)
    os.makedirs(FR)
    S = subs()
    rng = np.random.default_rng(7)
    punches = [EV[k] for k in ("m1", "m2", "m3", "m4", "m5")] + [EV["derriere"], EV["m5"] + 60]
    k = 0
    for i in range(0, FIN):
        img = Image.open(os.path.join(SRC, f"f{i:05d}.jpg"))
        zoom = 1.04                                                          # léger recadrage permanent (bord bas)
        for p in punches:
            if 0 <= i - p < 14:
                zoom += 0.10 * np.exp(-(i - p) / 3.5)                        # coup de zoom qui retombe
        shake = 0.0
        if i >= EV["derriere"]:
            shake = 0.004 + 0.012 * min(1.0, (i - EV["derriere"]) / 120.0)   # ça tremble de plus en plus
        img = C.zoom(img, zoom, cy=0.47, dx=rng.uniform(-shake, shake), dy=rng.uniform(-shake, shake))
        arr = C.to_arr(img)
        if EV["derriere"] <= i < EV["derriere"] + 3:
            arr = C.glitch(arr, 0.55, seed=i)                                # flash de glitch quand elle s'approche
        pop = 1.0 + 0.18 * np.exp(-i / 3.0)
        arr = C.over(arr, C.text_layer(["MODERATORS", "WANTED"], size=int(128 * pop), seed=i // 6, y=250, jitter=2, stroke=8,
                                       color=(240, 70, 80)), min(1.0, i / 4))
        arr = C.over(arr, C.text_layer(["discord · link in bio"], size=54, seed=i // 6, y=530, fnt=C.F_TYPE, jitter=1, stroke=5,
                                       color=(245, 235, 215)), min(1.0, max(0.0, (i - 6) / 8)))
        for a_, b_, lines in S:
            if a_ <= i < b_:
                al = min(1.0, (i - a_) / 2) * min(1.0, (b_ - i) / 4)
                size = int(64 * (1.0 + 0.22 * np.exp(-(i - a_) / 2.5)))      # le sous-titre rebondit à son arrivée
                arr = C.over(arr, C.text_layer(lines, size=size, seed=i // 5, y=1500, jitter=2, stroke=5, color=(245, 245, 245)), al)
        C.to_img(C.grain(arr, 0.03, seed=i)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    last = Image.open(os.path.join(SRC, f"f{FIN - 1:05d}.jpg"))
    for j in range(6):                                                       # coupure sèche, image qui se déchire
        arr = C.glitch(C.to_arr(C.zoom(last, 1.15 + 0.07 * j, cy=0.42)), 0.95 - j * 0.12, seed=60 + j)
        C.to_img(arr).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    for j in range(12):                                                      # noir
        C.to_img(np.zeros((C.H, C.W, 3), np.float32)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    for j in range(75):                                                      # logo + appel, qui claquent
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 10)))
        s1 = 1.0 + 0.3 * np.exp(-max(0, j - 6) / 3.0) if j >= 6 else 0.0
        if s1:
            arr = C.over(arr, C.text_layer(["MODERATORS WANTED"], size=int(84 * s1), seed=j // 3, y=1430, jitter=2, color=(240, 70, 80)))
        arr = C.over(arr, C.text_layer(["join the discord · apply via the form", "(link in bio)"], size=52, seed=j // 3, y=1560,
                                       fnt=C.F_TYPE, jitter=1), min(1.0, max(0.0, (j - 14) / 8)))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    return k


def sound(n):
    T = n / FPS + 1.0
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T)); vo = np.zeros(int(SR * T))
    t = lambda f: f / FPS                                                    # noqa: E731
    C.place(bed, loop(load("Ambience/drone_loop.wav"), t(FIN)) * 0.45, 0)
    for k in ["m1", "m2", "m3", "m4", "m5"]:
        C.place(vo, load(os.path.join(VOIXDIR, k + ".wav")), t(EV[k]) + 0.05)
    p0, p1 = EV["passage"], EV["derriere"]
    C.place(fx, loop(load("Monster/keys_loop.wav"), t(FIN) - t(p0)) * np.linspace(0.15, 0.7, int((t(FIN) - t(p0)) * SR)), t(p0))
    for s in np.arange(t(p0), t(FIN), 0.55):
        C.place(fx, load("Monster/step_0%d.wav" % (1 + int(s * 7) % 4), 0.35), s)
    C.place(fx, load("Monster/whisper_03.wav", 0.5), t(p1) + 0.4)
    bed *= 2.0; fx *= 2.0; vo *= 2.4
    sc, bm = load("Monster/scream_chase.wav"), C.boom(1.2, 45) * 0.8
    n_ = max(len(sc), len(bm))
    hit = np.tanh(3 * (np.pad(sc, (0, n_ - len(sc))) + np.pad(bm, (0, n_ - len(bm)))))
    C.place(fx, hit * 1.6, t(FIN))
    C.place(fx, C.boom(1.8, 40) * 0.5, t(FIN) + 18 / FPS)
    C.place(fx, C.boom(1.0, 60) * 0.35, t(FIN) + 24 / FPS)                 # le titre claque
    for k in ["m1", "m2", "m3", "m4", "m5"]:
        C.place(fx, C.tick() * 0.5, t(EV[k]))                              # petit tac sur chaque coup de zoom
    return C.mixdown([(bed, 0.0), (fx, 0.0), (vo, 0.0)], n / FPS)


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n = frames()
    wav = os.path.join(WORK, "son.wav")
    C.write_wav(wav, sound(n))
    C.encode(FR, wav, OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
