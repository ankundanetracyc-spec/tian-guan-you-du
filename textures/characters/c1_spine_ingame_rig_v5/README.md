# C1 局内角色 Spine 素材 v5

v5 收敛为六层，优先保证头身比例、颈部遮挡和整体连接稳定。头发不再拆成前发、后发、发饰；完整头部与全部头发由一张独立生成的透明素材负责。五官仍分别保留，供后续有限捏脸使用。

## 六层结构

1. `parts/01_body_full.png`：身体整体，包含衣服、双臂、手、裤子、鞋子和加长脖子。脖子向上完整延伸，由头部整体覆盖上段。
2. `parts/02_head_hair_full.png`：脸型、耳朵、全部头发、丸子、发带、两根飘带；不含眉毛、眼睛、鼻子、嘴巴和脖子。头部底部中心对接身体脖子顶部，作为颈椎锚点。
3. `parts/03_brows.png`：眉毛。
4. `parts/04_eyes.png`：眼睛。
5. `parts/05_nose.png`：鼻子。
6. `parts/06_mouth.png`：嘴巴。

头部和发饰不再出现层间错位；头发整体可以随 `head_hair` 骨骼旋转和摆动。由于它是一张整体素材，当前不支持单独替换前发、后发或让两根飘带分别物理摆动。v4 的独立发片保留在旧包中，仅作为后续扩展参考。

## Spine 绑定

- 工程：`spine/c1_spine_unified_final.spine`
- 导入描述：`spine/c1_spine_unified_import.json`
- JSON 导出：`export/final/c1_spine_unified_import.json`
- 静态叠合预览：`previews/rest_pose_composite.png`
- 图层检查：`previews/parts_contact_sheet.png`
- 原始透明参考：`source/c1_base_clear_no_scroll_transparent_v7.png`

骨骼为 `root / hips / torso / neck / head_hair`，眉眼鼻嘴作为 `head_hair` 子骨骼；基础动作保留 `idle`、`attack`、`hurt`、`death_kneel`。
