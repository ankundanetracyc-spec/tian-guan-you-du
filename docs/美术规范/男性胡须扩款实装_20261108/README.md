# 男性胡须扩款实装

日期：2026-11-08。用户认可四张后续胡须整稿，授权接入。四款已作为独立面饰追加，保留“无须”和原“短髭颌须”；1137项实机检查通过，0失败。

## 查看与选用

男性外观现有六项胡须／面饰，可在开局、角色仪容与九部位测试台独立选择。测试台入口：开始页“试演与预览”→“水墨立绘九部位测试”→“男”→“面饰”。

| 新款 | 活动素材 | 实际游戏界面 | 保存样式 |
| --- | --- | --- | --- |
| 八字短髭 | [03.png](../../../mods/appearance/ink_three_variants_20261004/male/face_accessory/03.png) | [查看](../../../mods/appearance/male_beard_round2_install_20261108/installation/game_beard_04_viewport.png) | 4／`male_beard_04` |
| 短颏须 | [04.png](../../../mods/appearance/ink_three_variants_20261004/male/face_accessory/04.png) | [查看](../../../mods/appearance/male_beard_round2_install_20261108/installation/game_beard_05_viewport.png) | 5／`male_beard_05` |
| 细髭尖颏 | [05.png](../../../mods/appearance/ink_three_variants_20261004/male/face_accessory/05.png) | [查看](../../../mods/appearance/male_beard_round2_install_20261108/installation/game_beard_06_viewport.png) | 6／`male_beard_06` |
| 薄络腮须 | [06.png](../../../mods/appearance/ink_three_variants_20261004/male/face_accessory/06.png) | [查看](../../../mods/appearance/male_beard_round2_install_20261108/installation/game_beard_07_viewport.png) | 7／`male_beard_07` |

原无须仍为物理01／样式0，短髭颌须仍为物理02／样式3；旧别名1、2→0保持。新增后全包52选项、59张活动PNG；此前55张PNG全部保留原字节，生产C#不改，年龄／随机规则沿用前次接通后的行为。

## 原稿与提取

源稿为用户认可的[四款整稿](../男性短须实装_20261108/README.md#后续四款整稿2026-11-08已接入)。延续同一男性母版的青年面部、浅暖肤色、灰黑细发和工笔淡彩，不接入其他新五官、发型或中年脸。

内置 `image_gen` 分别从获批局部提取透明胡须形状支持。Godot导入将支持等比缩小一次、整层定位到1024×1536共同画布，限定原稿前景分离范围；实际毛发色和分布取自获批稿，清掉唇部与夹带的皮肤底色。无程序绘制新毛发、无局部扭曲，运行时偏移为0。

前三款各自测量无须参考与获批稿的肤色中位差。薄络腮首稿在右下颌叠加了既有脸底阴影，最终改用已验证的实际无须组合为底色参考，避免重复暗化；不修改原脸底。其右侧近景仍有轻微浓淡差异，正常尺寸的细须轮廓和连接自然，独立复核未见明显肤片、白边或硬断层。原生生成支持、背景估计和最终导入分别留档，不宣称整张立绘逐像素等同原稿。

## 验证与记录

本轮仅添加素材和选款配置，不修改生产代码。验证覆盖55张旧PNG及其他组件保护、六项选款映射和旧别名、四款×三前发×三后发×七衣共252组实际拼合、独立显隐、女性保持、16／25／40岁正式渲染、真实仪容应用重开以及开局下拉选款。使用临时内存角色和Profile，不调用旅程／预设保存。

最终独立Godot运行1137项通过、0失败，四款均完成真实编辑器应用／重开及开局选择。55张旧PNG、三份相关生产C#原字节保持，其他组件配置保持；用户预设SHA保持，实际运行日志无错误／警告。本轮不改生产代码，未另作工程重编译；纹理重导入阶段的既有VFX重复UID诊断与本次四款无关。

纹理导入逐款检查全Alpha及存在的不透明核心RGB；低透明度RGB受到Godot既有边色处理影响，报告单独列出，不宣称完整RGBA不变。最终结果以以下实机报告为准。

- [本轮实机报告](../../../mods/appearance/male_beard_round2_install_20261108/installation/runtime_check.json)
- [实装与备份记录](../../../mods/appearance/male_beard_round2_install_20261108/installation/install_report.json)
- [旧素材与接口保护核对](../../../mods/appearance/male_beard_round2_install_20261108/installation/preservation_check.json)
- [内置生图提示词与来源索引](../../../mods/appearance/male_beard_round2_install_20261108/source/generation_prompts.json)
- [四款导入与底色测量](../../../mods/appearance/male_beard_round2_install_20261108/source/import_report.json)
- [04实际底色核对](../../../mods/appearance/male_beard_round2_install_20261108/source/04_background_review.json)
- [仪容实际图](../../../mods/appearance/male_beard_round2_install_20261108/installation/editor_beard_07_viewport.png) · [开局实际图](../../../mods/appearance/male_beard_round2_install_20261108/installation/creator_beard_07_viewport.png)
