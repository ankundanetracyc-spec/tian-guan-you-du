using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Runtime glue for official case review and evidence-backed field tasks.</summary>
public partial class MainScene
{
    private bool handle_yuxu_case_task_heguang_action(string actionId, HumanData human)
    {
        const string acceptPrefix = "heguang_accept_case_field:";
        const string cancelPrefix = "heguang_cancel_case_field:";
        const string submitPrefix = "heguang_submit_case_field:";
        if (actionId.StartsWith(acceptPrefix, StringComparison.Ordinal))
        {
            if (!try_parse_yuxu_case_field_key(actionId[acceptPrefix.Length..], out var incidentId, out var taskId)) return true;
            if (!is_at_yuxu_heguang_duty_board()) return true;
            if (_npc_world is null || player?.entity_data != human || human.sect_affiliation_id != YuxuSectId) return true;
            if (!string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_id))
            {
                show_message("宗门事务", "你已有一项事务在办；可先回和光峰取消或交回当前事务。");
                return true;
            }
            var task = _npc_world.get_yuxu_case_task(incidentId, taskId);
            if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAvailable)
            {
                refresh_yuxu_life_context();
                show_message("案卷事务已不可接取", "该外勤已被接取、完成或撤回；没有产生奖励或改变案卷事实。");
                return true;
            }
            var instanceId = Guid.NewGuid().ToString("N");
            if (!_npc_world.try_assign_yuxu_case_task(incidentId, taskId, instanceId))
            {
                refresh_yuxu_life_context();
                show_message("案卷事务已不可接取", "该外勤不再处于可领取状态；事务板已刷新。");
                return true;
            }
            human.sect_active_yuxu_duty_id = YuxuCaseFieldTaskDutyId;
            human.sect_active_yuxu_duty_instance_id = instanceId;
            human.sect_active_yuxu_duty_incident_id = incidentId;
            human.sect_active_yuxu_duty_task_id = taskId;
            human.sect_active_yuxu_duty_started_hour = -1;
            human.sect_active_yuxu_duty_stage = 0;
            human.sect_active_yuxu_duty_ready = false;
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("接取案卷外勤", $"已接取「{task.title}」。{task.goal} {task.completion_conditions} 击杀授权：{(task.kill_authorized ? "依文书授权" : "未授权；不得击杀目标")}。奖励：{task.contribution_reward} 点贡献。");
            return true;
        }

        if (actionId.StartsWith(cancelPrefix, StringComparison.Ordinal))
        {
            if (!try_parse_yuxu_case_field_key(actionId[cancelPrefix.Length..], out var incidentId, out var taskId)) return true;
            if (human.sect_active_yuxu_duty_id != YuxuCaseFieldTaskDutyId ||
                human.sect_active_yuxu_duty_incident_id != incidentId || human.sect_active_yuxu_duty_task_id != taskId ||
                _npc_world?.release_yuxu_case_task(incidentId, taskId, human.sect_active_yuxu_duty_instance_id) != true)
                return true;
            clear_active_yuxu_duty(human);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("取消接取", "案卷外勤已退回事务板，没有执行处分、追还或奖励结算。");
            return true;
        }

        if (actionId.StartsWith(submitPrefix, StringComparison.Ordinal))
        {
            if (!try_parse_yuxu_case_field_key(actionId[submitPrefix.Length..], out var incidentId, out var taskId)) return true;
            if (!is_at_yuxu_heguang_duty_board() || player?.entity_data != human || human.sect_affiliation_id != YuxuSectId ||
                human.sect_active_yuxu_duty_id != YuxuCaseFieldTaskDutyId ||
                human.sect_active_yuxu_duty_incident_id != incidentId || human.sect_active_yuxu_duty_task_id != taskId ||
                !human.sect_active_yuxu_duty_ready ||
                _npc_world?.complete_yuxu_case_task(incidentId, taskId, human.sect_active_yuxu_duty_instance_id) != true)
                return true;
            clear_active_yuxu_duty(human);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("案卷外勤已回报", "执中峰案卷已记录外勤结果；此任务未配置额外贡献或物资奖励。");
            return true;
        }

        return false;
    }

    private void on_yuxu_case_docket_action(string actionId, HumanData human)
    {
        if (_player_current_yuxu_peak_id != "zhizhong" || !is_yuxu_member_at_peak_interior()) return;
        var manager = _npc_world ?? GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        if (manager is null) return;
        var config = YuxuSectDraftLawEvaluator.LoadConfig();
        var cases = manager.get_all_east_woods_incidents();

        if (actionId == "zhizhong_case_handoff")
        {
            if (handoff_active_yuxu_case_transport(human, out var handoffText))
                show_message("执中峰正式交接", handoffText + " 案卷外勤可回和光峰事务板回报；交接完成后官员方可复核申诉。");
            else
                show_message("交接暂未完成", "只有押送队伍抵达执中峰指定地点、且尚在临时队伍中的案卷当事人，才能由执中峰官员接手。");
            return;
        }

        if (actionId == "zhizhong_case_docket")
        {
            var waitingRuling = cases.FirstOrDefault(item => item.legal_status == YuxuSectDraftLawEvaluator.LegalStatusPending);
            if (waitingRuling is not null && manager.try_adjudicate_yuxu_case(waitingRuling.incident_id, config, out var rulingText))
            {
                mark_save_dirty();
                refresh_yuxu_life_context();
                show_message("执中峰裁断", rulingText);
                return;
            }
            var waitingAppeal = cases.FirstOrDefault(item => item.legal_status == YuxuSectDraftLawEvaluator.LegalStatusAppealPending);
            if (waitingAppeal is not null && manager.try_review_yuxu_case_appeal(waitingAppeal.incident_id, config, out var appealText))
            {
                mark_save_dirty();
                refresh_yuxu_life_context();
                show_message("申诉复核", appealText);
                return;
            }
            show_message("执中峰案卷", "当前没有待裁断或待复核案卷。官员按案卷事实和配置门规处理案件，弟子不选择责任结论。");
            return;
        }

        if (actionId == "zhizhong_case_enforce")
        {
            var ready = cases.FirstOrDefault(item => !item.sanctions_executed &&
                item.legal_status is YuxuSectDraftLawEvaluator.LegalStatusRuled or YuxuSectDraftLawEvaluator.LegalStatusAppealReviewed);
            if (ready is not null && manager.try_begin_yuxu_case_enforcement(ready.incident_id, config, out var executionText))
            {
                mark_save_dirty();
                refresh_yuxu_life_context();
                show_message("执中峰处分执行", executionText);
                return;
            }
            show_message("执中峰处分执行", "当前没有可执行的终局裁断。待申诉复核的案件须先经官员复核；已开始执行的案件不能再次执行。");
        }
    }

    private bool try_execute_yuxu_case_task_at_npc(WorldNpcData npc)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            human.sect_active_yuxu_duty_id != YuxuCaseFieldTaskDutyId ||
            string.IsNullOrWhiteSpace(human.sect_active_yuxu_duty_task_id) || npc is null ||
            npc.current_node_id != _player_world_node_id || npc.is_traveling)
            return false;
        var task = _npc_world?.get_yuxu_case_task(human.sect_active_yuxu_duty_incident_id, human.sect_active_yuxu_duty_task_id);
        if (task is null || task.target_npc_id != npc.npc_id || task.status != YuxuSectCaseTaskRecord.StatusAssigned ||
            task.duty_instance_id != human.sect_active_yuxu_duty_instance_id)
            return false;
        if (task.task_type != "restitution") return false;
        var resultText = "";
        if (_npc_world?.try_execute_yuxu_restitution_task(
            human.sect_active_yuxu_duty_incident_id,
            task.task_id,
            human.sect_active_yuxu_duty_instance_id,
            npc.npc_id,
            out resultText) != true)
        {
            if (!string.IsNullOrWhiteSpace(resultText)) show_message("案卷追还未完成", resultText);
            return false;
        }
        human.sect_active_yuxu_duty_ready = true;
        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("原物追还", resultText);
        return true;
    }

    private void add_yuxu_case_npc_actions(WorldNpcData npc, List<(string, Action)> actions)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId ||
            npc is null || npc.is_traveling || _npc_world is null)
            return;

        var isAtCurrentDungeonNode = npc.dungeon_id == EastWoodsDungeonRules.DungeonId &&
            _east_woods_dungeon?.Visible == true && npc.dungeon_node_id == _east_woods_dungeon.get_current_route_node_id();
        if (!isAtCurrentDungeonNode && npc.current_node_id != _player_world_node_id) return;

        var activeTask = _npc_world.get_yuxu_case_task(human.sect_active_yuxu_duty_incident_id, human.sect_active_yuxu_duty_task_id);
        if (human.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId && activeTask is not null &&
            activeTask.status == YuxuSectCaseTaskRecord.StatusAssigned && activeTask.target_npc_id == npc.npc_id &&
            activeTask.task_type == "restitution")
            actions.Add(("执行案卷追还", () => try_execute_yuxu_case_task_at_npc(npc)));
        else if (human.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId && activeTask is not null &&
            activeTask.status == YuxuSectCaseTaskRecord.StatusAssigned && activeTask.target_npc_id == npc.npc_id &&
            activeTask.task_type == "appeal_transport" && activeTask.journey_status == YuxuSectCaseTaskRecord.JourneyNotStarted)
            actions.Add(("接纳当事人并启程押送", () => start_yuxu_case_transport(npc)));

        var config = YuxuSectDraftLawEvaluator.LoadConfig();
        var appealCase = _npc_world.get_all_east_woods_incidents().FirstOrDefault(incident =>
            !incident.sanctions_executed &&
            incident.legal_status is YuxuSectDraftLawEvaluator.LegalStatusRuled or YuxuSectDraftLawEvaluator.LegalStatusAppealReviewed &&
            (incident.attacker_npc_id == npc.npc_id || incident.target_npc_id == npc.npc_id) &&
            (incident.appeal_records?.Count(record => record is not null && record.appellant_npc_id == npc.npc_id) ?? 0) < Math.Max(1, config.appeals_per_party));
        if (appealCase is not null)
            actions.Add(("提交案卷申诉", () =>
            {
                if (_npc_world?.try_file_yuxu_case_appeal(appealCase.incident_id, npc.npc_id, "request_record_review", out var text) == true)
                {
                    mark_save_dirty();
                    refresh_yuxu_life_context();
                    show_message("申诉已收件", text);
                }
                else show_message("申诉未受理", "当事人申诉次数已用尽、处分已经开始执行，或案卷状态已变化。");
            }));
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> get_yuxu_case_task_summaries()
        => _npc_world?.get_yuxu_case_task_summaries(
            player?.entity_data is HumanData human && human.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId ? human.sect_active_yuxu_duty_incident_id : "",
            player?.entity_data is HumanData activeHuman && activeHuman.sect_active_yuxu_duty_id == YuxuCaseFieldTaskDutyId ? activeHuman.sect_active_yuxu_duty_task_id : "")
            ?? new Godot.Collections.Array<Godot.Collections.Dictionary>();

    private bool is_at_yuxu_heguang_duty_board()
    {
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        return _player_current_yuxu_peak_id == "heguang" && overview?.Visible == true &&
            overview.GetNodeOrNull<Control>("PeakInterior")?.Visible == true && _east_woods_dungeon?.Visible != true;
    }

    private bool is_yuxu_member_at_peak_interior()
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != YuxuSectId) return false;
        var overview = GetNodeOrNull<Control>("SceneLayer/YuxuXianzongOverview");
        return overview?.Visible == true && overview.GetNodeOrNull<Control>("PeakInterior")?.Visible == true;
    }

    private static bool try_parse_yuxu_case_field_key(string payload, out string incidentId, out string taskId)
    {
        incidentId = "";
        taskId = "";
        var separator = payload.IndexOf('|');
        if (separator <= 0 || separator >= payload.Length - 1) return false;
        incidentId = payload[..separator];
        taskId = payload[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(incidentId) && !string.IsNullOrWhiteSpace(taskId);
    }
}
