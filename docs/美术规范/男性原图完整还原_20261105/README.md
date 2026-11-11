# 男性原图完整还原（2026-11-05）

## 2026-11-09 当前：用户指定原画单侧镜像已接入、实机技术验证通过

当前入口为[男性五官定位、原笔触与单侧镜像验收](../男性五官统一定位与验收_20261109/README.md)。用户最新明确要求“不对称，你生成一边镜像一下不就好了吗”，覆盖上阶段默认不镜像的限制；按已授权范围执行，不再以旧禁令阻止或重复确认。

本轮 `mods/appearance/male_face_single_side_mirror_20261109/` 三眼01／04／05取屏幕左、三眉01／04／05取屏幕右，保原侧RGBA并围x513整侧整数反射，不重绘、不插值、不缩放；光点随整眼镜像如实留档，[实际游戏04整脸](../../../mods/appearance/male_face_single_side_mirror_20261109/installation/game_mirror_whole_04_face_1x.png)已保存。正常整脸、发衣选源及七发／胡须／衣领静态搭配已预审。六件已写入活动素材、其余61PNG及选款配置保持，本包实机335项技术检查通过、0失败，九无发／三完整／三正式渲染器输出已保存，六件镜像几何差0，用户预设保持；不以技术数作为美术分或用户最终认可。[选源预审与六SHA](../../../mods/appearance/male_face_single_side_mirror_20261109/qa/preinstall_review.json) · [源侧与最终候选](../../../mods/appearance/male_face_single_side_mirror_20261109/source/selected_features.json) · [本包实际运行记录](../../../mods/appearance/male_face_single_side_mirror_20261109/installation/runtime_check.json)。

鼻嘴与五款胡须原层定位沿用，头颈、发衣、女性及60选项／67PNG接口保持。此前 `male_face_natural_restore_20261109/` 六原画整数平移已实装并有305项技术记录，但保留原形差；再前一轮新绘／镜像糊图被用户否定，799／3355只作失败阶段技术历史。这些旧数量不套作本包运行结果。旧失败图及旧美术 `accepted` 不得复用，原笔触、清晰度和神态门槛继续有效。

## 2026-11-08 当前扩款：四对前后发追加

已接入后收低髻、高马尾、露耳松披和偏分鬓辫四对前后发，物理06–09／保存样式5–8；五官脸颈继续去重沿用01，原59PNG保持。当前前后发各七项、60选项／67PNG；仪容编辑器补读前后发旧别名，其他五官和随机规则保持。男441／女108组合、七衣领、六胡须及真实选款保存共2292项实机通过。[部件、实际画面与验证](../男性四款发型实装_20261108/README.md)。下方52／59等为早期接入快照。

## 2026-11-08 当前扩款：四款胡须追加

用户认可后续四款胡须，已追加八字短髭、短颏须、细髭尖颏、薄络腮须；保留无须、短髭颌须及55旧PNG。当前为六项男性面饰、52选项／59PNG，新物理03–06／样式4–7，旧0／3与1、2→0别名保持。生产C#、原五官与肤色发质及年龄随机规则不改。[实际游戏与扩款记录](../男性胡须扩款实装_20261108/README.md)。下方48／55是前次接入快照。

## 2026-11-08 当前追加：短髭颌须

用户已选中青年短须整稿并要求实装。独立面饰“短髭颌须”已追加，保留无须与全部54旧PNG；物理02／样式3／`male_beard_03`，旧别名1、2→0保持。当前为48选项／55PNG，下方47／54是追加前快照。青年明确手选的胡须可保存和正式显示，随机年龄筛选保持；中年和其他新五官发型未接入。[实装、来源与实机验证](../男性短须实装_20261108/README.md)。

## 2026-11-07 当前修正：左外眼角颜色断层

用户补充局部截图后，已精确定位到眼04左外上睫缘。旧松披导入的 `hidden()` 用 `sx<440` 二值选择生成原生RGB或原稿RGB，在当前画布x439→440、y357–365产生竖向色阶与笔画高度跳变。该处前发遮挡guard为false，Alpha连续；rawPNG及正式导入纹理都能重现，根因是旧颜色拼接，非锚点、眼层材质或导入设置。此前大小眼修正保留左侧，也保留了这个旧接缝。

