from __future__ import annotations

import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "textures" / "battle" / "forest_wolf_idle_sheet_v2.png"
OUTPUT = ROOT / "textures" / "characters" / "forest_wolf_spine_v1"
RUNTIME = OUTPUT / "runtime"
PREVIEW = OUTPUT / "preview"

GRID = 4
FPS = 6
SPINE_VERSION = "3.8.75"
FRAME_NAMES = [f"wolf_idle_{i:02d}" for i in range(GRID * GRID)]


def remove_baked_checkerboard(sheet: np.ndarray) -> np.ndarray:
    """Clear the pale checkerboard using its 24px tile pattern."""
    rgba = sheet.copy()
    rgb = rgba[:, :, :3].astype(np.int16)
    spread = rgb.max(axis=2) - rgb.min(axis=2)
    yy, xx = np.indices(rgba.shape[:2])
    dark_tile = ((xx // 24 + yy // 24) % 2) == 0
    expected_luma = np.where(dark_tile, 237, 254)
    background_candidate = (
        (np.abs(rgb.mean(axis=2) - expected_luma) <= 18)
        & (spread <= 20)
        & (rgba[:, :, 3] > 0)
    ).astype(np.uint8)

    _, labels = cv2.connectedComponents(background_candidate, connectivity=4)
    edge_labels = np.unique(
        np.concatenate((labels[0, :], labels[-1, :], labels[:, 0], labels[:, -1]))
    )
    edge_labels = edge_labels[edge_labels != 0]
    if edge_labels.size:
        background = np.isin(labels, edge_labels)
        rgba[background, 3] = 0

    # Remove one-pixel neutral antialiasing around the cleared background.
    cleared = (rgba[:, :, 3] == 0).astype(np.uint8)
    near_neutral = (
        (np.abs(rgb.mean(axis=2) - expected_luma) <= 30)
        & (spread <= 32)
        & (rgba[:, :, 3] > 0)
    )
    expanded_background = cv2.dilate(cleared, np.ones((3, 3), np.uint8), iterations=1) > 0
    rgba[near_neutral & expanded_background, 3] = 0
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return rgba


def extract_frames(sheet: np.ndarray, frame_size: int) -> list[Image.Image]:
    """Keep the 16 wolf silhouettes, sort them by rows, and align paw baselines."""
    rgba = remove_baked_checkerboard(sheet)
    foreground = (rgba[:, :, 3] > 30).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(foreground, connectivity=8)
    components = [
        (int(stats[i, cv2.CC_STAT_AREA]), i)
        for i in range(1, count)
        if stats[i, cv2.CC_STAT_AREA] >= 20000
    ]
    if len(components) != GRID * GRID:
        raise ValueError(f"Expected 16 separate wolf frames after background removal, found {len(components)}")

    vertical = sorted(
        components,
        key=lambda item: int(stats[item[1], cv2.CC_STAT_TOP] + stats[item[1], cv2.CC_STAT_HEIGHT] / 2),
    )
    ordered = []
    for row_start in range(0, len(vertical), GRID):
        row = vertical[row_start : row_start + GRID]
        ordered.extend(
            sorted(
                row,
                key=lambda item: int(stats[item[1], cv2.CC_STAT_LEFT] + stats[item[1], cv2.CC_STAT_WIDTH] / 2),
            )
        )
    frames: list[Image.Image] = []
    for _, label in ordered:
        x = int(stats[label, cv2.CC_STAT_LEFT])
        y = int(stats[label, cv2.CC_STAT_TOP])
        w = int(stats[label, cv2.CC_STAT_WIDTH])
        h = int(stats[label, cv2.CC_STAT_HEIGHT])
        component = (labels[y : y + h, x : x + w] == label).astype(np.uint8)
        component = cv2.dilate(component, np.ones((3, 3), np.uint8), iterations=1) > 0
        crop = rgba[y : y + h, x : x + w].copy()
        crop[~component, 3] = 0
        crop[crop[:, :, 3] == 0, :3] = 0

        scale = min(1.0, (frame_size - 16) / w, (frame_size - 32) / h)
        out_w = max(1, round(w * scale))
        out_h = max(1, round(h * scale))
        sprite = Image.fromarray(crop, "RGBA")
        if (out_w, out_h) != (w, h):
            sprite = sprite.resize((out_w, out_h), Image.Resampling.LANCZOS)
        frame = Image.new("RGBA", (frame_size, frame_size), (0, 0, 0, 0))
        target_x = (frame_size - out_w) // 2
        target_y = frame_size - 12 - out_h
        frame.alpha_composite(sprite, (target_x, target_y))
        frame_pixels = np.asarray(frame).copy()
        component_count, component_labels, component_stats, _ = cv2.connectedComponentsWithStats(
            (frame_pixels[:, :, 3] > 0).astype(np.uint8), connectivity=8
        )
        for component_index in range(1, component_count):
            if component_stats[component_index, cv2.CC_STAT_AREA] < 24:
                speck = component_labels == component_index
                frame_pixels[speck, 3] = 0
                frame_pixels[speck, :3] = 0
        frame = Image.fromarray(frame_pixels, "RGBA")
        frames.append(frame)
    return frames


def save_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    if not SOURCE.is_file():
        raise FileNotFoundError(SOURCE)

    source = np.asarray(Image.open(SOURCE).convert("RGBA"))
    height, width = source.shape[:2]
    if width % GRID or height % GRID or width // GRID != height // GRID:
        raise ValueError(f"Expected a square {GRID}x{GRID} sheet, got {width}x{height}")

    frame_size = width // GRID
    frames = extract_frames(source, frame_size)
    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for index, (name, frame) in enumerate(zip(FRAME_NAMES, frames)):
        x = (index % GRID) * frame_size
        y = (index // GRID) * frame_size
        atlas.alpha_composite(frame, (x, y))

    RUNTIME.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    page_name = "forest_wolf_idle.png"
    atlas.save(RUNTIME / page_name, optimize=True)

    for name, frame in zip(FRAME_NAMES, frames):
        frame.save(RUNTIME / f"{name}.png", optimize=True)

    atlas_lines = [
        page_name,
        f"size: {width}, {height}",
        "format: RGBA8888",
        "filter: Linear,Linear",
        "repeat: none",
    ]
    for index, name in enumerate(FRAME_NAMES):
        x = (index % GRID) * frame_size
        y = (index // GRID) * frame_size
        atlas_lines.extend(
            [
                "",
                name,
                "  rotate: false",
                f"  xy: {x}, {y}",
                f"  size: {frame_size}, {frame_size}",
                f"  orig: {frame_size}, {frame_size}",
                "  offset: 0, 0",
                "  index: -1",
            ]
        )
    (RUNTIME / "forest_wolf_spine.atlas").write_text("\n".join(atlas_lines) + "\n", encoding="utf-8")

    attachments = {
        name: {
            "type": "region",
            "name": name,
            "path": name,
            "x": 0,
            "y": 0,
            "width": frame_size,
            "height": frame_size,
        }
        for name in FRAME_NAMES
    }
    skeleton = {
        "skeleton": {
            "hash": "forest-wolf-idle-v1",
            "spine": SPINE_VERSION,
            "x": -frame_size / 2,
            "y": -frame_size / 2,
            "width": frame_size,
            "height": frame_size,
            "images": "./",
            "audio": "",
        },
        "bones": [
            {"name": "root"},
            {"name": "sprite", "parent": "root", "length": frame_size},
        ],
        "slots": [
            {"name": "wolf_sprite", "bone": "sprite", "attachment": FRAME_NAMES[0]}
        ],
        "skins": [
            {
                "name": "default",
                "attachments": {"wolf_sprite": attachments},
            }
        ],
        "animations": {
            "idle": {
                "slots": {
                    "wolf_sprite": {
                        "attachment": [
                            {"time": index / FPS, "name": name}
                            for index, name in enumerate(FRAME_NAMES)
                        ]
                        + [{"time": len(FRAME_NAMES) / FPS, "name": FRAME_NAMES[0]}]
                    }
                }
            }
        },
    }
    save_json(RUNTIME / "forest_wolf_spine.spinejson", skeleton)
    save_json(RUNTIME / "forest_wolf_spine.json", skeleton)

    # Contact sheet on a neutral canvas so transparent cutout edges are visible.
    cell_preview = 314
    margin = 12
    contact = Image.new("RGB", (GRID * cell_preview + (GRID + 1) * margin,
                                 GRID * cell_preview + (GRID + 1) * margin), "#e8e6e1")
    for index, frame in enumerate(frames):
        x = margin + (index % GRID) * (cell_preview + margin)
        y = margin + (index // GRID) * (cell_preview + margin)
        contact.paste(frame, (x, y), frame)
    contact.save(PREVIEW / "forest_wolf_idle_contact_sheet.png", optimize=True)

    gif_frames = []
    for frame in frames:
        bg = Image.new("RGBA", (frame_size, frame_size), (232, 230, 225, 255))
        bg.alpha_composite(frame)
        gif_frames.append(bg.convert("P", palette=Image.Palette.ADAPTIVE))
    gif_frames[0].save(
        PREVIEW / "forest_wolf_idle_preview.gif",
        save_all=True,
        append_images=gif_frames[1:],
        duration=round(1000 / FPS),
        loop=0,
        optimize=True,
        disposal=2,
    )

    readme = f"""# 林地凶狼 Spine 待机序列 v1

来源：`textures/battle/forest_wolf_idle_sheet_v2.png`（4×4、共 16 帧）。新版序列图把灰白棋盘格画进了像素；本资源按棋盘格颜色和连通轮廓去背，从每行识别四只完整妖兽，再统一脚底线与画布中心。循环速度沿用 `outputs/forest_wolf_idle_preview.html` 中的 6 FPS。

## 文件

- `runtime/forest_wolf_spine.spinejson`：Spine {SPINE_VERSION} 格式运行数据。
- `runtime/forest_wolf_spine.atlas`、`runtime/forest_wolf_idle.png`：图集页与区域定义。
- `runtime/wolf_idle_00.png` 至 `wolf_idle_15.png`：逐帧透明切片，便于替换和检查。
- `preview/forest_wolf_idle_preview.gif`：6 FPS 循环预览。
- `preview/forest_wolf_idle_contact_sheet.png`：去背结果总览。

## 绑定说明

该资源保留了序列帧本身的动作，用一个 `sprite` 骨骼和一个附件槽按 {FPS} FPS 切换 16 张全身图。原始素材没有头、躯干、四肢、尾巴的分层，所以这份数据不提供独立关节摆动；要改成可摆腿、摇尾的真骨骼，需要分层原画或补绘遮挡区域。

## 重建

在项目根目录运行 `python tools/build_forest_wolf_spine.py`。需要 Pillow、NumPy 和 OpenCV。
"""
    (OUTPUT / "README.md").write_text(readme, encoding="utf-8")


if __name__ == "__main__":
    main()
