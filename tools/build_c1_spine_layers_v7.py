from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "textures" / "characters" / "c1_spine_ingame_rig_v7"
LAYER_DIR = PACKAGE / "layers"
RUNTIME_DIR = PACKAGE / "runtime"
WIDTH = 1024
HEIGHT = 1536

# Setup-pose world positions. Weighted Spine vertices store coordinates in
# the local space of every influencing bone, not in skeleton/world space.
BONE_WORLD = {
    0: (0.0, 0.0),      # root
    1: (0.0, -420.0),   # hips
    2: (0.0, 0.0),      # torso
    3: (0.0, 350.0),    # neck
    4: (0.0, 495.0),    # head
    5: (-235.0, 100.0), # arm_waist
    6: (-335.0, 5.0),   # arm_waist_forearm
    7: (235.0, 140.0),  # arm_raised
    8: (350.0, 285.0),  # arm_raised_forearm
    9: (-155.0, 610.0), # ribbon_base
    10: (-260.0, 575.0),# ribbon_mid
    11: (-370.0, 520.0),# ribbon_tip
    12: (-150.0, -460.0), # lower_left
    13: (150.0, -460.0),  # lower_right
}


def timeline(values: list[tuple[float, float]]) -> list[dict[str, float]]:
    frames: list[dict[str, float]] = []
    for time, angle in values:
        frame: dict[str, float] = {"time": time, "angle": angle}
        if time == 0:
            frame = {"angle": angle}
        frames.append(frame)
    return frames


def translate_timeline(values: list[tuple[float, float, float]]) -> list[dict[str, float]]:
    frames: list[dict[str, float]] = []
    for time, x, y in values:
        frame: dict[str, float] = {"time": time, "x": x, "y": y}
        if time == 0:
            frame = {"x": x, "y": y}
        frames.append(frame)
    return frames


def rotate(values: list[tuple[float, float]]) -> dict[str, list[dict[str, float]]]:
    return {"rotate": timeline(values)}


def weighted_vertex(x: float, y: float, mode: str) -> list[float]:
    """Return Spine 3.8 weighted-mesh vertex data for one layer."""
    if mode == "body":
        if y > 420:
            influences = [(3, 0.78), (2, 0.22)]
        elif y > -100:
            influences = [(2, 0.82), (1, 0.18)]
        else:
            influences = [(1, 0.84), (0, 0.16)]
    elif mode == "head":
        influences = [(4, 0.88), (3, 0.12)]
    elif mode == "lower":
        if x < 0:
            influences = [(12, 0.86), (1, 0.14)]
        else:
            influences = [(13, 0.86), (1, 0.14)]
    elif mode == "arm_waist":
        if y > 80:
            influences = [(5, 0.82), (2, 0.18)]
        else:
            influences = [(6, 0.84), (5, 0.16)]
    elif mode == "arm_raised":
        if y > 230:
            influences = [(8, 0.84), (7, 0.16)]
        else:
            influences = [(7, 0.82), (2, 0.18)]
    else:
        influences = [(0, 1.0)]

    result: list[float] = [len(influences)]
    for bone, weight in influences:
        bone_x, bone_y = BONE_WORLD[bone]
        result.extend([bone, round(x - bone_x, 4), round(y - bone_y, 4), weight])
    return result


def make_mesh(path: str, mode: str) -> dict[str, object]:
    columns = 9
    rows = 13
    xs = [round(-WIDTH / 2 + WIDTH * i / (columns - 1), 4) for i in range(columns)]
    ys = [round(-HEIGHT / 2 + HEIGHT * i / (rows - 1), 4) for i in range(rows)]

    vertices: list[float] = []
    uvs: list[float] = []
    for y in ys:
        for x in xs:
            vertices.extend(weighted_vertex(x, y, mode))
            uvs.extend([
                round((x + WIDTH / 2) / WIDTH, 6),
                round((HEIGHT / 2 - y) / HEIGHT, 6),
            ])

    triangles: list[int] = []
    for row in range(rows - 1):
        for col in range(columns - 1):
            a = row * columns + col
            b = a + 1
            c = a + columns
            d = c + 1
            triangles.extend([a, b, d, a, d, c])

    return {
        "type": "mesh",
        "path": path,
        "uvs": uvs,
        "triangles": triangles,
        "vertices": vertices,
        "hull": 4,
        "width": WIDTH,
        "height": HEIGHT,
    }


def make_ribbon_region() -> dict[str, object]:
    # The ribbon was generated as an independent 1536x1024 asset. Its region
    # is scaled and placed above-left of the head; its draw order remains behind
    # the head/hair layer, while the ribbon bones provide the later motion path.
    world_x, world_y = (-212.0, 568.0)
    bone_x, bone_y = BONE_WORLD[9]
    return {
        "path": "ribbons",
        "x": round(world_x - bone_x, 4),
        "y": round(world_y - bone_y, 4),
        "width": 553,
        "height": 369,
    }


