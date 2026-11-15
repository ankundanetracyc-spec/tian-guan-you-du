# 符箓纸墨符笔新素材

2026-11-09。用户要求替换中央显得生硬的纸张素材，并为符纸、符墨、符箓及符笔生成背包图标。本轮已使用内置 image_gen 生成8张独立透明原画，接入原生三工艺、普通行囊、成品选择与鉴看，并统一两符的十份技能资源图标。

## 原生实机

| 页面 | 1280×720 | 1920×1080 |
| --- | --- | --- |
| 制符纸：新中央宣纸 | [查看](talisman_paper_ready_1280.png) | [查看](talisman_paper_ready_1920.png) |
| 研墨：水墨陶墨瓶 | [查看](talisman_ink_ready_1280.png) | [查看](talisman_ink_ready_1920.png) |
| 画符：朱砂符纸与毛笔 | [查看](talisman_drawing_ready_1280.png) | [查看](talisman_drawing_ready_1920.png) |
| 普通行囊：七种物品 | [查看](talisman_art_inventory_seven_products_1280.png) | [查看](talisman_art_inventory_seven_products_1920.png) |

截图为隔离验收人物，使用正式200格StoragePanel与真实物品控件。三工艺图中的纸墨来自实际制作；背包换图测试专门装入旧SVG定义的合规夹具，以证明既有物品刷新时不改能力、数量及摆放。首次人物就绪正常补入的穿戴衣着另有完整差异记录，不算换图赠送。

## 七张背包图标

| 符纸 | 符墨 | 护身符 | 炎矢符 | 青竹符笔 | 静毫符笔 | 藏锋符笔 |
| --- | --- | --- | --- | --- | --- | --- |
| ![符纸](../../../textures/ui/talisman_crafting_painted_20261109/paper.png) | ![符墨](../../../textures/ui/talisman_crafting_painted_20261109/ink.png) | ![护身符](../../../textures/ui/talisman_crafting_painted_20261109/hushen.png) | ![炎矢符](../../../textures/ui/talisman_crafting_painted_20261109/yanshi.png) | ![青竹符笔](../../../textures/ui/talisman_crafting_painted_20261109/brush_qingzhu.png) | ![静毫符笔](../../../textures/ui/talisman_crafting_painted_20261109/brush_jinghao.png) | ![藏锋符笔](../../../textures/ui/talisman_crafting_painted_20261109/brush_cangfeng.png) |

中央另有[高分辨率宣纸原画](../../../textures/ui/talisman_crafting_painted_20261109/scene_paper.png)。研墨与画符中央使用相应高分辨率原画；画符的笔图独立叠放，随选定笔款更新。

## 风格与文件

以项目现有瓷瓶、素面书册和松脂图为参考：水墨淡彩、自然材质本色、柔和体积与可见笔触。纸张改为薄软、带纤维和轻卷边的宣纸；墨采用矮陶瓶，避免与原高身丹瓶混同；两符以圆护纹、尖矢纹区别；三笔分别以青竹、深木细毫、暖竹丰毫区别。背景透明，不烘焙界面文字或边框。

所有正式素材位于 `textures/ui/talisman_crafting_painted_20261109/`。七张背包图按既有规范打包为每格128源像素：墨128×128，其余128×256。占地和物品能力保持。`raw/`保留七张原始生成PNG，中央宣纸原图单独保存；使用现有Godot打包方式，仅裁物外透明留白、等比缩小并置中，不拉伸、不重画、不放大。

[完整生成提示词、来源与SHA256](生成记录.json)记录每张独立内置生图任务、参考图、透明要求、原始输出位置及保存位置。八张项目原画与内置生成输出逐字节相同；[打包记录](图标尺寸与打包记录.json)给出原尺寸、透明主体范围及缩放结果。

## 接入与验证

`TalismanArtCatalog`统一纸、墨、两符和三笔的显示资源。新制物直接使用PNG；既有生成物首次加载、缓存命中及人物就绪恢复时派生更新图标。能力JSON、能力签名、物品路径、品阶、数量、位置、精力、灵石和制作记录不因换图重算。旧文件不必重新制作；缺少恢复字典但仍在库存中的旧产物同样刷新显示。

十份固定符箓技能资源仅替换icon_path，逐项剔除此行后的其余字节SHA256一致。费用、效果、目标、触发、符痕、词典及卡图效果文案保持。

- 项目编译0警告、0错误。
- 美术原生专项1280／1920各150项，共300项通过，包含七图密度与透明、三笔独立图、十份技能绑定、旧物就绪／缓存／恢复及普通行囊实际Texture、原能力与库存保持、新制纸墨绑定及中央PNG实际命中。报告：[1280](talisman_art_checks_1280.json) · [1920](talisman_art_checks_1920.json)。
- 三工艺真实制作与对齐检查两档各394项，共788项再次通过。报告：[1280](talisman_checks_1280.json) · [1920](talisman_checks_1920.json)。
- 上表三页及真实背包已逐张查看，原生截图使用实际物品控件加载新图标。

所有运行使用项目output中的隔离用户目录，未读取或更改用户旅程。现有《卸力》脚本UID按文本路径回退的加载提示保留。本轮无卡效或制作数值改动，原[制作候选参数](../../plans/百工技艺/制符/符箓三工艺候选参数与实装_20261109.md)与[战斗候选](../../plans/百工技艺/制符/符箓首轮战斗接入候选_20261109.md)继续适用。
