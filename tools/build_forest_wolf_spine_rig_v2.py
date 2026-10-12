#!/usr/bin/env python3
"""Build the battle-perspective Forest Wolf Spine 3.8 weighted-mesh asset."""

from __future__ import annotations

import json
import math
import struct
from html import escape
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "textures/characters/forest_wolf_spine_rig_v2"
SOURCE = PACKAGE / "source/wolf_rig_master.png"
RUNTIME = PACKAGE / "runtime"
DIV_X = 80
DIV_Y = 54
# The bundled renderer allows at most 1,024 vertices / 3,072 indices per
# attachment. Keep the original fine mesh and split it into seamless patches.
PATCH_X = 20
PATCH_Y = 18

# Screen-space points on the 1536 x 1024 approved 3/4-view wolf. The root sits
# on the nearest paw line; the farther paws remain higher in screen space.
BONES = [
    ("root", None, (760, 996)),
    ("hips", "root", (910, 555)),
    ("torso", "hips", (720, 455)),
    ("neck", "torso", (472, 410)),
    ("head", "neck", (300, 275)),
    ("foreleg_near_upper", "torso", (585, 570)),
    ("foreleg_near_knee", "foreleg_near_upper", (624, 760)),
    ("foreleg_near_paw", "foreleg_near_knee", (610, 938)),
    ("foreleg_far_upper", "torso", (397, 540)),
    ("foreleg_far_knee", "foreleg_far_upper", (397, 690)),
    ("foreleg_far_paw", "foreleg_far_knee", (378, 865)),
    ("hindleg_far_upper", "hips", (832, 562)),
    ("hindleg_far_knee", "hindleg_far_upper", (882, 672)),
    ("hindleg_far_paw", "hindleg_far_knee", (848, 812)),
    ("hindleg_near_upper", "hips", (1035, 565)),
    ("hindleg_near_knee", "hindleg_near_upper", (1080, 710)),
    ("hindleg_near_paw", "hindleg_near_knee", (1110, 867)),
    ("tail_base", "hips", (1080, 405)),
    ("tail_mid", "tail_base", (1280, 535)),
    ("tail_tip", "tail_mid", (1450, 755)),
]

LEG_POLYGONS = {
    "foreleg_near": [
        (508, 500), (566, 492), (631, 520), (675, 585), (690, 680),
        (689, 780), (669, 881), (645, 954), (618, 1005), (559, 1012),
        (521, 983), (535, 940), (550, 865), (558, 780), (548, 692),
        (520, 610),
    ],
    "foreleg_far": [
        (340, 482), (397, 470), (451, 495), (478, 550), (473, 630),
        (453, 708), (443, 788), (426, 850), (399, 897), (353, 906),
        (322, 882), (334, 837), (348, 775), (350, 698), (345, 610),
    ],
    "hindleg_far": [
        (760, 495), (831, 474), (905, 495), (948, 545), (954, 602),
        (928, 660), (915, 723), (922, 783), (902, 827), (857, 850),
        (804, 832), (788, 795), (798, 750), (789, 695), (770, 630),
    ],
    "hindleg_near": [
        (986, 472), (1064, 466), (1136, 510), (1172, 588), (1174, 678),
        (1188, 772), (1196, 853), (1179, 910), (1135, 933), (1072, 913),
        (1040, 878), (1049, 829), (1044, 769), (1015, 707), (993, 632),
    ],
}

HEAD_POLYGON = [
    (118, 20), (279, 16), (394, 50), (467, 113), (503, 204),
    (489, 311), (444, 404), (377, 491), (297, 526), (213, 486),
    (151, 420), (113, 340), (122, 251), (104, 176),
]
NECK_POLYGON = [
    (260, 352), (346, 349), (454, 392), (558, 460), (631, 545),
    (642, 630), (592, 711), (514, 717), (438, 669), (367, 612),
    (297, 559), (251, 478),
]
TAIL_POLYGON = [
    (1075, 355), (1138, 326), (1228, 336), (1323, 387), (1410, 465),
    (1482, 560), (1510, 660), (1510, 760), (1480, 835), (1438, 880),
    (1394, 857), (1388, 807), (1404, 748), (1380, 680), (1344, 615),
    (1290, 556), (1224, 502), (1162, 465), (1100, 440), (1064, 410),
]

