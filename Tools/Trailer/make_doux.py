"""
Animation TikTok n°2 (vertical, ~13 s) : « RED LIGHT... GREEN LIGHT » avec les Doux du théâtre.
On regarde à travers le rideau entrouvert. Noir (« green light... ») : on entend le costume avancer.
Image (« red light ») : le Doux est plus près, figé, et les autres têtes nous regardent. Au dernier
« red light », il est collé à nous -> jumpscare -> logo + COMING SOON on Meta Quest Store.

Usage : python3 Tools/Trailer/make_doux.py <dossier de travail> [render|compose|audio|all]
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile

import common as C

WORK = sys.argv[1]
FR = os.path.join(WORK, "frames_doux")
OUT = os.path.join(WORK, "out")
os.makedirs(FR, exist_ok=True)
os.makedirs(OUT, exist_ok=True)
WS = 1.4   # échelle du monde de la maquette (WORLD_SCALE)


def M(x, y, z):
    """Coordonnées maquette -> monde du fichier .glb (agrandi)."""
    return [x * WS, y * WS, z * WS]


# plans : (début, fin, nom) ; les noirs sont entre les plans
PLANS = [(0.4, 2.4, "s1"), (3.0, 4.6, "s2"), (5.1, 6.5, "s3"), (6.9, 8.0, "s4"), (8.2, 9.2, "s5")]
T_LOGO, T_END = 9.6, 13.2


def shots():
    base = dict(lights="theatre", spot=M(22.25, 6.3, -17.6), spotTarget=M(22.25, 1.1, -18.8), foot=M(22.25, 1.5, -15.3),
                fov=42, fog=0.02)
    heads_away = {"TeteDoux_%d_" % k: {"yaw": a} for k, a in enumerate((70, -55, 120, -100, 45, -80))}
    heads_look = {"TeteDoux_%d_" % k: {"yaw": a} for k, a in enumerate((-12, -6, -3, 3, 6, 12))}
    W_ = "DouxMarcheur_"
    out = []
    # s1 : il est derrière le rideau, les autres regardent ailleurs
    out.append(dict(base, out="frames_doux/s1.png", cam=M(22.25, 1.25, -11.6), target=M(22.25, 2.2, -18.4),
                    parts=dict(heads_away, **{W_: {"d": [0, 0, 0]}})))
    # s2 : il a avancé jusqu'au rideau ; toutes les têtes nous regardent
    out.append(dict(base, out="frames_doux/s2.png", cam=M(22.25, 1.25, -11.9), target=M(22.25, 2.2, -17.4),
                    parts=dict(heads_look, **{W_: {"d": [0, 0, 1.25 * WS], "yaw": 8}})))
    # s3 : il passe la fente du rideau
    out.append(dict(base, out="frames_doux/s3.png", cam=M(22.25, 1.25, -12.2), target=M(22.25, 2.3, -16.4),
                    parts=dict(heads_look, **{W_: {"d": [0.05 * WS, 0, 2.3 * WS], "yaw": -10}})))
    # s4 : il est descendu de la scène, au milieu de l'allée
    out.append(dict(base, out="frames_doux/s4.png", cam=M(22.25, 1.2, -11.6), target=M(22.25, 1.6, -13.8),
                    extra=[dict(c=0xffb070, i=4, d=8, p=M(22.25, 2.4, -12.5))],
                    parts=dict(heads_look, **{W_: {"d": [0, -1.1 * WS, 4.55 * WS], "yaw": 14}})))
    # s5 : collé à nous
    out.append(dict(base, out="frames_doux/s5.png", cam=M(22.3, 1.55, -11.2), target=M(22.25, 1.72, -12.4),
                    fov=46, extra=[dict(c=0xffd8b0, i=2.2, d=3, p=M(22.35, 0.9, -11.7))],
                    parts=dict(heads_look, **{W_: {"d": [0, -1.1 * WS, 6.0 * WS], "yaw": -5}})))
    return out


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def compose():
    imgs = {name: Image.open(os.path.join(FR, name + ".png")) for _, _, name in PLANS}
    black = np.zeros((C.H, C.W, 3), np.float32)
    n = int(T_END * C.FPS)
    r = np.random.default_rng(3)
    for f in range(n):
        t = f / C.FPS
        a = black.copy()
        for t0, t1, name in PLANS:
            if t0 <= t < t1:
                k = (t - t0) / (t1 - t0)
                if name == "s5":     # jumpscare : il fonce, ça tremble, ça se déchire
                    img = C.zoom(imgs[name], 1.0 + 0.25 * k, dx=r.uniform(-0.012, 0.012), dy=r.uniform(-0.012, 0.012))
                    a = C.glitch(C.to_arr(img) * (1.6 if f % 4 < 2 else 1.0), 0.35 + 0.5 * k, f)
                else:                # lente poussée, image figée (« red light »)
                    img = C.zoom(imgs[name], 1.02 + 0.06 * k, dx=0.0015 * np.sin(t * 7), dy=0.001 * np.cos(t * 5))
                    a = C.to_arr(img) * smooth((t - t0) / 0.15) * (0.92 + 0.08 * (r.random() > 0.08))
                    if name == "s1":
                        a = a * smooth((t - t0) / 0.6)
        if t >= T_LOGO:
            td = t - T_LOGO
            lg = C.logo(seed=int(f / 3), reveal=smooth((td - 0.2) / 0.7) if td < 0.95 else 1.0)
            a = C.over(black.copy(), lg, min(1.0, td / 0.3))
            if td > 1.2:
                a = C.over(a, C.text_layer(["Don't look away."], 64, (200, 190, 180), seed=int(f / 3), y=1350, jitter=3),
                           min(1.0, (td - 1.2) / 0.4))
            if td > 2.0:
                al = min(1.0, (td - 2.0) / 0.15)
                a = C.over(a, C.text_layer(["COMING SOON"], 96, (240, 230, 215), seed=int(f / 2), fnt=C.F_TYPE, y=1560, jitter=2), al)
                a = C.over(a, C.text_layer(["on Meta Quest Store"], 54, (190, 180, 175), seed=int(f / 2), fnt=C.F_TYPE, y=1660, jitter=1), al)
                if td < 2.15:
                    a = C.glitch(a, 0.5, f)
        # sous-titres manuscrits des comptines (un enfant parle)
        for t0, t1, line in ((0.6, 2.2, "red light..."), (2.45, 2.95, "green light..."), (3.1, 4.4, "red light."),
                             (4.65, 5.05, "green light"), (5.2, 6.3, "red light"), (6.55, 6.85, "gree-")):
            if t0 <= t < t1:
                col = (225, 60, 70) if "red" in line else (150, 210, 160)
                a = C.over(a, C.text_layer([line], 92, col, seed=int(f / 3), y=1560, stroke=6), min(1.0, (t - t0) / 0.1))
        a = C.vignette(a, 0.75)
        a = C.grain(a, 0.035, seed=f)
        C.to_img(a).save(os.path.join(FR, "f_%04d.jpg" % f), quality=92)


def load(rel):
    sr, x = wavfile.read(os.path.join(C.ROOT, "Assets/Horror/Audio", rel))
    x = x.astype(np.float64) / 32768
    return x if x.ndim == 1 else x.mean(1)


def kid(text):
    """Voix d'enfant qui chuchote la comptine (synthèse, pas un enregistrement)."""
    v = C.ms.espeak(text, voice="en-us+whisperf", speed=105, pitch=92)
    v = C.ms.resample(v, 1.15)
    return C.ms.reverb(C.ms.bandpass(v, 300, 7000), size=2.6, mix=0.45)


