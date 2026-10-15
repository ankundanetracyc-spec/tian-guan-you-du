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
            GD.Print("EAST_WOODS_ECOLOGY PASS: 7/30/90-day categorized resource nodes, category-gated actions, stock/cursor save round-trip, full cycles, 14-day beasts, current-cell exclusion and defeated history");
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
        Check(resourcePoints.Count == 8 && resourcePoints.Select(point => point.Action.Id).Distinct().Count() == 4,
            "六处常见采集点、一处30日植物点和一处90日矿产点应分别登记。");
        Check(resourcePoints.Count(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Common) == 6 &&
              resourcePoints.Count(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth) == 1 &&
              resourcePoints.Count(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral) == 1,
            "资源点恢复组与策划周期不匹配。");
        var slow = resourcePoints.Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth);
        var mineral = resourcePoints.Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral);
        var slowNode = EastWoodsDungeonRules.get_node(slow.Node);
        Check(slowNode is { Terrain: 0 or 1 } && !EastWoodsDungeonRules.is_action_anchor(slow.Node) &&
              EastWoodsDungeonRules.get_neighbors(slow.Node).Select(EastWoodsDungeonRules.get_node).Any(neighbor => neighbor?.Terrain == 5) &&
              slowNode.ResourceNodeId == "mistbell_slow" && slowNode.ResourceCategory == EastWoodsResourceCategory.Plant,
            "慢生资源应落在既有湿润林缘格，而不覆盖原行动点。");
        var mineralNode = EastWoodsDungeonRules.get_node(mineral.Node);
        Check(mineralNode is { Terrain: 2 or 7 } && mineralNode.Feature != "spring" &&
              !EastWoodsDungeonRules.is_action_anchor(mineral.Node) &&
              mineralNode.ResourceNodeId == "spirit_stone_vein" && mineralNode.ResourceCategory == EastWoodsResourceCategory.Mineral,
            "90日资源必须是显式矿产类别节点，不能借用普通岩格或泉眼。");
        Check(slow.Action.ItemPath == "res://resources/entities/items/雾铃花.tres",
            "慢生点引用了非现有物品。");
        Check(mineral.Action.ItemPath == "res://resources/entities/items/灵石.tres",
            "矿产点引用了非现有物品。");
        var slowItem = EastWoodsActions.Item(slow.Action.ItemPath);
        var mineralItem = EastWoodsActions.Item(mineral.Action.ItemPath);
        Check(slowItem is not null && ResourceLoader.Exists(slowItem.icon_path) &&
              mineralItem is not null && ResourceLoader.Exists(mineralItem.icon_path), "资源点缺少有效物品图标。");
        Check(slow.Action.Hours == 1 && slow.Action.Stamina == 2 && slow.Action.Quantity == 1 && slow.Action.Stock == 2,
            "慢生点未沿用现有单份采集行动成本与有限储量。");
        Check(mineral.Action.Hours == 1 && mineral.Action.Stamina == 2 && mineral.Action.Quantity == 1 && mineral.Action.Stock == 2,
            "矿产点未沿用现有单份采集行动成本与有限储量。");
        Check(EastWoodsActions.Find(slow.Node, mineral.Action.Id) is null &&
              EastWoodsActions.Find(mineral.Node, slow.Action.Id) is null,
            "植物与矿产行动没有按节点类别互相隔离。");
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

        var layoutSnapshot = EastWoodsDungeonRules.get_layout_state();
        check_resource_node_cycles(slow, layoutSnapshot);
        check_resource_node_cycles(mineral, layoutSnapshot);
    }

    private static void check_resource_node_cycles((string Node, EastWoodsActions.ActionDef Action) point,
        Godot.Collections.Dictionary layoutSnapshot)
    {
        const int startHour = 18000;
        var key = EastWoodsEcology.ActionProgressKey(point.Node, point.Action.Id);
        var periodHours = EastWoodsEcology.RecoveryRate(point.Action.Recovery).PeriodHours;
        var recoveredPerCycle = (int)Math.Ceiling(point.Action.Stock * EastWoodsEcology.RecoveryRate(point.Action.Recovery).Fraction);
        var route = new Godot.Collections.Dictionary
        {
            ["current"] = "forest_edge",
            ["layout"] = layoutSnapshot.Duplicate(true),
            ["actions"] = new Godot.Collections.Dictionary
            {
                [key] = point.Action.Stock,
            },
        };

        EastWoodsEcology.Synchronize(route, startHour, "forest_edge");
        var actions = route["actions"].AsGodotDictionary();
        var ecology = actions["__east_woods_ecology"].AsGodotDictionary();
        var cursors = ecology["resource_cursors"].AsGodotDictionary();
        Check(cursors[key].AsInt32() == startHour, "资源点初次启用时未保存恢复游标。");
        EastWoodsEcology.Synchronize(route, startHour + periodHours - 1, "forest_edge");
        Check(route["actions"].AsGodotDictionary()[key].AsInt32() == point.Action.Stock,
            "未满完整周期时资源点不应恢复。");

        var loaded = Json.ParseString(Json.Stringify(route)).AsGodotDictionary();
        EastWoodsDungeonRules.restore_layout(loaded["layout"].AsGodotDictionary());
        var restoredNode = EastWoodsDungeonRules.get_node(point.Node);
        var expectedCategory = point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral
            ? EastWoodsResourceCategory.Mineral : EastWoodsResourceCategory.Plant;
        var expectedNodeId = expectedCategory == EastWoodsResourceCategory.Mineral ? "spirit_stone_vein" : "mistbell_slow";
        Check(restoredNode?.ResourceCategory == expectedCategory &&
              restoredNode.ResourceNodeId == expectedNodeId,
            "读档后资源节点类别没有保存。");
        EastWoodsEcology.Synchronize(loaded, startHour + periodHours, "forest_edge");
        actions = loaded["actions"].AsGodotDictionary();
        Check(point.Action.Stock - actions[key].AsInt32() == recoveredPerCycle,
            "完整周期未按上限25%向上取整恢复储量。");
        ecology = actions["__east_woods_ecology"].AsGodotDictionary();
        cursors = ecology["resource_cursors"].AsGodotDictionary();
        Check(cursors[key].AsInt32() == startHour + periodHours,
            "恢复后没有推进并保存周期游标。");

        // Deplete it again and save after a partial period; loading must retain the remaining time.
        actions[key] = point.Action.Stock;
        EastWoodsEcology.Synchronize(loaded, startHour + periodHours + 23, "forest_edge");
        loaded = Json.ParseString(Json.Stringify(loaded)).AsGodotDictionary();
        EastWoodsEcology.Synchronize(loaded, startHour + periodHours * 2 - 1, "forest_edge");
        actions = loaded["actions"].AsGodotDictionary();
        Check(actions[key].AsInt32() == point.Action.Stock,
            "读档后不足完整周期的恢复余时没有保留。");
        EastWoodsEcology.Synchronize(loaded, startHour + periodHours * 2, "forest_edge");
        actions = loaded["actions"].AsGodotDictionary();
        Check(point.Action.Stock - actions[key].AsInt32() == recoveredPerCycle,
            "保留的周期余时没有在下个整周期边界恢复资源。");
    }

    private static void check_generated_resource_locations()
    {
        for (var index = 0; index < 20; index++)
        {
            EastWoodsDungeonRules.generate_layout("ecology-resource-smoke-" + index);
            var points = EastWoodsActions.ResourcePoints();
            var slow = points.Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth);
            var mineral = points.Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral);
            var slowNode = EastWoodsDungeonRules.get_node(slow.Node);
            Check(slowNode is { Terrain: 0 or 1 } &&
                  EastWoodsDungeonRules.get_neighbors(slow.Node).Select(EastWoodsDungeonRules.get_node).Any(neighbor => neighbor?.Terrain == 5) &&
                  slowNode.ResourceCategory == EastWoodsResourceCategory.Plant && slowNode.ResourceNodeId == "mistbell_slow",
                "种子地图未在现有湿润林缘配置慢生采集点。");
            var mineralNode = EastWoodsDungeonRules.get_node(mineral.Node);
            Check(mineralNode is { Terrain: 2 or 7 } && mineralNode.Feature != "spring" &&
                  mineralNode.ResourceCategory == EastWoodsResourceCategory.Mineral && mineralNode.ResourceNodeId == "spirit_stone_vein",
                "种子地图未创建独立类别的专用矿脉节点。");
            var layout = EastWoodsDungeonRules.get_layout_state();
            EastWoodsDungeonRules.restore_layout(layout);
            Check(EastWoodsActions.SlowGrowthPointNodeId() == slow.Node && EastWoodsActions.MineralPointNodeId() == mineral.Node &&
                  EastWoodsDungeonRules.get_node(slow.Node)?.ResourceCategory == EastWoodsResourceCategory.Plant &&
                  EastWoodsDungeonRules.get_node(mineral.Node)?.ResourceCategory == EastWoodsResourceCategory.Mineral,
                "地图快照恢复后资源点位置或类别改变。");
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
