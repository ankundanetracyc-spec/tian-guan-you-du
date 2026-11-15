"""Review the real trial replay without retiming or modifying the scene frames."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

root = Path(__file__).resolve().parent / "output"
current = root / "yuxu_robed_v21_arena"
previous = root / "yuxu_grounded_v20_arena"
records = [json.loads(line) for line in (current / "frames.jsonl").read_text().splitlines()]
assert len(records) == 360
assert all(row["facing"] == row["expected_facing"] for row in records)
assert all(abs(row["move_speed"] - 114.0) < 0.001 for row in records)
sole_error = max(abs(foot["sole_y"] + foot["lift"]) for row in records for foot in row["feet"].values())
floor_penetration = max(foot["sole_y"] for row in records for foot in row["feet"].values())
worst_support_gap = max(-max(foot["sole_y"] for foot in row["feet"].values()) for row in records)
assert sole_error < .02 and floor_penetration < .02 and worst_support_gap < .02
summary = {
    "rendered_frames": len(records), "duration_seconds": 12,
    "ordinary_trial_speed_before": 235, "ordinary_trial_speed_after": 114,
    "stride_rig_units_before": 56, "stride_rig_units_after": 44,
    "cycle_frequency_hz_before": 2.65, "cycle_frequency_hz_after": 1.8,
    "maximum_visible_sole_height_error_rig_units": sole_error,
    "maximum_floor_penetration_rig_units": floor_penetration,
    "maximum_support_foot_gap_rig_units": worst_support_gap,
    "maximum_segment_rotation_degrees": max(abs(value)*180/3.141592653589793 for row in records for value in row["cloth_angles"]),
    "forward_run_chest_degrees": [min(r["torso_degrees"] for r in records[36:66]), max(r["torso_degrees"] for r in records[36:66])],
    "backward_walk_chest_degrees": [min(r["torso_degrees"] for r in records[180:204]), max(r["torso_degrees"] for r in records[180:204])],
}
(current / "ground_contact_review.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

def pair(left: Image.Image, right: Image.Image, left_label: str, right_label: str) -> Image.Image:
    canvas = Image.new("RGB", (880, 525), "#58646a")
    canvas.paste(left.convert("RGB"), (0, 25))
    canvas.paste(right.convert("RGB"), (440, 25))
    draw = ImageDraw.Draw(canvas)
    draw.text((12, 8), left_label, fill="white")
    draw.text((452, 8), right_label, fill="white")
    return canvas.quantize(colors=96, dither=Image.Dither.NONE)

def save_loop(frames: list[Image.Image], name: str) -> None:
    durations = [round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(len(frames))]
    frames[0].save(current / name, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2)

frames = []
for index in range(0, 360, 2):
    with Image.open(previous / f"detail_{index:04d}.png") as before, Image.open(current / f"detail_{index:04d}.png") as after:
        frames.append(pair(before, after, f"v20 / 235 px/s / {index/30:.2f}s", "v21 / 114 px/s / restrained robe gait"))
save_loop(frames, "before_after_12s.gif")
frames = []
for index in range(360):
    with Image.open(current / f"detail_{(index*2)%360:04d}.png") as normal, Image.open(current / f"detail_{index:04d}.png") as slow:
        frames.append(pair(normal, slow, "Actual playback / 1x / repeats once", "Slow inspection / 0.5x / no frame synthesis"))
save_loop(frames, "normal_slow_24s.gif")
print(json.dumps(summary, ensure_ascii=False))
