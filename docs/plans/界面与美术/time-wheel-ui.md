# 昼夜时轮 UI

> 历史方案，非当前 UI 规则。2026-10-26 起，玩家可见的时间只显示年月日，不显示数字小时、地支时辰或具体钟点；本文件中关于盘内钟点读数、时辰文字及时间控制的内容均已被现行[历法显示规则](../世界与探索/游戏历法与纪年显示.md)取代。当前 `SundialClock` 只绘制月内日期刻度和昼夜色彩，不显示钟点。

## 设计

完整水墨圆盘以屏幕左上角为圆心，另外四分之三由屏幕自然裁切。可见半径从 184 调整为 310 像素，放大约 68%；时间、日期、天气和运行状态直接排在盘内宣纸留白处，文字不随盘面旋转。移除下方独立读数纸牌，影针收在刻度附近，避免穿过文字。固定影针朝向可见象限中央，刻盘每个游戏月（30 天、720 小时）转一周，每天 12°、每小时 0.5°，比日轮慢 30 倍。外圈为 30 个日期刻度，标注 1、5、10、15、20、25、30 日。盘面读取 GameTimeMgr 的整点与小数累积值，因此倍速、暂停、跨日都与实际游戏时间一致，不使用独立的视觉计时器。日期刻度文字保持正向以便阅读。

这是一件受日晷启发的游戏时辰仪，不是真实太阳时测量仪。真实日晷通过太阳产生的晷针影子读时；本 UI 使用转盘与固定指针，夜晚仍可使用。月历外圈采用统一墨色；盘内文字与针尖颜色表达游戏中 06–18 时的昼、18–06 时的夜，是等长昼夜的简化规则，不模拟真实经纬度、季节日照或均时差。

十二时辰按子时 23:00–01:00、丑时 01:00–03:00……划分。时辰只在盘内读数显示，外圈日期刻度对应各日零点。数字读数显示游戏时分，日期仍沿用项目每月 30 日的游戏历法。

- 日晷原理：https://www.rmg.co.uk/stories/time/sundials
- 十二时辰：https://www.ccdi.gov.cn/lswhn/wenhua/wenyuan/201907/t20190719_27179.html

## 素材

使用内置 image_gen 生成，保留原始 PNG 和透明通道；没有使用 CLI。文字、指针、昼夜标记和交互由 Godot 绘制。

- `textures/ui/time/sundial_disc_v1.png`：完整圆盘。
- `textures/ui/time/ink_tab_v1.png`：外缘暂停 / 倍速纸签。
- `textures/ui/time/ink_panel_v1.png`：早期独立读数纸牌素材，保留源文件，当前界面不再使用。

## 最终生成提示词

### 圆盘

Use case: stylized-concept. Asset type: production game UI rotating sundial disc texture, Chinese shuimo cultivation game. Exactly one COMPLETE CIRCULAR FLAT DISC, perfectly centered at canvas center, front orthographic view, diameter 94 percent of square canvas, transparent background outside circle. Warm pale aged rice paper face, delicate Chinese ink wash mottling, charcoal hand-painted concentric outer rings and 48 evenly spaced radial tick marks, every fourth tick slightly longer. Subtle gray-green washes near outer edge, generous clean pale interior. A refined ancient Chinese sundial/calendar wheel drawn with brush ink on paper. NO center needle, NO shadow, NO text, NO numerals, NO characters, NO pictograms, NO scenery, NO stand, NO perspective, NO gold, NO 3D. Circle must be complete, symmetrical, crisp enough for small UI, restrained handmade brush texture. Game engine adds rotating Chinese time labels and fixed gnomon shadow separately. Genuine transparent PNG.

### 纸签

Use case: stylized-concept. Asset type: single transparent PNG 2D game UI button background. A single BLANK small square Chinese rice-paper seal tab, width equal to height. Flat front view. Fill 90 percent of square canvas. Genuinely transparent background outside. Warm ivory rice paper with subtle fibers, hand-torn slightly irregular square silhouette, fine charcoal ink brush border, delicate gray-green ink stain only along edges. Center must be blank light paper, clean for game-engine text. Traditional Chinese shuimo ink-wash style, restrained and elegant. Designed as a tiny clickable tab hanging from edge of larger paper calendar panel. NO text, NO letters, NO icons, NO calligraphy, NO surrounding items, NO drop shadow, NO gold, NO bevel. Exactly ONE compact SQUARE tab, NOT a banner.

### 横向宣纸

Use case: stylized-concept. Asset type: production 2D game UI texture, single reusable blank edge-tab button for a Chinese ink-wash cultivation game. Create ONE small horizontal paper-and-ink tab, straight-on flat orthographic view, isolated on genuinely transparent background. Shape: about 1.6:1 width-to-height compact rectangular seal/paper slip, slightly irregular handmade rice-paper edges, quiet ivory parchment center for legible dark text to be overlaid by game engine, charcoal dry-brush outline, subtle black ink bleeding at corners, delicate desaturated gray-green wash on outer edges. Art should fill canvas with only 3% transparent padding. No text, no numbers, no symbols, no other objects, no scene, no mockup, no grid, no cast shadow, no glossy bevel or gold frame. Refined traditional Chinese shuimo brushwork with paper fibers, readable silhouette at 42x30 pixels. Deliver a single transparent PNG asset.

## 验证

.NET 编译；Godot 实际渲染；暂停、继续、倍速选中、真实点击命中；子时跨午夜边界；连续小数时间跨日；1× / 8× 时间推进比例；暂停时小数时间冻结。

## 月盘修订：避让任务与清除旧刻度

任务卡移至月盘右侧（x = 表盘半径 + 60），保留安全间距。盘内时间只显示时辰与小时，小数时间继续驱动月盘转动，但不再显示分钟。

当前底图为 `textures/ui/time/month_dial_clean_v2.png`，使用内置 image_gen 编辑原始表盘，清除全部烘焙刻度。实际 30 格刻度由引擎精确按 12° 间隔绘制，每月 30 天；底图不再包含另一套无法对齐的刻度。

编辑提示词：

Edit target: attached full circular Chinese ink wash dial texture. Remove EVERY radial tick mark from the outer annular band, replacing all tick marks with clean matching rice paper texture. Keep the complete centered circular outline, concentric circular brush rings, paper color and grain, green-gray ink washes, dimensions and transparent background outside the disc unchanged. The entire annular band must have ZERO radial lines, ZERO graduation marks, ZERO numbers, ZERO writing. This is a production base texture for a monthly game clock; engine will add precisely 30 evenly spaced date ticks, so baked-in ticks must all be removed. Preserve transparency. No other changes.
