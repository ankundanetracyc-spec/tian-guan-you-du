"""Build the C1 full-image Spine mesh resource.

The character remains one transparent texture.  The mesh is weighted to a small
set of bones so the runtime can deform the whole image without cutting it into
separate PNGs.
"""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "textures/characters/c1_spine_ingame_rig_v6/runtime/c1_spine_unified.spinejson"

WIDTH = 720
HEIGHT = 960


# World-space setup positions.  The mesh's setup pose is the original image,
# so every weighted vertex is converted to the local coordinates of its bone.
BONE_WORLD = {
    0: (0.0, 0.0),       # root
    1: (0.0, -250.0),    # hips
    2: (0.0, 0.0),       # torso
    3: (0.0, 330.0),     # neck
    4: (0.0, 400.0),     # head
    5: (-150.0, 120.0),  # back arm
    6: (-210.0, -10.0),  # back forearm
    7: (140.0, 140.0),   # pointing arm
    8: (225.0, 240.0),   # pointing forearm / hand
    9: (-95.0, 360.0),   # ribbon base
    10: (-195.0, 290.0), # ribbon tail
}


def local_vertex(x: float, y: float, bone: int) -> list[float]:
    bx, by = BONE_WORLD[bone]
    return [round(x - bx, 4), round(y - by, 4)]


def weighted_vertex(x: float, y: float) -> list[float]:
    """Return one to two setup-pose bone influences for a mesh vertex."""

    # The order is intentional: the more specific regions are checked first.
    if y >= 310 and abs(x) <= 175:
        influences = [(4, 0.82), (3, 0.18)]
    elif x <= -125 and y >= 160:
        influences = [(9, 0.72), (10, 0.28)]
    elif x >= 125 and y >= 175:
        influences = [(8, 0.78), (7, 0.22)]
    elif x >= 80 and -80 <= y < 175:
        influences = [(7, 0.78), (2, 0.22)]
    elif x <= -115 and -150 <= y < 160:
        influences = [(6, 0.72), (5, 0.28)]
    elif x < -65 and -80 <= y < 175:
        influences = [(5, 0.72), (2, 0.28)]
    elif y < -175:
        influences = [(1, 0.82), (0, 0.18)]
    elif y > 255 and abs(x) < 145:
        influences = [(3, 0.8), (2, 0.2)]
    else:
        influences = [(2, 0.82), (0, 0.18)]

    result: list[float] = [len(influences)]
    for bone, weight in influences:
        result.append(bone)
        result.extend(local_vertex(x, y, bone))
        result.append(weight)
    return result


def timeline(values: list[tuple[float, float]]) -> list[dict[str, float]]:
    result: list[dict[str, float]] = []
    for time, value in values:
        frame: dict[str, float] = {"time": time, "angle": value}
        if time == 0:
            frame = {"angle": value}
        result.append(frame)
    return result


def translate_timeline(values: list[tuple[float, float, float]]) -> list[dict[str, float]]:
    result: list[dict[str, float]] = []
    for time, x, y in values:
        frame: dict[str, float] = {"time": time, "x": x, "y": y}
        if time == 0:
            frame = {"x": x, "y": y}
        result.append(frame)
    return result


def rotate_bone(values: list[tuple[float, float]]) -> dict[str, list[dict[str, float]]]:
    return {"rotate": timeline(values)}


