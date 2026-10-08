"""Build independently selectable front and complete back hair.

The previous portrait exports remain untouched. These assets use the same
1024x1536 anchors. Buns, knots and ribbons belong to the back-hair choice.
"""
from pathlib import Path
from shutil import copy2
import gc

import cv2
import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SIZE = (1024, 1536)
YY, XX = np.indices((SIZE[1], SIZE[0]), dtype=np.float32)
MALE = ROOT / "mods/appearance/腰上男修拆件演示_v2"
FEMALE = ROOT / "mods/appearance/腰上女修拆件演示_v1"


def smoothstep(v):
    v = np.clip(v, 0, 1)
    return v * v * (3 - 2 * v)


def open_png(path):
    im = Image.open(path).convert("RGBA")
    if im.size != SIZE:
        raise ValueError(f"{path}: expected {SIZE}, got {im.size}")
    return im


def save(folder, name, im):
    im.save(folder / name, optimize=True)


def masked(im, mask):
    out = im.copy()
    out.putalpha(ImageChops.multiply(im.getchannel("A"), mask))
    return out


def mask_from_array(a):
    return Image.fromarray(np.uint8(np.clip(a, 0, 1) * 255), "L")


def polygon_mask(polygons):
    im = Image.new("L", SIZE)
    d = ImageDraw.Draw(im)
    for points in polygons:
        d.polygon(points, fill=255)
    return im


def braid_crown_silhouette():
    """Discard the broad soft matte around the braided female crown."""
    points = [(500, 101), (548, 101), (595, 114), (633, 143),
              (658, 181), (677, 233), (678, 293), (665, 348),
              (640, 403), (615, 458), (605, 512), (393, 512),
              (371, 466), (351, 413), (347, 361), (347, 306),
              (354, 251), (369, 203), (392, 157), (430, 125),
              (468, 106)]
    return polygon_mask([points]).filter(ImageFilter.GaussianBlur(1.3))


def ellipse_mask(box):
    im = Image.new("L", SIZE)
    ImageDraw.Draw(im).ellipse(box, fill=255)
    return im


def register_male_cutout(im, xscale, stops):
    src = np.asarray(im)
    dest_y = np.arange(SIZE[1], dtype=np.float32)
    src_y = np.interp(dest_y, [p[1] for p in stops], [p[0] for p in stops])
    src_y[(dest_y < stops[0][1]) | (dest_y > stops[-1][1])] = -1
    map_x = np.broadcast_to(512 + (XX - 512) / xscale, XX.shape).astype(np.float32)
    map_y = np.broadcast_to(src_y[:, None], XX.shape).astype(np.float32)
    return Image.fromarray(cv2.remap(src, map_x, map_y, cv2.INTER_NEAREST,
                                     borderMode=cv2.BORDER_CONSTANT), "RGBA")


def paint_only_hair(source):
    """Tighten a painted cutout, excluding haze and colored ribbons/ties."""
    rgba = np.asarray(source).copy()
    rgb = rgba[:, :, :3].astype(np.float32)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    lum = rgb @ np.array([.299, .587, .114], dtype=np.float32)
    neutral_hair = smoothstep((red - green + 5) / 12) * smoothstep((green - blue + 6) / 12)
    dark_hair = smoothstep((170 - lum) / 28)
    source_alpha = rgba[:, :, 3].astype(np.float32) / 255
    alpha = smoothstep((source_alpha - .26) / .64) * neutral_hair * dark_hair
    rgba[:, :, 3] = np.uint8(np.clip(alpha * 255, 0, 255))
    return Image.fromarray(rgba, "RGBA")


def cleaned_old_layer(im):
    """Discard detached one-pixel debris near lower ends without blurring."""
    rgba = np.asarray(im).copy()
    alpha = rgba[:, :, 3]
    binary = np.uint8(alpha > 35)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(binary, 8)
    for label in range(1, count):
        x, y, width, height, area = stats[label]
        if y > 490 and area < 80 and height < 45:
            alpha[labels == label] = 0
    rgba[:, :, 3] = alpha
    return Image.fromarray(rgba, "RGBA")


