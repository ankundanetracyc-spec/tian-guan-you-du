using Godot;
using System;
using System.Linq;

/// <summary>Runtime integration for the case-authorized appellant transport task.</summary>
public partial class MainScene
{
    private const string YuxuCaseTransportDestinationNodeId = "yuxu_xianzong_core";

    private YuxuSectCaseTaskRecord? get_active_yuxu_case_transport_task()
        => player?.entity_data is HumanData human ? _npc_world?.get_active_yuxu_case_transport(human) : null;

    private void start_yuxu_case_transport(WorldNpcData npc)
    {
        if (player?.entity_data is not HumanData human || _npc_world is null || npc is null ||
            human.sect_affiliation_id != YuxuSectId || npc.is_traveling)
            return;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAssigned ||
            task.target_npc_id != npc.npc_id || task.journey_status != YuxuSectCaseTaskRecord.JourneyNotStarted)
        {
            show_message("案卷押送暂不可启程", "当前没有与此当事人对应的已接取押送事务。");
            return;
        }

        var inDungeon = npc.dungeon_id == EastWoodsDungeonRules.DungeonId;
        if (inDungeon && (_east_woods_dungeon?.Visible != true ||
                npc.dungeon_node_id != _east_woods_dungeon.get_current_route_node_id()))
        {
            show_message("尚未会合", "需在东林地副本内与案卷指定当事人处于同一林径节点，才能建立同行状态。");
            return;
        }
        if (!inDungeon && npc.current_node_id != _player_world_node_id && npc.current_grid_cell != _player_world_grid_cell)
        {
            show_message("尚未会合", "需在案卷指定当事人所在地点与其会合，才能建立同行状态。");
            return;
        }

        var route = new Godot.Collections.Array<Vector2I>();
        var remainingHours = 0f;
        if (inDungeon)
        {
            var steps = _east_woods_dungeon!.get_assigned_route_step_count("forest_edge");
            if (steps < 0)
            {
                show_message("押送路线不可达", "东林地当前已探明的林径没有通往林缘出口的连续路线；当事人尚未加入队伍，事务保持未启程。");
                return;
            }
            remainingHours = steps * EastWoodsDungeonRules.player_edge_hours(player.dun_speed);
        }
        else
        {
            if (tile_world_map is null)
            {
                show_message("押送路线不可用", "大世界地图尚未就绪；当事人尚未加入队伍，事务保持未启程。");
                return;
            }
            if (!tile_world_map.Visible) open_world();
            route = get_yuxu_case_transport_route(YuxuCaseTransportDestinationNodeId);
            if (route.Count == 0)
            {
                show_message("押送路线不可达", "当前地图无法为执中峰官员的交接地点指派连续路线；当事人尚未加入队伍，事务保持未启程。");
                return;
            }
            remainingHours = tile_world_map.Call("get_path_travel_hours", route).AsSingle();
        }

        if (!_npc_world.join_yuxu_case_task_party(
                human.sect_active_yuxu_duty_incident_id,
                task.task_id,
                human.sect_active_yuxu_duty_instance_id,
                npc.npc_id,
                _player_world_grid_cell,
                _player_world_node_id,
                YuxuCaseTransportDestinationNodeId,
                route,
                remainingHours,
                inDungeon))
        {
            show_message("同行状态未建立", "案卷任务、当事人或当前队伍状态已变化；没有启动路线或完成交接。");
            return;
        }

