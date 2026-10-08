# -*- coding: utf-8 -*-
"""Prepare four overlapping reference crops for high-detail terrain redraw."""
from pathlib import Path
from PIL import Image

SOURCE = Path(r"C:\Users\34784\.codex\generated_images\019fd814-9245-70f0-83fb-c97b952236b0\exec-c05df959-7813-4f87-9cc7-ab69fcf3367d.png")
OUT = Path("textures/world_map/production_samples/v22_tile_refs")
NAMES = ("nw", "ne", "sw", "se")

def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    image = Image.open(SOURCE).convert("RGB")
    width, height = image.size
    # 24px overlap gives the generator visual context for seamless joins.
    mid_x, mid_y, overlap = width // 2, height // 2, 24
    boxes = [
        (0, 0, mid_x + overlap, mid_y + overlap),
        (mid_x - overlap, 0, width, mid_y + overlap),
        (0, mid_y - overlap, mid_x + overlap, height),
        (mid_x - overlap, mid_y - overlap, width, height),
    ]
    for name, box in zip(NAMES, boxes):
        image.crop(box).save(OUT / f"zhaoming_v22_{name}_ref.png", optimize=True)

if __name__ == "__main__":
    main()
