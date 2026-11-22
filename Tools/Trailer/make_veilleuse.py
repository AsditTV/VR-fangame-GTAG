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
Voix attendues (wav) : a1 a2 a3 / b1 b2 b3 b4 / c1 (textes dans VOIX ci-dessous).
La voix off et les cassettes passent sur une seule « piste » : jamais deux phrases en même temps.
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
    "a1": ["found this tape recorder in the dorm."],
    "a2": ["yeah. i pressed play."],
    "a3": ["then i went up to the attic."],
    "b1": ["one rule in the attic."],
    "b2": ["she can't step into the light."],
    "b3": ["yeah. but she can blow it out."],
    "b4": ["so... pull the string."],
    "c1": ["these slides were in the attic."],
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
    t0 = max(0.0, LEAD + speech * acc[first] / total - (0.15 if first > 0 else LEAD))
    t1 = LEAD + speech * acc[last + 1] / total + 0.35
    seg = a[int(t0 * SR):int(min(dur, t1) * SR)].copy()
    f = int(0.04 * SR)
    seg[:f] *= np.linspace(0, 1, f); seg[-f:] *= np.linspace(1, 0, f)
    times = [(lines[i], LEAD + speech * acc[i] / total - t0, LEAD + speech * acc[i + 1] / total - t0) for i in range(first, last + 1)]
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


# ---------------------------------------------------------------- textes
SUBS = []      # (image de début, image de fin, lignes, police) : sous-titres (un seul à la fois)
CAPS = []      # (image de début, image de fin, lignes) : gros titres manuscrits en haut
_voice_end = 0.0


def sub(at_s, lines, dur_s, fnt=None):
    SUBS.append((int(at_s * FPS), int((at_s + dur_s) * FPS), lines, fnt or C.F_KID))


def say(track, key, at, gain=1.0):
    """Une phrase de la voix off, jamais par-dessus une autre. Renvoie le début réel."""
    global _voice_end
    clip = voix(key, gain)
    start = max(at, _voice_end + 0.12)
    C.place(track, clip, start)
    sub(start, VOIX[key], len(clip) / SR + 0.15)
    _voice_end = start + len(clip) / SR
    return start


def play_tape(track, n, first, last, at, gain=0.85, max_len=None):
    """Un morceau de cassette sur la même piste que la voix off, sous-titres tapés à la machine."""
    global _voice_end
    seg, times = tape_seg(n, first, last)
    if max_len is not None and len(seg) > int(max_len * SR):
        seg = seg[:int(max_len * SR)]
        f = int(0.25 * SR); seg[-f:] *= np.linspace(1, 0, f)
    start = max(at, _voice_end + 0.12)
    C.place(track, seg * gain, start)
    end = start + len(seg) / SR
    for line, s0, s1 in times:
        if start + s0 < end - 0.3:
            sub(start + s0, ['"' + line + '"'], min(s1 + 0.25, end - start) - s0, C.F_TYPE)
    _voice_end = end
    return start


def wrap(lines, fnt, size, maxw=960):
    f = C.font(fnt, size)
    out = []
    for line in lines:
        cur = ""
        for w in line.split(" "):
            test = (cur + " " + w).strip()
            if cur and f.getlength(test) > maxw:
                out.append(cur); cur = w
            else:
                cur = test
        out.append(cur)
    return out


