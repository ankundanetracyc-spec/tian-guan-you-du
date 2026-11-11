"""Independent, read-only audit of the compact peak replacement and top-down bridges.

No production files are written. Native alpha pixels, tree rectangles, historical
navigation and source hashes are read directly, independently of the layout bake.
"""
from pathlib import Path
from collections import Counter
import hashlib
import json
import math
import re
import time

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/bai_chuan_compact_peaks_20261109"
RES = "res://resources/world/bai_chuan/"
CHECKS = 0
FAILURES = []
HASHES = {}


def check(ok, label, detail=None):
    global CHECKS
    CHECKS += 1
    if not ok:
        FAILURES.append({"message": label, "detail": detail or {}})


def path(value):
    return ROOT / str(value).removeprefix("res://")


def read(value):
    return json.loads(path(value).read_text(encoding="utf-8"))


def sha(value):
    key = str(value)
    if key not in HASHES:
        HASHES[key] = hashlib.sha256(path(key).read_bytes()).hexdigest()
    return HASHES[key]


def grid(rows):
    return np.array([np.frombuffer(row.encode("ascii"), dtype=np.uint8) - 48 for row in rows])


def cells_for_bounds(bounds, cell, n):
    x, y, w, h = bounds
    return (slice(max(0, math.floor(y / cell)), min(n, math.ceil((y + h) / cell))),
            slice(max(0, math.floor(x / cell)), min(n, math.ceil((x + w) / cell))))


def overlaps(a, b):
    return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]


def consistent_xyz(row, ground=False):
    return (all(k in row and math.isfinite(row[k]) for k in ("x", "y", "z"))
            and row.get("position") == [row["x"], row["y"]]
            and row["z"] == (24 if ground else row["y"]))


