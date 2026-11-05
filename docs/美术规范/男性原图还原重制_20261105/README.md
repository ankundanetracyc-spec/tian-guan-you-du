# 男性原图还原重制（2026-11-05）

状态：本轮素材已替换活动入口，实际游戏验证通过；美术观感待用户评价。用户否定上一轮男性整体观感，要求重新还原原三联图；女性活动素材仅作为共同画布、画风质量和透明边缘的比较对象。不能用旧版通过的工程检查代替本轮视觉还原。

## 当前依据与范围

男性造型以[本次用户原图](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/reference_user.png)为准。首先使用内置`image_gen`制作完整第三款还原母版，核对脸型、发色发丝、眼睑和虹膜、鼻翼及薄唇，再以原三款和新母版分别新绘前发3、后发3、眉3、眼2、鼻1、嘴1及完整脸耳颈1，共14件。相似五官共用，不强行拆成三套绑定组合；不复用被否定的v3/v4头发、脸或五官充作新绘。

女性22张活动PNG保持；男性衣服保持原涂绘、外框、纹样及七个选项，只重新适配与新脸的前后遮挡。鼻子层包括山根、鼻梁、鼻尖、鼻翼和鼻孔；不能只保留两个鼻孔或删掉鼻翼。百会前后分发、耳前后与身体衣前后归属仍按[锚点主规范](../本轮男女立绘锚点规范_20261003.md)处理。

## 尺寸与实际比例

全部安装用PNG采用1024×1536透明RGBA、共同左上原点、零运行时位移与比例1。生成原稿可有不同尺寸，安装前整层等比配准回共同画布；不逐眼、逐眉或逐束拉伸。配准只准至少三处不共线实际源点，最大点残差不超过2像素；隐藏后脑控制是设计框架，须如实标注，不能冒充原图可见解剖锚点。

细线大倍率缩小时使用预乘颜色的面积抗锯齿，避免仅四点采样造成眼线、鼻翼和细发丝锯齿。可见Alpha包围框仅用于测量画面内容，不是各部件独立铺满预览框的尺寸目标。男女按同一原画布区域、同一倍率比较。[实际尺寸报告](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/qa/asset_sizes.json)

## 眼睛与衣领复核

用户指出本轮首次新眼仍未还原：眼裂横向过长、眼尾拉平、眼睑弧线及虹膜露出比例偏离原图。该两件新眼已弃用，改为原图窄长与较清朗两种眼型分别新绘，保留原眼角关系、棕褐虹膜和上睑遮挡，不以瞳距相近宣称形状一致。[同倍率眼部对照](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/qa/eyes_revision_same_scale.png)

新脸与原图的颈侧宽度接近，但旧v4在y550横向切换前后领的分层会在新颈边产生台阶。本轮采用**完整原前衣在颈前＋原生连续纯后领带在颈后**，去掉该横向深度切线，保留全部原服装像素。前衣取v4安装前的完整服装，后领取v4各款纯后领带；不复制含旧上领的最终后件，不强压新脖子套旧窄颈边界。两层仍随同一服装选项切换，不增加衣服或后领选项。[分层来源与哈希](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/clothing_depth_sources.json) · [七套无头发遮挡对照](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/qa/new_face_seven_clothes_light.png)

## 来源与交付记录

候选包为`mods/appearance/portrait_male_faithful_restore_20261104_v5/`，不另放启用的外观配置。活动入口继续为`mods/appearance/ink_three_variants_20261004/`；旧版和未选稿保留溯源，不再作为本轮成品。

使用内置图像工具，未使用CLI替代。原生图、完整提示、所选输入及整层定位均在候选包`source/`：

- [完整母版提示](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/master_generation_prompt.json)、[脸底提示](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/face_generation_prompt.json)、[颈部新绘修订](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/face_neck_refinement_prompt.json)。
- [鼻唇等首次来源](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/core_generation_prompts.json)、[最终眼部提示](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/eyes_faithful_generation_prompts.json)、[眼鼻唇定位](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/core_registration.json)、[脸底定位](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/face_registration.json)。
- 前后发与眉分别保存在`source/frontNN_*`、`source/backNN_*`及`source/browNN_*`，每件绑定原生图、提示、真实点位和输出哈希。
- [安装前冻结基线](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/installation/current_active_baseline.json)区分383张包内PNG、51张注册纹理、44选项及22张女性纹理；配置副本使用snapshot名称，不启用重复包。

本轮验收依次为：原图同尺度外观比较、独立件透明及发色肤色检查、自由组合与七套颈领遮挡、活动资源安装与缓存导入、实际游戏截图。用户视觉认可仍单独保留，不以测试数量代替。

## 2026-11-05 再修绘与鼻层顺序

用户再次否定候选后，检查发现眼睑线偏硬、鼻尖下端不足、唇形偏直、肤色平铺。本次使用内置 image_gen 重新修绘两种眼睛、完整鼻层、嘴唇和脸耳颈，共五件；三款前后发及眉沿用本轮此前新绘版本。未将上一轮审查视为用户认可。透明修稿中的白色皮肤底、偏红山根及一份带黑底的鼻稿均未选用。

从后到前统一为：后发 → 后领 → 脸耳颈 → 前衣 → **鼻子（包括山根）→ 眼睛 → 眉毛** → 嘴巴 → 面饰 → 前发。该层序同时用于男女、正式 PortraitRenderer 和九部位测试合成器；鼻翼与鼻孔保持。女性22张素材原文件不改，仅适用共同层级修正。

安装用图仍全部1024×1536透明RGBA、共同原点、零运行时偏移。五件修稿的实际整层配准最大点残差低于1像素；耳点为隐藏设计支撑，不冒充原图可见测量。七套衣服沿用完整前衣与纯后领。

[本次提示集](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/revision_20261105/prompts.json) · [透明修订提示](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/revision_20261105/cleanup_prompts.json) · [最终鼻稿提示](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/revision_20261105/nose_soft_prompt.json) · [最终配准与输入](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/source/revision_20261105/registration.json) · [三款实际合成](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/preview/three_styles_light.png)。

[安装记录](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/installation/install_report.json)：替换28张男修纹理，其中14张头脸部件、14张服装前后层；保留23张纹理（含全部22张女性）。当前美术仍属于还原修订版，不宣称逐像素复刻或用户已认可。

### 实际运行结果

编译0警告、0错误。最终1250项检查通过，包含男女各54种前后发／眉眼组合、14套服装前后联动和正式渲染器鼻层低于眼与眉的验证；51张注册纹理、44个选项、全部22张女性素材基线保持。首轮发现男装后领01的旧导入缓存与源图不一致，已单独清除该缓存并重新导入，复验差异归零。导入编辑器退出时出现既有热重载计时器日志，最终实际游戏运行日志无报错。

[最终运行报告](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/installation/runtime_check.json) · [男修实机](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/installation/game_male_viewport.png) · [女修实机](../../../mods/appearance/portrait_male_faithful_restore_20261104_v5/installation/game_female_viewport.png)。这些验证证明安装、层序和组合正确，不代替对还原度的视觉判断。