本次从冻结当前眼04复制基底，仅在原source横坐标436–444之间连续混合既有配准原生／原稿颜色。实际只改181个RGB像素，半开范围为 `[436,355,444,381]`；整图Alpha、x≥512右半RGBA、左右瞳孔／虹膜及核心睑线区域全部保持，原左15／右14.5像素开合不变。用户同位置裁图、无遮挡及前发04／05下已独立检查，竖口接顺，未见双线、深影团或整体模糊。修复脚本和来源记录在 `mods/appearance/male_layer_seam_fix_20261107/`。

当前活动 `male/eyes/04.png` SHA为 `81723d5ca8413f3ecdb5a4a962a8850bf191aa7fd63c1a1818489eff4bd2f23a`。前一大小眼修正SHA `6c3f…252f14` 已作为本次基线与备份保存，下节是该阶段记录。另53张PNG与两份活动配置原字节保持，仍47选项／54PNG，style3／`male_eyes_03` 接口不变。

实机 **2029项通过、0失败**，含男81／女108组合、七领口、14组显隐及14组正式渲染配对，并从备份逐像素验证Alpha和右半RGBA保持、实际变动范围与来源报告一致。实际指定04搭配及无发／前01／04／05截图已保存。活动PNG与候选全部像素一致；Godot原有低Alpha边色处理差异仍单列，不能概括为导入完整RGBA一致。运行日志无ERROR／WARNING；本次只修素材导入与局部颜色接续，未改生产C#。

[用户原位置前后对照](../../../mods/appearance/male_layer_seam_fix_20261107/qa/reported_seam_before_after.png) · [原尺寸眼区对照](../../../mods/appearance/male_layer_seam_fix_20261107/qa/eye_area_before_after_1x.png) · [实际游戏画面](../../../mods/appearance/male_layer_seam_fix_20261107/installation/game_affected04_viewport.png) · [定位证据](../../../mods/appearance/male_layer_seam_fix_20261107/qa/left_lash_seam_diagnosis.json) · [独立视觉验收](../../../mods/appearance/male_layer_seam_fix_20261107/qa/final_left_seam_review.json) · [连续重导来源](../../../mods/appearance/male_layer_seam_fix_20261107/source/seam_reimport.json) · [安装与备份](../../../mods/appearance/male_layer_seam_fix_20261107/installation/install_report.json) · [实机检查](../../../mods/appearance/male_layer_seam_fix_20261107/installation/runtime_check.json)。

## 2026-11-07 当前修正：温雅杏目大小眼

用户眼部截图经活动组合匹配与独立复核，确认为前发04、眉04、眼04的温雅松披搭配；后发无法从窄截图唯一判断。问题不只来自前发遮挡：眼层04自身混入右侧竖发丝，右上睑开合也较左侧更高。固定同一取样口径下，原左约15像素、右约17像素；双侧虹膜横径接近，瞳心与原稿误差仅约0.1–0.6像素。旧工程检查证明加载与组合，不代表当时已发现这处美术缺陷。

本次只替换活动 `male/eyes/04.png`，名称“温雅杏目”、style3／完整ID `male_eyes_03` 及物理04接口保持。当前SHA为 `6c3f023df0d845a979727f4fab210c3ad95a03a881a40f3ea493d19dc0252f14`，修正包为 `mods/appearance/male_eye_balance_fix_20261107/`。下文松披初装与高束扩款的04哈希是当时历史，本节及本修正安装清单代表当前04。其余53张PNG、`appearance_parts.json` 和 `portrait_manifest.json` 原字节保持，仍47选项／54PNG。

右眼通过内置image_gen重新修绘主上睫缘和虹膜纹理，清除烘入发丝；左眼RGBA逐像素保持。没有镜像、局部拉伸整眼或缩小瞳孔，也未把旧不均原稿RGB回贴新右眼。生成大局部经同一整对等比定位归位，再用旧眼自然连续Alpha支持作柔和上限，收净额外内外尾和肤片；上限不增加生成Alpha、不复制旧右RGB。冻结参考作为复现输入，不读取安装后的活动图充当旧源。

最终v6独立复核：同ROI左15／15、右15／14，右中位14.5像素；正常尺寸、截图比例、无遮挡及三款前发均已检查，未见明显大小眼、烘发或额外尖长眼尾。数值是绘画边界粗测，误差约1像素，保留细微自然眼角与笔触差异，不宣称像素镜像。前几版仅改明暗或收得过窄的候选未安装。

