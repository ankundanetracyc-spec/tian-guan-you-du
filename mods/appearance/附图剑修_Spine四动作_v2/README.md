# 附图剑修 Spine 四动作 v2

v2 使用补绘后的透明全身稿作为统一画稿，再按 Spine 骨骼运动方向拆成同尺寸、同原点的完整透明 PNG。v1 原始拆分包保留作对照。

## 来源和重建
- `source/attached_material_sheet.jpg`：用户提供的原始部件表。
- `source/assembled_character_reference.jpg`：用户提供的全身造型参考。
- `source/completed_character_art_master.png`：按两张参考补齐被袖子、头发和裙摆遮住区域的完整透明画稿。
- `layers/`：从补绘画稿拆出的头发、头部、躯干、左右袖臂、剑、裙摆、双腿和头部发带图层。所有图层保留 384×700 画布和同一原点，重叠位置有意保留以避免关节露缝。

## 动作
- `idle`：呼吸、头部与发带轻摆，1 秒；播放时设为循环。
- `attack`：前压、右臂挥剑并收势，0.82 秒。
- `hurt`：受击后仰并回稳，0.58 秒。
- `death`：生命值归零后下沉、躯干前倾、手臂与武器垂落并保持，1.05 秒。

## Spine 文件
- `runtime/附图剑修_completed_spine.json`、`.spinejson`、`.atlas`：Spine 3.8.75 数据。
- 编辑器导入检查后应另存为 `.spine` 工程；四动作目前仍是切片骨架的关键帧草稿，细节变形与关节遮挡需要在 Spine 内复核。

## 预览和限制
- `preview/art_master_solid.png`：补绘后完整画稿。
- `preview/assembled_completed_v2.png`：拆层重新拼合预览。
- 衣料内侧、关节下的隐藏部分为基于参考重建的补绘，和原始素材的可见部分保持同一套颜色与纹样；动作大幅展开时仍要复查遮挡边界。
