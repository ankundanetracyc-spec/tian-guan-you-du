"""Create visual QA sheets for the five fixed-canvas variants of every portrait part."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(r"E:\stacklands\mods\appearance\水墨半身男修基础包")
OUT = ROOT / "素材展示"
BOXES = {
    "clothes": (0, 500, 1024, 1536),
    "face": (320, 210, 705, 655),
    "hair_back": (330, 45, 700, 610),
    "hair_front": (350, 150, 675, 500),
    "eye": (365, 300, 660, 400),
    "nose": (450, 330, 575, 470),
    "mouth": (420, 410, 605, 490),
    "beard": (400, 390, 625, 570),
}


def build_sheet(part):
    box = BOXES[part]
    width = box[2] - box[0]
    height = box[3] - box[1]
    scale = min(220.0 / width, 260.0 / height)
    item_size = (max(1, int(width * scale)), max(1, int(height * scale)))
    sheet = Image.new("RGBA", (5 * 240, 310), (239, 232, 213, 255))
    draw = ImageDraw.Draw(sheet)
    for index in range(5):
        image = Image.open(ROOT / part / (str(index) + ".png")).convert("RGBA")
        crop = image.crop(box)
        crop.thumbnail(item_size, Image.Resampling.LANCZOS)
        x = index * 240 + (240 - crop.width) // 2
        y = 32 + (260 - crop.height) // 2
        sheet.alpha_composite(crop, (x, y))
        draw.text((index * 240 + 108, 8), str(index), fill=(64, 54, 43, 255))
    OUT.mkdir(parents=True, exist_ok=True)
    sheet.save(OUT / (part + "_0-4.png"))


def main():
    for part in BOXES:
        build_sheet(part)


if __name__ == "__main__":
    main()
