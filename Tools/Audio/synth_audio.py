"""
Sintetizador de audio de SpellSimulator (M5). Genera efectos, ambientes y una musica ambiental en WAV (44.1 kHz,
16 bits, mono) dentro de Assets/_Project/Audio/Synth. Determinista (semillas fijas): correrlo de nuevo da lo mismo.

Uso:  python Tools/Audio/synth_audio.py
Solo depende de numpy. Los loops se cierran con fundido cruzado para que no hagan "click" al repetirse.
"""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Audio", "Synth")
rng = np.random.default_rng(7)


# ------------------------------------------------------------------ utilidades

def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def noise(seconds):
    return rng.standard_normal(int(seconds * SR))


def spectral(signal, low=None, high=None, tilt=0.0):
    """Filtro por FFT: pasa banda [low, high] Hz y pendiente (dB/octava aprox) para colorear ruido."""
    spec = np.fft.rfft(signal)
    f = np.fft.rfftfreq(len(signal), 1 / SR)
    gain = np.ones_like(f)
    if low:
        gain *= 1 / (1 + (low / np.maximum(f, 1)) ** 4)
    if high:
        gain *= 1 / (1 + (np.maximum(f, 1) / high) ** 4)
    if tilt:
        gain *= (np.maximum(f, 20) / 1000.0) ** (tilt / 6.0)
    return np.fft.irfft(spec * gain, len(signal))


def sweep_filter(signal, f_from, f_to, width=0.6, blocks=64):
    """Paso banda que se mueve en el tiempo (por bloques solapados)."""
    out = np.zeros_like(signal)
    n = len(signal)
    size = n // blocks
    win = np.hanning(size * 2)
    for b in range(blocks):
        start = max(0, b * size - size // 2)
        seg = signal[start:start + size * 2]
        if len(seg) < 16:
            continue
        k = b / max(blocks - 1, 1)
        fc = f_from * (f_to / f_from) ** k
        filtered = spectral(seg, fc * (1 - width), fc * (1 + width))
        out[start:start + len(seg)] += filtered * win[:len(seg)]
    return out


def env(seconds, attack, release, curve=2.0):
    t = t_axis(seconds)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    r = np.clip((seconds - t) / max(release, 1e-4), 0, 1) ** curve
    return a * r


def reverb(signal, seconds=1.8, mix=0.3, brightness=4000):
    impulse = noise(seconds) * np.exp(-t_axis(seconds) * 4.5 / seconds)
    impulse = spectral(impulse, high=brightness)
    impulse /= np.sqrt(np.sum(impulse ** 2))
    n = len(signal) + len(impulse)
    wet = np.fft.irfft(np.fft.rfft(signal, n) * np.fft.rfft(impulse, n), n)[:n]
    dry = np.concatenate([signal, np.zeros(len(impulse))])
    return dry * (1 - mix) + wet * mix


def bell(freq, seconds, decay=3.0, partials=((1, 1), (2.0, 0.5), (2.76, 0.3), (5.4, 0.12))):
    t = t_axis(seconds)
    out = np.zeros_like(t)
    for ratio, amp in partials:
        out += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t * decay * ratio ** 0.5)
    return out * np.clip(t / 0.003, 0, 1)


def crackle(seconds, density=40, low=1500):
    """Chisporroteo: impulsos al azar filtrados."""
    n = int(seconds * SR)
    out = np.zeros(n)
    count = int(seconds * density)
    for _ in range(count):
        p = rng.integers(0, n - 400)
        length = rng.integers(40, 300)
        out[p:p + length] += rng.standard_normal(length) * np.exp(-np.arange(length) / (length / 4)) * rng.uniform(0.3, 1)
    return spectral(out, low=low, high=9000)