新进程实机 **2021项通过、0失败**：男81／女108种组合、七套领口及14组显隐／14组正式渲染、原样式映射、指定04搭配、无发及前01／04／05均通过。活动眼层与最终候选SHA和原PNG全部像素一致；导入Alpha和Alpha≥230核心RGB一致，原有透明边色处理导致的低Alpha RGB差异单独记录。最终运行日志无ERROR／WARNING；导入阶段原有MCP端口占用及VFX重复UID诊断未改动。本次未改生产C#。

[前后对照](../../../mods/appearance/male_eye_balance_fix_20261107/qa/before_after_selected_hair04_light.png) · [原尺寸四组对照](../../../mods/appearance/male_eye_balance_fix_20261107/qa/before_after_eye_hair_native_1x_light.png) · [实际游戏画面](../../../mods/appearance/male_eye_balance_fix_20261107/installation/game_affected04_viewport.png) · [诊断](../../../mods/appearance/male_eye_balance_fix_20261107/qa/asymmetry_diagnosis.json) · [v6独立复核](../../../mods/appearance/male_eye_balance_fix_20261107/qa/v6_asymmetry_review.json) · [安装与备份](../../../mods/appearance/male_eye_balance_fix_20261107/installation/install_report.json) · [实机结果](../../../mods/appearance/male_eye_balance_fix_20261107/installation/runtime_check.json) · [完整内置生成记录](../../../mods/appearance/male_eye_balance_fix_20261107/source/eye_balance_generation_prompts.json)。重新进入立绘测试页会重新读取素材；本节不替代用户对最终视觉的评价。

## 2026-11-07 当前扩款：露额高束四件已追加

用户选定附件与 `male_exploration_round2_20261106/preview/01_open_crown_high_tie.png` 逐字节一致，SHA为 `fe7b9280cbb19019e76ffd6d451679e5e3ff3dc2b685b9020471b96451312f18`。已作为新可选款接入，未更换现行男性肤色、固定脸耳颈或衣服，也未接入本批另外两张整稿。当前是男23＋女24＝47选项、54张活动PNG；下文前次扩款的43／50是当时快照。

制作目录为 `mods/appearance/male_high_tie_install_20261107/`。新增前发“露额上梳”、后发“高束披发”、眉毛“清峻挑眉”、眼睛“清锐长目”，均为1024×1536透明共同画布、零运行时偏移，四槽各三款独立选择。新物理文件 `05.png` 对应保存样式4／完整ID后缀 `_04`；原01／04、style0／3及所有旧别名保持，原style3／完整 `_03` 仍读取04，不按退役ID回退。鼻嘴结构近似，去重沿用01；共同定位后的源鼻／唇相对旧件约有2.5–2.8像素横向差异，已记录，未单独移动旧鼻嘴补齐。

内置image_gen分别提取前后发和眉眼。第一版前发被工具放大，已弃用，选用重新提取的原尺寸版；高束髻归后发，额前上梳及耳前短绺归前发，后盖与颈后披发连续，胸前留空。两发和眉眼共同使用新原稿双瞳／下巴测得的整组定位：比例1.011042、旋转+0.487586°、三点最大残差1.755像素，保留原稿略窄眼距，不逐眼拉开。最终眼原生先整对配回原稿，10点最大残差1.808像素，再与共同定位合并一次预乘Alpha采样，无局部形变。

安装前已纠正眉外端颜色交界的硬口并清除188像素确认的前发残片，1197个主眉核心RGBA保持、Alpha无增加。眼采用包含原稿淡上睑褶线的第三版提取；左右原虹膜暗区的Alpha支持覆盖均为100%，实际眼裂／虹膜以同尺度原稿对照核验。浅深底、发际及九组前后发混搭已复核；六倍下仍有少量细毛软度与遮挡处生成补全差异，固定头颈与去重鼻嘴也使整套不能宣称逐像素等同于整稿。

实际新进程检查 **2292项通过、0失败**：男性81与女性108种前后发／眉眼组合、七套领口、14组前后衣显隐和14组正式渲染、独立选款、随机／恢复、男女记忆及全部旧数字／完整ID映射通过。四件候选与活动文件SHA一致，之前50张PNG（含女性24张）全部保持。测试页原PNG拼合逐像素通过；导入纹理Alpha和Alpha≥230核心RGB一致，原有Godot透明边色处理导致的低Alpha RGB差异另行记录，不宣称导入完整RGBA相同。最终运行日志无ERROR／WARNING；导入阶段原有MCP端口占用和VFX重复UID诊断保持，本轮未修改这些模块或生产C#。

