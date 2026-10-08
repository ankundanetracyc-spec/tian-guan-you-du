from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOURCE_PACKAGE = ROOT / "mods" / "appearance" / "附图剑修_Spine四动作_v1"
PACKAGE = ROOT / "mods" / "appearance" / "附图剑修_Spine四动作_v2"
MASTER = SOURCE_PACKAGE / "source" / "completed_character_art_master.png"
STAGE = (384, 700)
AA = 4


POLYGONS = {
    "hair_back": [
        (163, 35), (163, 25), (169, 17), (179, 11), (191, 8), (202, 14),
        (208, 26), (217, 31), (231, 35), (243, 45), (250, 57), (252, 70),
        (249, 81), (244, 91), (243, 103), (247, 116), (255, 130), (260, 143),
        (257, 155), (249, 165), (237, 176), (222, 186), (207, 194), (192, 198),
        (178, 194), (164, 185), (151, 177), (136, 175), (120, 180), (104, 190),
        (89, 198), (80, 191), (79, 177), (85, 162), (98, 151), (116, 143),
        (135, 136), (151, 124), (162, 109), (168, 92), (170, 74), (168, 56),
        (162, 45),
    ],
    "head": [
        (164, 39), (174, 32), (189, 29), (207, 31), (224, 36), (239, 45),
        (248, 57), (252, 70), (249, 82), (244, 92), (242, 104), (239, 116),
        (231, 126), (219, 132), (206, 128), (194, 121), (184, 112), (176, 101),
        (171, 88), (170, 72), (166, 56),
    ],
    "body": [
        (177, 127), (192, 128), (205, 137), (218, 134), (234, 139), (249, 144),
        (263, 153), (273, 172), (282, 197), (291, 218), (291, 235), (279, 245),
        (264, 251), (254, 263), (262, 277), (246, 284), (169, 284), (160, 276),
        (160, 256), (151, 239), (139, 222), (134, 204), (135, 173), (145, 151),
        (158, 139),
    ],
    "arm_left": [
        (137, 143), (124, 143), (112, 148), (101, 157), (90, 169), (81, 182),
        (75, 194), (72, 205), (73, 213), (80, 219), (88, 224), (80, 235),
        (73, 250), (68, 272), (63, 298), (60, 322), (60, 339), (64, 349),
        (72, 354), (70, 378), (66, 408), (63, 449), (62, 489), (64, 508),
        (74, 520), (89, 513), (100, 489), (112, 462), (125, 435), (139, 408),
        (153, 378), (168, 352), (176, 333), (176, 295), (164, 278), (156, 257),
        (157, 235), (166, 215), (169, 192), (163, 169), (152, 153),
    ],
    "arm_right": [
        (245, 143), (259, 143), (271, 149), (280, 162), (288, 183), (297, 204),
        (301, 215), (316, 221), (329, 230), (338, 241), (338, 299), (334, 328),
        (327, 346), (317, 354), (314, 372), (317, 403), (325, 438), (341, 474),
        (357, 491), (368, 501), (365, 485), (347, 461), (334, 433), (325, 405),
        (319, 376), (316, 348), (310, 319), (304, 290), (299, 265), (293, 240),
        (283, 222), (274, 208), (265, 187), (254, 165),
    ],
    "robe_lower": [
        (165, 276), (244, 276), (257, 283), (274, 306), (291, 340), (304, 382),
        (316, 427), (335, 478), (354, 511), (358, 522), (343, 529), (323, 530),
        (298, 537), (268, 543), (231, 548), (192, 549), (154, 547), (119, 541),
        (88, 534), (68, 526), (59, 514), (61, 488), (67, 459), (77, 432),
        (89, 404), (101, 376), (112, 350), (126, 320), (143, 293),
    ],
    "legs": [
        (92, 534), (112, 536), (143, 537), (174, 535), (187, 545), (187, 573),
        (177, 588), (166, 600), (165, 625), (155, 646), (149, 651), (147, 670),
        (141, 681), (111, 681), (99, 674), (99, 661), (104, 647), (109, 632),
        (109, 612), (109, 595), (100, 584), (93, 571), (88, 552),
        (202, 538), (233, 537), (266, 535), (281, 545), (284, 567), (278, 583),
        (270, 596), (269, 622), (277, 643), (289, 652), (289, 667), (280, 677),
        (247, 677), (235, 669), (233, 652), (226, 641), (223, 619), (221, 598),
        (211, 586), (204, 570),
    ],
    "sword": [
        (306, 187), (314, 186), (320, 193), (319, 207), (318, 234), (325, 238),
        (329, 244), (322, 249), (320, 415), (315, 427), (309, 427), (304, 415),
        (303, 249), (295, 245), (298, 239), (306, 235),
    ],
    "head_ribbon": [
        (191, 27), (205, 29), (216, 36), (204, 45), (193, 47), (182, 42),
        (171, 51), (154, 65), (135, 77), (114, 86), (94, 89), (106, 96),
        (121, 91), (143, 82), (163, 68), (183, 51), (201, 40), (189, 49),
        (174, 66), (156, 84), (139, 100), (120, 112), (109, 114), (115, 105),
        (129, 93), (145, 78), (163, 61), (179, 44),
    ],
}

