"""Package the unmodified Godot render into review animations."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = root / "output/forest_wolf_ingame_v2_20261104"
target = root / "docs/美术规范/局内凶狼骨骼重制_20261104"
target.mkdir(parents=True, exist_ok=True)
paths = sorted((source / "frames").glob("frame_*.png"))
assert len(paths) == 330, f"Expected a complete 11-second capture, found {len(paths)} frames"

for name, crop, size in [
    ("wolf_motion.gif", (24, 458, 708, 824), (760, 407)),
    ("wolf_rig_comparison.gif", None, (960, 600)),
]:
    frames = []
    # 15fps review keeps the original eleven-second timing.
    for path in paths[::2]:
        with Image.open(path) as source_frame:
            frame = source_frame.convert("RGB")
            if crop:
                frame = frame.crop(crop)
            frames.append(frame.resize(size, Image.Resampling.LANCZOS))
    samples = Image.new("RGB", (size[0], size[1] * 8))
    for row, index in enumerate([0, 25, 50, 70, 81, 95, 130, 160]):
        samples.paste(frames[index], (0, row * size[1]))
    palette = samples.quantize(colors=256)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    times = [round((i+1) * 100/15)*10 - round(i * 100/15)*10 for i in range(len(indexed))]
    indexed[0].save(target / name, save_all=True, append_images=indexed[1:],
                    duration=times, loop=0, optimize=True, disposal=1)
    with Image.open(target / name) as result:
        print(name, result.size, result.n_frames, sum(times), (target / name).stat().st_size)

for name in ["wolf_actual_arena.png", "pose_0000.png", "pose_0162.png", "pose_0276.png"]:
    (target / name).write_bytes((source / name).read_bytes())