LEG_CHAINS = {
    "foreleg_near": [("foreleg_near_upper", (585, 570)), ("foreleg_near_knee", (624, 760)), ("foreleg_near_paw", (610, 938))],
    "foreleg_far": [("foreleg_far_upper", (397, 540)), ("foreleg_far_knee", (397, 690)), ("foreleg_far_paw", (378, 865))],
    "hindleg_far": [("hindleg_far_upper", (832, 562)), ("hindleg_far_knee", (882, 672)), ("hindleg_far_paw", (848, 812))],
    "hindleg_near": [("hindleg_near_upper", (1035, 565)), ("hindleg_near_knee", (1080, 710)), ("hindleg_near_paw", (1110, 867))],
}
TAIL_CHAIN = [("tail_base", (1080, 405)), ("tail_mid", (1280, 535)), ("tail_tip", (1450, 755))]


def png_size(path: Path) -> tuple[int, int]:
    with path.open("rb") as image:
        header = image.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Not a PNG image: {path}")
    return struct.unpack(">II", header[16:24])


def point_in_polygon(point: tuple[float, float], polygon: list[tuple[int, int]]) -> bool:
    x, y = point
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


def segment_t(point: tuple[float, float], a: tuple[int, int], b: tuple[int, int]) -> tuple[float, float]:
    px, py = point
    dx, dy = b[0] - a[0], b[1] - a[1]
    denom = dx * dx + dy * dy
    t = 0.0 if denom == 0 else max(0.0, min(1.0, ((px - a[0]) * dx + (py - a[1]) * dy) / denom))
    qx, qy = a[0] + t * dx, a[1] + t * dy
    return t, math.hypot(px - qx, py - qy)


def chain_weights(point: tuple[float, float], chain: list[tuple[str, tuple[int, int]]]) -> list[tuple[str, float]]:
    candidates = []
    for index in range(len(chain) - 1):
        t, distance = segment_t(point, chain[index][1], chain[index + 1][1])
        candidates.append((distance, index, t))
    _, index, t = min(candidates)
    return [(chain[index][0], 1.0 - t), (chain[index + 1][0], t)]


def blend_weights(primary: list[tuple[str, float]], secondary: list[tuple[str, float]], amount: float) -> list[tuple[str, float]]:
    amount = max(0.0, min(1.0, amount))
    combined: dict[str, float] = {}
    for name, weight in primary:
        combined[name] = combined.get(name, 0.0) + weight * (1.0 - amount)
    for name, weight in secondary:
        combined[name] = combined.get(name, 0.0) + weight * amount
    return list(combined.items())


def mix(a: str, b: str, b_weight: float) -> list[tuple[str, float]]:
    b_weight = max(0.0, min(1.0, b_weight))
    return [(a, 1.0 - b_weight), (b, b_weight)]


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def weights_at(point: tuple[float, float]) -> list[tuple[str, float]]:
    x, y = point
    if point_in_polygon(point, TAIL_POLYGON):
        tail = chain_weights(point, TAIL_CHAIN)
        distance = math.hypot(x - 1080.0, y - 405.0)
        return blend_weights(tail, [("hips", 1.0)], 0.22 * clamp01((185.0 - distance) / 185.0))

    for part in ("foreleg_far", "foreleg_near", "hindleg_far", "hindleg_near"):
        if point_in_polygon(point, LEG_POLYGONS[part]):
            leg = chain_weights(point, LEG_CHAINS[part])
            shoulder_y = LEG_CHAINS[part][0][1][1]
            parent = "torso" if part.startswith("foreleg") else "hips"
            body_blend = 0.25 * (1.0 - clamp01((y - shoulder_y) / 145.0))
            return blend_weights(leg, [(parent, 1.0)], body_blend)

    if point_in_polygon(point, HEAD_POLYGON):
        head_weight = max(0.45, min(0.96, (505.0 - x) / 230.0))
        return mix("neck", "head", head_weight)
    if point_in_polygon(point, NECK_POLYGON):
        torso_weight = 0.48 * clamp01((x - 350.0) / 300.0)
        return mix("neck", "torso", torso_weight)

    torso_weight = clamp01((900.0 - x) / 255.0)
    return mix("hips", "torso", torso_weight)


