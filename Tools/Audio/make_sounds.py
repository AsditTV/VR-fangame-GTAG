"""
Fabrique le pack de sons du jeu (placeholders de bonne qualité, 100 % générés, sans droits) :
murmures du monstre en anglais, réaction quand elle entend, cri de chasse, pas, clés,
tapotements du joueur, ambiances en boucle, bruits ponctuels, jumpscare, coeur, berceuse
de la zone sûre, sons de la boutique.

Usage (depuis la racine du dépôt) :
    sudo apt-get install espeak-ng      # pour les murmures (voix chuchotée)
    pip install numpy scipy
    python3 Tools/Audio/make_sounds.py
Sortie : Assets/Horror/Audio/<catégorie>/*.wav (44,1 kHz, mono, 16 bits)
"""
import os
import subprocess
import tempfile

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets/Horror/Audio")
rng = np.random.default_rng(1985)


# ---------------------------------------------------------------- outils
def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def env(n, attack=0.005, release=0.2, curve=3.0):
    """Enveloppe attaque rapide / chute exponentielle."""
    e = np.ones(n)
    a = max(1, int(SR * attack))
    e[:a] = np.linspace(0, 1, a)
    r = min(n, int(SR * release))
    e[n - r:] *= np.linspace(1, 0, r) ** curve
    return e


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def bandpass(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, btype="low", fs=SR, output="sos"), x)


def highpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, btype="high", fs=SR, output="sos"), x)


def reverb(x, size=1.8, mix=0.35, damp=4000):
    """Réverbération par convolution avec une queue de bruit qui décroît (pièce vide et grande)."""
    n = int(SR * size)
    ir = rng.normal(size=n) * np.exp(-np.linspace(0, 7, n))
    ir = lowpass(ir, damp)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    wet = signal.fftconvolve(x, ir)
    wet = np.pad(wet, (0, max(0, len(x) + n - len(wet))))[:len(x) + n]
    dry = np.concatenate([x, np.zeros(n)])
    return dry * (1 - mix) + wet * mix


def resample(x, factor):
    """Change la hauteur ET la vitesse (factor < 1 = plus grave et plus lent)."""
    return signal.resample(x, int(len(x) / factor))


def normalize(x, peak=0.9):
    m = np.max(np.abs(x)) + 1e-9
    return x / m * peak


def loopify(x, fade=1.0):
    """Rend une boucle sans couture : la fin se fond dans le début."""
    f = int(SR * fade)
    head, tail = x[:f], x[-f:]
    ramp = np.linspace(0, 1, f)
    body = x[f:-f].copy()
    return np.concatenate([tail * (1 - ramp) + head * ramp, body])


def save(cat, name, x, peak=0.9):
    os.makedirs(os.path.join(OUT, cat), exist_ok=True)
    x = normalize(np.asarray(x, np.float64), peak)
    wavfile.write(os.path.join(OUT, cat, name + ".wav"), SR, (x * 32767).astype(np.int16))


def espeak(text, voice="en-us+whisperf", speed=110, pitch=35):
    """Voix chuchotée en anglais (espeak-ng)."""
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
        path = f.name
    subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", path, text],
                   check=True, capture_output=True)
    sr, data = wavfile.read(path)
    os.unlink(path)
    data = data.astype(np.float64) / 32768
    if sr != SR:
        data = signal.resample(data, int(len(data) * SR / sr))
    return data


# ---------------------------------------------------------------- le monstre
def whisper(text, slow=0.82):
    """Murmure de l'Écoute : voix chuchotée doublée (une plus grave), filtrée, avec écho de grande pièce."""
    a = espeak(text, speed=95)
    b = resample(espeak(text, speed=95, pitch=20), slow)
    n = max(len(a), len(b)) + int(SR * 0.08)
    mix = np.zeros(n)
    mix[:len(a)] += a * 0.8
    off = int(SR * 0.06)
    mix[off:off + len(b)] += b[:n - off] * 0.9
    mix = bandpass(mix, 180, 6500)
    # léger tremblement (souffle irrégulier)
    trem = 1 + 0.25 * np.sin(np.arange(n) / SR * 2 * np.pi * 5.5) * lowpass(rng.normal(size=n), 3) * 8
    mix *= np.clip(trem, 0.4, 1.6)
    return reverb(mix, size=2.4, mix=0.4)


