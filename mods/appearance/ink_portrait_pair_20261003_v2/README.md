# 男女水墨立绘九部位素材（2026-10-03）

最新选择：用户本次指定的双人附件与 `preview/pair_light.png` 像素一致，当前游戏预览使用本包原组合来源。后续定位以[选定母版 rig](../selected_ink_portrait_rig_20261003_v1/rig/portrait_rig.json)为准；本包原拆件的眼周底片、头发归属与边缘仍须返修验收。母版选定不等于九件已经清边合格。

本包包含男、女各一套 1024×1536 RGBA 透明立绘。两套均以本轮新完整稿和用户提供的光头图为来源，没有读取项目里的旧立绘素材。用户选定图对应的是九层实际重组 `preview/male_recomposed.png`、`preview/female_recomposed.png`，不是未拆分的 `male_portrait.png`、`female_portrait.png`。

每套的九张 PNG 位于 `male/`、`female/`。文件统一原点、无需位移；由后到前的拼合顺序见 [portrait_manifest.json](portrait_manifest.json)。对应用户定义的部位为：前发、后发、眉毛、眼睛、鼻子、嘴巴、脸型、衣服、面饰。脸型含双耳和颈部。两套原稿均无面饰，`face_accessory.png` 是保留槽位的全透明空件。

检查图：

- [男女浅底拼合](preview/pair_light.png)
- [男修九件总览](preview/male_nine_parts.png)
- [女修九件总览](preview/female_nine_parts.png)
- [男修眼上高光放大](preview/male_eye_highlight_review.png)
- [像素重组报告](qa_report.json)

男修上眼皮原本单侧偏白的高光已改为两侧轻暖、同位置的反光。完整图与九件实际重组分别在浅底和深底核对。

在游戏开始界面选择“水墨立绘九部位测试”，可切换男女，并逐件显示或隐藏九个部位。此入口仅用于预览，不写入角色或存档；本套尚未注册到旧固定锚点模板，也未替换正式角色与对话立绘。
