# 预装配数据接口草案（已撤回，未接入）

2026-11-08。本接口草案已按最新用户范围从正式游戏撤回，以下仅为后续战斗稿验收后可复用的设计记录，不代表当前游戏能力。权威来源为[新版角色战斗页](../../../plans/界面与美术/角色战斗页新版招式盘样稿_20261108.md)、[整盘方案规则](../../../plans/战斗与构筑/布阵方案与一键摆盘设计_20261107.md)及[人类招式盘实施](../../../plans/战斗与构筑/人类招式盘实施与验收_20261107.md)，最新用户授权覆盖这些文档早期“只做样稿”的接入状态。

新增源码与测试已移至暂存source，扩展名为.cs.txt／.tscn.txt，不参与编译。曾运行76项数据断言全部通过，但退出发生原生Array销毁AccessViolation，进程-1073741819，因此整体未验收通过；后补退出生命周期整理也未复测。未打开玩家正式旅程。

HumanData新增独立的combat_board_preparation和combat_board_named_plans。前者只有显式“设为预装配”改变，后者仅保存本人物命名布局；原combat_board_layout继续表示战斗运行及空瓶回传。默认与方案均逐项复制，不保存体力、法力、次数、冷却、待命或库存副本。普通资源存档随HumanData保存；服务不另写全局文件。

方案名称使用CombatBoardLayout显式Export的preset_name，并与旧ResourceName同步；后台DetachedResourceSnapshot只捕获自定义导出成员，不能仅依赖ResourceName。该导出属性同时读取旧ResourceName名称，CopyConfiguration明确复制，不改全局快照序列化器。专属测试包含正式Capture→Materialize后的逐方案名称、兼容旧名字及重建后的改名／覆盖／删除，不能以直接ResourceSaver往返替代。

静态服务位于[CombatBoardPreparationService](../source/csharp/battle/CombatBoardPreparationService.cs.txt)，供角色页调用：

| API | 行为 |
| --- | --- |
| GetDraft(Character) | 返回预装配副本；旧档无预装配时只读复制原运行配置，或生成旧槽迁移草稿，不提前保存。 |
| GetPlans(Character) | 返回本人物所有命名盘副本，不限固定套数。 |
| Validate(Character, layout) | 返回全部可读缺项；复用真实Catalog和Geometry，丹药另列需／有／缺数量。 |
| TrySetPreparation(Character, layout, out reason) | 整盘合法才写出战默认，不改运行盘或命名库。 |
| TrySavePlan(Character, layout, name, overwrite, out reason) | 名称Trim且非空；另存要求唯一，同名覆盖必须显式允许。 |
| TryDeletePlan(Character, name, out reason) | 只删命名记录，不删除当前UI草稿或默认配置。 |
| TryRenamePlan(Character, oldName, newName, out reason) | 新名称非空且唯一；原名确认成功无改动，布局与默认不变。 |
| TryLoadPlan(Character, layout, out loaded, out reason) | 先全盘校验，成功返回副本，失败不写人物或调用者草稿。 |
| RequiresIndicator(Character) | 只从真实当前心法／局内快照判断原周天要求。 |

当前仅开放实际3×3矩形。招式、化法、战斗丹药沿现行定义；玄枢原周天指示继续按原规则核验，不额外创造来源。化法无效邻接方向可以保留，其“未生效”仍由原展示解释；缺已学来源、超份数、越界、重叠或库存不足则整盘拒绝。外围手持、器物、符箓、非矩形与新扩盘没有新增模型或收益。

运行盘的expended保持空瓶含义，不能通过另存、设预装配或载入方案转换成新药品。旧档草稿中的空瓶保留待修，不自动移除或清标记；玩家须移出空瓶，再从真实普通行囊放入丹药。合法方案的丹药只预留，不消费；每次应用重新检查当前库存，不复制行囊、储物戒物品或恢复已消费药品。

快照新增[专用字段与深拷贝](../source/csharp/battle/BattleEntityData.CombatBoardPreparation.cs.txt)和[Character专用桥接](../source/csharp/entity/Character.CombatBoardPreparation.cs.txt)。世界人物出战时把预装配副本作为初始盘；战斗克隆的快照继续使用运行盘。命名库只传副本给克隆。战后apply只更新原有资源／物品／运行盘，拒绝由回传快照改写世界默认和命名库。显式预装配因心法资格变化而失效时保留原对象待修，不在入场时偷偷添加或删除周天指示。

BattleBoardHud方案页读取本人物快照库和实际入场配置，停止读取全局user://combat_board_presets.tres；旧文件原样保留，不把无法确定归属的全局方案自动导入其他角色。局内载入先经BattleManager.try_load_player_board_plan校验并返回副本，只替换编辑草稿。原commit_player_board仍负责整理窗口、整盘校验、ReplaceLayoutAtRoundBoundary和开始／继续；运行账本不重建。

[专属验收场景](../source/tscn/debug/CombatBoardPreparationSmoke.tscn.txt)与[测试源](../source/csharp/debug/CombatBoardPreparationSmoke.cs.txt)要求APPDATA路径含board_preparation；只新建隔离内存人物和正式人类战斗场景，不使用正式旅程。覆盖命名CRUD和唯一性、超过固定套数、默认独立、读取深拷贝、源缺失／丹药真实缺数、空瓶拒绝、盘形及占格、快照深拷贝、真实服丹回传、隔离存档重载、局内只改草稿、冷却身份与原提交边界。实际运行结果将保存本目录验证记录.json。
