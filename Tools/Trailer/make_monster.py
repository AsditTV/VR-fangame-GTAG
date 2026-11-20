"""
Animation TikTok n°3 (vertical, ~14 s) : L'Écoute, le monstre aveugle.
Au fond du couloir des chambres, elle murmure. Un petit « tac » (une main qui touche le sol) :
elle se retourne d'un coup. « She can't see you. » Elle avance, bras écartés. « She HEARS you. »
« Don't. Breathe. » -> elle fonce -> jumpscare -> logo + COMING SOON.
Musique très faible, calée sur les coupes ; bruitages synchronisés sur l'image.

Usage : python3 Tools/Trailer/make_monster.py <dossier de travail> [render|compose|audio|all]
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile

import common as C

WORK = sys.argv[1]
FR = os.path.join(WORK, "frames_monster")
OUT = os.path.join(WORK, "out")
os.makedirs(FR, exist_ok=True)
os.makedirs(OUT, exist_ok=True)

PLANS = [(0.5, 3.4, "s1"), (3.5, 5.1, "s2"), (5.25, 7.25, "s3"), (7.4, 8.6, "s4"), (8.6, 9.6, "s5")]
T_TAP, T_LOGO, T_END = 3.4, 9.85, 13.8
STEPS = [5.35, 5.85, 6.35, 6.85]           # ses pas pendant s3 (l'image tremble à chaque pas)
CAPTIONS = [(0.9, 3.3, "\"Where are you, little one?\"", (200, 190, 215), 70),
            (3.35, 3.65, "*tap*", (240, 230, 215), 64),
            (3.75, 5.0, "She can't see you.", (240, 230, 215), 92),
            (5.4, 7.2, "She HEARS you.", (225, 45, 60), 104),
            (7.55, 8.55, "Don't. Breathe.", (240, 230, 215), 96)]


def arms(a, s):
    return {"Epaule_G": [-a, 0, s], "Epaule_D": [-a, 0, -s]}


def shots():
    corridor = [dict(c=0xf2a7b9, i=5, d=7, p=[34, 1.9, 1.6]), dict(c=0xf2a7b9, i=4, d=7, p=[24, 1.9, -1.6]),
                dict(c=0xbfcfff, i=40, d=14, p=[30, 4.2, 0], spot=[32.5, 1.0, 0], angle=0.45)]

    def shot(name, cam, tgt, rot, yaw=-90, front=0.0, fov=44):
        ex = corridor + ([dict(c=0xd8e0ff, i=front, d=4, p=[cam[0] - 0.3, cam[1] + 0.2, 0.3])] if front else [])
        return dict(lights="custom", ambientI=0.35, extra=ex, fog=0.035, out="frames_monster/%s.png" % name,
                    monster=dict(pos=[32.5, 0, 0], yaw=yaw, rot=rot), cam=cam, target=tgt, fov=fov)
    return [
        shot("s1", [16, 1.0, 0], [32, 1.4, 0], {"Tete": [0, 0, 15]}, yaw=0),                    # de profil, elle écoute
        shot("s2", [22, 1.0, 0], [32, 1.7, 0], {"Tete": [-10, 0, 24]}),                          # retournée d'un coup
        shot("s3", [27, 1.05, 0], [32, 1.8, 0], arms(42, 32), front=1.5),                          # elle avance, bras écartés
        shot("s4", [29.6, 1.3, 0.04], [32.2, 1.95, 0], dict(arms(60, 40), Buste=[20, 0, 0], Tete=[0, 0, -15]), front=3),
        shot("s5", [31.0, 1.6, 0.05], [32.3, 2.0, 0], dict(arms(30, 50), Buste=[22, 0, 0], Tete=[0, 0, 18]), front=4, fov=50),
    ]


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def compose():
    imgs = {name: Image.open(os.path.join(FR, name + ".png")) for _, _, name in PLANS}
    black = np.zeros((C.H, C.W, 3), np.float32)
    r = np.random.default_rng(5)
    for f in range(int(T_END * C.FPS)):
        t = f / C.FPS
        a = black.copy()
        for t0, t1, name in PLANS:
            if t0 <= t < t1:
                k = (t - t0) / (t1 - t0)
                if name == "s5":
                    img = C.zoom(imgs[name], 1.0 + 0.3 * k, dx=r.uniform(-0.015, 0.015), dy=r.uniform(-0.015, 0.015))
                    a = C.glitch(C.to_arr(img) * (1.7 if f % 4 < 2 else 1.0), 0.35 + 0.5 * k, f)
                else:
                    sh = sum(np.exp(-(t - s) * 18) * 0.006 for s in STEPS if s <= t < s + 0.3)      # secousse à chaque pas
                    push = {"s1": 0.05, "s2": 0.04, "s3": 0.12, "s4": 0.06}[name]
                    img = C.zoom(imgs[name], 1.02 + push * k, dx=r.uniform(-sh, sh), dy=r.uniform(-sh, sh))
                    a = C.to_arr(img) * smooth((t - t0) / (0.6 if name == "s1" else 0.08))
        if t >= T_LOGO:
            td = t - T_LOGO
            lg = C.logo(seed=int(f / 3), reveal=smooth((td - 0.2) / 0.7) if td < 0.95 else 1.0)
            a = C.over(black.copy(), lg, min(1.0, td / 0.3))
            if td > 1.2:
                a = C.over(a, C.text_layer(["Don't make a sound."], 64, (200, 190, 180), seed=int(f / 3), y=1350, jitter=3),
                           min(1.0, (td - 1.2) / 0.4))
            if td > 2.0:
                al = min(1.0, (td - 2.0) / 0.15)
                a = C.over(a, C.text_layer(["COMING SOON"], 96, (240, 230, 215), seed=int(f / 2), fnt=C.F_TYPE, y=1560, jitter=2), al)
                a = C.over(a, C.text_layer(["on Meta Quest Store"], 54, (190, 180, 175), seed=int(f / 2), fnt=C.F_TYPE, y=1660, jitter=1), al)
                if td < 2.15:
                    a = C.glitch(a, 0.5, f)
        for t0, t1, line, col, size in CAPTIONS:
            if t0 <= t < t1:
                a = C.over(a, C.text_layer([line], size, col, seed=int(f / 3), y=1560, stroke=6), min(1.0, (t - t0) / 0.08))
        a = C.vignette(a, 0.75)
        a = C.grain(a, 0.035, seed=f)
        C.to_img(a).save(os.path.join(FR, "f_%04d.jpg" % f), quality=92)


def load(rel):
    sr, x = wavfile.read(os.path.join(C.ROOT, "Assets/Horror/Audio", rel))
    x = x.astype(np.float64) / 32768
    return x if x.ndim == 1 else x.mean(1)


def audio():
    L = T_END + 0.5
    mono = lambda: np.zeros(int(C.SR * L))
    MU = C.MUSIC
    tr = []
    mu = mono()                                                    # musique : très faible, coupée aux bons moments
    dr = C.drone(PLANS[3][1])
    dr *= np.minimum(1, np.linspace(0, 3, len(dr))) * np.linspace(0.5, 1.0, len(dr))
    C.place(mu, dr, 0.0, MU * 1.2)
    mu[int(C.SR * PLANS[3][0]):int(C.SR * T_LOGO)] = 0              # « Don't. Breathe. » : plus de musique du tout
    pad = C.drone(T_END - T_LOGO + 0.5)
    pad *= np.minimum(1, np.linspace(0, 4, len(pad)))
    C.place(mu, pad, T_LOGO, MU * 0.8)
    tr.append((mu, 0.0))
    fx = mono()
    C.place(fx, C.ms.whisper("shhh"), 0.0, 0.35)
    C.place(fx, load("Monster/whisper_03.wav"), 0.9, 0.55)          # « Where are you, little one? »
    keys = load("Monster/keys_loop.wav")
    kl = np.tile(keys, 4)[:int(C.SR * (PLANS[3][0] - 0.5))]
    kl *= np.linspace(0.08, 0.5, len(kl))
    C.place(fx, kl, 0.5, 1.0)
    C.place(fx, C.ms.tap(1, hard=False), T_TAP, 0.9)               # *tap*
    C.place(fx, load("Monster/hear_react_01.wav"), PLANS[1][0], 0.9)   # elle a entendu : pile sur la coupe
    C.place(fx, C.boom(1.6, 70), PLANS[1][0], 0.45)
    for k, s in enumerate(STEPS):                                   # ses pas (l'image tremble en même temps)
        C.place(fx, load("Monster/step_%02d.wav" % (k % 4 + 1)), s, 0.8)
    C.place(fx, C.tick(), PLANS[2][0], 0.4)
    hb = load("Jumpscare/heartbeat_loop.wav")
    C.place(fx, hb[:int(C.SR * (PLANS[3][1] - PLANS[2][0]))] * np.linspace(0.35, 0.8, int(C.SR * (PLANS[3][1] - PLANS[2][0]))),
            PLANS[2][0], 1.0)
    C.place(fx, load("Player/breath_loop.wav")[:int(C.SR * 1.1)], PLANS[3][0], 0.35)
    C.place(fx, load("Monster/scream_chase.wav"), PLANS[4][0], 0.9)    # elle fonce
    C.place(fx, load("Jumpscare/jumpscare_sting.wav"), PLANS[4][0], 1.0)
    tp = [(f / 30, 0.25 + 0.3 * (f / 30 - PLANS[4][0])) for f in range(int(PLANS[4][0] * 30), int(PLANS[4][1] * 30))]
    C.place(fx, C.glitch_audio(tp)[int(C.SR * PLANS[4][0]):], PLANS[4][0], 1.0)
    fx[int(C.SR * PLANS[4][1]):int(C.SR * T_LOGO)] = 0              # noir : silence franc
    C.place(fx, C.boom(3.0, 45), PLANS[4][1] + 0.05, 0.8)
    for k in range(8):
        gr = C.ms.bandpass(C.ms.noise(0.09), 2500, 7000) * np.hanning(int(C.SR * 0.09))
        C.place(fx, gr, T_LOGO + 0.2 + k * 0.09, 0.25)
    C.place(fx, C.tick(), T_LOGO + 1.2, 0.3)
    C.place(fx, C.boom(2.5, 60), T_LOGO + 2.0, 0.6)
    C.place(fx, C.music_box([60, 63, 66], beat=0.02), T_LOGO + 2.0, 0.25)
    C.place(fx, C.ms.whisper("I hear everything"), T_LOGO + 2.5, 0.4)
    tr.append((fx, 0.0))
    for sig, _ in tr:
        sig[int(C.SR * PLANS[4][1]):int(C.SR * (PLANS[4][1] + 0.05))] = 0
    st = C.mixdown(tr, L)
    fade = np.ones(len(st))
    fade[-int(C.SR * 0.6):] = np.linspace(1, 0, int(C.SR * 0.6))
    st *= fade[:, None]
    path = os.path.join(WORK, "monster.wav")
    C.write_wav(path, st)
    return path


if __name__ == "__main__":
    stage = sys.argv[2] if len(sys.argv) > 2 else "all"
    if stage in ("all", "render"):
        C.render(WORK, "monster", shots())
    if stage in ("all", "compose"):
        compose()
    if stage in ("all", "audio", "compose"):
        C.encode(FR, audio(), os.path.join(OUT, "calm_faces_monster_tiktok.mp4"))
        print("OK ->", os.path.join(OUT, "calm_faces_monster_tiktok.mp4"))