def draw_text(arr, i):
    for a, b, lines in CAPS:
        if a <= i < b:
            al = min(1.0, (i - a) / 5) * min(1.0, (b - i) / 6)
            arr = C.over(arr, C.text_layer(wrap(lines, C.F_KID, 92), size=92, seed=i // 4, y=360, jitter=3, stroke=6), al)
    active = [x for x in SUBS if x[0] <= i < x[1]]
    if active:
        a, b, lines, fnt = max(active, key=lambda x: x[0])                 # le plus récent remplace les autres
        al = min(1.0, (i - a) / 3) * min(1.0, (b - i) / 4)
        typed = fnt == C.F_TYPE
        size = 56 if typed else 64
        arr = C.over(arr, C.text_layer(wrap(lines, fnt, size), size=size, seed=i // 5, y=1480, fnt=fnt, jitter=1 if typed else 2,
                                       stroke=5, color=(250, 236, 200) if typed else (245, 245, 245)), al)
    return arr


# ---------------------------------------------------------------- images
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


def tracks(n_frames):
    T = n_frames / FPS + 2.0
    return np.zeros(int(SR * T)), np.zeros(int(SR * T)), np.zeros(int(SR * T))


# ---------------------------------------------------------------- les trois vidéos
def video_a():
    play, grenier, souffle, scream = EV["play"], EV["grenier"], EV["souffle"], EV["screamer"]
    lunge = lunge_frame(scream, FIN)
    t = lambda f: f / FPS                                                    # noqa: E731
    bed, fx, vo = tracks(FIN + 102)
    CAPS.append((0, 80, ["never press play."]))
    say(vo, "a1", 0.25)
    say(vo, "a2", t(play) - 2.2)
    C.place(fx, load("UI/button_click.wav", 0.7), t(play))
    play_tape(vo, 1, 0, 3, t(play) + 0.05, max_len=t(grenier) - t(play) - 0.1)
    say(vo, "a3", t(grenier) + 0.25)
    play_tape(vo, 1, 6, 6, t(grenier) + 2.3)
    SUBS[:] = [(a_, min(b_, scream), l_, f_) for a_, b_, l_, f_ in SUBS]   # rien par-dessus le screamer
    n = frames(0, FIN, scream_at=lunge)
    n = end_cards(n, ["she keeps them", "calm."])
    C.place(bed, C.drone(t(grenier)) * 0.35, 0)
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), t(scream) - t(grenier)) * 0.8, t(grenier))
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), t(souffle) + 1.5 - t(grenier)) * 0.35, t(grenier))
    C.place(fx, load("Grenier/ampoule_gresille.wav", 0.8), t(souffle))
    C.place(fx, load("Grenier/ampoule_souffle.wav", 0.9), t(souffle) + 1.5)
    C.place(fx, load("Grenier/veilleuse_repere.wav", 0.9), t(scream) + 0.1)
    bed *= 2.2; fx *= 2.2; vo *= 2.2
    C.place(fx, phone_scream() * 3.0, t(lunge))
    end_t = FIN / FPS
    C.place(fx, load("Death/murmure_04.wav", 0.45), end_t + 0.1)
    C.place(fx, C.boom(1.8, 40) * 0.5, end_t + 46 / FPS)
    return n, C.mixdown([(bed, 0.0), (fx, 0.0), (vo, 0.0)], n / FPS)


def video_b():
    elle, gres, appr, ral = EV["elle"], EV["gresille"], EV["approche"], EV["rallume"]
    t = lambda f: f / FPS                                                    # noqa: E731
    bed, fx, vo = tracks(FIN + 102)
    CAPS.extend([(0, elle, ["rule #1 of the attic"]), (elle, gres, ["she can't enter", "the light."]),
                 (gres, appr, ["but she can", "put it out."]), (ral, FIN, ["PULL THE STRINGS."])])
    say(vo, "b1", 0.3); say(vo, "b2", t(elle) + 0.2); say(vo, "b3", t(gres) + 0.1); say(vo, "b4", t(appr) - 0.3)
    play_tape(vo, 2, 5, 5, t(ral) + 0.4, gain=0.95)
    n = frames(0, FIN)
    n = end_cards(n, ["don't let them", "all go out."])
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), FIN / FPS) * 0.8, 0)
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), t(gres) + 1.5) * 0.4, 0)
    C.place(bed, loop(load("Grenier/ampoule_bourdon.wav"), FIN / FPS - t(ral)) * 0.4, t(ral))
    C.place(fx, load("Grenier/ampoule_gresille.wav", 0.8), t(gres))
    C.place(fx, load("Grenier/ampoule_souffle.wav", 0.9), t(gres) + 1.5)
    C.place(fx, loop(load("Grenier/veilleuse_clochette.wav"), t(ral) - t(appr) + 0.3) * 0.7, t(appr) - 0.3)
    C.place(fx, load("Grenier/plancher.wav", 0.6), t(appr) - 0.2)
    C.place(fx, load("Grenier/veilleuse_repere.wav", 0.7), t(appr) + 0.6)
    C.place(fx, load("Grenier/ampoule_rallume.wav", 1.0), t(ral))
    bed *= 2.2; fx *= 2.2; vo *= 2.2
    end_t = FIN / FPS
    C.place(fx, load("Death/murmure_02.wav", 0.45), end_t + 0.1)
    C.place(fx, C.boom(1.8, 40) * 0.5, end_t + 46 / FPS)
    return n, C.mixdown([(bed, 0.0), (fx, 0.0), (vo, 0.0)], n / FPS)


