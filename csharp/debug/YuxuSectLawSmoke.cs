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
        var journeySavePath = $"user://yuxu_case_transport_smoke_{Guid.NewGuid():N}.tres";
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
            Check(!manager.try_review_yuxu_case_appeal(appealCase.incident_id, config, out _), "没有押送交接的申诉不得复核");
            var transportTask = manager.get_east_woods_incident(appealCase.incident_id)!.field_tasks.Single(task => task.task_type == "appeal_transport");
            Check(!transportTask.kill_authorized && transportTask.contribution_reward == 0, "押送事务不得附加击杀授权或未经配置的奖励");
            var transportInstance = Guid.NewGuid().ToString("N");
            Check(manager.try_assign_yuxu_case_task(appealCase.incident_id, transportTask.task_id, transportInstance), "申诉押送事务不能从和光峰领取");
            var journeyRoute = new Godot.Collections.Array<Vector2I> { new(3, 4), new(4, 4), new(5, 4) };
            Check(manager.join_yuxu_case_task_party(
                appealCase.incident_id, transportTask.task_id, transportInstance, attacker.npc_id,
                journeyRoute[0], "east_woods", "yuxu_xianzong_core", journeyRoute, 24f, false), "涉案当事人没有加入保存的临时队伍状态");
            Check(attacker.temporary_party_task_id == transportTask.task_id && transportTask.temporary_party_member_active &&
                transportTask.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit && attacker.current_grid_cell == journeyRoute[0],
                "同行双方的存档状态和出发坐标没有互相对应");
            Check(manager.update_yuxu_case_transport_position(
                appealCase.incident_id, transportTask.task_id, transportInstance, journeyRoute[1], "", "", 8f), "行进格坐标没有写入同行者");
            Check(attacker.current_grid_cell == journeyRoute[1] && transportTask.journey_next_index == 2 &&
                Math.Abs(transportTask.journey_remaining_hours - 8f) < 0.01f, "路线格进度或剩余时间没有同步保存");

            var journeySnapshot = new PersistentGameSave
            {
                world_npcs = manager.get_persistent_npcs(),
                east_woods_npc_incidents = manager.get_persistent_east_woods_incidents(),
            };
            Check(ResourceSaver.Save(journeySnapshot, journeySavePath) == Error.Ok, "押送途中队伍状态不能写入正式存档");
            var restoredJourney = ResourceLoader.Load<PersistentGameSave>(journeySavePath, "", ResourceLoader.CacheMode.Ignore);
            Check(restoredJourney is not null &&
                restoredJourney.world_npcs.Any(npc => npc.npc_id == attacker.npc_id && npc.temporary_party_task_id == transportTask.task_id && npc.current_grid_cell == journeyRoute[1]) &&
                restoredJourney.east_woods_npc_incidents.Any(item => item.incident_id == appealCase.incident_id &&
                    item.field_tasks.Any(task => task.task_id == transportTask.task_id && task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit && task.journey_next_index == 2)),
                "读档没有恢复正在同行的当事人和自动路线进度");

            var interceptor = new WorldNpcData { npc_id = "law_smoke_interceptor", npc_name = "拦路者", current_node_id = "east_woods" };
            manager.register_npc(interceptor, false);
            Check(manager.mark_yuxu_case_transport_intercepted(
                appealCase.incident_id, transportTask.task_id, transportInstance, interceptor.npc_id, "world_map", journeyRoute[1]), "押送途中敌人拦截没有挂起自动行程");
            Check(manager.apply_yuxu_case_transport_battle_snapshot(new BattleEntityData
            {
                uid = "worldnpc:" + attacker.npc_id,
                current_health = 0,
                max_health = attacker.max_health,
            }), "战斗返回没有同步临时队伍成员快照");
            Check(attacker.is_alive && attacker.current_health == 1 && attacker.temporary_party_task_id == transportTask.task_id,
                "临时队伍成员在拦截战斗中倒地后被记作死亡或退出队伍");
            Check(manager.finish_yuxu_case_transport_interception(
                appealCase.incident_id, transportTask.task_id, transportInstance, "战败不构成拘押；行程恢复。"), "拦截战后没有恢复押送状态");
            Check(transportTask.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit &&
                transportTask.journey_last_interception_result.Contains("不构成拘押"), "战后继续行进状态没有写回案卷");
            Check(!manager.try_review_yuxu_case_appeal(appealCase.incident_id, config, out _), "押送途中不应提前复核申诉");
            Check(manager.update_yuxu_case_transport_position(
                appealCase.incident_id, transportTask.task_id, transportInstance, journeyRoute[2], "yuxu_xianzong_core", "", 0f), "抵达格坐标没有同步给队伍成员");
            Check(manager.mark_yuxu_case_transport_arrived(
                appealCase.incident_id, transportTask.task_id, transportInstance, "yuxu_xianzong_core"), "自动路线抵达执中峰后没有更新状态");
            Check(transportTask.temporary_party_member_active && attacker.temporary_party_task_id == transportTask.task_id,
                "仅抵达地图地点就提前解除了队伍，必须由指定官员接手");
            Check(!manager.handoff_yuxu_case_transport(
                appealCase.incident_id, transportTask.task_id, transportInstance, "heguang", out _), "事务板或其他地点不得冒充执中峰官员交接");
            Check(manager.handoff_yuxu_case_transport(
                appealCase.incident_id, transportTask.task_id, transportInstance, "zhizhong", out var handoffText), "执中峰官员不能接手已抵达的涉案当事人");
            Check(!string.IsNullOrWhiteSpace(handoffText) && transportTask.journey_status == YuxuSectCaseTaskRecord.JourneyHandedOff &&
                !transportTask.temporary_party_member_active && string.IsNullOrEmpty(attacker.temporary_party_task_id),
                "官员交接后临时队伍关系没有解除");
            Check(manager.try_review_yuxu_case_appeal(appealCase.incident_id, config, out _), "官员交接后仍不能复核申诉");
            Check(manager.get_east_woods_incident(appealCase.incident_id)!.appeal_status == YuxuSectDraftLawEvaluator.AppealStatusUpheld, "无改变事实的复核结果未按规则维持原裁断");
            Check(manager.complete_yuxu_case_task(appealCase.incident_id, transportTask.task_id, transportInstance), "交接完成的押送任务不能回事务板回报");
            Check(transportTask.status == YuxuSectCaseTaskRecord.StatusCompleted, "事务板回报后任务状态未完成");

            var snapshot = new PersistentGameSave
            {
                world_npcs = manager.get_persistent_npcs(),
                east_woods_npc_incidents = manager.get_persistent_east_woods_incidents(),
            };
            Check(ResourceSaver.Save(snapshot, savePath) == Error.Ok, "案卷与外勤状态未能存档");
            var restored = ResourceLoader.Load<PersistentGameSave>(savePath, "", ResourceLoader.CacheMode.Ignore);
            Check(restored is not null && restored.east_woods_npc_incidents.Any(item => item.incident_id == incident.incident_id && item.sanctions_executed), "裁断执行状态没有随正式存档恢复");
            Check(restored!.east_woods_npc_incidents.Any(item => item.incident_id == appealCase.incident_id && item.appeal_records.Count == 1 &&
                item.appeal_status == YuxuSectDraftLawEvaluator.AppealStatusUpheld && item.field_tasks.Any(task =>
                    task.task_id == transportTask.task_id && task.status == YuxuSectCaseTaskRecord.StatusCompleted &&
                    task.journey_status == YuxuSectCaseTaskRecord.JourneyHandedOff && task.handoff_node_id == "zhizhong")), "申诉与交接任务状态没有随正式存档恢复");
            Check(restored.world_npcs.Any(npc => npc.npc_id == attacker.npc_id && npc.is_alive &&
                string.IsNullOrEmpty(npc.temporary_party_task_id)), "交接后存档仍把涉案当事人留在临时队伍中");
            GD.Print("YUXU_SECT_LAW_SMOKE PASS: structured case, temporary party join, route progress and save/load, interception continuation, officer handoff, party exit, task report and review gate");
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
            if (Godot.FileAccess.FileExists(journeySavePath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(journeySavePath));
        }
    }
}
