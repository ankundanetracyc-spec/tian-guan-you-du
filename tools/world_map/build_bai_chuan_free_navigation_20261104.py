"""Bake free-world navigation once; runtime never infers terrain from the art."""
from pathlib import Path
import argparse
import hashlib
import json
import math
import re

import numpy as np
from scipy import ndimage
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/acceptance_20261030'
CONFIG = ROOT / 'gdscripts/world/BaiChuanWorldScale.gd'
REVIEW = ROOT / 'output/bai_chuan_free_world_3x3_20261104'


def scale_config():
    """Read the same authored numeric constants used by the game."""
    text = CONFIG.read_text(encoding='utf-8')

    def number(name):
        match = re.search(rf'^const {name} := ([0-9.]+)\s*$', text, re.M)
        if not match:
            raise ValueError(f'Missing numeric map scale: {name}')
        return float(match.group(1))

    art = re.search(r'^const ART_BASE := "res://([^"]+)"', text, re.M).group(1)
    master = re.search(r'^const MASTER_FILE := "([^"]+)"', text, re.M).group(1)
    layout = re.search(r'^const LAYOUT_PATH := "res://([^"]+)"', text, re.M).group(1)
    navigation = re.search(r'^const NAVIGATION_PATH := "res://([^"]+)"', text, re.M).group(1)
    return {
        'source_size': int(number('LEGACY_MAP_PIXELS')),
        'world_size': number('MAP_PIXELS'),
        'previous_free_size': number('PREVIOUS_FREE_MAP_PIXELS'),
        'world_km': number('WORLD_KM'),
        'source_step': int(number('NAVIGATION_SOURCE_CELL_SIZE')),
        'columns': int(number('NAVIGATION_COLUMNS')),
        'art': ROOT / art / master,
        'layout': ROOT / layout,
        'navigation': ROOT / navigation,
    }


def classify_terrain(image, size, terrain_mask=None):
    if terrain_mask:
        with Image.open(terrain_mask) as source:
            # Indexed masks carry terrain in palette indices, not in the
            # display colour luminance of the human-readable review palette.
            indexed = source if source.mode in ('P', 'L') else source.convert('L')
            mask = np.asarray(indexed.resize((size, size), Image.Resampling.NEAREST))
        if not set(np.unique(mask)).issubset({0, 1, 2}):
            raise ValueError('Authored terrain mask must use 0=walkable, 1=mountain, 2=water.')
        return mask.copy(), 'authored_terrain_mask'

    # Only this offline bake examines the new illustration. Ink mountain faces
    # and pale blue waterways are separate from the runtime presentation layer.
    rgb = np.asarray(image.resize((size, size), Image.Resampling.LANCZOS)).astype(np.int16)
    r, g, b = rgb.transpose(2, 0, 1)
    brightness = rgb.mean(2)
    water = (b - r >= 20) & (b - g >= 7) & (brightness >= 108)
    # The grey-green forests have a warmer blue channel than the slate faces.
    # Grow mountain seeds inside their connected shaded face, retaining woods
    # as ordinary ground rather than inventing an impassable forest rule.
    rock_face = (brightness < 180) & (rgb.max(2) - rgb.min(2) < 66) & (b - g >= -12) & ~water
    rock_seed = rock_face & (b - g >= -6) & (g - r >= 9)
    rock = ndimage.binary_propagation(rock_seed, mask=rock_face)
    # Fill solid mountain faces without growing the surrounding valley bank.
    # The final native tiles have pale rock striations inside ink outlines;
    # close those face gaps before filling, keeping broad valleys separate.
    rock = ndimage.binary_fill_holes(ndimage.binary_closing(rock, iterations=8))
    rock = ndimage.binary_opening(rock, iterations=2)
    wet = ndimage.binary_dilation(water, iterations=3)
    blocked = np.zeros((size, size), np.uint8)
    blocked[rock] = 1
    blocked[wet] = 2
    return blocked, 'offline_new_art_masks'