def build_bones() -> tuple[list[dict], dict[str, tuple[float, float]]]:
    root_x, root_y = BONES[0][2]
    world: dict[str, tuple[float, float]] = {}
    result = []
    for name, parent, screen_point in BONES:
        absolute = (float(screen_point[0] - root_x), float(root_y - screen_point[1]))
        world[name] = absolute
        bone = {"name": name}
        if parent is not None:
            px, py = world[parent]
            bone.update({"parent": parent, "x": round(absolute[0] - px, 4), "y": round(absolute[1] - py, 4)})
        result.append(bone)
    return result, world


def build_mesh(width: int, height: int, bone_indices: dict[str, int],
               bone_world: dict[str, tuple[float, float]],
               start_x: int, start_y: int, end_x: int, end_y: int) -> dict:
    columns = end_x - start_x + 1
    vertices: list[float | int] = []
    uvs: list[float] = []
    root_x, root_y = BONES[0][2]
    for iy in range(start_y, end_y + 1):
        y = height * iy / DIV_Y
        for ix in range(start_x, end_x + 1):
            x = width * ix / DIV_X
            influences = [(name, weight) for name, weight in weights_at((x, y)) if weight > 0.015]
            influences.sort(key=lambda item: item[1], reverse=True)
            influences = influences[:3]
            total = sum(weight for _, weight in influences) or 1.0
            influences = [(name, weight / total) for name, weight in influences]
            vx, vy = x - root_x, root_y - y
            vertices.append(len(influences))
            for name, weight in influences:
                bx, by = bone_world[name]
                vertices.extend([bone_indices[name], round(vx - bx, 4), round(vy - by, 4), round(weight, 6)])
            uvs.extend([round(x / width, 6), round(y / height, 6)])

    triangles: list[int] = []
    for iy in range(end_y - start_y):
        for ix in range(end_x - start_x):
            tl = iy * columns + ix
            tr, bl = tl + 1, tl + columns
            br = bl + 1
            triangles.extend([tl, tr, br, tl, br, bl])

    return {
        "type": "mesh", "path": "wolf", "uvs": uvs, "triangles": triangles,
        "vertices": vertices, "hull": 4, "width": width, "height": height,
    }


def rotate(*keys: tuple[float, float]) -> list[dict]:
    return [{"time": t, "angle": angle} for t, angle in keys]


def translate(*keys: tuple[float, float, float]) -> list[dict]:
    return [{"time": t, "x": x, "y": y} for t, x, y in keys]


