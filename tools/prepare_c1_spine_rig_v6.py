from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


CANVAS = (720, 960)
RAW_NAME = "character_full_watercolor_no_scroll_raw.png"
PART_NAME = "01_character_full.png"


def fit_on_canvas(image: Image.Image) -> Image.Image:
    """Scale the complete source canvas without cropping or replacing alpha."""
    image = image.convert("RGBA")
    image.thumbnail(CANVAS, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(
        image,
        ((CANVAS[0] - image.width) // 2, (CANVAS[1] - image.height) // 2),
    )
    return canvas


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    return alpha.point(lambda value: 255 if value > 8 else 0).getbbox()


def build_previews(root: Path, image: Image.Image) -> None:
    preview_dir = root / "previews"
    preview_dir.mkdir(parents=True, exist_ok=True)

    # Keep a transparent rest-pose preview so it can be dropped into the game
    # or opened in an image editor without baking in a matte color.
    image.save(preview_dir / "rest_pose_composite.png", optimize=True)

    tile_w, tile_h = 360, 480
    sheet = Image.new("RGBA", (tile_w, tile_h), (38, 43, 52, 255))
    tile = image.copy()
    tile.thumbnail((tile_w - 16, tile_h - 16), Image.Resampling.LANCZOS)
    sheet.alpha_composite(
        tile,
        ((tile_w - tile.width) // 2, (tile_h - tile.height) // 2),
    )
    sheet.save(preview_dir / "parts_contact_sheet.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path("textures/characters/c1_spine_ingame_rig_v6"),
        help="Asset package root containing the generated source image.",
    )
    args = parser.parse_args()

    root = args.root
    source_dir = root / "source"
    raw_dir = root / "generated_parts"
    part_dir = root / "parts"
    source_dir.mkdir(parents=True, exist_ok=True)
    part_dir.mkdir(parents=True, exist_ok=True)

    raw_path = raw_dir / RAW_NAME
    image = fit_on_canvas(Image.open(raw_path))
    image.save(source_dir / "character_full_reference_720x960.png", optimize=True)
    image.save(part_dir / PART_NAME, optimize=True)
    build_previews(root, image)

    print(f"prepared: {part_dir / PART_NAME}")
    print(f"size: {image.size[0]}x{image.size[1]}")
    print(f"alpha_bbox_gt8: {alpha_bbox(image)}")


if __name__ == "__main__":
    main()
