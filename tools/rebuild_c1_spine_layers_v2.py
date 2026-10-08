from __future__ import annotations

from pathlib import Path
from typing import Iterable

import numpy as np
from PIL import Image, ImageDraw, ImageChops


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "textures" / "characters" / "c1" / "001.png"
OUT_ROOT = ROOT / "textures" / "characters" / "c1_spine_v2"
LAYER_ROOT = OUT_ROOT / "layers"
PREVIEW_ROOT = OUT_ROOT / "preview"


LAYER_ORDER = [
    "01_hair_back",
    "02_feet_shoes",
    "03_body_clothes",
    "04_sleeve_scroll",
    "05_sleeve_pointing",
    "06_waist_accessories",
    "07_head_face",
    "08_hair_front",
]


def polygon_mask(size: tuple[int, int], polygons: Iterable[Iterable[tuple[int, int]]]) -> Image.Image:
    """Draw a slightly antialiased union of polygons at the source resolution."""
    scale = 4
    hi = Image.new("L", (size[0] * scale, size[1] * scale), 0)
    draw = ImageDraw.Draw(hi)
    for polygon in polygons:
        points = [(x * scale, y * scale) for x, y in polygon]
        draw.polygon(points, fill=255)
    return hi.resize(size, Image.Resampling.LANCZOS)


def union(*masks: Image.Image) -> Image.Image:
    result = Image.new("L", masks[0].size, 0)
    for mask in masks:
        result = ImageChops.lighter(result, mask)
    return result


def subtract(base: Image.Image, *cutouts: Image.Image) -> Image.Image:
    result = base
    for cutout in cutouts:
        result = ImageChops.subtract(result, cutout)
    return result


def apply_mask(source: Image.Image, mask: Image.Image) -> Image.Image:
    layer = source.copy()
    alpha = ImageChops.multiply(source.getchannel("A"), mask)
    layer.putalpha(alpha)
    return layer


def predicate_mask(source: Image.Image, predicate) -> Image.Image:
    pixels = source.load()
    mask = Image.new("L", source.size, 0)
    output = mask.load()
    for y in range(source.height):
        for x in range(source.width):
            if predicate(pixels[x, y]):
                output[x, y] = 255
    return mask


