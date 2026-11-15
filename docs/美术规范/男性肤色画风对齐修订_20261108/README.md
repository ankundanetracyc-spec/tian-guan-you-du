# 男性肤色与画风对齐修订

日期：2026-11-08。用户选中01、04、05、06，其八件前后发已按[四款发型实装记录](../男性四款发型实装_20261108/README.md)接入并通过2292项实机检查；02、03仍为未选候选。下文颜色统计和内部目测为整稿阶段记录，拆件验收以上方实装记录为准。

用户反馈上一组六张“感觉和现在的不是一个肤色和风格”。本轮对照发现旧稿脸部偏暗、偏红黄，头发更黑，明暗更硬；[上一轮清朗修订](../男性五官发型清朗修订_20261108/README.md)转为未认可的历史候选，不作为当前画风依据。

## 本轮依据与修改范围

- 直接以[已认可男性母版](../../../mods/appearance/male_style_alignment_20261106/preview/male_whole_01.png)作为编辑目标，约束原有浅暖肤色、灰黑细发、柔和工笔质感和明暗关系。
- [活动男性脸底](../../../mods/appearance/ink_three_variants_20261004/male/face_shape/01.png)仅作肤色及脸颈比例参考，不作为另一张待替换的脸。
- 本轮不再把上轮重绘稿或《鬼谷八荒》截图作为生成参考。六款保留母版脸型与鼻嘴，只细改眉眼和发型；不扩展中年脸或其他年龄外观规则。

## 六款修订整稿

文件保存于 `mods/appearance/male_style_locked_variants_20261108/preview/`。本轮主要展示不同发型，眉眼只作细微变化，不表示六套完全独立的新五官。

| 编号 | 方向 | 原生整稿 |
| --- | --- | --- |
| 01 | 清正·后收束发 | [查看](../../../mods/appearance/male_style_locked_variants_20261108/preview/01_clear_low_coil.png) |
| 02 | 温雅·斜扫低尾 | [查看](../../../mods/appearance/male_style_locked_variants_20261108/preview/02_gentle_side_sweep.png) |
| 03 | 明朗·碎发低辫 | [查看](../../../mods/appearance/male_style_locked_variants_20261108/preview/03_bright_fringe_braid.png) |
| 04 | 英气·露额高尾 | [查看](../../../mods/appearance/male_style_locked_variants_20261108/preview/04_heroic_high_tail.png) |
| 05 | 清和·露耳松披 | [查看](../../../mods/appearance/male_style_locked_variants_20261108/preview/05_calm_loose_back.png) |
| 06 | 沉静·偏束细辫 | [查看](../../../mods/appearance/male_style_locked_variants_20261108/preview/06_composed_temple_plait.png) |

本轮输出范围为1024×1536原生整稿预览。共同画布不等于五官、头发或颈领锚点已经配准；若后续选定，仍需拆件、去重及真实混搭检查。活动包59张PNG、两份配置和已实装六项面饰均不在本轮修改范围，原有游戏表现继续保留。

## 制作记录与核对

[本轮提示词集合](../../../mods/appearance/male_style_locked_variants_20261108/source/generation_prompts.json)记录输入依据及约束。使用内置 `image_gen`，全部交付PNG保留原生输出；六张均为1024×1536，项目副本与原生文件哈希逐一一致，活动包59张PNG和两配置共61项哈希保持。

六张已逐张与母版目测对照，肤色红黄倾向、深黑发色及硬阴影较上一批收敛。固定双颊小块的Lab色差ΔE76相对批准整稿为0.40–1.55，相对活动脸底为1.02–2.12；这只说明局部颜色更接近，不能作为整张风格一致的证明。未做几何配准或语义遮挡识别；颈部仍比活动脸底更亮、红黄更少，尤其04／05／06，后续拆件须沿用活动头颈并检查覆盖边界，不能直接整脸替换。

本轮未运行编译或游戏组合检查。共同画布和近似头部位置不能证明锚点已完成适配。

整稿阶段独立目测复核确认较上一批更接近现用稿；02仍略暖暗，颈影有差异。随后用户只批准01、04、05、06四张，拆件继续沿用活动脸颈，不将整张生成脸替换活动底层。

- [原生来源对应表](../../../mods/appearance/male_style_locked_variants_20261108/source/native_outputs.json)
- [预览及文件核对](../../../mods/appearance/male_style_locked_variants_20261108/source/preview_manifest.json)
- [肤色样本比较与方法局限](../../../mods/appearance/male_style_locked_variants_20261108/source/color_comparison.json)
