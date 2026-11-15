"""Build the compact peak profile from immutable expanded-map geometry.

This is a data/raster-footprint tool. Generated mountain and bridge art is never
repainted or resized on disk. All new blocker cells must belong to the original
mountain mask; every original walkable cell and water/crossing cell is retained.
Only the explicitly named 20261109 outputs below are written.
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

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage as ndi


ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "resources/world/bai_chuan"
ART = ROOT / "textures/world/bai_chuan/compact_peaks_20261109"
OUT = ROOT / "output/bai_chuan_compact_peaks_20261109"
PEAKS = ART / "peaks_catalog.json"
BRIDGES = ART / "bridge_catalog.json"
SCENE = RES / "landscape_compact_peaks_20261109.json"
NAV = RES / "free_navigation_compact_peaks_20261109.json"
TERRAIN = RES / "terrain_travel_compact_peaks_20261109.json"
PATCH = RES / "forest_grounding_sample_20261109.json"
FOREST_GROUND = RES / "forest_grounding_details_20261109.json"
MOUNTAIN_GROUND = RES / "mountain_ground_details_20261109.json"
GROUND_CATALOG = ROOT / "textures/world/bai_chuan/forest_grounding_20261106/ground_catalog.json"
PROFILE = "compact_peaks_20261109_v1"
WORLD = 22118.4
NEAR = .8
ALPHA = 46
SEED = 2026110907
PHASE = .25  # Fixed subcell phase permits exact cached search masks.
GROUND_RULES = {
    "mountain_soil": {"opacity": [.12, .24], "tint": [.80, .82, .70]},
    "mountain_scree": {"opacity": [.20, .38], "tint": [.82, .85, .78]},
    "root_shade": {"opacity": [.10, .18], "tint": [.64, .67, .50]},
}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write(path, value):
    path = Path(path)
    # The old snapshots remain immutable even if this tool is edited later.
    assert "20261109" in path.name or OUT in path.parents, path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def rel(path):
    return Path(path).relative_to(ROOT).as_posix()


def resource(path):
    return "res://" + rel(path)


def resolve(path):
    return ROOT / str(path).removeprefix("res://")


def rows_array(rows):
    return np.frombuffer("".join(rows).encode("ascii"), dtype=np.uint8).reshape(len(rows), -1) - ord("0")


def encode_rows(grid):
    return [bytes(row + ord("0")).decode("ascii") for row in np.asarray(grid, dtype=np.uint8)]


def integral(mask):
    return np.pad(mask.astype(np.int32).cumsum(0, dtype=np.int32).cumsum(1, dtype=np.int32), ((1, 0), (1, 0)))


def cells(bounds, cell, shape):
    x, y, w, h = bounds
    return (max(0, math.floor(x/cell)), max(0, math.floor(y/cell)),
            min(shape[1], math.ceil((x+w)/cell)), min(shape[0], math.ceil((y+h)/cell)))


def rect_sum(table, bounds, cell):
    xa, ya, xb, yb = cells(bounds, cell, (table.shape[0]-1, table.shape[1]-1))
    return int(table[yb, xb] - table[ya, xb] - table[yb, xa] + table[ya, xa])


def intersects(a, b):
    return a[0] < b[0]+b[2] and b[0] < a[0]+a[2] and a[1] < b[1]+b[3] and b[1] < a[1]+a[3]


def expand(bounds, margin):
    return [bounds[0]-margin, bounds[1]-margin, bounds[2]+2*margin, bounds[3]+2*margin]


def rect_distance(a, b):
    return math.hypot(max(a[0]-b[0]-b[2], b[0]-a[0]-a[2], 0.),
                      max(a[1]-b[1]-b[3], b[1]-a[1]-a[3], 0.))


def contained(outer, inner):
    return outer[0] <= inner[0] and outer[1] <= inner[1] and inner[0]+inner[2] <= outer[0]+outer[2] and inner[1]+inner[3] <= outer[1]+outer[3]


def clear_segment(grid, cell, a, b):
    """Conservative segment supercover, including the runtime corner rule."""
    nrows, ncols = grid.shape
    def free(x, y):
        return 0 <= x < ncols and 0 <= y < nrows and grid[y, x] == 0
    ax, ay = a[0]/cell, a[1]/cell
    bx, by = b[0]/cell, b[1]/cell
    x, y, ex, ey = math.floor(ax), math.floor(ay), math.floor(bx), math.floor(by)
    if not free(x, y) or not free(ex, ey):
        return False
    dx, dy = bx-ax, by-ay
    sx, sy = (1 if dx > 0 else -1), (1 if dy > 0 else -1)
    tx = ((x+1-ax) if sx > 0 else (ax-x))/abs(dx) if dx else math.inf
    ty = ((y+1-ay) if sy > 0 else (ay-y))/abs(dy) if dy else math.inf
    dtx, dty = (1/abs(dx) if dx else math.inf), (1/abs(dy) if dy else math.inf)
    for _ in range(nrows+ncols+2):
        if (x, y) == (ex, ey):
            return True
        if x == ex:
            tx = math.inf
        if y == ey:
            ty = math.inf
        if abs(tx-ty) < 1e-7:
            if not free(x+sx, y) or not free(x, y+sy):
                return False
            x, y, tx, ty = x+sx, y+sy, tx+dtx, ty+dty
        elif tx < ty:
            x, tx = x+sx, tx+dtx
        else:
            y, ty = y+sy, ty+dty
        if not free(x, y):
            return False
    return False


class Inputs:
    def __init__(self):
        self.scene = read(RES / "landscape_expanded3x_20261106.json")
        self.nav_data = read(RES / "free_navigation_expanded3x_20261106.json")
        self.nav = rows_array(self.nav_data["rows"])
        self.layout = read(RES / "free_layout_expanded3x_20261106.json")
        self.water = read(RES / "expanded_water_20261106.json")
        self.terrain = read(RES / "terrain_travel_expanded3x_20261106.json")
        self.patch = read(RES / "forest_grounding_sample_20261106.json")
        self.forest_ground = read(RES / "forest_grounding_details_20261106.json")
        self.cell = float(self.nav_data["cell_size"])
        self.n = int(self.nav_data["columns"])
        assert self.nav.shape == (1413, 1413)
        assert self.scene["world_size"] == self.layout["worldSize"] == self.nav_data["world_size"] == WORLD
        self.old_mountain = self.nav == 1
        self.old_water = self.nav == 2
        self.wet = rows_array(self.water["water_rows"]) == 2
        self.opened = rows_array(self.water["bridge_open_rows"]) == 1
        self.sdf = np.asarray(Image.open(resolve(self.water["water_sdf"])).convert("L"))
        self.display_water_table = integral(self.sdf < 128)
        self.source_cell = WORLD / self.sdf.shape[1]
        self.water_table = integral(self.wet | self.old_water)
        self.open_table = integral(self.opened)
        self.buildings = []
        for row in self.layout["locations"].values():
            x, y, x1, y1 = row["rect"]
            self.buildings.append(expand([x, y, x1-x, y1-y], 45.))
        self.protected18 = self.patch["source_sha256"]
        assert len(self.protected18) == 18
        for path, digest in self.protected18.items():
            assert sha(ROOT/path) == digest, f"Original source SHA mismatch: {path}"
        extra = [RES / "forest_grounding_sample_20261106.json", RES / "forest_grounding_details_20261106.json", GROUND_CATALOG]
        groundcat = read(GROUND_CATALOG)
        extra += [resolve(groundcat["atlas"]), resolve(self.patch["tree_catalog"])]
        self.before = {**self.protected18, **{rel(path): sha(path) for path in extra}}
        self.groups = defaultdict(list)
        for mountain in self.scene["mountains"]:
            self.groups[mountain["source_region"]].append(mountain)

    def raster(self, bounds, alpha, padding=True):
        """Project every actual native pixel's four corners without averaging."""
        x, y, w, h = bounds
        xa, ya = math.floor(x/self.cell)-1, math.floor(y/self.cell)-1
        xb, yb = math.ceil((x+w)/self.cell)+1, math.ceil((y+h)/self.cell)+1
        occupied = np.zeros((yb-ya, xb-xa), dtype=bool)
        sy, sx = np.nonzero(alpha >= ALPHA)
        ah, aw = alpha.shape
        for ex, ey in ((0., 0.), (1.-1e-7, 0.), (0., 1.-1e-7), (1.-1e-7, 1.-1e-7)):
            gx = np.floor((x+(sx+ex)*w/aw)/self.cell).astype(np.int32)-xa
            gy = np.floor((y+(sy+ey)*h/ah)/self.cell).astype(np.int32)-ya
            occupied[gy, gx] = True
        if padding:
            occupied = ndi.binary_dilation(occupied, iterations=1)
        return xa, ya, xb, yb, occupied

    def valid_ground(self, bounds, bridge_boxes):
        return (contained([0., 0., WORLD, WORLD], bounds)
                and not rect_sum(self.water_table, bounds, self.cell)
                and not rect_sum(self.open_table, bounds, self.cell)
                and not rect_sum(self.display_water_table, bounds, self.source_cell)
                and not any(intersects(bounds, box) for box in self.buildings + bridge_boxes))