def inhale_react():
    """Elle a entendu : inspiration brusque + petit "Hm ?" chuchoté."""
    d = 0.45
    n = noise(d)
    sweep = np.zeros_like(n)
    t = t_axis(d)
    f = 900 + 2600 * (t / d) ** 0.6
    for i in range(0, len(n), 256):
        seg = n[i:i + 256]
        fc = f[min(i, len(f) - 1)]
        sweep[i:i + 256] = bandpass(seg, fc * 0.6, min(fc * 1.6, 16000), order=1)
    sweep *= np.sin(np.pi * t / d) ** 0.7
    hm = espeak("hm?", speed=140, pitch=45)
    out = np.concatenate([sweep * 1.2, np.zeros(int(SR * 0.05)), hm * 0.9])
    return reverb(out, size=1.6, mix=0.3)


def scream():
    """Cri de chasse : hurlement aigu qui grince + souffle "SHHH" énorme."""
    d = 1.8
    t = t_axis(d)
    f0 = 520 + 380 * np.sin(np.pi * t / d) + 30 * np.sin(2 * np.pi * 7 * t)
    phase = 2 * np.pi * np.cumsum(f0) / SR
    tone = sum(np.sin(phase * k * (1 + 0.004 * k)) / k for k in range(1, 9))
    tone = np.tanh(tone * 3)                             # distorsion
    ring = np.sin(2 * np.pi * 73 * t)                     # modulation en anneau = son métallique, inhumain
    voice = tone * (0.6 + 0.4 * ring)
    hiss = highpass(noise(d), 2500) * 0.6
    e = env(len(t), attack=0.03, release=1.0, curve=2)
    x = (voice * 0.8 + hiss) * e
    return reverb(bandpass(x, 150, 12000), size=2.6, mix=0.35)


def monster_step(i):
    """Pas lourd : choc sourd + froissement de robe + petit bruit mouillé d'encre."""
    d = 0.5
    t = t_axis(d)
    thud = np.sin(2 * np.pi * (55 + 25 * np.exp(-t * 30)) * t) * np.exp(-t * 14)
    click = lowpass(noise(d), 1500) * np.exp(-t * 60) * 0.6
    rustle = bandpass(noise(d), 1500, 7000) * np.exp(-((t - 0.12) ** 2) / 0.004) * 0.35
    wet = bandpass(noise(d), 300, 1200) * np.exp(-((t - 0.05) ** 2) / 0.0008) * (0.25 + 0.15 * i)
    return reverb(thud + click + rustle + wet, size=1.2, mix=0.25)


def keys_loop():
    """Trousseau de clés qui tinte (boucle de 4 s)."""
    d = 4.0
    x = np.zeros(int(SR * d))
    for k in range(26):
        at = int(rng.uniform(0, d - 0.3) * SR)
        dur = 0.25
        t = t_axis(dur)
        partials = rng.uniform(2200, 7500, 4)
        hit = sum(np.sin(2 * np.pi * p * t) * np.exp(-t * rng.uniform(18, 40)) for p in partials)
        x[at:at + len(t)] += hit * rng.uniform(0.2, 0.7)
    return loopify(reverb(x, size=0.8, mix=0.2)[:int(SR * d)], 0.3)


# ---------------------------------------------------------------- le joueur
def tap(i, hard=False):
    """Main de poupée en tissu qui tape le sol : bruit mat et feutré."""
    d = 0.25
    t = t_axis(d)
    body = np.sin(2 * np.pi * (140 + 20 * i) * t) * np.exp(-t * (40 if not hard else 25))
    cloth = lowpass(noise(d), 2200 if not hard else 4000) * np.exp(-t * (55 if not hard else 30))
    x = body * (0.7 if not hard else 1.0) + cloth * (0.6 if not hard else 1.0)
    return reverb(x, size=0.9, mix=0.15)


