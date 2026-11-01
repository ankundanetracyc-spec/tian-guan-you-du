using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;

/// <summary>Exercises the formal scene's case-party travel and saved node state.</summary>
public partial class BaiChuanCaseTransportNodeSmoke : Node
{
    private static object? Invoke(MainScene owner, string method, params object[] args) =>
        typeof(MainScene).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    public override void _Ready() => CallDeferred(nameof(Run));

    private async void Run()
    {
        const string incidentId = "node_transport_test_case", taskId = "node_transport_test_task", dutyId = "node_transport_test_duty", npcId = "node_transport_test_npc";
        const string savePath = "user://node_transport_test.tres";
        try
        {
            var main = GD.Load<PackedScene>("res://tscn/scene/MainScene.tscn").Instantiate<MainScene>();
            AddChild(main);
            var deadline = Time.GetTicksMsec() + 90000UL;
            while (!main.StartupComplete && Time.GetTicksMsec() < deadline) await Frame();
            Check(main.StartupComplete && main.StartupError is null, "主场景启动失败");
            var stage = typeof(MainScene).GetField("_qingxi_prologue", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(main, Enum.Parse(stage.FieldType, "Departed"));
            Invoke(main, "open_world");
            await Frame();
            var map = main.GetNode<Control>("MapLayer/BaiChuanNodeWorld");
            var state = new Godot.Collections.Dictionary { ["current_node"] = "bc_place_070", ["remaining_route"] = new Godot.Collections.Array<string>() };
            Check(map.Call("restore_node_state", state).AsBool(), "未能移动到东林地测试起点");
            Invoke(main, "on_node_world_arrived", "bc_place_070");
            var player = (Character)typeof(MainScene).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            var human = player.entity_data as HumanData ?? throw new InvalidOperationException("主角数据缺失");
            human.sect_affiliation_id = "yuxu_xianzong";
            human.sect_active_yuxu_duty_id = "yuxu_case_field_task";
            human.sect_active_yuxu_duty_incident_id = incidentId;
            human.sect_active_yuxu_duty_task_id = taskId;
            human.sect_active_yuxu_duty_instance_id = dutyId;
            var world = GetNode<NpcWorldManager>("/root/NpcWorldManager");
            world.set_simulation_enabled(false);
            // Keep the route deterministic; unrelated generated NPCs can trigger combat.
            world.npcs.Clear();
            var npc = new WorldNpcData { npc_id = npcId, npc_name = "押送测试人物", current_node_id = "east_woods", origin_node_id = "east_woods" };
            world.register_npc(npc, false);
            var incident = new EastWoodsNpcIncidentRecord
            {
                incident_id = incidentId, occurred_at = "测试", location_id = "east_woods", attacker_npc_id = npcId,
                target_npc_id = "node_transport_test_target", appellant_npc_id = npcId,
            };
            incident.field_tasks.Add(new YuxuSectCaseTaskRecord
            {
                task_id = taskId, task_type = "appeal_transport", target_npc_id = npcId,
                status = YuxuSectCaseTaskRecord.StatusAssigned, duty_instance_id = dutyId,
            });
            Check(world.record_east_woods_npc_incident(incident), "案卷创建失败");
            var task = world.get_yuxu_case_task(incidentId, taskId)!;
            var finished = ToSignal(map, "node_travel_finished");
            var stopOnce = true;
            map.Connect("node_step_committed", Callable.From<string>(_ =>
            {
                if (!stopOnce) return;
                stopOnce = false;
                map.Call("interrupt_assigned_route");
            }));
            Invoke(main, "start_yuxu_case_transport", npc);
            await finished;
            Check(task.temporary_party_member_active && task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit,
                "节点押送没有建立同行状态");
            var currentId = map.Call("get_current_node").AsString();
            Check(currentId != "bc_place_070" && task.journey_current_cell == world.node_world_cell_for_node_id(currentId) && task.journey_next_index > 1,
                "押送中断后队伍位置或路线进度未同步");
            var interceptor = new WorldNpcData { npc_id = "node_transport_test_interceptor", npc_name = "拦截者", current_node_id = "east_woods" };
            world.register_npc(interceptor, false);
            interceptor.has_world_grid_position = false;
            Check(world.mark_yuxu_case_transport_intercepted(incidentId, taskId, dutyId, interceptor.npc_id, "world_map", task.journey_current_cell),
                "无法建立拦截战状态");
            var save = main.capture_battle_world_snapshot();
            Check(ResourceSaver.Save(save!, savePath) == Error.Ok, "中断状态保存失败");
            var loaded = ResourceLoader.Load<PersistentGameSave>(savePath, "", ResourceLoader.CacheMode.Ignore);
            Check(loaded is not null && loaded.node_world_state.GetValueOrDefault("current_node", "").AsString() == currentId &&
                loaded.east_woods_npc_incidents.Any(item => item.incident_id == incidentId && item.field_tasks.Any(t => t.task_id == taskId && t.journey_next_index == task.journey_next_index && t.journey_status == YuxuSectCaseTaskRecord.JourneyIntercepted)),
                "读回的玩家节点与押送进度不一致");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(savePath));
            finished = ToSignal(map, "node_travel_finished");
            var context = new Godot.Collections.Dictionary
            {
                ["kind"] = "yuxu_case_transport_interception", ["incident_id"] = incidentId,
                ["task_id"] = taskId, ["duty_instance_id"] = dutyId, ["source"] = "world_map",
            };
            var summary = new Godot.Collections.Dictionary { ["player_victory"] = true };
            Check((bool)Invoke(main, "handle_yuxu_case_transport_battle_return", new Godot.Collections.Array(),
                new Godot.Collections.Array(), summary, context)!, "拦截战返回未进入押送恢复链");
            await finished;
            Check(map.Call("get_current_node").AsString() == "bc_place_001" && task.journey_status == YuxuSectCaseTaskRecord.JourneyArrived,
                $"中断后未继续走到玉虚或未登记抵达：节点 {map.Call("get_current_node").AsString()}，任务 {task.journey_status}");
            Check(task.journey_last_interception_result.Contains("未发生拘押"), "拦截战结果未保存");
            GD.Print("BAICHUAN_CASE_TRANSPORT_NODE_SMOKE_OK");
            main.QueueFree();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("BAICHUAN_CASE_TRANSPORT_NODE_SMOKE_FAILED: " + e); GetTree().Quit(1); }
    }
}
