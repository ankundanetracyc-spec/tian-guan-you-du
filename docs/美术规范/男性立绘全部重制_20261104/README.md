# 男性立绘全部重制（2026-11-04）

**后续覆盖（2026-11-05）：** 用户已否定本文v3及后续v4的男性整体视觉，现按原三联图进行[男性原图还原重制](../男性原图还原重制_20261105/README.md)，当前制作中、未实装。本文头脸、五官、前后发不再作为新一轮造型来源；下文安装及工程检查保留为历史，不代表用户认可外观。不得执行本文旧安装脚本覆盖本轮新稿，女性保持现行版本。

后续山根与颈领已由[男女山根与男装领口修订](../男女山根与男装领口修订_20261104/README.md)接续：鼻层新增上段山根，七套男装改为后领与前衣的同选项配对。本文继续保留脸、前后发、眉眼、嘴及原下鼻来源；不要执行本轮旧安装脚本覆盖后续鼻层和服装分层。

状态：14件新稿已整体替换活动包，鼻翼补修与肤色统一已接入；609项实机检查全部通过，54种混搭与30件保留基线通过。首轮导入缓存不一致已重新导入并复验清零，最终实际游戏无运行错误；造型与参考相似度仍待用户评价。

## 当前指令与覆盖关系

用户否定此前两轮男性立绘，要求旧版全部撤掉并重新生成。本轮以原三联参考为造型依据，女性已完成素材仅用于发色、发质与共同画风参考。旧版[男性三款百会分层与五官接入](../男性三款百会分层与五官接入_20261104/README.md)和[男性参考三肤色与五官修正](../男性参考三肤色与五官修正_20261104/README.md)仅作历史记录，不得执行其安装脚本恢复被否定的素材。

此前“恢复最开始留耳稿”“六件发型原Alpha保持”等恢复旧件要求已被本轮重制指令覆盖。头部按百会前后关系、身体按衣服前后遮挡关系判断前后发归属，不能用横线机械截取。不得复用被否定的旧头发、脸或五官充作新生成结果。

## 制作范围

- 前发3件、后发3件、眉3件、眼2件、鼻1件、嘴1件、完整脸耳颈1件，共14件重新生成；相似五官共用，不强行分成三套绑定组合。
- 同一性别部件统一1024×1536透明RGBA画布，共同原点；定位仅作整层等比缩放和平移，不逐眉移动或扭曲局部造型。
- 三款眉参考原图各列，成对主体中点间距约106像素，不沿用旧控制架的123像素间距。眉毛使用细真实发丝，避免宽硬灰块。
- 统一女性活动素材的发色与水墨发质；脸耳颈与五官肌肤过渡保持同一肤色，避免肤色矩形、白鼻块或明显拼接边。
- 鼻部按用户最新要求保留两侧鼻翼：轮廓可以淡，不能只剩鼻尖和两个鼻孔而省去鼻翼。参考女性现行鼻部的透明边缘、肤色过渡与衔接方式，但男性鼻翼、鼻尖和鼻孔比例仍以男性原参考为准，不能直接套用女鼻。消除鼻梁上的U形灰边时，不能顺带删掉鼻翼结构。
- 女性22件、男性7套衣服及无须层共30张PNG保持；不修改服装装备或战斗规则。

女性目前实装的22张PNG均为1024×1536，男性新稿14件沿用相同完整画布。对照时保持共同展示比例，并分别检查可见内容的实际范围；不能将各部件的可见包围框各自铺满预览框。实测表见[素材尺寸记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/qa/asset_size_report.json)，其中可见范围用于复核尺寸与脸面占比，不替代原点和整层定位。

## 生成来源

本轮使用内置`image_gen`重新绘制透明部件，原生图、提示词与定位记录保存在新素材包`source/`。原三联图负责男性造型；女性活动素材负责共同尺寸、淡彩水墨质感、发色以及自然Alpha边缘的学习，不用于替换男性解剖比例。旧两轮男性生成件未作为新稿原画复用。

- 前发：[三款生成提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/front_generation_prompts.json)、[精修提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/front_refinement_prompts.json)、[整层定位记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/front_registration.json)。
- 后发：[三款生成提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/back_generation_prompts.json)、[整层定位记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/back_registration.json)。
- 三对眉：[独立生成提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/brow_generation_prompts.json)、[整对等比定位记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/brow_registration.json)。每对眉均为一张新绘透明原画，再作整对变换，未分别移动左右眉。
- 完整脸耳颈：[脸底生成提示词及原位记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/face_generation_prompt.json)。
- 眼、嘴与鼻：[五官生成提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/core_feature_prompts.json)、[最终定位记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/core_registration.json)。鼻部记录已更新为最终选稿。
- 最终鼻部重绘与肤色统一：[原生完整鼻翼重绘提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/nose_natural_regeneration_prompt.json)、[统一新脸肤色的提示词](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/nose_skin_harmony_prompt.json)、[最终注册记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/nose_final_registration.json)、[校色与整层定位脚本](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/register_final_nose.cjs)。

