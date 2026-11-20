"""
Teaser TikTok n°1 (vertical, ~17 s) :
  1. dans le noir, notre poupée ; son visage se met à gribouiller : elle crie ;
  2. l'image se déchire (glitch), des phrases du lore apparaissent, des murmures ;
  3. coupure. On revoit la poupée qui fait coucou... sans qu'on voie sa tête. Le masque tombe à ses pieds ;
  4. logo CALM FACES + COMING SOON on Meta Quest Store.
Musique et bruitages générés (drone, verre frotté, boîte à musique désaccordée, cri, murmures, impacts).

Usage : python3 Tools/Trailer/make_teaser.py <dossier de travail>
(le dossier de travail doit contenir www/ avec render.html, LeDessine.glb et les PNG du visage ;
un serveur web local doit servir www/ sur RENDER_PORT, voir common.render).
"""
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile

import common as C

WORK = sys.argv[1]
FR = os.path.join(WORK, "frames_teaser")
OUT = os.path.join(WORK, "out")
os.makedirs(FR, exist_ok=True)
os.makedirs(OUT, exist_ok=True)

T_A, T_SCREAM, T_B, T_CUT, T_C, T_CEND, T_D, T_END = 1.0, 2.6, 4.6, 8.0, 8.4, 12.6, 13.0, 17.0
LORE = ["I will not scream again.", "She hears everything.", "23 beds. 23 masks.",
        "The ink will give her back.", "Don't look away.", "Nobody screams here."]


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


# ---------------------------------------------------------------- 1) plans 3D (15 images/s, effet stop-motion)
def shots():
    out = []
    r = np.random.default_rng(7)
    n_a = int(round((T_B - T_A) * 15))
    for i in range(n_a):                       # A : le visage dans le noir, la caméra s'approche
        t = T_A + i / 15
        screaming = t >= T_SCREAM
        z = 0.98 - 0.34 * smooth((t - T_A) / (T_B - T_A))
        sh = [float(r.uniform(-0.006, 0.006)), float(r.uniform(-0.004, 0.004))] if screaming else [0, 0]
        out.append(dict(out="frames_teaser/a_%03d.png" % i, lights="dark",
                        face=("cry%d" % r.integers(0, 3)) if screaming else "idle",
                        mouth=float(r.uniform(0.8, 1.0)) if screaming else 0.0, mouthFrame=int(r.integers(0, 3)),
                        shake=sh, cam=[0.04, 1.0, z], target=[0, 0.94, 0], fov=34))
    n_b = int(round((T_CUT - T_B) * 15))
    for i in range(n_b):                       # B : caméra à l'épaule, le cri continue (sera déchiré au montage)
        jx, jy = r.uniform(-0.03, 0.03), r.uniform(-0.02, 0.02)
        out.append(dict(out="frames_teaser/b_%03d.png" % i, lights="dark", face="cry%d" % r.integers(0, 3),
                        mouth=float(r.uniform(0.85, 1.0)), mouthFrame=int(r.integers(0, 3)),
                        shake=[float(r.uniform(-0.01, 0.01)), float(r.uniform(-0.006, 0.006))],
                        cam=[0.04 + jx, 1.0 + jy, 0.6], target=[jx * 0.3, 0.94, 0], fov=34))
    n_c = int(round((T_CEND - T_C) * 15))
    for i in range(n_c):                       # C : coucou (sans la tête), le masque tombe
        tc = i / 15
        up = smooth(tc / 0.6)
        speed = 1.4 if tc < 2.7 else max(0.0, 1.4 - (tc - 2.7) * 1.2)        # le coucou ralentit... et se fige
        phase = 2 * math.pi * (1.4 * min(tc, 2.7) + max(0.0, tc - 2.7) * speed * 0.5)
        wave = 8 + 18 * math.sin(phase) * (1 if tc > 0.5 else 0)
        rot = {"Epaule_D": [-22 * up, 0, 14 * up], "Coude_D": [-80 * up, 0, wave * up]}
        shot = dict(out="frames_teaser/c_%03d.png" % i, lights="warm", face="idle", rot=rot,
                    cam=[0.0, 0.45, 1.5 - 0.08 * tc / 4.2], target=[0, 0.30, 0], fov=40)
        tf = tc - 2.4                              # chute du masque : 0,85 s, il papillonne
        if tf >= 0:
            k = min(1.0, tf / 0.85)
            fall = k * k
            land = k >= 1
            settle = min(1.0, max(0.0, tf - 0.85) / 0.25)
            shot["mask"] = dict(
                p=[-0.06 * fall + 0.05 * math.sin(tf * 9) * (1 - fall), MASK_DROP * fall + 0.012 * math.sin(settle * math.pi) * (1 - settle),
                   0.22 * min(1.0, k * 3) + MASK_FWD * fall],
                r=[-76 * fall if land else -76 * fall + 25 * math.sin(tf * 11) * (1 - fall), 0, 30 * math.sin(tf * 8) * (1 - fall) + 12 * fall])
        out.append(shot)
    return out


