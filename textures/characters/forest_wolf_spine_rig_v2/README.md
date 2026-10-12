# 林地凶狼 Spine 骨骼 v2

本版使用已确认的战斗视角候选：轻俯视的三分之四侧视，朝左，近侧爪更大更低，远侧爪更小并落在更靠后的地面位置。角色只有两条前腿和两条后腿，四条腿分别绑定。

## 文件

- `source/wolf_rig_master.png`：透明底角色原图。
- `runtime/forest_wolf_spine_rig_v2.spinejson`：Godot Spine 3.8 运行时资源。
- `runtime/forest_wolf_spine_rig_v2.atlas`：运行时图集。
- `runtime/forest_wolf_spine_rig_v2.json`：同一骨架 JSON，便于外部检查和导入。
- `preview/bone_layout.svg`：骨骼锚点与四肢分链示意。
- `tools/build_forest_wolf_spine_rig_v2.py`：重建网格、骨骼、动作和图集。

骨架使用 20 根骨骼，原图的加权网格按渲染容量分成 12 个无缝网格块；不按帧切图。每块最多 399 个顶点、2,160 个索引，避开当前运行器单块 1,024 顶点／3,072 索引的容量限制。四条腿各自拥有上段、膝/踝和爪骨，头颈、躯干、后胯与三段尾骨单独驱动。

## 动画

- `idle`：1.2 秒循环呼吸、重心起伏和尾巴摆动。
- `attack`：0.82 秒向目标扑击，前爪探出，后腿蹬地，随后回到待机。
- `hurt`：0.5 秒后缩、抬头并甩尾。
- `death`：1.25 秒下沉屈腿并垂尾，最终姿势保持到战斗移除角色。

战斗接入位于 `csharp/battle/BattlePaperDoll.cs`；狼的倒地动画播放完成后才开始淡出。
