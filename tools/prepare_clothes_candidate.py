"""Normalize the body-pose-and-outfit candidate into the shared portrait rig."""
from pathlib import Path
import sys

from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).parent))
from prepare_head_base_candidate import remove_dark_edge_background


ROOT = Path(r"E:\stacklands")
SOURCE = ROOT / "mods" / "appearance" / "水墨半身男修重制包" / "候选部件" / "姿势服饰_0_原图.png"
TARGET = ROOT / "mods" / "appearance" / "水墨半身男修重制包" / "候选部件" / "姿势服饰_0_标准画布.png"
CANVAS = (1024, 1536)
TARGET_BOX = (100, 470, 924, 1536)


def normalize(source: Image.Image) -> Image.Image:
    alpha = source.getchannel("A").point(lambda value: 255 if value >= 32 else 0)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("clothes candidate lost all visible pixels")
    crop = source.crop(bounds)
    width = TARGET_BOX[2] - TARGET_BOX[0]
    height = TARGET_BOX[3] - TARGET_BOX[1]
    scale = min(width / crop.width, height / crop.height)
    resized = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    result.alpha_composite(resized, ((TARGET_BOX[0] + TARGET_BOX[2] - resized.width) // 2, TARGET_BOX[1]))
    result.putalpha(result.getchannel("A").filter(ImageFilter.MinFilter(3)))
    return result


if __name__ == "__main__":
    prepared = normalize(remove_dark_edge_background(Image.open(SOURCE)))
    prepared.save(TARGET)
    print(prepared.getchannel("A").getbbox())