def remove_detached_hair_scraps(im):
    rgba = np.asarray(im).copy()
    binary = np.uint8(rgba[:, :, 3] > 30)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(binary, 8)
    if count <= 2:
        return im
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    scraps = np.zeros(binary.shape, dtype=np.uint8)
    for label in range(1, count):
        if label != largest and stats[label, cv2.CC_STAT_AREA] < 1000:
            scraps[labels == label] = 1
    scraps = cv2.dilate(scraps, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,
                                                          (7, 7))) > 0
    rgba[:, :, 3][scraps] = 0
    return Image.fromarray(rgba, "RGBA")


def strip_forehead_skin_from_front(im):
    """Front-hair swaps must not carry another portrait's forehead paint."""
    rgba = np.asarray(im).copy()
    rgb = rgba[:, :, :3].astype(np.int16)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    forehead = (XX >= 390) & (XX <= 635) & (YY >= 245) & (YY <= 345)
    skin = (red > 105) & (green > 85) & (blue > 65) & (red - blue > 12)
    selected = np.uint8(forehead & skin & (rgba[:, :, 3] > 0))
    selected = cv2.dilate(selected, cv2.getStructuringElement(cv2.MORPH_ELLIPSE,
                                                               (3, 3))) > 0
    rgba[:, :, 3][selected] = 0
    return Image.fromarray(rgba, "RGBA")


def make_back(fill_source, visible_back, allowed, omit_top_before=None):
    fill = paint_only_hair(fill_source)
    allowed_alpha = np.asarray(allowed, dtype=np.float32) / 255
    if omit_top_before is not None:
        allowed_alpha *= (YY >= omit_top_before).astype(np.float32)
    fill = masked(fill, mask_from_array(allowed_alpha))
    out = fill.copy()
    out.alpha_composite(cleaned_old_layer(visible_back))
    return out


def hidden_fill_guide(previous, index, lanes):
    """Extend hair only in regions covered by skin or robe."""
    face = np.asarray(open_png(previous / "face_0.png").getchannel("A")) > 252
    clothes = np.asarray(open_png(previous / "clothes_0.png").getchannel("A")) > 252
    lane = np.asarray(polygon_mask(lanes)) > 0
    # Painted completion is restricted to covered pixels. The visible contour
    # comes from the clean original hairstyle; this avoids gray silhouettes
    # from a different back option showing around an arbitrary front option.
    allowed = face | (lane & clothes)
    return Image.fromarray(np.uint8(allowed) * 255, "L")


def move_ribbon(front, polygons):
    """Move ribbon paint and its outline out of the independent front layer."""
    region = np.asarray(polygon_mask(polygons)) > 0
    rgb = np.asarray(front.convert("RGB"), dtype=np.float32)
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    lum = rgb @ np.array([.299, .587, .114], dtype=np.float32)
    seed = np.uint8(region & (((blue - red > 8) & (blue - green > 2)) | (lum > 160)))
    selected = cv2.dilate(seed, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))) > 0
    mask = Image.fromarray(np.uint8(selected) * 255, "L")
    return masked(front, ImageChops.invert(mask)), masked(front, mask)


def save_nonhair(previous, destination, facial_slot):
    for kind in ("face", "brow", "eye", "nose", "mouth", facial_slot, "clothes"):
        for i in range(5):
            copy2(previous / f"{kind}_{i}.png", destination / f"{kind}_{i}.png")


def compose(folder, face=0, brow=0, eye=0, nose=0, mouth=0, extra=0,
            back=0, front=0, clothes=0, extra_kind="beard"):
    out = Image.new("RGBA", SIZE)
    for kind, index in (("hair_back", back), ("face", face), ("clothes", clothes),
                        ("eye", eye), ("brow", brow), ("nose", nose),
                        ("mouth", mouth), (extra_kind, extra),
                        ("hair_front", front)):
        out.alpha_composite(open_png(folder / f"{kind}_{index}.png"))
    return out