def animations() -> dict:
    return {
        "idle": {"bones": {
            "root": {"translate": translate((0, 0, 0), (0.6, 0, 1.5), (1.2, 0, 0))},
            "hips": {"rotate": rotate((0, 0), (0.6, 0.8), (1.2, 0))},
            "torso": {"rotate": rotate((0, 0), (0.6, -1.1), (1.2, 0))},
            "neck": {"rotate": rotate((0, 0), (0.6, 0.8), (1.2, 0))},
            "head": {"rotate": rotate((0, 0), (0.6, -0.7), (1.2, 0))},
            "foreleg_near_upper": {"rotate": rotate((0, 0), (0.6, 0.6), (1.2, 0))},
            "foreleg_far_upper": {"rotate": rotate((0, 0), (0.6, -0.5), (1.2, 0))},
            "hindleg_near_upper": {"rotate": rotate((0, 0), (0.6, -0.6), (1.2, 0))},
            "hindleg_far_upper": {"rotate": rotate((0, 0), (0.6, 0.5), (1.2, 0))},
            "tail_base": {"rotate": rotate((0, -2), (0.35, 3), (0.8, -4), (1.2, -2))},
            "tail_mid": {"rotate": rotate((0, 0), (0.35, 4), (0.8, -4), (1.2, 0))},
            "tail_tip": {"rotate": rotate((0, 0), (0.35, 5), (0.8, -5), (1.2, 0))},
        }},
        "attack": {"bones": {
            "root": {"translate": translate((0, 0, 0), (0.12, 3, -9), (0.28, -34, 15), (0.42, -44, 8), (0.62, -12, -2), (0.82, 0, 0))},
            "hips": {"rotate": rotate((0, 0), (0.12, -3), (0.30, 3), (0.52, -1), (0.82, 0))},
            "torso": {"rotate": rotate((0, 0), (0.16, 2), (0.32, -4), (0.58, 1), (0.82, 0))},
            "neck": {"rotate": rotate((0, 0), (0.18, -4), (0.35, 3), (0.60, 0), (0.82, 0))},
            "head": {"rotate": rotate((0, 0), (0.20, -7), (0.42, 2), (0.82, 0))},
            "foreleg_near_upper": {"rotate": rotate((0, 0), (0.16, -12), (0.32, -21), (0.50, 4), (0.82, 0))},
            "foreleg_near_knee": {"rotate": rotate((0, 0), (0.20, 6), (0.36, 15), (0.58, -3), (0.82, 0))},
            "foreleg_far_upper": {"rotate": rotate((0, 0), (0.16, -6), (0.32, -12), (0.50, 2), (0.82, 0))},
            "foreleg_far_knee": {"rotate": rotate((0, 0), (0.20, 4), (0.36, 11), (0.58, -2), (0.82, 0))},
            "hindleg_near_upper": {"rotate": rotate((0, 0), (0.15, 5), (0.32, 10), (0.52, -3), (0.82, 0))},
            "hindleg_near_knee": {"rotate": rotate((0, 0), (0.16, -4), (0.34, -9), (0.56, 2), (0.82, 0))},
            "hindleg_far_upper": {"rotate": rotate((0, 0), (0.15, 4), (0.32, 7), (0.52, -2), (0.82, 0))},
            "hindleg_far_knee": {"rotate": rotate((0, 0), (0.16, -3), (0.34, -7), (0.56, 2), (0.82, 0))},
            "tail_base": {"rotate": rotate((0, -2), (0.20, 4), (0.40, 10), (0.82, -2))},
            "tail_mid": {"rotate": rotate((0, 0), (0.22, -4), (0.44, 8), (0.82, 0))},
            "tail_tip": {"rotate": rotate((0, 0), (0.26, -7), (0.48, 6), (0.82, 0))},
        }},
        "hurt": {"bones": {
            "root": {"translate": translate((0, 0, 0), (0.08, 15, 2), (0.20, 20, 0), (0.50, 0, 0))},
            "hips": {"rotate": rotate((0, 0), (0.10, -3), (0.25, -1), (0.50, 0))},
            "torso": {"rotate": rotate((0, 0), (0.08, -5), (0.22, -2), (0.50, 0))},
            "neck": {"rotate": rotate((0, 0), (0.10, 6), (0.24, 3), (0.50, 0))},
            "head": {"rotate": rotate((0, 0), (0.10, 9), (0.25, 4), (0.50, 0))},
            "foreleg_near_upper": {"rotate": rotate((0, 0), (0.10, 5), (0.24, 2), (0.50, 0))},
            "foreleg_far_upper": {"rotate": rotate((0, 0), (0.10, 3), (0.24, 1), (0.50, 0))},
            "hindleg_near_upper": {"rotate": rotate((0, 0), (0.10, -4), (0.24, -1), (0.50, 0))},
            "hindleg_far_upper": {"rotate": rotate((0, 0), (0.10, -3), (0.24, -1), (0.50, 0))},
            "tail_base": {"rotate": rotate((0, -2), (0.12, 8), (0.32, -4), (0.50, -2))},
            "tail_mid": {"rotate": rotate((0, 0), (0.14, 10), (0.34, -5), (0.50, 0))},
            "tail_tip": {"rotate": rotate((0, 0), (0.16, 10), (0.36, -5), (0.50, 0))},
        }},
        "death": {"bones": {
            "root": {"translate": translate((0, 0, 0), (0.28, 0, -5), (0.78, 5, -28), (1.25, 8, -32))},
            "hips": {"rotate": rotate((0, 0), (0.30, 2), (0.80, 5), (1.25, 5))},
            "torso": {"rotate": rotate((0, 0), (0.30, 3), (0.82, 8), (1.25, 9))},
            "neck": {"rotate": rotate((0, 0), (0.32, 8), (0.86, 18), (1.25, 18))},
            "head": {"rotate": rotate((0, 0), (0.35, 8), (0.90, 16), (1.25, 15))},
            "foreleg_near_upper": {"rotate": rotate((0, 0), (0.30, -3), (0.80, -8), (1.25, -8))},
            "foreleg_near_knee": {"rotate": rotate((0, 0), (0.32, 8), (0.82, 20), (1.25, 20))},
            "foreleg_far_upper": {"rotate": rotate((0, 0), (0.30, -2), (0.80, 6), (1.25, 6))},
            "foreleg_far_knee": {"rotate": rotate((0, 0), (0.32, -5), (0.82, -11), (1.25, -11))},
            "hindleg_near_upper": {"rotate": rotate((0, 0), (0.30, 5), (0.82, 13), (1.25, 13))},
            "hindleg_near_knee": {"rotate": rotate((0, 0), (0.32, -7), (0.82, -16), (1.25, -16))},
            "hindleg_far_upper": {"rotate": rotate((0, 0), (0.30, -4), (0.82, -10), (1.25, -10))},
            "hindleg_far_knee": {"rotate": rotate((0, 0), (0.32, 6), (0.82, 12), (1.25, 12))},
            "tail_base": {"rotate": rotate((0, -2), (0.35, -7), (0.85, -15), (1.25, -15))},
            "tail_mid": {"rotate": rotate((0, 0), (0.38, -9), (0.88, -13), (1.25, -13))},
            "tail_tip": {"rotate": rotate((0, 0), (0.40, -8), (0.90, -10), (1.25, -10))},
        }},
    }


