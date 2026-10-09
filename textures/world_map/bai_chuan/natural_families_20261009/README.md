# 百川自然地物族补全

本批新增二十张，与 `natural_scenery_20261009_v2/` 中已确认的二十张共同组成十八个素材族、每族两至三款的四十张素材库。配套暖米黄地表继续沿用。

类别为高峰、小山低脊、阔叶树、松树、柳树、竹丛、枯木、散石、岩台、树石、低灌木、岸景、浅溪、荷叶、泉石、小潭、短崖、洞口。此阶段为百川自然地物，不含草丛及雪地/沙漠/火山等其他生态。

所有新增素材使用内置 imagegen 独立生成，原样保留 PNG 与透明通道。完整提示见 `prompts.json`，生成来源见 `generation_sources.json`；全库选用索引位于 `resources/world/bai_chuan/natural_asset_catalog.json`。

`family_contact_sheet.png` 为十八族总览，展示缩放不代表占格比例。生成布局与用法见 `docs/plans/百川模板随机地物生成_20261009.md`。

已将种子 20261009、密度 1.0 的模板示例保存进原地图场景。新增素材为内置 imagegen 生成，二十张旧素材继续选用。`template_map_overview.png`、`template_northwest_region.png`、`template_woodland_detail.png` 为保存地物的合成示意，省略固定遗迹、节点、绿染、玩家与迷雾，不是引擎截图；原始 PNG 未改动。本轮未启动游戏或运行测试。

当前布置算法为 `regional_center_surround_edge_v2`，参考本机游戏随附区域配置的中心、周围、边缘结构和随机留空命令，采用本项目原创密度场与种子算法。增加 `template_mountain_detail.png` 山地局部预览，旧六块矩形分组方式已替换，完整四十款均已使用。
