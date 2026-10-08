from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
V1 = ROOT / "mods" / "appearance" / "附图剑修_Spine四动作_v1"
PACKAGE = ROOT / "mods" / "appearance" / "附图剑修_Spine四动作_v3"
MASTER_SOURCE = Path(r"D:\CodexData\generated_images\01a0ccea-dd7a-71e1-a826-1b8e355743bc\exec-fa579c29-7f0b-4710-b8a6-43624e3c4517.png")
STAGE = (384, 700)
AA = 4


# Hand-traced masks in 384x700 stage coordinates. They intentionally overlap at
# seams; each image stays on a shared canvas so Spine attachments keep one origin.
POLYGONS: dict[str, list[list[tuple[int, int]]]] = {
    "hair_back": [
        [(158, 27), (158, 14), (164, 6), (175, 3), (186, 8), (190, 19),
         (202, 25), (216, 35), (229, 49), (236, 63), (234, 79), (226, 94),
         (211, 103), (205, 119), (194, 135), (178, 145), (158, 146), (148, 137),
         (155, 119), (162, 100), (162, 78), (160, 57)],
        [(166, 48), (157, 68), (148, 91), (136, 111), (118, 127), (95, 142),
         (73, 154), (53, 167), (43, 180), (46, 192), (59, 201), (78, 206),
         (96, 214), (111, 226), (128, 240), (144, 257), (159, 276), (169, 299),
         (171, 333), (167, 360), (157, 387), (145, 408), (133, 413), (122, 406),
         (116, 392), (121, 376), (125, 352), (127, 326), (124, 300), (114, 275),
         (99, 251), (81, 231), (61, 218), (43, 207), (32, 194), (34, 178),
         (43, 163), (58, 149), (78, 137), (99, 125), (119, 111), (137, 96),
         (150, 78), (159, 59)],
    ],
    "head": [[
        (174, 33), (181, 26), (194, 25), (208, 31), (220, 39), (231, 50),
        (235, 63), (230, 76), (228, 88), (222, 99), (210, 109), (194, 108),
        (181, 99), (174, 85), (172, 67), (169, 51),
    ]],
    "head_ribbon": [
        [(165, 21), (163, 11), (169, 5), (179, 4), (190, 9), (194, 18),
         (186, 28), (174, 29)],
        [(170, 17), (154, 28), (133, 42), (111, 57), (91, 72), (74, 86),
         (56, 94), (41, 94), (48, 84), (65, 73), (82, 61), (105, 47),
         (127, 34), (149, 23), (165, 15)],
        [(177, 21), (162, 35), (145, 53), (129, 73), (114, 94), (101, 116),
         (89, 136), (76, 146), (64, 144), (72, 132), (84, 115), (98, 96),
         (114, 77), (132, 59), (150, 41), (168, 26)],
    ],
    "torso": [[
        (166, 89), (180, 88), (191, 94), (204, 95), (219, 98), (237, 101),
        (251, 111), (262, 128), (270, 153), (277, 182), (284, 211), (284, 233),
        (271, 251), (260, 265), (263, 282), (254, 299), (241, 309), (174, 309),
        (161, 300), (155, 279), (153, 253), (150, 220), (148, 186), (151, 151),
        (158, 119),
    ]],
    "sleeve_left": [
        [(167, 103), (153, 111), (139, 127), (121, 143), (104, 162), (87, 187),
         (72, 216), (61, 248), (55, 278), (56, 308), (64, 330), (77, 342),
         (93, 333), (104, 317), (115, 298), (128, 280), (143, 262), (158, 245),
         (172, 221), (178, 193), (178, 163)],
        [(116, 205), (99, 221), (82, 242), (65, 267), (47, 294), (34, 319),
         (32, 333), (43, 340), (56, 333), (72, 320), (88, 304), (104, 285),
         (120, 265), (137, 246), (152, 225)],
        [(90, 299), (76, 321), (62, 345), (48, 372), (38, 398), (36, 415),
         (48, 422), (62, 415), (76, 397), (91, 375), (106, 350), (120, 324),
         (134, 298)],
    ],
    "sleeve_right": [[
        (238, 101), (253, 104), (266, 116), (276, 137), (286, 166), (295, 196),
        (307, 224), (322, 252), (338, 285), (354, 322), (366, 352), (367, 367),
        (356, 378), (342, 371), (327, 355), (316, 333), (303, 306), (290, 277),
        (276, 250), (262, 228), (249, 202), (239, 174), (234, 144),
    ]],
    "robe_lower": [
        [(163, 295), (251, 295), (264, 308), (275, 333), (287, 361), (300, 393),
         (316, 428), (335, 462), (352, 490), (361, 511), (356, 524), (341, 531),
         (314, 535), (279, 541), (238, 546), (194, 547), (151, 543), (113, 537),
         (84, 529), (65, 518), (57, 506), (59, 486), (67, 459), (78, 431),
         (91, 402), (104, 374), (119, 344), (136, 317)],
        [(71, 488), (58, 503), (43, 514), (29, 523), (27, 531), (39, 537),
         (55, 532), (75, 520), (94, 506), (111, 489)],
        [(298, 468), (317, 477), (336, 492), (353, 509), (368, 527), (371, 538),
         (360, 545), (343, 540), (327, 529), (311, 515), (296, 501)],
    ],
    "legs": [[
        (91, 521), (120, 526), (158, 531), (186, 533), (198, 545), (198, 572),
        (188, 591), (172, 604), (164, 626), (158, 647), (153, 665), (143, 675),
        (113, 676), (100, 668), (100, 654), (108, 637), (111, 616), (108, 597),
        (98, 581), (91, 559),
    ], [
        (195, 532), (229, 529), (263, 525), (280, 533), (285, 552), (281, 573),
        (273, 589), (267, 608), (271, 630), (282, 646), (299, 655), (313, 663),
        (315, 673), (304, 680), (275, 679), (251, 675), (235, 665), (231, 646),
        (222, 628), (218, 605), (211, 586), (199, 567),
    ]],
    "arm_left": [[
        (137, 120), (145, 119), (152, 124), (154, 142), (160, 149), (164, 160),
        (159, 170), (153, 176), (151, 195), (148, 220), (141, 235), (130, 234),
        (127, 220), (128, 197), (128, 178), (131, 160), (132, 146),
    ]],
    "arm_right": [[
        (303, 249), (311, 245), (318, 249), (321, 262), (327, 271), (326, 281),
        (319, 289), (310, 291), (304, 284), (302, 270),
    ]],
}

