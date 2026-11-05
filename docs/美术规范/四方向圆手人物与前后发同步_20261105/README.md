# 四方向圆手人物与前后发同步

**历史方案，已被[正背双视圆手人物最小试玩](../正背双视圆手人物最小试玩_20261105/README.md)覆盖。** 用户最新要求先完成正背两个主体视图、一款前发与一款后发独立合成，不画眼睛；采用双脚上下交替、躯干仅垂直反馈、双手收近及无地平线轻斜俯视场地。下文四方向、无腿脚、三前发×三后发、62×2与461×2均仅为旧阶段记录，不能作为新MVP验证或当前制作目标。

2026-11-05，独立美术原型。用户已确认大头、重发型、无肢体的简化方向，并要求躯干改为锥形筒、相对首稿增加约15%体量。首版只做正面、背面、左侧、右侧四方向，一套玉虚内门常服和三款前发、三款后发的独立组合。

**最新制作优先级：先把首套／第1款前后发做好。** 用户指出侧面过扁，当前优先重绘第1款前发0＋后发0的侧头、后脑和袍身厚度。三前发×三后发的九种组合继续保留已有独立同步能力，但其可切换、可加载不等于其他组合或第一款已完成视觉验收。

**肤色与侧背造型仍在调整，未获最终视觉验收。** 本文记录的工程检查不代表用户认可当前肤色、比例或轮廓。原型不写正式存档，不替换[当前默认鬼谷原规格人物](../鬼谷原规格首套人物_20261105/README.md)，不修改原人物素材、骨骼、战斗规则或默认入口。

## 造型与范围

- 大头，重点保留发型差异；头部、前发、后发、衣身分别呈现。
- 衣身按用户确认的锥形筒方向修订，躯干约增加15%体量是相对首稿的造型要求，不是战斗属性或存档倍率。
- 两手为带袖口纹样的独立圆形块，不连手臂；不画腿脚，不恢复原骨骼肢段。
- 四方向独立绘制、注册。背面按空间归属隐藏前发可见层，但保存编号及图集行仍随前发字段更新。
- 仅覆盖男修内门常服5、前发0／1／2、后发0／1／2，共九种混搭。女修、其他衣服和未知编号显示未制对应局内素材，不静默套用样例。

当前[渲染参数](../../../tools/action_battle_test/pawn_four_way_avatar.gd)如下，单位为原型显示单位；造型修订仍需实际画面核验，不能以常量存在作为完成依据。

| 参数 | 当前值 |
| --- | ---: |
| 头部高度 | 38 |
| 衣身高度 | 48 |
| 圆手直径 | 14 |
| 圆手与衣身间隔 | 4 |

## 与原立绘同源的独立选款

原立绘仍读取[唯一活动部件配置](../../../mods/appearance/ink_three_variants_20261004/appearance_parts.json)。新原型按真实目录解析出的性别、部件ID和源PNG选择本轮图集，不能只比较中文名称或把存档编号直接当文件编号。

| 部件／保存字段 | 保存值与实际ID | 原立绘PNG |
| --- | --- | --- |
| 衣服 `appearance_clothes_style` | 5／`male_clothes_05` | [前衣06](../../../mods/appearance/ink_three_variants_20261004/male/clothing/06.png)＋[同款后领06](../../../mods/appearance/ink_three_variants_20261004/male/clothing_back/06.png) |
| 前发 `appearance_hair_style` | 0／`male_front_hair_00` | [前发01](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/01.png) |
| 同上 | 1／`male_front_hair_01` | [前发02](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/02.png) |
| 同上 | 2／`male_front_hair_02` | [前发03](../../../mods/appearance/ink_three_variants_20261004/male/hair_front/03.png) |
| 后发 `appearance_hair_back_style` | 0／`male_back_hair_00` | [后发01](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/01.png) |
| 同上 | 1／`male_back_hair_01` | [后发02](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/02.png) |
| 同上 | 2／`male_back_hair_02` | [后发03](../../../mods/appearance/ink_three_variants_20261004/male/hair_back/03.png) |

内门保存值5实际对应文件06.png，物品为[玉虚内门常服](../../../resources/entities/items/clothing/玉虚内门常服.tres)。前后发0／1／2各自对应01／02／03.png，改其中一项保留另一项及衣服编号。

前发依次为木簪半束前发、高束拢发、疏风偏分；后发依次为木簪半束长披、玄巾高束长披、散发长披。后发分别具有横木簪半束、黑巾高髻、无髻散披的轮廓差异，本轮分别制作，不能把一张发型换名充作三款。

右侧原立绘直接使用原PNG，保留1024×1536共同画布、零位移和比例1，整幅组合统一缩放，不按每层透明包围框分别居中。层序为后发→后领→脸耳颈→前衣→鼻子→眼睛→眉毛→嘴巴／面饰→前发。后领随同一衣服选项切换，不增加换领槽；空面饰保留其语义层。

