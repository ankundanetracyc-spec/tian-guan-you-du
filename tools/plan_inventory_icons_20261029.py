"""Produce the authoritative icon/footprint manifest; does not modify item resources."""
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/美术规范/行囊全物品图标与占地_20261029"
ASSET_ROOT = "res://textures/items/inventory_density_20261029/"

MATERIAL_SIZES = {
    "dark_feather": (2, 3), "cloud_mist_mushroom": (3, 3), "earthbeast_sinew": (2, 4),
    "beast_hide": (3, 4), "beast_bone": (2, 5), "frostdew_leaf": (2, 3),
    "dew_pine_resin": (2, 2), "return_spring_pearl": (2, 2), "geovein_flame_coal": (3, 3),
    "mountain_silkworm_thread": (2, 2), "cicada_shell": (2, 3), "mottled_beetle_shell": (3, 3),
    "broken_horn_bone": (2, 3), "moon_dew_moss": (2, 2), "moonmist_mushroom_juice": (2, 3),
    "redsun_flower_seed": (2, 2), "pineheart_charcoal": (2, 3), "sinking_cloud_jade_shard": (2, 2),
    "clay_vein_lump": (3, 3), "clear_chime_jade_shard": (2, 3), "streampattern_algae_fiber": (2, 4),
    "spirit_spring_algae": (2, 3), "spirit_spring_dew": (2, 3), "spirit_stone": (1, 1),
    "darkscale_thin_plate": (2, 3), "xuantie": (3, 3), "miasma_clay_pearl": (2, 2),
    "salt_gypsum": (3, 3), "stone_bee_wax": (2, 2), "sandvein_stone_fragment": (2, 3),
    "gravel_mushroom": (3, 3), "zisha_spirit_soil": (2, 2), "bitter_moss_root": (2, 4),
    "withered_vine_knot": (2, 3), "cracked_armor_stone": (3, 3), "brown_copper_sand": (2, 2),
    "redwood_seed_core": (2, 3), "chiwen_suilishi": (3, 3), "red_scale_shed": (2, 3),
    "tong_sui_ore": (3, 3), "mistheart_mushroom_core": (2, 2), "mist_bell_flower": (2, 4),
    "frostleaf_twig": (2, 5), "qinglan_vine_core": (2, 5), "qingmu_core": (2, 5),
    "bluegreen_feather": (2, 4), "qingmarrow_sprout": (2, 3), "windaperture_crystal_shard": (2, 2),
    "fengwen_quartz": (3, 3), "windvein_vine_fiber": (2, 4),
}
REUSE = {
    "mat_spirit_stone": "spirit_stone_1x1.png",
    "mat_xuantie": "xuantie_3x3.png",
    "mat_bitter_moss_root": "bitter_moss_root_2x4.png",
    "sect_sword_suxing": "slender_sword_2x6.png",
    "sect_artifact_qinghe": "slender_sword_2x6.png",
}
CLASSES = {"ItemData", "EquipmentData", "PillItemData", "PillContainerItemData", "PortraitClothingData", "SectArtifactOfferData"}
BASE = (OUT / '统一画风提示词.txt').read_text(encoding='utf-8').strip() + ' '


