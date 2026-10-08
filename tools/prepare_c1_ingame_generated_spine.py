from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


CANVAS = (720, 960)
RAW_TO_LAYER = {
    "hair_back_raw.png": "01_hair_back.png",
    "lower_body_raw.png": "02_feet_shoes.png",
    "body_clothes_raw.png": "03_body_clothes.png",
    "arm_scroll_raw.png": "04_arm_scroll.png",
    "arm_pointing_raw.png": "05_arm_pointing.png",
    "waist_accessories_raw.png": "06_waist_accessories.png",
    "head_face_raw.png": "07_head_face.png",
    "hair_front_raw.png": "08_hair_front.png",
}

LAYER_ORDER = [
    "01_hair_back.png",
    "02_feet_shoes.png",
    "07_head_face.png",
    "03_body_clothes.png",
    "04_arm_scroll.png",
    "05_arm_pointing.png",
    "06_waist_accessories.png",
    "08_hair_front.png",
]

# Target content boxes are only for placing newly generated art back into the
# new mother composition. They do not take pixels from the mother image.
TARGET_BOXES = {
    "01_hair_back.png": (175, 20, 385, 430),
    "02_feet_shoes.png": (190, 715, 580, 960),
    "03_body_clothes.png": (150, 170, 565, 830),
    "04_arm_scroll.png": (145, 300, 335, 610),
    "05_arm_pointing.png": (385, 255, 570, 545),
    "06_waist_accessories.png": (250, 465, 455, 710),
    "07_head_face.png": (305, 75, 430, 260),
    "08_hair_front.png": (205, 20, 455, 305),
}


def fit_on_canvas(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    image.thumbnail(CANVAS, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    x = (CANVAS[0] - image.width) // 2
    y = (CANVAS[1] - image.height) // 2
    canvas.alpha_composite(image, (x, y))
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
    for name in LAYER_ORDER:
        composite.alpha_composite(Image.open(root / "layers" / name).convert("RGBA"))
    composite.save(preview_dir / "rest_pose_composite.png", optimize=True)

    tile_w, tile_h = 240, 320
    sheet = Image.new("RGBA", (tile_w * 4, tile_h * 2), (38, 43, 52, 255))
    for index, name in enumerate(
        [RAW_TO_LAYER[key] for key in RAW_TO_LAYER]
    ):
        tile = Image.open(root / "layers" / name).convert("RGBA")
        tile.thumbnail((tile_w - 12, tile_h - 12), Image.Resampling.LANCZOS)
        x = (index % 4) * tile_w + (tile_w - tile.width) // 2
        y = (index // 4) * tile_h + (tile_h - tile.height) // 2
        sheet.alpha_composite(tile, (x, y))
    sheet.save(preview_dir / "layers_contact_sheet.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("textures/characters/c1_spine_ingame_generated_v1"),
    )
    args = parser.parse_args()

    root = args.root
    raw_dir = root / "generated_layers"
    layer_dir = root / "layers"
    source_dir = root / "source"
    layer_dir.mkdir(parents=True, exist_ok=True)
    source_dir.mkdir(parents=True, exist_ok=True)

    fit_on_canvas(Image.open(source_dir / "c1_ingame_master.png")).save(
        source_dir / "c1_ingame_master_720x960.png", optimize=True
    )

    for raw_name, layer_name in RAW_TO_LAYER.items():
        generated = fit_on_canvas(Image.open(raw_dir / raw_name))
        positioned = place_in_target(generated, TARGET_BOXES[layer_name])
        positioned.save(layer_dir / layer_name, optimize=True)

    build_previews(root)


if __name__ == "__main__":
    main()
