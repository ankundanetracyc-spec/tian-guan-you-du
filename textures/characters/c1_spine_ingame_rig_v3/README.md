# C1 局内角色 Spine 素材 v3

这是基于 `c1_base_reference_v7.png` 重新生成并拆分的清晰边界版角色素材：透明背景、无竹简、非水墨渲染。各部件由图像生成分别重绘，不使用整图裁切作为最终层。

## 部件

- `01_body_full.png`：完整身体，包含身体脖子、衣服、双臂、空手、裤子和鞋子。
- `02_hair_back.png`：后发、发髻和背后长发，不含前发。
- `03_head.png`：脸型和耳朵，不含脖子和五官；下颌中心用于对接身体脖子顶部。
- `04_brows.png`：眉毛。
- `05_eyes.png`：眼睛。
- `06_nose.png`：鼻子。
- `07_mouth.png`：嘴巴。
- `08_hair_front.png`：前发和脸侧短发。
- `09_hair_accessory.png`：青色发带/发饰，不含头发。
- `10_ribbon_tail_a.png`、`11_ribbon_tail_b.png`：两根独立飘带。

所有最终图层统一为 720×960 RGBA，并使用同一坐标原点。身体是一张完整图，头部不带脖子，五官按眉毛、眼睛、鼻子、嘴巴四层拆分，方便后续替换脸型和做基础捏脸。

## 工程与预览

- Spine 工程：`spine/c1_spine_unified_final.spine`
- Spine 导入描述：`spine/c1_spine_unified_import.json`
- JSON 导出：`export/final/c1_spine_unified_import.json`
- 静态合成：`previews/rest_pose_composite.png`
- 部件检查：`previews/parts_contact_sheet.png`
- 原始生成参考：`source/c1_base_reference_v7.png`

骨骼包括身体层级、头部与四层五官控制骨，以及两根飘带的主骨和末端骨。基础动作保留：`idle`、`attack`、`hurt`、`death_kneel`。

当前桌面 Spine 为 3.8.75，飘带使用独立骨骼和关键帧摆动；升级到 Spine 4.2+ 编辑器与运行库后，可在相同分层上接入 Physics Constraint。
