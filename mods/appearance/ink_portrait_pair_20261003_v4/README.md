# 男女水墨立绘基础套 v4

本轮用户参考的男女基础套，各九张同原点 1024×1536 透明 PNG。当前面饰为全透明的无须/无妆默认款，扩款数量尚未完成。制作与验证说明见 [统一风格基准](../../../docs/美术规范/男女立绘统一风格基准_20261003.md)。

## 使用与预览

从游戏开始界面进入“水墨立绘九部位测试”，选择男女，勾选部件检查组合。本套仅接入该测试入口。

- [男女组合](preview/pair_light.png)
- [男性九部位](preview/male_nine_parts.png)、[女性九部位](preview/female_nine_parts.png)
- [男性游戏画面](preview/male_game_test.png)、[女性游戏画面](preview/female_game_test.png)
- [固定锚点与来源变换](rig/portrait_rig_v4.json)
- [部件清单](portrait_manifest.json)、[透明度检查](qa_report.json)
- [前发生成提示词](source/front_hair_prompts.json)、[其他生成与被拒绝候选](source/generation_manifest.json)

`male/`、`female/` 内的 PNG 才是实际九件素材；完整立绘和 `preview/` 中的图片是这些九件的实际合成。完整稿不能替代拆件检查。

绘制顺序：后发 → 脸型（含耳朵、延长颈部）→ 衣服 → 眼睛 → 眉毛 → 鼻子 → 嘴巴 → 面饰 → 前发。导出位置为零位移；不按不透明边框重新居中。

头发用内置图像生成工具独立绘制；五官墨色去底、原位拆分与整层配准使用用户已授权的 Python。图像来源只包括本轮用户参考和本轮新画的头发，鬼谷官方示例仅用于学习透明度及配准经验。