# ---------------------------------------------------------------- ambiances
def drone_loop():
    """Grondement grave qui bat lentement + vent qui siffle (boucle de 24 s)."""
    d = 24
    t = t_axis(d)
    base = sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * a for f, a in ((41.2, 1), (41.7, 0.8), (61.8, 0.4), (82.6, 0.25)))
    wind = bandpass(noise(d), 300, 1400) * (0.3 + 0.25 * np.sin(2 * np.pi * t / 9) + 0.15 * np.sin(2 * np.pi * t / 5.3))
    whistle = bandpass(noise(d), 2400, 2600, order=3) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 11)) * 0.6
    return loopify(base * 0.5 + wind + whistle * 0.3, 2.0)


def basement_loop():
    """Sous-sol : bourdonnement électrique 50 Hz, chaudières, tuyaux (boucle de 20 s)."""
    d = 20
    t = t_axis(d)
    hum = sum(np.sin(2 * np.pi * 50 * k * t) / k ** 1.3 for k in range(1, 8))
    rumble = lowpass(noise(d), 120) * 3
    pipes = bandpass(noise(d), 600, 900) * (0.2 + 0.2 * np.sin(2 * np.pi * t / 7)) * 0.5
    return loopify(hum * 0.25 + rumble + pipes, 2.0)


def safe_lullaby_loop():
    """Zone sûre : berceuse de boîte à musique, majeure et chaude (le seul son rassurant du jeu)."""
    notes = [67, 72, 76, 74, 72, 67, 69, 72, 71, 67, 64, 67, 72, 76, 79, 76, 74, 72, 71, 72]
    beat = 0.42
    x = np.zeros(int(SR * beat * (len(notes) + 4)))
    for i, m in enumerate(notes):
        f = 440 * 2 ** ((m - 69) / 12)
        dur = 1.6
        t = t_axis(dur)
        bell = sum(np.sin(2 * np.pi * f * h * t) * np.exp(-t * (3 + 3 * h)) / h for h in (1, 2.01, 3.03, 4.2))
        at = int(i * beat * SR)
        x[at:at + len(t)] += bell[:len(x) - at]
    return reverb(x, size=2.0, mix=0.3)[:len(x)]


def music_box_far():
    """Boîte à musique lointaine, désaccordée, qui ralentit (bruit ponctuel qui fait froid dans le dos)."""
    notes = [76, 75, 76, 71, 74, 72, 69]
    x = np.zeros(int(SR * 7))
    at_time = 0.0
    for i, m in enumerate(notes):
        f = 440 * 2 ** ((m - 69 - 0.3 * i / len(notes)) / 12)   # se désaccorde
        t = t_axis(1.8)
        bell = sum(np.sin(2 * np.pi * f * h * t) * np.exp(-t * (4 + 2 * h)) / h for h in (1, 2.0, 3.1))
        at = int(at_time * SR)
        x[at:at + len(t)] += bell[:len(x) - at]
        at_time += 0.45 + i * 0.12                                # ralentit, comme si le ressort se vidait
    return reverb(lowpass(x, 3000), size=3.0, mix=0.55)


def drip(i):
    d = 0.6
    t = t_axis(d)
    f = 1500 + 300 * i
    plip = np.sin(2 * np.pi * (f - 900 * (t / 0.05).clip(0, 1)) * t) * np.exp(-t * 35)
    return reverb(plip, size=1.8, mix=0.5)