入口：首页“试演与预览”→“水墨立绘九部位测试”→“男”，四槽分别选择上述新名称。[实际游戏画面](../../../mods/appearance/male_high_tie_install_20261107/installation/game_male_viewport.png) · [实际组合PNG](../../../mods/appearance/male_high_tie_install_20261107/installation/game_male_composite.png) · [安装清单与备份](../../../mods/appearance/male_high_tie_install_20261107/installation/install_report.json) · [2292项实机结果](../../../mods/appearance/male_high_tie_install_20261107/installation/runtime_check.json) · [同尺度原稿对照](../../../mods/appearance/male_high_tie_install_20261107/qa/source_vs_selected_face.png) · [视觉复核](../../../mods/appearance/male_high_tie_install_20261107/qa/final_visual_review.json) · [内置生成提示词](../../../mods/appearance/male_high_tie_install_20261107/source/generation_prompts.json)。整稿的选定与拆件最终视觉评价分开记录。

## 2026-11-06 当前扩款：温雅松披四件已追加

用户最新选择的散发附件与 `male_guigu_inspiration_20261106/preview/02_asymmetric_loose_gentle.png` 逐字节一致，SHA为 `5ca3f4cc0ea0410c22bcf07dfef278a9496a0fea968c38008dfe8f7dc5cf6ee3`。本次作为新可选款追加，基础男性母版、肤色和原有01仍以下节的 `male_style_alignment_20261106/preview/male_whole_01.png` 为准；未把其他两张候选一并接入。

制作目录为 `mods/appearance/male_loose_style_install_20261106/`。新增前发“温雅偏分”、后发“松披长发”、眉毛“温雅疏眉”、眼睛“温雅杏目”四件，均为1024×1536透明共同画布、零运行时偏移，可分别选款、显隐和与原款组合。新文件为各部位 `04.png`，保存样式ID为3／完整ID后缀 `_03`，避免占用退役样式1／2；旧数字别名1／2→0保持，四个扩充槽另补完整旧ID `_01`／`_02`→0，防止旧档误选新款。当前男19＋女24＝43选项，共50张活动PNG。

内置image_gen负责拆件与遮挡区补全。两发、眉眼共用以双瞳和下巴测得的整组等比定位，比例约0.997543、旋转约−0.2084°，三点最大残差0.429像素；没有逐眼、逐眉或逐束局部扭曲。眼原生提取先整对配回所选原图，再与共同定位合并成一次预乘Alpha采样；首版长眼尾候选未使用。实际眼裂与虹膜大小已作同尺度对照，未沿用此前被指出过大的女性眼处理。

前发保留单侧胸前完整长束和松散发丝；后发为耳后、颈后连续底画，不含发髻或簪子。发际肤色残留以源图连续matte清理，眉头和下沿灰边在安装前再次清理，保持不透明眼球核心和共同位置。正常尺寸的浅／深底独立复核未见明显灰肤片或连续硬边；六倍下仍有少量眉端、下沿细毛及遮发眼尾的生成重构差异，不能据工程通过宣称与原稿逐像素一致。

鼻嘴按去重原则沿用原01，未新增近似款。新原稿鼻子经过共同定位后比原鼻高约3.118像素，该差异保留在来源记录，未单独拉回旧鼻位置，也未以仅位置不同作为新鼻型。固定脸耳颈、肤色、七套前后衣和所有原有素材均沿用；本次之前46张PNG，包括女性24张，SHA全部保持。

新进程实际游戏检查 **1807项通过、0失败**，覆盖男性16种前后发／眉眼组合、女性108种组合、七套前后衣、14组后领显隐和正式渲染、四个独立新选项、旧样式回退、随机／恢复及两性选款记忆。四件活动文件与候选SHA一致，实际界面原PNG组合逐像素核对；导入纹理Alpha和不透明核心RGB保持，沿用Godot现有透明边色处理，低透明边缘RGB差异单独记录。最终运行日志无ERROR／WARNING；导入阶段仍有既有VFX重复UID和已占用MCP桥端口诊断，未改动这些无关模块。本次未改生产C#或玩法规则。