def write_bone_layout() -> None:
    colors = {
        "root": "#ffffff", "hips": "#e3b86b", "torso": "#ffe5a6", "neck": "#97d9c4", "head": "#fff8df",
        "foreleg_near": "#ffb347", "foreleg_far": "#62d5cb",
        "hindleg_near": "#ff7c79", "hindleg_far": "#83a8ff", "tail": "#b2e87c",
    }
    lines = [
        '<svg xmlns="http://www.w3.org/2000/svg" width="1536" height="1024" viewBox="0 0 1536 1024">',
        '<image href="../source/wolf_rig_master.png" width="1536" height="1024"/>',
        '<rect x="1" y="1" width="1534" height="1022" fill="none" stroke="#ffffff" stroke-opacity=".45" stroke-width="2"/>',
        '<g fill="none" stroke-linecap="round" stroke-linejoin="round">',
    ]
    screen_points = {name: point for name, _, point in BONES}
    for name, parent, (x, y) in BONES:
        if parent is None:
            continue
        if name.startswith("foreleg_near"):
            color = colors["foreleg_near"]
        elif name.startswith("foreleg_far"):
            color = colors["foreleg_far"]
        elif name.startswith("hindleg_near"):
            color = colors["hindleg_near"]
        elif name.startswith("hindleg_far"):
            color = colors["hindleg_far"]
        elif name.startswith("tail_"):
            color = colors["tail"]
        else:
            color = colors.get(name, "#ffffff")
        px, py = screen_points[parent]
        lines.append(f'<line x1="{px}" y1="{py}" x2="{x}" y2="{y}" stroke="{color}" stroke-width="7" stroke-opacity=".9"/>')
    lines.append('</g><g font-family="Segoe UI, sans-serif" font-size="17" font-weight="700" paint-order="stroke" stroke="#101820" stroke-width="4" stroke-linejoin="round">')
    for name, _, (x, y) in BONES:
        if name.startswith("foreleg_near"):
            color = colors["foreleg_near"]
        elif name.startswith("foreleg_far"):
            color = colors["foreleg_far"]
        elif name.startswith("hindleg_near"):
            color = colors["hindleg_near"]
        elif name.startswith("hindleg_far"):
            color = colors["hindleg_far"]
        elif name.startswith("tail_"):
            color = colors["tail"]
        else:
            color = colors.get(name, "#ffffff")
        lines.append(f'<circle cx="{x}" cy="{y}" r="9" fill="{color}" stroke="#101820" stroke-width="3"/>')
        lines.append(f'<text x="{x + 12}" y="{y - 10}" fill="{color}" stroke="#101820">{escape(name)}</text>')
    lines.extend(['</g></svg>'])
    (PACKAGE / "preview/bone_layout.svg").write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    width, height = png_size(SOURCE)
    if (width, height) != (1536, 1024):
        raise ValueError(f"Expected approved 1536x1024 source, got {width}x{height}")
    RUNTIME.mkdir(parents=True, exist_ok=True)
    (PACKAGE / "preview").mkdir(parents=True, exist_ok=True)
    bones, bone_world = build_bones()
    bone_indices = {bone["name"]: index for index, bone in enumerate(bones)}
    slots = []
    attachments = {}
    triangle_count = 0
    for start_y in range(0, DIV_Y, PATCH_Y):
        for start_x in range(0, DIV_X, PATCH_X):
            name = f"wolf_{start_x // PATCH_X}_{start_y // PATCH_Y}"
            slot_name = f"slot_{name}"
            attachment = build_mesh(width, height, bone_indices, bone_world,
                                    start_x, start_y,
                                    min(start_x + PATCH_X, DIV_X),
                                    min(start_y + PATCH_Y, DIV_Y))
            assert len(attachment["uvs"]) <= 2048
            assert len(attachment["triangles"]) <= 3072
            triangle_count += len(attachment["triangles"]) // 3
            slots.append({"name": slot_name, "bone": "root", "attachment": name})
            attachments[slot_name] = {name: attachment}
    document = {
        "skeleton": {
            "spine": "3.8.99", "hash": "forest_wolf_spine_rig_v2",
            "x": -BONES[0][2][0], "y": BONES[0][2][1] - height,
            "width": width, "height": height, "images": "../source/",
        },
        "bones": bones,
        "slots": slots,
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": animations(),
    }
    stem = "forest_wolf_spine_rig_v2"
    raw = json.dumps(document, ensure_ascii=False, separators=(",", ":"))
    (RUNTIME / f"{stem}.spinejson").write_text(raw, encoding="utf-8")
    (RUNTIME / f"{stem}.json").write_text(raw, encoding="utf-8")
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
    (RUNTIME / f"{stem}.atlas").write_text(atlas, encoding="utf-8")
    write_bone_layout()
    print(f"Wrote {RUNTIME / (stem + '.spinejson')}")
    print(f"Mesh {DIV_X + 1}x{DIV_Y + 1}; {len(slots)} patches; {triangle_count} triangles; {len(bones)} bones")
    print("Animations: idle (loop), attack, hurt, death")


if __name__ == "__main__":
    main()
