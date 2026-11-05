# 男女山根与男装领口修订（2026-11-04）

**男性后续覆盖（2026-11-05）：** 用户已否定本阶段男性整体观感，现按原三联图进行[男性原图还原重制](../男性原图还原重制_20261105/README.md)，当前制作中、未实装。男性头脸、五官与发型全新还原；颈宽遵从新脸与原图，不再沿用本文y550的前后衣切线。新方案保留完整原前衣，在新颈后配各款纯连续后领带，不能直接恢复本文含原上领的最终后件。本文女性山根与素材保持；同衣服选项联动的渲染层序继续沿用。下文检查与安装状态保留为当时的工程事实，不是本轮男性外观认可。

状态：男女山根与七套男装前后领已接入，构建、274项两层结构检查及1126项实机检查通过。后领在颈后、前衣在颈前，随同一个衣服选项联动；原鼻翼、鼻孔及前V／衣身保持。旧单层补丁作废，视觉仍待用户评价。

## 本次依据与部件归属

用户提供眉眼间局部，要求补出山根，并检查女性是否同样缺失；随后明确男装存在领缘毛边及颈侧、后领未连接两处问题。本轮延续[上一轮男性新稿](../男性立绘全部重制_20261104/README.md)与现行女性素材，不重新生成整张头脸或更改头身比例。

山根属于鼻子层，与鼻梁、鼻尖、鼻翼和鼻孔组成同一个可选鼻型；眉毛和眼睛仍独立组合。补充的是眉内端下方、内眼角鼻侧到上鼻梁的浅暖灰褐过渡，不把眉毛或整块眼周皮肤烘进鼻层。男女分别绘制，女性更轻柔，中央透出原有脸底，不添加白鼻梁、肤色矩形或硬轮廓。本次鼻层y≥415的原始RGBA逐像素保留，鼻翼与鼻孔不重做。

两性仍使用1024×1536透明RGBA共同画布及原点。生成后只作整层等比配准与透明补层合成，不逐眼、逐眉或局部拉伸。

## 男装连接问题的定位

修订前，隐藏前后发后，七套衣服均能复现颈侧与领尖之间的小空隙。以碧岚云叶袍在y530为例：新颈的Alpha≥40范围约x441—590，原左领止于434、右领始于599，两侧分别缺6、8像素。旧宽颈曾盖住这些空间，新颈侧更窄，因此本轮重建颈领连接。

衣服、脸和五官均在同一画布零运行时偏移上合成，并未在运行时按锚点自动吸附。仅修改锚点JSON或全图平移不能同时消除左右空隙；整层缩放还会连带肩腰、头脸与已定五官。此前仅在领尖内侧加少量布片的做法已被用户否定，最新要求为“修不好就重做”：固定头脸、衣身和原画布，重新绘制完整自然的前后领结构。后领在颈后保持完整底画，由脸颈层自然遮住；前领在颈前接入原有交领V形，不用补Alpha或贴出竖直边界代替布料翻折。

用户再次指出首款对照稿仍未对齐，要求严格定好锚点。该稿虽消除了Alpha40空隙，却改厚、改圆了领尖，并沿颈侧产生机械竖切，已归档为未选用；不能以“没有空隙”宣称领型和接口通过。当时已暂停其余衣服套用，冻结当前脸的哈希及Alpha230颈侧实边界、各原衣领尖顶点、外框和前V过渡；后续小范围单层连接也撤回，最终采用下节真实前后两层结构。

当前颈侧真实支撑点为左侧`(442,530)/(441,550)/(436,580)`、右侧`(589,530)/(589,550)/(595,580)`；下方580行在成图被前衣遮挡，标为hidden支撑，不能强迫可见前V领挪到该皮肤轮廓。这些点用于检查新脸与两层衣领的空间连接，不再要求前领单层沿整条颈侧逐行贴合。衣服外领顶、外框和前V点从各件原图单独测量并冻结，禁止为“缺口为0”改成厚圆滚领或机械竖切；不能把提示词目标或透明包围盒当作实际控制点。最终需要逐点配准、完整前后遮挡、整段接缝及深浅底视觉检查共同通过。旧守卫通过和Alpha40 gap0都不等于艺术通过。[实边界](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing_strict_neck_rows.json)与[接口分析](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing_strict_interface_analysis.json)保留作为真实颈部与旧接口的测量依据，不作为新前后领已完成的证明。

[逐行缺口测量](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing_gap_measurement.json) · [七套领口原状](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing_seven_neck_light.png)

## 当前实现与生成来源

最终采用的是**新绘完整连续后领带＋原合格衣服的深度归属调整**：每件原衣`y<550`的上领保留原尺寸与位置，归入后领；`y≥550`的原前V与衣身逐RGBA保留，归入前衣。新连续后领带在上领与脸颈后方补足真实布料底画，实际脸颈夹在前后两层之间。这里重做的是颈领结构、连续后领底画与遮挡关系，不能称七套前衣的全部纹理均重新绘制。

首款后领带由内置`image_gen`新绘透明稿。源图三处实际Alpha230支撑点`(300,208)/(850,106)/(1400,203)`经一次整体相似变换，对位到共同画布`(433,528)/(516,514)/(600,528)`，最大实测残差为`0.819902`像素；完整中心底画保留，由脸颈自然遮挡。详见[真实配准记录](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing01_back_v2_registration.json)与[配准脚本](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing01_back_v2_register.cjs)。这是共享后领带的一次三点配准，不是七款各自重新拟合，也不代表整套衣服已经获得用户视觉认可。

