"""Check and package untouched native Godot cape-like rear-robe renders.

PNG figures only tile original captures without scaling or repainting. The one
GIF quantizes and encodes the original same-input comparison at native size.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
import statistics
import sys

from PIL import Image

PROJECT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv) > 1 else "cape_like_robe_open_full")
DEST = PROJECT / "docs" / "美术规范" / "长袍后摆披风式惯性_20261104"
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
assert len(ROWS) == 300
assert all(row["frame"] == index for index, row in enumerate(ROWS))
assert all(row["facing"] == row["expected_facing"] for row in ROWS)
for row in ROWS:
    for mode, data in row["profiles"].items():
        assert data["bones"] == 21
        assert data["meshes"] == 7
        assert len(data["legs"]) == 2
        assert all(mesh["visible"] and mesh["in_tree"] for mesh in data["legs"])
        assert set(data["contacts"]) == {"near", "far"}
    current = row["profiles"]["new"]["garment"]
    assert current["original_split_panels"]
    assert current["front_bones_resting"]
    assert current["visible_leg_meshes"] == 2
    assert not current.get("pants_uv_clipped", False), "Latest target allows natural legs outside the robe"
    assert current["lowest_cloth_y"] <= 0.002

NEW = [row["profiles"]["new"]["garment"] for row in ROWS]
assert all(row["delta"] == 0.0 for row in ROWS[216:228])
assert len({data["simulation_steps"] for data in NEW[215:228]}) == 1
assert len({data["gait_phase"] for data in NEW[215:228]}) == 1


def read_frame(mode, index):
    frame = Image.open(SOURCE / f"{mode}_{index:04d}.png").convert("RGB")
    assert frame.size == ((880, 600) if mode in ("compare", "arena") else (440, 500))
    return frame


def standing_comparison():
    original, candidate = read_frame("old", 0), read_frame("new", 0)
    backdrop = (88, 100, 106)
    row_bounds = []
    for frame in (original, candidate):
        bounds = []
        for y in range(frame.height):
            xs = [x for x in range(frame.width) if max(abs(frame.getpixel((x, y))[c] - backdrop[c]) for c in range(3)) > 25]
            bounds.append((min(xs), max(xs)) if xs else None)
        row_bounds.append(bounds)
    outline_delta = [abs(a[c] - b[c]) for a, b in zip(*row_bounds) if a is not None and b is not None for c in range(2)]
    original_bones = ROWS[0]["profiles"]["old"]["bone_points"]
    candidate_bones = ROWS[0]["profiles"]["new"]["bone_points"]
    bone_error = max(math.dist(value, candidate_bones[key]) for key, value in original_bones.items())
    shoes_same = original.crop((0, 410, 440, 500)).tobytes() == candidate.crop((0, 410, 440, 500)).tobytes()
    assert max(outline_delta) <= 2
    assert bone_error < 0.00001
    assert shoes_same
    return {"baseline": "approved_v21", "native_size": [440, 500], "max_outline_row_difference_px": max(outline_delta),
            "mean_outline_row_difference_px": statistics.mean(outline_delta), "body_and_leg_joint_position_error": bone_error,
            "standing_shoes_and_ground_pixels_identical": shoes_same,
            "edge_tolerance_reason": "Original split panels versus the new hanging rear-robe mesh."}


def speed_comparison():
    result = {}
    for name, target, start, stop in [("sample_slow", 114, 33, 54), ("sample_fast", 171, 75, 96), ("character_slow", 235, 117, 138), ("character_fast", 352.5, 159, 180)]:
        rows = ROWS[start:stop]
        data = NEW[start:stop]
        phases = [((NEW[index]["gait_phase"] - NEW[index - 1]["gait_phase"]) % 1) / ROWS[index]["delta"] for index in range(start, stop)]
        observed = statistics.mean(phases)
        declared = statistics.mean(item["gait_cadence_hz"] for item in data)
        assert all(abs(row["actual_speed"] - target) < 0.02 for row in rows)
        assert abs(observed - declared) < 0.0001, (name, observed, declared, "phase must advance once per simulation delta")
        result[name] = {"actual_speed": statistics.mean(row["actual_speed"] for row in rows), "mean_cadence_hz": declared,
                        "observed_phase_cycles_per_second": observed, "stride_half": statistics.mean(item["gait_stride_half"] for item in data),
                        "lift": statistics.mean(item["gait_lift"] for item in data), "window_frames": [start, stop - 1]}
        if all("arm_swing_scale" in item for item in data):
            result[name]["mean_arm_swing_scale"] = statistics.mean(item["arm_swing_scale"] for item in data)
    for field in ("mean_cadence_hz", "stride_half", "lift"):
        assert result["sample_slow"][field] < result["sample_fast"][field] < result["character_slow"][field] < result["character_fast"][field], (field, result)
    return result


def norm(vector):
    return math.sqrt(sum(item * item for item in vector))


def tile(indices, columns, name):
    sheet = Image.new("RGB", (440 * columns, 500 * math.ceil(len(indices) / columns)), "#58646a")
    for index, frame in enumerate(indices):
        sheet.paste(read_frame("new", frame), ((index % columns) * 440, (index // columns) * 500))
    sheet.save(DEST / name)


standing = standing_comparison()
speeds = speed_comparison()
release = [{"frame": index, "angles": NEW[index]["angles"], "angular_velocity": NEW[index]["angular_velocity"],
            "angle_norm": norm(NEW[index]["angles"])} for index in [204, 205, 207, 210, 215, 216, 227, 228, 234, 246, 270, 299]]
assert release[-1]["angle_norm"] < max(item["angle_norm"] for item in release[:5])
full = all((SOURCE / f"compare_{index:04d}.png").exists() for index in range(300))
input_validation = {"validated": False}
if "fast_step" in ROWS[0]:
    assert all(row["configured_test_speed"] == 114 for row in ROWS[12:96])
    assert all(row["configured_test_speed"] == 235 for row in ROWS[96:204])
    assert all(row["fast_step"] for row in ROWS[54:96] + ROWS[138:204])
    assert all(not row["fast_step"] for row in ROWS[12:54] + ROWS[96:138] + ROWS[204:])
    assert abs(ROWS[53]["stamina"] - ROWS[95]["stamina"] - 0.7) < 0.00001
    assert abs(ROWS[137]["stamina"] - ROWS[203]["stamina"] - 1.1) < 0.00001
    assert abs(ROWS[54]["stamina"] - (6.0 - 0.5 / 30.0)) < 0.00001
    for row in ROWS:
        assert len(row["hud_stamina_cells"]) == 6
        assert all(abs(fill - min(1.0, max(0.0, row["stamina"] - index))) < 0.00001
                   for index, fill in enumerate(row["hud_stamina_cells"]))
        assert abs(sum(row["hud_stamina_cells"]) - row["stamina"]) < 0.00001
    assert all(row["hp"] == 100 for row in ROWS), "Opponent must remain outside the visual movement replay"
    total_fast_seconds = sum(row["fast_step_seconds"] for row in ROWS)
    assert abs(total_fast_seconds - 3.6) < 0.00001
    input_validation = {"validated": True, "base_speeds_unchanged": [114, 235], "multiplier": 1.5,
                        "input_source": "Real double_tap_fast_step.feed_key press/release state", "fast_seconds": total_fast_seconds,
                        "stamina_rate_per_second": 0.5, "stamina_spent_in_two_uninterrupted_fast_stages": [0.7, 1.1],
                        "first_fast_frame_stamina": ROWS[54]["stamina"], "hud_fraction_matches_actual_stamina_all_frames": True,
                        "hud_and_stamina_samples": [{"frame": index, "stamina": ROWS[index]["stamina"], "hud_cells": ROWS[index]["hud_stamina_cells"]}
                                                   for index in [0, 53, 54, 55, 84, 95, 138, 168, 203, 204, 215, 228, 299]],
                        "input_events": [{"frame": row["frame"], "events": row["input_events"]} for row in ROWS if row["input_events"]]}
if full:
    assert input_validation["validated"], "Final capture must exercise the actual fast-step/stamina path"
    pause_hashes = {hashlib.sha256(read_frame("new", index).tobytes()).hexdigest() for index in range(215, 228)}
    assert len(pause_hashes) == 1

DEST.mkdir(parents=True, exist_ok=True)
if full:
    # Preserve superseded closed-front probe figures under the same formal
    # art category, keeping the current gallery free of conflicting variants.
    legacy_names = [f"{prefix}_{label}.png" for prefix in ("原生同输入对照", "实际战场")
                    for label in ("慢走57", "常速114", "快走235", "后退", "错步中相")]
    legacy_names += ["慢走常速快走_三个原生相位.png", "错步后摆_四相位.png"]
    archive = DEST / "历史_封闭后摆候选"
    assert archive.resolve().is_relative_to(DEST.resolve())
    for name in legacy_names:
        previous = DEST / name
        if not previous.exists():
            continue
        archive.mkdir(exist_ok=True)
        target = archive / name
        index = 2
        while target.exists():
            target = archive / f"{Path(name).stem}_{index}.png"
            index += 1
        previous.rename(target)
for index, name in [(0, "原版静站与新袍"), (42, "样例慢步114"), (84, "样例快步171"), (126, "角色慢步235"), (168, "角色快步352.5"),
                    (186, "转向甩摆"), (210, "急停"), (246, "回落中段"), (299, "回落稳定")]:
    shutil.copy2(SOURCE / f"compare_{index:04d}.png", DEST / f"原生同输入对照_{name}.png")
    if index in [0, 42, 84, 126, 168, 186, 210, 246, 299]:
        shutil.copy2(SOURCE / f"arena_{index:04d}.png", DEST / f"实际战场_{name}.png")
tile([42, 84, 126, 168], 2, "两种基准慢步与150%快步_四个原生相位.png")
tile([180, 181, 183, 186, 192], 5, "转向后摆甩动_五相位.png")
tile([204, 207, 215, 234, 246, 299], 3, "急停延续与回落_六相位.png")

if full:
    frames = [read_frame("compare", index) for index in range(0, 300, 2)]
    training = Image.new("RGB", (880, 600 * 13))
    for index, frame in enumerate(frames[::12]):
        training.paste(frame, (0, index * 600))
    durations = [round((index + 1) * 100 / 15) * 10 - round(index * 100 / 15) * 10 for index in range(len(frames))]
    assert sum(durations) == 10000
    gif = DEST / "长袍后摆垂坠与惯性_原生10秒.gif"
    for colors in (128, 80):
        palette = training.quantize(colors=colors, dither=Image.Dither.NONE)
        quantized = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
        quantized[0].save(gif, save_all=True, append_images=quantized[1:], duration=durations, loop=0, disposal=1, optimize=True)
        if gif.stat().st_size < 5_000_000:
            break
    assert gif.stat().st_size < 5_000_000

mirror = TOOLS / "output" / "cape_like_robe_open_mirror_probe" / "frames.jsonl"
mirror_checked = False
if mirror.exists():
    mirror_rows = [json.loads(line) for line in mirror.read_text(encoding="utf-8").splitlines()]
    assert len(mirror_rows) == 300
    assert all(row["facing"] == row["expected_facing"] for row in mirror_rows)
    assert all(data["garment"]["visible_leg_meshes"] == 2 for row in mirror_rows for key, data in row["profiles"].items() if key == "new")
    mirror_history = DEST / "历史_摆臂协调前反向检查"
    mirror_history.mkdir(exist_ok=True)
    shutil.copy2(mirror.parent / "compare_0186.png", mirror_history / "反朝向转身_原生对照.png")
    previous_mirror = DEST / "反朝向转身_原生对照.png"
    if previous_mirror.exists():
        previous_mirror.rename(mirror_history / "此前主目录_反朝向转身_原生对照.png")
    mirror_checked = True

report = {"current_target": "original_robed_shape_and_shoes_with_cape_like_rear_hem", "source_capture": str(SOURCE.relative_to(PROJECT)),
          "source_sha256": {name: hashlib.sha256((TOOLS / name).read_bytes()).hexdigest() for name in ["capture_cape_like_robe.gd", "capture_leg_supported_robe.gd", "cape_like_robe_skin.gd"]},
          "simulated_frames": 300, "seconds": 10, "native_comparison_resolution": [880, 600], "native_detail_resolution": [440, 500],
          "full_capture": full, "mirrored_physics_probe_300_frames_checked": mirror_checked, "all_facing_correct": True,
          "mirror_check_scope": "Supplemental open-leg rear-robe/leg physics probe predates the final arm-amplitude coordination; final full capture itself includes both facings.",
          "all_frames_two_complete_leg_chains_and_visible_leg_meshes": True, "natural_legs_outside_robe_allowed": True,
          "standing_comparison": standing, "actual_speed_gait_comparison": speeds, "stop_release_samples": release,
          "double_tap_fast_step_and_stamina": input_validation,
          "pause_12_frames_simulation_and_phase_frozen": True, "cloth_lowest_y_range": [min(data["lowest_cloth_y"] for data in NEW), max(data["lowest_cloth_y"] for data in NEW)],
          "native_pngs_repainted": False, "scope": "Lightweight 2D hanging rear-robe inertia with actual speed and acceleration; no full 3D cloth self-collision claim.",
          "artifacts": {path.name: path.stat().st_size for path in DEST.iterdir() if path.suffix in (".png", ".gif")}}
smoke_path = TOOLS / "output" / "cape_like_robe_smoke.json"
if smoke_path.exists():
    smoke = json.loads(smoke_path.read_text(encoding="utf-8"))
    assert smoke["passed"], smoke["failures"]
    shutil.copy2(smoke_path, DEST / "后摆与步态专项检查.json")
    report["independent_smoke"] = {key: smoke[key] for key in ("passed", "checks", "maximum_phase_error", "settled_angles", "settled_angular_speed")}
arm_smoke_path = TOOLS / "output" / "cape_arm_speed_smoke.json"
if arm_smoke_path.exists():
    arm_smoke = json.loads(arm_smoke_path.read_text(encoding="utf-8"))
    assert arm_smoke["passed"], arm_smoke.get("failures")
    shutil.copy2(arm_smoke_path, DEST / "摆臂与腿幅协调专项检查.json")
    report["arm_coordination_smoke"] = {"passed": arm_smoke["passed"], "checks": arm_smoke["checks"]}
shutil.copy2(SOURCE / "frames.jsonl", DEST / "同输入300帧姿态与后摆记录.jsonl")
(DEST / "原生动态与验收检查.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
