"""Read-only geography evidence for restoring the original yellow/green map.

Only writes review JSON/CSV and labelled diagnostic crops. Never changes art,
navigation, anchors, routes, game logic, or saves. Painted colours are evidence,
not gameplay terrain truth; existing authored crossing corridors are reported.
"""
from __future__ import annotations

import csv
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "textures/world/bai_chuan/acceptance_20261030"
ART = BASE / "master_3762.png"
LAYOUT = ROOT / "resources/world/bai_chuan/free_layout_3x3_20261104.json"
NAV = ROOT / "resources/world/bai_chuan/free_navigation_3x3_20261104.json"
OLD_LAYOUT = BASE / "display_layout_v3.json"
OLD_REVIEW = BASE / "route_terrain_review_v3/terrain_metrics.json"
PLAN = ROOT / "docs/prototypes/百川宗门城镇资源规划数据_20261027.json"
OUT = ROOT / "output/bai_chuan_original_ink_20261104/geography_review"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def font(size):
    return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", size)


def sample_polyline(points, step=1.0):
    points = np.asarray(points, dtype=float)
    lengths = np.r_[0.0, np.linalg.norm(np.diff(points, axis=0), axis=1).cumsum()]
    if lengths[-1] <= 0:
        return points[:1], 0.0, 0.0
    distances = np.linspace(0, lengths[-1], max(2, math.ceil(lengths[-1] / step) + 1))
    samples = np.column_stack([np.interp(distances, lengths, points[:, axis]) for axis in range(2)])
    return samples, float(lengths[-1]), float(lengths[-1] / (len(samples) - 1))


