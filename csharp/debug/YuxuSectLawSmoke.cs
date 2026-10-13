using Godot;
using System;
using System.Linq;

/// <summary>Checks configurable Yuxu case rules, appeal state, real item recovery, and save round-trip.</summary>
public partial class YuxuSectLawSmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public override void _Ready()
    {
        var savePath = $"user://yuxu_sect_law_smoke_{Guid.NewGuid():N}.tres";
        try
        {
            var config = new YuxuSectLawDraftConfig();
            var attacker = new WorldNpcData
            {
                npc_id = "law_smoke_attacker",
                npc_name = "甲",
                faction_id = config.sect_faction_id,
                current_node_id = "east_woods",
            };
            var target = new WorldNpcData
            {
                npc_id = "law_smoke_target",
                npc_name = "乙",
                faction_id = config.sect_faction_id,
                current_node_id = "east_woods",
            };
            const string itemPath = "res://resources/entities/items/兽骨.tres";
            attacker.change_carried_item(itemPath, 2);
            attacker.personal_relationships[target.npc_id] = -80;

            var manager = new NpcWorldManager();
            AddChild(manager);
            manager.register_npc(attacker, false);
            manager.register_npc(target, false);
            var incident = new EastWoodsNpcIncidentRecord
            {
                incident_id = "law_smoke_case_1",
                occurred_at = "云息二年三月初一",
                location_id = "east_woods",
                location_name = "东林地",
                route_node_id = "root_hollow",
                attacker_npc_id = attacker.npc_id,
                attacker_npc_name = attacker.npc_name,
                attacker_faction_id = config.sect_faction_id,
                target_npc_id = target.npc_id,
                target_npc_name = target.npc_name,
                target_faction_id = config.sect_faction_id,
                attack_trigger_id = config.automatic_attack_trigger_id,
                source_facts_complete = true,
                battle_result = EastWoodsNpcIncidentRecord.ResultAttackerWon,
                target_died = true,
            };
            incident.item_transfers.Add(new EastWoodsNpcItemTransferRecord
            {
                item_path = itemPath,
                quantity = 1,
                from_npc_id = target.npc_id,
                from_npc_name = target.npc_name,
                to_npc_id = attacker.npc_id,
                to_npc_name = attacker.npc_name,
            });
            incident.evidence_source_refs.Add("smoke:structured_battle_transfer");
            Check(manager.record_east_woods_npc_incident(incident), "结构化案卷登记失败");

            var assigned = Guid.NewGuid().ToString("N");
            Check(manager.try_assign_east_woods_incident(incident.incident_id, assigned), "案卷不能由事务板领取");
            Check(manager.try_submit_east_woods_incident(incident.incident_id, assigned, "smoke-submit"), "核验案卷不能上呈");
            Check(manager.try_adjudicate_yuxu_case(incident.incident_id, config, out _), "待裁案卷没有进入规则判定");
            var ruled = manager.get_east_woods_incident(incident.incident_id)!;
            Check(ruled.ruling_rule_ids.Contains(config.unauthorized_attack_rule_id), "自动突袭事实未命中对应条款");
            Check(ruled.ruling_sanction_ids.Contains(config.first_offense_sanction_id), "初犯处分映射不正确");
            Check(ruled.ruling_findings.Contains("死亡结果") && ruled.ruling_findings.Contains("不证明杀意") &&
                ruled.ruling_findings.Contains("不据此直接定故意杀人"), "死亡结果没有被明确限定为非杀意证据");
            Check(manager.try_begin_yuxu_case_enforcement(incident.incident_id, config, out _), "终局裁断没有执行");
            var taskSummary = manager.get_yuxu_case_task_summaries().Single();
            Check(taskSummary["task_type"].AsString() == "restitution", "物品流水没有产生追还外勤");
            Check(!taskSummary["kill_authorized"].AsBool() && taskSummary["reward"].AsInt32() == 0, "案卷外勤不得擅自附加击杀授权或奖励");
            var taskId = taskSummary["task_id"].AsString();
            var taskInstance = Guid.NewGuid().ToString("N");
            Check(manager.try_assign_yuxu_case_task(incident.incident_id, taskId, taskInstance), "追还外勤领取失败");
            Check(manager.try_execute_yuxu_restitution_task(incident.incident_id, taskId, taskInstance, attacker.npc_id, out _), "真实随身物品流水不能追还");
            Check(attacker.get_carried_item_count(itemPath) == 1 && target.get_carried_item_count(itemPath) == 1, "追还不是从被处分人实际物品转给原持有人");
            Check(manager.complete_yuxu_case_task(incident.incident_id, taskId, taskInstance), "已完成外勤不能向事务板回报");
            Check(manager.get_east_woods_incident(incident.incident_id)!.legal_status == YuxuSectDraftLawEvaluator.LegalStatusClosed, "案卷外勤完成后没有结案");

            var incomplete = new EastWoodsNpcIncidentRecord
            {
                incident_id = "law_smoke_incomplete",
                occurred_at = "test",
                location_id = "east_woods",
                route_node_id = "root_hollow",
                attacker_npc_id = attacker.npc_id,
                target_npc_id = target.npc_id,
                attacker_faction_id = config.sect_faction_id,
                target_faction_id = config.sect_faction_id,
                attack_trigger_id = config.automatic_attack_trigger_id,
                source_facts_complete = false,
                battle_result = EastWoodsNpcIncidentRecord.ResultAttackerWon,
            };
            var insufficient = YuxuSectDraftLawEvaluator.Evaluate(incomplete, manager.get_all_east_woods_incidents(), config);
            Check(!insufficient.EvidenceSufficient && insufficient.RuleIds.Count == 0, "不完整旧事实仍触发处分");

            var appealCase = new EastWoodsNpcIncidentRecord
            {
                incident_id = "law_smoke_appeal_case",
                occurred_at = "test",
                location_id = "east_woods",
                route_node_id = "root_hollow",
                attacker_npc_id = attacker.npc_id,
                attacker_npc_name = attacker.npc_name,
                attacker_faction_id = config.sect_faction_id,
                target_npc_id = target.npc_id,
                target_npc_name = target.npc_name,
                target_faction_id = config.sect_faction_id,
                attack_trigger_id = config.automatic_attack_trigger_id,
                source_facts_complete = true,
                battle_result = EastWoodsNpcIncidentRecord.ResultAttackerWon,
            };
            Check(manager.record_east_woods_npc_incident(appealCase), "申诉案卷登记失败");
            var appealAssignment = Guid.NewGuid().ToString("N");
            Check(manager.try_assign_east_woods_incident(appealCase.incident_id, appealAssignment), "申诉案卷不能接取");
            Check(manager.try_submit_east_woods_incident(appealCase.incident_id, appealAssignment, "smoke-submit"), "申诉案卷不能上呈");
            Check(manager.try_adjudicate_yuxu_case(appealCase.incident_id, config, out _), "申诉案卷裁断失败");
            Check(manager.try_file_yuxu_case_appeal(appealCase.incident_id, attacker.npc_id, "request_record_review", out _), "当事人不能递交申诉");
            Check(!manager.try_file_yuxu_case_appeal(appealCase.incident_id, attacker.npc_id, "request_record_review", out _), "同一当事人重复申诉被接受");
            Check(manager.try_review_yuxu_case_appeal(appealCase.incident_id, config, out _), "待复核申诉没有经过官员复核");
            Check(manager.get_east_woods_incident(appealCase.incident_id)!.appeal_status == YuxuSectDraftLawEvaluator.AppealStatusUpheld, "无改变事实的复核结果未按规则维持原裁断");

            var snapshot = new PersistentGameSave { east_woods_npc_incidents = manager.get_persistent_east_woods_incidents() };
            Check(ResourceSaver.Save(snapshot, savePath) == Error.Ok, "案卷与外勤状态未能存档");
            var restored = ResourceLoader.Load<PersistentGameSave>(savePath, "", ResourceLoader.CacheMode.Ignore);
            Check(restored is not null && restored.east_woods_npc_incidents.Any(item => item.incident_id == incident.incident_id && item.sanctions_executed), "裁断执行状态没有随正式存档恢复");
            Check(restored!.east_woods_npc_incidents.Any(item => item.incident_id == appealCase.incident_id && item.appeal_records.Count == 1 && item.appeal_status == YuxuSectDraftLawEvaluator.AppealStatusUpheld), "申诉案卷状态没有随正式存档恢复");
            GD.Print("YUXU_SECT_LAW_SMOKE PASS: structured jurisdiction/evidence, configurable ruling and sanction, one-time appeal/review, actual item restitution, task report, save round-trip");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"YUXU_SECT_LAW_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            if (Godot.FileAccess.FileExists(savePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
        }
    }
}
