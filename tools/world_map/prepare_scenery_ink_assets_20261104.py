"""Precisely split the original built-in imagegen atlases; no repaint or resampling.

Native generated files are preserved byte-for-byte. Only measured near-zero
alpha noise is removed in derived sprites; their remaining RGB/alpha is never
repainted or resampled. Four transparent pixels pad object crops.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw
import numpy as np

PROJECT = Path(__file__).resolve().parents[2]
ROOT = PROJECT / "textures/world/bai_chuan/scenery_ink_20261104"
RESOURCE = "res://textures/world/bai_chuan/scenery_ink_20261104/"
PAD = 4
SIGNAL_ALPHA = 3
SHEETS = [
    {
        "id": "atlas_a_vegetation",
        "source": "source/atlas_a_vegetation_native.png",
        "prompt": "prompts/atlas_a_vegetation.prompt.json",
        "grid": [3, 3],
        "items": [
            ("pine_single", 42), ("broadleaf_single", 42), ("willow_single", 44),
            ("pine_group", 56), ("mixed_group", 56), ("willow_group", 54),
            ("shrub", 14), ("reeds", 22), ("rock_group", 18),
        ],
    },
    {
        "id": "atlas_b_roads_bridges",
        "source": "source/atlas_b_roads_bridges_native.png",
        "prompt": "prompts/atlas_b_roads_bridges.prompt.json",
        "grid": [2, 3],
        "items": [
            ("road_dirt", 10), ("road_woodland", 10),
            ("road_stone", 10), ("bridge_wood", 16),
            ("bridge_stone", 18), ("roadside_stones", 9),
        ],
    },
]


def alpha_stats(image: Image.Image) -> dict:
    hist = image.getchannel("A").histogram()
    count = image.width * image.height
    return {
        "mode": image.mode, "size": list(image.size),
        "fully_transparent": hist[0], "partially_transparent": sum(hist[1:255]),
        "fully_opaque": hist[255], "transparent_fraction": hist[0] / count,
    }


def rgba_signal_bbox(image: Image.Image):
    return image.getchannel("A").point(
        lambda value: 255 if value >= SIGNAL_ALPHA else 0
    ).getbbox()


def clean_near_zero_alpha(image: Image.Image) -> tuple[Image.Image, dict]:
    """Remove only measured near-zero-alpha RGB contamination, never repaint."""
    pixels = np.array(image)
    rgb = pixels[:, :, :3].astype(np.int16)
    alpha = pixels[:, :, 3]
    red = (rgb[:, :, 0] > 180) & (rgb[:, :, 1] < 100) & (rgb[:, :, 2] < 110)
    green = (rgb[:, :, 1] > 170) & (rgb[:, :, 0] < 140) & (rgb[:, :, 2] < 140)
    yellow = (rgb[:, :, 0] > 170) & (rgb[:, :, 1] > 170) & (rgb[:, :, 2] < 100)
    fluorescent = red | green | yellow
    fluorescent_alpha_max = int(alpha[fluorescent].max()) if fluorescent.any() else 0
    mask = ((alpha > 0) & (alpha <= 2)) | (fluorescent & (alpha <= 12))
    removed = int(mask.sum())
    pixels[mask] = 0
    return Image.fromarray(pixels), {
        "alpha_1_2_noise_and_fluorescent_alpha_le_12_pixels_removed": removed,
        "other_rgba_pixels_preserved": True,
        "fluorescent_rgb_alpha_max_in_source": fluorescent_alpha_max,
    }


def split_sheet(sheet: dict) -> tuple[dict, dict]:
    source_path = ROOT / sheet["source"]
    native_image = Image.open(source_path).convert("RGBA")
    image, cleanup_record = clean_near_zero_alpha(native_image)
    columns, rows = sheet["grid"]
    record = {
        "id": sheet["id"], "tool": "image_gen.imagegen", "mode": "built-in",
        "source_path": RESOURCE + sheet["source"],
        "prompt_path": RESOURCE + sheet["prompt"],
        "native_size": list(image.size), "grid": sheet["grid"],
        "sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
        "original_source_alpha_preserved": True,
        "derived_near_zero_alpha_cleanup": cleanup_record,
        "split_method": "exact equal-cell crop; alpha 1/2 residual and fluorescent RGB alpha<=12 cleared; bbox at remaining alpha >=3; add 4px transparent padding; no resize, repaint or recolour",
        "alpha": alpha_stats(native_image), "cells": [],
    }
    metadata_path = ROOT / "source/generation_outputs.json"
    if metadata_path.exists():
        outputs = json.loads(metadata_path.read_text(encoding="utf-8"))["outputs"]
        record["generation_output"] = next(item for item in outputs if item["id"] == sheet["id"])
    # Repack without scaling into a genuinely empty, uniform production sheet.
    # 1404 fits all native subjects with >=35px separation from every cell edge;
    # the preserved generator output remains the separately labelled 1254 file.
    packed = Image.new("RGBA", (1404, 1404))
    packed_record = {"size": [1404, 1404], "grid": sheet["grid"], "cells": []}
    sprites = {}
    for index, (sprite_id, world_height) in enumerate(sheet["items"]):
        col, row = index % columns, index // columns
        cell_bounds = (
            col * image.width // columns, row * image.height // rows,
            (col + 1) * image.width // columns, (row + 1) * image.height // rows,
        )
        cell = image.crop(cell_bounds)
        bbox = rgba_signal_bbox(cell)
        if bbox is None:
            raise ValueError(f"Empty cell: {sprite_id}")
        # Center strips remove the painted end caps so Line2D can tile the road
        # along X. Keep each atlas cell intact in source and in full_cells/.
        full_bbox = bbox
        subject = cell.crop(full_bbox)
        packed_cell = (col * 1404 // columns, row * 1404 // rows,
                       (col + 1) * 1404 // columns, (row + 1) * 1404 // rows)
        packed_x = packed_cell[0] + ((packed_cell[2]-packed_cell[0])-subject.width)//2
        packed_y = packed_cell[1] + ((packed_cell[3]-packed_cell[1])-subject.height)//2
        packed.alpha_composite(subject, (packed_x, packed_y))
        packed_margins = [packed_x-packed_cell[0], packed_y-packed_cell[1],
                          packed_cell[2]-(packed_x+subject.width), packed_cell[3]-(packed_y+subject.height)]
        if min(packed_margins) < 35:
            raise ValueError(f"Insufficient production atlas margin: {sprite_id}")
        packed_record["cells"].append({"id": sprite_id, "bounds": list(packed_cell), "object_margins": packed_margins})
        if sprite_id in ("road_dirt", "road_woodland", "road_stone"):
            full = cell.crop(bbox)
            (ROOT / "full_cells").mkdir(exist_ok=True)
            full.save(ROOT / "full_cells" / f"{sprite_id}_full.png")
            width = bbox[2] - bbox[0]
            inset = max(1, round(width * 0.22))
            bbox = (bbox[0] + inset, bbox[1], bbox[2] - inset, bbox[3])
        cropped = cell.crop(bbox)
        # Padding only above/below road strips avoids adding longitudinal gaps
        # at each repeat; normal sprites receive padding on all four sides.
        pad_x = 0 if sprite_id.startswith("road_") else PAD
        final = Image.new("RGBA", (cropped.width + pad_x * 2, cropped.height + PAD * 2))
        final.alpha_composite(cropped, (pad_x, PAD))
        out_relative = f"sprites/{sprite_id}.png"
        final.save(ROOT / out_relative)
        sprite = {
            "path": RESOURCE + out_relative, "size": list(final.size),
            "world_height": world_height, "pivot": [0.5, 1.0],
        }
        if sprite_id.startswith("road_"):
            sprite.update({"texture_axis": "x", "world_width": 10, "tile_middle_crop": True})
        if sprite_id.startswith("bridge_"):
            sprite.update({"span_axis": "x", "short_contact_shadow": True})
        sprites[sprite_id] = sprite
        record["cells"].append({
            "id": sprite_id, "cell_bounds": list(cell_bounds),
            "signal_bbox_in_cell": list(full_bbox),
            "crop_bbox_in_cell": list(bbox),
            "signal_margins": [full_bbox[0], full_bbox[1], cell.width-full_bbox[2], cell.height-full_bbox[3]],
            "final_size": list(final.size), "alpha": alpha_stats(final),
        })
    (ROOT / "atlases").mkdir(exist_ok=True)
    packed_relative = f"atlases/{sheet['id']}_clean_padded.png"
    packed.save(ROOT / packed_relative)
    packed_record["path"] = RESOURCE + packed_relative
    record["production_atlas"] = packed_record
    return record, sprites


def make_contact_sheet(sprites: dict) -> None:
    width, cell_height, columns = 1000, 220, 3
    cell_width = width // columns
    rows = (len(sprites) + columns - 1) // columns
    for suffix, background in [("paper", (222, 224, 208, 255)), ("dark", (44, 54, 57, 255))]:
        sheet = Image.new("RGBA", (width, cell_height * rows), background)
        draw = ImageDraw.Draw(sheet)
        for i, (sprite_id, entry) in enumerate(sprites.items()):
            sprite = Image.open(ROOT / "sprites" / f"{sprite_id}.png").convert("RGBA")
            # QA contact sheet only. Final sprites above are never resampled.
            sprite.thumbnail((cell_width - 40, cell_height - 40), Image.Resampling.LANCZOS)
            x = (i % columns) * cell_width + (cell_width-sprite.width)//2
            y = (i // columns) * cell_height + (cell_height-26-sprite.height)//2
            sheet.alpha_composite(sprite, (x, y))
            draw.text(((i % columns) * cell_width + 12, (i // columns + 1) * cell_height - 24), sprite_id, fill=(80,91,83) if suffix=="paper" else (200,208,196))
        sheet.convert("RGB").save(ROOT / "qa" / f"sprites_contact_{suffix}.png")


def main() -> None:
    (ROOT / "sprites").mkdir(parents=True, exist_ok=True)
    (ROOT / "qa").mkdir(parents=True, exist_ok=True)
    catalog = {"version": 1, "source_records": [], "sprites": {}}
    for sheet in SHEETS:
        if not (ROOT / sheet["source"]).exists():
            continue
        record, sprites = split_sheet(sheet)
        catalog["source_records"].append(record)
        catalog["sprites"].update(sprites)
    (ROOT / "catalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    make_contact_sheet(catalog["sprites"])
    print(json.dumps({"catalog": str(ROOT / "catalog.json"), "sprites": {key: value["size"] for key, value in catalog["sprites"].items()}}, ensure_ascii=False))


if __name__ == "__main__":
    main()