def loop_close(signal, fade=0.5):
    """Cierra un loop: mezcla el final sobre el principio con fundido cruzado."""
    f = int(fade * SR)
    head, tail = signal[:f], signal[-f:]
    ramp = np.linspace(0, 1, f)
    body = signal[f:-f] if len(signal) > 2 * f else signal[f:]
    joined = tail * (1 - ramp) + head * ramp
    return np.concatenate([joined, body])


def save(name, signal, peak=0.8):
    os.makedirs(OUT, exist_ok=True)
    m = np.max(np.abs(signal))
    if m > 0:
        signal = signal / m * peak
    data = (np.clip(signal, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"{name}.wav  {len(data) / SR:.1f}s")


# ------------------------------------------------------------------ sello y hechizos

def seal_prepared():
    s = bell(880, 1.4, 3.5) * 0.6
    s[int(0.12 * SR):] += bell(1318.5, 1.4 - 0.12, 3.5)[:len(s) - int(0.12 * SR)] * 0.5
    return reverb(s, 1.2, 0.35)


def cast(element):
    d = 1.6
    t = t_axis(d)
    if element == "fire":
        s = sweep_filter(noise(d), 300, 2500, 0.8) * env(d, 0.05, 1.2) + crackle(d, 60) * env(d, 0.1, 1.0) * 0.6
        s += np.sin(2 * np.pi * 70 * t) * env(d, 0.01, 0.6) * 0.5
    elif element == "water":
        s = sweep_filter(noise(d), 1800, 400, 0.7) * env(d, 0.04, 1.2)
        for _ in range(18):
            p = rng.uniform(0.1, 1.2)
            f = rng.uniform(500, 1400)
            k = t_axis(0.08)
            blob = np.sin(2 * np.pi * (f + 3000 * k) * k) * np.exp(-k * 50)
            i = int(p * SR)
            s[i:i + len(blob)] += blob * 0.3
    elif element == "wind":
        s = sweep_filter(noise(d), 400, 1600, 0.5) * env(d, 0.25, 1.0) * 1.2
    elif element == "earth":
        s = spectral(noise(d), high=180) * env(d, 0.02, 1.3) * 3
        s += np.sin(2 * np.pi * 45 * t) * np.exp(-t * 3) * 1.5 + crackle(d, 25, 300) * env(d, 0.05, 0.8)
    else:  # light
        s = np.zeros_like(t)
        for i, f in enumerate([1046.5, 1318.5, 1568, 2093, 2637]):
            off = int(i * 0.07 * SR)
            b = bell(f, d - i * 0.07, 2.5) * 0.4
            s[off:off + len(b)] += b
        s += spectral(noise(d), low=5000) * env(d, 0.3, 1.0) * 0.15
    return reverb(s, 1.4, 0.25)


def fizzle():
    d = 0.7
    t = t_axis(d)
    s = crackle(d, 90, 2500) * env(d, 0.005, 0.6)
    s += np.sin(2 * np.pi * (600 - 500 * t) * t) * env(d, 0.01, 0.5) * 0.3
    return s


def seal_break():
    d = 0.6
    s = np.zeros(int(d * SR))
    for i in range(7):
        p = int(i * 0.06 * SR + rng.integers(0, 800))
        burst = noise(0.05) * np.exp(-t_axis(0.05) * 40)
        s[p:p + len(burst)] += burst
    return spectral(s, low=900, high=8000)


# ------------------------------------------------------------------ herramientas

def pen_loop():
    d = 2.0
    t = t_axis(d)
    s = spectral(noise(d), low=2500, high=9000) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 7 * t + np.sin(2 * np.pi * 1.3 * t))))
    return loop_close(s, 0.3)


def eraser_loop():
    d = 2.0
    t = t_axis(d)
    s = spectral(noise(d), low=300, high=2000) * (0.7 + 0.3 * np.sin(2 * np.pi * 5 * t))
    return loop_close(s, 0.3)


def rag_wipe():
    d = 0.5
    return sweep_filter(noise(d), 600, 2200, 0.6, 24) * env(d, 0.08, 0.3)