## 实时数据与独立预览

[适配器](../../../tools/action_battle_test/pawn_appearance_profile.gd)优先读取当前运行人物的真实外观资源；当前人物不可得时，读取GameStartProfile的只读立绘预览，界面和捕获记录明确来源。自动款式的原保存值保留在source_values，顶层三项appearance字段使用真实目录解析后的款式编号，不回写正式资源。

真实HumanData.changed触发重新解析，同一DTO供给主角、四方向对照及原立绘图层。单改前发只改变前发图集行与原前发PNG，单改后发只改变后发；换成未制作衣服时保留正确原立绘，局内样例明确显示不支持。

房间试穿采用私有Dictionary或Duplicate(true)的Resource草稿，不调用正式外观setter或存档API。明确选择“首版男修内门样例”时标注独立样例并保留最初读取来源，不伪称当前人物。反复改单字段不会无限嵌套capture_source来源历史。

## 当前文件与颜色修复

| 用途 | 文件 |
| --- | --- |
| 独立房间 | [场景](../../../tools/action_battle_test/pawn_four_way_room.tscn)、[房间脚本](../../../tools/action_battle_test/pawn_four_way_room.gd) |
| 组合渲染与选款适配 | [渲染器](../../../tools/action_battle_test/pawn_four_way_avatar.gd)、[适配器](../../../tools/action_battle_test/pawn_appearance_profile.gd) |
| 衣身、无发头部与圆手图集 | [base_parts.png](../../../tools/action_battle_test/art/pawn_four_way_20261105/base_parts.png) |
| 三款前发／后发四方向图集 | [hair_front.png](../../../tools/action_battle_test/art/pawn_four_way_20261105/hair_front.png)、[hair_back.png](../../../tools/action_battle_test/art/pawn_four_way_20261105/hair_back.png) |
| 各款各方向整体注册 | [registration.json](../../../tools/action_battle_test/art/pawn_four_way_20261105/registration.json) |

图集方向列为正面、右侧、背面、左侧；前后发三行各自对应保存值0、1、2。新局内图集不写回原立绘目录。

用户曾指出肤色偏橙、偏暗及侧背形状不符。已修复shader重复乘贴图颜色导致RGB平方、肤色异常加深的问题：从vertex传入tint，在fragment只乘一次。最新反馈为侧面过扁，当前先修第一款的侧头、后脑及袍身厚度，并继续核对肤色、侧面前发分界和侧背注册；修复shader不等于这些画面已获认可。

## 工程检查与证据

| 检查 | 结果与报告 | 实际范围 |
| --- | --- | --- |
| 数据适配与独立字段 | 根项目／独立项目各62项通过：[根报告](../../../output/pawn_four_way_20261105/profile_adapter_root.json)、[独立报告](../../../output/pawn_four_way_20261105/profile_adapter_nested.json) | 根项目使用真实C# HumanData；4次changed驱动实际AtlasTexture区域和原PNG；九混搭×四方向、未知外观拒绝映射、私有草稿与来源稳定 |
| 真实房间控件与组合 | 根项目／独立项目各461项通过：[根报告](../../../output/pawn_four_way_20261105/ui_room_qa_root.json)、[独立报告](../../../output/pawn_four_way_20261105/ui_room_qa_nested.json) | 同一DTO供给主角／四方向／原立绘；独立前后发与显隐、移动／暂停、读取／重置、未支持款与明确样例 |

适配器根项目核对80份用户存档／预设、9份活动立绘源文件SHA保持，原测试资源磁盘SHA也保持。独立项目没有正式C# autoload，使用明确标记的导出Resource夹具，不能把该部分冒称真实HumanData验证。脚本为[适配器冒烟](../../../tools/action_battle_test/smoke_pawn_appearance_profile.gd)与[房间冒烟](../../../tools/action_battle_test/smoke_pawn_four_way_room.gd)。

后续方向捕获暂按以下位置留档。目录名沿用预先约定，但当前不固定“最终图”；首套第一款的侧背仍在重绘，文件未到位或尚未复核时不作为已有视觉验收证据：

- [正面房间全景](../../../output/pawn_four_way_20261105/final_directions/front/room_overview.png)
- [背面房间全景](../../../output/pawn_four_way_20261105/final_directions/back/room_overview.png)
- [左侧房间全景](../../../output/pawn_four_way_20261105/final_directions/left/room_overview.png)
- [右侧房间全景](../../../output/pawn_four_way_20261105/final_directions/right/room_overview.png)

first_room、registered_room、clean_palette_room、profile_cap_room保留阶段画面，不用早期方向、旧肤色或旧注册代替当前第一款候选。通过数仅说明报告中的工程范围，不构成用户对美术的最终认可。
