"""Author sparse, visual-only scenery from the existing compact-world geography.

This writes a separate presentation file. It never writes the navigation, places,
or graph. Terrain decisions use the existing walkability rows AND a 1024px colour
sample; an authored bridge corridor in the nav mask is not accepted as dry land.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import re

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "resources/world/bai_chuan/scenery_ink_20261104.json"
CATALOG = ROOT / "textures/world/bai_chuan/scenery_ink_20261104/catalog.json"
REVIEW = ROOT / "output/bai_chuan_scenery_20261104"
SEED = 2026110417
PREVIEW_SIZE = 1024
DEFAULTS = {
    "pine_single": ([346, 308], 42.0),
    "broadleaf_single": ([348, 317], 42.0),
    "willow_single": ([303, 300], 44.0),
    "pine_group": ([355, 283], 56.0),
    "mixed_group": ([372, 283], 56.0),
    "willow_group": ([375, 268], 54.0),
    "shrub": ([301, 202], 14.0),
    "reeds": ([283, 219], 22.0),
    "rock_group": ([318, 175], 18.0),
}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def config():
    text = (ROOT / "gdscripts/world/BaiChuanWorldScale.gd").read_text(encoding="utf-8")
    number = lambda name: float(re.search(rf"^const {name} := ([0-9.]+)", text, re.M)[1])
    path = lambda name: ROOT / re.search(rf'^const {name} := "res://([^"]+)"', text, re.M)[1]
    return {"size": number("MAP_PIXELS"), "km": number("WORLD_KM"),
            "layout": path("LAYOUT_PATH"), "nav": path("NAVIGATION_PATH"),
            "preview": path("ART_BASE") / "preview_2090.png"}


def res_path(path):
    return "res://" + path.relative_to(ROOT).as_posix()


def distance(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def resample(points, step=8.0):
    result = [np.asarray(points[0], dtype=float)]
    for a, b in zip(points[:-1], points[1:]):
        a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
        length = np.linalg.norm(b - a)
        if length < 0.01:
            continue
        count = max(1, math.ceil(length / step))
        result.extend(a + (b - a) * t / count for t in range(1, count + 1))
    return np.asarray(result)


def runs(flags):
    start = None
    for i, flag in enumerate([*flags, False]):
        if flag and start is None:
            start = i
        elif not flag and start is not None:
            yield start, i
            start = None


class Geography:
    def __init__(self, cfg, layout, nav, catalog):
        self.cfg, self.layout, self.nav, self.catalog = cfg, layout, nav, catalog
        self.size = cfg["size"]
        self.ppw = PREVIEW_SIZE / self.size
        # Load only the existing 2090px review, immediately reduce it, and keep
        # no 6270px mother-image CPU copy in this authoring workflow or runtime.
        with Image.open(cfg["preview"]) as source:
            self.image = source.convert("RGB").resize((PREVIEW_SIZE, PREVIEW_SIZE), Image.Resampling.LANCZOS)
        rgb = np.asarray(self.image).astype(np.int16)
        r, g, b = rgb.transpose(2, 0, 1)
        self.brightness = rgb.mean(2)
        water = (b - r >= 20) & (b - g >= 7) & (self.brightness >= 108)
        face = (self.brightness < 180) & (rgb.max(2) - rgb.min(2) < 66) & (b - g >= -12) & ~water
        seed = face & (b - g >= -6) & (g - r >= 9)
        rock = ndimage.binary_propagation(seed, mask=face)
        rock = ndimage.binary_fill_holes(ndimage.binary_closing(rock, iterations=2))
        self.water = water
        self.mountain = ndimage.binary_dilation(rock, iterations=1)
        self.water_distance = ndimage.distance_transform_edt(~water) / self.ppw
        self.mountain_distance = ndimage.distance_transform_edt(~self.mountain) / self.ppw
        forest = (g - r > 7) & (g - b > 5) & (self.brightness < 187) & ~water & ~self.mountain
        self.forest = ndimage.gaussian_filter(forest.astype(float), 1.2) > 0.21
        self.forest_distance = ndimage.distance_transform_edt(~self.forest) / self.ppw
        self.forest_density = ndimage.gaussian_filter(self.forest.astype(float), 2.0)
        rows = np.array([[int(v) for v in row] for row in nav["rows"]], dtype=np.uint8)
        coords = np.minimum((np.arange(PREVIEW_SIZE) / self.ppw / nav["cell_size"]).astype(int), len(rows) - 1)
        self.walkable = rows[coords[:, None], coords[None, :]] == 0
        self.dry = self.walkable & ~ndimage.binary_dilation(water, iterations=1) & ~self.mountain
        # The complete route centrelines are kept free of vegetation even
        # though only a tiny selected subset will receive visible dirt art.
        route_image = Image.new("L", self.image.size, 0)
        draw = ImageDraw.Draw(route_image)
        for points in layout["displayEdges"].values():
            draw.line([(x * self.ppw, y * self.ppw) for x, y in points], fill=255, width=1)
        self.route_distance = ndimage.distance_transform_edt(np.asarray(route_image) == 0) / self.ppw
        self.avoid_rects = []
        for location in layout["locations"].values():
            x0, y0, x1, y1 = location["rect"]
            ax, ay = location["drawPos"]
            # Runtime papers can move to either side of a building. Reserve
            # both paper columns and the entrance, not just its logical point.
            self.avoid_rects.append((x0 - 195, y0 - 125, x1 + 195, y1 + 92))
            self.avoid_rects.append((ax - 88, ay - 90, ax + 88, ay + 85))
        self.placements = []
        self.rejected = Counter()

    def pixel(self, point):
        return np.clip((np.asarray(point) * self.ppw).astype(int), 0, PREVIEW_SIZE - 1)

    def sample(self, field, point):
        x, y = self.pixel(point)
        return field[y, x]

    def sample_many(self, field, points):
        xy = np.clip((np.asarray(points) * self.ppw).astype(int), 0, PREVIEW_SIZE - 1)
        return field[xy[:, 1], xy[:, 0]]

    def dimensions(self, asset, height):
        data = self.catalog.get(asset, {})
        w, h = data.get("size", DEFAULTS.get(asset, ([1, 1], 20))[0])
        return height * w / max(h, 1), height

    def sprite_rect(self, point, asset, height, compensation=1.0):
        width, height = self.dimensions(asset, height)
        width, height = width * compensation, height * compensation
        x, y = point
        return x - width / 2, y - height, x + width / 2, y + 2

    def rect_avoids_places(self, rect):
        x0, y0, x1, y1 = rect
        return not any(x0 < b[2] and x1 > b[0] and y0 < b[3] and y1 > b[1] for b in self.avoid_rects)

    def safe(self, point, asset, height):
        rect = self.sprite_rect(point, asset, height)
        x0, y0, x1, y1 = rect
        if min(x0, y0) < 35 or max(x1, y1) > self.size - 35:
            return "bounds"
        # Validate the foot, surrounding ground, and the entire small visual
        # rectangle, so a mask's artificial crossing cannot float a tree.
        footprint = [[x, y] for x in np.linspace(x0, x1, 5) for y in np.linspace(y0, y1, 5)]
        if not np.all(self.sample_many(self.dry, footprint)):
            return "water_or_mountain_or_nonwalkable"
        if self.sample(self.brightness, point) < 176 or self.sample(self.forest_density, point) > 0.52:
            return "existing_dense_ink"
        if self.sample(self.route_distance, point) < 32 + (x1 - x0) * 0.28:
            return "route_centreline"
        compensation = 2.0 if asset.endswith("group") and asset != "rock_group" else 1.72
        if not self.rect_avoids_places(self.sprite_rect(point, asset, height, compensation)):
            return "building_paper_or_entrance"
        if any(distance(point, p["position"]) < 26 for p in self.placements):
            return "trunk_spacing"
        return ""

    def add(self, point, asset, rng, composition, height_scale=1.0):
        base_height = float(self.catalog.get(asset, {}).get("world_height", DEFAULTS[asset][1]))
        height = round(base_height * height_scale, 3)
        reason = self.safe(point, asset, height)
        if reason:
            self.rejected[reason] += 1
            return False
        self.placements.append({
            "id": f"bc_scenery_{len(self.placements) + 1:04d}", "asset": asset,
            "position": [round(float(v), 3) for v in point], "height": height,
            "flip_h": bool(rng.integers(0, 2)), "opacity": round(float(rng.uniform(0.79, 0.94)), 3),
            "composition": composition, "lod_rank": int(rng.integers(0, 12)),
        })
        return True


def build_vegetation(geo, rng):
    # These loose bands create intentional openings between compositions.
    # They describe art rhythm, not new terrain regions or gameplay biomes.
    bands = [(.04, .31, .08, .22), (.37, .64, .08, .20), (.69, .87, .10, .24),
             (.04, .28, .28, .44), (.34, .56, .34, .46), (.63, .85, .35, .49),
             (.06, .29, .49, .59), (.31, .53, .53, .65), (.59, .84, .59, .73),
             (.08, .32, .73, .86), (.35, .55, .79, .94), (.60, .81, .81, .94)]
    centres = []
    for band_index, (x0, x1, y0, y1) in enumerate(bands):
        target = 5 if band_index < 8 else 6
        made = 0
        for _ in range(1800):
            if made >= target:
                break
            point = np.array([rng.uniform(x0, x1), rng.uniform(y0, y1)]) * geo.size
            if any(distance(point, centre) < 260 for centre in centres):
                continue
            edge_distance = geo.sample(geo.forest_distance, point)
            if not 24 <= edge_distance <= 165:
                continue
            group = "pine_group" if point[0] < geo.size * .34 or point[1] < geo.size * .24 else "mixed_group"
            if geo.sample(geo.water_distance, point) < 115:
                group = "willow_group"
            height_scale = float(rng.uniform(.90, 1.045))
            if not geo.add(point, group, rng, f"edge_{band_index + 1:02d}_{made + 1:02d}", height_scale):
                continue
            composition = geo.placements[-1]["composition"]
            centres.append(point)
            made += 1
            single = {"pine_group": "pine_single", "mixed_group": "broadleaf_single", "willow_group": "willow_single"}[group]
            # A broken crescent, a small outlier, and one low accent: avoid
            # repeating a single opaque forest tile or a regular tree grid.
            angle = rng.uniform(-math.pi, math.pi)
            offsets = [(-64, 18), (50, 34), (96, -18), (-103, 51), (34, 87)]
            rotation = np.array([[math.cos(angle), -math.sin(angle)], [math.sin(angle), math.cos(angle)]])
            for index, offset in enumerate(offsets):
                asset = single if index < 4 else ("shrub" if made % 3 else "rock_group")
                for _ in range(10):
                    candidate = point + rotation @ (np.array(offset) + rng.normal(0, 18, 2))
                    if geo.add(candidate, asset, rng, composition, float(rng.uniform(.89, 1.045))):
                        break

    # Small bank compositions sit on dry ground outside the blue silhouette.
    # Nothing in this shoreline pass is allowed on the water itself.
    bank_centres = []
    for _ in range(14000):
        if len(bank_centres) >= 26:
            break
        point = rng.uniform(.045, .88, 2) * geo.size
        if not 27 <= geo.sample(geo.water_distance, point) <= 90:
            continue
        if any(distance(point, centre) < 175 for centre in bank_centres):
            continue
        if not geo.add(point, "reeds", rng, f"bank_{len(bank_centres) + 1:02d}", float(rng.uniform(.92, 1.08))):
            continue
        composition = geo.placements[-1]["composition"]
        bank_centres.append(point)
        for _ in range(24):
            candidate = point + rng.uniform(-75, 75, 2)
            if geo.add(candidate, "willow_single" if len(bank_centres) % 3 == 0 else "reeds", rng, composition):
                break

    # Finish the authored count with very sparse outliers near an established
    # composition. Open valleys elsewhere deliberately stay open.
    count_before_accents = len(geo.placements)
    for _ in range(15000):
        if len(geo.placements) >= 500:
            break
        centre = centres[int(rng.integers(0, len(centres)))]
        point = centre + rng.normal(0, 133, 2)
        if distance(point, centre) < 80 or distance(point, centre) > 245:
            continue
        asset = str(rng.choice(["pine_single", "broadleaf_single", "shrub", "rock_group"], p=[.20, .40, .23, .17]))
        geo.add(point, asset, rng, "outlier", float(rng.uniform(.92, 1.035)))
    return {"forest_edge_compositions": len(centres), "dry_bank_compositions": len(bank_centres),
            "outlier_count": len(geo.placements) - count_before_accents}


def main_edges(layout, plan):
    source_main = set()
    routes = {route["id"]: route for route in plan["routes"]}
    corridor_routes = {route for corridor in plan["corridors"] for route in corridor["routes"]}
    for route_id in corridor_routes:
        for segment in routes[route_id].get("movementSegments", []):
            source_main.add(segment["edge"])
    selected = set()
    for key, edge in layout["displayGraph"]["edges"].items():
        if source_main.intersection(edge.get("sourceEdges", [])):
            selected.add(key)
    return selected, len(corridor_routes)


def build_roads(geo, plan, main_keys):
    settlements = {node["id"] for node in plan["nodes"] if node.get("type") in ("city", "town")}
    candidates = []
    for key in sorted(main_keys):
        endpoints = key.split("~")
        towns = [key for key in endpoints if key in settlements]
        if not towns:
            continue
        point_array = resample(geo.layout["displayEdges"][key], 7)
        for town in towns:
            anchor = geo.layout["locations"][town]["drawPos"]
            points = point_array if distance(anchor, point_array[0]) < distance(anchor, point_array[-1]) else point_array[::-1]
            along = np.r_[0, np.cumsum(np.linalg.norm(np.diff(points, axis=0), axis=1))]
            flags = geo.sample_many(geo.dry, points) & (geo.sample_many(geo.brightness, points) > 166)
            for start, end in runs(flags):
                # Keep a clean entrance and at most one short local approach.
                if along[start] > 120:
                    continue
                first = max(start, int(np.searchsorted(along, 36)))
                last = min(end, int(np.searchsorted(along, min(along[first] + 560, along[-1])) + 1))
                if last - first < 3 or along[last - 1] - along[first] < 150:
                    continue
                segment = points[first:last]
                candidates.append((float(along[last - 1] - along[first]), town, key, segment))
                break
    roads, taken_towns, centres = [], set(), []
    for length, town, key, points in sorted(candidates, key=lambda c: (-c[0], c[1], c[2])):
        if len(roads) >= 14:
            break
        centre = points[len(points) // 2]
        if town in taken_towns or any(distance(centre, other) < 330 for other in centres):
            continue
        taken_towns.add(town)
        centres.append(centre)
        forest_score = float(geo.sample_many(geo.forest_density, points).mean())
        roads.append({"id": f"bc_scenery_road_{len(roads) + 1:02d}", "source_edge": key,
                      "near_place": town, "asset": "road_woodland" if forest_score > .16 else "road_dirt",
                      "points": [[round(float(v), 3) for v in p] for p in points],
                      "width": 7.5, "opacity": .48, "length": round(length, 3), "min_zoom": .28})
    return roads


def build_bridges(geo, main_keys):
    candidates = []
    for key in sorted(main_keys):
        points = resample(geo.layout["displayEdges"][key], 5)
        for start, end in runs(geo.sample_many(geo.water, points)):
            if start < 4 or end + 3 >= len(points):
                continue
            a, b = points[start - 3], points[end + 2]
            span = distance(a, b)
            if not 55 <= span <= 155:
                continue
            if not geo.sample(geo.dry, a) or not geo.sample(geo.dry, b):
                continue
            across = np.linspace(a, b, 35)
            blue = geo.sample_many(geo.water, across)
            if not blue[17] or blue.mean() < .53 or blue.mean() > .89:
                continue
            if not np.all(geo.sample_many(geo.walkable, across)):
                continue
            if np.any(geo.sample_many(geo.mountain, across)):
                continue
            line = b - a
            offsets = points[start:end] - a
            deviation = np.abs(line[0] * offsets[:, 1] - line[1] * offsets[:, 0]) / span
            if float(deviation.max()) > 11:
                continue
            centre = (a + b) / 2
            if not geo.rect_avoids_places((centre[0] - 90, centre[1] - 65, centre[0] + 90, centre[1] + 35)):
                continue
            score = abs(span - 88) + float(deviation.mean())
            candidates.append((score, key, a, b, float(blue.mean())))
    bridges, centres = [], []
    for _, key, a, b, blue_ratio in sorted(candidates, key=lambda c: (c[0], c[1])):
        centre = (a + b) / 2
        if any(distance(centre, other) < 1050 for other in centres):
            continue
        centres.append(centre)
        length = distance(a, b)
        bridges.append({"id": f"bc_scenery_bridge_{len(bridges) + 1:02d}",
                        "asset": "bridge_stone" if len(bridges) % 2 == 0 else "bridge_wood",
                        "position": [round(float(v), 3) for v in centre],
                        "rotation": round(float(math.atan2(b[1] - a[1], b[0] - a[0])), 6),
                        "width": round(length + 6, 3), "height": round(min(30, max(21, length * .25)), 3),
                        "pivot": [.5, .5], "opacity": .90, "source_edge": key,
                        "bank_a": [round(float(v), 3) for v in a], "bank_b": [round(float(v), 3) for v in b],
                        "audit": {"both_banks_dry_and_walkable": True, "all_span_samples_nav_walkable": True,
                                  "visual_water_fraction": round(blue_ratio, 3)}})
        if len(bridges) >= 5:
            break
    return bridges


def build_footings(geo, plan):
    types = {node["id"]: node.get("type") for node in plan["nodes"]}
    result = []
    for place, row in geo.layout["locations"].items():
        if types.get(place) not in ("city", "town", "sect", "market", "village", "dock"):
            continue
        x0, y0, x1, y1 = row["rect"]
        samples = [[x, y] for x in np.linspace(x0, x1, 5) for y in [y1 - 5, y1, y1 + 3]]
        if not np.all(geo.sample_many(geo.dry, samples)):
            continue
        result.append({"place_id": place, "position": [round((x0 + x1) * .5, 3), round(y1 - 1, 3)],
                       "width": round(min(88, (x1 - x0) * .82), 3), "height": 6.5, "opacity": .055})
    return result


def review(geo, data, review_dir):
    review_dir.mkdir(parents=True, exist_ok=True)
    image = geo.image.convert("RGBA")
    draw = ImageDraw.Draw(image)
    for road in data["roads"]:
        draw.line([(x * geo.ppw, y * geo.ppw) for x, y in road["points"]], fill=(135, 100, 58, 220), width=2)
    # This is a layout audit overlay, not placeholder scenery used by the game.
    colours = {"reeds": "#bd8c38", "rock_group": "#594d6a", "shrub": "#789a56"}
    for item in data["placements"]:
        x, y = np.asarray(item["position"]) * geo.ppw
        radius = 3 if item["asset"].endswith("group") else 1.5
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=colours.get(item["asset"], "#175d2c"))
    for bridge in data["bridges"]:
        a = np.asarray(bridge["bank_a"]) * geo.ppw
        b = np.asarray(bridge["bank_b"]) * geo.ppw
        draw.line([tuple(a), tuple(b)], fill="#bc4d20", width=4)
    image.save(review_dir / "layout_audit_overlay_1024.png")
    (review_dir / "scenery_layout_audit.json").write_text(json.dumps(data["audit"], indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def build(review_dir, preview_path=None):
    cfg = config()
    if preview_path is not None:
        cfg["preview"] = preview_path.resolve()
    layout_bytes, nav_bytes = cfg["layout"].read_bytes(), cfg["nav"].read_bytes()
    layout, nav = json.loads(layout_bytes), json.loads(nav_bytes)
    plan = read_json(ROOT / "docs/prototypes/百川宗门城镇资源规划数据_20261027.json")
    catalog = read_json(CATALOG).get("sprites", {}) if CATALOG.exists() else {}
    assert cfg["size"] == 7372.8 and cfg["km"] == 8000.0 and len(layout["locations"]) == 123
    geo = Geography(cfg, layout, nav, catalog)
    rhythm = build_vegetation(geo, np.random.default_rng(SEED))
    main_keys, main_route_count = main_edges(layout, plan)
    roads = build_roads(geo, plan, main_keys)
    bridges = build_bridges(geo, main_keys)
    footings = build_footings(geo, plan)
    # Recheck serialized positions after rounding, not only the candidate floats.
    for item in geo.placements:
        p = item["position"]
        rect = geo.sprite_rect(p, item["asset"], item["height"])
        samples = [[x, y] for x in np.linspace(rect[0], rect[2], 5) for y in np.linspace(rect[1], rect[3], 5)]
        if not np.all(geo.sample_many(geo.dry, samples)):
            raise ValueError(f"Scenery rounding changed terrain acceptance: {item['id']}")
    sprite_rects = np.asarray([geo.sprite_rect(p["position"], p["asset"], p["height"]) for p in geo.placements])
    actual_bounds = [float(sprite_rects[:, 0].min()), float(sprite_rects[:, 1].min()),
                     float(sprite_rects[:, 2].max()), float(sprite_rects[:, 3].max())]
    audit = {"visual_only": True, "navigation_and_graph_mutations": 0, "interactive_nodes": 0,
             "place_count_unchanged": len(layout["locations"]), "existing_display_edge_count_unchanged": len(layout["displayEdges"]),
             "terrain_sampling_size": [PREVIEW_SIZE, PREVIEW_SIZE],
             "natural_land_validation": "existing nav 0 AND original preview dry ground; full 5x5 sprite rectangle sampled",
             "dry_land_placement_violations": 0, "building_paper_entrance_violations": 0,
             "centreline_clearance_world_units": 32, "centreline_placement_violations": 0,
             "vegetation_count": len(geo.placements), "by_asset": dict(Counter(p["asset"] for p in geo.placements)),
             "placement_visual_bounds": [round(v, 3) for v in actual_bounds], "bounds_violations": 0,
             "rhythm": rhythm, "rejected_candidate_counts": dict(geo.rejected),
             "main_corridor_source_route_count": main_route_count, "selected_road_count": len(roads),
             "selected_road_length": round(sum(r["length"] for r in roads), 3),
             "bridge_count": len(bridges), "dry_building_footings": len(footings),
             "source_layout_sha256": hashlib.sha256(layout_bytes).hexdigest(),
             "source_navigation_sha256": hashlib.sha256(nav_bytes).hexdigest(),
             "source_preview_path": res_path(cfg["preview"]) if cfg["preview"].is_relative_to(ROOT) else str(cfg["preview"]),
             "source_preview_sha256": sha256(cfg["preview"])}
    data = {"version": 1, "seed": SEED, "visual_only": True, "bounds": [0, 0, cfg["size"], cfg["size"]],
            "world_km": cfg["km"], "catalog_path": res_path(CATALOG),
            "layout_path": res_path(cfg["layout"]), "navigation_path": res_path(cfg["nav"]),
            "placements": geo.placements, "roads": roads, "bridges": bridges, "footings": footings,
            "avoidance": {"building_paper_margin_x": 195, "building_paper_margin_y_above": 125,
                          "building_paper_margin_y_below": 92, "entrance_half_extent": [88, 85],
                          "route_centreline_clearance": 32, "max_sprite_compensation": 2.0,
                          "runtime_actual_paper_rect_check": True}, "audit": audit}
    OUTPUT.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if cfg["layout"].read_bytes() != layout_bytes or cfg["nav"].read_bytes() != nav_bytes:
        raise RuntimeError("Visual scenery authoring must not mutate navigation or place layout.")
    review(geo, data, review_dir)
    print(json.dumps(audit, ensure_ascii=True, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--review-dir", type=Path, default=REVIEW)
    parser.add_argument("--preview", type=Path, help="Use a replacement terrain preview without modifying navigation or locations.")
    args = parser.parse_args()
    build(args.review_dir, args.preview)