后六款以各自原衣的内衬、包边及布料为材质参考，使用内置图像工具编辑共享后领带的原生颜色与材质，最终沿用首款纯后领带的每个Alpha字节。可见颜色来自各款选定原生材质图；极弱透明边缘从邻近原生布料颜色延续，避免带入透明背景RGB，没有程序合成布纹或改变后领带几何。每套上领仍从自己的原衣读取，保留各自的领尖、外框与纹理，不直接复制首款整张最终后领。各款来源、提示与材质选择见[02](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing02_back_v2_palette_registration.json)、[03](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing03_back_v2_palette_registration.json)、[04](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing04_back_v2_palette_registration.json)、[05](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing05_back_v2_palette_registration.json)、[06](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing06_back_v2_palette_registration.json)、[07](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing07_back_v2_palette_registration.json)。[共享组装脚本](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/assemble_clothing_pair_v2.cjs)只调整前后归属，首款实际前后件及原衣哈希由[组装记录](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/clothing01_pair_v2_registration.json)绑定。

七套实际前后PNG已完成结构与深浅底复核，来源示例见首款[选定浅底](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing01_selected_pair_v2_neck_light.png)、[选定深底](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing01_selected_pair_v2_neck_dark.png)、[四阶段浅底](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing01_four_stage_light.png)、[四阶段深底](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing01_four_stage_dark.png)。四阶段依次为原脸颈＋原单层衣服、实际后领单层、实际前衣单层、后领→脸颈→前衣的真实组合。[首款观察记录](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing01_independent_pair_review.json)与[全套观察记录](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing_available_independent_visual_review.json)绑定选定文件哈希，`root_reviewed_scheme`仅记录本轮结构复核，`user_visual_approval`仍为`false`。旧`pair_v2_neck`失败原生前领与旧单层补丁均不作为本段成品依据。

源码已在三处接入同选项两层路由：[普通立绘渲染](../../../csharp/panel/PortraitRenderer.cs)从选中的衣服读取私有后领，按后领→脸颈→前衣的顺序绘制；[九部位测试合成](../../../csharp/panel/InkPortraitTestComposer.cs)采用相同遮挡，并随衣服显隐；[外观目录](../../../csharp/autoload/AppearanceModCatalog.cs)将配置的`rear_file`解析为同款衣服的`rear_resource_path`，不增加独立选项。空间关系始终为后领在颈后、前衣在颈前。

安装配置只追加男装的`rear_file: male/clothing_back/{index}.png`及`anchor_overlays.male`两项绑定，后者指向[当前男性颈领锚点图](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/rig/male_overlay.png)与[对应记录](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/rig/neck_anchor_overlay.json)。显示锚点已实测读取当前男性颈领点，默认隐藏；女性仍读取原锚点图。剥离这两行后配置字节与原始快照一致，未改选项或保存编号。上述坐标、工艺与配准细节只用于美术文档和检查图，未新增到玩家界面正文。

男女山根同样使用内置图像工具补画独立透明笔触，再与各自原鼻层合成；[男性提示](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/male_nasion_prompt.json)、[女性提示](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/female_nasion_generation_prompt.json)及[男性配准](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/male_nasion_registration.json)、[女性配准](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/female_nasion_registration.json)均保留原生来源与选稿依据。

## 接入与最终验收

原生来源与最终件保存于[本轮素材包](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/)，已安装至活动包`mods/appearance/ink_three_variants_20261004/`。共16张PNG：7张前衣、7张后领、2张鼻子；44个可选款式保持，物理纹理由44张增至51张。每套男装的两层仍由同一个衣服编号选择，合成顺序为后发→后领→脸颈→前衣→五官→前发；无后领的旧快照按空层处理。

接入前44张活动PNG及两份配置已备份于[原始快照](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/before_install.zip)。[安装器](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/source/install_in_game.cjs)在16件齐备、来源及两层守卫检查通过后写入，保留其余35张旧PNG；配置仅追加上述两项绑定。[安装记录](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/install_report.json)保存最终哈希与原始基线。

- C#构建0警告、0错误，见[构建日志](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/build.log)。
- v2两层结构守卫274项通过、0失败。共享后领的原生非共线三点配准最大残差`0.819902`像素；七套各自原衣框架控制点最大残差1像素，原前衣`y≥550`逐RGBA保持。见[完整守卫报告](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/clothing_strict_v2_guard_report.json)。
- 实际游戏1126项通过、0失败，含男女各54种、共108种前后发／眉眼混搭，14项衣服两层显隐、14项正式渲染器配对，以及选项、性别记忆、导入纹理和35张保留PNG基线。见[实机结果](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/runtime_check.json)与[验证脚本](../../../tools/verify_portrait_nasion_clothing_20261104.gd)。
- 男山根上段改变2898像素、女1590像素；两性鼻层`y≥415`的原RGBA差异均为0，鼻翼与鼻孔保持。七套前衣的原前V与衣身同样保持，未改脸、耳颈、眉眼、头发、嘴或女装。
- 最终[游戏运行日志](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/runtime_check.log)无`ERROR`或`WARNING`。恢复模式导入退出时仍出现项目既有HotReload Timer诊断，该导入退出记录与最终实际游戏运行分开，不将其计作本轮游戏运行错误。

最终图像：[中文前后对照](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/preview/final_revision_before_after.png)、[七套男装实际两层浅底](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/all_seven_pairs_root_light.png)、[深底](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/qa/all_seven_pairs_root_dark.png)、[男性实机](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/game_male_viewport.png)、[女性实机](../../../mods/appearance/portrait_nasion_clothing_20261104_v4/installation/game_female_viewport.png)。[双性预览脚本](../../../tools/preview_portrait_nasion_clothing_20261104.gd)保留交互入口。上述安装、几何、哈希与混搭检查不代替用户对外观的评价；当前仍为视觉待评价。
