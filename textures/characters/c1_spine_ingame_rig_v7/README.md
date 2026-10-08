# C1 局内 Spine v7

这是一套以桌面版 Spine 3.8.75 工作流为目标、按项目当前 Godot Spine 3.8 运行时导出的局内角色资源。v6 保持不变。

## 层与绘制顺序

1. `lower_body.png`：下半身、裤腿和鞋
2. `body_core.png`：躯干、长袍和衣袖主体
3. `ribbons.png`：头部飘带
4. `arm_waist.png`：腰侧手臂
5. `arm_raised.png`：抬起手臂
6. `head_hair.png`：头部与整组头发/发饰

六张图均为独立透明画布，保留完整画布尺寸，便于后续替换、补画和重新布权；并非从 v6 裁切出来的单图。

## 骨骼与动画

运行时文件：`runtime/c1_desktop_layers_v7.spinejson` 与同名 `.atlas`。

骨骼包含 `root / hips / torso / neck / head`，双臂的上臂与前臂，下半身左右骨骼，以及 `ribbon_base / ribbon_mid / ribbon_tip` 三段飘带骨骼。网格采用透明层的网格附件和权重影响，飘带先用三段骨骼动画驱动，后续可在桌面 Spine 中改为物理约束。

动画名称：`idle`、`attack`、`hurt`、`death_kneel`。游戏内按 60 FPS 取样显示，不把动画硬编码成 60 张图片。

## 接入位置

- 战斗角色：`csharp/battle/BattlePaperDoll.cs`
- 角色序列帧展示：`csharp/scene/CharacterSequenceShowcase.cs`
- 可重复生成运行时 JSON/Atlas：`tools/build_c1_spine_layers_v7.py`

当前项目运行时加载的是 `.spinejson` 副本；它与 `.json` 内容相同，但项目的 Spine 导入器按此扩展名处理资源。