入口：首页“试演与预览”→“水墨立绘九部位测试”→“男”，分别选择上述四个新名称。[实际游戏画面](../../../mods/appearance/male_loose_style_install_20261106/installation/game_male_viewport.png) · [游戏实际组合PNG](../../../mods/appearance/male_loose_style_install_20261106/installation/game_male_composite.png) · [安装清单与备份](../../../mods/appearance/male_loose_style_install_20261106/installation/install_report.json) · [1807项实机结果](../../../mods/appearance/male_loose_style_install_20261106/installation/runtime_check.json) · [同尺度对照](../../../mods/appearance/male_loose_style_install_20261106/qa/source_vs_selected_face.png) · [视觉复核与边界](../../../mods/appearance/male_loose_style_install_20261106/qa/final_visual_review.json) · [内置生成提示词索引](../../../mods/appearance/male_loose_style_install_20261106/source/generation_prompts.json)。用户认可的是所选整稿，拆件后最终视觉评价仍由用户判断。

## 2026-11-06 基础款：用户选定肤色的新男性已替换

本节保留四件扩款前的基础款替换记录；下文39选项／46PNG为当时数量，当前数量以上节43选项／50PNG为准。

用户最新附件与 `mods/appearance/male_style_alignment_20261106/preview/male_whole_01.png` 逐字节一致，SHA为 `d97b79e3372df42db24b4dd5c3ec42f376d652616d9fa25d78a393b004da81a8`，并明确要求“男性就用这个肤色，把项目内的去掉，把你这个实装”。本轮以这张01原稿为男性造型和肤色依据，不使用同目录较白的02，也不再用下文旧目录的同名01当作现行男性母版。

制作与安装记录位于 `mods/appearance/male_style_alignment_20261106/integration/`。活动包内旧男性头脸、前发、后发、眉、眼、鼻含山根、嘴、首套前衣与后领共九张01已替换；原件留在 `installation/before_install.zip`，不再作为这九张活动素材读取。其他六套前后衣、无须层和女性24张素材共37张PNG逐字节保持；选款配置、样式ID、别名和数量不改，仍为男性15／女性24共39选项、46张PNG。

九件均为1024×1536 RGBA共同画布、零运行时偏移，仍可独立选择和显隐。内置image_gen负责透明拆件与被头发遮挡部分的补绘；可见五官核心保留本轮认可原图RGB，眼睛以眼角、虹膜和上下睑等九点作一次整层等比导入，最大残差1.079像素，同时复核眼裂及虹膜实际大小。眉毛只在原稿真实笔画、已有生成支持的像素内恢复透明度，无遮挡暗眉覆盖687／694，未用矩形填充皮肤底片。旧阶段整体左移5像素的操作不再重复执行。

新头耳颈保留既有完整Alpha，变化像素为0，继续兼容七套领口；皮肤绘画全部来自本轮新生成稿，没有回贴旧肤色。新底画初版偏暖、颈影偏深，导入时依据认可图额头、双颊、下巴及上下颈六处无遮挡皮肤样本作一次全层每通道线性材质校准，无空间分区、无局部绘画或形变。校准后这些样本与原稿差约1—3/255；隐藏头皮、发束、衣料及透明过渡仍含生成补全，实际组合不宣称与整稿逐像素相同。

新进程实际游戏检查 **1328项通过、0失败**：九件源／活动文件SHA一致，头颈Alpha保持、37张保留PNG、全部选项与旧样式映射、七套前后衣联动、14组后领显隐／正式渲染检查、女性108种前后发与眉眼组合、独立控件、随机／恢复及男女选款记忆均通过，实际两性截图已保存。九张导入纹理Alpha与不透明核心RGB一致；沿用Godot原有透明边色处理，低透明边缘RGB与原PNG的差异另行记录，测试页原PNG拼合仍逐像素核对。本次未改生产C#或玩法规则，工程检查与用户对拆件最终视觉的评价分开。

入口仍为首页“试演与预览”→“水墨立绘九部位测试”→“男”。[实际游戏画面](../../../mods/appearance/male_style_alignment_20261106/integration/installation/game_male_viewport.png) · [完整实际拼合](../../../mods/appearance/male_style_alignment_20261106/integration/qa/candidate_assembled_light.png) · [七套实际领口](../../../mods/appearance/male_style_alignment_20261106/integration/installation/seven_collars_light.png) · [安装清单](../../../mods/appearance/male_style_alignment_20261106/integration/installation/install_report.json) · [1328项运行结果](../../../mods/appearance/male_style_alignment_20261106/integration/installation/runtime_check.json) · [肤色校准](../../../mods/appearance/male_style_alignment_20261106/integration/qa/head_color_transfer.json) · [五官比例与来源](../../../mods/appearance/male_style_alignment_20261106/integration/source/feature_import.json) · [提示词与原生来源](../../../mods/appearance/male_style_alignment_20261106/integration/source/generation_prompts.json)。

