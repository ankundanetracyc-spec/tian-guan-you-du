# -*- coding: utf-8 -*-
"""Render the same atlas layers as NodeWorldMap for design review."""
from pathlib import Path
from PIL import Image, ImageDraw

BASE = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v24_atlas_base.png")
EDGES = Path("textures/world_map/territory_partition/zhaoming_land_partition_edges.png")
OUT = Path("docs/plans/世界与探索/历史地图/预览_昭明纪简略地图_州界道路山脉.png")
MAP_SIZE = 6400.0
RIDGES = [
    [(420, 620), (720, 910), (1030, 1250), (1320, 1580), (1560, 1900)],
    [(700, 2350), (1010, 2600), (1270, 2860)],
    [(2290, 790), (2570, 620), (2850, 820)],
    [(3960, 1520), (4240, 1780), (4490, 2030)],
    [(3650, 2920), (3930, 3180), (4180, 3420)],
    [(2080, 4200), (2320, 4510), (2600, 4770)],
]
ROADS = [
    [(1260, 1750), (1650, 1910), (2050, 2140), (2440, 2350)],
    [(2440, 2350), (2810, 2580), (3050, 2870)],
    [(2680, 3090), (3000, 3330), (3330, 3550)],
    [(3420, 2180), (3760, 2030), (4070, 1900)],
    [(1700, 3740), (2010, 3930), (2300, 4140)],
]


def local(point, scale):
    return point[0] * scale, point[1] * scale


def main() -> None:
    base = Image.open(BASE).convert("RGBA")
    edge = Image.open(EDGES).convert("RGBA").resize(base.size, Image.Resampling.NEAREST)
    result = Image.alpha_composite(base, edge)
    draw = ImageDraw.Draw(result)
    scale = base.width / MAP_SIZE
    result.convert("RGB").save(OUT, optimize=True)
    print(OUT)


if __name__ == "__main__":
    main()
