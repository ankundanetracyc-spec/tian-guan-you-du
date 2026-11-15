"""Verify and package native Godot renders; no character pixels are retouched.

The GIF only quantizes/encodes original frames. PNG sheets tile native images
without scaling, drawing over the character, extending alpha, or hiding feet.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
import sys

from PIL import Image

PROJECT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv) > 1 else "leg_supported_robe_shoes_full")
DEST = PROJECT / "docs" / "美术规范" / "袍内腿部支撑样片_20261104"
DEST.mkdir(parents=True, exist_ok=True)
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
assert len(ROWS) == 270
assert all(row["frame"] == index for index, row in enumerate(ROWS))
assert all(row["facing"] == row["expected_facing"] for row in ROWS)
assert all(set(row["profiles"]) == {"old", "new"} for row in ROWS)
assert any(row["clip"] == "Step1" for row in ROWS[204:212]), "Dash was not triggered"
for row in ROWS:
    for mode, data in row["profiles"].items():
        assert data["bones"] == 21, (row["frame"], mode, data["bones"])
        assert data["meshes"] == (7 if mode == "old" else 6)
        if mode == "new":
            assert data["garment"]["continuous_mesh"]
        assert len(data["legs"]) == 2
        assert all(mesh["visible"] and mesh["in_tree"] for mesh in data["legs"])
        assert set(data["contacts"]) == {"near", "far"}
    assert ROWS[row["frame"]]["profiles"]["new"]["garment"]["complete_leg_bones"]
    assert row["profiles"]["new"]["garment"]["lowest_cloth_y"] <= 0.002

new_data = [row["profiles"]["new"]["garment"] for row in ROWS]
assert all(row["delta"] == 0.0 for row in ROWS[240:252])
assert len({data["support_steps"] for data in new_data[239:252]}) == 1
assert new_data[93]["knee_support_peak"] > new_data[125]["knee_support_peak"]
assert new_data[90]["knee_support_peak"] > 0.2


def read_frame(mode, index):
    image = Image.open(SOURCE / f"{mode}_{index:04d}.png").convert("RGB")
    assert image.size == ((880, 600) if mode == "compare" else (440, 500))
    return image


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
    shoes_same = original.crop((0, 410, 440, 500)).tobytes() == candidate.crop((0, 410, 440, 500)).tobytes()
    assert max(outline_delta) <= 2, "Standing silhouette changed more than the CPU/GPU edge tolerance"
    assert shoes_same, "Standing shoes and ground region must retain original pixels"
    original_bones = ROWS[0]["profiles"]["old"]["bone_points"]
    candidate_bones = ROWS[0]["profiles"]["new"]["bone_points"]
    bone_error = max(math.dist(value, candidate_bones[key]) for key, value in original_bones.items())
    assert bone_error < 0.00001, "Original standing body and leg chains changed"
    return {"original": "approved_v21", "native_size": [440, 500], "max_row_outline_difference_px": max(outline_delta),
            "mean_row_outline_difference_px": sum(outline_delta) / len(outline_delta), "standing_shoes_and_ground_region_pixels_identical": shoes_same,
            "standing_body_and_leg_joint_position_error": bone_error,
            "edge_tolerance_reason": "Original split GPU-skinned panels versus new continuous CPU-skinned cloth; original standing geometry and shoes retained."}


def tile_frames(mode, indices, columns, filename):
    size = (880, 600) if mode == "compare" else (440, 500)
    rows = (len(indices) + columns - 1) // columns
    sheet = Image.new("RGB", (size[0] * columns, size[1] * rows), "#58646a")
    for index, frame in enumerate(indices):
        sheet.paste(read_frame(mode, frame), ((index % columns) * size[0], (index // columns) * size[1]))
    sheet.save(DEST / filename)


def encode_gif(mode, filename, colors=128):
    frames = [read_frame(mode, index) for index in range(0, 270, 2)]
    size = frames[0].size
    training = Image.new("RGB", (size[0], size[1] * 12))
    for index, frame in enumerate(frames[::12][:12]):
        training.paste(frame, (0, index * size[1]))
    palette = training.quantize(colors=colors, dither=Image.Dither.NONE)
    quantized = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = [round((index + 1) * 100 / 15) * 10 - round(index * 100 / 15) * 10 for index in range(len(frames))]
    output = DEST / filename
    quantized[0].save(output, save_all=True, append_images=quantized[1:], duration=durations, loop=0, disposal=1, optimize=True)
    assert sum(durations) == 9000
    return output


for index, name in [(0, "待机"), (54, "前跑中相一"), (60, "前跑中相二"), (69, "前跑中相三"),
                    (93, "停下回落"), (139, "转向前进"), (172, "后退"), (207, "错步中相"),
                    (239, "急停回落"), (269, "恢复稳定")]:
    shutil.copy2(SOURCE / f"compare_{index:04d}.png", DEST / f"旧新同相位_{name}.png")
shutil.copy2(SOURCE / "compare_0000.png", DEST / "原版与新袍静站_原袍长与露鞋.png")
standing = standing_comparison()
tile_frames("new", [54, 57, 60, 63, 66, 69], 3, "前跑一周期_六个原生中间相位.png")
tile_frames("new", [89, 90, 93, 110, 125], 5, "停下后的局部释放与回落_五相位.png")
tile_frames("new", [204, 205, 207, 209, 211], 5, "错步全过程_五个原生中间相位.png")
for index, name in [(54, "前跑"), (139, "转向"), (172, "后退"), (207, "错步")]:
    shutil.copy2(SOURCE / f"arena_{index:04d}.png", DEST / f"实际战场_{name}.png")

full = all((SOURCE / f"compare_{index:04d}.png").exists() for index in range(270))
if full:
    for mode, filename in [("compare", "旧新同输入对照_原生9秒.gif"), ("new", "袍内双腿支撑_原生近景9秒.gif")]:
        output = encode_gif(mode, filename)
        if output.stat().st_size > 5_000_000:
            output = encode_gif(mode, filename, 80)
        assert output.stat().st_size < 5_000_000
    pause_hashes = {hashlib.sha256(read_frame("new", index).tobytes()).hexdigest() for index in range(239, 252)}
    assert len(pause_hashes) == 1, "Paused native render must freeze"

causality = json.loads((TOOLS / "output" / "leg_support_causality" / "causality.json").read_text(encoding="utf-8"))
assert causality["passed"], causality["failures"]
shutil.copy2(TOOLS / "output" / "leg_support_causality" / "causality.json", DEST / "腿骨与布面相关性检查.json")
mirror = TOOLS / "output" / "leg_supported_robe_shoes_mirror_probe" / "frames.jsonl"
mirror_verified = False
if mirror.exists():
    mirror_rows = [json.loads(line) for line in mirror.read_text(encoding="utf-8").splitlines()]
    assert len(mirror_rows) == 270
    assert all(row["facing"] == row["expected_facing"] for row in mirror_rows)
    assert all(mesh["visible"] and mesh["in_tree"] for row in mirror_rows for mode in row["profiles"].values() for mesh in mode["legs"])
    assert mirror_rows[207]["clip"] == "Step1"
    shutil.copy2(mirror.parent / "compare_0207.png", DEST / "反朝向错步中相_旧新对照.png")
    shutil.copy2(mirror, DEST / "反朝向270帧姿态记录.jsonl")
    mirror_verified = True

report = {
    "source_capture": str(SOURCE.relative_to(PROJECT)),
    "source_sha256": {name: hashlib.sha256((TOOLS / name).read_bytes()).hexdigest() for name in ["capture_leg_supported_robe.gd", "leg_supported_robe_skin.gd", "smoke_leg_support_causality.gd"]},
    "simulated_frames": len(ROWS), "capture_seconds": 9, "native_detail_scale": 4,
    "native_comparison_resolution": [880, 600], "native_new_detail_resolution": [440, 500],
    "full_capture": full, "mirrored_270_frames_verified": mirror_verified,
    "all_facing_correct": True, "all_270_frames_complete_two_leg_chains": True,
    "standing_original_comparison": standing,
    "all_270_frames_two_leg_meshes_visible_for_original_shoes": True, "all_270_frames_new_continuous_mesh": True,
    "cloth_lowest_y_range": [min(data["lowest_cloth_y"] for data in new_data), max(data["lowest_cloth_y"] for data in new_data)],
    "local_support_peak_range": [min(data["local_support_peak"] for data in new_data), max(data["local_support_peak"] for data in new_data)],
    "knee_support_peak_range": [min(data["knee_support_peak"] for data in new_data), max(data["knee_support_peak"] for data in new_data)],
    "pause_12_frames_simulation_frozen": True,
    "causality": {key: value for key, value in causality.items() if key in ["passed", "fixed_legs_90_frames_translation_and_clock_peak", "static_leg_only_pose_delta", "pause_12_frames_surface_delta", "released_90_frames_final_delta"]},
    "scope": "Original approved v21 standing robe length/shape and visible shoes retained. Lightweight 2D actual-leg capsule support and local spring release change movement only; upper legs/knees remain enclosed. This is not full 3D cloth self-collision.",
    "native_pngs_repainted": False,
    "artifacts": {path.name: path.stat().st_size for path in DEST.iterdir() if path.suffix in (".png", ".gif")},
}
shutil.copy2(SOURCE / "frames.jsonl", DEST / "同输入270帧姿态与布料记录.jsonl")
(DEST / "原生动态与验收检查.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