def audio():
    L = T_END + 0.5
    mono = lambda: np.zeros(int(C.SR * L))
    tr = []
    # comptine à la boîte à musique, de plus en plus désaccordée ; elle s'arrête avant la fin
    mb = mono()
    C.place(mb, C.music_box([76, 74, 72, 74, 76, 76, 76], beat=0.42, detune=0.3, wow=0.004), 0.3, 0.55)
    C.place(mb, C.music_box([74, 74, 74, 76, 79, 79], beat=0.42, detune=1.2, wow=0.01, slow=1.12), 3.1, 0.5)
    mb[int(C.SR * 6.9):] = 0
    tr.append((mb, 0.15))
    d = mono()
    dr = C.drone(T_LOGO)
    dr *= np.linspace(0.25, 1.0, len(dr))
    C.place(d, dr, 0.0, 0.45)
    hb = load("Jumpscare/heartbeat_loop.wav")
    C.place(d, hb[:int(C.SR * 3.2)], 5.0, 0.8)
    C.place(d, load("Player/breath_loop.wav")[:int(C.SR * 1.3)], 6.9, 0.5)
    d[int(C.SR * 8.0):int(C.SR * 8.2)] = 0
    tr.append((d, 0.0))
    v = mono()
    for t0, text in ((0.6, "red light"), (2.45, "green light"), (3.1, "red light"), (4.65, "green light"), (5.2, "red light")):
        C.place(v, kid(text), t0, 0.9)
    g = kid("green")
    C.place(v, g[:int(C.SR * 0.35)], 6.55, 0.9)                # coupé net
    tr.append((v, -0.1))
    sh = mono()
    shuffle = load("World/softie_shuffle.wav")
    for t0, t1 in ((2.4, 3.0), (4.6, 5.1), (6.5, 6.9)):          # pendant le noir : le costume avance
        seg = shuffle[:int(C.SR * (t1 - t0))]
        C.place(sh, seg * np.hanning(len(seg)) ** 0.3, t0, 1.0)
    C.place(sh, load("World/softie_settle.wav"), 4.62, 0.6)
    tr.append((sh, 0.3))
    j = mono()
    C.place(j, load("Jumpscare/jumpscare_sting.wav"), 8.2, 1.0)
    C.place(j, C.child_scream(1.0), 8.25, 0.6)
    C.place(j, C.static(1.0), 8.2, 0.4)
    C.place(j, C.boom(3.0, 45), 9.2, 0.9)
    pad = C.drone(T_END - T_LOGO + 0.5) * 0.35
    pad *= np.minimum(1, np.linspace(0, 4, len(pad)))
    C.place(j, pad, T_LOGO, 0.6)
    for k in range(8):
        gr = C.ms.bandpass(C.ms.noise(0.09), 2500, 7000) * np.hanning(int(C.SR * 0.09))
        C.place(j, gr, T_LOGO + 0.2 + k * 0.09, 0.35)
    C.place(j, C.boom(2.5, 60), T_LOGO + 2.0, 0.8)
    C.place(j, C.music_box([60, 63, 66], beat=0.02), T_LOGO + 2.0, 0.6)
    C.place(j, kid("don't look away"), T_LOGO + 1.2, 0.6)
    tr.append((j, 0.0))
    st = C.mixdown(tr, L)
    fade = np.ones(len(st))
    fade[-int(C.SR * 0.6):] = np.linspace(1, 0, int(C.SR * 0.6))
    st *= fade[:, None]
    path = os.path.join(WORK, "doux.wav")
    C.write_wav(path, st)
    return path


if __name__ == "__main__":
    stage = sys.argv[2] if len(sys.argv) > 2 else "all"
    if stage in ("all", "render"):
        C.render(WORK, "map", shots())
    if stage in ("all", "compose"):
        compose()
    if stage in ("all", "audio", "compose"):
        C.encode(FR, audio(), os.path.join(OUT, "calm_faces_red_light_tiktok.mp4"))
        print("OK ->", os.path.join(OUT, "calm_faces_red_light_tiktok.mp4"))