def ink_refill():
    d = 0.9
    s = np.zeros(int(d * SR))
    for i in range(6):
        k = t_axis(0.12)
        f = 300 + i * 60 + rng.uniform(-20, 20)
        blob = np.sin(2 * np.pi * (f + 900 * k) * k) * np.exp(-k * 25)
        p = int(i * 0.12 * SR)
        s[p:p + len(blob)] += blob
    return reverb(s, 0.6, 0.2)


def pickup():
    d = 0.15
    return spectral(noise(d), low=1000, high=5000) * env(d, 0.002, 0.12) + bell(1500, d, 30) * 0.2


# ------------------------------------------------------------------ mundo

def fire_loop():
    d = 5.0
    s = crackle(d, 35) + spectral(noise(d), high=500) * 0.25
    return loop_close(s, 0.5)


def ignite():
    d = 1.0
    return sweep_filter(noise(d), 200, 1500, 0.8, 32) * env(d, 0.1, 0.7) + crackle(d, 40) * env(d, 0.2, 0.6) * 0.5


def hiss():
    d = 1.4
    return spectral(noise(d), low=3000, high=11000) * env(d, 0.02, 1.2, 1.5)


def splash():
    d = 0.9
    s = spectral(noise(d), low=300, high=5000) * env(d, 0.005, 0.8, 3)
    for _ in range(10):
        k = t_axis(0.06)
        blob = np.sin(2 * np.pi * rng.uniform(700, 1600) * k) * np.exp(-k * 60)
        p = int(rng.uniform(0.05, 0.6) * SR)
        s[p:p + len(blob)] += blob * 0.4
    return s


def crack(material):
    d = 0.8
    t = t_axis(d)
    if material == "wood":
        s = spectral(noise(d), low=400, high=4000) * np.exp(-t * 18) * 2
        s += np.sin(2 * np.pi * 180 * t) * np.exp(-t * 25)
        s += crackle(d, 50, 800) * np.exp(-t * 6)
    else:
        s = spectral(noise(d), low=150, high=2500) * np.exp(-t * 12) * 2
        s += np.sin(2 * np.pi * 90 * t) * np.exp(-t * 15) + crackle(d, 40, 1500) * np.exp(-t * 5) * 0.6
    return reverb(s, 0.8, 0.15)


def thud():
    d = 0.4
    t = t_axis(d)
    return np.sin(2 * np.pi * (110 - 60 * t) * t) * np.exp(-t * 14) + spectral(noise(d), high=800) * np.exp(-t * 30) * 0.5


def rock_rise():
    d = 1.0
    return spectral(noise(d), high=220) * env(d, 0.15, 0.7) * 3 + crackle(d, 30, 400) * env(d, 0.1, 0.6)


def reset_chime():
    d = 2.0
    s = np.zeros(int(d * SR))
    for i, f in enumerate([523.25, 659.25, 783.99, 1046.5, 1318.5]):
        b = bell(f, d - i * 0.08, 2.2) * 0.5
        p = int(i * 0.08 * SR)
        s[p:p + len(b)] += b
    return reverb(s, 2.0, 0.4)


# ------------------------------------------------------------------ ambiente

def wind_loop():
    d = 24.0
    t = t_axis(d)
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * t / 8.0) * np.sin(2 * np.pi * t / 5.3 + 1)
    s = sweep_filter(noise(d), 250, 700, 0.5, 96) * gust
    return loop_close(s, 2.0)


def waterfall_loop():
    d = 8.0
    s = spectral(noise(d), low=80, high=6000, tilt=-3) + spectral(noise(d), low=1500, high=8000) * 0.3
    return loop_close(s, 1.0)


