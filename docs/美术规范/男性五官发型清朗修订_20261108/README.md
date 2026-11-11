# 男性五官与发型：清朗修订预览

日期：2026-11-08。状态：用户反馈本组六张肤色、画风与项目现有素材不符，未获认可，未拆件、未实装。本页保留旧稿记录，当前修订转至[男性肤色与画风对齐修订](../男性肤色画风对齐修订_20261108/README.md)。

下文内部对照、推荐与文件核对仅记录当时制作过程，不表示用户已认可画风或批准实装。

用户对上一组六张反馈“有点丑，不要太丑的”。本轮按此修正：保留男性骨感与英气，允许端正、清朗、协调的美感；不再把此前“不太俊美”解释为粗眉、窄眼、宽鼻和厚唇同时加重。中年形象继续暂停，已实装的六款面饰沿用原状。

## 六张候选

| 编号 | 名称 | 发型与脸部方向 | 原生整稿 |
| --- | --- | --- | --- |
| 01 | 清正·后收束发 | 露额后收、后颈低束，眉眼舒展，面部较端正 | [查看](../../../mods/appearance/male_features_hair_refined_20261108/preview/01_clear_low_coil.png) |
| 02 | 温雅·斜扫低尾 | 单侧斜扫前发、低尾，眉眼和嘴角较温和 | [查看](../../../mods/appearance/male_features_hair_refined_20261108/preview/02_gentle_side_sweep.png) |
| 03 | 明朗·碎发低辫 | 不规则碎前发、单股长低辫，眼神较明朗 | [查看](../../../mods/appearance/male_features_hair_refined_20261108/preview/03_bright_fringe_braid.png) |
| 04 | 英气·露额高尾 | 露额上梳、高马尾，眉眼稍有力度，鼻口保持协调 | [查看](../../../mods/appearance/male_features_hair_refined_20261108/preview/04_heroic_high_tail.png) |
| 05 | 清和·露耳松披 | 露耳后梳、长发松披，神态较柔和 | [查看](../../../mods/appearance/male_features_hair_refined_20261108/preview/05_calm_loose_back.png) |
| 06 | 沉静·偏束细辫 | 偏分前发、单侧贴鬓细辫、半束长发，眼神较沉静 | [查看](../../../mods/appearance/male_features_hair_refined_20261108/preview/06_composed_temple_plait.png) |

## 风格与接入边界

以[已认可男性母版](../../../mods/appearance/male_style_alignment_20261106/preview/male_whole_01.png)及[温雅松披稿](../../../mods/appearance/male_guigu_inspiration_20261106/preview/02_asymmetric_loose_gentle.png)为画风依据：浅暖桃肤色、灰黑哑光细发丝、柔和工笔淡彩，沿用绿金衣服和米色背景。参考了项目已缓存的《鬼谷八荒》男性示例07／11／13的五官协调与神态；不采用其硬线条、服装或姿态。参考页面：[男性捏脸示例](https://www.gamersky.com/handbook/202103/1366633.shtml)。

六张仍是整体效果稿。画布均为1024×1536，但面部位置存在生成偏差，不能据此认定锚点已配准或已能混搭。选定后需按现有头颈、瞳孔、鼻口及领口逐项校准；只提取真正有区别的部件，近似五官继续去重，并检查左右眼开合、发丝遮挡及透明边缘。整稿中肉眼未见明显大小眼，不等同于拆层后的验收结论。

本轮发型差异比鼻唇差异明显，不预设六套全部增加独立鼻嘴。内部对照较推荐先看02、04、06的温和、英气及沉静方向；01眉眼仍稍沉，最终取舍以用户评审为准。

上一组保留在 `male_features_hair_round3_review_20261108`，状态为用户要求修订、未认可、未实装；不作为本轮最终候选。

## 生成与核对

使用内置 `image_gen`，每张单独以对应旧稿为发型／身体目标、认可母版为画风依据、游戏截图为比例参考。全部交付图保持原生输出，项目副本与原生文件哈希相同。六张尺寸均为1024×1536；活动包59张PNG与两份配置共61项哈希全部保持。本轮仅预览，不运行编译或游戏组合测试。

- [完整提示词集合](../../../mods/appearance/male_features_hair_refined_20261108/source/generation_prompts.json)
- [原生文件对应表](../../../mods/appearance/male_features_hair_refined_20261108/source/native_outputs.json)
- [用户反馈与参考来源](../../../mods/appearance/male_features_hair_refined_20261108/source/user_feedback.json)
- [文件核对记录](../../../mods/appearance/male_features_hair_refined_20261108/source/preview_manifest.json)
