"""
Outils communs aux vidéos TikTok (format vertical 1080 x 1920, 30 images/s) :
rendu 3D (three.js dans Chromium via Playwright), effets (grain, vignette, glitch), logo dessiné
à la main, sons, encodage ffmpeg.

Prérequis : Python (numpy, Pillow, scipy), Node + Playwright avec Chromium, three@0.160.0 dans un
dossier node_modules (variable THREE_DIR), ffmpeg, espeak-ng (murmures).
"""
import json
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Papers"))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Audio"))
from make_papers import HandDraw  # noqa: E402  (crayon tremblant)
import make_sounds as ms  # noqa: E402

W, H, FPS = 1080, 1920, 30
SR = ms.SR
FONTS = os.path.join(ROOT, "Tools", "Papers", "fonts")
F_TITLE = os.path.join(FONTS, "caveat-latin-700-normal.woff")
F_KID = os.path.join(FONTS, "gloria-hallelujah-latin-400-normal.woff")
F_TYPE = os.path.join(FONTS, "special-elite-latin-400-normal.woff")
rng = np.random.default_rng(1985)


def font(path, size):
    return ImageFont.truetype(path, size)


# ---------------------------------------------------------------- rendu 3D
def render(work, setup, shots, port=8769):
    """Rend les images 3D (une par entrée de shots, champ 'out' relatif à work)."""
    jobs = os.path.join(work, "jobs_%s.json" % setup)
    json.dump({"setup": setup, "shots": shots}, open(jobs, "w"))
    env = dict(os.environ, RENDER_PORT=str(port))
    subprocess.run(["node", os.path.join(HERE, "render.js"), work, jobs], check=True, env=env)


# ---------------------------------------------------------------- effets image
def to_arr(img):
    return np.asarray(img.convert("RGB")).astype(np.float32) / 255


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


_vig = None


def vignette(a, strength=0.75):
    global _vig
    if _vig is None:
        y, x = np.mgrid[0:H, 0:W]
        d = np.sqrt(((x - W / 2) / (W * 0.62)) ** 2 + ((y - H / 2) / (H * 0.62)) ** 2)
        _vig = np.clip(1 - d ** 2.2, 0, 1)[..., None]
    return a * (1 - strength + strength * _vig)


