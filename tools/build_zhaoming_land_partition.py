# -*- coding: utf-8 -*-
"""Build the authoritative, land-only partition shared by map tooling."""
from collections import deque
import json
from math import sin
from pathlib import Path

from PIL import Image


SOURCE = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v24_atlas_base.png")
OUT_DIR = Path("textures/world_map/territory_partition")
ID_MASTER = OUT_DIR / "zhaoming_land_partition_id.png"
EDGE_OVERLAY = OUT_DIR / "zhaoming_land_partition_edges.png"
METADATA = OUT_DIR / "zhaoming_land_partition.json"

# Order and colours deliberately match WorldTerritoryData / the runtime masks.
ZONES = [
    ("玄朔州", (229, 57, 53), (185, 180), 1.00),
    ("相山州", (0, 131, 143), (535, 185), 1.00),
    ("关河州", (196, 127, 39), (210, 510), 1.00),
    ("云阙州", (109, 145, 62), (565, 515), 0.93),
    ("东华州", (177, 125, 36), (825, 500), 1.05),
    ("沧溟州", (22, 131, 154), (980, 315), 1.10),
    ("云岫州", (111, 99, 139), (225, 820), 1.05),
    ("苍梧州", (78, 138, 72), (560, 870), 1.02),
    ("归潮州", (38, 125, 120), (860, 830), 1.08),
]


def ocean_mask(image: Image.Image) -> bytearray:
    """Only edge-connected blue water is ocean; inland water remains land-state."""
    width, height = image.size
    pixels = image.load()
    water = bytearray(width * height)
    for y in range(height):
        for x in range(width):
            red, green, blue = pixels[x, y]
            if blue >= red + 22 and blue >= green - 4:
                water[y * width + x] = 1
    ocean = bytearray(width * height)
    queue = deque((x, y) for x in range(width) for y in (0, height - 1))
    queue.extend((x, y) for y in range(1, height - 1) for x in (0, width - 1))
    while queue:
        x, y = queue.popleft()
        index = y * width + x
        if ocean[index] or not water[index]:
            continue
        ocean[index] = 1
        if x:
            queue.append((x - 1, y))
        if x + 1 < width:
            queue.append((x + 1, y))
        if y:
            queue.append((x, y - 1))
        if y + 1 < height:
            queue.append((x, y + 1))
    return ocean


def nearest_land(seed, ocean, width, height):
    sx, sy = seed
    for radius in range(max(width, height)):
        for x, y in ((sx - radius, sy), (sx + radius, sy), (sx, sy - radius), (sx, sy + radius)):
            if 0 <= x < width and 0 <= y < height and not ocean[y * width + x]:
                return x, y
    raise RuntimeError("No land found for territory seed")


def main() -> None:
    if not SOURCE.exists():
        raise RuntimeError(f"Terrain source not found: {SOURCE}")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    terrain = Image.open(SOURCE).convert("RGB")
    width, height = terrain.size
    ocean = ocean_mask(terrain)
    seeds = [(name, color, nearest_land(seed, ocean, width, height), weight) for name, color, seed, weight in ZONES]
    ids, counts, sums = bytearray(width * height), [0] * len(seeds), [[0, 0] for _ in seeds]
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if ocean[index]:
                continue
            # Warp the whole coordinate field at a continental scale.  This
            # preserves shared borders exactly while keeping them away from
            # rigid grid / Voronoi straight lines.
            warped_x = x + 42 * sin(y * 0.012) + 26 * sin(x * 0.009 + y * 0.017)
            warped_y = y + 36 * sin(x * 0.011) + 22 * sin(y * 0.008 - x * 0.014)
            bend = 0.008 * sin(x * 0.071 + y * 0.043) + 0.006 * sin(x * 0.021 - y * 0.059)
            best_zone, best_score = 0, float("inf")
            for zone, (_, _, (sx, sy), weight) in enumerate(seeds):
                dx, dy = warped_x - sx, warped_y - sy
                score = (dx * dx + dy * dy) / (weight * weight) + bend * (zone + 1) * 24000
                if score < best_score:
                    best_zone, best_score = zone, score
            ids[index] = best_zone + 1
            counts[best_zone] += 1
            sums[best_zone][0] += x
            sums[best_zone][1] += y
    master = Image.new("RGB", (width, height), (0, 0, 0))
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    master_pixels, edge_pixels = master.load(), overlay.load()
    for y in range(height):
        for x in range(width):
            index, zone = y * width + x, ids[y * width + x]
            if not zone:
                continue
            master_pixels[x, y] = seeds[zone - 1][1]
            neighbours = [
                ids[index - 1] if x else 0, ids[index + 1] if x + 1 < width else 0,
                ids[index - width] if y else 0, ids[index + width] if y + 1 < height else 0,
            ]
            if any(other == 0 for other in neighbours):
                edge_pixels[x, y] = (93, 145, 167, 205)
            elif any(other != zone for other in neighbours):
                edge_pixels[x, y] = (196, 154, 91, 220)
    master.save(ID_MASTER, optimize=True)
    overlay.save(EDGE_OVERLAY, optimize=True)
    metadata = {"size": [width, height], "zones": [
        {"name": name, "color": "#%02x%02x%02x" % color, "pixels": counts[index],
         "centre": [round(sums[index][0] / max(1, counts[index])), round(sums[index][1] / max(1, counts[index]))]}
        for index, (name, color, _, _) in enumerate(seeds)
    ]}
    METADATA.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    print("partition pixels:", counts)


if __name__ == "__main__":
    main()