def creak(i):
    """Grincement de plancher : résonance qui glisse."""
    d = 1.2 + 0.3 * i
    t = t_axis(d)
    f = 180 + 120 * np.sin(np.pi * t / d) + 40 * i
    phase = 2 * np.pi * np.cumsum(f) / SR
    grit = (np.abs(np.sin(phase * 0.5)) ** 8)
    x = bandpass(noise(d) * grit * 3 + np.sin(phase) * 0.2, 150, 3000) * np.sin(np.pi * t / d)
    return reverb(x, size=1.5, mix=0.35)


def child_giggle():
    """Rire d'enfant chuchoté, très loin (un des 23 ?)."""
    g = espeak("hee hee hee", speed=170, pitch=80)
    g = resample(g, 1.25)
    return reverb(lowpass(g, 2500), size=3.0, mix=0.65)


# ---------------------------------------------------------------- jumpscare et tension
def jumpscare():
    """Choc : boum grave qui chute + grappe dissonante + souffle + le cri, compressé."""
    d = 1.6
    t = t_axis(d)
    boom = np.sin(2 * np.pi * (90 * np.exp(-t * 3) + 28) * t) * np.exp(-t * 2.5) * 1.4
    cluster = sum(np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) for f in (466, 494, 523, 554, 988, 1046))
    cluster = np.tanh(cluster * 2) * np.exp(-t * 2.2)
    burst = highpass(noise(d), 1500) * np.exp(-t * 5)
    s = scream()[:len(t)]
    s = np.pad(s, (0, max(0, len(t) - len(s))))
    x = boom + cluster * 0.5 + burst * 0.6 + s * 0.8
    return np.tanh(x * 2.2)


def heartbeat_loop(bpm=112):
    beat = 60 / bpm
    x = np.zeros(int(SR * beat * 8))
    for k in range(8):
        for off, amp in ((0, 1.0), (0.18, 0.7)):
            at = int((k * beat + off) * SR)
            t = t_axis(0.25)
            th = np.sin(2 * np.pi * 50 * t) * np.exp(-t * 22) * amp
            x[at:at + len(t)] += th[:len(x) - at]
    return lowpass(x, 300)


# ---------------------------------------------------------------- interface
def coin():
    t = t_axis(0.6)
    x = sum(np.sin(2 * np.pi * f * t) * np.exp(-t * 7) for f in (1318.5, 1975.5))
    return reverb(x, size=0.8, mix=0.2)


def button_click():
    t = t_axis(0.08)
    return np.sin(2 * np.pi * 900 * t) * np.exp(-t * 90) + lowpass(noise(0.08), 3000) * np.exp(-t * 120) * 0.5


def denied():
    t = t_axis(0.35)
    return np.sign(np.sin(2 * np.pi * 140 * t)) * np.exp(-t * 6) * 0.6


# ---------------------------------------------------------------- papiers
def paper_rustle(dur, crackles):
    """Froissement de papier : souffle filtré + petits craquements."""
    t = t_axis(dur)
    hiss = bandpass(noise(dur), 2000, 9000) * (0.3 + 0.7 * np.abs(np.sin(np.pi * t / dur * 2.5)))
    x = hiss * 0.5
    for _ in range(crackles):
        at = int(rng.uniform(0, dur - 0.03) * SR)
        n = int(SR * 0.012)
        x[at:at + n] += highpass(noise(0.012), 3000)[:len(x) - at] * rng.uniform(0.4, 1.0)
    return x * np.sin(np.pi * t / dur) ** 0.5


def found_chime():
    """Papier trouvé : deux notes de boîte à musique, douces (pas une récompense "jeu vidéo")."""
    x = np.zeros(int(SR * 2.2))
    for i, m in enumerate((76, 83)):
        f = 440 * 2 ** ((m - 69) / 12)
        t = t_axis(1.6)
        bell = sum(np.sin(2 * np.pi * f * h * t) * np.exp(-t * (3 + 2 * h)) / h for h in (1, 2.0, 3.0))
        at = int(i * 0.28 * SR)
        x[at:at + len(t)] += bell
    return reverb(x, size=2.0, mix=0.4)[:len(x)]


