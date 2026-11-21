"""
Vidéo TikTok de VRAI gameplay (vertical 1080 x 1920, 30 images/s), à partir des images filmées dans Unity par
Horror.Tests.FrameRecorder (voir TESTER_DANS_UNITY.md, « Filmer une vidéo »).

Montage : textes manuscrits courts, sons DU JEU (fichiers de Assets/Horror/Audio) recalés sur les plans,
carte de fin avec le logo. Le son n'est pas capturé par Unity : il est remis ici, plan par plan.

Usage : python Tools/Trailer/make_gameplay.py <dossier des images fNNNNN.jpg> <sortie.mp4>
Les bornes des plans (en images) se règlent dans PLANS ci-dessous si on refilme.
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
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR

# (première image, dernière image exclue, texte en surimpression)
PLANS = [
    (0, 95, ["don't look away."]),
    (95, 196, ["the costumes", "are empty."]),
    (196, 237, ["don't answer."]),
    (237, 314, ["\"...Mila? Mila, answer me.", "It's too loud.\""]),
    (314, 397, ["don't touch", "the water."]),
    (397, 447, None),
    (447, 503, None),
]
END_FRAMES = 78                      # carte de fin (2,6 s)


def load(rel, gain=1.0):
    """Charge un son du jeu en mono, au taux d'échantillonnage du montage."""
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


def images():
    shutil.rmtree(FR, ignore_errors=True)
    os.makedirs(FR)
    k = 0
    for a, b, txt in PLANS:
        for i in range(a, b):
            p = os.path.join(SRC, f"f{i:05d}.jpg")
            if not os.path.exists(p):
                continue
            arr = C.to_arr(Image.open(p))
            if txt:
                t = (i - a) / FPS
                alpha = min(1.0, t / 0.25) * min(1.0, max(0.0, ((b - i) / FPS) / 0.2))
                arr = C.over(arr, C.text_layer(txt, size=86, seed=i // 4, y=1500, jitter=3, stroke=4), alpha)
            arr = C.grain(arr, 0.035, seed=i)
            C.to_img(arr).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
            k += 1
    # carte de fin : noir, logo qui tremble, sous-titre
    for j in range(END_FRAMES):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 30)))
        if j > 30:
            arr = C.over(arr, C.text_layer(["play it in VR.", "Meta Quest - coming soon"], size=64, seed=j // 3,
                                           y=1520, fnt=C.F_TYPE, jitter=2), min(1.0, (j - 30) / 12))
        C.to_img(C.grain(arr, 0.04, seed=9000 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    return k


def sound(total_frames):
    T = total_frames / FPS
    s = lambda f: f / FPS                                                    # noqa: E731  (image -> secondes)
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    C.place(bed, loop(load("Ambience/drone_loop.wav", 0.35), s(397)), 0)      # nappe grave jusqu'aux screamers
    C.place(bed, loop(load("Jumpscare/heartbeat_loop.wav", 0.4), s(397 - 95)), s(95))
    C.place(fx, load("Ambience/creak_01.wav", 0.5), 0.4)                      # théâtre
    C.place(fx, load("Ambience/music_box_far.wav", 0.35), 0.2)
    C.place(fx, load("Ambience/creak_02.wav", 0.6), s(95) + 0.6)              # loge
    C.place(fx, load("Ambience/child_giggle_far.wav", 0.35), s(150))
    C.place(fx, load("World/telephone_sonnerie.wav", 0.8)[: int(SR * s(237 - 196))], s(196))
    C.place(fx, load("World/telephone_murmure.wav", 0.9), s(237))
    C.place(fx, loop(load("World/drap_eau_loop.wav", 0.6), s(397 - 314)), s(314))
    C.place(fx, load("Jumpscare/drap_cri.wav", 1.0), s(397))
    C.place(fx, load("Jumpscare/ecoute_cri.wav", 1.0), s(447))
    C.place(fx, C.boom(2.5, 45) * 0.8, s(503) + 0.1)                         # coup grave sur le logo
    st = C.mixdown([(bed, 0.0), (fx, 0.0)], T)
    path = os.path.join(WORK, "son.wav")
    C.write_wav(path, st)
    return path


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n = images()
    wav = sound(n)
    C.encode(FR, wav, OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