以下为本轮替换前的来源、校中线与接入历史，旧“当前”字样及旧母版哈希不覆盖以上最新裁定。

当前状态：用户已认可完整男性新稿01并明确“可以，加”。同源的头脸、两发、四个五官与首套前后衣共九件已接入；旧七个多余发型／眉眼选项撤下，男性先保留这一套头脸搭配。实机检查通过，头颈轮廓及颈领锚点保持；女性与另外六套衣服保持。


## 最新修订（2026-11-06）：中线与眉眼透明边缘

用户指出头脸不对称。复核发现固定头骨Alpha中轴约512.5，而认可母版原生五官中轴约518—520，拆件后相差约5像素；并不是耳朵被单侧放大。眉眼鼻嘴和前发作为一组整体整数左移5像素，不缩放、不局部扭曲，头脸／脖子和全部衣服文件逐字节保持。校准后暗瞳中点513.01，与头轴相差约0.5像素。母版双眼约0.4像素高差、轻微眉尾形状差保留。见`integration/axis_alignment_20261106/correction.json`及`integration/qa/asymmetry_audit_20261106.json`。

随后用户指出眉毛下沿、眼角有未清干净的肤片和硬边。两件的透明边缘另行修订，不能只凭核心笔画覆盖率宣称抠干净。修订来源、深浅／棋盘放大检查和实际改变记录在`integration/edge_cleanup_20261106/`；核心笔画RGB、位置和头颈保持，当前活动哈希见`installation_report.json`。初版`face_parts/source/final_manifest.json`为左移／修边前来源，不代表当前最终哈希。

当前预览以游戏实际合成的`integration/qa/installed_game_portrait.png`和最新截图为准。返回开始页再进入立绘测试页会清理旧合成缓存并读取更新，不需改变游戏存档。

## 当前：认可整稿01的部件已接入（938项实机检查通过）

完整母版固定为 `whole_male_first_20261105/male_whole_01.png`，SHA `76e789ffa97e72c8ece941401e4b7aa93ba74e7567e881ed2e7f48a67ae266c2`。用户已明确认可并授权接入，不再请求重复批准。制作、备份与检查集合位于 `whole_male_first_20261105/integration/`。

实际替换九张：头脸、前发、后发、眉、眼、鼻含山根、嘴、01前衣与01后领。四个五官使用内置image_gen提取透明层并以母版实际瞳孔、鼻尖和唇线整件配准；核心可见笔画及肤色RGB保留认可母版，原发丝遮挡的小眉眼外端由生成稿补齐。没有使用已弃用的review_02五官。前发按母版发际整层配准并统一至母版发色，母版可见中性发色区域优先保留原RGB；后发为连续后盖、原款髻簪和披发，不再硬剪后盖轮廓。被头发遮挡的衣料和头皮需补绘，因此整个组合不宣称与整稿逐像素相同。

头脸沿用当前活动件的完整Alpha，改变像素为0；只更换皮肤绘画，边缘颜色从新稿实心肤色延展。头颈不缩放、不平移，固定颈侧Alpha230仍为y530 x428—601、y550 x426—603、y580 x421—608。五官保持母版实际眼位约y365而非强拉旧379行；山根随鼻层在眉、眼下。01衣领控制点回到母版(433,545)/(599,545)/(532,680)，其他六套前后衣未动，七套均在无头发遮挡的浅／深底检查。

撤下旧前发02/03、后发02/03、眉02/03、眼02共七件并保留备份；旧保存样式ID回落至新01。男性九部位仍可分别显隐与选择，其中头发、五官各一款、衣服七款。共男15＋女22＝37可选项，另有男私有后领七件，注册纹理共44。女性22张及配置字典、其他六套衣服十二张、无须空层共35张保持。

接入记录：`integration/installation/install_report.json`；9张活动PNG按原导入路径刷新缓存，`import_refresh.json`保留对应关系。`runtime_check.json`记录938项通过、零失败，含男性一套与女性54种组合、所有七套衣前后联动／显隐、随机／性别记忆、退役样式回落、正式渲染器鼻层级和活动PNG与实际导入纹理一致。最终运行日志无ERROR／WARNING。没有修改生产C#，测试脚本为 `tools/verify_portrait_approved_whole_20261105.gd`。

