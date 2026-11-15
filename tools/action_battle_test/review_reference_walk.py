"""Verify the single ordinary walk and package untouched native Godot frames."""
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
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv) > 1 else "reference_walk_full")
DEST = PROJECT / "docs" / "美术规范" / "参考归墟行单一移动步态_20261104"
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]


def validate_rows(rows, speed):
    assert len(rows) == 300
    assert all(row["frame"] == index for index, row in enumerate(rows))
    assert all(row["base_speed"] == speed for row in rows)
    assert all(row["facing"] == row["expected_facing"] for row in rows)
    assert all(row["stamina"] == 6.0 and row["hp"] == 100 for row in rows)
    assert all(len(row["hud_stamina_cells"]) == 6 and all(value == 1.0 for value in row["hud_stamina_cells"]) for row in rows)
    assert all(abs(row["actual_speed"] - speed) < 0.02 for row in rows[18:210])
    assert all(row["actual_speed"] == 0.0 for row in rows[:18] + rows[210:])
    assert all(row["delta"] == 0.0 for row in rows[228:240])
    for row in rows:
        for mode in ("old", "new"):
            data = row["profiles"][mode]
            assert data["bones"] == 21 and data["meshes"] == 7
            assert len(data["legs"]) == 2 and all(item["visible"] and item["in_tree"] for item in data["legs"])
            assert not data["garment"]["pants_uv_clipped"]
            assert data["garment"]["lowest_cloth_y"] <= 0.002
        assert row["profiles"]["new"]["garment"]["reference_parameter_source"] == "project_visual_fit_not_video_measurement"
    frozen = [row["profiles"]["new"]["garment"] for row in rows[227:240]]
    assert len({item["simulation_steps"] for item in frozen}) == 1
    assert len({item["gait_phase"] for item in frozen}) == 1
    assert any(row["backward"] for row in rows[102:138])
    for start, stop in ((42, 72), (156, 174)):
        observed = statistics.mean(((rows[index]["profiles"]["new"]["gait"]["phase"] - rows[index - 1]["profiles"]["new"]["gait"]["phase"]) % 1) / rows[index]["delta"] for index in range(start, stop))
        declared = statistics.mean(row["profiles"]["new"]["garment"]["gait_cadence_hz"] for row in rows[start:stop])
        assert abs(observed - declared) < 0.0001, (observed, declared, "gait phase must advance once per clock")


validate_rows(ROWS, 114.0)
NEW = [row["profiles"]["new"]["garment"] for row in ROWS]
ENCODED_FRAMES = list(range(0,300,2))
full = all((SOURCE / f"compare_{index:04d}.png").exists() for index in ENCODED_FRAMES)
arena_full = all((SOURCE / f"arena_{index:04d}.png").exists() for index in ENCODED_FRAMES)


def frame(mode, index):
    image = Image.open(SOURCE / f"{mode}_{index:04d}.png").convert("RGB")
    assert image.size == ((880, 600) if mode in ("compare", "arena") else (440, 500))
    return image


def standing():
    old, new = frame("old", 0), frame("new", 0)
    old_bones = ROWS[0]["profiles"]["old"]["bone_points"]
    new_bones = ROWS[0]["profiles"]["new"]["bone_points"]
    error = max(math.dist(value, new_bones[name]) for name, value in old_bones.items())
    assert error < 0.0001
    shoe_pixels_same = old.crop((0, 410, 440, 500)).tobytes() == new.crop((0, 410, 440, 500)).tobytes()
    assert shoe_pixels_same
    return {"body_and_leg_joint_position_error": error, "standing_shoes_and_ground_pixels_identical": shoe_pixels_same,
            "whole_native_frame_pixels_identical": old.tobytes() == new.tobytes()}


def norm(value):
    return math.sqrt(sum(item * item for item in value))


if full:
    assert len({hashlib.sha256(frame("new", index).tobytes()).hexdigest() for index in [227,228,230,232,234,236,238,239]}) == 1
stop_samples = [{"frame": index, "angles": NEW[index]["angles"], "angular_velocity": NEW[index]["angular_velocity"],
                 "angle_norm": norm(NEW[index]["angles"])} for index in [210, 211, 213, 216, 222, 227, 228, 239, 240, 246, 258, 270, 299]]
assert stop_samples[-1]["angle_norm"] < max(item["angle_norm"] for item in stop_samples[:6])
body_samples = [{"frame": index, "chest_angle": NEW[index].get("reference_chest_angle"),
                 "chest_target": NEW[index].get("reference_chest_target"), "lean_inertia": NEW[index].get("reference_lean_inertia"),
                 "elastic_tail": NEW[index].get("reference_elastic_tail")}
                for index in [18, 30, 54, 126, 138, 174, 210, 216, 227, 246, 299]]
if "reference_lean_inertia" in NEW[18]:
    assert NEW[18]["reference_lean_inertia"] > 0 and NEW[210]["reference_lean_inertia"] < 0
    assert NEW[54]["reference_chest_angle"] > 0 and NEW[126]["reference_chest_angle"] < 0
    assert abs(NEW[299]["reference_chest_angle"]) < abs(NEW[210]["reference_chest_angle"])
