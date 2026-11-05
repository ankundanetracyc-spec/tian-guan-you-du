# 男性参考三肤色与五官修正（2026-11-04）

**已否定的历史版本：用户随后要求三款全部去掉并重新生成。本文脸底、五官与沿用头发均已被全量新绘替换，不得执行旧安装脚本恢复；现行见[男性立绘全部重制](../男性立绘全部重制_20261104/README.md)。** 下文仅保留历史过程。

状态：新的脸底与七件五官已替换活动素材，545项实机检查通过，已打开第三款交互预览。肤色与质感以原图第三人为标准，原六件留耳版头发继续保持。视觉相似度仍待用户查看。[前一轮头发记录](../男性三款百会分层与五官接入_20261104/README.md)。

## 本轮依据

用户指出实机不像三联原图，并明确要求：**采用第三个人的脸的肤色与质感，五官周围的肌肤过渡同步统一。** 三联中的三款眉、两款眼及共用鼻嘴继续独立组合；肤色目标只用原图03，不使用三个人的均值。

旧版五官文件确实替换过，但宽块眉、鼻部白亮肤片、唇形和开度仍有偏差；脸型沿用旧底，只有颜色校准，不能视作已完成原图脸的替换。原脸下颌与参考其实只差约1—3像素，本轮不夸大为大幅几何变形。直接量原图后，眉01左右主体中点约x464/x570、中距106，旧控制架x456/x579的123中距不适合直接作为本组三联的眉距；瞳核间距约100—102像素。

[原附件](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/reference_user.png) · [用户最终指定肤色的附件](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/reference_skin03_user.png) · [两次上传像素核对](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/reference_upload_verification.json) · [历史五官来源审计](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/feature_source_audit.json)

## 新脸底与肤色质感

重新生成完整的空白男性脸、双耳与颈部；原生透明图经整层等比缩放和平移回到1024×1536画布，不采用旧脸Alpha作为新轮廓。颏线约y544，原图03约y545；完整颈端约y718，多出的隐藏底画由衣领遮挡，不通过裁切或局部拉伸修正。

首稿的裂纹斑驳过重，第二稿又偏平滑。本轮最后使用内置图像工具按原图03补回细柔水彩wash，再将材质RGB应用于已注册新脸的皮肤支撑，保留新脸Alpha与几何。没有将透明背景RGB带入皮肤，也没有程序添加噪点。

以原图03的14处干净皮肤样区温和校色，排除被偏分遮挡的左额及五官颜料：平均ΔE由1.729降至0.758，最大1.460，颈部／颏下平均由1.974降至0.806。此步骤Alpha变化像素为0；范围仍为`[370,175,662,718]`（Alpha≥180，含末端坐标）。这些是校色样点表现，不是整图纹理或光照逐像素一致的认证。最终微纹理比原图略柔，未宣称逐纹理复刻。

[第三人皮肤目标](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/reference03_skin_targets.json) · [新脸整层定位](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/face_refined03_registration.json) · [材质步骤](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/face03_texture_transfer_report.json) · [最终校色](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/face03_color_report.json)

## 五官与游戏接入

七件五官以原图局部细节定向精修，统一到第三人的暖肤色、细柔边缘与水彩质感。三款眉按原图约106像素的成对间距定位，撤回旧控制架123间距；两款眼最终瞳核间距约101.26／101.38像素，原图对应约100.57／101.74。鼻孔间距22.46，原图03约22.02；唇缝73像素，原图软边72，中心约(515.21,483.36)。保留原参考轻微不对称，不强行全部居中到x517。

鼻嘴以早期母版原位清理件作形状基础再定向精修，眉以较贴参考的历史生成件为起点；未将来源不同的历史件宣称为本三联精确透明源。眼02第一稿过圆，追加一次收敛开度与虹膜的精修后选用，未采用过于半眯的历史候选。全部只作整层等比缩放和平移，无局部程序扭曲。细眼睑弧度和皮肤微纹理仍属于生成近似，不称逐像素还原。

已检查浅深底六种眉眼组合、第三人并排以及七套男服的头耳颈衔接，未见明显白鼻板、灰块眉、肤色矩形或领口漏底。活动8件PNG均与本轮之前不同；选项总数44及3眉2眼1鼻1嘴的独立组合规则保持。不会以哈希或混搭测试通过来代替原图外观对照。

[五官最终选择](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/feature_selection.json) · [原图实测点位](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/reference_feature_landmarks.json) · [最终五官实测](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/final_feature_landmark_check.json) · [七衣衔接](../../../mods/appearance/portrait_male_face_match_20261104_v2/qa/seven_clothes_neck_check.png)

[七件精修提示词](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/feature_refinement_prompts.json) · [原生图来源记录](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/feature_refinement_records.json) · [五官定位](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/feature_registration.json) · [第三人并排对照](../../../mods/appearance/portrait_male_face_match_20261104_v2/preview/before_after_reference03_light.png)

脸底提示词：[完整脸底](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/face_generation_prompt.json)、[第三人肤色与细柔材质](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/skin03_refine_prompt.json)、[最后水彩质感匹配](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/texture03_match_prompt.json)。所有生成／精修均使用内置image_gen；最终文件保存在`mods/appearance/portrait_male_face_match_20261104_v2/`，活动包继续为`mods/appearance/ink_three_variants_20261004/`。

[安装脚本](../../../mods/appearance/portrait_male_face_match_20261104_v2/source/install_in_game.cjs) · [接入前备份](../../../mods/appearance/portrait_male_face_match_20261104_v2/installation/before_install.zip) · [真实游戏验证脚本](../../../tools/verify_male_face_match_20261104.gd) · [交互预览入口](../../../tools/preview_male_face_match_20261104.gd)

## 实机验证

Windows实际游戏场景545项通过：8件候选与活动PNG逐字节／逐像素一致，导入纹理的可见核心也与当前PNG一致；54种前后发／眉眼混搭合成、独立选项、随机／重置、性别选择记忆及旧编号回退通过。36张保留PNG的SHA256不变，包含女性全部22件、男性六发、七服及无须层，配置文件字节不变，总44选项保持。

新脸Alpha与旧底不同的108616个像素仅作更换诊断；不再要求新脸Alpha等同旧脸。它与本轮最终候选的像素完全一致。资源导入成功，恢复模式编辑器退出仍有项目既有热重载Timer提示，最终游戏运行日志无运行错误。未修改C#应用代码，此轮验证采用真实场景与素材导入，而非重跑无关系统构建。

[运行结果](../../../mods/appearance/portrait_male_face_match_20261104_v2/installation/runtime_check.json) · [安装哈希与36件基线](../../../mods/appearance/portrait_male_face_match_20261104_v2/installation/install_report.json) · [第三款实际预览](../../../mods/appearance/portrait_male_face_match_20261104_v2/installation/game_male_viewport.png) · [三款新脸拼合](../../../mods/appearance/portrait_male_face_match_20261104_v2/preview/three_styles_light.png)

