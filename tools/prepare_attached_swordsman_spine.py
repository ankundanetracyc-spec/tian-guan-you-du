from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "mods" / "appearance" / "附图剑修_Spine四动作_v1"
SOURCE = Path(r"C:\Users\34784\AppData\Local\Temp\codex-clipboard-a9334bd8-6b00-42f2-bd8d-6909765ae5c6.jpg")
REFERENCE = Path(r"C:\Users\34784\AppData\Local\Temp\codex-clipboard-0ee5a9c9-7f0d-49e4-9deb-a3ec1c126d0d.jpg")
STAGE = (384, 700)
SCALE_X = 1.46


def key_checker(image: Image.Image) -> Image.Image:
    """Remove neutral checker pixels while preserving enclosed gray artwork."""
    rgba = image.convert("RGBA")
    pix = rgba.load()
    width, height = rgba.size
    gray = [[sum(pix[x, y][:3]) / 3 for x in range(width)] for y in range(height)]
    for y in range(height):
        for x in range(width):
            r, g, b, _ = pix[x, y]
            if max(r, g, b) - min(r, g, b) > 18:
                continue
            center = gray[y][x]
            if min(abs(center - 67), abs(center - 118)) > 38:
                continue
            checker_class = ((x // 8) + ((y + 4) // 8)) & 1
            expected = 118 if checker_class else 67
            if abs(center - expected) <= 32:
                pix[x, y] = (r, g, b, 0)
                continue
            samples = [
                gray[ny][nx]
                for ny in (y - 16, y - 8, y, y + 8, y + 16)
                for nx in (x - 16, x - 8, x, x + 8, x + 16)
                if 0 <= nx < width and 0 <= ny < height
            ]
            dark = [v for v in samples if v < 90]
            light = [v for v in samples if v > 100]
            if len(dark) >= 5 and len(light) >= 5 and len(dark) + len(light) >= 15:
                dark_mean = sum(dark) / len(dark)
                light_mean = sum(light) / len(light)
                if light_mean - dark_mean >= 24 and (
                    abs(center - dark_mean) <= 38 or abs(center - light_mean) <= 38
                ):
                    pix[x, y] = (r, g, b, 0)
    return rgba


def isolate_hair(image: Image.Image) -> Image.Image:
    """Use a hand-traced matte for the gray hair, which matches the checker tones."""
    rgba = image.convert("RGBA")
    mask = Image.new("L", rgba.size, 0)
    ImageDraw.Draw(mask).polygon(
        [(69, 12), (71, 7), (77, 3), (87, 1), (96, 3), (101, 7), (104, 13),
         (104, 18), (101, 22), (107, 26), (117, 29), (127, 34), (137, 43),
         (143, 53), (147, 63), (147, 70), (144, 78), (140, 83), (135, 87),
         (133, 92), (132, 99), (134, 108), (139, 119), (145, 132), (150, 144),
         (153, 153), (152, 160), (148, 166), (140, 172), (130, 176), (119, 179),
         (108, 181), (98, 181), (88, 179), (78, 176), (68, 172), (58, 168),
         (48, 163), (39, 157), (31, 151), (24, 145), (19, 139), (15, 134),
         (16, 130), (22, 125), (30, 120), (39, 115), (48, 110), (57, 105),
         (64, 100), (70, 95), (74, 90), (77, 85), (79, 80), (80, 74),
         (80, 68), (79, 62), (78, 55), (77, 48), (78, 42), (80, 36),
         (84, 31), (88, 28), (82, 26), (74, 23), (69, 18)],
        fill=255,
    )
    # Trim the hand-drawn contour just inside the outer ink line to remove
    # checkerboard pixels that the JPEG blended into its edge.
    mask = mask.filter(ImageFilter.MinFilter(11))
    mask_pixels = mask.load()
    rgba_pixels = rgba.load()
    gray = [[sum(rgba_pixels[x, y][:3]) / 3 for x in range(rgba.width)] for y in range(rgba.height)]
    for y in range(rgba.height):
        for x in range(rgba.width):
            if not mask_pixels[x, y]:
                continue
            r, g, b, _ = rgba_pixels[x, y]
            center = gray[y][x]
            if max(r, g, b) - min(r, g, b) > 18 or min(abs(center - 67), abs(center - 118)) > 38:
                continue
            samples = [
                gray[ny][nx]
                for ny in (y - 16, y - 8, y, y + 8, y + 16)
                for nx in (x - 16, x - 8, x, x + 8, x + 16)
                if 0 <= nx < rgba.width and 0 <= ny < rgba.height
            ]
            dark = [value for value in samples if value < 90]
            light = [value for value in samples if value > 100]
            if len(dark) >= 5 and len(light) >= 5 and len(dark) + len(light) >= 15:
                dark_mean = sum(dark) / len(dark)
                light_mean = sum(light) / len(light)
                if light_mean - dark_mean >= 24 and (
                    abs(center - dark_mean) <= 38 or abs(center - light_mean) <= 38
                ):
                    mask_pixels[x, y] = 0
    rgba.putalpha(mask)
    return rgba


def extract(sheet: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    return sheet.crop(box)


def place(canvas: Image.Image, piece: Image.Image, box: tuple[int, int, int, int]) -> None:
    x, y, w, h = box
    piece = piece.resize((w, h), Image.Resampling.LANCZOS)
    canvas.alpha_composite(piece, (x, y))


def stage_layer(sheet: Image.Image, items: list[tuple[tuple[int, int, int, int], tuple[int, int, int, int]]]) -> Image.Image:
    result = Image.new("RGBA", STAGE, (0, 0, 0, 0))
    for crop, target in items:
        place(result, extract(sheet, crop), target)
    return result


def widen(image: Image.Image) -> Image.Image:
    inverse = 1 / SCALE_X
    offset = STAGE[0] / 2 - STAGE[0] / 2 * inverse
    return image.transform(
        STAGE,
        Image.Transform.AFFINE,
        (inverse, 0, offset, 0, 1, 0),
        Image.Resampling.BICUBIC,
    )


def attachment(name: str, bone: str, x: float, y: float, width: int, height: int) -> dict[str, object]:
    return {"type": "region", "name": name, "path": name, "x": x, "y": y, "width": width, "height": height}


def keyframes(values: list[tuple[float, float]]) -> list[dict[str, float]]:
    return [{"time": t, "angle": a} if i else {"angle": a} for i, (t, a) in enumerate(values)]


def timeline(values: list[tuple[float, float]], field: str = "rotate") -> dict[str, list[dict[str, float]]]:
    return {field: keyframes(values)}


def build() -> None:
    for part in ("source", "layers", "runtime", "preview"):
        (PACKAGE / part).mkdir(parents=True, exist_ok=True)
    source_path = SOURCE if SOURCE.exists() else PACKAGE / "source" / "attached_material_sheet.jpg"
    reference_path = REFERENCE if REFERENCE.exists() else PACKAGE / "source" / "assembled_character_reference.jpg"
    if not source_path.exists():
        raise FileNotFoundError(source_path)
    if not reference_path.exists():
        raise FileNotFoundError(reference_path)
    source_copy = PACKAGE / "source" / "attached_material_sheet.jpg"
    reference_copy = PACKAGE / "source" / "assembled_character_reference.jpg"
    if source_path.resolve() != source_copy.resolve():
        shutil.copy2(source_path, source_copy)
    if reference_path.resolve() != reference_copy.resolve():
        shutil.copy2(reference_path, reference_copy)
    original_sheet = Image.open(source_path).convert("RGB")
    source = key_checker(original_sheet)
    source_hair = isolate_hair(original_sheet)
    source.save(PACKAGE / "source" / "attached_material_sheet_clean.png")

    # Coordinates are taken from the supplied sheet. The images are arranged
    # into one right-facing full-body composition on a shared ground line.
    parts: dict[str, Image.Image] = {
        "hair_back": stage_layer(source_hair, [((8, 2, 160, 188), (112, 6, 150, 183))]),
        "head": stage_layer(source, [((355, 19, 426, 108), (158, 30, 77, 103))]),
        "body": stage_layer(source, [((103, 231, 201, 421), (136, 151, 112, 218))]),
        "robe_lower": stage_layer(source, [((263, 733, 497, 1007), (100, 315, 184, 216))]),
        "legs": stage_layer(source, [
            ((18, 484, 78, 683), (151, 397, 52, 194)),
            ((88, 483, 143, 685), (205, 397, 50, 194)),
            ((19, 680, 75, 729), (144, 585, 58, 48)),
            ((102, 678, 164, 730), (208, 584, 64, 49)),
        ]),
        "arm_left": stage_layer(source, [
            ((47, 207, 101, 253), (116, 184, 52, 49)),
            ((14, 398, 48, 464), (123, 225, 36, 66)),
        ]),
        "arm_right": stage_layer(source, [
            ((199, 205, 255, 254), (222, 184, 55, 50)),
            ((255, 395, 290, 464), (257, 224, 36, 68)),
        ]),
        "sword": stage_layer(source, [((518, 214, 548, 443), (261, 171, 28, 216))]),
        "ribbon": stage_layer(source, [((13, 27, 113, 107), (112, 18, 110, 87))]),
    }

    # Match the wider silhouette in the full-body reference.
    parts = {name: widen(image) for name, image in parts.items()}

    # Keep independent transparent attachment pages for Spine 3.8.
    for name, image in parts.items():
        image.save(PACKAGE / "layers" / f"{name}.png")

    # Composite preview, ordered from rear to front.
    preview = Image.new("RGBA", STAGE, (0, 0, 0, 0))
    for name in ("legs", "robe_lower", "body", "hair_back", "ribbon", "arm_left", "arm_right", "sword", "head"):
        preview.alpha_composite(parts[name])
    checker = Image.new("RGBA", STAGE, (0, 0, 0, 0))
    d = ImageDraw.Draw(checker)
    for y in range(0, STAGE[1], 24):
        for x in range(0, STAGE[0], 24):
            if ((x // 24) + (y // 24)) % 2 == 0:
                d.rectangle((x, y, x + 23, y + 23), fill=(71, 71, 76, 255))
            else:
                d.rectangle((x, y, x + 23, y + 23), fill=(93, 93, 98, 255))
    checker.alpha_composite(preview)
    checker.save(PACKAGE / "preview" / "assembled_idle.png")
    solid = Image.new("RGBA", STAGE, (42, 48, 60, 255))
    for name in ("legs", "robe_lower", "body", "hair_back", "ribbon", "arm_left", "arm_right", "sword", "head"):
        solid.alpha_composite(parts[name])
    solid.save(PACKAGE / "preview" / "assembled_idle_solid.png")

    # Every page is a full-canvas region so each attachment shares the same
    # origin; bone placement then gives a simple, editable cutout rig.
    layer_names = list(parts)
    bone_parent = {
        "hips": "root", "torso": "root", "neck": "torso", "head": "neck",
        "hair_back": "head", "ribbon": "head", "arm_left": "torso",
        "arm_right": "torso", "sword": "arm_right", "legs": "hips",
        "robe_lower": "hips",
    }
    desired_screen = {
        "root": (192, 640), "hips": (192, 450), "torso": (192, 355),
        "neck": (192, 125), "head": (196.5, 81.5), "hair_back": (185, 120),
        "ribbon": (168, 94), "arm_left": (142, 231), "arm_right": (249, 231),
        "sword": (284, 251), "legs": (192, 495), "robe_lower": (192, 450),
    }
    desired_screen = {
        name: (round(192 + (x - 192) * SCALE_X, 2), y)
        for name, (x, y) in desired_screen.items()
    }
    bones = [{"name": "root"}]
    for name, parent in bone_parent.items():
        sx, sy = desired_screen[name]
        px, py = desired_screen[parent]
        bones.append({"name": name, "parent": parent, "x": sx - px, "y": py - sy, "length": 80})
    slots = [
        {"name": "legs_slot", "bone": "legs", "attachment": "legs"},
        {"name": "robe_lower_slot", "bone": "robe_lower", "attachment": "robe_lower"},
        {"name": "body_slot", "bone": "torso", "attachment": "body"},
        {"name": "hair_back_slot", "bone": "hair_back", "attachment": "hair_back"},
        {"name": "ribbon_slot", "bone": "ribbon", "attachment": "ribbon"},
        {"name": "arm_left_slot", "bone": "arm_left", "attachment": "arm_left"},
        {"name": "arm_right_slot", "bone": "arm_right", "attachment": "arm_right"},
        {"name": "sword_slot", "bone": "sword", "attachment": "sword"},
        {"name": "head_slot", "bone": "head", "attachment": "head"},
    ]
    # Region coordinates are relative to the pivot bone; the full stage is
    # centered at (192,350) and Spine's positive Y points upward.
    bone_screen = {"legs": desired_screen["legs"], "robe_lower": desired_screen["robe_lower"],
                   "body": desired_screen["torso"], "hair_back": desired_screen["hair_back"],
                   "ribbon": desired_screen["ribbon"], "arm_left": desired_screen["arm_left"],
                   "arm_right": desired_screen["arm_right"], "sword": desired_screen["sword"],
                   "head": desired_screen["head"]}
    attachments: dict[str, dict[str, dict[str, object]]] = {}
    for slot in slots:
        name = slot["attachment"]
        sx, sy = bone_screen[name]
        # attachment full-canvas center from pivot (local Spine coordinates)
        x = STAGE[0] / 2 - sx
        y = sy - STAGE[1] / 2
        attachments[slot["name"]] = {
            name: attachment(name, slot["bone"], x, y, STAGE[0], STAGE[1])
        }

    animations = {
        "idle": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.5, "x": 0, "y": 2}, {"time": 1, "x": 0, "y": 0}]},
            "torso": timeline([(0, 0), (0.5, 1.1), (1, 0)]),
            "head": timeline([(0, 0), (0.5, -1.5), (1, 0)]),
            "ribbon": timeline([(0, 0), (0.28, -7), (0.65, 4), (1, 0)]),
            "arm_left": timeline([(0, 0), (0.5, -1.8), (1, 0)]),
            "arm_right": timeline([(0, 0), (0.5, 1.6), (1, 0)]),
        }},
        "attack": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.18, "x": 5, "y": 0}, {"time": 0.42, "x": 10, "y": 0}, {"time": 0.82, "x": 0, "y": 0}]},
            "torso": timeline([(0, 0), (0.18, -5), (0.42, 7), (0.82, 0)]),
            "head": timeline([(0, 0), (0.18, -3), (0.42, 4), (0.82, 0)]),
            "arm_right": timeline([(0, 0), (0.18, -20), (0.42, 34), (0.60, 17), (0.82, 0)]),
            "sword": timeline([(0, 0), (0.18, -14), (0.42, 48), (0.62, 15), (0.82, 0)]),
            "arm_left": timeline([(0, 0), (0.18, 12), (0.42, -8), (0.82, 0)]),
            "ribbon": timeline([(0, 0), (0.18, -18), (0.42, 22), (0.82, 0)]),
        }},
        "hurt": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.09, "x": -8, "y": 0}, {"time": 0.25, "x": 4, "y": 0}, {"time": 0.58, "x": 0, "y": 0}]},
            "torso": timeline([(0, 0), (0.09, 10), (0.25, -4), (0.58, 0)]),
            "head": timeline([(0, 0), (0.09, 7), (0.25, -3), (0.58, 0)]),
            "arm_left": timeline([(0, 0), (0.09, 20), (0.25, -10), (0.58, 0)]),
            "arm_right": timeline([(0, 0), (0.09, -22), (0.25, 11), (0.58, 0)]),
            "sword": timeline([(0, 0), (0.09, -12), (0.25, 18), (0.58, 0)]),
            "ribbon": timeline([(0, 0), (0.09, 20), (0.25, -14), (0.58, 0)]),
        }},
        "death": {"bones": {
            "root": {"translate": [{"x": 0, "y": 0}, {"time": 0.55, "x": -8, "y": -34}, {"time": 1.05, "x": -11, "y": -58}]},
            "hips": timeline([(0, 0), (0.55, -11), (1.05, -22)]),
            "torso": timeline([(0, 0), (0.30, 8), (0.72, 28), (1.05, 46)]),
            "head": timeline([(0, 0), (0.30, 11), (0.72, 29), (1.05, 42)]),
            "arm_left": timeline([(0, 0), (0.35, -20), (0.74, -40), (1.05, -48)]),
            "arm_right": timeline([(0, 0), (0.35, 23), (0.74, 43), (1.05, 51)]),
            "sword": timeline([(0, 0), (0.32, -18), (0.70, -44), (1.05, -72)]),
            "legs": timeline([(0, 0), (0.55, -12), (1.05, -23)]),
            "robe_lower": timeline([(0, 0), (0.55, 7), (1.05, 13)]),
            "ribbon": timeline([(0, 0), (0.30, 16), (0.70, 28), (1.05, 36)]),
        }},
    }
    data = {
        "skeleton": {"hash": "attached-swordsman-spine-v1", "spine": "3.8.75", "x": -192, "y": -640, "width": STAGE[0], "height": STAGE[1], "images": "../layers/", "audio": ""},
        "bones": bones,
        "slots": slots,
        "skins": [{"name": "default", "attachments": attachments}],
        "animations": animations,
    }
    runtime = PACKAGE / "runtime"
    (runtime / "附图剑修_spine.json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    (runtime / "附图剑修_spine.spinejson").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    atlas_lines: list[str] = []
    for name in layer_names:
        atlas_lines += [f"../layers/{name}.png", f"size: {STAGE[0]},{STAGE[1]}", "format: RGBA8888", "filter: Linear,Linear", "repeat: none", name, "  rotate: false", "  xy: 0, 0", f"  size: {STAGE[0]},{STAGE[1]}", f"  orig: {STAGE[0]},{STAGE[1]}", "  offset: 0, 0", "  index: -1", ""]
    (runtime / "附图剑修_spine.atlas").write_text("\n".join(atlas_lines), encoding="utf-8")

    readme = """# 附图剑修 Spine 四动作 v1

来源是 `source/attached_material_sheet.jpg`；`source/assembled_character_reference.jpg` 用于对照造型与比例。棋盘格 JPG 经过局部纹理清理并按部件框拆分。当前包含 Spine 3.8.75 JSON、Atlas 和区域附件切片骨架；打开编辑器验证后再保存为 `.spine` 项目。

## 从《觅长生》本机安装包只读观察到的可借鉴点
- Unity 托管程序集引用 `spine-csharp` 与 `spine-unity`；游戏程序集元数据可见 `PlayerSpine`、`AvatarSkeleton`、`LoadSpine`、`SetSpine`、`AnimationState_Start/Complete` 等名称，适合借鉴“角色组件管理骨架数据、以回调处理动作开始/结束”的组织方式。
- 可见 UI 路径使用 `SkeletonGraphic`；运行时库含 `SkeletonAnimation`。因此本包采用固定骨骼、插槽和动作名，便于游戏角色组件按名字播放。
- 安装包只有编译后的 DLL 与 Unity 资源数据，没有可直接阅读的 C# 源文件；没有提取或复制游戏美术资源，也未读取存档。其状态机和具体动画参数无法由程序集名称确认。

## 文件
- `runtime/附图剑修_spine.json`、`.spinejson` 与 `.atlas`：Spine 3.8.75 骨架数据与图集。
- `layers/`：骨骼插槽使用的透明图层。
- `preview/assembled_idle.png`：棋盘底拼合预览；`assembled_idle_solid.png`：纯色底边缘检查预览。

## 动作
- `idle`：呼吸、头部与发带轻摆，1 秒；播放时设为循环。
- `attack`：前压、右臂挥剑并收势，0.82 秒。
- `hurt`：受击后仰并回稳，0.58 秒。
- `death`：生命值归零后下沉、躯干前倾、手臂与武器垂落并保持，1.05 秒。

## 质量边界
输入素材是压平棋盘格 JPG，自动拆分后头发外沿与少数部件边缘仍有零星压缩残点；头发使用手绘轮廓蒙版。骨架为区域切片，关节变形、攻击挥砍弧线和死亡落地姿势需要在编辑器里校正。当前是可继续编辑的动作草稿，不等同最终动画品质。
    """
    (PACKAGE / "README.md").write_text(readme, encoding="utf-8")
    for debug_preview in (PACKAGE / "preview").glob("*debug*.png"):
        debug_preview.unlink()
    print(PACKAGE)


if __name__ == "__main__":
    build()