UNION_POLYGONS = {
    "arm_right": [
        [(245, 143), (259, 142), (272, 148), (282, 159), (289, 177), (297, 198),
         (301, 208), (296, 216), (285, 222), (276, 229), (270, 239), (263, 246),
         (252, 244), (244, 237), (239, 225), (239, 207), (241, 189), (241, 169)],
    ],
    "head_ribbon": [
        [(191, 27), (203, 27), (216, 36), (205, 44), (192, 47), (180, 42)],
        [(192, 32), (181, 40), (164, 56), (143, 70), (121, 82), (101, 88), (91, 87),
         (99, 79), (115, 68), (134, 56), (153, 45), (173, 36)],
        [(192, 36), (181, 48), (164, 67), (146, 85), (128, 101), (112, 112), (109, 114),
         (122, 115), (139, 105), (157, 89), (176, 69), (192, 47)],
    ],
    "robe_lower": [
        [(168, 281), (154, 299), (139, 321), (126, 347), (109, 371), (88, 392),
         (64, 408), (42, 417), (27, 423), (20, 432), (29, 441), (43, 436),
         (60, 430), (78, 420), (96, 406), (114, 390), (132, 371), (148, 348),
         (164, 324), (178, 301)],
        [(166, 309), (154, 336), (141, 367), (124, 399), (106, 428), (84, 453),
         (61, 474), (40, 486), (28, 492), (28, 501), (42, 509), (59, 508),
         (79, 499), (99, 483), (117, 462), (134, 438), (150, 411), (163, 383),
         (177, 350)],
        [(256, 283), (275, 294), (289, 318), (300, 345), (310, 376), (319, 410),
         (334, 441), (351, 462), (367, 482), (357, 490), (345, 476), (333, 455),
         (321, 433), (312, 407), (305, 380), (297, 354), (286, 330), (273, 310),
         (259, 297)],
    ],
    "legs": [
        [(92, 534), (112, 536), (143, 537), (174, 535), (187, 545), (187, 573),
         (177, 588), (166, 600), (165, 625), (155, 646), (149, 651), (147, 670),
         (141, 681), (111, 681), (99, 674), (99, 661), (104, 647), (109, 632),
         (109, 612), (109, 595), (100, 584), (93, 571), (88, 552)],
        [(202, 538), (233, 537), (266, 535), (281, 545), (284, 567), (278, 583),
         (270, 596), (269, 622), (277, 643), (289, 652), (303, 655), (319, 662),
         (324, 669), (315, 677), (287, 679), (264, 676), (247, 677), (235, 669),
         (233, 652), (226, 641), (223, 619), (221, 598), (211, 586), (204, 570)],
    ],
}


