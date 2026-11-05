"""Encode the actual four-human simulation frames at their simulated 24 fps."""
from pathlib import Path
import json
from PIL import Image

root = Path(__file__).resolve().parents[1]
folder = root / "output/human_sparring_20261104"
manifest = json.loads((folder / "capture_manifest.json").read_text(encoding="utf-8"))
sources = sorted((folder / manifest["frames"]).glob("frame_*.png"))
assert len(sources) == 240
frames = []
for source in sources:
    with Image.open(source) as frame:
        frames.append(frame.convert("RGB").resize((768, 432), Image.Resampling.LANCZOS))
samples = Image.new("RGB", (768, 432 * 5))
for row, index in enumerate([0, 32, 72, 120, 180]):
    samples.paste(frames[index], (0, row * 432))
palette = samples.quantize(colors=192)
indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
durations = [round((i + 1) * 100 / 24) * 10 - round(i * 100 / 24) * 10 for i in range(240)]
target = folder / "preview.gif"
indexed[0].save(target, save_all=True, append_images=indexed[1:], duration=durations,
                loop=0, optimize=True, disposal=1)
with Image.open(target) as result:
    assert sum(durations) == 10000
    print(f"HUMAN_PREVIEW_OK: {result.size}, {result.n_frames} frames, 10s, {target.stat().st_size} bytes")
