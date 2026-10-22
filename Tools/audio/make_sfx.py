"""Synthesises the game sounds the CC0 packs don't have: pops, squeaks, whooshes, zaps and sparkles.

Standard library only, seeded, so every run writes the same files.
Usage: python3 -I Tools/audio/make_sfx.py <out_dir>
"""
import math
import random
import struct
import sys
import wave
from pathlib import Path

RATE = 44100
PEAK = 0.89


def seconds(n):
    return int(n * RATE)


def env(i, n, attack, release_curve=4.0):
    """Linear attack, then an exponential-looking fall to zero at n."""
    a = seconds(attack)
    if i < a:
        return i / max(1, a)
    t = (i - a) / max(1, n - a)
    return (1.0 - t) ** release_curve


class Svf:
    """Trapezoidal state-variable filter: stable at any cutoff, which can change per sample."""

    def __init__(self, q=0.7):
        self.k = 1.0 / q
        self.ic1 = self.ic2 = 0.0

    def step(self, x, cutoff):
        g = math.tan(math.pi * min(cutoff, RATE * 0.45) / RATE)
        a1 = 1.0 / (1.0 + g * (g + self.k))
        a2 = g * a1
        a3 = g * a2
        v3 = x - self.ic2
        band = a1 * self.ic1 + a2 * v3
        low = self.ic2 + a2 * self.ic1 + a3 * v3
        self.ic1 = 2 * band - self.ic1
        self.ic2 = 2 * low - self.ic2
        return low, band, x - self.k * band - low


def finish(samples, fade_in=0.002, fade_out=0.006):
    n = len(samples)
    fi, fo = seconds(fade_in), seconds(fade_out)
    for i in range(min(fi, n)):
        samples[i] *= i / fi
    for i in range(min(fo, n)):
        samples[n - 1 - i] *= i / fo
    peak = max(1e-9, max(abs(s) for s in samples))
    return [s / peak * PEAK for s in samples]


def soft_clip(x, drive=1.0):
    return math.tanh(x * drive)


def pop(f0, length=0.11, seed=0):
    """A round bubble pop: a sine dropping in pitch, with a short click on top."""
    rng = random.Random(seed)
    n = seconds(length)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        f = f0 * (1.0 - 0.55 * (1 - (1 - t) ** 3))
        phase += 2 * math.pi * f / RATE
        body = math.sin(phase) + 0.25 * math.sin(2 * phase)
        click = rng.uniform(-1, 1) * max(0.0, 1 - i / seconds(0.006))
        out.append(soft_clip((body * env(i, n, 0.002, 2.5) + 0.35 * click), 1.4))
    return finish(out)


def balloon(seed=0):
    """A sharp burst of noise with a short bright tail, over a knock that phone speakers can play."""
    rng = random.Random(seed)
    n = seconds(0.18)
    hp, lp = Svf(0.6), Svf(0.6)
    out, phase = [], 0.0
    for i in range(n):
        noise = rng.uniform(-1, 1)
        _, _, high = hp.step(noise, 700)
        low, _, _ = lp.step(high, 8000 - 5000 * i / n)
        burst = low * (math.exp(-i / seconds(0.03)) + 0.25 * math.exp(-i / seconds(0.08)))
        phase += 2 * math.pi * (420 - 200 * i / n) / RATE
        knock = math.sin(phase) * math.exp(-i / seconds(0.025))
        out.append(1.6 * burst + 0.5 * knock)
    return finish(out, fade_in=0.0005)


def squeak(base, seed=0):
    """A rubber-duck squeak: a nasal tone that rises and wobbles."""
    rng = random.Random(seed)
    n = seconds(0.22)
    band = Svf(4.0)
    out, phase = [], 0.0
    wobble = rng.uniform(26, 34)
    for i in range(n):
        t = i / n
        f = base * (1 + 0.45 * math.sin(math.pi * min(1, t * 1.6))) * (1 + 0.03 * math.sin(2 * math.pi * wobble * i / RATE))
        phase += 2 * math.pi * f / RATE
        saw = 2 * ((phase / (2 * math.pi)) % 1) - 1
        _, formant, _ = band.step(saw, 2400)
        a = min(1, t * 10) * (1 - t) ** 1.2
        out.append(formant * 1.2 * a + 0.12 * saw * a)
    return finish(out)


def whoosh(length, start, end, seed=0, q=1.6):
    """Noise through a band-pass that sweeps from start to end Hz, swelling then fading."""
    rng = random.Random(seed)
    n = seconds(length)
    svf = Svf(q)
    out = []
    for i in range(n):
        t = i / n
        cutoff = start * (end / start) ** t
        _, band, _ = svf.step(rng.uniform(-1, 1), cutoff)
        a = math.sin(math.pi * min(1.0, t ** 0.7)) ** 1.5
        out.append(band * a)
    return finish(out, fade_out=0.02)


def fuse(seed=0):
    """A fizzing fuse: thin hiss with random crackles."""
    rng = random.Random(seed)
    n = seconds(0.5)
    hp = Svf(0.7)
    out = []
    crackle = 0.0
    for i in range(n):
        _, _, high = hp.step(rng.uniform(-1, 1), 4500)
        if rng.random() < 0.0016:
            crackle = rng.uniform(0.6, 1.0)
        crackle *= 0.985
        out.append(high * 0.35 + crackle * rng.uniform(-1, 1))
    return finish(out, fade_in=0.03, fade_out=0.05)


