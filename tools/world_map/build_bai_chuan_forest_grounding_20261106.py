"""Build the small Baishi forest/foothill grounding sample, without rebaking maps.

Only the two new sample JSON files and this tool's output directory are written.
All navigation, mountain art, existing landscape/layouts and the 11199 existing
ground decorations are immutable inputs. Tree bboxes avoid every nonzero nav
cell. Foothill decals may overlap mountain cells, but never water or entrances.
Mountain apron anchors use each native alpha column's actual lowest visible
pixel, including horizontal flip; they never use a mountain's rectangular base.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from scipy.spatial import cKDTree


ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "resources/world/bai_chuan"
ART = ROOT / "textures/world/bai_chuan/forest_grounding_20261106"
OUT = ROOT / "output/bai_chuan_forest_grounding_20261106"
PATCH = RES / "forest_grounding_sample_20261106.json"
GROUND = RES / "forest_grounding_details_20261106.json"
TREE_CATALOG = ART / "trees_catalog.json"
GROUND_CATALOG = ART / "ground_catalog.json"
WORLD = 22118.4
BOUNDS = [3800.0, 12900.0, 3200.0, 2400.0]
SEED = 2026110615
PROFILE = "baishi_forest_grounding_sample_20261106_v2"
NEAR_ZOOM = .8
ALPHA = 46  # Production mountain material begins visibility at alpha .18.
TREE_IDS = (
    "natural_oak_mature", "natural_oak_young", "natural_pine_wide",
    "natural_pine_lean", "natural_elm_wide", "natural_sapling",
)
GROUND_IDS = ("forest_floor", "forest_edge", "root_shade", "mountain_scree",
              "mountain_soil", "mountain_meadow")
APRON_RULES = {
    "mountain_soil": {"width": [110., 210.], "opacity": [.12, .24], "tint": [.80, .82, .70]},
    "mountain_scree": {"width": [60., 120.], "opacity": [.20, .38], "tint": [.82, .85, .78]},
    "mountain_meadow": {"width": [70., 130.], "opacity": [.18, .32], "tint": [1., 1., 1.]},
    "root_shade": {"width": [100., 180.], "opacity": [.10, .18], "tint": [.64, .67, .50]},
}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def rel(path):
    return Path(path).relative_to(ROOT).as_posix()


def resource(path):
    return "res://" + rel(path)


def resolve(path):
    return ROOT / str(path).removeprefix("res://")


def rows_array(rows):
    return np.frombuffer("".join(rows).encode("ascii"), dtype=np.uint8).reshape(len(rows), -1)


def integral(mask):
    return np.pad(mask.astype(np.int32).cumsum(0, dtype=np.int32).cumsum(1, dtype=np.int32),
                  ((1, 0), (1, 0)))


def rectangle_cells(bounds, cell, shape):
    x, y, w, h = bounds
    return (max(0, math.floor(x/cell)), max(0, math.floor(y/cell)),
            min(shape[1], math.ceil((x+w)/cell)), min(shape[0], math.ceil((y+h)/cell)))


def rect_sum(table, bounds, cell):
    xa, ya, xb, yb = rectangle_cells(bounds, cell, (table.shape[0]-1, table.shape[1]-1))
    return int(table[yb, xb]-table[ya, xb]-table[yb, xa]+table[ya, xa])


def intersects(a, b):
    return a[0] < b[0]+b[2] and b[0] < a[0]+a[2] and a[1] < b[1]+b[3] and b[1] < a[1]+a[3]


def contains(outer, inner):
    return (outer[0] <= inner[0] and outer[1] <= inner[1] and
            inner[0]+inner[2] <= outer[0]+outer[2] and inner[1]+inner[3] <= outer[1]+outer[3])


def expanded(bounds, margin):
    return [bounds[0]-margin, bounds[1]-margin, bounds[2]+2*margin, bounds[3]+2*margin]


def rect_distance(a, b):
    dx = max(a[0]-b[0]-b[2], b[0]-a[0]-a[2], 0.)
    dy = max(a[1]-b[1]-b[3], b[1]-a[1]-a[3], 0.)
    return math.hypot(dx, dy)


def point_segment(p, a, b):
    p, a, b = np.asarray(p), np.asarray(a), np.asarray(b)
    d = b-a
    t = np.clip(np.dot(p-a, d)/max(float(np.dot(d, d)), 1e-12), 0., 1.)
    return float(np.linalg.norm(p-(a+d*t)))


def segment_rect_distance(a, b, box):
    x, y, w, h = box
    lo, hi = 0., 1.
    for origin, delta, lower, upper in zip(a, np.subtract(b, a), (x, y), (x+w, y+h)):
        if abs(delta) < 1e-10:
            if not lower <= origin <= upper:
                lo, hi = 1., 0.
                break
        else:
            v0, v1 = sorted(((lower-origin)/delta, (upper-origin)/delta))
            lo, hi = max(lo, v0), min(hi, v1)
    if lo <= hi:
        return 0.
    corners = [(x, y), (x+w, y), (x, y+h), (x+w, y+h)]
    values = [point_segment(p, a, b) for p in corners]
    values += [math.hypot(max(x-p[0], p[0]-x-w, 0.), max(y-p[1], p[1]-y-h, 0.)) for p in (a, b)]
    return min(values)


class Inputs:
    def __init__(self):
        self.landscape_path = RES / "landscape_expanded3x_20261106.json"
        self.nav_path = RES / "free_navigation_expanded3x_20261106.json"
        self.layout_path = RES / "free_layout_expanded3x_20261106.json"
        self.water_path = RES / "expanded_water_20261106.json"
        self.terrain_path = RES / "terrain_travel_expanded3x_20261106.json"
        self.original_ground_path = RES / "ground_decorations_20261106.json"
        self.scene, nav, self.layout, self.water, terrain = map(read, (
            self.landscape_path, self.nav_path, self.layout_path, self.water_path, self.terrain_path))
        assert nav["world_size"] == WORLD == self.scene["world_size"] == self.layout["worldSize"]
        self.cell = float(nav["cell_size"])
        self.nav = rows_array(nav["rows"])
        self.nav_blocked = integral(self.nav != ord("0"))
        self.nav_water = integral(self.nav == ord("2"))
        self.mountain_cells = integral(self.nav == ord("1"))
        self.water_cells = integral(rows_array(self.water["water_rows"]) == ord("2"))
        self.bridge_cells = integral(rows_array(self.water["bridge_open_rows"]) == ord("1"))
        self.sdf_path = resolve(self.water["water_sdf"])
        sdf = np.asarray(Image.open(self.sdf_path).convert("L"))
        self.display_water = sdf < 128
        self.source_cell = WORLD / sdf.shape[1]
        self.display_water_table = integral(self.display_water)
        self.terrain = rows_array(terrain["rows"])
        self.terrain_cell = float(terrain["cell_size"])
        self.buildings = []
        for ident, row in self.layout["locations"].items():
            x, y, x1, y1 = row["rect"]
            self.buildings.append((ident, expanded([x, y, x1-x, y1-y], 45)))
        self.bridges = [(row.get("id", f"bridge_{i:02d}"), expanded(row["bounds"], 30))
                        for i, row in enumerate(self.water["bridges"])]
        self.old_removed = [row for row in self.scene["trees"] if contains(BOUNDS, row["bounds"])]
        removed = {row["id"] for row in self.old_removed}
        self.unchanged = [row for row in self.scene["trees"] if row["id"] not in removed]
        self.edge_trees = [row for row in self.unchanged if intersects(expanded(BOUNDS, 150), row["bounds"])]
        self.old_points = np.asarray([[t["x"], t["y"]] for t in self.old_removed])
        self.old_kd = cKDTree(self.old_points)
        self.mountains = [row for row in self.scene["mountains"] if intersects(BOUNDS, row["bounds"])]
        self.white = self.layout["locations"]["bc_place_021"]["drawPos"]
        # Every tree bbox avoids these measured empty ribbons. Their labels do
        # not create roads, collision or runtime geometry.
        self.corridors = [
            {"id": "inner_bank_glade", "width": 88., "points":
             [[4020., 14035.], [4590., 14070.], [5160., 14025.], [5510., 14070.]]},
            {"id": "baishi_front_approach", "width": 98., "points":
             [[5797.1138756, 14475.], [5808., 14800.], [5760., 15180.]]},
            {"id": "south_woodland_glade", "width": 84., "points":
             [[6030., 14935.], [6440., 14885.], [6770., 15030.]]},
        ]
        mountain_sources = [resolve(v["path"]) for v in self.scene["mountain_sources"].values()]
        self.protected = sorted(set(
            [p for p in RES.glob("*.json") if p.name.startswith(("free_", "terrain_", "landscape_", "layered_", "expanded_water"))]
            + [self.original_ground_path, self.sdf_path, resolve(self.scene["tree_atlas"])] + mountain_sources))
        self.before = {rel(p): sha(p) for p in self.protected}

    def valid_common(self, bounds):
        if not contains(BOUNDS, bounds) or not contains([0, 0, WORLD, WORLD], bounds):
            return False
        if rect_sum(self.nav_water, bounds, self.cell) or rect_sum(self.water_cells, bounds, self.cell):
            return False
        if rect_sum(self.bridge_cells, bounds, self.cell):
            return False
        if rect_sum(self.display_water_table, bounds, self.source_cell):
            return False
        if any(intersects(bounds, box) for _, box in self.buildings + self.bridges):
            return False
        return True

    def in_corridor(self, bounds):
        return any(segment_rect_distance(a, b, bounds) < row["width"]/2
                   for row in self.corridors for a, b in zip(row["points"], row["points"][1:]))

    def valid_tree(self, bounds):
        return self.valid_common(bounds) and not rect_sum(self.nav_blocked, bounds, self.cell) and not self.in_corridor(bounds)

    def contour(self, mountain):
        asset = self.scene["mountain_catalog"][mountain["asset"]]
        source = self.scene["mountain_sources"][asset["bank"]]
        x, y, w, h = asset["region"]
        image = Image.open(resolve(source["path"])).convert("RGBA").crop((x, y, x+w, y+h))
        if mountain.get("flip", False):
            image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        alpha = np.asarray(image.getchannel("A"))
        mask = alpha >= ALPHA
        valid = mask.any(0)
        columns = np.flatnonzero(valid)
        bottoms = h-1-np.argmax(mask[::-1, columns], axis=0)
        bx, by, bw, bh = mountain["bounds"]
        points = np.column_stack((bx+(columns+.5)/w*bw, by+(bottoms+.5)/h*bh))
        return {"image": image, "alpha": alpha, "columns": columns, "bottoms": bottoms,
                "points": points, "native_size": [w, h], "source": source["path"], "region": asset["region"]}


def catalog(path, required):
    if not path.exists():
        raise FileNotFoundError(f"Wait for the real generated native atlas/catalog: {path}")
    data = read(path)
    missing = set(required)-set(data["assets"])
    if missing:
        raise ValueError(f"Missing actual native assets: {sorted(missing)}")
    if not resolve(data["atlas"]).is_file():
        raise FileNotFoundError(data["atlas"])
    atlas = Image.open(resolve(data["atlas"]))
    for key in required:
        asset = data["assets"][key]
        x, y, w, h = asset["region"]
        if asset["native_size"] != [w, h] or min(w, h) <= 0:
            raise ValueError(f"Actual cropped native dimensions required: {key}")
        if x < 0 or y < 0 or x+w > atlas.width or y+h > atlas.height:
            raise ValueError(f"Native region out of atlas: {key}")
        if any(not 0 <= n <= 1 for n in asset["pivot"]):
            raise ValueError(f"Invalid pivot: {key}")
    return data


def make_tree(asset_id, point, assets, rng, cluster_id):
    asset = assets[asset_id]
    lo, hi = asset.get("height_range", [95., 138.])
    if asset_id == "natural_sapling":
        lo, hi = max(float(lo), 76.), min(float(hi), 98.)
    else:
        lo, hi = max(float(lo), 95.), min(float(hi), 138.)
    if lo > hi:
        lo = hi
    nw, nh = asset["native_size"]
    hi = min(hi, nh*.98/NEAR_ZOOM)
    height = float(rng.uniform(min(lo, hi), hi))
    width = height*nw/nh
    flip = bool(rng.integers(0, 2))
    px, py = asset["pivot"]
    if flip:
        px = 1-px
    x, y = map(float, point)
    bounds = [x-width*px, y-height*py, width, height]
    return {"id": "", "kind": "tree", "asset": asset_id, "x": round(x, 5), "y": round(y, 5),
            "z": round(y, 5), "position": [round(x, 5), round(y, 5)], "height": round(height, 5),
            "width": round(width, 5), "pivot": asset["pivot"], "flip": flip,
            "bounds": [round(v, 5) for v in bounds], "layer": "ground_props", "cluster_id": cluster_id}


def tree_composition(rows, main, companion, assets):
    counts = Counter(row["asset"] for row in rows)
    families = Counter(assets[row["asset"]]["family"] for row in rows)
    n = len(rows)
    main_ratio, companion_ratio = counts[main]/n, counts[companion]/n
    sapling_ratio = counts["natural_sapling"]/n
    main_family = assets[main]["family"]
    family_ratio = families[main_family]/n
    return {"species_counts": dict(counts), "family_counts": dict(families), "main_family": main_family,
            "main_asset_fraction": main_ratio, "companion_asset_fraction": companion_ratio,
            "sapling_fraction": sapling_ratio, "main_family_fraction": family_ratio,
            "composition_passed": .45 <= main_ratio <= .60 and .25 <= companion_ratio <= .35
            and .10 <= sapling_ratio <= .20 and family_ratio >= .75}


def composition_quota(n, rng):
    choices = []
    for main in range(n+1):
        for companion in range(n-main+1):
            sapling = n-main-companion
            if .45 <= main/n <= .60 and .25 <= companion/n <= .35 and .10 <= sapling/n <= .20:
                choices.append((main, companion, sapling))
    if not choices:
        raise ValueError(f"No natural main/companion/sapling composition at size {n}")
    return choices[int(rng.integers(len(choices)))]


def generate_trees(inputs, cat, seed):
    rng = np.random.default_rng(seed)
    trees, clusters, rejected = [], [], Counter()
    # Seed selection follows the actual old forest distribution, with enough
    # irregular expansion to form masses instead of preserving its old grid.
    cluster_min_gap = 78.
    for attempt in range(220):
        if len(trees) >= 165 or len(clusters) >= 14:
            break
        old_point = inputs.old_points[int(rng.integers(len(inputs.old_points)))]
        center = old_point+rng.normal(0, 185, 2)
        cx, cy = map(float, center)
        if not (BOUNDS[0]+120 < cx < BOUNDS[0]+BOUNDS[2]-120 and
                BOUNDS[1]+145 < cy < BOUNDS[1]+BOUNDS[3]-120):
            rejected["seed_too_close_to_sample_edge"] += 1
            continue
        radius = float(rng.uniform(230, 330))
        stretch = float(rng.uniform(.75, 1.12))
        angle = float(rng.uniform(-math.pi, math.pi))
        if any(math.dist(center, [c["x"], c["y"]]) < .55*(radius+c["radius"])+110 for c in clusters):
            rejected["cluster_seed_separation"] += 1
            continue
        main = ("natural_oak_mature", "natural_pine_wide", "natural_elm_wide",
                "natural_pine_lean", "natural_oak_mature", "natural_elm_wide")[len(clusters) % 6]
        companion = ("natural_pine_lean" if main == "natural_pine_wide" else
                     "natural_pine_wide" if main == "natural_pine_lean" else "natural_oak_young")
        main_native_w, main_native_h = cat["assets"][main]["native_size"]
        mid_height = float(np.mean(cat["assets"][main]["height_range"]))
        mid_width = mid_height*main_native_w/main_native_h
        pivot = cat["assets"][main]["pivot"]
        seed_bounds = [cx-mid_width*pivot[0], cy-mid_height*pivot[1], mid_width, mid_height]
        if not inputs.valid_tree(seed_bounds):
            rejected["seed_complete_tree_bbox_illegal"] += 1
            continue
        cluster_id = f"forest_grounding_cluster_{len(clusters):02d}"
        desired = min(int(rng.integers(12, 19)), 210-len(trees))
        if desired < 10:
            break
        quota = dict(zip((main, companion, "natural_sapling"), composition_quota(desired, rng)))
        proposed = []
        u, v = np.array([math.cos(angle), math.sin(angle)]), np.array([-math.sin(angle), math.cos(angle)])
        for trial in range(900):
            if len(proposed) >= desired:
                break
            # Unequal Gaussian lobes are mixed with a thin, sparse outer edge.
            edge = rng.random() < .28
            offset = rng.normal(0, [.47*radius, .35*radius*stretch])
            if edge:
                theta = float(rng.uniform(-math.pi, math.pi))
                offset = np.array([math.cos(theta)*radius*rng.uniform(.72, 1.05),
                                   math.sin(theta)*radius*stretch*rng.uniform(.72, 1.05)])
            if np.linalg.norm(offset/[radius, radius*stretch]) > 1.12:
                continue
            offset[0] += (24 if offset[1] < 0 else -13)*math.sin(offset[1]/83)
            point = center+u*offset[0]+v*offset[1]
            normalized = float(np.linalg.norm(offset/[radius, radius*stretch]))
            available = [key for key, remaining in quota.items() if remaining > 0]
            weights = np.asarray([quota[key]*(2.8 if key == "natural_sapling" and normalized > .67 else
                                               .28 if key == "natural_sapling" else 1.) for key in available], dtype=float)
            key = str(rng.choice(available, p=weights/weights.sum()))
            row = make_tree(key, point, cat["assets"], rng, cluster_id)
            trunk_gap = float(rng.uniform(80, 100) if normalized > .66 else rng.uniform(65, 85))
            row["minimum_trunk_gap_world"] = round(trunk_gap, 5)
            row["cluster_zone"] = "edge" if normalized > .66 else "core"
            box = row["bounds"]
            if not inputs.valid_tree(box):
                rejected["tree_illegal_bbox_or_gap"] += 1
                continue
            if any(rect_distance(box, old["bounds"]) < 36 for old in inputs.edge_trees):
                rejected["unchanged_edge_tree"] += 1
                continue
            if any(rect_distance(box, other["bounds"]) < cluster_min_gap for other in trees):
                rejected["between_cluster_canopy_gap"] += 1
                continue
            if any(math.dist(row["position"], other["position"]) < max(trunk_gap, other["minimum_trunk_gap_world"]) for other in proposed):
                rejected["trunk_spacing"] += 1
                continue
            proposed.append(row)
            quota[key] -= 1
        if len(proposed) < 10:
            rejected["cluster_too_small"] += 1
            continue
        composition = tree_composition(proposed, main, companion, cat["assets"])
        if not composition["composition_passed"]:
            rejected["truncated_cluster_composition"] += 1
            continue
        trees.extend(proposed)
        clusters.append({"id": cluster_id, "x": round(cx, 5), "y": round(cy, 5),
                         "radius": round(radius, 5), "stretch": round(stretch, 5),
                         "angle": round(angle, 6), "main_asset": main, "companion_asset": companion,
                         "count": len(proposed), **composition,
                         "minimum_other_cluster_canopy_gap_world": cluster_min_gap})
        print(json.dumps({"forest_cluster": cluster_id, "members": len(proposed), "requested": desired, "trees_so_far": len(trees)}), flush=True)
    if not trees:
        raise ValueError("No legal forest group was found in the actual native geometry")
    for i, row in enumerate(trees):
        row["id"] = f"forest_grounding_tree_{i:04d}"
    return trees, clusters, {"seed_attempts": attempt+1, "rejections": dict(rejected),
                              "quality_before_count": True, "soft_tree_count_target": [150, 210],
                              "soft_tree_count_target_met": 150 <= len(trees) <= 210}


def decoration(key, point, cat, rng, width=None, opacity=None, draw_order=0, opacity_scale=1., tint=None):
    asset = cat["assets"][key]
    lo, hi = asset.get("width_range", [90, 160])
    nw, nh = asset["native_size"]
    width = float(rng.uniform(lo, hi) if width is None else width)
    width = min(width, nw*.98/NEAR_ZOOM)
    height = width*nh/nw
    flip = bool(rng.integers(0, 2))
    px, py = asset["pivot"]
    if flip:
        px = 1-px
    x, y = map(float, point)
    bounds = [x-width*px, y-height*py, width, height]
    if opacity is None:
        opacity = float(rng.uniform(*asset.get("opacity_range", [.4, .7])))
    opacity *= opacity_scale
    return {"id": "", "asset": key, "x": round(x, 5), "y": round(y, 5), "z": 24,
            "position": [round(x, 5), round(y, 5)], "bounds": [round(v, 5) for v in bounds],
            "world_width": round(width, 5), "world_height": round(height, 5), "flip": flip,
            "opacity": round(opacity, 5), "tint": asset.get("tint", [1., 1., 1.]) if tint is None else tint,
            "catalog_opacity_scale": round(opacity_scale, 5),
            "layer": "ground_decals", "draw_order": draw_order}


def generate_ground(inputs, cat, trees, clusters, seed):
    rng = np.random.default_rng(seed+1)
    ground, rejected, contours = [], Counter(), []
    by_cluster = defaultdict(list)
    for tree in trees:
        by_cluster[tree["cluster_id"]].append(tree)
    for cluster in clusters:
        rows = by_cluster[cluster["id"]]
        # Overlapping wide, irregular washes join the feet into a common forest
        # floor. Smaller outer washes soften the boundary instead of haloing
        # every tree with the same independent circle.
        for i, tree in enumerate(rows):
            point = np.array(tree["position"])+[rng.uniform(-18, 18), rng.uniform(-21, 5)]
            key = "forest_floor" if i % 4 != 0 else "forest_edge"
            lo, hi = cat["assets"][key].get("width_range", [170, 240])
            row = decoration(key, point, cat, rng, width=float(rng.uniform(lo, hi)), draw_order=0,
                             opacity_scale=float(rng.uniform(.55, .75)))
            if inputs.valid_common(row["bounds"]) and not rect_sum(inputs.mountain_cells, row["bounds"], inputs.cell):
                row.update(habitat="forest_floor", cluster_id=cluster["id"], tree_anchor=tree["id"])
                ground.append(row)
            else:
                rejected["forest_floor_bbox"] += 1
            row = decoration("root_shade", tree["position"], cat, rng, draw_order=2)
            if inputs.valid_common(row["bounds"]):
                row.update(habitat="root_contact", cluster_id=cluster["id"], tree_anchor=tree["id"])
                ground.append(row)
            else:
                rejected["root_bbox"] += 1
        # Link nearby trunks with low-opacity bridge dabs wherever needed, so
        # the base reads as one woodland mass and still has irregular edges.
        points = np.asarray([t["position"] for t in rows])
        kd = cKDTree(points)
        linked = set()
        for index, point in enumerate(points):
            _, indices = kd.query(point, k=min(2, len(points)))
            for j in np.atleast_1d(indices)[1:]:
                pair = tuple(sorted((index, int(j))))
                if pair in linked or math.dist(point, points[j]) > 155:
                    continue
                linked.add(pair)
                if rng.random() > .62:
                    continue
                middle = (point+points[j])*.5+[0, -8]
                row = decoration("forest_floor", middle, cat, rng, draw_order=0,
                                 opacity_scale=float(rng.uniform(.55, .75)))
                if inputs.valid_common(row["bounds"]) and not rect_sum(inputs.mountain_cells, row["bounds"], inputs.cell):
                    row.update(habitat="forest_floor_link", cluster_id=cluster["id"])
                    ground.append(row)
        # A few off-centre litter/grass rims break the outside contour.
        for tree in sorted(rows, key=lambda t: math.dist(t["position"], [cluster["x"], cluster["y"]]), reverse=True)[:4]:
            point = np.array(tree["position"])+rng.normal(0, [24, 15])
            row = decoration("forest_edge", point, cat, rng, draw_order=1,
                             opacity_scale=float(rng.uniform(.55, .75)))
            if inputs.valid_common(row["bounds"]) and not rect_sum(inputs.mountain_cells, row["bounds"], inputs.cell):
                row.update(habitat="forest_edge", cluster_id=cluster["id"])
                ground.append(row)
    for mountain_index, mountain in enumerate(inputs.mountains):
        c = inputs.contour(mountain)
        points = c["points"]
        columns, bottoms = c["columns"], c["bottoms"]
        count = len(points)
        # The native lower edge is retained at every column for the report.
        contour_report = {"mountain_id": mountain["id"], "asset": mountain["asset"],
                          "flip": bool(mountain.get("flip", False)), "source": c["source"],
                          "source_region": c["region"], "alpha_threshold": ALPHA,
                          "native_size": c["native_size"], "visible_columns": count,
                          "native_columns": columns.tolist(), "native_bottom_y": bottoms.tolist(),
                          "world_lower_edge": np.round(points, 5).tolist(), "decoration_count": 0}
        # Contact earth and occasional debris replace the old three full bands.
        # Native columns still provide every individual anchor's actual y.
        selected = []
        target_step = 40. if mountain["bounds"][2] > 180 else 27.
        last_x = -math.inf
        for j, point in enumerate(points):
            if j == count-1 or point[0]-last_x >= target_step*rng.uniform(.72, 1.18):
                selected.append(j)
                last_x = point[0]
        for anchor_number, anchor_index in enumerate(selected):
            anchor = points[anchor_index]
            relative = anchor_number/max(1, len(selected)-1)
            layers = []
            if anchor_number % 3 == mountain_index % 3 and .10 < relative < .90:
                layers.append(("mountain_soil", 0))
            if rng.random() < .50:
                layers.append(("mountain_scree" if rng.random() < .66 else "mountain_meadow", 1))
            if anchor_number % 5 == 1 and .12 < relative < .88:
                layers.append(("root_shade", 2))
            for key, order in layers:
                asset = cat["assets"][key]
                rule = APRON_RULES[key]
                lo, hi = rule["width"]
                hi = max(lo, min(hi, mountain["bounds"][2]*.85))
                width = float(rng.uniform(lo, hi))
                # Place each patch so its footprint reaches uphill under the
                # visible mountain and downhill onto clear ground.
                nh, nw = asset["native_size"][1], asset["native_size"][0]
                h = width*nh/nw
                dx = float(rng.uniform(-7, 7))
                dy = float(rng.uniform(-.16, .07)*h)
                point = anchor+[dx, dy]
                row = decoration(key, point, cat, rng, width=width, draw_order=order,
                                 opacity=float(rng.uniform(*rule["opacity"])), tint=rule["tint"])
                if not inputs.valid_common(row["bounds"]):
                    rejected["mountain_apron_bbox"] += 1
                    continue
                row.update(habitat="mountain_apron", style_profile="foothill_contact_v2", mountain_id=mountain["id"],
                           native_contour_column=int(columns[anchor_index]),
                           native_contour_bottom_y=int(bottoms[anchor_index]),
                           contour_anchor=np.round(anchor, 5).tolist(),
                           contour_offset=[round(dx, 5), round(dy, 5)])
                ground.append(row)
                contour_report["decoration_count"] += 1
        contours.append(contour_report)
    for i, row in enumerate(ground):
        row["id"] = f"forest_grounding_decal_{i:05d}"
    ground.sort(key=lambda row: (row["draw_order"], GROUND_IDS.index(row["asset"]), row["id"]))
    return ground, contours, dict(rejected)


def analyze(inputs):
    return {"bounds": BOUNDS, "world_size": WORLD, "white_stone_runtime_id": "white_stone_town",
            "white_stone_location_id": "bc_place_021", "white_stone_position": inputs.white,
            "old_trees_intersecting": sum(intersects(BOUNDS, t["bounds"]) for t in inputs.scene["trees"]),
            "old_trees_fully_contained_and_removed": len(inputs.old_removed),
            "unchanged_edge_trees": [t["id"] for t in inputs.edge_trees if intersects(BOUNDS, t["bounds"])],
            "mountains": [{k: m[k] for k in ("id", "asset", "flip", "bounds")} for m in inputs.mountains],
            "navigation_cell_world": inputs.cell, "navigation_shape": list(inputs.nav.shape),
            "building_exclusions": len(inputs.buildings), "bridge_exclusions": len(inputs.bridges),
            "building_margin_world": 45, "bridge_margin_world": 30,
            "all_new_geometry_fully_inside_sample": True, "empty_forest_corridors": inputs.corridors}


def corridor_audit(inputs, trees):
    result = []
    for corridor in inputs.corridors:
        samples, blocked, displayed_water, canopy_overlap = 0, 0, 0, 0
        for a, b in zip(corridor["points"], corridor["points"][1:]):
            a, b = np.asarray(a), np.asarray(b)
            d = b-a
            length = float(np.linalg.norm(d))
            normal = np.array([-d[1], d[0]])/length
            for t in np.linspace(0, 1, max(2, math.ceil(length/(inputs.cell/3)))):
                center = a+d*t
                for across in np.linspace(-corridor["width"]/2, corridor["width"]/2, 21):
                    point = center+normal*across
                    gx, gy = np.floor(point/inputs.cell).astype(int)
                    sx, sy = np.floor(point/inputs.source_cell).astype(int)
                    blocked += int(inputs.nav[gy, gx] != ord("0"))
                    displayed_water += int(inputs.display_water[sy, sx])
                    samples += 1
            canopy_overlap += sum(segment_rect_distance(a, b, row["bounds"]) < corridor["width"]/2-.001 for row in trees)
        result.append({**corridor, "sampled_points": samples, "nonzero_navigation_samples": blocked,
                       "display_water_samples": displayed_water, "new_tree_bbox_intersections": canopy_overlap,
                       "passable_and_clear": not blocked and not displayed_water and not canopy_overlap})
    return result


def validate(inputs, tcat, gcat, trees, clusters, ground, contours, generation):
    violations, dimensions = Counter(), defaultdict(list)
    native_ratios = []
    for row in trees:
        asset = tcat["assets"][row["asset"]]
        box = row["bounds"]
        if not inputs.valid_tree(box):
            violations["tree_complete_bbox_illegal"] += 1
        w, h = box[2:]
        px, py = asset["pivot"]
        if row["flip"]:
            px = 1-px
        if abs(box[0]+px*w-row["x"]) > .0001 or abs(box[1]+py*h-row["y"]) > .0001:
            violations["tree_pivot_bbox"] += 1
        if abs(w/h-asset["native_size"][0]/asset["native_size"][1]) > .00001:
            violations["tree_actual_native_aspect"] += 1
        if row["z"] != row["y"] or row["position"] != [row["x"], row["y"]]:
            violations["tree_xyz_feet"] += 1
        if not 76 <= h <= 138.00001 or row["asset"] == "natural_sapling" and h > 98.00001:
            violations["tree_height_range"] += 1
        lo, hi = (65, 85) if row["cluster_zone"] == "core" else (80, 100)
        if not lo <= row["minimum_trunk_gap_world"] <= hi:
            violations["loose_core_edge_trunk_spacing_rule"] += 1
        dimensions[row["asset"]].append([w, h])
        native_ratios.append(h*NEAR_ZOOM/asset["native_size"][1])
    for row in ground:
        asset = gcat["assets"][row["asset"]]
        box = row["bounds"]
        if not inputs.valid_common(box):
            violations["ground_water_building_bridge_bounds"] += 1
        if row["z"] != 24 or row["layer"] != "ground_decals":
            violations["ground_xyz_layer"] += 1
        if abs(box[2]/box[3]-asset["native_size"][0]/asset["native_size"][1]) > .00001:
            violations["ground_actual_native_aspect"] += 1
        px, py = asset["pivot"]
        if row["flip"]:
            px = 1-px
        if abs(box[0]+px*box[2]-row["x"]) > .0001 or abs(box[1]+py*box[3]-row["y"]) > .0001:
            violations["ground_pivot_bbox"] += 1
        expected_tint = APRON_RULES[row["asset"]]["tint"] if row["habitat"] == "mountain_apron" else asset.get("tint", [1., 1., 1.])
        if row.get("tint", [1., 1., 1.]) != expected_tint:
            violations["ground_catalog_tint"] += 1
        if row["habitat"] == "mountain_apron":
            anchor = row["contour_anchor"]
            if not box[1] < anchor[1] < box[1]+box[3]:
                violations["apron_must_reach_both_sides_of_real_edge"] += 1
            rule = APRON_RULES[row["asset"]]
            if not rule["width"][0]-.00001 <= box[2] <= rule["width"][1]+.00001:
                violations["subtle_apron_width"] += 1
            if not rule["opacity"][0]-.00001 <= row["opacity"] <= rule["opacity"][1]+.00001:
                violations["subtle_apron_opacity"] += 1
        elif row["asset"] in ("forest_floor", "forest_edge"):
            multiplier = row["catalog_opacity_scale"]
            if not .55 <= multiplier <= .75:
                violations["forest_floor_opacity_multiplier"] += 1
            lo, hi = asset["opacity_range"]
            if not lo*multiplier-.00001 <= row["opacity"] <= hi*multiplier+.00001:
                violations["forest_floor_effective_opacity"] += 1
        native_ratios.append(box[2]*NEAR_ZOOM/asset["native_size"][0])
    overlap = []
    nearest = []
    between = math.inf
    for i, row in enumerate(trees):
        others = [t for t in trees if t["id"] != row["id"] and t["cluster_id"] == row["cluster_id"]]
        overlap.append(any(intersects(row["bounds"], t["bounds"]) for t in others))
        nearest.append(min(math.dist(row["position"], t["position"]) for t in others))
        for other in trees[i+1:]:
            if row["cluster_id"] != other["cluster_id"]:
                between = min(between, rect_distance(row["bounds"], other["bounds"]))
            elif math.dist(row["position"], other["position"])+.00002 < max(row["minimum_trunk_gap_world"], other["minimum_trunk_gap_world"]):
                violations["actual_minimum_core_edge_trunk_distance"] += 1
    if between < 77.999:
        violations["between_cluster_gap"] += 1
    if max(native_ratios) > 1.00001:
        violations["near_view_native_upscale"] += 1
    corridor_report = corridor_audit(inputs, trees)
    if any(not row["passable_and_clear"] for row in corridor_report):
        violations["measured_passable_forest_corridor"] += 1
    for cluster in clusters:
        members = [tree for tree in trees if tree["cluster_id"] == cluster["id"]]
        if not 10 <= len(members) <= 18:
            violations["loose_cluster_tree_count_10_18"] += 1
        if not tree_composition(members, cluster["main_asset"], cluster["companion_asset"], tcat["assets"])["composition_passed"]:
            violations["cluster_family_main_companion_sapling_composition"] += 1
    mountain_apron_count = sum(row["habitat"] == "mountain_apron" for row in ground)
    floor_link_count = sum(row["habitat"] == "forest_floor_link" for row in ground)
    if not 30 <= mountain_apron_count <= 55:
        violations["sparse_mountain_apron_count_30_55"] += 1
    if floor_link_count > 262:
        violations["forest_floor_links_not_at_least_halved"] += 1
    after = {rel(p): sha(p) for p in inputs.protected}
    source_changed = {p: [h, after[p]] for p, h in inputs.before.items() if after[p] != h}
    if source_changed:
        violations["immutable_source_changed"] = len(source_changed)
    views = {}
    for name, center in (("white_stone", inputs.white), ("sample_center", [5400, 14100]),
                         ("mountain_foot", [4860, 14480])):
        box = [center[0]-1200, center[1]-675, 2400, 1350]
        visible_ids = {t["id"] for t in inputs.scene["trees"] if intersects(box, t["bounds"])}
        removed_visible = len(visible_ids & {t["id"] for t in inputs.old_removed})
        views[name] = {"center": center, "zoom": .8, "viewport_pixels": [1920, 1080],
                       "old_visible": len(visible_ids), "removed_visible": removed_visible,
                       "unchanged_visible": len(visible_ids)-removed_visible,
                       "new_visible": sum(intersects(box, t["bounds"]) for t in trees),
                       "ground_visible": sum(intersects(box, t["bounds"]) for t in ground),
                       "mountain_aprons_visible": sum(intersects(box, t["bounds"]) and t["habitat"] == "mountain_apron" for t in ground)}
    return {"passed": not violations, "violations": dict(violations), "profile": PROFILE,
            "source": analyze(inputs), "seed": SEED, "tree_count": len(trees), "cluster_count": len(clusters),
            "tree_count_target": [150, 210], "tree_count_target_met": 150 <= len(trees) <= 210,
            "tree_count_is_not_a_fill_quota": True,
            "tree_by_asset": dict(Counter(t["asset"] for t in trees)),
            "tree_world_width_height_ranges": {k: {"min": np.min(v, axis=0).tolist(), "max": np.max(v, axis=0).tolist()} for k, v in dimensions.items()},
            "nearest_same_cluster_trunk_distance_world": {"min": min(nearest), "median": float(np.median(nearest)), "max": max(nearest)},
            "tree_crowns_with_overlapping_same_cluster_bbox": sum(overlap),
            "tree_crown_overlap_fraction": sum(overlap)/len(overlap),
            "minimum_between_cluster_complete_bbox_gap_world": between,
            "measured_empty_corridors": corridor_report,
            "ground_count": len(ground), "ground_by_asset": dict(Counter(t["asset"] for t in ground)),
            "ground_by_habitat": dict(Counter(t["habitat"] for t in ground)),
            "forest_floor_link_count_first_round": 524, "forest_floor_link_count_current": floor_link_count,
            "mountain_apron_rules": APRON_RULES,
            "mountain_apron_count": mountain_apron_count,
            "mountain_apron_by_mountain": dict(Counter(t["mountain_id"] for t in ground if t["habitat"] == "mountain_apron")),
            "cluster_family_composition": [{key: c[key] for key in ("id", "count", "main_asset", "companion_asset", "main_family", "main_asset_fraction", "companion_asset_fraction", "sapling_fraction", "main_family_fraction")} for c in clusters],
            "ground_is_nonblocking": True, "ground_z": 24, "tree_navigation_changed": False,
            "ground_can_overlap_only_mountain_nav_class": 1,
            "ground_overlapping_mountain_nav_count": sum(bool(rect_sum(inputs.mountain_cells, t["bounds"], inputs.cell)) for t in ground),
            "ground_displayed_water_overlap_count": sum(bool(rect_sum(inputs.display_water_table, t["bounds"], inputs.source_cell)) for t in ground),
            "max_near_view_pixels_per_native_pixel": max(native_ratios),
            "mountain_contour_algorithm": "each native alpha column's lowest pixel >=46, flip before extraction; transform column centres by original bounds",
            "mountain_contour_count": len(contours), "mountain_contour_native_column_count": sum(c["visible_columns"] for c in contours),
            "views": views, "generation": generation, "source_sha256": inputs.before,
            "new_native_atlases_sha256": {tcat["atlas"]: sha(resolve(tcat["atlas"])),
                                          gcat["atlas"]: sha(resolve(gcat["atlas"]))},
            "sources_unchanged": not source_changed, "changed_sources": source_changed,
            "runtime_performance_measurement": "pending isolated renderer GPU preview; this offline QA does not claim FPS"}


def preview(inputs, trees, ground, contours, name="placement_plan.png"):
    scale = .5
    im = Image.new("RGB", (round(BOUNDS[2]*scale), round(BOUNDS[3]*scale)), (213, 197, 152))
    draw = ImageDraw.Draw(im)
    def box(bounds):
        return [(bounds[0]-BOUNDS[0])*scale, (bounds[1]-BOUNDS[1])*scale,
                (bounds[0]+bounds[2]-BOUNDS[0])*scale, (bounds[1]+bounds[3]-BOUNDS[1])*scale]
    xa, ya, xb, yb = rectangle_cells(BOUNDS, inputs.cell, inputs.nav.shape)
    for gy in range(ya, yb):
        for gx in range(xa, xb):
            value = inputs.nav[gy, gx]
            if value == ord("0"):
                continue
            bounds = [gx*inputs.cell, gy*inputs.cell, inputs.cell+.1, inputs.cell+.1]
            draw.rectangle(box(bounds), fill=(125, 127, 96) if value == ord("1") else (92, 154, 160))
    for corridor in inputs.corridors:
        points = [((p[0]-BOUNDS[0])*scale, (p[1]-BOUNDS[1])*scale) for p in corridor["points"]]
        draw.line(points, fill=(238, 221, 177), width=round(corridor["width"]*scale))
    colors = {"natural_oak_mature": (58, 98, 53), "natural_oak_young": (82, 121, 70),
              "natural_pine_wide": (39, 81, 57), "natural_pine_lean": (57, 94, 70),
              "natural_elm_wide": (93, 112, 55), "natural_sapling": (125, 141, 83)}
    for row in ground:
        if row["habitat"] == "mountain_apron":
            draw.ellipse(box(row["bounds"]), outline=(155, 111, 70), width=1)
    for row in trees:
        draw.rectangle(box(row["bounds"]), outline=colors[row["asset"]], width=1)
        x, y = (row["x"]-BOUNDS[0])*scale, (row["y"]-BOUNDS[1])*scale
        draw.ellipse([x-2, y-2, x+2, y+2], fill=colors[row["asset"]])
    for contour in contours:
        pts = [((p[0]-BOUNDS[0])*scale, (p[1]-BOUNDS[1])*scale) for p in contour["world_lower_edge"]]
        draw.line(pts, fill=(224, 70, 42), width=2)
    for ident, b in inputs.buildings:
        if intersects(BOUNDS, b):
            draw.rectangle(box(b), outline=(180, 49, 42), width=2)
            draw.text((box(b)[0], box(b)[3]+2), ident+" +45", fill=(130, 35, 31))
    im.save(OUT/name)


def independent_audit(inputs, patch_path=PATCH, ground_path=GROUND):
    """Read saved runtime output again; inspect source cell slices directly."""
    patch, ground = read(patch_path), read(ground_path)
    tree_cat = read(resolve(patch["tree_catalog"]))
    ground_cat = read(resolve(ground["catalog_path"]))
    failures = Counter()
    ground_mountain_count = 0
    row_count = Counter()
    sdf_mask = np.asarray(Image.open(inputs.sdf_path).convert("L")) < 128
    water_nav = rows_array(inputs.water["water_rows"]) == ord("2")
    bridge_nav = rows_array(inputs.water["bridge_open_rows"]) == ord("1")
    for kind, rows, assets in (("tree", patch["trees"], tree_cat["assets"]),
                               ("ground", ground["decorations"], ground_cat["assets"])):
        for row in rows:
            row_count[kind] += 1
            x, y, w, h = row["bounds"]
            gx0, gy0 = max(0, math.floor(x/inputs.cell)), max(0, math.floor(y/inputs.cell))
            gx1, gy1 = math.ceil((x+w)/inputs.cell), math.ceil((y+h)/inputs.cell)
            cells = inputs.nav[gy0:gy1, gx0:gx1]
            if kind == "tree" and np.any(cells != ord("0")):
                failures["tree_bbox_has_nonzero_nav"] += 1
            if np.any(cells == ord("2")) or np.any(water_nav[gy0:gy1, gx0:gx1]):
                failures[kind+"_bbox_has_nav_water"] += 1
            if np.any(bridge_nav[gy0:gy1, gx0:gx1]):
                failures[kind+"_bbox_has_bridge_open"] += 1
            sx0, sy0 = math.floor(x/inputs.source_cell), math.floor(y/inputs.source_cell)
            sx1, sy1 = math.ceil((x+w)/inputs.source_cell), math.ceil((y+h)/inputs.source_cell)
            if np.any(sdf_mask[sy0:sy1, sx0:sx1]):
                failures[kind+"_bbox_has_displayed_water"] += 1
            if kind == "ground" and np.any(cells == ord("1")):
                ground_mountain_count += 1
            if not contains(BOUNDS, row["bounds"]):
                failures[kind+"_outside_sample"] += 1
            if any(intersects(row["bounds"], b) for _, b in inputs.buildings + inputs.bridges):
                failures[kind+"_entrance_or_bridge_exclusion"] += 1
            asset = assets[row["asset"]]
            nw, nh = asset["native_size"]
            if abs(w*nh-h*nw) > .025:
                failures[kind+"_aspect_changed"] += 1
            if row["z"] != (row["y"] if kind == "tree" else 24):
                failures[kind+"_invalid_z"] += 1
            if kind == "ground":
                expected_tint = APRON_RULES[row["asset"]]["tint"] if row["habitat"] == "mountain_apron" else asset.get("tint", [1., 1., 1.])
                if row.get("tint", [1., 1., 1.]) != expected_tint:
                    failures["saved_ground_catalog_tint_mismatch"] += 1
            if kind == "ground" and row["habitat"] == "mountain_apron":
                mountain = next(m for m in inputs.mountains if m["id"] == row["mountain_id"])
                cat = inputs.scene["mountain_catalog"][mountain["asset"]]
                source = inputs.scene["mountain_sources"][cat["bank"]]
                rx, ry, rw, rh = cat["region"]
                native = Image.open(resolve(source["path"])).convert("RGBA").crop((rx, ry, rx+rw, ry+rh))
                if mountain.get("flip", False):
                    native = native.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                column = row["native_contour_column"]
                column_alpha = np.asarray(native.getchannel("A"))[:, column]
                bottom = int(np.flatnonzero(column_alpha >= ALPHA)[-1])
                bx, by, bw, bh = mountain["bounds"]
                expected_anchor = [bx+(column+.5)*bw/rw, by+(bottom+.5)*bh/rh]
                if bottom != row["native_contour_bottom_y"] or math.dist(expected_anchor, row["contour_anchor"]) > .00002:
                    failures["mountain_apron_not_on_true_native_lower_edge"] += 1
                if not y < expected_anchor[1] < y+h:
                    failures["mountain_apron_does_not_straddle_lower_edge"] += 1
    removed = set(patch["removed_tree_ids"])
    expected_removed = {t["id"] for t in inputs.scene["trees"] if contains(BOUNDS, t["bounds"])}
    if removed != expected_removed:
        failures["removed_tree_membership_changed"] += 1
    if len({t["id"] for t in patch["trees"]}) != len(patch["trees"]):
        failures["duplicate_new_tree_id"] += 1
    if {t["id"] for t in patch["trees"]} & {t["id"] for t in inputs.scene["trees"]}:
        failures["new_tree_id_collides_with_source"] += 1
    source_changes = {p: h for p, h in patch["source_sha256"].items() if sha(ROOT/p) != h}
    if source_changes:
        failures["immutable_source_sha_changed"] = len(source_changes)
    result = {"passed": not failures, "failures": dict(failures), "checked_saved_rows": dict(row_count),
              "method": "Saved JSON parsed again; direct original navigation/water/SDF array slices, exact reserved rectangles and per-decal native alpha column recomputation",
              "tree_source_count": len(inputs.scene["trees"]), "old_trees_removed": len(removed),
              "old_trees_unchanged": len(inputs.scene["trees"])-len(removed),
              "ground_overlapping_only_mountain_cells": ground_mountain_count,
              "verified_native_alpha_apron_anchors": sum(t["habitat"] == "mountain_apron" for t in ground["decorations"]),
              "immutable_source_sha_verified_count": len(patch["source_sha256"]),
              "outside_sample_geometry_added": 0, "navigation_or_existing_ground_details_written": False}
    write(OUT/"placement_independent_qa.json", result)
    if failures:
        raise AssertionError(f"Independent saved-output QA failed: {dict(failures)}")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--analyze-only", action="store_true", help="Inspect immutable source geometry; do not write final sample JSON")
    parser.add_argument("--verify-only", action="store_true", help="Independently inspect already written runtime JSON without generating it")
    parser.add_argument("--ground-catalog", type=Path, default=GROUND_CATALOG)
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    inputs = Inputs()
    if args.verify_only:
        print(json.dumps(independent_audit(inputs), ensure_ascii=False))
        return
    if args.analyze_only:
        write(OUT/"sample_inputs.json", analyze(inputs))
        print(json.dumps(analyze(inputs), ensure_ascii=False))
        return
    tcat, gcat = catalog(TREE_CATALOG, TREE_IDS), catalog(args.ground_catalog, GROUND_IDS)
    trees, clusters, generation = generate_trees(inputs, tcat, SEED)
    ground, contours, rejected_ground = generate_ground(inputs, gcat, trees, clusters, SEED)
    generation["ground_rejections"] = rejected_ground
    report = validate(inputs, tcat, gcat, trees, clusters, ground, contours, generation)
    write(OUT/"placement_qa.json", report)
    write(OUT/"mountain_native_lower_contours.json", contours)
    write(OUT/"forest_clusters.json", clusters)
    preview(inputs, trees, ground, contours)
    if not report["passed"]:
        raise ValueError(f"QA failed; final runtime JSON not written: {report['violations']}")
    patch = {"version": 1, "profile": PROFILE, "world_size": WORLD, "bounds": BOUNDS, "seed": SEED,
             "tree_catalog": resource(TREE_CATALOG), "tree_sources": {},
             "removed_tree_ids": [t["id"] for t in inputs.old_removed], "trees": trees,
             "ground_details_path": resource(GROUND), "grounded_mountain_ids": [m["id"] for m in inputs.mountains],
             "source_sha256": inputs.before,
             "coordinate_rule": "XY are immutable world feet; tree Z=Y; complete native-aspect bboxes avoid all nonzero navigation cells",
             "sample_membership": "Only old trees with complete bounds contained in bounds are removed; crossing-edge trees remain unchanged"}
    decoration_data = {"version": 1, "profile": PROFILE, "world_size": WORLD, "grid_size": 640.,
                       "catalog_path": resource(args.ground_catalog), "decorations": ground,
                       "bounds": BOUNDS, "seed": SEED,
                       "coordinate_rule": "Explicit XYZ24 ground_decals; no collision or navigation changes; foothill patches overlap original mountain bases"}
    write(PATCH, patch)
    write(GROUND, decoration_data)
    independent_audit(inputs)
    original_bytes = {p: p.read_bytes() for p in (PATCH, GROUND)}
    again_trees, again_clusters, again_generation = generate_trees(inputs, tcat, SEED)
    again_ground, again_contours, again_rejections = generate_ground(inputs, gcat, again_trees, again_clusters, SEED)
    deterministic = (trees == again_trees and clusters == again_clusters and ground == again_ground and contours == again_contours)
    serialized = lambda value: (json.dumps(value, ensure_ascii=False, indent=2)+"\n").encode("utf-8")
    second_patch = {**patch, "trees": again_trees}
    second_ground = {**decoration_data, "decorations": again_ground}
    byte_identical = original_bytes[PATCH] == serialized(second_patch) and original_bytes[GROUND] == serialized(second_ground)
    if not deterministic or not byte_identical:
        raise AssertionError("Deterministic generation failed")
    write(OUT/"determinism_qa.json", {"passed": deterministic, "seed": SEED,
        "independent_second_generation_same_objects": deterministic,
        "independent_second_runtime_serialization_identical_bytes": byte_identical,
        "runtime_files_sha256": {rel(p): sha(p) for p in original_bytes},
        "source_sha256_unchanged": all(sha(ROOT/p) == h for p, h in inputs.before.items())})
    print(json.dumps({"passed": report["passed"], "trees_removed": len(inputs.old_removed), "trees_added": len(trees),
                      "forest_clusters": len(clusters), "ground_decals": len(ground), "mountains_grounded": len(contours),
                      "tree_crown_overlap_fraction": report["tree_crown_overlap_fraction"],
                      "minimum_between_cluster_gap_world": report["minimum_between_cluster_complete_bbox_gap_world"],
                      "deterministic": deterministic, "files": [rel(PATCH), rel(GROUND)]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
