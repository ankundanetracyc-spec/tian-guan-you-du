"""Slice the authoritative mainland partition into Godot's 5×5 mask tiles.

The source contains flat state-ID colours for land and black for every sea
pixel.  It is the same source used by the Draw.io border overlay, so game
hovering can never disagree with the design map.
"""
from pathlib import Path

from PIL import Image


MAP_SIZE = 6400
GRID = 5
OUT_DIR = Path("textures/world_map/territory_masks_5x5_v2_land_only")
PARTITION_SOURCE = Path("textures/world_map/territory_partition/zhaoming_land_partition_id.png")
MASTER_OUT = OUT_DIR / "zhaoming_territory_id_master.png"


def main() -> None:
    if not PARTITION_SOURCE.exists():
        raise RuntimeError("Build zhaoming_land_partition before exporting masks")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    master = Image.open(PARTITION_SOURCE).convert("RGB").resize((MAP_SIZE, MAP_SIZE), Image.Resampling.NEAREST)
    master.save(MASTER_OUT, optimize=True)
    tile_size = MAP_SIZE // GRID
    for row in range(GRID):
        for column in range(GRID):
            left, top = column * tile_size, row * tile_size
            master.crop((left, top, left + tile_size, top + tile_size)).save(
                OUT_DIR / f"zhaoming_territory_id_r{row}{column}.png", optimize=True
            )


if __name__ == "__main__":
    main()
