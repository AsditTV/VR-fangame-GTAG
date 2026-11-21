"""
Chuchotements d'enfant entendus pendant le noir après une mort (et son de la mort vu par les autres).

Voix : synthèse vocale de Windows (SAPI, voix anglaise installée), puis « chuchotée » : on garde l'enveloppe
du spectre (les mots) et on remplace la phase par du hasard (plus de voix voisée = un souffle qui parle),
on monte la hauteur (voix plus jeune) et on ajoute une petite réverbération de pièce vide.

Les phrases DOIVENT rester dans le même ordre que Jumpscare.whisperLines (sous-titres).
Usage (Windows, depuis la racine du dépôt) : python Tools/Audio/make_whispers.py
"""
import os
import subprocess
import tempfile

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, resample_poly, sosfilt, stft, istft

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Horror", "Audio", "Death")
SR = 44100
LINES = [
    "You breathed too loud.",
    "She heard your heart.",
    "Mila hid under the bed. Hide like Mila.",
    "Don't run. She hears running.",
    "We were twenty three.",
    "The softies move when you look away.",
    "Odette is still in the water.",
    "Count the doors. One is missing.",
    "Now you know why we whisper.",
    "Find my drawing. Please.",
]
rng = np.random.default_rng(23)


def tts(text, path, voice="Microsoft Zira Desktop", rate=-3):
    ps = (
        "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
        f"try {{ $s.SelectVoice('{voice}') }} catch {{}}; $s.Rate = {rate}; "
        f"$s.SetOutputToWaveFile('{path}'); $s.Speak('{text.replace(chr(39), chr(39) * 2)}'); $s.Dispose()"
    )
    subprocess.run(["powershell.exe", "-NoProfile", "-Command", ps], check=True)


def load(path):
    sr, a = wavfile.read(path)
    a = a.astype(np.float32)
    if a.ndim > 1:
        a = a.mean(axis=1)
    a /= np.max(np.abs(a)) + 1e-9
    if sr != SR:
        g = np.gcd(sr, SR)
        a = resample_poly(a, SR // g, sr // g)
    return a


def whisperize(a):
    f, t, Z = stft(a, SR, nperseg=1024, noverlap=768)
    mag = np.abs(Z)
    # adoucit le peigne des harmoniques (la voix « chantée ») avant de casser la phase
    k = np.ones(5) / 5
    mag = np.apply_along_axis(lambda c: np.convolve(c, k, mode="same"), 0, mag)
    phase = rng.uniform(-np.pi, np.pi, mag.shape)
    _, y = istft(mag * np.exp(1j * phase), SR, nperseg=1024, noverlap=768)
    return y.astype(np.float32)


def pitch_up(a, semitones=3.5):
    # rééchantillonnage : plus aigu ET un peu plus rapide (une voix d'enfant pressée, essoufflée)
    r = 2 ** (semitones / 12)
    up, down = int(round(1000 / r)), 1000
    return resample_poly(a, up, down).astype(np.float32)


def room(a, tail=0.9, mix=0.22):
    n = int(SR * tail)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = sosfilt(butter(2, 3500, "low", fs=SR, output="sos"), ir)
    wet = np.convolve(a, ir)[: len(a) + n]
    wet = np.pad(wet, (0, len(a) + n - len(wet)))
    wet /= np.max(np.abs(wet)) + 1e-9
    dry = np.concatenate([a, np.zeros(n)])
    return dry * (1 - mix) + wet * mix


def finish(a, peak=0.7):
    a = sosfilt(butter(2, 280, "high", fs=SR, output="sos"), a)
    fade = int(0.02 * SR)
    a[:fade] *= np.linspace(0, 1, fade)
    a[-fade:] *= np.linspace(1, 0, fade)
    return (a / (np.max(np.abs(a)) + 1e-9) * peak).astype(np.float32)


def save(name, a):
    wavfile.write(os.path.join(OUT, name), SR, (a * 32767).astype(np.int16))
    print("->", name, round(len(a) / SR, 2), "s")


def death_sound():
    """La mort vue de loin : une éclaboussure d'encre (coup sourd mouillé) puis du papier qui se froisse."""
    d = 1.6
    t = np.arange(int(SR * d)) / SR
    thud = np.sin(2 * np.pi * (48 * t + 30 * (1 - np.exp(-9 * t)) / 9)) * np.exp(-7 * t)
    splash = rng.standard_normal(len(t)) * np.exp(-11 * t)
    splash = sosfilt(butter(2, [300, 2500], "band", fs=SR, output="sos"), splash)
    crumple = np.zeros(len(t))
    for _ in range(140):                                           # petits craquements de papier
        at = int(SR * (0.15 + rng.exponential(0.35)))
        if at >= len(t) - 400:
            continue
        n = rng.integers(60, 380)
        crumple[at:at + n] += rng.standard_normal(n) * np.exp(-np.linspace(0, 6, n)) * rng.uniform(0.2, 1)
    crumple = sosfilt(butter(2, 1800, "high", fs=SR, output="sos"), crumple) * np.exp(-1.6 * t)
    return finish(room(thud * 0.9 + splash * 0.6 + crumple * 0.5, 0.6, 0.18), 0.85)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    tmp = tempfile.mkdtemp()
    for i, line in enumerate(LINES, 1):
        p = os.path.join(tmp, f"l{i}.wav")
        tts(line, p, "Microsoft Zira Desktop" if i % 2 else "Microsoft Hazel Desktop")
        a = whisperize(load(p))
        save(f"murmure_{i:02d}.wav", finish(room(pitch_up(a))))
    save("encre_mort.wav", death_sound())