UNION_POLYGONS = {
    "hair_back": [[
        (152, 21), (166, 14), (182, 14), (197, 24), (211, 39), (220, 57),
        (220, 78), (208, 95), (196, 107), (185, 122), (170, 131), (157, 124),
        (160, 102), (160, 78), (157, 53),
    ]],
    "sleeve_left": [[
        (148, 126), (132, 142), (113, 159), (93, 182), (76, 211), (64, 241),
        (56, 269), (53, 298), (59, 323), (73, 336), (88, 324), (103, 304),
        (120, 282), (137, 260), (154, 237), (168, 211), (174, 181), (168, 151),
    ]],
    "sleeve_right": [[
        (251, 103), (270, 114), (282, 139), (293, 169), (304, 198), (317, 225),
        (333, 253), (347, 286), (360, 319), (370, 350), (368, 368), (355, 379),
        (339, 369), (324, 348), (312, 322), (299, 294), (286, 265), (272, 239),
        (256, 213), (244, 182), (237, 151),
    ]],
    "robe_lower": [[
        (156, 299), (244, 300), (262, 316), (278, 348), (294, 386), (312, 426),
        (334, 466), (355, 500), (366, 522), (359, 536), (340, 541), (309, 543),
        (274, 549), (236, 551), (190, 550), (148, 547), (111, 540), (78, 530),
        (58, 516), (56, 500), (66, 470), (80, 436), (94, 400), (110, 365),
        (128, 331),
    ]],
}


def mask_from_polygons(polygons: list[list[tuple[int, int]]]) -> Image.Image:
    high = Image.new("L", (STAGE[0] * AA, STAGE[1] * AA), 0)
    draw = ImageDraw.Draw(high)
    for points in polygons:
        draw.polygon([(x * AA, y * AA) for x, y in points], fill=255)
    return high.resize(STAGE, Image.Resampling.LANCZOS)


