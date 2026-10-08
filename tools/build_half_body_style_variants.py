"""Build color and beard variants on the fixed half-body portrait canvas."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance


ROOT = Path(r"E:\stacklands\mods\appearance\水墨半身男修基础包")
MATURE_SOURCE = Path(r"C:\Users\34784\.codex\generated_images\019ff181-300f-7d32-911e-ad70ca3bd517\exec-8efa0628-0fb7-409b-85b0-0917b98f0c39.png")
SIZE = (1024, 1536)


def recolor_clothes(source, target_rgb, strength, value):
    image = source.convert("RGBA")
    alpha = image.getchannel("A")
    grayscale = image.convert("L")
    tint = Image.new("RGBA", SIZE, target_rgb + (255,))
    tint.putalpha(alpha)
    colored = Image.blend(image, tint, strength)
    colored = ImageEnhance.Brightness(colored).enhance(value)
    colored.putalpha(alpha)
    return colored


def dark_mask(source, rectangles):
    result = Image.new("L", SIZE, 0)
    out = result.load()
    src = source.convert("RGBA").load()
    for left, top, right, bottom in rectangles:
        for y in range(top, bottom):
            for x in range(left, right):
                red, green, blue, alpha = src[x, y]
                if alpha > 20 and red + green + blue < 420:
                    out[x, y] = 255
    return result


def export_beard(source, rectangles, index, opacity=255):
    mask = dark_mask(source, rectangles)
    if opacity < 255:
        mask = mask.point(lambda value: value * opacity // 255)
    result = source.copy().convert("RGBA")
    result.putalpha(mask)
    result.save(ROOT / "beard" / (str(index) + ".png"))


def export_safe_head_variants():
    """Keep the prototype package playable while hand-painted trait layers are pending.

    The clean base head already contains its neutral hairstyle.  Copies remain on
    the identical canvas; only a subtle ink/skin value change is applied.  Empty
    overlay slots prevent unverified extracted pixels from contaminating a face.
    """
    base = Image.open(ROOT / "face" / "0.png").convert("RGBA")
    alpha = base.getchannel("A")
    adjustments = [(1.01, 0.99), (0.96, 1.01), (1.04, 0.98), (0.92, 1.02)]
    blank = Image.new("RGBA", SIZE, (0, 0, 0, 0))
    for index, (brightness, color) in enumerate(adjustments, start=1):
        variant = ImageEnhance.Brightness(base).enhance(brightness)
        variant = ImageEnhance.Color(variant).enhance(color)
        variant.putalpha(alpha)
        variant.save(ROOT / "face" / (str(index) + ".png"))
        for part in ("hair_back", "hair_front", "eye", "nose", "mouth", "beard"):
            blank.save(ROOT / part / (str(index) + ".png"))


def main():
    clothes = Image.open(ROOT / "clothes" / "0.png").convert("RGBA")
    palettes = [
        ((116, 142, 115), 0.22, 1.00),
        ((54, 73, 105), 0.33, 0.80),
        ((205, 202, 187), 0.18, 1.08),
        ((150, 123, 90), 0.20, 0.96),
    ]
    for index, (color, strength, value) in enumerate(palettes, start=1):
        recolor_clothes(clothes, color, strength, value).save(ROOT / "clothes" / (str(index) + ".png"))

    export_safe_head_variants()


if __name__ == "__main__":
    main()
