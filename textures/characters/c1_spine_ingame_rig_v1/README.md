# C1 局内角色 Spine 素材（重新生成版）

这套素材是按当前确认的模块化方案重新生成的，不从旧角色图裁切，也不把旧图像素拼接到新素材里。

## 结构

- `01_body_full.png`：身体整体，包含头以下的衣服、袖子、手、卷轴、裤子和鞋子。
- `02_hair_back.png`：后发与发髻主体。
- `03_head.png`：头部皮肤、耳朵和颈部，不含五官。
- `04_brows.png`：眉毛独立层。
- `05_eyes.png`：眼睛独立层。
- `06_nose.png`：鼻子独立层。
- `07_mouth.png`：嘴巴独立层。
- `08_hair_front.png`：前发与侧边碎发。
- `09_hair_accessory.png`：发饰主体，不含长飘带。
- `10_ribbon_tail_a.png`、`11_ribbon_tail_b.png`：两根独立飘带。

所有最终部件使用同一张 720×960 RGBA 画布和同一坐标原点，身体不会因为拆层而产生袖子、衣摆或手部的遮盖断裂。

## Spine 工程

- 工程：`spine/c1_spine_unified_final.spine`
- 可复用导入描述：`spine/c1_spine_unified_import.json`
- 导出 JSON：`export/final/c1_spine_unified_import.json`
- 静态合成预览：`previews/rest_pose_composite.png`
- 部件检查图：`previews/parts_contact_sheet.png`

骨骼包含：

```text
root
└─ hips
   └─ torso
      └─ neck
         └─ head
            ├─ hair_back
            ├─ hair_front
            ├─ brows
            ├─ eyes
            ├─ nose
            ├─ mouth
            ├─ hair_accessory
            ├─ ribbon_a
            │  └─ ribbon_a_tip
            └─ ribbon_b
               └─ ribbon_b_tip
```

眉毛、眼睛、鼻子、嘴巴现在可以分别做位置、旋转和缩放调整，满足基础捏脸；如果后续需要左右眉、左右眼的非对称捏脸，再把对应层继续拆成左右插槽即可，不需要重做身体。

已写入的基础动作：`idle`、`attack`、`hurt`、`death_kneel`。

## 飘带与物理

当前桌面 Spine 是 3.8.75。工程已经为两根飘带建立了独立的主骨与末端骨，并在基础动作里加入了可编辑的摆动关键帧，因此可以直接继续调动作。

Spine 3.8 工程本身不包含新版 Physics Constraint；若升级到支持物理约束的 Spine 4.2+ 编辑器和对应运行库，再给 `ribbon_a`、`ribbon_a_tip`、`ribbon_b`、`ribbon_b_tip` 接入 Physics Constraint，不需要重新生成素材或改变分层结构。
