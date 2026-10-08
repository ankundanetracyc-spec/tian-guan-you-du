"""Build the first mainland from the user's black/white land mask.

The mask is the only source of geometry. All visual layers are authored in
the project palette and exported independently so the ocean, terrain, routes,
labels and interaction layers can be composed later in Godot.
"""

from __future__ import annotations

import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[3]
SOURCE_MASK = Path(
    r"C:\Users\34784\AppData\Local\Temp\codex-clipboard-0cb07ece-9688-4beb-8115-79121c07912f.png"
)
OUT = ROOT / "textures" / "world_map" / "legacy" / "continent_01" / "assets"
OUT.mkdir(parents=True, exist_ok=True)
MOUNTAIN_ASSET_DIR = OUT / "mountain_assets"
# The exported visual assets use a 180-degree mainland presentation. Each
# mountain bitmap is counter-rotated before the complete layer is baked so the
# geography flips while the mountain illustration stays upright.
MAP_PRESENTATION_ROTATION_DEGREES = 180

# Visual layers share one tight crop around the continent.  The logical map
# canvas remains the native mask size so travel coordinates and hit testing do
# not change; this padding only keeps a small paper margin around the coast.
VISUAL_PADDING = 24
# A small blur/threshold pass removes single-pixel coastline noise while
# preserving the supplied silhouette, bays and the inner gulf.
COAST_SMOOTH_RADIUS = 16.0
COAST_BOUNDARY_SNAP_RADIUS = 48


# The three states follow the user's orange sketch: a near-vertical western
# split and a rising eastward split leave one block on the left, one above and
# one below on the right. Both chords terminate on the actual land boundary;
# the binary land mask remains the source of truth for coast clipping and later
# hit testing. Straight authoring segments are intentional here and can later
# receive a small hand-placed bend where terrain calls for it.
# Stored as (y, x), with endpoints snapped to the supplied coastline mask.
LEFT_BOUNDARY = [
    (190, 1264), (1133, 1387),
]
# Stored as (x, y), with the first endpoint sharing the inner-gulf tip with the
# western split and the second endpoint snapped to the east coast. This
# separates the upper-right and lower-right blocks.
RIGHT_BOUNDARY = [
    (1387, 1133), (2991, 790),
]

# A narrow barrier is used only while partitioning the land mask. The visible
# boundary is drawn independently below, so these pixels can still be assigned
# to a state for tinting and hit testing.
BOUNDARY_BARRIER_WIDTH = 18
STATE_SEEDS = {
    "guanhe_state": (480, 520),
    "xiangshan_state": (2000, 500),
    "yunqu_state": (2300, 1500),
}

STATE_META = {
    "xiangshan_state": {
        "name": "相山州", "order": 1, "color": (145, 161, 161), "color_hex": "#91A1A1"
    },
    "guanhe_state": {
        "name": "关河州", "order": 2, "color": (177, 145, 105), "color_hex": "#B19169"
    },
    "yunqu_state": {
        "name": "云阙州", "order": 3, "color": (137, 158, 124), "color_hex": "#899E7C"
    },
}


def _binary_mask(source: Image.Image) -> Image.Image:
    """Normalize the supplied black/white image without changing its shape."""

    values = np.asarray(source.convert("L"))
    land = values >= 128
    # Determine polarity from the image border. The supplied mask has black
    # ocean at the corners, so land is white even though the two areas are
    # close in size; a simple majority test would invert this particular map.
    corner_values = np.asarray([
        values[0, 0], values[0, -1], values[-1, 0], values[-1, -1]
    ])
    if float(corner_values.mean()) >= 128.0:
        land = ~land
    return Image.fromarray((land.astype(np.uint8) * 255), mode="L")


def _smooth_coastline(mask: Image.Image) -> Image.Image:
    """Round tiny raster teeth without materially changing the coastline."""

    softened = mask.filter(ImageFilter.GaussianBlur(COAST_SMOOTH_RADIUS))
    values = np.asarray(softened) >= 128
    return Image.fromarray((values.astype(np.uint8) * 255), mode="L")


def _snap_land_point(x: int, y: int, land_mask: Image.Image) -> tuple[int, int]:
    """Snap an authored boundary endpoint to the nearest smoothed land pixel."""

    land = np.asarray(land_mask) > 0
    height, width = land.shape
    left = max(0, x - COAST_BOUNDARY_SNAP_RADIUS)
    top = max(0, y - COAST_BOUNDARY_SNAP_RADIUS)
    right = min(width, x + COAST_BOUNDARY_SNAP_RADIUS + 1)
    bottom = min(height, y + COAST_BOUNDARY_SNAP_RADIUS + 1)
    ys, xs = np.where(land[top:bottom, left:right])
    if len(xs) == 0:
        raise RuntimeError(f"Could not snap boundary endpoint ({x}, {y}) to land")
    xs = xs + left
    ys = ys + top
    distances = (xs - x) ** 2 + (ys - y) ** 2
    index = int(np.argmin(distances))
    return int(xs[index]), int(ys[index])


