"""Read-only restoration audit. Writes only independent QA reports/fixtures."""
from pathlib import Path
import hashlib
import json
import math
import re
import time
from collections import defaultdict

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "output/bai_chuan_original_grounding_20261109"
RES = "res://resources/world/bai_chuan/"
checks, failures = 0, []


def path(p): return ROOT / str(p).removeprefix("res://")
def read(p): return json.loads(path(p).read_text("utf-8"))
def sha(p): return hashlib.sha256(path(p).read_bytes()).hexdigest()
def grid(rows): return np.array([np.frombuffer(r.encode(), np.uint8) - 48 for r in rows])


def check(ok, label, **detail):
    global checks
    checks += 1
    if not ok: failures.append({"message": label, "detail": detail})


def intersects(a, b):
    return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]


def expanded(a, n): return [a[0] - n, a[1] - n, a[2] + n * 2, a[3] + n * 2]


def portion(a, bounds, cell):
    x, y, w, h = bounds
    return a[max(0, math.floor(y / cell)):min(a.shape[0], math.ceil((y + h) / cell)),
             max(0, math.floor(x / cell)):min(a.shape[1], math.ceil((x + w) / cell))]


def main():
    began = time.perf_counter()
    source = path("gdscripts/world/BaiChuanWorldScale.gd").read_text("utf-8")
    conf = lambda k: re.search(rf'^const {k} := "([^"]+)"', source, re.M).group(1)
    expected = {"NAVIGATION_PATH": RES + "free_navigation_expanded3x_20261106.json",
                "NAVIGATION_PROFILE": "expanded3x_20261106_v1", "TERRAIN_PATH": RES + "terrain_travel_expanded3x_20261106.json",
                "LAYOUT_PATH": RES + "free_layout_expanded3x_20261106.json", "LANDSCAPE_PATH": RES + "landscape_original_grounded_20261109.json"}
    for k, v in expected.items(): check(conf(k) == v, "active original source restored", field=k)
    scene, nav, terrain, layout = [read(conf(k)) for k in ("LANDSCAPE_PATH", "NAVIGATION_PATH", "TERRAIN_PATH", "LAYOUT_PATH")]
    original, compact = [read(RES + n) for n in ("landscape_expanded3x_20261106.json", "landscape_compact_peaks_20261109.json")]
    old_patch = read(RES + "forest_grounding_sample_20261106.json")
    patch = read(scene["forest_grounding_patch_path"])
    floor = read(patch["ground_details_path"])
    ground = read(scene["mountain_ground_details_path"])
    old_floor = read(old_patch["ground_details_path"])
    files = [conf(k) for k in ("LANDSCAPE_PATH", "NAVIGATION_PATH", "TERRAIN_PATH", "LAYOUT_PATH")] + [scene["forest_grounding_patch_path"], patch["ground_details_path"], scene["mountain_ground_details_path"]]
    hashes = {p: sha(p) for p in files}
    for p, h in old_patch["source_sha256"].items(): check(sha(p) == h, "immutable original source SHA256", path=p)
    check(len(original["mountains"]) == len(scene["mountains"]) == 429, "all original 429 mountains restored")
    for current, old in zip(scene["mountains"], original["mountains"]): check(current == old, "entire mountain row exactly matches original", id=old["id"])
    for k in ("mountain_catalog", "mountain_sources", "trees", "tree_catalog", "tree_atlas", "world_size", "world_km", "ground_texture", "ground_tone_data", "water_distance", "water_texture"):
        check(scene[k] == original[k], "original scenery data preserved exactly", field=k)
    for k in ("bridges", "bridge_atlas", "bridge_catalog"): check(scene[k] == compact[k], "new top-down bridge data exactly preserved", field=k)
    history = read("res://output/bai_chuan_compact_peaks_20261109/geometry_independent_report.json")
    check(sha(scene["bridge_atlas"]) == history["source_sha256"][scene["bridge_atlas"]], "top-down bridge native image is unchanged since independent bridge audit")
    check(len(layout["locations"]) == 123 and len(layout["displayEdges"]) == 575 and len(layout["transitPositions"]) == 321, "original 123 places / 444 anchors / 575 curves")
    check(nav["navigation_profile"] == expected["NAVIGATION_PROFILE"] and nav["columns"] == 1413 and terrain["columns"] == 471, "original active collision/terrain profile and resolutions")
    for k in ("trees", "removed_tree_ids", "bounds", "grounded_mountain_ids", "source_sha256", "tree_catalog", "tree_sources"):
        check(patch[k] == old_patch[k], "forest sample remains exactly original", field=k)
    retained_floor = [r for r in old_floor["decorations"] if r["habitat"] != "mountain_apron"]
    check(len(retained_floor) == 344 and floor["decorations"] == retained_floor, "344 forest floor/root decals preserved exactly")
    g = grid(nav["rows"])
    removed = set(patch["removed_tree_ids"])
    trees = [t for t in scene["trees"] if t["id"] not in removed] + patch["trees"]
    check(len(trees) == 5490, "all 5490 active original trees remain")
    old_water_crowns = 0
    for t in trees:
        x, y = math.floor(t["x"] / nav["cell_size"]), math.floor(t["y"] / nav["cell_size"])
        box = portion(g, t["bounds"], nav["cell_size"])
        valid = g[y, x] == 0 and not np.any(box == 1)
        if t["id"].startswith("forest_grounding_tree_"): valid = valid and not np.any(box != 0)
        if np.any(box == 2): old_water_crowns += 1
        check(valid, "tree foot remains walkable and full crown does not cross original mountains", id=t["id"])
    check(old_water_crowns == 54, "only the original 54 old riverside crowns retain water overlap")

    images = {bank: np.asarray(Image.open(path(v["path"])).convert("RGBA")) for bank, v in scene["mountain_sources"].items()}
    alpha = {}
    for ident, asset in scene["mountain_catalog"].items():
        x, y, w, h = asset["region"]
        alpha[ident] = images[asset["bank"]][y:y + h, x:x + w, 3]
    mountains = {m["id"]: m for m in scene["mountains"]}
    groups = defaultdict(list)
    bottoms = {}
    for m in scene["mountains"]:
        a = alpha[m["asset"]]
        if m["flip"]: a = a[:, ::-1]
        visible = a >= 46
        bottom = np.where(visible.any(0), a.shape[0] - 1 - np.argmax(visible[::-1], axis=0), -1)
        bottoms[m["id"]] = (a, bottom)
        groups[m["source_region"]].append(m)

    def lowest_at(m, wx):
        x, y, w, h = m["bounds"]
        a, bottom = bottoms[m["id"]]
        column = math.floor((wx - x) * a.shape[1] / w)
        if column < 0 or column >= a.shape[1] or bottom[column] < 0: return -math.inf
        return y + (bottom[column] + .5) * h / a.shape[0]

    def covers(m, point):
        x, y, w, h = m["bounds"]
        a = bottoms[m["id"]][0]
        sx, sy = math.floor((point[0] - x) * a.shape[1] / w), math.floor((point[1] - y) * a.shape[0] / h)
        return 0 <= sx < a.shape[1] and 0 <= sy < a.shape[0] and a[sy, sx] >= 46

    water = read(RES + "expanded_water_20261106.json")
    wet = grid(water["water_rows"]) == 2
    sdf = np.asarray(Image.open(path(water["water_sdf"])).convert("L")) < 128
    obstacles = [expanded([r["rect"][0], r["rect"][1], r["rect"][2] - r["rect"][0], r["rect"][3] - r["rect"][1]], 45) for r in layout["locations"].values()]
    obstacles += [expanded(b["bounds"], 30) for b in scene["bridges"]]
    for d in ground["decorations"]:
        m, anchor, box = mountains[d["mountain_id"]], d["contour_anchor"], d["bounds"]
        edge_y = lowest_at(m, anchor[0])
        check(abs(edge_y - anchor[1]) < .1, "decal anchor is the original native alpha column's true lower edge", id=d["id"])
        covered = [p["id"] for p in scene["mountains"] if p["z"] > m["z"] and covers(p, anchor)]
        lower = [p["id"] for p in groups[m["source_region"]] if lowest_at(p, anchor[0]) > anchor[1] + 4.00001]
        check(not covered and not lower, "decal avoids covered back-mountain feet and the interior of its mountain group", id=d["id"], covering=covered, lower_envelope=lower)
        check(box[0] >= 0 and box[1] >= 0 and box[0] + box[2] <= nav["world_size"] and box[1] + box[3] <= nav["world_size"] and not np.any(portion(wet, box, nav["cell_size"])) and not np.any(portion(sdf, box, nav["world_size"] / sdf.shape[1])) and not any(intersects(box, obstacle) for obstacle in obstacles), "ground whole bbox avoids actual water, 123 entrances and nine top-down bridges", id=d["id"])
        check(d["z"] == 24 and d["position"] == [d["x"], d["y"]] and d["layer"] == "ground_decals" and box[1] < anchor[1] < box[1] + box[3], "ground decal retains explicit XYZ24 and straddles the real foot", id=d["id"])

    # Independent source-derived migration fixtures: all points on one compact
    # legal row, with a real restored old mountain between common legal ends.
    compact_nav = read(RES + "free_navigation_compact_peaks_20261109.json")
    c = grid(compact_nav["rows"])
    corridor = None
    for y in range(80, g.shape[0] - 80):
        transitions = np.diff(np.r_[False, (g[y] == 1) & (c[y] == 0), False].astype(np.int8))
        for a, b in zip(np.flatnonzero(transitions == 1), np.flatnonzero(transitions == -1)):
            if b - a < 5 or a < 5 or b + 5 >= g.shape[1]: continue
            left, right = a - 2, b + 1
            if g[y, left] != 0 or g[y, right] != 0 or not np.all(c[y, left:right + 1] == 0): continue
            coords = lambda x: [(float(x) + .5) * nav["cell_size"], (float(y) + .5) * nav["cell_size"]]
            corridor = {"start": coords(left), "blocked": coords((a + b) // 2), "end": coords(right), "blocked_cells_in_line": int(b - a)}
            break
        if corridor: break
    check(corridor is not None, "real compact-open / restored-mountain route fixture exists")
    blocked_source = np.argwhere(c == 1)[len(np.argwhere(c == 1)) // 2]
    fixtures = {"corridor": corridor, "source_illegal": [(float(blocked_source[1]) + .5) * nav["cell_size"], (float(blocked_source[0]) + .5) * nav["cell_size"]]}
    for p, h in hashes.items(): check(sha(p) == h, "QA does not modify production source bytes", path=p)
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "migration_fixtures.json").write_text(json.dumps(fixtures, ensure_ascii=False, indent=2) + "\n", "utf-8")
    report = {"checks": checks, "failures": failures, "elapsed_seconds": time.perf_counter() - began, "source_sha256": hashes,
              "original_mountains": len(scene["mountains"]), "active_trees": len(trees), "forest_ground_decals": len(retained_floor),
              "mountain_ground_decals": len(ground["decorations"]), "inherited_water_crowns": old_water_crowns,
              "bridges": len(scene["bridges"]), "immutable_original_sources": len(old_patch["source_sha256"]), "production_files_modified": False}
    (OUT / "geometry_independent_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", "utf-8")
    print(f"ORIGINAL_GROUNDING_GEOMETRY checks={checks} failures={len(failures)} seconds={report['elapsed_seconds']:.2f}")
    for f in failures[:15]: print("FAIL", json.dumps(f, ensure_ascii=False))
    raise SystemExit(1 if failures else 0)


if __name__ == "__main__": main()
