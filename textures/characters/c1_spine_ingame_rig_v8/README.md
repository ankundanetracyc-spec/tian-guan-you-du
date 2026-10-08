# C1 局内 Spine v8

这版以用户提供的角色素材表为动作补充来源，保留 v6/v7 不变。素材表先经过真实透明度清理，再作为 v8 的动作资源使用；v7 的已对齐基础层经过 alpha 硬化，去掉生成时的环境光晕。

## 资源

- `source/c1_material_sheet_source.jpg`：用户提供的原始素材表。
- `source/c1_material_sheet_clean_v1.png`：移除棋盘格后的透明素材表。
- `layers/`：运行时使用的透明图层；`lower_kneel.png` 是从素材表中的跪地下半身姿态提取并重新放置的动作附件，`lower_empty.png` 用于切换时隐藏站立下半身。
- `runtime/c1_desktop_layers_v8.spinejson`：项目当前 Godot Spine 3.8 运行时加载的 JSON。
- `runtime/c1_desktop_layers_v8.atlas`：按图层分页的 Atlas；每页与对应 PNG 一一对应。

## 骨骼与动画

沿用 v7 已验证的 `root / hips / torso / neck / head`、双臂、双腿和三段飘带骨骼，并增加 `slot_lower_kneel` 覆盖插槽。动画仍按运行时实时采样，不生成 60 张硬编码序列帧：

- `idle`：呼吸、重心和飘带轻摆，1.20 秒循环。
- `attack`：前冲、抬臂和收势，0.90 秒。
- `hurt`：受击后仰与回弹，0.62 秒。
- `death_kneel`：先收身，再在 0.30 秒切换素材表中的真实跪地下半身姿态，最终保持跪地，1.10 秒。

## 接入

- 战斗角色：`csharp/battle/BattlePaperDoll.cs`。
- 生命值清零：`csharp/entity/WorldEntity.cs` 的 `play_death_animation()` 已为 Spine 人形保留到动作完成后再移除。
- 序列帧展示：`csharp/scene/CharacterSequenceShowcase.cs`；A=攻击、H=受击、D=跪地死亡、空格=暂停、左右键按 60 FPS 取样。

## 生成与验证

- `tools/extract_c1_material_v8.py`：清理用户素材表并生成透明动作层。
- `tools/build_c1_spine_layers_v8.py`：生成 Spine 3.8.99 JSON/Atlas。
- `tmp/C1SpineV8LayerLoad.gd`：无头资源、动作和跪地附件时间点检查。
