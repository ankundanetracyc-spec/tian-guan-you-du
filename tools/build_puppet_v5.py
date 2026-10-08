"""Build true-alpha, same-canvas v5 shadow-puppet layers from approved source parts."""
from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(r"E:\stacklands")
TEMP = Path(r"C:\Users\34784\AppData\Local\Temp")
OUT = ROOT / "mods" / "appearance" / "皮影局内男修_v5"
CANVAS = (1024, 2048)
OUTER_RADIUS = 28
INNER_RADIUS = 14


def clear_baked_checkerboard(image):
    """Flood-fill only the bright checkerboard that touches an image edge."""
    image = image.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    clear = set()
    queue = deque()

    def is_checker(x: int, y: int) -> bool:
        r, g, b, _ = pixels[x, y]
        return r >= 225 and g >= 225 and b >= 225

    for x in range(width):
        for y in (0, height - 1):
            if is_checker(x, y):
                clear.add((x, y)); queue.append((x, y))
    for y in range(height):
        for x in (0, width - 1):
            if is_checker(x, y):
                clear.add((x, y)); queue.append((x, y))
    while queue:
        x, y = queue.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < width and 0 <= ny < height and (nx, ny) not in clear and is_checker(nx, ny):
                clear.add((nx, ny)); queue.append((nx, ny))
    for x, y in clear:
        r, g, b, _ = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)
    return image


def resized(image, sx, sy=None):
    sy = sx if sy is None else sy
    return image.resize((round(image.width * sx), round(image.height * sy)), Image.Resampling.LANCZOS)


def make_layer(source, destination, scale, position, holes, checkerboard=False):
    image = Image.open(TEMP / source).convert("RGBA")
    if checkerboard:
        image = clear_baked_checkerboard(image)
    image = resized(image, *scale)
    result = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    result.alpha_composite(image, position)
    draw = ImageDraw.Draw(result)
    for x, y, material in holes:
        # Covers legacy gold rims, then draws a muted material-coloured axle rim.
        draw.ellipse((x - OUTER_RADIUS, y - OUTER_RADIUS, x + OUTER_RADIUS, y + OUTER_RADIUS), fill=(*material, 255))
        shade = tuple(max(0, channel - 22) for channel in material)
        draw.ellipse((x - OUTER_RADIUS + 4, y - OUTER_RADIUS + 4, x + OUTER_RADIUS - 4, y + OUTER_RADIUS - 4), outline=(*shade, 180), width=3)
        draw.ellipse((x - INNER_RADIUS, y - INNER_RADIUS, x + INNER_RADIUS, y + INNER_RADIUS), fill=(0, 0, 0, 0))
    target = OUT / destination
    target.parent.mkdir(parents=True, exist_ok=True)
    result.save(target)
    assert result.getpixel((0, 0))[3] == 0 and result.getpixel((1023, 2047))[3] == 0


SKIN = (217, 187, 139)
HAIR = (47, 46, 43)
ORNAMENT = (69, 79, 69)
RED = (125, 56, 45)

# All target connectors use the same 56px outer / 28px transparent aperture.
CROWN = (545, 455)
NECK = (512, 903)
FAR_SHOULDER = (380, 986)
NEAR_SHOULDER = (654, 986)

make_layer("codex-clipboard-9c253133-5688-4eee-9a69-b39d969cad7f.png", "head/male_bald.png", (.40, .40), (312, 405), [(CROWN[0], CROWN[1], SKIN), (NECK[0], NECK[1], SKIN)])
make_layer("codex-clipboard-0b076843-9598-4aa4-ae2f-5619c1d7e1c0.png", "head/female_bald.png", (.42, .45), (276, 396), [(CROWN[0], CROWN[1], SKIN), (NECK[0], NECK[1], SKIN)])
make_layer("codex-clipboard-9a358d14-735b-414f-b6bf-be617192d446.png", "hair/hair_back.png", (.40, .40), (273, 410), [(CROWN[0], CROWN[1], HAIR)])
make_layer("codex-clipboard-b9e7a29b-7828-42f5-9fdc-28a131a246d0.png", "hair/hair_front.png", (.40, .40), (194, 413), [(CROWN[0], CROWN[1], HAIR)])
make_layer("codex-clipboard-d45443bf-5516-4efb-8ce5-8481176ea000.png", "ornament/cloud_hairpin.png", (.18, .18), (379, 324), [(CROWN[0], CROWN[1], ORNAMENT)])
make_layer("codex-clipboard-f063670e-0112-48a4-a15e-5479b6774ffa.png", "body/vermilion_body.png", (.70, .70), (154, 850), [(NECK[0], NECK[1], SKIN), (FAR_SHOULDER[0], FAR_SHOULDER[1], RED), (NEAR_SHOULDER[0], NEAR_SHOULDER[1], RED)])
make_layer("codex-clipboard-6bec9bd9-c965-47d8-b1cf-b98e15ffd7ff.png", "arm/left_arm.png", (.45, .45), (29, 896), [(FAR_SHOULDER[0], FAR_SHOULDER[1], RED)], checkerboard=True)
make_layer("codex-clipboard-771889bf-565a-4c35-8c7d-393e9a1db899.png", "arm/right_arm.png", (.42, .42), (542, 951), [(NEAR_SHOULDER[0], NEAR_SHOULDER[1], RED)])

preview = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
for relative_path in (
    "arm/left_arm.png", "hair/hair_back.png", "head/male_bald.png", "body/vermilion_body.png",
    "ornament/cloud_hairpin.png", "hair/hair_front.png", "arm/right_arm.png",
):
    preview.alpha_composite(Image.open(OUT / relative_path).convert("RGBA"))
preview.save(OUT / "assembled_preview.png")
