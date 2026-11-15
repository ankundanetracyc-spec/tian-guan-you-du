"""Package actual native Guigu-like run captures; keep their original pixels."""
from pathlib import Path
import hashlib
import json
import shutil
import sys
from PIL import Image, ImageChops, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv) > 1 else "guigu_guard_run_final")
DEST = PROJECT / "docs/美术规范/鬼谷持架跑姿与衣摆_20261104"
DEST.mkdir(parents=True, exist_ok=True)
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
META = json.loads((SOURCE / "capture_source.json").read_text(encoding="utf-8"))
assert len(ROWS) == 192
for key, name in [("new_source_sha256", "reference_walk_skin.gd"),
                  ("old_source_sha256", "reference_walk_pre_guigu_baseline_20261104.gd")]:
    assert META[key] == hashlib.sha256((TOOLS / name).read_bytes()).hexdigest(), name
if "source_profile_sha256" in META:
    assert META["source_profile_sha256"] == hashlib.sha256((TOOLS / "guigu_run_pose_profile_20261104.json").read_bytes()).hexdigest()
assert all(r["base_speed"] == 114 and r["stamina"] == 6 and r["hp"] == 100 for r in ROWS)
assert all(abs(r["actual_speed"] - 114) < .02 for r in ROWS[:90])
assert all(r["actual_speed"] == 0 for r in ROWS[90:150])

def shot(mode, number):
    tag = "idle" if number is None else f"{number:04d}"
    return Image.open(SOURCE / f"{mode}_{tag}.png").convert("RGB")

def encode(mode, name):
    frames = [shot(mode, n) for n in range(0, 150, 2)]
    w, h = frames[0].size
    selected = frames[::5]
    training = Image.new("RGB", (w, h * len(selected)))
    for i, im in enumerate(selected):
        training.paste(im, (0, i * h))
    palette = training.quantize(colors=128, dither=Image.Dither.NONE)
    frames = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in frames]
    duration = [round((i + 1) * 100 / 15) * 10 - round(i * 100 / 15) * 10 for i in range(75)]
    frames[0].save(DEST / name, save_all=True, append_images=frames[1:], duration=duration,
                   loop=0, disposal=1, optimize=True)
    with Image.open(DEST / name) as check:
        total = 0
        for i in range(check.n_frames):
            check.seek(i)
            total += check.info.get("duration", 0)
        assert total == 5000

encode("new", "鬼谷式跑动3秒与停步2秒_近景.gif")
encode("arena", "鬼谷式跑动3秒与停步2秒_实际局内.gif")
encode("compare", "改前与鬼谷式跑动_同输入5秒.gif")

font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)
phases = [(12, "起步 0.43秒"), (74, "持续跑动 2.50秒"),
          (92, "松键余势 0.10秒"), (148, "停步回垂 1.97秒")]
sheet = Image.new("RGB", (880, 1064), "#424d53")
native_sheet = Image.new("RGB", (400, 424), "#424d53")
for index, (frame, label) in enumerate(phases):
    x, y = (index % 2) * 440, (index // 2) * 532
    sheet.paste(shot("new", frame), (x, y + 32))
    ImageDraw.Draw(sheet).text((x + 8, y + 5), label, fill="#e7e9e4", font=font)
    arena = shot("arena", frame)
    fx, fy = ROWS[frame]["native"]["foot_origin"]
    crop = arena.crop((round(fx) - 100, round(fy) - 155, round(fx) + 100, round(fy) + 25))
    nx, ny = (index % 2) * 200, (index // 2) * 212
    native_sheet.paste(crop, (nx, ny + 32))
    ImageDraw.Draw(native_sheet).text((nx + 5, ny + 5), label, fill="#e7e9e4", font=font)
sheet.save(DEST / "跑姿衣摆与停步_四相位.png")
native_sheet.save(DEST / "实际尺寸_跑停四相位.png")
cycle = Image.new("RGB", (440 * 3, 532 * 2), "#424d53")
for index, frame in enumerate([60, 64, 68, 72, 76, 80]):
    x, y = (index % 3) * 440, (index // 3) * 532
    cycle.paste(shot("new", frame), (x, y + 32))
    p = ROWS[frame]["profiles"]["new"]["gait"]["phase"]
    ImageDraw.Draw(cycle).text((x + 8, y + 5), f"步态相位 {p:.2f}", fill="#e7e9e4", font=font)
cycle.save(DEST / "持架跑动_六相位.png")
for frame in [12, 74, 92, 148, 150, 160, 162, 164, 170, 174, 182, 190, 191]:
    for mode in ("new", "arena", "compare"):
        original = SOURCE / f"{mode}_{frame:04d}.png"
        if original.exists():
            shutil.copy2(original, DEST / original.name)
for name in ("capture_source.json", "frames.jsonl", "idle_record.json"):
    shutil.copy2(SOURCE / name, DEST / name)
shutil.copy2(SOURCE / "new_idle.png", DEST / "原静止轮廓.png")
shutil.copy2(TOOLS / "reference_walk_pre_guigu_baseline_20261104.gd", DEST / "本轮改前源码.gd.txt")
shutil.copy2(TOOLS / "reference_walk_skin.gd", DEST / "鬼谷式跑动源码.gd.txt")
shutil.copy2(TOOLS / "guigu_run_pose_profile_20261104.json", DEST / "原骨链归一化姿势.json")
report = dict(META, source_capture=str(SOURCE.relative_to(PROJECT)), diagnostic_frames=len(ROWS),
              primary_gif_seconds=5, primary_run_seconds=3, primary_release_seconds=2,
              ordinary_movement_stamina_cost=0, ai_and_camera_rules="unchanged",
              idle_detail_pixel_equal=ImageChops.difference(shot("old", None), shot("new", None)).getbbox() is None,
              native_pixels_repainted=False, native_crops_resampled=False,
              selected_phases=[{"frame":n,"label":label,"new":ROWS[n]["profiles"]["new"]["garment"]} for n,label in phases],
              turn_check_frames=[150,160,162,164,170,174,182,190,191])
smoke = TOOLS / "output/guigu_guard_run_smoke.json"
if smoke.exists():
    data = json.loads(smoke.read_text(encoding="utf-8"))
    assert data["passed"] and data["source_sha256"] == META["new_source_sha256"]
    shutil.copy2(smoke, DEST / "持架跑动专项检查.json")
    report["checks"] = data
report["artifacts"] = {p.name:p.stat().st_size for p in DEST.iterdir() if p.suffix in (".gif", ".png")}
(DEST / "原生捕捉与验收记录.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps({"destination":str(DEST),"idle_pixels_equal":report["idle_detail_pixel_equal"],
                  "artifacts":report["artifacts"]},ensure_ascii=False,indent=2))
