"""Ground the immutable 429 original mountains; keep the nine overhead bridges.

This tool only writes the three named visual JSON files and its own report
directory. It never writes navigation, terrain, original scenery, or source art.
Contact dabs follow real native alpha columns and the visible group envelope.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import copy
import hashlib
import json
import math
from pathlib import Path
import time

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "resources/world/bai_chuan"
OUT = ROOT / "output/bai_chuan_original_grounding_20261109"
SCENE = RES / "landscape_original_grounded_20261109.json"
GROUND = RES / "mountain_ground_original_20261109.json"
PATCH = RES / "forest_grounding_sample_original_20261109.json"
FOREST_GROUND = RES / "forest_grounding_details_20261109.json"
SOURCE_SCENE = RES / "landscape_expanded3x_20261106.json"
BRIDGE_SCENE = RES / "landscape_compact_peaks_20261109.json"
SOURCE_PATCH = RES / "forest_grounding_sample_20261106.json"
SOURCE_NAV = RES / "free_navigation_expanded3x_20261106.json"
GROUND_CATALOG = ROOT / "textures/world/bai_chuan/forest_grounding_20261106/ground_catalog.json"
PROFILE = "original_mountains_grounded_20261109_v1"
WORLD = 22118.4
ALPHA = 46
SEED = 2026110921
RULES = {
    "mountain_soil": {"opacity": [.12, .21], "tint": [.80, .82, .70]},
    "root_shade": {"opacity": [.10, .16], "tint": [.64, .67, .50]},
    "mountain_scree": {"opacity": [.17, .29], "tint": [.76, .79, .73]},
    "mountain_meadow": {"opacity": [.10, .16], "tint": [.80, .86, .68]},
}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write(path, value):
    path = Path(path)
    assert path in (SCENE, GROUND, PATCH) or OUT in path.parents, path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+"\n", encoding="utf-8", newline="\n")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def rel(path):
    return Path(path).relative_to(ROOT).as_posix()


def resource(path):
    return "res://"+rel(path)


def resolve(path):
    return ROOT / str(path).removeprefix("res://")


def rows_array(rows):
    return np.frombuffer("".join(rows).encode("ascii"), dtype=np.uint8).reshape(len(rows), -1)-ord("0")


def integral(mask):
    return np.pad(mask.astype(np.int32).cumsum(0, dtype=np.int32).cumsum(1, dtype=np.int32), ((1, 0), (1, 0)))


def rect_sum(table, bounds, cell):
    x, y, w, h = bounds
    xa, ya = max(0, math.floor(x/cell)), max(0, math.floor(y/cell))
    xb, yb = min(table.shape[1]-1, math.ceil((x+w)/cell)), min(table.shape[0]-1, math.ceil((y+h)/cell))
    return int(table[yb, xb]-table[ya, xb]-table[yb, xa]+table[ya, xa])


def intersects(a, b):
    return a[0] < b[0]+b[2] and b[0] < a[0]+a[2] and a[1] < b[1]+b[3] and b[1] < a[1]+a[3]


def expand(bounds, margin):
    return [bounds[0]-margin, bounds[1]-margin, bounds[2]+2*margin, bounds[3]+2*margin]


class Inputs:
    def __init__(self):
        self.scene = read(SOURCE_SCENE)
        self.bridge_scene = read(BRIDGE_SCENE)
        self.patch = read(SOURCE_PATCH)
        self.forest = read(FOREST_GROUND)
        self.catalog = read(GROUND_CATALOG)
        self.nav = read(SOURCE_NAV)
        self.water = read(RES / "expanded_water_20261106.json")
        self.layout = read(RES / "free_layout_expanded3x_20261106.json")
        assert len(self.scene["mountains"]) == 429 and len(self.scene["trees"]) == 5509
        assert len(self.forest["decorations"]) == 344
        self.cell = float(self.nav["cell_size"])
        self.wet_table = integral((rows_array(self.nav["rows"]) == 2) | (rows_array(self.water["water_rows"]) == 2))
        self.bridge_open_table = integral(rows_array(self.water["bridge_open_rows"]) == 1)
        sdf = np.asarray(Image.open(resolve(self.water["water_sdf"])).convert("L"))
        self.display_water_table = integral(sdf < 128)
        self.sdf_cell = WORLD/sdf.shape[1]
        self.buildings = []
        for row in self.layout["locations"].values():
            x, y, x1, y1 = row["rect"]
            self.buildings.append(expand([x, y, x1-x, y1-y], 45.))
        self.bridge_boxes = [expand(row["bounds"], 30.) for row in self.bridge_scene["bridges"]]
        self.source18 = self.patch["source_sha256"]
        assert len(self.source18) == 18
        for path, digest in self.source18.items():
            assert sha(ROOT/path) == digest, f"Immutable source SHA mismatch: {path}"
        extras = [SOURCE_PATCH, BRIDGE_SCENE, FOREST_GROUND, GROUND_CATALOG,
                  resolve(self.catalog["atlas"]), RES / "forest_grounding_sample_20261109.json",
                  RES / "free_navigation_compact_peaks_20261109.json", RES / "terrain_travel_compact_peaks_20261109.json"]
        self.before = {**self.source18, **{rel(path): sha(path) for path in extras}}
        banks = {key: np.asarray(Image.open(resolve(asset["path"])).convert("RGBA"))[:, :, 3]
                 for key, asset in self.scene["mountain_sources"].items()}
        self.images = {}
        for key, asset in self.scene["mountain_catalog"].items():
            x, y, w, h = asset["region"]
            self.images[key] = banks[asset["bank"]][y:y+h, x:x+w]
        self.contours = {}
        self.front = {}
        self.siblings = defaultdict(list)
        for mountain in self.scene["mountains"]:
            alpha = self.images[mountain["asset"]]
            if mountain.get("flip", False):
                alpha = alpha[:, ::-1]
            visible = alpha >= ALPHA
            cols = np.flatnonzero(visible.any(0))
            bottom = np.full(alpha.shape[1], -1, dtype=np.int32)
            bottom[cols] = np.array([np.flatnonzero(visible[:, x])[-1] for x in cols])
            x, y, w, h = mountain["bounds"]
            anchors = np.column_stack((x+(cols+.5)*w/alpha.shape[1], y+(bottom[cols]+.5)*h/alpha.shape[0]))
            self.contours[mountain["id"]] = {"alpha": alpha, "columns": cols, "bottoms": bottom,
                                             "anchors": anchors, "native_pixels": int(visible.sum())}
            self.siblings[mountain["source_region"]].append(mountain)
        for mountain in self.scene["mountains"]:
            self.front[mountain["id"]] = [other for other in self.scene["mountains"]
                if other["z"] > mountain["z"] and intersects(mountain["bounds"], other["bounds"])]

    def alpha_at(self, mountain, point):
        x, y, w, h = mountain["bounds"]
        alpha = self.contours[mountain["id"]]["alpha"]
        px, py = math.floor((point[0]-x)/w*alpha.shape[1]), math.floor((point[1]-y)/h*alpha.shape[0])
        return int(alpha[py, px]) if 0 <= px < alpha.shape[1] and 0 <= py < alpha.shape[0] else 0

    def hidden_reason(self, mountain, anchor):
        for other in self.front[mountain["id"]]:
            if self.alpha_at(other, anchor) >= ALPHA:
                return "front_native_alpha"
        # At overlapping native groups, only the lowest actual silhouette gets
        # contact dabs. This avoids separate rims under every old repeated piece.
        for other in self.siblings[mountain["source_region"]]:
            if other["id"] == mountain["id"] or not intersects(mountain["bounds"], other["bounds"]):
                continue
            x, y, w, h = other["bounds"]
            contour = self.contours[other["id"]]
            column = math.floor((anchor[0]-x)/w*contour["alpha"].shape[1])
            if 0 <= column < len(contour["bottoms"]) and contour["bottoms"][column] >= 0:
                bottom = y+(contour["bottoms"][column]+.5)*h/contour["alpha"].shape[0]
                if bottom > anchor[1]+4.:
                    return "above_group_lower_envelope"
        return None

    def valid_bounds(self, bounds):
        x, y, w, h = bounds
        return (x >= 0. and y >= 0. and x+w <= WORLD and y+h <= WORLD
            and not rect_sum(self.wet_table, bounds, self.cell)
            and not rect_sum(self.bridge_open_table, bounds, self.cell)
            and not rect_sum(self.display_water_table, bounds, self.sdf_cell)
            and not any(intersects(bounds, other) for other in self.buildings+self.bridge_boxes))


def generate(inputs, strength=1.):
    rng = np.random.default_rng(SEED)
    result, reports, rejected = [], [], Counter()
    key_cycle = ["mountain_soil", "mountain_scree", "mountain_soil", "root_shade", "mountain_meadow",
                 "mountain_scree", "mountain_soil", "root_shade", "mountain_scree", "mountain_soil"]
    for number, mountain in enumerate(inputs.scene["mountains"]):
        contour = inputs.contours[mountain["id"]]
        columns, anchors = contour["columns"], contour["anchors"]
        width = mountain["bounds"][2]
        count = max(3, min(10, round(width/90.)+2))
        # Slightly irregular samples stay away from the source's extreme tips.
        fractions = np.linspace(.07, .93, count)+rng.uniform(-.018, .018, count)
        accepted, hidden, invalid = 0, Counter(), 0
        for slot, fraction in enumerate(fractions):
            at = int(np.clip(round(fraction*(len(columns)-1)), 0, len(columns)-1))
            anchor = anchors[at]
            reason = inputs.hidden_reason(mountain, anchor)
            if reason:
                hidden[reason] += 1
                rejected[reason] += 1
                continue
            key = key_cycle[(slot+number % 3) % len(key_cycle)]
            # Low strips need a contact seam, rather than a large soil pad.
            if width < 180. and key == "mountain_meadow":
                key = "root_shade"
            asset = inputs.catalog["assets"][key]
            rule = RULES[key]
            if key == "mountain_soil":
                ground_width = min(145., width*float(rng.uniform(.24, .37)))
            elif key == "root_shade":
                ground_width = min(110., width*float(rng.uniform(.18, .28)))
            elif key == "mountain_meadow":
                ground_width = min(105., width*float(rng.uniform(.18, .27)))
            else:
                ground_width = min(90., width*float(rng.uniform(.15, .24)))
            ground_width = max(22., ground_width)
            ground_height = ground_width*asset["native_size"][1]/asset["native_size"][0]
            offset = [float(rng.uniform(-4., 4.)), float(rng.uniform(-.20, -.02)*ground_height)]
            point = anchor+offset
            flip = bool(rng.integers(0, 2))
            pivot = asset.get("pivot", [.5, .5])[:]
            if flip:
                pivot[0] = 1.-pivot[0]
            bounds = [point[0]-ground_width*pivot[0], point[1]-ground_height*pivot[1], ground_width, ground_height]
            if not inputs.valid_bounds(bounds):
                invalid += 1
                rejected["full_bbox_water_building_bridge_or_world"] += 1
                continue
            row = {"id": f"original_mountain_decal_{len(result):05d}", "asset": key,
                   "x": round(float(point[0]), 5), "y": round(float(point[1]), 5), "z": 24,
                   "position": np.round(point, 5).tolist(), "bounds": np.round(bounds, 5).tolist(),
                   "world_width": round(ground_width, 5), "world_height": round(ground_height, 5),
                   "flip": flip, "opacity": round(float(rng.uniform(*rule["opacity"]))*strength, 5),
                   "tint": rule["tint"], "catalog_opacity_scale": 1., "layer": "ground_decals",
                   "draw_order": 0 if key in ("mountain_soil", "mountain_meadow") else 2 if key == "root_shade" else 1,
                   "habitat": "mountain_apron", "style_profile": "original_native_visible_lower_envelope_20261109",
                   "mountain_id": mountain["id"], "source_region": mountain["source_region"],
                   "native_contour_column": int(columns[at]), "native_contour_bottom_y": int(contour["bottoms"][columns[at]]),
                   "contour_anchor": np.round(anchor, 5).tolist(), "contour_offset": np.round(offset, 5).tolist(),
                   "visible_lower_envelope": True, "native_alpha_threshold": ALPHA}
            result.append(row)
            accepted += 1
        reports.append({"mountain_id": mountain["id"], "source_region": mountain["source_region"],
                        "sampled_candidates": count, "accepted_ground_details": accepted,
                        "hidden_candidates": dict(hidden), "bbox_exclusions": invalid,
                        "native_visible_pixels": contour["native_pixels"]})
    result.sort(key=lambda row: (row["draw_order"], row["asset"], row["id"]))
    ground = {"version": 1, "profile": PROFILE, "world_size": WORLD, "grid_size": 640.,
              "catalog_path": resource(GROUND_CATALOG), "decorations": result, "seed": SEED,
              "coordinate_rule": "XYZ24 visual contact details; original 429 mountains and navigation unchanged",
              "style_rules": RULES, "strength": strength,
              "lower_edge_rule": "native alpha>=46 column bottom, exclude higher-Z native alpha coverage and overlapping sibling lower envelopes"}
    return ground, reports, dict(rejected)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--strength", type=float, default=1.)
    args = parser.parse_args()
    assert 0. <= args.strength <= 2.
    began = time.perf_counter()
    inputs = Inputs()
    ground, mountains_report, rejected = generate(inputs, args.strength)
    scene = copy.deepcopy(inputs.scene)
    for field in ("bridge_atlas", "bridge_catalog", "bridges"):
        scene[field] = copy.deepcopy(inputs.bridge_scene[field])
    scene.update(profile=PROFILE, all_mountains_grounded=True,
                 mountain_ground_details_path=resource(GROUND), forest_grounding_patch_path=resource(PATCH),
                 meta={"all_mountains_grounded": True, "profile": PROFILE, "original_mountains_restored": True,
                       "original_scene_sha256": sha(SOURCE_SCENE), "original_navigation_path": resource(SOURCE_NAV),
                       "original_navigation_sha256": sha(SOURCE_NAV), "navigation_written": False,
                       "mountain_geometry_and_source_art_unchanged": True},
                 geometry_summary={"mountains": 429, "original_mountain_rows_preserved": 429,
                     "trees_immutable_base": 5509, "active_trees_after_unchanged_forest_patch": 5490,
                     "bridges": 9, "mountain_ground_details": len(ground["decorations"]),
                     "mountains_with_ground_details": sum(row["accepted_ground_details"] > 0 for row in mountains_report),
                     "navigation_changed": False, "terrain_changed": False})
    patch = copy.deepcopy(inputs.patch)
    patch["ground_details_path"] = resource(FOREST_GROUND)
    write(SCENE, scene)
    write(GROUND, ground)
    write(PATCH, patch)
    # Reparse saved outputs and independently re-evaluate the actual anchors.
    scene_saved, ground_saved, patch_saved = map(read, (SCENE, GROUND, PATCH))
    failures = []
    def check(condition, label):
        if not condition:
            failures.append(label)
    for field in ("mountains", "mountain_catalog", "mountain_sources", "trees", "tree_catalog", "tree_atlas", "max_prop_height"):
        check(scene_saved[field] == inputs.scene[field], "Original exact scene field: "+field)
    for field in ("bridge_atlas", "bridge_catalog", "bridges"):
        check(scene_saved[field] == inputs.bridge_scene[field], "Current overhead bridges preserved: "+field)
    expected_patch = copy.deepcopy(inputs.patch)
    expected_patch["ground_details_path"] = resource(FOREST_GROUND)
    check(patch_saved == expected_patch, "Original forest patch exact except ground path")
    byid = {mountain["id"]: mountain for mountain in inputs.scene["mountains"]}
    for row in ground_saved["decorations"]:
        mountain = byid[row["mountain_id"]]
        c = inputs.contours[mountain["id"]]
        column = row["native_contour_column"]
        bottom = c["bottoms"][column]
        x, y, w, h = mountain["bounds"]
        anchor = [x+(column+.5)*w/c["alpha"].shape[1], y+(bottom+.5)*h/c["alpha"].shape[0]]
        check(bottom == row["native_contour_bottom_y"] and np.max(np.abs(np.subtract(anchor, row["contour_anchor"]))) <= 1e-5,
              "Actual native lower edge: "+row["id"])
        check(inputs.hidden_reason(mountain, anchor) is None, "Visible group lower envelope: "+row["id"])
        check(inputs.valid_bounds(row["bounds"]), "Contact bbox safe: "+row["id"])
    for path, digest in inputs.before.items():
        check(sha(ROOT/path) == digest, "Immutable source hash: "+path)
    checks = {"all_passed": not failures, "failures": failures, "original_mountain_rows_exact": 429,
              "original_catalog_and_sources_exact": True, "original_trees_exact": 5509,
              "active_forest_trees_unchanged": 5490, "forest_details_unchanged": 344,
              "original_four_grounded_mountain_ids": patch_saved["grounded_mountain_ids"],
              "overhead_bridges_preserved_exact": 9, "navigation_files_written": False,
              "terrain_files_written": False, "source18_sha256_unchanged": True,
              "verified_native_lower_edge_details": len(ground_saved["decorations"])}
    report = {"profile": PROFILE, "elapsed_seconds": time.perf_counter()-began, "verification": checks,
              "source_sha256": inputs.before, "output_sha256": {rel(path): sha(path) for path in (SCENE, GROUND, PATCH)},
              "ground_asset_usage": dict(Counter(row["asset"] for row in ground["decorations"])),
              "mountains_with_ground_details": sum(row["accepted_ground_details"] > 0 for row in mountains_report),
              "mountains_fully_hidden_or_excluded": sum(row["accepted_ground_details"] == 0 for row in mountains_report),
              "candidate_exclusions": rejected, "per_mountain": mountains_report,
              "runtime_source_geometry": "expanded3x_20261106_v1", "strength": args.strength,
              "hidden_anchor_rule": "front mountain z>owner.z and native alpha46 at anchor; or overlapping same-source-region silhouette bottom at same X exceeds anchorY by >4world",
              "world_size": WORLD, "world_km": 8000, "closest_zoom": .8, "avatar_height": 52., "building_scale": .85}
    write(OUT / "generation_manifest_20261109.json", report)
    print(json.dumps({key: value for key, value in report.items() if key not in ("source_sha256", "output_sha256", "per_mountain")}, ensure_ascii=True), flush=True)
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
