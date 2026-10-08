"""Normalize a generated featureless head candidate without touching runtime art."""
from collections import deque
from pathlib import Path

from PIL import Image, ImageFilter


ROOT = Path(r"E:\stacklands")
SOURCE = ROOT / "mods" / "appearance" / "水墨半身男修重制包" / "候选部件" / "头部_无五官_0_原图.png"
TARGET = ROOT / "mods" / "appearance" / "水墨半身男修重制包" / "候选部件" / "头部_无五官_0_标准画布_v2.png"
CANVAS = (1024, 1536)
TARGET_BOX = (360, 138, 664, 760)


def remove_dark_edge_background(source: Image.Image) -> Image.Image:
    image = source.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    queue = deque()
    seen = set()

    def is_dark_backdrop(x: int, y: int) -> bool:
        red, green, blue, opacity = pixels[x, y]
        return opacity > 0 and max(red, green, blue) <= 142 and max(red, green, blue) - min(red, green, blue) <= 28

    for x in range(width):
        queue.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        queue.extend(((0, y), (width - 1, y)))
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not is_dark_backdrop(x, y):
            continue
        seen.add((x, y))
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height:
                queue.append((nx, ny))
    for x, y in seen:
        alpha_pixels[x, y] = 0
    image.putalpha(alpha)
    return image


def normalize(source: Image.Image) -> Image.Image:
    visible = source.getchannel("A").point(lambda value: 255 if value >= 32 else 0)
    bounds = visible.getbbox()
    if bounds is None:
        raise ValueError("head candidate lost all visible pixels")
    crop = source.crop(bounds)
    target_width = TARGET_BOX[2] - TARGET_BOX[0]
    target_height = TARGET_BOX[3] - TARGET_BOX[1]
    scale = min(target_width / crop.width, target_height / crop.height)
    resized = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    position = ((TARGET_BOX[0] + TARGET_BOX[2] - resized.width) // 2, TARGET_BOX[1])
    result.alpha_composite(resized, position)
    # Remove a thin matte left by the dark studio background.
    result.putalpha(result.getchannel("A").filter(ImageFilter.MinFilter(3)))
    return result


def extend_hidden_neck(image: Image.Image) -> Image.Image:
    """Create a broad lower-neck reserve that every outfit collar can cover."""
    result = image.copy()
    # This sampled region is entirely below the visible jaw. It is deliberately
    # extended well below the future collar line, so its broad lower edge will
    # never become part of a final character silhouette.
    neck_source = result.crop((402, 560, 622, 728))
    extension = neck_source.resize((360, 360), Image.Resampling.LANCZOS)
    alpha = extension.getchannel("A")
    alpha_pixels = alpha.load()
    for y in range(extension.height):
        fade_in = min(255, y * 20)
        for x in range(extension.width):
            alpha_pixels[x, y] = min(alpha_pixels[x, y], fade_in)
    extension.putalpha(alpha)
    result.alpha_composite(extension, (332, 570))
    return result


if __name__ == "__main__":
    prepared = extend_hidden_neck(normalize(remove_dark_edge_background(Image.open(SOURCE))))
    prepared.save(TARGET)
    print(prepared.getchannel("A").getbbox())
