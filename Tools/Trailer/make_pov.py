"""
TikTok « POV » en vrai gameplay (vertical 1080 x 1920, 30 i/s, ~9 s) : on avance dans le couloir, on fait
trop de bruit, on se retourne, L'Écoute arrive en courant -> screamer -> le noir chuchoté -> logo.

Images : Horror.Tests.PovScareDirector (Unity, en Play) qui écrit aussi evenements.txt (image de chaque bruit,
de la claque, du demi-tour, du screamer, du murmure). Le son est remonté ici avec les fichiers DU JEU, recalés
sur ces instants.

Règles de montage (pour qu'on ait envie de revoir, pas de zapper) : une accroche écrite dès la 1re image,
aucun texte pendant la peur, pas de temps mort (on coupe le début de la marche et la fin du noir), fin courte
qui boucle sur le début.

Usage : python Tools/Trailer/make_pov.py <dossier des images + evenements.txt> <sortie.mp4>
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile
from scipy.signal import resample_poly

import common as C

SRC, OUT_MP4 = sys.argv[1], sys.argv[2]
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_pov")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR
rng = np.random.default_rng(7)


def events():
    ev = []
    for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
        f, what = line.rstrip("\n").split("\t", 1)
        ev.append((int(f), what))
    first = lambda key: next(f for f, w in ev if w.startswith(key))      # noqa: E731
    return ev, first


EV, FIRST = events()
SLAP, TURN, SCARE, WHISPER = FIRST("claque"), FIRST("retourne"), FIRST("screamer"), FIRST("murmure")
START = max(0, SLAP - 70)                 # ~2,3 s de marche suffisent
END_WHISPER = WHISPER + 52                # on coupe le noir chuchoté après ~1,7 s
END_FRAMES = 40                           # logo (1,3 s)


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
    n = int(dur * SR)
    return np.tile(clip, n // len(clip) + 1)[:n]


def t(frame):
    """Image source -> secondes dans la vidéo montée."""
    return (frame - START) / FPS


def images():
    shutil.rmtree(FR, ignore_errors=True)
    os.makedirs(FR)
    k = 0
    for i in list(range(START, SCARE + 48)) + list(range(WHISPER, END_WHISPER)):
        p = os.path.join(SRC, f"f{i:05d}.jpg")
        if not os.path.exists(p):
            continue
        img = Image.open(p)
        if SCARE <= i < SCARE + 48:
            # le screamer : plus lisible (le visage restait sombre) + un coup de zoom à l'impact
            j = i - SCARE
            img = C.zoom(img, 1.0 + 0.16 * np.exp(-j / 5.0))
            arr = C.to_arr(img)
            arr = np.clip((arr - 0.02) * 2.1, 0, 1)
            if j < 3:
                arr = C.glitch(arr, 0.8, seed=j)
        else:
            arr = C.to_arr(img)
        if i >= WHISPER:                                          # le murmure, lisible sur un téléphone
            arr = np.zeros_like(arr)
            arr = C.over(arr, C.text_layer(["you breathed", "too loud."], size=96, seed=i // 4, y=900, jitter=3,
                                           color=(205, 195, 215)), min(1.0, (i - WHISPER) / 5))
        if i < SLAP - 4:                                          # l'accroche, dès la première image
            a = min(1.0, (i - START) / 4) * min(1.0, (SLAP - 4 - i) / 6)
            arr = C.over(arr, C.text_layer(["don't make", "a sound."], size=92, seed=i // 4, y=430, jitter=3, stroke=5), a)
        C.to_img(C.grain(arr, 0.03, seed=i)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    for j in range(END_FRAMES):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 14)))
        arr = C.over(arr, C.text_layer(["would you turn around?"], size=66, seed=j // 3, y=1520, fnt=C.F_TYPE, jitter=2),
                     min(1.0, j / 10))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    return k


def sound(total):
    T = total / FPS
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    scare_t = t(SCARE)
    C.place(bed, loop(load("Ambience/drone_loop.wav", 0.3), scare_t), 0)
    C.place(bed, loop(load("Player/breath_loop.wav", 0.25), scare_t), 0)
    steps = [f"Steps/tissu_soft_0{n}.wav" for n in (1, 2, 3)]
    last = -99
    for f, w in EV:                                            # chaque main posée = un bruit réel du jeu
        if w.startswith("bruit Pas") and START <= f < SLAP and f != last:
            C.place(fx, load(steps[rng.integers(0, 3)], 0.55), t(f))
            last = f
    C.place(fx, load("Player/tap_hard_01.wav", 1.0), t(SLAP))  # la claque de trop
    C.place(fx, load("Monster/hear_react_01.wav", 0.8), t(SLAP) + 0.12)
    C.place(fx, load("Monster/scream_chase.wav", 0.75), t(TURN) + 0.1)
    keys = loop(load("Monster/keys_loop.wav", 1.0), scare_t - t(TURN))
    keys *= np.linspace(0.25, 1.0, len(keys)) ** 1.5           # ses clés : de plus en plus près
    C.place(fx, keys * 0.7, t(TURN))
    for k, s in enumerate(np.arange(t(TURN) + 0.3, scare_t, 0.27)):
        C.place(fx, load(f"Monster/step_0{k % 4 + 1}.wav", 0.5 + 0.5 * (s - t(TURN)) / (scare_t - t(TURN))), s)
    C.place(fx, load("Jumpscare/ecoute_cri.wav", 1.0), scare_t)
    w_t = scare_t + 48 / FPS                                   # le murmure démarre avec le noir monté
    C.place(fx, load("Death/murmure_01.wav", 0.8), w_t)
    C.place(fx, C.boom(1.6, 42) * 0.6, w_t + (END_WHISPER - WHISPER) / FPS)
    st = C.mixdown([(bed, 0.0), (fx, 0.0)], T)
    path = os.path.join(WORK, "son.wav")
    C.write_wav(path, st)
    return path


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n = images()
    C.encode(FR, sound(n), OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
