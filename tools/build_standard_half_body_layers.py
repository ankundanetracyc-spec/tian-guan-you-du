"""Build the standard fixed-canvas portrait layers from one approved master.

Every output stays on the original 1024 x 1536 canvas.  This means a feature
is never cropped or re-centred: its location is the shared rig anchor.
"""
from collections import deque
from pathlib import Path
from typing import List, Tuple

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(r"E:\stacklands")
PACKAGE = ROOT / "mods" / "appearance" / "水墨半身男修标准包"
SOURCE = PACKAGE / "母版" / "男修_半身标准母版_0.png"
SIZE = (1024, 1536)
ORDER = ("hair_back", "face", "clothes", "eye", "nose", "mouth", "beard", "hair_front")


def clear_edge_backdrop(source: Image.Image) -> Image.Image:
    """Remove only dark neutral pixels connected to the canvas edge.

    The generated master has a dark vignette.  Flood filling from the edge
    preserves enclosed black hair while making the final PNG truly transparent.
    """
    result = source.convert("RGBA")
    pixels = result.load()
    width, height = result.size
    queue = deque()
    visited = set()

    def is_backdrop(x: int, y: int) -> bool:
        red, green, blue, alpha = pixels[x, y]
        return alpha > 0 and max(red, green, blue) - min(red, green, blue) < 28 and max(red, green, blue) < 145

    for x in range(width):
        queue.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        queue.extend(((0, y), (width - 1, y)))
    while queue:
        x, y = queue.popleft()
        if (x, y) in visited or not is_backdrop(x, y):
            continue
        visited.add((x, y))
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height:
                queue.append((nx, ny))
    alpha = result.getchannel("A")
    alpha_pixels = alpha.load()
    for x, y in visited:
        alpha_pixels[x, y] = 0
    result.putalpha(alpha)
    return result


def polygon_mask(points: List[Tuple[int, int]]) -> Image.Image:
    mask = Image.new("L", SIZE, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def ellipse_mask(ellipses: List[Tuple[int, int, int, int]]) -> Image.Image:
    mask = Image.new("L", SIZE, 0)
    draw = ImageDraw.Draw(mask)
    for center_x, center_y, radius_x, radius_y in ellipses:
        draw.ellipse((center_x - radius_x, center_y - radius_y, center_x + radius_x, center_y + radius_y), fill=255)
    return mask


def painted_mark_mask(source: Image.Image, region: Image.Image, brightness_limit: int) -> Image.Image:
    """Keep ink and local painted feature colour; skin can never enter a part."""
    result = Image.new("L", SIZE, 0)
    source_pixels = source.load()
    region_pixels = region.load()
    result_pixels = result.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = source_pixels[x, y]
            if region_pixels[x, y] and alpha > 20 and red + green + blue < brightness_limit:
                result_pixels[x, y] = 255
    return result


def hair_mask(source: Image.Image, region: Image.Image) -> Image.Image:
    result = Image.new("L", SIZE, 0)
    source_pixels = source.load()
    region_pixels = region.load()
    result_pixels = result.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = source_pixels[x, y]
            if region_pixels[x, y] and alpha > 20 and red + green + blue < 410:
                result_pixels[x, y] = 255
    return result


def export(source: Image.Image, mask: Image.Image, part: str) -> None:
    target = PACKAGE / part / "0.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    output = source.copy()
    output.putalpha(ImageChops.multiply(output.getchannel("A"), mask))
    output.save(target)


def build() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    if source.size != SIZE:
        raise ValueError(f"Expected {SIZE}, got {source.size}")
    # Built-in generation already supplies a real alpha channel.  Do not key
    # the visually black preview background again: that would erase black hair.
    source.save(PACKAGE / "母版" / "男修_半身标准母版_0_透明.png")

    # Bottom hair: only visible around the head and below the jaw.
    back_region = polygon_mask([(338, 76), (690, 76), (711, 614), (648, 690), (598, 620), (426, 620), (376, 690), (315, 614)])
    back = hair_mask(source, back_region)
    back_draw = ImageDraw.Draw(back)
    back_draw.polygon([(394, 255), (630, 255), (650, 565), (374, 565)], fill=0)
    export(source, back, "hair_back")

    # Head owns skin, ears and a deliberately long neck; the robe covers it.
    face = polygon_mask([(419, 244), (606, 244), (644, 286), (654, 370), (640, 452), (606, 520), (582, 658), (442, 658), (418, 520), (384, 452), (370, 370), (380, 286)])
    face_draw = ImageDraw.Draw(face)
    face_draw.ellipse((414, 318, 491, 365), fill=0)
    face_draw.ellipse((524, 318, 601, 365), fill=0)
    face_draw.ellipse((456, 374, 568, 428), fill=0)
    face_draw.ellipse((450, 429, 564, 470), fill=0)
    # Side locks and fringe never belong to the face.  Brows stay in the head
    # layer because this compact rig deliberately has no separate brow slot.
    face_pixels = face.load()
    source_pixels = source.load()
    for y in range(244, 521):
        for x in range(370, 655):
            red, green, blue, alpha = source_pixels[x, y]
            is_hair_zone = y < 310 or x < 425 or x > 595
            if face_pixels[x, y] and is_hair_zone and alpha > 20 and red + green + blue < 430:
                face_pixels[x, y] = 0
    export(source, face, "face")

    # Body pose and robe are one component, above the long hidden neck.
    clothes = polygon_mask([(362, 570), (278, 597), (132, 690), (40, 854), (0, 1050), (0, 1536), (1024, 1536), (1024, 1050), (984, 854), (892, 690), (746, 597), (662, 570), (638, 664), (386, 664)])
    export(source, clothes, "clothes")

    export(source, painted_mark_mask(source, ellipse_mask([(452, 344, 38, 20), (561, 344, 38, 20)]), 540), "eye")
    export(source, painted_mark_mask(source, ellipse_mask([(510, 402, 54, 31)]), 500), "nose")

    # Mouth has its own strict feature-only colour rule: no skin-tone pixels.
    mouth_region = ellipse_mask([(507, 448, 59, 19)])
    mouth = Image.new("L", SIZE, 0)
    source_pixels = source.load()
    region_pixels = mouth_region.load()
    mouth_pixels = mouth.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = source_pixels[x, y]
            lip_colour = red - green >= 28 and red + green + blue < 550
            lip_ink = red + green + blue < 365
            if region_pixels[x, y] and alpha > 20 and (lip_colour or lip_ink):
                mouth_pixels[x, y] = 255
    export(source, mouth, "mouth")

    Image.new("RGBA", SIZE, (0, 0, 0, 0)).save(PACKAGE / "beard" / "0.png")

    front_region = polygon_mask([(378, 80), (649, 80), (676, 202), (646, 329), (598, 342), (574, 292), (510, 274), (445, 292), (422, 342), (370, 329), (349, 202)])
    front = hair_mask(source, front_region)
    export(source, front, "hair_front")


if __name__ == "__main__":
    build()