def animation_set() -> dict[str, object]:
    return {
        "idle": {
            "bones": {
                "root": {"translate": translate_timeline([(0, 0, 0), (0.6, 0, 3), (1.2, 0, 0)])},
                "hips": rotate([(0, 0), (0.6, 0.8), (1.2, 0)]),
                "torso": rotate([(0, 0), (0.6, 1.2), (1.2, 0)]),
                "head": rotate([(0, 0), (0.6, -1.6), (1.2, 0)]),
                "ribbon_base": rotate([(0, 0), (0.35, -4), (0.7, 3), (1.2, 0)]),
                "ribbon_mid": rotate([(0, 0), (0.4, 7), (0.8, -5), (1.2, 0)]),
                "ribbon_tip": rotate([(0, 0), (0.45, 11), (0.85, -8), (1.2, 0)]),
            }
        },
        "attack": {
            "bones": {
                "root": {
                    "translate": translate_timeline([(0, 0, 0), (0.16, 8, 0), (0.34, -6, 0), (0.9, 0, 0)]),
                    "rotate": timeline([(0, 0), (0.16, -2), (0.34, 4), (0.9, 0)]),
                },
                "hips": rotate([(0, 0), (0.25, -4), (0.48, 2), (0.9, 0)]),
                "torso": rotate([(0, 0), (0.18, -8), (0.38, 6), (0.9, 0)]),
                "head": rotate([(0, 0), (0.18, -5), (0.38, 4), (0.9, 0)]),
                "arm_raised": rotate([(0, 0), (0.18, -16), (0.4, -38), (0.66, -10), (0.9, 0)]),
                "arm_raised_forearm": rotate([(0, 0), (0.18, 18), (0.4, 58), (0.66, 18), (0.9, 0)]),
                "arm_waist": rotate([(0, 0), (0.2, 8), (0.42, -10), (0.9, 0)]),
                "arm_waist_forearm": rotate([(0, 0), (0.2, -12), (0.42, 18), (0.9, 0)]),
                "ribbon_base": rotate([(0, 0), (0.2, -20), (0.45, 15), (0.9, 0)]),
                "ribbon_mid": rotate([(0, 0), (0.2, -30), (0.45, 24), (0.9, 0)]),
                "ribbon_tip": rotate([(0, 0), (0.2, -38), (0.45, 30), (0.9, 0)]),
            }
        },
        "hurt": {
            "bones": {
                "root": {
                    "translate": translate_timeline([(0, 0, 0), (0.1, -10, 0), (0.28, 4, 0), (0.62, 0, 0)]),
                    "rotate": timeline([(0, 0), (0.1, -7), (0.28, 3), (0.62, 0)]),
                },
                "torso": rotate([(0, 0), (0.1, 7), (0.28, -3), (0.62, 0)]),
                "head": rotate([(0, 0), (0.1, 5), (0.28, -2), (0.62, 0)]),
                "ribbon_base": rotate([(0, 0), (0.1, 13), (0.28, -10), (0.62, 0)]),
                "ribbon_mid": rotate([(0, 0), (0.1, 20), (0.28, -14), (0.62, 0)]),
                "ribbon_tip": rotate([(0, 0), (0.1, 25), (0.28, -18), (0.62, 0)]),
            }
        },
        "death_kneel": {
            "bones": {
                "root": {"translate": translate_timeline([(0, 0, 0), (0.65, 0, -18), (1.1, 0, -24)])},
                "hips": rotate([(0, 0), (0.65, -8), (1.1, -10)]),
                "torso": rotate([(0, 0), (0.65, -12), (1.1, -14)]),
                "head": rotate([(0, 0), (0.65, -14), (1.1, -16)]),
                "arm_raised": rotate([(0, 0), (0.65, 18), (1.1, 20)]),
                "arm_raised_forearm": rotate([(0, 0), (0.65, 22), (1.1, 24)]),
                "arm_waist": rotate([(0, 0), (0.65, -12), (1.1, -14)]),
                "arm_waist_forearm": rotate([(0, 0), (0.65, -16), (1.1, -18)]),
                "ribbon_base": rotate([(0, 0), (0.65, 16), (1.1, 20)]),
                "ribbon_mid": rotate([(0, 0), (0.65, 23), (1.1, 28)]),
                "ribbon_tip": rotate([(0, 0), (0.65, 30), (1.1, 36)]),
            }
        },
    }


