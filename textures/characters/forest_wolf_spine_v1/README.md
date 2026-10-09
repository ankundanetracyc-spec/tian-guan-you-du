# 林地凶狼 Spine 待机序列 v1

来源：`textures/battle/forest_wolf_idle_sheet_v2.png`（4×4、共 16 帧）。新版序列图把灰白棋盘格画进了像素；本资源按棋盘格颜色和连通轮廓去背，从每行识别四只完整妖兽，再统一脚底线与画布中心。

## 文件

- `runtime/forest_wolf_spine.spinejson`：Spine 3.8.75 格式运行数据。
- `runtime/forest_wolf_spine.atlas`、`runtime/forest_wolf_idle.png`：图集页与区域定义。
- `runtime/wolf_idle_00.png` 至 `wolf_idle_15.png`：逐帧透明切片，便于替换和检查。
- `preview/forest_wolf_idle_preview.gif`：8 FPS 循环预览。
- `preview/forest_wolf_idle_contact_sheet.png`：去背结果总览。

## 绑定说明

该资源保留了序列帧本身的动作，用一个 `sprite` 骨骼和一个附件槽按 8 FPS 切换 16 张全身图。原始素材没有头、躯干、四肢、尾巴的分层，所以这份数据不提供独立关节摆动；要改成可摆腿、摇尾的真骨骼，需要分层原画或补绘遮挡区域。

## 重建

在项目根目录运行 `python tools/build_forest_wolf_spine.py`。需要 Pillow、NumPy 和 OpenCV。
