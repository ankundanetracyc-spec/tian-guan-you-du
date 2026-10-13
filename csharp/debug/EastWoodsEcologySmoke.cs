using Godot;
using System;
using System.Linq;

public partial class EastWoodsEcologySmoke : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public override void _Ready()
    {
        try
        {
            check_generated_resource_locations();
            check_resource_recovery();
            check_beast_recovery();
            GD.Print("EAST_WOODS_ECOLOGY PASS: configured 7/30-day points, 90-day mineral rule without an invented point, full cycles, retained remainder, save round-trip, new beast identity, current-cell exclusion, defeated history");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private static void check_resource_recovery()
    {
        EastWoodsDungeonRules.use_legacy_layout();
        var root = EastWoodsActions.Content("fern_lowland")!;
        var moss = EastWoodsActions.Content("dry_pool")!;
        var resourcePoints = EastWoodsActions.ResourcePoints();
        Check(resourcePoints.Count == 7 && resourcePoints.Select(point => point.Action.Id).Distinct().Count() == 3,
            "六处常见采集点与一处慢生点应分别登记；没有专门矿脉位置时不得添加矿点。");
        Check(resourcePoints.Count(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Common) == 6 &&
              resourcePoints.Count(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth) == 1 &&
              resourcePoints.Count(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral) == 0,
            "资源点恢复组与策划周期不匹配。");
        var slow = resourcePoints.Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth);
        var slowNode = EastWoodsDungeonRules.get_node(slow.Node);
        Check(slowNode is { Terrain: 0 or 1 } && !EastWoodsDungeonRules.is_action_anchor(slow.Node) &&
              EastWoodsDungeonRules.get_neighbors(slow.Node).Select(EastWoodsDungeonRules.get_node).Any(neighbor => neighbor?.Terrain == 5),
            "慢生资源应落在既有湿润林缘格，而不覆盖原行动点。");
        Check(slow.Action.ItemPath == "res://resources/entities/items/雾铃花.tres",
            "慢生点引用了非现有物品。");
        var slowItem = EastWoodsActions.Item(slow.Action.ItemPath);
        Check(slowItem is not null && ResourceLoader.Exists(slowItem.icon_path), "慢生点资源缺少有效物品图标。");
        Check(slow.Action.Hours == 1 && slow.Action.Stamina == 2 && slow.Action.Quantity == 1 && slow.Action.Stock == 2,
            "慢生点未沿用现有单份采集行动成本与有限储量。");
        Check(EastWoodsEcology.RecoveryRate(EastWoodsActions.ResourceRecoveryGroup.Mineral) == (90 * 24, 0.25),
            "90日矿脉恢复参数未按每期上限25%向上取整保留。");

        const int startHour = 9000;
        var actions = new Godot.Collections.Dictionary
        {
            [EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)] = root.Stock,
            [EastWoodsEcology.ActionProgressKey("dry_pool", moss.Id)] = moss.Stock,
            ["unrelated_event:clue"] = 1,
            ["__east_woods_ecology"] = new Godot.Collections.Dictionary(),
        };
        var route = new Godot.Collections.Dictionary
        {
            ["current"] = "forest_edge",
            ["visited"] = new Godot.Collections.Array<string> { "forest_edge", "fern_lowland" },
            ["discovered"] = new Godot.Collections.Array<string> { "forest_edge", "fern_lowland", "dry_pool" },
            ["actions"] = actions,
            ["layout"] = new Godot.Collections.Dictionary { ["seed"] = "kept-seed" },
        };

        EastWoodsEcology.Synchronize(route, startHour, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        Check(actions[EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)].AsInt32() == root.Stock &&
              actions[EastWoodsEcology.ActionProgressKey("dry_pool", moss.Id)].AsInt32() == moss.Stock,
            "旧存档初始化刷新游标时不应追补资源。");
        EastWoodsEcology.Synchronize(route, startHour + 167, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        Check(actions[EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)].AsInt32() == root.Stock, "未满七日不应恢复常见资源。");

        EastWoodsEcology.Synchronize(route, startHour + 168, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        Check(actions[EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)].AsInt32() == 2, "苦苔根应按每七日恢复一半容量。");
        Check(actions[EastWoodsEcology.ActionProgressKey("dry_pool", moss.Id)].AsInt32() == 1, "月露苔应按每七日恢复一半容量。");
        Check(actions["unrelated_event:clue"].AsInt32() == 1, "生态同步不得覆盖一次性事件进度。");
        Check(route["visited"].AsGodotArray().Count == 2 && route["discovered"].AsGodotArray().Count == 3 &&
              route["layout"].AsGodotDictionary()["seed"].AsString() == "kept-seed", "生态同步不得改动探索、发现和地图种子。");

        var saved = Json.ParseString(Json.Stringify(route)).AsGodotDictionary();
        EastWoodsEcology.Synchronize(saved, startHour + 335, "forest_edge");
        actions = saved["actions"].AsGodotDictionary();
        Check(actions[EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)].AsInt32() == 2 &&
              actions[EastWoodsEcology.ActionProgressKey("dry_pool", moss.Id)].AsInt32() == 1,
            "存档读回后应保留七日周期的余时。");

        EastWoodsEcology.Synchronize(saved, startHour + 336, "forest_edge");
        actions = saved["actions"].AsGodotDictionary();
        Check(actions[EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)].AsInt32() == 0 &&
              actions[EastWoodsEcology.ActionProgressKey("dry_pool", moss.Id)].AsInt32() == 0,
            "第二个完整周期应补足剩余资源，且不超过采集上限。");
        EastWoodsEcology.Synchronize(saved, startHour + 336, "forest_edge");
        actions = saved["actions"].AsGodotDictionary();
        Check(actions[EastWoodsEcology.ActionProgressKey("fern_lowland", root.Id)].AsInt32() == 0 &&
              actions[EastWoodsEcology.ActionProgressKey("dry_pool", moss.Id)].AsInt32() == 0,
            "同一游戏时刻重复同步不得重复补给。");

        check_slow_cycles(slow);
    }

    private static void check_slow_cycles((string Node, EastWoodsActions.ActionDef Action) slow)
    {
        const int startHour = 18000;
        var slowKey = EastWoodsEcology.ActionProgressKey(slow.Node, slow.Action.Id);
        var route = new Godot.Collections.Dictionary
        {
            ["current"] = "forest_edge",
            ["actions"] = new Godot.Collections.Dictionary
            {
                [slowKey] = slow.Action.Stock,
            },
        };

        EastWoodsEcology.Synchronize(route, startHour, "forest_edge");
        EastWoodsEcology.Synchronize(route, startHour + 30 * 24 - 1, "forest_edge");
        var actions = route["actions"].AsGodotDictionary();
        Check(actions[slowKey].AsInt32() == slow.Action.Stock,
            "未满30日时不应恢复慢生资源。");

        EastWoodsEcology.Synchronize(route, startHour + 30 * 24, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        Check(actions[slowKey].AsInt32() == 1,
            "满30日时慢生点应恢复上限的25%向上取整。");

        // Simulate harvesting the recovered unit, then verify the next boundary after save/load.
        actions[slowKey] = slow.Action.Stock;
        EastWoodsEcology.Synchronize(route, startHour + 60 * 24, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        actions[slowKey] = slow.Action.Stock;
        EastWoodsEcology.Synchronize(route, startHour + 90 * 24 - 1, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        Check(actions[slowKey].AsInt32() == slow.Action.Stock,
            "保存前不足完整周期时不得提前恢复资源。");
        var loaded = Json.ParseString(Json.Stringify(route)).AsGodotDictionary();
        EastWoodsEcology.Synchronize(loaded, startHour + 90 * 24, "forest_edge");
        actions = loaded["actions"].AsGodotDictionary();
        Check(actions[slowKey].AsInt32() == 1,
            "读档后应保留慢生点的30日游标与资源余数。");
        EastWoodsEcology.Synchronize(loaded, startHour + 90 * 24, "forest_edge");
        Check(actions[slowKey].AsInt32() == 1,
            "同刻重复同步不得重复发放资源。");
    }

    private static void check_generated_resource_locations()
    {
        for (var index = 0; index < 20; index++)
        {
            EastWoodsDungeonRules.generate_layout("ecology-resource-smoke-" + index);
            var points = EastWoodsActions.ResourcePoints();
            var slow = points.Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth);
            var slowNode = EastWoodsDungeonRules.get_node(slow.Node);
            Check(slowNode is { Terrain: 0 or 1 } &&
                  EastWoodsDungeonRules.get_neighbors(slow.Node).Select(EastWoodsDungeonRules.get_node).Any(neighbor => neighbor?.Terrain == 5),
                "种子地图未在现有湿润林缘配置慢生采集点。");
            Check(!points.Any(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral),
                "没有专门灵石脉标记时不得把普通岩格配置成矿点。");
            var layout = EastWoodsDungeonRules.get_layout_state();
            EastWoodsDungeonRules.restore_layout(layout);
            Check(EastWoodsActions.SlowGrowthPointNodeId() == slow.Node,
                "地图快照恢复后慢生点位置改变。");
        }
        EastWoodsDungeonRules.use_legacy_layout();
    }

    private static void check_beast_recovery()
    {
        const int startHour = 12000;
        var protectedRoute = new Godot.Collections.Dictionary
        {
            ["actions"] = new Godot.Collections.Dictionary
            {
                [EastWoodsEcology.ActionProgressKey("outer_13_06", "wolf")] = 1,
                [EastWoodsEcology.ActionProgressKey("outer_08_08", "wolf")] = 1,
            },
        };
        EastWoodsEcology.Synchronize(protectedRoute, startHour, "root_hollow");
        EastWoodsEcology.Synchronize(protectedRoute, startHour + 336, "root_hollow");
        var protectedActions = protectedRoute["actions"].AsGodotDictionary();
        Check(EastWoodsEcology.AvailableRegeneratedBeasts("root_hollow", protectedActions).Count == 0 &&
              !protectedActions["__east_woods_ecology"].AsGodotDictionary()["pending_beast_checks"].AsGodotDictionary().ContainsKey("root_hollow"),
            "未结算的固定遭遇不得被刷新遭遇覆盖或延后替换。");

        var actions = new Godot.Collections.Dictionary
        {
            [EastWoodsEcology.ActionProgressKey("root_hollow", "wolf")] = 1,
            [EastWoodsEcology.ActionProgressKey("outer_13_06", "wolf")] = 1,
            [EastWoodsEcology.ActionProgressKey("outer_08_08", "wolf")] = 1,
        };
        var route = new Godot.Collections.Dictionary { ["current"] = "root_hollow", ["actions"] = actions };
        EastWoodsEcology.Synchronize(route, startHour, "root_hollow");
        EastWoodsEcology.Synchronize(route, startHour + 335, "root_hollow");
        actions = route["actions"].AsGodotDictionary();
        Check(EastWoodsEcology.AvailableRegeneratedBeasts("root_hollow", actions).Count == 0, "普通妖兽检查未满十四日不得生成遭遇。");

        const int thirdCheckHour = startHour + 1008;
        EastWoodsEcology.Synchronize(route, thirdCheckHour, "root_hollow");
        actions = route["actions"].AsGodotDictionary();
        Check(EastWoodsEcology.AvailableRegeneratedBeasts("root_hollow", actions).Count == 0, "刷新时不得在玩家当前地格生成妖兽。");
        var ecology = actions["__east_woods_ecology"].AsGodotDictionary();
        Check(ecology["pending_beast_checks"].AsGodotDictionary().GetValueOrDefault("root_hollow", 0).AsInt32() == 1,
            "当前格被跳过时应保存待补遭遇。");
        var firstA = EastWoodsEcology.AvailableRegeneratedBeasts("outer_13_06", actions).Single();
        var firstB = EastWoodsEcology.AvailableRegeneratedBeasts("outer_08_08", actions).Single();
        Check(firstA.EncounterIdentity != firstB.EncounterIdentity && firstA.EncounterIdentity.Length > 0 && firstB.EncounterIdentity.Length > 0,
            "每个再生遭遇必须拥有独立身份。");

        route = Json.ParseString(Json.Stringify(route)).AsGodotDictionary();
        actions = route["actions"].AsGodotDictionary();
        Check(EastWoodsEcology.AvailableRegeneratedBeasts("outer_13_06", actions).Single().EncounterIdentity == firstA.EncounterIdentity,
            "读档后应恢复同一只尚未处理的遭遇。");
        route["current"] = "forest_edge";
        EastWoodsEcology.Synchronize(route, thirdCheckHour, "forest_edge");
        actions = route["actions"].AsGodotDictionary();
        var deferredRoot = EastWoodsEcology.AvailableRegeneratedBeasts("root_hollow", actions).Single();
        Check(deferredRoot.EncounterIdentity.Length > 0 && deferredRoot.EncounterIdentity != firstA.EncounterIdentity &&
              actions["__east_woods_ecology"].AsGodotDictionary()["pending_beast_checks"].AsGodotDictionary().Count == 0,
            "离开当前格后应立即补入待补遭遇，不需再等十四日。");
        actions[EastWoodsEcology.ActionProgressKey("outer_13_06", firstA.Id)] = 1;
        EastWoodsEcology.MarkEncounterDefeated(actions, firstA.EncounterIdentity, thirdCheckHour);
        route["current"] = "root_hollow";
        EastWoodsEcology.Synchronize(route, startHour + 1344, "root_hollow");
        actions = route["actions"].AsGodotDictionary();
        var nextA = EastWoodsEcology.AvailableRegeneratedBeasts("outer_13_06", actions).Single();
        Check(nextA.EncounterIdentity != firstA.EncounterIdentity, "新一轮妖兽必须使用新遭遇身份。");
        Check(EastWoodsEcology.AvailableRegeneratedBeasts("outer_08_08", actions).Single().EncounterIdentity == firstB.EncounterIdentity,
            "未击败的妖兽应保留原遭遇身份，不得重置。");
        Check(actions[EastWoodsEcology.ActionProgressKey("root_hollow", "wolf")].AsInt32() == 1,
            "妖兽再生不得重置固定遭遇的战斗记录。");
    }
}