def load_peaks(path):
    data = read(path)
    sources = data.get("mountain_sources", data.get("sources", {}))
    catalog = data.get("mountain_catalog", data.get("assets", {}))
    if not sources and data.get("atlas"):
        sources = {"compact": {"path": data["atlas"], "size": data.get("atlas_size")}}
    assert sources and len(catalog) == 6, "Expected six native compact assets"
    banks = {}
    for bank, entry in sources.items():
        banks[bank] = np.asarray(Image.open(resolve(entry["path"])).convert("RGBA"))[:, :, 3]
        if entry.get("sha256"):
            assert sha(resolve(entry["path"])) == entry["sha256"]
    images = {}
    for key, asset in catalog.items():
        asset.setdefault("bank", next(iter(sources)))
        x, y, w, h = asset["region"]
        images[key] = banks[asset["bank"]][y:y+h, x:x+w]
        assert images[key].shape == (h, w)
        asset.setdefault("native_size", [w, h])
        asset.setdefault("pivot", [.5, .98])
    return data, sources, catalog, images


class Composer:
    def __init__(self, inputs, catalog, images):
        self.i = inputs
        self.catalog = catalog
        self.images = images
        self.cache = {}
        self.rng = np.random.default_rng(SEED)
        self.mountains = []
        self.mask = np.zeros_like(inputs.old_mountain)
        self.rejected = Counter()
        self.groups = []

    def is_hill(self, key):
        asset = self.catalog[key]
        return asset.get("kind", "") in ("foothill", "hill", "rounded_foothill") or "hill" in key

    def template(self, key, height, flip):
        cachekey = key, round(height, 6), flip
        if cachekey in self.cache:
            return self.cache[cachekey]
        alpha = self.images[key][:, ::-1] if flip else self.images[key]
        h, w = alpha.shape
        width = height*w/h
        bounds = [PHASE*self.i.cell, PHASE*self.i.cell, width, height]
        raster = self.i.raster(bounds, alpha)
        self.cache[cachekey] = width, alpha, raster
        return self.cache[cachekey]

    def find(self, parent, group_id, target, target_height, hill, used, role, allowed_keys=None):
        px, py, pw, ph = parent["bounds"]
        available = [key for key in self.catalog if self.is_hill(key) == hill]
        if allowed_keys is not None:
            available = [key for key in available if key in allowed_keys]
        if not available:
            available = list(self.catalog)
        self.rng.shuffle(available)
        # Natural source aspects and actual lower limits avoid miniature spikes.
        minimum = 70. if hill else 150.
        cap = min(target_height, ph*.93, 180. if hill else 360.)
        heights = sorted(set([round(cap - j*12., 4) for j in range(24) if cap-j*12. >= minimum] + ([minimum] if cap >= minimum else [])), reverse=True)
        for height in heights:
            options = []
            for key in available:
                asset = self.catalog[key]
                ah, aw = self.images[key].shape
                low, high = asset.get("height_range", [70., 180.] if hill else [150., 360.])
                if not low-1e-7 <= height <= high+1e-7:
                    continue
                width = height*aw/ah
                if not 120. <= width <= 300. or width*NEAR > aw*.98 or height*NEAR > ah*.98:
                    continue
                flip = bool(self.rng.integers(0, 2))
                width, alpha, (dx0, dy0, dx1, dy1, template) = self.template(key, height, flip)
                # Keep the new group inside one old parent; neighbouring old
                # horizontal copies are released rather than rebuilt as a row.
                gx0, gy0 = math.ceil(px/self.i.cell-PHASE), math.ceil(py/self.i.cell-PHASE)
                gx1 = math.floor((px+pw-width)/self.i.cell-PHASE)
                gy1 = math.floor((py+ph-height)/self.i.cell-PHASE)
                if gx0 > gx1 or gy0 > gy1:
                    continue
                xa, ya, xb, yb = gx0+dx0, gy0+dy0, gx1+dx1, gy1+dy1
                if xa < 0 or ya < 0 or xb > self.i.n or yb > self.i.n:
                    continue
                local = self.i.old_mountain[ya:yb, xa:xb].astype(np.float32)
                score = cv2.matchTemplate(local, template.astype(np.float32), cv2.TM_CCORR)
                yy, xx = np.nonzero(score >= float(template.sum())-.05)
                pivot = asset["pivot"][:]
                if flip:
                    pivot[0] = 1.-pivot[0]
                for sy, sx in zip(yy, xx):
                    x0, y0 = (gx0+int(sx)+PHASE)*self.i.cell, (gy0+int(sy)+PHASE)*self.i.cell
                    bounds = [x0, y0, width, height]
                    foot = [x0+width*pivot[0], y0+height*pivot[1]]
                    if used:
                        # Front hills sit lower, offset in X, and overlap only
                        # part of the taller back peak's lower silhouette.
                        main = used[0]
                        if foot[1] < main["y"]+42. or foot[1] > main["y"]+145.:
                            continue
                        if abs(foot[0]-main["x"]) < max(55., main["bounds"][2]*.22):
                            continue
                        left = min([bounds[0]] + [m["bounds"][0] for m in used])
                        right = max([bounds[0]+width] + [m["bounds"][0]+m["bounds"][2] for m in used])
                        if right-left > min(500., pw*.76):
                            continue
                        if any(math.dist(foot, m["position"]) < 100. for m in used):
                            continue
                    if any(m["source_region"] != group_id and rect_distance(bounds, m["bounds"]) < 78. for m in self.mountains):
                        continue
                    # Prefer the requested silhouette position, with a small
                    # width penalty so a broad source cannot recreate a ridge.
                    distance = math.dist(foot, target) + width*.035
                    options.append((distance, key, flip, bounds, foot, alpha))
            if not options:
                continue
            options.sort(key=lambda item: (item[0], item[1], item[2], item[3][0], item[3][1]))
            for _, key, flip, bounds, foot, alpha in options[:20]:
                xa, ya, xb, yb, occupied = self.i.raster(bounds, alpha)
                if np.any(occupied & ~self.i.old_mountain[ya:yb, xa:xb]):
                    self.rejected["exact_native_projection_outside_old1"] += 1
                    continue
                ident = parent["id"] if not used else f"compact_peak_{group_id.removeprefix('mountain_')}_{len(used):02d}"
                row = {"id": ident, "kind": "mountain", "asset": key, "flip": flip,
                       "x": foot[0], "y": foot[1], "z": foot[1], "position": foot,
                       "bounds": bounds, "layer": "ground_props", "source_region": group_id,
                       "original_parent_id": parent["id"], "compact_role": role,
                       "native_alpha_threshold": ALPHA}
                self.mask[ya:yb, xa:xb] |= occupied
                self.mountains.append(row)
                return row
        return None

    def run(self):
        for number, (group_id, parents) in enumerate(self.i.groups.items()):
            eligible = [m for m in parents if m["bounds"][2] >= 180. and m["bounds"][3] >= 82.]
            if not eligible:
                self.rejected["old_repeated_strip_below_useful_size"] += 1
                continue
            # Largest parent owns the compact group. Equal native repeats keep
            # the western first ID, including Xiyun's mountain_00318.
            parent = max(eligible, key=lambda m: (round(m["bounds"][3]/15.), -parents.index(m)))
            if group_id == "mountain_0247":
                parent = next(m for m in parents if m["id"] == "mountain_00318")
            px, py, pw, ph = parent["bounds"]
            target = [px+pw*.50, py+ph*.72]
            used = []
            hill = ph < 180.
            main = None
            if not hill:
                peak_keys = [key for key in self.catalog if not self.is_hill(key)]
                preferred = peak_keys[number % len(peak_keys)]
                if group_id == "mountain_0247":
                    preferred = peak_keys[0]
                main = self.find(parent, group_id, target, min(340., ph*.8), False, used, "back_peak", [preferred])
            if main is None:
                main = self.find(parent, group_id, target, min(340., ph*.8), hill, used, "rounded_hill" if hill else "back_peak")
            if main is None and not hill:
                main = self.find(parent, group_id, target, min(170., ph*.76), True, used, "rounded_hill")
            if main is None:
                self.rejected["no_quality_native_subset_fit"] += 1
                continue
            used.append(main)
            if main["bounds"][3] >= 155.:
                side = -1. if number % 2 else 1.
                offsets = [(side, 67.), (-side, 112.)]
                wanted = 2 if main["bounds"][3] >= 225. and pw >= 600. else 1
                for side, dy in offsets[:wanted]:
                    target = [main["x"]+side*main["bounds"][2]*.53, main["y"]+dy]
                    front = self.find(parent, group_id, target, min(155., main["bounds"][3]*.65), True, used, "front_foothill")
                    if front:
                        used.append(front)
            boxes = [m["bounds"] for m in used]
            left, top = min(b[0] for b in boxes), min(b[1] for b in boxes)
            right, bottom = max(b[0]+b[2] for b in boxes), max(b[1]+b[3] for b in boxes)
            old_left = min(m["bounds"][0] for m in parents)
            old_right = max(m["bounds"][0]+m["bounds"][2] for m in parents)
            self.groups.append({"source_region": group_id, "original_parent_id": parent["id"],
                                "original_ids": [m["id"] for m in parents], "new_ids": [m["id"] for m in used],
                                "bounds": [left, top, right-left, bottom-top],
                                "old_horizontal_span": old_right-old_left,
                                "new_horizontal_span": right-left,
                                "feet_y_span": max(m["y"] for m in used)-min(m["y"] for m in used)})
            if number % 40 == 0:
                print(f"GROUPS {number}/{len(self.i.groups)} peaks={len(self.mountains)}", flush=True)
        return self.mountains, self.mask


