"""Package the upper-body tension review from native, unretouched captures."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import statistics
import sys

from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv)>1 else "reference_upper_open_short_probe")
BASELINE = TOOLS / "output/reference_walk_full/frames.jsonl"
DEST = PROJECT / "docs/美术规范/斗法移动上身张力_20261104"
DEST.mkdir(parents=True, exist_ok=True)
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
IDLE = json.loads((SOURCE / "idle_record.json").read_text(encoding="utf-8"))
OLD_ROWS = [json.loads(line) for line in BASELINE.read_text(encoding="utf-8").splitlines()]
assert len(ROWS) == 84 and len(OLD_ROWS) == 300
assert all(row["base_speed"] == 114 and row["stamina"] == 6 and row["hp"] == 100 for row in ROWS)
assert all(abs(row["actual_speed"] - 114) < 0.02 for row in ROWS)
assert all(not row["backward"] for row in ROWS[:60]) and all(row["backward"] for row in ROWS[60:])
assert all(row["profiles"]["new"]["bones"] == 21 and row["profiles"]["new"]["meshes"] == 7 for row in ROWS)
assert IDLE["actual_speed"] == 0


def vec(a, b):
    return [a[0] - b[0], a[1] - b[1]]


def measurements(data):
    near = data["arm_bones"]["near_hand"]["position"]
    shoulder = data["arm_bones"]["near_upper"]["position"]
    elbow = data["arm_bones"]["near_forearm"]["position"]
    far = data["arm_bones"]["far_hand"]["position"]
    hip = data["bone_points"]["pelvis"]
    forearm = vec(near, elbow)
    return {"near_hand_forward_of_hip": near[0] - hip[0], "near_hand_above_hip": hip[1] - near[1],
            "near_hand_forward_of_shoulder": near[0] - shoulder[0], "near_hand_below_shoulder": near[1] - shoulder[1],
            "forearm_angle_degrees_down_from_horizontal": math.degrees(math.atan2(forearm[1], forearm[0])),
            "near_far_hand_distance": math.dist(near, far), "near_far_horizontal_span": abs(near[0] - far[0]),
            "head_forward_of_hip": data["bone_points"]["head"][0] - hip[0]}


def summarize(values):
    return {key: {"min": min(row[key] for row in values), "mean": statistics.mean(row[key] for row in values), "max": max(row[key] for row in values)} for key in values[0]}


old_samples = [{"frame": row["frame"], "metrics": measurements(row["profiles"]["new"])} for row in OLD_ROWS[30:71]]
new_samples = [{"frame": row["frame"], "metrics": measurements(row["profiles"]["new"])} for row in ROWS[8:60]]
old_stats = summarize([row["metrics"] for row in old_samples])
new_stats = summarize([row["metrics"] for row in new_samples])
assert new_stats["near_hand_forward_of_hip"]["mean"] > old_stats["near_hand_forward_of_hip"]["mean"]
assert new_stats["near_hand_below_shoulder"]["mean"] < old_stats["near_hand_below_shoulder"]["mean"]
native_metrics = []
for row in ROWS[8:60]:
    if "native" not in row:
        continue
    points = row["native"]["points"]
    native_metrics.append({"near_hand_forward_of_shoulder_px": points["near_hand"][0] - points["near_upper"][0],
                           "near_hand_above_hip_px": points["pelvis"][1] - points["near_hand"][1],
                           "near_far_hand_distance_px": math.dist(points["near_hand"], points["far_hand"])})


def shot(mode, number):
    tag = "idle" if number is None else f"{number:04d}"
    image = Image.open(SOURCE / f"{mode}_{tag}.png").convert("RGB")
    assert image.size == ((880, 600) if mode in ("arena", "compare") else (440, 500))
    return image


def native_crop(number):
    row = IDLE if number is None else ROWS[number]
    x, y = row["native"]["foot_origin"]
    return shot("arena", number).crop((round(x) - 75, round(y) - 130, round(x) + 85, round(y) + 25))


samples = [(None, "静止"), (18, "前进 0.63 秒"), (40, "前进 1.37 秒"), (64, "后退 0.17 秒"), (74, "后退 0.50 秒"), (82, "后退 0.77 秒")]
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
small_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 12)
sheet = Image.new("RGB", (1320, 1064), "#424d53")
native_sheet = Image.new("RGB", (480, 366), "#424d53")
for index, (number, label) in enumerate(samples):
    col, row = index % 3, index // 3
    sheet.paste(shot("new", number), (col * 440, row * 532 + 32))
    ImageDraw.Draw(sheet).text((col * 440 + 16, row * 532 + 4), label, fill="#e7e9e4", font=font)
    native_sheet.paste(native_crop(number), (col * 160, row * 183 + 28))
    ImageDraw.Draw(native_sheet).text((col * 160 + 6, row * 183 + 6), label, fill="#e7e9e4", font=small_font)
sheet.save(DEST / "静止前进后退_六相位.png")
native_sheet.save(DEST / "实际游戏尺寸_未缩放局部六相位.png")
for number, label in [(18, "前进一"), (40, "前进二"), (74, "后退")]:
    shutil.copy2(SOURCE / f"compare_{number:04d}.png", DEST / f"旧方案与新展开动作_{label}.png")
    shutil.copy2(SOURCE / f"arena_{number:04d}.png", DEST / f"实际局内_{label}.png")
shutil.copy2(SOURCE / "compare_idle.png", DEST / "旧方案与新展开动作_静止.png")


def encode(mode, name):
    frames = [shot(mode, number) if mode != "native_crop" else native_crop(number) for number in range(0, 60, 2)]
    width, height = frames[0].size
    training = Image.new("RGB", (width, height * 10))
    for index, image in enumerate(frames[::3]):
        training.paste(image, (0, index * height))
    palette = training.quantize(colors=128, dither=Image.Dither.NONE)
    frames = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in frames]
    durations = [round((i + 1) * 100 / 15) * 10 - round(i * 100 / 15) * 10 for i in range(len(frames))]
    assert sum(durations) == 2000
    frames[0].save(DEST / name, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=1, optimize=True)


encode("compare", "上身展开_原生近景对照2秒.gif")
encode("arena", "上身展开_实际局内2秒.gif")
encode("native_crop", "实际游戏尺寸_未缩放人物局部2秒.gif")

report = {"status": "upper_body_open_candidate_short_review", "single_movement_speed": 114, "ordinary_movement_stamina_cost": 0,
          "source_capture": str(SOURCE.relative_to(PROJECT)), "forward_clip_seconds": 2, "backward_review_seconds": 0.8,
          "fixture_initial_opponent_distance": 600, "opponent_ai_and_camera": "unchanged live battle rules",
          "comparison_image_left": "cape_like_robe_skin old scheme", "comparison_image_right": "new reference_walk_skin open upper-body candidate",
          "bone_measurement_baseline_source": str(BASELINE.relative_to(PROJECT)), "bone_measurement_baseline_profile": "new, previous delivered compact pose",
          "bone_measurement_baseline_sha256": hashlib.sha256(BASELINE.read_bytes()).hexdigest(),
          "baseline_window": [30, 70], "candidate_forward_window": [8, 59], "baseline_metrics_world_units": old_stats,
          "candidate_metrics_world_units": new_stats, "candidate_metrics_native_pixels": summarize(native_metrics),
          "cadence_mean_previous": statistics.mean(row["profiles"]["new"]["garment"]["gait_cadence_hz"] for row in OLD_ROWS[30:71]),
          "cadence_mean_candidate": statistics.mean(row["profiles"]["new"]["garment"]["gait_cadence_hz"] for row in ROWS[8:60]),
          "body_lean_degrees_previous_mean": statistics.mean(math.degrees(row["profiles"]["new"]["garment"]["reference_chest_angle"]) for row in OLD_ROWS[30:71]),
          "body_lean_degrees_candidate_mean": statistics.mean(math.degrees(row["profiles"]["new"]["garment"]["reference_chest_angle"]) for row in ROWS[8:60]),
          "source_sha256": {name: hashlib.sha256((TOOLS / name).read_bytes()).hexdigest() for name in ["capture_reference_upper_short.gd", "capture_reference_walk.gd", "reference_walk_skin.gd", "cape_like_robe_skin.gd"]},
          "native_pngs_repainted": False, "native_crop_resampled": False,
          "metrics_scope": "Own rig bone measurements. No exact source-video joint or cadence measurement is claimed. Baseline bone comparison is independent of the old-scheme rendering panel.",
          "artifacts": {path.name: path.stat().st_size for path in DEST.iterdir() if path.suffix in (".png", ".gif")}}
for source_name, destination_name in [("reference_walk_pose_smoke.json", "本轮专用姿态与惯性检查.json"),
                                      ("reference_walk_actual_contact.json", "本轮真实腿长与鞋底接触检查.json")]:
    source = TOOLS / "output" / source_name
    if source.exists():
        data = json.loads(source.read_text(encoding="utf-8"))
        if "passed" in data: assert data["passed"]
        shutil.copy2(source, DEST / destination_name)
        report[source_name] = data
(DEST / "上轮成品new与本次new_骨向量对照.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
(DEST / "骨向量逐相位数据.json").write_text(json.dumps({"baseline": old_samples, "candidate": new_samples}, ensure_ascii=False, indent=2), encoding="utf-8")
shutil.copy2(SOURCE / "frames.jsonl", DEST / "短录姿态与资源记录.jsonl")
shutil.copy2(SOURCE / "idle_record.json", DEST / "静止姿态记录.json")
print(json.dumps({"passed": True, "previous": old_stats, "candidate": new_stats, "native": report["candidate_metrics_native_pixels"], "artifacts": report["artifacts"]}, ensure_ascii=False, indent=2))
