from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "mods" / "appearance" / "腰上男修拆件演示_v2"
SOURCE = PACKAGE / "reference" / "user_selected_portrait.png"
HAIR_B_SOURCE = PACKAGE / "preview" / "appearance_variants" / "hair_style_b_generated.png"
OUT = PACKAGE / "test_assets"
OUT.mkdir(parents=True, exist_ok=True)

source = Image.open(SOURCE).convert("RGBA")
hair_b = Image.open(HAIR_B_SOURCE).convert("RGBA")
if source.size != (1024, 1536) or hair_b.size != source.size:
    raise ValueError("Appearance test assets must share the 1024x1536 master canvas")

width, height = source.size


def polygon_mask(points, blur=0.0):
    mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    if blur:
        mask = mask.filter(ImageFilter.GaussianBlur(blur))
    return mask


def transparent_layer(image, mask):
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    layer.paste(image, (0, 0), mask)
    return layer


# The face layer owns the skin and all facial marks; hairline and outer locks remain on the hairstyle.
face_points = [
    (445, 280), (463, 275), (489, 272), (535, 272), (561, 275), (579, 281),
    (589, 299), (601, 326), (611, 353), (617, 384), (620, 414), (618, 438),
    (610, 464), (598, 486), (583, 506), (565, 524), (544, 539), (525, 547),
    (512, 551), (497, 548), (478, 540), (459, 527), (440, 509), (424, 487),
    (412, 466), (405, 444), (401, 420), (403, 391), (408, 364), (418, 336),
    (430, 308),
]
face_mask = polygon_mask(face_points, blur=2.0)
face_a = transparent_layer(source, face_mask)
face_a.save(OUT / "face_standard.png")


def jaw_scale(y):
    # Keep the brow/eye anchors fixed; broaden only the lower cheek and jaw.
    stops = [(272, 1.0), (410, 1.0), (448, 1.045), (493, 1.12), (522, 1.18), (545, 1.06), (552, 1.0)]
    for (y0, s0), (y1, s1) in zip(stops, stops[1:]):
        if y0 <= y <= y1:
            t = (y - y0) / max(1, y1 - y0)
            return s0 + (s1 - s0) * t
    return 1.0


wide_source = source.copy()
src_px = source.load()
wide_px = wide_source.load()
for y in range(270, 556):
    scale = jaw_scale(y)
    for x in range(390, 635):
        sx = 512.0 + (x - 512.0) / scale
        x0 = int(sx)
        x1 = min(width - 1, x0 + 1)
        t = sx - x0
        p0 = src_px[max(0, x0), y]
        p1 = src_px[x1, y]
        wide_px[x, y] = tuple(round(p0[c] * (1 - t) + p1[c] * t) for c in range(4))
face_b = transparent_layer(wide_source, face_mask)
face_b.save(OUT / "face_wide_jaw.png")


# A second dye option changes the large pale robe surfaces to blue-grey while preserving ink texture.
clothes_points = [
    (416, 541), (443, 548), (512, 707), (581, 548), (608, 541), (675, 615),
    (789, 646), (917, 680), (969, 722), (1023, 800), (1023, 1535), (0, 1535),
    (0, 800), (55, 722), (107, 680), (235, 646), (349, 615),
]
clothes_region = polygon_mask(clothes_points, blur=1.0)
clothes_alpha = Image.new("L", (width, height), 0)
clothes_px = clothes_alpha.load()
cloth_rgb = source.convert("RGB")
cloth_px = cloth_rgb.load()
region_px = clothes_region.load()
for y in range(height):
    for x in range(width):
        if region_px[x, y] < 32:
            continue
        r, g, b, a = src_px[x, y]
        if a == 0:
            continue
        luminance = (r * 30 + g * 59 + b * 11) // 100
        # Select the broad light fabric only; dark hair, lapels, belt, line work and embroidery stay intact.
        if luminance < 138 or r < g - 8 or g < b - 2:
            continue
        alpha = min(a, region_px[x, y])
        if alpha < 20:
            continue
        cloth_px[x, y] = (20 + int(luminance * 0.23), 25 + int(luminance * 0.26), 32 + int(luminance * 0.29))
        clothes_alpha.putpixel((x, y), alpha)
clothes_b = Image.new("RGBA", (width, height), (0, 0, 0, 0))
clothes_b.paste(cloth_rgb.convert("RGBA"), (0, 0), clothes_alpha)
clothes_b.save(OUT / "clothes_ink_blue.png")


# Preserve the user-selected image as A and the matched generated hair edit as B.
source.save(OUT / "portrait_hair_a.png")
hair_b.save(OUT / "portrait_hair_b.png")


def blend(base, layer):
    result = base.copy()
    result.alpha_composite(layer)
    return result


def compose(face_variant, hair_variant, clothes_variant):
    result = (source if hair_variant == 0 else hair_b).copy()
    result = blend(result, face_a if face_variant == 0 else face_b)
    if clothes_variant == 1:
        result = blend(result, clothes_b)
    return result


for face in range(2):
    for hair in range(2):
        for clothes in range(2):
            compose(face, hair, clothes).save(OUT / f"combo_f{face}_h{hair}_c{clothes}.png")

samples = [
    ("基准", compose(0, 0, 0)),
    ("宽阔下颌", compose(1, 0, 0)),
    ("披发", compose(0, 1, 0)),
    ("青灰外袍", compose(0, 0, 1)),
]
tile_w, tile_h = 410, 615
sheet = Image.new("RGB", (tile_w * 2, (tile_h + 36) * 2), (239, 235, 225))
try:
    from PIL import ImageFont
    font = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 20)
except OSError:
    font = ImageFont.load_default()
draw = ImageDraw.Draw(sheet)
for index, (label, image) in enumerate(samples):
    x = (index % 2) * tile_w
    y = (index // 2) * (tile_h + 36)
    draw.text((x + 10, y + 4), label, font=font, fill=(42, 49, 51))
    preview = image.resize((tile_w, tile_h), Image.Resampling.LANCZOS)
    paper_preview = Image.new("RGB", preview.size, (215, 211, 199))
    paper_preview.paste(preview, (0, 0), preview.getchannel("A"))
    sheet.paste(paper_preview, (x, y + 36))
sheet.save(PACKAGE / "preview" / "in_game_appearance_options.png")

print(f"Generated face, hair and clothing options on the fixed {width}x{height} canvas: {OUT}")