def make_animation(kind: str) -> tuple[float, dict[str, dict[str, list[dict[str, float]]]]]:
    if kind == "idle":
        duration = 1.2
        bones = {
            "root": {"translate": translate_timeline([(0, 0, 0), (0.6, 0, 3), (1.2, 0, 0)])},
            "hips": rotate_bone([(0, 0), (0.6, 0.8), (1.2, 0)]),
            "torso": rotate_bone([(0, 0), (0.6, 1.2), (1.2, 0)]),
            "head": rotate_bone([(0, 0), (0.6, -1.8), (1.2, 0)]),
            "arm_front": rotate_bone([(0, 0), (0.6, 2.2), (1.2, 0)]),
            "arm_front_forearm": rotate_bone([(0, 0), (0.6, -2.8), (1.2, 0)]),
            "ribbon": rotate_bone([(0, 0), (0.6, -5), (1.2, 0)]),
            "ribbon_tail": rotate_bone([(0, 0), (0.6, 8), (1.2, 0)]),
        }
        return duration, bones

    if kind == "attack":
        duration = 0.9
        bones = {
            "root": {
                "translate": translate_timeline([(0, 0, 0), (0.16, 6, 0), (0.34, -5, 0), (0.9, 0, 0)]),
                "rotate": timeline([(0, 0), (0.16, -2), (0.34, 4), (0.9, 0)]),
            },
            "hips": rotate_bone([(0, 0), (0.25, -4), (0.48, 2), (0.9, 0)]),
            "torso": rotate_bone([(0, 0), (0.18, -8), (0.38, 6), (0.9, 0)]),
            "head": rotate_bone([(0, 0), (0.18, -5), (0.38, 5), (0.9, 0)]),
            "arm_front": rotate_bone([(0, 0), (0.18, -16), (0.4, -38), (0.66, -10), (0.9, 0)]),
            "arm_front_forearm": rotate_bone([(0, 0), (0.18, 18), (0.4, 58), (0.66, 18), (0.9, 0)]),
            "arm_back": rotate_bone([(0, 0), (0.2, 8), (0.42, -10), (0.9, 0)]),
            "arm_back_forearm": rotate_bone([(0, 0), (0.2, -12), (0.42, 18), (0.9, 0)]),
            "ribbon": rotate_bone([(0, 0), (0.2, -22), (0.45, 16), (0.9, 0)]),
            "ribbon_tail": rotate_bone([(0, 0), (0.2, -36), (0.45, 28), (0.9, 0)]),
        }
        return duration, bones

    if kind == "hurt":
        duration = 0.62
        bones = {
            "root": {
                "translate": translate_timeline([(0, 0, 0), (0.1, -10, 0), (0.28, 4, 0), (0.62, 0, 0)]),
                "rotate": timeline([(0, 0), (0.1, -7), (0.28, 3), (0.62, 0)]),
            },
            "torso": rotate_bone([(0, 0), (0.1, 7), (0.28, -3), (0.62, 0)]),
            "head": rotate_bone([(0, 0), (0.1, 5), (0.28, -2), (0.62, 0)]),
            "arm_front": rotate_bone([(0, 0), (0.1, 12), (0.28, -8), (0.62, 0)]),
            "arm_front_forearm": rotate_bone([(0, 0), (0.1, -18), (0.28, 9), (0.62, 0)]),
            "ribbon": rotate_bone([(0, 0), (0.1, 16), (0.28, -12), (0.62, 0)]),
            "ribbon_tail": rotate_bone([(0, 0), (0.1, 22), (0.28, -18), (0.62, 0)]),
        }
        return duration, bones

    duration = 1.1
    bones = {
        # Keep the root upright. The collapse is carried by the local bones so
        # the whole character can never turn upside down during death_kneel.
        "root": {
            "translate": translate_timeline([(0, 0, 0), (0.65, 0, -18), (1.1, 0, -24)]),
            "rotate": timeline([(0, 0), (0.65, 0), (1.1, 0)]),
        },
        "hips": rotate_bone([(0, 0), (0.65, -8), (1.1, -10)]),
        "torso": rotate_bone([(0, 0), (0.65, -12), (1.1, -14)]),
        "head": rotate_bone([(0, 0), (0.65, -14), (1.1, -16)]),
        "arm_front": rotate_bone([(0, 0), (0.65, 18), (1.1, 20)]),
        "arm_front_forearm": rotate_bone([(0, 0), (0.65, 22), (1.1, 24)]),
        "arm_back": rotate_bone([(0, 0), (0.65, -12), (1.1, -14)]),
        "arm_back_forearm": rotate_bone([(0, 0), (0.65, -16), (1.1, -18)]),
        "ribbon": rotate_bone([(0, 0), (0.65, 18), (1.1, 22)]),
        "ribbon_tail": rotate_bone([(0, 0), (0.65, 26), (1.1, 30)]),
    }
    return duration, bones