def bird(seed):
    r = np.random.default_rng(seed)
    d = 1.2
    s = np.zeros(int(d * SR))
    p = 0
    for _ in range(r.integers(2, 6)):
        length = r.uniform(0.05, 0.14)
        k = t_axis(length)
        f0, f1 = r.uniform(2500, 4200), r.uniform(2000, 5000)
        chirp = np.sin(2 * np.pi * (f0 * k + (f1 - f0) * k * k / (2 * length))) * np.sin(np.pi * k / length)
        i = int(p * SR)
        if i + len(chirp) >= len(s):
            break
        s[i:i + len(chirp)] += chirp
        p += length + r.uniform(0.03, 0.12)
    return reverb(s, 0.8, 0.2)


# ------------------------------------------------------------------ musica

def music_pad():
    """~64 s de pads suaves (Cmaj7, Am9, Fmaj7, Gsus) con campanitas pentatonicas. Loopeable."""
    bpm_bar = 8.0
    chords = [
        [130.81, 196.00, 246.94, 329.63],  # Cmaj7
        [110.00, 164.81, 246.94, 261.63],  # Am9-ish
        [87.31, 174.61, 220.00, 329.63],   # Fmaj7
        [98.00, 146.83, 196.00, 261.63],   # Gsus4
    ]
    total = bpm_bar * len(chords) * 2
    n = int(total * SR)
    out = np.zeros(n + int(10 * SR))
    for rep in range(2):
        for ci, chord in enumerate(chords):
            start = (rep * len(chords) + ci) * bpm_bar
            d = bpm_bar + 3.0
            t = t_axis(d)
            pad = np.zeros_like(t)
            for f in chord:
                for det in (-0.6, 0.0, 0.7):
                    ff = f * 2 ** (det / 1200 * 10)
                    pad += np.sin(2 * np.pi * ff * t) + 0.25 * np.sin(2 * np.pi * 2 * ff * t)
            pad *= env(d, 2.5, 3.5, 1.5) * (0.85 + 0.15 * np.sin(2 * np.pi * 0.2 * t))
            i = int(start * SR)
            out[i:i + len(pad)] += pad * 0.12
    # Campanitas: pentatonica de Do, pocas notas al azar.
    scale = [523.25, 587.33, 659.25, 783.99, 880.0, 1046.5]
    r = np.random.default_rng(11)
    p = 1.5
    while p < total - 2:
        b = bell(scale[r.integers(0, len(scale))], 3.0, 1.8) * r.uniform(0.15, 0.3)
        i = int(p * SR)
        out[i:i + len(b)] += b
        p += r.choice([1.0, 2.0, 2.0, 3.0, 4.0])
    out = reverb(out, 3.5, 0.45, 3000)
    # Cierre de loop: la cola de reverb pasa al principio.
    body = out[:n].copy()
    tail = out[n:n + int(8 * SR)]
    body[:len(tail)] += tail
    return body


if __name__ == "__main__":
    save("seal_prepared", seal_prepared(), 0.5)
    for e in ["fire", "water", "wind", "earth", "light"]:
        save(f"cast_{e}", cast(e))
    save("cast_fizzle", fizzle(), 0.6)
    save("seal_break", seal_break(), 0.6)
    save("pen_loop", pen_loop(), 0.35)
    save("eraser_loop", eraser_loop(), 0.35)
    save("rag_wipe", rag_wipe(), 0.5)
    save("ink_refill", ink_refill(), 0.5)
    save("pickup", pickup(), 0.4)
    save("fire_loop", fire_loop(), 0.6)
    save("ignite", ignite(), 0.6)
    save("hiss", hiss(), 0.5)
    save("splash", splash(), 0.7)
    save("crack_wood", crack("wood"))
    save("crack_stone", crack("stone"))
    save("thud", thud(), 0.6)
    save("rock_rise", rock_rise(), 0.7)
    save("reset_chime", reset_chime(), 0.5)
    save("wind_loop", wind_loop(), 0.5)
    save("waterfall_loop", waterfall_loop(), 0.6)
    for i in range(4):
        save(f"bird_{i}", bird(100 + i), 0.35)
    save("music_pad", music_pad(), 0.55)