def montage(folder, output, extra_kind, clothes, rows):
    tile_size = (245, 368)
    sheet = Image.new("RGB", (tile_size[0] * 5, tile_size[1] * rows), (215, 211, 200))
    for row in range(rows):
        for col in range(5):
            if row == 0:
                back, front = col, 0
            elif row == 1:
                back, front = 0, col
            else:
                back, front = col, 4 - col
            pic = compose(folder, back=back, front=front,
                          clothes=clothes, extra_kind=extra_kind)
            bg = Image.new("RGBA", SIZE, (215, 211, 200, 255))
            bg.alpha_composite(pic)
            tile = bg.convert("RGB").resize(tile_size, Image.Resampling.LANCZOS)
            sheet.paste(tile, (col * tile_size[0], row * tile_size[1]))
    sheet.save(output, optimize=True)


def full_front_back_matrix(folder, output, extra_kind, background=(215, 211, 200)):
    """All 25 combinations; rows are back choices, columns are front choices."""
    sheet = Image.new("RGB", (1000, 1000), background)
    for back in range(5):
        for front in range(5):
            pic = compose(folder, back=back, front=front, extra_kind=extra_kind)
            bg = Image.new("RGBA", SIZE, (*background, 255))
            bg.alpha_composite(pic)
            tile = bg.convert("RGB").crop((162, 0, 862, 700)).resize(
                (200, 200), Image.Resampling.LANCZOS)
            sheet.paste(tile, (front * 200, back * 200))
    sheet.save(output, optimize=True)