def grain(a, amount=0.06, seed=None):
    r = np.random.default_rng(seed)
    n = r.normal(0, amount, (H // 2, W // 2, 1)).astype(np.float32)
    n = np.repeat(np.repeat(n, 2, 0), 2, 1)
    return a + n * (0.4 + a)


def zoom(img, k, cx=0.5, cy=0.5, dx=0.0, dy=0.0):
    """Zoom (k >= 1) centré, avec petit décalage (secousses)."""
    w, h = img.size
    cw, ch = w / k, h / k
    x0 = cx * w - cw / 2 + dx * w
    y0 = cy * h - ch / 2 + dy * h
    return img.crop((int(x0), int(y0), int(x0 + cw), int(y0 + ch))).resize((W, H), Image.BICUBIC)


def glitch(a, power, seed):
    """Image qui se déchire : canaux décalés, bandes qui glissent, blocs, lignes de balayage."""
    r = np.random.default_rng(seed)
    out = a.copy()
    s = int(6 + 40 * power)
    out[..., 0] = np.roll(a[..., 0], s, axis=1)
    out[..., 2] = np.roll(a[..., 2], -s, axis=1)
    for _ in range(int(3 + 14 * power)):
        y = r.integers(0, H - 20)
        h = r.integers(6, int(30 + 160 * power))
        out[y:y + h] = np.roll(out[y:y + h], r.integers(-int(260 * power) - 1, int(260 * power) + 1), axis=1)
    for _ in range(int(6 * power)):
        x, y = r.integers(0, W - 120), r.integers(0, H - 120)
        out[y:y + r.integers(20, 120), x:x + r.integers(40, 300)] = r.random(3) * 0.8
    out[::3] *= 1 - 0.25 * power
    if r.random() < 0.2 * power:
        out = 1 - out
    return out


def text_layer(lines, size=72, color=(235, 220, 205), seed=0, fnt=F_KID, y=None, jitter=6, stroke=0):
    """Texte manuscrit qui tremble, sur fond transparent."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = np.random.default_rng(seed)
    f = font(fnt, size)
    y0 = y if y is not None else H / 2 - (len(lines) - 1) * size * 0.6
    for i, line in enumerate(lines):
        d.text((W / 2 + r.uniform(-jitter, jitter), y0 + i * size * 1.2 + r.uniform(-jitter, jitter)), line,
               fill=color + (255,), font=f, anchor="mm", stroke_width=stroke, stroke_fill=(8, 6, 10, 255))
    return img


def over(a, layer, alpha=1.0):
    la = np.asarray(layer).astype(np.float32) / 255
    m = la[..., 3:4] * alpha
    return a * (1 - m) + la[..., :3] * m


# ---------------------------------------------------------------- logo
def logo(seed=0, reveal=1.0, mask_eye_scribble=True):
    """Logo CALM FACES : un masque en papier dessiné à la main (un oeil gribouillé) + le titre.
    seed change le tremblé (animation « boil » comme le jeu), reveal (0..1) dévoile le titre de gauche à droite."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = HandDraw(img, wobble=1.6)
    r = np.random.default_rng(seed)
    cx, cy = W / 2 + r.uniform(-3, 3), 700 + r.uniform(-3, 3)
    cream, ink = (238, 228, 210, 255), (40, 32, 38, 255)
    d.ellipse([cx - 190, cy - 240, cx + 190, cy + 230], fill=cream, outline=ink, width=7)        # le masque
    d.line([(cx - 190, cy - 30), (cx - 330, cy - 60)], fill=(160, 140, 170, 255), width=6)        # l'élastique
    d.line([(cx + 190, cy - 30), (cx + 330, cy - 60)], fill=(160, 140, 170, 255), width=6)
    # oeil gauche : un losange calme ; oeil droit : gribouillé noir
    d.polygon([(cx - 95, cy - 110), (cx - 50, cy - 60), (cx - 95, cy - 10), (cx - 140, cy - 60)], outline=ink, width=6)
    if mask_eye_scribble:
        pts = [(cx + 90 + r.uniform(-48, 48), cy - 60 + r.uniform(-48, 48)) for _ in range(26)]
        d.line(pts, fill=ink, width=5)
    else:
        d.polygon([(cx + 95, cy - 110), (cx + 140, cy - 60), (cx + 95, cy - 10), (cx + 50, cy - 60)], outline=ink, width=6)
    pts = [(cx - 120 + k * 24, cy + 110 + 14 * math.sin(k * 1.3)) for k in range(11)]               # sourire en vague
    d.line(pts, fill=ink, width=7)
    for k in range(3):                                                                            # une larme d'encre
        d.ellipse([cx + 88 - k, cy + 10 + k * 26, cx + 102 + k, cy + 30 + k * 30], fill=(20, 16, 24, 255))
    # le titre
    t = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(t)
    f = font(F_TITLE, 168)
    x = W / 2 - td.textlength("CALM FACES", font=f) / 2
    for ch in "CALM FACES":
        dy = r.uniform(-8, 8)
        td.text((x, 1130 + dy), ch, fill=(238, 228, 210, 255), font=f, anchor="lm")
        x += td.textlength(ch, font=f) + r.uniform(-2, 4)
    tw = Image.new("L", (W, H), 0)                                                                # dévoilement à bord déchiré
    rd = ImageDraw.Draw(tw)
    edge = 60 + reveal * (W - 60)
    poly = [(0, 0), (edge, 0)] + [(edge + r.uniform(-25, 25), y) for y in range(0, H, 40)] + [(edge, H), (0, H)]
    rd.polygon(poly, fill=255)
    if reveal < 1:
        t.putalpha(Image.composite(t.getchannel("A"), Image.new("L", (W, H), 0), tw))
    img.alpha_composite(t)
    # trait rouge-brun sous FACES
    if reveal >= 1:
        HandDraw(img, wobble=2.0).line([(W / 2 + 10, 1240), (W / 2 + 400, 1225)], fill=(150, 50, 45, 255), width=9)
    return img


# ---------------------------------------------------------------- audio
def silence(sec):
    return np.zeros(int(SR * sec))


def place(track, clip, at, gain=1.0):
    s = int(at * SR)
    if s >= len(track):
        return
    n = min(len(clip), len(track) - s)
    track[s:s + n] += clip[:n] * gain


def boom(dur=2.5, f0=55):
    t = ms.t_axis(dur)
    x = np.sin(2 * np.pi * (f0 * np.exp(-t * 1.5) + 20) * t) * np.exp(-t * 1.6)
    x += ms.lowpass(ms.noise(dur), 200) * np.exp(-t * 4) * 1.5
    return ms.reverb(x, size=3.0, mix=0.4)


def riser(dur=3.0):
    t = ms.t_axis(dur)
    f = 200 + 2600 * (t / dur) ** 2
    x = ms.bandpass(ms.noise(dur), 300, 6000) * (t / dur) ** 2
    x += np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.15 * (t / dur) ** 3
    return x


def static(dur):
    return ms.highpass(ms.noise(dur), 1500) * (0.5 + 0.5 * (np.random.default_rng(3).random(int(SR * dur)) > 0.7))


def drone(dur):
    t = ms.t_axis(dur)
    x = sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * a for f, a in ((36.7, 1.0), (55.0, 0.5), (73.9, 0.3), (110.3, 0.12)))
    x += ms.bandpass(ms.noise(dur), 200, 700) * 0.35 * (0.6 + 0.4 * np.sin(2 * np.pi * t / 5))
    glass = sum(np.sin(2 * np.pi * f * t) * 0.05 for f in (1174.7, 1189.0, 1661.2))   # verre frotté, dissonant
    return x * 0.5 + glass * (0.5 + 0.5 * np.sin(2 * np.pi * t / 3.7))


def music_box(notes, beat=0.48, detune=0.0, slow=1.0, wow=0.0):
    """Boîte à musique (désaccordée si detune > 0, qui ralentit si slow > 1)."""
    total = sum(beat * slow ** i for i in range(len(notes))) + 2.5
    x = np.zeros(int(SR * total))
    at = 0.0
    for i, m in enumerate(notes):
        f = 440 * 2 ** ((m - 69 - detune * i / len(notes)) / 12)
        t = ms.t_axis(1.8)
        fm = f * (1 + wow * np.sin(2 * np.pi * 0.7 * t))
        ph = 2 * np.pi * np.cumsum(fm) / SR
        bell = sum(np.sin(ph * h) * np.exp(-t * (3 + 2.5 * h)) / h for h in (1, 2.01, 3.04, 4.3))
        place(x, bell, at)
        at += beat * slow ** i
    return ms.reverb(x, size=2.4, mix=0.4)


def child_scream(dur=1.8):
    """Le cri de la poupée : voix d'enfant aiguë, saturée, avec écho (pas un vrai enregistrement)."""
    v = ms.espeak("aaaaaaaaaah", voice="en-us+f4", speed=60, pitch=99)
    v = ms.resample(v, 1.25)
    v = np.tanh(v * 6) * 0.6
    t = np.arange(len(v)) / SR
    v *= 1 + 0.3 * np.sin(2 * np.pi * 7 * t)
    noise_ = ms.bandpass(np.random.default_rng(5).uniform(-1, 1, len(v)), 1500, 7000) * 0.25
    x = ms.bandpass(v + noise_, 250, 9000)
    return ms.reverb(x[:int(SR * dur)], size=2.2, mix=0.35)


def whisper_line(text, pan):
    w = ms.whisper(text)
    return w, pan


def mixdown(tracks, length):
    """tracks : liste de (signal mono, pan -1..1). Renvoie stéréo normalisé."""
    L = np.zeros(int(SR * length))
    R = np.zeros(int(SR * length))
    for sig, pan in tracks:
        n = min(len(sig), len(L))
        L[:n] += sig[:n] * math.sqrt((1 - pan) / 2)
        R[:n] += sig[:n] * math.sqrt((1 + pan) / 2)
    st = np.stack([L, R], 1)
    st = np.tanh(st / (np.max(np.abs(st)) + 1e-9) * 1.4) * 0.9   # léger écrêtage doux
    return st


def write_wav(path, st):
    from scipy.io import wavfile
    wavfile.write(path, SR, (st * 32767).astype(np.int16))


def encode(frames_dir, wav, out_mp4):
    # le grain coûte cher en poids : crf 23 + débit plafonné (vidéo d'environ 10-20 Mo, OK pour TikTok)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", os.path.join(frames_dir, "f_%04d.jpg"),
                    "-i", wav, "-c:v", "libx264", "-preset", "slow", "-crf", "23", "-maxrate", "9M", "-bufsize", "18M",
                    "-pix_fmt", "yuv420p",
                    "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", out_mp4], check=True)
