"""Package the native joint-review capture without changing its timing."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = root / "output/forest_wolf_joint_revision_20261104"
target = root / "docs/美术规范/局内凶狼骨骼重制_20261104"
frames = sorted((source / "frames").glob("frame_*.png"))
assert len(frames) == 120
for name, crop, size in [
    ("wolf_joint_detail.gif", (400, 595, 1030, 837), (1008, 387)),
    ("wolf_joint_review.gif", None, (1152, 720)),
]:
    images = []
    for path in frames[::2]:
        with Image.open(path) as rendered:
            frame = rendered.convert("RGB")
            if crop:
                frame = frame.crop(crop)
            images.append(frame.resize(size, Image.Resampling.LANCZOS))
    samples = Image.new("RGB", (size[0], size[1] * 8))
    for row, index in enumerate([0, 8, 15, 22, 30, 37, 45, 55]):
        samples.paste(images[index], (0, row * size[1]))
    palette = samples.quantize(colors=256)
    images = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in images]
    durations = [round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(len(images))]
    images[0].save(target / name, save_all=True, append_images=images[1:],
                   duration=durations, disposal=1, loop=0, optimize=True)
    with Image.open(target / name) as result:
        print(name, result.n_frames, sum(durations), (target / name).stat().st_size)
(target / "wolf_joint_standing.png").write_bytes((source / "pose_0089.png").read_bytes())
