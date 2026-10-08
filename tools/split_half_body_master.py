"""Export fixed-canvas starter portrait layers from an approved half-body master.

This is intentionally a coordinate-preserving extraction tool. It never crops,
resizes, or recenters source art. Later hand-painted variants use the same canvas.
"""
from pathlib import Path
import sys
from collections import deque

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(r"E:\stacklands")
MASTER = ROOT / "mods" / "appearance" / "水墨头像标准包" / "母版" / "男修_半身标准母版_v3.png"
OUT = ROOT / "mods" / "appearance" / "水墨半身男修基础包"
SIZE = (1024, 1536)


def alpha_mask(polygons, blur=0):
    mask = Image.new("L", SIZE, 0)
    draw = ImageDraw.Draw(mask)
    for polygon in polygons:
        draw.polygon(polygon, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(blur)) if blur else mask


def dark_hair_mask(source, regions):
    region_mask = alpha_mask(regions, 2)
    result = Image.new("L", SIZE, 0)
    src = source.convert("RGBA")
    src_pixels = src.load()
    region_pixels = region_mask.load()
    out_pixels = result.load()
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            red, green, blue, alpha = src_pixels[x, y]
            # Masters can arrive with a dark backdrop.  Pure backdrop black is not hair.
            # Keeping a small lower brightness bound avoids rectangular black leaks.
            if region_pixels[x, y] and alpha > 20 and 90 <= red + green + blue < 410:
                out_pixels[x, y] = region_pixels[x, y]
    return result.filter(ImageFilter.GaussianBlur(0.6))


def export(source, mask, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    result = source.copy().convert("RGBA")
    result.putalpha(Image.composite(result.getchannel("A"), Image.new("L", SIZE, 0), mask))
    result.save(target)


def remove_edge_background(source):
    """Remove a baked neutral checkerboard through edge-connected flood fill."""
    image = source.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    seen = set()
    queue = deque()

    def is_background(x, y):
        red, green, blue, alpha = pixels[x, y]
        return alpha > 0 and max(red, green, blue) - min(red, green, blue) <= 16 and (red + green + blue) / 3.0 >= 190

    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not is_background(x, y):
            continue
        seen.add((x, y))
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height and (nx, ny) not in seen:
                queue.append((nx, ny))
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    for x, y in seen:
        alpha_pixels[x, y] = 0
    image.putalpha(alpha)
    return image


def export_hair_pair(source, index):
    suffix = str(index) + ".png"
    if index in (0, 1):
        # The approved base head is kept intact in face/0.  Its neutral hairstyle
        # is therefore part of that master layer; separate hair slots stay empty.
        blank = Image.new("RGBA", SIZE, (0, 0, 0, 0))
        blank.save(OUT / "hair_back" / suffix)
        blank.save(OUT / "hair_front" / suffix)
        return
    rear_regions = [
        [(422, 78), (600, 78), (648, 186), (663, 370), (641, 548), (590, 586), (605, 374), (419, 374), (433, 586), (381, 548), (360, 370), (376, 186)],
    ]
    rear_mask = dark_hair_mask(source, rear_regions)
    rear_pixels = rear_mask.load()
    # Rear hair must never paint through the face. It is only visible above and
    # around the face silhouette, then reappears below the jawline.
    for y in range(245, 480):
        for x in range(395, 630):
            rear_pixels[x, y] = 0
    for y in range(305, 445):
        for x in list(range(360, 432)) + list(range(592, 665)):
            rear_pixels[x, y] = 0
    export(source, rear_mask, OUT / "hair_back" / suffix)
    front_regions = [
        [(382, 188), (642, 188), (651, 305), (608, 337), (579, 289), (512, 269), (444, 289), (415, 337), (373, 305)],
        [(371, 280), (437, 285), (424, 440), (381, 468)],
        [(587, 285), (653, 280), (643, 468), (600, 440)],
    ]
    front_mask = dark_hair_mask(source, front_regions)
    front_pixels = front_mask.load()
    # Keep the fringe and temple locks, but cut out the eye/cheek field.  This
    # prevents dark facial lines in the master from becoming false hair pixels.
    for y in range(240, 440):
        for x in range(420, 605):
            front_pixels[x, y] = 0
    for y in range(305, 445):
        for x in list(range(360, 432)) + list(range(592, 665)):
            front_pixels[x, y] = 0
    export(source, front_mask, OUT / "hair_front" / suffix)


def export_face_traits(source, index):
    suffix = str(index) + ".png"
    if index in (0, 1):
        base_head = [[
            (461, 42), (565, 42), (615, 105), (652, 214), (665, 318),
            (654, 411), (625, 494), (582, 554), (512, 596), (442, 554),
            (399, 494), (370, 411), (359, 318), (372, 214), (409, 105),
        ]]
        export(source, alpha_mask(base_head, 1), OUT / "face" / suffix)
        blank = Image.new("RGBA", SIZE, (0, 0, 0, 0))
        blank.save(OUT / "eye" / suffix)
        blank.save(OUT / "nose" / suffix)
        blank.save(OUT / "mouth" / suffix)
        return
    # Tight silhouette: do not let any master-image backdrop leak into the face layer.
    face_regions = [[(432, 237), (592, 237), (628, 276), (654, 329), (657, 390), (641, 461), (610, 522), (558, 572), (512, 596), (466, 572), (414, 522), (383, 461), (367, 390), (370, 329), (396, 276)]]
    face_mask = alpha_mask(face_regions, 1)
    mask_pixels = face_mask.load()
    source_pixels = source.convert("RGBA").load()
    for y in range(236, 522):
        for x in range(333, 692):
            red, green, blue, alpha = source_pixels[x, y]
            is_dark_hair = red + green + blue < 420 and (y < 318 or (x < 423 or x > 601))
            if is_dark_hair:
                mask_pixels[x, y] = 0
    export(source, face_mask, OUT / "face" / suffix)
    export(source, alpha_mask([[(384, 320), (640, 320), (640, 380), (384, 380)]], 1), OUT / "eye" / suffix)
    export(source, alpha_mask([[(472, 348), (554, 348), (560, 451), (466, 451)]], 1), OUT / "nose" / suffix)
    export(source, alpha_mask([[(442, 427), (583, 427), (580, 474), (445, 474)]], 1), OUT / "mouth" / suffix)


def export_clothes(source):
    """Extract only the half-body garment with a fixed, master-space silhouette."""
    clothes_regions = [[
        (370, 455), (295, 480), (182, 545), (92, 660), (28, 830),
        (0, 1040), (0, 1536), (1024, 1536), (1024, 1040), (996, 830),
        (932, 660), (842, 545), (729, 480), (654, 455), (634, 525),
        (390, 525),
    ]]
    mask = alpha_mask(clothes_regions, 1)
    export(source, mask, OUT / "clothes" / "0.png")


def main():
    source_path = Path(sys.argv[1]) if len(sys.argv) > 1 else MASTER
    index = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    source = Image.open(source_path).convert("RGBA")
    if source.size != SIZE:
        raise SystemExit("master must use the 1024x1536 half-body canvas")

    if index != 0:
        export_hair_pair(source, index)
        export_face_traits(source, index)
        return

    export_clothes(source)

    export_face_traits(source, 0)

    export_hair_pair(source, 0)

    blank = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    (OUT / "beard").mkdir(parents=True, exist_ok=True)
    blank.save(OUT / "beard" / "0.png")


if __name__ == "__main__":
    main()
