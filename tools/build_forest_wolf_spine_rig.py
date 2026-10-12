#!/usr/bin/env python3
"""Deprecated v1 builder for the rejected side-view wolf; use build_forest_wolf_spine_rig_v2.py."""

from __future__ import annotations

import json
import math
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RIG = ROOT / "textures/characters/forest_wolf_spine_rig_v1"
SOURCE = RIG / "source/wolf_rig_master.png"
RUNTIME = RIG / "runtime"
DIVISIONS = 64


# Screen-space bind anchors on the generated 1254 x 1254 master illustration.
# Spine's setup-space is Y-up; the source artwork is Y-down.
BONES = [
    ("root", None, (790, 640)),
    ("torso", "root", (585, 590)),
    ("neck", "torso", (390, 500)),
    ("head", "neck", (240, 350)),
    ("foreleg_near_upper", "torso", (350, 675)),
    ("foreleg_near_knee", "foreleg_near_upper", (340, 865)),
    ("foreleg_near_paw", "foreleg_near_knee", (275, 1045)),
    ("foreleg_far_upper", "torso", (455, 670)),
    ("foreleg_far_knee", "foreleg_far_upper", (465, 870)),
    ("foreleg_far_paw", "foreleg_far_knee", (470, 1040)),
    ("hindleg_far_upper", "root", (690, 665)),
    ("hindleg_far_knee", "hindleg_far_upper", (735, 850)),
    ("hindleg_far_paw", "hindleg_far_knee", (710, 1040)),
    ("hindleg_near_upper", "root", (850, 660)),
    ("hindleg_near_knee", "hindleg_near_upper", (950, 870)),
    ("hindleg_near_paw", "hindleg_near_knee", (955, 1040)),
    ("tail_base", "root", (1005, 580)),
    ("tail_mid", "tail_base", (1120, 740)),
    ("tail_tip", "tail_mid", (1190, 940)),
]

LEG_POLYGONS = {
    "foreleg_near": [
        (285, 640), (365, 654), (397, 716), (386, 790), (365, 850),
        (360, 930), (348, 1010), (339, 1072), (316, 1100), (230, 1100),
        (200, 1074), (219, 1028), (251, 955), (269, 864), (276, 770),
    ],
    "foreleg_far": [
        (392, 645), (469, 652), (500, 724), (503, 815), (522, 917),
        (542, 1036), (536, 1080), (510, 1102), (419, 1100), (393, 1076),
        (402, 1020), (409, 950), (400, 873), (384, 800), (370, 720),
    ],
    "hindleg_far": [
        (650, 638), (741, 640), (797, 684), (819, 735), (803, 797),
        (779, 850), (778, 920), (793, 986), (787, 1065), (756, 1100),
        (664, 1102), (627, 1077), (653, 1023), (657, 965), (642, 904),
        (633, 833), (637, 760),
    ],
    "hindleg_near": [
        (802, 638), (894, 640), (935, 684), (960, 755), (987, 831),
        (1017, 933), (1025, 1036), (1005, 1082), (977, 1102), (891, 1100),
        (868, 1078), (872, 1020), (879, 960), (853, 897), (836, 824),
        (810, 755),
    ],
}

HEAD_POLYGON = [
    (0, 145), (323, 151), (393, 202), (431, 292), (420, 405),
    (383, 486), (319, 540), (217, 527), (104, 493), (6, 470),
]
NECK_POLYGON = [
    (114, 453), (228, 409), (338, 423), (446, 465), (527, 531),
    (597, 605), (612, 673), (565, 743), (475, 724), (383, 678),
    (283, 622), (187, 572), (120, 521),
]
TAIL_POLYGON = [
    (963, 510), (1021, 490), (1096, 518), (1167, 581), (1213, 663),
    (1249, 789), (1244, 922), (1216, 1017), (1170, 1060), (1133, 1037),
    (1145, 978), (1119, 916), (1099, 838), (1076, 755), (1030, 677),
    (984, 631),
]

LEG_CHAINS = {
    "foreleg_near": [("foreleg_near_upper", (350, 675)), ("foreleg_near_knee", (340, 865)), ("foreleg_near_paw", (275, 1045))],
    "foreleg_far": [("foreleg_far_upper", (455, 670)), ("foreleg_far_knee", (465, 870)), ("foreleg_far_paw", (470, 1040))],
    "hindleg_far": [("hindleg_far_upper", (690, 665)), ("hindleg_far_knee", (735, 850)), ("hindleg_far_paw", (710, 1040))],
    "hindleg_near": [("hindleg_near_upper", (850, 660)), ("hindleg_near_knee", (950, 870)), ("hindleg_near_paw", (955, 1040))],
}
TAIL_CHAIN = [("tail_base", (1005, 580)), ("tail_mid", (1120, 740)), ("tail_tip", (1190, 940))]


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as f:
        header = f.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Not a PNG image: {path}")
    return struct.unpack(">II", header[16:24])


