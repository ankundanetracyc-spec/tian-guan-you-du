from __future__ import annotations

import json
from pathlib import Path

from build_c1_spine_layers_v7 import (
    HEIGHT,
    WIDTH,
    atlas_page,
    build_json as build_v7_json,
    make_mesh,
)


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v8"
LAYER_DIR = PACKAGE / "layers"
RUNTIME_DIR = PACKAGE / "runtime"


def keyframe(time: float, name: str) -> dict[str, object]:
    frame: dict[str, object] = {"name": name}
    if time > 0:
        frame["time"] = time
    return frame


def build_json() -> dict[str, object]:
    data = build_v7_json()
    data["skeleton"]["hash"] = "c1-desktop-layers-v8-material-sheet"

    slots = data["slots"]
    lower_slot = next(slot for slot in slots if slot["name"] == "slot_lower_body")
    body_slot_index = next(index for index, slot in enumerate(slots) if slot["name"] == "slot_body_core")
    slots.insert(body_slot_index + 1, {"name": "slot_lower_kneel", "bone": "root"})
    attachments = data["skins"][0]["attachments"]["slot_lower_body"]
    # This attachment is a real alternate pose extracted from the supplied
    # material sheet, not a rotated crop of the standing legs.  Root-weighted
    # vertices keep the pose stable while the torso settles into the kneel.
    attachments["lower_kneel"] = make_mesh("lower_kneel", "root")
    attachments["lower_empty"] = make_mesh("lower_empty", "root")
    data["skins"][0]["attachments"]["slot_lower_kneel"] = {
        "lower_kneel": make_mesh("lower_kneel", "root")
    }

    death = data["animations"]["death_kneel"]
    death["slots"] = {
            lower_slot["name"]: {
                "attachment": [
                    keyframe(0.0, "lower_body"),
                    keyframe(0.30, "lower_empty"),
                ]
            },
            "slot_lower_kneel": {
                "attachment": [keyframe(0.30, "lower_kneel")]
            },
    }
    death["bones"].update({
        "lower_left": {
            "rotate": [
                {"angle": 0},
                {"time": 0.30, "angle": -8},
                {"time": 0.72, "angle": -18},
                {"time": 1.10, "angle": -24},
            ]
        },
        "lower_right": {
            "rotate": [
                {"angle": 0},
                {"time": 0.30, "angle": 8},
                {"time": 0.72, "angle": 20},
                {"time": 1.10, "angle": 28},
            ]
        },
    })
    return data


def main() -> None:
    required = [
        "body_core.png",
        "head_hair.png",
        "lower_body.png",
        "lower_empty.png",
        "lower_kneel.png",
        "ribbons.png",
        "arm_waist.png",
        "arm_raised.png",
    ]
    missing = [name for name in required if not (LAYER_DIR / name).exists()]
    if missing:
        raise FileNotFoundError(f"Missing v8 layer files: {', '.join(missing)}")

    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    json_path = RUNTIME_DIR / "c1_desktop_layers_v8.json"
    atlas_path = RUNTIME_DIR / "c1_desktop_layers_v8.atlas"
    payload = json.dumps(build_json(), ensure_ascii=False, separators=(",", ":"))
    json_path.write_text(payload, encoding="utf-8")
    atlas = "\n".join([
        atlas_page("body_core", WIDTH, HEIGHT),
        atlas_page("head_hair", WIDTH, HEIGHT),
        atlas_page("lower_body", WIDTH, HEIGHT),
        atlas_page("lower_empty", WIDTH, HEIGHT),
        atlas_page("lower_kneel", WIDTH, HEIGHT),
        atlas_page("arm_waist", WIDTH, HEIGHT),
        atlas_page("arm_raised", WIDTH, HEIGHT),
        atlas_page("ribbons", 1536, 1024),
    ])
    atlas_path.write_text(atlas, encoding="utf-8")
    # The project importer keys Spine data off this extension.  Keep the JSON
    # copy beside it so the result remains inspectable and converter-friendly.
    (RUNTIME_DIR / "c1_desktop_layers_v8.spinejson").write_text(payload, encoding="utf-8")
    print(json_path)
    print(atlas_path)


if __name__ == "__main__":
    main()