MASK_DROP, MASK_FWD = -0.855, -0.04   # réglés pour que le masque finisse à plat au sol, devant elle


# ---------------------------------------------------------------- 2) montage image par image (30 images/s)
def frame_at(name, t, t0):
    i = int((t - t0) * 15 + 1e-6)
    while i > 0 and not os.path.exists(os.path.join(FR, "%s_%03d.png" % (name, i))):
        i -= 1
    return Image.open(os.path.join(FR, "%s_%03d.png" % (name, i)))


def compose():
    n = int(T_END * C.FPS)
    black = np.zeros((C.H, C.W, 3), np.float32)
    lore_slots = [(T_B + 0.25 + k * 0.52, LORE[k]) for k in range(len(LORE))]
    for f in range(n):
        t = f / C.FPS
        if t < T_A:
            a = black.copy()
        elif t < T_B:
            img = frame_at("a", t, T_A)
            a = C.to_arr(img) * smooth((t - T_A) / 0.7) * 0.55          # on voit à peine, dans le noir
            a = C.vignette(a, 0.6)
            if t >= T_SCREAM and t < T_SCREAM + 0.12:
                a = np.clip(a * 1.8 + 0.05, 0, 1)                       # flash du cri
        elif t < T_CUT:
            img = frame_at("b", t, T_B)
            p = 0.25 + 0.75 * ((t - T_B) / (T_CUT - T_B)) ** 1.5
            a = C.glitch(C.vignette(C.to_arr(C.zoom(img, 1.0 + 0.08 * p)) * 0.7, 0.5), p if (f % 3) else p * 0.4, f)
            for ts, line in lore_slots:
                if ts <= t < ts + 0.36:
                    a = C.over(a, C.text_layer([line], 104, (225, 40, 55) if f % 4 < 2 else (245, 235, 220), seed=f, stroke=7), 0.97)
                    a = C.glitch(a, 0.12, f + 999)   # léger : le texte doit rester lisible
        elif t < T_C:
            a = black.copy()
        elif t < T_CEND:
            img = frame_at("c", t, T_C)
            a = C.to_arr(img)
            flick = 1.0 if t - T_C > 0.5 else (0.2 if int((t - T_C) * 20) % 3 == 0 else 1.0)   # la lampe s'allume en grésillant
            a *= flick * smooth((t - T_C) / 0.35)
            y = np.linspace(0, 1, C.H)[:, None, None]
            a *= np.clip((y - 0.02) / 0.2, 0, 1)                      # la tête reste dans le noir
        elif t < T_D:
            a = black.copy()
        else:
            td = t - T_D
            reveal = smooth((td - 0.3) / 0.8)
            lg = C.logo(seed=int(f / 3), reveal=reveal if td < 1.1 else 1.0)
            a = C.over(black.copy(), lg, min(1.0, td / 0.4))
            if td > 1.4:
                a = C.over(a, C.text_layer(["Don't make a sound."], 64, (200, 190, 180), seed=int(f / 3), y=1350, jitter=3),
                           min(1.0, (td - 1.4) / 0.4))
            if td > 2.2:
                al = min(1.0, (td - 2.2) / 0.15)
                a = C.over(a, C.text_layer(["COMING SOON"], 96, (240, 230, 215), seed=int(f / 2), fnt=C.F_TYPE, y=1560, jitter=2), al)
                a = C.over(a, C.text_layer(["on Meta Quest Store"], 54, (190, 180, 175), seed=int(f / 2), fnt=C.F_TYPE, y=1660, jitter=1), al)
                if td < 2.35:
                    a = C.glitch(a, 0.5, f)
            if int(t * 30) % 47 == 0:
                a *= 0.55                                               # petit grésillement de temps en temps
        a = C.vignette(a, 0.7)
        a = C.grain(a, 0.035, seed=f)
        C.to_img(a).save(os.path.join(FR, "f_%04d.jpg" % f), quality=92)