def point_in_polygon(p: tuple[float, float], polygon: list[tuple[int, int]]) -> bool:
    x, y = p
    inside = False
    j = len(polygon) - 1
    for i, (xi, yi) in enumerate(polygon):
        xj, yj = polygon[j]
        if (yi > y) != (yj > y):
            cross_x = (xj - xi) * (y - yi) / ((yj - yi) or 1e-9) + xi
            if x < cross_x:
                inside = not inside
        j = i
    return inside


def segment_t(p: tuple[float, float], a: tuple[int, int], b: tuple[int, int]) -> tuple[float, float]:
    px, py = p
    dx, dy = b[0] - a[0], b[1] - a[1]
    denom = dx * dx + dy * dy
    t = 0.0 if denom == 0 else max(0.0, min(1.0, ((px - a[0]) * dx + (py - a[1]) * dy) / denom))
    qx, qy = a[0] + t * dx, a[1] + t * dy
    return t, math.hypot(px - qx, py - qy)


def chain_weights(p: tuple[float, float], chain: list[tuple[str, tuple[int, int]]]) -> list[tuple[str, float]]:
    candidates = []
    for i in range(len(chain) - 1):
        t, distance = segment_t(p, chain[i][1], chain[i + 1][1])
        candidates.append((distance, i, t))
    _, i, t = min(candidates)
    return [(chain[i][0], 1.0 - t), (chain[i + 1][0], t)]


def blend_weights(primary: list[tuple[str, float]], secondary: list[tuple[str, float]], secondary_amount: float) -> list[tuple[str, float]]:
    amount = max(0.0, min(1.0, secondary_amount))
    return ([(name, weight * (1.0 - amount)) for name, weight in primary]
            + [(name, weight * amount) for name, weight in secondary])


def mix(a: str, b: str, wb: float) -> list[tuple[str, float]]:
    wb = max(0.0, min(1.0, wb))
    return [(a, 1.0 - wb), (b, wb)]


def weights_at(p: tuple[float, float]) -> list[tuple[str, float]]:
    x, y = p
    if point_in_polygon(p, TAIL_POLYGON):
        tail = chain_weights(p, TAIL_CHAIN)
        root_blend = 0.24 * max(0.0, min(1.0, (190.0 - math.hypot(x - 1005.0, y - 580.0)) / 190.0))
        return blend_weights(tail, [("root", 1.0)], root_blend)

    # The lower leg meshes get their own pivot chains; the mane and skull stay
    # on the torso/neck/head chain even where their silhouettes overlap.
    for part in ("foreleg_near", "foreleg_far", "hindleg_far", "hindleg_near"):
        if point_in_polygon(p, LEG_POLYGONS[part]):
            leg = chain_weights(p, LEG_CHAINS[part])
            body_bone = "torso" if part.startswith("foreleg") else "root"
            shoulder_blend = 0.24 * max(0.0, min(1.0, (760.0 - y) / 120.0))
            return blend_weights(leg, [(body_bone, 1.0)], shoulder_blend)

    if point_in_polygon(p, HEAD_POLYGON):
        head_weight = max(0.30, min(0.96, (430.0 - x) / 285.0))
        return mix("neck", "head", head_weight)
    if point_in_polygon(p, NECK_POLYGON):
        torso_weight = max(0.10, min(0.70, (x - 315.0) / 390.0))
        return mix("neck", "torso", torso_weight)

    torso_weight = max(0.0, min(1.0, (805.0 - x) / 285.0))
    return mix("root", "torso", torso_weight)


def build_bones(root_x: int, root_y: int) -> tuple[list[dict], dict[str, tuple[float, float]]]:
    indices = {name: i for i, (name, _, _) in enumerate(BONES)}
    screen_to_world = lambda p: (float(p[0] - root_x), float(root_y - p[1]))
    world: dict[str, tuple[float, float]] = {}
    result = []
    for name, parent, screen_point in BONES:
        absolute = screen_to_world(screen_point)
        world[name] = absolute
        bone = {"name": name}
        if parent is not None:
            parent_world = world[parent]
            bone.update({"parent": parent, "x": round(absolute[0] - parent_world[0], 4), "y": round(absolute[1] - parent_world[1], 4)})
        result.append(bone)
    return result, world


