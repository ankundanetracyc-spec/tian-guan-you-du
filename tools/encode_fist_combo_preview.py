"""Encode the original Godot captures without changing animation timing."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
folder = root / "output/fist_combo_20261104"
sources = sorted((folder / "right").glob("frame_*.png"))
assert len(sources) == 84

for name, crop, size in [
    ("combo_closeup.gif", (460, 260, 820, 500), (720, 480)),
    ("combo_full.gif", None, (960, 540)),
]:
    frames = []
    for source in sources:
        with Image.open(source) as frame:
            if crop:
                frame = frame.crop(crop)
            frames.append(frame.convert("RGB").resize(size, Image.Resampling.LANCZOS))
    samples = Image.new("RGB", (size[0], size[1] * 5))
    for row, index in enumerate([0, 6, 17, 30, 45]):
        samples.paste(frames[index], (0, row * size[1]))
    palette = samples.quantize(colors=256)
    images = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
    times = [round((i + 1) * 100 / 24) * 10 - round(i * 100 / 24) * 10 for i in range(84)]
    target = folder / name
    images[0].save(target, save_all=True, append_images=images[1:], duration=times,
                   loop=0, optimize=True, disposal=1)
    with Image.open(target) as result:
        assert result.n_frames == 84
        print(f"{name}: {result.size}, {result.n_frames} frames, {sum(times)}ms, {target.stat().st_size} bytes")
