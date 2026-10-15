using Godot;
using System;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Deterministic lifecycle, layout, ecology and save migration smoke for Mist Hidden Valley.</summary>
public partial class DynamicDungeonExpeditionSmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public override void _Ready()
    {
        try
        {
            check_scheduler_and_instance_isolation();
            check_no_debt_and_expiry();
            check_layout_and_route_events();
            check_resource_and_beast_cycles();
            check_v7_migration_and_resource_roundtrip();
            check_dynamic_panel_setup();
            GD.Print("DYNAMIC_DUNGEON_EXPEDITION PASS: first attempt, 180/360-day schedule, cap, no debt, seeded 128-cell routes, event choices, isolated state, 7/30/90-day resources, 14-day new beast identity, inside expiry, v7 migration and save round-trip");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private static Vector2I[] candidates() => [new(28, 160), new(31, 163), new(36, 169)];

    private static GDictionary only_instance(GDictionary state, int index = 0)
        => state["instances"].AsGodotArray()[index].AsGodotDictionary();

    private static void check_scheduler_and_instance_isolation()
    {
        const int start = 10_000;
        var cells = candidates();
        var firstState = DynamicDungeonExpedition.CreateEmpty(start);
        Check(DynamicDungeonExpedition.Synchronize(firstState, start, () => cells), "首次启用应立刻尝试生成机遇。");
        Check(firstState["activated"].AsBool() && firstState["instances"].AsGodotArray().Count == 1,
            "首次进入世界后应且仅应立即生成一处。");
        var first = only_instance(firstState);
        var firstId = first["instance_id"].AsString();
        var firstSeed = first["seed"].AsString();
        var firstMap = Json.Stringify(first["map_snapshot"]);

        var repeatedState = DynamicDungeonExpedition.CreateEmpty(start);
        DynamicDungeonExpedition.Synchronize(repeatedState, start, () => cells);
        Check(Json.Stringify(only_instance(repeatedState)["map_snapshot"]) == firstMap,
            "相同种子和世界位置应生成相同地图快照。");
        DynamicDungeonExpedition.Synchronize(firstState, start, () => cells);
        Check(firstState["instances"].AsGodotArray().Count == 1, "同一游戏时刻重复回调不得重复生成。");

        DynamicDungeonExpedition.Synchronize(firstState, start + DynamicDungeonExpedition.ScheduleIntervalHours, () => cells);
        Check(firstState["instances"].AsGodotArray().Count == 2 &&
              DynamicDungeonExpedition.OpenInstanceCount(firstState, start + DynamicDungeonExpedition.ScheduleIntervalHours) == 2,
            "180日应检查并生成第二处，开放入口最多两处。");
        var second = only_instance(firstState, 1);
        Check(second["seed"].AsString() != firstSeed, "新实例必须有独立种子。");
        var firstRoute = DynamicDungeonExpedition.GetRoute(first);
        firstRoute["discovered"] = new GArray { "valley_00_04", "valley_05_03" };
        DynamicDungeonExpedition.StoreRoute(first, firstRoute);
        Check(DynamicDungeonExpedition.GetRoute(second)["discovered"].AsGodotArray().Count == 1,
            "两个实例的探索状态不得互相污染。");

        DynamicDungeonExpedition.Synchronize(firstState, start + DynamicDungeonExpedition.OpenDurationHours, () => cells);
        Check(!first["entry_open"].AsBool() && DynamicDungeonExpedition.OpenInstanceCount(firstState, start + DynamicDungeonExpedition.OpenDurationHours) == 2,
            "到期检查应先关闭旧入口，再生成替代机遇且开放数不超过两处。");
        Check(first["instance_id"].AsString() == firstId, "到期不得覆盖或重置旧实例身份。");
    }

    private static void check_no_debt_and_expiry()
    {
        const int start = 24_000;
        var state = DynamicDungeonExpedition.CreateEmpty(start);
        var none = Array.Empty<Vector2I>();
        DynamicDungeonExpedition.Synchronize(state, start, () => none);
        Check(state["activated"].AsBool() && state["instances"].AsGodotArray().Count == 0,
            "没有合格地点时首次机遇应跳过。");
        DynamicDungeonExpedition.Synchronize(state, start + DynamicDungeonExpedition.ScheduleIntervalHours, () => none);
        DynamicDungeonExpedition.Synchronize(state, start + DynamicDungeonExpedition.ScheduleIntervalHours, candidates);
        Check(state["instances"].AsGodotArray().Count == 0, "失败的周期不得积累欠发，周期中途出现地点也不补发。");
        DynamicDungeonExpedition.Synchronize(state, start + DynamicDungeonExpedition.ScheduleIntervalHours * 2, candidates);
        Check(state["instances"].AsGodotArray().Count == 1, "下一次180日检查应重新尝试。");

        const int insideStart = 40_000;
        var insideState = DynamicDungeonExpedition.CreateEmpty(insideStart);
        DynamicDungeonExpedition.Synchronize(insideState, insideStart, candidates);
        var inside = only_instance(insideState);
        var route = DynamicDungeonExpedition.GetRoute(inside);
        route["inside"] = true;
        DynamicDungeonExpedition.StoreRoute(inside, route);
        var expiry = inside["expires_hour"].AsInt32();
        DynamicDungeonExpedition.Synchronize(insideState, expiry, candidates);
        Check(!inside["entry_open"].AsBool() && DynamicDungeonExpedition.FindInsideInstance(insideState) is not null,
            "入口到期不得驱逐仍在副本内的玩家。");
        Check(DynamicDungeonExpedition.OpenEntries(insideState, expiry).All(entry => entry.AsGodotDictionary()["instance_id"].AsString() != inside["instance_id"].AsString()),
            "到期实例不得继续显示外界入口，即使同一时刻生成了替代机遇。");
        route = DynamicDungeonExpedition.GetRoute(inside);
        route["inside"] = false;
        DynamicDungeonExpedition.StoreRoute(inside, route);
        Check(!DynamicDungeonExpedition.IsEntryOpen(inside, expiry), "离开到期副本后不得重新进入。");
    }

    private static void check_layout_and_route_events()
    {
        const int start = 50_000;
        var state = DynamicDungeonExpedition.CreateEmpty(start);
        DynamicDungeonExpedition.Synchronize(state, start, candidates);
        var instance = only_instance(state);
        var nodes = DynamicDungeonExpedition.GetNodes(instance);
        Check(nodes.Count == 128 && nodes.Select(node => node.Id).Distinct().Count() == 128,
            "副本应保存128个唯一格位。");
        foreach (var node in nodes)
            foreach (var neighborId in DynamicDungeonExpedition.GetNeighbors(instance, node.Id))
            {
                var neighbor = DynamicDungeonExpedition.GetNode(instance, neighborId);
                Check(neighbor is not null && Math.Abs(neighbor.GridPosition.X - node.GridPosition.X) + Math.Abs(neighbor.GridPosition.Y - node.GridPosition.Y) == 1,
                    "地图连接必须是正交相邻格。");
            }
        Check(DynamicDungeonExpedition.ValidateState(state), "实例状态和地图拓扑应通过正式存档验证。");

        var alternatives = DynamicDungeonExpedition.AvailableActions(instance, "valley_02_04", new GDictionary());
        Check(alternatives.Count == 2 && alternatives.All(action => action.Hours == 1 && action.Stamina == 1),
            "竹影线索应提供两种有明确路线后果、使用统一时间和体力的选择。");
        var route = DynamicDungeonExpedition.GetRoute(instance);
        DynamicDungeonExpedition.ApplyCompletedAction(instance, "valley_02_04", "event_bamboo_north", route);
        DynamicDungeonExpedition.StoreRoute(instance, route);
        Check(instance["event_results"].AsGodotDictionary()["bamboo_tracks"].AsString() == "event_bamboo_north" &&
              route["discovered"].AsGodotArray().Any(value => value.AsString() == "valley_06_02"),
            "线索选择应记为一次性结果并揭示对应的北侧路线。");
        var discoveredCount = route["discovered"].AsGodotArray().Count;
        DynamicDungeonExpedition.ApplyCompletedAction(instance, "valley_02_04", "event_bamboo_south", route);
        Check(instance["event_results"].AsGodotDictionary()["bamboo_tracks"].AsString() == "event_bamboo_north" &&
              route["discovered"].AsGodotArray().Count == discoveredCount,
            "环境事件重复结算不得改写结果或重复揭路。");
    }

    private static void check_resource_and_beast_cycles()
    {
        const int start = 80_000;
        var state = DynamicDungeonExpedition.CreateEmpty(start);
        DynamicDungeonExpedition.Synchronize(state, start, candidates);
        var instance = only_instance(state);
        DynamicDungeonExpedition.SynchronizeInstanceEcology(instance, start + 170 * 24);
        var herbNode = DynamicDungeonExpedition.GetNodes(instance).First(node => node.ResourceNodeId == "common_herb");
        var herb = DynamicDungeonExpedition.AvailableActions(instance, herbNode.Id, new GDictionary()).Single(action => action.ItemPath.EndsWith("苦苔根.tres", StringComparison.Ordinal));
        var route = DynamicDungeonExpedition.GetRoute(instance);
        var actions = route["actions"].AsGodotDictionary();
        var key = herbNode.Id + ":" + herb.Id;
        actions[key] = herb.Quantity;
        route["actions"] = actions;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        var sevenDays = DynamicDungeonExpedition.RecoveryRate(herb.Recovery).PeriodHours;
        DynamicDungeonExpedition.SynchronizeInstanceEcology(instance, start + 170 * 24 + sevenDays - 1);
        Check(DynamicDungeonExpedition.GetRoute(instance)["actions"].AsGodotDictionary()[key].AsInt32() == herb.Quantity,
            "晚采集资源应从采集时起算，未满七日不得恢复。");
        DynamicDungeonExpedition.SynchronizeInstanceEcology(instance, start + 170 * 24 + sevenDays);
        Check(DynamicDungeonExpedition.GetRoute(instance)["actions"].AsGodotDictionary()[key].AsInt32() == 0,
            "常见植物应在完整七日后按周期恢复。");

        foreach (var (resourceId, expectedGroup, expectedDays) in new[]
        {
            ("mistbell_slow", EastWoodsActions.ResourceRecoveryGroup.SlowGrowth, 30),
            ("spirit_stone_vein", EastWoodsActions.ResourceRecoveryGroup.Mineral, 90),
        })
        {
            var node = DynamicDungeonExpedition.GetNodes(instance).Single(candidate => candidate.ResourceNodeId == resourceId);
            var action = DynamicDungeonExpedition.AvailableActions(instance, node.Id, new GDictionary()).Single(candidate => candidate.Recovery == expectedGroup);
            Check(DynamicDungeonExpedition.RecoveryRate(action.Recovery).PeriodHours == expectedDays * 24,
                $"资源类别 {resourceId} 的恢复周期不匹配。");
        }

        var beasts = instance["beasts"].AsGodotArray();
        var firstRecord = beasts[0].AsGodotDictionary();
        var deadIdentity = firstRecord["encounter_id"].AsString();
        var deadNode = firstRecord["node"].AsString();
        var deadAction = firstRecord["action_id"].AsString();
        route = DynamicDungeonExpedition.GetRoute(instance);
        actions = route["actions"].AsGodotDictionary();
        actions[deadNode + ":" + deadAction] = 1;
        route["actions"] = actions;
        DynamicDungeonExpedition.StoreRoute(instance, route);
        DynamicDungeonExpedition.SynchronizeInstanceEcology(instance, start + 170 * 24 + 14 * 24);
        var records = instance["beasts"].AsGodotArray();
        Check(records.Any(value => value.AsGodotDictionary()["encounter_id"].AsString() == deadIdentity && value.AsGodotDictionary()["status"].AsString() == "defeated"),
            "已击败妖兽的身份记录必须保留。");
        Check(records.Any(value => value.AsGodotDictionary()["node"].AsString() == deadNode && value.AsGodotDictionary()["status"].AsString() == "active" && value.AsGodotDictionary()["encounter_id"].AsString() != deadIdentity),
            "14日检查应生成新的妖兽身份，不复活原个体。");
    }

    private static void check_v7_migration_and_resource_roundtrip()
    {
        const int oldHour = (4 - 1) * 360 * 24 + (7 - 1) * 30 * 24 + (19 - 1) * 24 + 11;
        var legacy = new PersistentGameSave
        {
            format_version = 7,
            year = 4,
            month = 7,
            day = 19,
            hour = 11,
            scene_progress = new GDictionary
            {
                ["east_woods_dungeon_state"] = new GDictionary { ["inside"] = true, ["route"] = "kept" },
            },
        };
        Check(legacy.migrate_to_current_format() && legacy.format_version == 8 &&
              legacy.dynamic_dungeon_state["last_schedule_hour"].AsInt32() == oldHour &&
              legacy.dynamic_dungeon_state["instances"].AsGodotArray().Count == 0,
            "v7→v8 迁移应以存档当前时刻建立空机遇游标，不补发历史实例。");
        Check(legacy.scene_progress["east_woods_dungeon_state"].AsGodotDictionary()["inside"].AsBool(),
            "版本迁移不得改写东林地原进度。");

        const int now = 100_000;
        var state = DynamicDungeonExpedition.CreateEmpty(now);
        DynamicDungeonExpedition.Synchronize(state, now, candidates);
        var save = new PersistentGameSave { dynamic_dungeon_state = state.Duplicate(true) };
        var path = "user://dynamic_dungeon_smoke_" + Guid.NewGuid().ToString("N") + ".tres";
        try
        {
            Check(ResourceSaver.Save(save, path) == Error.Ok, "动态副本 Resource 测试存档无法保存。");
            var restored = ResourceLoader.Load<PersistentGameSave>(path);
            Check(restored is not null && DynamicDungeonExpedition.ValidateState(restored.dynamic_dungeon_state) &&
                  restored.dynamic_dungeon_state["instances"].AsGodotArray().Count == 1,
                "正式存档读回后未恢复活动副本实例。");
        }
        finally
        {
            var absolute = ProjectSettings.GlobalizePath(path);
            if (Godot.FileAccess.FileExists(absolute)) DirAccess.RemoveAbsolute(absolute);
        }
    }

    private void check_dynamic_panel_setup()
    {
        const int start = 120_000;
        var state = DynamicDungeonExpedition.CreateEmpty(start);
        DynamicDungeonExpedition.Synchronize(state, start, candidates);
        var panel = new EastWoodsDungeonPanel { Name = "DynamicDungeonSmokePanel" };
        AddChild(panel);
        panel.configure_dynamic_instance(only_instance(state));
        panel.show_dungeon();
        Check(panel.is_dynamic_instance && panel.get_current_route_node_id() == DynamicDungeonExpedition.GetEntryNodeId(only_instance(state)),
            "副本面板应以动态实例入口和独立地图启动。");
        Check(panel.available_actions("valley_02_04").Count == 2,
            "面板应显示动态实例提供的路线选择交互。");
        panel.Hide();
        panel.QueueFree();
    }
}