# ---------------------------------------------------------------- portes
def door_slide():
    """Panneau qui coulisse : roulement grave + grincement métallique qui monte et descend."""
    d = 2.2
    t = t_axis(d)
    roll = lowpass(noise(d), 180) * 3 * (0.6 + 0.4 * np.sin(2 * np.pi * 7 * t) ** 2)
    f = 900 + 500 * np.sin(np.pi * t / d) + 60 * np.sin(2 * np.pi * 3 * t)
    phase = 2 * np.pi * np.cumsum(f) / SR
    squeak = np.sin(phase) * (np.abs(np.sin(2 * np.pi * 1.3 * t)) ** 6) * 0.35
    env_ = np.minimum(1, t / 0.15) * np.minimum(1, (d - t) / 0.3)
    return reverb((roll + squeak) * env_, size=1.4, mix=0.3)


def door_lock():
    """Fin de course : choc sourd + "clac" de pêne."""
    t = t_axis(0.6)
    thud = np.sin(2 * np.pi * (70 + 40 * np.exp(-t * 30)) * t) * np.exp(-t * 18)
    clack = highpass(noise(0.6), 1500) * np.exp(-((t - 0.08) ** 2) / 0.00005) * 0.8
    return reverb(thud + clack, size=1.2, mix=0.3)


def keys_unlock():
    """Trousseau qui tinte, puis une clé qui tourne dans la serrure."""
    x = keys_loop()[:int(SR * 0.9)] * np.linspace(1, 0.3, int(SR * 0.9))
    t = t_axis(0.5)
    turn = bandpass(noise(0.5), 800, 3000) * np.exp(-((t - 0.25) ** 2) / 0.002) * 0.6
    click = highpass(noise(0.5), 2000) * np.exp(-((t - 0.38) ** 2) / 0.00003) * 0.9
    return reverb(np.concatenate([x, turn + click]), size=1.0, mix=0.25)


# ---------------------------------------------------------------- tout fabriquer
def knock(i):
    """Trois petits coups de poing d'enfant sur une porte en bois, de l'autre côté."""
    pattern = ((0.0, 1.0), (0.32, 0.85), (0.62, 0.95)) if i == 0 else ((0.0, 0.9), (0.22, 0.7), (0.9, 1.0), (1.05, 0.6))
    x = np.zeros(int(SR * 1.8))
    for at, amp in pattern:
        d = 0.18
        t = t_axis(d)
        body = sum(np.sin(2 * np.pi * f * t) * np.exp(-t * k) for f, k in ((110 + 15 * i, 30), (230, 45), (480, 70)))
        thud = lowpass(noise(d), 1200) * np.exp(-t * 60)
        hit = (body * 0.8 + thud * 0.6) * amp
        s0 = int(at * SR)
        x[s0:s0 + len(hit)] += hit
    return reverb(lowpass(x, 2500), size=1.4, mix=0.35)


def door_voice(text):
    """Voix d'enfant étouffée derrière une porte (chuchotée, aigüe, sans aigus : le bois filtre)."""
    v = espeak(text, voice="en-us+whisperf", speed=120, pitch=85)
    v = lowpass(resample(v, 1.12), 1400)
    return reverb(v, size=1.2, mix=0.3)


def fan_loop():
    """Grand ventilateur qui tourne lentement : souffle qui revient à chaque pale + grondement (boucle 15 s)."""
    d = 15
    t = t_axis(d)
    blade = 0.5 + 0.5 * np.sin(2 * np.pi * t * (5 / 3.0)) ** 2        # 5 pales, un tour en 3 s
    air = bandpass(noise(d), 120, 900) * (0.4 + 0.6 * blade)
    rumble = sum(np.sin(2 * np.pi * f * t) * a for f, a in ((33, 1.0), (66.4, 0.4), (99.2, 0.15)))
    squeak = bandpass(noise(d), 1800, 2100, order=3) * (np.sin(2 * np.pi * t / 3.0) > 0.97) * 2
    return loopify(air * 1.2 + rumble * 0.35 + squeak, 1.5)


