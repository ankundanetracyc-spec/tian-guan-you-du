"""Compose the fixed-canvas half-body package for alignment QA."""
from pathlib import Path
import sys
from PIL import Image

ROOT = Path(r"E:\stacklands\mods\appearance\水墨半身男修基础包")
ORDER = ["hair_back", "face", "clothes", "eye", "nose", "mouth", "beard", "hair_front"]


def main():
    variant = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    preview = Image.new("RGBA", (1024, 1536), (0, 0, 0, 0))
    for part in ORDER:
        index = variant if part in ("hair_back", "hair_front") else 0
        preview.alpha_composite(Image.open(ROOT / part / (str(index) + ".png")).convert("RGBA"))
    preview.save(ROOT / ("组合预览_" + str(variant) + ".png"))


if __name__ == "__main__":
    main()
