"""Package native Godot showcase frames for review; never modifies outfit art."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    record = json.loads((args.capture / "capture_record.json").read_text(encoding="utf-8"))
    sources = sorted(args.capture.glob("run_stop_*.png"))
    if len(sources) != 75:
        raise SystemExit(f"Expected 75 native run/stop frames, got {len(sources)}")
    args.destination.mkdir(parents=True, exist_ok=True)
    frames = []
    for source in sources:
        with Image.open(source) as image:
            frames.append(image.convert("RGB").quantize(colors=256, method=Image.Quantize.MEDIANCUT))
    frames[0].save(
        args.destination / "首套水墨长衣_跑停.gif",
        save_all=True,
        append_images=frames[1:],
        duration=[70 if i % 3 else 60 for i in range(len(frames))],
        loop=0,
        optimize=False,
        disposal=2,
    )
    for source_name, output_name in [
        ("showcase_041.png", "首套水墨长衣_静站与持架跑动.png"),
        ("transition_089.png", "首套水墨长衣_停步前.png"),
        ("transition_094.png", "首套水墨长衣_停步过渡.png"),
        ("transition_149.png", "首套水墨长衣_回落后.png"),
    ]:
        shutil.copy2(args.capture / source_name, args.destination / output_name)
    record["packaging"] = {
        "native_png_pixels_unchanged": True,
        "gif": "75 full native frames at 15 fps; palette encoding only",
        "artwork_modified": False,
    }
    (args.destination / "原生采集记录.json").write_text(
        json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"output": str(args.destination), "frames": len(frames)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