def inpaint_rgb(source: Image.Image, region: Image.Image, holes: Image.Image) -> Image.Image:
    """Fill hidden pixels from nearby pixels inside a constrained region."""
    rgba = np.asarray(source.convert("RGBA")).copy()
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3] > 8
    region_bits = np.asarray(region) > 8
    hole_bits = (np.asarray(holes) > 8) & region_bits & alpha
    known = region_bits & alpha & ~hole_bits
    remaining = hole_bits.copy()

    # Iterative neighbour propagation keeps the hidden patch in the local cloth
    # or skin family instead of painting through the transparent silhouette.
    for _ in range(80):
        sums = np.zeros_like(rgb, dtype=np.float32)
        counts = np.zeros(known.shape, dtype=np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                sy0 = max(0, -dy)
                sy1 = min(rgb.shape[0], rgb.shape[0] - dy)
                sx0 = max(0, -dx)
                sx1 = min(rgb.shape[1], rgb.shape[1] - dx)
                ty0 = sy0 + dy
                ty1 = sy1 + dy
                tx0 = sx0 + dx
                tx1 = sx1 + dx
                source_known = known[sy0:sy1, sx0:sx1]
                sums[ty0:ty1, tx0:tx1] += rgb[sy0:sy1, sx0:sx1] * source_known[:, :, None]
                counts[ty0:ty1, tx0:tx1] += source_known
        fill_now = remaining & (counts > 0)
        if not fill_now.any():
            break
        rgb[fill_now] = sums[fill_now] / counts[fill_now][:, None]
        known[fill_now] = True
        remaining[fill_now] = False

    if remaining.any() and known.any():
        rgb[remaining] = rgb[known].mean(axis=0)
    result = Image.fromarray(np.uint8(np.clip(rgb, 0, 255)), "RGB").convert("RGBA")
    result.putalpha(source.getchannel("A"))
    return result


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    size = source.size
    if size != (720, 960):
        raise RuntimeError(f"Expected 720x960 source, got {size}")

    # The source contains one isolated opaque pixel in the top-left corner. It is
    # an export artifact, not part of the character, and must not become a Spine
    # attachment speck.
    # The character never touches the two-pixel canvas border, so clear export
    # specks there without changing any visible art.
    border = Image.new("L", size, 255)
    border_draw = ImageDraw.Draw(border)
    border_draw.rectangle((0, 0, size[0] - 1, 1), fill=0)
    border_draw.rectangle((0, size[1] - 2, size[0] - 1, size[1] - 1), fill=0)
    border_draw.rectangle((0, 0, 1, size[1] - 1), fill=0)
    border_draw.rectangle((size[0] - 2, 0, size[0] - 1, size[1] - 1), fill=0)
    source.putalpha(ImageChops.multiply(source.getchannel("A"), border))

    # These contours follow the visible character in c1/001.png. They are deliberately
    # component-sized rather than the broad rectangles used by the first draft.
    candidates: dict[str, Image.Image] = {
        "01_hair_back": polygon_mask(size, [
            [(279, 29), (319, 20), (354, 30), (379, 54), (383, 88), (367, 117),
             (337, 133), (304, 126), (280, 101), (269, 67)],
            [(291, 106), (326, 122), (330, 158), (318, 208), (307, 255),
             (295, 315), (280, 370), (260, 420), (232, 458), (204, 469),
             (187, 461), (204, 426), (220, 388), (238, 337), (250, 282),
             (263, 221), (272, 156)],
            [(282, 91), (262, 121), (248, 165), (229, 216), (204, 256),
             (211, 277), (232, 260), (254, 222), (278, 172), (299, 126)],
            [(214, 252), (198, 271), (203, 285), (222, 276), (235, 260)],
        ]),
        "02_feet_shoes": polygon_mask(size, [
            [(244, 820), (286, 820), (302, 840), (302, 843), (311, 873),
             (313, 906), (305, 934), (287, 950), (258, 951), (240, 937),
             (235, 908), (240, 878), (238, 840)],
            [(365, 820), (406, 820), (434, 835), (451, 842), (471, 865),
             (487, 878), (482, 902), (458, 915), (423, 912), (391, 904),
             (371, 888), (361, 859)],
        ]),
        "03_body_clothes": polygon_mask(size, [
            [(276, 191), (316, 204), (353, 221), (393, 220), (431, 191),
             (453, 231), (452, 291), (446, 354), (451, 421), (461, 501),
             (471, 588), (482, 674), (502, 770), (505, 801), (472, 830),
             (421, 847), (350, 854), (278, 847), (229, 827), (204, 793),
             (221, 700), (232, 602), (238, 510), (241, 427), (244, 342),
             (248, 270)],
        ]),
        "04_sleeve_scroll": polygon_mask(size, [
            [(251, 211), (286, 226), (302, 270), (303, 321), (293, 382),
             (280, 440), (284, 462), (306, 477), (322, 502), (319, 530),
             (302, 549), (279, 550), (251, 540), (224, 529), (204, 511),
             (197, 488), (207, 451), (220, 414), (226, 361), (230, 305),
             (237, 251)],
            [(205, 470), (225, 462), (249, 477), (275, 495), (303, 511),
             (321, 535), (319, 554), (302, 565), (276, 550), (249, 533),
             (221, 516), (207, 501)],
            [(219, 540), (240, 546), (246, 571), (240, 595), (228, 595),
             (222, 573)],
        ]),
        "05_sleeve_pointing": polygon_mask(size, [
            [(412, 201), (447, 211), (475, 238), (492, 270), (513, 291),
             (530, 300), (545, 284), (559, 258), (575, 239), (591, 241),
             (583, 257), (566, 273), (582, 277), (591, 291), (578, 301),
             (590, 309), (585, 322), (565, 325), (548, 344), (541, 386),
             (550, 430), (553, 470), (540, 504), (521, 520), (500, 508),
             (482, 484), (469, 446), (458, 405), (447, 368), (423, 337),
             (407, 301)],
        ]),
        "06_waist_accessories": polygon_mask(size, [
            [(286, 350), (326, 351), (367, 351), (410, 350), (449, 356),
             (458, 382), (449, 405), (413, 410), (375, 414), (333, 409),
             (296, 401)],
            [(348, 382), (378, 369), (408, 379), (416, 399), (406, 423),
             (383, 433), (356, 419)],
            [(359, 407), (381, 408), (388, 619), (378, 656), (360, 646)],
            [(389, 408), (413, 413), (432, 634), (416, 661), (399, 612)],
        ]),
        "07_head_face": polygon_mask(size, [
            [(348, 95), (388, 88), (418, 95), (434, 116), (432, 157),
             (424, 192), (406, 219), (385, 235), (358, 227), (342, 205),
             (336, 177), (338, 142)],
            [(348, 212), (390, 214), (407, 250), (397, 273), (378, 283),
             (354, 262)],
        ]),
        "08_hair_front": polygon_mask(size, [
            [(320, 72), (350, 60), (383, 65), (409, 80), (430, 98),
             (447, 120), (442, 143), (428, 128), (417, 153), (406, 168),
             (395, 136), (386, 180), (369, 157), (360, 207), (344, 181),
             (335, 150), (323, 132)],
            [(412, 124), (438, 137), (439, 176), (425, 209), (406, 235),
             (394, 228), (408, 185)],
            [(314, 119), (332, 143), (338, 181), (332, 214), (318, 206),
             (319, 165)],
        ]),
    }

    # Keep the broad underlay regions before the visible component masks are
    # color-filtered. These regions are used for hidden-pixel backfill beneath
    # moving sleeves, hair, waist ribbons, and the face.
    body_region = candidates["03_body_clothes"].copy()
    head_region = candidates["07_head_face"].copy()

    # Hair and face contours intentionally cross the robe boundary in the source
    # image. Keep the hair's dark ink/paint values and the muted ribbon values, and
    # keep the face's light values; this removes the beige robe wedges that made the
    # first draft visibly unusable when rotated.
    hair_paint = predicate_mask(
        source,
        lambda rgba: (
            (rgba[0] * 0.30 + rgba[1] * 0.59 + rgba[2] * 0.11) <= 155
            or (rgba[1] >= rgba[0] - 8 and rgba[2] >= rgba[0] - 24 and
                rgba[0] * 0.30 + rgba[1] * 0.59 + rgba[2] * 0.11 <= 185)
        ),
    )
    face_paint = predicate_mask(
        source,
        lambda rgba: rgba[0] * 0.30 + rgba[1] * 0.59 + rgba[2] * 0.11 >= 145,
    )
    candidates["01_hair_back"] = ImageChops.multiply(candidates["01_hair_back"], hair_paint)
    candidates["08_hair_front"] = ImageChops.multiply(candidates["08_hair_front"], hair_paint)
    candidates["07_head_face"] = ImageChops.multiply(candidates["07_head_face"], face_paint)

    # Remove component candidates from the clothing base so moving an arm or head
    # cannot leave a second frozen copy underneath it.
    component_cutouts = union(
        candidates["01_hair_back"],
        candidates["02_feet_shoes"],
        candidates["04_sleeve_scroll"],
        candidates["05_sleeve_pointing"],
        candidates["06_waist_accessories"],
        candidates["07_head_face"],
        candidates["08_hair_front"],
    )
    candidates["03_body_clothes"] = subtract(candidates["03_body_clothes"], component_cutouts)

    # Preserve every original visible pixel at rest. Residuals are assigned to the
    # clothing base only inside the character silhouette; this prevents accidental
    # holes from hard polygon edges while keeping the deliberately separated regions.
    candidate_union = union(*candidates.values())
    residual = ImageChops.subtract(source.getchannel("A"), candidate_union)

    # Route only obvious missed component pixels into their component. The rest
    # belongs to the robe base. This avoids reintroducing stray hair/face pixels
    # into the body layer merely because a hand-drawn contour missed an antialias
    # fringe.
    residual_routes = {name: Image.new("L", size, 0) for name in LAYER_ORDER}
    source_pixels = source.load()
    route_pixels = {name: residual_routes[name].load() for name in LAYER_ORDER}
    residual_pixels = residual.load()
    for y in range(size[1]):
        for x in range(size[0]):
            if residual_pixels[x, y] <= 8:
                continue
            rgba = source_pixels[x, y]
            luma = rgba[0] * 0.30 + rgba[1] * 0.59 + rgba[2] * 0.11
            target = "03_body_clothes"
            if y >= 830 and ((220 <= x <= 320) or (350 <= x <= 500)) and luma <= 180:
                target = "02_feet_shoes"
            elif y < 80 and x < 410:
                target = "01_hair_back"
            elif y < 245 and x < 360 and luma <= 155:
                target = "01_hair_back"
            elif y < 230 and 310 <= x <= 460 and luma <= 155:
                target = "08_hair_front"
            elif 75 <= y < 275 and 320 <= x <= 440 and luma >= 145:
                target = "07_head_face"
            route_pixels[target][x, y] = residual_pixels[x, y]

    routed = Image.new("L", size, 0)
    for name, route in residual_routes.items():
        if name == "03_body_clothes":
            continue
        candidates[name] = ImageChops.lighter(candidates[name], route)
        routed = ImageChops.lighter(routed, route)
    residual_for_body = ImageChops.subtract(residual, routed)
    candidates["03_body_clothes"] = ImageChops.lighter(
        candidates["03_body_clothes"], residual_for_body
    )

    # Build real hidden backfills instead of leaving transparent holes beneath
    # moving attachments. The visible component is still drawn above these pixels
    # at rest, but rotation now reveals painted cloth/skin rather than black.
    body_hidden_components = union(
        candidates["04_sleeve_scroll"],
        candidates["05_sleeve_pointing"],
        candidates["06_waist_accessories"],
        candidates["07_head_face"],
    )
    body_backfill = inpaint_rgb(source, body_region, body_hidden_components)
    body_alpha = ImageChops.multiply(source.getchannel("A"), body_region)
    head_holes = candidates["08_hair_front"]
    head_backfill = inpaint_rgb(source, head_region, head_holes)
    head_alpha = ImageChops.multiply(source.getchannel("A"), head_region)

    LAYER_ROOT.mkdir(parents=True, exist_ok=True)
    PREVIEW_ROOT.mkdir(parents=True, exist_ok=True)

    layers: dict[str, Image.Image] = {}
    for name in LAYER_ORDER:
        if name == "03_body_clothes":
            layer = apply_mask(body_backfill, body_alpha)
        elif name == "07_head_face":
            layer = apply_mask(head_backfill, head_alpha)
        else:
            layer = apply_mask(source, candidates[name])
        layers[name] = layer

    def compose() -> Image.Image:
        result = Image.new("RGBA", size, (0, 0, 0, 0))
        for layer_name in LAYER_ORDER:
            result = Image.alpha_composite(result, layers[layer_name])
        return result

    # At rest, every visible source pixel must remain exact. If a contour missed
    # an antialiased edge, put that original pixel back into the topmost logical
    # component rather than accepting a seam or a color patch in the rest pose.
    source_array = np.asarray(source.convert("RGBA"))
    target_masks = {
        "01_hair_back": candidates["01_hair_back"],
        "02_feet_shoes": candidates["02_feet_shoes"],
        "03_body_clothes": body_region,
        "04_sleeve_scroll": candidates["04_sleeve_scroll"],
        "05_sleeve_pointing": candidates["05_sleeve_pointing"],
        "06_waist_accessories": candidates["06_waist_accessories"],
        "07_head_face": head_region,
        "08_hair_front": candidates["08_hair_front"],
    }
    target_map = np.full((size[1], size[0]), -1, dtype=np.int16)
    for index, name in enumerate(LAYER_ORDER):
        target_map[np.asarray(target_masks[name]) > 8] = index

    for _ in range(2):
        composite = compose()
        composite_array = np.asarray(composite.convert("RGBA"))
        mismatch = (
            np.any(np.abs(source_array.astype(np.int16) - composite_array.astype(np.int16)) > 3, axis=2)
            & (source_array[:, :, 3] > 8)
        )
        if not mismatch.any():
            break
        for index, name in enumerate(LAYER_ORDER):
            correction = mismatch & (target_map == index)
            if not correction.any():
                continue
            layer_array = np.asarray(layers[name].convert("RGBA")).copy()
            layer_array[correction] = source_array[correction]
            layers[name] = Image.fromarray(layer_array, "RGBA")

    composite = compose()
    stats: list[str] = []
    for name in LAYER_ORDER:
        layer = layers[name]
        layer.save(LAYER_ROOT / f"{name}.png")
        alpha = layer.getchannel("A")
        bbox = alpha.getbbox()
        pixels = sum(1 for value in alpha.getdata() if value > 8)
        stats.append(f"{name}: bbox={bbox}, visible_pixels={pixels}")

    composite.save(PREVIEW_ROOT / "rest_pose_composite.png")
    diff = ImageChops.difference(source, composite)
    diff.save(PREVIEW_ROOT / "rest_pose_diff.png")

    # Render representative poses with the same pivot locations used by the
    # imported Spine skeleton. This is a lightweight visual smoke test for layer
    # separation; the Spine project remains the source of truth for animation.
    bone_setup = {
        "root": (None, 0, 0, 0),
        "hips": ("root", 0, -200, 0),
        "torso": ("hips", 0, 260, 0),
        "neck": ("torso", 10, 180, 0),
        "head": ("neck", 5, 95, 0),
        "hair_back": ("head", -35, -5, 0),
        "hair_front": ("head", 0, 0, 0),
        "arm_scroll": ("torso", -110, -50, 0),
        "arm_pointing": ("torso", 95, 140, 0),
        "waist": ("hips", 0, 290, 0),
        "feet": ("root", 0, -370, 0),
    }
    layer_bones = {
        "01_hair_back": "hair_back",
        "02_feet_shoes": "feet",
        "03_body_clothes": "torso",
        "04_sleeve_scroll": "arm_scroll",
        "05_sleeve_pointing": "arm_pointing",
        "06_waist_accessories": "waist",
        "07_head_face": "head",
        "08_hair_front": "hair_front",
    }

    def global_bones(pose: dict[str, tuple[float, float, float]]) -> dict[str, tuple[float, float, float]]:
        resolved: dict[str, tuple[float, float, float]] = {}

        def resolve(name: str) -> tuple[float, float, float]:
            if name in resolved:
                return resolved[name]
            parent, local_x, local_y, local_angle = bone_setup[name]
            dx, dy, da = pose.get(name, (0, 0, 0))
            local_x += dx
            local_y += dy
            local_angle += da
            if parent is None:
                result = (local_x, local_y, local_angle)
            else:
                px, py, pa = resolve(parent)
                radians = pa * 3.141592653589793 / 180.0
                cos_a, sin_a = __import__("math").cos(radians), __import__("math").sin(radians)
                result = (
                    px + local_x * cos_a - local_y * sin_a,
                    py + local_x * sin_a + local_y * cos_a,
                    pa + local_angle,
                )
            resolved[name] = result
            return result

        for name in bone_setup:
            resolve(name)
        return resolved

    def render_pose(name: str, pose: dict[str, tuple[float, float, float]]) -> None:
        rendered = Image.new("RGBA", size, (0, 0, 0, 0))
        transforms = global_bones(pose)
        for layer_name in LAYER_ORDER:
            bone_name = layer_bones[layer_name]
            gx, gy, angle = transforms[bone_name]
            pivot = (360 + gx, 480 - gy)
            layer = Image.open(LAYER_ROOT / f"{layer_name}.png").convert("RGBA")
            transformed = layer.rotate(-angle, resample=Image.Resampling.BICUBIC,
                                        expand=False, center=pivot)
            rendered = Image.alpha_composite(rendered, transformed)
        rendered.save(PREVIEW_ROOT / f"{name}.png")

    render_pose("idle_mid", {
        "torso": (0, 2, 0.45), "head": (0, 0, -0.7),
        "hair_back": (0, 0, 1.5), "hair_front": (0, 0, -0.8),
        "waist": (0, 0, 0.6),
    })
    render_pose("attack_mid", {
        "torso": (0, 0, 2.9), "arm_pointing": (0, 0, 9),
        "arm_scroll": (0, 0, 4), "hair_front": (0, 0, -2),
    })
    render_pose("hurt_hit", {
        "torso": (0, 0, -5), "head": (0, 0, 6),
        "arm_pointing": (0, 0, 6),
    })
    render_pose("death_kneel_end", {
        "hips": (0, -12, 0), "torso": (0, 0, -8),
        "head": (0, 0, 10), "arm_scroll": (0, 0, -8),
        "arm_pointing": (0, 0, 10), "hair_front": (0, 0, 4),
    })

    # A light background makes individual alpha cutouts inspectable; the source
    # and runtime layers themselves remain transparent PNGs.
    tile_w, tile_h = 360, 480
    sheet = Image.new("RGBA", (tile_w * 4, tile_h * 2), (186, 194, 202, 255))
    for index, name in enumerate(LAYER_ORDER):
        tile = Image.open(LAYER_ROOT / f"{name}.png").convert("RGBA")
        tile.thumbnail((tile_w, tile_h), Image.Resampling.LANCZOS)
        x = (index % 4) * tile_w
        y = (index // 4) * tile_h
        sheet.alpha_composite(tile, (x, y))
    sheet.save(PREVIEW_ROOT / "layers_contact_sheet.png")

    (OUT_ROOT / "BUILD_REPORT.txt").write_text(
        "Source: textures/characters/c1/001.png\n"
        "Canvas: 720x960 RGBA\n"
        "Layer order: " + " -> ".join(LAYER_ORDER) + "\n"
        "Backfill: constrained cloth underlay + head/neck underlay\n"
        "Pose previews: idle_mid, attack_mid, hurt_hit, death_kneel_end\n"
        + "\n".join(stats) + "\n"
        + f"Residual bbox before fallback: {residual.getbbox()}\n",
        encoding="utf-8",
    )
    print("Generated", len(LAYER_ORDER), "layers in", LAYER_ROOT)
    print("Residual bbox before fallback:", residual.getbbox())


if __name__ == "__main__":
    main()
