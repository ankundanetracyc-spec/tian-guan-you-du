# 符箓选中样式修订

2026-11-09。按[全局最新选中规范](../../../plans/界面与美术/全局界面统一风格规范_20261108.md)，选中时整张原纸面变为中灰绿，文字保持深墨绿，保留纸纹和原纸框；不添加中央矩形、底线或侧线。准备页与选择弹窗采用同一处理。

## 覆盖范围

原生页检查了制符纸、研墨、画符顶栏，三页技艺／写法，留墨功用，选材与质地筛选，符纸、符墨、符笔和符谱选择，以及配伍册三个章节。共用 `CraftingPaperStyle` 的选中染色同步当前网页标准，选中技艺内单独绘制的名称、用途、取舍和标记同步深墨字；没有增加符箓专用染色层。

现行三页网页样稿接入共用选中样式，悬停与按住时保留整纸选中色。旧技艺侧线、留墨功用实底清除；留墨按钮改用已有纸面，保持原紧凑高度。选中小字继承深墨色，样稿入口更新样式版本。

原生既有 `PaperButtonFeedback` 页面的既定选中处理继续保留，本轮没有重做其布局。制作、造诣、资源费用、库存和存档规则均未改动。网页中未实装的应变、纸墨夹具仍是演示，不因本轮样式同步而成为原生功能。

## 原生实机

| 页面 | 1280×720 | 1920×1080 |
| --- | --- | --- |
| 制符纸 | [查看](native/1280/talisman_paper_ready_1280.png) | [查看](native/1920/talisman_paper_ready_1920.png) |
| 研墨及留墨功用 | [查看](native/1280/talisman_ink_ready_1280.png) | [查看](native/1920/talisman_ink_ready_1920.png) |
| 画符 | [查看](native/1280/talisman_drawing_ready_1280.png) | [查看](native/1920/talisman_drawing_ready_1920.png) |
| 配伍册制纸章节 | [查看](native/1280/talisman_plans_paper_book_1280.png) | [查看](native/1920/talisman_plans_paper_book_1920.png) |
| 配伍册研墨章节 | [查看](native/1280/talisman_plans_ink_book_1280.png) | [查看](native/1920/talisman_plans_ink_book_1920.png) |
| 配伍册画符章节 | [查看](native/1280/talisman_plans_drawing_book_1280.png) | [查看](native/1920/talisman_plans_drawing_book_1920.png) |

## 网页选择弹窗

| 选择内容 | 1280×720 | 1920×1080 |
| --- | --- | --- |
| 符纸 | [查看](web/papers_1280.png) | [查看](web/papers_1920.png) |
| 符墨 | [查看](web/inks_1280.png) | [查看](web/inks_1920.png) |
| 符笔 | [查看](web/brushes_1280.png) | [查看](web/brushes_1920.png) |
| 符谱 | [查看](web/recipes_1280.png) | [查看](web/recipes_1920.png) |

网页三页、原料筛选与其余演示状态的完整截图也保存在 `web/`，与原生图分开。

## 本轮验证

- 编译通过，0警告、0错误；[构建记录](build.log)。
- 原生三工艺两档各394项、配伍册与鉴看两档各213项通过；报告分别保存在 `native/1280/`、`native/1920/`。使用隔离人物和用户目录，不打开正式旅程。
- 网页原有流程加选中样式核对共148项通过、17图；另核对两档纸墨笔谱弹窗及选中后的悬停、按住、松开和命中区，共18项通过、8图。[页面检查](web/checks.json) · [弹窗与按压检查](web/interaction.json)。没有脚本错误或缺失资源。
- 共用材质的炼丹原生1280流程134项通过、4图，未开炉、未扣料或推进历法；[炼丹报告](alchemy-1280/alchemy_paper_checks_1280.json)。

运行记录保留现有资源UID按文本路径回退的提示；不将其写成无警告运行。上层图集与旧134项等历史记录保留，当前符箓选中效果以本页为准。