def make_free_layout(source, blocked, config):
    """Keep place identities while moving obstructed entrances to nearby land."""
    size, step = config['source_size'], config['source_step']
    n, factor = config['columns'], config['world_size'] / size
    padded = np.pad(blocked, ((0, n * step - size), (0, n * step - size)), constant_values=2)
    terrain = padded.reshape(n, step, n, step).max(axis=(1, 3))
    clearance = ndimage.distance_transform_edt(terrain == 0)
    safe_y, safe_x = np.nonzero(clearance >= 2.0)
    safe_points = np.column_stack(((safe_x + 0.5) * step, (safe_y + 0.5) * step))
    adjusted = {}
    moves = []
    for node_id, place in source['locations'].items():
        original = np.array(place['drawPos'], dtype=float)
        x, y = original
        terrain_type = int(terrain[int(y // step), int(x // step)])
        chosen = original
        if terrain_type:
            squared = ((safe_points - original) ** 2).sum(axis=1)
            closest = int(squared.argmin())
            if squared[closest] > 160 ** 2:
                raise RuntimeError(f'No natural land within the same region for {node_id}: {original.tolist()}')
            chosen = safe_points[closest].copy()
        adjusted[node_id] = chosen.tolist()
        distance = float(np.linalg.norm(chosen - original))
        if distance:
            moves.append({'id': node_id, 'from': original.tolist(), 'to': chosen.tolist(),
                          'source_distance': round(distance, 3), 'virtual_distance': round(distance * factor, 3),
                          'original_terrain': {1: 'mountain', 2: 'water'}[terrain_type]})

    result = json.loads(json.dumps(source))
    result.update(version='free_layout_8000_20261104', worldSize=config['world_size'],
                  worldKm=config['world_km'], coordinateScale=factor, displayOnly=False,
                  sourceWorldSize=size, anchorAdjustments=moves)
    for node_id, place in result['locations'].items():
        old = source['locations'][node_id]
        old_x, old_y = old['drawPos']
        x, y = (v * factor for v in adjusted[node_id])
        left, top, right, bottom = old.get('rect', [old_x - 7, old_y - 7, old_x + 7, old_y + 7])
        place['drawPos'] = [x, y]
        place['rect'] = [x + left - old_x, y + top - old_y, x + right - old_x, y + bottom - old_y]
        place['sourceDrawPos'] = old['drawPos']
    result['transitPositions'] = {k: [p[0] * factor, p[1] * factor] for k, p in source['transitPositions'].items()}
    for key, points in result['displayEdges'].items():
        edge = source['displayGraph']['edges'][key]
        points[0] = adjusted.get(edge['from'], points[0])
        points[-1] = adjusted.get(edge['to'], points[-1])
        result['displayEdges'][key] = [[p[0] * factor, p[1] * factor] for p in points]
    return result, adjusted, moves


def main():
    config = scale_config()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--art', type=Path, default=config['art'])
    parser.add_argument('--terrain-mask', type=Path)
    parser.add_argument('--preserve-topology-from', type=Path,
                        help='Keep the known previous 8000 km grid when only display scale changes.')
    parser.add_argument('--review-dir', type=Path, default=REVIEW)
    args = parser.parse_args()
    args.review_dir.mkdir(parents=True, exist_ok=True)
    with Image.open(args.art) as source:
        image = source.convert('RGB')
    if image.width != image.height:
        raise ValueError('The continuous world illustration must be square.')
    size, step = config['source_size'], config['source_step']
    factor = config['world_size'] / size
    cell_size = step * factor
    blocked, terrain_source = classify_terrain(image, size, args.terrain_mask)
    layout_bytes = (BASE / 'display_layout_v3.json').read_bytes()
    layout = json.loads(layout_bytes)
    if int(layout['worldSize']) != size:
        raise ValueError('Authored locations no longer match the source coordinate space.')

    free_layout, named_positions, anchor_moves = make_free_layout(layout, blocked, config)
    free_layout['artSha256'] = hashlib.sha256(args.art.read_bytes()).hexdigest()
    free_layout_bytes = (json.dumps(free_layout, ensure_ascii=False, indent=2) + '\n').encode('utf-8')

    roads = Image.new('L', (size, size))
    draw = ImageDraw.Draw(roads)
    for points in free_layout['displayEdges'].values():
        draw.line([(p[0] / factor, p[1] / factor) for p in points], fill=255, width=18, joint='curve')
    positions = {**named_positions, **layout['transitPositions']}
    for x, y in positions.values():
        draw.ellipse((x - 13, y - 13, x + 13, y + 13), fill=255)
    # Existing bridge/ferry/mountain-gate geometry remains a crossing corridor.
    # Its anonymous indices do not create visible or interactive world nodes.
    blocked[np.asarray(roads) > 0] = 0
    n = math.ceil(size / step)
    if n != config['columns']:
        raise ValueError('Navigation grid dimensions disagree with shared world scale.')
    padded = np.pad(blocked, ((0, n * step - size), (0, n * step - size)), constant_values=2)
    grid = padded.reshape(n, step, n, step).max(axis=(1, 3))
    # Preserve a centerline at the same proportional resolution after scaling.
    # Draw in exact logical cell coordinates. Resizing the padded raster would
    # drift at the far edge and could leave a blocked orthogonal diagonal corner.
    grid_roads = Image.new('L', (n, n))
    grid_draw = ImageDraw.Draw(grid_roads)
    for points in free_layout['displayEdges'].values():
        grid_draw.line([(p[0] / cell_size, p[1] / cell_size) for p in points], fill=255, width=3, joint='curve')
    road_grid = np.asarray(grid_roads) > 0
    # Supercover the authored centreline using the runtime's diagonal rule.
    # A raster stroke can miss one corner cell even while the line looks joined.
    for points in free_layout['displayEdges'].values():
        for a, b in zip(points, points[1:]):
            a, b = np.array(a), np.array(b)
            count = max(1, math.ceil(np.linalg.norm(b - a) / (cell_size * 0.125)))
            previous = np.floor(a / cell_size).astype(int)
            for i in range(count + 1):
                current = np.floor((a + (b - a) * i / count) / cell_size).astype(int)
                x, y = current
                road_grid[y, x] = True
                if current[0] != previous[0] and current[1] != previous[1]:
                    road_grid[current[1], previous[0]] = True
                    road_grid[previous[1], current[0]] = True
                previous = current
    grid[road_grid] = 0
    for x, y in positions.values():
        grid[int(y // step), int(x // step)] = 0

    topology_source = None
    if args.preserve_topology_from:
        previous_bytes = args.preserve_topology_from.read_bytes()
        previous = json.loads(previous_bytes)
        if (previous.get('version') != 2 or
                previous.get('world_size') != config['previous_free_size'] or
                previous.get('world_km') != config['world_km'] or
                previous.get('columns') != n or
                previous.get('source_raster_size') != size or
                previous.get('source_cell_size') != step or
                previous.get('art_sha256') != free_layout['artSha256'] or
                previous.get('source_layout_sha256') != hashlib.sha256(layout_bytes).hexdigest() or
                previous.get('terrain_source') != terrain_source or
                previous.get('terrain_mask_sha256') != (hashlib.sha256(args.terrain_mask.read_bytes()).hexdigest() if args.terrain_mask else None)):
            raise ValueError('Previous topology does not match the known scale, source illustration and authored layout.')
        previous_rows = previous.get('rows', [])
        if len(previous_rows) != n or any(len(row) != n or set(row) - {'0', '1', '2'} for row in previous_rows):
            raise ValueError('Previous topology has invalid grid rows.')
        preserved_grid = np.array([[int(cell) for cell in row] for row in previous_rows], dtype=np.uint8)
        topology_source = {'path': str(args.preserve_topology_from.relative_to(ROOT).as_posix()),
                           'sha256': hashlib.sha256(previous_bytes).hexdigest(),
                           'avoided_raster_rounding_changes': int((grid != preserved_grid).sum())}
        grid = preserved_grid

    labels, _ = ndimage.label(grid == 0, structure=np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]]))
    groups = {int(labels[int(y // step), int(x // step)]) for x, y in positions.values()}
    named_groups = {int(labels[int(y // step), int(x // step)]) for x, y in named_positions.values()}
    if 0 in groups or len(groups) != 1 or len(named_groups) != 1:
        raise RuntimeError(f'Authored anchors disconnected: {groups}')
    rows = [''.join(map(str, row)) for row in grid.tolist()]
    source_path = args.art.resolve()
    try:
        art_path = 'res://' + source_path.relative_to(ROOT).as_posix()
    except ValueError:
        art_path = str(source_path)
    result = {
        'version': 2, 'world_size': config['world_size'], 'world_km': config['world_km'],
        'cell_size': cell_size, 'columns': n, 'rows': rows,
        'legend': {'0': 'walkable', '1': 'mountain_face', '2': 'water'},
        'art_path': art_path, 'art_native_size': list(image.size),
        'art_sha256': hashlib.sha256(args.art.read_bytes()).hexdigest(),
        'layout_path': 'res://' + config['layout'].relative_to(ROOT).as_posix(),
        'layout_sha256': hashlib.sha256(free_layout_bytes).hexdigest(),
        'source_layout_sha256': hashlib.sha256(layout_bytes).hexdigest(),
        'coordinate_scale': factor, 'source_raster_size': size,
        'source_cell_size': step, 'terrain_source': terrain_source,
        'source': 'New free-world terrain baked offline, with authored crossing corridors.',
    }
    if args.terrain_mask:
        result['terrain_mask_sha256'] = hashlib.sha256(args.terrain_mask.read_bytes()).hexdigest()
    if topology_source:
        result['preserved_topology'] = topology_source
    config['navigation'].parent.mkdir(parents=True, exist_ok=True)
    config['layout'].write_bytes(free_layout_bytes)
    config['navigation'].write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    # These are review artifacts, never loaded by the runtime as navigation.
    palette = Image.fromarray(grid, mode='P')
    palette.putpalette([232, 220, 192, 123, 71, 62, 72, 124, 172] + [0] * (256 * 3 - 9))
    palette.save(args.review_dir / 'terrain_mask_471.png')
    overlay = image.resize((1410, 1410)).convert('RGBA')
    colors = np.zeros((n, n, 4), np.uint8)
    colors[grid == 1] = [110, 32, 24, 155]
    colors[grid == 2] = [15, 75, 175, 165]
    overlay = Image.alpha_composite(overlay, Image.fromarray(colors).resize(overlay.size, Image.Resampling.NEAREST))
    overlay.save(args.review_dir / 'navigation_review.png')
    audit = {
        'anchors': len(positions), 'places': len(named_positions),
        'connected_anchor_components': len(groups), 'connected_place_components': len(named_groups),
        'world_size': config['world_size'], 'world_km': config['world_km'],
        'cell_size': cell_size, 'grid': [n, n],
        'walkable_cells': int((grid == 0).sum()), 'mountain_cells': int((grid == 1).sum()),
        'water_cells': int((grid == 2).sum()),
        'art_path': art_path, 'art_sha256': result['art_sha256'],
        'adjusted_place_entrances': len(anchor_moves), 'anchor_adjustments': anchor_moves,
        'sha256': hashlib.sha256(config['navigation'].read_bytes()).hexdigest(),
    }
    if topology_source:
        audit['preserved_topology'] = topology_source
    old_points = np.array([place['drawPos'] for place in layout['locations'].values()], dtype=float)
    new_points = np.array([place['drawPos'] for place in free_layout['locations'].values()], dtype=float)
    def nearest_distances(points):
        distances = np.linalg.norm(points[:, None, :] - points[None, :, :], axis=2)
        np.fill_diagonal(distances, np.inf)
        return distances.min(axis=1)
    old_nearest = nearest_distances(old_points)
    new_nearest = nearest_distances(new_points)
    audit['place_spacing'] = {
        'old_pixels': {'min': float(old_nearest.min()), 'median': float(np.median(old_nearest))},
        'new_virtual_pixels': {'min': float(new_nearest.min()), 'median': float(np.median(new_nearest))},
        'new_kilometres': {'min': float(new_nearest.min() * config['world_km'] / config['world_size']),
                           'median': float(np.median(new_nearest) * config['world_km'] / config['world_size'])},
        'median_visual_spacing_factor': float(np.median(new_nearest) / np.median(old_nearest)),
    }
    (args.review_dir / 'navigation_audit.json').write_text(json.dumps(audit, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(audit))


if __name__ == '__main__':
    main()
