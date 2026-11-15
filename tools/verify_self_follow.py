"""Check rendered effect pixels follow the body / hand and leave no old-position residue."""
import json
from pathlib import Path
from PIL import Image, ImageChops

source = Path("E:/tmp/stacklands_self_follow_20261104")
records = json.loads((source / "anchors.json").read_text(encoding="utf-8"))
checks = 0
for record in records:
    prefix = record["prefix"]
    with Image.open(source / (prefix + "_base.png")) as base, Image.open(source / (prefix + "_fx.png")) as fx:
        delta = ImageChops.difference(base.convert("RGB"), fx.convert("RGB"))
        channels = delta.split()
        mask = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2]).point(lambda v: 255 if v > 4 else 0)
        bounds = mask.getbbox()
        assert bounds is not None, (prefix, "effect missing")
        x, y = record["anchor"]
        rx, top, bottom = (66, 160, 50) if record["kind"] == "催血" else (52, 52, 52)
        assert bounds[0] >= x-rx and bounds[2] <= x+rx, (prefix, bounds, record["anchor"], "horizontal attachment")
        assert bounds[1] >= y-top and bounds[3] <= y+bottom, (prefix, bounds, record["anchor"], "vertical attachment")
        checks += 3
        if record["pose"]:
            ox, oy = record["old_anchor"]
            old_area = (round(ox-rx), round(oy-top), round(ox+rx), round(oy+bottom))
            assert mask.crop(old_area).getbbox() is None, (prefix, "residue at previous body / hand")
            checks += 1
    print(prefix, "pixels at", bounds, "attached to", [round(x, 1), round(y, 1)])
assert len(records) == 12
print(f"SELF_FOLLOW_PIXEL_CHECKS {checks} passed: cast/tail, translation and turning")