前期放大生成后再缩入画布的“大鼻剪贴型”候选，以及删除鼻桥后仅留鼻尖的试稿均未采用。最终鼻部为原生1024×1536画布、共同坐标上的小幅男鼻，完整保留两侧鼻翼、鼻孔与淡鼻梁；鼻型来自男性原图，女性素材只供学习实装尺寸与自然透明过渡。

色温统一的AI编辑稿增加了大范围肤雾，因此没有直接使用暖稿整图或其Alpha。最终仅在新自然鼻原生Alpha的支撑范围内，将65%的AI暖稿RGB与35%的原生鼻RGB混合；随后整层平移`(+2,-3)`像素对齐鼻孔，不做局部形变。原鼻Alpha在相同整层移动后变化像素为0，鼻翼轮廓保留。最终鼻孔中心分别约`(504.03,451.97)`与`(526.11,451.61)`；这不是完整三点控制架的2像素认证。最终鼻部已安装并通过实机复核，局部见[鼻翼与肤色合成近景](../../../mods/appearance/portrait_male_regenerated_20261104_v3/qa/nose_final_face_zoom.png)。

## 资产与撤旧安排

新素材包为[portrait_male_regenerated_20261104_v3](../../../mods/appearance/portrait_male_regenerated_20261104_v3)。新包不另启用外观配置；活动包继续使用`mods/appearance/ink_three_variants_20261004/`。

[接入前备份](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/before_install.zip)已保存14件被否定的男性PNG及两份配置；[备份记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/backup_report.json)与[30件保留基线](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/preserved_baseline.json)用于确认替换范围。备份仅作追溯，不代表允许恢复旧造型。

当前目录将`variant_count`的0值强制按1处理，不能用数量设0可靠撤掉旧男性选项。本轮已在14件新稿齐备后原位整体替换，未通过设0留下旧01或制造短暂缺层。安装脚本要求全部14件存在、画布与透明格式有效、每件不同于被否定的旧件，才开始写入；最终鼻翼修订也已重新安装、导入并复核。

## 接入与验收入口

- [安装脚本](../../../mods/appearance/portrait_male_regenerated_20261104_v3/source/install_in_game.cjs)：在项目根目录执行`node mods/appearance/portrait_male_regenerated_20261104_v3/source/install_in_game.cjs`；已完成本轮14件及最终鼻翼修订安装，结果见[安装记录](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/install_report.json)。
- [实机验证脚本](../../../tools/verify_male_regenerated_20261104.gd)：导入新素材后使用项目Godot运行时的`--path E:/stacklands --script res://tools/verify_male_regenerated_20261104.gd`。检查14件新稿哈希、导入纹理、54种混搭、独立选项与30件保留基线；不沿用旧脸Alpha或旧肤色报告要求。
- [交互预览](../../../tools/preview_male_regenerated_20261104.gd)：用同一运行时执行该脚本，默认显示男03前后发、眉03与眼02。

## 实机验收与画面

[最终运行结果](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/runtime_check.json)为609项通过、失败0项：14件新稿的来源／活动PNG哈希及导入纹理核心一致，54种前后发与眉眼混搭、独立选项、随机／重置、男女选择记忆及旧编号回退通过；30张保留PNG的SHA256保持。男性九槽仍为`3/3/3/2/1/1/1/7/1`，男女总44选项，1024×1536画布保持。

首轮检查发现的14项导入纹理缓存不一致，已重新导入并在最终实机检查中清零。恢复模式资源导入退出时仍出现项目既有HotReload Timer诊断；[最终实际游戏日志](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/runtime_check.log)无运行错误，实际窗口截图保存成功。未通过修改战斗或装备规则绕过导入问题。

[三款头部近景](../../../mods/appearance/portrait_male_regenerated_20261104_v3/preview/three_heads_light.png) · [三款完整拼合](../../../mods/appearance/portrait_male_regenerated_20261104_v3/preview/three_styles_light.png) · [女性与三款男性同尺度对照](../../../mods/appearance/portrait_male_regenerated_20261104_v3/preview/female_male_same_scale_light.png) · [实际游戏窗口](../../../mods/appearance/portrait_male_regenerated_20261104_v3/installation/game_viewport.png)

上述检查证明安装、导入与独立组合正确，不代替与原参考的视觉对照或用户评价；未宣称逐像素还原参考或已获用户造型认可。
