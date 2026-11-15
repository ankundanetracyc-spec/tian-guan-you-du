# 三组参考五官去重接入（2026-11-04）

**2026-11-06 当前覆盖：** 按用户“重复的五官不用加，不重复的可以加”的裁定，已追加女性眉04「柔弯细眉」与眼03「舒展长目」，现为 **4眉、3眼、1鼻、1嘴**。本轮沿用项目已有画风，新增件与旧款独立混搭；鼻、嘴及其余旧PNG保持。新安装记录与618项实机、108种混搭验收见[本轮新增与验证](#2026-11-06按现有画风追加非重复五官当前)。下方11月4日的3眉2眼、七件来源和检查数保留为当时记录，不代表当前数量。新增眼采用按批准原稿校正的v4生成稿，并非逐像素抠取；美术观感仍待用户评价。

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

## 2026-11-06：按现有画风追加非重复五官（当前）

用户先认可对齐项目原有浅暖肤色、轻淡五官与灰炭黑发质的整体候选，再明确允许新增不重复的部件。本轮仅追加两件，活动包仍为 `mods/appearance/ink_three_variants_20261004/`，不替换旧款、不绑定整套。

| 新增部位 | 活动物理文件 | 显示名称 | 来源原稿 | 保存样式ID |
| --- | --- | --- | --- | --- |
| 成对眉毛 | `female/eyebrows/04.png` | 柔弯细眉 | [获批整体01](../../../mods/appearance/female_aligned_features_20261106/preview/01_soft_arch_almond.png) | 3 |
| 成对眼睛 | `female/eyes/03.png` | 舒展长目 | [获批整体02](../../../mods/appearance/female_aligned_features_20261106/preview/02_soft_straight_long_eyes.png) | 3 |

去重按同脸、同尺度的实际造型判断：只保留第一组的柔弯眉，第三组眉与其接近、第二组眉与已有平直细眉接近，不再追加；眼睛只保留第二组的舒展长目，第一组、第三组分别与已有浅杏长目、清圆杏目接近。三组鼻与现有含山根柔圆鼻近似，三组嘴也未形成独立新唇型，因此鼻与嘴仍各一款。本轮未把颜色深浅、重绘细微差异或换名当作新款数量，旧去重研究中的相关系数也不作为通用门槛。[本轮取舍与来源](../../../mods/appearance/female_features_install_20261106/source/selection.json)。

当前女性九槽数量为 `[3,3,4,3,1,1,1,7,1]`，五官共9个选项，仍可逐部位独立选择。原文件编号、保存样式与别名全部保留；新眼的物理文件03使用保存样式ID **3**，没有占用已退役的样式ID 2。旧眼别名 `2 → 0` 与 `female_eyes_02 → 0` 继续生效，旧档不会因本次追加而误读成新眼。脸耳颈、前后发、服装、鼻、嘴、素颜层及男性素材均沿用原件，全部44张旧PNG哈希保持。

### 眼层校正范围与版本边界

用户指出v2的眼睛过大；v3也改变了批准原稿的眼形。两者均未作为活动选项成功接入，不得再运行旧配准或导出脚本覆盖当前眼层。失败安装尝试已完整回滚，原备份留档于 [installation_attempt01_rolledback](../../../mods/appearance/female_features_install_20261106/installation_attempt01_rolledback)；当前安装使用新建的 `installation/` 批次。

最终眼层为内置图像工具参照批准原稿02及其原尺寸局部制作的 **v4生成透明稿**，不是原图逐像素抠取。导入核对外眼线末端、内眼角、虹膜中心和上下睑共10处实测控制点，只作一次整层等比配准，不逐眼移动、不局部变形、不修色。最大控制点残差约 **1.882像素**，处于项目2像素工程门槛内；人工点位约有1像素读取误差，残差仅说明几何配准，不证明笔触、眼形或美术观感与原图完全一致。当前褶线、下睑笔触仍略清楚，这些绘画差异保留供用户评价。[v4来源与导入记录](../../../mods/appearance/female_features_install_20261106/source/eye_import_v4.json) · [同尺度原稿／实际眼层对照](../../../mods/appearance/female_features_install_20261106/qa/eyes_v4_10point_same_crop_comparison_6x.png)。

### 当前安装与验证

两件最终PNG均为1024×1536 RGBA共同画布、零运行时偏移，已追加活动配置；新眉、新眼均可在首页“试演与预览”→“水墨立绘九部位测试”中独立选择。当前备份为 [installation/before_install.zip](../../../mods/appearance/female_features_install_20261106/installation/before_install.zip)，[安装报告](../../../mods/appearance/female_features_install_20261106/installation/install_report.json)记录最终源文件、活动文件、保存ID、旧组件与PNG基线；[安装脚本](../../../mods/appearance/female_features_install_20261106/source/install_selected_features.py)保留旧件与别名，未改渲染代码。

配套运行时增量编译0警告、0错误。导入完成后，新进程实际游戏检查 **618项通过、0失败**，涵盖全部 **108种**前后发／眉眼组合的真实合成像素、两个新选项的独立切换、44张旧PNG哈希、旧样式与别名回退、显隐、随机、恢复，以及男女选择记忆；实际窗口截图已保存。导入日志中的并行编辑器LimboAI复制、桥端口告警与最终实际游戏验收分开记录；上述工程通过不代替新增部件的用户美术评价。

[本轮运行结果](../../../mods/appearance/female_features_install_20261106/installation/runtime_check.json) · [实际游戏画面](../../../mods/appearance/female_features_install_20261106/installation/game_viewport.png) · [实际新五官合成](../../../mods/appearance/female_features_install_20261106/installation/game_new_features_composite.png) · [最终浅底拼合](../../../mods/appearance/female_features_install_20261106/qa/new_both_light.png) · [专项验证脚本](../../../tools/verify_female_feature_expansion_20261106.gd) · [编译日志](../../../mods/appearance/female_features_install_20261106/qa/build.log)。
