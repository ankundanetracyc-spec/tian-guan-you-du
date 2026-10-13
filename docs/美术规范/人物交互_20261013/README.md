# 人物交互居中排版与水墨图标（2026-10-13）

当前按用户最新反馈：过程每行水平居中，保留逐字写出、句尾试探、撤回与显出本行反应的动画。每行一个实际判定点，用短句表现，如“你接话得体，对方愿意多谈几句”“说起真传入门，对方郑重还礼”。同一判定点保留多种句式；相邻独立判定的展示顺序每次变换，收束、阻断与交易交割等步骤保留先后。属性数值、分数门槛与内心动机不展示。

## 交互图标

使用内置 imagegen 生成，未使用 CLI/API 回退。三张独立 PNG 均为 1536×1024、带透明通道，实际显示为 138×92，保持原比例；下方显示简短交互类别。原图保留于生成目录，项目副本如下：

- [相互见礼](../../../textures/ui/social_interaction_20261013/greeting_v1.png)：结识、闲谈、来访与告辞。
- [木剑相交](../../../textures/ui/social_interaction_20261013/spar_v1.png)：非性命切磋。
- [药材与灵石衡量](../../../textures/ui/social_interaction_20261013/trade_v1.png)：求购、交易。

三张素材四角 alpha 为 0；保留柔和水墨边缘，无压扁的印泥底板。实现见 [StoryDialoguePanel.Social.cs](../../../csharp/panel/StoryDialoguePanel.Social.cs) 与[措辞库](../../../csharp/panel/NpcSocialPresentationText.cs)。

## 最终生成提示词

验收：完整 C# 编译 0 警告、0 错误；图形模式人物交互回归 PASS。已查看实际结束画面，短句逐行居中，见礼图无纸面矩形底板、保留原比例，按钮与姓名位置正常。日志 `tmp/npc_social_brief_shuffled_20261013.log`。

### 相互见礼

```text
Use case: stylized-concept. Asset type: transparent game UI icon for a minimalist Chinese cultivation game. Create ONE standalone icon showing two small ancient Chinese scholar cultivators, waist-up, facing one another and performing a polite cupped-hands greeting. No facial features, clear graceful flowing sleeves, simple readable silhouettes, one dark charcoal robe and one muted sage-grey robe. Traditional Chinese ink-wash painting, dry-brush contours, restrained soft grey wash, subtle warm ivory highlights for visibility on a dark ink veil. The figures form a balanced compact wide composition, viewed from the front at a slight angle. This is a tiny symbolic UI vignette, NOT full character portraits, NOT a landscape. At 140x92 display size the greeting gesture must remain legible. Subject occupies most of a 3:2 canvas, small even transparent margin. Truly transparent background, soft ink edges, no solid paper rectangle, no scenery, no banner, no circle/frame, no typography, no Chinese characters, no symbols, no stamp, no glow, no gradients of a UI badge, no ornament. Output only the isolated painted greeting icon.
```

### 木剑相交

```text
Use case: stylized-concept. Asset type: small transparent UI vignette for a Chinese cultivation game. Traditional Chinese ink-wash painting with dry-brush charcoal outlines, soft desaturated grey and sage wash, restrained ivory highlights readable against a dark ink veil. Simple compact silhouette, legible at 138x92 pixels, subject fills a balanced 3:2 canvas with a small transparent margin. Genuinely transparent background, no backdrop/paper, no badge or frame, no text, no characters, no stamp, no glow, no landscape, no particles, no decorative foliage. Subject: two simple ancient Chinese straight wooden practice swords crossing diagonally in a calm balanced X, blade tips up, clearly distinct hilts at bottom, old pale wood and muted grey cloth handles. Non-lethal friendly sparring emblem, no blood, no magic, no aggressive effects. Only two crossed practice swords, isolated.
```

### 药材与灵石衡量

```text
Use case: stylized-concept. Asset type: small transparent UI vignette for a Chinese cultivation game. Traditional Chinese ink-wash painting with dry-brush charcoal outlines, soft desaturated grey and sage wash, restrained ivory highlights readable against a dark ink veil. Simple compact silhouette, legible at 138x92 pixels, subject fills a balanced 3:2 canvas with a small transparent margin. Genuinely transparent background, no backdrop/paper, no badge or frame, no text, no characters, no stamp, no glow, no landscape, no particles, no decorative foliage. Subject: a small ancient Chinese handheld balance scale in dark aged wood and bronze with two shallow suspended pans; in one pan a small tied bundle of medicinal leaves, in the other a few pale rough spirit stones. Clear sparse contour, no abundance of trinkets, no coins or modern money, no table or person. Only the compact balance scale and its few contents, isolated.
```

