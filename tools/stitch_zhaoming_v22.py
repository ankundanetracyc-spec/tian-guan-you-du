# -*- coding: utf-8 -*-
"""Stitch four high-detail terrain redraws into one seamless presentation map."""
from pathlib import Path

from PIL import Image, ImageFilter


TILES = Path("textures/world_map/production_samples/v22_tile_refs")
MASTER_OUT = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v22_native_detail.png")
PRESENTATION_OUT = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v22_native_detail_4096.png")
ORDER = (("nw", "ne"), ("sw", "se"))


def main() -> None:
    images = {
        name: Image.open(TILES / f"zhaoming_v22_{name}_detail.png").convert("RGB")
        for row in ORDER for name in row
    }
    width, height = images["nw"].size
    canvas = Image.new("RGB", (width * 2, height * 2))
    for row, names in enumerate(ORDER):
        for column, name in enumerate(names):
            canvas.paste(images[name], (column * width, row * height))
    # A small blur only across the join suppresses a seam without softening
    # the freshly rendered terrain detail across the rest of the map.
    seam = canvas.filter(ImageFilter.GaussianBlur(radius=2.0))
    mask = Image.new("L", canvas.size, 0)
    mask_pixels = mask.load()
    half_w, half_h = width, height
    for y in range(canvas.height):
        for x in range(canvas.width):
            distance = min(abs(x - half_w), abs(y - half_h))
            if distance < 12:
                mask_pixels[x, y] = int((12 - distance) / 12.0 * 170)
    canvas = Image.composite(seam, canvas, mask)
    MASTER_OUT.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(MASTER_OUT, optimize=True)
    canvas.resize((4096, 4096), Image.Resampling.LANCZOS).save(PRESENTATION_OUT, optimize=True)
    print(MASTER_OUT, canvas.size, PRESENTATION_OUT)


if __name__ == "__main__":
    main()
