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
    # Classify by visible depth, not by whether a pixel existed in the bald
    # reference. The latter placed the side drapes in the rear slot merely
    # because they extend past the bald silhouette.
    neutral_ink = (np.abs(r - g) < 17) & (np.abs(g - b) < 17)
    crown = (yy >= 125) & (yy < 340) & (xx > 345) & (xx < 695)
    upper_left = (yy >= 300) & (yy < 650) & (xx > 245) & (xx < 427)
    upper_right = (yy >= 300) & (yy < 650) & (xx > 612) & (xx < 785)
    inner_left = (yy >= 405) & (yy < 650) & (xx > 385) & (xx < 465)
    inner_right = (yy >= 405) & (yy < 650) & (xx > 570) & (xx < 650)
    lower_left = (yy >= 520) & (yy < 1070) & (xx > 345) & (xx < 435)
    lower_right = (yy >= 520) & (yy < 1070) & (xx > 607) & (xx < 710)
    outside_bald = bald[:, :, 3] < 60
    candidate = neutral_ink & (
        (crown & (high < 205) & (distance > 24))
        | ((upper_left | upper_right) & (high < 175)
           & ((distance > 50) | outside_bald))
        | ((inner_left | inner_right) & (high < 170)
           & (distance > 55))
        | ((lower_left | lower_right) & (high < 126)
           & ((distance > 68) | outside_bald))
    )
    # The source's exterior hair includes warm semi-transparent edge pixels.
    # Keep these complete even when they fail the neutral-ink color test.
    exterior_hair = (
        (yy >= 125) & (yy < 660) & (xx > 240) & (xx < 790)
        & outside_bald & (master[:, :, 3] >= 24)
    )
    candidate |= exterior_hair
    candidate &= master[:, :, 3] >= 45
    candidate &= ~skin_candidate(master)
    candidate = cv2.morphologyEx(
        candidate.astype(np.uint8), cv2.MORPH_CLOSE,
        np.ones((3, 3), np.uint8),
    ).astype(bool)
    # In the frontal view every painted strand, including the locks crossing
    # the chest, belongs to HairFront. HairBack is concealed underpainting.
    front = candidate
    back = np.zeros(SIZE, bool)
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


