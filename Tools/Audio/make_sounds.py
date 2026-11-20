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


# ---------------------------------------------------------------- tout fabriquer
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

    total = sum(len(fs) for _, _, fs in os.walk(OUT))
    print("OK ->", OUT, "|", total, "fichiers")


if __name__ == "__main__":
    main()