# ---------------------------------------------------------------- 3) son
def load(rel):
    sr, x = wavfile.read(os.path.join(C.ROOT, "Assets/Horror/Audio", rel))
    x = x.astype(np.float64) / 32768
    return x if x.ndim == 1 else x.mean(1)


def audio():
    """Bruitages synchronisés sur l'image (cri = flash, grésillements = images déchirées, tac = textes,
    coupures nettes), et une musique flippante mais TRÈS faible, qui change aux mêmes moments que l'image."""
    L = T_END + 0.5
    tr = []
    mono = lambda: np.zeros(int(C.SR * L))
    MU = C.MUSIC
    # --- musique (très faible) : drone + verre frotté jusqu'à la coupure, boîte à musique pendant le coucou
    mu = mono()
    dr = C.drone(T_CUT - T_A)
    dr *= np.minimum(1, np.linspace(0, 3, len(dr))) * np.linspace(0.6, 1.0, len(dr))
    C.place(mu, dr, T_A, MU * 1.3)
    notes = [67, 72, 76, 74, 72, 67, 69, 72]
    C.place(mu, C.music_box(notes[:5], beat=0.42, detune=0.4, wow=0.004), T_C + 0.5, MU * 0.9)    # dès que la lampe est allumée
    C.place(mu, C.music_box([69, 67, 64, 61], beat=0.3, slow=1.5, detune=2.5, wow=0.02), T_C + 2.4, MU * 0.8)  # le masque tombe : elle se dérègle
    mu[int(C.SR * (T_C + 3.25)):int(C.SR * T_D)] = 0          # le masque touche le sol : la musique s'arrête net
    pad = C.drone(T_END - T_D + 0.5)
    pad *= np.minimum(1, np.linspace(0, 4, len(pad)))
    C.place(mu, pad, T_D, MU * 0.8)
    mu[int(C.SR * T_CUT):int(C.SR * T_C)] = 0                 # coupure : silence total
    tr.append((mu, 0.0))
    # --- bruitages
    fx = mono()
    hb = load("Jumpscare/heartbeat_loop.wav")
    C.place(fx, hb[:int(C.SR * (T_SCREAM - 0.2))] * np.linspace(0.2, 0.55, int(C.SR * (T_SCREAM - 0.2))), 0.2, 1.0)
    C.place(fx, load("Player/breath_loop.wav")[:int(C.SR * 1.5)], T_A + 0.1, 0.22)
    C.place(fx, C.boom(2.5, 50), T_SCREAM, 1.0)                # cri = flash à l'image
    scream = C.child_scream(1.9)
    C.place(fx, scream, T_SCREAM, 0.85)
    # grésillements image par image (même logique que le glitch du montage)
    tp = []
    for f in range(int(T_B * 30), int(T_CUT * 30)):
        t = f / 30
        p = 0.25 + 0.75 * ((t - T_B) / (T_CUT - T_B)) ** 1.5
        tp.append((t, (p if f % 3 else p * 0.4) * 0.35))
    C.place(fx, C.glitch_audio(tp)[int(C.SR * T_B):], T_B, 1.0)
    r = np.random.default_rng(11)
    for f in range(int(T_B * 30), int(T_CUT * 30), 4):        # le cri bégaie sur les images déchirées
        if r.random() < 0.5:
            st = int(C.SR * r.uniform(0.2, 1.4))
            g = scream[st:st + int(C.SR / 15)]
            C.place(fx, g * np.hanning(len(g)), f / 30, 0.45)
    for k, line in enumerate(LORE):                           # un « tac » + un murmure pour chaque phrase affichée
        ts = T_B + 0.25 + k * 0.52
        C.place(fx, C.tick(), ts, 0.5)
        tr.append((np.pad(C.ms.whisper(line), (int(C.SR * ts), 0))[:len(fx)] * 0.55, -0.6 if k % 2 == 0 else 0.6))
    C.place(fx, C.riser(T_CUT - T_B - 0.3), T_B + 0.3, 0.3)
    fx[int(C.SR * T_CUT):int(C.SR * T_C)] = 0                 # coupure : silence total
    for i in range(3):                                        # la lampe grésille en s'allumant (calé sur l'image)
        C.place(fx, C.tick() * 0.6, T_C + i * 0.15, 0.4)
    C.place(fx, load("Papers/paper_rustle.wav"), T_C + 2.4, 0.55)         # le masque se détache
    C.place(fx, C.ms.tap(0, hard=True), T_C + 3.25, 0.8)                 # ...et touche le sol
    C.place(fx, load("Ambience/child_giggle_far.wav"), T_C + 3.5, 0.35)
    fx[int(C.SR * T_CEND):int(C.SR * T_D)] = 0
    C.place(fx, C.boom(3.0, 42), T_CEND, 0.9)                 # noir
    C.place(fx, C.ms.whisper("shhh"), T_CEND + 0.1, 0.5)
    for k in range(9):                                        # le crayon gratte pendant que le titre se dessine
        g = C.ms.bandpass(C.ms.noise(0.09), 2500, 7000) * np.hanning(int(C.SR * 0.09))
        C.place(fx, g, T_D + 0.3 + k * 0.09, 0.25)
    C.place(fx, C.tick(), T_D + 1.4, 0.3)                     # « Don't make a sound. »
    C.place(fx, C.boom(2.5, 60), T_D + 2.2, 0.7)              # COMING SOON
    C.place(fx, C.music_box([60, 63, 66], beat=0.02), T_D + 2.2, 0.25)
    C.place(fx, C.ms.whisper("calm faces"), T_D + 2.6, 0.4)
    tr.append((fx, 0.0))
    for sig, _ in tr:                                         # coupures franches : rien ne déborde (même pas l'écho)
        for a, b in ((T_CUT, T_C), (T_CEND, T_D)):
            sig[int(C.SR * a):int(C.SR * b)] = 0
    for sig, _ in tr[1:-1]:                                   # les murmures s'arrêtent net à la coupure
        sig[int(C.SR * T_CUT):] = 0
    st = C.mixdown(tr, L)
    fade = np.ones(len(st))
    fade[-int(C.SR * 0.6):] = np.linspace(1, 0, int(C.SR * 0.6))
    st *= fade[:, None]
    path = os.path.join(WORK, "teaser.wav")
    C.write_wav(path, st)
    return path


if __name__ == "__main__":
    stage = sys.argv[2] if len(sys.argv) > 2 else "all"
    if stage in ("all", "render"):
        C.render(WORK, "doll", shots())
    if stage in ("all", "compose"):
        compose()
    if stage in ("all", "audio", "compose"):
        wav = audio()
        C.encode(FR, wav, os.path.join(OUT, "calm_faces_teaser_tiktok.mp4"))
        print("OK ->", os.path.join(OUT, "calm_faces_teaser_tiktok.mp4"))
