from __future__ import annotations

import json
import argparse
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v1"


def region(path: str, global_x: float, global_y: float) -> dict[str, object]:
    # All generated parts already share a 720x960 canvas. A region attachment
    # is centered on its bone, so compensate for the bone's rest position while
    # retaining one common image origin for every attachment.
    return {
        "path": path,
        "x": -global_x,
        "y": 480 - global_y,
        "width": 720,
        "height": 960,
    }


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
            {"name": "hair_back", "parent": "head", "length": 220},
            {"name": "hair_front", "parent": "head", "length": 150},
            {"name": "brows", "parent": "head", "y": -40, "length": 40},
            {"name": "eyes", "parent": "head", "y": -59, "length": 40},
            {"name": "nose", "parent": "head", "y": -72, "length": 20},
            {"name": "mouth", "parent": "head", "y": -98, "length": 30},
            {"name": "hair_accessory", "parent": "head", "x": -30, "y": 65, "length": 100},
            {"name": "ribbon_a", "parent": "head", "x": -50, "y": 45, "length": 140},
            {"name": "ribbon_a_tip", "parent": "ribbon_a", "y": 140, "length": 110},
            {"name": "ribbon_b", "parent": "head", "x": -35, "y": 5, "length": 140},
            {"name": "ribbon_b_tip", "parent": "ribbon_b", "y": 140, "length": 110},
        ],
        "slots": [
            {"name": "slot_hair_back", "bone": "hair_back", "attachment": "hair_back"},
            {"name": "slot_ribbon_tail_a", "bone": "ribbon_a", "attachment": "ribbon_tail_a"},
            {"name": "slot_ribbon_tail_b", "bone": "ribbon_b", "attachment": "ribbon_tail_b"},
            {"name": "slot_body_full", "bone": "torso", "attachment": "body_full"},
            {"name": "slot_head", "bone": "head", "attachment": "head"},
            {"name": "slot_brows", "bone": "brows", "attachment": "brows"},
            {"name": "slot_eyes", "bone": "eyes", "attachment": "eyes"},
            {"name": "slot_nose", "bone": "nose", "attachment": "nose"},
            {"name": "slot_mouth", "bone": "mouth", "attachment": "mouth"},
            {"name": "slot_hair_front", "bone": "hair_front", "attachment": "hair_front"},
            {"name": "slot_hair_accessory", "bone": "hair_accessory", "attachment": "hair_accessory"},
        ],
        "skins": [
            {
                "name": "default",
                "attachments": {
                    "slot_hair_back": {
                        "hair_back": region("02_hair_back", 0, 870),
                    },
                    "slot_ribbon_tail_a": {
                        "ribbon_tail_a": region("10_ribbon_tail_a", -50, 915),
                    },
                    "slot_ribbon_tail_b": {
                        "ribbon_tail_b": region("11_ribbon_tail_b", -35, 875),
                    },
                    "slot_body_full": {
                        "body_full": region("01_body_full", 0, 480),
                    },
                    "slot_head": {
                        "head": region("03_head", 0, 870),
                    },
                    "slot_brows": {
                        "brows": region("04_brows", 0, 830),
                    },
                    "slot_eyes": {
                        "eyes": region("05_eyes", 0, 811),
                    },
                    "slot_nose": {
                        "nose": region("06_nose", 0, 798),
                    },
                    "slot_mouth": {
                        "mouth": region("07_mouth", 0, 772),
                    },
                    "slot_hair_front": {
                        "hair_front": region("08_hair_front", 0, 870),
                    },
                    "slot_hair_accessory": {
                        "hair_accessory": region("09_hair_accessory", -30, 935),
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
                    "head": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.5, "angle": -0.6},
                            {"time": 1.0, "angle": 0},
                        ],
                    },
                    "ribbon_a": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.35, "angle": 4},
                            {"time": 0.7, "angle": -3},
                            {"time": 1.0, "angle": 0},
                        ],
                    },
                    "ribbon_b": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.4, "angle": -5},
                            {"time": 0.8, "angle": 3},
                            {"time": 1.0, "angle": 0},
                        ],
                    },
                    "hair_front": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.5, "angle": -0.8},
                            {"time": 1.0, "angle": 0},
                        ],
                    },
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
                    "ribbon_a": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.3, "angle": 12},
                            {"time": 0.6, "angle": 0},
                        ]
                    },
                    "ribbon_b": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.28, "angle": -15},
                            {"time": 0.6, "angle": 0},
                        ]
                    },
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
                    "ribbon_a": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.15, "angle": 9},
                            {"time": 0.48, "angle": 0},
                        ]
                    },
                    "ribbon_b": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.15, "angle": -8},
                            {"time": 0.48, "angle": 0},
                        ]
                    },
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
                    "ribbon_a": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.8, "angle": 18},
                        ]
                    },
                    "ribbon_b": {
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.8, "angle": -14},
                        ]
                    },
                }
            },
        },
    }

    target = out / "c1_spine_unified_import.json"
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(target)


if __name__ == "__main__":
    main()