最终预览直接取游戏生成纹理：`integration/qa/installed_game_portrait.png`，若已运行最新截图，界面见`integration/installation/game_male_viewport_latest_20261106.png`。原`game_male_viewport.png`及母版与拆件对照是中线修订前记录；不能当成最新修边效果。七套领口见 `qa/seven_collars_light.png`／`seven_collars_dark.png`。这些工程检查证明接入、层序与接口，不把整稿认可扩张为用户已认可全部重构部件。

生成模式均为内置image_gen；根部提示在 `integration/source/generation_prompts.json`，五官提示在 `integration/face_parts/source/generation_prompts.json`，服装提示与原生来源在 `integration/garment_parts/source/selection_manifest.json`。后续扩款仍以同一认可整稿的画风与当前固定头颈为依据，不恢复旧弃用样例。

## 已完成阶段：先完整整体，再逐步制作部件（用户已认可）

用户明确要求“丑，全部去掉。你先生成整体效果吧，然后再慢慢生成部件”。此前三套前后发和新五官样例整批废止，`new_hair_pairs_review_20261105/review_02/asset_manifest.json`与`source/selected_assets.json`均标为用户拒绝、禁止接入。历史文件只作来源，不是可继续选装的候选。

新制作目录：`mods/appearance/portrait_original_restore_20261105_v6/whole_male_first_20261105/`。`male_whole_01.png`为内置image_gen一次完整绘制的1024×1536整张稿，原生结果未修改。以当前女性实际完整组合`reference/female_active.png`对齐绘画质感、暖黑灰发色和细柔发丝，以原男性三联图参考自然男性五官、长发与青绿道袍审美。没有继续用前轮部件拼成新预览。

当时只交付整体稿：五官、发型、皮肤、头颈与衣领在同一张画里一起确定。提示与来源见`source/whole_male_prompt.txt`、`source/whole_review_manifest.json`，女性实际组合清单在`reference/female_reference_manifest.json`。该阶段未拆部件、未接入；现已按用户认可完成上节接入。头颈共同画布、锚点、百会前后归属和鼻山根在眉眼下的要求保留为后续分件约束，不把本轮提示坐标当作已实测通过的锚点。

本轮核对当前活动51张纹理和配置均未改变。此前七套衣领接入记录是历史工程状态，不表示本次整张新稿已经实装或通过用户视觉评价。

## 已弃用：新前后发与五官样例（用户整批否定）

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/new_hair_pairs_review_20261105/`。最终审阅包为 `review_02/`，01疏朗半束、02清劲高束、03松风低束；每套对应独立前发与后发，四个五官样例由三套共用。眉、眼、鼻含山根、嘴均由内置image_gen新绘，无旧男性五官像素。首次浓睫毛、厚唇和嘴周色块稿被用户否定，已换成当前较清淡的新稿。

`review_02/asset_manifest.json`冻结各编号的10张PNG、预览及哈希。用户随后整批否定，该清单现标为弃用、禁止接入，不再作为可选择候选。新发型和五官未加入活动包；旧稿仅保留工程历史。

新发型采用女性已装素材作发色与笔触参照，前发亮度中位约76、后发约68，压住高光。后盖在y250—400各检查行均窄于对应前发；前发蓬松稿过大已舍弃。所有素材使用1024×1536共同画布；头颈原PNG与配置未改。五官按源图实测瞳孔中心、鼻尖和唇线作整件配准，鼻子与山根先于眼、眉叠加。

样例图：`review_02/three_new_pairs_light.png`、`paired_layers.png`、`new_features_closeup.png`、`four_feature_samples.png`。生成提示分别保存在 `source/generation_prompts.json` 和 `features/source/generation_prompts.json`，全部使用内置image_gen；清边、发色统一和配准记录见 `source/selected_assets.json`。

## 历史工程记录：固定头颈重做七套领口（已接入）

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/collar_hair_fixed_neck_20261105/`。七套前衣与后领分别重新生成，并按冻结颈部与各套交领V点局部配准。最终使用连续的新绘衣服，避免新旧领线交叉渐混产生的重影；衣服设计配色沿用原款，不能宣称旧衣身逐像素不变。早期局部拼片稿及六发修订稿均未接入。

实际接入仅14张衣服层，头、颈、五官、六发及女性共37张纹理保持，配置保持。`collars_only/installation/runtime_check.json`共1250项检查通过，男女各54种组合，最终日志无ERROR／WARNING。备份配置已改为.before文件，避免被外观目录扫描为额外MOD。该检查只证明衣领接入和现有组合，不代替新发型、五官的视觉验收。

