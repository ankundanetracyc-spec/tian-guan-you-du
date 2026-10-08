# 附图剑修 Spine 四动作 v3（无剑剑指版）

## 本版姿势
角色不携带武器；左手并指向上作剑指，右手空手自然垂在袖下。补绘透明全身母图后再拆出图层，原始用户参考保留在 `source/`。

## 资源
- `source/attached_material_sheet.jpg`：用户提供的部件表。
- `source/assembled_character_reference.jpg`：用户提供的全身参考。
- `source/completed_character_art_master.png`：补全遮挡区域并按本版剑指姿势重绘的透明全身母图。
- `layers/`：头发、头部、发带、躯干、左右袖、左右手臂、裙摆、腿部与静态底图，共用 384×700 画布和原点。
- `preview/assembled_jianfinger_v3.png`：所有切片按静止姿势重新拼合。
- `preview/animation_contact_sheet.jpg`：四动作的姿势方向示意；动作运行数据以 Spine JSON 中的关键帧为准。

## 四个动作
- `idle`：小幅呼吸、头部和发带摆动，1.2 秒，建议设循环。
- `attack`：左手剑指向前快速发力后回收，0.58 秒，无剑。
- `hurt`：躯干受击摆动、双臂反应并回稳，0.55 秒。
- `death`：生命值归零后躯干下沉、手臂与衣袖松落，0.95 秒并保持。

## Spine 数据
- `runtime/附图剑修_jianfinger_spine.json` / `.spinejson` / `.atlas`：面向 Spine 3.8.75 的切片骨架与关键帧草稿。
- 尚未在 Spine 编辑器中导入、检查或另存 `.spine` 工程。此数据使用整画布区域附件；需要在编辑器内检查骨骼轴心、遮挡边缘，并按实际预览修正动作曲线。
- 图层是按完整母图的可见轮廓做的切片，动作幅度较大时可能出现静止底层残影或透明缺口；目前关键帧采用轻量位移，作为可编辑起点。