def build() -> None:
    # A moderate grid keeps the full image intact while giving each weighted
    # region enough vertices to bend smoothly at 60 FPS.
    xs = [-360, -300, -240, -180, -120, -60, 0, 60, 120, 180, 240, 300, 360]
    ys = [-480, -420, -360, -300, -240, -180, -120, -60, 0, 60, 120, 180, 240, 300, 360, 420, 480]

    vertices: list[float] = []
    uvs: list[float] = []
    for y in ys:
        for x in xs:
            vertices.extend(weighted_vertex(x, y))
            # This runtime exposes imported page V coordinates top-down. Flip
            # the normalized mesh V so the source image's head stays at +Y
            # and its feet stay at -Y in Spine's coordinate system.
            uvs.extend([round((x + 360) / WIDTH, 6), round((480 - y) / HEIGHT, 6)])

    triangles: list[int] = []
    columns = len(xs)
    for row in range(len(ys) - 1):
        for col in range(columns - 1):
            a = row * columns + col
            b = a + 1
            c = a + columns
            d = c + 1
            triangles.extend([a, b, d, a, d, c])

    bones = [
        {"name": "root"},
        {"name": "hips", "parent": "root", "y": -250, "length": 150},
        {"name": "torso", "parent": "hips", "y": 250, "length": 300},
        {"name": "neck", "parent": "torso", "y": 330, "length": 120},
        {"name": "head", "parent": "neck", "y": 70, "length": 160},
        {"name": "arm_back", "parent": "torso", "x": -150, "y": 120, "length": 170},
        {"name": "arm_back_forearm", "parent": "arm_back", "x": -60, "y": -130, "length": 150},
        {"name": "arm_front", "parent": "torso", "x": 140, "y": 140, "length": 180},
        {"name": "arm_front_forearm", "parent": "arm_front", "x": 85, "y": 100, "length": 130},
        {"name": "ribbon", "parent": "head", "x": -95, "y": -40, "length": 160},
        {"name": "ribbon_tail", "parent": "ribbon", "x": -100, "y": -70, "length": 180},
    ]

    animations = {}
    for name in ("idle", "attack", "hurt", "death_kneel"):
        _, bones_for_animation = make_animation(name)
        animations[name] = {"bones": bones_for_animation}

    attachment = {
        "type": "mesh",
        "path": "01_character_full",
        "uvs": uvs,
        "triangles": triangles,
        "vertices": vertices,
        "hull": 4,
        "width": WIDTH,
        "height": HEIGHT,
    }

    data = {
        "skeleton": {
            "hash": "c1-full-image-deform-v1",
            # The project runtime identifies its compatible 3.8 binary format
            # as 3.8.99; 3.8.75 is intentionally rejected by that runtime.
            "spine": "3.8.99",
            "x": -360,
            "y": -480,
            "width": WIDTH,
            "height": HEIGHT,
            "images": "../parts/",
            "audio": "",
        },
        "bones": bones,
        "slots": [{"name": "slot_character_full", "bone": "root", "attachment": "character_full_mesh"}],
        "skins": [{
            "name": "default",
            "attachments": {
                "slot_character_full": {
                    "character_full_mesh": attachment
                }
            },
        }],
        "animations": animations,
    }
    OUTPUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"wrote {OUTPUT} ({len(vertices)} vertex values, {len(triangles) // 3} triangles)")


if __name__ == "__main__":
    build()