def video_c():
    d = [EV["diapo%d" % k] for k in range(1, 6)]
    t = lambda f: f / FPS                                                    # noqa: E731

    def look(img):                                                          # recadré sur le drap, éclairci
        a = np.asarray(C.zoom(img, 1.3, cy=0.47), np.float32) / 255
        return Image.fromarray(np.clip(a ** 0.75 * 255, 0, 255).astype(np.uint8))

    glitch_n = 12
    bed, fx, vo = tracks(FIN + glitch_n + 102)
    CAPS.append((0, d[0], ["found a slide projector", "in the attic."]))
    slides = ["madam night, our night monitor.", "dormitory, 1974.", "visiting day.", "the ones who woke up..."]
    for k in range(4):
        CAPS.append((d[k], d[k + 1], [slides[k]]))
    CAPS.append((d[4], FIN, ["the last slide", "wasn't in the box."]))
    say(vo, "c1", 0.3)
    play_tape(vo, 3, 0, 4, t(d[0]) + 0.2, gain=0.8, max_len=t(FIN) - t(d[0]) - 0.6)
    n = frames(0, FIN, look=look)
    last = look(Image.open(os.path.join(SRC, f"f{FIN - 1:05d}.jpg")))
    for j in range(glitch_n):                                               # la dernière diapo se déchire
        img = C.zoom(last, 1.25 + 0.06 * j, dx=rng.uniform(-0.02, 0.02), dy=rng.uniform(-0.02, 0.02))
        arr = C.glitch(np.clip(C.to_arr(img), 0, 1) ** 1.4, 0.9 - j * 0.05, seed=40 + j)
        C.to_img(C.grain(arr, 0.04, seed=700 + j)).save(os.path.join(FR, f"f_{n:04d}.jpg"), quality=93); n += 1
    n = end_cards(n, ['"she always keeps them...', 'calm."'])
    C.place(bed, loop(load("Grenier/grenier_ambiance.wav"), FIN / FPS) * 0.7, 0)
    for k in range(5):
        C.place(fx, load("UI/button_click.wav", 0.9), t(d[k]))
    C.place(fx, load("Grenier/veilleuse_repere.wav", 0.6), t(d[4]) + 0.2)
    bed *= 2.2; fx *= 2.2; vo *= 2.2
    C.place(fx, phone_scream() * 3.0, FIN / FPS)
    seg_end, _ = tape_seg(3, 6, 6)
    C.place(fx, seg_end * 1.3, (FIN + glitch_n) / FPS + 0.2)
    C.place(fx, C.boom(1.8, 40) * 0.5, (FIN + glitch_n + 46) / FPS)
    return n, C.mixdown([(bed, 0.0), (fx, 0.0), (vo, 0.0)], n / FPS)


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    n, st = {"A": video_a, "B": video_b, "C": video_c}[WHICH]()
    wav = os.path.join(WORK, "son.wav")
    C.write_wav(wav, st)
    C.encode(FR, wav, OUT_MP4)
    print("VIDEO ->", OUT_MP4, n, "images,", round(n / FPS, 1), "s")
