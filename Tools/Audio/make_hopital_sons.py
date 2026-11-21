"""Sons de l'infirmerie du sous-sol et du Docteur (synthèse, aucun enregistrement extérieur).

Usage (depuis la racine du dépôt) : python Tools/Audio/make_hopital_sons.py
Sortie : Assets/Horror/Audio/Hopital/*.wav et Assets/Horror/Audio/Jumpscare/docteur_cri.wav
 - docteur_fredonne : il fredonne une comptine (en boucle) : on l'entend venir, c'est voulu (jeu honnête) ;
 - docteur_bec : claquement du bec ; docteur_souffle : respiration dans le masque (boucle) ;
 - docteur_pas : bottine lourde ; docteur_cri : le screamer (croassement + bec + souffle + impact, monté fort) ;
 - fracas_metal, fracas_verre, fracas_perfusion : les objets fragiles (ÉNORME bruit) ;
 - monte_charge : le monte-charge qui grince ; hopital_bourdon : néons + gouttes + bip lointain (boucle).
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Horror", "Audio", "Hopital")
JS = os.path.join(ROOT, "Assets", "Horror", "Audio", "Jumpscare")
SR = 44100
rng = np.random.default_rng(1985)


def t(d):
    return np.arange(int(SR * d)) / SR


def bp(x, lo, hi):
    return sosfilt(butter(2, [lo, hi], "band", fs=SR, output="sos"), x)


def lp(x, f):
    return sosfilt(butter(2, f, "low", fs=SR, output="sos"), x)


def hp(x, f):
    return sosfilt(butter(2, f, "high", fs=SR, output="sos"), x)


def env(n, a=0.005, r=0.1):
    e = np.ones(n)
    na, nr = int(a * SR), int(r * SR)
    e[:na] = np.linspace(0, 1, max(1, na))
    e[-nr:] = np.linspace(1, 0, max(1, nr))
    return e


def room(x, tail=0.8, mix=0.25):
    n = int(SR * tail)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 6, n))
    wet = np.convolve(x, lp(ir, 4000))[: len(x) + n]
    wet = np.pad(wet, (0, len(x) + n - len(wet)))
    wet /= np.max(np.abs(wet)) + 1e-9
    return np.concatenate([x, np.zeros(n)]) * (1 - mix) + wet * mix


def save(folder, name, x, peak=0.9):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    os.makedirs(folder, exist_ok=True)
    wavfile.write(os.path.join(folder, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("->", name, round(len(x) / SR, 2), "s")


def clang(f0, d, decay=5.0):
    tt = t(d)
    partials = [(1.0, 1.0), (2.76, 0.6), (5.4, 0.4), (8.9, 0.25)]
    x = sum(a * np.sin(2 * np.pi * f0 * r * tt + rng.uniform(0, 6)) * np.exp(-decay * r ** 0.5 * tt) for r, a in partials)
    return x + bp(rng.standard_normal(len(tt)), 2000, 9000) * np.exp(-60 * tt) * 0.8


def place(track, clip, at):
    s = int(at * SR)
    n = min(len(clip), len(track) - s)
    if n > 0:
        track[s:s + n] += clip[:n]


# --- le Docteur fredonne (une comptine originale, « mmm » nasal, un peu faux)
notes = [(67, 0.5), (64, 0.5), (65, 0.5), (62, 0.75), (0, 0.25), (64, 0.5), (60, 0.5), (62, 1.0), (0, 0.5),
         (67, 0.5), (69, 0.5), (67, 0.5), (64, 0.5), (65, 0.5), (62, 0.5), (60, 1.25), (0, 0.75)]
hum = np.zeros(0)
for m, d in notes:
    tt = t(d * 0.9)
    if m == 0:
        hum = np.concatenate([hum, np.zeros(len(tt))])
        continue
    f = 440 * 2 ** ((m - 69 - 12) / 12) * (1 + 0.006 * np.sin(2 * np.pi * 5.2 * tt)) * (1 + rng.uniform(-0.01, 0.01))
    ph = 2 * np.pi * np.cumsum(f) / SR
    v = sum(np.sin(k * ph) / k ** 1.3 for k in range(1, 9))          # voix nasale fermée
    v = bp(v, 180, 1400) + 0.05 * bp(rng.standard_normal(len(tt)), 300, 3000)
    hum = np.concatenate([hum, v * env(len(tt), 0.06, 0.12)])
save(OUT, "docteur_fredonne", room(hum, 1.2, 0.35)[: len(hum)], 0.7)

# --- bec qui claque (deux coups de bois sec)
x = np.zeros(int(0.35 * SR))
for at in (0.0, 0.09):
    c = bp(rng.standard_normal(int(0.04 * SR)), 900, 4500) * np.exp(-np.linspace(0, 9, int(0.04 * SR)))
    c += 0.6 * np.sin(2 * np.pi * 620 * t(0.04)) * np.exp(-70 * t(0.04))
    place(x, c, at)
save(OUT, "docteur_bec", room(x, 0.4, 0.2), 0.8)

# --- souffle dans le masque (boucle de 4 s : inspiration, expiration rauque)
tt = t(4.0)
breath = bp(rng.standard_normal(len(tt)), 300, 2200) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.25 * tt - 1.2)) ** 2
breath += 0.25 * bp(rng.standard_normal(len(tt)), 80, 300) * (np.sin(2 * np.pi * 0.25 * tt) > 0.3)
save(OUT, "docteur_souffle", breath, 0.6)

# --- pas de bottine lourde
step = lp(rng.standard_normal(int(0.3 * SR)), 900) * np.exp(-np.linspace(0, 12, int(0.3 * SR)))
step += 0.8 * np.sin(2 * np.pi * 70 * t(0.3)) * np.exp(-18 * t(0.3))
save(OUT, "docteur_pas", room(step, 0.5, 0.25), 0.8)

# --- le screamer : croassement déchiré + bec + souffle + impact grave, saturé (fait pour sursauter)
d = 1.9
tt = t(d)
f = 520 - 180 * tt + 60 * np.sin(2 * np.pi * 23 * tt)
ph = 2 * np.pi * np.cumsum(f) / SR
caw = sum(np.sin(k * ph + rng.uniform(0, 3)) / k for k in range(1, 14))
caw = bp(caw * (1 + 0.6 * np.sign(np.sin(2 * np.pi * 31 * tt))), 400, 6500)
caw *= np.minimum(1, tt / 0.015) * np.exp(-0.9 * tt)
sub = np.sin(2 * np.pi * (34 * tt + 14 * (1 - np.exp(-8 * tt)))) * np.exp(-2.2 * tt) * 1.3
snap = rng.standard_normal(len(tt)) * np.exp(-45 * tt)
hiss = bp(rng.standard_normal(len(tt)), 1500, 8000) * np.exp(-1.5 * tt) * 0.5
scream = np.tanh(2.4 * (caw + sub + hp(snap, 900) * 0.9 + hiss))
scream *= np.minimum(1, (d - tt) / 0.3)
save(JS, "docteur_cri", scream, 0.97)

# --- fracas : plateau d'instruments (beaucoup de métal), verre, pied à perfusion
x = np.zeros(int(2.2 * SR))
for k in range(18):
    place(x, clang(rng.uniform(900, 3200), 0.8, rng.uniform(4, 9)) * rng.uniform(0.3, 1.0), max(0, rng.exponential(0.12) + (0 if k < 4 else 0.1)))
place(x, clang(260, 1.6, 2.5) * 1.2, 0.0)
save(OUT, "fracas_metal", room(x, 1.2, 0.3), 0.97)
x = np.zeros(int(2.0 * SR))
for k in range(40):
    n = int(rng.uniform(0.02, 0.2) * SR)
    shard = hp(rng.standard_normal(n), 3000) * np.exp(-np.linspace(0, rng.uniform(5, 14), n))
    shard += 0.4 * np.sin(2 * np.pi * rng.uniform(2500, 6000) * np.arange(n) / SR) * np.exp(-np.linspace(0, 10, n))
    place(x, shard * rng.uniform(0.2, 1), rng.exponential(0.15))
place(x, lp(rng.standard_normal(int(0.2 * SR)), 600) * np.exp(-np.linspace(0, 10, int(0.2 * SR))) * 1.5, 0.0)
save(OUT, "fracas_verre", room(x, 1.0, 0.3), 0.97)
x = np.zeros(int(2.0 * SR))
place(x, clang(420, 1.8, 2.0), 0.0)
place(x, clang(610, 1.2, 3.0) * 0.7, 0.18)
place(x, clang(380, 1.0, 4.0) * 0.5, 0.42)
save(OUT, "fracas_perfusion", room(x, 1.0, 0.3), 0.95)

# --- monte-charge (grincement, câbles, choc à l'arrivée)
tt = t(3.2)
grind = bp(rng.standard_normal(len(tt)), 150, 900) * (0.6 + 0.4 * np.sin(2 * np.pi * 1.7 * tt))
squeal = np.sin(2 * np.pi * (900 + 200 * np.sin(2 * np.pi * 0.6 * tt)) * tt) * 0.15 * (tt > 0.5)
mc = (grind + squeal) * np.minimum(1, tt / 0.3)
place(mc, clang(140, 1.0, 3.0) * 1.5, 2.6)
save(OUT, "monte_charge", room(mc, 0.8, 0.25), 0.85)

# --- bourdon de l'infirmerie (boucle 8 s : néons, gouttes, bip très lointain)
tt = t(8.0)
hum2 = sum(np.sin(2 * np.pi * 100 * k * tt) / k ** 1.6 for k in range(1, 8)) * 0.15
hum2 *= 1 + 0.15 * np.sin(2 * np.pi * 0.37 * tt)
hum2 += 0.05 * bp(rng.standard_normal(len(tt)), 2000, 6000)
for at in (1.3, 3.9, 6.2):
    place(hum2, np.sin(2 * np.pi * 1700 * t(0.06)) * np.exp(-40 * t(0.06)) * 0.25, at)
for at in (0.6, 2.2, 5.1, 7.0):
    place(hum2, np.sin(2 * np.pi * 980 * t(0.12)) * env(int(0.12 * SR), 0.005, 0.05) * 0.06, at)
save(OUT, "hopital_bourdon", hum2, 0.5)
