# 当前水墨地物素材 · 2026-10-09

本批为独立生成的地图素材，已接入 `tscn/world/bai_chuan/dual_grid_v1/BaiChuanDualGridTerrainPrototype_v1.tscn` 的静态节点。鬼谷八荒原素材只用于解包研究，没有挂入本项目场景。场景尚未启动游戏验收。

当前树木为 `broadleaf_symbol_v1.png`、`pine_symbol_v2.png`、`willow_symbol_v1.png`，每个有独立树木的格子只放一棵，当前共 895 棵。山内画好的微型点景树属于整幅山景，不计为独立森林格。

## 当前山景（每幅一个场景节点）

| 文件 | 占地 | 位置（左上角） |
| --- | --- | --- |
| northwest_continuous_range_v1.png | 24×16 | (57,18) |
| southern_folded_range_v1.png | 24×14 | (116,153) |
| northeast_crag_group_v1.png | 18×12 | (140,24) |
| western_weathered_group_v1.png | 14×9 | (30,113) |
| southwest_saddle_group_v1.png | 16×12 | (23,141) |
| eastern_low_group_v1.png | 12×8 | (158,114) |

六幅均为独立的新构图，由内置 imagegen 生成透明 PNG，保留原始像素与透明通道；提示和来源见 `continuous_ranges_manifest.json`，固定落位见 `bespoke_range_placements.json`。不采用重复小山沿线路铺设的上一轮方案。

`mountain_symbol_v1.png`、`mountain_main_peak_v1.png`、`mountain_low_ridge_v1.png` 及旧 `scene_layout_detail.png` / `scene_layout_southwest.png` 为先前小山试稿，当前场景不使用。`pine_symbol_v1.png` 是树干较弯的第一稿，也不启用。

## 预览与记录

`bespoke_northwest_layout_preview.png`、`bespoke_southern_layout_preview.png`、`scene_layout_overview.png` 按当前静态地物坐标制作，属于地物层拼摆预览，不是游戏截图；水域仅示意逻辑格，未复现双网格水面材质、绿染、节点图示与迷雾。

`scene_layout_20261009.json` 记录当前地物节点的位置和尺寸。

早期一格一树的候选拼摆见 `one_tree_per_cell_preview.png`；树木生成提示见 `prompts.json`。早期独立样张和概念图不代表当前场景落位。
