#!/usr/bin/env python3
"""Read-only portrait alignment gate. Requires Pillow and numpy.

Coordinates are image-left/right on the unchanged 1024x1536 canvas. The locked
face and authored axis in feature_frame.json are authoritative, never a feature
alpha bounding box, an old importer transform, or a rig constant. Pixel probes
are deliberately simple ink diagnostics, not an anatomical symmetry score.
Independent semantic measurements AND SHA-bound whole-face visual review are
required. No image is altered; only --report writes a requested QA JSON.

Examples (run from the project root):
  python tools/validate_portrait_feature_alignment.py --candidate-root \
    mods/appearance/male_face_alignment_fix_20261109 --report <qa/report.json>
  python tools/validate_portrait_feature_alignment.py --pack-root \
    mods/appearance/ink_three_variants_20261004 --measurements <measurements.json> \
    --review <review.json> --report <qa/report.json>

Exit 0: complete and within gates; 1: rejected or incomplete; 2: invalid input.
Passing is a recorded release gate, not a claim of pixel-mirror symmetry or user
approval. Reviewers must look at the actual native-scale whole face.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
DEFAULT_PACK = PROJECT / "mods/appearance/ink_three_variants_20261004"
DEFAULT_FRAME = PROJECT / "docs/美术规范/男性五官统一定位与验收_20261109/feature_frame.json"
PART_DIRS = {"eye": "eyes", "brow": "eyebrows", "nose": "nose", "mouth": "mouth", "beard": "face_accessory"}
PART_NAMES = {"eyes":"eye", "eyebrows":"brow", "face_accessory":"beard"}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value, dict):
        raise ValueError(f"JSON object required: {path}")
    return value


def relative_file(value: str) -> str:
    path = Path(value)
    if path.is_absolute() or ".." in path.parts or "\\" in value or not value:
        raise ValueError(f"Package-relative forward-slash path required: {value!r}")
    return path.as_posix()


def finite_pair(value) -> bool:
    return isinstance(value, list) and len(value) == 2 and all(isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x) for x in value)


class Gate:
    def __init__(self, args):
        self.args = args
        self.frame = read_json(args.frame)
        if self.frame.get("schema") != "male-portrait-feature-frame/v1":
            raise ValueError("Unsupported feature-frame schema")
        self.axis = float(self.frame["axis_x"])
        self.tol = self.frame["tolerances"]
        self.checks = []
        self.results = []
        self.arrays = {}
        self.input_hashes = {}
        self.feature_defs = {relative_file(x["file"]): x for x in self.frame["features"]}
        if len(self.feature_defs) != len(self.frame["features"]):
            raise ValueError("Duplicate canonical feature file")
        self.measurements = self.optional_json(args.measurements)
        self.review = self.optional_json(args.review)
        self.measured = self.index_records(self.measurements, "semantic measurements")
        self.reviewed = self.index_records(self.review, "visual review")

    def check(self, ok, code, file="", actual=None, limit=None):
        row = {"pass": bool(ok), "code": code}
        if file:
            row["file"] = file
        if actual is not None:
            row["actual"] = actual
        if limit is not None:
            row["limit"] = limit
        self.checks.append(row)
        return bool(ok)

    def near(self, actual, target, tolerance, code, file):
        return self.check(abs(float(actual) - float(target)) <= tolerance + 1e-8, code, file, round(float(actual), 4), {"target": target, "tolerance_px": tolerance})

    @staticmethod
    def optional_json(path):
        return read_json(path) if path.is_file() else {}

    @staticmethod
    def index_records(document, name):
        result = {}
        for row in document.get("records", []):
            file = relative_file(row["file"])
            if file in result:
                raise ValueError(f"Duplicate {name} record: {file}")
            result[file] = row
        return result

    def resolve(self, file, require_candidate=False):
        file = relative_file(file)
        if self.args.candidate_root:
            candidate = self.args.candidate_root / file
            if candidate.is_file() or require_candidate:
                return candidate
        return self.args.pack_root / file

    def image(self, file, require_candidate=False):
        if file not in self.arrays:
            path = self.resolve(file, require_candidate)
            if not self.check(path.is_file(), "png_exists", file):
                return None
            with Image.open(path) as image:
                self.check(list(image.size) == self.frame["canvas"], "shared_canvas", file, list(image.size), self.frame["canvas"])
                self.arrays[file] = np.asarray(image.convert("RGBA"), dtype=np.float64)
            self.input_hashes[file] = sha256(path)
        return self.arrays[file]

    def ink(self, image, roi, threshold, alpha_floor=24):
        x0, y0, x1, y1 = map(int, roi)
        region = image[y0:y1, x0:x1]
        if region.size == 0:
            raise ValueError(f"Empty ROI {roi}")
        gray = region[:, :, :3].mean(axis=2)
        alpha = region[:, :, 3]
        weights = np.maximum(threshold - gray, 0) * alpha / 255
        weights[alpha < alpha_floor] = 0
        return weights, gray, alpha

    def ink_summary(self, image, roi, threshold):
        weights, gray, alpha = self.ink(image, roi, threshold)
        total = float(weights.sum())
        if total <= 0:
            return {"visible_ink_pixels": 0, "centroid": None, "x_quantiles_01_99": None}
        yy, xx = np.indices(weights.shape)
        profile = weights.sum(axis=0).cumsum()
        ends = [int(np.searchsorted(profile, total * q)) + roi[0] for q in (.01, .99)]
        core = (gray < threshold) & (alpha >= 128)
        return {"visible_ink_pixels": int((weights > 0).sum()), "core_pixels": int(core.sum()),
                "centroid": [round(float((weights * (xx + roi[0])).sum() / total), 4), round(float((weights * (yy + roi[1])).sum() / total), 4)],
                "x_quantiles_01_99": ends, "ink_span_px": ends[1] - ends[0] + 1,
                "mean_core_darkness": round(float((255-gray[core]).mean()), 4) if core.any() else None}

    def point(self, points, key, file, count=None):
        value = points.get(key)
        ok = finite_pair(value) if count is None else isinstance(value, list) and len(value) == count and all(finite_pair(p) for p in value)
        self.check(ok, "semantic_point:" + key, file)
        if not ok:
            return None
        all_points = [value] if count is None else value
        w, h = self.frame["canvas"]
        for p in all_points:
            self.check(0 <= p[0] < w and 0 <= p[1] < h, "semantic_point_in_canvas:" + key, file, p)
        return value

    def support(self, image, point, file, label, dark=True):
        x, y = map(lambda x: int(round(x)), point)
        radius = int(self.tol["point_support_radius_px"])
        region = image[max(0,y-radius):y+radius+1, max(0,x-radius):x+radius+1]
        if region.size == 0:
            self.check(False, "semantic_pixel_support:" + label, file)
            return
        support = region[:, :, 3] >= 16
        if dark:
            support &= region[:, :, :3].mean(axis=2) < 220
        self.check(support.any(), "semantic_pixel_support:" + label, file)

    def paired_points(self, points, key, image, file, dark=True, axis=True):
        pair = self.point(points, key, file, 2)
        if pair:
            for p in pair:
                self.support(image, p, file, key, dark)
            if axis:
                self.near((pair[0][0]+pair[1][0])/2, self.axis, self.tol["axis_midpoint_px"], "semantic_axis:"+key, file)
            self.near(pair[1][1]-pair[0][1], 0, self.tol["paired_landmark_px"], "paired_height:"+key, file)
        return pair

    def number_pair(self, measures, key, file, minimum=0, maximum=100):
        values = measures.get(key)
        if not self.check(finite_pair(values) and all(minimum <= x <= maximum for x in values), "semantic_measurement:"+key, file, values):
            return None
        return values

    def eye_ink_profiles(self, image, rois, summaries):
        # Ink envelope probes at 25/50/75% between each side's pigment extent.
        # These are screening proxies, not substituted anatomical eyelid points.
        output = []
        for side, (roi, summary) in enumerate(zip(rois, summaries)):
            ends = summary["x_quantiles_01_99"]
            if not ends:
                output.append(None)
                continue
            inner, outer = ends[::-1] if side == 0 else ends
            samples = []
            y0, y1 = self.frame["pixel_probes"]["eyes"]["profile_y_range"]
            for fraction in (.25,.5,.75):
                x = round(inner + fraction * (outer-inner))
                weight, _, _ = self.ink(image, [x-1,y0,x+2,y1], 190)
                vertical = weight.sum(axis=1).cumsum()
                samples.append([int(np.searchsorted(vertical, vertical[-1]*q))+y0 for q in (.05,.95)] if vertical[-1] else None)
            output.append(samples)
        return output

    def eyes(self, file, image, record, result):
        probe = self.frame["pixel_probes"]["eyes"]
        pupils = [self.ink_summary(image, roi, 100) for roi in probe["pupil_rois"]]
        ink = [self.ink_summary(image, roi, 170) for roi in probe["side_rois"]]
        result["pixel_pupil_kernels"] = pupils
        result["pixel_side_ink"] = ink
        profiles = self.eye_ink_profiles(image, probe["side_rois"], ink)
        result["pixel_ink_envelope_proxies_25_50_75"] = profiles
        if all(p["centroid"] for p in pupils):
            self.near(sum(p["centroid"][0] for p in pupils)/2, self.axis, self.tol["axis_midpoint_px"], "pixel_pupil_midpoint_axis", file)
        else:
            self.check(False, "both_pixel_pupil_kernels_present", file)
        if all(side and all(side) for side in profiles):
            for n, fraction in enumerate((25,50,75)):
                for k, edge in enumerate(("upper","lower")):
                    self.near(profiles[1][n][k]-profiles[0][n][k], 0, self.tol["ink_envelope_difference_px"], f"pixel_ink_envelope_{edge}_{fraction}", file)
        else:
            self.check(False, "complete_pixel_lid_envelope_proxies", file)
        if not record:
            return
        points, measures = record.get("points", {}), record.get("measurements", {})
        pair_pupils = self.paired_points(points, "pupils", image, file)
        inner = self.paired_points(points, "inner_corners", image, file)
        outer = self.paired_points(points, "outer_corners", image, file)
        centers = self.paired_points(points, "iris_centers", image, file)
        if pair_pupils:
            for side in range(2):
                if pupils[side]["centroid"]:
                    distance = math.dist(pair_pupils[side], pupils[side]["centroid"])
                    self.check(distance <= self.tol["semantic_to_probe_px"], "measured_pupil_matches_current_ink", file, distance, self.tol["semantic_to_probe_px"])
        lids = {}
        for key in ("upper_lid_samples", "lower_lid_samples"):
            value = points.get(key)
            ok = isinstance(value,list) and len(value)==2 and all(isinstance(side,list) and len(side)==3 and all(finite_pair(p) for p in side) for side in value)
            if self.check(ok, "complete_semantic:"+key, file):
                lids[key] = value
                for side, samples in enumerate(value):
                    for n, p in enumerate(samples):
                        self.support(image, p, file, key)
                        if inner and outer:
                            expected_x = inner[side][0] + (.25,.5,.75)[n] * (outer[side][0]-inner[side][0])
                            self.near(p[0], expected_x, self.tol["sample_fraction_x_px"], "lid_sample_at_declared_fraction", file)
                        self.near(value[1][n][1]-value[0][n][1], 0, self.tol["paired_landmark_px"], "semantic_lid_contour:"+key+str(n), file)
        if inner and outer:
            widths = [abs(inner[s][0]-outer[s][0]) for s in range(2)]
            self.near(widths[1]-widths[0], 0, self.tol["paired_width_px"], "semantic_eye_width_difference", file)
        if len(lids)==2:
            openings = [[lids["lower_lid_samples"][s][n][1]-lids["upper_lid_samples"][s][n][1] for n in range(3)] for s in range(2)]
            result["semantic_apertures_25_50_75"] = openings
            self.check(all(0 < v < 35 for side in openings for v in side), "positive_plausible_eye_openings", file, openings)
            for n in range(3):
                self.near(openings[1][n]-openings[0][n], 0, self.tol["paired_opening_px"], "semantic_aperture_difference_"+str(n), file)
            if centers:
                for side in range(2):
                    def at_iris(key):
                        samples = sorted(lids[key][side],key=lambda p:p[0])
                        return float(np.interp(centers[side][0],[p[0] for p in samples],[p[1] for p in samples]))
                    upper, lower = at_iris("upper_lid_samples"), at_iris("lower_lid_samples")
                    self.check(upper-1.5 <= centers[side][1] <= lower+1.5,"iris_center_inside_observed_lid_envelope",file,{"side":side,"iris_center":centers[side],"upper_lower":[upper,lower]})
                    bounds = measures.get("iris_brown_core_vertical_bounds_y_inclusive")
                    if isinstance(bounds,list) and len(bounds)==2 and finite_pair(bounds[side]):
                        top,bottom = bounds[side]
                        self.check(upper-4 <= top <= bottom <= lower+self.tol["semantic_to_probe_px"],"iris_vertical_segment_near_observed_lid_envelope",file,{"side":side,"segment":[top,bottom],"upper_lower":[upper,lower]})
        for key in ("iris_widths","iris_heights","upper_lid_occlusion_px"):
            values = self.number_pair(measures,key,file, 0 if key=="upper_lid_occlusion_px" else 1, 40)
            if values:
                self.near(values[1]-values[0], 0, self.tol["paired_iris_px"], "paired_iris_measure:"+key, file)
        if centers and pair_pupils:
            self.check(all(math.dist(centers[s],pair_pupils[s]) <= 4 for s in range(2)), "iris_center_and_pupil_same_scale", file)

    def brows(self, file, image, record, result):
        probe = self.frame["pixel_probes"]["brows"]
        summaries = [self.ink_summary(image, roi, 170) for roi in probe["side_rois"]]
        result["pixel_side_ink"] = summaries
        if all(s["centroid"] for s in summaries):
            self.near(summaries[1]["ink_span_px"]-summaries[0]["ink_span_px"], 0, self.tol["paired_width_px"], "pixel_brow_main_ink_span_difference", file)
        else:
            self.check(False, "both_pixel_brow_bodies_present", file)
        body_probes = []
        for side, (roi, summary) in enumerate(zip(probe["side_rois"],summaries)):
            ends = summary["x_quantiles_01_99"]
            readings = []
            if ends:
                inner_x, outer_x = ends[::-1] if side==0 else ends
                for fraction in (.25,.5,.75):
                    x = round(inner_x+(outer_x-inner_x)*fraction)
                    weights, gray, alpha = self.ink(image,[x-1,roi[1],x+2,roi[3]],170)
                    profile = weights.sum(axis=1).cumsum()
                    core = (gray < 170) & (alpha >= 128)
                    if profile[-1] and core.any():
                        endpoints = [int(np.searchsorted(profile,profile[-1]*q)) for q in (.05,.95)]
                        readings.append({"x":x,"thickness_px":endpoints[1]-endpoints[0]+1,"mean_darkness_0_255":round(float((255-gray[core]).mean()),4)})
                    else:
                        readings.append(None)
            body_probes.append(readings)
        result["pixel_brow_body_probes_25_50_75"] = body_probes
        if all(len(side)==3 and all(side) for side in body_probes):
            for n in range(3):
                for key, limit in (("thickness_px",self.tol["paired_brow_thickness_px"]),("mean_darkness_0_255",self.tol["paired_darkness_0_255"])):
                    self.near(body_probes[1][n][key]-body_probes[0][n][key],0,limit,"pixel_brow_body_"+key+str(n),file)
        else:
            self.check(False,"complete_pixel_brow_body_probes",file)
        if not record:
            return
        points, measures = record.get("points", {}), record.get("measurements", {})
        inner = self.paired_points(points,"inner_heads",image,file)
        outer = self.paired_points(points,"outer_tails",image,file)
        self.paired_points(points,"crests",image,file)
        widths = self.number_pair(measures,"widths",file,1,150)
        if widths:
            self.near(widths[1]-widths[0],0,self.tol["paired_width_px"],"semantic_brow_width_difference",file)
            if inner and outer:
                for s in range(2):
                    self.near(widths[s],abs(inner[s][0]-outer[s][0]),self.tol["semantic_to_probe_px"],"semantic_brow_width_coherent",file)
        for key, difference_limit in (("thickness_at_quartiles", self.tol["paired_brow_thickness_px"]), ("mean_darkness_at_quartiles", self.tol["paired_darkness_0_255"])):
            values = measures.get(key)
            valid = isinstance(values,list) and len(values)==2 and all(isinstance(side,list) and len(side)==3 and all(isinstance(v,(int,float)) and math.isfinite(v) and 0<=v<=255 for v in side) for side in values)
            if self.check(valid,"complete_semantic_measure:"+key,file):
                for n in range(3):
                    self.near(values[1][n]-values[0][n],0,difference_limit,"paired_brow_"+key+str(n),file)

    def nose(self, file, image, record, result):
        probe = self.frame["pixel_probes"]["nose"]
        nostrils = [self.ink_summary(image, roi, 150) for roi in probe["nostril_rois"]]
        result["pixel_nostril_kernels"] = nostrils
        if all(p["centroid"] for p in nostrils):
            self.near(sum(p["centroid"][0] for p in nostrils)/2,self.axis,self.tol["axis_midpoint_px"],"pixel_nostril_midpoint_axis",file)
        else:
            self.check(False,"both_pixel_nostrils_present",file)
        if record:
            points = record.get("points",{})
            self.paired_points(points,"nostrils",image,file)
            center = self.point(points,"center",file)
            if center:
                self.support(image,center,file,"nose_center",dark=False)
                self.near(center[0],self.axis,self.tol["axis_midpoint_px"],"semantic_nose_center_axis",file)

    def mouth(self, file, image, record, result):
        seam = self.ink_summary(image,self.frame["pixel_probes"]["mouth"]["seam_roi"],160)
        result["pixel_seam_ink"] = seam
        if self.check(seam["centroid"] is not None,"pixel_lip_seam_present",file):
            self.near(seam["centroid"][0],self.axis,self.tol["axis_midpoint_px"],"pixel_lip_seam_axis",file)
        if record:
            points = record.get("points",{})
            self.paired_points(points,"corners",image,file)
            center = self.point(points,"seam_center",file)
            if center:
                self.support(image,center,file,"lip_seam_center")
                self.near(center[0],self.axis,self.tol["axis_midpoint_px"],"semantic_lip_center_axis",file)

    def beard(self, file, image, record, result):
        probe = self.frame["pixel_probes"]["beard"]
        regions = {region:self.ink_summary(image,roi,170) for region,roi in probe["region_rois"].items()}
        result["pixel_regions"] = regions
        present = [key for key, value in regions.items() if value["visible_ink_pixels"] >= probe["minimum_region_pixels"]]
        for region in present:
            ends = regions[region]["x_quantiles_01_99"]
            self.near(sum(ends)/2,self.axis,self.tol["beard_ink_extent_midpoint_px"],"pixel_beard_extent_midpoint:"+region,file)
        if record:
            declared = record.get("measurements",{}).get("present_regions")
            self.check(isinstance(declared,list) and set(declared)==set(present),"semantic_beard_present_regions_match_current_pixels",file,{"declared":declared,"observed":present})
            for region in present:
                p = self.point(record.get("points",{}),region+"_center",file)
                # The philtrum gap can be transparent. A true semantic midpoint
                # does not itself need a hair pixel; validate its actual paired
                # ink endpoints instead of manufacturing central alpha support.
                ends = self.paired_points(record.get("points",{}),region+"_endpoints",image,file,axis=False)
                if p:
                    self.near(p[0],self.axis,self.tol["axis_midpoint_px"],"semantic_beard_center:"+region,file)
                    if ends:
                        self.near(p[0],sum(e[0] for e in ends)/2,self.tol["semantic_to_probe_px"],"beard_center_matches_observed_endpoints:"+region,file)

    def visual_record(self, file, part):
        row = self.reviewed.get(file)
        if not self.check(row is not None,"current_file_has_independent_visual_review",file):
            return
        self.check(row.get("sha256")==self.input_hashes[file],"visual_review_matches_exact_current_png_sha",file)
        self.check(row.get("status")=="accepted","visual_review_accepted",file,row.get("status"))
        required = ["whole_face_native_scale","light_dark_backgrounds","axis_alignment"]
        required += ["eye_corners_and_lid_contours","iris_same_scale"] if part=="eye" else ["brow_shape_width_depth"] if part=="brow" else []
        for key in required:
            self.check(row.get("checks",{}).get(key) is True,"visual_review_check:"+key,file)
        self.check(isinstance(row.get("notes"),str) and bool(row["notes"].strip()),"visual_review_has_observation",file)

    def inventory(self):
        config = read_json(self.args.parts_config)
        features, hair = {}, []
        for entry in config.get("components",[]):
            if entry.get("gender")!="male":
                continue
            part = entry.get("part")
            if part not in PART_DIRS and part not in ("hair_front","hair_back"):
                continue
            count = int(entry["variant_count"])
            indices = entry.get("variant_indices",list(range(1,count+1)))
            styles = entry.get("variant_style_ids",list(range(count)))
            if len(indices)!=count or len(styles)!=count:
                raise ValueError("Configuration variants have inconsistent lengths")
            for index, style in zip(indices, styles):
                file = relative_file(entry["file"].replace("{index}",f"{int(index):02d}"))
                if part.startswith("hair_"):
                    hair.append(file)
                    continue
                if file==self.frame["empty_beard"]["file"]:
                    image = self.image(file)
                    if image is not None:
                        self.check(self.input_hashes[file]==self.frame["empty_beard"]["sha256"] and not image[:,:,3].any(),"unchanged_truly_empty_beard",file)
                    continue
                if file in features:
                    raise ValueError("Configured feature file used by multiple options: "+file)
                features[file] = {"part":part,"index":int(index),"style_id":int(style)}
        # A new physical feature must not silently evade the gate merely because
        # its configuration/measurement record has not yet been added.
        physical_root = self.args.candidate_root or self.args.pack_root
        for folder in PART_DIRS.values():
            for path in (physical_root/"male"/folder).glob("*.png"):
                file = path.relative_to(physical_root).as_posix()
                self.check(file in features or file==self.frame["empty_beard"]["file"],"physical_feature_has_configured_variant",file)
        return features, hair

    def global_review(self, features, hair):
        fixed_sha = self.frame["fixed_face"]["sha256"]
        self.check(self.measurements.get("schema")=="male-face-final-semantics/v1","semantic_schema_current")
        self.check(self.measurements.get("axis_x")==self.axis and self.measurements.get("fixed_head_sha256")==fixed_sha,"semantic_record_uses_locked_face_and_axis")
        self.check(self.review.get("schema")=="male-face-visual-review/v1","visual_review_schema_current")
        self.check(self.review.get("fixed_head_sha256")==fixed_sha,"visual_review_uses_locked_face")
        reviewer = self.review.get("reviewer")
        self.check(self.review.get("status")=="accepted" and isinstance(reviewer,str) and bool(reviewer.strip()),"independent_visual_review_accepted_and_attributed")
        composition = self.review.get("composition_review",{})
        self.check(composition.get("status")=="accepted","whole_face_composition_review_accepted")
        for key in ("nine_brow_eye_nohair","seven_front_hair","five_nonempty_beards","hair09_occlusion"):
            self.check(composition.get(key) is True,"composition_review:"+key)
        for file in list(features)+[self.frame["fixed_face"]["file"]]+hair:
            image = self.image(file,require_candidate=file in features)
            if image is not None:
                self.check(composition.get("input_hashes",{}).get(file)==self.input_hashes[file],"composition_review_matches_current_input_sha",file)
        evidence = composition.get("evidence",[])
        self.check(isinstance(evidence,list) and len(evidence)>0,"whole_face_visual_evidence_present")
        evidence_root = self.args.review.parent.parent if self.args.review.parent.name=="qa" else self.args.review.parent
        for item in evidence if isinstance(evidence,list) else []:
            file = relative_file(item["file"])
            path = evidence_root / file
            self.check(path.is_file() and sha256(path)==item.get("sha256"),"visual_evidence_file_hash_matches",file)

    def run(self):
        self.check(self.axis==513 and self.frame["canvas"]==[1024,1536],"canonical_anatomical_frame_is_locked")
        face = self.frame["fixed_face"]
        image = self.image(face["file"])
        if image is not None:
            self.check(self.input_hashes[face["file"]]==face["sha256"],"fixed_head_sha_is_authoritative",face["file"])
        features, hair = self.inventory()
        self.check(bool(features),"configured_feature_inventory_not_empty")
        for file, item in features.items():
            first = len(self.checks)
            definition = self.feature_defs.get(file)
            self.check(definition is not None,"configured_variant_has_canonical_roi_record",file)
            image = self.image(file,require_candidate=True)
            if image is None or definition is None:
                continue
            self.check(definition["part"]==item["part"],"canonical_part_matches_live_configuration",file)
            record = self.measured.get(file)
            self.check(record is not None,"current_file_has_full_semantic_measurements",file)
            if record:
                self.check(record.get("sha256")==self.input_hashes[file],"measurements_match_exact_current_png_sha",file)
                self.check(PART_NAMES.get(record.get("part"),record.get("part"))==item["part"] and record.get("index")==item["index"],"semantic_part_and_physical_index_match",file)
                # Stale points are never reused after a changed PNG.
                if record.get("sha256")!=self.input_hashes[file]:
                    record = None
            result = {"file":file,**item,"sha256":self.input_hashes[file]}
            {"eye":self.eyes,"brow":self.brows,"nose":self.nose,"mouth":self.mouth,"beard":self.beard}[item["part"]](file,image,record,result)
            self.visual_record(file,item["part"])
            result["pass"] = all(x["pass"] for x in self.checks[first:])
            self.results.append(result)
        self.global_review(features,hair)
        failures = [x for x in self.checks if not x["pass"]]
        return {"schema":"portrait-feature-alignment-gate/v1","pass":not failures,"checks":len(self.checks),"failures":failures,
                "frame_sha256":sha256(self.args.frame),"fixed_face_sha256":face["sha256"],"axis_x":self.axis,
                "pack_root":str(self.args.pack_root),"candidate_root":str(self.args.candidate_root) if self.args.candidate_root else None,
                "measurements_file":str(self.args.measurements),"review_file":str(self.args.review),
                "configured_feature_count":len(features),"records":self.results,
                "limits":"Fixed-ROI ink readings are screening proxies. They do not establish anatomy or beauty. Acceptance also requires complete independent semantic points, current-image SHA binding and native-scale full-face visual evidence. No pixel mirror, alpha-bbox face axis, old rig authority or image mutation is used."}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pack-root",type=Path,default=DEFAULT_PACK,help="Installed pack containing the locked head/config and unchanged hair")
    parser.add_argument("--candidate-root",type=Path,help="Candidate overrides; every configured nonempty feature must be present")
    parser.add_argument("--frame",type=Path,default=DEFAULT_FRAME)
    parser.add_argument("--parts-config",type=Path,help="Configuration inventory; defaults to candidate or installed appearance_parts.json")
    parser.add_argument("--measurements",type=Path,help="Independent semantic JSON; defaults to candidate/qa/final_semantic_measurements.json")
    parser.add_argument("--review",type=Path,help="Independent SHA-bound visual JSON; defaults to candidate/qa/final_visual_review.json")
    parser.add_argument("--report",type=Path,help="Only explicit output; no files are otherwise written")
    args = parser.parse_args(argv)
    args.pack_root = args.pack_root.resolve()
    args.candidate_root = args.candidate_root.resolve() if args.candidate_root else None
    base = args.candidate_root or args.pack_root
    args.parts_config = args.parts_config or (base/"appearance_parts.json" if (base/"appearance_parts.json").is_file() else args.pack_root/"appearance_parts.json")
    args.measurements = args.measurements or base/"qa/final_semantic_measurements.json"
    args.review = args.review or base/"qa/final_visual_review.json"
    try:
        report = Gate(args).run()
        code = 0 if report["pass"] else 1
    except (OSError,ValueError,KeyError,TypeError,IndexError) as exc:
        report = {"schema":"portrait-feature-alignment-gate/v1","pass":False,"input_error":str(exc)}
        code = 2
    if args.report:
        destination = args.report.resolve()
        # An explicit report may never overwrite source/candidate assets, config,
        # measurements, review, or the canonical frame. Reports belong in QA.
        if destination.suffix.lower()!=".json" or "qa" not in destination.parts or any(destination==p.resolve() for p in (args.frame,args.parts_config,args.measurements,args.review)) or destination.is_relative_to(args.pack_root):
            parser.error("--report must be a separate QA JSON outside the active pack and input files")
        destination.parent.mkdir(parents=True,exist_ok=True)
        destination.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    failures = report.get("failures",[])
    print(json.dumps({"pass":report["pass"],"checks":report.get("checks",0),"failure_count":len(failures),"input_error":report.get("input_error"),"report":str(args.report) if args.report else None},ensure_ascii=False))
    for failure in failures[:18]:
        print(json.dumps(failure,ensure_ascii=False))
    return code


if __name__=="__main__":
    sys.exit(main())