def make_layers(sex: str, master: np.ndarray, bald: np.ndarray,
                clean_skin: np.ndarray) -> tuple[dict[str, np.ndarray], np.ndarray]:
    yy, xx = np.indices(SIZE)
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
    # Only clear skin is copied from the finished portrait. Dark temple and
    # ear pixels can never return to the skin slot through a missed hair mask.
    interior_face = cv2.erode(visible_face.astype(np.uint8),
                              np.ones((17, 17), np.uint8)).astype(bool)
    exposed_face = (
        visible_face & (master[:, :, 3] > 0)
        & interior_face & (yy >= 335)
        & ~front_mask & skin_candidate(master)
    )
    face[exposed_face] = master[exposed_face]
    face[:, :, :3][exposed_face] = featureless_rgb[exposed_face]
    # Blank the entire feature regions, including faint remnants which did
    # not pass the individual feature threshold.
    feature_erase = np.zeros(SIZE, np.uint8)
    shift = 0 if sex == "male" else 2
    for x in (460, 574):
        cv2.ellipse(feature_erase, (x, 379 + shift), (70, 32), 0, 0, 360, 255, -1)
    for x in (458, 576):
        cv2.ellipse(feature_erase, (x, 349 + shift), (68, 23), 0, 0, 360, 255, -1)
    cv2.ellipse(feature_erase, (517, 444 + shift), (45, 47), 0, 0, 360, 255, -1)
    cv2.ellipse(feature_erase, (518, 488 + shift), (58, 29), 0, 0, 360, 255, -1)
    face[:, :, :3] = cv2.inpaint(face[:, :, :3], feature_erase, 13, cv2.INPAINT_TELEA)
    # Use the generated featureless painting only as an interior skin texture.
    # Its outer geometry/halo is rejected; the original bald reference owns
    # the exact alpha silhouette and every registration landmark.
    scale = 1.4 if sex == "male" else 2.5
    source_top = 225 if sex == "male" else 85
    source_scale_y = 1.33 if sex == "male" else 2.57
    map_x = (512 + (xx - 517) * scale).astype(np.float32)
    map_y = (source_top + (yy - (171 if sex == "male" else 169)) * source_scale_y).astype(np.float32)
    painted = cv2.remap(clean_skin[:, :, :3], map_x, map_y,
                        cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    paint_alpha = cv2.remap(clean_skin[:, :, 3], map_x, map_y,
                            cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)
    oval = np.zeros(SIZE, np.uint8)
    cv2.ellipse(oval, (517, 423 + shift), (110, 119), 0, 0, 360, 255, -1)
    oval = cv2.GaussianBlur(oval, (0, 0), 11).astype(np.float32) / 255.0
    oval *= interior_face & (paint_alpha > 235)
    ring = (oval > 0.05) & (oval < 0.45) & skin_candidate(face)
    if np.any(ring):
        color_shift = np.median(
            face[:, :, :3][ring].astype(np.float32)
            - painted[ring].astype(np.float32), axis=0,
        )
    else:
        color_shift = np.zeros(3, np.float32)
    painted = np.clip(painted.astype(np.float32) + color_shift, 0, 255)
    face[:, :, :3] = np.clip(
        face[:, :, :3].astype(np.float32) * (1 - oval[:, :, None])
        + painted * oval[:, :, None], 0, 255,
    ).astype(np.uint8)

    clothing = np.zeros_like(master)
    clothing_mask = (
        (bald[:, :, 3] > 0) & ~visible_face
        & (yy >= (520 if sex == "male" else 530))
    )
    clothing[clothing_mask] = bald[clothing_mask]
    # The bald reference carries the same authored outfit without locks.
    # Copying exposed pixels from the finished portrait left hair on lapels.

    # Rear hair follows the actual painted outer drapes. Its interior is
    # intentionally transparent: the skin/head and clothing always occlude
    # that area, while a synthetic dark slab fails single-layer inspection.
    hair_back = np.zeros_like(master)
    hair_back[front_mask] = master[front_mask]
    rear_weight = np.clip((np.abs(xx - 517) - 68) / 48.0, 0, 1)
    rear_weight[yy < 320] = 1
    rear_weight *= np.clip((600 - yy) / 70.0, 0, 1)
    hair_back[:, :, 3] = np.clip(
        hair_back[:, :, 3].astype(np.float32) * rear_weight, 0, 255,
    ).astype(np.uint8)

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
    return layers, master


def main() -> None:
    (ROOT / "preview").mkdir(parents=True, exist_ok=True)
    report = {}
    light_previews = []
    for sex in ("male", "female"):
        source = read_image(V1 / f"{sex}_portrait.png")
        bald = read_image(V1 / "reference" / f"{sex}_bald_user.png")
        clean_skin = read_image(ROOT / "source" / f"{sex}_clean_skin_generated.png")
        if sex == "male":
            source = repair_eye_highlights(source)
        layers, master = make_layers(sex, source, bald, clean_skin)
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
        without_back = dict(layers)
        without_back["hair_back"] = np.zeros_like(master)
        front_alone = composite(without_back)
        Image.fromarray(on_background(front_alone, "#e8e3dc"), "RGB").save(
            ROOT / "preview" / f"{sex}_without_hair_back.png"
        )
        without_front = dict(layers)
        without_front["hair_front"] = np.zeros_like(master)
        rear_alone = composite(without_front)
        Image.fromarray(on_background(rear_alone, "#e8e3dc"), "RGB").save(
            ROOT / "preview" / f"{sex}_without_hair_front.png"
        )
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
            "slot_purity": {
                "clothing_alpha_above_y520": int(np.count_nonzero(layers["clothing"][:520, :, 3])),
                "front_hair_skin_like_pixels": int(np.count_nonzero(skin_candidate(layers["hair_front"]))),
                "rear_hair_skin_like_pixels": int(np.count_nonzero(skin_candidate(layers["hair_back"]))),
            },
            "hair_toggle": {
                "pixels_changed_without_back_over_12": int(np.count_nonzero(np.any(
                    np.abs(on_background(rebuilt, "#e8e3dc").astype(np.int16)
                           - on_background(front_alone, "#e8e3dc").astype(np.int16)) > 12,
                    axis=2,
                ))),
                "pixels_changed_without_front_over_12": int(np.count_nonzero(np.any(
                    np.abs(on_background(rebuilt, "#e8e3dc").astype(np.int16)
                           - on_background(rear_alone, "#e8e3dc").astype(np.int16)) > 12,
                    axis=2,
                ))),
            },
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
