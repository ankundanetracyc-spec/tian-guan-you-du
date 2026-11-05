"""Encode/collate unaltered rendered battle frames for motion review."""
from pathlib import Path
from PIL import Image, ImageDraw
import sys

root = Path(__file__).resolve().parents[1]
actual = "--actual-queue" in sys.argv
source = root / ("tmp/queue_avatar_palm_actual_capture" if actual else "tmp/queue_avatar_palm_capture")
prefix = "queue_palm_actual_" if actual else "queue_palm_"
output = root / "docs/美术规范/行动队列局内形象_20261103"
files = sorted(source.glob("motion_*.png"))
assert len(files) == 180

def encode(filename, crop=None):
    frames = []
    palette = None
    for path in files:
        with Image.open(path) as image:
            frame = image.convert("RGB")
            frame = frame.crop(crop) if crop else frame.resize((960, 540), Image.Resampling.LANCZOS)
            if palette is None:
                palette = frame.quantize(colors=256)
            frames.append(frame.quantize(palette=palette, dither=Image.Dither.NONE))
    frames[0].save(output / filename, save_all=True, append_images=frames[1:],
                   duration=[30, 30, 40] * 60, loop=0, disposal=1, optimize=False)

encode(prefix + "6s.gif")
if actual:
    print("Encoded actual queue action 6s GIF")
    sys.exit(0)
encode(prefix + "native_crop_6s.gif", (265, 275, 560, 580))
indices = [0, 30, 34, 38, 41, 44, 50, 60]
sheet = Image.new("RGB", (4*295, 2*335), "#ddd9ca")
draw = ImageDraw.Draw(sheet)
for k, index in enumerate(indices):
    with Image.open(files[index]) as image:
        x, y = (k % 4)*295, (k // 4)*335
        sheet.paste(image.convert("RGB").crop((265, 275, 560, 580)), (x,y+30))
        draw.text((x+8,y+8), f"Frame {index} | action {(index-30)/30:.2f}s", fill="#182b2a")
sheet.save(output / "queue_palm_contact_sheet.png")
print("Encoded full/cropped 6s GIF and eight-phase contact sheet from actual battle frames")
