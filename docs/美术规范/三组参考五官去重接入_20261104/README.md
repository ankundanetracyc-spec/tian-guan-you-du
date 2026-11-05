# 三组参考五官去重接入（2026-11-04）

后续前后发与女脸肤色已修订，七件去重五官保持，当前立绘和运行结果见[百会分层与发型肤色修订](../百会分层与发型肤色修订_20261104/README.md)。本页保留五官来源与去重依据。

状态：按用户新附件接入并打开游戏预览，保留3眉、2眼、1鼻、1嘴，共7个独立透明部件。各槽与前后发自由组合，不要求每类三款或与发型一一对应。

## 来源与取舍

本次附件与 `mods/appearance/portrait_rebuild_20261103/source/female_reference.png` 的SHA256完全相同，为 `98ac9eed9b4692d859152895cab666a7e618aa1c1c341a6d3fd414904e695b03`。三个分图与此前对应的单人参考RGB也完全一致。因此使用该附件已有的透明拆件 `three_style_parts/female/`，不采用造型不同的 `portrait_fresh_sets_20261103_v2` 五官，也不重新生成、平均混色或重绘。

| 部位 | 保留来源编号 | 合并来源编号 | 活动名称 |
| --- | --- | --- | --- |
| 眉 | 01、02、03 | 无 | 疏平柔眉、弯挑细眉、平直细眉 |
| 眼 | 01、02 | 03并入01 | 浅杏长目、清圆杏目 |
| 鼻 | 01 | 02、03并入01 | 柔圆鼻 |
| 嘴 | 01 | 02、03并入01 | 自然浅唇 |

眉的粗细、眉峰和走向有明确区别，三对局部明度相关约0.837—0.859，保留三款。眼01与03相关0.9606，明显高于与02的0.8512—0.8614，合并长目、保留更圆的02。三鼻相关0.988—0.994，局部平均明度差不超过1.04/255；三嘴相关0.975—0.989，基本为同一唇型，分别共用01。这些数值是辅助观察，不作为所有未来五官的通用去重阈值。

[取舍与来源记录](../../../mods/appearance/portrait_reference_features_20261104_v1/source/selection_and_deduplication.json) · [本次原附件](../../../mods/appearance/portrait_reference_features_20261104_v1/source/reference_user.png)

## 实际接入

- 候选与安装记录存于 `mods/appearance/portrait_reference_features_20261104_v1/`；活动包仍为 `mods/appearance/ink_three_variants_20261004/`。
- 七件1024×1536 RGBA原文件直接复制到活动女性眉、眼、鼻、嘴目录；未缩放、平移、修色或重新抠取。眉、眼仍为左右成对同张，鼻和嘴各自独立。
- 三款前发、三款后发、固定柔和卵面04、七款女性服装与素颜层保持。女性九槽数量依次为 `[3,3,3,2,1,1,1,7,1]`，活动总选项68→71。
- 已合并的旧眼样式2在头像目录解析为保留样式0；旧鼻／嘴样式1、2同样映射为0，避免随机回退到另一种眼型。对应完整旧部件ID也支持映射。该映射只影响请求的单个部位。
- 预览页提示改为各部位独立、不绑定整套；“查看女修”仅返回女性并保留她的各槽选择，不再重置眉眼等选款。原有各部件显隐可用于查看无五官底脸。
- 替换前的空层与配置备份于新包 `installation/before_install.zip`。前一阶段的无五官接入脚本和测试保留为历史，不能重新执行来覆盖本轮五官。

[安装脚本](../../../mods/appearance/portrait_reference_features_20261104_v1/source/install_in_game.cjs) · [安装清单和基线](../../../mods/appearance/portrait_reference_features_20261104_v1/installation/install_report.json)

## 验证与入口

编译0警告、0错误。实际Windows游戏场景423项检查通过：七件来源／候选／活动SHA256一致，66张其他活动PNG不变，各部位真实选项数、旧款合并映射、独立控件、全部54种五官与发型组合的实际合成像素、四类五官显隐、随机／恢复、男女切换及“查看女修”保留选择。已目视浅深底六种五官组合与真实游戏截图。测试退出时既有纹理释放诊断仍出现，不影响上述运行检查。

入口：首页“试演与预览”→“水墨立绘九部位测试”，默认女修。当前游戏窗口已重新打开，视觉效果仍待用户评价。

[游戏实际画面](../../../mods/appearance/portrait_reference_features_20261104_v1/installation/game_viewport.png) · [六种自由组合](../../../mods/appearance/portrait_reference_features_20261104_v1/preview/six_feature_combinations_light.png) · [参考三款拼合](../../../mods/appearance/portrait_reference_features_20261104_v1/preview/three_faces_light.png) · [运行结果](../../../mods/appearance/portrait_reference_features_20261104_v1/installation/runtime_check.json) · [专项检查脚本](../../../tools/verify_reference_features_install_20261104.gd)
