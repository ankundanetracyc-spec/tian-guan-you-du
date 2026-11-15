"""Validate the authored combat WAVs and their practical mix headroom."""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DIRECTORY = ROOT/"audio/combat_impact_20261105"
manifest = json.loads((DIRECTORY/"manifest.json").read_text(encoding="utf-8"))
checks = 0
failures = []


def check(condition, label):
    global checks
    checks += 1
    if not condition:
        failures.append(label)


decoded = {}
for name, info in manifest["files"].items():
    with wave.open(str(DIRECTORY/name), "rb") as sound:
        channels, bits, rate = sound.getnchannels(), sound.getsampwidth()*8, sound.getframerate()
        samples = np.frombuffer(sound.readframes(sound.getnframes()), "<i2").astype(float)/32768
    decoded[name] = samples
    check((channels, bits, rate)==(1,16,48000), name+": PCM16 mono 48kHz")
    check(0.08<len(samples)/rate<0.22, name+": compact one shot")
    check(np.max(np.abs(samples))<=10**(-7.9/20), name+": headroom")
    check(samples[0]==0 and samples[-1]==0, name+": no hard edge click")
    check(np.count_nonzero(np.abs(samples)>=0.999)==0, name+": no clipped samples")
    check(abs(float(np.mean(samples)))<0.0015, name+": negligible DC")
    check(float(np.sqrt(np.mean(samples[-240:]**2)))<0.005,
          name+": quiet last five milliseconds")
    check(abs(len(samples)/rate-info["seconds"])<1/rate, name+": manifest timing")
for family, names in manifest["families"].items():
    first, second = (decoded[name] for name in names)
    check(not np.array_equal(first,second), family+": distinct variants")
    check(float(np.max(np.abs(first)) + np.max(np.abs(second)))<1.0,
          family+": bounded two-voice peak cannot clip")
check(max(manifest["files"][name]["rms_dbfs"] for name in manifest["families"]["heavy"]) >
      max(manifest["files"][name]["rms_dbfs"] for name in manifest["families"]["whoosh"]),
      "impact carries more body than swing")
check(min(manifest["files"][name]["rms_dbfs"] for name in manifest["families"]["light"]) >
      max(manifest["files"][name]["rms_dbfs"] for name in manifest["families"]["whoosh"])+2.0,
      "miss air stays quieter than the light contact it accompanies")
result={"checks":checks,"failures":failures,"files":len(decoded)}
print(json.dumps(result,ensure_ascii=False))
raise SystemExit(1 if failures else 0)