def build_mesh(width: int, height: int, bone_indices: dict[str, int], bone_world: dict[str, tuple[float, float]]) -> dict:
    columns = rows = DIVISIONS + 1
    vertices: list[float | int] = []
    uvs: list[float] = []
    root_x, root_y = BONES[0][2]

    for iy in range(rows):
        y = height * iy / DIVISIONS
        for ix in range(columns):
            x = width * ix / DIVISIONS
            influences = [(name, weight) for name, weight in weights_at((x, y)) if weight > 0.015]
            influences.sort(key=lambda item: item[1], reverse=True)
            influences = influences[:3]
            total = sum(weight for _, weight in influences) or 1.0
            influences = [(name, weight / total) for name, weight in influences]
            vx, vy = x - root_x, root_y - y
            vertices.append(len(influences))
            for name, weight in influences:
                px, py = bone_world[name]
                vertices.extend([bone_indices[name], round(vx - px, 4), round(vy - py, 4), round(weight, 6)])
            uvs.extend([round(x / width, 6), round(y / height, 6)])

    triangles: list[int] = []
    for iy in range(DIVISIONS):
        for ix in range(DIVISIONS):
            tl = iy * columns + ix
            tr, bl = tl + 1, tl + columns
            br = bl + 1
            triangles.extend([tl, tr, br, tl, br, bl])

    return {
        "type": "mesh",
        "path": "wolf",
        "uvs": uvs,
        "triangles": triangles,
        "vertices": vertices,
        "hull": 4,
        "width": width,
        "height": height,
    }


def rotate(*keys: tuple[float, float]) -> list[dict]:
    return [{"time": t, "angle": angle} for t, angle in keys]


def translate(*keys: tuple[float, float, float]) -> list[dict]:
    return [{"time": t, "x": x, "y": y} for t, x, y in keys]


def animations() -> dict:
    return {
        "idle": {
            "bones": {
                "root": {"translate": translate((0, 0, 0), (0.7, 0, 3), (1.4, 0, 0))},
                "torso": {"rotate": rotate((0, 0), (0.7, 0.9), (1.4, 0))},
                "neck": {"rotate": rotate((0, 0), (0.7, -0.7), (1.4, 0))},
                "head": {"rotate": rotate((0, 0), (0.7, -1.0), (1.4, 0))},
                "foreleg_near_upper": {"rotate": rotate((0, 0), (0.7, 1.3), (1.4, 0))},
                "foreleg_far_upper": {"rotate": rotate((0, 0), (0.7, -0.9), (1.4, 0))},
                "hindleg_near_upper": {"rotate": rotate((0, 0), (0.7, -1.0), (1.4, 0))},
                "hindleg_far_upper": {"rotate": rotate((0, 0), (0.7, 1.1), (1.4, 0))},
                "tail_base": {"rotate": rotate((0, -1), (0.45, 6), (0.95, -4), (1.4, -1))},
                "tail_mid": {"rotate": rotate((0, 0), (0.45, 9), (0.95, -8), (1.4, 0))},
                "tail_tip": {"rotate": rotate((0, 0), (0.45, 12), (0.95, -10), (1.4, 0))},
            }
        },
        "attack": {
            "bones": {
                "root": {"translate": translate((0, 0, 0), (0.14, -9, -4), (0.30, -22, -2), (0.46, -16, 3), (0.72, 0, 0))},
                "torso": {"rotate": rotate((0, 0), (0.14, 3), (0.30, 6), (0.50, -2), (0.72, 0))},
                "neck": {"rotate": rotate((0, 0), (0.20, -3), (0.34, 4), (0.55, 0), (0.72, 0))},
                "head": {"rotate": rotate((0, 0), (0.24, -5), (0.42, 3), (0.72, 0))},
                "foreleg_near_upper": {"rotate": rotate((0, 0), (0.18, -9), (0.32, -18), (0.52, 4), (0.72, 0))},
                "foreleg_near_knee": {"rotate": rotate((0, 0), (0.25, 4), (0.38, 13), (0.58, -2), (0.72, 0))},
                "foreleg_far_upper": {"rotate": rotate((0, 0), (0.16, 4), (0.32, -11), (0.51, 2), (0.72, 0))},
                "foreleg_far_knee": {"rotate": rotate((0, 0), (0.26, -3), (0.39, 10), (0.60, 0), (0.72, 0))},
                "hindleg_near_upper": {"rotate": rotate((0, 0), (0.18, 4), (0.32, 8), (0.53, -3), (0.72, 0))},
                "hindleg_far_upper": {"rotate": rotate((0, 0), (0.18, -4), (0.32, 6), (0.54, 2), (0.72, 0))},
                "tail_base": {"rotate": rotate((0, 0), (0.22, -5), (0.4, 7), (0.72, 0))},
                "tail_mid": {"rotate": rotate((0, 0), (0.25, -7), (0.45, 8), (0.72, 0))},
                "tail_tip": {"rotate": rotate((0, 0), (0.27, -9), (0.50, 6), (0.72, 0))},
            }
        },
        "hurt": {
            "bones": {
                "root": {"translate": translate((0, 0, 0), (0.07, 14, 2), (0.18, 16, 0), (0.46, 0, 0))},
                "torso": {"rotate": rotate((0, 0), (0.08, -5), (0.20, -3), (0.46, 0))},
                "neck": {"rotate": rotate((0, 0), (0.09, 6), (0.23, 4), (0.46, 0))},
                "head": {"rotate": rotate((0, 0), (0.08, 9), (0.22, 5), (0.46, 0))},
                "foreleg_near_upper": {"rotate": rotate((0, 0), (0.10, 6), (0.24, 3), (0.46, 0))},
                "hindleg_near_upper": {"rotate": rotate((0, 0), (0.10, -5), (0.24, -2), (0.46, 0))},
                "tail_base": {"rotate": rotate((0, 0), (0.12, 9), (0.30, -3), (0.46, 0))},
                "tail_mid": {"rotate": rotate((0, 0), (0.14, 11), (0.32, -4), (0.46, 0))},
                "tail_tip": {"rotate": rotate((0, 0), (0.15, 12), (0.34, -4), (0.46, 0))},
            }
        },
        "death": {
            "bones": {
                "root": {"translate": translate((0, 0, 0), (0.22, 2, -15), (0.55, 1, -52), (1.1, 0, -62))},
                "torso": {"rotate": rotate((0, 0), (0.24, 5), (0.60, 13), (1.1, 11))},
                "neck": {"rotate": rotate((0, 0), (0.25, 4), (0.62, 11), (1.1, 10))},
                "head": {"rotate": rotate((0, 0), (0.30, 3), (0.70, 8), (1.1, 7))},
                "foreleg_near_upper": {"rotate": rotate((0, 0), (0.35, 8), (0.72, 17), (1.1, 16))},
                "foreleg_near_knee": {"rotate": rotate((0, 0), (0.34, -6), (0.70, -14), (1.1, -13))},
                "foreleg_far_upper": {"rotate": rotate((0, 0), (0.34, -5), (0.72, -11), (1.1, -10))},
                "foreleg_far_knee": {"rotate": rotate((0, 0), (0.34, 4), (0.70, 8), (1.1, 8))},
                "hindleg_near_upper": {"rotate": rotate((0, 0), (0.35, -7), (0.72, -14), (1.1, -13))},
                "hindleg_near_knee": {"rotate": rotate((0, 0), (0.35, 5), (0.72, 11), (1.1, 10))},
                "hindleg_far_upper": {"rotate": rotate((0, 0), (0.34, 5), (0.72, 11), (1.1, 10))},
                "hindleg_far_knee": {"rotate": rotate((0, 0), (0.35, -4), (0.70, -8), (1.1, -8))},
                "tail_base": {"rotate": rotate((0, 0), (0.35, -7), (0.78, -14), (1.1, -13))},
                "tail_mid": {"rotate": rotate((0, 0), (0.38, -9), (0.82, -15), (1.1, -14))},
                "tail_tip": {"rotate": rotate((0, 0), (0.40, -10), (0.85, -16), (1.1, -15))},
            }
        },
    }