def fit_bridges(inputs, path):
    data = read(path)
    catalog = data.get("bridge_catalog", data.get("assets", {}))
    atlas_path = data.get("bridge_atlas", data.get("atlas"))
    assert catalog, "No strict overhead bridge asset"
    if not atlas_path:
        atlas_path = next(iter(catalog.values()))["atlas"]
    alpha = np.asarray(Image.open(resolve(atlas_path)).convert("RGBA"))[:, :, 3]
    result, proofs = [], []
    for old in inputs.scene["bridges"]:
        choices = []
        theta = old["rotation"]
        rotation = np.array([[math.cos(theta), -math.sin(theta)], [math.sin(theta), math.cos(theta)]])
        centers = np.array([[(x+.5)*inputs.cell, (y+.5)*inputs.cell] for y, x0, x1 in old["open_runs"] for x in range(x0, x1)])
        corners = (centers[:, None, :] + inputs.cell*np.array([[-.5, -.5], [-.5, .5], [.5, -.5], [.5, .5]])[None, :, :]).reshape(-1, 2)
        # Required old land footings keep both ends visibly seated on the bank.
        supports = np.array([p["position"] for p in old["end_footings"]])
        local = (np.concatenate([corners, supports])-old["position"]) @ rotation
        for key, asset in catalog.items():
            asset.setdefault("atlas", atlas_path)
            rx, ry, rw, rh = asset["region"]
            asset.setdefault("native_size", [rw, rh])
            if "native_pivot_px" not in asset:
                asset["native_pivot_px"] = [rx+rw*asset["pivot"][0], ry+rh*asset["pivot"][1]]
            if "deck_rect_atlas" not in asset:
                dx, dy, dw, dh = asset["deck_rect"]
                asset["deck_rect_atlas"] = [rx+dx, ry+dy, rx+dx+dw, ry+dy+dh]
            d = np.array(asset["deck_rect_atlas"], dtype=float)
            pivot = np.array(asset["native_pivot_px"], dtype=float)
            lower, upper = pivot-d[:2]-.75, d[2:]-pivot-.75
            assert np.all(lower > 0) and np.all(upper > 0), "Pivot must be inside opaque deck"
            scale = float(np.max(np.where(local >= 0, local/upper, -local/lower)))*1.025
            native = local[:len(corners)]/scale+pivot
            ix, iy = np.floor(native).astype(int).T
            minimum = int(alpha[iy, ix].min())
            deck_minimum = int(alpha[int(d[1]):int(d[3]), int(d[0]):int(d[2])].min())
            assert minimum >= 240 and deck_minimum >= 240, (key, old["id"], minimum, deck_minimum)
            assert scale*NEAR <= 1.+1e-9, "Bridge source must remain native at near zoom"
            choices.append((scale*rw, key, scale, minimum, deck_minimum))
        _, key, scale, minimum, deck_minimum = min(choices)
        asset = catalog[key]
        rx, ry, rw, rh = asset["region"]
        pivot = np.array(asset["native_pivot_px"])
        native_corners = np.array([[rx, ry], [rx+rw, ry], [rx, ry+rh], [rx+rw, ry+rh]])
        world_corners = (native_corners-pivot)*scale @ rotation.T + old["position"]
        lo, hi = world_corners.min(0), world_corners.max(0)
        row = copy.deepcopy(old)
        row.update(asset=key, world_width=rw*scale, world_height=rh*scale,
                   source_to_world_scale=scale, bounds=[*lo.tolist(), *(hi-lo).tolist()],
                   rect=[*lo.tolist(), *hi.tolist()],
                   deck_world_extent=[(asset["deck_rect_atlas"][2]-asset["deck_rect_atlas"][0])*scale,
                                      (asset["deck_rect_atlas"][3]-asset["deck_rect_atlas"][1])*scale],
                   perspective="strict_top_down", source_catalog=resource(path))
        result.append(row)
        proofs.append({"id": row["id"], "asset": key, "open_water_cells": len(centers),
                       "native_corner_alpha_min": minimum, "opaque_deck_alpha_min": deck_minimum,
                       "source_to_world_scale": scale, "world_size": [rw*scale, rh*scale],
                       "old_position_rotation_runs_and_contacts_preserved": all(row[f] == old[f] for f in ("id", "x", "y", "z", "position", "rotation", "open_runs", "bank_contacts", "end_footings", "connected_land_components"))})
    return atlas_path, catalog, result, proofs


