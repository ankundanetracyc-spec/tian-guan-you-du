# 百会分层与发型肤色修订（2026-11-04）

后续女性鼻01已在[男女山根与男装领口修订](../男女山根与男装领口修订_20261104/README.md)补充眉眼间山根过渡，原鼻翼、鼻孔与本文脸底、头发保持。旧女性鼻全图哈希仅为本阶段来源记录，不覆盖后续山根稿。

状态：按用户最终分层定义完成修订并接入游戏，已打开第二款前后发＋玉虚真传常服供查看。眉3／眼2／鼻1／嘴1继续独立组合，视觉效果待用户评价。

## 本轮依据与范围

- [现行分层规范](../本轮男女立绘锚点规范_20261003.md#最新发型制作裁定)：头部以百会穴为前后分界；身体处按衣服前后遮挡归属。分界按头部空间理解，不是按图片高度横切，也不是以发髻最高点作为百会位置。
- 用户指出第二款大小、位置未还原参考，并授权将女修肤色调向三联图。本轮采用同一原参考 `portrait_rebuild_20261103/source/female_reference.png`。
- 单人参考先按脸部原尺寸回到1024×1536画布，不能拉大填满画布。左列约平移(+204,+51)，中列约(+217,+51)，右列约(+219,+51)。中列脸轴局部约x302，不能照搬左列平移。
- 候选、原稿、提示与检查保存在 `mods/appearance/portrait_hair_skin_revision_20261104_v1/`，活动包仍为 `mods/appearance/ink_three_variants_20261004/`。

## 前后发修订

之前第二款前发确实混入了上方后髻轮廓；后发还重复包含前侧发冠或侧编发。旧记录中“front02无额外后髻”的判断被本轮独立图层检查纠正。

- 前发01、02重新绘制为前侧发冠、两侧梳发及衣服前发束，移除后髻。前发03经复核保留原活动件，其贴头编发在前发层。
- 三款后发重绘为后脑连续底画、后髻及身体后披发。后发01保留后髻和发簪；后发02保留上方后髻及低盘髻；后发03只保留后脑、身后长发，移除前侧编发。
- 使用内置 `image_gen` 原生透明重绘。生成后只作整层等比配准；第二款低髻宽度的局部修正由图像工具重绘完成，没有逐束拉伸或脚本局部扭曲。
- 第二款前冠顶部约y150、后髻顶部y103；参考前冠交界约y153—163、后髻顶部y103。y300前发外缘约349／677，参考348／680；y350约348／679，参考345／680。低髻y450外缘约357／668，参考365／668；下沿约520，参考约515。整体位置与宽度已明显改善，仍存在数像素绘画差异，不称逐像素还原。
- 前发02四个绘画控制点的整层拟合最大残差约4.92像素；这些轮廓点含款式形状差异，未宣称通过非共线三点≤2像素完整控制架认证。没有把生成提示中的坐标当成实际验收结果。

[第二款旧稿／修订／参考对照](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/preview/style02_before_after_reference_light.png) · [六件独立分层](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/preview/six_layers_light.png) · [九种混搭深底](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/preview/nine_mixes_dark.png)

完整提示：[首轮](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/hair_generation_prompts.json)、[后脑修订](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/hair_generation_prompts_v2.json)、[前发01](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/front01_generation_prompt.json)、[低髻局部修订](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/rear02_local_edit_prompts.json)。原生选稿保留在 `source/raw_hair/`，整层变换在 [hair_registration.json](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/hair_registration.json)，[实际像素测量](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/qa/hair_registration_report.json)随包保存。

## 肤色核对与调整

原女脸与三联参考肤色并不完全相同：原脸略暗、偏黄红，颊部及下颌差异更明显。三张参考自身肤色接近，可采用共同色调方向。

先用内置图像工具制作肤色色稿，再以参考的14块干净皮肤样本对原脸RGB进行平滑校准。生成色稿有少于1像素的轮廓漂移、外围低Alpha散点和过亮颈部，没有直接覆盖原脸；最终严格沿用原脸每个Alpha字节，保留脸、双耳、完整颈肩的轮廓、位置、纹理位置和阴影结构。

- 14个皮肤采样区平均ΔE76由3.36降至0.74，最大约1.81；颈部平均由3.58降至1.23。这是指定采样区的比较，不是整张参考逐像素一致。
- 最终脸底1024×1536 RGBA；Alpha变化像素为0，可见范围仍为`[331,208,706,727)`。
- 仅替换女性唯一脸底 `female/face_shape/04.png`，未改变肤色选项或给男性强行套用女脸色图。五官、服装、男性素材保持。

[肤色前后浅底](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/qa/skin_before_after_light.png) · [深底](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/qa/skin_before_after_dark.png) · [校色测量](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/qa/skin_color_report.json) · [肤色编辑提示](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/skin_edit_prompt.json) · [校准脚本](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/source/adjust_skin.cjs)

## 游戏接入与验证

活动前后发01—03及女性脸04与本包最终PNG逐字节一致，保存编号和71个活动选项不变。安装前旧文件与配置备份于 `installation/before_install.zip`。66张其他活动PNG经SHA256核对不变，包括男修、已选七件女性五官、七款女服与素颜层。

素材重新导入后，实际Windows游戏场景432项检查通过：全部54种五官／前后发组合的真实合成像素、独立选项、旧款合并映射、随机／恢复、男女性切换、七个接入件哈希及原脸Alpha完全一致。已目视浅深底分层、九种发型搭配及游戏界面。退出时项目既有纹理释放诊断仍出现，不影响检查与交互预览。

[游戏验证结果](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/installation/runtime_check.json) · [第二款实际游戏画面](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/installation/game_style02_viewport.png) · [安装记录](../../../mods/appearance/portrait_hair_skin_revision_20261104_v1/installation/install_report.json) · [检查脚本](../../../tools/verify_portrait_hair_skin_revision_20261104.gd)

首页“试演与预览”→“水墨立绘九部位测试”仍可进入。此次打开的窗口已选第二款前后发、第二款眉眼和玉虚真传常服，前后发与五官仍可分别切换。前三阶段的生成/安装报告保留为历史，不要执行旧安装脚本覆盖本轮修订。