def main() -> None:
    width, height = png_size(SOURCE)
    RUNTIME.mkdir(parents=True, exist_ok=True)
    bones, bone_world = build_bones(*BONES[0][2])
    indices = {name: i for i, (name, _, _) in enumerate(BONES)}
    attachment = build_mesh(width, height, indices, bone_world)

    document = {
        "skeleton": {
            "spine": "3.8.99",
            "hash": "forest_wolf_spine_rig_v1",
            "x": -BONES[0][2][0],
            "y": BONES[0][2][1] - height,
            "width": width,
            "height": height,
            "images": "../source/",
        },
        "bones": bones,
        "slots": [{"name": "slot_wolf", "bone": "root", "attachment": "wolf"}],
        "skins": [{"name": "default", "attachments": {"slot_wolf": {"wolf": attachment}}}],
        "animations": animations(),
    }
    json_path = RUNTIME / "forest_wolf_spine_rig_v1.spinejson"
    json_path.write_text(json.dumps(document, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")

    atlas = (
        "../source/wolf_rig_master.png\n"
        f"size: {width}, {height}\n"
        "format: RGBA8888\n"
        "filter: Linear,Linear\n"
        "repeat: none\n"
        "wolf\n"
        "  rotate: false\n"
        "  xy: 0, 0\n"
        f"  size: {width}, {height}\n"
        f"  orig: {width}, {height}\n"
        "  offset: 0, 0\n"
        "  index: -1\n"
    )
    (RUNTIME / "forest_wolf_spine_rig_v1.atlas").write_text(atlas, encoding="utf-8")
    print(f"Wrote {json_path}")
    print(f"Mesh grid: {DIVISIONS + 1} x {DIVISIONS + 1}, {len(attachment['triangles']) // 3} triangles")
    print(f"Bones: {len(bones)}; animations: {', '.join(document['animations'])}")


if __name__ == "__main__":
    main()
