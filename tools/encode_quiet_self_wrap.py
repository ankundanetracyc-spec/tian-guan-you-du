"""Present native-scale crops of the actual blood-wrap and milky hand-qi captures."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
source = root / "output/quiet_self_refine_v3_20261104"
output = root / "docs/美术规范/即时战斗四技能特效_20261104"
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)
crop = (460, 275, 650, 480)
frames = []
for frame in range(60):
    sheet = Image.new("RGB", (380, 239), "#ede8d9")
    draw = ImageDraw.Draw(sheet)
    for col, (key, title) in enumerate([("blood", "催血 · 环绕上升"), ("qi", "聚炁 · 掌间乳白气息")]):
        with Image.open(source / f"{key}_{frame:02}.png") as image:
            sheet.paste(image.convert("RGB").crop(crop), (col * 190, 34))
        draw.text((col * 190 + 6, 7), title, fill="#314540", font=font)
    frames.append(sheet)
palette = frames[21].quantize(colors=256)
indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
indexed[0].save(output / "self_refined_v3.gif", save_all=True, append_images=indexed[1:],
                duration=[30, 30, 40] * 20, loop=0, disposal=1, optimize=True)
frames[21].save(output / "self_refined_v3.png")
print("Encoded 60 native-scale paired frames, with no alteration of effect artwork")
