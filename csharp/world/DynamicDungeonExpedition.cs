using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Persisted lifecycle, layout, actions and ecology for optional limited-time field instances.</summary>
public static class DynamicDungeonExpedition
{
    public const string TemplateId = "mist_hidden_valley";
    public const string DisplayName = "雾隐幽谷";
    public const string DungeonIdPrefix = "mist_hidden_valley:";
    public const int ScheduleIntervalHours = 180 * 24;
    public const int OpenDurationHours = 360 * 24;
    public const int MaximumOpenInstances = 2;
    public const int MapColumns = 16;
    public const int MapRows = 8;
    public const int MapCellCount = MapColumns * MapRows;
    private const int HoursPerDay = 24;
    private const int OrdinaryBeastCheckHours = 14 * HoursPerDay;
    private const string EntryNodeId = "valley_00_04";
    private const string ItemRoot = "res://resources/entities/items/";
    private static readonly Vector2I[] Directions = [Vector2I.Right, Vector2I.Down, Vector2I.Left, Vector2I.Up];
    private static readonly System.Collections.Generic.Dictionary<string, LayoutRuntime> LayoutCache = new(StringComparer.Ordinal);

    private sealed class LayoutRuntime
    {
        public required EastWoodsDungeonRules.NodeDefinition[] Nodes;
        public required System.Collections.Generic.Dictionary<string, EastWoodsDungeonRules.NodeDefinition> NodesById;
        public required System.Collections.Generic.Dictionary<string, List<string>> Neighbors;
    }

    public static GDictionary CreateEmpty(int currentGameHour)
        => new()
        {
            ["schema_version"] = 1,
            ["activated"] = false,
            ["last_schedule_hour"] = Math.Max(0, currentGameHour),
            ["next_instance_sequence"] = 1,
            ["instances"] = new GArray(),
        };

    /// <summary>
    /// Runs an immediate first attempt, then one attempt at each scheduled 180-day boundary.
    /// Failed attempts are consumed by advancing the cursor; they never become spawn debt.
    /// </summary>
    public static bool Synchronize(GDictionary state, int currentGameHour, Func<IReadOnlyList<Vector2I>> candidateProvider)
    {
        var changed = EnsureState(state, currentGameHour);
        var now = Math.Max(0, currentGameHour);
        var instances = state["instances"].AsGodotArray();
        if (!state.GetValueOrDefault("activated", false).AsBool())
        {
            state["activated"] = true;
            state["last_schedule_hour"] = now;
            changed = true;
            changed |= ExpireAndSyncInstances(instances, now);
            changed |= TrySpawn(state, now, candidateProvider?.Invoke() ?? System.Array.Empty<Vector2I>());
        }
        else
        {
            var cursor = Math.Max(0, state.GetValueOrDefault("last_schedule_hour", now).AsInt32());
            if (now < cursor)
            {
                state["last_schedule_hour"] = now;
                changed = true;
                cursor = now;
            }
            var next = cursor + ScheduleIntervalHours;
            while (next <= now)
            {
                changed |= ExpireAndSyncInstances(instances, next);
                state["last_schedule_hour"] = next;
                changed = true;
                changed |= TrySpawn(state, next, candidateProvider?.Invoke() ?? System.Array.Empty<Vector2I>());
                cursor = next;
                next = cursor + ScheduleIntervalHours;
            }
            changed |= ExpireAndSyncInstances(instances, now);
        }
        return changed;
    }

    private static bool EnsureState(GDictionary state, int currentGameHour)
    {
        var changed = false;
        if (!state.ContainsKey("schema_version")) { state["schema_version"] = 1; changed = true; }
        if (!state.ContainsKey("activated")) { state["activated"] = false; changed = true; }
        if (!state.ContainsKey("last_schedule_hour")) { state["last_schedule_hour"] = Math.Max(0, currentGameHour); changed = true; }
        if (!state.ContainsKey("next_instance_sequence")) { state["next_instance_sequence"] = 1; changed = true; }
        if (!state.ContainsKey("instances") || state["instances"].VariantType != Variant.Type.Array)
        {
            state["instances"] = new GArray();
            changed = true;
        }
        return changed;
    }

