# 碧岚云叶袍战斗分件贴图适配

此目录供独立动作战斗试玩和运行时贴图替换接口使用。原版《鬼谷八荒》资源只用于骨架、网格、UV、裁切轮廓和静态布局对照；替换贴图的 RGB 来自项目自有设计的图像生成版本，不是对原版贴图改色。

## 自有素材来源

- 七个部件共用唯一一套战斗用图集 `bilan_battle_atlas_flat_v2.png`，参考项目现有碧岚云叶袍与 `bilan_puppet_parts_v2.png` 设计生成；色块改为低饱和玉绿、连续象牙内襟和少量大形云叶。正式服装图标不改。
- 衣身、两臂、外袖、头发和腿部从该战斗图集裁取颜色；各贴图最终沿原分件 alpha 轮廓裁定可见形状。
- 脸部保持无五官；肤色从项目 `textures/characters/side_ink_portrait_outfits_20261015_v3/male_idle.png` 的脸部区域采样。手部沿用同一肤色调。
- 原版 sprite PNG 只用于读取透明轮廓与建立布局预览；不取用其中的 RGB。

## 4×贴图与骨架接口

7 张唯一替换图均使用原 sprite 裁切画布宽高的 4 倍：原始 13–55×24–87 px 的分件对应 52–220×96–348 px。保持宽高比和 UV 不变；不要把 UV 再乘 4。原 alpha 值按 4×最近邻放大，保留原有轮廓和抗锯齿值。源 `uvCropLowerLeft` 名义上是 0–1 归一化坐标，但个别网格顶点略超出裁切边界，最大为 1.00404（腿），最小约 -1.1e-8；manifest 原样保存这些值，运行时需沿用纹理边界 clamp 行为。mesh、骨骼、权重、bindposes 与动画均未改。运行时建议用 Linear 过滤。

`manifest.json` 按 `sourceSpriteId` 提供 PNG、源逻辑尺寸、4×画布尺寸、renderer 路径与 ID、骨名、中心 pivot、`uvCropLowerLeft` 及默认启用状态。`own_layout.json` 另保留 `layout.json` 的根空间坐标、旋转和图层顺序，供静态配准预览使用。

sprite 61872 由左右腿 renderer 共用。默认可见的是 `/10002/Root/Body/Foot/zuotui/zuotui`（renderer 1150706）和 `/10002/Root/Body/Foot/youtui/youtui`（renderer 1147507）；`/10002/Root/Foot2/Foot/*` 的另两份 renderer（1139896、1116661）在 `activeInHierarchy=false`，保存在 manifest 作为备用信息，不应拿来替换默认画面。

## 文件

- `bilan_robe.png`：sprite 79450，39×87 → 156×348。
- `bilan_battle_atlas_flat_v2.png`：本项目单套简化云叶袍战斗图集，生成器的颜色来源。
- `bilan_left_arm.png`：sprite 78517，24×48 → 96×192。
- `bilan_right_arm.png`：sprite 89721，13×44 → 52×176。
- `bilan_sleeve_overlay.png`：sprite 71535，55×44 → 220×176。
- `bilan_hair.png`：sprite 56808，29×48 → 116×192。
- `bilan_featureless_face.png`：sprite 76416，24×24 → 96×96。
- `bilan_leg.png`：sprite 61872，19×73 → 76×292。
- `static_layout_alignment_preview.png`：原版与项目素材使用同一静态根空间锚点；未播放动画。
- `distant_readability_preview.png`：1280×512 战场远景对照，人物约 80 px 高，检查细身侧向比例和远景色块。
- `pixel_size_and_alpha_check.png`：逐部件显示原版轮廓参照与 4×项目贴图。
- `build_own_parts.py`：从本项目战斗图集和脸／手肤色源重新生成贴图、manifest 和预览。

## 已检查与待验证

- 使用 rig 导出的 `skins.json` 对应 renderer 路径、ID、mesh 是否蒙皮、骨名、pivot 与归一化 UV。除无骨骼权重的脸外，其余对应网格保留原骨骼和权重；静态锚点经完整父层级矩阵核对，Body/Foot 腿部最大差值 0.00049 px。
- 七张替换 PNG 均使用原 alpha 轮廓，静态预览保持相同锚点；80 px 远景图检查玉绿/象牙色分区与窄身侧影。
- `smoke.gd` 已确认原骨架数据、7个加权网格、26个骨骼绑定与贴图切换正常；Godot 窗口短时观测无错误或警告。
- 未逐帧验收所有六段动画、动态衣物接缝、边界 UV 采样和正式游戏接入；静态预览不等于完整动画验收。
