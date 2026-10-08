"""Register the female test paintings into fixed-canvas modular layers.

Source drawings live beside the exported assets. The generated previews are
inspection aids; the game loads only test_assets/*.png.
"""
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
PKG = ROOT / "mods/appearance/腰上女修拆件演示_v1"
SRC = PKG / "source"
OUT = PKG / "test_assets_v4"
PREVIEW = PKG / "preview/rework_v4"
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
SIZE = (1024, 1536)
YY, XX = np.indices((SIZE[1], SIZE[0]), dtype=np.float32)


def open_source(name):
    im = Image.open(SRC / name).convert("RGBA")
    if im.size != SIZE:
        raise ValueError(f"{name}: expected {SIZE}, got {im.size}")
    return im


def shifted(im, dy):
    if dy == 0:
        return im
    out = Image.new("RGBA", SIZE)
    out.paste(im, (0, dy))
    return out


BASE = open_source("base_full.png")
BLANK = open_source("blank_full.png")
FULL = [BASE] + [shifted(open_source(f"variant_{i}_full.png"), 28 if i == 1 else 0) for i in range(1, 5)]
CLOTH = [open_source(f"clothes_{i}_full.png") for i in range(5)]
HAIR_4 = open_source("hair_4_cutout.png")
HAIR_REWORK = {i: shifted(open_source("hair_rework_full_1_single_line.png" if i == 1 else "hair_clean_full_3.png" if i == 3 else f"hair_rework_full_{i}.png"), 28 if i == 1 else 0) for i in range(5)}
hairline_detail = Image.open(SRC / "hairline_single_detail.png").convert("RGBA").resize((320, 210), Image.Resampling.LANCZOS)
HAIRLINE_PAINT = Image.new("RGBA", SIZE)
HAIRLINE_PAINT.paste(hairline_detail, (350, 180))


def polygon(points, blur=0):
    mask = Image.new("L", SIZE)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(blur)) if blur else mask


def ellipses(boxes, blur=0):
    mask = Image.new("L", SIZE)
    draw = ImageDraw.Draw(mask)
    for box in boxes:
        draw.ellipse(box, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(blur)) if blur else mask


def save(kind, i, im):
    im.save(OUT / f"{kind}_{i}.png", optimize=True)


