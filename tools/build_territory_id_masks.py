"""Build 4x4 lossless territory-ID masks from the approved world-layout polygons.

The output contains only exact flat colours. It is interaction data, not visible art.
Re-run after changing the territory outlines in the map-layout source.
"""
from pathlib import Path

from PIL import Image, ImageDraw


MAP_SIZE = 6400
GRID = 4
TILE_SIZE = 1254
OUT_DIR = Path("textures/world_map/territory_masks")

TERRITORIES = [
    ((229, 145, 62), [(2100,2050),(2850,1580),(3650,1720),(4120,2400),(3970,3160),(3290,3380),(2510,3090),(1900,2640)]),
    ((57, 139, 53), [(620,520),(1680,270),(2710,500),(3100,1050),(2740,1620),(1910,1760),(950,1480),(430,980)]),
    ((22, 131, 154), [(4310,780),(5340,530),(6120,990),(6280,1920),(5940,2800),(5200,3050),(4550,2540),(4370,1710)]),
    ((0, 131, 143), [(3850,2280),(4520,2020),(5110,2380),(5240,3190),(4800,3620),(4150,3400),(3710,2890)]),
    ((196, 127, 39), [(260,2740),(1050,2380),(1890,2740),(2100,3690),(1650,4510),(820,4630),(250,4050)]),
    ((78, 138, 72), [(1770,4110),(2740,3730),(3860,3990),(4240,4880),(3860,5840),(2740,6120),(1900,5590),(1450,4800)]),
    ((38, 125, 120), [(4010,3670),(4960,3240),(5850,3540),(6230,4390),(6100,5530),(5300,5850),(4400,5300),(3910,4560)]),
    ((177, 125, 36), [(3920,1700),(4580,1450),(5220,1840),(5300,2700),(4920,3300),(4290,3160),(3860,2540)]),
    ((111, 99, 139), [(550,3880),(1540,3650),(2350,4060),(2540,4910),(2050,5450),(1070,5350),(460,4670)]),
]


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    master = Image.new("RGB", (MAP_SIZE, MAP_SIZE), (0, 0, 0))
    draw = ImageDraw.Draw(master)
    for color, points in TERRITORIES:
        draw.polygon(points, fill=color)
    for row in range(GRID):
        for column in range(GRID):
            left = column * MAP_SIZE // GRID
            top = row * MAP_SIZE // GRID
            right = (column + 1) * MAP_SIZE // GRID
            bottom = (row + 1) * MAP_SIZE // GRID
            tile = master.crop((left, top, right, bottom)).resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)
            tile.save(OUT_DIR / f"world_territory_id_r{row}{column}.png")


if __name__ == "__main__":
    main()