def chair_clatter(i):
    """Chaise en bois qui tombe : plusieurs chocs secs qui rebondissent."""
    x = np.zeros(int(SR * 1.2))
    for k, (at, amp) in enumerate(((0.0, 1.0), (0.13, 0.6), (0.24 + 0.03 * i, 0.45), (0.34, 0.25))):
        d = 0.12
        t = t_axis(d)
        f = 300 + 170 * ((k + i) % 3)
        hit = (np.sin(2 * np.pi * f * t) * np.exp(-t * 50) + highpass(noise(d), 900) * np.exp(-t * 80) * 0.8) * amp
        s0 = int(at * SR)
        x[s0:s0 + len(hit)] += hit
    return reverb(x, size=1.6, mix=0.3)


def chair_shiver():
    """La chaise tremble en l'air avant de tomber : petit cliquetis de bois qui s'accélère."""
    d = 0.9
    t = t_axis(d)
    rate = 12 + 30 * t / d
    clicks = (np.sin(2 * np.pi * np.cumsum(rate) / SR) > 0.92).astype(float)
    x = bandpass(clicks + noise(d) * 0.05, 500, 3000) * np.linspace(0.3, 1, len(t))
    return reverb(x, size=1.0, mix=0.25)


def step_surface(kind, i, hard=False):
    """Main de poupée qui tape une surface : chaque matière a sa signature (pas = coups de mains)."""
    d = 0.7
    t = t_axis(d)
    g = 1.0 if hard else 0.6
    sharp = 1.6 if hard else 1.0
    jit = 1 + 0.06 * (i - 1)
    if kind == "bois":
        body = sum(np.sin(2 * np.pi * f * jit * t) * np.exp(-t * k) for f, k in ((170, 28), (310, 40), (620, 70)))
        x = body * 0.9 + lowpass(noise(d), 2500) * np.exp(-t * 60 * sharp) * 0.7
        x = reverb(x, size=0.8, mix=0.18)
    elif kind == "carrelage":
        click = highpass(noise(d), 2500) * np.exp(-t * 140 / sharp)
        ring = np.sin(2 * np.pi * 1850 * jit * t) * np.exp(-t * 35) * 0.25
        x = click + ring + lowpass(noise(d), 900) * np.exp(-t * 70) * 0.5
        x = reverb(x, size=1.6, mix=0.35)
    elif kind == "beton":
        x = lowpass(noise(d), 1400) * np.exp(-t * 55) + np.sin(2 * np.pi * 95 * jit * t) * np.exp(-t * 35) * 0.6
        x += bandpass(noise(d), 2000, 5000) * np.exp(-t * 90) * 0.15 * sharp   # grain
        x = reverb(x, size=1.2, mix=0.25)
    elif kind == "metal":
        parts = ((523, 6), (1311, 9), (2207, 12), (3120, 16))
        x = sum(np.sin(2 * np.pi * f * jit * t + rng.uniform(0, 6)) * np.exp(-t * k / sharp) / (n + 1)
                for n, (f, k) in enumerate(parts))
        x += highpass(noise(d), 3000) * np.exp(-t * 120) * 0.5
        rattle = bandpass(noise(d), 800, 2400) * (np.sin(2 * np.pi * 38 * t) > 0.6) * np.exp(-t * 18) * 0.25 * sharp
        x = reverb(x + rattle, size=1.8, mix=0.3)
    elif kind == "tissu":
        x = lowpass(noise(d), 550) * np.exp(-t * 45) + np.sin(2 * np.pi * 85 * t) * np.exp(-t * 40) * 0.4
        x = reverb(x, size=0.6, mix=0.1) * 0.6
    elif kind == "encre":
        slap = bandpass(noise(d), 300, 2500) * np.exp(-t * 30)
        drops = np.zeros(len(t))
        for k in range(6 + 4 * int(hard)):          # gouttelettes qui retombent
            at = int(SR * rng.uniform(0.05, 0.45))
            n = int(SR * 0.04)
            tt = np.arange(n) / SR
            f = rng.uniform(900, 2200)
            drops[at:at + n] += np.sin(2 * np.pi * (f + 2000 * tt) * tt) * np.exp(-tt * 90)[:len(drops[at:at + n])] * 0.3
        x = reverb(slap + drops, size=1.4, mix=0.3)
    else:  # platre
        x = np.sin(2 * np.pi * 240 * jit * t) * np.exp(-t * 45) + lowpass(noise(d), 1800) * np.exp(-t * 70) * 0.6
        x = reverb(x, size=0.9, mix=0.2)
    return x[:int(SR * d)] * g