def make_ground(inputs, mountains, images, bridges):
    catalog = read(GROUND_CATALOG)
    rng = np.random.default_rng(SEED+2)
    bridge_boxes = [expand(bridge["bounds"], 30.) for bridge in bridges]
    decorations, contours, rejected = [], [], Counter()
    for mountain in mountains:
        alpha = images[mountain["asset"]]
        if mountain["flip"]:
            alpha = alpha[:, ::-1]
        visible = alpha >= ALPHA
        columns = np.flatnonzero(visible.any(0))
        bottoms = np.array([np.flatnonzero(visible[:, x])[-1] for x in columns])
        x, y, width, height = mountain["bounds"]
        anchors = np.column_stack((x+(columns+.5)*width/alpha.shape[1], y+(bottoms+.5)*height/alpha.shape[0]))
        count = 7 if width >= 225. else 5
        fractions = np.linspace(.12, .88, count)
        keys = ["mountain_soil", "mountain_scree", "root_shade", "mountain_soil", "mountain_scree", "mountain_soil", "mountain_scree"]
        accepted = 0
        for index, fraction in enumerate(fractions):
            at = min(len(columns)-1, int(fraction*(len(columns)-1)))
            key = keys[index]
            asset = catalog["assets"][key]
            rule = GROUND_RULES[key]
            ground_width = min(width*float(rng.uniform(.40, .64)), 150. if key == "mountain_soil" else 110.)
            ground_width = max(52., ground_width)
            ground_height = ground_width*asset["native_size"][1]/asset["native_size"][0]
            offset = [float(rng.uniform(-4., 4.)), float(rng.uniform(-.13, .05)*ground_height)]
            point = anchors[at]+offset
            flip = bool(rng.integers(0, 2))
            pivot = asset.get("pivot", [.5, .5])[:]
            if flip:
                pivot[0] = 1.-pivot[0]
            bounds = [point[0]-ground_width*pivot[0], point[1]-ground_height*pivot[1], ground_width, ground_height]
            if not inputs.valid_ground(bounds, bridge_boxes):
                rejected["full_bbox_water_building_bridge"] += 1
                continue
            row = {"id": f"compact_mountain_decal_{len(decorations):05d}", "asset": key,
                   "x": round(float(point[0]), 5), "y": round(float(point[1]), 5), "z": 24,
                   "position": np.round(point, 5).tolist(), "bounds": np.round(bounds, 5).tolist(),
                   "world_width": round(ground_width, 5), "world_height": round(ground_height, 5),
                   "flip": flip, "opacity": round(float(rng.uniform(*rule["opacity"])), 5),
                   "tint": rule["tint"], "catalog_opacity_scale": 1., "layer": "ground_decals",
                   "draw_order": 0 if key == "mountain_soil" else 2 if key == "root_shade" else 1,
                   "habitat": "mountain_apron", "style_profile": "compact_true_lower_edge_20261109",
                   "mountain_id": mountain["id"], "native_contour_column": int(columns[at]),
                   "native_contour_bottom_y": int(bottoms[at]), "contour_anchor": np.round(anchors[at], 5).tolist(),
                   "contour_offset": np.round(offset, 5).tolist()}
            decorations.append(row)
            accepted += 1
        contours.append({"mountain_id": mountain["id"], "native_size": [alpha.shape[1], alpha.shape[0]],
                         "native_visible_pixels": int(visible.sum()), "visible_columns": len(columns),
                         "ground_details": accepted})
    decorations.sort(key=lambda item: (item["draw_order"], item["asset"], item["id"]))
    ground = {"version": 1, "profile": PROFILE, "world_size": WORLD, "grid_size": 640.,
              "catalog_path": resource(GROUND_CATALOG), "decorations": decorations,
              "seed": SEED+2, "coordinate_rule": "Independent XYZ24 ground decals; exact native lower-edge anchors; no navigation or art mutation",
              "style_rules": GROUND_RULES}
    return ground, contours, dict(rejected)


