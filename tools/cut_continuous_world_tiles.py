"""Cut a verified high-resolution continuous world map into a 5x5 tile set.

This tool intentionally rejects upscaling: a concept-sized image must never be
passed off as the runtime map.  The source must provide at least 2048 native
pixels for each of the 25 tiles.
"""
import argparse
from pathlib import Path

from PIL import Image


GRID = 5
MIN_TILE_SIZE = 2048


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Continuous square terrain master PNG")
    parser.add_argument("output", type=Path, help="Destination directory for r00..r44 PNG tiles")
    args = parser.parse_args()

    with Image.open(args.source) as opened:
        source = opened.convert("RGBA")
    width, height = source.size
    if width != height:
        raise SystemExit("Source must be square.")
    if width < GRID * MIN_TILE_SIZE:
        raise SystemExit(
            "Refusing to upscale a concept map: source is "
            f"{width}px wide; need at least {GRID * MIN_TILE_SIZE}px for 5x5 native tiles."
        )
    if width % GRID != 0:
        raise SystemExit("Source dimensions must divide evenly into 5x5 tiles.")
    native_tile_size = width // GRID
    if native_tile_size < MIN_TILE_SIZE:
        raise SystemExit(
            "Refusing to upscale a concept map: source provides only "
            f"{native_tile_size}px per tile; need at least {MIN_TILE_SIZE}px."
        )

    args.output.mkdir(parents=True, exist_ok=True)
    for row in range(GRID):
        for column in range(GRID):
            left, top = column * native_tile_size, row * native_tile_size
            tile = source.crop((left, top, left + native_tile_size, top + native_tile_size))
            tile.save(args.output / f"zhaoming_terrain_r{row}{column}.png", optimize=True)
    print(f"Exported {GRID * GRID} native {native_tile_size}px tiles to {args.output}")


if __name__ == "__main__":
    main()
