"""Encode native replay evidence with a shared palette; inspect capture metadata."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).resolve().parent / "output" / (sys.argv[1] if len(sys.argv) > 1 else "realtime_closed_robe_20261104")
DEST = PROJECT / "docs" / "美术规范" / "即时角色闭合袍摆_20261104"
DEST.mkdir(parents=True, exist_ok=True)
FONT = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
SMALL = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 15)
LABELS = {
    "idle": "待机", "start_forward": "起步前进", "diagonal_up": "斜向前进",
    "turn_left": "转向左移", "stop_left": "停下回落", "standing_strike": "站立出掌",
    "backward_right": "朝左后退", "turn_forward_right": "转身前进／移动出掌",
    "forward_dash": "前向错步",
    "stop_settle": "急停与回落", "pause_freeze": "暂停冻结", "resume_settle": "恢复后平稳待机",
}
rows = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
assert len(rows) == 360
assert all(row["facing"] == row["expected_facing"] for row in rows)
assert all("garment" in row for row in rows), "The current garment adapter was not loaded"
assert all(row["delta"] == 0 for row in rows[288:300])
assert all(row["bones"] == rows[288]["bones"] for row in rows[288:300]), "Paused bone pose changed"
assert all(row["garment"]["simulation_steps"] == rows[288]["garment"]["simulation_steps"] for row in rows[288:300]), "Paused garment physics advanced"


def native(prefix, index):
    suffix = "jpg" if prefix == "arena" else "png"
    return Image.open(SOURCE / f"{prefix}_{index:04d}.{suffix}").convert("RGB")


def phase_text(index):
    return f"{index / 30:.2f}s  {LABELS[rows[index]['label']]}"


def compare(index):
    frame = Image.new("RGB", (880, 560), "#424d53")
    frame.paste(native("legacy", index), (0, 31))
    frame.paste(native("current", index), (440, 31))
    draw = ImageDraw.Draw(frame)
    draw.text((12, 5), "旧版开衩袍形（仅作历史对照）", font=FONT, fill="#e2e8e9")
    draw.text((452, 5), "当前闭合长袍", font=FONT, fill="#e2e8e9")
    draw.line((440, 0, 440, 530), fill="#88959b")
    draw.text((12, 534), phase_text(index), font=FONT, fill="#e2e8e9")
    return frame


def compact_gif(frames, output, fps=15, colors=128):
    # One representative palette avoids per-frame color shifts and excess size.
    tiles = [frames[i].resize((240, 150)) for i in range(0, len(frames), max(1, len(frames) // 12))]
    palette_strip = Image.new("RGB", (240 * len(tiles), 150))
    for i, tile in enumerate(tiles): palette_strip.paste(tile, (i * 240, 0))
    palette = palette_strip.quantize(colors=colors, dither=Image.Dither.NONE)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [round((i + 1) * 100 / fps) * 10 - round(i * 100 / fps) * 10 for i in range(len(frames))]
    encoded[0].save(output, save_all=True, append_images=encoded[1:], duration=durations, loop=0, disposal=1, optimize=True)


sample_indices = list(range(0, 360, 2))
comparison = [compare(i) for i in sample_indices]
compact_gif(comparison, DEST / "闭袍与旧版同输入对照_12秒.gif")
arena = []
current = []
for i in sample_indices:
    closeup = Image.new("RGB", (440, 530), "#424d53")
    closeup.paste(native("current", i), (0, 0))
    ImageDraw.Draw(closeup).text((10, 503), phase_text(i), font=FONT, fill="white")
    current.append(closeup)
for i in range(0, 360, 3):
    shot = native("arena", i).resize((640, 360), Image.Resampling.LANCZOS)
    draw = ImageDraw.Draw(shot)
    draw.rectangle((5, 322, 436, 355), fill="#424d53")
    draw.text((12, 327), phase_text(i), font=FONT, fill="white")
    arena.append(shot)
compact_gif(arena, DEST / "石坪实际窗口_12秒.gif", fps=10, colors=48)
compact_gif(current, DEST / "当前闭袍原生四倍近景_12秒.gif")


def contact(indices, name, columns=4):
    height = math.ceil(len(indices) / columns)
    sheet = Image.new("RGB", (columns * 330, height * 400), "#424d53")
    draw = ImageDraw.Draw(sheet)
    for i, index in enumerate(indices):
        x, y = (i % columns) * 330, (i // columns) * 400
        shot = native("current", index).resize((330, 375), Image.Resampling.LANCZOS)
        sheet.paste(shot, (x, y))
        draw.text((x + 5, y + 378), phase_text(index), font=SMALL, fill="white")
    sheet.save(DEST / name)


contact([18, 21, 25, 30, 66, 69, 74, 80, 150, 156, 164, 172, 208, 211, 214, 217], "起步转向后退出掌_中间相位.png")
contact([228, 231, 235, 240, 246, 252, 264, 276, 287, 288, 294, 299, 300, 312, 336, 359], "急停回落与暂停_中间相位.png")
contact([0, 24, 54, 72, 103, 129, 163, 205, 232, 248, 289, 359], "关键帧总览.png")
compare(163).save(DEST / "后退同相位前后对照.png")
for index, name in [(30, "起步"), (74, "转向"), (129, "出掌"), (163, "后退"), (232, "急停"), (252, "回落"), (359, "静止")]:
    native("current", index).save(DEST / f"近景_{name}.png")
native("current", 219).save(DEST / "近景_错步.png")
native("arena", 163).save(DEST / "石坪实际窗口_后退.png")
native("arena", 232).save(DEST / "石坪实际窗口_急停.png")
mirror_probe = SOURCE.parent / "realtime_closed_robe_knee_mirror_probe_20261104" / "current_0219.png"
if mirror_probe.exists(): shutil.copy2(mirror_probe, DEST / "反向错步_原生四倍近景.png")
shutil.copy2(SOURCE / "frames.jsonl", DEST / "采集姿态与衣摆记录.jsonl")
paused_equal = len({hashlib.sha256(native("current", i).tobytes()).hexdigest() for i in range(288, 300)}) == 1
report = {
    "source_capture": str(SOURCE.relative_to(PROJECT)),
    "garment_source_sha256": hashlib.sha256((SOURCE.parent.parent / "realtime_garment_skin.gd").read_bytes()).hexdigest(),
    "rendered_frames": len(rows), "replay_seconds": 12,
    "active_simulation_seconds": rows[-1]["clock"], "detail_native_scale": 4,
    "facing_correct_all_frames": True, "paused_bones_equal_12_frames": True,
    "paused_simulation_steps_equal_12_frames": True,
    "paused_native_images_equal_12_frames": paused_equal,
    "garment_start": rows[18]["garment"], "garment_stop": rows[228]["garment"],
    "garment_settle": rows[359]["garment"],
    "lowest_cloth_y_range": [min(row["garment"]["lowest_cloth_y"] for row in rows), max(row["garment"]["lowest_cloth_y"] for row in rows)],
    "maximum_foot_contact_y": max(c["center"][1] for row in rows for c in row["contacts"].values()),
    "stop_decay_samples": [
        {"frame": rows[index]["frame"], "angle_norm": math.sqrt(sum(v * v for v in rows[index]["garment"]["angles"])),
         "speed_norm": math.sqrt(sum(v * v for v in rows[index]["garment"]["angular_velocity"]))}
        for index in [228, 231, 240, 252, 264, 276, 287, 288, 299, 300, 359]
    ],
    "artifacts": {p.name: p.stat().st_size for p in DEST.iterdir() if p.is_file() and p.suffix in (".gif", ".png")},
}
(DEST / "动态采集检查.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