def build_json() -> dict[str, object]:
    bones = [
        {"name": "root"},
        {"name": "hips", "parent": "root", "y": -420, "length": 180},
        {"name": "torso", "parent": "hips", "y": 420, "length": 330},
        {"name": "neck", "parent": "torso", "y": 350, "length": 140},
        {"name": "head", "parent": "neck", "y": 145, "length": 180},
        {"name": "arm_waist", "parent": "torso", "x": -235, "y": 100, "length": 180},
        {"name": "arm_waist_forearm", "parent": "arm_waist", "x": -100, "y": -95, "length": 160},
        {"name": "arm_raised", "parent": "torso", "x": 235, "y": 140, "length": 220},
        {"name": "arm_raised_forearm", "parent": "arm_raised", "x": 115, "y": 145, "length": 170},
        {"name": "ribbon_base", "parent": "head", "x": -155, "y": 115, "length": 150},
        {"name": "ribbon_mid", "parent": "ribbon_base", "x": -105, "y": -35, "length": 165},
        {"name": "ribbon_tip", "parent": "ribbon_mid", "x": -110, "y": -55, "length": 150},
        {"name": "lower_left", "parent": "hips", "x": -150, "y": -40, "length": 260},
        {"name": "lower_right", "parent": "hips", "x": 150, "y": -40, "length": 260},
    ]

    slots = [
        {"name": "slot_lower_body", "bone": "root", "attachment": "lower_body"},
        {"name": "slot_body_core", "bone": "root", "attachment": "body_core"},
        {"name": "slot_ribbons", "bone": "ribbon_base", "attachment": "ribbons"},
        {"name": "slot_arm_waist", "bone": "arm_waist", "attachment": "arm_waist"},
        {"name": "slot_arm_raised", "bone": "arm_raised", "attachment": "arm_raised"},
        {"name": "slot_head_hair", "bone": "head", "attachment": "head_hair"},
    ]

    attachments = {
        "slot_lower_body": {"lower_body": make_mesh("lower_body", "lower")},
        "slot_body_core": {"body_core": make_mesh("body_core", "body")},
        "slot_ribbons": {"ribbons": make_ribbon_region()},
        "slot_arm_waist": {"arm_waist": make_mesh("arm_waist", "arm_waist")},
        "slot_arm_raised": {"arm_raised": make_mesh("arm_raised", "arm_raised")},
        "slot_head_hair": {"head_hair": make_mesh("head_hair", "head")},
    }

    return {
        "skeleton": {
            "hash": "c1-desktop-layers-v7",
        # The bundled Godot Spine 3.8 runtime accepts the same 3.8 JSON
        # schema used by the existing v6 asset, whose export marker is 3.8.99.
        "spine": "3.8.99",
            "x": -WIDTH / 2,
            "y": -HEIGHT / 2,
            "width": WIDTH,
            "height": HEIGHT,
            "images": "../layers/",
            "audio": "",
        },
        "bones": bones,
        "slots": slots,
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": animation_set(),
    }


def atlas_page(name: str, width: int, height: int) -> str:
    return "\n".join([
        f"../layers/{name}.png",
        f"size: {width},{height}",
        "format: RGBA8888",
        "filter: Linear,Linear",
        "repeat: none",
        name,
        "  rotate: false",
        "  xy: 0, 0",
        f"  size: {width}, {height}",
        f"  orig: {width}, {height}",
        "  offset: 0, 0",
        "  index: -1",
        "",
    ])


def main() -> None:
    required = [
        "body_core.png",
        "head_hair.png",
        "lower_body.png",
        "ribbons.png",
        "arm_waist.png",
        "arm_raised.png",
    ]
    missing = [name for name in required if not (LAYER_DIR / name).exists()]
    if missing:
        raise FileNotFoundError(f"Missing generated layer files: {', '.join(missing)}")

    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    json_path = RUNTIME_DIR / "c1_desktop_layers_v7.json"
    atlas_path = RUNTIME_DIR / "c1_desktop_layers_v7.atlas"
    json_path.write_text(json.dumps(build_json(), ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    # Spine atlas pages are separated by a blank line. Without that separator
    # the next page header is parsed as another region of the previous page,
    # which makes the first attachment on the following page fail to load.
    atlas = "\n".join([
        atlas_page("body_core", WIDTH, HEIGHT),
        atlas_page("head_hair", WIDTH, HEIGHT),
        atlas_page("lower_body", WIDTH, HEIGHT),
        atlas_page("arm_waist", WIDTH, HEIGHT),
        atlas_page("arm_raised", WIDTH, HEIGHT),
        atlas_page("ribbons", 1536, 1024),
    ])
    atlas_path.write_text(atlas, encoding="utf-8")
    print(json_path)
    print(atlas_path)


if __name__ == "__main__":
    main()