def build_male(destination=None, export_preview=True, copy_nonhair=True):
    previous = MALE / "test_assets_v6"
    destination = Path(destination) if destination is not None else MALE / "test_assets_v7"
    destination.mkdir(exist_ok=True)
    preview = MALE / "preview/independent_hair_v7"
    if export_preview:
        preview.mkdir(parents=True, exist_ok=True)
    if copy_nonhair:
        save_nonhair(previous, destination, "beard")

    registration = [
        (.82, [(30, 35), (445, 279), (1515, 1280)]),
        (.86, [(57, 116), (425, 280), (1515, 1280)]),
        (.84, [(28, 55), (460, 279), (1518, 1280)]),
        (.86, [(30, 110), (420, 280), (1525, 1280)]),
        (.90, [(30, 90), (435, 280), (1536, 1280)]),
    ]
    lower_lanes = [
        [[(290, 510), (455, 510), (435, 900), (395, 1280), (280, 1280)],
         [(580, 510), (745, 510), (810, 1120), (680, 1200), (620, 820)]],
        [[(290, 510), (450, 510), (425, 1000), (380, 1280), (280, 1280)],
         [(580, 510), (750, 510), (805, 1180), (660, 1220), (620, 820)]],
        [[(275, 500), (450, 500), (420, 1010), (380, 1300), (280, 1300)],
         [(580, 500), (760, 500), (820, 1160), (675, 1230), (620, 790)]],
        [[(290, 505), (450, 505), (425, 950), (385, 1250), (280, 1250)],
         [(580, 505), (750, 505), (810, 1140), (675, 1200), (620, 810)]],
        [[(270, 480), (460, 480), (440, 1100), (390, 1420), (275, 1420)]],
    ]
    ribbon_regions = {
        0: [[(355, 150), (425, 150), (360, 350), (270, 530), (75, 660), (70, 560), (270, 355)]],
        3: [[(350, 385), (428, 385), (350, 560), (105, 730), (105, 630)]],
        4: [[(350, 270), (425, 270), (360, 480), (160, 685), (155, 570)]],
    }
    front_locks = [
        [[(389, 238), (462, 238), (447, 415), (436, 535), (400, 550), (385, 498)],
         [(562, 238), (639, 238), (648, 495), (620, 548), (588, 520)]],
        [[(383, 235), (460, 235), (446, 445), (425, 630), (402, 694), (381, 651)],
         [(563, 235), (641, 235), (657, 445), (653, 625), (627, 688), (601, 645)]],
        [[(390, 235), (460, 235), (448, 475), (410, 554), (389, 510)],
         [(564, 235), (643, 235), (672, 560), (665, 666), (645, 699), (612, 649)]],
        [[(382, 240), (456, 240), (444, 455), (417, 627), (394, 660), (380, 610)],
         [(565, 240), (642, 240), (650, 515), (622, 562), (590, 520)]],
        [[(390, 238), (458, 238), (445, 470), (410, 515), (386, 485)],
         [(565, 238), (639, 238), (648, 488), (615, 530), (591, 495)]],
    ]
    for i, (xscale, stops) in enumerate(registration):
        original_front = open_png(previous / f"hair_front_{i}.png")
        original_back = open_png(previous / f"hair_back_{i}.png")
        front_window = polygon_mask(front_locks[i])
        ImageDraw.Draw(front_window).polygon(
            [(420, 225), (512, 210), (604, 225), (622, 284),
             (588, 310), (512, 292), (436, 310), (402, 284)], fill=255)
        cutout = register_male_cutout(open_png(MALE / "test_sources" / f"hair_cut_{i}.png"), xscale, stops)
        allowed = hidden_fill_guide(previous, i, lower_lanes[i])
        complete_visible = original_back.copy()
        # Style 3's old crown contained a mismatched forehead skin strip;
        # the clean loose crown is a better back-layer base for mixing.
        crown_for_back = open_png(previous / "hair_front_1.png") if i == 3 else original_front
        complete_visible.alpha_composite(crown_for_back)
        complete_visible.alpha_composite(open_png(previous / f"hair_accessory_{i}.png"))
        if i in ribbon_regions:
            original_front, _ = move_ribbon(original_front, ribbon_regions[i])
        back = make_back(cutout, complete_visible, allowed)
        if i == 0:
            # New loose back choice: a clean unbraided crown joins the longer
            # straight rear lengths; new painted underlayers complete the
            # sections hidden by the head and robe.
            clean_visible = original_back.copy()
            clean_alpha = clean_visible.getchannel("A")
            ImageDraw.Draw(clean_alpha).rectangle((0, 260, 265, 690), fill=0)
            clean_visible.putalpha(clean_alpha)
            loose_crown = open_png(previous / "hair_front_1.png")
            clean_visible.alpha_composite(loose_crown)
            fresh_paint = open_png(MALE / "test_sources/hair_back_unbraided_full_0.png")
            old_rgb = np.asarray(loose_crown.convert("RGB"), dtype=np.float32)
            old_lum = old_rgb @ np.array([.299, .587, .114], dtype=np.float32)
            old_alpha = np.asarray(loose_crown.getchannel("A"), dtype=np.float32) / 255
            # Transfer the newly painted dark strands through a registered,
            # crisp hair mask, leaving skin and robe paint out of the layer.
            fresh_paint.putalpha(mask_from_array(
                old_alpha * smoothstep((145 - old_lum) / 35)))
            clean_visible.alpha_composite(fresh_paint)
            del old_rgb, old_lum, old_alpha, fresh_paint
            back = make_back(open_png(MALE / "test_sources/hair_back_unbraided_front_0.png"),
                             clean_visible, allowed)
            back = remove_detached_hair_scraps(back)
            del clean_visible, clean_alpha, loose_crown
        save(destination, f"hair_back_{i}.png", back)
        front = strip_forehead_skin_from_front(original_front)
        save(destination, f"hair_front_{i}.png", cleaned_old_layer(masked(front, front_window)))
    del original_front, original_back, front_window, cutout, allowed
    del complete_visible, crown_for_back, back, front
    gc.collect()
    if export_preview:
        montage(destination, preview / "independent_matrix_white.png", "beard", 0, 3)
        montage(destination, preview / "independent_matrix_green.png", "beard", 4, 3)
        full_front_back_matrix(destination, preview / "front_back_25.png", "beard")
        full_front_back_matrix(destination, preview / "front_back_25_dark.png", "beard",
                               (42, 47, 49))


