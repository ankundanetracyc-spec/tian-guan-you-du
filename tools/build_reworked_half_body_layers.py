"""Build the first fixed-canvas layer set from the approved rework master.

This is an ingestion script, not a runtime compositor: every output keeps the
same 1024 x 1536 canvas and the same top-left origin.
"""
from collections import deque
from pathlib import Path
from typing import List, Set, Tuple

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(r"E:\stacklands")
SOURCE = ROOT / "mods" / "appearance" / "水墨半身男修重制包" / "母版" / "男修_半身水墨母版_v1.png"
PACKAGE = ROOT / "mods" / "appearance" / "水墨半身男修重制包"
SIZE = (1024, 1536)
PARTS = ("hair_back", "face", "clothes", "eye", "nose", "mouth", "beard", "hair_front")


def remove_white_background(source: Image.Image) -> Image.Image:
    """Key only edge-connected near-white pixels; preserve pale robe paint."""
    image = source.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    queue = deque()
    visited: Set[Tuple[int, int]] = set()

    def is_background(x: int, y: int) -> bool:
        red, green, blue, alpha = pixels[x, y]
        return alpha > 0 and min(red, green, blue) >= 242 and max(red, green, blue) - min(red, green, blue) <= 14

    for x in range(width):
        queue.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        queue.extend(((0, y), (width - 1, y)))
    while queue:
        x, y = queue.popleft()
        if (x, y) in visited or not is_background(x, y):
            continue
        visited.add((x, y))
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height:
                queue.append((nx, ny))
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    for x, y in visited:
        alpha_pixels[x, y] = 0
    image.putalpha(alpha)
    return image


