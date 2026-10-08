# C1 局内角色 Spine 素材 v4

这是按模块化角色结构重新生成的清晰边界版素材包。每个图层都是独立生成的透明 PNG，再统一到同一张 720×960 画布；没有从整张角色图裁切出层，也没有把旧飘带裁成假素材。

## 固定九层

1. `parts/01_body_full.png`：身体整体，包含脖子、衣服、双臂、空手、裤子和鞋子。
2. `parts/02_head.png`：脸型和耳朵，不含脖子和五官；头部底部中心对接身体脖子顶部，作为颈椎旋转锚点。
3. `parts/03_brows.png`：眉毛独立层。
4. `parts/04_eyes.png`：眼睛独立层。
5. `parts/05_nose.png`：鼻子独立层。
6. `parts/06_mouth.png`：嘴巴独立层。
7. `parts/07_hair_front.png`：只负责额头和刘海，不含头顶发套、后发或发饰。
8. `parts/08_hair_back.png`：头顶后侧、侧后发和背后长发，不含丸子和发带。
9. `parts/09_hair_accessory.png`：丸子、青色发带和两根飘带作为一个整体，不再拆出单独飘带层。

五官四层保留独立替换入口，后续可在不改身体和头部骨骼的情况下替换脸型、眉眼、鼻子和嘴巴，支持有限度的捏脸组合。

## Spine 绑定

- 工程：`spine/c1_spine_unified_final.spine`
- 导入描述：`spine/c1_spine_unified_import.json`
- JSON 导出：`export/final/c1_spine_unified_import.json`
- 静态叠合预览：`previews/rest_pose_composite.png`
- 图层检查：`previews/parts_contact_sheet.png`
- 原始透明参考：`source/c1_base_clear_no_scroll_transparent_v7.png`

骨骼包含 `root / hips / torso / neck / head` 身体层级，以及后发、前发、眉毛、眼睛、鼻子、嘴巴和合并发饰控制骨。保留 `idle`、`attack`、`hurt`、`death_kneel` 四个基础动作。当前发饰是一根整体骨，动作中只做整体轻摆；如果以后要让两根飘带分别受物理约束，需要重新把它们做成网格内部骨骼或重新拆层。
