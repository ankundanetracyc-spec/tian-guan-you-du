# 法器战斗测试词典

本目录仅收录用户批准的首版战斗接入样本，不作为正式炼制、掉落或平衡表。

| 主图 | 来源 | 内容 |
| --- | --- | --- |
| [御鸣锋剑](御鸣锋剑.drawio) | 鸣锋剑外祭 | 召唤与主动穿防攻伐 |
| [御守锋剑](御守锋剑.drawio) | 守锋剑外祭 | 召唤与自身护盾 |
| [鸣锋内炼化法](鸣锋内炼化法.drawio) | 鸣锋剑内炼 | 原技能保留，追加20%物防穿透 |
| [守锋内炼化法](守锋内炼化法.drawio) | 守锋剑内炼 | 实际架招化解增加2 |

数据由 `ArtifactCombatSamples` 显式配置，经 `ArtifactSkillFactory` 生成运行时技能；没有正式 `.tres` 配方或菜单奖励。测试技能只通过独立场景提供。测试数值不由材料、材评或功用品级推导。

交互入口：`res://tools/ArtifactCombatPreview.tscn`。集成检查入口：`res://tools/ArtifactCombatIntegrationSmoke.tscn`。
