"""Validate fixed-canvas half-body portrait layers before they enter the MOD manifest."""
from pathlib import Path
import sys

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(r"E:\stacklands\mods\appearance\水墨半身男修基础包")
SIZE = (1024, 1536)
ORDER = ["hair_back", "face", "clothes", "eye", "nose", "mouth", "beard", "hair_front"]
ANCHORS = {
    "center_axis_x": 512,
    "hairline": (512, 270),
    "eye_line": (512, 350),
    "nose_tip": (512, 420),
    "mouth_center": (512, 452),
    "chin": (512, 525),
    "collar_center": (512, 600),
}
EXPECTED_BOXES = {
    "clothes": (0, 500, 1024, 1536),
    "face": (320, 35, 705, 650),
    "hair_back": (330, 50, 700, 610),
    "hair_front": (350, 35, 675, 490),
    "eye": (365, 300, 660, 400),
    "nose": (450, 330, 575, 470),
    "mouth": (420, 410, 605, 490),
    "beard": (400, 390, 625, 570),
}


def bbox_is_reasonable(part, bbox, index):
    if bbox is None:
        # Neutral base slots intentionally carry no overlay artwork.
        return part in ("beard", "eye", "nose", "mouth", "hair_back", "hair_front")
    expected = EXPECTED_BOXES[part]
    left, top, right, bottom = bbox
    return left >= expected[0] - 40 and top >= expected[1] - 40 and right <= expected[2] + 40 and bottom <= expected[3] + 40


def visible_bbox(image):
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 24 else 0)
    return alpha.getbbox()


def compose(hair_index):
    result = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    for part in ORDER:
        result.alpha_composite(Image.open(ROOT / part / (str(hair_index) + ".png")).convert("RGBA"))
    return result


def build_guide(image, hair_index):
    guide = image.copy()
    draw = ImageDraw.Draw(guide)
    draw.line([(512, 0), (512, 1536)], fill=(203, 120, 48, 210), width=2)
    for name, point in ANCHORS.items():
        if name == "center_axis_x":
            continue
        x, y = point
        draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(203, 120, 48, 230))
    guide.save(ROOT / ("锚点检验_" + str(hair_index) + ".png"))


def main():
    hair_index = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    failures = []
    for part in ORDER:
        path = ROOT / part / (str(hair_index) + ".png")
        if not path.exists():
            failures.append(part + ": missing")
            continue
        image = Image.open(path).convert("RGBA")
        if image.size != SIZE:
            failures.append(part + ": invalid canvas " + str(image.size))
            continue
        bbox = visible_bbox(image)
        if not bbox_is_reasonable(part, bbox, hair_index):
            failures.append(part + ": invalid paint box " + str(bbox))
    result = compose(hair_index)
    result.save(ROOT / ("组合预览_" + str(hair_index) + ".png"))
    build_guide(result, hair_index)
    if failures:
        raise SystemExit("\n".join(failures))
    print("PASS half_body_v1 hair", hair_index)


if __name__ == "__main__":
    main()
