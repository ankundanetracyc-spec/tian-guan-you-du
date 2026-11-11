# 男性三款百会分层与五官接入（2026-11-04）

**已否定的历史版本：用户随后要求三款全部去掉并重新生成。本文的头发、脸与五官均不再现行，不得执行旧安装脚本恢复；以[男性立绘全部重制](../男性立绘全部重制_20261104/README.md)为准。** 下文保留前一阶段来源与验证过程，不代表当前选稿。

后续脸与五官已由[参考三肤色与五官修正](../男性参考三肤色与五官修正_20261104/README.md)替换：肤色／质感以第三人为唯一标准，新脸与七件五官已接入。本页六件留耳发型仍现行，下文旧脸Alpha保持与旧五官视觉记录为前一阶段，不再执行本页旧安装脚本覆盖新脸。

状态：已按用户最终指令恢复最开始保留耳部空间的六件头发，统一至女性活动发色并接入游戏；521项实机检查通过。五官去重为3眉、2眼、1鼻、1嘴，固定男性01脸耳颈与七套衣服保留，女性素材不变。造型仍待用户查看。

## 本轮规则

- 头部按百会穴前后划分前发、后发；身体处按衣服前与身体后分层。发髻、木簪及后脑披发位于后发，额前与身前垂发位于前发，不使用横向切图代替空间归属。
- **最终选稿以“最开始为耳朵轮廓留出空间的那版”为准**：六件采用初版已注册稿的原尺寸、原位置与完整Alpha，仅校色。此前全盖耳、削窄鬓发或扩大后发的试稿不再使用。耳边重点检查断口、白边、杂边与异常漏底，不再设定露耳面积或透明率；第三款原先的卷鬓和衣前整束一并保留。
- 三款前发、三款后发独立选择；眉、眼、鼻、嘴同样独立。参考中的相近五官合并为3眉、2眼、1鼻、1嘴，源01/02共用眼01，源03采用眼02。
- 所有游戏件为1024×1536 RGBA，统一原点；定位只用整层等比缩放和平移，不扭曲局部发束或五官。未宣称完整三点控制架≤2像素认证。

## 素材与来源

[素材包](../../../mods/appearance/portrait_male_reference_20261104_v1)保存原附件、原生透明图、定位记录与预览；游戏活动目录仍为`mods/appearance/ink_three_variants_20261004/`，候选目录不启用第二份外观配置。

- [本轮男性原附件](../../../mods/appearance/portrait_male_reference_20261104_v1/source/reference_user.png)及[原尺寸单人放置记录](../../../mods/appearance/portrait_male_reference_20261104_v1/source/reference_placements.json)：三列分别从x=0、593、1187取590、589、587像素宽，不把整列拉伸成游戏画布。
- [头发生成提示词](../../../mods/appearance/portrait_male_reference_20261104_v1/source/hair_generation_prompts.json)、[五官生成提示词](../../../mods/appearance/portrait_male_reference_20261104_v1/source/feature_generation_prompts.json)、[肤色提示词](../../../mods/appearance/portrait_male_reference_20261104_v1/source/skin_generation_prompt.json)：本轮原生素材均使用内置image_gen。后续耳侧重构提示与试稿仅作过程追溯，最终选择由头发定位记录确定，不执行中间安装或恢复脚本覆盖本轮结果。
- [头发定位记录](../../../mods/appearance/portrait_male_reference_20261104_v1/source/hair_registration.json)、[五官定位记录](../../../mods/appearance/portrait_male_reference_20261104_v1/source/feature_registration.json)：保留原生图与整体变换。旧男性五官拆件经检查与本轮参考不符，因此重新制作，没有直接恢复旧款。
- [三款拼合](../../../mods/appearance/portrait_male_reference_20261104_v1/preview/three_styles_light.png)、[九种前后发组合](../../../mods/appearance/portrait_male_reference_20261104_v1/preview/nine_mixes_dark.png)、[六张发层](../../../mods/appearance/portrait_male_reference_20261104_v1/preview/six_layers_light.png)。

## 发色与耳侧

以女性活动六件头发的冠部、鬓发、胸前发、后颈及后帘为分区目标，平滑调整RGB，收掉灰白高光尾部并校回共同暖炭灰；深浅两组同时收敛，没有将整张统一压黑。18个分区的中位亮度最大差约1.22、高光P90最大差约5.65（0—255亮度）。原发丝位置、轮廓、透明边缘及后发y≤190的髻饰保护区保持；未用程序添加新的发丝纹理，原画细节仍存在绘画差异。

最终活动六件与初版留耳稿的Alpha逐字节一致，画布均1024×1536；校色没有缩窄、放大或移动头发。浅底三款六处耳侧、深底九种混搭已目视复核，未见明显硬断口、白边、独立肤色碎片或异常漏底。该结论不代替用户造型评价，也不宣称原参考逐像素复刻。

[最终恢复核对](../../../mods/appearance/portrait_male_reference_20261104_v1/qa/final_selection_check.json) · [女性发色基线](../../../mods/appearance/portrait_male_reference_20261104_v1/qa/hair_tone_baseline.json) · [校色结果](../../../mods/appearance/portrait_male_reference_20261104_v1/qa/hair_tone_report.json) · [校色脚本](../../../mods/appearance/portrait_male_reference_20261104_v1/source/adjust_hair_tone.cjs)

## 肤色

先生成男性颜色参考，再在原男性01底图的RGB上做平滑校色。脸稍加暖、颈部暗影适当减轻；原有脸、耳、颈轮廓与纹理坐标保留，Alpha逐字节一致，变化像素为0，范围仍为`[370,172,660,714)`。

[肤色报告](../../../mods/appearance/portrait_male_reference_20261104_v1/qa/skin_color_report.json)的14处校色样点平均ΔE由2.823降至0.603，最大最终差0.964；颈部平均3.365降至0.520。这些也是校色拟合使用的样点，不能视作独立泛化验证，也不代表参考整图纹理与光照逐像素一致。

## 游戏验证

构建通过：0错误、0警告。活动男性九槽数量为`3/3/3/2/1/1/1/7/1`，共22项；女性22项不变，合计44项。男性旧前后发、眉眼鼻嘴、其余脸型和两款旧胡须从本活动包退下；旧保存编号沿用目录原有确定性回退。保留七套男服与无须层，不引入新装备规则。

真实Windows游戏场景521项检查通过：54种前后发／五官混搭逐像素核对、14件新安装PNG哈希与导入、独立选项、随机与重置、男女选择记忆、旧编号回退、原脸Alpha均通过。30张未改PNG的SHA256保持，包括22张女性、7张男服与无须层。耳内Alpha探针只记录诊断，不设露耳率验收。最终截图与合成已保存。

资源导入已完成。首次普通编辑器导入在重开场景布局时出现内存异常，恢复模式完成后续导入；最终实际游戏验证无运行错误。此问题没有通过改动角色或战斗规则绕过。

[实机结果](../../../mods/appearance/portrait_male_reference_20261104_v1/installation/runtime_check.json) · [实际游戏画面](../../../mods/appearance/portrait_male_reference_20261104_v1/installation/game_male_viewport.png) · [安装与基线](../../../mods/appearance/portrait_male_reference_20261104_v1/installation/install_report.json)

[安装脚本](../../../mods/appearance/portrait_male_reference_20261104_v1/source/install_in_game.cjs) · [接入前备份](../../../mods/appearance/portrait_male_reference_20261104_v1/installation/before_install.zip) · [真实游戏检查脚本](../../../tools/verify_male_reference_20261104.gd) · [男性交互预览入口](../../../tools/preview_male_reference_20261104.gd)

