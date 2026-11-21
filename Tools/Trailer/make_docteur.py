"""
TikTok « Le Docteur » en vrai gameplay (vertical 1080 x 1920, 30 i/s, ~16 s), filmé par
Horror.Tests.DoctorScareDirector : caché dans un lit de l'infirmerie, on le regarde passer, lanterne levée, en
fredonnant... il se retourne, se tait, et fonce. Screamer, noir chuchoté, logo.
Son : fichiers DU JEU (comptine, pas, bec, fracas, cri) calés sur evenements.txt ; screamer monté pour téléphone.

Usage : python Tools/Trailer/make_docteur.py <dossier des images + evenements.txt> <sortie.mp4>
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile
from scipy.signal import butter, resample_poly, sosfilt

import common as C

SRC, OUT_MP4 = sys.argv[1], sys.argv[2]
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_docteur")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR
rng = np.random.default_rng(11)

EV = []
for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
    f, what = line.rstrip("\n").split("\t", 1)
    EV.append((int(f), what))
FIRST = lambda key: next(f for f, w in EV if w.startswith(key))           # noqa: E731
ENTER, PASS, SPOT, CRASH, SCARE, WHISPER = (FIRST(k) for k in ("entre", "passe", "etat Traque", "fracas", "screamer", "murmure"))
START = max(0, ENTER + 100)            # on commence quand il sort de derrière le rideau
SCARE_LEN, WHISPER_LEN, END_FRAMES = 64, 52, 48
SNAP = 17


def load(rel, gain=1.0):
    sr, a = wavfile.read(os.path.join(AUDIO, rel))
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


def t(frame):
    return (frame - START) / FPS


def caption(arr, lines, i, a, b, y=430, size=88):
    if not (a <= i < b):
        return arr
    alpha = min(1.0, (i - a) / 5) * min(1.0, (b - i) / 6)
    return C.over(arr, C.text_layer(lines, size=size, seed=i // 4, y=y, jitter=3, stroke=5), alpha)


def images():
    shutil.rmtree(FR, ignore_errors=True)
    os.makedirs(FR)
    k = 0
    for i in range(START, SCARE + SCARE_LEN):
        img = Image.open(os.path.join(SRC, f"f{i:05d}.jpg"))
        if i >= SCARE:
            j = i - SCARE - SNAP
            img = C.zoom(img, 1.0 + (0.2 * np.exp(-j / 6.0) if j >= 0 else 0.0), dx=rng.uniform(-0.012, 0.012), dy=rng.uniform(-0.012, 0.012))
            arr = np.clip(C.to_arr(img), 0, 1) ** 1.8 * 0.95
            if 0 <= j < 4:
                arr = C.glitch(arr, 0.9 - j * 0.2, seed=j)
        else:
            arr = C.to_arr(img)
            arr = caption(arr, ["he only sees", "what his lamp sees."], i, START, START + 75)
            arr = caption(arr, ["don't", "move."], i, PASS - 20, SPOT)
        C.to_img(C.grain(arr, 0.03, seed=i)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    for j in range(WHISPER_LEN):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.text_layer(["now you know", "why we whisper."], size=94, seed=j // 4, y=900, jitter=3,
                                       color=(205, 195, 215)), min(1.0, j / 6) * min(1.0, (WHISPER_LEN - j) / 6))
        C.to_img(C.grain(arr, 0.035, seed=900 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    for j in range(END_FRAMES):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 14)))
        arr = C.over(arr, C.text_layer(["new: the infirmary."], size=66, seed=j // 3, y=1520, fnt=C.F_TYPE, jitter=2), min(1.0, j / 10))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    return k


def phone_scream():
    game = load("Jumpscare/docteur_cri.wav", 1.0)
    hit = rng.standard_normal(int(0.25 * SR)) * np.exp(-np.linspace(0, 14, int(0.25 * SR)))
    n = int(1.7 * SR)
    mix = np.zeros(n)
    for sig, g in ((game, 1.0), (hit, 0.9), (load("Hopital/docteur_bec.wav"), 0.8)):
        m = min(n, len(sig))
        mix[:m] += sig[:m] * g
    mix = np.tanh(2.4 * mix / (np.max(np.abs(mix)) + 1e-9))
    mix *= np.minimum(1, np.linspace(0, 40, n)) * np.minimum(1, np.linspace(14, 0, n))
    return mix


def sound(total):
    T = total / FPS
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    spot_t, scare_t = t(SPOT), t(SCARE)
    C.place(bed, loop(load("Hopital/hopital_bourdon.wav", 0.5), scare_t), 0)
    hum = loop(load("Hopital/docteur_fredonne.wav", 1.0), spot_t)            # il fredonne... jusqu'à ce qu'il te voie
    hum *= np.minimum(1, np.linspace(0.35, 1.3, len(hum)))
    C.place(fx, hum * 0.55, 0)
    for s in np.arange(0.2, spot_t, 0.62):
        C.place(fx, load("Hopital/docteur_pas.wav", 0.28), s)
    C.place(fx, load("Hopital/docteur_bec.wav", 0.9), spot_t)                  # le silence, puis le bec
    for s in np.arange(spot_t + 0.25, scare_t, 0.3):
        C.place(fx, load("Hopital/docteur_pas.wav", 0.55), s)
    C.place(fx, load("Hopital/fracas_perfusion.wav", 0.8), t(CRASH))
    bed *= 3.0; fx *= 3.0
    C.place(fx, phone_scream() * 3.0, scare_t + SNAP / FPS)
    w_t = scare_t + SCARE_LEN / FPS
    C.place(fx, load("Death/murmure_09.wav", 0.45), w_t + 0.1)
    C.place(fx, C.boom(1.8, 40) * 0.5, w_t + WHISPER_LEN / FPS)
    st = C.mixdown([(bed, 0.0), (fx, 0.0)], T)
    path = os.path.join(WORK, "son.wav")
    C.write_wav(path, st)
    return path


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n = images()
    C.encode(FR, sound(n), OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
