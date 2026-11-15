"""Package unmodified native candidate renders and check capture metadata.

The GIF changes only palette/encoding; the PNG figures tile original screenshots.
No character pixels are painted, extended, removed, or resampled here.
"""
from pathlib import Path
import hashlib
import json
import shutil
import sys

from PIL import Image

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).resolve().parent / "output" / (sys.argv[1] if len(sys.argv) > 1 else "closed_robe_options_full")
DEST = PROJECT / "docs" / "美术规范" / "闭袍全遮脚三方案_20261104"
DEST.mkdir(parents=True, exist_ok=True)
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
assert len(ROWS) == 270
assert all(row["frame"] == index for index, row in enumerate(ROWS))
assert all(row["facing"] == row["expected_facing"] for row in ROWS)
assert all(set(row["profiles"]) == {"A", "B", "C"} for row in ROWS)
assert any(row["clip"] == "AttackIdle" for row in ROWS[108:135])
assert any(row["clip"] == "Step1" for row in ROWS[204:212]), "Dash was not triggered"
for row in ROWS:
    for profile, data in row["profiles"].items():
        assert data["bones"] == 21
        assert data["meshes"] == 6
        assert data["garment"]["continuous_mesh"]
        assert len(data["legs"]) == 2, (row["frame"], profile, data["legs"])
        assert all(not mesh["visible"] and not mesh["in_tree"] for mesh in data["legs"]), (row["frame"], profile, data["legs"])
        assert set(data["contacts"]) == {"near", "far"}


def shot(index):
    frame = Image.open(SOURCE / f"compare_{index:04d}.png").convert("RGB")
    assert frame.size == (1320, 600), frame.size
    return frame


def same_size_sheet(indices, output):
    sheet = Image.new("RGB", (1320, 600 * len(indices)))
    for row_index, frame_index in enumerate(indices):
        sheet.paste(shot(frame_index), (0, row_index * 600))
    sheet.save(DEST / output)


def gif(colors):
    frames = [shot(index) for index in range(0, 270, 2)]
    # Palette training is separate from the full-size output frames.
    palette_rows = [frames[index].resize((330, 150)) for index in range(0, len(frames), 11)]
    strip = Image.new("RGB", (330, 150 * len(palette_rows)))
    for index, frame in enumerate(palette_rows):
        strip.paste(frame, (0, index * 150))
    palette = strip.quantize(colors=colors, dither=Image.Dither.NONE)
    quantized = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    duration = [round((index + 1) * 100 / 15) * 10 - round(index * 100 / 15) * 10 for index in range(len(frames))]
    output = DEST / "闭袍全遮脚三方案_同屏9秒.gif"
    quantized[0].save(output, save_all=True, append_images=quantized[1:], duration=duration, loop=0, disposal=1, optimize=True)
    return output


for index, name in [(0, "待机"), (29, "前进"), (68, "转向"), (110, "出掌"), (149, "后退"), (207, "错步中相"), (269, "回落")]:
    shot(index).save(DEST / f"三方案同相位_{name}.png")
same_size_sheet([29, 149, 207], "三方案静态对照_前进后退错步.png")
same_size_sheet([204, 205, 207, 209, 211], "错步全过程_五个中间相位.png")

full = all((SOURCE / f"compare_{index:04d}.png").exists() for index in range(270))
if full:
    output = gif(128)
    if output.stat().st_size > 5_000_000:
        output = gif(80)
    assert output.stat().st_size < 5_000_000, "GIF exceeds requested size"

mirror_source = SOURCE.parent / "closed_robe_options_mirror_probe"
mirror_checked = False
if (mirror_source / "frames.jsonl").exists():
    mirror_rows = [json.loads(line) for line in (mirror_source / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
    assert len(mirror_rows) == 270
    assert all(row["facing"] == row["expected_facing"] for row in mirror_rows)
    assert all(len(data["legs"]) == 2 and all(not mesh["visible"] and not mesh["in_tree"] for mesh in data["legs"])
               for row in mirror_rows for data in row["profiles"].values())
    assert mirror_rows[207]["clip"] == "Step1" and mirror_rows[207]["facing"] == -1
    shutil.copy2(mirror_source / "compare_0207.png", DEST / "反向错步中相_三方案.png")
    shutil.copy2(mirror_source / "frames.jsonl", DEST / "反朝向采集姿态记录.jsonl")
    mirror_checked = True

per_profile = {}
for profile in "ABC":
    data = [row["profiles"][profile] for row in ROWS]
    per_profile[profile] = {
        "bones": 21,
        "meshes": 6,
        "hidden_leg_meshes_per_frame": 2,
        "all_270_frames_leg_render_hidden": True,
        "continuous_body_all_frames": True,
        "contact_shadow_uses_two_full_leg_chains": True,
        "cloth_lowest_y_range": [min(item["garment"]["lowest_cloth_y"] for item in data), max(item["garment"]["lowest_cloth_y"] for item in data)],
        "simulation_steps_first_last": [data[0]["garment"]["simulation_steps"], data[-1]["garment"]["simulation_steps"]],
    }

report = {
    "source_capture": str(SOURCE.relative_to(PROJECT)),
    "capture_script_sha256": hashlib.sha256((SOURCE.parent.parent / "capture_closed_robe_options.gd").read_bytes()).hexdigest(),
    "candidate_script_sha256": hashlib.sha256((SOURCE.parent.parent / "closed_robe_options_skin.gd").read_bytes()).hexdigest(),
    "simulated_frames": len(ROWS),
    "capture_seconds": 9,
    "native_detail_scale": 4,
    "native_comparison_resolution": [1320, 600],
    "full_capture": full,
    "dash_frames": [row["frame"] for row in ROWS if row["clip"] == "Step1"],
    "attack_frames": [row["frame"] for row in ROWS if row["clip"] == "AttackIdle"],
    "all_facing_correct": True,
    "mirrored_replay_270_frames_checked": mirror_checked,
    "profiles": per_profile,
    "coverage_method": "Candidate-only leg/foot render meshes hidden; complete leg bones/contact calculations retained. This is not a pixel-perfect physical cloth-occlusion proof.",
    "native_pngs_repainted": False,
    "artifacts": {path.name: path.stat().st_size for path in DEST.iterdir() if path.suffix in (".png", ".gif")},
}
shutil.copy2(SOURCE / "frames.jsonl", DEST / "采集姿态与三方案记录.jsonl")
(DEST / "动态采集检查.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
