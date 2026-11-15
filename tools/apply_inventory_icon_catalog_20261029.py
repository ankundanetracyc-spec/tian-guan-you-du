"""Preflight and bind the approved inventory catalog; default is a read-only dry run.

Only icon_path, inventory_width, inventory_height and inventory_can_rotate may
change. Combat sprites, item mechanics and saved inventory are never rewritten.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/美术规范/行囊全物品图标与占地_20261029/物品占地与图标清单.json"
FIELDS = ("icon_path", "inventory_width", "inventory_height", "inventory_can_rotate")


def property_pattern(name: str) -> str:
    return rf"(?m)^{re.escape(name)}\s*=.*(?:\r?\n|$)"


def without_bindings(text: str) -> str:
    for name in FIELDS:
        text = re.sub(property_pattern(name), "", text)
    return text


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(26)
    if header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"Not a PNG: {path}")
    # Built-in image generation and the Godot packer both produce RGBA PNGs.
    if header[25] not in (4, 6):
        raise ValueError(f"PNG lacks alpha: {path}")
    return struct.unpack(">II", header[16:24])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="Write only after all 96 resources and 66 packed images pass preflight")
    args = parser.parse_args()
    rows = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
    assert len(rows) == 96 and len({r["path"] for r in rows}) == 96
    assert len({r["new_icon"] for r in rows}) == 66
    changes: list[tuple[Path, str]] = []
    for row in rows:
        target = (ROOT / row["path"]).resolve()
        assert target.is_relative_to(ROOT / "resources") and target.suffix == ".tres", target
        assert row["width"] > 0 and row["height"] > 0
        assert row["area"] == row["width"] * row["height"]
        assert row["footprint"] == "rectangle" and row["can_rotate"] is True
        assert row["pixels_w"] == row["width"] * 128 and row["pixels_h"] == row["height"] * 128
        assert row["new_icon"].startswith("res://textures/items/inventory_density_20261029/")
        image_path = ROOT / row["new_icon"].removeprefix("res://")
        assert png_size(image_path) == (row["pixels_w"], row["pixels_h"]), image_path
        if row["category"] == "秘籍":
            assert image_path.name == "manual_generic_2x3.png"
        if row["category"] == "丹药":
            assert image_path.name == "pill_2x2.png"
        if row["script"] == "PillContainerItemData":
            assert image_path.name == "pill_container_empty_porcelain.png"
        original = target.read_text(encoding="utf-8")
        assert re.search(rf'^definition_id\s*=\s*(?:&)?"{re.escape(row["id"])}"\s*$', original, re.M), target
        assert len(re.findall(property_pattern("icon_path"), original)) == 1, target
        updated = re.sub(property_pattern("icon_path"), f'icon_path = "{row["new_icon"]}"\n', original)
        for name, value in (("inventory_width", str(row["width"])), ("inventory_height", str(row["height"])), ("inventory_can_rotate", "true")):
            updated = re.sub(property_pattern(name), "", updated)
            updated += f"{name} = {value}\n"
        assert without_bindings(updated) == without_bindings(original), f"Unrelated property changed: {target}"
        if updated != original:
            changes.append((target, updated))
    if args.apply:
        for target, updated in changes:
            target.write_text(updated, encoding="utf-8", newline="\n")
    print(f'INVENTORY_CATALOG_{"APPLIED" if args.apply else "DRY_RUN"}_OK: 96 resources, 66 packed icons, {len(changes)} resource changes')


if __name__ == "__main__":
    main()
