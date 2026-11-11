# 男性四款发型实装

日期：2026-11-08。用户已批准肤色画风修订稿中的01、04、05、06四张；八件前后发已接入，实际游戏2292项检查通过、0失败。

## 用户选择与部件

四张附件与[上轮整稿](../男性肤色画风对齐修订_20261108/README.md)对应文件逐字节一致，分别为后收低髻、高马尾、露耳松披、偏分鬓辫。02斜扫低尾与03碎发低辫未获本次接入选择。

| 原稿 | 前发部件 | 后发部件 | 物理编号／保存样式 | 实际游戏 |
| --- | --- | --- | --- | --- |
| 01 后收低髻 | [后收拢发](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/06.png) | [后颈低髻](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/06.png) | 06／5 | [查看](../../../mods/appearance/male_four_styles_install_20261108/installation/game_hair_05_viewport.png) |
| 04 高马尾 | [露额拢发](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/07.png) | [长尾高束](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/07.png) | 07／6 | [查看](../../../mods/appearance/male_four_styles_install_20261108/installation/game_hair_06_viewport.png) |
| 05 露耳松披 | [露耳后梳](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/08.png) | [自然松披](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/08.png) | 08／7 | [查看](../../../mods/appearance/male_four_styles_install_20261108/installation/game_hair_07_viewport.png) |
| 06 偏分鬓辫 | [偏分鬓辫](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/09.png) | [半束长披](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/09.png) | 09／8 | [查看](../../../mods/appearance/male_four_styles_install_20261108/installation/game_hair_08_viewport.png) |

四稿脸型、眉眼、鼻口均与当前母版相近，本次继续去重，沿用活动01，不新增近似五官。原有59张男女PNG、胡须六项及衣服保持。两个发型槽由三项增至七项，当前为60项选择、67张活动PNG；前后发可分别选择。

## 分层与锚点

低髻、高尾及绑带归后发；额前梳发与耳前细绺归前发。鬓辫款可见的贴鬓辫段归前发，隐藏连接和半束披发归后发；松披以实际身体前后关系拆分。后发由现有脸耳颈自然遮挡，不在生产代码增加局部偏移或染色。

使用内置 `image_gen` 分别提取八张透明发层，原生输出留档。仅对整层做共同几何配准，不复用旧稿矩阵，不逐束局部拉伸，不从原稿回贴RGB，不以固定横坐标切换颜色。共同画布为1024×1536，运行时偏移保持零；整稿与组合存在生成补全和去重五官的差异，不能宣称逐像素完整还原。

制作包：[male_four_styles_install_20261108](../../../mods/appearance/male_four_styles_install_20261108)。[提取提示词](../../../mods/appearance/male_four_styles_install_20261108/source/extraction_prompts.json)和[原生来源](../../../mods/appearance/male_four_styles_install_20261108/source/native_outputs.json)已保存。

## 兼容与验证范围

仪容编辑器原先仅对胡须读取退役编号别名；男性旧发型1／2打开编辑时可能被重新分配。本次将已有别名分支扩至前后发，维持1／2→0的已定兼容关系；仍先保留有效样式，避免影响女性合法1／2。其他五官、手选胡须及年龄随机规则不变。

该单行兼容改动编译通过，0警告、0错误。新进程实际游戏2292项通过、0失败，覆盖441种男性前后发／眉眼组合、108种女性组合、56种新前后发独立搭配七衣领、24种新发对与六款胡须、旧数字／完整ID／别名，以及四组仪容应用重开和四组建角真实选项。128种随机种子覆盖全部七种前后发样式，用户预设SHA保持。

八件活动素材与候选SHA相同，旧59PNG全部保持；Godot导入Alpha和不透明核心RGB一致，低Alpha边缘存在既有导入颜色处理差异，不宣称完整RGBA逐像素等同。运行日志无素材缺失或画像断言错误；出现一次WASAPI音频设备失效错误和警告，与本次画像检查分开记录。导入扫描的历史资源UID提示未在本轮改动。

四套浅深底、头部近景、原稿对照与49前后发混搭已目测复核，未见必须返工的肤片、白边、发际硬口、耳根缺盖或帽盖外扩。低髻／高尾新发丝略亮软，细碎发丝有生成提取差异；复用活动01五官后眉眼也与整稿细节不同，不宣称整张原图像素还原。鬓辫前发首版被放大，最终采用重新提取的v2。

- [浅底49种混搭](../../../mods/appearance/male_four_styles_install_20261108/qa/mix49_light.png) · [深底49种混搭](../../../mods/appearance/male_four_styles_install_20261108/qa/mix49_dark.png)
- [四款与七套衣肩颈对照](../../../mods/appearance/male_four_styles_install_20261108/qa/four_styles_seven_collars.png)：已逐张看松披七套原尺寸近景，未见后发斜截边外露、领口缺口或肤片；其余三款肩颈接缝也未见异常。
- [最终拆件视觉复核](../../../mods/appearance/male_four_styles_install_20261108/qa/final_visual_review.json)
- [安装与部件清单](../../../mods/appearance/male_four_styles_install_20261108/installation/install_report.json) · [运行验证](../../../mods/appearance/male_four_styles_install_20261108/installation/runtime_check.json) · [旧素材保留检查](../../../mods/appearance/male_four_styles_install_20261108/installation/preservation_check.json)
- [共同配准](../../../mods/appearance/male_four_styles_install_20261108/source/hair_registration.json) · [加工与透明层记录](../../../mods/appearance/male_four_styles_install_20261108/source/hair_staging_report.json) · [鬓辫前发v2提示词](../../../mods/appearance/male_four_styles_install_20261108/source/temple_front_v2_prompt.txt)