        if (inDungeon)
        {
            _npc_world.update_yuxu_case_transport_position(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                _player_world_grid_cell, _player_world_node_id, _east_woods_dungeon!.get_current_route_node_id(), remainingHours);
            if (_east_woods_dungeon.get_current_route_node_id() == "forest_edge")
            {
                _east_woods_dungeon.close();
                CallDeferred(nameof(start_yuxu_case_transport_world_route));
            }
            else if (!_east_woods_dungeon.begin_assigned_route("forest_edge"))
            {
                _npc_world.reset_yuxu_case_task_party_start(human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id);
                show_message("押送路线未启动", "林径行程未能开始；同行状态已撤销，案卷事务仍未启程。");
                return;
            }
            show_message("案卷押送启程", $"{npc.npc_name} 已作为临时队伍成员同行。系统先指派林径至林缘出口，途中如遭拦截将进入现有战斗；战后继续行程。抵达执中峰后须向官员交接。未授权击杀，也不以战败或死亡作为拘押。");
        }
        else
        {
            if (route.Count == 1)
            {
                _npc_world.mark_yuxu_case_transport_arrived(
                    human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                    YuxuCaseTransportDestinationNodeId);
                _npc_world.update_yuxu_case_transport_position(
                    human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                    _player_world_grid_cell, YuxuCaseTransportDestinationNodeId, "", 0f);
                show_message("案卷押送已抵达", "队伍已抵达执中峰所在地点；请进入执中峰并向指定官员交接。");
            }
            else if (!start_yuxu_case_transport_world_route())
            {
                _npc_world.reset_yuxu_case_task_party_start(human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id);
                show_message("押送路线未启动", "自动路线未能开始；同行状态已撤销，案卷事务仍未启程。");
                return;
            }
            else show_message("案卷押送启程", $"{npc.npc_name} 已作为临时队伍成员同行。系统已指派至执中峰的路线，按现有遁速逐格推进游戏时间；途中如遭拦截将进入现有战斗，战后继续行程。抵达后须向官员交接。未授权击杀，也不以战败或死亡作为拘押。");
        }
        mark_save_dirty();
        refresh_yuxu_life_context();
    }

    private Godot.Collections.Array<Vector2I> get_yuxu_case_transport_route(string destinationNodeId)
    {
        if (tile_world_map is null) return [];
        var route = tile_world_map.Call("get_assigned_route_to_node", destinationNodeId).AsGodotArray<Vector2I>();
        return route.Duplicate();
    }

    private bool start_yuxu_case_transport_world_route()
    {
        if (player?.entity_data is not HumanData human || _npc_world is null || tile_world_map is null) return false;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || !task.temporary_party_member_active ||
            task.journey_status is not (YuxuSectCaseTaskRecord.JourneyExitingDungeon or YuxuSectCaseTaskRecord.JourneyInTransit))
            return false;
        if (!tile_world_map.Visible) open_world();
        var route = get_yuxu_case_transport_route(task.journey_destination_node_id);
        if (route.Count == 0) return false;
        var hours = tile_world_map.Call("get_path_travel_hours", route).AsSingle();
        if (!_npc_world.set_yuxu_case_transport_world_route(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                _player_world_grid_cell, _player_world_node_id, route, hours))
            return false;
        if (route.Count == 1)
        {
            _npc_world.mark_yuxu_case_transport_arrived(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                task.journey_destination_node_id);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("案卷押送已抵达", "队伍已抵达执中峰所在地点；请进入执中峰并向指定官员交接。");
            return true;
        }
        tile_world_map.Call("set_assigned_route_destination", task.journey_destination_node_id);
        if (tile_world_map.Call("start_assigned_route", route, task.journey_destination_node_id).AsBool())
        {
            mark_save_dirty();
            refresh_yuxu_life_context();
            return true;
        }
        return false;
    }

    private void update_yuxu_case_transport_dungeon_position(string dungeonNodeId, float elapsedHours)
    {
        if (player?.entity_data is not HumanData human || _npc_world is null) return;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || task.journey_status != YuxuSectCaseTaskRecord.JourneyExitingDungeon) return;
        var remaining = Mathf.Max(0f, task.journey_remaining_hours - elapsedHours);
        if (_npc_world.update_yuxu_case_transport_position(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                _player_world_grid_cell, _player_world_node_id, dungeonNodeId, remaining))
            mark_save_dirty();
    }

    private void check_yuxu_case_transport_world_interception(Vector2I cell)
    {
        if (tile_world_map?.Call("is_traveling").AsBool() != true || player?.entity_data is not HumanData human || _npc_world is null)
            return;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || task.journey_status != YuxuSectCaseTaskRecord.JourneyInTransit) return;
        var interception = _npc_world.try_find_yuxu_case_transport_interceptor(
            cell, human, human.sect_active_yuxu_duty_incident_id, task.task_id);
        if (interception.Count == 0) return;
        var attackerId = interception.GetValueOrDefault("attacker_npc_id", "").AsString();
        if (!_npc_world.mark_yuxu_case_transport_intercepted(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                attackerId, "world_map", cell))
            return;
        tile_world_map.Call("interrupt_assigned_route");
        mark_save_dirty();
        CallDeferred(nameof(start_yuxu_case_transport_interception_battle), attackerId, "world_map", cell);
    }

    private bool try_mark_yuxu_case_transport_dungeon_interception(string attackerNpcId)
    {
        if (player?.entity_data is not HumanData human || _npc_world is null) return false;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || task.journey_status != YuxuSectCaseTaskRecord.JourneyExitingDungeon) return false;
        return _npc_world.mark_yuxu_case_transport_intercepted(
            human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
            attackerNpcId, "east_woods_dungeon", _player_world_grid_cell);
    }

    private void start_yuxu_case_transport_interception_battle(string attackerNpcId, string source, Vector2I cell)
    {
        if (player is null || _npc_world is null || player.entity_data is not HumanData human) return;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        var attacker = _npc_world.get_npc(attackerNpcId);
        var partyNpc = task is null ? null : _npc_world.get_npc(task.journey_party_npc_id);
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        if (task is null || attacker is null || partyNpc is null || transition is null ||
            task.journey_status != YuxuSectCaseTaskRecord.JourneyIntercepted ||
            partyNpc.temporary_party_task_id != task.task_id)
            return;

        var (attackerScene, attackerSnapshot) = create_npc_battle_package(attacker, (int)WorldEntity.Faction.ENEMY);
        var (partyScene, partySnapshot) = create_npc_battle_package(partyNpc, (int)WorldEntity.Faction.PLAYER);
        var playerScene = player.pack_for_battle();
        var playerSnapshot = player.get_snapshot_data();
        if (attackerScene is null || attackerSnapshot is null || partyScene is null || partySnapshot is null ||
            playerScene is null || playerSnapshot is null) return;
        var routeState = source == "east_woods_dungeon"
            ? (_east_woods_dungeon?.get_route_state() ?? _east_woods_dungeon_state).Duplicate(true)
            : new Godot.Collections.Dictionary();
        transition.set_battle_return_context(new Godot.Collections.Dictionary
        {
            ["kind"] = "yuxu_case_transport_interception",
            ["incident_id"] = human.sect_active_yuxu_duty_incident_id,
            ["task_id"] = task.task_id,
            ["duty_instance_id"] = human.sect_active_yuxu_duty_instance_id,
            ["attacker_npc_id"] = attackerNpcId,
            ["party_npc_id"] = partyNpc.npc_id,
            ["source"] = source,
            ["cell"] = cell,
            ["route_state"] = routeState,
        });
        var attackers = new Godot.Collections.Array<PackedScene> { attackerScene };
        var defenders = new Godot.Collections.Array<PackedScene> { playerScene, partyScene };
        var attackerData = new Godot.Collections.Array { attackerSnapshot };
        var defenderData = new Godot.Collections.Array { playerSnapshot, partySnapshot };
        var worldPath = GetTree().CurrentScene?.SceneFilePath ?? "res://tscn/scene/MainScene.tscn";
        transition.start_team_battle(attackers, defenders, attackerData, defenderData, worldPath);
    }

    private bool handle_yuxu_case_transport_battle_return(
        Godot.Collections.Array attackers,
        Godot.Collections.Array defenders,
        Godot.Collections.Dictionary summary,
        Godot.Collections.Dictionary context)
    {
        if (context.GetValueOrDefault("kind", "").AsString() != "yuxu_case_transport_interception" || _npc_world is null)
            return false;
        foreach (var value in attackers.Concat(defenders))
            if (value.AsGodotObject() is BattleEntityData snapshot)
                _npc_world.apply_yuxu_case_transport_battle_snapshot(snapshot);

        var incidentId = context.GetValueOrDefault("incident_id", "").AsString();
        var taskId = context.GetValueOrDefault("task_id", "").AsString();
        var dutyInstanceId = context.GetValueOrDefault("duty_instance_id", "").AsString();
        var resultText = summary.GetValueOrDefault("escaped", false).AsBool()
            ? "玩家撤离战斗；未发生拘押，押送继续。"
            : summary.GetValueOrDefault("player_victory", false).AsBool()
                ? "玩家赢下拦截战；未发生拘押，押送继续。"
                : summary.GetValueOrDefault("player_defeated", false).AsBool()
                    ? "玩家在拦截战中战败；战败不构成拘押，押送继续。"
                    : "战斗未分出胜负；未发生拘押，押送继续。";
        _npc_world.finish_yuxu_case_transport_interception(incidentId, taskId, dutyInstanceId, resultText);
        var source = context.GetValueOrDefault("source", "").AsString();
        if (source == "east_woods_dungeon")
        {
            _east_woods_dungeon_state = context.GetValueOrDefault("route_state", new Godot.Collections.Dictionary()).AsGodotDictionary().Duplicate(true);
            var dungeon = get_or_create_east_woods_dungeon();
            dungeon.restore_route_state(_east_woods_dungeon_state);
            dungeon.set_player_dun_speed(player?.dun_speed ?? EastWoodsDungeonRules.BaseDunSpeed);
            dungeon.set_npc_encounter_busy(false);
            dungeon.show_dungeon();
            refresh_east_woods_route_roster(dungeon.get_current_route_node_id());
        }
        else open_world();

        mark_save_dirty();
        refresh_yuxu_life_context();
        show_message("押送途中拦截", resultText + " 该战斗未授予击杀案卷目标的权限；须抵达执中峰并由指定官员接手，事务才算交接。");
        CallDeferred(nameof(continue_yuxu_case_transport_after_interception));
        return true;
    }

    private void continue_yuxu_case_transport_after_interception()
    {
        if (player?.entity_data is not HumanData human || _npc_world is null) return;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || !task.temporary_party_member_active) return;
        if (task.journey_status == YuxuSectCaseTaskRecord.JourneyExitingDungeon)
        {
            if (!_east_woods_dungeon_state.GetValueOrDefault("inside", false).AsBool())
            {
                if (!start_yuxu_case_transport_world_route())
                    show_message("押送暂停", "读档后大世界路线暂不可用；同行状态和案卷进度已恢复，未完成交接。");
                return;
            }
            var dungeon = get_or_create_east_woods_dungeon();
            if (!dungeon.Visible) dungeon.show_dungeon();
            if (dungeon.get_current_route_node_id() == "forest_edge")
            {
                dungeon.close();
                CallDeferred(nameof(start_yuxu_case_transport_world_route));
            }
            else if (!dungeon.begin_assigned_route("forest_edge"))
                show_message("押送暂停", "林径出口路线暂不可用；同行状态与案卷记录已保留，可在东林地恢复路线后继续。");
        }
        else if (task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit)
        {
            if (_player_world_node_id == task.journey_destination_node_id)
            {
                _npc_world.mark_yuxu_case_transport_arrived(
                    human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                    task.journey_destination_node_id);
                mark_save_dirty();
                refresh_yuxu_life_context();
                return;
            }
            var remaining = get_remaining_yuxu_case_transport_route(task);
            if (remaining.Count < 2 || tile_world_map is null ||
                !tile_world_map.Call("start_assigned_route", remaining, task.journey_destination_node_id).AsBool())
                show_message("押送暂停", "大世界路线暂不可用；同行状态与路线进度已保留，未登记到达或交接。");
        }
    }

    private Godot.Collections.Array<Vector2I> get_remaining_yuxu_case_transport_route(YuxuSectCaseTaskRecord task)
    {
        var remaining = new Godot.Collections.Array<Vector2I>();
        if (task.journey_route.Count == 0) return remaining;
        var currentIndex = Math.Clamp(task.journey_next_index - 1, 0, task.journey_route.Count - 1);
        if (task.journey_route[currentIndex] != _player_world_grid_cell)
        {
            currentIndex = -1;
            for (var index = 0; index < task.journey_route.Count; index++)
                if (task.journey_route[index] == _player_world_grid_cell) { currentIndex = index; break; }
            if (currentIndex < 0) return remaining;
        }
        for (var index = currentIndex; index < task.journey_route.Count; index++) remaining.Add(task.journey_route[index]);
        return remaining;
    }

    private void resume_yuxu_case_transport_after_load()
    {
        if (player?.entity_data is not HumanData human || _npc_world is null) return;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || !task.temporary_party_member_active) return;
        if (task.journey_status == YuxuSectCaseTaskRecord.JourneyIntercepted)
        {
            start_yuxu_case_transport_interception_battle(task.journey_interception_npc_id,
                task.journey_interception_source, task.journey_interception_cell);
            return;
        }
        if (task.journey_status == YuxuSectCaseTaskRecord.JourneyExitingDungeon)
        {
            var dungeon = get_or_create_east_woods_dungeon();
            if (!dungeon.Visible) dungeon.show_dungeon();
            if (dungeon.get_current_route_node_id() == "forest_edge")
            {
                dungeon.close();
                CallDeferred(nameof(start_yuxu_case_transport_world_route));
            }
            else if (!dungeon.is_route_traveling() && !dungeon.begin_assigned_route("forest_edge"))
                show_message("押送暂停", "读档后没有可用的林径出口路线；同行状态和案卷进度已恢复，未完成交接。");
            return;
        }
        if (task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit)
        {
            tile_world_map?.Call("set_assigned_route_destination", task.journey_destination_node_id);
            if (_player_world_node_id == task.journey_destination_node_id)
            {
                _npc_world.mark_yuxu_case_transport_arrived(
                    human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                    task.journey_destination_node_id);
                return;
            }
            var remaining = get_remaining_yuxu_case_transport_route(task);
            if (remaining.Count < 2) remaining = get_yuxu_case_transport_route(task.journey_destination_node_id);
            if (tile_world_map?.Visible != true) open_world();
            if (remaining.Count > 1 && tile_world_map?.Call("start_assigned_route", remaining, task.journey_destination_node_id).AsBool() == true)
                return;
            show_message("押送暂停", "读档后自动路线未能恢复；当事人仍作为临时队伍成员保存，案卷未登记到达。");
        }
    }

    private bool handoff_active_yuxu_case_transport(HumanData human, out string resultText)
    {
        resultText = "";
        if (_npc_world is null || !is_yuxu_member_at_peak_interior() || _player_current_yuxu_peak_id != "zhizhong") return false;
        var task = _npc_world.get_active_yuxu_case_transport(human);
        if (task is null || task.status != YuxuSectCaseTaskRecord.StatusAssigned ||
            task.journey_status != YuxuSectCaseTaskRecord.JourneyArrived) return false;
        if (!_npc_world.handoff_yuxu_case_transport(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                "zhizhong", out resultText))
            return false;
        human.sect_active_yuxu_duty_ready = true;
        mark_save_dirty();
        refresh_yuxu_life_context();
        return true;
    }
}
