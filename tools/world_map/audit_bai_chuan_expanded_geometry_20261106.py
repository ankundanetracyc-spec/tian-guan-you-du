"""Independent read-only asset/grid audit for the expanded world geometry."""
from pathlib import Path
import hashlib
import json
import math
import re
import time
from collections import Counter

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/bai_chuan_expanded3x_20261106"
checks = 0
failures = []
hashes = {}


def check(condition, label):
    global checks
    checks += 1
    if not condition:
        failures.append(label)


def path(value):
    value = str(value)
    return ROOT / value.removeprefix("res://") if value.startswith("res://") else ROOT / value


def read(value):
    return json.loads(path(value).read_text(encoding="utf-8"))


def sha(value):
    p = path(value)
    key = str(p)
    if key not in hashes:
        hashes[key] = hashlib.sha256(p.read_bytes()).hexdigest()
    return hashes[key]


def verify_hash(value, expected, label):
    p = path(value)
    check(p.exists() and sha(value) == expected, label)


def main():
    began = time.perf_counter()
    scale = (ROOT / "gdscripts/world/BaiChuanWorldScale.gd").read_text(encoding="utf-8")
    config = lambda key: re.search(rf'^const {key} := "([^"]+)"', scale, re.M).group(1)
    scene = read(config("LANDSCAPE_PATH"))
    layout = read(config("LAYOUT_PATH"))
    nav = read(config("NAVIGATION_PATH"))
    water = read("res://resources/world/bai_chuan/expanded_water_20261106.json")
    terrain = read(config("TERRAIN_PATH"))
    catalog = read(layout["native_node_catalog"])
    complete = read("res://textures/world/bai_chuan/expanded3x_20261106/mountain_catalog_complete.json")
    world, cell, n = nav["world_size"], nav["cell_size"], nav["columns"]
    grid = np.array([np.frombuffer(r.encode("ascii"), dtype=np.uint8) - 48 for r in nav["rows"]])
    mountains = np.load(OUT / "mountain_collision_grid.npy")
    wet = np.array([np.frombuffer(r.encode("ascii"), dtype=np.uint8) == 50 for r in water["water_rows"]])
    opened = np.array([np.frombuffer(r.encode("ascii"), dtype=np.uint8) == 49 for r in water["bridge_open_rows"]])
    check(world == 22118.4 and nav["world_km"] == 8000 and n == 1413, "expanded world/navigation metadata")
    check(terrain["columns"] == 471 and abs(terrain["cell_size"] - cell * 3) < 1e-9, "terrain resolution independent from collision")
    check(len(layout["locations"]) == 123 and len(layout["transitPositions"]) == 321 and len(layout["displayEdges"]) == 575, "123 places / 444 anchors / 575 edges")
    bake = read("res://output/bai_chuan_expanded3x_20261106/layout_bake_report.json")
    check((len(scene["mountains"]), len(scene["trees"]), len(scene["bridges"])) == (bake["mountains"], bake["trees"], bake["bridges"]) and len(scene["bridges"]) == 9, "final scene counts match current bake")
    check(scene["mountain_catalog"] == complete["mountain_catalog"], "scene uses final complete component catalog")
    check(mountains.shape == (n, n) and np.array_equal(mountains, grid == 1), "saved collision mask exactly matches navigation mountains")
    padding = (np.arange(n) + .5) * cell >= world
    check(np.all(grid[padding, :] != 0) and np.all(grid[:, padding] != 0), "every outside-world padding cell blocked")
    check(not np.any(opened & ~wet), "global bridge openings only replace real water cells")

    for key, source in scene["mountain_sources"].items():
        verify_hash(source["path"], source["sha256"], f"mountain native bank hash {key}")
    for filename, entry in catalog["atlases"].items():
        verify_hash(entry["path"], entry["sha256"], f"node atlas hash {filename}")
    for field in (water["source_sha256"], terrain["source_files"], read(water["reports"]["alpha"])["source_sha256"]):
        for source, expected in field.items():
            verify_hash(source, expected, f"provenance hash {source}")
    verify_hash("res://docs/prototypes/百川宗门城镇资源规划数据_20261027.json", layout["logicalPlanSha256"], "logical plan source hash")
    # This inherited field identifies the preserved historical layout artwork,
    # rather than the new independently textured landscape surface.
    verify_hash("res://textures/world/bai_chuan/free_world_8000_3x3_20261104/master_8000.png", layout["artSha256"], "historical layout artwork provenance hash")
    provenance = read(catalog["generation_provenance"])
    for entry in provenance["atlases"]:
        native = path(entry["tool_source"])
        atlas = path(catalog["atlases"][entry["file"]]["path"])
        check(native.exists() and sha(native) == sha(atlas), f"byte-identical generated original {entry['file']}")

    images = {key: np.asarray(Image.open(path(value["path"])).convert("RGBA")) for key, value in scene["mountain_sources"].items()}
    complete_regions = {}
    for bank, image in images.items():
        alpha = image[:, :, 3]
        labels, count = ndi.label(alpha >= complete["segmentation"]["opaque_threshold"])
        sizes = np.bincount(labels.ravel())
        major = np.flatnonzero(sizes >= 1000)
        major = major[major != 0]
        check(len(major) == 4, f"four independent native opaque components {bank}")
        seeds = np.where(np.isin(labels, major), labels, 0)
        nearest = ndi.distance_transform_edt(seeds == 0, return_distances=False, return_indices=True)
        owners = seeds[tuple(nearest)]
        for ident, asset in scene["mountain_catalog"].items():
            if asset["bank"] != bank:
                continue
            x, y, width, height = asset["region"]
            check(x >= 0 and y >= 0 and x + width <= image.shape[1] and y + height <= image.shape[0], f"complete region within bank {ident}")
            region_labels = labels[y:y+height, x:x+width]
            contained = np.bincount(region_labels.ravel(), minlength=count + 1)
            contained[0] = 0
            component = int(contained.argmax())
            owned = (owners == component) & (alpha >= complete["segmentation"]["soft_threshold"])
            inside = np.zeros_like(owned)
            inside[y:y+height, x:x+width] = True
            check(not np.any(owned & ~inside), f"all actual opaque/soft mountain component retained {ident}")
            border = np.concatenate((alpha[y, x:x+width], alpha[y+height-1, x:x+width], alpha[y:y+height, x], alpha[y:y+height, x+width-1]))
            check(int(border.max()) <= 2, f"native mountain region has transparent full border {ident}")
            complete_regions[ident] = image[y:y+height, x:x+width, 3]

    native_mountain_pixels = 0
    mountain_leak_samples = []
    leak_by_cell = {}
    leak_statistics = {str(level): {"pixels": 0, "cells": set(), "mountains": set()} for level in (46, 95, 128, 200)}
    leaking_assets = Counter()
    leaking_flips = Counter()
    for entry in scene["mountains"]:
        ident = entry["id"]
        asset = scene["mountain_catalog"][entry["asset"]]
        alpha = complete_regions[entry["asset"]]
        x, y, width, height = entry["bounds"]
        check(all(k in entry and math.isfinite(entry[k]) for k in ("x", "y", "z")) and entry["position"] == [entry["x"], entry["y"]] and entry["z"] == entry["y"], f"mountain explicit consistent XYZ {ident}")
        check(abs(width / asset["region"][2] - height / asset["region"][3]) < 1e-10, f"native mountain uniform scale {ident}")
        check(abs(x + width * asset["pivot"][0] - entry["x"]) < 1e-7 and abs(y + height * asset["pivot"][1] - entry["y"]) < 1e-7, f"native mountain pivot transform {ident}")
        py, px = np.nonzero(alpha >= 46)
        source_px = px.copy()
        native_alpha = alpha[py, px]
        native_mountain_pixels += len(px)
        if entry["flip"]:
            px = alpha.shape[1] - 1 - px
        wx = x + (px + .5) * width / alpha.shape[1]
        wy = y + (py + .5) * height / alpha.shape[0]
        gx = np.floor(wx / cell).astype(int)
        gy = np.floor(wy / cell).astype(int)
        bad = grid[gy, gx] == 0
        check(not np.any(bad), f"every actual native mountain pixel alpha>=46 blocked {ident}")
        for level in (46, 95, 128, 200):
            selected = np.flatnonzero(bad & (native_alpha >= level))
            if len(selected):
                stats = leak_statistics[str(level)]
                stats["pixels"] += len(selected)
                stats["mountains"].add(ident)
                stats["cells"].update(zip(gy[selected].tolist(), gx[selected].tolist()))
        if np.any(bad):
            leaking_assets[entry["asset"]] += 1
            leaking_flips[str(entry["flip"])] += 1
        for at in np.flatnonzero(bad):
            key = (int(gy[at]), int(gx[at]))
            sample = {"id": ident, "world": [float(wx[at]), float(wy[at])], "cell": [key[1], key[0]],
                      "native_alpha": int(native_alpha[at]), "native_pixel": [int(source_px[at]), int(py[at])],
                      "asset": entry["asset"], "bank": asset["bank"], "flip": entry["flip"]}
            if key not in leak_by_cell or sample["native_alpha"] > leak_by_cell[key]["native_alpha"]:
                leak_by_cell[key] = sample
        if np.any(bad) and len(mountain_leak_samples) < 12:
            strong = np.flatnonzero(bad & (native_alpha >= 200))
            at = int(strong[0] if len(strong) else np.flatnonzero(bad)[0])
            mountain_leak_samples.append({"id": ident, "world": [float(wx[at]), float(wy[at])], "cell": [int(gx[at]), int(gy[at])], "native_alpha": int(native_alpha[at]), "nav_code": int(grid[gy[at], gx[at]])})

    for entry in scene["trees"]:
        ident = entry["id"]
        x, y, width, height = entry["bounds"]
        xa, ya = max(0, math.floor(x / cell)), max(0, math.floor(y / cell))
        xb, yb = min(n, math.ceil((x+width) / cell)), min(n, math.ceil((y+height) / cell))
        check(not mountains[ya:yb, xa:xb].any(), f"entire tree bbox avoids mountain grid {ident}")
        check(all(k in entry and math.isfinite(entry[k]) for k in ("x", "y", "z")) and entry["position"] == [entry["x"], entry["y"]] and entry["z"] == entry["y"], f"tree explicit consistent XYZ {ident}")

    bridge_alpha = np.asarray(Image.open(path(scene["bridge_atlas"])).convert("RGBA"))[:, :, 3]
    union = np.zeros((n, n), dtype=bool)
    bridge_proofs = []
    for bridge in scene["bridges"]:
        ident = bridge["id"]
        check(all(k in bridge and math.isfinite(bridge[k]) for k in ("x", "y", "z")) and bridge["position"] == [bridge["x"], bridge["y"]] and bridge["z"] == 28, f"bridge explicit consistent XYZ {ident}")
        asset = scene["bridge_catalog"][bridge["asset"]]
        cells = np.array([(y, x) for y, x0, x1 in bridge["open_runs"] for x in range(x0, x1)], dtype=int)
        check(len(cells) == bridge["open_water_cells"] and np.all(wet[cells[:, 0], cells[:, 1]]) and np.all(grid[cells[:, 0], cells[:, 1]] == 0), f"bridge opens exact real-water navigation cells {ident}")
        union[cells[:, 0], cells[:, 1]] = True
        d = asset["deck_rect_atlas"]
        check(int(bridge_alpha[d[1]:d[3], d[0]:d[2]].min()) >= 240, f"whole native bridge deck opaque {ident}")
        theta = bridge["rotation"]
        rotation = np.array([[math.cos(theta), -math.sin(theta)], [math.sin(theta), math.cos(theta)]])
        centers = (cells[:, ::-1] + .5) * cell
        # A cell's four corners inside an axis-aligned opaque source rectangle
        # prove that every point in its rotated square is covered by the deck.
        corners = (centers[:, None, :] + cell * np.array([[-.5, -.5], [-.5, .5], [.5, -.5], [.5, .5]])[None, :, :]).reshape(-1, 2)
        native = ((corners - bridge["position"]) @ rotation) / bridge["source_to_world_scale"] + asset["native_pivot_px"]
        covered = (native[:, 0] >= d[0]) & (native[:, 0] < d[2]) & (native[:, 1] >= d[1]) & (native[:, 1] < d[3])
        check(np.all(covered), f"all opened cell corners fully covered by actual native bridge deck {ident}")
        ix, iy = np.floor(native).astype(int).T
        minimum = int(bridge_alpha[iy, ix].min())
        check(minimum >= 240, f"real native alpha covers every bridge cell corner {ident}")
        bridge_proofs.append({"id": ident, "opened_cells": len(cells), "minimum_corner_alpha": minimum})
    check(np.array_equal(union, opened), "nine bridge run unions equal global opening mask exactly")

    minimum_sampling = math.inf
    used_assets = set()
    for entry in catalog["location_bindings"]:
        ident = entry["id"]
        row = layout["locations"][ident]
        asset = catalog["assets"][entry["asset"]]
        used_assets.add(entry["asset"])
        width = row["rect"][2] - row["rect"][0]
        height = row["rect"][3] - row["rect"][1]
        sampling = min(asset["native_size"][0] / (width * .8), asset["native_size"][1] / (height * .8))
        minimum_sampling = min(minimum_sampling, sampling)
        check(sampling >= 1.0 - 1e-9, f"building never upsampled on screen at .8 {ident}")
        check(all(k in row and math.isfinite(row[k]) for k in ("x", "y", "z")) and row["drawPos"] == [row["x"], row["y"]] and row["z"] == row["y"], f"building explicit consistent XYZ {ident}")
    check(len(catalog["assets"]) == 24 and len(used_assets) == 24, "all 24 native building assets present and used")

    for stats in leak_statistics.values():
        stats["cell_count"] = len(stats["cells"])
        stats["mountain_count"] = len(stats["mountains"])
        stats["cells"] = [[x, y] for y, x in sorted(stats["cells"])]
        stats["mountains"] = sorted(stats["mountains"])
    leak_bands = Counter([min(2, int((y+.5)*cell/world*3)) for y, x in leak_by_cell])
    result = {"checks": checks, "failures": failures, "elapsed_seconds": time.perf_counter() - began,
              "scene_counts": {"mountains": len(scene["mountains"]), "trees": len(scene["trees"]), "bridges": len(scene["bridges"])},
              "native_mountain_pixels_checked": native_mountain_pixels, "mountain_leak_samples": mountain_leak_samples,
              "mountain_leak_cells": list(leak_by_cell.values()), "mountain_leak_statistics": leak_statistics,
              "leaking_mountain_assets": dict(leaking_assets), "leaking_mountain_flips": dict(leaking_flips),
              "leak_cells_north_middle_south": [leak_bands[i] for i in range(3)],
              "source_files_hashed": len(hashes), "minimum_building_source_pixels_per_screen_pixel": minimum_sampling,
              "bridges": bridge_proofs, "production_files_modified": False}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "geometry_runtime_report_python.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"GEOMETRY_PYTHON checks={checks} failures={len(failures)} native_pixels={native_mountain_pixels} seconds={result['elapsed_seconds']:.2f}")
    for label in failures[:12]:
        print("FAIL", label)
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__":
    main()
