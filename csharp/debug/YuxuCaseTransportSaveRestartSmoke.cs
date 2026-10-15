using Godot;
using System;
using System.Linq;

/// <summary>Writes or reloads a real save slot to verify case transport state across process restarts.</summary>
public partial class YuxuCaseTransportSaveRestartSmoke : Node
{
    private const string MarkerPath = "user://yuxu_case_transport_restart_slot.txt";

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public override void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var mode = args.Length > 0 ? args[0] : "";
            if (mode == "write") write_save_slot();
            else if (mode == "read") read_save_slot();
            else throw new Exception("请使用 write 或 read 阶段参数运行此烟测。");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"YUXU_CASE_TRANSPORT_RESTART_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void write_save_slot()
    {
        var saves = GetSaveManager();
        saves.begin_new_game("押送跨进程烟测");
        var slotId = saves.active_slot_id;
        var taskId = "appeal_transport:restart_smoke:1";
        var dutyId = Guid.NewGuid().ToString("N");
        var incidentId = "restart_smoke_case";
        var npcId = "restart_smoke_appellant";
        var destination = new Vector2I(5, 4);
        var current = new Vector2I(4, 4);
        var route = new Godot.Collections.Array<Vector2I> { new(3, 4), current, destination };

        var player = new HumanData
        {
            sect_active_yuxu_duty_id = "yuxu_case_field_task",
            sect_active_yuxu_duty_instance_id = dutyId,
            sect_active_yuxu_duty_incident_id = incidentId,
            sect_active_yuxu_duty_task_id = taskId,
        };
        var npc = new WorldNpcData
        {
            npc_id = npcId,
            npc_name = "存档烟测当事人",
            current_node_id = "east_woods",
            current_grid_cell = current,
            has_world_grid_position = true,
            temporary_party_task_id = taskId,
        };
        var task = new YuxuSectCaseTaskRecord
        {
            task_id = taskId,
            task_type = "appeal_transport",
            title = "存档烟测押送",
            target_npc_id = npcId,
            status = YuxuSectCaseTaskRecord.StatusAssigned,
            duty_instance_id = dutyId,
            temporary_party_member_active = true,
            journey_party_npc_id = npcId,
            journey_destination_node_id = "yuxu_xianzong_core",
            journey_route = route,
            journey_next_index = 2,
            journey_current_cell = current,
            journey_remaining_hours = 8f,
            journey_status = YuxuSectCaseTaskRecord.JourneyInTransit,
        };
        var incident = new EastWoodsNpcIncidentRecord
        {
            incident_id = incidentId,
            occurred_at = "烟测",
            location_id = "east_woods",
            route_node_id = "root_hollow",
            attacker_npc_id = npcId,
            target_npc_id = "other_case_party",
            appellant_npc_id = npcId,
            appeal_status = YuxuSectDraftLawEvaluator.AppealStatusPending,
        };
        incident.field_tasks.Add(task);

        EastWoodsDungeonRules.generate_layout($"restart-smoke:{slotId}");
        var dungeonState = new Godot.Collections.Dictionary
        {
            ["inside"] = false,
            ["layout"] = EastWoodsDungeonRules.get_layout_state(),
        };
        var save = new PersistentGameSave
        {
            player_data = player,
            save_name = "押送跨进程烟测",
            current_node_id = "east_woods",
            world_cell = current,
            remaining_travel_path = new Godot.Collections.Array<Vector2I> { current, destination },
            world_npcs = new Godot.Collections.Array<WorldNpcData> { npc },
            east_woods_npc_incidents = new Godot.Collections.Array<EastWoodsNpcIncidentRecord> { incident },
            scene_progress = new Godot.Collections.Dictionary { ["east_woods_dungeon_state"] = dungeonState },
        };
        Check(saves.save_snapshot(save), "正式存档管理器没有写入押送进度");
        using var marker = Godot.FileAccess.Open(MarkerPath, Godot.FileAccess.ModeFlags.Write);
        Check(marker is not null, "无法保存跨进程烟测的存档槽标识");
        marker!.StoreString(slotId);
        marker.Flush();
        GD.Print($"YUXU_CASE_TRANSPORT_RESTART_SMOKE WRITE_PASS: {slotId}");
    }

    private static void read_save_slot()
    {
        var slotId = Godot.FileAccess.GetFileAsString(MarkerPath).Trim();
        Check(!string.IsNullOrWhiteSpace(slotId), "没有找到上一进程写入的存档槽标识");
        var saves = GetSaveManager();
        Check(saves.load_slot(slotId), "新进程未能通过正式存档管理器载入押送存档槽");
        var save = saves.consume_pending_load();
        Check(save?.player_data is not null, "正式存档没有恢复玩家数据");

        const string incidentId = "restart_smoke_case";
        const string taskId = "appeal_transport:restart_smoke:1";
        const string npcId = "restart_smoke_appellant";
        var npc = save!.world_npcs.SingleOrDefault(item => item.npc_id == npcId);
        var task = save.east_woods_npc_incidents.SingleOrDefault(item => item.incident_id == incidentId)?
            .field_tasks.SingleOrDefault(item => item.task_id == taskId);
        Check(npc is not null && npc.temporary_party_task_id == taskId && npc.current_grid_cell == save.world_cell,
            "新进程没有恢复 NPC 的临时队伍关联和当前位置");
        Check(task is not null && task.temporary_party_member_active &&
              task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit &&
              task.journey_route.Count == 3 && task.journey_next_index == 2 &&
              task.journey_current_cell == save.world_cell && Math.Abs(task.journey_remaining_hours - 8f) < 0.01f,
            "新进程没有恢复押送任务的路线游标和剩余时间");
        Check(save.player_data!.sect_active_yuxu_duty_incident_id == incidentId &&
              save.player_data.sect_active_yuxu_duty_task_id == taskId &&
              save.remaining_travel_path.Count == 2 && save.remaining_travel_path[0] == save.world_cell,
            "新进程恢复的玩家当前事务与地图待行路线不一致");

        var slotPath = ProjectSettings.GlobalizePath($"user://game_saves/{slotId}.tres");
        DirAccess.RemoveAbsolute(slotPath);
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(MarkerPath));
        GD.Print("YUXU_CASE_TRANSPORT_RESTART_SMOKE READ_PASS: fresh process loaded the formal slot with the appellant in the temporary party and route progress intact");
    }

    private static GameSaveManager GetSaveManager()
        => Engine.GetMainLoop() is SceneTree tree
            ? tree.Root.GetNodeOrNull<GameSaveManager>("GameSaveManager") ?? throw new Exception("GameSaveManager autoload is unavailable")
            : throw new Exception("SceneTree is unavailable");
}
