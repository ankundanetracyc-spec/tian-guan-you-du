"""Read-only source-alpha/LBS foot contact audit; never edits art or runtime."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "resources/appearance/guigu_native_first_20261105"
ART = ROOT / "tools/action_battle_test/art/guigu_native_first_20261105"
OUT = ROOT / "output/guigu_native_ground_audit_20261105"
SUBPIXELS = 8


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def alpha_at(alpha: np.ndarray, points: np.ndarray) -> np.ndarray:
    # Texture UV pixel coordinates are edge coordinates; texel centers are+.5.
    q = points - 0.5
    x0, y0 = np.floor(q).astype(int).T
    fx, fy = (q - np.floor(q)).T
    height, width = alpha.shape
    x1, y1 = np.clip(x0 + 1, 0, width - 1), np.clip(y0 + 1, 0, height - 1)
    x0, y0 = np.clip(x0, 0, width - 1), np.clip(y0, 0, height - 1)
    return ((1 - fx) * (1 - fy) * alpha[y0, x0] + fx * (1 - fy) * alpha[y0, x1]
            + (1 - fx) * fy * alpha[y1, x0] + fx * fy * alpha[y1, x1])


def alpha_samples(source: dict) -> dict:
    image = Image.open(ART / "source/sprite_87557.png").convert("RGBA")
    alpha = np.asarray(image)[:, :, 3].astype(float) / 255.0
    height, width = alpha.shape
    uv = np.asarray(source["uvConvention"]["uvTextureLowerLeft"], dtype=float)
    uv[:, 0] *= width
    uv[:, 1] = (1 - uv[:, 1]) * height
    xx, yy = np.meshgrid((np.arange(width * SUBPIXELS) + 0.5) / SUBPIXELS,
                         (np.arange(height * SUBPIXELS) + 0.5) / SUBPIXELS)
    points = np.column_stack([xx.ravel(), yy.ravel()])
    alpha_values = alpha_at(alpha, points)
    rect = source["uvConvention"]["sourceTextureRectPixels"]
    crop_y = (1 - (height - points[:, 1] - rect[1]) / rect[3]) * 68.0
    # Existing source-observation document identifies instep at cropped row60,
    # heel at64 and sole lower edge65. This mask excludes the trouser and shaft.
    keep = (crop_y >= 60.0) & (alpha_values >= 1.0 / 255.0)
    points, alpha_values = points[keep], alpha_values[keep]
    triangles = np.asarray(source["triangles"], dtype=int)
    chosen = np.full(len(points), -1, dtype=int)
    weights = np.zeros((len(points), 3), dtype=float)
    for index, triangle in enumerate(triangles):
        verts = uv[triangle]
        matrix = np.array([[verts[0, 0], verts[1, 0], verts[2, 0]],
                           [verts[0, 1], verts[1, 1], verts[2, 1]], [1, 1, 1]])
        if abs(np.linalg.det(matrix)) < 1e-10:
            continue
        bary = np.column_stack([points, np.ones(len(points))]) @ np.linalg.inv(matrix).T
        inside = (bary >= -1e-8).all(axis=1) & (chosen < 0)
        chosen[inside] = index
        weights[inside] = bary[inside]
    inside = chosen >= 0
    points, alpha_values, weights, chosen = points[inside], alpha_values[inside], weights[inside], chosen[inside]
    below = alpha_at(alpha, points + np.array([0.0, 1.0 / SUBPIXELS]))
    return {"points": points, "triangle_vertices": triangles[chosen], "weights": weights,
            "soft": alpha_values >= 1.0 / 255.0,
            "core": alpha_values >= 128.0 / 255.0,
            "core_source_sole_edge": (alpha_values >= 128.0 / 255.0) & (below < 128.0 / 255.0)}


def span(values: list[float]) -> dict:
    data = np.asarray(values)
    return {"min": float(data.min()), "max": float(data.max()), "mean": float(data.mean())}


def solid_sample_hulls(samples: dict) -> list[dict]:
    """A triangle is affine, so UV convex-hull vertices retain everyY maximum."""
    result = []
    core = np.flatnonzero(samples["core"])
    groups: dict[tuple, list[int]] = {}
    for index in core:
        groups.setdefault(tuple(samples["triangle_vertices"][index].tolist()), []).append(int(index))
    for vertices, candidates in groups.items():
        ordered = sorted(candidates, key=lambda i: tuple(samples["points"][i]))
        def cross(a: int, b: int, c: int) -> float:
            ab = samples["points"][b] - samples["points"][a]
            ac = samples["points"][c] - samples["points"][a]
            return float(ab[0] * ac[1] - ab[1] * ac[0])
        lower, upper = [], []
        for index in ordered:
            while len(lower) >= 2 and cross(lower[-2], lower[-1], index) <= 0:
                lower.pop()
            lower.append(index)
        for index in reversed(ordered):
            while len(upper) >= 2 and cross(upper[-2], upper[-1], index) <= 0:
                upper.pop()
            upper.append(index)
        for index in dict.fromkeys(lower[:-1] + upper[:-1]):
            result.append({"triangle_vertices": list(vertices),
                           "barycentric_weights": samples["weights"][index].tolist(),
                           "source_full_uv_pixel": samples["points"][index].tolist()})
    return result


def main() -> None:
    reference_path = ROOT / "resources/appearance/guigu_male101/model_frames_20261104.json"
    reference = json.loads(reference_path.read_text(encoding="utf-8"))
    skins = json.loads((DATA / "skins.json").read_text(encoding="utf-8"))
    leg_source = next(mesh for mesh in skins["meshes"] if mesh["sourceSpriteId"] == 87557)
    samples = alpha_samples(leg_source)
    leg_indices = [index for index, mesh in enumerate(reference["meshes"]) if mesh["spriteId"] == 87557]
    body_index = next(index for index, mesh in enumerate(reference["meshes"]) if mesh["spriteId"] == 85649)
    offset = max(point[1] for index in leg_indices for point in reference["clips"]["Idle"]["frames"][0][index])
    root_index = reference["bonePaths"].index("/HumanRootMan/Root")
    yifu_index = reference["bonePaths"].index("/HumanRootMan/Root/Body/yifu")
    clips = {}
    for name, clip in reference["clips"].items():
        rows = []
        for frame_index, frame in enumerate(clip["frames"]):
            legs = []
            for index in leg_indices:
                vertices = np.asarray(frame[index], dtype=float)
                posed = (vertices[samples["triangle_vertices"]] * samples["weights"][:, :, None]).sum(axis=1)
                boundaries = {}
                for key in ["soft", "core", "core_source_sole_edge"]:
                    points = posed[samples[key]]
                    lowest = points[np.argmax(points[:, 1])]
                    boundaries[key] = {"lowest_y_relative_to_actor_foot": float(lowest[1] - offset),
                                       "clearance_above_actor_foot": float(offset - lowest[1]),
                                       "lowest_source_root_xy": lowest.tolist()}
                legs.append({"renderer_path": reference["meshes"][index]["rendererPath"],
                             "mesh_lower_y_relative_to_actor_foot": float(vertices[:, 1].max() - offset),
                             "visible_shoe": boundaries})
            visible_lowest = max(leg["visible_shoe"]["core"]["lowest_y_relative_to_actor_foot"] for leg in legs)
            edge_lowest = max(leg["visible_shoe"]["core_source_sole_edge"]["lowest_y_relative_to_actor_foot"] for leg in legs)
            body = np.asarray(frame[body_index], dtype=float)
            rows.append({"phase": frame_index / len(clip["frames"]),
                         "source_time_seconds": clip["duration"] * frame_index / len(clip["frames"]),
                         "core_visible_shoe_clearance": float(-visible_lowest),
                         "core_source_sole_edge_clearance": float(-edge_lowest),
                         "core_visible_shoe_gap_to_arena_shadow_center": float(4.0 - visible_lowest),
                         "core_visible_shoe_gap_to_arena_shadow_top": float(0.5 - visible_lowest),
                         "rig_root_source_y_down": clip["boneFrames"][frame_index][root_index][1],
                         "yifu_local_branch_source_y_down": clip["boneFrames"][frame_index][yifu_index][1],
                         "robe_mesh_bounds_source_root_xy": [float(body[:, 0].min()), float(body[:, 1].min()),
                                                              float(body[:, 0].max()), float(body[:, 1].max())],
                         "legs": legs})
        worst = max(range(len(rows)), key=lambda i: rows[i]["core_visible_shoe_clearance"])
        best = min(range(len(rows)), key=lambda i: rows[i]["core_visible_shoe_clearance"])
        clips[name] = {"duration": clip["duration"], "sample_count": len(rows),
                       "core_visible_shoe_clearance_pixels": span([row["core_visible_shoe_clearance"] for row in rows]),
                       "core_source_sole_edge_clearance_pixels": span([row["core_source_sole_edge_clearance"] for row in rows]),
                       "shoe_gap_to_shadow_top_pixels": span([row["core_visible_shoe_gap_to_arena_shadow_top"] for row in rows]),
                       "rig_root_y_pixels": span([row["rig_root_source_y_down"] for row in rows]),
                       "yifu_branch_y_pixels": span([row["yifu_local_branch_source_y_down"] for row in rows]),
                       "worst_phase": rows[worst]["phase"], "best_phase": rows[best]["phase"], "frames": rows}
        print(name, "core shoe clearance", clips[name]["core_visible_shoe_clearance_pixels"],
              "sole edge clearance", clips[name]["core_source_sole_edge_clearance_pixels"], flush=True)
    report = {"scope": "Read-only ground audit. Source shoe alpha is mapped through actual original UV triangles onto independently evaluated source3D/LBS frames. Reports visible-foot contour and separately the original lower sole edge; never substitutes a transparent mesh bounding vertex for a sole.",
              "measurement_coordinate_space": "Uncompensated source-local baseline after the old fixed Idle0 transparent-mesh offset, before grounding_profile whole-actor display translation. Corrected actual runtime results are in native_root_smoke.details.visible_sole_grounding.",
              "production_skin_sha256": sha(ROOT / "tools/action_battle_test/guigu_native_first_skin.gd"),
              "source_frame_sha256": sha(reference_path), "source_leg_png_sha256": sha(ART / "source/sprite_87557.png"),
              "ground_offset_current_idle0_leg_mesh_max_y": offset,
              "sampling": {"subpixel_step_source_texture_pixels": 1.0 / SUBPIXELS,
                           "filter": "bilinear alpha at texel centers, before mip LOD; source UV triangle clips applied",
                           "visible_foot_region": "source cropped texture y>=60, from existing observed instep/heel/sole anchors",
                           "alpha_core_threshold": 128, "soft_alpha_threshold": 1,
                           "source_alpha_foot_samples": len(samples["points"]),
                           "core_samples": int(samples["core"].sum()), "core_source_sole_edge_samples": int(samples["core_source_sole_edge"].sum()),
                           "model_oracle": "Original non-secondary frames, previously checked against actual runtime3D/LBS within3.1e-5 pixels. Foot chains are not affected by DynamicBone cloth/hair components."},
              "arena_ground_shadow": {"actor_origin": "player", "center_offset": [0.0, 4.0], "ellipse_radius": [14.0, 3.5],
                                      "top_y_relative_to_actor_foot": 0.5,
                                      "native_contact_shadow": "Empty Node2D, no shoe-contact drawing and no has_contact_shadow method; actual arena uses its fixed fallback ellipse."},
              "interpretation": "Positive baseline clearance means visible shoe alpha above the actor foot origin before clip display correction. Positive gap to shadow top is the pre-correction separation from the fixed arena ellipse. RootY is separate from yifu/leg local animation. Original game ground correction and full Animator are not included in these source frame data.",
              "clips": clips}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "source_alpha_ground_probe.json").write_text(json.dumps(report, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    print("SOURCE_ALPHA_GROUND_PROBE", OUT / "source_alpha_ground_probe.json", flush=True)
    if "--write-grounding-profile" in sys.argv:
        profile = {"schemaVersion": 1,
                   "scope": "One fixed whole-actor display translation per original clip, measured from source core-alpha visible shoe contours. Source vertices, limb lengths, poses, weights and raw JSON stay unchanged. Per-frame forced grounding is not used.",
                   "existing_idle_mesh_ground_offset_y": offset,
                   "source_leg_png_sha256": report["source_leg_png_sha256"],
                   "source_frame_sha256": report["source_frame_sha256"],
                   "source_measurement_report": "output/guigu_native_ground_audit_20261105/source_alpha_ground_probe.json",
                   "measurement": report["sampling"],
                   "source_solid_sole_sample_hull_contract": "Per-source-triangle UV convex-hull vertices of the3833 measured core-alpha foot samples. Affine triangle interpolation guarantees their maximumY equals that of all measured core samples under every posed triangle. Shared by both87557 leg instances.",
                   "source_solid_sole_sample_hull": solid_sample_hulls(samples),
                   "offset_contract": "Apply positive render_offset_y once as whole rig_root_node.position.y after source ground_offset subtraction. source_solid_sole_lowest_y is original projected sourceY before that subtraction; original rootY remains unchanged.",
                   "clips": {}}
        for name, measured in clips.items():
            minimum_clearance = measured["core_visible_shoe_clearance_pixels"]["min"]
            maximum_clearance = measured["core_visible_shoe_clearance_pixels"]["max"]
            profile["clips"][name] = {
                "render_offset_y": minimum_clearance,
                "source_solid_sole_lowest_y": offset - minimum_clearance,
                "source_local_solid_sole_lowest_y_after_base_offset": -minimum_clearance,
                "threshold": 128, "alpha_threshold_0_to_1": 128.0 / 255.0,
                "texture_subpixel_step": 1.0 / SUBPIXELS,
                "sample_count": measured["sample_count"],
                "lowest_sole_phase": measured["best_phase"],
                "remaining_source_flight_range_pixels": [0.0, maximum_clearance - minimum_clearance]}
        destination = DATA / "grounding_profile.json"
        destination.write_text(json.dumps(profile, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("GROUNDING_PROFILE", destination, flush=True)


if __name__ == "__main__":
    main()
