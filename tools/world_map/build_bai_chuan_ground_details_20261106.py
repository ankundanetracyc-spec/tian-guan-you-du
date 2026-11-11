"""Place native, non-blocking ground details on the current expanded map.

Only this tool's new decoration JSON and its review directory are written.
Navigation, travel terrain, landscape art and all existing layouts are read-only.
The placement uses real navigation cells, displayed water, terrain, trees and
mountain distance; the legacy 575-edge road graph is deliberately never read.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path
import time

import numpy as np
from PIL import Image
from scipy import ndimage as ndi
from scipy.spatial import cKDTree


ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "resources/world/bai_chuan"
ART = ROOT / "textures/world/bai_chuan/ground_details_20261106"
OUT = ROOT / "output/bai_chuan_ground_details_20261106"
DEST = RES / "ground_decorations_20261106.json"
CATALOG = ART / "catalog.json"
PROFILE = "ground_details_20261106_v1"
SEED = 2026110607
WORLD = 22118.4
CHUNK = 640.0
Z = 24
MAX_COUNT = 12000
TARGET_COUNT = 11200
NEAR_ZOOM = 0.8
ASSETS = (
    "meadow_wash", "moss_wash", "sand_wash", "grass_green", "grass_dry",
    "wildflowers", "pebbles", "flat_stones", "shore_reeds",
)
WIDTHS = {
    "meadow_wash": (155, 220), "moss_wash": (135, 205),
    "sand_wash": (145, 215), "grass_green": (70, 114),
    "grass_dry": (70, 106), "wildflowers": (75, 112),
    "pebbles": (70, 112), "flat_stones": (70, 104),
    "shore_reeds": (70, 104),
}
HEIGHT_CAP = {
    "grass_green": 40, "grass_dry": 40, "wildflowers": 38,
    "pebbles": 32, "flat_stones": 34, "shore_reeds": 42,
}
PALETTES = {
    "meadow": (("meadow_wash", "moss_wash"),
               ("grass_green", "grass_green", "grass_dry", "wildflowers")),
    "forest_edge": (("moss_wash", "meadow_wash"),
                    ("grass_green", "grass_green", "wildflowers", "grass_dry")),
    "dry_plain": (("sand_wash", "meadow_wash"),
                  ("grass_dry", "grass_dry", "grass_green", "pebbles")),
    "foothill": (("sand_wash", "moss_wash"),
                 ("pebbles", "pebbles", "flat_stones", "grass_dry")),
    "shore": (("sand_wash", "moss_wash"),
              ("shore_reeds", "shore_reeds", "grass_green", "pebbles")),
}


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha(path: Path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rel(path: Path):
    return path.relative_to(ROOT).as_posix()


def resource(path: Path):
    return "res://" + rel(path)


def resolve_resource(path: str):
    return ROOT / path.removeprefix("res://")


def row_array(rows):
    return np.frombuffer("".join(rows).encode("ascii"), dtype=np.uint8).reshape(len(rows), -1)


def integral(mask):
    return np.pad(mask.astype(np.int32).cumsum(0, dtype=np.int32).cumsum(1, dtype=np.int32),
                  ((1, 0), (1, 0)))


def rectangle_cells(bounds, cell, shape):
    x, y, w, h = bounds
    xa, ya = math.floor(x / cell), math.floor(y / cell)
    xb, yb = math.ceil((x + w) / cell), math.ceil((y + h) / cell)
    return max(0, xa), max(0, ya), min(shape[1], xb), min(shape[0], yb)


def rect_sum(table, bounds, cell):
    xa, ya, xb, yb = rectangle_cells(bounds, cell, (table.shape[0]-1, table.shape[1]-1))
    return int(table[yb, xb] - table[ya, xb] - table[yb, xa] + table[ya, xa])


def intersects(a, b):
    return a[0] < b[0]+b[2] and b[0] < a[0]+a[2] and a[1] < b[1]+b[3] and b[1] < a[1]+a[3]


def expanded_rect(bounds, margin):
    x, y, w, h = bounds
    return [x-margin, y-margin, w+2*margin, h+2*margin]


def viewport(center, zoom, size=(1920, 1080)):
    w, h = size[0]/zoom, size[1]/zoom
    return [center[0]-w/2, center[1]-h/2, w, h]


class Inputs:
    def __init__(self):
        self.nav_path = RES / "free_navigation_expanded3x_20261106.json"
        self.terrain_path = RES / "terrain_travel_expanded3x_20261106.json"
        self.layout_path = RES / "free_layout_expanded3x_20261106.json"
        self.water_path = RES / "expanded_water_20261106.json"
        self.landscape_path = RES / "landscape_expanded3x_20261106.json"
        nav, terrain, layout, water, landscape = map(read, (
            self.nav_path, self.terrain_path, self.layout_path, self.water_path, self.landscape_path))
        if float(nav["world_size"]) != WORLD or float(layout["worldSize"]) != WORLD:
            raise ValueError("This tool only targets the current 22118.4-unit expanded map")
        self.cell = float(nav["cell_size"])
        self.nav = row_array(nav["rows"])
        self.n = self.nav.shape[0]
        self.terrain = row_array(terrain["rows"])
        self.terrain_cell = float(terrain["cell_size"])
        self.water = row_array(water["water_rows"]) == ord("2")
        self.bridge_open = row_array(water["bridge_open_rows"]) == ord("1")
        self.land = self.nav == ord("0")
        self.locations = layout["locations"]
        self.location_positions = np.array([v["drawPos"] for v in self.locations.values()])
        self.mountains = landscape["mountains"]
        self.trees = landscape["trees"]
        self.bridges = water["bridges"]
        self.reserved_buildings = []
        for ident, row in self.locations.items():
            x0, y0, x1, y1 = row["rect"]
            self.reserved_buildings.append((ident, expanded_rect([x0, y0, x1-x0, y1-y0], 45)))
        self.reserved_bridges = [(f"bridge_{i:02d}", expanded_rect(v["bounds"], 30))
                                 for i, v in enumerate(self.bridges)]
        # Exact rectangles own the 45/30-unit exclusion. The raster field is a
        # quick conservative prefilter, never a replacement for those bounds.
        reserved = np.zeros_like(self.land)
        for _, bounds in self.reserved_buildings + self.reserved_bridges:
            xa, ya, xb, yb = rectangle_cells(bounds, self.cell, self.land.shape)
            reserved[ya:yb, xa:xb] = True
        self.blocked_integral = integral(~self.land)
        self.reserved_integral = integral(reserved)
        self.mountain_integral = integral(self.nav == ord("1"))
        self.water_integral = integral(self.water)
        self.bridge_integral = integral(self.bridge_open)
        self.safe_centers = self.land & ~reserved
        self.mountain_distance = ndi.distance_transform_edt(self.nav != ord("1")).astype(np.float32)*self.cell
        tree_grid = np.zeros_like(self.land)
        for tree in self.trees:
            gx, gy = int(tree["x"]/self.cell), int(tree["y"]/self.cell)
            if 0 <= gx < self.n and 0 <= gy < self.n:
                tree_grid[gy, gx] = True
        self.tree_distance = ndi.distance_transform_edt(~tree_grid).astype(np.float32)*self.cell
        self.tree_kd = cKDTree(np.array([[v["x"], v["y"]] for v in self.trees]))
        self.sdf_path = resolve_resource(water["water_sdf"])
        sdf = np.asarray(Image.open(self.sdf_path).convert("L"))
        self.source_cell = WORLD / sdf.shape[1]
        self.display_water = sdf < 128
        self.display_water_integral = integral(self.display_water)
        # The art SDF saturates after 12 native pixels (~70.55 world units).
        # Extend its actual sign with an unbounded distance transform so reeds
        # can correctly occupy the requested 40--160-unit dry shore strip.
        self.shore_distance = ndi.distance_transform_edt(~self.display_water).astype(np.float32)*self.source_cell
        self.sdf_distance = (sdf.astype(np.float32)/255*24-12)*self.source_cell
        axis = np.clip(((np.arange(self.n)+.5)*self.cell/self.source_cell).astype(int), 0, sdf.shape[0]-1)
        self.shore_nav = self.shore_distance[np.ix_(axis, axis)]
        taxis = np.clip(((np.arange(self.n)+.5)*self.cell/self.terrain_cell).astype(int),
                        0, self.terrain.shape[0]-1)
        self.terrain_nav = self.terrain[np.ix_(taxis, taxis)]
        self.clearance = ndi.distance_transform_edt(self.safe_centers).astype(np.float32)*self.cell
        base = self.safe_centers & (self.clearance > 60)
        tc, md, td, wd = self.terrain_nav, self.mountain_distance, self.tree_distance, self.shore_nav
        # C is a real bridge/crossing travel class; its approaches remain clean.
        self.pools = {
            "meadow": np.flatnonzero(base & (tc == ord("P")) & (wd > 160) & (md > 130)),
            "forest_edge": np.flatnonzero(base & ((td < 230) | (tc == ord("F"))) & (wd > 130)),
            "dry_plain": np.flatnonzero(base & ((tc == ord("P")) | (tc == ord("H"))) & (td > 160) & (wd > 180)),
            "foothill": np.flatnonzero(base & (md > 65) & (md < 420) & (wd > 130)),
            "shore": np.flatnonzero(base & (wd >= 55) & (wd <= 155) & (md > 100)),
        }
        for kind, pool in self.pools.items():
            if len(pool) == 0:
                raise ValueError(f"No eligible {kind} cells")
        self.protected = sorted(set(
            [p for p in RES.glob("*.json") if p.name.startswith(("free_", "terrain_", "landscape_", "layered_", "expanded_water"))]
            + [self.sdf_path]
        ))
        self.before = {rel(p): sha(p) for p in self.protected}

    def sample(self, array, x, y, cell=None):
        cell = self.cell if cell is None else cell
        gx, gy = int(x/cell), int(y/cell)
        return array[min(array.shape[0]-1, max(0, gy)), min(array.shape[1]-1, max(0, gx))]

    def valid(self, bounds):
        x, y, w, h = bounds
        if x < 4 or y < 4 or x+w > WORLD-4 or y+h > WORLD-4:
            return False
        if rect_sum(self.blocked_integral, bounds, self.cell):
            return False
        if rect_sum(self.reserved_integral, bounds, self.cell):
            return False
        if rect_sum(self.bridge_integral, bounds, self.cell):
            return False
        if rect_sum(self.display_water_integral, bounds, self.source_cell):
            return False
        return True


def load_catalog(path: Path, planning=False):
    if planning:
        aspects = (2.7, 2.8, 2.9, 2.5, 2.4, 2.8, 3.3, 3.4, 2.3)
        return {key: {"native_size": [400, round(400/aspect)], "region": [0, 0, 400, round(400/aspect)],
                      "pivot": [.5, .5]} for key, aspect in zip(ASSETS, aspects)}
    catalog = read(path)
    missing = set(ASSETS) - set(catalog["assets"])
    if missing:
        raise ValueError(f"Missing native atlas assets: {sorted(missing)}")
    for key, asset in catalog["assets"].items():
        if key not in ASSETS:
            continue
        w, h = asset["native_size"]
        if w <= 0 or h <= 0 or len(asset["region"]) != 4:
            raise ValueError(f"Invalid native region for {key}")
        if abs(w-asset["region"][2]) > .01 or abs(h-asset["region"][3]) > .01:
            raise ValueError(f"native_size must describe the actual atlas cutout for {key}")
        if any(not 0 <= v <= 1 for v in asset["pivot"]):
            raise ValueError(f"Invalid normalized pivot for {key}")
    return catalog["assets"]


def fields_report(inputs):
    return {
        "navigation_shape": list(inputs.nav.shape), "navigation_cell_world": inputs.cell,
        "navigation_counts": {chr(int(c)): int(n) for c, n in zip(*np.unique(inputs.nav, return_counts=True))},
        "terrain_shape": list(inputs.terrain.shape), "terrain_cell_world": inputs.terrain_cell,
        "terrain_counts": {chr(int(c)): int(n) for c, n in zip(*np.unique(inputs.terrain, return_counts=True))},
        "displayed_water_sdf": resource(inputs.sdf_path),
        "displayed_water_encoding": "R/255*24-12 source pixels; positive=land",
        "shore_distance_extension": "Euclidean distance from displayed SDF water sign; unbounded after its 12px saturation",
        "location_exclusions": len(inputs.reserved_buildings), "building_margin_world": 45,
        "bridge_exclusions": len(inputs.reserved_bridges), "bridge_margin_world": 30,
        "source_mountains": len(inputs.mountains), "source_trees": len(inputs.trees),
        "eligible_center_cells": {k: int(len(v)) for k, v in inputs.pools.items()},
        "legacy_road_geometry_consulted": False,
    }


def make_decoration(asset_id, point, catalog, rng):
    asset = catalog[asset_id]
    native_w, native_h = map(float, asset["native_size"])
    low, high = WIDTHS[asset_id]
    high = min(high, native_w*.98/NEAR_ZOOM)
    if asset_id in HEIGHT_CAP:
        high = min(high, HEIGHT_CAP[asset_id]*native_w/native_h)
    # A narrow native silhouette may need to be a few units below the nominal
    # 70-unit width in order to remain low; never stretch its aspect ratio.
    low = min(low, high*.92)
    width = float(rng.uniform(low, high))
    height = width*native_h/native_w
    flip = bool(rng.integers(0, 2))
    px, py = asset["pivot"]
    if flip:
        px = 1-px
    x, y = map(float, point)
    bounds = [x-width*px, y-height*py, width, height]
    wash = asset_id.endswith("_wash")
    opacity_range = asset.get("opacity_range", [.34, .56] if wash else [.72, .95])
    opacity = float(rng.uniform(*opacity_range))
    return {"asset": asset_id, "x": round(x, 5), "y": round(y, 5), "z": Z,
            "position": [round(x, 5), round(y, 5)],
            "bounds": [round(v, 5) for v in bounds],
            "world_width": round(width, 5), "world_height": round(height, 5),
            "flip": flip, "opacity": round(opacity, 5), "layer": "ground_decals",
            "draw_order": 0 if wash else 1}


def generate(inputs, catalog, seed, target):
    rng = np.random.default_rng(seed)
    # Smooth non-periodic regional abundance; the grid only stores real terrain,
    # never decoration positions. Centers are sampled randomly and then spaced
    # by their whole support envelopes, leaving independent open corridors.
    noise = ndi.gaussian_filter(rng.random((91, 91)), 1.45)
    noise = (noise-noise.min())/(noise.max()-noise.min())
    clusters, decorations = [], []
    spatial = defaultdict(list)
    bin_size = 680
    kinds = tuple(PALETTES)
    kind_probs = [.44, .18, .19, .12, .07]
    rejected = Counter()
    # Prefer about one quarter of candidates in all named places' surrounding
    # countryside; the same rule applies to every entrance, never to old roads.
    def candidate(kind):
        if rng.random() < .25:
            node = inputs.location_positions[int(rng.integers(len(inputs.location_positions)))]
            point = node+rng.normal(0, 660, size=2)
            x, y = map(float, point)
            if not 100 < x < WORLD-100 or not 100 < y < WORLD-100:
                return None
            tx = int(x/inputs.cell); ty = int(y/inputs.cell)
            if not inputs.safe_centers[ty, tx]:
                return None
            td = inputs.tree_distance[ty, tx]; md = inputs.mountain_distance[ty, tx]
            wd = inputs.shore_nav[ty, tx]; terrain = inputs.terrain_nav[ty, tx]
            if 65 < wd < 150:
                kind = "shore"
            elif 70 < md < 350:
                kind = "foothill"
            elif td < 200 or terrain == ord("F"):
                kind = "forest_edge"
            else:
                kind = "meadow" if rng.random() < .73 else "dry_plain"
            return kind, x, y
        flat = int(rng.choice(inputs.pools[kind]))
        gy, gx = divmod(flat, inputs.n)
        return kind, (gx+float(rng.random()))*inputs.cell, (gy+float(rng.random()))*inputs.cell

    for attempt in range(45000):
        # A residual of one or two items cannot form a coherent minimum group.
        if target-len(decorations) < 3:
            break
        kind = str(rng.choice(kinds, p=kind_probs))
        chosen = candidate(kind)
        if chosen is None:
            rejected["center_invalid"] += 1
            continue
        kind, x, y = chosen
        regional = float(noise[min(90, int(y/WORLD*91)), min(90, int(x/WORLD*91))])
        if rng.random() > .27+.73*regional:
            rejected["regional_blank"] += 1
            continue
        small = rng.random() < .28
        radius = float(rng.uniform(115, 165) if small else rng.uniform(215, 285))
        if kind == "shore":
            radius *= .76
        gap = 55.0 if not small else 45.0
        bx, by = int(x/bin_size), int(y/bin_size)
        overlap = False
        for ky in range(by-1, by+2):
            for kx in range(bx-1, bx+2):
                for other in spatial[(kx, ky)]:
                    if math.hypot(x-other["x"], y-other["y"]) < radius+other["radius"]+max(gap, other["gap"]):
                        overlap = True
                        break
                if overlap:
                    break
            if overlap:
                break
        if overlap:
            rejected["cluster_corridor"] += 1
            continue
        desired = int(rng.integers(5, 9) if small else rng.integers(17, 24))
        desired = min(desired, target-len(decorations))
        theta = float(rng.uniform(-math.pi, math.pi))
        if kind == "shore":
            # Tangential shoreline groups, with actual dry-shore checks below.
            sx = max(0, min(inputs.shore_distance.shape[1]-1, int(x/inputs.source_cell)))
            sy = max(0, min(inputs.shore_distance.shape[0]-1, int(y/inputs.source_cell)))
            dx = float(inputs.shore_distance[sy, min(sx+2, inputs.shore_distance.shape[1]-1)]-inputs.shore_distance[sy, max(0, sx-2)])
            dy = float(inputs.shore_distance[min(sy+2, inputs.shore_distance.shape[0]-1), sx]-inputs.shore_distance[max(0, sy-2), sx])
            theta = math.atan2(dy, dx)+math.pi/2
        u, v = np.array([math.cos(theta), math.sin(theta)]), np.array([-math.sin(theta), math.cos(theta)])
        wash_keys, prop_keys = PALETTES[kind]
        members = []
        prop_points = []
        wash_count = 0 if small else int(rng.integers(1, 3))
        for index in range(desired):
            asset_id = str(rng.choice(wash_keys if index < wash_count else prop_keys))
            placed = None
            for _ in range(36):
                # Two unequal lobes and an open central slit give each group a
                # coherent direction without a regular flower/stone grid.
                side = -1 if rng.random() < .62 else 1
                along = float(rng.normal(side*.26*radius, radius*.25))
                across = float(rng.normal(side*.13*radius, radius*(.10 if kind == "shore" else .19)))
                point = np.array([x, y])+u*along+v*across
                px, py = map(float, point)
                if not 0 < px < WORLD or not 0 < py < WORLD:
                    continue
                row = make_decoration(asset_id, point, catalog, rng)
                r = row["bounds"]
                if any(math.hypot(cx-x, cy-y) > radius for cx, cy in
                       ((r[0], r[1]), (r[0]+r[2], r[1]), (r[0], r[1]+r[3]), (r[0]+r[2], r[1]+r[3]))):
                    continue
                if not inputs.valid(r):
                    continue
                terrain = int(inputs.sample(inputs.terrain, px, py, inputs.terrain_cell))
                md = float(inputs.sample(inputs.mountain_distance, px, py))
                wd = float(inputs.sample(inputs.shore_distance, px, py, inputs.source_cell))
                td = float(inputs.tree_kd.query([px, py])[0])
                if terrain in (ord("_"), ord("C")):
                    continue
                if asset_id in ("grass_green", "wildflowers") and terrain not in (ord("P"), ord("F")) and td > 130:
                    continue
                if asset_id == "shore_reeds" and not 40 <= wd <= 160:
                    continue
                if kind == "foothill" and md > 470:
                    continue
                if not asset_id.endswith("_wash"):
                    if any(math.dist([px, py], p) < 24 for p in prop_points):
                        continue
                row.update(terrain=chr(terrain), cluster_id=f"ground_cluster_{len(clusters):04d}",
                           habitat=kind)
                placed = row
                if not asset_id.endswith("_wash"):
                    prop_points.append([px, py])
                break
            if placed:
                members.append(placed)
        if len(members) < (3 if small else 8):
            rejected["cluster_too_sparse"] += 1
            continue
        cluster = {"id": f"ground_cluster_{len(clusters):04d}", "x": round(x, 5), "y": round(y, 5),
                   "radius": round(radius, 5), "gap": gap, "habitat": kind,
                   "small": small, "count": len(members), "orientation": round(theta, 6)}
        clusters.append(cluster)
        spatial[(bx, by)].append(cluster)
        decorations.extend(members)
    if not 8000 <= len(decorations) <= MAX_COUNT:
        raise ValueError(f"Expected 8000--{MAX_COUNT} decorations, got {len(decorations)}")
    for index, row in enumerate(decorations):
        row["id"] = f"ground_detail_{index:05d}"
    # A stable semantic draw order keeps soft earth washes below low objects.
    decorations.sort(key=lambda row: (row["draw_order"], ASSETS.index(row["asset"]), row["id"]))
    return decorations, clusters, {"candidate_attempts": attempt+1, "rejections": dict(rejected)}


def camera_specs(inputs, overrides):
    runtime_ids = read(RES / "bai_chuan_node_runtime_ids_20261031.json")
    specs = {
        "baishi": {"runtime_id": "white_stone_town", "location": runtime_ids["white_stone_town"], "center": None},
        "yuxu": {"runtime_id": "yuxu_xianzong_core", "location": runtime_ids["yuxu_xianzong_core"], "center": None},
        "south": {"location": None, "center": [10900, 19400]},
    }
    # The caller can bind these to the exact runtime screenshot camera centers.
    for key, point in overrides.items():
        specs[key]["center"] = point
        specs[key]["location"] = None
    for spec in specs.values():
        if spec["center"] is None:
            spec["center"] = inputs.locations[spec["location"]]["drawPos"]
    return specs


def validate(inputs, catalog, rows, clusters, generation, seed, catalog_path, planning, cameras):
    errors = []
    violations = Counter()
    by_asset = Counter()
    dims = defaultdict(list)
    shore = []
    native_sample_scale = []
    narrow_by_height_cap = Counter()
    max_extent = 0.0
    for row in rows:
        asset_id = row["asset"]
        by_asset[asset_id] += 1
        bounds = row["bounds"]
        x, y, w, h = bounds
        cx, cy = row["position"]
        max_extent = max(max_extent, abs(cx-x), abs(cy-y), abs(x+w-cx), abs(y+h-cy))
        dims[asset_id].append([w, h])
        if x < 0 or y < 0 or x+w > WORLD or y+h > WORLD:
            violations["world_boundary"] += 1
        for label, table, cell in (
            ("navigation_blocked", inputs.blocked_integral, inputs.cell),
            ("navigation_mountain", inputs.mountain_integral, inputs.cell),
            ("navigation_water", inputs.water_integral, inputs.cell),
            ("bridge_open_cells", inputs.bridge_integral, inputs.cell),
            ("displayed_water", inputs.display_water_integral, inputs.source_cell),
        ):
            if rect_sum(table, bounds, cell):
                violations[label] += 1
        if any(intersects(bounds, box) for _, box in inputs.reserved_buildings):
            violations["entrance_building_plus_45"] += 1
        if any(intersects(bounds, box) for _, box in inputs.reserved_bridges):
            violations["bridge_bounds_plus_30"] += 1
        if row["x"] != cx or row["y"] != cy or row["z"] != Z or row["layer"] != "ground_decals":
            violations["explicit_xyz_layer"] += 1
        if abs(w-row["world_width"]) > .0001 or abs(h-row["world_height"]) > .0001:
            violations["explicit_size_matches_bounds"] += 1
        native_w, native_h = catalog[asset_id]["native_size"]
        if abs(w/h-native_w/native_h) > .0001:
            violations["native_aspect"] += 1
        native_sample_scale.append(w*NEAR_ZOOM/native_w)
        if w*NEAR_ZOOM/native_w > 1.00001:
            violations["near_view_source_upscale"] += 1
        if asset_id in HEIGHT_CAP and h > HEIGHT_CAP[asset_id]+.0001:
            violations["low_prop_height"] += 1
        opacity_range = catalog[asset_id].get("opacity_range", [.34, .56] if asset_id.endswith("_wash") else [.72, .95])
        if not opacity_range[0] <= row["opacity"] <= opacity_range[1]:
            violations["native_catalog_opacity_range"] += 1
        px, py = catalog[asset_id]["pivot"]
        if row["flip"]:
            px = 1-px
        if abs(x+w*px-cx) > .0001 or abs(y+h*py-cy) > .0001:
            violations["native_pivot_matches_bounds"] += 1
        if w < WIDTHS[asset_id][0]-.001:
            narrow_by_height_cap[asset_id] += 1
        if asset_id == "shore_reeds":
            wd = float(inputs.sample(inputs.shore_distance, cx, cy, inputs.source_cell))
            shore.append(wd)
            if not 40 <= wd <= 160:
                violations["reed_dry_shore_40_160"] += 1
    # Whole-support circles are disjoint, including a real 45--55-unit corridor.
    center_tree = cKDTree(np.array([[c["x"], c["y"]] for c in clusters]))
    corridor_min = math.inf
    corridor_pair_count = 0
    for a, b in center_tree.query_pairs(650):
        ca, cb = clusters[a], clusters[b]
        gap = math.hypot(ca["x"]-cb["x"], ca["y"]-cb["y"])-ca["radius"]-cb["radius"]
        corridor_min = min(corridor_min, gap)
        corridor_pair_count += 1
        if gap < max(ca["gap"], cb["gap"])-.0001:
            violations["cluster_open_corridor"] += 1
    after = {rel(p): sha(p) for p in inputs.protected}
    changed = [p for p in inputs.before if inputs.before[p] != after[p]]
    if changed:
        errors.append(f"Protected input files changed: {changed}")
    if any(violations.values()):
        errors.append(f"Placement violations: {dict(violations)}")
    camera_qa = {}
    for name, spec in cameras.items():
        views = {}
        for zoom in (.8, .4):
            rect = viewport(spec["center"], zoom)
            visible = [row for row in rows if intersects(row["bounds"], rect)]
            views[str(zoom)] = {
                "viewport_world": rect, "viewport_px": [1920, 1080],
                "visible_count": len(visible), "clusters": len({r["cluster_id"] for r in visible}),
                "by_asset": dict(Counter(row["asset"] for row in visible)),
            }
        camera_qa[name] = {**spec, "zoom_views": views}
    chunk_counts = Counter((int(r["x"]/CHUNK), int(r["y"]/CHUNK)) for r in rows)
    size_stats = {}
    for key, values in dims.items():
        ar = np.array(values)
        size_stats[key] = {
            "count": len(values), "world_width_min_max": ar[:, 0].min().item(),
            "world_width_max": ar[:, 0].max().item(),
            "world_height_min": ar[:, 1].min().item(), "world_height_max": ar[:, 1].max().item(),
            "near_screen_height_max_px": ar[:, 1].max().item()*.8,
            "native_size": catalog[key]["native_size"],
        }
        size_stats[key]["world_width_min"] = size_stats[key].pop("world_width_min_max")
    report = {
        "status": "PASS" if not errors else "FAIL", "planning_only": planning,
        "profile": PROFILE, "seed": seed, "world_size": WORLD, "z": Z,
        "decorations": len(rows), "target": TARGET_COUNT, "cap": MAX_COUNT,
        "cluster_count": len(clusters), "clusters_by_habitat": dict(Counter(c["habitat"] for c in clusters)),
        "items_by_habitat": dict(Counter(r["habitat"] for r in rows)), "items_by_asset": dict(by_asset),
        "terrain_at_items": dict(Counter(r["terrain"] for r in rows)),
        "size_stats": size_stats, "width_reduced_to_keep_low_native_aspect": dict(narrow_by_height_cap),
        "full_bbox_violations": {k: int(violations[k]) for k in (
            "world_boundary", "navigation_blocked", "navigation_mountain", "navigation_water",
            "displayed_water", "bridge_open_cells", "entrance_building_plus_45", "bridge_bounds_plus_30",
            "explicit_xyz_layer", "explicit_size_matches_bounds", "native_catalog_opacity_range",
            "native_pivot_matches_bounds", "native_aspect", "near_view_source_upscale", "low_prop_height",
            "reed_dry_shore_40_160", "cluster_open_corridor")},
        "reed_shore_distance_world_min_max": [min(shore), max(shore)] if shore else [],
        "native_near_view_scale_max": max(native_sample_scale),
        "max_detail_extent": round(max_extent, 5),
        "minimum_cluster_support_gap_world": corridor_min if math.isfinite(corridor_min) else None,
        "nearby_cluster_pairs_checked": corridor_pair_count,
        "active_spatial_chunks": len(chunk_counts), "max_items_per_chunk": max(chunk_counts.values()),
        "camera_counts": camera_qa, "generation": generation,
        "input_fields": fields_report(inputs), "source_sha256_before": inputs.before,
        "source_sha256_after": after, "source_files_changed": changed,
        "catalog": resource(catalog_path) if not planning else "planning-only assumed native ratios; not production",
        "catalog_sha256": sha(catalog_path) if not planning else None,
        "no_interaction_or_collision_added": True, "legacy_road_edges_used": 0,
        "errors": errors,
    }
    if errors:
        raise ValueError("; ".join(errors))
    return report


def map_preview(inputs, rows, clusters, cameras, path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Rectangle
    fig, ax = plt.subplots(figsize=(13, 13), dpi=140)
    ax.set_facecolor("#c8be9c")
    preview = np.full((*inputs.nav.shape, 3), [201, 193, 162], dtype=np.uint8)
    preview[inputs.nav == ord("1")] = [121, 117, 98]
    preview[inputs.water] = [118, 148, 151]
    ax.imshow(preview, extent=(0, inputs.n*inputs.cell, inputs.n*inputs.cell, 0), interpolation="nearest")
    colors = {"meadow": "#546b39", "forest_edge": "#214e39", "dry_plain": "#8c7542",
              "foothill": "#5b5d56", "shore": "#6a7b40"}
    for kind, color in colors.items():
        local = [r for r in rows if r["habitat"] == kind]
        if local:
            xy = np.array([r["position"] for r in local])
            ax.scatter(xy[:, 0], xy[:, 1], s=1.25, c=color, alpha=.65, label=f"{kind}: {len(local)}")
    for _, bounds in inputs.reserved_buildings:
        ax.add_patch(Rectangle(bounds[:2], bounds[2], bounds[3], linewidth=.35, edgecolor="#684936", facecolor="none"))
    for name, spec in cameras.items():
        r = viewport(spec["center"], .8)
        ax.add_patch(Rectangle(r[:2], r[2], r[3], linewidth=1.1, edgecolor="#a34c42", facecolor="none"))
        ax.text(r[0], r[1]-60, name, fontsize=8, color="#73362c")
    ax.set(xlim=(0, WORLD), ylim=(WORLD, 0), aspect="equal",
           title=f"Ground details | seed {SEED} | {len(rows)} items in {len(clusters)} irregular groups")
    ax.legend(loc="upper right", fontsize=8)
    ax.set_xlabel("world x"); ax.set_ylabel("world y")
    fig.tight_layout()
    fig.savefig(path)
    plt.close(fig)


def text_report(report):
    lines = [
        f"{report['status']} - Bai Chuan ground-detail placement",
        f"Planning only: {report['planning_only']}",
        f"Seed: {report['seed']}; items: {report['decorations']}; clusters: {report['cluster_count']}",
        "All decoration projections stay off real navigation mountains/water, displayed water, bridges and entrances.",
        "All items use independent explicit XYZ; fixed z=24 / ground_decals; no collision or interactions.",
        f"Protected source SHA-256 unchanged: {not report['source_files_changed']}",
        f"Largest nearest-view/native sampling ratio: {report['native_near_view_scale_max']:.4f} (must <=1)",
        f"Minimum cluster support gap: {report['minimum_cluster_support_gap_world']:.3f} world units",
        f"Reed shore band min/max: {report['reed_shore_distance_world_min_max']}",
        "Near-view counts are full-bounds intersection with a 1920x1080 camera at zoom0.8:",
    ]
    for name, entry in report["camera_counts"].items():
        view = entry["zoom_views"]["0.8"]
        lines.append(f"  {name}: center={entry['center']}; {view['visible_count']} items / {view['clusters']} groups")
    lines += ["", "Asset counts: " + json.dumps(report["items_by_asset"], ensure_ascii=False),
              "Placement violations: " + json.dumps(report["full_bbox_violations"], ensure_ascii=False),
              "No Main launch, game build, production scene/script/shader modification, or legacy road tracing."]
    return "\n".join(lines)+"\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan-only", action="store_true", help="Write QA only with assumed ratios; do not write decoration data")
    parser.add_argument("--inputs-only", action="store_true")
    parser.add_argument("--catalog", type=Path, default=CATALOG)
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--target", type=int, default=TARGET_COUNT)
    parser.add_argument("--baishi", type=float, nargs=2)
    parser.add_argument("--yuxu", type=float, nargs=2)
    parser.add_argument("--south", type=float, nargs=2)
    args = parser.parse_args()
    if not 8000 <= args.target <= MAX_COUNT:
        parser.error(f"Target must be 8000--{MAX_COUNT}")
    started = time.perf_counter()
    OUT.mkdir(parents=True, exist_ok=True)
    inputs = Inputs()
    if args.inputs_only:
        report = fields_report(inputs)
        report["source_sha256"] = inputs.before
        write(OUT/"input_fields_report.json", report)
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return
    catalog = load_catalog(args.catalog, args.plan_only)
    if not args.plan_only:
        catalog_data = read(args.catalog)
        for p in [args.catalog, resolve_resource(catalog_data["atlas"])]:
            if p not in inputs.protected:
                inputs.protected.append(p)
                inputs.before[rel(p)] = sha(p)
    rows, clusters, generation = generate(inputs, catalog, args.seed, args.target)
    overrides = {k: getattr(args, k) for k in ("baishi", "yuxu", "south") if getattr(args, k) is not None}
    cameras = camera_specs(inputs, overrides)
    report = validate(inputs, catalog, rows, clusters, generation, args.seed, args.catalog, args.plan_only, cameras)
    report["elapsed_seconds"] = round(time.perf_counter()-started, 3)
    stem = "placement_plan" if args.plan_only else "placement"
    if not args.plan_only:
        data = {"version": 1, "profile": PROFILE, "world_size": WORLD, "grid_size": CHUNK,
                "catalog_path": resource(args.catalog), "seed": args.seed,
                "z": Z, "layer": "ground_decals", "max_detail_extent": report["max_detail_extent"],
                "decorations": rows,
                "coordinate_rule": "x/y are stable native-pivot ground coordinates; bounds are complete world projection rectangles; fixed z=24 ground_decals",
                "source_sha256": inputs.before,
                "placement_rule": "Independent terrain-driven irregular clusters with open corridors; no legacy edge-road tracing; no collision or interactions"}
        write(DEST, data)
        report["output_sha256"] = sha(DEST)
    write(OUT/f"{stem}_qa.json", report)
    write(OUT/f"{stem}_clusters.json", clusters)
    (OUT/f"{stem}_qa.txt").write_text(text_report(report), encoding="utf-8")
    map_preview(inputs, rows, clusters, cameras, OUT/f"{stem}_map.png")
    print(text_report(report))
    print(f"Elapsed: {report['elapsed_seconds']} seconds")


if __name__ == "__main__":
    main()
