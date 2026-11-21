"""
TikTok « POV » en vrai gameplay (vertical 1080 x 1920, 30 i/s, ~18 s), filmé par Horror.Tests.PovScareDirector :
on avance dans le couloir -> on se retourne : elle est là-bas, immobile -> on repart -> la main claque le mur ->
SILENCE -> ses clés qui approchent, on n'ose pas se retourner -> on se retourne : elle fonce -> screamer ->
le noir chuchoté -> logo.

Le son est remonté ici avec les fichiers DU JEU, calés sur evenements.txt. Le screamer est « monté pour téléphone » :
les haut-parleurs de téléphone ne rendent pas le grave, donc on empile le cri du jeu, un cri aigu saturé et un
coup sec, et on écrase tout ce qui précède (le contraste fait le sursaut).

Usage : python Tools/Trailer/make_pov.py <dossier des images + evenements.txt> <sortie.mp4>
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
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_pov")
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR
rng = np.random.default_rng(7)

EV = []
for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
    f, what = line.rstrip("\n").split("\t", 1)
    EV.append((int(f), what))
FIRST = lambda key: next(f for f, w in EV if w.startswith(key))           # noqa: E731
LOOK, SEEN, AGAIN, SLAP, TURN, SCARE, WHISPER = (FIRST(k) for k in
                                                 ("regard", "la_voit", "repart", "claque", "retourne", "screamer", "murmure"))
START = 8
SCARE_LEN = 64      # la ruée, l'arrêt silencieux (0,4 s), puis le saut avec le cri
SNAP = 17           # image du saut (cri) après le début du screamer                             # images du screamer gardées
WHISPER_LEN = 56
END_FRAMES = 48


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


def caption(arr, lines, i, a, b, y=430, size=90):
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
            # le screamer : zoom d'impact, secousse, plus clair (le visage doit se lire), déchirure à l'impact
            img = C.zoom(img, 1.0 + (0.22 * np.exp(-j / 6.0) if j >= 0 else 0.0), dx=rng.uniform(-0.012, 0.012), dy=rng.uniform(-0.012, 0.012))
            arr = np.clip(C.to_arr(img), 0, 1) ** 1.8 * 0.95   # les prises ont un visage trop éclairé : on assombrit
            if 0 <= j < 4:
                arr = C.glitch(arr, 0.9 - j * 0.2, seed=j)
        else:
            arr = C.to_arr(Image.open(os.path.join(SRC, f"f{i:05d}.jpg")))
            arr = caption(arr, ["she can't", "see you."], i, START, LOOK)
            arr = caption(arr, ["but she hears", "everything."], i, SEEN + 12, AGAIN + 20)
            arr = caption(arr, ["don't", "turn around."], i, SLAP + 26, TURN)
        C.to_img(C.grain(arr, 0.03, seed=i)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    for j in range(WHISPER_LEN):                                    # le noir chuchoté (sous-titre lisible au téléphone)
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.text_layer(["you breathed", "too loud."], size=96, seed=j // 4, y=900, jitter=3,
                                       color=(205, 195, 215)), min(1.0, j / 6) * min(1.0, (WHISPER_LEN - j) / 6))
        C.to_img(C.grain(arr, 0.035, seed=900 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    for j in range(END_FRAMES):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 14)))
        arr = C.over(arr, C.text_layer(["would you turn around?"], size=66, seed=j // 3, y=1520, fnt=C.F_TYPE, jitter=2),
                     min(1.0, j / 10))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    return k


def phone_scream():
    """Le screamer monté pour téléphone : le cri du jeu + un cri aigu saturé + un coup sec, écrasés ensemble."""
    game = load("Jumpscare/ecoute_cri.wav", 1.0)
    # cri strident synthétisé : dent de scie qui glisse 1300 -> 650 Hz, vibrato nerveux, souffle, saturé
    tt = np.arange(int(1.7 * SR)) / SR
    f0 = 650 + 650 * np.exp(-2.2 * tt) + 40 * np.sin(2 * np.pi * 9 * tt)
    ph = np.cumsum(f0) / SR
    shriek = 2 * (ph - np.floor(ph)) - 1 + 0.5 * (2 * (2 * ph - np.floor(2 * ph)) - 1)
    shriek = np.tanh(3 * (shriek + 0.4 * rng.standard_normal(len(tt))))
    shriek = sosfilt(butter(2, [900, 6000], "band", fs=SR, output="sos"), shriek)
    shriek /= np.max(np.abs(shriek)) + 1e-9
    hit = rng.standard_normal(int(0.25 * SR)) * np.exp(-np.linspace(0, 14, int(0.25 * SR)))
    n = int(1.7 * SR)
    mix = np.zeros(n)
    for sig, g in ((game, 1.0), (shriek, 0.75), (hit, 0.9), (load("Jumpscare/jumpscare_sting.wav"), 0.5)):
        m = min(n, len(sig))
        mix[:m] += sig[:m] * g
    mix = np.tanh(2.6 * mix / (np.max(np.abs(mix)) + 1e-9))       # saturé : fort à l'oreille, pas seulement en crête
    mix *= np.minimum(1, np.linspace(0, 40, n)) * np.minimum(1, np.linspace(14, 0, n))
    return mix


def sound(total):
    T = total / FPS
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    slap_t, turn_t, scare_t = t(SLAP), t(TURN), t(SCARE)
    # la nappe et le souffle, coupés net à la claque (le silence qui suit est le moment le plus long)
    C.place(bed, loop(load("Ambience/drone_loop.wav", 0.22), slap_t), 0)
    C.place(bed, loop(load("Player/breath_loop.wav", 0.16), slap_t), 0)
    heart = loop(load("Jumpscare/heartbeat_loop.wav", 1.0), slap_t - t(SEEN))
    C.place(bed, heart * np.linspace(0.05, 0.3, len(heart)), t(SEEN))
    steps = [f"Steps/tissu_soft_0{n}.wav" for n in (1, 2, 3)]
    last = -99
    for f, w in EV:                                               # chaque main posée = un vrai bruit du jeu
        if w.startswith("bruit Pas") and START <= f < SLAP and f != last:
            C.place(fx, load(steps[rng.integers(0, 3)], 0.3), t(f))
            last = f
    C.place(fx, C.boom(2.2, 48) * 0.18, t(SEEN))                  # elle est là
    C.place(fx, load("Player/tap_hard_01.wav", 0.75), slap_t)
    # silence... puis elle : le cri qui l'annonce, ses clés et ses pas de plus en plus près
    hush = slap_t + 0.9
    C.place(fx, load("Monster/hear_react_01.wav", 0.35), hush)
    keys = loop(load("Monster/keys_loop.wav", 1.0), scare_t - hush)
    keys *= np.linspace(0.05, 0.6, len(keys)) ** 1.6
    C.place(fx, keys, hush)
    for k, s in enumerate(np.arange(hush + 0.2, scare_t, 0.2)):
        C.place(fx, load(f"Monster/step_0{k % 4 + 1}.wav", 0.12 + 0.4 * (s - hush) / max(0.1, scare_t - hush)), s)
    C.place(fx, load("Monster/scream_chase.wav", 0.4), turn_t)
    # avant le screamer, tout est remonté (~ -22 LUFS : on entend ses pas, ses clés) ; le screamer reste ~15 dB au-dessus
    bed *= 3.2; fx *= 3.2
    C.place(fx, phone_scream() * 3.0, scare_t + SNAP / FPS)                     # le screamer domine tout
    w_t = scare_t + SCARE_LEN / FPS
    C.place(fx, load("Death/murmure_01.wav", 0.45), w_t + 0.1)
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