def zap(f0, seed=0):
    """A short electric zap: a buzzy tone falling fast, with noise."""
    rng = random.Random(seed)
    n = seconds(0.13)
    out, phase, mod = [], 0.0, 0.0
    for i in range(n):
        t = i / n
        f = f0 * (1.6 - 0.9 * t)
        mod += 2 * math.pi * f * 2.01 / RATE
        phase += 2 * math.pi * f / RATE + 0.9 * math.sin(mod) / 20
        tone = math.copysign(abs(math.sin(phase)) ** 0.5, math.sin(phase))
        out.append((0.8 * tone + 0.3 * rng.uniform(-1, 1)) * env(i, n, 0.002, 2.2))
    return finish(out)


def bloop(f0, seed=0):
    """A collect sound: a soft tone sliding up, with a bell-like overtone."""
    n = seconds(0.16)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        f = f0 * (1 + 0.5 * (1 - (1 - min(1, t * 2.5)) ** 2))
        phase += 2 * math.pi * f / RATE
        out.append((math.sin(phase) + 0.3 * math.sin(3 * phase) * (1 - t)) * env(i, n, 0.004, 2.0))
    return finish(out)


def land(f0, seed=0):
    """A soft wooden tock for a piece settling; pitched high enough for phone speakers."""
    rng = random.Random(seed)
    n = seconds(0.08)
    band = Svf(3.0)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        phase += 2 * math.pi * f0 * (1 - 0.15 * t) / RATE
        _, knock, _ = band.step(rng.uniform(-1, 1), f0 * 2.4)
        tone = math.sin(phase) + 0.4 * math.sin(2.7 * phase)
        out.append(tone * math.exp(-i / seconds(0.018)) + 0.6 * knock * math.exp(-i / seconds(0.006)))
    return finish(out, fade_in=0.0005)


PENTATONIC = [0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24]


def sparkle(length, notes, seed=0, root=1046.5):
    """A run of bell pings on a pentatonic scale."""
    rng = random.Random(seed)
    n = seconds(length)
    out = [0.0] * n
    for k in range(notes):
        start = int(k * n / (notes + 2) + rng.uniform(0, n / (notes * 3)))
        f = root * 2 ** (rng.choice(PENTATONIC) / 12)
        ring = seconds(0.25)
        for i in range(ring):
            j = start + i
            if j >= n:
                break
            e = math.exp(-i / seconds(0.06))
            out[j] += (math.sin(2 * math.pi * f * i / RATE) + 0.3 * math.sin(2 * math.pi * f * 2.76 * i / RATE)) * e
    return finish(out)


def riser(length, seed=0):
    """Rising tone and noise for the gather before a combo."""
    rng = random.Random(seed)
    n = seconds(length)
    svf = Svf(2.0)
    out, phase = [], 0.0
    for i in range(n):
        t = i / n
        f = 220 * 2 ** (2.2 * t ** 1.5)
        phase += 2 * math.pi * f / RATE
        _, band, _ = svf.step(rng.uniform(-1, 1), 600 + 5000 * t)
        a = t ** 1.4
        out.append((0.6 * math.sin(phase) + 0.3 * math.sin(2 * phase) + 0.7 * band) * a)
    return finish(out, fade_out=0.01)


def confetti(seed=0):
    """A party popper: a crack, then paper rustle."""
    rng = random.Random(seed)
    n = seconds(0.6)
    hp = Svf(0.7)
    out = []
    for i in range(n):
        _, _, high = hp.step(rng.uniform(-1, 1), 2500)
        crack = rng.uniform(-1, 1) * math.exp(-i / seconds(0.012))
        rustle = high * 0.35 * math.exp(-i / seconds(0.18)) * (0.6 + 0.4 * rng.random())
        out.append(crack + rustle)
    return finish(out, fade_in=0.0003, fade_out=0.05)


SOUNDS = {
    "sfx_pop_1": lambda: pop(620, seed=1),
    "sfx_pop_2": lambda: pop(700, seed=2),
    "sfx_pop_3": lambda: pop(780, seed=3),
    "sfx_balloon_1": lambda: balloon(seed=1),
    "sfx_balloon_2": lambda: balloon(seed=2),
    "sfx_squeak_1": lambda: squeak(820, seed=1),
    "sfx_squeak_2": lambda: squeak(900, seed=2),
    "sfx_whoosh_1": lambda: whoosh(0.38, 500, 4200, seed=1),
    "sfx_whoosh_2": lambda: whoosh(0.34, 600, 5000, seed=2),
    "sfx_whoosh_short": lambda: whoosh(0.22, 800, 3600, seed=3),
    "sfx_fuse": lambda: fuse(seed=1),
    "sfx_zap_1": lambda: zap(520, seed=1),
    "sfx_zap_2": lambda: zap(600, seed=2),
    "sfx_bloop": lambda: bloop(660),
    "sfx_land_1": lambda: land(380, seed=1),
    "sfx_land_2": lambda: land(430, seed=2),
    "sfx_sparkle": lambda: sparkle(0.6, 6, seed=1),
    "sfx_sparkle_long": lambda: sparkle(1.1, 12, seed=2),
    "sfx_riser": lambda: riser(0.55, seed=1),
    "sfx_confetti": lambda: confetti(seed=1),
}


def write(path, samples):
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in samples))


def main():
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    for name, make in SOUNDS.items():
        samples = make()
        write(out / f"{name}.wav", samples)
        print(f"{name}.wav  {len(samples) / RATE:.2f}s")


if __name__ == "__main__":
    main()