def longest(mask, spacing, samples):
    changes = np.diff(np.r_[False, mask, False].astype(np.int8))
    starts, ends = np.flatnonzero(changes == 1), np.flatnonzero(changes == -1)
    if not len(starts):
        return 0.0, samples[len(samples) // 2].tolist()
    index = int(np.argmax(ends - starts))
    start, end = starts[index], ends[index]
    return float((end - start) * spacing), samples[start:end].mean(axis=0).tolist()


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    inputs = [ART, LAYOUT, NAV, OLD_LAYOUT, OLD_REVIEW, PLAN]
    hashes_before = {str(p.relative_to(ROOT)).replace("\\", "/"): digest(p) for p in inputs}
    layout, nav, source, old_review, plan = map(read, [LAYOUT, NAV, OLD_LAYOUT, OLD_REVIEW, PLAN])
    names = {node["id"]: node.get("name", node["id"]) for node in plan["nodes"]}
    image = Image.open(ART).convert("RGB")
    rgb = np.asarray(image).astype(np.int16)
    size, step, columns = image.width, int(nav["source_cell_size"]), nav["columns"]
    factor = layout["coordinateScale"]
    assert image.size == (3762, 3762) and layout["worldSize"] == 7372.8 and nav["world_km"] == 8000
    grid = np.asarray([[int(cell) for cell in row] for row in nav["rows"]], np.uint8)
    r, g, b = rgb.transpose(2, 0, 1)
    water = (b > r * 1.15) & (b > g * 1.04)
    water_absolute = (b - r >= 20) & (b - g >= 7) & (rgb.mean(2) >= 108)
    # Same shadow proxy as the old route review. It also includes dark trees;
    # it is deliberately never described as a mountain collision mask.
    dark_proxy = (rgb.max(2) - rgb.min(2) < 44) & (rgb.mean(2) < 160) & ~water
    cool_rock_proxy = dark_proxy & (b >= g - 12) & (b >= r - 12)
    padding = ((0, columns * step - size), (0, columns * step - size))

    def cell_sum(mask):
        return np.pad(mask.astype(np.float32), padding).reshape(columns, step, columns, step).sum(axis=(1, 3))

    valid_pixels = cell_sum(np.ones(water.shape, dtype=bool))
    wet_pixels = cell_sum(water)
    wet_fraction = wet_pixels / valid_pixels
    absolute_fraction = cell_sum(water_absolute) / valid_pixels
    cool_rock_fraction = cell_sum(cool_rock_proxy) / valid_pixels
    walkable = grid == 0
    positions = {key: np.asarray(value["drawPos"]) / factor for key, value in layout["locations"].items()}
    transits = {key: np.asarray(value) / factor for key, value in layout["transitPositions"].items()}
    routes = {key: np.asarray(points) / factor for key, points in layout["displayEdges"].items()}

    # Replay only the existing bake's crossing corridor for review annotation.
    # This image is not fed back to navigation or any production resource.
    road_image = Image.new("L", (size, size))
    road_draw = ImageDraw.Draw(road_image)
    grid_image = Image.new("L", (columns, columns))
    grid_draw = ImageDraw.Draw(grid_image)
    for points in routes.values():
        road_draw.line([tuple(point) for point in points], fill=255, width=18, joint="curve")
        grid_draw.line([tuple(point / step) for point in points], fill=255, width=3, joint="curve")
    for x, y in {**positions, **transits}.values():
        road_draw.ellipse((x - 13, y - 13, x + 13, y + 13), fill=255)
    corridor = (cell_sum(np.asarray(road_image) > 0) > 0) | (np.asarray(grid_image) > 0)
    for points in routes.values():
        for a, z in zip(points, points[1:]):
            count = max(1, math.ceil(np.linalg.norm(z - a) / (step * 0.125)))
            previous = np.floor(a / step).astype(int)
            for index in range(count + 1):
                current = np.floor((a + (z - a) * index / count) / step).astype(int)
                x, y = current
                corridor[y, x] = True
                if current[0] != previous[0] and current[1] != previous[1]:
                    corridor[current[1], previous[0]] = True
                    corridor[previous[1], current[0]] = True
                previous = current

    dry_distance = ndimage.distance_transform_edt(~water)
    wet_distance = ndimage.distance_transform_edt(water)
    anchor_rows = []
    for key, point in positions.items():
        x, y = np.round(point).astype(int)
        row = {"id": key, "name": names.get(key, key), "source_xy": point.tolist(),
               "world_xy": layout["locations"][key]["drawPos"], "rgb": rgb[y, x].tolist(),
               "on_old_water": bool(water[y, x]), "dark_tree_or_rock_proxy": bool(dark_proxy[y, x]),
               "distance_to_old_water_source_px": float(dry_distance[y, x]),
               "current_navigation_type": int(grid[int(point[1] // step), int(point[0] // step)])}
        for radius in (7, 14):
            x0, x1 = max(0, x - radius), min(size, x + radius + 1)
            y0, y1 = max(0, y - radius), min(size, y + radius + 1)
            yy, xx = np.mgrid[y0:y1, x0:x1]
            disk = (xx - point[0]) ** 2 + (yy - point[1]) ** 2 <= radius ** 2
            row[f"water_fraction_radius_{radius}_source_px"] = float(water[y0:y1, x0:x1][disk].mean())
        anchor_rows.append(row)

    # Match the original review's coastal-water convention to identify any
    # newly introduced route in the open sea separately from river crossings.
    land_labels, _ = ndimage.label(~water)
    land_sizes = np.bincount(land_labels.ravel())
    major_land = land_sizes > 80000
    major_land[0] = False
    mainland = major_land[land_labels]
    shore = np.array([np.flatnonzero(row)[-1] if row.any() else int(size * 0.87) for row in mainland])
    shore = ndimage.median_filter(shore, size=35)
    sea = water & (np.arange(size)[None, :] > shore[:, None])
    route_rows = []
    for key, points in routes.items():
        samples, length, spacing = sample_polyline(points)
        xy = np.clip(np.round(samples).astype(int), 0, size - 1)
        row = {"edge": key, "length_source_px": length}
        for label, mask in (("water", water), ("sea", sea), ("dark_proxy", dark_proxy)):
            hit = mask[xy[:, 1], xy[:, 0]]
            run, center = longest(hit, spacing, samples)
            row.update({f"{label}_fraction": float(hit.mean()), f"{label}_longest_run_source_px": run,
                        f"{label}_worst_center_source_xy": center})
        route_rows.append(row)
    total_length = sum(row["length_source_px"] for row in route_rows)
    water_walk = walkable & (wet_fraction >= 0.5)
    outside = water_walk & ~corridor
    components, component_count = ndimage.label(outside)
    component_rows = []
    for label, slices in enumerate(ndimage.find_objects(components), start=1):
        if slices is None:
            continue
        yy, xx = slices
        ys, xs = np.nonzero(components[yy, xx] == label)
        ys, xs = ys + yy.start, xs + xx.start
        cx, cy = (xs.mean() + 0.5) * step, (ys.mean() + 0.5) * step
        nearby = sorted(((float(np.linalg.norm(point - [cx, cy])), key) for key, point in positions.items()))[:3]
        component_rows.append({"cells": int(len(xs)), "source_rect": [xx.start * step, yy.start * step, min(size, xx.stop * step), min(size, yy.stop * step)],
                               "source_center_xy": [float(cx), float(cy)], "world_center_xy": [float(cx * factor), float(cy * factor)],
                               "nearest_places": [{"id": key, "name": names.get(key, key), "distance_source_px": distance} for distance, key in nearby]})
    component_rows.sort(key=lambda row: -row["cells"])
    outside_y, outside_x = np.nonzero(outside)
    outside_depth = wet_distance[np.minimum(size - 1, outside_y * step + step // 2),
                                 np.minimum(size - 1, outside_x * step + step // 2)]
    cool_rock_walk = walkable & (cool_rock_fraction >= 0.5)
    rock_labels, _ = ndimage.label(cool_rock_walk)
    rock_regions = []
    for label, slices in enumerate(ndimage.find_objects(rock_labels), start=1):
        if slices is None:
            continue
        yy, xx = slices
        ys, xs = np.nonzero(rock_labels[yy, xx] == label)
        ys, xs = ys + yy.start, xs + xx.start
        cx, cy = (xs.mean() + 0.5) * step, (ys.mean() + 0.5) * step
        rock_regions.append({"cells": int(len(xs)),
                             "source_rect": [xx.start * step, yy.start * step, min(size, xx.stop * step), min(size, yy.stop * step)],
                             "source_center_xy": [float(cx), float(cy)],
                             "world_center_xy": [float(cx * factor), float(cy * factor)],
                             "inside_existing_corridor_cells": int(corridor[ys, xs].sum())})
    rock_regions.sort(key=lambda row: -row["cells"])

    # Raw and annotated crops are paired so a reviewer can inspect real pixels
    # without mistaking diagnostic dots or paths for an edited source map.
    crop_records = []
    jobs = [(f"water_outside_corridor_{i+1:02}", row["source_center_xy"], row) for i, row in enumerate(component_rows[:5])]
    jobs += [(f"longest_existing_water_crossing_{i+1:02}", row["water_worst_center_source_xy"], {"edge": row["edge"], "run_source_px": row["water_longest_run_source_px"]})
             for i, row in enumerate(sorted(route_rows, key=lambda row: -row["water_longest_run_source_px"])[:3])]
    jobs += [(f"anchor_shadow_proxy_{row['id']}", row["source_xy"], row) for row in anchor_rows if row["dark_tree_or_rock_proxy"]]
    jobs += [(f"adjusted_anchor_{move['id']}", move["to"], move) for move in layout["anchorAdjustments"]]
    jobs += [(f"cool_rock_proxy_walkable_{i+1:02}", row["source_center_xy"], row) for i, row in enumerate(rock_regions[:4])]
    for job_id, center, evidence in jobs:
        cx, cy = center
        crop_size = 480
        left = max(0, min(size - crop_size, int(cx) - crop_size // 2))
        top = max(0, min(size - crop_size, int(cy) - crop_size // 2))
        box = [left, top, left + crop_size, top + crop_size]
        raw = image.crop(box)
        raw.save(OUT / f"{job_id}_raw.png")
        annotated = raw.convert("RGBA")
        overlay = Image.new("RGBA", annotated.size)
        draw = ImageDraw.Draw(overlay)
        gx0, gx1 = left // step, min(columns, math.ceil((left + crop_size) / step))
        gy0, gy1 = top // step, min(columns, math.ceil((top + crop_size) / step))
        for gy in range(gy0, gy1):
            for gx in range(gx0, gx1):
                if walkable[gy, gx] and wet_fraction[gy, gx] >= 0.5:
                    colour = (232, 39, 38, 145) if not corridor[gy, gx] else (255, 150, 30, 110)
                    draw.rectangle((gx * step - left, gy * step - top, (gx + 1) * step - left - 1, (gy + 1) * step - top - 1), fill=colour)
                elif job_id.startswith("cool_rock_proxy_walkable") and cool_rock_walk[gy, gx]:
                    colour = (210, 55, 170, 130) if not corridor[gy, gx] else (255, 170, 40, 120)
                    draw.rectangle((gx * step - left, gy * step - top, (gx + 1) * step - left - 1, (gy + 1) * step - top - 1), fill=colour)
        for key, points in routes.items():
            if ((points[:, 0] >= left - 100) & (points[:, 0] <= left + crop_size + 100) & (points[:, 1] >= top - 100) & (points[:, 1] <= top + crop_size + 100)).any():
                p = [(float(x - left), float(y - top)) for x, y in points]
                draw.line(p, fill=(255, 255, 230, 230), width=4)
                draw.line(p, fill=(45, 35, 25, 240), width=2)
        for key, (x, y) in positions.items():
            if left <= x <= left + crop_size and top <= y <= top + crop_size:
                x, y = x - left, y - top
                draw.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(255, 240, 185, 255), outline=(20, 20, 20, 255), width=2)
                draw.text((x + 8, y - 10), f"{names.get(key, key)} {key[-3:]}", font=font(14), fill=(20, 20, 20, 255), stroke_width=1, stroke_fill=(255, 244, 205, 255))
        annotated = Image.alpha_composite(annotated, overlay).convert("RGB")
        annotated.save(OUT / f"{job_id}_annotated.png")
        crop_records.append({"id": job_id, "source_crop_rect": box, "raw": str((OUT / f"{job_id}_raw.png").relative_to(ROOT)).replace("\\", "/"), "annotated": str((OUT / f"{job_id}_annotated.png").relative_to(ROOT)).replace("\\", "/"), "evidence": evidence})

    def overlap_stats(mask):
        return {"all_walkable": int((mask & walkable).sum()),
                "inside_existing_corridors": int((mask & walkable & corridor).sum()),
                "outside_existing_corridors": int((mask & walkable & ~corridor).sum())}

    current_routes_equal_source = []
    for key, points in routes.items():
        original = np.asarray(source["displayEdges"][key])
        current_routes_equal_source.append(float(np.max(np.abs(points[1:-1] - original[1:-1]))) if len(points) > 2 else 0.0)
    old_water_pixels_by_nav = {str(kind): int(wet_pixels[grid == kind].sum()) for kind in (0, 1, 2)}
    report = {
        "role": "Read-only original-art restoration evidence; no changes to game terrain or save data.",
        "inputs_sha256": hashes_before,
        "scale": {"original_pixels": size, "world_size": layout["worldSize"], "world_km": nav["world_km"], "columns": columns, "source_cell_size": step, "coordinate_factor": factor},
        "counts": {"named_places": len(positions), "transit_points": len(transits), "route_edges": len(routes)},
        "original_water": {"definition": "Original route review: B>1.15R and B>1.04G, at native pixels.", "pixels": int(water.sum()), "fraction": float(water.mean()),
                           "absolute_threshold_fraction": float(water_absolute.mean()), "threshold_disagreement_pixels": int((water != water_absolute).sum()),
                           "water_pixels_by_current_navigation_type": old_water_pixels_by_nav,
                           "water_pixels_currently_blocked_fraction": float((wet_pixels[grid != 0].sum()) / wet_pixels.sum())},
        "navigation": {"counts": {str(kind): int((grid == kind).sum()) for kind in (0, 1, 2)},
                       "old_water_any": overlap_stats(wet_fraction > 0), "old_water_majority": overlap_stats(wet_fraction >= 0.5),
                       "old_water_core_90_percent": overlap_stats(wet_fraction >= 0.9), "absolute_threshold_water_majority": overlap_stats(absolute_fraction >= 0.5),
                       "old_dry_cells_95_percent_blocked": int(((wet_fraction <= 0.05) & ~walkable).sum()),
                       "old_dry_pixels_currently_blocked": int((valid_pixels[~walkable] - wet_pixels[~walkable]).sum()),
                       "outside_majority_water_cell_centers_depth_p0_p50_p90_p95_p100_source_px": np.percentile(outside_depth, [0, 50, 90, 95, 100]).tolist() if outside_depth.size else [],
                       "outside_majority_water_cell_centers_deeper_than_8_source_px": int((outside_depth > 8).sum()),
                       "outside_majority_water_components": len(component_rows), "outside_majority_water_regions": component_rows},
        "anchors": {"centres_on_old_water": sum(row["on_old_water"] for row in anchor_rows),
                    "all_current_navigation_walkable": all(row["current_navigation_type"] == 0 for row in anchor_rows),
                    "within_7_source_px_of_water": sum(row["distance_to_old_water_source_px"] <= 7 for row in anchor_rows),
                    "within_14_source_px_of_water": sum(row["distance_to_old_water_source_px"] <= 14 for row in anchor_rows),
                    "dark_tree_or_rock_proxy_hits": [row for row in anchor_rows if row["dark_tree_or_rock_proxy"]],
                    "centres_on_strict_cool_rock_proxy": sum(bool(cool_rock_proxy[round(row["source_xy"][1]), round(row["source_xy"][0])]) for row in anchor_rows),
                    "rows": anchor_rows, "existing_adjustments_preserved": layout["anchorAdjustments"]},
        "routes": {"length_source_px": total_length,
                   "length_weighted_water_fraction": sum(row["water_fraction"] * row["length_source_px"] for row in route_rows) / total_length,
                   "length_weighted_sea_fraction": sum(row["sea_fraction"] * row["length_source_px"] for row in route_rows) / total_length,
                   "edges_touching_water": sum(row["water_fraction"] > 0 for row in route_rows),
                   "edges_over_80_source_px_water_run": sum(row["water_longest_run_source_px"] > 80 for row in route_rows),
                   "longest_water_run_source_px": max(row["water_longest_run_source_px"] for row in route_rows),
                   "top_water": sorted(route_rows, key=lambda row: -row["water_longest_run_source_px"])[:10],
                   "current_vs_source_route_interior_max_difference_source_px": max(current_routes_equal_source),
                   "old_review_water_fraction": old_review["lengthWeightedFractions"]["water"],
                   "old_review_longest_water_run_source_px": max(row["waterLongestRunPixels"] for row in old_review["allEdges"])},
        "cool_rock_colour_evidence": {"definition": "Old dark proxy additionally B>=G-12 and B>=R-12 to exclude yellow/green trees; still a colour proxy, not authored terrain.",
                                      "original_pixel_fraction": float(cool_rock_proxy.mean()),
                                      "majority_rock_walkable_cells": overlap_stats(cool_rock_fraction >= 0.5),
                                      "majority_rock_blocked_cells": int(((cool_rock_fraction >= 0.5) & ~walkable).sum()),
                                      "largest_walkable_rock_proxy_regions": rock_regions[:20],
                                      "visual_review": "The largest four crop centres contain original painted mountain peaks/faces. No automatic nav correction is justified by this mask."},
        "existing_terrain_evidence": {"old_route_review": str(OLD_REVIEW.relative_to(ROOT)).replace("\\", "/"),
                                      "old_route_review_is_authored_terrain_mask": False,
                                      "current_nav_source": nav["terrain_source"],
                                      "current_review_mask": "output/bai_chuan_free_world_3x3_20261104/terrain_mask_471.png",
                                      "current_review_mask_is_original_map_terrain": False,
                                      "authored_original_0_1_2_mask_found_in_target_catalog": False,
                                      "meaning": "Existing old review is an image-colour diagnostic, current P-mode mask is current navigation after crossing corridors; neither defines original painted mountains."},
        "crops": crop_records,
        "manual_visual_review": ["Largest three outside-water regions are short narrow strips along river banks, not a new whole-river crossing.",
                                 "Anchor 014 is on low foothill/vegetation; 067 and 111 are on forest edges. Their broad dark-proxy hits do not identify blocked peak faces.",
                                 "Largest four cool-rock proxy regions visibly contain painted mountain peaks/faces; current free navigation cannot be certified as matching all original mountains."],
        "minimal_scope_recommendation": ["Keep 7372.8 canvas, 8000 km, 123 place positions, 321 transit positions, 575 routes and all existing navigation rows unchanged for save and gameplay continuity.",
                                         "Replace only the manually restyled original map pixels and exact 3x3 tile exports; any resource provenance update must not regenerate topology or move anchors.",
                                         "Describe preserved navigation as the existing gameplay terrain. Do not claim exact old-bank or old-mountain collision alignment: 141 outside-corridor majority-water cells and cool-rock proxy overlaps remain reviewable visual differences.",
                                         "If exact painted-mountain collision becomes an explicit requirement, it is separate authored terrain work that needs per-region review and old-route/save compatibility checks."],
        "limits": ["Original bright-blue water is strong visual evidence; grey/brown mountain shadows also include forests and are not collision proof.",
                   "Existing route corridors intentionally open short water crossings; counts inside them are not automatically defects.",
                   "Free navigation outside corridors can still differ from old river banks even when all 123 places and 575 routes remain usable.",
                   "No runtime Main scene was launched and no old save was rewritten by this read-only audit."],
    }
    hashes_after = {str(p.relative_to(ROOT)).replace("\\", "/"): digest(p) for p in inputs}
    report["input_files_unchanged"] = hashes_after == hashes_before
    if not report["input_files_unchanged"]:
        raise RuntimeError("An input changed while auditing; rerun after the concurrent write finishes.")
    (OUT / "original_geography_audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for filename, rows in (("anchors.csv", anchor_rows), ("route_water.csv", route_rows)):
        with (OUT / filename).open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)
    Image.fromarray(water.astype(np.uint8) * 255).save(OUT / "original_water_colour_evidence.png")
    diagnostic = np.zeros((columns, columns, 3), np.uint8) + [225, 213, 180]
    diagnostic[grid == 1] = [95, 95, 84]
    diagnostic[grid == 2] = [59, 105, 127]
    diagnostic[water_walk & corridor] = [245, 160, 35]
    diagnostic[outside] = [220, 38, 35]
    Image.fromarray(diagnostic.astype(np.uint8)).resize((1413, 1413), Image.Resampling.NEAREST).save(OUT / "current_navigation_original_water_diagnostic.png")
    summary = {"counts": report["counts"], "water": report["original_water"],
               "navigation": {k: v for k, v in report["navigation"].items() if k != "outside_majority_water_regions"},
               "anchor_centres_on_water": report["anchors"]["centres_on_old_water"],
               "cool_rock_majority_walkable_cells": report["cool_rock_colour_evidence"]["majority_rock_walkable_cells"],
               "water_routes_fraction": report["routes"]["length_weighted_water_fraction"],
               "longest_route_water_run_source_px": report["routes"]["longest_water_run_source_px"],
               "sea_route_fraction": report["routes"]["length_weighted_sea_fraction"],
               "input_files_unchanged": report["input_files_unchanged"], "output": str(OUT)}
    (OUT / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
