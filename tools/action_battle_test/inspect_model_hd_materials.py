"""Inspect actual model HD PNG inputs without touching materials or registration.

Each record is derived from one stable byte snapshot. Before/after file hashes
and a final batch hash pass catch concurrent edits; only changed files reread.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timedelta, timezone
import hashlib
from io import BytesIO
import json
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[2]
DEFAULT_INPUT = PROJECT / "tools/action_battle_test/art/guigu_male101_ink_hd"
DEFAULT_OUTPUT = PROJECT / "docs/美术规范/鬼谷原作网格与素材重构_20261104/高清材质实际尺寸清单.json"
SPRITE_IDS = (65806, 79543, 84807, 85649, 87557, 87679)
SHANGHAI = timezone(timedelta(hours=8))
STATUS = "本轮待最终注册验收"


def timestamp() -> str:
    return datetime.now(SHANGHAI).isoformat(timespec="milliseconds")


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def inspect_stable(path: Path, sprite_id: int, previous_attempts: int = 0) -> dict:
    for attempt in range(1, 9):
        read_at = timestamp()
        data = path.read_bytes()
        before_hash = digest(data)
        with Image.open(BytesIO(data)) as png:
            if png.format != "PNG":
                raise ValueError(f"Expected PNG: {path}")
            width, height = png.size
            png_mode = png.mode
            alpha = png.convert("RGBA").getchannel("A")
            nonzero_bbox = alpha.getbbox()
            opaque_bbox = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
        after_hash = digest(path.read_bytes())
        if before_hash == after_hash:
            return {
                "sprite_id": sprite_id,
                "png_path": str(path.resolve()),
                "width": width,
                "height": height,
                "png_mode": png_mode,
                "png_sha256": before_hash,
                "file_size_bytes": len(data),
                "alpha_nonzero_bbox": list(nonzero_bbox) if nonzero_bbox is not None else None,
                "alpha_ge_128_bbox": list(opaque_bbox) if opaque_bbox is not None else None,
                "read_at": read_at,
                "status": STATUS,
                "stable_read_check": {
                    "before_sha256": before_hash,
                    "after_sha256": after_hash,
                    "match": True,
                    "attempts": previous_attempts + attempt,
                },
            }
    raise RuntimeError(f"PNG changed during eight successive reads: {path}")


def inspect_all(directory: Path) -> dict:
    started_at = timestamp()
    materials = {sprite_id: inspect_stable(directory / f"sprite_{sprite_id}.png", sprite_id) for sprite_id in SPRITE_IDS}
    batch_passes = 0
    for batch_passes in range(1, 9):
        changed = []
        for sprite_id, record in materials.items():
            if digest(Path(record["png_path"]).read_bytes()) != record["png_sha256"]:
                changed.append(sprite_id)
        if not changed:
            break
        for sprite_id in changed:
            old = materials[sprite_id]
            materials[sprite_id] = inspect_stable(Path(old["png_path"]), sprite_id, old["stable_read_check"]["attempts"])
    else:
        raise RuntimeError("Material files continued changing during eight final batch checks")
    return {
        "schema_version": 1,
        "inspection_started_at": started_at,
        "inspected_at": timestamp(),
        "timezone": "Asia/Shanghai",
        "input_directory": str(directory.resolve()),
        "status": STATUS,
        "approval_status": "本清单仅记录本轮实际文件元数据，不代表用户已视觉批准，也不代替最终注册与动态验收。",
        "bbox_convention": "[left, top, right, bottom]；右界和下界不包含；全透明图为null。alpha_nonzero_bbox使用alpha>0；alpha_ge_128_bbox使用alpha>=128。",
        "source_of_dimensions": "直接读取PNG字节得到实际W/H和Alpha范围；未从提示词、理想尺寸或锚点注册预期值推断。",
        "batch_stability": {"final_file_hashes_match_records": True, "final_check_passes": batch_passes},
        "materials": [materials[sprite_id] for sprite_id in SPRITE_IDS],
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=DEFAULT_INPUT)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    report = inspect_all(args.input)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output.resolve()), "status": STATUS, "inspected_at": report["inspected_at"],
        "materials": [{key: row[key] for key in ("sprite_id", "width", "height", "alpha_nonzero_bbox", "alpha_ge_128_bbox", "png_sha256")} for row in report["materials"]]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