def _smooth_curve(points: list[tuple[float, float]], samples_per_segment: int = 18) -> np.ndarray:
    """Return a gently smoothed Catmull–Rom curve through the author points."""

    values = np.asarray(points, dtype=float)
    padded = np.vstack([values[0], values, values[-1]])
    curve: list[np.ndarray] = []
    for index in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[index - 1:index + 3]
        for sample in range(samples_per_segment):
            t = sample / samples_per_segment
            t2, t3 = t * t, t * t * t
            point = 0.5 * (
                (2.0 * p1)
                + (-p0 + p2) * t
                + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
                + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
            )
            curve.append(point)
    curve.append(values[-1])
    return np.asarray(curve)


def _interpolated_left_x_boundary(y_grid: np.ndarray) -> np.ndarray:
    curve = _smooth_curve(LEFT_BOUNDARY)
    # Once the watershed reaches the inner gulf, no western state continues
    # below it; this keeps the lower-right block compact.
    return np.interp(y_grid, curve[:, 0], curve[:, 1], left=curve[0, 1], right=0.0)


def _interpolated_right_y_boundary(x_grid: np.ndarray) -> np.ndarray:
    curve = _smooth_curve(RIGHT_BOUNDARY)
    return np.interp(x_grid, curve[:, 0], curve[:, 1], left=curve[0, 1], right=curve[-1, 1])


def _state_barrier_mask(size: tuple[int, int]) -> np.ndarray:
    """Rasterize the authored coast-to-coast splits for flood partitioning."""

    barrier = Image.new("L", size, 0)
    draw = ImageDraw.Draw(barrier)
    draw.line(
        [(int(x), int(y)) for y, x in LEFT_BOUNDARY],
        fill=255,
        width=BOUNDARY_BARRIER_WIDTH,
        joint="curve",
    )
    draw.line(
        [(int(x), int(y)) for x, y in RIGHT_BOUNDARY],
        fill=255,
        width=BOUNDARY_BARRIER_WIDTH,
        joint="curve",
    )
    return np.asarray(barrier) > 0


def _flood_component(passable: np.ndarray, seed: tuple[int, int]) -> np.ndarray:
    """Return one 4-connected land component using an iterative span fill."""

    height, width = passable.shape
    sx, sy = seed
    visited = np.zeros((height, width), dtype=bool)
    if not (0 <= sx < width and 0 <= sy < height and passable[sy, sx]):
        return visited

    stack = [(int(sx), int(sy))]
    while stack:
        x, y = stack.pop()
        if not (0 <= x < width and 0 <= y < height):
            continue
        if not passable[y, x] or visited[y, x]:
            continue

        # Expand to the complete unvisited span on this row, then enqueue the
        # contiguous spans immediately above and below it.
        left = x
        while left >= 0 and passable[y, left] and not visited[y, left]:
            left -= 1
        left += 1

        cursor = left
        span_up = False
        span_down = False
        while cursor < width and passable[y, cursor] and not visited[y, cursor]:
            visited[y, cursor] = True

            if y > 0:
                above = passable[y - 1, cursor] and not visited[y - 1, cursor]
                if above and not span_up:
                    stack.append((cursor, y - 1))
                    span_up = True
                elif not above:
                    span_up = False

            if y + 1 < height:
                below = passable[y + 1, cursor] and not visited[y + 1, cursor]
                if below and not span_down:
                    stack.append((cursor, y + 1))
                    span_down = True
                elif not below:
                    span_down = False

            cursor += 1

    return visited


def _fill_unassigned_state_pixels(
    labels: np.ndarray,
    land: np.ndarray,
    state_order: list[str],
) -> np.ndarray:
    """Assign the line strip and tiny detached pixels without overlaps."""

    remaining = land & (labels == 0)
    # Grow labels through the removed boundary strip. This keeps each strip
    # pixel attached to the nearest already-partitioned state.
    for _ in range(BOUNDARY_BARRIER_WIDTH + 2):
        if not remaining.any():
            break
        candidate = np.zeros_like(labels)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            shifted = np.roll(labels, (dy, dx), axis=(0, 1))
            if dy < 0:
                shifted[dy:, :] = 0
            elif dy > 0:
                shifted[:dy, :] = 0
            if dx < 0:
                shifted[:, dx:] = 0
            elif dx > 0:
                shifted[:, :dx] = 0
            candidate[(candidate == 0) & (shifted > 0)] = shifted[
                (candidate == 0) & (shifted > 0)
            ]
        newly = remaining & (candidate > 0)
        if not newly.any():
            break
        labels[newly] = candidate[newly]
        remaining = land & (labels == 0)

    # The source mask contains a few isolated one-pixel coastline artefacts.
    # Give any still-unassigned land pixel the closest authored state seed so
    # the three masks remain a complete, disjoint partition of the land.
    if remaining.any():
        ys, xs = np.where(remaining)
        seed_array = np.asarray([STATE_SEEDS[state_id] for state_id in state_order], dtype=float)
        distances = np.stack([
            (xs - seed_x) ** 2 + (ys - seed_y) ** 2
            for seed_x, seed_y in seed_array
        ], axis=1)
        labels[ys, xs] = np.argmin(distances, axis=1).astype(np.uint8) + 1
    return labels


