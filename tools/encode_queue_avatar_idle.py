"""Encode captured Godot frames; this does not alter character source artwork."""
from pathlib import Path
from PIL import Image
import sys

root = Path(__file__).resolve().parents[1]
v3 = "--v3" in sys.argv
suffix = "v3_" if v3 else ""
source = root / ("tmp/queue_avatar_idle_capture_v3" if v3 else "tmp/queue_avatar_idle_capture")
output = root / "docs/美术规范/行动队列局内形象_20261103"
files = sorted(source.glob("motion_*.png"))
assert len(files) == 180, f"Expected 180 rendered frames, got {len(files)}"
frames = []
palette = None
for path in files:
    with Image.open(path) as image:
        frame = image.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS)
        if palette is None:
            palette = frame.quantize(colors=256)
        frames.append(frame.quantize(palette=palette, dither=Image.Dither.NONE))
frames[0].save(output / f"queue_idle_{suffix}6s.gif", save_all=True,
               append_images=frames[1:], duration=[30, 30, 40] * 60,
               loop=0, disposal=1, optimize=False)
print(f"Encoded {len(frames)} actual runtime frames at 30 fps, variant: {suffix or 'v2'}")
close_frames = []
close_palette = None
for path in files:
    with Image.open(path) as image:
        # Native pixels from the actual left combatant, no pose synthesis or upscaling.
        frame = image.convert("RGB").crop((265, 275, 450, 580))
        if close_palette is None:
            close_palette = frame.quantize(colors=256)
        close_frames.append(frame.quantize(palette=close_palette, dither=Image.Dither.NONE))
close_frames[0].save(output / f"queue_idle_{suffix}native_crop_6s.gif", save_all=True,
                     append_images=close_frames[1:], duration=[30, 30, 40] * 60,
                     loop=0, disposal=1, optimize=False)
print("Encoded native pixel combatant crop, no upscaling")
