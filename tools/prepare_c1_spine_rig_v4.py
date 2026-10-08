from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


CANVAS = (720, 960)

# The three hair layers in v4 are independently generated assets. The raw
# images are normalized onto the same full canvas before they enter Spine;
# this is alignment, not source-image cropping.
RAW_TO_PART = {
    "body_full_raw.png": "01_body_full.png",
    "head_raw.png": "02_head.png",
    "brows_raw.png": "03_brows.png",
    "eyes_raw.png": "04_eyes.png",
    "nose_raw.png": "05_nose.png",
    "mouth_raw.png": "06_mouth.png",
    "hair_front_bangs_raw.png": "07_hair_front.png",
    "hair_back_no_bun_raw.png": "08_hair_back.png",
    "hair_accessory_merged_raw.png": "09_hair_accessory.png",
}

COMPOSITE_ORDER = [
    "08_hair_back.png",
    "09_hair_accessory.png",
    "01_body_full.png",
    "02_head.png",
    "03_brows.png",
    "04_eyes.png",
    "05_nose.png",
    "06_mouth.png",
    "07_hair_front.png",
]

# Shared 720x960 placement targets measured from the clear v7 reference.
# The accessory target includes the bun, band, and both tails as one layer.
TARGET_BOXES = {
    "01_body_full.png": (165, 150, 595, 950),
    "02_head.png": (305, 72, 425, 225),
    "03_brows.png": (322, 112, 406, 138),
    "04_eyes.png": (322, 130, 410, 158),
    "05_nose.png": (360, 145, 387, 176),
    "06_mouth.png": (354, 174, 394, 198),
    "07_hair_front.png": (275, 50, 440, 235),
    "08_hair_back.png": (150, 8, 480, 520),
    "09_hair_accessory.png": (170, 5, 470, 270),
}


def fit_on_canvas(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    image.thumbnail(CANVAS, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(image, ((CANVAS[0] - image.width) // 2, (CANVAS[1] - image.height) // 2))
    return canvas


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    return alpha.point(lambda value: 255 if value > 8 else 0).getbbox()


def place_in_target(image: Image.Image, target: tuple[int, int, int, int]) -> Image.Image:
    image = image.convert("RGBA")
    bbox = alpha_bbox(image)
    if bbox is None:
        return Image.new("RGBA", CANVAS, (0, 0, 0, 0))

    source_w = bbox[2] - bbox[0]
    source_h = bbox[3] - bbox[1]
    target_w = target[2] - target[0]
    target_h = target[3] - target[1]
    scale = min(target_w / source_w, target_h / source_h)
    scaled = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    x = round(target[0] - bbox[0] * scale)
    y = round(target[1] - bbox[1] * scale)

    output = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    output.paste(scaled, (x, y), scaled)
    return output


def build_previews(root: Path) -> None:
    preview_dir = root / "previews"
    preview_dir.mkdir(parents=True, exist_ok=True)

    composite = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    for name in COMPOSITE_ORDER:
        composite.alpha_composite(Image.open(root / "parts" / name).convert("RGBA"))
    composite.save(preview_dir / "rest_pose_composite.png", optimize=True)

    tile_w, tile_h = 240, 320
    part_names = list(RAW_TO_PART.values())
    columns = 5
    rows = (len(part_names) + columns - 1) // columns
    sheet = Image.new("RGBA", (tile_w * columns, tile_h * rows), (38, 43, 52, 255))
    for index, name in enumerate(part_names):
        tile = Image.open(root / "parts" / name).convert("RGBA")
        tile.thumbnail((tile_w - 12, tile_h - 12), Image.Resampling.LANCZOS)
        x = (index % columns) * tile_w + (tile_w - tile.width) // 2
        y = (index // columns) * tile_h + (tile_h - tile.height) // 2
        sheet.alpha_composite(tile, (x, y))
    sheet.save(preview_dir / "parts_contact_sheet.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("textures/characters/c1_spine_ingame_rig_v4"),
    )
    args = parser.parse_args()

    root = args.root
    source_dir = root / "source"
    raw_dir = root / "generated_parts"
    part_dir = root / "parts"
    source_dir.mkdir(parents=True, exist_ok=True)
    part_dir.mkdir(parents=True, exist_ok=True)

    reference = source_dir / "c1_base_clear_no_scroll_transparent_v7.png"
    fit_on_canvas(Image.open(reference)).save(source_dir / "c1_base_reference_720x960.png", optimize=True)

    for raw_name, part_name in RAW_TO_PART.items():
        generated = fit_on_canvas(Image.open(raw_dir / raw_name))
        positioned = place_in_target(generated, TARGET_BOXES[part_name])
        positioned.save(part_dir / part_name, optimize=True)

    build_previews(root)


if __name__ == "__main__":
    main()
