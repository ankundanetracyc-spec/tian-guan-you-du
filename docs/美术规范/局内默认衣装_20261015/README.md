# 局内男女默认衣装对应样片（2026-10-15）

状态：用户选定默认两套，男女待机／下劈共四张新人物图已接原游戏测试页；保留已认可的序列帧墨效。运行检查通过，衣装与发型的侧向转绘供评价。

同日后续：用户认可本页人物方向；新增[受击、防御与休整演出](../局内受击防御休整_20261015/README.md)沿用这两套衣装，并将手刀作为通用出手。当前操作和新增动图以上述记录为准。

## 本轮对应关系

| 局内人物 | 当前立绘衣装与保存序号 | 局内保留的识别特征 | 对应基础发型 |
| --- | --- | --- | --- |
| 男修 | 碧岚云叶袍，`appearance_clothes_style=0`，`male/clothing/01.png` | 深青绿外袍、象牙白交领、浅米云纹和叶枝、窄衣缘、同色布腰带 | 墨云中分＋墨发长披 |
| 女修 | 青瓷云梅袍，`appearance_clothes_style=0`，`female/clothing/01.png` | 象牙白主衣、蓝灰肩片／衣缘／腰带、蓝灰云纹和梅枝、少量浅色梅花 | 流云中分＋墨发长披 |

造型来源以活动立绘包 [appearance_parts.json](../../../mods/appearance/ink_three_variants_20261004/appearance_parts.json)及其男女第一套衣服、组合图为准，名称和编号遵守[立绘部件命名与局内衣着](../../plans/立绘部件命名与局内衣着_20261005.md)。半身立绘没有展示的裤、靴与下摆沿用既有局内样片的轮廓，再延续同套配色与纹样。

采用已有简化水墨动作样片的概括衣褶、粗墨轮廓与干笔明暗；云叶／云梅保留少量大形，供局内尺寸辨认。头部继续朝右、面部留白；基础长发以侧向重新绘制，取消上一版发髻与发带。下劈继续掌心朝镜头、拇指在外侧、四指并拢朝右下。

本轮为完整人物静帧的造型对应，尚未制作前后发／衣服拆件，也没有把正式角色的外观选择接到这个预览。两套各自保持待机与下劈一致，不新增存档字段或更改正式衣着编号。

## 画布、配准与墨效

四张运行图均为 **2048×2048 RGBA8**，共用 **(1024,1900)** 地面原点，待机标准高度约1375。两种姿势在每个性别内使用同一导出比例；原图落脚中点与脚底线人工登记到[配准清单](../../../textures/characters/side_ink_portrait_outfits_20261015_v3/export_manifest.json)，运行时不按各图边界自动居中或缩放。

保留[上一版16帧挥砍＋20帧命中墨效](../局内水墨序列帧_20261015/README.md)的颜色、纹理、泛光控制与时间轴，拉近和同步定格节奏保持。只按新下劈手指末端登记墨效落点：男修局部(685,-376)，女修(751,-397)，墨痕和命中同时平移，不改变大小或回放时序。人物／墨效随同一父节点镜像。

此前批准的人物V2与墨效文件哈希均保持，见[更换前记录](../../../textures/characters/side_ink_portrait_outfits_20261015_v3/source/change_baseline.json)。新运行素材另存V3。

## 游戏内查看

首页「试演与预览」→「局内动作姿势库预览」仍指向 [BattlePoseLibraryShowcase.tscn](../../../tscn/scene/BattlePoseLibraryShowcase.tscn)。使用「男／女」或Tab切换，「立绘对照」或P显示该人物对应立绘。左右键逐帧，Space暂停，其他重播、浅深背景、镜像、190／320高度、缺图回退继续可用。

| 男修 | 女修 |
| --- | --- |
| [立绘与待机对照](preview_male_reference.png) | [立绘与待机对照](preview_female_reference.png) |
| [实际运行动图](male_motion.gif) | [实际运行动图](female_motion.gif) |
| [下劈命中](preview_chop.png) | [下劈命中](preview_female.png) |

另有[男女浅深对照](preview_comparison.png)、[190高度](preview_190px.png)、[浅底下劈](preview_light.png)。动图直接捕获运行场景，960像素宽、30帧／秒采样，GIF颜色精度低于原生窗口。

## 素材来源与复现

- [四张运行图及清单](../../../textures/characters/side_ink_portrait_outfits_20261015_v3/preview_manifest.json)
- [衣装／发型与立绘来源对应](../../../textures/characters/side_ink_portrait_outfits_20261015_v3/wardrobe.json)
- [最终生成提示集](../../../textures/characters/side_ink_portrait_outfits_20261015_v3/generation_prompts.json)：使用内置 **image_gen.imagegen** 编辑旧动作样片，并提供活动立绘衣服、发型参考；四张原始生成PNG保存在该素材包source目录中。
- [导出工具](../../../tools/export_side_ink_preview.gd)：传入 `--art-root=res://textures/characters/side_ink_portrait_outfits_20261015_v3/`，按固定清单输出，再由Godot重新导入。

## 验证记录

[实际D3D12场景日志](verification.log)记录通过：四张尺寸／RGBA／画布外边缘、脚底线、按固定清单重建逐像素一致、待机与动作比例稳定、暂停恢复、真实空纹理及模拟缺图、镜像墨效、回位与190／320高度；上一版序列帧的帧数、透明首尾、有效帧区别和同步定格检查继续通过。另核对两套名称与立绘配置中保存值0的名称一致，旧人物和墨效素材哈希未改变。

本轮未接正式战斗的外观读取，也未改技能、伤害、目标、费用或存档。实际窗口仍有项目既存icon.ico缺失诊断；美术造型需用户评价。
