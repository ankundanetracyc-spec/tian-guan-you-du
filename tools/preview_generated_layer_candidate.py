"""Prepare, normalize and preview generated transparent-layer candidates.

Candidates stay outside the MOD package until their alpha and anchor checks pass.
"""
from collections import deque
from pathlib import Path
import sys

from PIL import Image, ImageFilter


ROOT = Path(r"E:\stacklands")
OUT = ROOT / "tmp" / "portrait_candidate"
FACE_SOURCE = Path(r"C:\Users\34784\.codex\generated_images\019ff181-300f-7d32-911e-ad70ca3bd517\exec-9ab35569-3aa1-46fa-8512-1e4206943981.png")
HAIR_SOURCE = Path(r"C:\Users\34784\.codex\generated_images\019ff181-300f-7d32-911e-ad70ca3bd517\exec-02066ee4-ecfe-49eb-8f89-b85b0541391b.png")
CANVAS = (1024, 1536)


def remove_edge_white(image):
    result = image.convert("RGBA")
    pixels = result.load()
    width, height = result.size
    queue = deque()
    seen = set()

    def white_background(x, y):
        red, green, blue, alpha = pixels[x, y]
        return alpha > 0 and min(red, green, blue) >= 236 and max(red, green, blue) - min(red, green, blue) <= 14

    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or not white_background(x, y):
            continue
        seen.add((x, y))
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height:
                queue.append((nx, ny))
    alpha = result.getchannel("A")
    alpha_pixels = alpha.load()
    for x, y in seen:
        alpha_pixels[x, y] = 0
    # The generator leaves an opaque white anti-alias fringe around the cutout.
    # It is not skin paint, so key it out globally instead of keeping a white halo.
    for y in range(height):
        for x in range(width):
            red, green, blue, _ = pixels[x, y]
            if min(red, green, blue) >= 242 and max(red, green, blue) - min(red, green, blue) <= 12:
                alpha_pixels[x, y] = 0
    result.putalpha(alpha)
    return result


def normalize(image, target_box):
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 24 else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError("candidate has no visible alpha")
    crop = image.crop(bbox)
    target_width = target_box[2] - target_box[0]
    target_height = target_box[3] - target_box[1]
    scale = min(target_width / crop.width, target_height / crop.height)
    size = (round(crop.width * scale), round(crop.height * scale))
    crop = crop.resize(size, Image.Resampling.LANCZOS)
    result = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    x = round((target_box[0] + target_box[2] - size[0]) / 2)
    y = target_box[1] + (target_height - size[1]) // 2
    result.alpha_composite(crop, (x, y))
    return result


def taper_neck(image):
    """Let the robe collar cover the lower neck naturally after composition."""
    result = image.copy()
    alpha = result.getchannel("A")
    pixels = alpha.load()
    for y in range(498, 611):
        half_width = round(72 - (y - 498) * 0.38)
        for x in range(1024):
            if abs(x - 512) > max(26, half_width):
                pixels[x, y] = 0
    result.putalpha(alpha)
    return result


def erode_edge_alpha(image):
    """Remove the one-pixel bright matte left by white-background generation."""
    result = image.copy()
    result.putalpha(result.getchannel("A").filter(ImageFilter.MinFilter(3)))
    return result


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    face = erode_edge_alpha(taper_neck(normalize(remove_edge_white(Image.open(FACE_SOURCE)), (360, 160, 664, 610))))
    hair = normalize(Image.open(HAIR_SOURCE).convert("RGBA"), (370, 35, 654, 445))
    face.save(OUT / "face_candidate.png")
    hair.save(OUT / "hair_front_candidate.png")
    preview = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    preview.alpha_composite(face)
    preview.alpha_composite(hair)
    preview.save(OUT / "head_candidate_preview.png")
    body = Image.open(ROOT / "mods" / "appearance" / "水墨半身男修基础包" / "clothes" / "0.png").convert("RGBA")
    body.alpha_composite(face)
    body.alpha_composite(hair)
    body.save(OUT / "full_candidate_preview.png")
    if "--install" in sys.argv:
        package = ROOT / "mods" / "appearance" / "水墨半身男修基础包"
        face.save(package / "face" / "1.png")
        hair.save(package / "hair_front" / "1.png")
        blank = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        for part in ("hair_back", "eye", "nose", "mouth", "beard"):
            blank.save(package / part / "1.png")
        print("installed candidate as face/hair_front variant 1")
    for name, image in (("face", face), ("hair_front", hair)):
        bbox = image.getchannel("A").point(lambda value: 255 if value >= 24 else 0).getbbox()
        print(name, bbox)


if __name__ == "__main__":
    main()
