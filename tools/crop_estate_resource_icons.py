from pathlib import Path
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "textures/ui/estate_resources/estate_resource_sheet_v1.png"
OUTPUT = ROOT / "textures/ui/estate_resources"
NAMES = ["food", "stone", "herbs", "ore", "spirit_stones"]
CENTERS = [(256, 256), (1280, 256), (256, 768), (768, 768), (1280, 768)]
SIZE = 470


def main() -> None:
    sheet = Image.open(SOURCE).convert("RGBA")
    half = SIZE // 2
    for name, (x, y) in zip(NAMES, CENTERS):
        icon = sheet.crop((x - half, y - half, x - half + SIZE, y - half + SIZE))
        mask = Image.new("L", (SIZE, SIZE), 0)
        ImageDraw.Draw(mask).ellipse((4, 4, SIZE - 4, SIZE - 4), fill=255)
        icon.putalpha(mask)
        icon.save(OUTPUT / f"estate_{name}_v1.png")


if __name__ == "__main__":
    main()