def breath_loop():
    """Respiration du joueur qui panique : inspirations courtes et tremblées, expirations par la bouche (boucle 8 s)."""
    d = 8
    x = np.zeros(int(SR * d))
    at = 0.0
    while at < d - 0.8:
        inh, exh = rng.uniform(0.35, 0.5), rng.uniform(0.45, 0.65)
        for dur, lo, hi, amp in ((inh, 900, 3800, 0.8), (exh, 400, 2200, 1.0)):
            n = int(SR * dur)
            tt = np.arange(n) / SR
            shake = 1 + 0.35 * np.sin(2 * np.pi * rng.uniform(9, 14) * tt)          # souffle qui tremble
            e = np.sin(np.pi * tt / dur) ** 1.5 * shake
            s0 = int(at * SR)
            x[s0:s0 + n] += bandpass(rng.uniform(-1, 1, n), lo, hi) * e * amp
            at += dur + 0.04
        at += rng.uniform(0.05, 0.2)
    return loopify(x, 0.4)


def slow_clap():
    """Une seule personne qui applaudit, lentement, dans un théâtre vide."""
    x = np.zeros(int(SR * 5.5))
    for k in range(5):
        at = int(SR * (0.2 + k * (0.85 + 0.08 * k)))          # ralentit
        d = 0.09
        tt = np.arange(int(SR * d)) / SR
        clap = bandpass(rng.uniform(-1, 1, len(tt)), 700, 3500) * np.exp(-tt * 70) * (1 - 0.1 * k)
        x[at:at + len(tt)] += clap
    return reverb(x, size=3.2, mix=0.6)


def softie_shuffle():
    """Frottement d'un gros costume de peluche qui avance (boucle 4 s) : tissu épais + pas étouffés."""
    d = 4
    t = t_axis(d)
    fabric = bandpass(noise(d), 300, 1800) * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * t * 1.1)))
    steps = np.zeros(len(t))
    for k in range(9):
        at = int(SR * (k * 0.45 + 0.1))
        tt = np.arange(int(SR * 0.2)) / SR
        steps[at:at + len(tt)] += lowpass(rng.uniform(-1, 1, len(tt)), 300) * np.exp(-tt * 30) * 2.5
    return loopify(fabric * 0.6 + steps, 0.3)


def softie_settle():
    """Quand tu te retournes : le costume se fige, un craquement de rembourrage et un petit rire étouffé."""
    d = 1.6
    t = t_axis(d)
    creak_ = bandpass(noise(d), 500, 2500) * np.exp(-t * 6) * (np.sin(2 * np.pi * 23 * t) > 0.3)
    g = espeak("hee", speed=160, pitch=90)
    g = lowpass(resample(g, 1.2), 900) * 0.6
    x = np.zeros(len(t))
    x += creak_ * 0.8
    off = int(SR * 0.6)
    x[off:off + len(g)] += g[:len(x) - off]
    return reverb(x, size=1.4, mix=0.35)