def build_state_masks(land_mask: Image.Image) -> dict[str, Image.Image]:
    """Partition the supplied coast using the two authored boundary chords."""

    land = np.asarray(land_mask) > 0
    barrier = _state_barrier_mask(land_mask.size)
    passable = land & ~barrier
    state_order = ["guanhe_state", "xiangshan_state", "yunqu_state"]

    labels = np.zeros(land.shape, dtype=np.uint8)
    for label, state_id in enumerate(state_order, start=1):
        component = _flood_component(passable, STATE_SEEDS[state_id])
        if not component.any():
            raise RuntimeError(f"State seed is outside its partition: {state_id}")
        labels[component] = label

    labels = _fill_unassigned_state_pixels(labels, land, state_order)
    if np.any(land & (labels == 0)) or np.any((labels > 0) & ~land):
        raise RuntimeError("State masks do not cover the land mask exactly")

    return {
        state_id: Image.fromarray((labels == label).astype(np.uint8) * 255, mode="L")
        for label, state_id in enumerate(state_order, start=1)
    }


def _clip_alpha(image: Image.Image, mask: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    channels = np.asarray(rgba).copy()
    channels[:, :, 3] = np.minimum(channels[:, :, 3], np.asarray(mask))
    return Image.fromarray(channels, mode="RGBA")


def _visual_crop_rect(land_mask: Image.Image) -> tuple[int, int, int, int]:
    """Return one shared visual crop rect around the occupied coastline."""

    values = np.asarray(land_mask) > 0
    ys, xs = np.where(values)
    if len(xs) == 0:
        raise RuntimeError("The source land mask contains no land pixels")
    width, height = land_mask.size
    left = max(0, int(xs.min()) - VISUAL_PADDING)
    top = max(0, int(ys.min()) - VISUAL_PADDING)
    right = min(width, int(xs.max()) + VISUAL_PADDING + 1)
    bottom = min(height, int(ys.max()) + VISUAL_PADDING + 1)
    return left, top, right, bottom


def _crop_visual(image: Image.Image, crop_rect: tuple[int, int, int, int]) -> Image.Image:
    """Crop a visual layer without changing its logical coordinate metadata."""

    return image.crop(crop_rect)


def _rotate_presentation(image: Image.Image) -> Image.Image:
    """Bake the chosen map presentation rotation into one visual asset."""

    if MAP_PRESENTATION_ROTATION_DEGREES % 360 == 180:
        return image.transpose(Image.Transpose.ROTATE_180)
    if MAP_PRESENTATION_ROTATION_DEGREES % 360 == 90:
        return image.transpose(Image.Transpose.ROTATE_90)
    if MAP_PRESENTATION_ROTATION_DEGREES % 360 == 270:
        return image.transpose(Image.Transpose.ROTATE_270)
    return image.copy()


def _rotate_visual_crop(image: Image.Image, crop_rect: tuple[int, int, int, int]) -> Image.Image:
    """Rotate a cropped layer around the shared visual crop center."""

    return _rotate_presentation(_crop_visual(image, crop_rect))


def _rotate_full_visual_crop(image: Image.Image, crop_rect: tuple[int, int, int, int]) -> Image.Image:
    """Keep full logical dimensions while rotating only the shared visual area."""

    left, top, right, bottom = crop_rect
    rotated_crop = _rotate_visual_crop(image, crop_rect)
    if image.mode == "RGBA":
        result = Image.new(image.mode, image.size, (0, 0, 0, 0))
        result.alpha_composite(rotated_crop, (left, top))
    else:
        result = Image.new(image.mode, image.size, 0)
        result.paste(rotated_crop, (left, top))
    return result


def _texture_base(mask: Image.Image) -> Image.Image:
    """Create a restrained grey-green xuan-paper wash with fake depth."""

    width, height = mask.size
    rng = np.random.default_rng(20260916)
    coarse = rng.normal(0.0, 1.0, (height, width)).astype(np.float32)
    coarse = np.clip(coarse * 32.0 + 128.0, 0, 255).astype(np.uint8)
    low = np.asarray(
        Image.fromarray(coarse, mode="L").filter(ImageFilter.GaussianBlur(30)),
        dtype=np.float32,
    )
    low = (low - low.mean()) / max(low.std(), 1.0)

    # Use broadcastable one-dimensional grids so the 32-bit Windows editor
    # does not need several extra full-size int64 arrays at once.
    y_grid = np.arange(height, dtype=np.float32)[:, None]
    x_grid = np.arange(width, dtype=np.float32)[None, :]
    wash = 3.2 * np.sin(x_grid / 310.0) + 2.1 * np.cos(y_grid / 260.0)
    # The final asset is rotated 180 degrees. Author the quiet/far wash at the
    # source bottom so it lands at the displayed top; the displayed lower edge
    # becomes the slightly warmer/denser foreground. This is a paper-map depth
    # cue, not a 3D camera tilt.
    depth = 1.0 - np.clip(y_grid.astype(np.float32) / max(height - 1, 1), 0.0, 1.0)
    far = np.array([173.0, 183.0, 180.0], dtype=np.float32)
    near = np.array([196.0, 190.0, 173.0], dtype=np.float32)
    base = far[None, None, :] * (1.0 - depth[:, :, None]) + near[None, None, :] * depth[:, :, None]
    rgb = np.empty((height, width, 3), dtype=np.float32)
    for channel, (far_value, near_value, noise_scale, wash_scale) in enumerate(
         ((far[0], near[0], 4.0, 0.72),
         (far[1], near[1], 3.6, 0.62),
         (far[2], near[2], 3.0, 0.34))
    ):
        rgb[:, :, channel] = (
            far_value + depth * (near_value - far_value)
            + low * noise_scale + wash * wash_scale
        )
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)

    rgba = np.empty((height, width, 4), dtype=np.uint8)
    rgba[:, :, :3] = rgb
    rgba[:, :, 3] = np.asarray(mask)
    return Image.fromarray(rgba, mode="RGBA")


