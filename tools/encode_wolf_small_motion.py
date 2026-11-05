"""Encode genuine Godot motion captures while preserving their timing."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = root / "output/forest_wolf_small_bones_20261104"
target = root / "docs/美术规范/局内凶狼骨骼重制_20261104"
target.mkdir(parents=True, exist_ok=True)
paths = sorted((source / "frames").glob("frame_*.png"))
assert len(paths) == 120, f"Expected 120 rendered frames, got {len(paths)}"
for name, crop, size in [
    ("wolf_v3_actual_motion.gif", (24, 90, 1416, 520), (1114, 344)),
    ("wolf_v3_motion_review.gif", None, (960, 600)),
]:
    frames = []
    for path in paths[::2]:
        with Image.open(path) as image:
            frame = image.convert("RGB")
            if crop:
                frame = frame.crop(crop)
            frames.append(frame.resize(size, Image.Resampling.LANCZOS))
    samples = Image.new("RGB", (size[0], size[1] * 6))
    for row, index in enumerate([0, 12, 24, 36, 48, 59]):
        samples.paste(frames[index], (0, row * size[1]))
    palette = samples.quantize(colors=256)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(len(frames))]
    indexed[0].save(target / name, save_all=True, append_images=indexed[1:],
                    duration=durations, loop=0, disposal=1, optimize=True)
    with Image.open(target / name) as result:
        print(name, result.size, result.n_frames, sum(durations), (target / name).stat().st_size)

for name in ["preview.png", "sizing_hero.png", "sizing_wolf.png"]:
    (target / f"v3_{name}").write_bytes((source / name).read_bytes())
