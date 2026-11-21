"""Les cassettes du lore (voix des années 70 retrouvées au grenier, à l'infirmerie, au dortoir).

1. Voix brutes : synthèse vocale de Windows (gratuite, hors ligne), via Tools/Audio/tts_windows.ps1 :
   powershell -File Tools/Audio/tts_windows.ps1 -voice "Microsoft Hazel Desktop" -rate -3 -text "..." -out raw1.wav
   (textes ci-dessous, dans CASSETTES). 2. Ce script : python Tools/Audio/make_cassettes.py DOSSIER_DES_RAW
   -> Assets/Horror/Audio/Lore/cassette_N.wav, traitées « vieille bande » : hauteur changée (le Docteur grave),
   bande passante de magnétophone, pleurage, souffle, craquements, déclics de touche au début et à la fin.
   La voix 2 est « chuchotée » (bruit modulé par l'enveloppe de la voix) : une patiente qui a peur d'être entendue.
"""
import os
import subprocess
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Horror", "Audio", "Lore")
SR = 44100
rng = np.random.default_rng(1974)

CASSETTES = [
    # (fichier brut, sortie, demi-tons, chuchotée, texte = sous-titres du jeu)
    ("raw1.wav", "cassette_1_veilleuse", -2.0, False),
    ("raw2.wav", "cassette_2_patiente", 0.0, True),
    ("raw3.wav", "cassette_3_docteur", -6.0, False),
]


def bp(x, lo, hi):
    return sosfilt(butter(2, [lo, hi], "band", fs=SR, output="sos"), x)


def lp(x, f):
    return sosfilt(butter(2, f, "low", fs=SR, output="sos"), x)


def pitch(src, semis, dst):
    """Change la hauteur sans changer la durée (ffmpeg : asetrate + atempo)."""
    r = 2 ** (semis / 12.0)
    tempo = 1.0 / r
    chain = []
    while tempo > 2.0: chain.append("atempo=2.0"); tempo /= 2.0
    while tempo < 0.5: chain.append("atempo=0.5"); tempo /= 0.5
    chain.append(f"atempo={tempo:.5f}")
    flt = f"asetrate={int(SR * r)},aresample={SR}," + ",".join(chain)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, "-af", flt, "-ac", "1", "-ar", str(SR), dst], check=True)


def load(p):
    sr, x = wavfile.read(p)
    x = x.astype(np.float32) / 32768.0
    return x if x.ndim == 1 else x.mean(axis=1)


def wow(x, depth=0.0025, rate=0.6):
    """Pleurage : la bande tourne un peu irrégulièrement."""
    t = np.arange(len(x)) / SR
    shift = depth * SR * (np.sin(2 * np.pi * rate * t) + 0.4 * np.sin(2 * np.pi * 3.1 * t))
    idx = np.clip(np.arange(len(x)) + shift, 0, len(x) - 1)
    return np.interp(idx, np.arange(len(x)), x)


def whisper(x):
    env = lp(np.abs(x), 30.0)
    env /= env.max() + 1e-9
    noise = bp(rng.standard_normal(len(x)), 900, 6500)
    return noise * env * 0.9 + bp(x, 300, 3000) * 0.18


def click(n=0.05):
    m = int(n * SR)
    c = rng.standard_normal(m) * np.exp(-np.linspace(0, 30, m)) * 0.6
    c[: int(0.004 * SR)] += 0.8
    return c


def tape(x):
    x = bp(x, 220, 3800)
    x = wow(x)
    x = np.tanh(x * 1.8) / np.tanh(1.8)
    hiss = bp(rng.standard_normal(len(x)), 2000, 9000) * 0.035
    crack = np.zeros(len(x))
    for _ in range(int(len(x) / SR * 3)):
        i = rng.integers(0, len(x) - 200)
        crack[i:i + 60] += rng.standard_normal(60) * np.exp(-np.linspace(0, 6, 60)) * rng.uniform(0.05, 0.25)
    hum = 0.01 * np.sin(2 * np.pi * 50 * np.arange(len(x)) / SR)
    pad = np.zeros(int(0.5 * SR))
    body = np.concatenate([pad, x, pad])
    noise = np.concatenate([pad, hiss + crack + hum, pad])
    noise[: len(pad)] = bp(rng.standard_normal(len(pad)), 2000, 9000) * 0.035
    noise[-len(pad):] = bp(rng.standard_normal(len(pad)), 2000, 9000) * 0.035
    y = np.concatenate([click(), body + noise, click()])
    return y / (np.max(np.abs(y)) + 1e-9) * 0.9


def main(raw_dir):
    os.makedirs(OUT, exist_ok=True)
    for raw, name, semis, wh in CASSETTES:
        src = os.path.join(raw_dir, raw)
        tmp = os.path.join(raw_dir, "p_" + raw)
        if semis != 0.0: pitch(src, semis, tmp)
        else: tmp = src
        x = load(tmp)
        if wh: x = whisper(x)
        y = tape(x)
        wavfile.write(os.path.join(OUT, name + ".wav"), SR, (y * 32767).astype(np.int16))
        print("->", name, round(len(y) / SR, 1), "s")


if __name__ == "__main__":
    main(sys.argv[1])