def make_bone_data(layer_names: list[str]) -> dict[str, object]:
    parent = {
        "hips": "root", "torso": "hips", "neck": "torso", "head": "neck",
        "hair_back": "head", "head_ribbon": "head", "arm_left": "torso",
        "arm_right": "torso", "sleeve_left": "torso", "sleeve_right": "torso",
        "robe_lower": "hips", "legs": "hips", "underfill": "root",
    }
    screen = {
        "root": (192, 690), "hips": (192, 365), "torso": (199, 204),
        "neck": (191, 112), "head": (203, 69), "hair_back": (132, 180),
        "head_ribbon": (126, 78), "arm_left": (143, 203), "arm_right": (313, 269),
        "sleeve_left": (118, 215), "sleeve_right": (285, 204),
        "robe_lower": (192, 327), "legs": (192, 590), "underfill": (192, 350),
    }
    bones = [{"name": "root"}]
    for name, ancestor in parent.items():
        x, y = screen[name]
        px, py = screen[ancestor]
        bones.append({"name": name, "parent": ancestor, "x": x - px, "y": py - y, "length": 80})

    attach_bone = {name: name for name in layer_names}
    attach_bone["underfill"] = "underfill"
    attach_bone["torso"] = "torso"
    ordered = ["underfill", "hair_back", "legs", "robe_lower", "sleeve_left",
               "sleeve_right", "torso", "arm_left", "arm_right", "head", "head_ribbon"]
    slots = [{"name": f"{name}_slot", "bone": attach_bone[name], "attachment": name} for name in ordered]
    attachments: dict[str, dict[str, dict[str, object]]] = {}
    for slot in slots:
        name = slot["attachment"]
        sx, sy = screen[attach_bone[name]]
        attachments[slot["name"]] = {name: {
            "type": "region", "name": name, "path": name,
            "x": STAGE[0] / 2 - sx, "y": sy - STAGE[1] / 2,
            "width": STAGE[0], "height": STAGE[1],
        }}

    def angle_keys(values: list[tuple[float, float]]) -> list[dict[str, float]]:
        return [{"angle": a} if i == 0 else {"time": t, "angle": a}
                for i, (t, a) in enumerate(values)]

    def rotate(values: list[tuple[float, float]]) -> dict[str, list[dict[str, float]]]:
        return {"rotate": angle_keys(values)}

    def translate(values: list[tuple[float, float, float]]) -> dict[str, list[dict[str, float]]]:
        return {"translate": [{"x": x, "y": y} if i == 0 else {"time": t, "x": x, "y": y}
                              for i, (t, x, y) in enumerate(values)]}

    animations = {
        "idle": {"bones": {
            "root": translate([(0, 0, 0), (0.6, 0, 1.2), (1.2, 0, 0)]),
            "torso": rotate([(0, 0), (0.6, 0.8), (1.2, 0)]),
            "head": rotate([(0, 0), (0.6, -0.8), (1.2, 0)]),
            "arm_left": rotate([(0, 0), (0.6, -0.6), (1.2, 0)]),
            "head_ribbon": rotate([(0, 0), (0.3, -4), (0.8, 3), (1.2, 0)]),
            "sleeve_left": rotate([(0, 0), (0.6, 0.7), (1.2, 0)]),
            "sleeve_right": rotate([(0, 0), (0.6, -0.7), (1.2, 0)]),
        }},
        "attack": {"bones": {
            "root": translate([(0, 0, 0), (0.14, 3, 0), (0.32, 6, 0), (0.58, 0, 0)]),
            "torso": rotate([(0, 0), (0.14, -3), (0.32, 5), (0.58, 0)]),
            "arm_left": rotate([(0, 0), (0.14, -9), (0.32, -26), (0.43, -10), (0.58, 0)]),
            "head": rotate([(0, 0), (0.14, -2), (0.32, -5), (0.58, 0)]),
            "head_ribbon": rotate([(0, 0), (0.14, -10), (0.32, 14), (0.58, 0)]),
            "sleeve_left": rotate([(0, 0), (0.14, -4), (0.32, -9), (0.58, 0)]),
        }},
        "hurt": {"bones": {
            "root": translate([(0, 0, 0), (0.08, -7, 0), (0.23, 3, 0), (0.55, 0, 0)]),
            "torso": rotate([(0, 0), (0.08, 8), (0.23, -3), (0.55, 0)]),
            "head": rotate([(0, 0), (0.08, 7), (0.23, -3), (0.55, 0)]),
            "arm_left": rotate([(0, 0), (0.08, 15), (0.23, -8), (0.55, 0)]),
            "arm_right": rotate([(0, 0), (0.08, -13), (0.23, 7), (0.55, 0)]),
            "sleeve_left": rotate([(0, 0), (0.08, 8), (0.23, -4), (0.55, 0)]),
            "sleeve_right": rotate([(0, 0), (0.08, -8), (0.23, 4), (0.55, 0)]),
            "head_ribbon": rotate([(0, 0), (0.08, 15), (0.23, -10), (0.55, 0)]),
        }},
        "death": {"bones": {
            "root": translate([(0, 0, 0), (0.45, -6, -22), (0.95, -12, -53)]),
            "hips": rotate([(0, 0), (0.45, -9), (0.95, -18)]),
            "torso": rotate([(0, 0), (0.28, -9), (0.65, -22), (0.95, -34)]),
            "head": rotate([(0, 0), (0.28, -4), (0.65, -14), (0.95, -23)]),
            "arm_left": rotate([(0, 0), (0.3, 8), (0.68, 22), (0.95, 32)]),
            "arm_right": rotate([(0, 0), (0.3, -12), (0.68, -26), (0.95, -37)]),
            "legs": rotate([(0, 0), (0.45, 5), (0.95, 11)]),
            "robe_lower": rotate([(0, 0), (0.45, -4), (0.95, -9)]),
            "sleeve_left": rotate([(0, 0), (0.3, 8), (0.68, 18), (0.95, 24)]),
            "sleeve_right": rotate([(0, 0), (0.3, -8), (0.68, -17), (0.95, -23)]),
            "head_ribbon": rotate([(0, 0), (0.3, 12), (0.68, 24), (0.95, 32)]),
        }},
    }
    return {
        "skeleton": {"hash": "jianfinger-cultivator-spine-v3", "spine": "3.8.75", "x": 0, "y": 0,
                     "width": STAGE[0], "height": STAGE[1], "images": "../layers/", "audio": ""},
        "bones": bones, "slots": slots, "skins": [{"name": "default", "attachments": attachments}],
        "animations": animations,
    }


