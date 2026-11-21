"""Sons du grenier et de La Veilleuse (synthèse, aucun enregistrement extérieur).

Usage (racine du dépôt) : python Tools/Audio/make_grenier_sons.py
Sortie : Assets/Horror/Audio/Grenier/*.wav et Assets/Horror/Audio/Jumpscare/veilleuse_cri.wav
 - veilleuse_clochette : la petite cloche de laiton de son éteignoir, qui tinte quand elle marche (boucle) :
   on l'entend venir (jeu honnête, comme la comptine du Docteur) ;
 - ampoule_souffle : elle étouffe l'ampoule (souffle + cloche qui se pose) ; ampoule_gresille : l'avertissement ;
 - ampoule_rallume : le cordon tiré (déclic) + le filament qui chauffe ; ampoule_bourdon : une ampoule allumée (boucle) ;
 - plancher : une planche qui grince ; grenier_ambiance : vent dans la toiture, poutres (boucle) ;
 - veilleuse_cri : le screamer (souffle inversé qui monte en cri, cloche, impact).
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Horror", "Audio", "Grenier")
JS = os.path.join(ROOT, "Assets", "Horror", "Audio", "Jumpscare")
SR = 44100
rng = np.random.default_rng(1977)


def t(d): return np.arange(int(SR * d)) / SR
def bp(x, lo, hi): return sosfilt(butter(2, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f): return sosfilt(butter(2, f, "low", fs=SR, output="sos"), x)
def hp(x, f): return sosfilt(butter(2, f, "high", fs=SR, output="sos"), x)


def save(folder, name, x, peak=0.9):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    os.makedirs(folder, exist_ok=True)
    wavfile.write(os.path.join(folder, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("->", name, round(len(x) / SR, 2), "s")


def place(track, clip, at):
    s = int(at * SR); n = min(len(clip), len(track) - s)
    if n > 0: track[s:s + n] += clip[:n]


def room(x, tail=0.9, mix=0.3):
    n = int(SR * tail)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 5, n))
    wet = np.convolve(x, lp(ir, 3500))[: len(x) + n]
    wet = np.pad(wet, (0, len(x) + n - len(wet)))
    wet /= np.max(np.abs(wet)) + 1e-9
    return np.concatenate([x, np.zeros(n)]) * (1 - mix) + wet * mix


def bell(f0, d, decay=3.0):
    tt = t(d)
    parts = [(1.0, 1.0), (2.41, 0.5), (3.9, 0.3), (5.3, 0.2)]
    return sum(a * np.sin(2 * np.pi * f0 * r * tt + rng.uniform(0, 6)) * np.exp(-decay * r ** 0.6 * tt) for r, a in parts)


# --- la clochette de l'éteignoir (boucle 2,4 s : deux petits tintements irréguliers)
x = np.zeros(int(2.4 * SR))
for at, f in ((0.15, 2350), (1.25, 2420), (1.42, 2380)):
    place(x, bell(f, 0.9, 6.0) * rng.uniform(0.5, 1.0), at)
save(OUT, "veilleuse_clochette", room(x, 0.6, 0.35)[: len(x)], 0.55)

# --- l'ampoule soufflée : souffle long + cloche de laiton qui se pose + filament qui claque
d = 1.6; tt = t(d)
blow = bp(rng.standard_normal(len(tt)), 400, 3000) * np.minimum(1, tt / 0.25) * np.exp(-2.2 * np.maximum(0, tt - 0.4))
x = blow * 0.8
place(x, bell(820, 1.0, 4.0) * 0.7, 0.55)
place(x, hp(rng.standard_normal(int(0.03 * SR)), 2500) * np.exp(-np.linspace(0, 12, int(0.03 * SR))) * 0.6, 0.6)
save(OUT, "ampoule_souffle", room(x, 0.7, 0.3), 0.85)

# --- l'ampoule grésille (avertissement, 1,5 s)
tt = t(1.5)
buzz = np.sign(np.sin(2 * np.pi * 100 * tt)) * 0.3 + bp(rng.standard_normal(len(tt)), 2000, 7000) * 0.4
gate = (rng.random(len(tt) // 441 + 1) > 0.45).repeat(441)[: len(tt)]
save(OUT, "ampoule_gresille", buzz * gate * np.minimum(1, (1.5 - tt) / 0.1), 0.6)

# --- cordon tiré + filament qui chauffe
x = np.zeros(int(1.2 * SR))
place(x, hp(rng.standard_normal(int(0.02 * SR)), 1500) * np.exp(-np.linspace(0, 15, int(0.02 * SR))), 0.0)
place(x, bell(3100, 0.15, 25.0) * 0.4, 0.02)                                        # la chaînette
hum = np.sin(2 * np.pi * 100 * t(1.0)) * 0.2 * np.minimum(1, t(1.0) / 0.3) * np.exp(-1.5 * t(1.0))
place(x, hum + bp(rng.standard_normal(int(1.0 * SR)), 3000, 8000) * 0.05 * np.exp(-3 * t(1.0)), 0.08)
save(OUT, "ampoule_rallume", x, 0.7)

# --- une ampoule allumée (boucle 3 s : bourdon très léger)
tt = t(3.0)
save(OUT, "ampoule_bourdon", sum(np.sin(2 * np.pi * 100 * k * tt) / k ** 2 for k in range(1, 6)) * (1 + 0.1 * np.sin(2 * np.pi * 0.33 * tt)), 0.25)

# --- une planche qui grince
tt = t(0.7)
f = 260 + 120 * np.sin(2 * np.pi * 1.4 * tt) + 40 * rng.standard_normal(len(tt)).cumsum() / SR * 30
ph = 2 * np.pi * np.cumsum(f) / SR
creak = sum(np.sin(k * ph) / k for k in range(1, 8)) * (rng.random(len(tt)) > 0.3)
save(OUT, "plancher", room(bp(creak, 200, 2500) * np.sin(np.pi * tt / 0.7), 0.5, 0.3), 0.7)

# --- ambiance du grenier (boucle 10 s : vent dans les tuiles, poutres, une goutte)
tt = t(10.0)
wind = lp(rng.standard_normal(len(tt)), 600) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.11 * tt) ** 2)
amb = wind * 0.5
for at in (2.3, 6.8):
    c = np.sin(2 * np.pi * (180 + 60 * t(0.6)) * t(0.6)) * np.sin(np.pi * t(0.6) / 0.6) * 0.15
    place(amb, bp(c, 120, 1500), at)
place(amb, np.sin(2 * np.pi * 1500 * t(0.08)) * np.exp(-40 * t(0.08)) * 0.15, 4.4)
save(OUT, "grenier_ambiance", amb, 0.45)

# --- le screamer : souffle inversé qui monte, cri déchiré, cloche qui hurle, impact
d = 1.9; tt = t(d)
rise = bp(rng.standard_normal(len(tt)), 500, 5000) * (tt / d) ** 3 * (tt < 0.5) * 0.6
f = 380 + 900 * np.minimum(1, np.maximum(0, tt - 0.35) / 0.25) - 220 * np.maximum(0, tt - 0.6)
ph = 2 * np.pi * np.cumsum(f) / SR
scream = sum(np.sin(k * ph + rng.uniform(0, 3)) / k for k in range(1, 14)) * (tt > 0.35) * np.exp(-0.8 * np.maximum(0, tt - 0.4))
scream = bp(scream * (1 + 0.5 * np.sign(np.sin(2 * np.pi * 27 * tt))), 350, 7000)
x = rise + scream
place(x, bell(1250, 1.4, 1.6) * 0.8, 0.36)
place(x, np.sin(2 * np.pi * (40 * t(0.6) + 12 * (1 - np.exp(-8 * t(0.6))))) * np.exp(-3 * t(0.6)) * 1.2, 0.36)
x = np.tanh(2.3 * x) * np.minimum(1, (d - tt) / 0.3)
save(JS, "veilleuse_cri", x, 0.97)

# --- « elle t'a vu » : un seul coup de cloche sec et grave, puis un souffle qui s'arrête net
d = 1.6; tt = t(d)
x = bell(620, d, 2.2) * 0.9 + bell(931, d, 3.4) * 0.35
gasp = bp(rng.standard_normal(len(tt)), 700, 3500) * np.exp(-((tt - 0.25) / 0.12) ** 2) * 0.35
x = room(x + gasp, 0.8, 0.35)[: len(tt)]
save(OUT, "veilleuse_repere", x, 0.95)
