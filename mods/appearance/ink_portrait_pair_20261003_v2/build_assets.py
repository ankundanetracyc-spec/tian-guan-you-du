"""Rebuild the new male/female portraits as nine interchangeable PNG slots.

Only the two newly generated full portraits and the user's bald references are
inputs.  The older in-project portrait packs are deliberately never read.
"""

from __future__ import annotations

import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
V1 = ROOT.parent / "ink_portrait_pair_20261003_v1"
HEIGHT, WIDTH = 1536, 1024
SIZE = (HEIGHT, WIDTH)
ORDER = (
    "hair_back",
    "face_shape",
    "clothing",
    "eyes",
    "eyebrows",
    "nose",
    "mouth",
    "face_accessory",
    "hair_front",
)


def read_image(path: Path) -> np.ndarray:
    data = np.asarray(Image.open(path).convert("RGBA"), dtype=np.uint8).copy()
    if data.shape != (HEIGHT, WIDTH, 4):
        raise ValueError(f"Unexpected canvas: {path}: {data.shape}")
    return data


def save_image(path: Path, data: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    clean = data.astype(np.uint8).copy()
    clean[:, :, :3][clean[:, :, 3] == 0] = 0
    Image.fromarray(clean, "RGBA").save(path)


def repair_eye_highlights(male: np.ndarray) -> np.ndarray:
    """Paint two restrained, mirrored warm eyelid glints on an even skin base."""
    result = male.copy()
    yy, xx = np.indices(SIZE)
    # The source's one strong white spot is replaced with the corresponding
    # unmarked patch from the other eyelid; this cannot pull in the black lash.
    right_patch = np.exp(-0.5 * (((xx - 567) / 5.2) ** 2 + ((yy - 361) / 2.8) ** 2))
    right_patch[right_patch < 0.01] = 0
    mirror_x = np.clip(1033 - xx, 0, WIDTH - 1)
    mirrored_skin = male[yy, mirror_x, :3].astype(np.float32)
    result[:, :, :3] = np.clip(
        result[:, :, :3].astype(np.float32) * (1 - right_patch[:, :, None])
        + mirrored_skin * right_patch[:, :, None], 0, 255
    ).astype(np.uint8)
    strength = np.zeros(SIZE, np.float32)
    for x in (466, 567):
        strength += np.exp(-0.5 * (((xx - x) / 2.4) ** 2 + ((yy - 360) / 1.3) ** 2))
    strength = np.minimum(strength, 1.0)[:, :, None]
    warm_lift = np.array([7.0, 6.0, 5.0], np.float32)
    result[:, :, :3] = np.clip(
        result[:, :, :3].astype(np.float32) + strength * warm_lift, 0, 255
    ).astype(np.uint8)
    return result


def skin_candidate(image: np.ndarray) -> np.ndarray:
    rgb = image[:, :, :3].astype(np.int16)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    return (
        (image[:, :, 3] > 80)
        & (r > 105)
        & (r - g > 10)
        & (g - b > 3)
        & (r - b > 18)
    )


def face_geometry(bald: np.ndarray, sex: str) -> tuple[np.ndarray, np.ndarray]:
    skin = skin_candidate(bald).astype(np.uint8)
    skin[:130, :] = 0
    skin[680:, :] = 0
    skin[:, :340] = 0
    skin[:, 700:] = 0
    skin = cv2.morphologyEx(skin, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    n, labels, _, _ = cv2.connectedComponentsWithStats(skin, connectivity=8)
    if n <= 1 or labels[300, 512] == 0:
        raise ValueError("Cannot find the bald reference's face")
    connected = (labels == labels[300, 512]).astype(np.uint8)
    # Fill tiny feature holes only in the head. The collar begins below it.
    upper = connected.copy()
    upper[520:, :] = 0
    contours, _ = cv2.findContours(upper, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    upper[:] = 0
    cv2.drawContours(upper, contours, -1, 1, cv2.FILLED)
    rgb = bald[:, :, :3].astype(np.int16)
    rg_min, gb_min = (20, 14) if sex == "male" else (17, 10)
    neck = (
        connected.astype(bool)
        & ((rgb[:, :, 0] - rgb[:, :, 1]) > rg_min)
        & ((rgb[:, :, 1] - rgb[:, :, 2]) > gb_min)
    ).astype(np.uint8)
    neck[:510, :] = 0
    neck = cv2.morphologyEx(neck, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    yy, xx = np.indices(SIZE)
    neck_polygon = np.zeros(SIZE, np.uint8)
    if sex == "male":
        outline = [(426, 505), (607, 505), (590, 575), (573, 610),
                   (544, 653), (516, 683), (486, 653), (459, 610), (441, 575)]
    else:
        outline = [(441, 505), (593, 505), (582, 575), (565, 610),
                   (540, 651), (513, 679), (486, 651), (460, 610), (446, 575)]
    cv2.fillPoly(neck_polygon, [np.array(outline, np.int32)], 1)
    neck &= neck_polygon
    neck[(yy >= 595) & (rgb[:, :, 0] < 170)] = 0
    visible = (upper | neck) > 0
    # A concealed extension makes the neck reusable with another collar.
    extension = np.zeros(SIZE, np.uint8)
    cv2.fillPoly(
        extension,
        [np.array([(443, 565), (591, 565), (578, 614), (548, 655),
                   (516, 690), (484, 655), (454, 614)], np.int32)],
        1,
    )
    visible &= bald[:, :, 3] > 8
    underlay = visible | ((extension > 0) & (yy >= 595))
    return visible, underlay


def hair_geometry(master: np.ndarray, bald: np.ndarray, sex: str) -> tuple[np.ndarray, np.ndarray]:
    rgb = master[:, :, :3].astype(np.int16)
    old = bald[:, :, :3].astype(np.int16)
    high = rgb.max(axis=2)
    low = rgb.min(axis=2)
    distance = np.sqrt(np.sum((rgb - old).astype(np.float32) ** 2, axis=2))
    yy, xx = np.indices(SIZE)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    neutral_dark = (
        (high < 174)
        & (np.abs(r - g) < 12)
        & (np.abs(g - b) < 12)
        & (r >= g - 3)
    )
    crown = (yy >= 120) & (yy < 340) & (xx > 330) & (xx < 710)
    side = (yy >= 300) & (yy < 1080) & (xx > 245) & (xx < 785)
    near_bald = bald[:, :, 3] > 80
    change = distance > (43 if sex == "male" else 38)
    candidate = neutral_dark & (
        (crown & (distance > 32))
        | (side & ((change & (((xx < 432) | (xx > 610)) | (yy < 365))) | ~near_bald))
    )
    forehead_hair = (
        (yy >= 180) & (yy < 332) & (xx > 350) & (xx < 685)
        & (high < 210) & (distance > 25)
    )
    temple_hair = (
        (yy >= 330) & (yy < 465) & (xx > 365) & (xx < 668)
        & ((xx < 442) | (xx > 594)) & (high < 130) & (distance > 75)
    )
    candidate |= forehead_hair | temple_hair
    temple_zone = (yy >= 330) & (yy < 465) & ((xx < 445) | (xx > 592))
    candidate[temple_zone & (distance < 75)] = False
    candidate &= master[:, :, 3] >= 70
    candidate = cv2.morphologyEx(candidate.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    candidate = candidate.astype(bool)
    # Crown and strands painted across the supplied bald art are front hair.
    front = candidate & near_bald
    ear_zone = (
        (yy >= 340) & (yy < 450)
        & (((xx >= 360) & (xx < 436)) | ((xx > 600) & (xx <= 675)))
    )
    front[ear_zone & ~neutral_dark] = False
    count, labels, stats, centers = cv2.connectedComponentsWithStats(front.astype(np.uint8), 8)
    keep = np.zeros(count, bool)
    for part in range(1, count):
        x, y, width, height, area = stats[part]
        cx = centers[part, 0]
        keep[part] = (
            (y < 380 and area > 70)
            or (
                area > 55 and height > 18 and width < 90
                and ((350 <= cx <= 440) or (605 <= cx <= 705))
            )
        )
    front &= keep[labels]
    front |= candidate & crown & near_bald
    # Hair beyond the old face/robe silhouette is behind both.
    back = candidate & ~near_bald
    # Keep translucent locks around the ears even when their warm mixed
    # edge pixels are too bright for the neutral-dark hair color test.
    back |= (
        (master[:, :, 3] >= 50)
        & (yy >= 300) & (yy < 560)
        & (xx > 340) & (xx < 700)
        & ~near_bald
    )
    return back, front


def feature_regions(sex: str) -> dict[str, np.ndarray]:
    shift = 0 if sex == "male" else 2
    masks = {name: np.zeros(SIZE, np.uint8) for name in ("eyes", "eyebrows", "nose", "mouth")}
    for x in (460, 574):
        cv2.ellipse(masks["eyes"], (x, 379 + shift), (58, 23), 0, 0, 360, 255, -1)
    for x in (458, 576):
        cv2.ellipse(masks["eyebrows"], (x, 350 + shift), (57, 15), 0, 0, 360, 255, -1)
    cv2.ellipse(masks["nose"], (517, 444 + shift), (37, 39), 0, 0, 360, 255, -1)
    cv2.ellipse(masks["mouth"], (518, 488 + shift), (49, 19), 0, 0, 360, 255, -1)
    # The long temple locks are owned by the front-hair slot, not the eyes
    # and brows even where the source art crosses the outer corners.
    for name in ("eyes", "eyebrows"):
        masks[name][:, :424] = 0
        masks[name][:, 611:] = 0
    masks["nose"][masks["mouth"] > 0] = 0
    masks["eyebrows"][masks["eyes"] > 0] = 0
    return masks


def features_and_base(master: np.ndarray, sex: str) -> tuple[dict[str, np.ndarray], np.ndarray]:
    regions = feature_regions(sex)
    base = master[:, :, :3].copy()
    features: dict[str, np.ndarray] = {}
    # Move clean skin texture from the nearest matching facial plane. This
    # avoids OpenCV inpainting pulling black lash or brow ink into the base.
    skin_source_offset = {"eyebrows": -43, "eyes": 58, "nose": -130, "mouth": -175}
    for name in ("eyebrows", "eyes", "nose", "mouth"):
        roi = regions[name]
        offset = skin_source_offset[name]
        sample_y = np.clip(np.arange(HEIGHT) + offset, 0, HEIGHT - 1)
        clean_skin = master[sample_y, :, :3].copy()
        ys, xs = np.where(roi > 0)
        source_pixels = clean_skin[ys, xs]
        valid_skin = (
            (source_pixels[:, 0] > 185)
            & ((source_pixels[:, 0].astype(np.int16) - source_pixels[:, 1]) > 11)
            & ((source_pixels[:, 1].astype(np.int16) - source_pixels[:, 2]) > 7)
        )
        if np.any(valid_skin):
            fallback = np.median(source_pixels[valid_skin], axis=0).astype(np.uint8)
            clean_skin[ys[~valid_skin], xs[~valid_skin]] = fallback
        inset = cv2.distanceTransform((roi > 0).astype(np.uint8), cv2.DIST_L2, 3)
        blend = np.clip(inset / 4.0, 0, 1)[:, :, None]
        cleaned = np.clip(
            base.astype(np.float32) * (1 - blend) + clean_skin.astype(np.float32) * blend,
            0, 255,
        ).astype(np.uint8)
        difference = np.max(np.abs(base.astype(np.int16) - cleaned.astype(np.int16)), axis=2)
        threshold = {"eyebrows": 23, "eyes": 20, "nose": 16, "mouth": 15}[name]
        semantic = ((difference > threshold) & (roi > 0)).astype(np.uint8)
        semantic = cv2.dilate(semantic, np.ones((3, 3), np.uint8))
        feather = cv2.GaussianBlur(semantic.astype(np.float32), (0, 0), 0.7)
        feather[semantic > 0] = 1.0
        layer = np.zeros_like(master)
        layer[:, :, :3] = master[:, :, :3]
        layer[:, :, 3] = np.clip(feather * 255, 0, 255).astype(np.uint8)
        features[name] = layer
        base[semantic > 0] = cleaned[semantic > 0]
    return features, base


def composite(layers: dict[str, np.ndarray]) -> np.ndarray:
    result = Image.new("RGBA", (WIDTH, HEIGHT))
    for name in ORDER:
        result = Image.alpha_composite(result, Image.fromarray(layers[name], "RGBA"))
    return np.asarray(result)


def on_background(image: np.ndarray, color: str) -> np.ndarray:
    back = Image.new("RGBA", (WIDTH, HEIGHT), color)
    back.alpha_composite(Image.fromarray(image, "RGBA"))
    return np.asarray(back.convert("RGB"))


def save_parts_contact(sex: str, layers: dict[str, np.ndarray]) -> None:
    sheet = Image.new("RGB", (3 * 340, 3 * 470), "#d8d2c9")
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(ORDER):
        column, row = index % 3, index // 3
        origin = (column * 340, row * 470)
        tile = Image.new("RGBA", (320, 420), "#ece7df")
        layer = Image.fromarray(layers[name], "RGBA")
        extent = layer.getchannel("A").getbbox()
        if extent:
            left, top, right, bottom = extent
            crop = layer.crop((max(0, left - 8), max(0, top - 8),
                               min(WIDTH, right + 8), min(HEIGHT, bottom + 8)))
            crop.thumbnail((300, 396), Image.Resampling.LANCZOS)
            tile.alpha_composite(crop, ((320 - crop.width) // 2, (420 - crop.height) // 2))
        sheet.paste(tile.convert("RGB"), (origin[0] + 10, origin[1] + 35))
        label = name + (" (empty)" if extent is None else "")
        draw.text((origin[0] + 12, origin[1] + 10), f"{sex} / {label}", fill="#25211e")
    sheet.save(ROOT / "preview" / f"{sex}_nine_parts.png")


def make_layers(sex: str, master: np.ndarray, bald: np.ndarray) -> tuple[dict[str, np.ndarray], np.ndarray]:
    visible_face, face_underlay = face_geometry(bald, sex)
    back_mask, front_mask = hair_geometry(master, bald, sex)
    features, featureless_rgb = features_and_base(master, sex)

    # Drop the generator's low-alpha ambient haze while retaining real wisps.
    valid = (bald[:, :, 3] > 75) | back_mask | front_mask
    master = master.copy()
    master[:, :, 3][~valid] = 0
    master[:, :, :3][master[:, :, 3] == 0] = 0

    face = np.zeros_like(master)
    face[visible_face] = bald[visible_face]
    source_skin = np.median(bald[296:326, 493:541, :3].reshape(-1, 3), axis=0)
    target_skin = np.median(master[296:326, 493:541, :3].reshape(-1, 3), axis=0)
    skin_delta = target_skin - source_skin
    shifted_face = np.clip(face[:, :, :3].astype(np.float32) + skin_delta, 0, 255).astype(np.uint8)
    face[:, :, :3][visible_face] = shifted_face[visible_face]
    hidden_neck = face_underlay & ~visible_face
    neck_tone = np.array([208, 180, 163] if sex == "male" else [222, 200, 184], np.uint8)
    continuation = np.empty((HEIGHT, WIDTH, 3), np.uint8)
    continuation[:] = neck_tone
    face[hidden_neck, :3] = continuation[hidden_neck]
    face[hidden_neck, 3] = 255
    # Visible face uses the full new art; only features are removed.
    exposed_face = visible_face & (master[:, :, 3] > 0) & ~front_mask & ~back_mask
    face[exposed_face] = master[exposed_face]
    face[:, :, :3][exposed_face] = featureless_rgb[exposed_face]

    clothing = np.zeros_like(master)
    clothing_mask = (bald[:, :, 3] > 0) & ~visible_face
    clothing[clothing_mask] = bald[clothing_mask]
    exposed_clothing = (master[:, :, 3] > 0) & ~visible_face & ~front_mask & ~back_mask
    clothing[exposed_clothing] = master[exposed_clothing]

    # Complete concealed rear hair. Directional irregular ink grain and a
    # feathered organic outline avoid a flat placeholder slab when isolated.
    hair_back = np.zeros_like(master)
    yy, xx = np.indices(SIZE)
    widths = np.interp(
        yy,
        [145, 200, 290, 430, 600, 780, 950, 1060, 1100],
        [0, 95, 135, 150, 175, 205, 195, 145, 0],
    )
    centerline = 516 + 4 * np.sin(yy / 115.0)
    organic_edge = 5 * np.sin(yy / 27.0 + xx / 80.0) + 2 * np.sin(yy / 11.0)
    depth = widths + organic_edge - np.abs(xx - centerline)
    hidden = np.clip(depth / 9.0, 0, 1)
    hidden *= np.clip((1100 - yy) / 48.0, 0, 1)
    hidden[(yy < 145) | (master[:, :, 3] < 80)] = 0
    hidden[(master[:, :, 3] < 250) & (bald[:, :, 3] > 0)] = 0
    rng = np.random.default_rng(20261003 if sex == "male" else 20261004)
    grain = rng.standard_normal(SIZE, dtype=np.float32)
    strands = cv2.GaussianBlur(grain, (0, 0), 2.2, 24)
    strands /= max(float(np.std(strands)), 0.001)
    broad = cv2.GaussianBlur(grain, (0, 0), 18, 65)
    broad /= max(float(np.std(broad)), 0.001)
    ink = np.clip(45 + 6 * strands + 3 * broad, 26, 72).astype(np.uint8)
    hair_back[:, :, :3] = np.stack((ink + 2, ink, ink - 2), axis=2)
    hair_back[:, :, 3] = np.clip(hidden * 255, 0, 255).astype(np.uint8)
    hair_back[back_mask] = master[back_mask]

    hair_front = np.zeros_like(master)
    hair_front[front_mask] = master[front_mask]
    # Pixels already blended with the original bald art become opaque when
    # placed over their independent face/clothes layer.
    hair_front[:, :, 3][front_mask] = 255

    # The two available outfits intentionally have no face adornment.
    layers = {
        "hair_back": hair_back,
        "face_shape": face,
        "clothing": clothing,
        "eyes": features["eyes"],
        "eyebrows": features["eyebrows"],
        "nose": features["nose"],
        "mouth": features["mouth"],
        "face_accessory": np.zeros_like(master),
        "hair_front": hair_front,
    }
    # Only face pixels may carry the facial feature overlays.
    for name in ("eyes", "eyebrows", "nose", "mouth"):
        layers[name][:, :, 3][~visible_face] = 0
    # The generated masters have semi-transparent edge strokes. Resolve the
    # few pixels whose underpaint changes their composited appearance by
    # assigning each edge pixel to exactly one of the nine existing parts.
    first_pass = composite(layers)
    source_light = on_background(master, "#e8e3dc").astype(np.int16)
    result_light = on_background(first_pass, "#e8e3dc").astype(np.int16)
    edge_error = (
        np.max(np.abs(source_light - result_light), axis=2) > 12
    ) | (np.abs(master[:, :, 3].astype(np.int16) - first_pass[:, :, 3]) > 5)
    edge_error = cv2.dilate(edge_error.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    if np.any(edge_error):
        owner = np.full(SIZE, ORDER.index("clothing"), np.uint8)
        owner[visible_face] = ORDER.index("face_shape")
        owner[bald[:, :, 3] < 80] = ORDER.index("hair_back")
        owner[front_mask] = ORDER.index("hair_front")
        master_rgb = master[:, :, :3].astype(np.int16)
        bald_rgb = bald[:, :, :3].astype(np.int16)
        hair_like = (
            (np.max(master_rgb, axis=2) < 150)
            & (np.linalg.norm(master_rgb - bald_rgb, axis=2) > 60)
            & (yy < 450) & (xx > 350) & (xx < 685)
        )
        owner[hair_like] = ORDER.index("hair_front")
        for part in ("eyes", "eyebrows", "nose", "mouth"):
            owner[features[part][:, :, 3] > 0] = ORDER.index(part)
        for layer in layers.values():
            layer[edge_error] = 0
        for index, name in enumerate(ORDER):
            selected = edge_error & (owner == index)
            layers[name][selected] = master[selected]
    return layers, master


def main() -> None:
    (ROOT / "preview").mkdir(parents=True, exist_ok=True)
    report = {}
    light_previews = []
    for sex in ("male", "female"):
        source = read_image(V1 / f"{sex}_portrait.png")
        bald = read_image(V1 / "reference" / f"{sex}_bald_user.png")
        if sex == "male":
            source = repair_eye_highlights(source)
        layers, master = make_layers(sex, source, bald)
        for name, layer in layers.items():
            save_image(ROOT / sex / f"{name}.png", layer)
            if name in ("hair_back", "face_shape", "clothing", "hair_front"):
                Image.fromarray(on_background(layer, "#e5e0d9"), "RGB").save(
                    ROOT / "preview" / f"{sex}_{name}_light.png"
                )
        save_parts_contact(sex, layers)
        save_image(ROOT / f"{sex}_portrait.png", master)
        rebuilt = composite(layers)
        save_image(ROOT / "preview" / f"{sex}_recomposed.png", rebuilt)
        delta = np.abs(master.astype(np.int16) - rebuilt.astype(np.int16))
        backgrounds = {}
        for name, color in (("light", "#e8e3dc"), ("dark", "#252423")):
            original_rgb = on_background(master, color)
            rebuilt_rgb = on_background(rebuilt, color)
            visible_delta = np.abs(
                original_rgb.astype(np.int16) - rebuilt_rgb.astype(np.int16)
            )
            backgrounds[name] = {
                "mean_channel_difference": round(float(visible_delta.mean()), 4),
                "pixels_with_difference_over_12": int(
                    np.count_nonzero(np.any(visible_delta > 12, axis=2))
                ),
            }
            Image.fromarray(rebuilt_rgb, "RGB").save(
                ROOT / "preview" / f"{sex}_{name}.png"
            )
            if name == "light":
                light_previews.append(Image.fromarray(rebuilt_rgb, "RGB"))
        report[sex] = {
            "canvas": [WIDTH, HEIGHT],
            "alpha_pixels": {name: int(np.count_nonzero(layer[:, :, 3])) for name, layer in layers.items()},
            "alpha_mismatch_pixels_over_5": int(np.count_nonzero(delta[:, :, 3] > 5)),
            "recomposition_on_background": backgrounds,
        }
        if sex == "male":
            left = master[360, 466, :3].astype(np.int16)
            right = master[360, 567, :3].astype(np.int16)
            report[sex]["mirrored_eyelid_highlights"] = {
                "left_rgb": left.tolist(),
                "right_rgb": right.tolist(),
                "largest_channel_difference": int(np.max(np.abs(left - right))),
            }
            Image.fromarray(master, "RGBA").crop((410, 330, 625, 405)).resize(
                (1290, 450)
            ).save(ROOT / "preview" / "male_eye_highlight_review.png")
    paired = Image.new("RGB", (WIDTH * 2, HEIGHT), "#e8e3dc")
    paired.paste(light_previews[0], (0, 0))
    paired.paste(light_previews[1], (WIDTH, 0))
    paired.save(ROOT / "preview" / "pair_light.png")
    contacts = [
        Image.open(ROOT / "preview" / f"{sex}_nine_parts.png").convert("RGB")
        for sex in ("male", "female")
    ]
    side_by_side = Image.new("RGB", (contacts[0].width * 2, contacts[0].height), "#d8d2c9")
    side_by_side.paste(contacts[0], (0, 0))
    side_by_side.paste(contacts[1], (contacts[0].width, 0))
    side_by_side.save(ROOT / "preview" / "nine_parts_contact.png")
    manifest = {
        "canvas": [WIDTH, HEIGHT],
        "format": "RGBA PNG, one shared origin and zero offsets",
        "layer_order_back_to_front": list(ORDER),
        "slot_names_zh": {
            "hair_front": "前发",
            "hair_back": "后发",
            "eyebrows": "眉毛",
            "eyes": "眼睛",
            "nose": "鼻子",
            "mouth": "嘴巴",
            "face_shape": "脸型（含耳朵及完整颈部）",
            "clothing": "衣服",
            "face_accessory": "面饰（本套为空）",
        },
        "source": "2026-10-03 new male/female full portraits and the user's bald references only",
        "sexes": ["male", "female"],
        "runtime_integrated": False,
    }
    (ROOT / "portrait_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    (ROOT / "qa_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
