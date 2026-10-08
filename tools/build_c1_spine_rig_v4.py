from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v4"


def region(path: str, global_x: float, global_y: float) -> dict[str, object]:
    # Every prepared part is a 720x960 full-canvas image. The attachment
    # offset compensates for the rest position of its controlling bone.
    return {
        "path": path,
        "x": -global_x,
        "y": 480 - global_y,
        "width": 720,
        "height": 960,
    }


def keyed(values: list[dict[str, float]]) -> dict[str, list[dict[str, float]]]:
    return {"rotate": values}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=DEFAULT_ROOT,
        help="Asset package root containing the prepared parts directory.",
    )
    args = parser.parse_args()
    out = (args.root / "spine").resolve()
    out.mkdir(parents=True, exist_ok=True)

    data = {
        "skeleton": {
            "hash": "",
            "spine": "3.8.75",
            "x": -360,
            "y": 0,
            "width": 720,
            "height": 960,
            "images": "../parts/",
            "audio": "",
        },
        "bones": [
            {"name": "root"},
            {"name": "hips", "parent": "root", "y": 220, "length": 150},
            {"name": "torso", "parent": "hips", "y": 260, "length": 300},
            {"name": "neck", "parent": "torso", "y": 330, "length": 80},
            {"name": "head", "parent": "neck", "y": 60, "length": 120},
            {"name": "hair_back", "parent": "head", "length": 300},
            {"name": "hair_front", "parent": "head", "length": 150},
            {"name": "brows", "parent": "head", "y": -40, "length": 40},
            {"name": "eyes", "parent": "head", "y": -59, "length": 40},
            {"name": "nose", "parent": "head", "y": -72, "length": 20},
            {"name": "mouth", "parent": "head", "y": -98, "length": 30},
            {"name": "hair_accessory", "parent": "head", "x": -30, "y": 65, "length": 180},
        ],
        "slots": [
            {"name": "slot_hair_back", "bone": "hair_back", "attachment": "hair_back"},
            {"name": "slot_hair_accessory", "bone": "hair_accessory", "attachment": "hair_accessory"},
            {"name": "slot_body_full", "bone": "torso", "attachment": "body_full"},
            {"name": "slot_head", "bone": "head", "attachment": "head"},
            {"name": "slot_brows", "bone": "brows", "attachment": "brows"},
            {"name": "slot_eyes", "bone": "eyes", "attachment": "eyes"},
            {"name": "slot_nose", "bone": "nose", "attachment": "nose"},
            {"name": "slot_mouth", "bone": "mouth", "attachment": "mouth"},
            {"name": "slot_hair_front", "bone": "hair_front", "attachment": "hair_front"},
        ],
        "skins": [
            {
                "name": "default",
                "attachments": {
                    "slot_hair_back": {
                        "hair_back": region("08_hair_back", 0, 870),
                    },
                    "slot_hair_accessory": {
                        "hair_accessory": region("09_hair_accessory", -30, 935),
                    },
                    "slot_body_full": {
                        "body_full": region("01_body_full", 0, 480),
                    },
                    "slot_head": {
                        "head": region("02_head", 0, 870),
                    },
                    "slot_brows": {
                        "brows": region("03_brows", 0, 830),
                    },
                    "slot_eyes": {
                        "eyes": region("04_eyes", 0, 811),
                    },
                    "slot_nose": {
                        "nose": region("05_nose", 0, 798),
                    },
                    "slot_mouth": {
                        "mouth": region("06_mouth", 0, 772),
                    },
                    "slot_hair_front": {
                        "hair_front": region("07_hair_front", 0, 870),
                    },
                },
            }
        ],
        "animations": {
            "idle": {
                "bones": {
                    "torso": {
                        "translate": [
                            {"x": 0, "y": 0},
                            {"time": 0.5, "x": 0, "y": 2},
                            {"time": 1.0, "x": 0, "y": 0},
                        ],
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.5, "angle": 0.4},
                            {"time": 1.0, "angle": 0},
                        ],
                    },
                    "head": keyed([
                        {"angle": 0},
                        {"time": 0.5, "angle": -0.6},
                        {"time": 1.0, "angle": 0},
                    ]),
                    "hair_front": keyed([
                        {"angle": 0},
                        {"time": 0.5, "angle": -0.8},
                        {"time": 1.0, "angle": 0},
                    ]),
                    "hair_accessory": keyed([
                        {"angle": 0},
                        {"time": 0.35, "angle": 2.5},
                        {"time": 0.7, "angle": -2},
                        {"time": 1.0, "angle": 0},
                    ]),
                }
            },
            "attack": {
                "bones": {
                    "torso": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.16, "angle": -3},
                            {"time": 0.34, "angle": 5},
                            {"time": 0.6, "angle": 0},
                        ]
                    },
                    "head": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.25, "angle": -4},
                            {"time": 0.6, "angle": 0},
                        ]
                    },
                    "hair_accessory": keyed([
                        {"angle": 0},
                        {"time": 0.3, "angle": 10},
                        {"time": 0.6, "angle": 0},
                    ]),
                }
            },
            "hurt": {
                "bones": {
                    "torso": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.1, "angle": -5},
                            {"time": 0.28, "angle": 2},
                            {"time": 0.48, "angle": 0},
                        ]
                    },
                    "head": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.1, "angle": 6},
                            {"time": 0.48, "angle": 0},
                        ]
                    },
                    "hair_accessory": keyed([
                        {"angle": 0},
                        {"time": 0.15, "angle": 8},
                        {"time": 0.48, "angle": 0},
                    ]),
                }
            },
            "death_kneel": {
                "bones": {
                    "hips": {
                        "translate": [
                            {"x": 0, "y": 0},
                            {"time": 0.8, "x": 0, "y": -26},
                        ]
                    },
                    "torso": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.8, "angle": -10},
                        ]
                    },
                    "head": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.8, "angle": 12},
                        ]
                    },
                    "hair_accessory": keyed([
                        {"angle": 0},
                        {"time": 0.8, "angle": 15},
                    ]),
                }
            },
        },
    }

    target = out / "c1_spine_unified_import.json"
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(target)


if __name__ == "__main__":
    main()
