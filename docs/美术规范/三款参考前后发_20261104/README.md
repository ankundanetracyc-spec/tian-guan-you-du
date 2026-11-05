# 三款参考前后发（2026-11-04）

当前前后发分层、第二款尺寸和女修肤色已按用户新裁定修订，详见[百会分层与发型肤色修订](../百会分层与发型肤色修订_20261104/README.md)。本页生成与初次接入数据保留为历史。

后续五官已按用户新附件去重接入，当前为3眉、2眼、1鼻、1嘴，详见[三组参考五官去重接入](../三组参考五官去重接入_20261104/README.md)。下文“无五官”是此前只审头发的阶段记录，不再代表当前预览默认画面。

状态：用户要求“先接入游戏，我看看”后，六件已替换女性活动前后发，并打开真实游戏预览页。上一轮女性五官和发型退出本轮；视觉效果待用户实机评价。

## 本轮来源与范围

- 用户要求先生成所附三款的前发、后发，保持尺寸比例、发质、发色、肤色，使用图中的无五官脸；此前五官与发型不再沿用。
- 唯一造型参考为[本次附件原图](../../../mods/appearance/portrait_reference_hair_20261104_v1/source/reference_user.png)。01对应左侧簪束披发，02对应中间盘发，03对应右侧编发披发。
- 附件是头像裁切展示，不能将每列直接放大到生产画布。其下巴、中央发际与现有固定女性脸底的原尺寸近似一致；制作使用固定脸底的1024×1536共同坐标。
- [无五官脸耳颈底图](../../../mods/appearance/portrait_reference_hair_20261104_v1/female/face_shape.png)与[衣服](../../../mods/appearance/portrait_reference_hair_20261104_v1/female/clothing.png)无损沿用固定素材；不生成眉、眼、鼻、嘴。附件与底图皮肤并非逐像素相同，不能称为附件整图原样拆取。

## 制作与交付

制作目录：`mods/appearance/portrait_reference_hair_20261104_v1/`。

- `female/hair_front/01.png`—`03.png`：前侧发冠、发际、贴脸及胸前细束。
- `female/hair_back/01.png`—`03.png`：连续后脑、发髻、颈后及肩后披发；无脸洞、无肩线裁口。
- 六件均为1024×1536 RGBA、共同左上原点；哑光暖炭黑、细线工笔发丝与克制暖灰高光。
- 使用内置 `image_gen`，每件分别从头生成原生透明画稿。没有抠取附件、颜色阈值去底、改色或局部变形。
- 配准仅用整层等比缩放和平移：三款前发为比例0.85、x=77，y依次−28、−15、−28；三款后发为比例0.75、x=129、y=0。具体记录以最终QA文件为准。
- [完整六条生成提示](../../../mods/appearance/portrait_reference_hair_20261104_v1/source/generation_prompts.json)、`source/raw/`原生画稿、`source/register_and_preview.cjs`重现脚本和[尺寸/Alpha/配准记录](../../../mods/appearance/portrait_reference_hair_20261104_v1/qa/metadata.json)随目录保存。

## 检查范围

叠放顺序为后发→固定脸耳颈→衣服→前发，不加载任何旧五官。已检查六件尺寸和真实Alpha，并查看三套浅底、深底拼合、独立前后层及九种前后发搭配。初次前发比例0.75时耳侧露出较多，最终改用整层0.85试拼位置；没有拉伸脸或调整肤色来迁就头发。

耳侧未发现不透明肤色耳片，细碎暖灰高光不等同于皮肤残片。固定脸和衣服保持原始文件。未宣称通过非共线三点残差≤2像素的完整控制架认证；素材尺寸检查、实际游戏接入检查与用户视觉评价分别记录。

[三款拼合预览](../../../mods/appearance/portrait_reference_hair_20261104_v1/preview/three_styles_light.png) · [六件分层预览](../../../mods/appearance/portrait_reference_hair_20261104_v1/preview/six_layers_light.png) · [九种混搭](../../../mods/appearance/portrait_reference_hair_20261104_v1/preview/nine_mixes_light.png)

## 游戏接入（2026-11-04）

- 实际活动包仍为 `mods/appearance/ink_three_variants_20261004/`，女性 `hair_front/01.png`—`03.png`、`hair_back/01.png`—`03.png`均以本次成品原文件替换，保存样式编号0—2保持。
- 女性脸仍为唯一 `face_shape/04.png`，与本次交付的无五官脸底逐字节一致。眉、眼、鼻、嘴各只保留一张全透明空层；面饰只保留素颜空层，随机和旧存档回退不会再带出旧五官或眼妆。
- 旧女性部件和配置先保存于新包 `installation/before_install.zip`，旧五官及眼妆02/03已从活动目录移除。男性51张、女性服装7张共58张PNG经安装前后SHA256检查不变；活动选项总数78→68。
- 入口为首页“试演与预览”中的“水墨立绘九部位测试”；默认显示女性，右侧前发、后发各三款可独立选择，支持原有显隐、随机、恢复、呼吸及男女切换。“参考女修五官”按钮改名为“无五官女修”。
- [安装脚本](../../../mods/appearance/portrait_reference_hair_20261104_v1/source/install_in_game.cjs)、[安装清单与基线](../../../mods/appearance/portrait_reference_hair_20261104_v1/installation/install_report.json)、[专项游戏检查](../../../tools/verify_reference_hair_install_20261104.gd)均已保存。
- 编译成功，0警告、0错误。首次编译被既有 `SwordBattlePresentationSmoke.cs` 三处集合构造二义性阻断，仅将这三处改为无参构造加集合初始化，数据与战斗规则未改。
- 实际Windows窗口运行348项检查通过：六件哈希、五类空层、旧选项退役与回退、固定脸、58张基线、九种前后发混搭的真实合成像素、九槽显隐、随机／恢复、男女切换与无五官按钮。已目视[游戏实际界面](../../../mods/appearance/portrait_reference_hair_20261104_v1/installation/game_viewport.png)与[真实合成图](../../../mods/appearance/portrait_reference_hair_20261104_v1/installation/game_composite.png)，[运行结果](../../../mods/appearance/portrait_reference_hair_20261104_v1/installation/runtime_check.json)。测试退出时原有纹理释放诊断仍出现，不影响上述检查或交互预览。
