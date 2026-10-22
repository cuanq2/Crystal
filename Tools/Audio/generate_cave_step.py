"""Generate a synthetic replacement for Sound/22.wav using only Python stdlib."""

import argparse
import math
from pathlib import Path
import random
import struct
import wave


def generate():
    rate = 44100
    duration = 0.66
    rng = random.Random(10022)
    dry = []
    low = 0.0
    smooth = 0.0
    for i in range(round(rate * duration)):
        t = i / rate
        noise = rng.uniform(-1, 1)
        low += 0.045 * (noise - low)
        smooth += 0.32 * (noise - smooth)
        # A short heel impact, followed by the softer contact of the sole.
        attack = 1 - math.exp(-t / 0.0015)
        heel = attack * math.exp(-t / 0.018)
        body = (math.sin(2 * math.pi * 108 * t)
                + 0.38 * math.sin(2 * math.pi * 187 * t))
        sole_t = max(0.0, t - 0.025)
        sole = (1 - math.exp(-sole_t / 0.008)) * math.exp(-sole_t / 0.030)
        grit = smooth - low
        dry.append(heel * (0.55 * body + 1.7 * low + 0.32 * grit)
                   + sole * (0.8 * low + 0.28 * grit))

    samples = dry.copy()
    # Subtle, diffuse reflections; no sampled or downloaded audio is used.
    for delay, gain in [(0.047, 0.13), (0.073, 0.09), (0.113, 0.055),
                        (0.163, 0.035), (0.229, 0.018), (0.307, 0.009)]:
        offset = round(delay * rate)
        for i in range(offset, len(samples)):
            samples[i] += gain * dry[i - offset]

    mean = sum(samples) / len(samples)
    samples = [s - mean for s in samples]
    # Fade both ends and match the quiet peak level of adjacent cave steps.
    for i in range(len(samples)):
        samples[i] *= min(1.0, i / (rate * 0.001),
                          (len(samples) - 1 - i) / (rate * 0.020))
    gain = 0.089 / max(abs(s) for s in samples)
    return rate, [round(s * gain * 32767) for s in samples]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[2]
                        / 'release/Client/Sound/22.wav')
    args = parser.parse_args()
    rate, samples = generate()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    # Refuse to overwrite an existing asset; choose another output for variants.
    with args.output.open('xb') as destination:
        with wave.open(destination, 'wb') as audio:
            audio.setnchannels(1)
            audio.setsampwidth(2)
            audio.setframerate(rate)
            audio.writeframes(struct.pack('<' + 'h' * len(samples), *samples))
    print(args.output)


if __name__ == '__main__':
    main()
