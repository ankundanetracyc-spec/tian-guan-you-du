# 常规法术：原地挥手抛出

状态：2026-10-20修订，按用户反馈取消收手蓄势，直接挥手施法。延续现行局内水墨人物，补充不同于惊蛰提指引雷的常规向外释放动作。

## 动作

男女直接从待机向前侧挥开手臂，放松手腕随势抛出；另一手留在腹前，袖摆与头发向后展开。双脚保持原站位。原男女收手蓄势素材及源图保留在素材包中，当前动作不加载或播放，供后续另用。

| 阶段 | 动作时钟 | 表现 |
| --- | --- | --- |
| 挥出 | 动作开始即进入 | 直接切换伸臂挥袖姿势，法术由掌前释放，不等待蓄势 |
| 命中 | 沿用0.30秒接触点 | 保持挥出姿势，与既有局部特写和接触停顿同步 |
| 收势 | 末0.22秒 | 衔接回原待机；人物动作时钟共0.90秒，不含接触停顿 |

全部素材为2048×2048 RGBA透明PNG，共用(1024,1900)地面原点、1375参考身高；创作阶段配准后保存，运行时不逐张裁边、居中或改变人物缩放。缺少姿势图时使用当前默认衣装待机。镜像围绕角色原点，暂停及接触停顿沿用同一人物动作时钟。

## 接入与查看

原水墨特效实验室新增：

- **动作·挥手抛术**：只看人物，使用切换男女、切换交手方向和重播按钮观察。
- **战斗·挥手释法**：使用现有灵击及其特效，展示从掌前释放到命中的衔接；本轮未新增法术特效包。

正式表现路由中的既有Spirit、SpiritBurst、SoulNeedle类别使用此动作（灵击、灵爆、魂刺及原归入相同类别的技能）。惊蛰保持提指引雷，其他动作类别保留原路由。只改姿势和特效释放位置／起始时间，不改任何技能资源、费用、伤害、目标、逻辑射程、行动槽或存档字段。

## 预览

- 男修：[挥出](male_release.png)、[回待机](male_idle.png)、[动作动图](male_pose.gif)、[灵击衔接动图](male_spell.gif)。
- 女修，反向：[挥出](female_release.png)、[回待机](female_idle.png)、[动作动图](female_pose.gif)、[灵击衔接动图](female_spell.gif)。
- 首版蓄势截图留档：[男修](male_windup.png)、[女修](female_windup.png)，不代表当前播放流程。

图片与动图来自原游戏展示入口，使用固定30fps录制。编译0警告、0错误；本轮未新增或运行自动化测试，未重做技能结算功能回归。

## 素材与来源

人物资产保存在`textures/characters/side_ink_throw_20261020_v1/`：男女各`throw_windup`及`throw_release`。使用imagegen技能与内置image_gen，以原男女默认待机为角色／衣装参考生成；没有使用外部游戏素材。

- [完整生成提示词](../../../textures/characters/side_ink_throw_20261020_v1/generation_prompts.json)
- [画布配准参数](../../../textures/characters/side_ink_throw_20261020_v1/export_manifest.json)

源图保留在该素材包`source/`内。人物动作接口位于`BattlePaperDoll.Throw.cs`；类别选择位于`BattleInkCombatVfx.Throw.cs`，由原`PresentAttackPose`共用。素材配准复用`tools/export_side_ink_preview.gd`，展示录制工具为`tools/capture_throw_showcase.gd`。
