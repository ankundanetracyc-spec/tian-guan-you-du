"""Package the native run-to-release cloth take; never renders new pixels."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import sys
from PIL import Image, ImageChops, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv) > 1 else "running_cloth_return_final")
DEST = PROJECT / "docs/美术规范/跑动衣摆后飘与停步回落_20261104"
DEST.mkdir(parents=True, exist_ok=True)
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
META = json.loads((SOURCE / "capture_source.json").read_text(encoding="utf-8"))
assert len(ROWS) == 192
assert META["old_source_sha256"] == "1daba5359212c43277861c2c8e5eccce2a068e3ae5a8949d9c2d716ebb0d735a"
assert META["new_source_sha256"] == hashlib.sha256((TOOLS / "reference_walk_skin.gd").read_bytes()).hexdigest()
assert all(r["base_speed"] == 114 and r["stamina"] == 6 and r["hp"] == 100 for r in ROWS)
assert all(abs(r["actual_speed"] - 114) < .02 for r in ROWS[:90])
assert all(r["actual_speed"] == 0 for r in ROWS[90:150])

def shot(mode, number):
    tag = "idle" if number is None else f"{number:04d}"
    return Image.open(SOURCE / f"{mode}_{tag}.png").convert("RGB")

def encode(mode, name):
    frames = [shot(mode, n) for n in range(0, 150, 2)]
    w, h = frames[0].size
    training = Image.new("RGB", (w, h * 15))
    for i, im in enumerate(frames[::5]):
        training.paste(im, (0, i * h))
    palette = training.quantize(colors=128, dither=Image.Dither.NONE)
    frames = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in frames]
    duration = [round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(75)]
    frames[0].save(DEST / name, save_all=True, append_images=frames[1:], duration=duration,
                   loop=0, disposal=1, optimize=True)

encode("new", "新版衣摆_跑动3秒与松键回落2秒_近景.gif")
encode("arena", "新版衣摆_跑动3秒与松键回落2秒_实际局内.gif")
encode("compare", "原v3与新衣摆_同输入跑停5秒.gif")

font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)
phases = [(12, "起步 0.43秒"), (74, "持续跑动 2.50秒"),
          (92, "松键余势 0.10秒"), (148, "停步回垂 1.97秒")]
sheet = Image.new("RGB", (880, 1064), "#424d53")
native_sheet = Image.new("RGB", (400, 424), "#424d53")
for index, (frame, label) in enumerate(phases):
    x, y = (index % 2)*440, (index // 2)*532
    sheet.paste(shot("new", frame), (x, y+32))
    ImageDraw.Draw(sheet).text((x+8, y+5), label, fill="#e7e9e4", font=font)
    arena = shot("arena", frame)
    fx, fy = ROWS[frame]["native"]["foot_origin"]
    crop = arena.crop((round(fx)-100, round(fy)-155, round(fx)+100, round(fy)+25))
    nx, ny = (index % 2)*200, (index // 2)*212
    native_sheet.paste(crop, (nx, ny+32))
    ImageDraw.Draw(native_sheet).text((nx+5, ny+5), label, fill="#e7e9e4", font=font)
    for mode in ("new", "arena", "compare"):
        shutil.copy2(SOURCE / f"{mode}_{frame:04d}.png", DEST / f"{mode}_{frame:04d}.png")
sheet.save(DEST / "起步_持续后飘_松键余势_回垂四相位.png")
native_sheet.save(DEST / "实际尺寸未缩放_跑停四相位.png")
for frame in (150, 160, 162, 164, 170, 174, 182, 190, 191):
    for mode in ("new", "arena"):
        shutil.copy2(SOURCE / f"{mode}_{frame:04d}.png", DEST / f"{mode}_{frame:04d}.png")
shutil.copy2(SOURCE / "new_idle.png", DEST / "新版原静止轮廓.png")
for name in ("capture_source.json", "frames.jsonl", "idle_record.json"):
    shutil.copy2(SOURCE / name, DEST / name)
shutil.copy2(TOOLS / "reference_walk_run_v3_cloth_baseline_20261104.gd", DEST / "旧v3基准源码快照.gd.txt")
shutil.copy2(TOOLS / "reference_walk_skin.gd", DEST / "新版衣摆源码.gd.txt")

max_body_position_error = 0.
max_body_angle_error = 0.
for row in ROWS:
    old, new = row["profiles"]["old"], row["profiles"]["new"]
    for bone, position in old["bone_points"].items():
        target = new["bone_points"][bone]
        max_body_position_error = max(max_body_position_error, math.dist(position, target))
    for bone, state in old["arm_bones"].items():
        target = new["arm_bones"][bone]
        max_body_position_error = max(max_body_position_error, math.dist(state["position"], target["position"]))
        max_body_angle_error = max(max_body_angle_error, abs(state["rotation"]-target["rotation"]))
    for bone, angle in old["body_rotations"].items():
        max_body_angle_error = max(max_body_angle_error, abs(angle-new["body_rotations"][bone]))

report = dict(META, source_capture=str(SOURCE.relative_to(PROJECT)), diagnostic_frames=len(ROWS),
              primary_gif_seconds=5, primary_run_seconds=3, primary_release_seconds=2,
              ordinary_movement_stamina_cost=0, ai_and_camera_rules="unchanged",
              old_v3_vs_new_body_position_max_error=max_body_position_error,
              old_v3_vs_new_body_rotation_max_error=max_body_angle_error,
              idle_detail_pixel_equal=ImageChops.difference(shot("old",None),shot("new",None)).getbbox() is None,
              native_pixels_repainted=False, native_crops_resampled=False,
              selected_phases=[{"frame":frame,"label":label,"old":ROWS[frame]["profiles"]["old"]["garment"],
                                "new":ROWS[frame]["profiles"]["new"]["garment"]} for frame,label in phases],
              turn_check_frames=[150,160,162,164,170,174,182,190,191])
report["artifacts"] = {p.name:p.stat().st_size for p in DEST.iterdir() if p.suffix in (".gif", ".png")}
cloth_report = TOOLS / "output/reference_cloth_trail_smoke.json"
if cloth_report.exists():
    shutil.copy2(cloth_report, DEST / "衣摆后飘与停步物理检查.json")
    report["cloth_checks"] = json.loads(cloth_report.read_text(encoding="utf-8"))
(DEST / "衣摆跑停与身体不改动记录.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps({"source":META,"max_body_position_error":max_body_position_error,
                  "max_body_angle_error":max_body_angle_error,"artifacts":report["artifacts"]},ensure_ascii=False,indent=2))