def build_female(destination=None, export_preview=True, copy_nonhair=True):
    previous = FEMALE / "test_assets_v4"
    destination = Path(destination) if destination is not None else FEMALE / "test_assets_v5"
    destination.mkdir(exist_ok=True)
    preview = FEMALE / "preview/independent_hair_v5"
    if export_preview:
        preview.mkdir(parents=True, exist_ok=True)
    if copy_nonhair:
        save_nonhair(previous, destination, "makeup")

    lower_lanes = [
        [[(290, 500), (460, 500), (445, 1160), (390, 1350), (270, 1350)],
         [(565, 500), (755, 500), (795, 1230), (650, 1300), (585, 780)]],
        [[(310, 485), (460, 485), (440, 900), (370, 1040), (295, 940)]],
        [[(575, 455), (725, 455), (865, 1230), (690, 1350), (605, 760)]],
        [[(315, 500), (455, 500), (440, 740), (340, 820)],
         [(570, 500), (715, 500), (740, 800), (590, 830)]],
        [[(290, 470), (480, 470), (465, 1230), (405, 1480), (285, 1480)]],
    ]
    ribbon_regions = {
        0: [[(348, 145), (435, 145), (383, 385), (294, 540), (170, 625), (160, 540), (270, 390)]],
    }
    front_locks = [
        [[(377, 235), (456, 235), (444, 445), (420, 548), (390, 555), (372, 505)],
         [(566, 235), (648, 235), (655, 510), (623, 555), (589, 520)]],
        [[(372, 238), (453, 238), (440, 455), (421, 620), (400, 665), (375, 625)],
         [(570, 238), (650, 238), (649, 512), (622, 548), (592, 515)]],
        [[(378, 238), (456, 238), (445, 500), (416, 552), (383, 518)],
         [(565, 238), (650, 238), (677, 580), (683, 675), (660, 718), (622, 668)]],
        [[(374, 235), (457, 235), (442, 488), (416, 584), (394, 610), (375, 565)],
         [(566, 235), (653, 235), (664, 540), (650, 588), (628, 610), (600, 555)]],
        [[(374, 248), (453, 248), (442, 480), (412, 550), (386, 522)],
         [(568, 248), (651, 248), (657, 510), (625, 554), (596, 519)]],
    ]
    for i in range(5):
        original_front = open_png(previous / f"hair_front_{i}.png")
        if i == 4:
            original_front = masked(original_front, braid_crown_silhouette())
        original_back = open_png(previous / f"hair_back_{i}.png")
        front_window = polygon_mask(front_locks[i])
        if i != 4:
            ImageDraw.Draw(front_window).polygon(
                [(420, 225), (512, 210), (604, 225), (619, 284),
                 (585, 308), (512, 290), (439, 308), (405, 284)], fill=255)
        if i == 4:
            # A separate braid cutout supplies the long hidden plait. Its
            # crown braid is cut away: the crown belongs to an accessory.
            fill_source = open_png(FEMALE / "source/hair_4_cutout.png")
        else:
            fill_source = open_png(FEMALE / "source" / f"back_fill_full_{i}.png")
        allowed = hidden_fill_guide(previous, i, lower_lanes[i])
        if i in (0, 2):
            # Top buns/tufts travel with the back choice as painted ornaments.
            ImageDraw.Draw(allowed).rectangle((0, 0, 1023, 110), fill=0)
        if i == 4:
            ImageDraw.Draw(allowed).rectangle((0, 0, 1023, 255), fill=0)
        complete_visible = original_back.copy()
        complete_visible.alpha_composite(original_front)
        complete_visible.alpha_composite(open_png(previous / f"hair_accessory_{i}.png"))
        if i in ribbon_regions:
            original_front, _ = move_ribbon(original_front, ribbon_regions[i])
        back = make_back(fill_source, complete_visible, allowed)
        save(destination, f"hair_back_{i}.png", back)
        front = strip_forehead_skin_from_front(original_front)
        save(destination, f"hair_front_{i}.png", cleaned_old_layer(masked(front, front_window)))
    del original_front, original_back, front_window, fill_source, allowed
    del complete_visible, back, front
    gc.collect()
    if export_preview:
        montage(destination, preview / "independent_matrix_white.png", "makeup", 0, 3)
        montage(destination, preview / "independent_matrix_green.png", "makeup", 4, 3)
        full_front_back_matrix(destination, preview / "front_back_25.png", "makeup")
        full_front_back_matrix(destination, preview / "front_back_25_dark.png", "makeup",
                               (42, 47, 49))


if __name__ == "__main__":
    import json
    current = ROOT / "tools/character_ink_hair_sources.json"
    if current.exists() and len(json.loads(current.read_text(encoding="utf-8"))["assets"]) == 20:
        import export_character_front_ears
        export_character_front_ears.export(install=True)
        raise SystemExit(0)
    build_male()
    build_female()
    print("Wrote independent male and female front/back hair layers and preview matrices.")
