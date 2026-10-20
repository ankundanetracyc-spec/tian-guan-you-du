# 炼丹围炉四角色界面（2026-10-19）

现行规则见[槽位配伍主案](../../plans/炼丹槽位式自动配伍新案_20261019.md)顶部最新修订。入口为首页「试演与预览」中的「炼丹配伍」，使用 `tscn/scene/AlchemyPreviewScene.tscn`。这是开发试演：当前默认造诣36、灵识24、精力100，未接正式角色库存、精力扣除、历法提交与丹炉耐久。

## 原有丹炉与背景

- 背景：`textures/ui/alchemy_room_20261017/wall_only_front_v8.png`。
- 后层：`textures/ui/alchemy_room_20261017/furnace_rear_half_soft_v13.png`。
- 前层：`textures/ui/alchemy_room_20261017/furnace_front_half_soft_v13.png`。
- 通过原 `AlchemyFurnaceView` 正面二维组件合成，保持原锚点、前后遮挡和脚底对齐，不重新生成炉体。

## 本轮选用素材

使用内置 imagegen 生成，保存进项目；所有文字由引擎绘制，不烘焙进素材。

|素材|项目路径|用途|
|---|---|---|
|竖版药笺|`textures/ui/alchemy_20261019/role_portrait_v3.png`|君臣单味卡、佐使分组底材及悬停小纸笺|
|窄长用度笺|`textures/ui/alchemy_20261019/batch_ledger_v3.png`|右侧整炉成本与耗时|
|横向拣药纸|`textures/ui/alchemy_20261019/role_paper_v2.png`|选药和丹方按钮底材|
|丹方卷|`textures/ui/alchemy_20261019/formula_scroll_v2.png`|左下丹方入口，卷图等比完整显示|
|既有淡墨笔触|`textures/ui/alchemy_20261019/brew_brush_v1.png`|炼丹按钮|

纸面底材按目标卡面适配，取消把素材透明边距当作固定像素九宫边距的错误用法；单味卡约226×270、多味组约284×304、用度笺约202×684设计像素。全屏通过1672×941设计画布等比缩放。

药材图标统一使用 `ExpandMode.IgnoreSize` 与 `StretchMode.KeepAspectCentered`，保持资源自身比例、全图可见、居中留白。大卡图标区域与多味小图区域分开设置；图标不随纸面底材拉伸。

## 交互

左侧君、臣各一种；右侧佐、使各最多三种。造诣32／68／203／508分别解锁第二使、第二佐、第三使、第三佐。锁定位收成短笺，空置的已解锁位显示「添药」，填入后显示紧凑材料行；全部展开仍不遮住丹炉主体。造诣在场景导出属性 `Attainment` 读取，并传入模拟器，准入、掌控率和药位限制使用同一个值。

份数使用独立加减按钮，每味1—4整份，总计8份；同种材料不重复占位。右侧只显示种数、总份数、灵识占用、精力消耗与耗时单值，炼丹后才揭晓结果。耗时按用户确认公式计算，开发试演不提交正式历法。

药材详情使用局部悬停纸笺，短暂停留后出现、移出收起；开启选药、丹方或结果窗口会收起。内容包括材性、五行、阴阳、功用品级，以及当前药位可发挥／不发挥的功用。君位只将已登记成品核心作为识别功用，不把君药自带的聚等标成可在君位发挥。禁用原生巨型通用提示框。

## 检查与边界

- 编译：0错误、0警告。
- 解锁边界：造诣8、31、32、67、68、202、203、507、508依次可用4、4、5、5、6、6、7、7、8味；UI与计算层均有资格检查。
- 实际指针点击：炼丹按钮、份数加号可响应；总量到8份后再次加量仍保持8份，且不会误触选药。
- 造诣8的四角色四味样方：青芽多味方成回春丹4枚、丹效86；藻丝多味方成通脉丹3枚、丹效76；泥脉木水方成静心丹4枚、丹效66。上述结果是固定试演资源下的检查，不表示所有九套样方的多路线高效覆盖均已验收。
- 八味布局已实际选入并截图，佐、使三行不裁切；检查时排除引擎内部滚动条纹理，不把其拉伸方式误判为材料图标。
- 后续检查悬停、选药截图，未见操作异常；退出仍有原有纹理释放提示及部分旧技能资源UID回退警告，未改动无关战斗资源。

实际画面：

- [四味入门，造诣8](../../../outputs/alchemy_ui_beginner_20261019.png)
- [默认试演档，造诣36](../../../outputs/alchemy_ui_preview_20261019.png)
- [八味展开，造诣508](../../../outputs/alchemy_ui_eight_herbs_20261019.png)
- [悬停纸笺](../../../outputs/alchemy_ui_hover_20261019.png)
- [选药窗口](../../../outputs/alchemy_ui_picker_20261019.png)

## 素材提示词记录

竖版药笺：`One blank Chinese watercolor game UI paper CARD background, portrait aspect ratio 4:5. Ivory xuan-paper fiber, thin natural deckled edge, very pale gray-green wash only at extreme corners, almost uniform light ivory center. Flat frontal 2D. Paper fills 98% of the frame, only 1% transparent padding around it. TRUE transparent exterior, NO shadow haze, no diffuse glow. No writing, no text, no frame/border, no symbols, no pictures, no objects, no gold. Needs to support dark UI text and herb icon composited later. Consistent matte Chinese ink-wash cultivation game.`

用度笺：`One blank long narrow Chinese watercolor game UI paper NOTE background, exact tall portrait aspect ratio 1:3. A single uninterrupted ivory xuan-paper sheet with delicate straight gently deckled edges, warm paper fibers and barely visible gray-green wash only at very bottom corners, nearly uniform light ivory writing surface. Strictly flat 2D front view. Paper fills 98% of frame, 1% transparent padding. TRUE transparent exterior, NO hazy shadow or glow. No text, no symbols, no frame or border, no drawing, no folds, no objects, no gold. This is a clean tall sidebar background for legible game resource values.`

丹方卷：`ONE standalone 2D handpainted Chinese ink-wash game UI icon: a small partly unrolled ivory xuan-paper scroll seen from the front, short dark wooden rollers, muted jade-gray binding, a few sparse gray ink mountain strokes on paper. Warm paper fiber, watercolor edges, delicate aged but clean. No text, NO Chinese characters (engine will add label), no letters, no border, no glow, no pedestal, no realistic perspective or 3D. Compact horizontal silhouette about 4:3 aspect, completely visible with generous transparent padding, truly transparent background. Match antique ink-and-watercolor cultivation game.`

横向拣药纸：`Production game UI asset: ONE EMPTY ivory handmade Chinese xuan-paper slip on a truly transparent background, front-facing 2D flat watercolor. Wide horizontal 3:2 sheet, subtle gently deckled edges, pale warm fiber texture, sparse soft gray-green ink wash only at corners. Clean light center for readable Chinese UI text added by engine. Restrained subtle natural soft edge shadow. Absolutely NO text, no letters, no symbols, no gold border, no drawn frame, no plants, no objects, no perspective. Sheet fills 94 percent of canvas with narrow equal transparent margins. Match muted Chinese ink-wash cultivation game, elegant matte watercolor.`