def normalize_master(source: Image.Image) -> Image.Image:
    alpha = source.getchannel("A").point(lambda value: 255 if value >= 24 else 0)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("The master has no visible pixels after background removal.")
    crop = source.crop(bounds)
    scale = min(824 / crop.width, 1500 / crop.height)
    scaled = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    result.alpha_composite(scaled, ((SIZE[0] - scaled.width) // 2, 18))
    return result


def polygon_mask(points: List[Tuple[int, int]], blur: float = 0.0) -> Image.Image:
    mask = Image.new("L", SIZE, 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(blur)) if blur else mask


def dark_hair_mask(master: Image.Image, region: Image.Image) -> Image.Image:
    output = Image.new("L", SIZE, 0)
    source_pixels = master.load()
    region_pixels = region.load()
    output_pixels = output.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = source_pixels[x, y]
            if region_pixels[x, y] and alpha > 20 and red + green + blue < 400:
                output_pixels[x, y] = region_pixels[x, y]
    # Keep hair alpha hard here. Soft alpha would reintroduce the white matte
    # from the generated source into gaps between individual hair strands.
    return output


def facial_ink_mask(master: Image.Image, region: Image.Image, limit: int) -> Image.Image:
    """Keep only painted facial marks; never export rectangular skin patches."""
    output = Image.new("L", SIZE, 0)
    source_pixels = master.load()
    region_pixels = region.load()
    output_pixels = output.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = source_pixels[x, y]
            if region_pixels[x, y] and alpha > 20 and red + green + blue < limit:
                output_pixels[x, y] = region_pixels[x, y]
    return output


def facial_ellipse_mask(master: Image.Image, ellipses: List[Tuple[int, int, int, int]], limit: int) -> Image.Image:
    """Keep facial ink inside named feature anchors, excluding all surrounding skin."""
    region = Image.new("L", SIZE, 0)
    draw = ImageDraw.Draw(region)
    for center_x, center_y, radius_x, radius_y in ellipses:
        draw.ellipse((center_x - radius_x, center_y - radius_y, center_x + radius_x, center_y + radius_y), fill=255)
    return facial_ink_mask(master, region, limit)


def mouth_mark_mask(master: Image.Image) -> Image.Image:
    """Export lips only: coloured lip paint and the lip contour, never skin."""
    output = Image.new("L", SIZE, 0)
    source_pixels = master.load()
    output_pixels = output.load()
    center_x, center_y = 505, 447
    radius_x, radius_y = 61, 20
    for y in range(center_y - radius_y, center_y + radius_y + 1):
        for x in range(center_x - radius_x, center_x + radius_x + 1):
            # A fixed oval is deliberately narrower than the surrounding face.
            # It makes a skin-colour patch structurally impossible to export.
            if ((x - center_x) / radius_x) ** 2 + ((y - center_y) / radius_y) ** 2 > 1.0:
                continue
            red, green, blue, alpha = source_pixels[x, y]
            is_lip_paint = red - green >= 32 and red + green + blue < 540
            is_lip_contour = red + green + blue < 365
            if alpha > 20 and (is_lip_paint or is_lip_contour):
                output_pixels[x, y] = 255
    return output


def export(master: Image.Image, mask: Image.Image, part: str) -> None:
    result = master.copy()
    result.putalpha(ImageChops.multiply(result.getchannel("A"), mask))
    target = PACKAGE / part / "0.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    result.save(target)


def build() -> None:
    for part in PARTS:
        (PACKAGE / part).mkdir(parents=True, exist_ok=True)
    master = normalize_master(remove_white_background(Image.open(SOURCE)))
    master.save(PACKAGE / "母版" / "男修_半身水墨母版_v1_标准画布.png")

    # Body pose and robe are deliberately one layer. Its collar covers the
    # lower end of the extended neck in the face layer.
    clothes = polygon_mask([(118, 535), (350, 475), (430, 510), (470, 610), (554, 610), (594, 510), (674, 475), (906, 535), (954, 1518), (70, 1518)])
    export(master, clothes, "clothes")

    # Head owns skin, ears and a long vertical neck. Facial feature windows are
    # removed so the matching eye/nose/mouth layers can replace them cleanly.
    face = polygon_mask([(420, 184), (604, 184), (645, 228), (672, 304), (676, 395), (640, 471), (590, 520), (570, 650), (454, 650), (434, 520), (384, 471), (348, 395), (352, 304), (379, 228)])
    face_draw = ImageDraw.Draw(face)
    # Two separate eye windows are used so neither side can accidentally pull
    # in temple hair.  Their coordinates are rig anchors, not crop bounds.
    left_eye_window = [(426, 322), (488, 322), (488, 370), (426, 370)]
    right_eye_window = [(510, 322), (582, 322), (582, 370), (510, 370)]
    nose_window = [(445, 365), (575, 365), (575, 425), (445, 425)]
    mouth_window = [(444, 427), (566, 427), (566, 467), (444, 467)]
    face_draw.polygon(left_eye_window, fill=0)
    face_draw.polygon(right_eye_window, fill=0)
    face_draw.polygon(nose_window, fill=0)
    face_draw.polygon(mouth_window, fill=0)
    # The head silhouette is intentionally broad enough for ears and neck, but
    # dark strands in that region still belong to the two hair layers.
    face_pixels = face.load()
    master_pixels = master.load()
    for y in range(184, 522):
        for x in range(344, 680):
            red, green, blue, _ = master_pixels[x, y]
            if red + green + blue < 330:
                face_pixels[x, y] = 0
    export(master, face, "face")

    feature_masks = {
        "eye": facial_ellipse_mask(master, [(457, 347, 34, 20), (548, 347, 34, 20)], 555),
        "nose": facial_ellipse_mask(master, [(510, 399, 57, 28)], 485),
        "mouth": mouth_mark_mask(master),
    }
    for part, mask in feature_masks.items():
        export(master, mask, part)

    rear_region = polygon_mask([(366, 70), (660, 70), (724, 240), (707, 580), (650, 690), (600, 590), (424, 590), (374, 690), (317, 580), (300, 240)])
    rear = dark_hair_mask(master, rear_region)
    rear_draw = ImageDraw.Draw(rear)
    rear_draw.polygon([(407, 220), (617, 220), (622, 450), (402, 450)], fill=0)
    export(master, rear, "hair_back")

    front_region = polygon_mask([(370, 72), (654, 72), (675, 296), (626, 344), (594, 285), (512, 266), (430, 285), (398, 344), (349, 296)])
    front = dark_hair_mask(master, front_region)
    # Brows belong to the eye layer, never to front hair. Leave only fringe and
    # temple locks in the central face field.
    front_draw = ImageDraw.Draw(front)
    front_draw.rectangle((402, 258, 622, 338), fill=0)
    export(master, front, "hair_front")

    Image.new("RGBA", SIZE, (0, 0, 0, 0)).save(PACKAGE / "beard" / "0.png")


if __name__ == "__main__":
    build()
