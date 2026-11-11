# 符箓配伍册与鉴看

选中效果后续已统一为整张中灰绿纸面与深墨字，三页和所有选择弹窗同步。当前效果见[选中样式修订](选中样式修订/README.md)；下方保留本阶段原始功能图。

2026-11-09。天工峰进入“符工 · 制符”，顶栏打开“配伍册”。这是原生实装截图，沿用现有纸面控件及[纸墨符笔素材](../符箓纸墨符笔新素材_20261109/README.md)。规则及实现边界见[本轮方案](../../plans/百工技艺/制符/符箓配伍册与鉴看补完_20261109.md)。

## 当前操作

- 制纸、研墨、画符配置均可命名保存、载入、更名、覆盖与删除，并随当前人物存档。
- 载入保留原需求数量；缺货、失效引用或未学技艺不被自动补齐。失效原料和符墨可以单独移除后重选。
- 换谱可查看现行五档固定符效及使用费用。它说明符谱效果，不提前确定本批结果。
- 成品鉴看显示实际字段或实际符效，以及本批采用的技艺、造诣、精力与耗时。

准备页不追加成品字段、预测值或计算说明。保存和载入配伍不收费、不发物品、不推进时间。

## 实机图

| 内容 | 1280×720 | 1920×1080 |
| --- | --- | --- |
| 制纸配伍册 | [查看](1280/talisman_plans_paper_book_1280.png) | [查看](1920/talisman_plans_paper_book_1920.png) |
| 研墨配伍册 | [查看](1280/talisman_plans_ink_book_1280.png) | [查看](1920/talisman_plans_ink_book_1920.png) |
| 画符配伍册 | [查看](1280/talisman_plans_drawing_book_1280.png) | [查看](1920/talisman_plans_drawing_book_1920.png) |
| 覆盖确认 | [查看](1280/talisman_plans_overwrite_confirm_1280.png) | [查看](1920/talisman_plans_overwrite_confirm_1920.png) |
| 更名冲突 | [查看](1280/talisman_plans_rename_conflict_1280.png) | [查看](1920/talisman_plans_rename_conflict_1920.png) |
| 删除确认 | [查看](1280/talisman_plans_delete_confirm_1280.png) | [查看](1920/talisman_plans_delete_confirm_1920.png) |
| 缺料仍保留原需求 | [查看](1280/talisman_plans_paper_missing_1280.png) | [查看](1920/talisman_plans_paper_missing_1920.png) |
| 移除失效原料 | [查看](1280/talisman_plans_missing_raw_replace_1280.png) | [查看](1920/talisman_plans_missing_raw_replace_1920.png) |
| 移除失效符墨 | [查看](1280/talisman_plans_missing_ink_replace_1280.png) | [查看](1920/talisman_plans_missing_ink_replace_1920.png) |
| 未学写法 | [查看](1280/talisman_plans_drawing_unlearned_1280.png) | [查看](1920/talisman_plans_drawing_unlearned_1920.png) |
| 已载入画符配置 | [查看](1280/talisman_plans_drawing_loaded_1280.png) | [查看](1920/talisman_plans_drawing_loaded_1920.png) |
| 护身符固定效果 | [查看](1280/talisman_plans_recipe_hushen_1280.png) | [查看](1920/talisman_plans_recipe_hushen_1920.png) |
| 炎矢符固定效果 | [查看](1280/talisman_plans_recipe_yanshi_1280.png) | [查看](1920/talisman_plans_recipe_yanshi_1920.png) |
| 实际成符鉴看 | [查看](1280/talisman_plans_actual_outcome_1280.png) | [查看](1920/talisman_plans_actual_outcome_1920.png) |

## 验证与资料边界

编译通过，0警告、0错误。配伍册专项使用真实窗口输入，两个窗口尺寸各213项通过、14张截图，共28图。三工艺原生回归两个窗口尺寸各394项通过。专项包括人物数据存读档、记录隔离、显式覆盖、缺货需求保留、失效材料替换、结果与固定战斗目录一致，以及反复查看不重复扣发。

- [1280专项报告](1280/talisman_plans_checks_1280.json) · [1920专项报告](1920/talisman_plans_checks_1920.json)
- [1280生产回归报告](1280/talisman_checks_1280.json) · [1920生产回归报告](1920/talisman_checks_1920.json)
- [编译记录](build.log)

测试使用项目输出目录下的隔离用户资料、测试人物与冻结纸墨夹具；图中库存及造诣是验证用配置，不代表正式角色已有这些物品或技艺。实际成符鉴看截图来自该隔离人物完成的真实制作结算，不是手填结果。

各尺寸目录同时保存运行日志。现有“卸力.tres”存在脚本UID提示，按文本路径回退加载成功；本轮不改动该战斗资源。未增加纸墨字段、原料字段、战斗数值或制作费用。