def main():
    began = time.perf_counter()
    scale = path("gdscripts/world/BaiChuanWorldScale.gd").read_text(encoding="utf-8")
    config = lambda key: re.search(rf'^const {key} := "([^"]+)"', scale, re.M).group(1)
    scene_path, nav_path, terrain_path, layout_path = [config(k) for k in ("LANDSCAPE_PATH", "NAVIGATION_PATH", "TERRAIN_PATH", "LAYOUT_PATH")]
    check(scene_path == RES + "landscape_compact_peaks_20261109.json", "active scene is the new independent compact-peak file")
    check(nav_path == RES + "free_navigation_compact_peaks_20261109.json", "active collision file is independently versioned")
    check(layout_path == RES + "free_layout_expanded3x_20261106.json", "the immutable existing graph remains active")
    scene, nav, terrain, layout = [read(p) for p in (scene_path, nav_path, terrain_path, layout_path)]
    old_scene = read(RES + "landscape_expanded3x_20261106.json")
    old_nav = read(RES + "free_navigation_expanded3x_20261106.json")
    old_terrain = read(RES + "terrain_travel_expanded3x_20261106.json")
    old_patch = read(RES + "forest_grounding_sample_20261106.json")
    old_ground = read(old_patch["ground_details_path"])
    water = read(RES + "expanded_water_20261106.json")
    patch_path = scene.get("forest_grounding_patch_path", RES + "forest_grounding_sample_20261106.json")
    patch = read(patch_path)
    forest_ground = read(patch["ground_details_path"])
    mountain_ground_path = scene.get("mountain_ground_details_path", RES + "mountain_ground_details_20261109.json")
    mountain_ground = read(mountain_ground_path)
    before = {p: sha(p) for p in [scene_path, nav_path, terrain_path, layout_path, patch_path, patch["ground_details_path"], mountain_ground_path]}
    world, cell, n = nav["world_size"], nav["cell_size"], nav["columns"]
    actual, previous = grid(nav["rows"]), grid(old_nav["rows"])
    check((world, nav["world_km"], n, nav["navigation_profile"]) == (22118.4, 8000, 1413, "compact_peaks_20261109_v1") and abs(cell - 15.678468899521532) < 1e-12, "new source retains world and collision ruler")
    check(actual.shape == (1413, 1413) and set(np.unique(actual)).issubset({0, 1, 2}), "complete rectangular collision grid and allowed codes")
    check(not np.any((actual == 1) & (previous != 1)), "new mountain obstruction is a subset of the immutable old mountain obstruction")
    check(np.all(actual[previous == 0] == 0), "every previously walkable cell stays walkable")
    check(np.array_equal(actual == 2, previous == 2), "water obstruction cells remain exactly unchanged")
    check(nav["mainland_rows"] == old_nav["mainland_rows"] or np.all(grid(nav["mainland_rows"])[grid(old_nav["mainland_rows"]) == 1] == 1), "previous mainland migration candidates stay available")
    pad = (np.arange(n) + .5) * cell >= world
    check(np.all(actual[pad, :] != 0) and np.all(actual[:, pad] != 0), "outside-world allocated padding stays blocked")
    check(len(layout["locations"]) == 123 and len(layout["transitPositions"]) == 321 and len(layout["displayEdges"]) == 575, "123 places, 444 anchors and 575 graph links remain")
    check(terrain["rows"] == old_terrain["rows"] and terrain["profile_id"] == old_terrain["profile_id"], "terrain classifications and profile stay unchanged")
    check(terrain["types"] == old_terrain["types"], "all terrain names, identities and speed multipliers stay unchanged")
    check(terrain["columns"] == 471 and abs(terrain["cell_size"] - cell * 3) < 1e-9, "471-cell terrain remains separate from collision")
    for p, expected in old_patch["source_sha256"].items():
        check(path(p).exists() and sha(p) == expected, "immutable original source SHA256", {"path": p})
    check(len(old_patch["source_sha256"]) == 18, "all eighteen immutable source hashes verified")

    # The active tree list is reconstructed without calling the runtime patch.
    check(patch["trees"] == old_patch["trees"] and patch["removed_tree_ids"] == old_patch["removed_tree_ids"], "existing 136 natural trees and 155 removed IDs are byte-for-field preserved")
    check(scene["trees"] == old_scene["trees"], "the original 5509-tree base is preserved")
    removed = set(patch["removed_tree_ids"])
    trees = [r for r in scene["trees"] if r["id"] not in removed] + patch["trees"]
    check((len(removed), len(patch["trees"]), len(trees)) == (155, 136, 5490), "active trees are 5509 minus 155 plus 136")
    check(len({r["id"] for r in trees}) == 5490, "active tree identities remain unique")
    tree_bad, inherited_tree_water = [], []
    new_tree_ids = {r["id"] for r in patch["trees"]}
    for row in trees:
        index = cells_for_bounds(row["bounds"], cell, n)
        current_box, old_box = actual[index], previous[index]
        point_x, point_y = math.floor(row["x"] / cell), math.floor(row["y"] / cell)
        valid = (consistent_xyz(row) and actual[point_y, point_x] == 0
                 and not np.any(current_box == 1)
                 and np.array_equal(current_box == 2, old_box == 2)
                 and (row["id"] not in new_tree_ids or not np.any(current_box != 0)))
        check(valid, "active tree keeps its walkable footpoint and mountain-free crown; new sample also avoids all water", {"id": row["id"]})
        if not valid: tree_bad.append(row["id"])
        if np.any(old_box == 2): inherited_tree_water.append({"id": row["id"], "old_water_cells": int((old_box == 2).sum()), "new_water_cells": int((current_box == 2).sum())})
    check(len(inherited_tree_water) == 54 and all(r["id"] not in new_tree_ids and r["old_water_cells"] == r["new_water_cells"] for r in inherited_tree_water), "existing 54 old tree crowns over water remain unchanged and are not new overlaps")
    expected_ground = [r for r in old_ground["decorations"] if r["habitat"] != "mountain_apron"]
    check(len(expected_ground) == 344 and forest_ground["decorations"] == expected_ground, "all 344 existing forest floor/root decals are exactly preserved, old 32 aprons removed")
    live_peaks = {m["id"]: m for m in scene["mountains"]}
    check(all(i in live_peaks and overlaps(patch["bounds"], live_peaks[i]["bounds"]) for i in patch.get("grounded_mountain_ids", [])), "forest patch only references current visible short peaks within its sample")

    # Reconstruct the new collision support from every native visible pixel.
    images, alphas = {}, {}
    for bank, source in scene["mountain_sources"].items():
        native = Image.open(path(source["path"]))
        check(native.mode == "RGBA", "peak source is a real native RGBA image", {"bank": bank})
        images[bank] = np.asarray(native)
        check(sha(source["path"]) == source["sha256"], "peak source native SHA256", {"bank": bank})
    peak_catalog = read(scene.get("meta", {}).get("peak_catalog_path", "res://textures/world/bai_chuan/compact_peaks_20261109/peaks_catalog.json"))
    new_assets = peak_catalog.get("mountain_catalog", peak_catalog.get("assets", {}))
    used_assets = {m["asset"] for m in scene["mountains"]}
    check(len(new_assets) == 6 and used_assets.issubset(new_assets), "six new native short-peak silhouettes replace all visible old long-ridge assets")
    for key, asset in scene["mountain_catalog"].items():
        image = images[asset["bank"]]
        rx, ry, rw, rh = asset["region"]
        check(rx >= 0 and ry >= 0 and rx + rw <= image.shape[1] and ry + rh <= image.shape[0], "peak native region stays within original source", {"asset": key})
        alpha = image[ry:ry + rh, rx:rx + rw, 3]
        alphas[key] = alpha
        border = np.concatenate((alpha[0], alpha[-1], alpha[:, 0], alpha[:, -1]))
        check(int(border.max()) <= 2, "complete peak region has a transparent border on every side", {"asset": key, "edge_alpha": int(border.max())})
        check(np.any(alpha >= 46), "peak has actual opaque native content", {"asset": key})
    support = np.zeros_like(actual, dtype=bool)
    visible_pixels = 0
    leak_samples = []
    native_samples = []
    peak_index = {m["id"]: m for m in scene["mountains"]}
    for row in scene["mountains"]:
        asset = scene["mountain_catalog"][row["asset"]]
        alpha = alphas[row["asset"]]
        x, y, width, height = row["bounds"]
        rw, rh = alpha.shape[1], alpha.shape[0]
        sx, sy = width / rw, height / rh
        check(consistent_xyz(row) and row.get("layer") == "ground_props", "peak sorts by explicit fixed XYZ footpoint", {"id": row["id"]})
        check(abs(sx - sy) < 1e-9 and sx <= 1 + 1e-9 and sx > 0, "peak uses native aspect ratio without enlargement", {"id": row["id"], "native_scale": sx})
        pivot_x = 1 - asset["pivot"][0] if row.get("flip", False) else asset["pivot"][0]
        # The production renderer mirrors the texture inside a fixed rectangle;
        # source catalogs may already author a symmetric horizontal footpoint.
        pivot_err = min(abs(x + width * asset["pivot"][0] - row["x"]), abs(x + width * pivot_x - row["x"]))
        check(pivot_err < 1e-6 and abs(y + height * asset["pivot"][1] - row["y"]) < 1e-6, "peak world rectangle retains its catalog footpoint", {"id": row["id"]})
        py, source_px = np.nonzero(alpha >= 46)
        px = rw - 1 - source_px if row.get("flip", False) else source_px
        wx, wy = x + (px + .5) * sx, y + (py + .5) * sy
        gx, gy = np.floor(wx / cell).astype(int), np.floor(wy / cell).astype(int)
        inside = (gx >= 0) & (gx < n) & (gy >= 0) & (gy < n) & (wx < world) & (wy < world)
        check(np.all(inside), "every visible native peak pixel remains within the world", {"id": row["id"]})
        gx, gy = gx[inside], gy[inside]
        visible_pixels += len(gx)
        # A native pixel is a small rectangle, not an averaged navigation
        # sample. Map all four corners, with the right/bottom edge kept inside
        # the pixel. Since there is no enlargement its width is below a cell.
        for ox, oy in ((0., 0.), (0., 1. - 1e-9), (1. - 1e-9, 0.), (1. - 1e-9, 1. - 1e-9)):
            cx = np.floor((x + (px + ox) * sx) / cell).astype(int)
            cy = np.floor((y + (py + oy) * sy) / cell).astype(int)
            keep = (cx >= 0) & (cx < n) & (cy >= 0) & (cy < n)
            support[cy[keep], cx[keep]] = True
        bad = actual[gy, gx] != 1
        check(not np.any(bad), "every native peak pixel alpha>=46 maps to actually blocked navigation", {"id": row["id"], "leaked_pixels": int(bad.sum())})
        if np.any(bad) and len(leak_samples) < 20:
            at = int(np.flatnonzero(bad)[0])
            leak_samples.append({"id": row["id"], "world": [float(wx[at]), float(wy[at])], "cell": [int(gx[at]), int(gy[at])], "native_pixel": [int(source_px[at]), int(py[at])], "native_alpha": int(alpha[py[at], source_px[at]])})
        if len(gx):
            for at in np.linspace(0, len(gx) - 1, 5, dtype=int):
                native_samples.append({"id": row["id"], "world": [float(wx[at]), float(wy[at])], "cell": [int(gx[at]), int(gy[at])], "native_pixel": [int(source_px[at]), int(py[at])], "native_alpha": int(alpha[py[at], source_px[at]])})
    expected_mountain = ndi.binary_dilation(support, iterations=1) & (previous == 1)
    check(np.all(actual[support] == 1), "every native visible pixel footprint corner is covered by actual mountain obstruction")
    unsupported = (actual == 1) & ~expected_mountain
    padding_mask = np.zeros_like(support)
    padding_mask[pad, :] = True
    padding_mask[:, pad] = True
    unsupported &= ~padding_mask
    check(not np.any(unsupported), "no retained invisible mountain wall farther than one collision cell from real native alpha", {"unsupported_cells": int(unsupported.sum())})
    check(np.array_equal((actual == 1) & ~padding_mask, expected_mountain & ~padding_mask), "actual mountain mask equals the independent native pixel footprint union plus one conservative cross cell")
    released = (previous == 1) & (actual == 0)
    check(np.any(released), "clear gaps left by replaced long ridges are actually released to walking")
    groups = {}
    for row in scene["mountains"]: groups.setdefault(row["source_region"], []).append(row)
    check(all(1 <= len(v) <= 3 for v in groups.values()), "native short peaks are arranged in small groups of at most three")
    check(all(len(v) == 1 or max(p["y"] for p in v) > min(p["y"] for p in v) for v in groups.values()), "multiple peaks in one group use actual front/back footpoints")

    # Validate the actual bridge deck, not its rotated outer bounding box.
    bridge_img = Image.open(path(scene["bridge_atlas"]))
    check(bridge_img.mode == "RGBA", "new top-down bridge atlas is native RGBA")
    bridge_catalog_data = read(scene.get("meta", {}).get("bridge_catalog_path", "res://textures/world/bai_chuan/compact_peaks_20261109/bridge_catalog.json"))
    check(sha(scene["bridge_atlas"]) == bridge_catalog_data["source_sha256"], "new bridge source remains byte-identical to its native generation catalog")
    bridge_alpha = np.asarray(bridge_img)[:, :, 3]
    check(scene["bridge_atlas"] != old_scene["bridge_atlas"], "bridge uses the newly generated top-down art")
    old_by_id = {b["id"]: b for b in old_scene["bridges"]}
    wet, opened = grid(water["water_rows"]) == 2, grid(water["bridge_open_rows"]) == 1
    union = np.zeros_like(support)
    bridge_proofs = []
    check(len(scene["bridges"]) == len(old_scene["bridges"]) == 9, "all nine bridge crossings remain")
    for bridge in scene["bridges"]:
        key = bridge["id"]
        old = old_by_id[key]
        asset = scene["bridge_catalog"][bridge["asset"]]
        check(all(bridge[k] == old[k] for k in ("position", "open_runs", "open_water_cells", "bank_contacts")), "bridge preserves original centre, banks and exact navigation opening", {"id": key})
        cells = np.array([(y, x) for y, x0, x1 in bridge["open_runs"] for x in range(x0, x1)], dtype=int)
        union[cells[:, 0], cells[:, 1]] = True
        check(len(cells) == bridge["open_water_cells"] and np.all(wet[cells[:, 0], cells[:, 1]]) and np.all(actual[cells[:, 0], cells[:, 1]] == 0), "bridge opens precisely its original real water cells", {"id": key})
        theta = bridge["rotation"]
        rw, rh = asset["region"][2:]
        scale = bridge["source_to_world_scale"]
        check(abs(bridge["world_width"] / rw - scale) < 1e-9 and abs(bridge["world_height"] / rh - scale) < 1e-9 and scale <= 1, "top-down bridge keeps the native aspect ratio without enlargement", {"id": key, "scale": scale})
        delta = np.asarray(bridge["bank_contacts"][1]["position"]) - bridge["bank_contacts"][0]["position"]
        alignment = abs(float(delta @ np.array([math.cos(theta), math.sin(theta)]))) / float(np.linalg.norm(delta))
        check(theta == old["rotation"] and alignment >= .99, "bridge retains the bank-to-bank direction across the river", {"id": key, "axis_alignment_cosine": alignment})
        rotation = np.array([[math.cos(theta), -math.sin(theta)], [math.sin(theta), math.cos(theta)]])
        centers = (cells[:, ::-1] + .5) * cell
        corners = (centers[:, None, :] + cell * np.array([[-.5, -.5], [-.5, .5], [.5, -.5], [.5, .5]])[None, :, :]).reshape(-1, 2)
        native = ((corners - bridge["position"]) @ rotation) / bridge["source_to_world_scale"] + asset["native_pivot_px"]
        d = asset["deck_rect_atlas"]
        covered = (native[:, 0] >= d[0]) & (native[:, 0] < d[2]) & (native[:, 1] >= d[1]) & (native[:, 1] < d[3])
        check(np.all(covered), "each old opened water cell's four corners lie inside the new opaque deck", {"id": key, "corners_outside": int((~covered).sum())})
        within_image = (native[:, 0] >= 0) & (native[:, 0] < bridge_alpha.shape[1]) & (native[:, 1] >= 0) & (native[:, 1] < bridge_alpha.shape[0])
        minimum = 0
        if np.all(within_image):
            ix, iy = np.floor(native).astype(int).T
            minimum = int(bridge_alpha[iy, ix].min())
        check(minimum >= 240, "real native RGBA is opaque at every transformed water-cell corner", {"id": key, "minimum_alpha": minimum})
        check(int(bridge_alpha[d[1]:d[3], d[0]:d[2]].min()) >= 240, "whole certified bridge deck is actually opaque", {"id": key})
        # Real bank landing points also need to land inside visible bridge art.
        landings = np.array([v["position"] for v in bridge["bank_contacts"] + bridge["end_footings"]])
        bank_native = ((landings - bridge["position"]) @ rotation) / bridge["source_to_world_scale"] + asset["native_pivot_px"]
        ix, iy = np.floor(bank_native).astype(int).T
        bank_inside = np.all((ix >= 0) & (iy >= 0) & (ix < bridge_alpha.shape[1]) & (iy < bridge_alpha.shape[0]))
        bank_alpha = bridge_alpha[iy, ix].tolist() if bank_inside else []
        check(bank_inside and min(bank_alpha, default=0) >= 46, "actual bank contacts and both end footings land on visible native bridge art", {"id": key, "bank_native": bank_native.tolist(), "bank_alpha": bank_alpha})
        footing_cells = [(math.floor(v["position"][1] / cell), math.floor(v["position"][0] / cell)) for v in bridge["end_footings"]]
        check(all(actual[cy, cx] == 0 and not wet[cy, cx] for cy, cx in footing_cells), "both bridge ends are seated on original dry walkable banks", {"id": key})
        bridge_proofs.append({"id": key, "opened_cells": len(cells), "corners_checked": len(corners), "minimum_corner_alpha": minimum, "bank_alpha": bank_alpha,
                              "native_scale": scale, "axis_alignment_cosine": alignment, "world_size": [bridge["world_width"], bridge["world_height"]]})
    check(np.array_equal(union, opened) and int(union.sum()) == 306, "nine bridge openings exactly preserve the original 306 water cells")
    check(not any("ford" in k.lower() for k in scene), "no obsolete ford mask is connected to the scene")

    # Each new mountain ground decal must anchor on the actual alpha lower edge.
    decorations = mountain_ground["decorations"]
    anchors_verified = 0
    buildings = [[v["rect"][0], v["rect"][1], v["rect"][2] - v["rect"][0], v["rect"][3] - v["rect"][1]] for v in layout["locations"].values()]
    bridge_bounds = [b["bounds"] for b in old_scene["bridges"] + scene["bridges"]]
    display_wet = np.asarray(Image.open(path(water["water_sdf"])).convert("L")) < 128
    display_cell = world / display_wet.shape[1]
    for row in decorations:
        key = row.get("mountain_id", "")
        mountain = peak_index.get(key)
        check(mountain is not None, "new ground decal refers to a visible current short peak", {"id": row["id"], "mountain_id": key})
        box = row["bounds"]
        check(consistent_xyz(row, True) and row.get("layer") == "ground_decals", "mountain ground decal is explicit nonblocking XYZ24", {"id": row["id"]})
        check(box[0] >= 0 and box[1] >= 0 and box[0] + box[2] <= world and box[1] + box[3] <= world and not np.any(wet[cells_for_bounds(box, cell, n)]) and not np.any(display_wet[cells_for_bounds(box, display_cell, display_wet.shape[1])]) and not any(overlaps(box, b) for b in buildings + bridge_bounds), "mountain ground whole box avoids navigation/display water, all 123 locations, old/new crossings and world boundary", {"id": row["id"]})
        anchor = row.get("contour_anchor", row.get("contour_world", row.get("edge_anchor", row.get("anchor")) ))
        if mountain is not None and anchor is not None:
            alpha = alphas[mountain["asset"]]
            if mountain.get("flip", False): alpha = np.fliplr(alpha)
            bx, by, bw, bh = mountain["bounds"]
            column = min(alpha.shape[1] - 1, max(0, math.floor((anchor[0] - bx) * alpha.shape[1] / bw)))
            ys = np.flatnonzero(alpha[:, column] >= 46)
            expected_y = by + (ys[-1] + .5) * bh / alpha.shape[0] if len(ys) else math.inf
            check(len(ys) > 0 and abs(anchor[1] - expected_y) <= .1, "ground anchor follows that native alpha column's actual lower edge", {"id": row["id"], "error": abs(anchor[1] - expected_y)})
            check(box[0] <= anchor[0] <= box[0] + box[2] and box[1] < anchor[1] < box[1] + box[3], "mountain ground decal straddles the true visible lower edge", {"id": row["id"]})
            anchors_verified += 1
        else:
            check(False, "ground decal exposes its actual native lower-edge anchor for independent verification", {"id": row["id"], "fields": list(row)})
    check(len(decorations) > 0 and anchors_verified == len(decorations), "every new ground decal has an independently verified native contour anchor")

    # Report real cells for a second check through the actual C# packed grid.
    selected = np.argwhere(released)
    gap_samples = [{"cell": [int(x), int(y)], "world": [(float(x) + .5) * cell, (float(y) + .5) * cell]} for y, x in selected[np.linspace(0, len(selected) - 1, min(24, len(selected)), dtype=int)]] if len(selected) else []
    for p, expected in before.items():
        check(hashlib.sha256(path(p).read_bytes()).hexdigest() == expected, "audit leaves every production data byte unchanged", {"path": p})
    report = {"checks": CHECKS, "failures": FAILURES, "elapsed_seconds": time.perf_counter() - began,
              "native_mountain_pixels_checked": visible_pixels, "native_mountain_blocked_cells": int(support.sum()),
              "old_mountain_cells": int((previous == 1).sum()), "new_mountain_cells": int((actual == 1).sum()),
              "released_mountain_cells": int(released.sum()), "unsupported_mountain_cells": int(unsupported.sum()),
              "mountain_leak_samples": leak_samples, "native_runtime_samples": native_samples, "released_runtime_samples": gap_samples,
              "scene_counts": {"mountains": len(scene["mountains"]), "active_trees": len(trees), "bridges": len(scene["bridges"]), "forest_ground_decals": len(forest_ground["decorations"]), "mountain_ground_decals": len(decorations)},
              "active_tree_box_failures": tree_bad, "inherited_tree_water_box_count": len(inherited_tree_water), "inherited_tree_water_boxes": inherited_tree_water,
              "bridges": bridge_proofs, "source_sha256": HASHES,
              "immutable_sources_verified": len(old_patch["source_sha256"]), "production_files_modified": False}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "geometry_independent_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"COMPACT_PEAKS_GEOMETRY checks={CHECKS} failures={len(FAILURES)} native_pixels={visible_pixels} seconds={report['elapsed_seconds']:.2f}")
    for failure in FAILURES[:15]: print("FAIL", json.dumps(failure, ensure_ascii=False))
    raise SystemExit(1 if FAILURES else 0)


if __name__ == "__main__":
    main()
