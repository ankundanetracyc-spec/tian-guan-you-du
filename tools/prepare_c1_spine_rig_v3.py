from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


CANVAS = (720, 960)
RAW_TO_PART = {
    "body_full_raw.png": "01_body_full.png",
    "hair_back_raw.png": "02_hair_back.png",
    "head_raw.png": "03_head.png",
    "brows_raw.png": "04_brows.png",
    "eyes_raw.png": "05_eyes.png",
    "nose_raw.png": "06_nose.png",
    "mouth_raw.png": "07_mouth.png",
    "hair_front_raw.png": "08_hair_front.png",
    "hair_accessory_raw.png": "09_hair_accessory.png",
    "ribbon_tail_a_raw.png": "10_ribbon_tail_a.png",
    "ribbon_tail_b_raw.png": "11_ribbon_tail_b.png",
}

COMPOSITE_ORDER = [
    "02_hair_back.png",
    "10_ribbon_tail_a.png",
    "11_ribbon_tail_b.png",
    "01_body_full.png",
    "03_head.png",
    "04_brows.png",
    "05_eyes.png",
    "06_nose.png",
    "07_mouth.png",
    "08_hair_front.png",
    "09_hair_accessory.png",
]

# Shared 720x960 placement targets, measured against the v7 reference pose.
TARGET_BOXES = {
    "01_body_full.png": (165, 150, 595, 950),
    "02_hair_back.png": (165, 8, 425, 420),
    "03_head.png": (305, 72, 425, 225),
    "04_brows.png": (322, 112, 406, 138),
    "05_eyes.png": (322, 130, 410, 158),
    "06_nose.png": (360, 145, 387, 176),
    "07_mouth.png": (354, 174, 394, 198),
    "08_hair_front.png": (275, 50, 440, 235),
    "09_hair_accessory.png": (285, 18, 390, 90),
    "10_ribbon_tail_a.png": (195, 55, 340, 185),
    "11_ribbon_tail_b.png": (215, 75, 355, 235),
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
    part_names = [RAW_TO_PART[name] for name in RAW_TO_PART]
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
        default=Path("textures/characters/c1_spine_ingame_rig_v3"),
    )
    args = parser.parse_args()

    root = args.root
    source_dir = root / "source"
    raw_dir = root / "generated_parts"
    part_dir = root / "parts"
    source_dir.mkdir(parents=True, exist_ok=True)
    part_dir.mkdir(parents=True, exist_ok=True)

    reference = source_dir / "c1_base_reference_v7.png"
    fit_on_canvas(Image.open(reference)).save(source_dir / "c1_base_reference_720x960.png", optimize=True)

    for raw_name, part_name in RAW_TO_PART.items():
        generated = fit_on_canvas(Image.open(raw_dir / raw_name))
        positioned = place_in_target(generated, TARGET_BOXES[part_name])
        positioned.save(part_dir / part_name, optimize=True)

    build_previews(root)


if __name__ == "__main__":
    main()