def main():
    lines = ["Shhh...", "I can hear you.", "Where are you, little one?", "Quiet. Quiet.",
             "Back to bed.", "Mila? Is that you?", "Nobody screams here.", "I hear everything."]
    for i, line in enumerate(lines):
        save("Monster", "whisper_%02d" % (i + 1), whisper(line), 0.8)
    for i in range(2):
        save("Monster", "hear_react_%02d" % (i + 1), inhale_react(), 0.9)
    save("Monster", "scream_chase", scream(), 0.95)
    for i in range(4):
        save("Monster", "step_%02d" % (i + 1), monster_step(i), 0.9)
    save("Monster", "keys_loop", keys_loop(), 0.6)

    for i in range(4):
        save("Player", "tap_soft_%02d" % (i + 1), tap(i), 0.7)
    for i in range(2):
        save("Player", "tap_hard_%02d" % (i + 1), tap(i, hard=True), 0.9)

    save("Ambience", "drone_loop", drone_loop(), 0.5)
    save("Ambience", "basement_loop", basement_loop(), 0.5)
    save("Ambience", "music_box_far", music_box_far(), 0.6)
    save("Ambience", "child_giggle_far", child_giggle(), 0.5)
    for i in range(3):
        save("Ambience", "drip_%02d" % (i + 1), drip(i), 0.5)
        save("Ambience", "creak_%02d" % (i + 1), creak(i), 0.6)

    save("Jumpscare", "jumpscare_sting", jumpscare(), 1.0)
    save("Jumpscare", "heartbeat_loop", heartbeat_loop(), 0.8)

    save("SafeZone", "lullaby_loop", safe_lullaby_loop(), 0.55)
    save("UI", "coin", coin(), 0.7)
    save("UI", "button_click", button_click(), 0.6)
    save("UI", "denied", denied(), 0.5)

    # Papiers (ajoutés à la fin pour ne pas changer les sons déjà générés)
    save("Papers", "paper_pickup", paper_rustle(0.45, 9), 0.7)
    save("Papers", "paper_rustle", paper_rustle(0.9, 16), 0.6)
    save("Papers", "paper_found", found_chime(), 0.55)

    # Portes du Protocole Verrou (ajoutées à la fin pour ne pas changer les sons existants)
    save("Doors", "door_slide", door_slide(), 0.75)
    save("Doors", "door_lock", door_lock(), 0.8)
    save("Doors", "keys_unlock", keys_unlock(), 0.8)

    # Galerie des portes, ventilateur, chaises volantes (ajoutés à la fin : les sons existants ne changent pas)
    for i in range(2):
        save("World", "knock_%02d" % (i + 1), knock(i), 0.8)
    for i, line in enumerate(("let me out", "is it you?", "dont open it")):
        save("World", "door_voice_%02d" % (i + 1), door_voice(line), 0.6)
    save("World", "fan_loop", fan_loop(), 0.6)
    for i in range(2):
        save("World", "chair_clatter_%02d" % (i + 1), chair_clatter(i), 0.85)
    save("World", "chair_shiver", chair_shiver(), 0.6)

    # Pas selon la surface (ajoutés à la fin : les sons existants ne changent pas)
    for kind in ("bois", "carrelage", "beton", "metal", "tissu", "encre", "platre"):
        for i in range(3):
            save("Steps", "%s_soft_%02d" % (kind, i + 1), step_surface(kind, i), 0.6 if kind != "tissu" else 0.4)
        for i in range(2):
            save("Steps", "%s_hard_%02d" % (kind, i + 1), step_surface(kind, i, hard=True), 0.85 if kind != "tissu" else 0.55)

    save("Player", "breath_loop", breath_loop(), 0.5)   # la peur (ajouté à la fin)

    # Théâtre des Doux (ajoutés à la fin)
    save("World", "slow_clap", slow_clap(), 0.7)
    save("World", "softie_shuffle", softie_shuffle(), 0.6)
    save("World", "softie_settle", softie_settle(), 0.7)

    total = sum(len(fs) for _, _, fs in os.walk(OUT))
    print("OK ->", OUT, "|", total, "fichiers")


if __name__ == "__main__":
    main()