def _state_tint(masks: dict[str, Image.Image], size: tuple[int, int]) -> Image.Image:
    tint = Image.new("RGBA", size, (0, 0, 0, 0))
    for state_id, state_mask in masks.items():
        colour = STATE_META[state_id]["color"]
        wash = Image.new("RGBA", size, (*colour, 0))
        # A low opacity wash keeps the paper and future terrain visible.
        alpha = state_mask.point(lambda value: int(value * 78 / 255))
        wash.putalpha(alpha)
        tint = Image.alpha_composite(tint, wash)
    return tint


def _draw_mountain_chain(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]],
                         peak_height: int, peak_width: int) -> None:
    for index, (x, y) in enumerate(points):
        offset = ((index % 3) - 1) * 18
        px, py = x + offset, y
        height = peak_height + (index % 2) * 18
        left, right = px - peak_width // 2, px + peak_width // 2
        ridge = [(left, py + 22), (px - 18, py - height // 2),
                 (px, py - height), (px + 22, py - height // 3), (right, py + 22)]
        draw.polygon(ridge, fill=(77, 82, 74, 36))
        draw.line([(left, py + 22), (px, py - height), (right, py + 22)],
                  fill=(65, 69, 63, 120), width=5, joint="curve")
        draw.line([(px, py - height), (px - 10, py - height // 3), (px - 46, py + 8)],
                  fill=(70, 74, 66, 95), width=3)
        draw.line([(px, py - height), (px + 18, py - height // 3), (px + 48, py + 8)],
                  fill=(70, 74, 66, 85), width=3)


def _draw_forest_cluster(draw: ImageDraw.ImageDraw, center: tuple[int, int],
                         count: int, spread: int, seed: int) -> None:
    rng = np.random.default_rng(seed)
    cx, cy = center
    for _ in range(count):
        x = int(cx + rng.normal(0, spread))
        y = int(cy + rng.normal(0, spread * 0.55))
        tree_height = int(rng.integers(35, 70))
        tree_width = max(18, tree_height // 3)
        draw.line([(x, y + 10), (x, y - tree_height // 2)], fill=(61, 83, 69, 90), width=4)
        draw.polygon([(x, y - tree_height), (x - tree_width, y - tree_height // 3),
                      (x + tree_width, y - tree_height // 3)], fill=(66, 94, 75, 82))
        draw.polygon([(x, y - tree_height // 2), (x - tree_width - 6, y),
                      (x + tree_width + 6, y)], fill=(63, 91, 72, 64))


MOUNTAIN_SOURCE_FILES = {
    f"cluster_{index:02d}": MOUNTAIN_ASSET_DIR / f"mountain_cluster_{index:02d}.png"
    for index in range(1, 9)
}


def _trim_alpha(image: Image.Image, threshold: int = 8) -> Image.Image:
    """Trim only fully transparent atlas margins while keeping soft fog."""

    rgba = image.convert("RGBA")
    alpha = np.asarray(rgba.getchannel("A"))
    ys, xs = np.where(alpha > threshold)
    if len(xs) == 0:
        return rgba
    return rgba.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))


def _load_mountain_sprites() -> dict[str, Image.Image]:
    """Load the latest supplied transparent mountain batch."""

    MOUNTAIN_ASSET_DIR.mkdir(parents=True, exist_ok=True)
    sprites: dict[str, Image.Image] = {}
    for sprite_id, path in MOUNTAIN_SOURCE_FILES.items():
        if not path.exists():
            continue
        sprite = _trim_alpha(Image.open(path).convert("RGBA"), threshold=6)
        # The supplied batch has bright white fog pads. Tint the ink toward the
        # same grey-green paper family and lower only the brightest fog pixels,
        # so a cluster dissolves into the ground instead of reading as a sticker.
        rgba = np.asarray(sprite).copy()
        rgb = rgba[:, :, :3].astype(np.float32)
        luminance = (rgb[:, :, 0] * 0.299 + rgb[:, :, 1] * 0.587 + rgb[:, :, 2] * 0.114)
        ink_tint = np.array([0.74, 0.78, 0.75], dtype=np.float32)
        toned = luminance[:, :, None] * ink_tint[None, None, :]
        rgb = np.clip(rgb * 0.24 + toned * 0.76, 0.0, 255.0)
        fog = np.clip((luminance - 168.0) / 87.0, 0.0, 1.0)
        rgba[:, :, :3] = rgb.astype(np.uint8)
        rgba[:, :, 3] = np.clip(
            rgba[:, :, 3].astype(np.float32) * (1.0 - fog * 0.58), 0, 255
        ).astype(np.uint8)
        sprite = Image.fromarray(rgba, mode="RGBA")
        sprite.save(MOUNTAIN_ASSET_DIR / f"mountain_prepared_{sprite_id}.png")
        sprites[sprite_id] = sprite
    return sprites


def _paste_mountain(canvas: Image.Image, sprite: Image.Image, center: tuple[int, int],
                    width: int, opacity: float, vertical_scale: float) -> None:
    """Place one mountain that will be upright after the baked map rotation."""

    ratio = width / max(sprite.width, 1)
    height = max(1, int(sprite.height * ratio * vertical_scale))
    placed = sprite.resize((width, height), Image.Resampling.LANCZOS)
    # The final visual layer is rotated as a whole below. Counter-rotate the
    # bitmap now so the mountain illustration reads upright in that final asset.
    placed = placed.transpose(Image.Transpose.ROTATE_180)
    channels = np.asarray(placed).copy()
    channels[:, :, 3] = np.clip(channels[:, :, 3].astype(np.float32) * opacity, 0, 255).astype(np.uint8)
    placed = Image.fromarray(channels, mode="RGBA")
    x = int(center[0] - width / 2)
    y = int(center[1] - height / 2)
    canvas.alpha_composite(placed, (x, y))


def _assert_mountain_inside_land(
    sprite: Image.Image,
    center: tuple[int, int],
    width: int,
    land_mask: Image.Image,
    sprite_id: str,
    vertical_scale: float,
) -> None:
    """Fail early instead of silently slicing a mountain at the coastline."""

    height = max(1, int(sprite.height * width / max(sprite.width, 1) * vertical_scale))
    x = int(center[0] - width / 2)
    y = int(center[1] - height / 2)
    alpha = np.asarray(sprite.getchannel("A").resize((width, height), Image.Resampling.LANCZOS)) > 0
    mask = np.asarray(land_mask) > 0
    if x < 0 or y < 0 or x + width > mask.shape[1] or y + height > mask.shape[0]:
        raise RuntimeError(f"Mountain placement leaves the map canvas: {sprite_id}")
    if np.any(alpha & ~mask[y:y + height, x:x + width]):
        raise RuntimeError(f"Mountain placement touches the coastline: {sprite_id} {center}")


def _terrain_layer(mask: Image.Image) -> Image.Image:
    width, height = mask.size
    terrain = Image.new("RGBA", (width, height), (0, 0, 0, 0))

    # The supplied transparent mountain batch is placed at a restrained scale:
    # each cluster occupies only a small portion of a state, leaving room for
    # routes and interactive locations. North/far clusters are lighter;
    # south/near clusters are slightly darker for flat-map depth.
    sprites = _load_mountain_sprites()
    placements = [
        # Far layer: small, quiet ridges with generous spacing.
        ("cluster_03", (2050, 1320), 300, 0.23),
        ("cluster_07", (2250, 1450), 330, 0.25),
        ("cluster_02", (2450, 1600), 320, 0.22),
        ("cluster_08", (2550, 1650), 300, 0.24),
        # Middle layer: overlapping groups that establish the main terrain
        # rhythm without covering the entire state.
        ("cluster_08", (460, 1010), 380, 0.32),
        ("cluster_04", (1100, 900), 400, 0.35),
        ("cluster_05", (1900, 980), 430, 0.33),
        ("cluster_06", (2600, 1030), 400, 0.31),
        # Near layer: a few larger, darker anchors with low enough density to
        # leave open valleys for later locations and routes.
        ("cluster_03", (550, 620), 440, 0.48),
        ("cluster_02", (930, 520), 480, 0.50),
        ("cluster_07", (1500, 530), 480, 0.46),
        ("cluster_01", (2290, 520), 510, 0.45),
    ]
    for sprite_id, center, sprite_width, opacity in placements:
        sprite = sprites.get(sprite_id)
        if sprite is not None:
            # The oblique compression was intentionally removed. Perspective
            # is now supplied only by the single 180-degree layout transform.
            vertical_scale = 1.0
            _assert_mountain_inside_land(
                sprite, center, sprite_width, mask, sprite_id, vertical_scale
            )
            _paste_mountain(terrain, sprite, center, sprite_width, opacity, vertical_scale)

    return _clip_alpha(terrain, mask)


def _roads_layer(mask: Image.Image) -> Image.Image:
    width, height = mask.size
    roads = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(roads, "RGBA")
    provisional_routes = [
        [(380, 1380), (720, 1280), (1070, 1260), (1430, 1320), (1760, 1450),
         (2120, 1560), (2460, 1660)],
        [(1430, 1320), (1370, 1100), (1280, 900), (1190, 700)],
        [(1760, 1450), (1940, 1260), (2150, 1120), (2420, 1010)],
    ]
    for path in provisional_routes:
        draw.line(path, fill=(104, 82, 59, 38), width=18, joint="curve")
        draw.line(path, fill=(112, 88, 62, 105), width=5, joint="curve")
    return _clip_alpha(roads, mask)


def _boundary_layer(mask: Image.Image, size: tuple[int, int]) -> Image.Image:
    boundary = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(boundary, "RGBA")
    left_curve = _smooth_curve(LEFT_BOUNDARY)
    right_curve = _smooth_curve(RIGHT_BOUNDARY)
    lines = [
        [(int(x), int(y)) for y, x in left_curve],
        [(int(x), int(y)) for x, y in right_curve],
    ]
    for line in lines:
        draw.line(line, fill=(66, 61, 52, 44), width=24, joint="curve")
    for line in lines:
        draw.line(line, fill=(78, 70, 57, 148), width=7, joint="curve")
    return _clip_alpha(boundary, mask)


def _bezier_path(points: list[tuple[float, float]]) -> str:
    """Convert author points to a cubic SVG path with smooth joins."""

    values = np.asarray(points, dtype=float)
    padded = np.vstack([values[0], values, values[-1]])
    start = values[0]
    commands = [f"M {start[0]:.1f},{start[1]:.1f}"]
    for index in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[index - 1:index + 3]
        control_1 = p1 + (p2 - p0) / 6.0
        control_2 = p2 - (p3 - p1) / 6.0
        commands.append(
            f"C {control_1[0]:.1f},{control_1[1]:.1f} "
            f"{control_2[0]:.1f},{control_2[1]:.1f} {p2[0]:.1f},{p2[1]:.1f}"
        )
    return " ".join(commands)


def _write_boundary_svg(
    size: tuple[int, int], crop_rect: tuple[int, int, int, int] | None = None
) -> None:
    width, height = size
    left, top, right, bottom = crop_rect or (0, 0, width, height)
    crop_width, crop_height = right - left, bottom - top
    def presented_point(x: int, y: int) -> tuple[float, float]:
        local_x, local_y = x - left, y - top
        if MAP_PRESENTATION_ROTATION_DEGREES % 360 == 180:
            return crop_width - 1 - local_x, crop_height - 1 - local_y
        if MAP_PRESENTATION_ROTATION_DEGREES % 360 == 90:
            return crop_height - 1 - local_y, local_x
        if MAP_PRESENTATION_ROTATION_DEGREES % 360 == 270:
            return local_y, crop_width - 1 - local_x
        return local_x, local_y

    left_path = _bezier_path([presented_point(x, y) for y, x in LEFT_BOUNDARY])
    right_path = _bezier_path([presented_point(x, y) for x, y in RIGHT_BOUNDARY])
    svg = f'''<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="{crop_width}" height="{crop_height}" viewBox="0 0 {crop_width} {crop_height}">
  <g fill="none" stroke="#4e4639" stroke-opacity="0.72" stroke-width="7" stroke-linecap="round" stroke-linejoin="round">
    <path d="{left_path}" />
    <path d="{right_path}" />
  </g>
</svg>
'''
    (OUT / "continent_01_state_boundaries.svg").write_text(svg, encoding="utf-8")


def _save_state_id_mask(state_masks: dict[str, Image.Image], size: tuple[int, int]) -> None:
    ids = np.zeros((size[1], size[0], 3), dtype=np.uint8)
    for state_id, state_mask in state_masks.items():
        ids[np.asarray(state_mask) > 0] = (STATE_META[state_id]["order"], 0, 0)
    Image.fromarray(ids, mode="RGB").save(OUT / "continent_01_state_id_mask.png")


def main() -> None:
    global LEFT_BOUNDARY, RIGHT_BOUNDARY

    source = Image.open(SOURCE_MASK).convert("L")
    land_mask = _smooth_coastline(_binary_mask(source))
    # The smoothed coast can move a raster edge by a few pixels.  Re-snap only
    # the authored endpoints so both state lines still meet the new coastline.
    LEFT_BOUNDARY = [
        (snapped_y, snapped_x)
        for y, x in LEFT_BOUNDARY
        for snapped_x, snapped_y in [_snap_land_point(x, y, land_mask)]
    ]
    RIGHT_BOUNDARY = [
        (snapped_x, snapped_y)
        for x, y in RIGHT_BOUNDARY
        for snapped_x, snapped_y in [_snap_land_point(x, y, land_mask)]
    ]
    width, height = land_mask.size
    size = (width, height)
    visual_crop = _visual_crop_rect(land_mask)
    crop_left, crop_top, crop_right, crop_bottom = visual_crop
    visual_size = (crop_right - crop_left, crop_bottom - crop_top)
    presented_source = _rotate_full_visual_crop(source, visual_crop)
    presented_land_mask = _rotate_full_visual_crop(land_mask, visual_crop)
    presented_source.save(OUT / "continent_01_source_land_mask.png")
    presented_land_mask.save(OUT / "continent_01_land_mask.png")
    shutil.copyfile(SOURCE_MASK, OUT / "continent_01_source_land_mask_original.png")

    # Neutral cutout and project-palette base are intentionally separate.
    cutout = Image.new("RGBA", size, (216, 203, 171, 255))
    cutout.putalpha(land_mask)
    _rotate_visual_crop(cutout, visual_crop).save(OUT / "continent_01_land_cutout.png")
    land_base_full = _texture_base(land_mask)
    land_base = _rotate_visual_crop(land_base_full, visual_crop)
    land_base.save(OUT / "continent_01_land_base.png")

    state_masks = build_state_masks(land_mask)
    areas = {state_id: int(np.asarray(state_mask).sum() // 255) for state_id, state_mask in state_masks.items()}
    total_area = max(1, sum(areas.values()))
    presented_state_masks: dict[str, Image.Image] = {}
    for state_id, state_mask in state_masks.items():
        presented_state_mask = _rotate_full_visual_crop(state_mask, visual_crop)
        presented_state_masks[state_id] = presented_state_mask
        presented_state_mask.save(OUT / f"{state_id}_mask.png")

    tint_full = _state_tint(state_masks, size)
    tint = _rotate_visual_crop(tint_full, visual_crop)
    tint.save(OUT / "continent_01_state_tint.png")
    terrain_full = _terrain_layer(land_mask)
    terrain = _rotate_visual_crop(terrain_full, visual_crop)
    terrain.save(OUT / "continent_01_terrain.png")
    roads_full = _roads_layer(land_mask)
    roads = _rotate_visual_crop(roads_full, visual_crop)
    roads.save(OUT / "continent_01_roads.png")
    boundaries_full = _boundary_layer(land_mask, size)
    boundaries = _rotate_visual_crop(boundaries_full, visual_crop)
    boundaries.save(OUT / "continent_01_state_boundaries.png")
    _write_boundary_svg(size, visual_crop)
    _save_state_id_mask(presented_state_masks, size)

    # The normal editor/runtime reference stays geographically neutral. State
    # colour and border assets are retained separately and can be enabled by
    # an interaction layer when a player hovers or selects a territory.
    # This checkpoint is deliberately a pure terrain study: no road lines are
    # baked into the editor preview yet. The roads texture remains exported as
    # a separate future layer for the next composition pass.
    preview = Image.alpha_composite(land_base, terrain)
    preview.save(OUT / "continent_01_layer_preview.png")
    state_preview = Image.alpha_composite(land_base, tint)
    state_preview = Image.alpha_composite(state_preview, boundaries)
    state_preview.save(OUT / "continent_01_state_preview.png")

    catalogue = {
        "source_mask": "continent_01_source_land_mask_original.png",
        "source_mask_origin": str(SOURCE_MASK),
        "size": {"width": width, "height": height},
        "visual_crop": {
            "left": crop_left,
            "top": crop_top,
            "right": crop_right,
            "bottom": crop_bottom,
            "width": visual_size[0],
            "height": visual_size[1],
            "padding": VISUAL_PADDING,
            "origin": {
                "x": (crop_left + crop_right) / 2.0,
                "y": (crop_top + crop_bottom) / 2.0,
            },
        },
        "geometry": {
            "land_mask": "continent_01_land_mask.png",
            "cutout": "continent_01_land_cutout.png",
            "coast_is_transparent": True,
            "coastline_smoothing": {
                "method": "gaussian_threshold",
                "radius": COAST_SMOOTH_RADIUS,
                "source_mask": "continent_01_source_land_mask.png",
            },
            "presentation_rotation_degrees": MAP_PRESENTATION_ROTATION_DEGREES,
            "rotation_baked_into_visual_assets": True,
        },
        "terrain_sources": {
            "mountain_assets": [
                f"mountain_assets/mountain_cluster_{index:02d}.png"
                for index in range(1, 9)
            ],
            "mountain_style": "淡墨远近层叠、平面化斜俯视",
            "placement_policy": "主体与雾气均留在平滑大陆边界内",
            "presentation_rotation_degrees": MAP_PRESENTATION_ROTATION_DEGREES,
            "mountain_counter_rotation": True,
            "coast_clip_guard": True,
        },
        "partition": {
            "guanhe_xiangshan_boundary": LEFT_BOUNDARY,
            "xiangshan_yunqu_boundary": RIGHT_BOUNDARY,
            "curve": "straight_segments",
            "boundary_layer": "continent_01_state_boundaries.png",
            "boundary_vector": "continent_01_state_boundaries.svg",
            "presentation_rotation_degrees": MAP_PRESENTATION_ROTATION_DEGREES,
            "setting_notes": {
                "xiangshan_state": "东北高地与山海带，山系密集、通行受地形影响",
                "guanhe_state": "西部与西南荒域，盐泽、古渠和内湾之间保留大片无人地",
                "yunqu_state": "东南河湖腹地，聚落与交通最密集，面积自然偏大",
            },
        },
        "states": {
            state_id: {
                "name": STATE_META[state_id]["name"],
                "order": STATE_META[state_id]["order"],
                "color": STATE_META[state_id]["color_hex"],
                "mask": f"{state_id}_mask.png",
                "area_pixels": areas[state_id],
                "area_fraction": round(areas[state_id] / total_area, 4),
            }
            for state_id in state_masks
        },
    }
    (OUT / "continent_01_state_catalog.json").write_text(
        json.dumps(catalogue, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    manifest = {
        "canvas": {"width": width, "height": height},
        "coastline_smoothing": {
            "method": "gaussian_threshold",
            "radius": COAST_SMOOTH_RADIUS,
            "source_mask": "continent_01_source_land_mask.png",
        },
        "presentation_rotation_degrees": MAP_PRESENTATION_ROTATION_DEGREES,
        "rotation_baked_into_visual_assets": True,
        "visual_crop": {
            "left": crop_left,
            "top": crop_top,
            "right": crop_right,
            "bottom": crop_bottom,
            "width": visual_size[0],
            "height": visual_size[1],
            "padding": VISUAL_PADDING,
            "origin": {
                "x": (crop_left + crop_right) / 2.0,
                "y": (crop_top + crop_bottom) / 2.0,
            },
        },
        "source": "continent_01_source_land_mask_original.png",
        "terrain_sources": {
            "mountain_assets": [
                f"mountain_assets/mountain_cluster_{index:02d}.png"
                for index in range(1, 9)
            ],
            "mountain_placement": "coast_safe",
            "presentation_rotation_degrees": MAP_PRESENTATION_ROTATION_DEGREES,
            "mountain_counter_rotation": True,
            "coast_clip_guard": True,
        },
        "layers": [
            {"id": "ocean", "z_index": -120, "asset": None, "default_visible": False},
            {"id": "land_base", "z_index": -100, "asset": "continent_01_land_base.png", "default_visible": True},
            {"id": "state_tint", "z_index": -90, "asset": "continent_01_state_tint.png", "default_visible": False},
            {"id": "roads", "z_index": -85, "asset": "continent_01_roads.png", "default_visible": False},
            {"id": "terrain", "z_index": -80, "asset": "continent_01_terrain.png", "default_visible": True},
            {"id": "state_boundaries", "z_index": -50, "asset": "continent_01_state_boundaries.png", "default_visible": False},
            {"id": "territory_labels", "z_index": -50, "asset": None, "default_visible": True},
            {"id": "interaction", "z_index": 0, "asset": None, "default_visible": True},
            {"id": "player", "z_index": 10, "asset": None, "default_visible": True},
            {"id": "effects", "z_index": 20, "asset": None, "default_visible": True},
            {"id": "fog", "z_index": 30, "asset": None, "default_visible": False},
            {"id": "debug", "z_index": 999, "asset": None, "default_visible": False},
        ],
        "previews": {
            "states": "continent_01_state_preview.png",
            "all_static": "continent_01_layer_preview.png",
        },
    }
    (OUT / "continent_01_layer_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    print(f"Built {width}x{height} layers from {SOURCE_MASK.name}")
    for state_id, area in areas.items():
        print(f"{STATE_META[state_id]['name']}: {area / total_area:.2%}")


if __name__ == "__main__":
    main()
