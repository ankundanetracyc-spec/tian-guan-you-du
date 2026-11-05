"""Collate unaltered battle captures into a full preview and a native-size detail grid."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import sys

root = Path(__file__).resolve().parents[1]
source = root / "output/quiet_vfx_20261104"
output = root / "docs/美术规范/即时战斗四技能特效_20261104"
output.mkdir(parents=True, exist_ok=True)
files = sorted(source.glob("motion_*.png"))
assert len(files) == 316, f"Expected 316 rendered frames, got {len(files)}"
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)


def encode(frames, destination):
    palette = frames[0].quantize(colors=256)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    indexed[0].save(destination, save_all=True, append_images=indexed[1:],
                    duration=[30, 30, 40] * (len(indexed) // 3) + [30] * (len(indexed) % 3),
                    loop=0, disposal=1, optimize=True)


crop = (420, 300, 850, 480)
labels = ["拳掌 · 随机拳劲／掌劲", "痛击 · 聚劲与接触", "催血 · 血气上涌", "聚炁 · 炁息内收"]
static = Image.new("RGB", (860, 428), "#ede8d9")
draw = ImageDraw.Draw(static)
for panel, case in enumerate([0, 2, 3, 4]):
    x, y = (panel % 2) * 430, (panel // 2) * 214
    with Image.open(source / f"case_{case}.png") as image:
        static.paste(image.convert("RGB").crop(crop), (x, y + 34))
    draw.text((x + 12, y + 6), labels[panel], fill="#314540", font=font)
static.save(output / "four_skills.png")
if "--stills" in sys.argv:
    sys.exit(0)
detail = []
for step in range(60):
    sheet = Image.new("RGB", (860, 428), "#ede8d9")
    draw = ImageDraw.Draw(sheet)
    indices = [step, 60 + step % 30, 90 + min(step, 48), 139 + min(step, 48)]
    for panel, index in enumerate(indices):
        x, y = (panel % 2) * 430, (panel // 2) * 214
        with Image.open(files[index]) as image:
            sheet.paste(image.convert("RGB").crop(crop), (x, y + 34))
        draw.text((x + 12, y + 6), labels[panel], fill="#314540", font=font)
    detail.append(sheet)
encode(detail, output / "preview_detail.gif")

full = []
for path in files:
    with Image.open(path) as image:
        full.append(image.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS))
encode(full, output / "preview_full.gif")
print("Encoded actual frames: 316 full-scene, 60 detail-grid; no effect artwork altered")