def with_alpha(im, alpha):
    rgba = np.asarray(im).copy()
    rgba[:, :, 3] = np.clip(np.asarray(im.getchannel("A"), dtype=np.float32) * alpha, 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def repaint_hair_interior(original, repaint, matte):
    """Transfer painted strands through the hair edge without the old halo."""
    old = np.asarray(original, dtype=np.uint8)
    new = np.asarray(repaint, dtype=np.uint8)
    core = (matte > .12).astype(np.uint8)
    new_lum = new[:, :, :3] @ np.array([.299, .587, .114])
    core *= (new_lum < 175).astype(np.uint8)
    blend = cv2.GaussianBlur(core.astype(np.float32), (0, 0), .6)[:, :, None] * .98
    painted = old.copy()
    painted[:, :, :3] = np.clip(old[:, :, :3] * (1 - blend) + new[:, :, :3] * blend, 0, 255).astype(np.uint8)
    return Image.fromarray(painted, "RGBA")


def smoothstep(v):
    v = np.clip(v, 0, 1)
    return v * v * (3 - 2 * v)


def remove_double_hairline(source, matte, extra_mask=None):
    # All five generated female paintings repeat a pale arc above the true
    # forehead transition. Transfer the one-line redraw at the shared rig.
    old = np.asarray(source, dtype=np.uint8).copy()
    redraw = np.asarray(HAIRLINE_PAINT, dtype=np.uint8)
    hairline = np.asarray(polygon([(418, 238), (460, 239), (512, 246), (563, 239), (608, 238),
                                   (613, 271), (565, 280), (512, 288), (460, 280), (412, 271)], 7), dtype=np.float32) / 255
    if extra_mask is not None:
        hairline = np.maximum(hairline, extra_mask)
    mix = np.clip(hairline * matte, 0, 1)[:, :, None]
    old[:, :, :3] = np.clip(old[:, :, :3] * (1 - mix) + redraw[:, :, :3] * mix, 0, 255).astype(np.uint8)
    if extra_mask is not None:
        old[:, :, 3] = np.maximum(old[:, :, 3], np.clip(redraw[:, :, 3] * extra_mask, 0, 255).astype(np.uint8))
    return Image.fromarray(old, "RGBA")


# The skin silhouette and neck overlap rather than meeting at one thin line.
# This prevents the transparent cheek-to-neck wedges seen in the first male pass.
face_mask = np.asarray(polygon([
    (455, 275), (485, 266), (512, 264), (539, 266), (569, 275),
    (591, 299), (603, 334), (605, 386), (607, 428), (599, 467),
    (581, 503), (537, 536), (512, 552), (487, 536), (443, 503),
    (425, 467), (417, 428), (419, 386), (421, 334), (433, 299),
], 1.5), dtype=np.float32) / 255
neck_mask = np.asarray(polygon([
    (438, 463), (586, 463), (601, 490), (602, 538), (590, 622),
    (565, 705), (512, 800), (459, 705), (434, 622), (422, 538),
    (423, 490),
], 1.5), dtype=np.float32) / 255
ear_mask = np.asarray(ellipses([(365, 357, 429, 457), (595, 357, 659, 457)], 2), dtype=np.float32) / 255
face_alpha = np.maximum.reduce((face_mask, neck_mask, ear_mask))

face_source = np.asarray(BLANK, dtype=np.float32)
neck_source = np.asarray(BASE, dtype=np.float32)
for i, stops in enumerate([
    [(270, 1), (420, 1), (475, 1), (550, 1)],
    [(270, 1), (420, 1), (475, 1.025), (550, 1.07)],
    [(270, 1), (420, 1), (475, .98), (550, .93)],
    [(270, 1), (420, 1), (475, 1.01), (550, 1.04)],
    [(270, 1), (420, 1), (475, 1.02), (550, 1.055)],
]):
    # Shape variation changes both paint and silhouette; by the collar the
    # neck returns to the same width for all outfits.
    stops = stops + [(650, 1), (1535, 1)]
    scale = np.interp(np.arange(SIZE[1]), [y for y, _ in stops], [s for _, s in stops]).astype(np.float32)[:, None]
    map_x = np.broadcast_to(512 + (XX - 512) / scale, XX.shape).astype(np.float32)
    warped = cv2.remap(face_source, map_x, YY, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    shaped_face_mask = cv2.remap(face_mask, map_x, YY, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    shaped_neck_mask = cv2.remap(neck_mask, map_x, YY, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    # Paint the featureless face over the neck. A direct RGB blend used the
    # original mouth/skin across the whole overlap and drew a horizontal seam.
    head = with_alpha(BLANK, np.maximum(shaped_neck_mask, ear_mask))
    head.alpha_composite(with_alpha(Image.fromarray(np.clip(warped, 0, 255).astype(np.uint8), "RGBA"), shaped_face_mask))
    save("face", i, head)


# A fixed semantic window only limits where a feature may live. Within it,
# local paint contrast determines alpha, leaving unrelated skin transparent.
FEATURE_WINDOWS = {
    "brow": ([(416, 332, 501, 370), (523, 332, 608, 370)], 3),
    "eye": ([(415, 368, 501, 409), (523, 368, 609, 409)], 2),
    "nose": ([(478, 405, 546, 470)], 5),
    "mouth": ([(463, 472, 561, 510)], 3),
}
for kind, (boxes, feather) in FEATURE_WINDOWS.items():
    roi = np.asarray(ellipses(boxes, feather), dtype=np.float32) / 255
    for i, source in enumerate(FULL):
        src = np.asarray(source, dtype=np.float32)
        rgb = src[:, :, :3]
        local = cv2.GaussianBlur(rgb, (0, 0), 11)
        lum = rgb @ np.array([.299, .587, .114], dtype=np.float32)
        local_lum = local @ np.array([.299, .587, .114], dtype=np.float32)
        darkness = local_lum - lum
        if kind == "brow":
            strength = smoothstep((darkness - 4) / 35) * smoothstep((190 - lum) / 55)
            # Reserve genuinely clear pixels between eyebrow and eyelid.
            strength *= smoothstep((372 - YY) / 7)
            if i in (1, 3):
                # These two paintings also contain a second stroke beneath the
                # eyebrow. It begins at y=363 and must not enter the brow layer.
                strength *= smoothstep((363 - YY) / 4)
        elif kind == "eye":
            strength = np.maximum(smoothstep((darkness - 3) / 34), smoothstep((160 - lum) / 60))
            strength *= smoothstep((YY - 367) / 6)
        elif kind == "nose":
            strength = smoothstep((darkness - 3) / 30) * .84
        else:
            red_tint = rgb[:, :, 0] - rgb[:, :, 1]
            strength = np.maximum(smoothstep((darkness - 6) / 28), smoothstep((red_tint - 29) / 20))
        alpha = roi * strength
        if kind == "eye":
            # Retain the small sclera while the surrounding painted skin stays clear.
            whites = np.asarray(ellipses([(433, 375, 488, 398), (536, 375, 591, 398)], 2), dtype=np.float32) / 255
            alpha = np.maximum(alpha, whites * smoothstep((lum - local_lum + 3) / 30) * .75)
        save(kind, i, with_alpha(source, alpha))


# A fifth independent cosmetic slot replaces beard for women. All four painted
# variants stay subtle and away from the eye/nose/mouth component windows.
save("makeup", 0, Image.new("RGBA", SIZE))
for i in range(1, 5):
    layer = Image.new("RGBA", SIZE)
    blush = Image.new("L", SIZE)
    draw = ImageDraw.Draw(blush)
    if i in (1, 3, 4):
        for box in ((439, 419, 477, 455), (547, 419, 585, 455)):
            draw.ellipse(box, fill=28 if i == 1 else 38)
    blush = blush.filter(ImageFilter.GaussianBlur(15))
    pigment = Image.new("RGBA", SIZE, (156, 81, 78, 0))
    pigment.putalpha(blush)
    layer.alpha_composite(pigment)
    if i in (2, 3):
        dot = Image.new("RGBA", SIZE)
        ImageDraw.Draw(dot).ellipse((571 if i == 2 else 450, 452, 575 if i == 2 else 454, 456), fill=(72, 55, 48, 168))
        layer.alpha_composite(dot)
    if i == 4:
        freckle = ImageDraw.Draw(layer)
        for x, y in ((445, 444), (452, 448), (575, 444), (582, 448)):
            freckle.ellipse((x, y, x + 2, y + 2), fill=(125, 92, 80, 95))
    save("makeup", i, with_alpha(layer, face_alpha))


# Color/position matte from each matching full portrait. The head, long locks
# and braid remain a paired option while the garment is clean of old hair.
front_corridors = [
    [[(225, 500), (445, 500), (430, 1310), (275, 1310)], [(580, 500), (800, 500), (785, 1270), (620, 1270)]],
    [[(330, 475), (444, 475), (425, 700), (315, 700)]],
    [[(590, 470), (740, 470), (925, 1120), (760, 1130), (630, 750)]],
    [[(305, 490), (445, 490), (420, 695), (310, 695)], [(580, 490), (715, 490), (720, 695), (590, 695)]],
    [[(315, 480), (470, 480), (455, 1360), (310, 1360)]],
]
head_hole = np.asarray(polygon([
    (450, 284), (485, 270), (512, 267), (539, 270), (574, 284),
    (597, 323), (603, 385), (587, 449), (553, 510), (512, 554),
    (471, 510), (437, 449), (421, 385), (427, 323),
], 3), dtype=np.float32) / 255
scalp_bridge = np.asarray(ellipses([(418, 222, 606, 302)], 2), dtype=np.float32) / 255
for i, source in enumerate(FULL):
    if i == 4:
        # The braided option has a dedicated cutout. Color-keying its dark
        # braid against a dark green robe left robe paint on other outfits.
        front = (YY < 510).astype(np.float32)
        tail = smoothstep((1370 - YY) / 90)
        right_cleanup = 1 - smoothstep((XX - 590) / 15) * smoothstep((YY - 450) / 25)
        braid_matte = np.asarray(HAIR_4.getchannel("A"), dtype=np.float32) / 255
        braid = repaint_hair_interior(HAIR_4, HAIR_REWORK[4], braid_matte)
        braid = remove_double_hairline(braid, braid_matte)
        back = with_alpha(braid, (1 - front) * tail * right_cleanup)
        # The cutout omitted some dark hair immediately behind the jaw. Bring
        # those pixels from the matching full painting, never its skin/robe.
        side = Image.new("L", SIZE)
        side_draw = ImageDraw.Draw(side)
        side_draw.polygon([(355, 405), (423, 405), (448, 475), (440, 546), (360, 546)], fill=255)
        side_draw.polygon([(669, 405), (601, 405), (576, 475), (584, 546), (664, 546)], fill=255)
        side_arr = np.asarray(side.filter(ImageFilter.GaussianBlur(3)), dtype=np.float32) / 255
        src_rgb = np.asarray(source.convert("RGB"), dtype=np.float32)
        src_lum = src_rgb @ np.array([.299, .587, .114], dtype=np.float32)
        side_arr *= smoothstep((145 - src_lum) / 50) * smoothstep((560 - YY) / 22)
        back.alpha_composite(with_alpha(source, side_arr))
        save("hair_back", i, back)
        save("hair_front", i, with_alpha(braid, front * tail * right_cleanup))
        save("hair_accessory", i, Image.new("RGBA", SIZE))
        continue
    rgba = np.asarray(source, dtype=np.float32)
    rgb = rgba[:, :, :3]
    lum = rgb @ np.array([.299, .587, .114], dtype=np.float32)
    neutral = np.clip((19 - np.abs(rgb[:, :, 0] - rgb[:, :, 1])) / 14, 0, 1)
    dark = smoothstep((168 - lum) / 63)
    hair_color = np.clip(dark * neutral * 1.35, 0, 1)
    garment = shifted(CLOTH[i], 28) if i == 1 else CLOTH[i]
    garment_rgba = np.asarray(garment, dtype=np.float32)
    garment_diff = np.max(np.abs(rgb - garment_rgba[:, :, :3]), axis=2)
    hair_over_robe = np.where(
        garment_rgba[:, :, 3] > 50,
        smoothstep((garment_diff - 11) / 38),
        1.0,
    )
    corridor_image = Image.new("L", SIZE)
    corridor_draw = ImageDraw.Draw(corridor_image)
    for path in front_corridors[i]:
        corridor_draw.polygon(path, fill=255)
    corridor = np.asarray(corridor_image.filter(ImageFilter.GaussianBlur(5)), dtype=np.float32) / 255
    # Upper mass and side wisps: preserve original painted pixels. Near the
    # neck and below, only dark hair in the traced corridors survives.
    upper = (YY < 535).astype(np.float32)
    # The old unrestricted upper mask carried collar, shoulders and even ears
    # from the complete source portrait into the hair layer.
    # Brown transition pixels at the hairline are still hair. The neutral-tone
    # gate alone removed them and left a transparent row above the forehead.
    top_mask = (1 - head_hole) * upper * np.maximum(hair_color, scalp_bridge)
    if i == 2:
        head_area = np.asarray(ellipses([(260, 0, 790, 650)], 3), dtype=np.float32) / 255
        top_mask *= head_area
    lower_mask = hair_color * hair_over_robe * corridor * (1 - upper)
    source_clean = smoothstep((rgba[:, :, 3] - 205) / 40) if i == 2 else 1
    hair_alpha = np.maximum(top_mask, lower_mask) * source_clean
    if i in (1, 3):
        # The face cutout and hair matte leave a thin transparent slit at the
        # right temple of these styles. The close-up has painted hair here.
        temple_bridge = np.asarray(polygon([(583, 279), (597, 285), (606, 326),
                                            (604, 356), (592, 356), (590, 322)], 2), dtype=np.float32) / 255
        hair_alpha = np.maximum(hair_alpha, temple_bridge)
    # Suppress the weak gray fringe carried by the full-portrait background.
    # The retained one-pixel transition is enough for a clean scaled edge.
    hair_alpha = smoothstep((hair_alpha - .18) / .64)
    if i in HAIR_REWORK:
        source = repaint_hair_interior(source, HAIR_REWORK[i], hair_alpha)
    source = remove_double_hairline(source, hair_alpha, temple_bridge if i in (1, 3) else None)
    # Forehead/temple locks are foreground. Their length varies by hairstyle;
    # every long tail and shoulder-length end belongs behind the robe.
    front_ends = (540, 510, 525, 500)
    front_area = (YY < front_ends[i]).astype(np.float32)
    cheek_locks = {
        1: [[(375, 460), (435, 460), (427, 555), (416, 635), (398, 666), (377, 638)]],
        2: [[(590, 465), (654, 465), (674, 575), (682, 676), (663, 715), (624, 663)]],
        3: [[(370, 455), (433, 455), (425, 535), (413, 587), (393, 607), (377, 572)],
            [(592, 455), (654, 455), (665, 548), (654, 587), (633, 604), (605, 563)]],
    }
    for lock in cheek_locks.get(i, []):
        front_area = np.maximum(front_area, np.asarray(polygon(lock), dtype=np.float32) / 255)
    ornament_windows = {0: (375, 72, 649, 170), 2: (397, 94, 635, 190)}
    ornament_selector = np.zeros_like(hair_alpha)
    if i in ornament_windows:
        x0, y0, x1, y1 = ornament_windows[i]
        ornament_selector[y0:y1, x0:x1] = 1
    # Carry the complete painted band, including its dark contour and white
    # thread, rather than keying ribbon colors into transparent fragments.
    ornament_source = HAIR_REWORK[i] if i in (0, 2) else source
    save("hair_accessory", i, with_alpha(ornament_source, ornament_selector))
    remaining_alpha = hair_alpha * (1 - ornament_selector)
    front_alpha = remaining_alpha * front_area
    back_alpha = remaining_alpha * (1 - front_area)
    save("hair_back", i, with_alpha(source, back_alpha))
    save("hair_front", i, with_alpha(source, front_alpha))

for i, garment in enumerate(CLOTH):
    if i == 1:
        garment = shifted(garment, 28)
    # Source alpha already excludes head and hair; clamp sparse ghost pixels
    # above the shoulder/collar height without trimming any robe geometry.
    # Remove low-alpha generation haze outside the actual garment contour.
    source_alpha = np.asarray(garment.getchannel("A"), dtype=np.float32)
    alpha = smoothstep((YY - 526) / 20) * smoothstep((source_alpha - 45) / 150)
    save("clothes", i, with_alpha(garment, alpha))


def compose(parts):
    out = Image.new("RGBA", SIZE)
    for kind, index in (
        ("hair_back", parts["hair"]), ("face", parts["face"]),
        ("clothes", parts["clothes"]), ("eye", parts["eye"]),
        ("brow", parts["brow"]), ("nose", parts["nose"]),
        ("mouth", parts["mouth"]), ("makeup", parts["makeup"]),
        ("hair_front", parts["hair"]), ("hair_accessory", parts["hair"]),
    ):
        out.alpha_composite(Image.open(OUT / f"{kind}_{index}.png").convert("RGBA"))
    return out


base = dict(face=0, brow=0, eye=0, nose=0, mouth=0, makeup=0, hair=0, clothes=0)
compose(base).save(PREVIEW / "baseline.png")
for i in range(1, 5):
    choice = {key: i for key in base}
    compose(choice).save(PREVIEW / f"variant_{i}.png")
compose(dict(face=2, brow=4, eye=1, nose=3, mouth=2, makeup=4, hair=4, clothes=1)).save(PREVIEW / "mixed_a.png")
compose(dict(face=4, brow=1, eye=3, nose=2, mouth=4, makeup=2, hair=2, clothes=3)).save(PREVIEW / "mixed_b.png")

thumb_w, thumb_h = 256, 384
sheet = Image.new("RGB", (5 * thumb_w, 2 * (thumb_h + 25)), (224, 220, 211))
draw = ImageDraw.Draw(sheet)
preview_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 16)
for j, name in enumerate(["baseline", "variant_1", "variant_2", "variant_3", "variant_4", "mixed_a", "mixed_b"]):
    pic = Image.open(PREVIEW / f"{name}.png").convert("RGBA").resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
    x, y = j % 5 * thumb_w, j // 5 * (thumb_h + 25)
    sheet.paste(pic, (x, y + 25), pic)
    caption = ["基准", "整套1", "整套2", "整套3", "整套4", "混搭甲", "混搭乙"][j]
    draw.text((x + 6, y + 3), caption, font=preview_font, fill=(35, 40, 37))
sheet.save(PREVIEW / "female_combinations.png")

part_names = ["face", "brow", "eye", "nose", "mouth", "makeup", "hair", "clothes"]
part_labels = ["脸型", "眉毛", "眼睛", "鼻子", "嘴巴", "妆容", "发型", "衣服"]
part_sheet = Image.new("RGB", (5 * 220, len(part_names) * 286), (224, 220, 211))
part_draw = ImageDraw.Draw(part_sheet)
for row, kind in enumerate(part_names):
    for option in range(5):
        parts = base.copy()
        parts[kind] = option
        portrait = compose(parts)
        if kind in ("hair", "clothes"):
            thumbnail = portrait.resize((176, 264), Image.Resampling.LANCZOS)
            px = option * 220 + 22
        else:
            thumbnail = portrait.crop((390, 270, 634, 570)).resize((208, 256), Image.Resampling.LANCZOS)
            px = option * 220 + 6
        py = row * 286 + 24
        part_sheet.paste(thumbnail, (px, py), thumbnail)
        part_draw.text((option * 220 + 6, row * 286 + 3), f"{part_labels[row]} {option + 1}", font=preview_font, fill=(35, 40, 37))
part_sheet.save(PREVIEW / "part_options.png")

# All 25 hairstyle/outfit pairs share the same female anchor and layer order.
matrix = Image.new("RGB", (5 * 220, 5 * 354), (224, 220, 211))
matrix_draw = ImageDraw.Draw(matrix)
for hair_index in range(5):
    for clothes_index in range(5):
        parts = base.copy()
        parts["hair"] = hair_index
        parts["clothes"] = clothes_index
        portrait = compose(parts).resize((220, 330), Image.Resampling.LANCZOS)
        x, y = clothes_index * 220, hair_index * 354
        matrix.paste(portrait, (x, y + 24), portrait)
        matrix_draw.text((x + 5, y + 2), f"发{hair_index + 1} 衣{clothes_index + 1}", font=preview_font, fill=(35, 40, 37))
matrix.save(PREVIEW / "hair_clothes_matrix.png")
Image.open(PREVIEW / "variant_1.png").crop((350, 180, 670, 390)).resize((960, 630), Image.Resampling.LANCZOS).save(PREVIEW / "low_bangs_detail.png")
print(f"Wrote {len(list(OUT.glob('*.png')))} female layers to {OUT}")
