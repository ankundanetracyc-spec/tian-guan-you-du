# 符箓纸墨笔成品信息

2026-11-09。修复已制成纸墨笔在共用悬停中显示“具体效果尚未配置”、副标题暴露内部分类的问题。现在读取有效冻结结果，纸、墨、笔各显示现行四字段；纸墨附带实际保留的五行。规则边界见[本轮方案](../../plans/百工技艺/制符/符箓纸墨笔成品信息统一_20261109.md)。

行囊、收纳、交易、善功堂、市场和其他复用 `StoragePanel.ItemHover` 的入口采用同一读法，制符已选纸墨笔及产品选择卡也接同一悬停。本轮实机操作覆盖普通行囊和制符选择，不另计商业页面的全页验收。

## 实机图

| 查看内容 | 1280×720 | 1920×1080 |
| --- | --- | --- |
| 行囊符纸 | [查看](1280/talisman_art_inventory_hover_paper_1280.png) | [查看](1920/talisman_art_inventory_hover_paper_1920.png) |
| 行囊符墨 | [查看](1280/talisman_art_inventory_hover_ink_1280.png) | [查看](1920/talisman_art_inventory_hover_ink_1920.png) |
| 青竹符笔 | [查看](1280/talisman_art_inventory_hover_qingzhu_1280.png) | [查看](1920/talisman_art_inventory_hover_qingzhu_1920.png) |
| 静毫符笔 | [查看](1280/talisman_art_inventory_hover_jinghao_1280.png) | [查看](1920/talisman_art_inventory_hover_jinghao_1920.png) |
| 藏锋符笔 | [查看](1280/talisman_art_inventory_hover_cangfeng_1280.png) | [查看](1920/talisman_art_inventory_hover_cangfeng_1920.png) |
| 制符选纸 | [查看](1280/talisman_art_picker_hover_paper_1280.png) | [查看](1920/talisman_art_picker_hover_paper_1920.png) |
| 制符配墨 | [查看](1280/talisman_art_picker_hover_ink_1280.png) | [查看](1920/talisman_art_picker_hover_ink_1920.png) |
| 制符选笔 | [查看](1280/talisman_art_picker_hover_brush_1280.png) | [查看](1920/talisman_art_picker_hover_brush_1920.png) |

图中使用隔离旧物夹具，简介特意为空，字段直接来自有效的成品能力。夹具纸墨未保存五行，因此显示“无”；实际成品按自身留存五行显示，另有木水变体的字段验证。这里没有伪造实际生产的产量或属性。

## 布局和只读范围

保留共用340常规栏宽、96主图标和名称下真实行囊格图。四字段各一行，没有额外计算说明、来源预算、数量或内部路径。纸墨笔不显示战斗底栏；固定战斗符继续使用原有效果、费用、目标和战斗占格说明。

旧简介或旧图标字段无需改写，图标按现有素材只读派生；无效成品明确提示资料失效。真实指针可以打开悬停，信息栏及其子控件透传鼠标，仍可正常选用纸墨笔。查看、切换显示模式和改变查看者造诣不会重算或改变成品。

本轮没有增加原料字段、加工规则、费用、辅纹、主动应变或造诣成长。准备页不恢复未制成的结果字段。

## 验证

编译通过，0警告、0错误。两档成品信息与美术专项各279项通过，共20张原生截图（其中16张为新增悬停图）；三工艺制作、演出与存档回归两档各466项通过。

- [1280成品信息](1280/talisman_art_checks_1280.json) · [1920成品信息](1920/talisman_art_checks_1920.json)
- [1280三工艺回归](1280/production-talisman_checks_1280.json) · [1920三工艺回归](1920/production-talisman_checks_1920.json)
- [编译记录](build.log)

检查包括旧空简介、过时简介、不同冻结纸性和五行、查看者造诣变化、简略／详细切换、失效数据、不合法战斗布局清除，以及普通原料和固定战斗符分派保持；实际窗口核对字段完整、图标尺寸、名称下格图、视口避让及鼠标透传。库存身份、能力JSON、简介、精力、造诣、所学、制作记录及历法保持不变。

日志保留现有资源UID按文本路径回退的提示；本轮无运行错误。历史150项美术记录未包含成品悬停，当前结果以本页279项报告为准。
