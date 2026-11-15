# 男性短须实装

最新追加：用户认可后续四款并授权接入，当前男性面饰共六项，全包52选项／59PNG。见[四款扩展的实际游戏与记录](../男性胡须扩款实装_20261108/README.md)。下文首款48／55及390项为前次接入快照。

日期：2026-11-08。用户选中01短须整稿并明确要求实装；本轮只接入这一款独立短须，保留无须。中年与其余三套五官／发型搭配保持暂停或预览状态。

## 查看与选用

首页“试演与预览”→“水墨立绘九部位测试”→“男”→“面饰”选择“短髭颌须”。可单独选择、显隐，并与现有前后发和衣服搭配。“无须”继续是第一项。

[实际游戏界面](../../../mods/appearance/male_short_beard_install_20261108/installation/game_male_short_beard_viewport.png) · [实际游戏拼合](../../../mods/appearance/male_short_beard_install_20261108/installation/game_male_short_beard_composite.png) · [浅底放大](../../../mods/appearance/male_short_beard_install_20261108/qa/beard_close_light.png) · [深底放大](../../../mods/appearance/male_short_beard_install_20261108/qa/beard_close_dark.png) · [批准稿同位置](../../../mods/appearance/male_short_beard_install_20261108/qa/approved_beard_close.png)。

[角色仪容实际图](../../../mods/appearance/male_short_beard_install_20261108/installation/editor_short_beard_viewport.png) · [开局选款实际图](../../../mods/appearance/male_short_beard_install_20261108/installation/creator_short_beard_viewport.png) · [最终短须PNG](../../../mods/appearance/ink_three_variants_20261004/male/face_accessory/02.png)。

## 部件与旧接口

- 活动文件：`mods/appearance/ink_three_variants_20261004/male/face_accessory/02.png`；名称“短髭颌须”。
- 物理文件02、保存样式3、完整ID `male_beard_03`；测试台第二个选项的序号为1，不等于保存样式。
- 无须01／样式0及旧别名1、2→0保持。新增后为48个选项、55张活动PNG；此前54张PNG原字节保持，包含全部旧头脸、五官、发型、衣服与女性素材。
- 1024×1536共同画布，零运行时偏移；胡须位于嘴部之上、前发之下。未改已有五官的位置、大小、肤色或发色。

## 来源、提取与边缘

用户附件与[获批整稿](../../../mods/appearance/male_short_beard_install_20261108/reference/approved_short_beard_whole.png)逐字节一致，SHA为 `b0e58ce2489e1e2ae3e77f00388ea982edb51f16245b6940a8442ac2177df9f5`。

内置 `image_gen` 提供胡须透明形状支持。初版缺少嘴角下垂细须和下巴中央向上的须尖，另有过淡候选，均未作为最终部件。最终使用获批稿放大局部提取的v4支持，经一次预乘透明度的等比缩小、整层定位，限定原稿的胡须提取范围。

最终导入依据获批有须图与冻结的无须母版，分离胡须前景与肤色底色，清除夹带肤色造成的浅边；仅保留生成支持内实际观测到的原稿暗色笔触，并排除唇部开口。最终胡须使用原稿共同画布坐标，未程序绘制新毛发、未局部扭曲，也未重画其他层。背景皮肤估计存在小误差，不宣称整稿逐像素还原；正常尺寸及浅深底放大已独立复核，嘴角须尾和中央须尖保留，未见明显肤片、白边或硬断层。

最终活动PNG SHA：`8b44416a488007d1bd25c83540024bf7dfb0cc2869c7ea99a92b19220bfd1769`。

## 手动选择与年龄

旧代码在正式渲染、外观编辑器和开局选款三处屏蔽青年胡须，导致仅添加素材后，青年手选仍会丢失。本轮接通这三处手选入口：先保留合法的明确选款，再应用既有随机筛选。明确的部件 `min_age`／`max_age`、境界等限制仍生效；青年未指定或点击随机仍沿用原无须倾向，原有35岁判断未变。

胡须保存字段和复制流程继续复用现有数据。未增加中年脸、外观年龄、驻颜或境界换脸规则，也未修改用户存档。

## 验证

工程编译通过，0警告、0错误。独立Godot实际运行首轮354项通过、0失败，覆盖三前发×三后发×七衣服共63种搭配、独立选款与显隐、恢复无须、性别切换记忆、正式渲染16／25／40岁明确选款、旧别名及54张旧PNG保护。实际游戏截图和拼合已保存。

补充36项真实控件验证通过、0失败：角色仪容在16／25岁手选→应用→重开保留，随机后恢复无须，旧别名1／2归0；开局真实下拉可选短须并写入临时Profile，18岁预览正常。验证只使用进程内临时角色／Profile，未调用旅程或预设保存，用户预设文件SHA保持。合计本轮两组390项通过。

纹理导入保持全部Alpha和不透明核心RGB；Godot既有透明边色处理改变3575个低Alpha像素的RGB，单独记录，不能概括为导入全部RGBA逐像素一致。导入编辑器中既有VFX重复UID诊断与本部件无关；本次实际运行检查日志无错误或警告。

- [实装与备份清单](../../../mods/appearance/male_short_beard_install_20261108/installation/install_report.json)
- [旧素材与配置保护核对](../../../mods/appearance/male_short_beard_install_20261108/installation/preservation_check.json)
- [354项实机结果](../../../mods/appearance/male_short_beard_install_20261108/installation/runtime_check.json)
- [36项手选应用与重开结果](../../../mods/appearance/male_short_beard_install_20261108/installation/manual_save_check.json)
- [内置生成提示词索引](../../../mods/appearance/male_short_beard_install_20261108/source/generation_prompts.json)
- [生成支持定位](../../../mods/appearance/male_short_beard_install_20261108/source/import_report.json)
- [前景分离导入记录](../../../mods/appearance/male_short_beard_install_20261108/source/matting_report.json)

## 后续四款整稿（2026-11-08，已接入）

用户在短髭颌须接入后要求“再来几款”。本轮继续以同一男性母版的青年脸、浅暖肤色、灰黑细发与工笔画风为约束，只探索胡须覆盖范围及轮廓，不扩展中年形象。

| 款式 | 区别 | 整稿原图 |
| --- | --- | --- |
| 八字短髭 | 只留上唇短髭，下巴与下颌留白 | [01](../../../mods/appearance/male_beard_round2_review_20261108/preview/01_fine_moustache.png) |
| 短颏须 | 上唇干净，胡须集中于下巴中央 | [02](../../../mods/appearance/male_beard_round2_review_20261108/preview/02_short_chin_only.png) |
| 细髭尖颏 | 细髭与稍长尖颏分开，下颌两侧留白 | [03](../../../mods/appearance/male_beard_round2_review_20261108/preview/03_separate_moustache_goatee.png) |
| 薄络腮须 | 低颊短毛连接鬓部和下颌，保留较多皮肤 | [04](../../../mods/appearance/male_beard_round2_review_20261108/preview/04_light_connected_beard.png) |

四张均为内置 `image_gen` 原生整稿，1024×1536，已保存并核对来源。用户随后回复“可以”，四款已完成拆件、锚点校准和追加；实际效果与验证以[本轮扩款记录](../男性胡须扩款实装_20261108/README.md)为准。原整稿继续留作来源，不替代实际拼合验收。

[本轮提示词集合](../../../mods/appearance/male_beard_round2_review_20261108/source/generation_prompts.json) · [原生来源与文件核对](../../../mods/appearance/male_beard_round2_review_20261108/source/preview_manifest.json)。
