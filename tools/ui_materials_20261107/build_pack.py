"""Index unchanged imagegen PNGs; write atlas resources and inspection metadata.

Image pixels are read only. This utility never writes or edits an image.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "textures/ui/combat_board_20261107_v1"


def image_info(name: str) -> dict:
    path = PACK / name
    with Image.open(path) as source:
        source.verify()
    with Image.open(path) as source:
        rgba = np.asarray(source.convert("RGBA"))
        alpha = rgba[:, :, 3]
        rgb = rgba[:, :, :3]
        return {
            "file": name,
            "width": source.width,
            "height": source.height,
            "mode": source.mode,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "bytes": path.stat().st_size,
            "alpha_min": int(alpha.min()),
            "alpha_max": int(alpha.max()),
            "transparent_fraction": round(float(np.mean(alpha == 0)), 6),
            "corner_alpha": [int(alpha[0, 0]), int(alpha[-1, -1])],
            "visible_red_fringe_pixels": int(np.sum(
                (rgb[:, :, 0] > 200) & (rgb[:, :, 1] < 100)
                & (rgb[:, :, 2] < 100) & (alpha > 30)
            )),
        }


def alpha_bounds(name: str, roi: tuple[int, int, int, int] | None = None) -> list[int]:
    with Image.open(PACK / name) as source:
        alpha = np.asarray(source.getchannel("A"))
    if roi is None:
        roi = (0, 0, alpha.shape[1], alpha.shape[0])
    x, y, width, height = roi
    yy, xx = np.where(alpha[y:y + height, x:x + width] > 8)
    if not len(xx):
        raise ValueError(f"Empty part: {name}, {roi}")
    return [x + int(xx.min()), y + int(yy.min()),
            int(xx.max() - xx.min() + 1), int(yy.max() - yy.min() + 1)]


def slot_roi(name: str, col: int, row: int, cols: int, rows: int) -> tuple[int, int, int, int]:
    with Image.open(PACK / name) as source:
        width, height = source.size
    x0, x1 = round(col * width / cols), round((col + 1) * width / cols)
    y0, y1 = round(row * height / rows), round((row + 1) * height / rows)
    return x0, y0, x1 - x0, y1 - y0


def main() -> None:
    images = {name: image_info(name) for name in (
        "grid_parts_atlas.png", "source_panel.png", "tooltip_panel.png",
        "buttons_atlas.png", "resources_atlas.png",
    )}
    for info in images.values():
        assert info["mode"] == "RGBA", info
        assert info["alpha_min"] == 0 and info["alpha_max"] >= 250, info
        assert info["corner_alpha"] == [0, 0], info
        assert info["visible_red_fringe_pixels"] == 0, info

    parts: dict[str, dict] = {}

    def add(key: str, image: str, rect: list[int], *,
            source_insets: list[int] | None = None,
            display_insets: list[int] | None = None) -> None:
        x, y, width, height = rect
        info = images[image]
        assert width > 0 and height > 0
        assert x >= 0 and y >= 0
        assert x + width <= info["width"] and y + height <= info["height"]
        part = {"image": image, "rect": rect, "atlas_resource": f"atlas/{key}.tres"}
        if source_insets:
            assert source_insets[0] + source_insets[2] < width
            assert source_insets[1] + source_insets[3] < height
            part["source_insets"] = source_insets
            part["display_insets"] = display_insets
        parts[key] = part

    grid_names = ["cell_face_source", "outer_edge", "outer_corner", "inner_corner",
                  "hand_connector", "hand_socket", "junction_pin", "focus_outline"]
    for index, key in enumerate(grid_names):
        roi = slot_roi("grid_parts_atlas.png", index % 4, index // 4, 4, 2)
        add(key, "grid_parts_atlas.png", alpha_bounds("grid_parts_atlas.png", roi))
    # The painted inner L turns at its lower-left, unlike the upper-left outer L.
    parts["outer_corner"]["anchor_fraction"] = [0.095, 0.095]
    parts["inner_corner"]["anchor_fraction"] = [0.095, 0.905]
    # Atlas-region inset excludes the source square's painted edge; no PNG is edited.
    x, y, width, height = parts["cell_face_source"]["rect"]
    inset = 8
    side = min(width, height) - 2 * inset
    add("cell_face", "grid_parts_atlas.png", [x + inset, y + inset, side, side])
    assert side >= 128

    for index, key in enumerate(("primary_normal", "primary_hover", "secondary_normal", "secondary_hover")):
        roi = slot_roi("buttons_atlas.png", index % 2, index // 2, 2, 2)
        add(key, "buttons_atlas.png", alpha_bounds("buttons_atlas.png", roi),
            source_insets=[42, 24, 42, 24], display_insets=[14, 8, 14, 8])

    add("source_panel", "source_panel.png", alpha_bounds("source_panel.png"),
        source_insets=[64, 64, 64, 64], display_insets=[23, 23, 23, 23])
    add("tooltip_panel", "tooltip_panel.png", alpha_bounds("tooltip_panel.png"),
        source_insets=[48, 48, 48, 48], display_insets=[12, 12, 12, 12])

    resource_info = images["resources_atlas.png"]
    width, height = resource_info["width"], resource_info["height"]
    split = round(height * .6)
    add("resource_frame", "resources_atlas.png", alpha_bounds("resources_atlas.png", (0, 0, width, split)),
        source_insets=[100, 80, 100, 80], display_insets=[25, 20, 25, 20])
    for index, key in enumerate(("stamina_glyph", "mana_glyph", "spirit_glyph")):
        x0, x1 = round(index * width / 3), round((index + 1) * width / 3)
        add(key, "resources_atlas.png", alpha_bounds("resources_atlas.png", (x0, split, x1 - x0, height - split)))

    atlas_dir = PACK / "atlas"
    atlas_dir.mkdir(exist_ok=True)
    for key, part in parts.items():
        region = ", ".join(map(str, part["rect"]))
        resource = (
            '[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
            f'[ext_resource type="Texture2D" path="res://textures/ui/combat_board_20261107_v1/{part["image"]}" id="1"]\n\n'
            '[resource]\natlas = ExtResource("1")\n'
            f'region = Rect2({region})\nfilter_clip = true\n'
        )
        (atlas_dir / f"{key}.tres").write_text(resource, encoding="utf-8")

    manifest = {
        "version": 1,
        "role": "ui_materials_only",
        "selected_reference": "docs/美术规范/局内战斗布阵与开战理想图_20261107/38_用户选定_逐格拼接参考.png",
        "minimum_source_cell_pixels": 128,
        "part_order": [key for key in parts if key != "cell_face_source"],
        "parts": parts,
        "images": images,
        "layer_order": ["cell_surface", "grid_and_edges", "placed_art"],
        "nine_slice_note": "Source insets are atlas pixels; display insets are preview layout units. Use the supplied draw helpers when scaling.",
        "scope": "No combat rules, skill shapes, real resource values or saved-game data are defined by this pack.",
    }
    serialized = json.dumps(manifest, ensure_ascii=False, indent=2)
    (PACK / "skin_manifest.json").write_text(serialized + "\n", encoding="utf-8")
    (PACK / "skin_manifest.js").write_text("window.BATTLE_UI_SKIN_MANIFEST = " + serialized + ";\n", encoding="utf-8")
    print(json.dumps({"png_count": len(images), "atlas_texture_count": len(parts),
                      "source_cell_region": parts["cell_face"]["rect"],
                      "alpha_checks_passed": True}, ensure_ascii=False))


if __name__ == "__main__":
    main()
