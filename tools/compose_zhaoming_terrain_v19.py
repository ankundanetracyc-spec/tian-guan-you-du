# -*- coding: utf-8 -*-
"""Finalize the v19 terrain: retain the generated relief and add restrained roads."""
from pathlib import Path

from PIL import Image, ImageDraw


SOURCE = Path(r"C:\Users\34784\.codex\generated_images\019fd814-9245-70f0-83fb-c97b952236b0\exec-6ff9f398-2c14-42c2-a3cc-65487958c933.png")
OUT = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v19_compact_landmarks.png")
HIGHRES_OUT = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v19_compact_landmarks_4096.png")

# Each route is deliberately local.  Roads describe settled corridors, not
# direct node-to-node graph edges; no route crosses sea or spans the continent.
ROUTES = [
    [(0.14, 0.46), (0.19, 0.42), (0.25, 0.44), (0.29, 0.40)],
    [(0.29, 0.40), (0.35, 0.43), (0.41, 0.46), (0.47, 0.43)],
    [(0.47, 0.43), (0.52, 0.48), (0.56, 0.54), (0.61, 0.57)],
    [(0.49, 0.51), (0.53, 0.57), (0.50, 0.63), (0.45, 0.68)],
    [(0.34, 0.62), (0.39, 0.65), (0.45, 0.68)],
    [(0.62, 0.50), (0.68, 0.46), (0.73, 0.43), (0.79, 0.46)],
    [(0.60, 0.61), (0.67, 0.66), (0.72, 0.70), (0.76, 0.75)],
    [(0.24, 0.71), (0.30, 0.74), (0.35, 0.79), (0.39, 0.84)],
]


def points(route, width, height):
    return [(round(x * width), round(y * height)) for x, y in route]


def main() -> None:
    if not SOURCE.exists():
        raise RuntimeError(f"Generated terrain source missing: {SOURCE}")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    terrain = Image.open(SOURCE).convert("RGBA")
    roads = Image.new("RGBA", terrain.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(roads)
    width, height = terrain.size
    for route in ROUTES:
        path = points(route, width, height)
        # Pale shoulder keeps the road readable on both snow and grass; the
        # darker centre stays narrow enough to be a terrain feature, not UI.
        draw.line(path, fill=(244, 236, 204, 120), width=max(2, width // 430), joint="curve")
        draw.line(path, fill=(153, 128, 82, 145), width=max(1, width // 900), joint="curve")
    result = Image.alpha_composite(terrain, roads).convert("RGB")
    result.save(OUT, optimize=True)
    # The compact source remains available for cheap political-mask rebuilds;
    # the map renderer receives this smooth 4096px presentation texture.
    result.resize((4096, 4096), Image.Resampling.LANCZOS).save(HIGHRES_OUT, optimize=True)
    print(OUT, terrain.size, HIGHRES_OUT)


if __name__ == "__main__":
    main()
