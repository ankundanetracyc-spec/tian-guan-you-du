using Godot;
using System;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Random tower lifecycle, graph generation, ecology and save migration smoke for Mist Hidden Valley.</summary>
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
            check_compact_creature_roster();
            check_creature_battle_packages();
            check_v7_migration_and_resource_roundtrip();
            check_dynamic_panel_setup();
            GD.Print("DYNAMIC_DUNGEON_EXPEDITION PASS: persistent tower entrance, seeded 15-layer randomized graphs, all-node exit reachability, 5:6:4 node weights, target-node stamina costs, resource and beast snapshots, creature encounter groups, compact sample compatibility, save migration and round-trip");
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

    private static (GDictionary State, GDictionary Instance) create_tower(int start, string worldSeed)
    {
        var state = DynamicDungeonExpedition.CreateEmpty(start);
        state["world_seed"] = worldSeed;
        DynamicDungeonExpedition.SynchronizeTowerOpportunities(state, start);
        return (state, only_instance(state));
    }

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
        var uniqueLayouts = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var totals = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        for (var seedIndex = 0; seedIndex < 64; seedIndex++)
        {
            var (state, instance) = create_tower(start, "tower-graph-smoke-" + seedIndex);
            var nodes = DynamicDungeonExpedition.GetNodes(instance);
            var floorCounts = nodes.Where(node => node.Id.StartsWith("tower_", StringComparison.Ordinal) && node.Id != DynamicDungeonExpedition.EntryNodeId && node.Id != DynamicDungeonExpedition.ExitNodeId)
                .GroupBy(node => DynamicDungeonExpedition.TowerLayerCount - node.GridPosition.Y + 1);
            Check(nodes.Select(node => node.Id).Distinct().Count() == nodes.Count && floorCounts.Count() == DynamicDungeonExpedition.TowerLayerCount &&
                  floorCounts.All(group => group.Count() is >= DynamicDungeonExpedition.MinimumNodesPerLayer and <= DynamicDungeonExpedition.MaximumNodesPerLayer),
                "每张随机塔图都应包含15层，每层3至4个唯一节点。");
            Check(DynamicDungeonExpedition.ValidateState(state), "随机塔图应通过正式存档校验，确保所有节点均可从入口抵达并可通往出口。");
            Check(DynamicDungeonExpedition.IsTowerInstance(instance) &&
                  DynamicDungeonExpedition.MovementStaminaCost(instance, DynamicDungeonExpedition.GetExitNodeId(instance)) == 1.0f &&
                  DynamicDungeonExpedition.ExpectedRealmRange(instance) != "未详",
                "新副本应使用塔图、出口费用，并能从实际遭遇读取境界范围。");

            foreach (var node in nodes)
            {
                if (node.ContentType is DynamicDungeonExpedition.BattleNodeType or DynamicDungeonExpedition.ResourceNodeType or DynamicDungeonExpedition.RestNodeType)
                    totals[node.ContentType] = totals.GetValueOrDefault(node.ContentType) + 1;
                if (node.ContentType == DynamicDungeonExpedition.BattleNodeType)
                {
                    Check(DynamicDungeonExpedition.MovementStaminaCost(instance, node.Id) == 3.0f &&
                          DynamicDungeonExpedition.AvailableActions(instance, node.Id, new GDictionary()).Any(action => action.Battle),
                        "战斗节点应收取3点移动精力并绑定一场现有战斗遭遇。");
                }
                else if (node.ContentType == DynamicDungeonExpedition.ResourceNodeType)
                    Check(DynamicDungeonExpedition.MovementStaminaCost(instance, node.Id) == 2.0f,
                        "资源节点应收取2点移动精力。");
                else if (node.ContentType == DynamicDungeonExpedition.RestNodeType)
                    Check(DynamicDungeonExpedition.MovementStaminaCost(instance, node.Id) == 0.5f &&
                          DynamicDungeonExpedition.AvailableActions(instance, node.Id, new GDictionary()).Any(action => action.Rest),
                        "休整节点应收取0.5点移动精力，并提供通用休息行动。");
            }
            uniqueLayouts.Add(string.Join(";", nodes.OrderBy(node => node.Id, StringComparer.Ordinal)
                .Select(node => $"{node.Id}:{node.GridPosition.X},{node.GridPosition.Y}:{node.ContentType}:{node.ResourceNodeId}:{node.CreatureId}")));
        }
        var totalNodes = totals.Values.Sum();
        Check(uniqueLayouts.Count == 64 &&
              totals.GetValueOrDefault(DynamicDungeonExpedition.BattleNodeType) / (double)totalNodes is > 0.29 and < 0.38 &&
              totals.GetValueOrDefault(DynamicDungeonExpedition.ResourceNodeType) / (double)totalNodes is > 0.36 and < 0.46 &&
              totals.GetValueOrDefault(DynamicDungeonExpedition.RestNodeType) / (double)totalNodes is > 0.20 and < 0.32,
            "不同种子应改变节点位置和类型；汇总分布应接近战斗5、资源6、休整4的权重。");

        var (resumableState, resumable) = create_tower(start, "tower-resume-smoke");
        var originalId = resumable["instance_id"].AsString();
        var route = DynamicDungeonExpedition.GetRoute(resumable);
        var firstStep = DynamicDungeonExpedition.GetNeighbors(resumable, DynamicDungeonExpedition.GetEntryNodeId(resumable)).First();
        route["current"] = firstStep;
        route["inside"] = false;
        DynamicDungeonExpedition.StoreRoute(resumable, route);
        DynamicDungeonExpedition.SynchronizeTowerOpportunities(resumableState, start + 24 * 30);
        Check(resumableState["instances"].AsGodotArray().Count == 1 && only_instance(resumableState)["instance_id"].AsString() == originalId &&
              DynamicDungeonExpedition.GetRoute(only_instance(resumableState))["current"].AsString() == firstStep,
            "中途暂离应保留入口和当前节点，时间推进不得重置本次地图。");

        resumable["completed"] = true;
        resumable["entry_open"] = false;
        DynamicDungeonExpedition.SynchronizeTowerOpportunities(resumableState, start + 24 * 31);
        Check(resumableState["instances"].AsGodotArray().Count == 2 &&
              resumableState["instances"].AsGodotArray().Cast<Variant>().Count(value => !value.AsGodotDictionary().GetValueOrDefault("completed", false).AsBool()) == 1,
            "通关后应关闭旧入口并只创建一处新的有效入口。");
    }

    private static void check_resource_and_beast_cycles()
    {
        const int start = 80_000;
        GDictionary? instance = null;
        var resourceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var seedIndex = 0; seedIndex < 128 && resourceIds.Count < 4; seedIndex++)
        {
            var candidate = create_tower(start, "tower-resource-smoke-" + seedIndex).Instance;
            resourceIds = DynamicDungeonExpedition.GetNodes(candidate).Select(node => node.ResourceNodeId)
                .Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
            if (new[] { "common_herb", "common_moss", "mistbell_slow", "spirit_stone_vein" }.All(resourceIds.Contains))
                instance = candidate;
        }
        Check(instance is not null, "多组种子应覆盖四种已配置采集资源。");
        var tower = instance!;
        DynamicDungeonExpedition.SynchronizeInstanceEcology(tower, start + 170 * 24);
        var herbNode = DynamicDungeonExpedition.GetNodes(tower).First(node => node.ResourceNodeId == "common_herb");
        var herb = DynamicDungeonExpedition.AvailableActions(tower, herbNode.Id, new GDictionary()).Single(action => action.ItemPath.EndsWith("苦苔根.tres", StringComparison.Ordinal));
        var route = DynamicDungeonExpedition.GetRoute(tower);
        var actions = route["actions"].AsGodotDictionary();
        var key = herbNode.Id + ":" + herb.Id;
        actions[key] = herb.Quantity;
        route["actions"] = actions;
        DynamicDungeonExpedition.StoreRoute(tower, route);
        var sevenDays = DynamicDungeonExpedition.RecoveryRate(herb.Recovery).PeriodHours;
        DynamicDungeonExpedition.SynchronizeInstanceEcology(tower, start + 170 * 24 + sevenDays - 1);
        Check(DynamicDungeonExpedition.GetRoute(tower)["actions"].AsGodotDictionary()[key].AsInt32() == herb.Quantity,
            "晚采集资源应从采集时起算，未满七日不得恢复。");
        DynamicDungeonExpedition.SynchronizeInstanceEcology(tower, start + 170 * 24 + sevenDays);
        Check(DynamicDungeonExpedition.GetRoute(tower)["actions"].AsGodotDictionary()[key].AsInt32() == 0,
            "常见植物应在完整七日后按周期恢复。");

        foreach (var (resourceId, expectedGroup, expectedDays) in new[]
        {
            ("mistbell_slow", EastWoodsActions.ResourceRecoveryGroup.SlowGrowth, 30),
            ("spirit_stone_vein", EastWoodsActions.ResourceRecoveryGroup.Mineral, 90),
        })
        {
            var node = DynamicDungeonExpedition.GetNodes(tower).First(candidate => candidate.ResourceNodeId == resourceId);
            var action = DynamicDungeonExpedition.AvailableActions(tower, node.Id, new GDictionary()).Single(candidate => candidate.Recovery == expectedGroup);
            Check(DynamicDungeonExpedition.RecoveryRate(action.Recovery).PeriodHours == expectedDays * 24,
                $"资源类别 {resourceId} 的恢复周期不匹配。");
        }

        var beasts = tower["beasts"].AsGodotArray();
        var firstRecord = beasts[0].AsGodotDictionary();
        var deadIdentity = firstRecord["encounter_id"].AsString();
        var deadNode = firstRecord["node"].AsString();
        var deadAction = firstRecord["action_id"].AsString();
        route = DynamicDungeonExpedition.GetRoute(tower);
        actions = route["actions"].AsGodotDictionary();
        actions[deadNode + ":" + deadAction] = 1;
        route["actions"] = actions;
        DynamicDungeonExpedition.StoreRoute(tower, route);
        DynamicDungeonExpedition.SynchronizeInstanceEcology(tower, start + 170 * 24 + 14 * 24);
        var records = tower["beasts"].AsGodotArray();
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
        Check(legacy.migrate_to_current_format() && legacy.format_version == PersistentGameSave.CurrentFormatVersion &&
              legacy.dynamic_dungeon_state["last_schedule_hour"].AsInt32() == oldHour &&
              legacy.dynamic_dungeon_state["instances"].AsGodotArray().Count == 0 && legacy.banquet_events.Count == 0,
            "v7→v9 迁移应以存档当前时刻建立空机遇游标，并初始化空宴席表，不补发历史实例。");
        Check(legacy.scene_progress["east_woods_dungeon_state"].AsGodotDictionary()["inside"].AsBool(),
            "版本迁移不得改写东林地原进度。");

        var v8 = new PersistentGameSave
        {
            format_version = 8,
            year = 5,
            month = 2,
            scene_progress = new GDictionary { ["east_woods_dungeon_state"] = new GDictionary { ["inside"] = true, ["route"] = "kept-v8" } },
            dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(500_000),
        };
        v8.world_npcs.Add(new WorldNpcData { npc_id = "v8_social_state", acquaintance_window_until_hour = 540_000 });
        Check(v8.migrate_to_current_format() && v8.format_version == PersistentGameSave.CurrentFormatVersion &&
              v8.banquet_events.Count == 0 && v8.world_npcs[0].acquaintance_window_until_hour == 540_000 &&
              v8.scene_progress["east_woods_dungeon_state"].AsGodotDictionary()["route"].AsString() == "kept-v8" &&
              v8.dynamic_dungeon_state["last_schedule_hour"].AsInt32() == 500_000,
            "v8→v9 应初始化空宴席表，并保留信社交字段、东林地与动态副本快照。");

        const int now = 100_000;
        var state = DynamicDungeonExpedition.CreateEmpty(now);
        DynamicDungeonExpedition.Synchronize(state, now, candidates);
        var banquet = new BanquetEventData
        {
            event_id = "banquet-roundtrip",
            host_id = "npc-host",
            host_name = "陈砚",
            player_is_host = false,
            source_letter_id = "letter-source",
            venue_id = "waterside_pavilion",
            starts_at_hour = now,
            duration_hours = 4,
            status = BanquetEventData.InProgress,
            fee_settled = true,
            time_settled = true,
            next_beat_index = 1,
            active_beat_id = "banquet-roundtrip:beat:1",
        };
        banquet.invited_npc_ids.Add("player"); banquet.invited_npc_ids.Add("guest");
        banquet.attendee_npc_ids.Add("npc-host"); banquet.attendee_npc_ids.Add("guest");
        banquet.beats.Add(new GDictionary { ["beat_id"] = "banquet-roundtrip:beat:1", ["speaker_id"] = "guest", ["text"] = "席间一语" });
        banquet.responses.Add(new GDictionary
        {
            ["beat_id"] = "banquet-roundtrip:beat:0", ["choice_id"] = "humble", ["tone"] = "谦让",
            ["heard_by"] = new Godot.Collections.Array<string> { "npc-host", "guest" }, ["timed_out"] = true,
        });
        var save = new PersistentGameSave
        {
            dynamic_dungeon_state = state.Duplicate(true),
            banquet_events = new Godot.Collections.Array<BanquetEventData> { banquet },
        };
        var path = "user://dynamic_dungeon_smoke_" + Guid.NewGuid().ToString("N") + ".tres";
        try
        {
            Check(ResourceSaver.Save(save, path) == Error.Ok, "动态副本 Resource 测试存档无法保存。");
            var restored = ResourceLoader.Load<PersistentGameSave>(path);
            Check(restored is not null && DynamicDungeonExpedition.ValidateState(restored.dynamic_dungeon_state) &&
                  restored.dynamic_dungeon_state["instances"].AsGodotArray().Count == 1 && restored.banquet_events.Count == 1 &&
                  restored.banquet_events[0].status == BanquetEventData.InProgress && restored.banquet_events[0].fee_settled &&
                  restored.banquet_events[0].time_settled && restored.banquet_events[0].active_beat_id == banquet.active_beat_id &&
                  restored.banquet_events[0].responses[0].GetValueOrDefault("timed_out", false).AsBool(),
                "正式存档读回后未恢复活动副本实例或席间中断／回应状态。");
        }
        finally
        {
            var absolute = ProjectSettings.GlobalizePath(path);
            if (Godot.FileAccess.FileExists(absolute)) DirAccess.RemoveAbsolute(absolute);
        }
    }

    private static void check_compact_creature_roster()
    {
        const int start = 133_000;
        var state = DynamicDungeonExpedition.CreateEmpty(start);
        var instance = DynamicDungeonExpedition.EnsureCompactInstance(state, new Vector2I(4, 7), start);
        var beasts = instance["beasts"].AsGodotArray().Select(value => value.AsGodotDictionary()).ToArray();
        var byNode = beasts.ToDictionary(record => record.GetValueOrDefault("node", "").AsString());
        Check(byNode["fern_02"].GetValueOrDefault("creature_id", "").AsString() == "forest_wolf" &&
              byNode["fern_05"].GetValueOrDefault("creature_id", "").AsString() == "gray_back_boar" &&
              byNode["fern_07"].GetValueOrDefault("creature_id", "").AsString() == "forest_bei_pack",
            "蕨林试玩副本的三个遭遇应分别使用凶狼、野猪和狼狈组合。");

        var packAction = DynamicDungeonExpedition.AvailableActions(instance, "fern_07", new GDictionary()).Single(action => action.Battle);
        Check(packAction.CreatureId == "forest_bei_pack" && packAction.Name == "挑战狼狈同行" && packAction.Description.Contains("狈首"),
            "狼狈遭遇应显示对应的生灵名称与种族天赋说明。");
        var pack = DynamicDungeonExpedition.GetEncounterCreatureIds(instance, byNode["fern_07"].GetValueOrDefault("encounter_id", "").AsString());
        Check(pack.SequenceEqual(new[] { "forest_bei", "forest_wolf" }), "狼狈遭遇应由妖狈与凶狼共同进入战斗。");

        foreach (var record in beasts) record["creature_id"] = "forest_wolf";
        var savedNodes = instance["map_snapshot"].AsGodotDictionary()["nodes"].AsGodotArray();
        savedNodes[5].AsGodotDictionary()["title"] = "旧版凶狼节点";
        instance = DynamicDungeonExpedition.EnsureCompactInstance(state, new Vector2I(4, 7), start + 1);
        byNode = instance["beasts"].AsGodotArray().Select(value => value.AsGodotDictionary()).ToDictionary(record => record.GetValueOrDefault("node", "").AsString());
        Check(byNode["fern_05"].GetValueOrDefault("creature_id", "").AsString() == "gray_back_boar" &&
              byNode["fern_07"].GetValueOrDefault("creature_id", "").AsString() == "forest_bei_pack" &&
              DynamicDungeonExpedition.GetNode(instance, "fern_05")?.Title == "灰背野猪",
            "已有试玩存档应更新未完成遭遇和节点名称，不重置路线进度。");

        var boar = GD.Load<BeastTemplate>("res://resources/beast_templates/灰背野猪_试玩.tres");
        var wolf = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres");
        var bei = GD.Load<BeastTemplate>("res://resources/beast_templates/林地妖狈.tres");
        Check(boar?.deck is not null && boar.racial_talent?.talent_id == "greyback_boar_thick_hide" &&
              boar.deck.get_deck_skills().Contains("res://resources/activeskills/妖兽/灰背野猪/拱撞.tres") &&
              boar.deck.get_deck_skills().Contains("res://resources/activeskills/妖兽/灰背野猪/拱身戒备.tres"),
            "灰背野猪模板应绑定现有拱撞、拱身架招与厚皮天赋。");
        Check(wolf?.deck is not null && wolf.racial_talent?.talent_id == "forest_wolf_pack_hunt" &&
              bei?.deck is not null && bei.racial_talent?.talent_id == "forest_bei_pack_chief",
            "凶狼与妖狈模板应继续引用现有技能组和种族天赋。");
        Check(ResourceLoader.Exists("res://textures/bestiary/static/gray_back_boar.png") &&
              ResourceLoader.Exists("res://textures/bestiary/static/forest_wolf.png") &&
              ResourceLoader.Exists("res://textures/bestiary/static/forest_bei_v1.png"),
            "三种试玩生灵的占位贴图都应能加载。");

        foreach (var (speciesId, templatePath, portraitPath) in new[]
        {
            ("qing_tail_fox", "res://resources/beast_templates/青尾狐.tres", "res://textures/bestiary/static/qing_tail_fox.png"),
            ("red_mane_wolf", "res://resources/beast_templates/赤鬃狼.tres", "res://textures/bestiary/static/red_mane_wolf.png"),
            ("rock_back_badger", "res://resources/beast_templates/岩背獾.tres", "res://textures/bestiary/static/rock_back_badger.png"),
        })
        {
            var template = GD.Load<BeastTemplate>(templatePath);
            Check(template is not null && template.species_id == speciesId && template.deck is not null &&
                  template.combat_board_layout is { confirmed: true } && template.deck.get_deck_skills().Count > 0 &&
                  ResourceLoader.Exists(portraitPath),
                $"{speciesId} 应绑定完整固定盘、既有兽招与对应占位贴图。");
        }

        var seenNewCreatures = new HashSet<string>(StringComparer.Ordinal);
        for (var seedIndex = 0; seedIndex < 64 && seenNewCreatures.Count < 3; seedIndex++)
        {
            var generated = create_tower(140_000, "tower-creature-smoke-" + seedIndex).Instance;
            foreach (var record in generated["beasts"].AsGodotArray().Select(value => value.AsGodotDictionary()))
            {
                var creatureId = record.GetValueOrDefault("creature_id", "").AsString();
                if (creatureId is not ("qing_tail_fox" or "red_mane_wolf" or "rock_back_badger")) continue;
                var count = record.GetValueOrDefault("creature_count", 0).AsInt32();
                var encounterId = record.GetValueOrDefault("encounter_id", "").AsString();
                var roster = DynamicDungeonExpedition.GetEncounterCreatureIds(generated, encounterId);
                Check(count is 2 or 3 && roster.Length == count && roster.All(id => id == creatureId),
                    $"{creatureId} 遭遇组应为存档固定的 2–3 只同种生灵。");
                seenNewCreatures.Add(creatureId);
            }
        }
        Check(seenNewCreatures.SetEquals(new[] { "qing_tail_fox", "red_mane_wolf", "rock_back_badger" }),
            "随机塔图应能抽取青尾狐、赤鬃狼和岩背獾三种新增生灵。");
    }

    private void check_creature_battle_packages()
    {
        foreach (var (speciesId, templatePath, portraitPath) in new[]
        {
            ("forest_wolf", "res://resources/beast_templates/林地凶狼.tres", "res://textures/bestiary/static/forest_wolf.png"),
            ("gray_back_boar", "res://resources/beast_templates/灰背野猪_试玩.tres", "res://textures/bestiary/static/gray_back_boar.png"),
            ("forest_bei", "res://resources/beast_templates/林地妖狈.tres", "res://textures/bestiary/static/forest_bei_v1.png"),
        })
        {
            var beast = new Beast
            {
                Name = "CreaturePackageSmoke_" + speciesId,
                unique_id = "creature_package_smoke_" + speciesId,
                beast_template = GD.Load<BeastTemplate>(templatePath),
                entity_data = new LivingData(),
                faction = (int)WorldEntity.Faction.ENEMY,
                Visible = false,
            };
            AddChild(beast);
            var packed = beast.pack_for_battle();
            var snapshot = beast.get_snapshot_data();
            Check(packed is not null && snapshot is not null && snapshot.entity_name != "未命名" &&
                  snapshot.battle_portrait_path == portraitPath && beast.beast_deck is { } deck && deck.get_deck_skills().Count > 0,
                $"{speciesId} 应能生成带技能组和占位立绘的战斗单位快照。");
            RemoveChild(beast);
            beast.Free();
        }
    }

    private void check_dynamic_panel_setup()
    {
        const int start = 120_000;
        var (state, instance) = create_tower(start, "tower-panel-smoke");
        var panel = new EastWoodsDungeonPanel { Name = "DynamicDungeonSmokePanel" };
        AddChild(panel);
        panel.configure_dynamic_instance(instance);
        panel.show_dungeon();
        Check(panel.is_dynamic_instance && panel.get_current_route_node_id() == DynamicDungeonExpedition.GetEntryNodeId(instance),
            "副本面板应以动态实例入口和独立地图启动。");
        var nextNode = DynamicDungeonExpedition.GetNeighbors(instance, DynamicDungeonExpedition.GetEntryNodeId(instance)).First();
        Check(panel.get_assigned_route_step_count(nextNode) == 1 &&
              panel.get_assigned_route_step_count(DynamicDungeonExpedition.GetExitNodeId(instance)) == -1,
            "塔图面板应允许选择相连的下一层节点，不得自动规划直达出口的路线。");
        panel.Hide();
        panel.QueueFree();
    }
}