def parse():
    rows = []
    for path in sorted((ROOT / "resources").rglob("*.tres")):
        content = path.read_text(encoding="utf-8")
        cls = re.search(r'script_class="([^"]+)"', content)
        if not cls or cls[1] not in CLASSES:
            continue
        def val(key):
            match = re.search(r'^' + key + r'\s*=\s*&?"([^"]*)"', content, re.M)
            return match[1] if match else ""
        if not val("icon_path"):
            continue
        rows.append({"path": path.relative_to(ROOT).as_posix(), "id": val("definition_id"),
            "name": val("entity_name"), "category": val("item_category"), "subcategory": val("item_subcategory"),
            "old_icon": val("icon_path"), "script": cls[1]})
    return rows


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rows, jobs = parse(), {}
    for index, row in enumerate(rows):
        ident = row["id"]
        key = ident
        if ident.startswith("mat_"):
            size = MATERIAL_SIZES[ident[4:]]
            subject = "Recompose the reference material " + row["name"] + " (" + ident[4:].replace("_", " ") + "). Preserve its recognizable material, colors and distinguishing marks. "
            subject += "Keep the physical specimen compact and upright, without sprawling thin tips. "
            if any(word in ident for word in ("juice", "spring_dew")):
                subject += "One small plain ceramic vial with stopper, representing one existing recipe dose; no extra containers. "
            if any(word in ident for word in ("sand", "soil", "seed", "thread", "fiber", "moss")):
                subject += "A small cohesive portion or neat bundle of the existing material, not a bulk sack. "
        elif row["category"] == "秘籍":
            size = (2, 3)
            key = "manual_generic_2x3"
            subject = ("A universal plain Chinese stitched book used for ALL cultivation manuals. "
                "Upright portrait, simple left stitched binding, warm ivory paper edges. "
                "Simple muted blue-grey or warm-grey cloth cover, no label or lettering. "
                "Remove all mountain paintings, golden cloud rivers, fancy borders, gilding and decorative motifs. "
                "Plain practical scholarly book, fine tactile cloth and paper brush texture, gently worn edges. "
                "No lettering, no diagonal tilt. ")
        elif row["script"] == "PortraitClothingData":
            size = (3, 4)
            subject = ("Recompose the reference robe " + row["name"] + " as one neatly FOLDED garment, preserving its fabric colors, trim and embroidery identity. "
                "No person, mannequin, hanger or body parts. Collar, crossed front and folded sleeves readable. Top-down, compact portrait silhouette. ")
        elif "sword" in ident or ident == "sect_artifact_qinghe":
            wide = ident in ("sect_sword_hanzhang", "sect_sword_chenglu")
            size = (3, 6) if wide else (2, 6)
            key = {"sect_sword_hanzhang": "spined_sword_3x6", "sect_sword_chenglu": "broad_sword_3x6"}.get(ident, ident)
            subject = ("Recompose reference sword " + row["name"] + " fully VERTICAL, tip UP, grip DOWN. Preserve recognizable blade, guard and pommel. "
                "Broad blade and crossguard" if wide else "Slender blade and jade-bronze crossguard")
            subject += ". Orthographic front view; no diagonal tilt, scabbard or perspective foreshortening. Entire sword inside canvas. "
        elif row["category"] == "丹药" or row["script"] == "PillContainerItemData":
            size = (2, 3)
            key = "pill_container_empty_porcelain"
            subject = ("One universal small ivory ceramic medicine vial with wooden stopper. "
                "All inventory medicines use this same porcelain bottle silhouette, not a naked pill. "
                "Preserve the reference bottle identity, pale glazed ceramic, restrained blue-grey neck accent. "
                "Simple, upright, complete, no label text, no painted medicine names and no visible contents. ")
        else:
            raise ValueError("Unplanned item: " + ident)
        filename = REUSE.get(ident, key + ".png")
        if row["category"] == "丹药" and key == "pill_2x2":
            filename = "pill_2x2.png"
        w, h = size
        row.update(width=w, height=h, area=w*h, footprint="rectangle", can_rotate=True,
            silhouette="方形" if w == h else ("长条" if h >= w*2 else "竖长矩形"),
            new_icon=ASSET_ROOT + filename, pixels_w=w*128, pixels_h=h*128)
        if filename not in jobs:
            ratio = f"{w}:{h}"
            reference = row["old_icon"]
            if row["category"] == "丹药":
                reference = "res://textures/items/alchemy_20261024/porcelain_bottle_closed.png"
            elif row["category"] == "秘籍":
                reference = ASSET_ROOT + "manual_2x3.png"
            jobs[filename] = {"key": key, "file": filename, "name": row["name"], "cells": [w,h],
                "ref": str(ROOT / reference.removeprefix("res://")),
                "style_reference": str(OUT / "风格参考_用户指定瓷瓶.png"),
                "prompt": BASE + subject + f"Canvas aspect {ratio}, intended packed size {w*128}x{h*128}, footprint {w}x{h}. "
                    "Compose the item to use approximately 85-90 percent of the relevant canvas dimensions without stretching."}
    (OUT / "物品占地与图标清单.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    (OUT / "生成任务.json").write_text(json.dumps(list(jobs.values()), ensure_ascii=False, indent=2), encoding="utf-8")
    table = "# 全物品图标与占地规划\n\n2026-10-29。用户授权：当前所有带图标物品全部替换并规定形状与格数。\n\n"
    table += "逐项登记，不只检查独立图片。占地先用实心矩形，方形、长条和竖长矩形是其长宽分类；图片轮廓不改变可占格区域。允许90度旋转。每格128源像素；不改变物品数值、来源、配方、堆叠或穿戴规则。\n\n"
    table += "用户最新裁定：所有秘籍共用一张朴素通用书册图（2×3），不加复杂花纹；所有丹药的行囊图标共用瓷瓶图（2×3），裸药丸图只用于动画，不按丹种或丹效档生成行囊药丸图。瓷瓶图不另定义装药数量、堆叠或消耗规则。清和剑和修长剑型共用新剑图，衣物按折叠形态3×4；灵石特殊1×1，其余普通小件从2×2起。\n\n"
    table += f"本轮共{len(rows)}份物品定义、{len(jobs)}份目标图。生成、替换与实机验收状态见[制作记录](../美术规范/行囊全物品图标与占地_20261029/README.md)。\n\n"
    table += "| 物品 | 分类 | 占地外形 | 宽×高 | 格数 | 源图像素 |\n| --- | --- | --- | --- | --- | --- |\n"
    for row in rows:
        table += f"| {row['name']} | {row['category']} | {row['silhouette']} | {row['width']}×{row['height']} | {row['area']} | {row['pixels_w']}×{row['pixels_h']} |\n"
    (ROOT / "docs/plans/行囊全物品图标与占地规划_20261029.md").write_text(table, encoding="utf-8")
    # The audited unified batch supersedes the historical subject/style fragments above.
    # Always restore its clean prompts after rebuilding the footprint table.
    if (OUT / "物品形态语义审计.json").exists():
        subprocess.run([sys.executable, str(ROOT / 'tools/prepare_unified_inventory_prompts_20261029.py')], check=True)
    print(json.dumps({"items":len(rows),"jobs":len(jobs),"pending":sum(not (ROOT / "textures/items/inventory_density_20261029/raw_unified_final" / j["file"]).exists() for j in jobs.values())}))


if __name__ == "__main__":
    main()
