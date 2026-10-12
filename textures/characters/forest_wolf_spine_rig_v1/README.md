# 林地凶狼 Spine 骨骼素材 v1（已弃用）

> 该目录对应先前的侧视候选，已停止接入战斗。当前采用地图透视版 [`forest_wolf_spine_rig_v2`](../forest_wolf_spine_rig_v2/)。

这套资源以 `source/wolf_rig_master.png` 为绘制底图，在 Spine 3.8 网格上绑定 19 根骨骼权重。`runtime/forest_wolf_spine_rig_v1.spinejson` 由 `tools/build_forest_wolf_spine_rig.py` 生成；atlas 直接引用同一张透明 PNG。角色是单张连续网格，通过头颈、躯干、四肢与尾部的骨骼权重变形，不使用逐帧切图。

前肢按近、远侧分别绑定：带云纹的近侧前腿与右侧浅色远侧前腿各有独立的上肢、膝和爪骨；后肢也分为近、远侧两条骨链。`preview/bone_layout.png` 标明骨架位置，`preview/animation_poses.png` 展示四种动作。

动画：

- `idle`：呼吸、轻微重心变化与尾巴摆动，循环。
- `attack`：向目标冲扑并挥动前肢，播放一次。
- `hurt`：受击后短促后缩并抬头，播放一次。
- `death`：下沉、屈腿并垂下尾巴，结束后保持倒地姿态。

重新生成运行时文件：

```powershell
py -3 tools/build_forest_wolf_spine_rig.py
```