def mask_from_polygons(polygons: list[list[tuple[int, int]]]) -> Image.Image:
    mask_hi = Image.new("L", (STAGE[0] * AA, STAGE[1] * AA), 0)
    draw = ImageDraw.Draw(mask_hi)
    for points in polygons:
        draw.polygon([(x * AA, y * AA) for x, y in points], fill=255)
    return mask_hi.resize(STAGE, Image.Resampling.LANCZOS)


def layer_from(master: Image.Image, mask: Image.Image) -> Image.Image:
    alpha = ImageChops.multiply(master.getchannel("A"), mask)
    layer = master.copy()
    layer.putalpha(alpha)
    return layer


def make_bone_data(layer_names: list[str]) -> dict[str, object]:
    parent = {
        "underfill": "root", "hips": "root", "torso": "root", "neck": "torso", "head": "neck",
        "hair_back": "head", "head_ribbon": "head", "arm_left": "torso",
        "arm_right": "torso", "sword": "arm_right", "legs": "hips",
        "robe_lower": "hips",
    }
    screen = {
        "root": (192, 690), "underfill": (192, 350), "hips": (192, 278), "torso": (202, 185),
        "neck": (207, 130), "head": (208, 79), "hair_back": (174, 112),
        "head_ribbon": (160, 75), "arm_left": (137, 157),
        "arm_right": (269, 158), "sword": (312, 245), "legs": (192, 604),
        "robe_lower": (192, 280),
    }
    bones = [{"name": "root"}]
    for name, ancestor in parent.items():
        x, y = screen[name]
        px, py = screen[ancestor]
        bones.append({"name": name, "parent": ancestor, "x": x - px, "y": py - y, "length": 80})
    attachment_bone = {
        "underfill": "underfill",
        "hair_back": "hair_back", "head": "head", "body": "torso",
        "arm_left": "arm_left", "arm_right": "arm_right", "robe_lower": "robe_lower",
        "legs": "legs", "sword": "sword", "head_ribbon": "head_ribbon",
    }
    ordered = ["underfill", "hair_back", "legs", "robe_lower", "body", "arm_left", "sword", "arm_right", "head", "head_ribbon"]
    slots = [{"name": f"{name}_slot", "bone": attachment_bone[name], "attachment": name} for name in ordered]
    attachments: dict[str, dict[str, dict[str, object]]] = {}
    for slot in slots:
        name = slot["attachment"]
        sx, sy = screen[attachment_bone[name]]
        attachments[slot["name"]] = {name: {
            "type": "region", "name": name, "path": name,
            "x": STAGE[0] / 2 - sx, "y": sy - STAGE[1] / 2,
            "width": STAGE[0], "height": STAGE[1],
        }}

    def keys(values: list[tuple[float, float]]) -> list[dict[str, float]]:
        return [{"angle": angle} if i == 0 else {"time": time, "angle": angle}
                for i, (time, angle) in enumerate(values)]

    def rotate(values: list[tuple[float, float]]) -> dict[str, list[dict[str, float]]]:
        return {"rotate": keys(values)}

    animations = {
        "idle": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.5, "x": 0, "y": 1.6}, {"time": 1, "x": 0, "y": 0}]},
            "torso": rotate([(0, 0), (0.5, 1.0), (1, 0)]),
            "head": rotate([(0, 0), (0.5, -1.3), (1, 0)]),
            "head_ribbon": rotate([(0, 0), (0.3, -6), (0.7, 4), (1, 0)]),
            "arm_left": rotate([(0, 0), (0.5, -1.2), (1, 0)]),
            "arm_right": rotate([(0, 0), (0.5, 1.2), (1, 0)]),
        }},
        "attack": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.18, "x": 3, "y": 0}, {"time": 0.4, "x": 8, "y": 0}, {"time": 0.82, "x": 0, "y": 0}]},
            "torso": rotate([(0, 0), (0.18, -4), (0.42, 6), (0.82, 0)]),
            "arm_right": rotate([(0, 0), (0.18, -26), (0.42, 34), (0.62, 15), (0.82, 0)]),
            "sword": rotate([(0, 0), (0.18, -10), (0.42, 48), (0.62, 12), (0.82, 0)]),
            "arm_left": rotate([(0, 0), (0.2, 10), (0.5, -8), (0.82, 0)]),
            "head_ribbon": rotate([(0, 0), (0.18, -16), (0.42, 20), (0.82, 0)]),
        }},
        "hurt": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.09, "x": -8, "y": 0}, {"time": 0.25, "x": 4, "y": 0}, {"time": 0.58, "x": 0, "y": 0}]},
            "torso": rotate([(0, 0), (0.09, 9), (0.25, -4), (0.58, 0)]),
            "head": rotate([(0, 0), (0.09, 6), (0.25, -3), (0.58, 0)]),
            "arm_left": rotate([(0, 0), (0.09, 18), (0.25, -8), (0.58, 0)]),
            "arm_right": rotate([(0, 0), (0.09, -20), (0.25, 10), (0.58, 0)]),
            "sword": rotate([(0, 0), (0.09, -10), (0.25, 15), (0.58, 0)]),
            "head_ribbon": rotate([(0, 0), (0.09, 18), (0.25, -12), (0.58, 0)]),
        }},
        "death": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.55, "x": -8, "y": -30}, {"time": 1.05, "x": -12, "y": -56}]},
            "hips": rotate([(0, 0), (0.55, -10), (1.05, -21)]),
            "torso": rotate([(0, 0), (0.3, 8), (0.72, 26), (1.05, 43)]),
            "head": rotate([(0, 0), (0.3, 10), (0.72, 28), (1.05, 40)]),
            "arm_left": rotate([(0, 0), (0.35, -18), (0.74, -38), (1.05, -48)]),
            "arm_right": rotate([(0, 0), (0.35, 20), (0.74, 40), (1.05, 50)]),
            "sword": rotate([(0, 0), (0.32, -18), (0.7, -43), (1.05, -68)]),
            "legs": rotate([(0, 0), (0.55, -10), (1.05, -20)]),
            "robe_lower": rotate([(0, 0), (0.55, 6), (1.05, 12)]),
            "head_ribbon": rotate([(0, 0), (0.3, 15), (0.7, 28), (1.05, 36)]),
        }},
    }
    return {
        "skeleton": {"hash": "attached-swordsman-spine-v2", "spine": "3.8.75", "x": 0, "y": 0,
                     "width": STAGE[0], "height": STAGE[1], "images": "../layers/", "audio": ""},
        "bones": bones, "slots": slots, "skins": [{"name": "default", "attachments": attachments}],
        "animations": animations,
    }