    private static bool ExpireAndSyncInstances(GArray instances, int hour)
    {
        var changed = false;
        foreach (var value in instances)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var instance = value.AsGodotDictionary();
            var expiry = instance.GetValueOrDefault("expires_hour", hour).AsInt32();
            changed |= SynchronizeEcology(instance, Math.Min(hour, expiry));
            if (instance.GetValueOrDefault("entry_open", false).AsBool() && hour >= expiry)
            {
                instance["entry_open"] = false;
                changed = true;
            }
        }
        return changed;
    }

    private static bool TrySpawn(GDictionary state, int generatedHour, IReadOnlyList<Vector2I> candidates)
    {
        if (OpenInstanceCount(state, generatedHour) >= MaximumOpenInstances || candidates.Count == 0) return false;
        var occupied = new HashSet<Vector2I>();
        foreach (var value in state["instances"].AsGodotArray())
            if (value.VariantType == Variant.Type.Dictionary)
            {
                var instance = value.AsGodotDictionary();
                if (instance.GetValueOrDefault("entry_open", false).AsBool())
                    occupied.Add(instance.GetValueOrDefault("world_cell", Vector2I.Zero).AsVector2I());
            }
        var uniqueCandidates = candidates.Distinct().Where(cell => !occupied.Contains(cell)).OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        if (uniqueCandidates.Length == 0) return false;

        var sequence = Math.Max(1, state.GetValueOrDefault("next_instance_sequence", 1).AsInt32());
        var id = "mhv_" + sequence.ToString("D6");
        var cycle = Math.Max(0, state.GetValueOrDefault("last_schedule_hour", generatedHour).AsInt32());
        var pick = (int)(StableSeed(TemplateId + "|" + generatedHour + "|" + cycle + "|" + id) % (ulong)uniqueCandidates.Length);
        var seed = TemplateId + "|" + id + "|" + generatedHour + "|" + uniqueCandidates[pick].X + "," + uniqueCandidates[pick].Y;
        state["instances"].AsGodotArray().Add(CreateInstance(id, seed, uniqueCandidates[pick], generatedHour));
        state["next_instance_sequence"] = sequence + 1;
        return true;
    }

    private static GDictionary CreateInstance(string id, string seed, Vector2I worldCell, int generatedHour)
    {
        var layout = GenerateLayout(seed);
        var instance = new GDictionary
        {
            ["instance_id"] = id,
            ["template_id"] = TemplateId,
            ["seed"] = seed,
            ["world_cell"] = worldCell,
            ["generated_hour"] = generatedHour,
            ["expires_hour"] = generatedHour + OpenDurationHours,
            ["entry_open"] = true,
            ["inside"] = false,
            ["map_snapshot"] = layout,
            ["resource_cursors"] = new GDictionary(),
            ["beast_cursor_hour"] = generatedHour,
            ["beast_sequence"] = 1,
            ["beasts"] = new GArray(),
            ["event_results"] = new GDictionary(),
            ["route"] = new GDictionary
            {
                ["current"] = EntryNodeId,
                ["visited"] = new GArray { EntryNodeId },
                ["discovered"] = new GArray { EntryNodeId },
                ["actions"] = new GDictionary(),
                ["inside"] = false,
            },
        };
        foreach (var node in GetNodes(instance))
            if (GetResourceAction(node) is { OrdinaryBeastSource: true } action)
                instance["beasts"].AsGodotArray().Add(NewBeastRecord(instance, node.Id, action, generatedHour));
        return instance;
    }

    private static GDictionary NewBeastRecord(GDictionary instance, string nodeId, EastWoodsActions.ActionDef source, int hour)
    {
        var sequence = Math.Max(1, instance.GetValueOrDefault("beast_sequence", 1).AsInt32());
        instance["beast_sequence"] = sequence + 1;
        var identity = instance.GetValueOrDefault("instance_id", "unknown").AsString() + "_beast_" + sequence.ToString("D4");
        return new GDictionary
        {
            ["encounter_id"] = identity,
            ["action_id"] = "beast_" + sequence.ToString("D4"),
            ["node"] = nodeId,
            ["creature_id"] = "forest_wolf",
            ["created_hour"] = hour,
            ["resolved_hour"] = -1,
            ["status"] = "active",
        };
    }

    private static GDictionary GenerateLayout(string seed)
    {
        var nodes = new GArray();
        var edges = new GArray();
        var layoutSeed = StableSeed(seed);
        for (var y = 0; y < MapRows; y++)
        for (var x = 0; x < MapColumns; x++)
        {
            var id = node_id(x, y);
            var terrainNoise = StableSeed(seed + "|terrain|" + x + "," + y) % 7;
            var terrain = x <= 5
                ? terrainNoise == 0 ? 0 : 6
                : x <= 9 ? y is >= 2 and <= 5 ? 5 : terrainNoise % 3 == 0 ? 3 : 1
                : (terrainNoise % 3 == 0 ? 7 : 2);
            var feature = x == 7 && y == 4 ? "spring" : "";
            var resourceId = (x, y) switch
            {
                (2, 1) or (3, 6) => "common_herb",
                (4, 2) or (5, 5) => "common_moss",
                (7, 6) => "mistbell_slow",
                (12, 2) => "spirit_stone_vein",
                _ => "",
            };
            var category = resourceId switch
            {
                "common_herb" or "common_moss" or "mistbell_slow" => EastWoodsResourceCategory.Plant,
                "spirit_stone_vein" => EastWoodsResourceCategory.Mineral,
                _ => EastWoodsResourceCategory.None,
            };
            var eventId = (x, y) switch
            {
                (2, 4) => "bamboo_tracks",
                (7, 4) => "spring_flow",
                (12, 4) => "rock_marks",
                _ => "",
            };
            var title = (x, y) switch
            {
                (0, 4) => "幽谷入口",
                (7, 4) => "回涌泉眼",
                (7, 3) => "中段湿地",
                (2, 4) => "竹影旧痕",
                (12, 4) => "岩隙爪痕",
                (15, 4) => "岩壁回环",
                _ => "",
            };
            var description = terrain switch
            {
                6 => "竹影层叠，窄径在林间相接；可留意现有草木与地上的旧痕。",
                5 => "湿地浅水沿石间缓慢流动，脚下仍有可通行的干地。",
                7 => "阴岩裂隙交错，路边留着妖兽经过的痕迹。",
                2 => "岩隙路向谷内延伸，两侧偶有岔道和兽迹。",
                3 => "浅水切过低洼地，沿可见的干燥地块能够绕行。",
                _ => "谷中林地暂时平静，几条相连的路可以继续深入。",
            };
            if (!string.IsNullOrEmpty(feature)) description = "泉水从岩下涌出，在湿地中段分作两股，周围仍可沿干地通行。";
            if (!string.IsNullOrEmpty(resourceId)) description += " 此处有可辨认的采集资源。";
            if (!string.IsNullOrEmpty(eventId)) description += " 现场留有值得亲自判断的线索。";
            var grid = new Vector2I(x, y);
            var showTitle = title.Length > 0;
            var node = new EastWoodsDungeonRules.NodeDefinition(id, title, description, grid, showTitle,
                terrain, feature, resourceId, category);
            nodes.Add(new GDictionary
            {
                ["id"] = node.Id, ["title"] = node.Title, ["description"] = node.Description,
                ["x"] = x, ["y"] = y, ["show_title"] = node.ShowTitleOnMap,
                ["terrain"] = node.Terrain, ["feature"] = node.Feature,
                ["resource_id"] = node.ResourceNodeId, ["resource_category"] = (int)node.ResourceCategory,
                ["event_id"] = eventId,
            });
            if (x + 1 < MapColumns) edges.Add(new GDictionary { ["from"] = id, ["to"] = node_id(x + 1, y) });
            if (y + 1 < MapRows) edges.Add(new GDictionary { ["from"] = id, ["to"] = node_id(x, y + 1) });
        }

        var snapshot = new GDictionary
        {
            ["template_id"] = TemplateId,
            ["seed"] = seed,
            ["seed_hash"] = (long)(layoutSeed & long.MaxValue),
            ["version"] = 1,
            ["columns"] = MapColumns,
            ["rows"] = MapRows,
            ["entry_node_id"] = EntryNodeId,
            ["exit_node_id"] = EntryNodeId,
            ["nodes"] = nodes,
            ["edges"] = edges,
        };
        ValidateLayout(snapshot);
        return snapshot;
    }

    private static bool SynchronizeEcology(GDictionary instance, int hour)
    {
        var changed = false;
        var generatedHour = instance.GetValueOrDefault("generated_hour", hour).AsInt32();
        var limit = Math.Max(generatedHour, Math.Min(hour, instance.GetValueOrDefault("expires_hour", hour).AsInt32()));
        var route = instance.GetValueOrDefault("route", new GDictionary()).AsGodotDictionary();
        var actions = route.GetValueOrDefault("actions", new GDictionary()).AsGodotDictionary();
        var cursors = instance.GetValueOrDefault("resource_cursors", new GDictionary()).AsGodotDictionary();
        foreach (var node in GetNodes(instance))
        {
            var action = GetResourceAction(node);
            if (action is null || action.Recovery == EastWoodsActions.ResourceRecoveryGroup.None) continue;
            var key = node.Id + ":" + action.Id;
            var current = Math.Max(0, actions.GetValueOrDefault(key, 0).AsInt32());
            if (!cursors.ContainsKey(key))
            {
                // Recovery starts when a node is first depleted, not merely because
                // its instance has existed for several weeks before anyone harvests it.
                cursors[key] = limit;
                changed = true;
                continue;
            }
            if (current <= 0)
            {
                if (cursors.GetValueOrDefault(key, generatedHour).AsInt32() != limit)
                {
                    cursors[key] = limit;
                    changed = true;
                }
                continue;
            }
            var last = cursors.GetValueOrDefault(key, generatedHour).AsInt32();
            var (period, amount) = RecoveryRate(action.Recovery);
            var cycles = Math.Max(0, (limit - last) / period);
            if (cycles > 0)
            {
                var remaining = Math.Max(0, current - cycles * amount);
                if (remaining != current) actions[key] = remaining;
                cursors[key] = last + cycles * period;
                changed = true;
            }
            else if (limit < last)
            {
                cursors[key] = limit;
                changed = true;
            }
        }

        var beastCursor = Math.Max(generatedHour, instance.GetValueOrDefault("beast_cursor_hour", generatedHour).AsInt32());
        var beasts = instance.GetValueOrDefault("beasts", new GArray()).AsGodotArray();
        foreach (var recordValue in beasts)
        {
            if (recordValue.VariantType != Variant.Type.Dictionary) continue;
            var record = recordValue.AsGodotDictionary();
            var node = record.GetValueOrDefault("node", "").AsString();
            var actionId = record.GetValueOrDefault("action_id", "").AsString();
            if (record.GetValueOrDefault("status", "active").AsString() == "active" && actions.GetValueOrDefault(node + ":" + actionId, 0).AsInt32() > 0)
            {
                record["status"] = "defeated";
                record["resolved_hour"] = limit;
                changed = true;
            }
        }
        while (beastCursor + OrdinaryBeastCheckHours <= limit)
        {
            beastCursor += OrdinaryBeastCheckHours;
            foreach (var node in GetNodes(instance))
            {
                if (GetResourceAction(node) is not { OrdinaryBeastSource: true } source) continue;
                var active = beasts.Any(value => value.VariantType == Variant.Type.Dictionary &&
                    value.AsGodotDictionary().GetValueOrDefault("node", "").AsString() == node.Id &&
                    value.AsGodotDictionary().GetValueOrDefault("status", "active").AsString() == "active" &&
                    actions.GetValueOrDefault(node.Id + ":" + value.AsGodotDictionary().GetValueOrDefault("action_id", "").AsString(), 0).AsInt32() <= 0);
                if (active) continue;
                beasts.Add(NewBeastRecord(instance, node.Id, source, beastCursor));
                changed = true;
            }
        }
        if (beastCursor != instance.GetValueOrDefault("beast_cursor_hour", generatedHour).AsInt32())
        {
            instance["beast_cursor_hour"] = beastCursor;
            changed = true;
        }
        instance["resource_cursors"] = cursors;
        route["actions"] = actions;
        instance["route"] = route;
        instance["beasts"] = beasts;
        return changed;
    }

    public static bool SynchronizeInstanceEcology(GDictionary instance, int hour)
        => SynchronizeEcology(instance, hour);

    public static (int PeriodHours, int Amount) RecoveryRate(EastWoodsActions.ResourceRecoveryGroup group)
        => group switch
        {
            EastWoodsActions.ResourceRecoveryGroup.Common => (7 * HoursPerDay, 2),
            EastWoodsActions.ResourceRecoveryGroup.SlowGrowth => (30 * HoursPerDay, 1),
            EastWoodsActions.ResourceRecoveryGroup.Mineral => (90 * HoursPerDay, 1),
            _ => (int.MaxValue, 0),
        };

    private static EastWoodsActions.ActionDef? GetResourceAction(EastWoodsDungeonRules.NodeDefinition node)
        => node.ResourceNodeId switch
        {
            "common_herb" => new("gather_root", "采集", "竹根下生着苦苔根。每次可采两份，储量有限；此类植物每7日恢复。", 1, 2,
                "你收好苦苔根 ×2。", ItemRoot + "苦苔根.tres", 2, 4, Recovery: EastWoodsActions.ResourceRecoveryGroup.Common),
            "common_moss" => new("gather_moss", "采集", "背阴处附着月露苔。每次可采一份，储量有限；此类植物每7日恢复。", 1, 2,
                "你收好月露苔 ×1。", ItemRoot + "月露苔.tres", 1, 2, Recovery: EastWoodsActions.ResourceRecoveryGroup.Common),
            "mistbell_slow" => new("gather_mistbell", "采集", "湿润林缘生着少量雾铃花。每次可采一份，按30日慢生周期恢复。", 1, 2,
                "你采下雾铃花 ×1。", ItemRoot + "雾铃花.tres", 1, 2, Recovery: EastWoodsActions.ResourceRecoveryGroup.SlowGrowth),
            "spirit_stone_vein" => new("mine_spirit_stone", "采矿", "岩层裂隙中有一处灵石脉。每次可采一份，按90日矿脉周期恢复。", 1, 2,
                "你采得灵石 ×1。", ItemRoot + "灵石.tres", 1, 2, Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral),
            _ => GetBeastSource(node),
        };

    private static List<EastWoodsActions.ActionDef> GetEventActions(EastWoodsDungeonRules.NodeDefinition node) => node.Id switch
    {
        "valley_02_04" =>
        [
            new("event_bamboo_north", "循竹影向北", "耗时1小时、体力1。你辨出竹林北侧干径，揭开一段山麓路线；不获得额外物品。", 1, 1,
                "北侧干径已记明。", Quantity: 1, Stock: 1),
            new("event_bamboo_south", "沿苔痕向南", "耗时1小时、体力1。你沿湿苔找到竹林南侧的绕行路；不获得额外物品。", 1, 1,
                "南侧绕行路已记明。", Quantity: 1, Stock: 1),
        ],
        "valley_07_04" =>
        [
            new("event_spring_north", "沿泉声辨北岸", "耗时1小时、体力1。你辨出湿地北岸的干地，揭开通往北侧岩隙的路线；不采走泉水。", 1, 1,
                "湿地北岸路线已记明。", Quantity: 1, Stock: 1),
            new("event_spring_south", "沿泉声辨南岸", "耗时1小时、体力1。你辨出湿地南岸的绕行路；不采走泉水。", 1, 1,
                "湿地南岸路线已记明。", Quantity: 1, Stock: 1),
        ],
        "valley_12_04" =>
        [
            new("event_rock_north", "循爪痕探北隙", "耗时1小时、体力1。你辨出岩隙北面的回环路线；爪痕指向的妖兽仍须由你决定是否应战。", 1, 1,
                "北面回环路线已记明。", Quantity: 1, Stock: 1),
            new("event_rock_south", "循碎石探南隙", "耗时1小时、体力1。你辨出岩隙南面的支路；可选择绕开兽迹或之后再来。", 1, 1,
                "南面支路已记明。", Quantity: 1, Stock: 1),
        ],
        _ => [],
    };

    private static EastWoodsActions.ActionDef? GetBeastSource(EastWoodsDungeonRules.NodeDefinition node)
        => node.Id is "valley_11_01" or "valley_12_06" or "valley_14_03"
            ? new("wolf", "应战", "一头林地凶狼在岩隙间活动。你可以应战，也可以沿其他路线绕行；胜负使用现有战斗与战利品结算。", 0, 0,
                "此处妖兽已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1)
            : null;

    public static List<EastWoodsActions.ActionDef> AvailableActions(GDictionary instance, string nodeId, GDictionary actions)
    {
        var result = new List<EastWoodsActions.ActionDef>();
        var node = GetNode(instance, nodeId);
        if (node is null) return result;
        if (GetResourceAction(node) is { } fixedAction && fixedAction.Recovery != EastWoodsActions.ResourceRecoveryGroup.None &&
            Math.Max(0, fixedAction.Stock - actions.GetValueOrDefault(nodeId + ":" + fixedAction.Id, 0).AsInt32()) >= Math.Max(1, fixedAction.Quantity))
            result.Add(fixedAction);
        var eventId = GetEventId(node);
        if (eventId.Length > 0 && !instance.GetValueOrDefault("event_results", new GDictionary()).AsGodotDictionary().ContainsKey(eventId))
            result.AddRange(GetEventActions(node));
        var beasts = instance.GetValueOrDefault("beasts", new GArray()).AsGodotArray();
        foreach (var value in beasts)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            if (record.GetValueOrDefault("node", "").AsString() != nodeId ||
                record.GetValueOrDefault("status", "active").AsString() != "active") continue;
            var actionId = record.GetValueOrDefault("action_id", "").AsString();
            if (actions.GetValueOrDefault(nodeId + ":" + actionId, 0).AsInt32() > 0) continue;
            var source = GetBeastSource(node);
            if (source is not null) result.Add(source with { Id = actionId, EncounterIdentity = record.GetValueOrDefault("encounter_id", "").AsString() });
        }
        return result;
    }

    public static void ApplyCompletedAction(GDictionary instance, string nodeId, string actionId, GDictionary routeState)
    {
        if (string.IsNullOrWhiteSpace(actionId) || !actionId.StartsWith("event_", StringComparison.Ordinal)) return;
        var node = GetNode(instance, nodeId);
        if (node is null || string.IsNullOrEmpty(node.Id)) return;
        var eventId = GetEventId(node);
        if (eventId.Length == 0) return;
        var results = instance.GetValueOrDefault("event_results", new GDictionary()).AsGodotDictionary();
        if (results.ContainsKey(eventId)) return;
        results[eventId] = actionId;
        instance["event_results"] = results;
        var reveals = actionId switch
        {
            "event_bamboo_north" => new[] { "valley_05_03", "valley_06_03", "valley_06_02" },
            "event_bamboo_south" => new[] { "valley_05_05", "valley_06_05", "valley_06_06" },
            "event_spring_north" => new[] { "valley_08_03", "valley_09_02", "valley_10_01" },
            "event_spring_south" => new[] { "valley_08_05", "valley_09_06", "valley_10_07" },
            "event_rock_north" => new[] { "valley_13_02", "valley_14_01", "valley_15_00" },
            "event_rock_south" => new[] { "valley_13_06", "valley_14_07", "valley_15_07" },
            _ => System.Array.Empty<string>(),
        };
        var discovered = routeState.GetValueOrDefault("discovered", new GArray()).AsGodotArray();
        foreach (var id in reveals)
            if (!discovered.Any(value => value.AsString() == id)) discovered.Add(id);
        routeState["discovered"] = discovered;
    }

    public static void StoreRoute(GDictionary instance, GDictionary routeState)
    {
        var route = routeState.Duplicate(true);
        instance["route"] = route;
        instance["inside"] = route.GetValueOrDefault("inside", false).AsBool();
    }

    public static GDictionary GetRoute(GDictionary instance)
        => instance.GetValueOrDefault("route", new GDictionary()).AsGodotDictionary().Duplicate(true);

    private static string GetEventId(EastWoodsDungeonRules.NodeDefinition node) => node.Id switch
    {
        "valley_02_04" => "bamboo_tracks",
        "valley_07_04" => "spring_flow",
        "valley_12_04" => "rock_marks",
        _ => "",
    };

    public static bool IsEntryOpen(GDictionary instance, int currentHour)
        => instance.GetValueOrDefault("entry_open", false).AsBool() && currentHour < instance.GetValueOrDefault("expires_hour", 0).AsInt32();

    public static int OpenInstanceCount(GDictionary state, int currentHour)
        => state.GetValueOrDefault("instances", new GArray()).AsGodotArray().Count(value => value.VariantType == Variant.Type.Dictionary && IsEntryOpen(value.AsGodotDictionary(), currentHour));

    public static GDictionary? FindInstance(GDictionary state, string instanceId)
    {
        foreach (var value in state.GetValueOrDefault("instances", new GArray()).AsGodotArray())
            if (value.VariantType == Variant.Type.Dictionary && value.AsGodotDictionary().GetValueOrDefault("instance_id", "").AsString() == instanceId)
                return value.AsGodotDictionary();
        return null;
    }

    public static GDictionary? FindInsideInstance(GDictionary state)
    {
        foreach (var value in state.GetValueOrDefault("instances", new GArray()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var instance = value.AsGodotDictionary();
            var route = instance.GetValueOrDefault("route", new GDictionary()).AsGodotDictionary();
            if (instance.GetValueOrDefault("inside", false).AsBool() || route.GetValueOrDefault("inside", false).AsBool()) return instance;
        }
        return null;
    }

    public static GArray OpenEntries(GDictionary state, int currentHour)
    {
        var result = new GArray();
        foreach (var value in state.GetValueOrDefault("instances", new GArray()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var instance = value.AsGodotDictionary();
            if (!IsEntryOpen(instance, currentHour)) continue;
            result.Add(new GDictionary
            {
                ["instance_id"] = instance.GetValueOrDefault("instance_id", "").AsString(),
                ["name"] = DisplayName,
                ["cell"] = instance.GetValueOrDefault("world_cell", Vector2I.Zero).AsVector2I(),
                ["expires_hour"] = instance.GetValueOrDefault("expires_hour", 0).AsInt32(),
            });
        }
        return result;
    }

    public static IReadOnlyList<EastWoodsDungeonRules.NodeDefinition> GetNodes(GDictionary instance)
        => GetLayout(instance).Nodes;

    public static EastWoodsDungeonRules.NodeDefinition? GetNode(GDictionary instance, string nodeId)
        => GetLayout(instance).NodesById.GetValueOrDefault(nodeId);

    public static IReadOnlyList<string> GetNeighbors(GDictionary instance, string nodeId)
        => GetLayout(instance).Neighbors.TryGetValue(nodeId, out var neighbors) ? neighbors : System.Array.Empty<string>();

    public static string GetEntryNodeId(GDictionary instance) => EntryNodeId;
    public static string GetExitNodeId(GDictionary instance) => instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary().GetValueOrDefault("exit_node_id", EntryNodeId).AsString();
    public static string GetDungeonId(GDictionary instance) => DungeonIdPrefix + instance.GetValueOrDefault("instance_id", "unknown").AsString();
    public static string GetLayoutSeed(GDictionary instance) => instance.GetValueOrDefault("seed", "missing").AsString();

    public static ulong StableSeed(string value)
    {
        unchecked
        {
            ulong hash = 1469598103934665603;
            foreach (var character in value) { hash ^= character; hash *= 1099511628211; }
            return hash == 0 ? 1UL : hash;
        }
    }

    public static bool ValidateState(GDictionary state)
    {
        if (state.GetValueOrDefault("schema_version", 0).AsInt32() != 1 ||
            state.GetValueOrDefault("instances", new GArray()).VariantType != Variant.Type.Array) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in state["instances"].AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) return false;
            var instance = value.AsGodotDictionary();
            var id = instance.GetValueOrDefault("instance_id", "").AsString();
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || instance.GetValueOrDefault("template_id", "").AsString() != TemplateId ||
                instance.GetValueOrDefault("map_snapshot", new GDictionary()).VariantType != Variant.Type.Dictionary) return false;
            ValidateLayout(instance["map_snapshot"].AsGodotDictionary());
            if (instance.GetValueOrDefault("route", new GDictionary()).VariantType != Variant.Type.Dictionary) return false;
        }
        return true;
    }

    private static LayoutRuntime GetLayout(GDictionary instance)
    {
        var snapshot = instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary();
        var seed = snapshot.GetValueOrDefault("seed", instance.GetValueOrDefault("seed", "")).AsString();
        var cacheKey = instance.GetValueOrDefault("instance_id", "") + "|" + seed;
        if (LayoutCache.TryGetValue(cacheKey, out var cached)) return cached;
        ValidateLayout(snapshot);
        var nodes = new List<EastWoodsDungeonRules.NodeDefinition>();
        var byId = new System.Collections.Generic.Dictionary<string, EastWoodsDungeonRules.NodeDefinition>(StringComparer.Ordinal);
        var neighbors = new System.Collections.Generic.Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var value in snapshot.GetValueOrDefault("nodes", new GArray()).AsGodotArray())
        {
            var data = value.AsGodotDictionary();
            var node = new EastWoodsDungeonRules.NodeDefinition(
                data.GetValueOrDefault("id", "").AsString(),
                data.GetValueOrDefault("title", "").AsString(),
                data.GetValueOrDefault("description", "").AsString(),
                new Vector2I(data.GetValueOrDefault("x", 0).AsInt32(), data.GetValueOrDefault("y", 0).AsInt32()),
                data.GetValueOrDefault("show_title", false).AsBool(),
                data.GetValueOrDefault("terrain", 0).AsInt32(),
                data.GetValueOrDefault("feature", "").AsString(),
                data.GetValueOrDefault("resource_id", "").AsString(),
                (EastWoodsResourceCategory)data.GetValueOrDefault("resource_category", 0).AsInt32());
            nodes.Add(node);
            byId[node.Id] = node;
            neighbors[node.Id] = new List<string>();
        }
        foreach (var value in snapshot.GetValueOrDefault("edges", new GArray()).AsGodotArray())
        {
            var edge = value.AsGodotDictionary();
            var from = edge.GetValueOrDefault("from", "").AsString();
            var to = edge.GetValueOrDefault("to", "").AsString();
            neighbors[from].Add(to);
            neighbors[to].Add(from);
        }
        var runtime = new LayoutRuntime { Nodes = nodes.ToArray(), NodesById = byId, Neighbors = neighbors };
        LayoutCache[cacheKey] = runtime;
        return runtime;
    }

    private static void ValidateLayout(GDictionary snapshot)
    {
        var nodes = snapshot.GetValueOrDefault("nodes", new GArray()).AsGodotArray();
        var edges = snapshot.GetValueOrDefault("edges", new GArray()).AsGodotArray();
        if (snapshot.GetValueOrDefault("template_id", "").AsString() != TemplateId ||
            snapshot.GetValueOrDefault("columns", 0).AsInt32() != MapColumns ||
            snapshot.GetValueOrDefault("rows", 0).AsInt32() != MapRows || nodes.Count != MapCellCount ||
            snapshot.GetValueOrDefault("entry_node_id", "").AsString() != EntryNodeId ||
            snapshot.GetValueOrDefault("exit_node_id", "").AsString() != EntryNodeId)
            throw new InvalidOperationException("雾隐幽谷实例地图的模板尺寸、入口或格数无效。");
        var byId = new System.Collections.Generic.Dictionary<string, Vector2I>(StringComparer.Ordinal);
        var byGrid = new HashSet<Vector2I>();
        foreach (var value in nodes)
        {
            if (value.VariantType != Variant.Type.Dictionary) throw new InvalidOperationException("雾隐幽谷地图节点数据无效。");
            var node = value.AsGodotDictionary();
            var id = node.GetValueOrDefault("id", "").AsString();
            var grid = new Vector2I(node.GetValueOrDefault("x", -1).AsInt32(), node.GetValueOrDefault("y", -1).AsInt32());
            if (id != node_id(grid.X, grid.Y) || grid.X < 0 || grid.X >= MapColumns || grid.Y < 0 || grid.Y >= MapRows ||
                !byId.TryAdd(id, grid) || !byGrid.Add(grid) || node.GetValueOrDefault("terrain", -1).AsInt32() is < 0 or > 7)
                throw new InvalidOperationException("雾隐幽谷地图含有重复、越界或非法节点。");
        }
        var adjacency = byId.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in edges)
        {
            var edge = value.AsGodotDictionary();
            var from = edge.GetValueOrDefault("from", "").AsString();
            var to = edge.GetValueOrDefault("to", "").AsString();
            if (!byId.TryGetValue(from, out var a) || !byId.TryGetValue(to, out var b) ||
                Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1 || !seenEdges.Add(edge_key(from, to)))
                throw new InvalidOperationException("雾隐幽谷路线含有非正交、重复或未知连接。");
            adjacency[from].Add(to);
            adjacency[to].Add(from);
        }
        if (!byId.ContainsKey(EntryNodeId) || edges.Count < MapCellCount - 1)
            throw new InvalidOperationException("雾隐幽谷入口或连通路线缺失。");
        var visited = new HashSet<string>(StringComparer.Ordinal) { EntryNodeId };
        var queue = new Queue<string>(); queue.Enqueue(EntryNodeId);
        while (queue.Count > 0)
            foreach (var next in adjacency[queue.Dequeue()])
                if (visited.Add(next)) queue.Enqueue(next);
        if (visited.Count != MapCellCount) throw new InvalidOperationException("雾隐幽谷地图不能从出口连通全部128格。");
    }

    private static string node_id(int x, int y) => "valley_" + x.ToString("D2") + "_" + y.ToString("D2");
    private static string edge_key(string from, string to) => string.CompareOrdinal(from, to) <= 0 ? from + "|" + to : to + "|" + from;
}
