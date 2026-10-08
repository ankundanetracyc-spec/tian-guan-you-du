from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


CANVAS = (720, 960)
RAW_TO_LAYER = {
    "hair_back_raw.png": "01_hair_back.png",
    "lower_body_raw.png": "02_lower_body.png",
    "body_clothes_raw.png": "03_body_clothes.png",
    "head_skin_raw.png": "04_head_skin.png",
    "hair_front_raw.png": "05_hair_front.png",
    "arm_scroll_raw.png": "06_arm_scroll.png",
    "arm_cast_raw.png": "07_arm_cast.png",
    "waist_accessories_raw.png": "08_waist_accessories.png",
}

LAYER_ORDER = [
    "02_lower_body.png",
    "01_hair_back.png",
    "03_body_clothes.png",
    "04_head_skin.png",
    "05_hair_front.png",
    "06_arm_scroll.png",
    "07_arm_cast.png",
    "08_waist_accessories.png",
]

# Target content boxes are measured in the new 720x960 master composition.
# They place generated components into the same silhouette without reusing
# pixels from the master or clipping any painted content from the new art.
TARGET_BOXES = {
    "01_hair_back.png": (105, 0, 480, 500),
    "02_lower_body.png": (190, 660, 560, 960),
    "03_body_clothes.png": (135, 150, 660, 840),
    "04_head_skin.png": (305, 65, 425, 255),
    "05_hair_front.png": (180, 0, 470, 310),
    "06_arm_scroll.png": (130, 205, 315, 600),
    "07_arm_cast.png": (370, 110, 675, 500),
    "08_waist_accessories.png": (215, 270, 470, 560),
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
    bbox = alpha.point(lambda value: 255 if value > 8 else 0).getbbox()
    return bbox


def align_generated_layer(image: Image.Image, target_box: tuple[int, int, int, int]) -> Image.Image:
    image = image.convert("RGBA")
    bbox = alpha_bbox(image)
    if bbox is None:
        return image

    source_w = bbox[2] - bbox[0]
    source_h = bbox[3] - bbox[1]
    target_w = target_box[2] - target_box[0]
    target_h = target_box[3] - target_box[1]
    scale = min(target_w / source_w, target_h / source_h)
    scaled = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )

    x = round(target_box[0] - bbox[0] * scale)
    y = round(target_box[1] - bbox[1] * scale)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.paste(scaled, (x, y), scaled)
    return canvas


def make_previews(root: Path) -> None:
    preview_dir = root / "previews"
    preview_dir.mkdir(parents=True, exist_ok=True)

    composite = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    for name in LAYER_ORDER:
        composite.alpha_composite(Image.open(root / "layers" / name).convert("RGBA"))
    composite.save(preview_dir / "composite_standing.png", optimize=True)

    tile_w, tile_h = 240, 320
    columns = 4
    rows = 2
    sheet = Image.new("RGBA", (columns * tile_w, rows * tile_h), (34, 39, 48, 255))
    for index, name in enumerate(LAYER_ORDER):
        tile = Image.open(root / "layers" / name).convert("RGBA")
        tile.thumbnail((tile_w - 12, tile_h - 12), Image.Resampling.LANCZOS)
        x = (index % columns) * tile_w + (tile_w - tile.width) // 2
        y = (index // columns) * tile_h + (tile_h - tile.height) // 2
        sheet.alpha_composite(tile, (x, y))
    sheet.save(preview_dir / "layers_contact_sheet.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("textures/characters/c1_spine_generated_v1"),
    )
    args = parser.parse_args()

    root = args.root
    raw_dir = root / "generated_layers"
    layer_dir = root / "layers"
    source_dir = root / "source"
    layer_dir.mkdir(parents=True, exist_ok=True)
    source_dir.mkdir(parents=True, exist_ok=True)

    master = fit_on_canvas(Image.open(source_dir / "c1_generated_master.png"))
    master.save(source_dir / "c1_generated_master_720x960.png", optimize=True)

    for raw_name, layer_name in RAW_TO_LAYER.items():
        output = fit_on_canvas(Image.open(raw_dir / raw_name))
        output = align_generated_layer(output, TARGET_BOXES[layer_name])
        output.save(layer_dir / layer_name, optimize=True)

    make_previews(root)


if __name__ == "__main__":
    main()