冻结头颈SHA：`ed790ac8bcd41fd54d9a2e0a7be55f21704413ff5b86213a91285782a5d6f7c2`。当前颈侧Alpha230逐行实测在 `source/frozen_head_neck.json`；头部没有缩放、移动或改Alpha。衣领浅／深底检查见 `qa/collars_light.png`、`qa/collars_dark.png`。

以下是此前阶段记录，以以上最新状态为准。

## 历史阶段：直接重新生成头和脖子

用户指出男头骨不对，明确选择“最早颅顶较高、圆弧完整”的形状；随后要求直接重新生成，不继续在旧图抠修。最终采用内置image_gen完整新绘的无五官头部、双耳与连续颈部。最早旧高头只作结构参考；原三联第三款作肤色依据。前两次抠修／Alpha约束稿均作为过程归档，最终没有采用。

最终原生PNG为 `skull_partition_fix_20261105/source/head_final_native.png`。活动脸底与该原生结果逐字节相同：RGB、Alpha均未再编辑，不裁切、不缩放、不平移。头顶保持高圆弧，脸为空白，原五官继续独立叠加；不把旧图像素贴回新头，也不把旧颈部的凹口和毛刺保回来。

脖子按原画布中的下巴和颈领位置生成，左右轮廓连续，没有上一稿y510—520处的凹口／台阶。新颈不宣称与旧Alpha逐像素相同：实测Alpha≥230轮廓为y530的x428—601、y550的x426—603、y580的x421—608。上下位置沿用1024×1536共同画布与零运行时偏移，七套衣服在不靠头发遮挡的浅／深底条件下逐套检查，颈领连续、无漏底。下方尖端是交领后的隐藏皮肤覆盖，并非最终裸露颈形。

本次实际只改一张男性face_shape/01.png。其余50张活动纹理保持，包括六发、女性22张、眉眼鼻嘴、七套前衣和后领。男性鼻子与山根仍在眉眼下。运行时选项与配置保持。

## 当前来源与验证

制作目录：`mods/appearance/portrait_original_restore_20261105_v6/skull_partition_fix_20261105/`。

- `source/head_final_native.png` / `head_final_native_prompt.txt`：最终原生新稿与完整生成要求。
- `qa/head_final_native_preview.png`：新头深底预览；`qa/final_head_and_neck.png`：实际安装PNG的浅底展示。
- `qa/new_face_seven_clothes_dark.png` / `_light.png`：七套颈领实际素材组合。
- `head_only/male/`：本次选定的28件男性素材检查集合，其中只有头部更新，其他件来自本轮前快照。
- `head_only/installation/install_report.json`：一张替换、50张保持及最终原生来源SHA。
- `head_only/installation/runtime_check.json`：1250项游戏检查通过，男女各54种组合；活动51纹理／44选项、导入像素一致、前后衣配对、鼻子层级、选项显隐／切换均通过。
- `head_only/installation/game_male_viewport.png`：实际游戏显示。

程序检查证明上述素材加载与拼装行为，不代替用户的头形、肤色与整体观感评价。

## 当前：三款前发与三款后发已接入

用户指出后发含大量前冠、额侧、耳前绕鬓，属于实际分层错误。三件后发候选已按百会—耳尖的前后空间收为后盖、后髻、耳后及颈肩后连续披发；前侧发冠应留在前发。候选在本目录 `male/hair_back/`，旧／新对照 `qa/rear_only_before_after.png`。用户本轮授权接入后，三件后发已替换活动素材；现有三件前发沿用女性发色发质校准版。新头颈原生PNG、五官、七套衣服和全部女性素材保持，均为1024×1536共同画布、零偏移。

## 既有来源

`female_hair_match_20261105/`为当前活动头发的发色发质校准来源：男性前发03耳侧硬片与环圈修绘，六发向女性既有暖黑灰色调和柔和微高光靠拢。

`edge_cleanup_20261105/`及更早v6原像素拆层是历史来源，不能用其“头部保持”或旧哈希覆盖本次用户要求的新绘头部。后续继续处理后发时，以本次新头和当前颈领实测为依据。

本轮接入记录：`skull_partition_fix_20261105/hair_set/installation/install_report.json`。实际替换3张后发，其余48张纹理保持；专门刷新三件导入缓存。`runtime_check.json`中1250项游戏检查通过，男女各54种组合。`hair_set/qa/installed_three_styles.png`及`installed_nine_mixes.png`直接读取活动素材合成预览；视觉待用户评价。
