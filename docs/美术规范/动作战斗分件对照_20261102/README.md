# 动作战斗人物与原版骨骼对照（2026-11-02）

当前玉虚试玩以本文末尾[长袍约束步态、下摆惯性与自然手势 v21](#2026-11-03-玉虚长袍约束步态下摆惯性与自然手势-v21当前试玩)为准。用户确认普通移动一起减速，恢复小步幅、慢步频；待机手臂自然放低，动作准备时再举主手。v20的重叠袍、脚影接地和前后倾保留。v11—v20段落保留为历史过程，不再作为当前步幅、移速或待机举手标准。

用户要求参考本机《鬼谷八荒》战斗小人的部件和骨骼动画，素材改为项目自己的衣装，脸部不画五官。当前以本项目已实装的碧岚云叶袍为衣装参考重新生成透明分件，并在独立试玩场拼接；不是复用原已实装人物的原像素，也没有替换正式游戏人物。

## 原游戏资源核对

从本机安装目录的 `guigubahuang_Data/resources.assets` 只读检查战斗人物 `battle/human/10002`、人物根节点和对应 AnimatorController；该资源包含衣身、头发、脸、两臂、双腿及骨骼挂点。找到 `Idle`、`Run_New1`、`Run_New2`、`AttackIdle`、`AttackRun`、`Step1` 动画。当前仅将对应原始贴图、挂点和曲线复制到独立试玩的 `art/guigu_reference/` 作本机对照，不用于正式游戏。`animation_curves.json` 保存曲线的时间、绑定路径和原始多项式系数；部分路径或蒙皮信息仍未解析。用户明确要求直接对照原作，此次测试资料范围覆盖旧文档中的“不导入原图”研究约定，但不等于正式素材发布授权或正式游戏接入。

## 试玩实现与限制

- 默认显示项目自有衣装贴图；G 可切到本机原版贴图对照。试玩将原版层级、权重、bind pose、mesh 与动画曲线载入独立 Godot 场景，不改这些骨架数据。
- 运行时重建 71 个 Transform 节点、32 个 SpriteBone 挂点和 8 个可见 renderer，其中 7 个网格使用骨骼权重；头脸保持无五官，头部皮肤网格沿原层级定位。
- 动态输入目前驱动 `Idle`、`Run_New1`、`AttackIdle` 和 `Step1`。数据还载入 `Run_New2`、`AttackRun`，但试玩尚未切换到这两段；不代表完整复刻原动画状态机。
- 独立试玩使用临时敌人、技能与反馈；不接角色装备、性别外观、心法、正式遭遇或战斗结算。人物约 80 px 的预览只用于检查侧身比例与色块，不代表正式战斗接入。

[原版对照试玩截图](原版对照.png) · [自制云叶袍静态截图（旧临时动作）](云叶袍候选.png) · [自制跑步关键帧](跑步关键帧.png) · [自制攻击关键帧](攻击关键帧.png)。放大图只用于检查分件，不代表正式显示尺度。

运行基础战斗冒烟通过，并检查跑步、攻击曲线输出非零；Godot 窗口抽查跑步两个时刻与出手关键帧。用户仍需实机评价风格、拼接和运动；未完成逐帧人工验收、网格蒙皮及正式战斗回归。

## 2026-11-02 参考原分件重生 v2

按用户要求参考本机原版的窄身侧视、垂手、衣身与腿部分件，结合项目碧岚云叶袍造型重新生成透明部件图 `tools/action_battle_test/art/bilan_puppet_parts_v2.png`；面部留白，保留 v1。试玩默认已换 v2，继续由已提取的部分原动画关节曲线驱动；为适配刚性部件调整手臂摆幅与头颈接合。G 仍可查看原版人物对照。

[实际尺寸试玩截图](云叶袍v2试玩.png) · [待机放大](云叶袍v2待机放大.png) · [跑步放大](云叶袍v2跑步放大.png)。Godot 画面抽查和技能／碰撞／动作曲线冒烟通过；未完成原版完整蒙皮、逐帧动作验收或正式游戏接入。

## 2026-11-02 碧岚云叶袍清爽版 v3

按本轮参考将同一套碧岚云叶袍收敛为小比例侧身清爽国风：衣袍改用低饱和玉绿与连贯象牙内襟，云叶收为少量大形，脸部不画五官。通过七张替换纹理沿用原 alpha 轮廓和 UV；没有移动骨骼、网格、权重、bind pose 或动作曲线。测试素材只供独立试玩，不替换正式人物或行囊服装图。

[80 px 远景可读性对照](../../../tools/action_battle_test/art/own_guigu_fit/distant_readability_preview.png) · [静态骨架锚点对照](../../../tools/action_battle_test/art/own_guigu_fit/static_layout_alignment_preview.png)。

运行方式：双击 `tools/action_battle_test/启动动作试验.cmd`，或从项目根目录运行 `godot.cmd --path tools/action_battle_test`。WASD 移动，鼠标左键平击，空格错步，鼠标右键/Q/E/R/F 使用五槽，Tab 进入技能槽交换，G 切换本机原版贴图对照。

本轮执行了 `build_own_parts.py` 与 `smoke.gd`。冒烟检查确认 71 个层级节点、32 个 SpriteBone、8 个网格（7 个加权）、26 个骨骼绑定，六段曲线已载入；跑步腿骨骼和攻击手臂曲线输出非零，项目/原版贴图切换通过。Godot 窗口观测 3 秒，0 错误、0 警告。已验证 Idle/Run_New1/AttackIdle/Step1 的场景选择与曲线读取；未逐帧验收全部六段、每个动作下的衣物接缝、UV 边缘及正式战斗场景。

## 2026-11-02 碧岚骨骼分件实验（独立入口）

在保留「动作战斗试玩（独立样例）」默认旧蒙皮显示的基础上，新增「动作战斗试玩（新分件实验）」菜单入口，加载 `tscn/scene/ActionBattleBilanExperimentScene.tscn`。实验场使用新裁切的八个碧岚云叶袍部件，分别挂到原骨架头、衣身、远近手臂及小腿节点；沿用原 `Idle`、`Run_New1`、`AttackIdle`、`Step1` 骨骼曲线。实验分件的纯视觉根节点比例为 **0.96**，旧项目蒙皮和原版对照仍为 **0.72**；不改角色逻辑位置、攻击距离或碰撞判定。H 可切换新分件／旧项目蒙皮，G 可暂看原版贴图对照。头部侧脸留白，无眼、鼻、口等五官。此为试玩素材，未覆盖旧入口、旧贴图、正式人物或战斗规则。

复核四动作时发现此前截图驱动的名称判断未匹配带 `experiment_` 前缀的姿态名，导致旧截图实际上都采到待机。已修正驱动，在动作关键时刻分别采样待机 0.817s、跑动 0.267s、攻击 0.067s、错步 0.083s；`smoke.gd` 对手臂、前臂和腿部局部角度签名作差异断言，四种姿态均通过。窗口测试图：实际战场尺寸的[待机](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_idle_v3.png)、[跑动](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_run_v3.png)、[攻击](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_attack_v3.png)、[错步](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_step_v3.png)；放大检查用图：[四动作待机放大](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_idle_closeup_v4.png)、[跑动放大](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_run_closeup_v4.png)、[攻击放大](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_attack_closeup_v4.png)、[错步放大](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_step_closeup_v4.png)。

`dotnet build Stacklands.csproj --no-restore` 构建成功，0 警告、0 错误；旧试玩冒烟全部通过，并断言 project/reference 比例保持0.72、experiment为0.96，攻击距离仍为70；新实验场在 Godot Mono 窗口运行 2 秒通过，0 运行错误、0 警告。放大后的[待机](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_idle_actual_v5.png)、[跑动](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_run_actual_v5.png)、[攻击](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_attack_actual_v5.png)、[错步](../../../tools/action_battle_test/output/action_battle_bilan_standalone_experiment_step_actual_v5.png)截图显示角色仍完整落在战场中，未遮挡 HUD、技能槽或敌人。新分件的绑定、接缝及全部时序仍需继续实机评审；当前已具备从首页可见进入的独立试玩方式，不设为默认。

## 2026-11-02 实验人物动作修订

实验人物的近臂肩点随头颈重定位，双腿骨链缩至原来的 80%，远腿移至衣身前以保持步态可读；跑动衣摆的主摆幅收小，后摆按腿部曲线延迟 0.06 秒采样。上述调整仅作用于实验外观；战斗距离、费用和碰撞不变。独立试玩窗口采集并目视检查了 4 个待机、5 个跑动、3 个攻击和 3 个错步时刻，图见 `tools/action_battle_test/output/action_battle_bilan_standalone_review_*_closeup.png`。双腿交替和肩部连接比前一版清楚，但个别跑动姿态腿部与衣摆仍有交叠、衣摆轮廓仍较宽，尚需用户实机评价；不宣称完成逐帧验收。

`dotnet build Stacklands.csproj --no-restore` 通过；独立试玩技能／碰撞冒烟通过；Godot Mono 分别启动首页与新分件场景通过。实验新增两根衣摆骨，骨架共 73 节点；实验模式 28 个绑定，切回旧外观后恢复原来的 26 个绑定。前襟单独遮挡腿部，冒烟检查覆盖外观切换。旧项目蒙皮与原版贴图仍可切换。

## 2026-11-02 前后衣片实验（进行中）

依据参考视频连续帧，保留原有跑步步幅，衣身裁出后片并将新生成的前片单独绑定，中间绘制双腿；前片试用裁切的碧岚衣料，沿现有网格权重变形。只作用于独立实验外观，旧外观与战斗规则不变。窗口采集左右朝向的待机、五个跑步相位、攻击和错步，见 `tools/action_battle_test/output/action_battle_bilan_standalone_review_*`；独立试玩 smoke 与项目编译通过，主菜单无窗口启动通过。

逐层隐藏截图确认跑步后伸近腿由前摆遮断；现将双腿大腿各分一层，按髋关节角度渐变显示到前摆之上。左右方向的采样中后伸腿已连通，步态及前摆仍需用户实机评价；尚未逐帧检查完整动作和动作切换。新增 smoke 覆盖大腿层显现与切回旧外观时隐藏。

## 2026-11-02 原创行旅者独立试作

依据用户要求，不再以碧岚作为新人物主视觉；新增首页「试演与预览 → 动作战斗试玩（原创行旅者）」，旧独立样例及碧岚实验均保留。新角色为墨蓝／米白衣装的无五官行旅者，素材由内置 imagegen 新生成，源图 `tools/action_battle_test/art/original_wanderer/source.png`，切成头、上衣、袖、双腿及前后衣片。提示词要点：侧身正常成人比例、小头、交领上衣、分离长袍前后片、深色裤靴；各分件透明分离、无五官和文字。新视觉是独立髋／膝关节与衣片运动程序，并非沿用原版动作曲线；G 可对照本机原版，H 切换到保留的碧岚实验。战斗逻辑、碰撞和数值未改。

运行窗口在左右两个方向分别采集待机4、跑动5、攻击3、错步3个时刻，文件 `tools/action_battle_test/output/original_wanderer_standalone_review_*`。放大检查后调整了头颈间隙；腿部可分别摆动，后衣片位于双腿之后。当前仍可见刚性关节和衣片拼接感，连续动作、动作切换及与参考视频的一致程度尚需用户实机评审，不代表已达到最终美术质量。`dotnet build Stacklands.csproj --no-restore` 0警告0错误；旧动作试玩 smoke 通过；主项目新场景无窗口启动通过。截图为独立试玩窗口采样，非正式战斗接入。

## 2026-11-02 原创行旅者接回原骨架（未通过视觉验收）

应用户此前的要求，新角色改为加载导出的原骨架和 `Idle`、`Run_New1`、`AttackIdle`、`Step1` 曲线，腿使用原 SpriteSkin 权重，头和手臂绑定原骨骼，前后衣片使用原衣摆网格及新增衣摆骨。为适配身形，双腿骨链缩至0.8、跑步髋角度缩至0.72；上身跑步转角作稳定化处理。之前的独立程序动作脚本保留为试作对照，但新菜单入口现运行原骨架版本。G／H 仍可切换旧对照外观。

Godot 窗口重新采集双向各 4 待机、5 跑步、3 攻击和 3 错步时刻，见 `tools/action_battle_test/output/original_wanderer_rig_standalone_review_*`；比较图像确认动作时刻并非同一帧。实拍仍未合格：[跑步放大](../../../tools/action_battle_test/output/original_wanderer_rig_standalone_review_run_t145_closeup.png)可见手臂连接分离，腿和衣摆拉扯、后摆轮廓杂乱。新素材形态与原网格 UV、权重和关节尺寸不匹配，仅缩放贴图不能可靠解决，后续需按实际网格重画分件或重新制作蒙皮。当前不宣称满足参考视频动作效果。构建 0 警告 0 错误，旧试玩 smoke 与主项目新场景无窗口启动通过；未完成视觉验收或正式战斗接入。

## 2026-11-03 玉虚内门常服长袍修订

首页「试演与预览 → 玉虚内门常服试玩」现进入独立人物场景；场内标题同步为「玉虚内门常服 · 原骨架蒙皮试玩」。角色以[男内门常服立绘](../../../mods/appearance/yuxu_daily_drafts_20261005/preview/male_neimen.png)为衣着依据，保留米白衣身、青灰交领和腰带、淡云纹及无五官头部；采用真实成人比例。试玩保留原骨架及 Idle、Run_New1、AttackIdle、Step1 动作曲线，衣摆使用两组新增骨链和原创蒙皮；只修改该独立外观，旧试玩、战斗机制和正式战斗未改。

原衣片带独立腰头及粗重边框，跑动时像窄围片。本轮用内置 imagegen 参考原分件和内门立绘重绘软质衣片，移除独立腰头与重边，再做透明背景提取；运行素材为 `tools/action_battle_test/art/original_wanderer/yuxu_neimen_parts_v4.png`，配置见 `skin_v2.json`。前后摆从腰部向下展开，分别随原衣摆及近远腿曲线延迟、分段变形；生成的 v3 带背景残影，保留但未用于运行。生成提示要点：只改两块衣片，米白、细青边、淡云纹、连续软袍，其他分件与位置保持；第二次提取透明背景。

窗口对左右各 4 个待机、5 个跑步、3 个攻击及 3 个闪避时刻进行采样并目视检查：[待机](../../../tools/action_battle_test/output/yuxu_neimen_skin_v5_idle_contact.png)、[跑步](../../../tools/action_battle_test/output/yuxu_neimen_skin_v5_run_contact.png)、[攻击](../../../tools/action_battle_test/output/yuxu_neimen_skin_v5_attack_contact.png)、[闪避](../../../tools/action_battle_test/output/yuxu_neimen_skin_v5_step_contact.png)。跑动后摆可见展开与回落，灰底和原重边已消除；这些采样不能代替完整动作切换及连续实机评价，视觉效果仍待用户确认。

专用蒙皮检查通过 6,984 项，覆盖 96 个动作相位、双向镜像、原骨曲线、权重和外观切换；独立试玩基础 smoke 通过，窗口采样日志无错误。`dotnet build Stacklands.csproj --no-restore` 通过，0 警告、0 错误；Godot Mono 无窗口启动主菜单与独立人物场景通过。先误用普通非 Mono Godot 启动主项目时无法加载 C#，该次不计为通过；已换项目自带 Mono 版本复验。


## 2026-11-03 玉虚内门常服侧面与清晰度修订（阶段记录）

按用户要求，使用内置 imagegen 以[内门常服立绘](../../../mods/appearance/yuxu_daily_drafts_20261005/preview/male_neimen.png)及前一版分件为依据，重绘右侧身头、躯干、双袖、手、腿和前后衣片；整个人物左向时整体镜像。头部不绘五官，青白交领、腰带和简洁云纹保留。运行图为 [v7 透明分件图](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_parts_v7.png)，配置为 `tools/action_battle_test/art/original_wanderer/skin_v2.json`，此前 v4—v6 素材保留。提示词要点：各部位严格同向侧视、袖手相接，保留衣装配色和轮廓，简化纹样以适应小尺寸，不画五官。

图集实际为 1536×1024 像素，躯干有效画面约 183×404 像素，双臂各约 148×450 像素，并非更高像素尺寸的新图。角色在当前战场画面约 80 像素高；将云纹精简为较大的色块以减少远景杂点。仍使用 mipmap 缩小过滤，并采用 -0.65 的细节偏置；对照关闭 mipmap 会在衣纹及轮廓产生明显噪点。新画面相较前版更简洁、轮廓更粗，与原立绘的精细笔触存在取舍，不能视为完成最终美术验收。

左右方向各采集待机 4、跑步 5、攻击 3、闪避 3 个相位，窗口采集正常退出：[实际镜头攻击](../../../tools/action_battle_test/output/yuxu_profile_v7_full_standalone_review_attack_t067.png)、[攻击放大](../../../tools/action_battle_test/output/yuxu_profile_v7_full_standalone_review_attack_t067_closeup.png)、[左向跑步](../../../tools/action_battle_test/output/yuxu_profile_v7_full_standalone_review_run_t145_left_closeup.png)。逐像素检查显示攻击帧手与袖口相接；深色袖口在灰色场景上对比仍有限。以上仅为离散采样，连续动作、切换和最终清晰度仍需用户实机评价。

专用蒙皮检查通过 7,010 项，覆盖 96 个动作相位、双向镜像、原骨曲线、权重与外观切换；基础试玩 smoke 及 Godot Mono 主项目独立试玩场景无窗口启动通过。正式战斗未接入。

## 2026-11-03 玉虚内门常服精细袖手与长袍连接修订（阶段记录）

用户实机指出手部形态不对，走动时袖手与下部长袍有脱节。本轮保持现有玉虚内门常服、严格侧身、无五官和整体左右镜像；头、躯干、双腿、前后袍片仍使用 v7 原像素，只重做双袖手素材与相关蒙皮映射，并修订袍片腰部连接。原骨架动作曲线、角色显示比例、跟随镜头、战斗规则与旧外观入口均不因本次美术修订而改变，正式战斗未接入。

### 精细袖手与腕点

内置 imagegen 以 v7 衣装为颜色与款式参考，生成 [v8 双袖手透明图](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_arms_v8.png)，第二次调用仅提取透明背景；[两次完整提示词及接入记录](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_arms_v8.prompt.txt)随素材保留。图集实际1536×1024，双臂各约220×970像素；米白织物、青灰袖口和简洁云纹保持，手指改为自然合拢的细长侧掌。`skin_v2.json` 的独立 `armSource` 仅供双臂使用，未用程序描线替代生成的手部绘画。

此前手部在网格末段仅分配4.5单位长度，腕点位于原图约83%处，已经进入掌部；单一横向轴心又将袖口与骨骼中心错开。本轮将腕点对准新图袖口末端约74%处，手部长改为8单位，并分别映射肩、肘、腕和指端的贴图轴心。袖、腕、手仍在同一连续网格中，共享原手臂骨链；原有肩部对躯干的权重过渡和近臂肩头遮挡保留。动作采样中手掌不再被压成短粗斜块，袖口至腕部保持连续，左右攻击的掌尖与头身朝向一致。

### 长袍腰部连接

前后袍片的顶端原来完全随各自延迟的衣摆骨链，跑步上身摆动时可与腰带错开。现将每片顶部8%完全绑定躯干，在8%—30%高度之间平滑过渡回原衣摆骨链；下方继续保留前后片各自响应和延迟。只调整蒙皮权重，不添加遮缝色块、不替换衣片纹理，也不改双腿动作。与新袖手合并采样后，腰带下方的连接保持随躯干移动；完整动作过渡和所有遮挡时序仍需连续实机评价。

### 实际镜头与验证边界

新采样脚本 [capture_yuxu_hands_v8.gd](../../../tools/action_battle_test/capture_yuxu_hands_v8.gd) 在场景初始化前启用该外观，使实际1.35倍跟随镜头正常创建；另外使用独立渲染视口生成4倍角色细节图，细节图没有对旧截图进行插值放大。左右各采集待机4、跑动5、倒退5、攻击3、闪避3个时点，共40姿态、80张图，位于 `tools/action_battle_test/output/yuxu_hands_v8/`。已目视检查双向全部跑动、攻击，以及待机、倒退、闪避的主要相位，关注袖腕指向与比例、腰部连接、远手进入躯干后的遮挡。

- [实际跟随镜头待机](../../../tools/action_battle_test/output/yuxu_hands_v8/Idle_000_right_camera.png)
- [右向跑动原生细节](../../../tools/action_battle_test/output/yuxu_hands_v8/Run_New1_145_right_detail.png)
- [左向出掌原生细节](../../../tools/action_battle_test/output/yuxu_hands_v8/AttackIdle_067_left_detail.png)
- [左向倒退原生细节](../../../tools/action_battle_test/output/yuxu_hands_v8/BackstepWalk_267_left_detail.png)
- [左向闪避原生细节](../../../tools/action_battle_test/output/yuxu_hands_v8/Step1_083_left_detail.png)

本次纠正前一节的截图范围说明：旧 v7 采样脚本在场景初始化后才启用角色，截图没有创建该入口的实际跟随镜头；此前约80像素的角色显示和“实际镜头攻击”链接只能作为旧采样条件下的记录，不代表当前入口镜头效果。现以本节新采样为准。

`smoke_wanderer_skin.gd` 通过7016项，覆盖96动作相位、双向镜像、原骨曲线一致、权重归一化、独立袖手纹理绑定与外观切换；基础 `smoke.gd` 通过，采样进程退出0。专用检查同时修正了失败处理：错误统一累计，失败以非零状态退出，不会再被末尾成功退出覆盖。本次这些结果属于独立试玩与蒙皮验证，不替代正式战斗回归。

手部使用固定的放松侧掌素材，没有逐指动画或握拳切换；正常镜头主要读到手形和袖腕轮廓，不能保证每根手指的细线均可辨认。新袖的细笔触与 v7 躯干较简化的笔触仍有差异。离散采样与检查通过不等于完整连续动作、全部动作切换或最终美术验收，仍需用户实机评价。

## 2026-11-03 玉虚自然屈肘摆臂与肩掌比例修订（用户否定的阶段稿）

用户指出 v8 跑步仍为一手立掌、一手下垂，大臂未从衣身肩部伸出、手掌过小，并提供自然屈肘跑步参考。此次修订覆盖上节“袖手已修正”的视觉结论；首页入口仍为「试演与预览 → 玉虚内门常服试玩」。只改玉虚外观的袖手贴图、蒙皮适配和手臂动作；头身侧面、无五官、长袍遮住上腿、原玩法、相机及界面保持。

原肩骨约在角色坐标 y=-78，而新衣身的肩线在 y=-84 附近，直接绑定造成袖子从胸口附近伸出。现在新增两组玉虚专用三节臂骨，肩点绑定实际衣身肩线，近袖帽恢复前景显示；袖腕仍为一张连续蒙皮。71个原骨节点及导出曲线没有改写，连同已有衣摆和腿部派生链共89骨。手臂适配在整体侧面投影和左右朝向完成后计算，避免投影修正再次扭转前臂。

跑步按原近远腿曲线的相位交替摆臂，肘部保持约75度屈曲：前手抬至下胸附近，后手降至腰侧；腕部沿前臂延续，取消移动时的立掌旋转。倒退采用较小摆幅。待机、攻击及闪避保留原臂部动作，并使用修正后的肩点和掌部比例。整体左右镜像，未分别绘制两套朝向。

内置 imagegen 参考 v8 双袖生成 [v9 放松弯指袖手](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_running_arms_v9.png)，保留米白衣料、青灰袖口和云纹；[完整提示词与接入说明](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_running_arms_v9.prompt.txt)。移动时使用自然微握手型，待机、出掌和闪避使用原打开的手型。弯指图末端单独映射，掌段长度从8增至10.5单位，掌宽在腕口外平滑增加最多22%，避免弯指后再次缩成小块。袖口至掌部仍连续，没有新增遮缝贴片。

[capture_yuxu_hands_v9.gd](../../../tools/action_battle_test/capture_yuxu_hands_v9.gd) 采集左右各待机4、跑步12、倒退6、攻击6、闪避6相位，共68姿态。每姿态均有实际1.35跟随镜头和4倍原生细节渲染，共136张最终图片。另以48相位采集左右完整跑步循环，接触表逐相位目视检查了肩线、肘部弯曲、腕口连续、远臂遮挡和袍腰连接，未见脱腕或臂部穿出躯干。部分远手在身体后方自然被遮挡；没有为了双手常显而强行调到前层。

- [实际镜头右跑](../../../tools/action_battle_test/output/yuxu_hands_v9/Run_New1_146_right_camera.jpg) · [左跑](../../../tools/action_battle_test/output/yuxu_hands_v9/Run_New1_146_left_camera.jpg)
- [右跑48相位循环](../../../tools/action_battle_test/output/yuxu_hands_v9/run_right_cycle.gif) · [左跑48相位循环](../../../tools/action_battle_test/output/yuxu_hands_v9/run_left_cycle.gif)
- [右跑12相位](../../../tools/action_battle_test/output/yuxu_hands_v9/Run_New1_right_contact.png) · [左跑12相位](../../../tools/action_battle_test/output/yuxu_hands_v9/Run_New1_left_contact.png)
- [攻击6相位](../../../tools/action_battle_test/output/yuxu_hands_v9/AttackIdle_right_contact.png) · [闪避6相位](../../../tools/action_battle_test/output/yuxu_hands_v9/Step1_left_contact.png) · [倒退6相位](../../../tools/action_battle_test/output/yuxu_hands_v9/BackstepWalk_right_contact.png)

专用蒙皮 smoke 通过7516项，覆盖源曲线96相位、跑步49采样（包括循环接缝）、肩腕连续、屈肘范围、有效摆幅、双向镜像、权重及手型/外观切换；基础试玩 smoke 通过。最终68姿态采集与双向48相位采集均退出0。过程中本机Godot的全视口PNG编码多次发生内存分配错误；最终实际镜头改用98%质量JPEG后完整重采，细节继续使用无损PNG。目录内旧 `_camera.png` 是过程输出，最终以 `_camera.jpg` 为准。

当前是两种固定手型切换，没有逐指变形；状态切换继续使用原试玩方式，未新增跨动作融合。袖部细笔触与原衣身的简化笔触仍有差异。此轮有完整跑步周期采样与多动作检查，仍不代表所有操作组合或最终美术风格获用户验收；正式战斗尚未接入。

## 2026-11-03 玉虚掌背肩袖与长袍整体修订（当前试作）

用户否定 v9 的大手、肩袖连接与整体协调；后续 v10 的侧缘薄手也被否定。当前要求以整个人物为准：身体侧身但手背或掌面朝屏幕可见，手、袖、躯干和袍的画风一致，肩袖从实际肩线自然接出，上腿始终藏在连续长袍内而脚靴仍显示步态。前两稿的采样或数字检查不等于视觉验收，本节取代其当前状态说明。

**袖手与肩部。** 内置 imagegen 以 v7 衣身和玉虚常服为参考重绘整张宽袖，再按最新指示重画朝屏幕的手背；最终运行素材为 [v11 宽袖与掌背](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_broad_hand_v11.png)，实际724×2172。[最终提示词与接入参数](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_broad_hand_v11.prompt.txt)及[宽袖生成和被否定手型的过程提示词](../../../tools/action_battle_test/art/original_wanderer/yuxu_neimen_sleeve_v10.prompt.txt)保留。宽袖使用与衣身相同的大块平涂阴影、青灰云纹和灰色轮廓；手为淡米肤色简化绘画，没有写实织物、皮肤高光或肌腱细节。近远臂共用同一素材，整个人物左右镜像；待机、跑动、攻击和闪避不再切换两种画风不同的手图。

上一稿 y=-84 的肩点提得过高，现按躯干贴图的肩线映射到近肩(-2.5,-80)、远肩(0.5,-80)。袖帽最上部与躯干权重过渡，过渡缩短至袖高12%以内，让肩下至肘真正随臂摆动；近袖处于躯干前、远袖位于躯干后。远近上臂统一16、前臂13.5单位，掌段为6.5单位，保持可见手背面而不过度放大手。保留71个源骨节点及导出曲线，玉虚外观派生链总89骨；跑步双臂仍按原腿部相位连续屈肘交替，腕口与手属于同一连续蒙皮。

**长袍与腿。** 保留袍腰绑定躯干并渐变至前后衣摆骨链的连接，继续原有分段摆动。腿部蒙皮只绘制靴口以下的网格，避免大腿或浅色裤腿在摆动中越出袍边。当前不是布料物理碰撞；长袍外轮廓仍由原前后袍片和衣摆骨链形成。没有改头身、无五官设定、角色比例、相机、界面或战斗机制。

**合并实拍。** [v11采样脚本](../../../tools/action_battle_test/capture_yuxu_hands_v11.gd) 在袖手和腿部可见范围合并后重新采集双向各待机4、跑步12、倒退6、攻击6、闪避6相位，共68姿态；每姿态包括实际1.35跟随镜头JPEG和4倍原生细节PNG，共136张。左右完整跑步各另采48相位，逐相位接触表已目视检查掌背、肩肘腕连续、前后层级、袍腰和上腿遮挡。双向待机、攻击、闪避、倒退的接触表也已逐项检查；采样中未见脱腕、肩部断口、上腿露出或靴子与袍边明显脱离。

- [实际镜头右跑](../../../tools/action_battle_test/output/yuxu_hands_v11/Run_New1_146_right_camera.jpg) · [左跑](../../../tools/action_battle_test/output/yuxu_hands_v11/Run_New1_146_left_camera.jpg) · [原生放大](../../../tools/action_battle_test/output/yuxu_hands_v11/Run_New1_146_right_detail.png)
- [右跑正常速度循环](../../../tools/action_battle_test/output/yuxu_hands_v11/run_right_cycle.gif) · [左跑正常速度循环](../../../tools/action_battle_test/output/yuxu_hands_v11/run_left_cycle.gif)：从48相位中取24帧，周期580ms，接近原583ms。
- [右跑48相位慢放](../../../tools/action_battle_test/output/yuxu_hands_v11/run_right_slow.gif) · [左跑48相位慢放](../../../tools/action_battle_test/output/yuxu_hands_v11/run_left_slow.gif) · [右向48相位接触表](../../../tools/action_battle_test/output/yuxu_hands_v11_cycle/right_48_phases.png) · [左向接触表](../../../tools/action_battle_test/output/yuxu_hands_v11_cycle/left_48_phases.png)
- [待机](../../../tools/action_battle_test/output/yuxu_hands_v11/Idle_right_contact.png) · [攻击](../../../tools/action_battle_test/output/yuxu_hands_v11/AttackIdle_right_contact.png) · [闪避](../../../tools/action_battle_test/output/yuxu_hands_v11/Step1_left_contact.png) · [倒退](../../../tools/action_battle_test/output/yuxu_hands_v11/BackstepWalk_right_contact.png)

最终专用蒙皮检查通过7854项，基础试玩 smoke 通过；68姿态和双向48相位采集均退出0，最终日志在同一输出目录的 `capture_final.log`、`capture_cycle_final.log`、`smoke_skin_final.log`、`smoke_basic_final.log`。正式战斗未接入；手指仍是固定绘画，状态切换沿用原试玩方式，未增加跨动作融合。当前已完成所列周期与姿态检查，最终美术评价仍以用户实机观察为准。

## 2026-11-03 玉虚认可造型与自然动态修订（当前试玩）

本节覆盖上方 v11 及其“只绘靴口以下”的临时做法。当前首页「试演与预览 → 玉虚内门常服试玩」进入 `ActionBattleOriginalWandererScene.tscn`，使用用户认可的 [v15 完整造型](../../../tools/action_battle_test/art/yuxu_fullbody_v15/design.png)：青白长袍、简洁云纹、无五官侧身与统一成人比例。头、衣身、袖手、完整腿靴沿用该版派生图；本轮没有重新生成纹理。双腿从髋、膝到鞋底保留全部网格，由连续长袍覆盖上腿，不用裁腿掩盖接缝。

用户要求参考 BV1mvsX6xEHb 30:00—30:30 整体自然战斗感，本轮依据已取得的连续帧观察屈臂防御姿态、支撑与收腿交替、上身轻微前倾和衣尾延迟。旧原骨架与曲线保留供对照；认可造型使用自己的19骨连续蒙皮和视觉运动适配，不宣称直接复刻原作动画。

- 步态区分落脚、支撑、离地、收腿、前摆，按完整腿链反解膝踝位置，鞋底参与接地修正；修复旧版长时间直腿后伸、双脚悬空的轮廓。
- 重心随支撑脚下沉再抬起，跑动时稍向支撑前方移动，胸肩有小幅前倾与反向摆动，头部部分抵消躯干角度；双臂保持自然屈肘，按对侧步伐交替。最后一轮提高了跑动节奏与屈臂摆幅，下摆跑时略提、停止后回落，改善小步疾走感和摆动靴被完全遮住的时段。
- 连续衣身从腰至膝渐变蒙皮，包住抬起的上腿；前后摆使用短链延迟和衰减，停步后回落。首轮露膝与后撤时靴子被袍遮没均已在窗口采样后修正。
- 视觉层接收真实位移速度和模拟时间，处理起停、面向变化与出掌过渡；移动施法下肢继续迈步，暂停时动作和衣摆不按墙钟推进。原地后撤采用独立脚位，不叠加跑步的双脚抬升。

战斗费用、伤害、范围、位移、输入、镜头、界面及旧外观没有因此更改。左右仍为侧身整体镜像，不是三维转身；掌形仍是固定绘画，衣料是分段蒙皮的运动近似，未实现真实布料碰撞。

### 连续局内与原生细节

[12秒完整局内动态图（1280×720 WebP）](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/arena_12s.webp) · [局内GIF备份（960×540）](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/arena_12s.gif) · [同姿态原生4倍细节GIF](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/detail_12s.gif)。

`capture_yuxu_natural.gd` 通过实际战场更新函数重放输入，记录360张原生窗口帧及对应位置、朝向和动作，包含左右与斜向移动、启动、停步、转向、原地及移动出掌、后退、无输入后撤和回到待机；360帧实际朝向均与预期一致。角色细节由第二个独立渲染视口在同一模拟时刻绘制，完整保留头、双手、长袍和双腿，不是裁掉身体的图片放大。

[启动](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/start_contact.png) · [停止](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/stop_contact.png) · [转向](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/turn_contact.png) · [移动出掌](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/moving_attack_contact.png) · [后撤](../../../tools/action_battle_test/output/yuxu_natural_v16_arena/dash_contact.png)。另已重采并目视检查[右向48相位](../../../tools/action_battle_test/output/yuxu_fullbody_v15_cycle/Run_New1_right_contact.png)和[左向48相位](../../../tools/action_battle_test/output/yuxu_fullbody_v15_cycle/Run_New1_left_contact.png)。这些相位图是循环拆解，实际起停与衣摆惯性以12秒连续局内记录为准。

### 验证

`smoke_yuxu_natural.gd` 5907项通过，覆盖完整腿网格、权重、48相位连续性与首尾闭合、镜像、支撑脚、移动施法和暂停；`smoke_basic_skills.gd` 24项、旧 `smoke.gd`、`ui_smoke.gd` 均通过。`dotnet build Stacklands.csproj --no-restore` 0警告0错误；Godot Mono 主项目玉虚场景无窗口启动退出0，独立试玩OpenGL窗口连续采样完成且日志无运行错误。日志位于 `tools/action_battle_test/output/natural_*.log` 与 `yuxu_natural_main_scene.log`。本次仍为可玩的独立试玩，未接入正式遭遇或结算；自然度与最终风格以用户实际连续观看评价为准。

## 2026-11-03 玉虚下摆柔软度修订 v17（当前试玩）

用户认可 v15 造型与 v16 自然动态，只要求衣服下摆更柔软。本节只覆盖上一节的衣摆实现：沿用原头身、袖手和完整腿靴贴图，以及手臂、步态、相机、HUD、输入与战斗规则；首页「试演与预览 → 玉虚内门常服试玩」直接使用修订后的衣摆。

连续袍身仍是一张从衣领至下摆的蒙皮，没有拆出接缝或遮缝片。前后各从两段扩为三段，总骨数19→21；腰部权重保持，膝下至末端的权重逐渐过渡，后侧比前襟更早释放。上段随腿部形状让位，中段逐步释放膝部横移，末端再独立弯曲，减少整片随膝平移和统一板式摆动。前襟响应稍紧，后摆稍松；使用实际移动速度、加减速与步态驱动不同阻尼的逐段延迟，停步后提摆与曲率逐步回落。骨骼不靠缩放拉长，弯曲幅度有限；这仍是蒙皮近似，不是完整布料物理。

转向时只保留自由衣褶的世界方向惯性，不反转膝部遮挡约束。连续检查曾发现初稿转身瞬间露腿，以及 v16 已有的倒退启动露膝；现让上段在膝部外撑时及时让位、向内回落时继续缓动，下段柔性独立保留。最终360帧的逐帧接触表全部目视复核，覆盖启动、左右/斜向跑动、转身、停止、原地与移动出掌、倒退、后撤与回到待机；未见前述露膝、衣腰断口或明显橡皮拉长，完整脚靴保持可见。正常速度的最终柔软度仍由用户实机评价。

- [v16／v17 同时刻前后对照 GIF（12秒）](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/before_after_12s.gif)
- [实际局内新动态图 GIF](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/arena_12s.gif) · [1280×720 WebP](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/arena_12s.webp) · [同姿态原生细节 GIF](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/detail_12s.gif)
- [起步](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/start_contact.png) · [转向](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/turn_contact.png) · [停止](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/stop_contact.png) · [移动出掌](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/moving_attack_contact.png) · [后撤](../../../tools/action_battle_test/output/yuxu_soft_hem_v17_arena/dash_contact.png)

复用 `capture_yuxu_natural.gd` 的原360帧、12秒真实战场输入回放，新输出独立存放，v16采集保留；`continuous_00.jpg`—`continuous_11.jpg` 按顺序覆盖所有帧，没有抽掉转场。`smoke_yuxu_natural.gd` 8014项通过，新增检查转身时膝部覆盖方向、停止后仍有短暂衣摆延迟、两秒后回落、暂停与骨段不缩放；旧试玩 `smoke.gd` 与基础四招24项通过。Godot Mono 4.7.2 主项目玉虚场景启动退出0，最终窗口回放日志无错误；本轮无C#改动，未重跑整项目构建。具体日志为本轮输出目录的 `smoke_natural.log`、`smoke_basic.log`、`smoke_skills.log`、`main_scene.log`、`capture.log`。

## 2026-11-03 玉虚单手自然动作与自由跑动袍 v19（当前试玩）

用户最新要求为：只举一只手，另一只手自然放低；手型放松；腿脚可以露出，袍应适合跑动。此要求覆盖此前强制遮住上腿、通过袍体追膝避免露膝的约束。保留认可的 v15 青白云纹款式、成人比例和无五官侧身造型；首页「试演与预览 → 玉虚内门常服试玩」已直接使用本轮修订。v18双手举起方案未作为最终版本。

**手臂与手型。** 近侧主手保持自然屈肘抬起，远侧辅手在腰侧低位摆动；肩、肘和腕分别响应步态，上身有小幅带动，出掌时主手伸出后收回。内置 imagegen 依据原袖图只重绘放松半握手与自然展开掌，运行素材为 [arm_relaxed_v19.png](../../../tools/action_battle_test/art/yuxu_fullbody_v15/arm_relaxed_v19.png) 与 [arm_open_v19.png](../../../tools/action_battle_test/art/yuxu_fullbody_v15/arm_open_v19.png)，保留原袖、袖口和云纹；[提示词记录](../../../tools/action_battle_test/art/yuxu_fullbody_v15/arm_v19.prompt.txt)。同时修正旧肩缝权重横跨整行、误把远端手指绑到躯干的问题，让放低的手随腕部完整运动。手指仍为两种绘画姿态，不是逐指骨骼动画。

**腿脚与袍。** 原完整衣身纹理沿既有青边分成前后两片蒙皮，腰上仍采用相同权重连合，髋部以下逐渐独立，静止时衣襟留少量重叠，迈步时自然开衩。前后片仍各三段延迟，减小追膝外撑与抬摆量，膝腿可以从开衩和袍边露出，不再包进膨胀的袍体。前后踝轨迹跨度由47增至56、摆动脚抬升由14增至20（视觉骨架单位），配合重心降低保留支撑与收腿；未更改玩法移动速度。两条腿从髋、膝至鞋底保持全部513个网格顶点与原骨长；近腿在远腿前，双腿仍在袍片后，未通过裁腿、隐藏顶点或强提腿层级遮掩问题。袍为原生网格与权重改造，没有重画衣服纹理或改变款式。总计7张主网格、21骨。

**合并后局内记录。** 最终 [12秒实际局内 GIF](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/arena_12s.gif)、[1280×720 WebP](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/arena_12s.webp) 与 [同姿态原生4倍细节 GIF](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/detail_12s.gif) 来自同一次合并后的真实战场输入回放。360张窗口帧与360张原生细节帧全部保留，包含左右及斜向跑动、启动、停止、转身、原地和移动出掌、倒退、后撤与回到待机；实际朝向和预期的360次核对均一致。`continuous_00.jpg`—`continuous_11.jpg` 覆盖全部360帧，已按顺序目视复核，未见腰部脱开、肢体裁断或明显过量甩摆；脚腿露出属于本轮期望表现。正常速度下的自然度仍以用户连续观看评价为准。

- [启动](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/start_contact.png) · [转向](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/turn_contact.png) · [停止](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/stop_contact.png)
- [原地出掌](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/attack_contact.png) · [移动出掌](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/moving_attack_contact.png) · [后撤](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/dash_contact.png) · [停止回落](../../../tools/action_battle_test/output/yuxu_arm_motion_v19_arena/settle_contact.png)

**验证。** 最终合并版本 `smoke_yuxu_natural.gd` 8074项、`smoke_yuxu_arms.gd` 1254项、基础四招24项，以及旧试玩 `smoke.gd` 和 `ui_smoke.gd` 均通过。腿袍测试已经删除强制遮膝断言，改查完整腿网格、实际步幅与抬脚、衣襟UV连续、有限袍片位移、支撑脚、镜像、移动出掌、起停和暂停；上肢检查覆盖单主手抬起、辅手低位、腕掌连贯及手指不误绑躯干。Godot Mono 4.7.2 主项目玉虚场景启动退出0，最终窗口回放无运行错误。日志均在 `tools/action_battle_test/output/yuxu_arm_motion_v19_arena/`；本轮无C#修改，未重跑整项目构建。玩法、相机、HUD和技能规则保持，正式战斗未接入；衣料仍为分段蒙皮近似，并非布料碰撞模拟。

## 2026-11-03 玉虚长袍重叠、鞋底接地与重心 v20（当前试玩）

用户指出 v19 前后袍分叉不自然、脚与影子脱离，并要求上部身体在运动中有合适的前倾后倾。本轮保持认可的青白云纹款式、头身比例、无五官侧身、单主手抬起和辅手放低、两种放松手型；不重新生成图像。首页「试演与预览 → 玉虚内门常服试玩」直接使用本版，原战斗数值、移动速度、输入、相机与 HUD 保持。

**长袍。** 沿原前襟保留前后网格，但后片现在真正延伸到前襟下面，髋腿段重叠最多48源像素，向末端收至24像素；腰下的独立权重过渡也下移、加长，避免从腰开始分成两条相互远离的直片。下摆分为外层、窄反面和前襟的遮挡层，反面复用同一衣料纹理并略压暗；前襟优先覆盖重叠边。脚腿仍可从衣缘与开衩露出，完整腿链和网格未删减。衣摆受实际行进、加减速、膝部让位及收腿过程驱动三段阻尼，撤下上一版独立循环的衣摆正弦项；停止后逐段回落。首轮曾把整个后摆放在腿后，产生上腿矩形切口，窗口检查后已否决；最终所有袍层按实际衣缘遮腿，没有该切口。

**鞋底与落地影。** 载入时从现有靴图的可见 alpha 轮廓提取鞋底，按每相位脚掌角度计算真正的最低点，再反解踝膝；支撑距离将超出固定腿长时，仅补足所需的重心下沉，避免无法达到目标的脚悬在地面上方。场景原固定椭圆影由人物自己的软身体影和两处小接触影取代；接触影沿脚底横向落点移动，抬脚后减弱。鞋底与影子使用同一地平面，不通过压入地面解决悬浮，待机也保持该关系。

**上身。** 躯干随前进、后退和加减速进行有阻尼的倾斜，头部部分反向补偿，肩肘继续沿 v19 的单手姿态运动。最终局内稳定前跑胸部约前倾8.9°—10.8°，后退约后倾4.9°—6.5°；起步渐入、停步回正，出掌与后撤仍可叠加各自动作。这里是视觉角度，未新增玩法属性或战斗规则。

### 实际局内与逐帧检查

- [最终12秒实际局内 GIF](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/arena_12s.gif) · [1280×720 WebP](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/arena_12s.webp) · [同姿态原生4倍细节 GIF](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/detail_12s.gif)
- [v19／v20 同时刻完整对照 GIF](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/before_after_12s.gif)
- [起步](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/start_contact.png) · [转身](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/turn_contact.png) · [停步](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/stop_contact.png) · [移动出掌](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/moving_attack_contact.png) · [后撤](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/dash_contact.png)
- [右向48相位](../../../tools/action_battle_test/output/yuxu_grounded_v20_cycle/Run_New1_right_contact.png) · [左向48相位](../../../tools/action_battle_test/output/yuxu_grounded_v20_cycle/Run_New1_left_contact.png) · [右跑循环](../../../tools/action_battle_test/output/yuxu_grounded_v20_cycle/run_right_cycle.gif) · [左跑循环](../../../tools/action_battle_test/output/yuxu_grounded_v20_cycle/run_left_cycle.gif)

最终360张真实战场帧和360张同一时刻的原生细节帧保留在 `yuxu_grounded_v20_arena`，`continuous_00.jpg`—`continuous_11.jpg` 覆盖全部帧，已依次目视核查袍腰、重叠开合、落脚、上身与头肩。另采集双向各48跑步相位及各4待机、6后退、6出掌、6后撤，共140张原生细节，全部接触表已目视检查；不是以 smoke 代替视觉验收。动态播放文件按原12秒时间编码。局内记录包含左右、斜向、起停、转向、倒退、原地/移动出掌及后撤。

### 验证范围与限制

腿袍与接地专项 **9301项**、v19单手专项 **1254项**、基础四招 **24项**、旧试玩及 UI 回归均通过。新增检查按变换后的可见鞋底轮廓核验抬脚高度、至少一只支撑脚接地、鞋底不沉地，以及腰部重叠 UV；原完整腿网格、镜像、暂停、移动出掌和衣摆回落仍检查。最终360帧记录中，鞋底接触高度误差、最大沉地与最大支撑脚间隙均小于 **0.0003视觉骨架单位**，见[接地记录](../../../tools/action_battle_test/output/yuxu_grounded_v20_arena/ground_contact_review.json)；这些数字只验证几何接触，视觉自然度另由上述画面判断。

Godot Mono 4.7.2 主项目玉虚入口场景启动退出0；最终真实窗口回放及多相位采集无运行错误。日志位于最终两目录的 `capture.log`、`main_scene.log` 与各 `smoke*.log`、`ui_smoke.log`。本轮只有 GDScript 和文档更改，未重跑 C# 构建。仍是分段蒙皮近似，没有真实布料碰撞、逐指动画或三维转身；未接入正式遭遇/结算，最终正常速度下的自然感仍由用户实机评价。

## 2026-11-03 玉虚长袍约束步态、下摆惯性与自然手势 v21（当前试玩）

用户本轮要求长袍束住腿、降低可见步频，并让下摆有轻微物理感；在说明小步慢频与原235移速不能同时无滑步后，用户明确允许一起降低普通移动速度。随后补充自然状态手势：平时放低手、腰前轻收、短暂剑指凝神，进攻／防御准备时再抬主手。本节覆盖此前“大步快速跑动”和“始终抬主手”的规定。首页「试演与预览 → 玉虚内门常服试玩」直接使用本版。

**长袍约束的步态与移速。** 玉虚外观试玩的普通移动由235降至 **114像素／秒**；左右及后退使用同一速度，未增加后退减速规则。完整周期由前进2.65次／秒改为 **1.8次／秒**，前后踝轨迹跨度56→44视觉骨架单位，支撑占周期0.46→0.50，抬脚20→11.5；后退保持更低抬脚。显示比例仍为0.72，`0.72×44×1.8÷0.5≈114`，配平步态与实际前进距离。斜向按真实横向分量投影步幅。两条腿仍从髋至鞋底各保留513个网格顶点及原骨长，未裁腿、隐去大腿或靠强改层级藏腿。v20鞋底轮廓反解、接触影与头身前后倾继续使用；骨盆高低起伏随短步幅适当收小。

这项114仅是**用户批准的玉虚美术试玩移速**；旧样例仍为235，正式动作人物的战斗移速依旧未配置，不把114写入角色基础属性。错步仍使用原500像素／秒及原持续时间，攻击、费用、冷却、范围、镜头和HUD保持。其它并行任务对四招特效的修订不属于本次步态改动。

**下摆轻量物理。** 前后各三段保留固定骨长、连续蒙皮和腰部重叠。每段现在分别积算角速度：接缝弹性约束局部折角，重力按该段绝对垂挂角回正，真实加减速产生惯性，上段角速度传向下段，阻尼逐渐消耗余摆；每次更新用四小步积分。腿部让位仅提供轻微推力，未增加独立循环的正弦摆。转向保留松散段的世界方向惯性，停步后回落，腰部不跟着整片甩动。12秒记录中最大单段折角约4.77°，属于克制的视觉物理；它不是完整布料碰撞或三维布料模拟。

**自然手势。** 安静站立时双手自然放低，行走时低位屈臂交替、腕部稍滞后；连续静止4—11.6秒轻收主手至腰前，18—23秒短暂凝神剑指，然后自然垂手，长周期56秒才可再次出现。移动、出手或架招会取消静止计时，恢复对应动作。真实攻击准备与架招窗口只读传给上肢，不增益、不施法、不添加新技能；出掌仍为主手展开掌，辅手保持低位。新增[剑指手型](../../../tools/action_battle_test/art/yuxu_fullbody_v15/arm_sword_fingers_v21.png)沿用原腕点与袖口，手型连续混合避免突然换图；[素材提示与处理记录](../../../tools/action_battle_test/art/yuxu_fullbody_v15/arm_sword_fingers_v21.prompt.txt)随素材保存。仍为绘画手型混合，没有逐指骨骼。原青白云纹款式、成人比例和无五官侧身保持。

### 最终局内动态与目视范围

- [12秒实际局内 GIF](../../../tools/action_battle_test/output/yuxu_robed_v21_arena/arena_12s.gif) · [1280×720 WebP](../../../tools/action_battle_test/output/yuxu_robed_v21_arena/arena_12s.webp) · [同姿态原生细节 GIF](../../../tools/action_battle_test/output/yuxu_robed_v21_arena/detail_12s.gif)
- [v20／v21 前后对照](../../../tools/action_battle_test/output/yuxu_robed_v21_arena/before_after_12s.gif) · [正常速度／半速检查对照](../../../tools/action_battle_test/output/yuxu_robed_v21_arena/normal_slow_24s.gif)；前者使用相同输入时刻，因114移速不同，实际行进路程不同；后者左边重复两次12秒、右边24秒半速，无补帧。
- [26秒自然手势实际局内 GIF](../../../tools/action_battle_test/output/yuxu_gestures_v21/arena_26s.gif) · [完整 WebP](../../../tools/action_battle_test/output/yuxu_gestures_v21/gestures_26s.webp) · [原生手势细节 GIF](../../../tools/action_battle_test/output/yuxu_gestures_v21/detail_26s.gif)
- [右向48相位](../../../tools/action_battle_test/output/yuxu_robed_v21_cycle/Run_New1_right_contact.png) · [左向48相位](../../../tools/action_battle_test/output/yuxu_robed_v21_cycle/Run_New1_left_contact.png) · [后退](../../../tools/action_battle_test/output/yuxu_robed_v21_cycle/BackstepWalk_left_contact.png) · [出掌](../../../tools/action_battle_test/output/yuxu_robed_v21_cycle/AttackIdle_right_contact.png) · [后撤](../../../tools/action_battle_test/output/yuxu_robed_v21_cycle/Step1_left_contact.png)

最终12秒由真实战场移动与技能路径推进，360张窗口帧与360张同一输入的原生细节帧全部保留。`yuxu_robed_v21_arena/continuous_00.jpg`—`continuous_11.jpg`已逐张、逐相位目视；包含左右与斜向起停、转向、后退、原地／移动出掌、错步、架招及回落。另采双向各48移动、4待机、6后退、6出掌、6后撤，共140张并逐表目视。未见腰带脱开、袍片断缝、裁腿或持续大抖；前后袍与膝腿仍按实际衣缘遮挡。自然手势另采26秒520帧，包含完整静止状态及最后2秒真实移动取消，按状态与200张关键转场连续帧复核；上肢还进行了独立交叉目视检查。正常速度下的最终自然感仍由用户实机评价。

### 验证与明确限制

腿袍／接地 **9301项**、新增慢步支撑／回落 **501项**、自然上肢 **10976项**、基础四招 **24项**，以及旧试玩和UI回归均通过；Godot Mono 4.7.2的主项目玉虚入口启动退出0。日志在 `yuxu_robed_v21_arena`。本轮没有C#修改，未重复整项目C#构建。

新增检查测量变换后的真实支撑脚世界坐标，覆盖左右朝向、前进、后退和斜向，稳定支撑段最大**横向**滑移0.0513像素／秒，来自114取整差；不把此数字外推至启动、转向、脚掌滚动、错步或完整二维／三维接触。最终12秒鞋底高度误差、最大沉地和支撑间隙均低于0.0002视觉骨架单位，见[接地记录](../../../tools/action_battle_test/output/yuxu_robed_v21_arena/ground_contact_review.json)。斜向的纵深仍是二维侧视近似，瞬时换向和错步不是逐脚世界锁定；没有真实布料自碰撞或腿部碰撞求解，靠有限折角、原重叠层和较小步幅保持本轮已检查相位的连续。未接入正式遭遇／战斗结算。