def verify(inputs, scene, nav, mountains, images, terrain, forest, patch, ground):
    failures = []
    def check(condition, label):
        if not condition:
            failures.append(label)
    grid = rows_array(nav["rows"])
    check(not np.any((grid == 1) & (inputs.nav != 1)), "new mountain blockers must be an old1 subset")
    check(np.all(grid[inputs.nav == 0] == 0), "all original walkable cells remain walkable")
    check(np.array_equal(grid == 2, inputs.old_water), "water2 cells are unchanged")
    check(np.all(grid[inputs.opened] == inputs.nav[inputs.opened]), "old bridge crossing openings remain unchanged")
    exact = np.zeros_like(inputs.old_mountain)
    native_pixels = 0
    for mountain in mountains:
        alpha = images[mountain["asset"]]
        if mountain["flip"]:
            alpha = alpha[:, ::-1]
        native_pixels += int((alpha >= ALPHA).sum())
        xa, ya, xb, yb, projected = inputs.raster(mountain["bounds"], alpha)
        exact[ya:yb, xa:xb] |= projected
        check(not np.any(projected & (grid[ya:yb, xa:xb] != 1)), "native alpha plus one cell must be blocked: "+mountain["id"])
        asset = scene["mountain_catalog"][mountain["asset"]]
        w, h = mountain["bounds"][2:]
        check(abs(w/h - asset["native_size"][0]/asset["native_size"][1]) < 1e-9, "native mountain aspect: "+mountain["id"])
    check(np.array_equal(exact, grid == 1), "no invisible old mountain wall remains")
    removed = set(patch["removed_tree_ids"])
    active_trees = [tree for tree in scene["trees"] if tree["id"] not in removed] + patch["trees"]
    check(len(active_trees) == 5490, "all 5490 active trees retained")
    for tree in active_trees:
        xa, ya, xb, yb = cells(tree["bounds"], inputs.cell, grid.shape)
        check(not (grid[ya:yb, xa:xb] == 1).any(), "tree bbox clear of mountains: "+tree["id"])
    check(terrain["rows"] == inputs.terrain["rows"] and terrain["types"] == inputs.terrain["types"] and terrain["profile_id"] == inputs.terrain["profile_id"], "terrain rows types names multipliers profile unchanged")
    expected = [row for row in inputs.forest_ground["decorations"] if row["habitat"] != "mountain_apron"]
    check(forest["decorations"] == expected and len(expected) == 344, "344 original forest decals remain verbatim")
    check(patch["trees"] == inputs.patch["trees"] and patch["removed_tree_ids"] == inputs.patch["removed_tree_ids"], "forest tree patch remains verbatim")
    check(len(scene["bridges"]) == 9, "nine bridges retained")
    labels, _ = ndi.label(grid == 0)
    anchors = {key: row["drawPos"] for key, row in inputs.layout["locations"].items()}
    anchors.update(inputs.layout["transitPositions"])
    components = [int(labels[int(y/inputs.cell), int(x/inputs.cell)]) for x, y in anchors.values()]
    check(len(anchors) == 444 and len(inputs.layout["locations"]) == 123 and len(inputs.layout["displayEdges"]) == 575, "opaque layout identities retained")
    check(0 not in components and len(set(components)) == 1, "all 444 original anchors share a walkable component")
    route_segments = 0
    for ident, points in inputs.layout["displayEdges"].items():
        forward = all(clear_segment(grid, inputs.cell, a, b) for a, b in zip(points, points[1:]))
        reverse = all(clear_segment(grid, inputs.cell, b, a) for a, b in zip(points, points[1:]))
        route_segments += len(points)-1
        check(forward and reverse, "original route geometry walkable both ways: "+ident)
    for path, digest in inputs.before.items():
        check(sha(ROOT/path) == digest, "immutable original SHA: "+path)
    return {"failures": failures, "all_passed": not failures, "old_walkable_cells_preserved": int((inputs.nav == 0).sum()),
            "old_mountain_cells": int(inputs.old_mountain.sum()), "new_mountain_cells": int((grid == 1).sum()),
            "released_mountain_cells": int(((inputs.nav == 1) & (grid == 0)).sum()),
            "unchanged_water_cells": int(inputs.old_water.sum()), "active_trees": len(active_trees),
            "native_alpha_pixels_verified": native_pixels, "mountain_mask_padding": "one 4-neighbour cross dilation",
            "locations": 123, "anchors": len(anchors), "edges": 575, "route_segments_verified_bidirectionally": route_segments,
            "original_sources18_preserved": True,
            "forest_decals_preserved": len(expected), "bridges": 9, "bridge_open_water_cells": sum(b["open_water_cells"] for b in scene["bridges"])}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--peaks-catalog", type=Path, default=PEAKS)
    parser.add_argument("--bridge-catalog", type=Path, default=BRIDGES)
    parser.add_argument("--analyze-only", action="store_true")
    args = parser.parse_args()
    began = time.perf_counter()
    inputs = Inputs()
    if args.analyze_only:
        analysis = {"old_mountains": len(inputs.scene["mountains"]), "source_regions": len(inputs.groups),
                    "old_nav_counts": dict(Counter("".join(inputs.nav_data["rows"]))),
                    "source_regions_with_parent_height": {str(h): sum(max(m["bounds"][3] for m in rows) >= h for rows in inputs.groups.values()) for h in (70, 100, 140, 180, 240, 300)},
                    "xiyun": inputs.layout["locations"]["bc_place_022"]["drawPos"],
                    "xiyun_old_range": inputs.groups["mountain_0247"], "protected_sources18": inputs.protected18}
        write(OUT / "source_analysis_20261109.json", analysis)
        print(json.dumps({key: value for key, value in analysis.items() if key not in ("xiyun_old_range", "protected_sources18")}, ensure_ascii=True))
        return
    peakdata, sources, catalog, images = load_peaks(args.peaks_catalog)
    composer = Composer(inputs, catalog, images)
    mountains, mountain_mask = composer.run()
    bridge_atlas, bridge_catalog, bridges, bridge_proofs = fit_bridges(inputs, args.bridge_catalog)
    ground, contours, ground_rejected = make_ground(inputs, mountains, images, bridges)
    grid = np.where(inputs.old_water, 2, 0).astype(np.uint8)
    grid[mountain_mask] = 1
    labels, _ = ndi.label(grid == 0)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    mainland = labels == int(sizes.argmax())
    nav = copy.deepcopy(inputs.nav_data)
    nav.update(rows=encode_rows(grid), mainland_rows=encode_rows(mainland), navigation_profile=PROFILE,
               terrain_source="native_compact_peak_alpha46_and_original_water_20261109",
               geometry_source_profile=inputs.nav_data["navigation_profile"],
               geometry_source_sha256=sha(RES / "free_navigation_expanded3x_20261106.json"),
               compatibility="All original0 retained; only original1 released; original2 and nine crossing openings unchanged")
    write(NAV, nav)
    terrain = copy.deepcopy(inputs.terrain)
    terrain["source_files"][rel(NAV)] = sha(NAV)
    write(TERRAIN, terrain)
    forest = copy.deepcopy(inputs.forest_ground)
    forest.update(profile=PROFILE, decorations=[row for row in forest["decorations"] if row["habitat"] != "mountain_apron"])
    forest["coordinate_rule"] = "Original 344 forest and root details preserved verbatim; obsolete original-mountain aprons removed"
    write(FOREST_GROUND, forest)
    patch = copy.deepcopy(inputs.patch)
    live_ids = {mountain["id"] for mountain in mountains}
    patch.update(profile=PROFILE, ground_details_path=resource(FOREST_GROUND),
                 grounded_mountain_ids=[ident for ident in patch["grounded_mountain_ids"] if ident in live_ids])
    write(PATCH, patch)
    write(MOUNTAIN_GROUND, ground)
    scene = copy.deepcopy(inputs.scene)
    scene["mountain_sources"].update(sources)
    scene["mountain_catalog"].update(catalog)
    scene.update(profile=PROFILE, mountains=mountains, bridges=bridges, bridge_atlas=bridge_atlas, bridge_catalog=bridge_catalog,
                 max_prop_height=max(mountain["bounds"][3] for mountain in mountains),
                 forest_grounding_patch_path=resource(PATCH), mountain_ground_details_path=resource(MOUNTAIN_GROUND),
                 all_mountains_grounded=True, geometry_summary={"source_regions": len(composer.groups),
                    "mountains": len(mountains), "old_mountains": len(inputs.scene["mountains"]), "active_trees": 5490,
                    "trees_immutable_base": 5509, "forest_patch_removes": 155, "forest_patch_adds": 136,
                    "bridges": 9, "old_walkable_cells_closed": 0, "old_water_cells_changed": 0,
                    "native_alpha_threshold": ALPHA, "navigation_padding_cells": 1,
                    "navigation_padding_neighbours": 4, "maximum_group_horizontal_span": max(g["new_horizontal_span"] for g in composer.groups)},
                 meta={"all_mountains_grounded": True, "profile": PROFILE,
                       "geometry_source_profile": inputs.nav_data["navigation_profile"],
                       "peak_catalog_path": resource(args.peaks_catalog), "bridge_catalog_path": resource(args.bridge_catalog),
                       "source_geometry_unchanged": True, "seed": SEED})
    write(SCENE, scene)
    verification = verify(inputs, scene, nav, mountains, images, terrain, forest, patch, ground)
    outputpaths = [SCENE, NAV, TERRAIN, PATCH, FOREST_GROUND, MOUNTAIN_GROUND]
    report = {"profile": PROFILE, "elapsed_seconds": time.perf_counter()-began, "verification": verification,
              "source_sha256": inputs.before, "output_sha256": {rel(path): sha(path) for path in outputpaths},
              "peak_catalog_sha256": sha(args.peaks_catalog), "bridge_catalog_sha256": sha(args.bridge_catalog),
              "groups": composer.groups, "mountains": len(mountains), "group_count_distribution": dict(Counter(len(g["new_ids"]) for g in composer.groups)),
              "asset_usage": dict(Counter(m["asset"] for m in mountains)), "placement_rejected": dict(composer.rejected),
              "bridges": bridge_proofs, "mountain_ground_details": len(ground["decorations"]),
              "ground_rejected": ground_rejected, "native_contours": contours,
              "xiyun_compact_group": next((group for group in composer.groups if group["source_region"] == "mountain_0247"), None)}
    write(OUT / "generation_manifest_20261109.json", report)
    print(json.dumps({"profile": PROFILE, "verification": verification, "mountains": len(mountains),
                      "groups": len(composer.groups), "asset_usage": report["asset_usage"],
                      "ground_details": len(ground["decorations"]), "xiyun": report["xiyun_compact_group"],
                      "seconds": report["elapsed_seconds"]}, ensure_ascii=True), flush=True)
    if verification["failures"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