def build() -> None:
    (PACKAGE / "source").mkdir(parents=True, exist_ok=True)
    (PACKAGE / "layers").mkdir(parents=True, exist_ok=True)
    (PACKAGE / "runtime").mkdir(parents=True, exist_ok=True)
    (PACKAGE / "preview").mkdir(parents=True, exist_ok=True)
    shutil.copy2(SOURCE_PACKAGE / "source" / "attached_material_sheet.jpg", PACKAGE / "source" / "attached_material_sheet.jpg")
    shutil.copy2(SOURCE_PACKAGE / "source" / "assembled_character_reference.jpg", PACKAGE / "source" / "assembled_character_reference.jpg")
    shutil.copy2(MASTER, PACKAGE / "source" / "completed_character_art_master.png")
    master = Image.open(MASTER).convert("RGBA").resize(STAGE, Image.Resampling.LANCZOS)

    masks = {name: mask_from_polygons([points]) for name, points in POLYGONS.items()}
    for name, polygons in UNION_POLYGONS.items():
        union = mask_from_polygons(polygons)
        masks[name] = union if name in {"head_ribbon", "legs"} else ImageChops.lighter(masks[name], union)
    masks = {name: mask.filter(ImageFilter.MaxFilter(5)) for name, mask in masks.items()}
    covered = Image.new("L", STAGE, 0)
    for mask in masks.values():
        covered = ImageChops.lighter(covered, mask)
    masks["underfill"] = ImageChops.subtract(master.getchannel("A"), covered)
    hand_mask = mask_from_polygons([[(297, 241), (321, 241), (324, 275), (314, 281), (298, 271)]])
    # Keep the blade and guard separate while restoring the hand over the hilt.
    masks["arm_right"] = ImageChops.lighter(ImageChops.subtract(masks["arm_right"], masks["sword"]), hand_mask)

    for name, mask in masks.items():
        layer_from(master, mask).save(PACKAGE / "layers" / f"{name}.png")

    order = ["underfill", "hair_back", "legs", "robe_lower", "body", "arm_left", "sword", "arm_right", "head", "head_ribbon"]
    preview = Image.new("RGBA", STAGE, (42, 48, 60, 255))
    for name in order:
        preview.alpha_composite(Image.open(PACKAGE / "layers" / f"{name}.png").convert("RGBA"))
    preview.save(PACKAGE / "preview" / "assembled_completed_v2.png")
    master_preview = Image.new("RGBA", STAGE, (42, 48, 60, 255))
    master_preview.alpha_composite(master)
    master_preview.save(PACKAGE / "preview" / "art_master_solid.png")

    data = make_bone_data(list(masks))
    runtime = PACKAGE / "runtime"
    filename = "附图剑修_completed_spine"
    raw = json.dumps(data, ensure_ascii=False, separators=(",", ":"))
    (runtime / f"{filename}.json").write_text(raw, encoding="utf-8")
    (runtime / f"{filename}.spinejson").write_text(raw, encoding="utf-8")
    atlas = []
    for name in masks:
        atlas.extend([f"../layers/{name}.png", f"size: {STAGE[0]},{STAGE[1]}", "format: RGBA8888",
                      "filter: Linear,Linear", "repeat: none", name, "  rotate: false", "  xy: 0, 0",
                      f"  size: {STAGE[0]},{STAGE[1]}", f"  orig: {STAGE[0]},{STAGE[1]}",
                      "  offset: 0, 0", "  index: -1", ""])
    (runtime / f"{filename}.atlas").write_text("\n".join(atlas), encoding="utf-8")
    readme = """# 附图剑修 Spine 四动作 v2

v2 使用补绘后的透明全身稿作为统一画稿，再按 Spine 骨骼运动方向拆成同尺寸、同原点的完整透明 PNG。v1 原始拆分包保留作对照。

## 来源和重建
- `source/attached_material_sheet.jpg`：用户提供的原始部件表。
- `source/assembled_character_reference.jpg`：用户提供的全身造型参考。
- `source/completed_character_art_master.png`：按两张参考补齐被袖子、头发和裙摆遮住区域的完整透明画稿。
- `layers/`：从补绘画稿拆出的头发、头部、躯干、左右袖臂、剑、裙摆、双腿和头部发带图层。所有图层保留 384×700 画布和同一原点，重叠位置有意保留以避免关节露缝。

## 动作
- `idle`：呼吸、头部与发带轻摆，1 秒；播放时设为循环。
- `attack`：前压、右臂挥剑并收势，0.82 秒。
- `hurt`：受击后仰并回稳，0.58 秒。
- `death`：生命值归零后下沉、躯干前倾、手臂与武器垂落并保持，1.05 秒。

## Spine 文件
- `runtime/附图剑修_completed_spine.json`、`.spinejson`、`.atlas`：Spine 3.8.75 数据。
- 编辑器导入检查后应另存为 `.spine` 工程；四动作目前仍是切片骨架的关键帧草稿，细节变形与关节遮挡需要在 Spine 内复核。

## 预览和限制
- `preview/art_master_solid.png`：补绘后完整画稿。
- `preview/assembled_completed_v2.png`：拆层重新拼合预览。
- 衣料内侧、关节下的隐藏部分为基于参考重建的补绘，和原始素材的可见部分保持同一套颜色与纹样；动作大幅展开时仍要复查遮挡边界。
"""
    (PACKAGE / "README.md").write_text(readme, encoding="utf-8")
    print(PACKAGE)


if __name__ == "__main__":
    build()
