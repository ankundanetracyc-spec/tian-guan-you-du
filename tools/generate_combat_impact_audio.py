"""Create original short combat foley; no recordings or third-party samples.

The independent fixed seeds are authoring inputs, not gameplay random numbers.
Outputs are ordinary 48 kHz mono PCM16 WAVs for a bounded Godot one-shot pool.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import wave

import numpy as np
from scipy import signal


ROOT = Path(__file__).resolve().parents[1]
RATE = 48_000


def envelope(t, attack, decay):
    return -np.expm1(-t / attack) * np.exp(-t / decay)


def band_noise(rng, length, lo, hi):
    source = rng.standard_normal(length)
    sos = signal.butter(2, (lo, hi), btype="bandpass", fs=RATE, output="sos")
    filtered = signal.sosfilt(sos, source)
    return filtered / max(float(np.sqrt(np.mean(filtered**2))), 1e-9)


def drop_mode(t, initial, final, speed, decay, phase=0.0):
    # The pitch falls within milliseconds; it is a damped impact, not a note.
    cycles = final * t + (initial - final) * speed * (-np.expm1(-t / speed))
    return np.sin(2 * np.pi * cycles + phase) * envelope(t, 0.0012, decay)


def impact(kind, variant):
    heavy = kind == "heavy"
    duration = ((0.183, 0.197) if heavy else (0.118, 0.126))[variant]
    t = np.arange(round(duration * RATE)) / RATE
    rng = np.random.default_rng(861_027 + variant * 53 + (401 if heavy else 0))
    pitch = (0.97, 1.035)[variant]
    low = band_noise(rng, len(t), 65 if heavy else 100, 1150 if heavy else 1550)
    skin = band_noise(rng, len(t), 900, 4400)
    air = band_noise(rng, len(t), 1400, 5900)
    if heavy:
        result = 0.66 * low * envelope(t, 0.0017, 0.038)
        result += 0.43 * drop_mode(t, 148*pitch, 53*pitch, 0.013, 0.043)
        result += 0.12 * drop_mode(t, 260*pitch, 101*pitch, 0.0045, 0.019)
        result += 0.15 * skin * envelope(t, 0.00065, 0.009)
        result += 0.055 * air * envelope(t, 0.003, 0.021)
    else:
        result = 0.59 * low * envelope(t, 0.00085, 0.020)
        result += 0.27 * drop_mode(t, 211*pitch, 86*pitch, 0.0068, 0.022)
        result += 0.18 * skin * envelope(t, 0.0005, 0.0078)
        result += 0.055 * air * envelope(t, 0.0025, 0.014)
    return finish(result, -8.0 if heavy else -9.0)


def guard(variant):
    duration = (0.105, 0.116)[variant]
    t = np.arange(round(duration * RATE)) / RATE
    rng = np.random.default_rng(954_107 + variant * 71)
    pitch = (1.0, 0.935)[variant]
    dry = band_noise(rng, len(t), 390, 5200)
    body = band_noise(rng, len(t), 180, 1550)
    result = 0.59 * dry * envelope(t, 0.0005, 0.010)
    result += 0.31 * body * envelope(t, 0.0010, 0.018)
    # Inharmonic, quickly damped contact modes add a dry clack, without a
    # sustained bell/metal note on the unarmed defensive action.
    for frequency, strength, decay in [(435, 0.10, 0.019), (738, 0.07, 0.013),
                                       (1231, 0.038, 0.009)]:
        result += strength * np.sin(2*np.pi*frequency*pitch*t) * envelope(t, 0.0007, decay)
    return finish(result, -10.0)


def whoosh(variant):
    duration = (0.126, 0.142)[variant]
    t = np.arange(round(duration * RATE)) / RATE
    rng = np.random.default_rng(447_011 + variant * 89)
    progress = t / duration
    upper = band_noise(rng, len(t), 1250, 6100)
    lower = band_noise(rng, len(t), 260, 1900)
    # A single airy envelope opens then drops in spectral weight; no tonal
    # oscillators, granular particles or additional impact sounds in a miss.
    sweep = (upper * (1.0 - 0.68*progress) + lower * (0.22 + 0.38*progress))
    result = sweep * np.sin(np.pi*progress)**1.8
    return finish(result, -18.0)


def finish(samples, peak_db):
    samples = signal.sosfilt(signal.butter(2, 37, btype="highpass", fs=RATE,
                                         output="sos"), samples)
    samples = np.tanh(samples * 1.1)
    fade = min(round(0.006 * RATE), len(samples) // 5)
    samples[-fade:] *= np.linspace(1.0, 0.0, fade)**2
    samples[:round(0.00025 * RATE)] *= np.linspace(0.0, 1.0, round(0.00025*RATE))
    samples *= (10**(peak_db / 20)) / max(float(np.max(np.abs(samples))), 1e-9)
    samples[0] = 0.0
    samples[-1] = 0.0
    return samples.astype(np.float64)


def write_wav(path, samples):
    encoded = np.rint(np.clip(samples, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(encoded.tobytes())


def metrics(samples):
    squared = samples**2
    frequencies = np.fft.rfftfreq(len(samples), 1/RATE)
    power = np.abs(np.fft.rfft(samples))**2
    tail = samples[-round(0.005*RATE):]
    peak = float(np.max(np.abs(samples)))
    return {"sample_rate": RATE, "channels": 1, "pcm_bits": 16,
            "frames": len(samples), "seconds": len(samples)/RATE,
            "peak_dbfs": round(20*np.log10(peak), 4),
            "rms_dbfs": round(10*np.log10(max(float(np.mean(squared)), 1e-20)), 4),
            "dc_offset": float(np.mean(samples)),
            "tail_5ms_rms_dbfs": round(20*np.log10(max(float(np.sqrt(np.mean(tail**2))), 1e-10)), 4),
            "spectral_centroid_hz": round(float(np.sum(frequencies*power)/np.sum(power)), 2),
            "sub_250hz_power_fraction": round(float(np.sum(power[frequencies<250])/np.sum(power)), 6),
            "clipped_samples": int(np.count_nonzero(np.abs(samples)>=1.0)),
            "first_sample": float(samples[0]), "last_sample": float(samples[-1])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, default=ROOT/"audio/combat_impact_20261105")
    parser.add_argument("--review-dir", type=Path, default=ROOT/"output/combat_impact_audio_20261105")
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    args.review_dir.mkdir(parents=True, exist_ok=True)
    manifest = {"schema": 1, "origin": "Original procedural foley. No recordings or external samples.",
                "generator": "tools/generate_combat_impact_audio.py", "sample_rate": RATE,
                "playback_notes": {"variants": "Deterministic round-robin or a private presentation RNG only.",
                    "mixing": "Coalesce same-frame body/blood contact; limit simultaneous near-field impacts to two voices.",
                    "pause": "Pause active voices with the battle; clear voices on reset/exit.",
                    "kind": "Use hit only after real hit; a fully blocked hit uses guard; a miss uses only whoosh."},
                "families": {}, "files": {}}
    sequence = []
    labels = []
    cursor = 0
    for kind, generate in [("light", lambda v: impact("light", v)),
                           ("heavy", lambda v: impact("heavy", v)),
                           ("guard", guard), ("whoosh", whoosh)]:
        names = []
        for variant in range(2):
            name = f"{kind}_{variant+1:02d}.wav"
            samples = generate(variant)
            write_wav(args.output_dir/name, samples)
            manifest["files"][name] = metrics(samples)
            names.append(name)
            silence = np.zeros(round(0.45*RATE))
            labels.append({"file": name, "starts_at": cursor/RATE,
                           "seconds": len(samples)/RATE})
            sequence.extend((samples, silence))
            cursor += len(samples) + len(silence)
        manifest["families"][kind] = names
    (args.output_dir/"manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    write_wav(args.review_dir/"audition_all.wav", np.concatenate(sequence))
    (args.review_dir/"audition_labels.json").write_text(json.dumps(labels, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"files": len(manifest["files"]), "output_dir": str(args.output_dir),
                      "audition": str(args.review_dir/"audition_all.wav"),
                      "seconds": cursor/RATE}, ensure_ascii=False))


if __name__ == "__main__":
    main()