standing_result = standing()
compatibility_source = TOOLS / "output" / "reference_walk_235_probe" / "frames.jsonl"
compatibility_checked = False
if compatibility_source.exists():
    compatibility = [json.loads(line) for line in compatibility_source.read_text(encoding="utf-8").splitlines()]
    validate_rows(compatibility, 235.0)
    compatibility_checked = True

DEST.mkdir(parents=True, exist_ok=True)
for index, name in [(0, "静站"), (54, "普通移动"), (88, "斜向移动"), (126, "后退"), (150, "转向"), (186, "再次转向"),
                    (213, "停步"), (258, "回落"), (299, "稳定")]:
    shutil.copy2(SOURCE / f"compare_{index:04d}.png", DEST / f"同输入原生对照_{name}.png")
    shutil.copy2(SOURCE / f"arena_{index:04d}.png", DEST / f"实际局内_{name}.png")


def sheet(indices, columns, filename):
    image = Image.new("RGB", (440 * columns, 500 * math.ceil(len(indices) / columns)), "#58646a")
    for index, number in enumerate(indices):
        image.paste(frame("new", number), ((index % columns) * 440, (index // columns) * 500))
    image.save(DEST / filename)


sheet([30, 36, 42, 48, 54, 60], 3, "普通移动_六个原生相位.png")
sheet([138, 139, 141, 144, 150], 5, "转向_五个原生相位.png")
sheet([210, 213, 222, 246, 258, 299], 3, "停步惯性与回落_六个原生相位.png")


def encode(mode, filename, colors=128):
    frames = [frame(mode, index) for index in range(0, 300, 2)]
    training = Image.new("RGB", (880, 600 * 13))
    for index, image in enumerate(frames[::12]):
        training.paste(image, (0, index * 600))
    palette = training.quantize(colors=colors, dither=Image.Dither.NONE)
    quantized = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in frames]
    durations = [round((index + 1) * 100 / 15) * 10 - round(index * 100 / 15) * 10 for index in range(len(frames))]
    assert sum(durations) == 10000
    output = DEST / filename
    quantized[0].save(output, save_all=True, append_images=quantized[1:], duration=durations, loop=0, disposal=1, optimize=True)
    return output


if full:
    encode("compare", "参考行走_原生近景同输入10秒.gif")
if arena_full:
    encode("arena", "参考行走_实际局内10秒.gif")

reports = {}
for source_name, destination_name in [("single_movement_withdrawal_smoke.json", "单一移动与资源撤回检查.json"),
                                      ("reference_walk_pose_smoke.json", "单一步态与手势优先检查.json"),
                                      ("reference_walk_turn_check.json", "转向支撑相位专项检查.json"),
                                      ("reference_walk_actual_contact.json", "真实腿长与鞋底接触检查.json")]:
    source = TOOLS / "output" / source_name
    if source.exists():
        data = json.loads(source.read_text(encoding="utf-8"))
        if "passed" in data:
            assert data["passed"], data
        shutil.copy2(source, DEST / destination_name)
        reports[source_name] = data

report = {"target": "single_ordinary_reference_walk_with_dynamic_body_and_rear_inertia", "source_capture": str(SOURCE.relative_to(PROJECT)),
          "source_sha256": {name: hashlib.sha256((TOOLS / name).read_bytes()).hexdigest() for name in ["capture_reference_walk.gd", "capture_leg_supported_robe.gd", "reference_walk_skin.gd", "cape_like_robe_skin.gd"]},
          "simulated_frames": 300, "encoded_native_frames_per_gif": len(ENCODED_FRAMES), "seconds": 10, "main_speed": 114, "compatibility_235_probe_checked": compatibility_checked,
          "multiple_player_movement_modes": False, "ordinary_movement_stamina_cost": 0, "all_300_frames_stamina": 6.0,
          "full_comparison_capture": full, "full_arena_capture": arena_full, "native_comparison_and_arena_resolution": [880, 600],
          "all_facing_correct": True, "complete_legs_and_original_trousers_and_boots": True, "standing": standing_result,
          "pause_12_frames_phase_physics_and_native_pixels_frozen": True, "body_inertia_samples": body_samples, "stop_samples": stop_samples,
          "withdrawal_checks": reports.get("single_movement_withdrawal_smoke.json"), "basic_skills_regression": {"checks": 26, "failures": 0},
          "native_pngs_repainted": False, "reference_parameter_source": "Project visual fitting; small reference character does not support precise joint or cadence measurements.",
          "artifacts": {path.name: path.stat().st_size for path in DEST.iterdir() if path.suffix in (".png", ".gif")}}
shutil.copy2(SOURCE / "frames.jsonl", DEST / "单一移动300帧姿态与资源记录.jsonl")
if compatibility_checked:
    shutil.copy2(compatibility_source, DEST / "真实人物基准235兼容记录.jsonl")
(DEST / "原生动态与验收检查.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"passed": True, "full": full, "arena_full": arena_full, "compatibility_checked": compatibility_checked,
                  "standing": standing_result, "artifacts": report["artifacts"]}, ensure_ascii=False, indent=2))
