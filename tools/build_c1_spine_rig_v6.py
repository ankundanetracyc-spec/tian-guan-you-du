from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v6"


def region(path: str) -> dict[str, object]:
    # The prepared PNG is a complete 720x960 canvas. Its center is placed on
    # the root bone, so the one attachment can be moved as a whole.
    return {
        "path": path,
        "x": 0,
        "y": 0,
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
            "y": -480,
            "width": 720,
            "height": 960,
            "images": "../parts/",
            "audio": "",
        },
        "bones": [
            {"name": "root"},
            # These bones establish a usable whole-character hierarchy for the
            # first pass. The current region is intentionally attached to root;
            # later split or weighted attachments can use these guides.
            {"name": "hips", "parent": "root", "y": -250, "length": 150},
            {"name": "torso", "parent": "hips", "y": 250, "length": 300},
            {"name": "neck", "parent": "torso", "y": 330, "length": 120},
            {"name": "head", "parent": "neck", "y": 70, "length": 160},
        ],
        "slots": [
            {
                "name": "slot_character_full",
                "bone": "root",
                "attachment": "character_full",
            }
        ],
        "skins": [
            {
                "name": "default",
                "attachments": {
                    "slot_character_full": {
                        "character_full": region("01_character_full"),
                    }
                },
            }
        ],
        "animations": {
            "idle": {
                "bones": {
                    "root": {
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
                    }
                }
            },
            "attack": {
                "bones": {
                    "root": {
                        "translate": [
                            {"x": 0, "y": 0},
                            {"time": 0.16, "x": 8, "y": 0},
                            {"time": 0.34, "x": -4, "y": 0},
                            {"time": 0.82, "x": 0, "y": 0},
                            {"time": 1.0, "x": 0, "y": 0},
                        ],
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.16, "angle": -3},
                            {"time": 0.34, "angle": 5},
                            {"time": 0.82, "angle": 0},
                            {"time": 1.0, "angle": 0},
                        ],
                    }
                }
            },
            "hurt": {
                "bones": {
                    "root": {
                        "translate": [
                            {"x": 0, "y": 0},
                            {"time": 0.1, "x": -8, "y": 0},
                            {"time": 0.28, "x": 3, "y": 0},
                            {"time": 0.48, "x": 0, "y": 0},
                            {"time": 1.0, "x": 0, "y": 0},
                        ],
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.1, "angle": -5},
                            {"time": 0.28, "angle": 2},
                            {"time": 0.48, "angle": 0},
                            {"time": 1.0, "angle": 0},
                        ],
                    }
                }
            },
            "death_kneel": {
                "bones": {
                    "root": {
                        "translate": [
                            {"x": 0, "y": 0},
                            {"time": 0.8, "x": 0, "y": -26},
                            {"time": 1.0, "x": 0, "y": -26},
                        ],
                        "rotate": [
                            {"angle": 0},
                            {"time": 0.8, "angle": -10},
                            {"time": 1.0, "angle": -10},
                        ],
                    }
                }
            },
        },
    }

    target = out / "c1_spine_unified_import.json"
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(target)


if __name__ == "__main__":
    main()