def render_contact(master: Image.Image) -> None:
    # Pose panels transform the cut layers around their rough bone pivots.
    order = ["underfill", "hair_back", "legs", "robe_lower", "sleeve_left", "sleeve_right",
             "torso", "arm_left", "arm_right", "head", "head_ribbon"]
    pivots = {"hair_back": (132, 180), "legs": (192, 590), "robe_lower": (192, 327),
              "sleeve_left": (118, 215), "sleeve_right": (285, 204), "torso": (199, 204),
              "arm_left": (143, 203), "arm_right": (313, 269), "head": (203, 69),
              "head_ribbon": (126, 78)}

    def compose(rotations: dict[str, float] | None = None,
                translations: dict[str, tuple[int, int]] | None = None) -> Image.Image:
        rotations = rotations or {}
        translations = translations or {}
        canvas = Image.new("RGBA", STAGE, (0, 0, 0, 0))
        for name in order:
            layer = Image.open(PACKAGE / "layers" / f"{name}.png").convert("RGBA")
            if name in rotations:
                layer = layer.rotate(rotations[name], resample=Image.Resampling.BICUBIC,
                                     center=pivots.get(name, (192, 350)))
            dx, dy = translations.get(name, (0, 0))
            if dx or dy:
                shifted_layer = Image.new("RGBA", STAGE, (0, 0, 0, 0))
                shifted_layer.alpha_composite(layer, (dx, dy))
                layer = shifted_layer
            canvas.alpha_composite(layer)
        return canvas

    poses: list[tuple[str, Image.Image]] = [
        ("idle", compose()),
        ("attack - jian finger", compose({"arm_left": -26, "sleeve_left": -7, "torso": -3,
                                           "head_ribbon": -12})),
        ("hurt", compose({"torso": 7, "head": 5, "arm_left": 13, "arm_right": -12,
                          "sleeve_left": 7, "sleeve_right": -7, "head_ribbon": 12})),
        ("death", master.rotate(-14, resample=Image.Resampling.BICUBIC, center=(192, 365))),
    ]
    sheet = Image.new("RGBA", (STAGE[0] * 2, 750), (36, 40, 48, 255))
    draw = ImageDraw.Draw(sheet)
    for i, (label, pose) in enumerate(poses):
        x = (i % 2) * STAGE[0]
        y = (i // 2) * 375
        panel = pose.copy()
        panel.thumbnail((350, 340), Image.Resampling.LANCZOS)
        sheet.alpha_composite(panel, (x + 17, y + 25))
        draw.text((x + 12, y + 8), label, fill=(245, 243, 232, 255))
    sheet.convert("RGB").save(PACKAGE / "preview" / "animation_contact_sheet.jpg", quality=94)


def build() -> None:
    for folder in ("source", "layers", "runtime", "preview"):
        (PACKAGE / folder).mkdir(parents=True, exist_ok=True)
    shutil.copy2(V1 / "source" / "attached_material_sheet.jpg", PACKAGE / "source" / "attached_material_sheet.jpg")
    shutil.copy2(V1 / "source" / "assembled_character_reference.jpg", PACKAGE / "source" / "assembled_character_reference.jpg")
    shutil.copy2(MASTER_SOURCE, PACKAGE / "source" / "completed_character_art_master.png")
    master = Image.open(MASTER_SOURCE).convert("RGBA").resize(STAGE, Image.Resampling.LANCZOS)

    masks = {name: mask_from_polygons(polys) for name, polys in POLYGONS.items()}
    for name, polys in UNION_POLYGONS.items():
        masks[name] = ImageChops.lighter(masks[name], mask_from_polygons(polys))
    masks = {name: mask.filter(ImageFilter.MaxFilter(5)) for name, mask in masks.items()}
    covered = Image.new("L", STAGE, 0)
    for mask in masks.values():
        covered = ImageChops.lighter(covered, mask)
    masks["underfill"] = ImageChops.subtract(master.getchannel("A"), covered)

    for name, mask in masks.items():
        alpha = ImageChops.multiply(master.getchannel("A"), mask)
        layer = master.copy()
        layer.putalpha(alpha)
        layer.save(PACKAGE / "layers" / f"{name}.png")

    order = ["underfill", "hair_back", "legs", "robe_lower", "sleeve_left", "sleeve_right",
             "torso", "arm_left", "arm_right", "head", "head_ribbon"]
    preview = Image.new("RGBA", STAGE, (42, 48, 60, 255))
    for name in order:
        preview.alpha_composite(Image.open(PACKAGE / "layers" / f"{name}.png").convert("RGBA"))
    preview.save(PACKAGE / "preview" / "assembled_jianfinger_v3.png")
    master_preview = Image.new("RGBA", STAGE, (42, 48, 60, 255))
    master_preview.alpha_composite(master)
    master_preview.save(PACKAGE / "preview" / "art_master_solid.png")
    render_contact(master)

    data = make_bone_data(list(masks))
    runtime = PACKAGE / "runtime"
    filename = "附图剑修_jianfinger_spine"
    raw = json.dumps(data, ensure_ascii=False, separators=(",", ":"))
    (runtime / f"{filename}.json").write_text(raw, encoding="utf-8")
    (runtime / f"{filename}.spinejson").write_text(raw, encoding="utf-8")
    atlas: list[str] = []
    for name in masks:
        atlas.extend([f"../layers/{name}.png", f"size: {STAGE[0]},{STAGE[1]}", "format: RGBA8888",
                      "filter: Linear,Linear", "repeat: none", name, "  rotate: false", "  xy: 0, 0",
                      f"  size: {STAGE[0]},{STAGE[1]}", f"  orig: {STAGE[0]},{STAGE[1]}",
                      "  offset: 0, 0", "  index: -1", ""])
    (runtime / f"{filename}.atlas").write_text("\n".join(atlas), encoding="utf-8")
    readme = """# 附图剑修 Spine 四动作 v3（无剑剑指版）

## 本版姿势
角色不携带武器；左手并指向上作剑指，右手空手自然垂在袖下。补绘透明全身母图后再拆出图层，原始用户参考保留在 `source/`。

## 资源
- `source/attached_material_sheet.jpg`：用户提供的部件表。
- `source/assembled_character_reference.jpg`：用户提供的全身参考。
- `source/completed_character_art_master.png`：补全遮挡区域并按本版剑指姿势重绘的透明全身母图。
- `layers/`：头发、头部、发带、躯干、左右袖、左右手臂、裙摆、腿部与静态底图，共用 384×700 画布和原点。
- `preview/assembled_jianfinger_v3.png`：所有切片按静止姿势重新拼合。
- `preview/animation_contact_sheet.jpg`：四动作的姿势方向示意；动作运行数据以 Spine JSON 中的关键帧为准。

## 四个动作
- `idle`：小幅呼吸、头部和发带摆动，1.2 秒，建议设循环。
- `attack`：左手剑指向前快速发力后回收，0.58 秒，无剑。
- `hurt`：躯干受击摆动、双臂反应并回稳，0.55 秒。
- `death`：生命值归零后躯干下沉、手臂与衣袖松落，0.95 秒并保持。

## Spine 数据
- `runtime/附图剑修_jianfinger_spine.json` / `.spinejson` / `.atlas`：面向 Spine 3.8.75 的切片骨架与关键帧草稿。
- 尚未在 Spine 编辑器中导入、检查或另存 `.spine` 工程。此数据使用整画布区域附件；需要在编辑器内检查骨骼轴心、遮挡边缘，并按实际预览修正动作曲线。
- 图层是按完整母图的可见轮廓做的切片，动作幅度较大时可能出现静止底层残影或透明缺口；目前关键帧采用轻量位移，作为可编辑起点。
"""
    (PACKAGE / "README.md").write_text(readme, encoding="utf-8")
    print(PACKAGE)


if __name__ == "__main__":
    build()
