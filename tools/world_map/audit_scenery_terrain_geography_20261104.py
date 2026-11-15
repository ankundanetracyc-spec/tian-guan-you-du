"""Read-only art/geography review against the existing 123 anchors and nav grid.

Creates diagnostic annotations and exact local crops, never edited terrain art.
Image colours are uncertain evidence and do not define gameplay passability.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
BASELINE = ROOT / "textures/world/bai_chuan/free_world_8000_3x3_20261104/master_8000.png"
LAYOUT = ROOT / "resources/world/bai_chuan/free_layout_3x3_20261104.json"
NAVIGATION = ROOT / "resources/world/bai_chuan/free_navigation_3x3_20261104.json"
PLAN = ROOT / "docs/prototypes/百川宗门城镇资源规划数据_20261027.json"
MEASURE_SIZE = 2048


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def label(path):
    path = Path(path).resolve()
    return (str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path)).replace("\\", "/")


def font(size):
    for path in (Path("C:/Windows/Fonts/msyh.ttc"), Path("C:/Windows/Fonts/simhei.ttf")):
        if path.exists():
            return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def read_measurement(path, water_style="blue", allow_rect=False):
    with Image.open(path) as image:
        if image.width != image.height and not allow_rect:
            raise ValueError(f"A full square world image is required: {path}")
        native = image.size
        rgb = np.asarray(image.convert("RGB").resize((MEASURE_SIZE, MEASURE_SIZE), Image.Resampling.BOX), np.float32)
    r, g, b = np.moveaxis(rgb, -1, 0)
    lum = rgb.mean(axis=2)
    # This repeats the established source-blue evidence, without changing nav.
    water = (b - r >= 20) & (b - g >= 7) & (lum >= 108)
    # Cyan/grey-green ink can also be rock or foliage: this is never promoted
    # silently to definite water. It remains an explicit low-confidence review.
    muted_water = (b - r >= 9) & (g - r >= 8) & (b - g >= -9) & (lum >= 125) & ~water
    if water_style == "gray-teal":
        gray_teal = (g - r >= 9) & (b - r >= -8) & (b - g >= -26) & (lum >= 85) & (lum < 215)
        # This is deliberately labelled low-confidence painting evidence.
        water |= ndimage.binary_fill_holes(ndimage.binary_closing(gray_teal, iterations=1))
        muted_water = (g - r >= 6) & (b - r >= -13) & (b - g >= -33) & (lum >= 80) & (lum < 220) & ~water
    ink_density = ndimage.uniform_filter(((lum < 178) & ~water).astype(np.float32), size=7)
    dark_mass = ndimage.binary_fill_holes(ndimage.binary_closing(ink_density > 0.18, iterations=2))
    return {"native_size": list(native), "water": water, "muted_water": muted_water, "dark_mass": dark_mass}


def audit_crop_jobs(args):
    jobs_path = args.crop_jobs.resolve()
    jobs = json.loads(jobs_path.read_text(encoding="utf-8"))
    records = []
    out = args.output_dir.resolve()
    out.mkdir(parents=True, exist_ok=True)
    selected = set(args.crop_ids or [])
    for job in jobs["jobs"]:
        if selected and job["id"] not in selected:
            continue
        old_path, new_path = Path(job["reference"]), Path(job["native_output"])
        if not new_path.exists():
            continue
        old, new = read_measurement(old_path, "blue", True), read_measurement(new_path, args.water_style, True)
        old_border = old["water"] & ~ndimage.binary_erosion(old["water"])
        new_border = new["water"] & ~ndimage.binary_erosion(new["water"])
        distances = np.concatenate((ndimage.distance_transform_edt(~old_border)[new_border],
                                    ndimage.distance_transform_edt(~new_border)[old_border]))
        native_factor = new["native_size"][0] / MEASURE_SIZE
        source_world_width = (job["source_box"][2] - job["source_box"][0]) / 6270.0 * 7372.8
        percentiles = np.percentile(distances, [50, 90, 95, 99]) if distances.size else np.array([0.0] * 4)
        union = old["water"] | new["water"]
        record = {"id": job["id"], "source_box_native_6270": job["source_box"],
                  "native_candidate_pixels": new["native_size"], "candidate_sha256": sha256(new_path),
                  "reference_sha256": sha256(old_path), "classification": args.water_style,
                  "confidence": "low: grey-teal appearance, not authored water truth" if args.water_style == "gray-teal" else "blue colour heuristic",
                  "old_water_fraction": float(old["water"].mean()), "new_water_fraction": float(new["water"].mean()),
                  "water_intersection_over_union": float((old["water"] & new["water"]).sum() / max(union.sum(), 1)),
                  "shore_distance_percentiles_50_90_95_99_candidate_native_px": (percentiles * native_factor).tolist(),
                  "shore_distance_percentiles_50_90_95_99_world_units": (percentiles * source_world_width / MEASURE_SIZE).tolist(),
                  "interpretation": "Includes bank line thickness, colour inference error and actual silhouette drift; never changes nav."}
        records.append(record)
        with Image.open(old_path) as old_source, Image.open(new_path) as new_source:
            panel = Image.new("RGB", (1536, 560), "#eee6d6")
            panel.paste(old_source.convert("RGB").resize((512, 512), Image.Resampling.LANCZOS), (0, 48))
            panel.paste(new_source.convert("RGB").resize((512, 512), Image.Resampling.LANCZOS), (512, 48))
            diff = np.zeros((MEASURE_SIZE, MEASURE_SIZE, 3), np.uint8)
            diff[old["water"] & new["water"]] = [93, 133, 123]
            diff[new["water"] & ~old["water"]] = [210, 78, 42]
            diff[old["water"] & ~new["water"]] = [52, 139, 216]
            panel.paste(Image.fromarray(diff, "RGB").resize((512, 512), Image.Resampling.BOX), (1024, 48))
            d = ImageDraw.Draw(panel)
            d.text((10, 5), f"{job['id']} old reference | new native | scalar delta: rust=extra water, blue=missing water; LOW CONFIDENCE",
                   fill="#43392d", font=font(19))
            d.text((10, 27), f"IoU {record['water_intersection_over_union']:.3f}; shore median/p95 = {percentiles[0] * native_factor:.1f}/{percentiles[2] * native_factor:.1f} new native px", fill="#43392d", font=font(17))
            panel.save(out / f"{job['id']}_water_comparison.png", optimize=True)
    result = {"version": 1, "role": "Local original/native painting water evidence, not navigation certification.",
              "jobs_sha256": sha256(jobs_path), "records": records,
              "no_source_art_resampled_or_written": True, "no_navigation_or_layout_writes": True}
    (out / "crop_water_audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


def sample_disk(field, point, radius, world_size):
    scale = MEASURE_SIZE / world_size
    cx, cy = np.array(point) * scale
    rad = radius * scale
    x0, x1 = max(0, int(cx - rad)), min(MEASURE_SIZE, int(cx + rad) + 2)
    y0, y1 = max(0, int(cy - rad)), min(MEASURE_SIZE, int(cy + rad) + 2)
    yy, xx = np.mgrid[y0:y1, x0:x1]
    disk = (xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2 <= rad ** 2
    values = field[y0:y1, x0:x1][disk]
    return float(values.mean()) if values.size else 0.0


def sample_rect(field, rect, world_size):
    bounds = np.array(rect) * MEASURE_SIZE / world_size
    x0, y0 = np.clip(np.floor(bounds[:2]).astype(int), 0, MEASURE_SIZE - 1)
    x1, y1 = np.clip(np.ceil(bounds[2:]).astype(int), 1, MEASURE_SIZE)
    values = field[y0:y1, x0:x1]
    return float(values.mean()) if values.size else 0.0


def nav_measure(field, columns):
    return np.asarray(Image.fromarray(field.astype(np.uint8) * 255, "L").resize(
        (columns, columns), Image.Resampling.BOX), np.float32) / 255.0


def native_crop_box(point, radius, native_size, world_size):
    x, y = point
    scale = native_size[0] / world_size
    return [max(0, int((x - radius) * scale)), max(0, int((y - radius) * scale)),
            min(native_size[0], int((x + radius) * scale) + 1),
            min(native_size[1], int((y + radius) * scale) + 1)]


def mismatch_components(mask, world_size, limit=30):
    components, count = ndimage.label(mask)
    regions = []
    for component, slices in enumerate(ndimage.find_objects(components), start=1):
        if slices is None:
            continue
        yy, xx = slices
        count = int(np.count_nonzero(components[yy, xx] == component))
        size = mask.shape[0]
        regions.append({"cell_count": count,
                        "world_rect": [xx.start * world_size / size, yy.start * world_size / size,
                                       xx.stop * world_size / size, yy.stop * world_size / size]})
    return sorted(regions, key=lambda record: -record["cell_count"])[:limit]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--art", type=Path, help="Candidate full square world painting.")
    parser.add_argument("--baseline", type=Path, default=BASELINE)
    parser.add_argument("--layout", type=Path, default=LAYOUT)
    parser.add_argument("--navigation", type=Path, default=NAVIGATION)
    parser.add_argument("--water-mask", type=Path, help="Optional explicit candidate water mask, white=water; avoids colour ambiguity.")
    parser.add_argument("--water-style", choices=("blue", "gray-teal"), default="blue",
                        help="gray-teal supports b-g<0 water but is explicitly low-confidence colour inference.")
    parser.add_argument("--crop-jobs", type=Path, help="Audit already generated local native crops from jobs.json instead of a full master.")
    parser.add_argument("--crop-ids", nargs="*", help="Limit --crop-jobs to named IDs such as r0c0 r2c0.")
    parser.add_argument("--candidate-manifest", type=Path,
                        help="Optional draft assembly manifest; inspect only source_records with draft_fallback=false.")
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--max-crops", type=int, default=24)
    parser.add_argument("--crop-radius", type=float, default=170.0, help="World-unit half-width of repair-reference crops.")
    args = parser.parse_args()
    if args.crop_jobs:
        audit_crop_jobs(args)
        return
    if not args.art:
        parser.error("--art or --crop-jobs is required")
    out = args.output_dir.resolve()
    out.mkdir(parents=True, exist_ok=True)
    paths = [args.art, args.baseline, args.layout, args.navigation, PLAN]
    if args.water_mask:
        paths.append(args.water_mask)
    if args.candidate_manifest:
        paths.append(args.candidate_manifest)
    input_hashes = {label(path): sha256(path) for path in paths}
    layout = json.loads(args.layout.read_text(encoding="utf-8"))
    nav = json.loads(args.navigation.read_text(encoding="utf-8"))
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    names = {node["id"]: node for node in plan["nodes"]}
    world_size = float(layout["worldSize"])
    assert len(layout["locations"]) == 123 and world_size == 7372.8
    assert nav["world_size"] == world_size and nav["world_km"] == 8000.0
    before, after = read_measurement(args.baseline), read_measurement(args.art, args.water_style)
    water_mode = "native_source_blue_heuristic" if args.water_style == "blue" else "gray_teal_painting_inference_low_confidence"
    scope = np.ones((MEASURE_SIZE, MEASURE_SIZE), bool)
    scope_ids = []
    if args.candidate_manifest:
        assembly = json.loads(args.candidate_manifest.read_text(encoding="utf-8"))
        rows, cols = assembly["source_detail_grid"]
        scope[:] = False
        for record in assembly["source_records"]:
            if record.get("draft_fallback", False):
                continue
            ident = record["id"]
            row, column = [int(number) for number in ident[1:].split("c")]
            scope[int(row * MEASURE_SIZE / rows):int((row + 1) * MEASURE_SIZE / rows),
                  int(column * MEASURE_SIZE / cols):int((column + 1) * MEASURE_SIZE / cols)] = True
            scope_ids.append(ident)
    if args.water_mask:
        with Image.open(args.water_mask) as image:
            after["water"] = np.asarray(image.convert("L").resize(
                (MEASURE_SIZE, MEASURE_SIZE), Image.Resampling.BOX)) >= 128
        after["muted_water"] = np.zeros((MEASURE_SIZE, MEASURE_SIZE), bool)
        water_mode = "explicit_candidate_water_mask"
    added_water = after["water"] & ~before["water"] & scope
    removed_water = before["water"] & ~after["water"] & scope
    added_ink = after["dark_mass"] & ~before["dark_mass"] & scope
    # Grey-green evidence is only interesting near the established water band.
    baseline_distance = ndimage.distance_transform_edt(~before["water"]) * world_size / MEASURE_SIZE
    uncertain_added = after["muted_water"] & ~before["muted_water"] & (baseline_distance < 140.0) & scope
    distance = ndimage.distance_transform_edt(~after["water"]) * world_size / MEASURE_SIZE
    grid = np.array([[int(char) for char in row] for row in nav["rows"]], dtype=np.uint8)
    columns = int(nav["columns"])
    added_walkable = (nav_measure(after["water"], columns) > 0.35) & (nav_measure(before["water"], columns) <= 0.35) & (grid == 0) & (nav_measure(scope, columns) > 0.5)
    ink_walkable = (nav_measure(added_ink, columns) > 0.45) & (grid == 0)
    records = []
    for ident, place in layout["locations"].items():
        node = names[ident]
        point = place["drawPos"]
        x, y = np.clip((np.array(point) * MEASURE_SIZE / world_size).astype(int), 0, MEASURE_SIZE - 1)
        gx, gy = np.clip((np.array(point) / nav["cell_size"]).astype(int), 0, columns - 1)
        rect = place.get("rect", [point[0] - 25, point[1] - 35, point[0] + 25, point[1]])
        old_disk = sample_disk(before["water"], point, 28.0, world_size)
        new_disk = sample_disk(after["water"], point, 28.0, world_size)
        added_disk = sample_disk(added_water, point, 28.0, world_size)
        added_rect = sample_rect(added_water, rect, world_size)
        uncertain = sample_disk(uncertain_added, point, 38.0, world_size)
        ink_added = sample_disk(added_ink, point, 38.0, world_size)
        flags = []
        if (scope[y, x] and after["water"][y, x] and not before["water"][y, x]) or added_disk > 0.12 or added_rect > 0.20:
            flags.append("introduced_water_at_entrance_or_icon")
        if scope[y, x] and old_disk > 0.20 and new_disk > 0.20:
            flags.append("existing_water_colour_evidence")
        if uncertain > 0.18:
            flags.append("muted_water_colour_needs_visual_review")
        if ink_added > 0.28:
            flags.append("new_dark_mass_needs_visual_review")
        severity = 100.0 * added_disk + 70.0 * added_rect + 35.0 * uncertain + 18.0 * ink_added
        if "introduced_water_at_entrance_or_icon" in flags:
            severity += 100.0
        records.append({"id": ident, "name": node["name"], "type": node.get("type"),
                        "world_point": point, "world_icon_rect": rect, "nav_cell": [int(gx), int(gy)],
                        "candidate_scope": "generated_or_full" if scope[y, x] else "draft_fallback_excluded",
                        "existing_nav_value": int(grid[gy, gx]), "flags": flags, "review_score": round(severity, 4),
                        "old_entrance_water_fraction": old_disk, "new_entrance_water_fraction": new_disk,
                        "added_entrance_water_fraction": added_disk, "added_icon_water_fraction": added_rect,
                        "muted_colour_review_fraction": uncertain, "added_dark_mass_fraction": ink_added,
                        "nearest_detected_new_water_world_units": float(distance[y, x]),
                        "candidate_repair_crop_native_px": native_crop_box(point, args.crop_radius, after["native_size"], world_size),
                        "baseline_reference_crop_native_px": native_crop_box(point, args.crop_radius, before["native_size"], world_size)})
    records.sort(key=lambda record: (-record["review_score"], record["id"]))
    review = [record for record in records if record["flags"]]
    with Image.open(args.art) as source:
        overview = source.convert("RGB").resize((2048, 2048), Image.Resampling.BOX)
    drawing = ImageDraw.Draw(overview)
    map_font = font(16)
    for record in records:
        x, y = np.array(record["world_point"]) * 2048 / world_size
        colour = "#b42f21" if "introduced_water_at_entrance_or_icon" in record["flags"] else "#ba791f" if record["flags"] else "#396a43"
        drawing.ellipse((x - 5, y - 5, x + 5, y + 5), outline=colour, width=2)
        drawing.text((x + 7, y - 14), record["id"].replace("bc_place_", ""), font=map_font, fill=colour,
                     stroke_width=1, stroke_fill="#f3ead8")
    drawing.rectangle((0, 0, 2048, 64), fill="#eee5d1")
    drawing.text((12, 6), "ART REVIEW ONLY — red: new water evidence / amber: uncertain ink or existing evidence / green: no flagged delta",
                 fill="#493d2e", font=font(19))
    drawing.text((12, 33), "Image inference does not change or certify navigation. Numbers match the report's bc_place IDs.",
                 fill="#493d2e", font=font(17))
    overview.save(out / "anchor_geography_review.png", optimize=True)
    mismatch = np.zeros((columns, columns, 3), np.uint8)
    mismatch[grid == 2] = [78, 112, 126]
    mismatch[grid == 1] = [82, 76, 63]
    mismatch[ink_walkable] = [171, 130, 49]
    mismatch[added_walkable] = [217, 54, 37]
    Image.fromarray(mismatch, "RGB").resize((1413, 1413), Image.Resampling.NEAREST).save(out / "walkable_art_delta_review.png")
    selected = review[:max(0, args.max_crops)]
    contact = Image.new("RGB", (1024, max(1, len(selected)) * 280), "#eee6d7")
    contact_draw = ImageDraw.Draw(contact)
    if selected:
        crops = out / "local_repair_references"
        crops.mkdir(exist_ok=True)
        with Image.open(args.baseline) as old_source, Image.open(args.art) as new_source:
            for rank, record in enumerate(selected, start=1):
                old = old_source.crop(record["baseline_reference_crop_native_px"]).convert("RGB")
                new = new_source.crop(record["candidate_repair_crop_native_px"]).convert("RGB")
                stem = f"{rank:02d}_{record['id']}"
                old.save(crops / f"{stem}_baseline.png")
                new.save(crops / f"{stem}_candidate.png")
                record["repair_reference_files"] = [str((crops / f"{stem}_{name}.png").relative_to(out)).replace("\\", "/")
                                                     for name in ("baseline", "candidate")]
                for image, x_offset in ((old, 0), (new, 512)):
                    panel = image.resize((512, 238), Image.Resampling.LANCZOS)
                    panel_draw = ImageDraw.Draw(panel)
                    # Crop centre can shift only at the outside world boundary.
                    panel_draw.line((248, 119, 264, 119), fill="#b83421", width=2)
                    panel_draw.line((256, 111, 256, 127), fill="#b83421", width=2)
                    contact.paste(panel, (x_offset, (rank - 1) * 280 + 42))
                text = f"{rank:02d} {record['id']} {record['name']} | old / candidate | " + ", ".join(record["flags"])
                contact_draw.text((8, (rank - 1) * 280 + 4), text, fill="#483a2b", font=font(16))
    else:
        contact_draw.text((18, 16), "No introduced/uncertain anchor deltas were flagged by this image heuristic.", fill="#483a2b", font=font(20))
    contact.save(out / "anchor_local_comparison.png", optimize=True)
    union = (before["water"] | after["water"]) & scope
    after_distance = ndimage.distance_transform_edt(~after["water"]) * world_size / MEASURE_SIZE
    delta_distances = np.concatenate((baseline_distance[after["water"] & scope], after_distance[before["water"] & scope]))
    report = {"version": 1, "role": "Read-only image/geography review; never gameplay data or a navigation certification.",
              "source_art": label(args.art), "source_art_sha256": sha256(args.art),
              "baseline_art": label(args.baseline), "baseline_matches_navigation_source": sha256(args.baseline) == nav.get("art_sha256"),
              "input_sha256": input_hashes, "tool_sha256": sha256(Path(__file__)),
              "world_size": world_size, "world_km": nav["world_km"], "named_locations": len(records),
              "candidate_native_size": after["native_size"], "measurement_size": MEASURE_SIZE,
              "water_mode": water_mode,
              "candidate_scope_ids": scope_ids, "scope_fraction": float(scope.mean()),
              "limits": ["Source-blue classification is high confidence only for blue-water painting styles.",
                         "Muted grey-green water, rock and foliage can share colours; amber findings require visual review or --water-mask.",
                         "Dark-mass changes are visual clutter/possible hills, not newly blocked cells.",
                         "Existing navigation crossings can intentionally run over source water; only introduced visual deltas are ranked.",
                         "Review crops are exact native crops for repair references. Annotated overviews are diagnostics, not replacement art."],
              "summary": {"review_anchor_count": len(review),
                          "introduced_water_anchor_count": sum("introduced_water_at_entrance_or_icon" in record["flags"] for record in records),
                          "muted_water_review_count": sum("muted_water_colour_needs_visual_review" in record["flags"] for record in records),
                          "new_dark_mass_review_count": sum("new_dark_mass_needs_visual_review" in record["flags"] for record in records),
                          "old_water_fraction": float(before["water"][scope].mean()), "new_water_fraction": float(after["water"][scope].mean()),
                          "water_intersection_over_union": float((before["water"] & after["water"] & scope).sum() / max(union.sum(), 1)),
                          "symmetric_water_footprint_distance_p95_world_units": float(np.percentile(delta_distances, 95)) if delta_distances.size else None,
                          "new_water_evidence_on_existing_walkable_cells": int(added_walkable.sum()),
                          "new_dark_mass_on_existing_walkable_cells": int(ink_walkable.sum())},
              "walkable_water_review_regions": mismatch_components(added_walkable, world_size),
              "walkable_dark_mass_review_regions": mismatch_components(ink_walkable, world_size),
              "anchors": records}
    (out / "geography_audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (out / "anchor_geography.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        fields = ["id", "name", "type", "review_score", "existing_nav_value", "added_entrance_water_fraction",
                  "added_icon_water_fraction", "muted_colour_review_fraction", "added_dark_mass_fraction", "flags"]
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(records)
    if any(sha256(ROOT / path) != digest for path, digest in input_hashes.items()):
        raise RuntimeError("A read-only input changed while the audit was running; rerun before accepting the report.")
    print(json.dumps({"summary": report["summary"], "inputs_unchanged": True, "output_dir": label(out)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
