"""
Trois TikToks « lore » du Grenier et de La Veilleuse (vertical 1080 x 1920, 30 i/s), en VRAI gameplay filmé par
Horror.Tests.FrameRecorder (script de tournage : plans scriptés, evenements.txt = image <TAB> moment).
Mise en scène : une voix off « TikTok » (synthèse vocale Windows, Tools/Audio/tts_windows.ps1) raconte, les
cassettes du jeu donnent la parole aux gens de l'Institut, sous-titres incrustés, fin sur le logo + wishlist.

  A « la cassette »   : une cassette au fond du dortoir, PLAY, le journal de la Veilleuse, le grenier, l'ampoule
                        soufflée, le screamer.
  B « la règle »      : elle ne peut pas entrer dans la lumière... mais elle peut la souffler. Tire le cordon.
  C « les diapos »    : le projecteur du grenier, la dictée du Docteur, la dernière diapo n'était pas dans la boîte.

Usage : python Tools/Trailer/make_veilleuse.py A|B|C <dossier des images> <dossier des voix> <sortie.mp4>
Voix attendues (wav) : a1 a2 a3 / b1 b2 b3 b4 / c1 c2 (textes dans VOIX ci-dessous).
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image
from scipy.io import wavfile
from scipy.signal import resample_poly

import common as C

WHICH, SRC, VOIXDIR, OUT_MP4 = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
WORK = os.path.join(os.path.dirname(os.path.abspath(OUT_MP4)), "_montage_veilleuse_" + WHICH)
FR = os.path.join(WORK, "frames")
AUDIO = os.path.join(C.ROOT, "Assets", "Horror", "Audio")
FPS, SR = C.FPS, C.SR
rng = np.random.default_rng(23)

VOIX = {   # sous-titres de la voix off (mêmes textes que les fichiers de voix)
    "a1": ["okay so. i found a tape recorder", "at the back of the dormitory."],
    "a2": ["yeah. obviously, i pressed play."],
    "a3": ["then i went up to the attic."],
    "b1": ["okay. there's one rule", "in the attic."],
    "b2": ["she can't step", "into the light."],
    "b3": ["yeah. but she can", "blow it out."],
    "b4": ["so when it buzzes...", "pull the string."],
    "c1": ["so, these slides were in", "the attic of the orphanage."],
    "c2": ["yeah. the last one", "was not in the box."],
}
TAPES = {  # (fichier, lignes = sous-titres du jeu, InstitutBuilder.Grenier.cs)
    1: ("Lore/cassette_1_veilleuse.wav", ["Night log. November the second.", "Lights out at nine, as Madam wants.",
                                          "Number twelve cried again.", "I told her: the dark is kind.", "The dark keeps you quiet.",
                                          "If you see the light, you are awake.", "And awake children... get tested."]),
    2: ("Lore/cassette_2_patiente.wav", ["If somebody hears this.", "She is coming up the attic stairs.", "I can hear the brass bell.",
                                         "She puts the bulbs out, one by one.", "Don't let them all go out.", "Pull the strings. Pull the strings!"]),
    3: ("Lore/cassette_3_docteur.wav", ["Dictation.", "Subject twenty three shows", "no fear response in the dark.", "Recommend transfer",
                                        "to the attic programme.", "The night monitor will watch her.", "She always keeps them... calm."]),
}
LEAD = 0.55

EV = {}
for line in open(os.path.join(SRC, "evenements.txt"), encoding="utf-8"):
    f, what = line.rstrip("\n").split("\t", 1)
    EV[what] = int(f)
FIN = EV["fin"]


# ---------------------------------------------------------------- sons
def load(path, gain=1.0):
    sr, a = wavfile.read(path if os.path.isabs(path) else os.path.join(AUDIO, path))
    a = a.astype(np.float32)
    if a.ndim > 1:
        a = a.mean(axis=1)
    a /= (np.max(np.abs(a)) + 1e-9)
    if sr != SR:
        g = np.gcd(sr, SR)
        a = resample_poly(a, SR // g, sr // g)
    return a * gain


def voix(key, gain=1.0):
    return load(os.path.join(VOIXDIR, key + ".wav"), gain)


def loop(clip, dur):
    n = max(1, int(dur * SR))
    return np.tile(clip, n // len(clip) + 1)[:n]


def tape_seg(n, first, last):
    """Morceau de la cassette n, de la ligne first à la ligne last (comprises), temps comme LoreTape.cs."""
    path, lines = TAPES[n]
    a = load(path)
    dur = len(a) / SR
    speech = dur - 2 * LEAD
    total = sum(len(l) for l in lines)
    acc = [0]
    for l in lines:
        acc.append(acc[-1] + len(l))
    t0 = LEAD + speech * acc[first] / total - (0.15 if first > 0 else LEAD)
    t1 = LEAD + speech * acc[last + 1] / total + 0.35
    seg = a[int(max(0, t0) * SR):int(min(dur, t1) * SR)].copy()
    f = int(0.04 * SR)
    seg[:f] *= np.linspace(0, 1, f); seg[-f:] *= np.linspace(1, 0, f)
    # minutage des lignes dans le morceau (pour les sous-titres)
    times = [(lines[i], LEAD + speech * acc[i] / total - max(0, t0), LEAD + speech * acc[i + 1] / total - max(0, t0)) for i in range(first, last + 1)]
    return seg, times


def phone_scream():
    game = load("Jumpscare/veilleuse_cri.wav", 1.0)
    hit = rng.standard_normal(int(0.25 * SR)) * np.exp(-np.linspace(0, 14, int(0.25 * SR)))
    n = int(1.8 * SR)
    mix = np.zeros(n)
    for sig, g in ((game, 1.0), (hit, 0.9), (load("Grenier/veilleuse_repere.wav"), 0.5)):
        m = min(n, len(sig))
        mix[:m] += sig[:m] * g
    mix = np.tanh(2.4 * mix / (np.max(np.abs(mix)) + 1e-9))
    mix *= np.minimum(1, np.linspace(0, 40, n)) * np.minimum(1, np.linspace(14, 0, n))
    return mix


# ---------------------------------------------------------------- images
SUBS = []      # (image de début, image de fin, lignes, police) : sous-titres
CAPS = []      # (image de début, image de fin, lignes) : gros titres manuscrits en haut


def sub(at_s, lines, dur_s, fnt=None):
    SUBS.append((int(at_s * FPS), int((at_s + dur_s) * FPS), lines, fnt or C.F_KID))


def draw_text(arr, i):
    for a, b, lines in CAPS:
        if a <= i < b:
            al = min(1.0, (i - a) / 5) * min(1.0, (b - i) / 6)
            arr = C.over(arr, C.text_layer(lines, size=92, seed=i // 4, y=360, jitter=3, stroke=6), al)
    for a, b, lines, fnt in SUBS:
        if a <= i < b:
            al = min(1.0, (i - a) / 3) * min(1.0, (b - i) / 4)
            typed = fnt == C.F_TYPE
            arr = C.over(arr, C.text_layer(lines, size=60 if typed else 70, seed=i // 5, y=1480, fnt=fnt, jitter=1 if typed else 2,
                                           stroke=5, color=(250, 236, 200) if typed else (245, 245, 245)), al)
    return arr


def end_cards(k, whisper_lines, n_black=46, n_logo=56):
    for j in range(n_black):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.text_layer(whisper_lines, size=90, seed=j // 4, y=900, jitter=3, color=(205, 195, 215)),
                     min(1.0, j / 6) * min(1.0, (n_black - j) / 6))
        C.to_img(C.grain(arr, 0.035, seed=900 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    for j in range(n_logo):
        arr = np.zeros((C.H, C.W, 3), np.float32)
        arr = C.over(arr, C.logo(seed=j // 3, reveal=min(1.0, j / 14)))
        arr = C.over(arr, C.text_layer(["wishlist on Meta Quest"], size=66, seed=j // 3, y=1520, fnt=C.F_TYPE, jitter=2), min(1.0, j / 10))
        C.to_img(C.grain(arr, 0.04, seed=500 + j)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93); k += 1
    return k


def frames(first, last, scream_at=None, look=None):
    shutil.rmtree(FR, ignore_errors=True)
    os.makedirs(FR)
    k = 0
    for i in range(first, last):
        img = Image.open(os.path.join(SRC, f"f{i:05d}.jpg"))
        if look is not None:
            img = look(img)
        if scream_at is not None and i >= scream_at:
            j = i - scream_at
            img = C.zoom(img, 1.0 + 0.18 * np.exp(-j / 6.0), dx=rng.uniform(-0.012, 0.012), dy=rng.uniform(-0.012, 0.012))
            arr = np.clip(C.to_arr(img), 0, 1) ** 1.6
            if j < 4:
                arr = C.glitch(arr, 0.9 - j * 0.2, seed=j)
        else:
            arr = draw_text(C.to_arr(img), i - first)
        C.to_img(C.grain(arr, 0.03, seed=i)).save(os.path.join(FR, f"f_{k:04d}.jpg"), quality=93)
        k += 1
    return k


def lunge_frame(a, b):
    """Image où le visage du screamer fonce sur toi : le plus gros saut de luminosité après le regard fixe."""
    lum = [np.asarray(Image.open(os.path.join(SRC, f"f{i:05d}.jpg")).convert("L").resize((54, 96)), np.float32).mean() for i in range(a, b)]
    d = np.diff(lum)
    return a + 1 + int(np.argmax(np.abs(d[8:]))) + 8 if len(d) > 9 else a + 15


# ---------------------------------------------------------------- les trois vidéos
def video_a():
    play, grenier, souffle, scream = EV["play"], EV["grenier"], EV["souffle"], EV["screamer"]
    lunge = lunge_frame(scream, FIN)
    t = lambda f: f / FPS                                                    # noqa: E731
    CAPS.append((0, 80, ["never press play."]))
    sub(0.3, VOIX["a1"], 3.3)
    sub(t(play) - 2.0, VOIX["a2"], 2.0)
    seg1, times1 = tape_seg(1, 0, 3)
    seg1 = seg1[:int((t(grenier) - t(play)) * SR)]
    for line, s0, s1 in times1:
        if t(play) + s0 < t(grenier):
            sub(t(play) + s0, ['"' + line + '"'], min(s1, t(grenier) - t(play)) - s0, C.F_TYPE)
    sub(t(grenier) + 0.3, VOIX["a3"], 1.9)
    seg2, times2 = tape_seg(1, 6, 6)
    tape2_at = t(grenier) + 2.5
    for line, s0, s1 in times2:
        sub(tape2_at + s0, ['"' + line + '"'], s1 - s0 + 0.3, C.F_TYPE)
    n = frames(0, FIN, scream_at=lunge)
    n = end_cards(n, ["she keeps them", "calm."])
    T = n / FPS
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    C.place(bed, C.drone(t(grenier)) * 0.35, 0)
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), t(scream) - t(grenier)) * 0.8, t(grenier))
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), t(souffle) + 1.5 - t(grenier)) * 0.35, t(grenier))
    C.place(fx, voix("a1"), 0.3); C.place(fx, voix("a2"), t(play) - 2.0)
    C.place(fx, load("UI/button_click.wav", 0.7), t(play))
    C.place(fx, seg1 * 0.8, t(play) + 0.05)
    C.place(fx, voix("a3"), t(grenier) + 0.3)
    C.place(fx, seg2 * 0.85, tape2_at)
    C.place(fx, load("Grenier/ampoule_gresille.wav", 0.8), t(souffle))
    C.place(fx, load("Grenier/ampoule_souffle.wav", 0.9), t(souffle) + 1.5)
    C.place(fx, load("Grenier/veilleuse_repere.wav", 0.9), t(scream) + 0.1)
    bed *= 2.2; fx *= 2.2
    C.place(fx, phone_scream() * 3.0, t(lunge))
    end_t = FIN / FPS
    C.place(fx, load("Death/murmure_04.wav", 0.45), end_t + 0.1)
    C.place(fx, C.boom(1.8, 40) * 0.5, end_t + 46 / FPS)
    return n, C.mixdown([(bed, 0.0), (fx, 0.0)], T)


def video_b():
    elle, gres, appr, ral = EV["elle"], EV["gresille"], EV["approche"], EV["rallume"]
    t = lambda f: f / FPS                                                    # noqa: E731
    CAPS.extend([(0, elle, ["rule #1", "of the attic"]), (elle, gres, ["she can't enter", "the light."]),
                 (gres, appr, ["but she can", "put it out."]), (ral, FIN, ["PULL THE STRINGS."])])
    sub(0.3, VOIX["b1"], 2.3); sub(t(elle) + 0.2, VOIX["b2"], 1.8); sub(t(gres) + 0.1, VOIX["b3"], 2.4)
    sub(t(appr), VOIX["b4"], 2.8)
    seg, times = tape_seg(2, 5, 5)
    tape_at = t(ral) + 0.5
    for line, s0, s1 in times:
        sub(tape_at + s0, ['"' + line + '"'], s1 - s0 + 0.3, C.F_TYPE)
    n = frames(0, FIN)
    n = end_cards(n, ["don't let them", "all go out."])
    T = n / FPS
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), FIN / FPS) * 0.8, 0)
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), t(gres) + 1.5) * 0.4, 0)
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), FIN / FPS - t(ral)) * 0.4, t(ral))
    C.place(fx, voix("b1"), 0.3); C.place(fx, voix("b2"), t(elle) + 0.2); C.place(fx, voix("b3"), t(gres) + 0.1)
    C.place(fx, voix("b4"), t(appr))
    C.place(fx, load("Grenier/ampoule_gresille.wav", 0.8), t(gres))
    C.place(fx, load("Grenier/ampoule_souffle.wav", 0.9), t(gres) + 1.5)
    C.place(fx, loop(load("Grenier/veilleuse_clochette.wav"), t(ral) - t(appr) + 0.3) * 0.7, t(appr) - 0.3)
    C.place(fx, load("Grenier/plancher.wav", 0.6), t(appr) - 0.2)
    C.place(fx, load("Grenier/veilleuse_repere.wav", 0.7), t(appr) + 0.6)
    C.place(fx, load("Grenier/ampoule_rallume.wav", 1.0), t(ral))
    C.place(fx, seg * 0.9, tape_at)
    bed *= 2.2; fx *= 2.2
    end_t = FIN / FPS
    C.place(fx, load("Death/murmure_02.wav", 0.45), end_t + 0.1)
    C.place(fx, C.boom(1.8, 40) * 0.5, end_t + 46 / FPS)
    return n, C.mixdown([(bed, 0.0), (fx, 0.0)], T)


def video_c():
    d = [EV["diapo%d" % k] for k in range(1, 6)]
    t = lambda f: f / FPS                                                    # noqa: E731
    look = lambda img: Image.fromarray(np.clip((np.asarray(C.zoom(img, 1.3, cy=0.47), np.float32) / 255) ** 0.75 * 255, 0, 255).astype(np.uint8))  # noqa: E731
    CAPS.append((0, d[0], ["found a slide projector", "in the attic."]))
    sub(0.3, VOIX["c1"], 2.6)
    slides = ["MADAM NIGHT, OUR NIGHT MONITOR.", "DORMITORY, 1974.", "VISITING DAY.", "THE ONES WHO WOKE UP..."]
    for k in range(4):
        CAPS.append((d[k], d[k + 1], [slides[k].lower()]))
    seg, times = tape_seg(3, 0, 4)
    tape_at = t(d[0]) + 0.3
    for line, s0, s1 in times:
        sub(tape_at + s0, ['"' + line + '"'], s1 - s0 + 0.2, C.F_TYPE)
    sub(t(d[4]) - 0.2, VOIX["c2"], 2.4)
    glitch_n = 12
    n = frames(0, FIN, look=look)
    last = look(Image.open(os.path.join(SRC, f"f{FIN - 1:05d}.jpg")))
    for j in range(glitch_n):                                               # la dernière diapo se déchire
        img = C.zoom(last, 1.25 + 0.06 * j, dx=rng.uniform(-0.02, 0.02), dy=rng.uniform(-0.02, 0.02))
        arr = C.glitch(np.clip(C.to_arr(img), 0, 1) ** 1.4, 0.9 - j * 0.05, seed=40 + j)
        C.to_img(C.grain(arr, 0.04, seed=700 + j)).save(os.path.join(FR, f"f_{n:04d}.jpg"), quality=93); n += 1
    n = end_cards(n, ['"she always keeps them...', 'calm."'])
    T = n / FPS
    bed = np.zeros(int(SR * T)); fx = np.zeros(int(SR * T))
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), FIN / FPS) * 0.7, 0)
    C.place(fx, voix("c1"), 0.3)
    for k in range(5):
        C.place(fx, load("UI/button_click.wav", 0.9), t(d[k]))
    C.place(fx, seg * 0.75, tape_at)
    C.place(fx, voix("c2"), t(d[4]) - 0.2)
    C.place(fx, load("Grenier/veilleuse_repere.wav", 0.6), t(d[4]) + 0.2)
    bed *= 2.2; fx *= 2.2
    C.place(fx, phone_scream() * 3.0, FIN / FPS)
    seg_end, _ = tape_seg(3, 6, 6)
    C.place(fx, seg_end * 1.3, (FIN + glitch_n) / FPS + 0.2)
    C.place(fx, C.boom(1.8, 40) * 0.5, (FIN + glitch_n + 46) / FPS)
    return n, C.mixdown([(bed, 0.0), (fx, 0.0)], T)


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n, st = {"A": video_a, "B": video_b, "C": video_c}[WHICH]()
    wav = os.path.join(WORK, "son.wav")
    C.write_wav(wav, st)
    C.encode(FR, wav, OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
