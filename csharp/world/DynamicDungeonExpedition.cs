using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>Persisted lifecycle, layout, actions and ecology for randomly generated field towers.</summary>
public static class DynamicDungeonExpedition
{
    public const string TemplateId = "mist_hidden_valley";
    public const string DisplayName = "雾隐幽谷";
    public const string DungeonIdPrefix = "mist_hidden_valley:";
    public const int TowerLayerCount = 15;
    public const int RoguelikeLayerCount = 30;
    public const int MapColumns = 7;
    public const int MapRows = TowerLayerCount + 2;
    public const int MinimumNodesPerLayer = 3;
    public const int MaximumNodesPerLayer = 4;
    public const string EntryNodeId = "tower_entry";
    public const string ExitNodeId = "tower_exit";
    public const string BattleNodeType = "battle";
    public const string ResourceNodeType = "resource";
    public const string RestNodeType = "rest";
    public const string BossNodeType = "boss";
    public const int ScheduleIntervalHours = 180 * 24;
    public const int OpenDurationHours = 360 * 24;
    public const int MaximumOpenInstances = 2;
    private const int LegacyMapColumns = 16;
    private const int LegacyMapRows = 8;
    private const int LegacyMapCellCount = LegacyMapColumns * LegacyMapRows;
    private const int HoursPerDay = 24;
    private const int OrdinaryBeastCheckHours = 14 * HoursPerDay;
    private const string LegacyEntryNodeId = "valley_00_04";
    private const string TowerLayoutVersion = "tower_15x7_v1";
    private const string RoguelikeLayoutVersion = "rogue_30x7_v4";
    private const string PreviousRoguelikeLayoutVersion = "rogue_30x7_v3";
    private const string OlderRoguelikeLayoutVersion = "rogue_30x7_v2";
    private const string OldestRoguelikeLayoutVersion = "rogue_30x7_v1";
    private const string LegacyRoguelikeLayoutVersion = "rogue_15x7_v1";
    private const string ItemRoot = "res://resources/entities/items/";
    public static readonly int[] RoguelikeShopLayers = [4, 10, 16, 22, 28];
    private static readonly Vector2I[] Directions = [Vector2I.Right, Vector2I.Down, Vector2I.Left, Vector2I.Up];
    private static readonly System.Collections.Generic.Dictionary<string, LayoutRuntime> LayoutCache = new(StringComparer.Ordinal);

    private sealed class LayoutRuntime
    {
        public required EastWoodsDungeonRules.NodeDefinition[] Nodes;
        public required System.Collections.Generic.Dictionary<string, EastWoodsDungeonRules.NodeDefinition> NodesById;
        public required System.Collections.Generic.Dictionary<string, List<string>> Neighbors;
    }

    public static string RoguelikeMaterialGroupForLayer(int layer) => layer switch
    {
        2 or 14 or 26 => "herb",
        5 or 17 or 29 => "fungus",
        8 or 20 => "wood",
        11 or 23 => "mine",
        _ => "resource",
    };

    public static string RoguelikeMaterialLabelForLayer(int layer) => RoguelikeMaterialGroupForLayer(layer) switch
    {
        "herb" => "药圃",
        "fungus" => "菌泽",
        "wood" => "林木",
        "mine" => "矿脉",
        _ => "材料",
    };

    public static int RoguelikeMaterialYieldForLayer(int layer) => RoguelikeMaterialGroupForLayer(layer) switch
    {
        "herb" => 3,
        "fungus" or "wood" or "mine" => 2,
        _ => 1,
    };

    public static (float AttributeMultiplier, float HealthMultiplier) RoguelikeBattleScaling(int layer)
        => layer switch
        {
            <= 6 => (1.10f, 1.25f),
            <= 12 => (1.25f, 1.55f),
            <= 18 => (1.40f, 1.90f),
            <= 24 => (1.55f, 2.30f),
            < RoguelikeLayerCount => (1.75f, 2.75f),
            _ => (2.00f, 3.50f),
        };

    private static string RoguelikeAnchorKind(int layer) => layer switch
    {
        2 or 5 or 8 or 11 or 14 or 17 or 20 or 23 or 26 or 29 => "material",
        3 or 6 or 9 or 12 or 15 or 18 or 21 or 24 or 27 => "cultivation",
        1 or 13 or 25 => "manual",
        7 or 19 => "alchemy",
        4 or 16 => "beast_nest",
        10 or 22 => "forging",
        _ => "",
    };

    public static int RoguelikeLayerForNodeId(string nodeId)
    {
        var parts = nodeId.Split('_');
        return parts.Length > 1 && int.TryParse(parts[1], out var layer) ? layer : 0;
    }

    public static string RoguelikeRealmName(int tier) => tier is >= 1 and <= 10
        ? "云息" + new[] { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" }[tier] + "层"
        : "";

    private static string[] RoguelikeMaterialPool(int layer) => RoguelikeMaterialGroupForLayer(layer) switch
    {
        "herb" => ["苦苔根", "月露苔", "雾铃花"],
        "fungus" => ["云雾菌", "砾地菌伞", "雾心菌核", "月露菌汁"],
        "wood" => ["青木芯", "赤瓤木核", "凝露松脂", "松心炭", "霜叶枝", "风络藤丝"],
        "mine" => ["灵石", "赤纹燧石", "地脉焰炭", "风纹石英", "裂甲石", "褐铜砂", "铜髓矿", "玄铁", "盐纹石膏", "泥脉石团", "紫砂灵土", "瘴泥珠", "砂脉石片", "风窍晶屑", "沉云玉屑", "清音玉片"],
        _ => System.Array.Empty<string>(),
    };

    public static GDictionary CreateEmpty(int currentGameHour)
        => new()
        {
            ["schema_version"] = 1,
            ["activated"] = false,
            ["last_schedule_hour"] = Math.Max(0, currentGameHour),
            ["next_instance_sequence"] = 1,
            ["world_seed"] = Guid.NewGuid().ToString("N"),
            ["instances"] = new GArray(),
        };

    /// <summary>Creates one persistent world-map entrance and replenishes it only after completion.</summary>
    public static bool SynchronizeTowerOpportunities(GDictionary state, int currentGameHour)
    {
        var changed = EnsureState(state, currentGameHour);
        var now = Math.Max(0, currentGameHour);
        var instances = state["instances"].AsGodotArray();
        foreach (var value in instances)
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var existingInstance = value.AsGodotDictionary();
            if (IsCompactInstance(existingInstance))
            {
                changed |= SynchronizeEcology(existingInstance, now);
                continue;
            }
            if (!existingInstance.GetValueOrDefault("entry_open", false).AsBool()) continue;
            changed |= SynchronizeEcology(existingInstance, now);
            if (!IsTowerInstance(existingInstance) && !existingInstance.GetValueOrDefault("inside", false).AsBool())
            {
                // Retire pre-tower opportunities on upgraded saves. An expedition
                // already in progress is allowed to finish before a new tower appears.
                existingInstance["entry_open"] = false;
                existingInstance["completed"] = true;
                changed = true;
            }
        }

        if (instances.Any(value => value.VariantType == Variant.Type.Dictionary &&
                !IsCompactInstance(value.AsGodotDictionary()) &&
                value.AsGodotDictionary().GetValueOrDefault("entry_open", false).AsBool() &&
                !value.AsGodotDictionary().GetValueOrDefault("completed", false).AsBool()))
            return changed;

        // An active expedition survives map reloads. A new one appears only after
        // the previous tower has been completed at its exit.
        if (instances.Any(value => value.VariantType == Variant.Type.Dictionary &&
                !IsCompactInstance(value.AsGodotDictionary()) &&
                value.AsGodotDictionary().GetValueOrDefault("inside", false).AsBool()))
            return changed;

        var sequence = Math.Max(1, state.GetValueOrDefault("next_instance_sequence", 1).AsInt32());
        var worldSeed = state.GetValueOrDefault("world_seed", "").AsString();
        if (string.IsNullOrWhiteSpace(worldSeed))
        {
            worldSeed = Guid.NewGuid().ToString("N");
            state["world_seed"] = worldSeed;
            changed = true;
        }
        var id = "tower_" + sequence.ToString("D6");
        var seed = worldSeed + "|" + id + "|" + now;
        var instance = CreateInstance(id, seed, Vector2I.Zero, now);
        instance["expires_hour"] = int.MaxValue;
        instance["world_position"] = new Vector2(-1, -1);
        instance["world_node_id"] = GetWorldNodeId(instance);
        instances.Add(instance);
        state["next_instance_sequence"] = sequence + 1;
        state["activated"] = true;
        state["last_schedule_hour"] = now;
        return true;
    }

    public static string GetWorldNodeId(GDictionary instance)
        => "tower_entrance_" + instance.GetValueOrDefault("instance_id", "unknown").AsString();

    public static GDictionary? FindInstanceByWorldNodeId(GDictionary state, string nodeId)
    {
        foreach (var value in state.GetValueOrDefault("instances", new GArray()).AsGodotArray())
            if (value.VariantType == Variant.Type.Dictionary && GetWorldNodeId(value.AsGodotDictionary()) == nodeId)
                return value.AsGodotDictionary();
        return null;
    }

    public static bool IsTowerInstance(GDictionary instance)
        => !IsCompactInstance(instance) && instance.GetValueOrDefault("template_id", "").AsString() == TemplateId &&
           instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary().GetValueOrDefault("layout_version", "").AsString() is TowerLayoutVersion or RoguelikeLayoutVersion or PreviousRoguelikeLayoutVersion or OlderRoguelikeLayoutVersion or OldestRoguelikeLayoutVersion or LegacyRoguelikeLayoutVersion;

    public static bool IsRoguelikeInstance(GDictionary instance)
        => instance.GetValueOrDefault("roguelike_run", false).AsBool() &&
           instance.GetValueOrDefault("template_id", "").AsString() == TemplateId &&
           instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary().GetValueOrDefault("layout_version", "").AsString() is RoguelikeLayoutVersion or PreviousRoguelikeLayoutVersion or OlderRoguelikeLayoutVersion or OldestRoguelikeLayoutVersion or LegacyRoguelikeLayoutVersion;

    public static GDictionary CreateRoguelikeInstance(string id, string seed, int generatedHour, string archetype)
    {
        var instance = CreateInstance(id, seed, Vector2I.Zero, generatedHour, roguelike: true);
        instance["roguelike_run"] = true;
        instance["archetype"] = archetype;
        instance["inside"] = true;
        instance["entry_open"] = false;
        instance["route"].AsGodotDictionary()["inside"] = true;
        return instance;
    }

    public static string GetNodeType(EastWoodsDungeonRules.NodeDefinition? node)
        => node?.ContentType ?? "";

    public static float MovementStaminaCost(GDictionary instance, string nodeId)
        => IsRoguelikeInstance(instance) ? 0.0f : GetNode(instance, nodeId)?.ContentType switch
        {
            BattleNodeType or BossNodeType => 3.0f,
            ResourceNodeType => 2.0f,
            RestNodeType => 0.5f,
            "alchemy" or "forging" or "manual" or "cultivation" => 2.0f,
            "shop" => 0.0f,
            "exit" => 1.0f,
            _ => 0.0f,
        };

    public static string ExpectedRealmRange(GDictionary instance)
    {
        var labels = new List<string>();
        foreach (var value in instance.GetValueOrDefault("beasts", new GArray()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            foreach (var creatureId in GetEncounterCreatureIds(instance, record.GetValueOrDefault("encounter_id", "").AsString()))
            {
                var path = creatureId switch
                {
                    "gray_back_boar" => "res://resources/beast_templates/灰背野猪_试玩.tres",
                    "forest_bei" => "res://resources/beast_templates/林地妖狈.tres",
                    "qing_tail_fox" => "res://resources/beast_templates/青尾狐.tres",
                    "red_mane_wolf" => "res://resources/beast_templates/赤鬃狼.tres",
                    "rock_back_badger" => "res://resources/beast_templates/岩背獾.tres",
                    _ => "res://resources/beast_templates/林地凶狼.tres",
                };
                if (GD.Load<BeastTemplate>(path) is not { } template) continue;
                labels.Add(string.IsNullOrWhiteSpace(template.cultivation_stage)
                    ? template.cultivation_realm
                    : template.cultivation_realm + template.cultivation_stage);
            }
        }
        var ordered = labels.Distinct(StringComparer.Ordinal).OrderBy(RealmOrder).ThenBy(label => label, StringComparer.Ordinal).ToArray();
        return ordered.Length switch
        {
            0 => "未详",
            1 => ordered[0],
            _ => ordered[0] + "—" + ordered[^1],
        };
    }

    private static int RealmOrder(string label)
    {
        var realm = label.StartsWith("凡", StringComparison.Ordinal) ? 0 :
            label.StartsWith("通智", StringComparison.Ordinal) ? 1 :
            label.StartsWith("结丹", StringComparison.Ordinal) ? 2 : 3;
        var stage = label.EndsWith("初期", StringComparison.Ordinal) ? 0 :
            label.EndsWith("中期", StringComparison.Ordinal) ? 1 :
            label.EndsWith("后期", StringComparison.Ordinal) ? 2 : 0;
        return realm * 10 + stage;
    }

    public static bool IsCompactInstance(GDictionary instance)
        => instance.GetValueOrDefault("template_id", "").AsString() == CompactDungeonExpedition.TemplateId;

    public static string GetDisplayName(GDictionary instance)
        => IsCompactInstance(instance) ? CompactDungeonExpedition.DisplayName :
           IsRoguelikeInstance(instance) ? "修行秘境" : DisplayName;

    public static bool IsExitNode(GDictionary instance, string nodeId)
        => nodeId == GetExitNodeId(instance) || IsCompactInstance(instance) && nodeId == GetEntryNodeId(instance);

    public static GDictionary EnsureCompactInstance(GDictionary state, Vector2I worldCell, int currentGameHour)
    {
        EnsureState(state, currentGameHour);
        var existing = FindInstance(state, CompactDungeonExpedition.InstanceId);
        if (existing is not null)
        {
            if (!IsCompactInstance(existing)) throw new InvalidOperationException("蕨林采径的实例标识被其他模板占用。");
            ValidateLayout(existing["map_snapshot"].AsGodotDictionary());
            MigrateCompactEncounterContent(existing);
            SynchronizeInstanceEcology(existing, currentGameHour);
            return existing;
        }
        var instance = CreateInstance(CompactDungeonExpedition.InstanceId, CompactDungeonExpedition.TemplateId + "|v1", worldCell, Math.Max(0, currentGameHour), true);
        state["instances"].AsGodotArray().Add(instance);
        return instance;
    }

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
            if (IsCompactInstance(instance))
            {
                changed |= SynchronizeEcology(instance, hour);
                continue;
            }
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
                if (!IsCompactInstance(instance) && instance.GetValueOrDefault("entry_open", false).AsBool())
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

    private static GDictionary CreateInstance(string id, string seed, Vector2I worldCell, int generatedHour, bool compact = false, bool roguelike = false)
    {
        var layout = compact ? CompactDungeonExpedition.GenerateLayout(seed) : GenerateLayout(seed, roguelike);
        var entry = compact ? CompactDungeonExpedition.EntryNodeId : EntryNodeId;
        var instance = new GDictionary
        {
            ["instance_id"] = id,
            ["template_id"] = compact ? CompactDungeonExpedition.TemplateId : TemplateId,
            ["seed"] = seed,
            ["world_cell"] = worldCell,
            ["generated_hour"] = generatedHour,
            ["expires_hour"] = compact ? int.MaxValue : generatedHour + OpenDurationHours,
            ["entry_open"] = true,
            ["inside"] = roguelike,
            ["map_snapshot"] = layout,
            ["resource_cursors"] = new GDictionary(),
            ["beast_cursor_hour"] = generatedHour,
            ["beast_sequence"] = 1,
            ["beasts"] = new GArray(),
            ["event_results"] = new GDictionary(),
            ["route"] = new GDictionary
            {
                ["current"] = entry,
                ["visited"] = new GArray { entry },
                ["discovered"] = new GArray { entry },
                ["actions"] = new GDictionary(),
                ["inside"] = roguelike,
            },
        };
        foreach (var node in GetNodes(instance))
            if (GetResourceAction(node) is { OrdinaryBeastSource: true } action)
                instance["beasts"].AsGodotArray().Add(NewBeastRecord(instance, node.Id, action, generatedHour));
        return instance;
    }

    private static void MigrateCompactEncounterContent(GDictionary instance)
    {
        var authored = CompactDungeonExpedition.GenerateLayout(instance.GetValueOrDefault("seed", "").AsString());
        var currentNodes = instance["map_snapshot"].AsGodotDictionary()["nodes"].AsGodotArray();
        var authoredNodes = authored["nodes"].AsGodotArray();
        for (var index = 0; index < Math.Min(currentNodes.Count, authoredNodes.Count); index++)
        {
            var current = currentNodes[index].AsGodotDictionary();
            var next = authoredNodes[index].AsGodotDictionary();
            current["title"] = next.GetValueOrDefault("title", "");
            current["description"] = next.GetValueOrDefault("description", "");
        }

        var route = GetRoute(instance);
        var actions = route.GetValueOrDefault("actions", new GDictionary()).AsGodotDictionary();
        foreach (var value in instance.GetValueOrDefault("beasts", new GArray()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            var nodeId = record.GetValueOrDefault("node", "").AsString();
            var actionId = record.GetValueOrDefault("action_id", "").AsString();
            var desiredCreatureId = CompactDungeonExpedition.CreatureIdForNode(nodeId);
            if (desiredCreatureId.Length == 0 || record.GetValueOrDefault("status", "active").AsString() != "active" ||
                actions.GetValueOrDefault(nodeId + ":" + actionId, 0).AsInt32() > 0) continue;
            record["creature_id"] = desiredCreatureId;
        }
    }

    private static GDictionary NewBeastRecord(GDictionary instance, string nodeId, EastWoodsActions.ActionDef source, int hour)
    {
        var sequence = Math.Max(1, instance.GetValueOrDefault("beast_sequence", 1).AsInt32());
        instance["beast_sequence"] = sequence + 1;
        var identity = instance.GetValueOrDefault("instance_id", "unknown").AsString() + "_beast_" + sequence.ToString("D4");
        var creatureId = string.IsNullOrWhiteSpace(source.CreatureId) ? "forest_wolf" : source.CreatureId;
        var creatureCount = creatureId switch
        {
            "qing_tail_fox" or "red_mane_wolf" or "rock_back_badger" => 2 + (int)(StableSeed(instance.GetValueOrDefault("seed", "").AsString() + "|" + identity + "|pack") % 2),
            "forest_bei_pack" => 2,
            _ => 1,
        };
        return new GDictionary
        {
            ["encounter_id"] = identity,
            ["action_id"] = source.Id == "rogue_boss" ? "rogue_boss" : "beast_" + sequence.ToString("D4"),
            ["node"] = nodeId,
            ["creature_id"] = creatureId,
            ["creature_count"] = creatureCount,
            ["created_hour"] = hour,
            ["resolved_hour"] = -1,
            ["status"] = "active",
        };
    }

    public static string[] GetEncounterCreatureIds(GDictionary instance, string encounterIdentity)
    {
        foreach (var value in instance.GetValueOrDefault("beasts", new GArray()).AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary) continue;
            var record = value.AsGodotDictionary();
            if (record.GetValueOrDefault("encounter_id", "").AsString() != encounterIdentity) continue;
            var creatureId = record.GetValueOrDefault("creature_id", "forest_wolf").AsString();
            return creatureId switch
            {
                "forest_bei_pack" => ["forest_bei", "forest_wolf"],
                "qing_tail_fox" or "red_mane_wolf" or "rock_back_badger" => Enumerable.Repeat(
                    creatureId, Math.Clamp(record.GetValueOrDefault("creature_count", 2).AsInt32(), 2, 3)).ToArray(),
                "forest_wolf" or "gray_back_boar" or "forest_bei" => [creatureId],
                _ => ["forest_wolf"],
            };
        }
        return [];
    }

    private static GDictionary GenerateLayout(string seed, bool roguelike = false)
    {
        var layerCount = roguelike ? RoguelikeLayerCount : TowerLayerCount;
        var nodes = new GArray();
        var edges = new GArray();
        var rng = new RandomNumberGenerator { Seed = StableSeed(seed) };
        var coreColumns = new List<int>[layerCount + 1];
        var startColumns = new[] { 2, 3, 4 };
        var exitColumns = new[] { 2, 3, 4 };
        coreColumns[1] = [.. startColumns];
        for (var layer = 2; layer <= layerCount; layer++)
        {
            var previous = coreColumns[layer - 1];
            var remainingSteps = layerCount - layer;
            var candidates = new List<int[]>();
            for (var first = Math.Max(0, previous[0] - 1); first <= Math.Min(MapColumns - 1, previous[0] + 1); first++)
            for (var second = Math.Max(0, previous[1] - 1); second <= Math.Min(MapColumns - 1, previous[1] + 1); second++)
            for (var third = Math.Max(0, previous[2] - 1); third <= Math.Min(MapColumns - 1, previous[2] + 1); third++)
            {
                var next = new[] { first, second, third };
                if (first >= second || second >= third || next.Where((column, index) => Math.Abs(column - exitColumns[index]) > remainingSteps).Any()) continue;
                candidates.Add(next);
            }
            if (candidates.Count == 0) throw new InvalidOperationException("爬塔主干无法保持通往出口的连接。");
            coreColumns[layer] = [.. candidates[(int)rng.RandiRange(0, candidates.Count - 1)]];
        }

        var columnsByLayer = new List<int>[layerCount + 1];
        for (var layer = 1; layer <= layerCount; layer++)
        {
            columnsByLayer[layer] = [.. coreColumns[layer]];
            IReadOnlyList<int> previous = layer == 1 ? new[] { 3 } : coreColumns[layer - 1];
            IReadOnlyList<int> next = layer == layerCount ? new[] { 3 } : coreColumns[layer + 1];
            var branches = Enumerable.Range(0, MapColumns)
                .Where(column => !columnsByLayer[layer].Contains(column) &&
                    previous.Any(parent => Math.Abs(parent - column) <= 1) &&
                    next.Any(child => Math.Abs(child - column) <= 1))
                .ToList();
            if (branches.Count > 0 && rng.RandiRange(0, 99) < 52)
                columnsByLayer[layer].Add(branches[(int)rng.RandiRange(0, branches.Count - 1)]);
            columnsByLayer[layer].Sort();
        }

        add_node(EntryNodeId, "入口", "副本入口。每次只沿一条连接进入下一层。", 3, layerCount + 1, "entry", "", rng.RandiRange(0, 7));
        var typeCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        var resourceIds = new[] { "common_herb", "common_moss", "mistbell_slow", "spirit_stone_vein" };
        var creatureIds = new[] { "forest_wolf", "gray_back_boar", "forest_bei_pack", "qing_tail_fox", "red_mane_wolf", "rock_back_badger" };
        var anchorColumns = new System.Collections.Generic.Dictionary<int, int>();
        var shopColumns = new System.Collections.Generic.Dictionary<int, int>();
        if (roguelike)
        for (var layer = 1; layer < layerCount; layer++)
        {
            if (RoguelikeAnchorKind(layer).Length == 0 || RoguelikeAnchorKind(layer) == "cultivation") continue;
            var layerColumns = columnsByLayer[layer];
            anchorColumns[layer] = layerColumns[(int)rng.RandiRange(0, layerColumns.Count - 1)];
        }
        if (roguelike)
        foreach (var layer in RoguelikeShopLayers)
        {
            var candidates = coreColumns[layer].Where(column => !anchorColumns.TryGetValue(layer, out var anchor) || column != anchor).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException($"肉鸽第{layer}层无法为商栈保留独立节点。");
            shopColumns[layer] = candidates[(int)rng.RandiRange(0, candidates.Length - 1)];
        }
        for (var layer = 1; layer <= layerCount; layer++)
        foreach (var column in columnsByLayer[layer])
        {
            var type = (int)rng.RandiRange(0, 14) switch
            {
                < 5 => BattleNodeType,
                < 11 => ResourceNodeType,
                _ => RestNodeType,
            };
            var resourceId = type == ResourceNodeType ? resourceIds[(int)rng.RandiRange(0, resourceIds.Length - 1)] : "";
            var creatureId = type == BattleNodeType ? creatureIds[(int)rng.RandiRange(0, creatureIds.Length - 1)] : "";
            var feature = "";
            if (roguelike && layer == layerCount)
            {
                type = BossNodeType;
                creatureId = "forest_wolf";
            }
            else if (roguelike && RoguelikeAnchorKind(layer) == "cultivation")
            {
                type = "cultivation";
                var pathIndex = columnsByLayer[layer].IndexOf(column) % 3;
                feature = pathIndex switch
                {
                    0 => "cultivation_physique",
                    1 => "cultivation_intelligence",
                    _ => "cultivation_soul",
                };
            }
            else if (roguelike && RoguelikeAnchorKind(layer) == "manual")
            {
                // Quiet-room rows are fixed checkpoints so book progression cannot be skipped by route choice.
                type = "manual";
            }
            else if (roguelike && anchorColumns.GetValueOrDefault(layer, -1) == column)
            {
                var anchorKind = RoguelikeAnchorKind(layer);
                switch (anchorKind)
                {
                    case "material":
                        var pool = RoguelikeMaterialPool(layer);
                        type = ResourceNodeType;
                        resourceId = "rogue_material_" + pool[(int)rng.RandiRange(0, pool.Length - 1)];
                        break;
                    case "alchemy":
                    case "forging":
                    case "manual":
                    case "cultivation":
                        type = anchorKind;
                        break;
                    case "beast_nest":
                        type = BattleNodeType;
                        creatureId = new[] { "forest_wolf", "gray_back_boar", "qing_tail_fox", "red_mane_wolf", "rock_back_badger" }[(int)rng.RandiRange(0, 4)];
                        feature = "beast_nest";
                        break;
                }
            }
            else if (roguelike && shopColumns.GetValueOrDefault(layer, -1) == column)
            {
                type = "shop";
            }
            // Overrides must not retain the random node's unrelated resource or encounter.
            if (type != ResourceNodeType) resourceId = "";
            if (type is not (BattleNodeType or BossNodeType)) creatureId = "";
            typeCounts[type] = typeCounts.GetValueOrDefault(type) + 1;
            var terrain = (int)rng.RandiRange(0, 7);
            var id = tower_node_id(layer, column);
            var title = type switch
            {
                BattleNodeType => "战斗",
                ResourceNodeType => "资源",
                "alchemy" => "炼丹工坊",
                "forging" => "炼器工坊",
                "manual" => "秘境静室",
                "shop" => "秘境商栈",
                "cultivation" => feature switch
                {
                    "cultivation_physique" => "淬体周天",
                    "cultivation_intelligence" => "蕴灵周天",
                    "cultivation_soul" => "凝神周天",
                    _ => "修炼静室",
                },
                BossNodeType => "首领",
                _ => "休整",
            };
            if (resourceId.StartsWith("rogue_material_", StringComparison.Ordinal))
                title = RoguelikeMaterialLabelForLayer(layer);
            if (type == BattleNodeType && feature == "beast_nest") title = "兽巢";
            var description = type switch
            {
                _ when feature == "beast_nest" => roguelike ? "兽巢附近有妖兽守护。抵达后直接进入战斗。" : "兽巢附近有妖兽守护，可查看战前情报后决定是否应战。",
                BattleNodeType => roguelike ? "妖兽活动的节点。抵达后直接进入战斗。" : "一处生灵活动地。抵达后可查看战前信息，再决定是否应战。",
                ResourceNodeType => roguelike ? "一处资源点。抵达后按现有采集规则直接收取。" : "一处已确定采集内容的资源点。可按现有行动规则采集。",
                "alchemy" => "遗留的丹房仍可使用。进入后沿用现有炼丹流程。",
                "forging" => "遗留的器作仍可使用。进入后沿用现有炼器流程。",
                "manual" => "静室可分别修习周天与研读秘籍；两条进度各自记载。",
                "shop" => "行商收购本局材料，也出售丹药与法器。",
                "cultivation" => "本局修行关隘。抵达后自动晋一层，并按所选周天提升一项战斗属性。",
                BossNodeType => "出口前最后的强敌。击败它后才能继续前往出口。",
                _ => "可在此休息；恢复量按通用休息规则结算。",
            };
            add_node(id, title, description, column, layerCount - layer + 1, type, resourceId, terrain, creatureId, feature);
        }
        add_node(ExitNodeId, "出口", "抵达出口后可离开副本；不附加额外奖励。", 3, 0, "exit", "", rng.RandiRange(0, 7));

        for (var column = 0; column < MapColumns; column++)
            if (Math.Abs(column - 3) <= 1 && columnsByLayer[1].Contains(column))
                edges.Add(new GDictionary { ["from"] = EntryNodeId, ["to"] = tower_node_id(1, column) });
        for (var layer = 1; layer < layerCount; layer++)
        foreach (var fromColumn in columnsByLayer[layer])
        foreach (var toColumn in columnsByLayer[layer + 1])
            if (Math.Abs(fromColumn - toColumn) <= 1)
                edges.Add(new GDictionary { ["from"] = tower_node_id(layer, fromColumn), ["to"] = tower_node_id(layer + 1, toColumn) });
        foreach (var column in columnsByLayer[layerCount])
            if (Math.Abs(column - 3) <= 1)
                edges.Add(new GDictionary { ["from"] = tower_node_id(layerCount, column), ["to"] = ExitNodeId });

        var snapshot = new GDictionary
        {
            ["template_id"] = TemplateId,
            ["layout_version"] = roguelike ? RoguelikeLayoutVersion : TowerLayoutVersion,
            ["seed"] = seed,
            ["seed_hash"] = (long)(StableSeed(seed) & long.MaxValue),
            ["version"] = 2,
            ["columns"] = MapColumns,
            ["rows"] = layerCount + 2,
            ["layers"] = layerCount,
            ["minimum_nodes_per_layer"] = MinimumNodesPerLayer,
            ["maximum_nodes_per_layer"] = MaximumNodesPerLayer,
            ["type_counts"] = new GDictionary { [BattleNodeType] = typeCounts.GetValueOrDefault(BattleNodeType), [ResourceNodeType] = typeCounts.GetValueOrDefault(ResourceNodeType), [RestNodeType] = typeCounts.GetValueOrDefault(RestNodeType) },
            ["entry_node_id"] = EntryNodeId,
            ["exit_node_id"] = ExitNodeId,
            ["nodes"] = nodes,
            ["edges"] = edges,
        };
        ValidateLayout(snapshot);
        return snapshot;

        void add_node(string id, string title, string description, int x, int y, string type, string resourceId, int terrain, string creatureId = "", string feature = "")
        {
            var category = resourceId switch
            {
                "common_herb" or "common_moss" or "mistbell_slow" => EastWoodsResourceCategory.Plant,
                "spirit_stone_vein" => EastWoodsResourceCategory.Mineral,
                _ => EastWoodsResourceCategory.None,
            };
            nodes.Add(new GDictionary
            {
                ["id"] = id, ["title"] = title, ["description"] = description,
                ["x"] = x, ["y"] = y, ["show_title"] = true,
                ["terrain"] = terrain, ["feature"] = feature,
                ["resource_id"] = resourceId, ["resource_category"] = (int)category,
                ["content_type"] = type, ["creature_id"] = creatureId,
            });
        }
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
        if (IsRoguelikeInstance(instance))
        {
            // Older snapshots could leave a beast nest without its encounter when
            // its node retained a resource ID from the initial random roll.
            foreach (var node in GetNodes(instance).Where(node => node.Feature == "beast_nest"))
            {
                if (beasts.Any(value => value.VariantType == Variant.Type.Dictionary &&
                        value.AsGodotDictionary().GetValueOrDefault("node", "").AsString() == node.Id)) continue;
                if (GetBeastSource(node) is not { } source) continue;
                beasts.Add(NewBeastRecord(instance, node.Id, source, generatedHour));
                changed = true;
            }
        }
        var pendingBeasts = instance.GetValueOrDefault("pending_beast_nodes", new GDictionary()).AsGodotDictionary();
        var occupiedNode = IsCompactInstance(instance) && route.GetValueOrDefault("inside", false).AsBool()
            ? route.GetValueOrDefault("current", "").AsString() : "";
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
                if (IsCompactInstance(instance) && node.Id == occupiedNode)
                {
                    pendingBeasts[node.Id] = true;
                    changed = true;
                    continue;
                }
                beasts.Add(NewBeastRecord(instance, node.Id, source, beastCursor));
                pendingBeasts.Remove(node.Id);
                changed = true;
            }
        }
        if (IsCompactInstance(instance))
        {
            foreach (var key in pendingBeasts.Keys.ToArray())
            {
                var nodeId = key.AsString();
                if (nodeId == occupiedNode) continue;
                var node = GetNode(instance, nodeId);
                var source = node is null ? null : GetBeastSource(node);
                var active = beasts.Any(value => value.VariantType == Variant.Type.Dictionary &&
                    value.AsGodotDictionary().GetValueOrDefault("node", "").AsString() == nodeId &&
                    value.AsGodotDictionary().GetValueOrDefault("status", "active").AsString() == "active" &&
                    actions.GetValueOrDefault(nodeId + ":" + value.AsGodotDictionary().GetValueOrDefault("action_id", "").AsString(), 0).AsInt32() <= 0);
                if (source is not null && !active) beasts.Add(NewBeastRecord(instance, nodeId, source, limit));
                pendingBeasts.Remove(key);
                changed = true;
            }
            instance["pending_beast_nodes"] = pendingBeasts;
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
    {
        if (node.Feature == "beast_nest" && node.ContentType == BattleNodeType)
            return GetBeastSource(node);
        if (node.ResourceNodeId.StartsWith("rogue_material_", StringComparison.Ordinal))
        {
            var itemName = node.ResourceNodeId["rogue_material_".Length..];
            var layerParts = node.Id.Split('_');
            var layer = layerParts.Length > 1 && int.TryParse(layerParts[1], out var parsedLayer) ? parsedLayer : 0;
            var group = RoguelikeMaterialGroupForLayer(layer);
            var quantity = RoguelikeMaterialYieldForLayer(layer);
            var recovery = group switch
            {
                "mine" => EastWoodsActions.ResourceRecoveryGroup.Mineral,
                "fungus" or "wood" => EastWoodsActions.ResourceRecoveryGroup.SlowGrowth,
                _ => EastWoodsActions.ResourceRecoveryGroup.Common,
            };
            return new("gather_rogue_material", group == "mine" ? "采掘" : "采集",
                $"采集{itemName} ×{quantity}。每次耗时1小时、精力2；储量有限。", 1, 2,
                $"你收好{itemName} ×{quantity}。", ItemRoot + itemName + ".tres", quantity, quantity * 2,
                Recovery: recovery);
        }
        if (node.ContentType == "alchemy")
            return new("rogue_alchemy", "炼丹", "进入丹房，沿用现有炼丹流程。", 0, 0, "", Stock: int.MaxValue);
        if (node.ContentType == "forging")
            return new("rogue_forging", "炼器", "进入器作，沿用现有炼器流程。", 0, 0, "", Stock: int.MaxValue);
        if (node.ContentType == "cultivation")
        {
            var path = node.Feature switch
            {
                "cultivation_intelligence" => "intelligence",
                "cultivation_soul" => "soul",
                _ => "physique",
            };
            var title = path switch { "intelligence" => "蕴灵周天", "soul" => "凝神周天", _ => "淬体周天" };
            var attribute = path switch { "intelligence" => "灵慧", "soul" => "神魂", _ => "体魄" };
            return new("rogue_cultivation_" + path, title,
                $"抵达后自动晋升一层云息境界，并使{attribute}根基 +1。", 0, 0,
                "秘境周天已圆满。", Quantity: 1, Stock: 1);
        }
        if (node.ContentType == "manual")
            return new("rogue_meditation", "进入静室", "选择周天修炼与秘籍研读；两条进度各自记载。", 0, 0,
                "静室中，两条修行进度各自推进。", Stock: int.MaxValue);
        if (node.ContentType == "shop")
            return new("rogue_shop", "进入商栈", "出售本局材料，购买丹药或法器。商品与收购额度固定且用尽不补。", 0, 0,
                "你离开了秘境商栈。", Stock: int.MaxValue);
        if (node.ContentType == BossNodeType)
            return new("rogue_boss", "挑战首领", "出口前的首领挡住了前路。战胜后才能前往出口。", 0, 0,
                "首领已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: "forest_wolf");
        return node.ResourceNodeId switch
        {
            "common_herb" => new("gather_root", "采集", "竹根下生着苦苔根。每次可采两份，储量有限；此类植物每7日恢复。", 1, 2,
                "你收好苦苔根 ×2。", ItemRoot + "苦苔根.tres", 2, 4, Recovery: EastWoodsActions.ResourceRecoveryGroup.Common),
            "common_moss" => new("gather_moss", "采集", "背阴处附着月露苔。每次可采一份，储量有限；此类植物每7日恢复。", 1, 2,
                "你收好月露苔 ×1。", ItemRoot + "月露苔.tres", 1, 2, Recovery: EastWoodsActions.ResourceRecoveryGroup.Common),
            "mistbell_slow" => new("gather_mistbell", "采集", "湿润林缘生着少量雾铃花。每次可采一份，按30日慢生周期恢复。", 1, 2,
                "你采下雾铃花 ×1。", ItemRoot + "雾铃花.tres", 1, 2, Recovery: EastWoodsActions.ResourceRecoveryGroup.SlowGrowth),
            "spirit_stone_vein" => new("mine_spirit_stone", "采矿", "岩层裂隙中有一处灵石脉。每次可采一份，按90日矿脉周期恢复。", 1, 2,
                "你采得灵石 ×1。", ItemRoot + "灵石.tres", 1, 2, Recovery: EastWoodsActions.ResourceRecoveryGroup.Mineral),
            _ when node.ContentType == RestNodeType => new("rest_hour", "休息一小时", "休息一小时，按通用休息规则恢复大世界精力。", 1, 0,
                "休息过后，你的精力有所恢复。", Quantity: 1, Stock: 100, Rest: true),
            _ => GetBeastSource(node),
        };
    }

    private static List<EastWoodsActions.ActionDef> GetEventActions(EastWoodsDungeonRules.NodeDefinition node) => node.Id switch
    {
        "valley_02_04" =>
        [
            new("event_bamboo_north", "循竹影向北", "耗时1小时、精力1。你辨出竹林北侧干径，揭开一段山麓路线；不获得额外物品。", 1, 1,
                "北侧干径已记明。", Quantity: 1, Stock: 1),
            new("event_bamboo_south", "沿苔痕向南", "耗时1小时、精力1。你沿湿苔找到竹林南侧的绕行路；不获得额外物品。", 1, 1,
                "南侧绕行路已记明。", Quantity: 1, Stock: 1),
        ],
        "valley_07_04" =>
        [
            new("event_spring_north", "沿泉声辨北岸", "耗时1小时、精力1。你辨出湿地北岸的干地，揭开通往北侧岩隙的路线；不采走泉水。", 1, 1,
                "湿地北岸路线已记明。", Quantity: 1, Stock: 1),
            new("event_spring_south", "沿泉声辨南岸", "耗时1小时、精力1。你辨出湿地南岸的绕行路；不采走泉水。", 1, 1,
                "湿地南岸路线已记明。", Quantity: 1, Stock: 1),
        ],
        "valley_12_04" =>
        [
            new("event_rock_north", "循爪痕探北隙", "耗时1小时、精力1。你辨出岩隙北面的回环路线；爪痕指向的妖兽仍须由你决定是否应战。", 1, 1,
                "北面回环路线已记明。", Quantity: 1, Stock: 1),
            new("event_rock_south", "循碎石探南隙", "耗时1小时、精力1。你辨出岩隙南面的支路；可选择绕开兽迹或之后再来。", 1, 1,
                "南面支路已记明。", Quantity: 1, Stock: 1),
        ],
        _ => [],
    };

    private static EastWoodsActions.ActionDef? GetBeastSource(EastWoodsDungeonRules.NodeDefinition node)
    {
        if (node.ContentType == BossNodeType)
            return new("rogue_boss", "挑战首领", "出口前的首领挡住了前路。战胜后才能前往出口。", 0, 0,
                "首领已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: "forest_wolf");
        var creatureId = !string.IsNullOrWhiteSpace(node.CreatureId) ? node.CreatureId : CompactDungeonExpedition.CreatureIdForNode(node.Id);
        if (creatureId.Length == 0)
            creatureId = node.Id switch
            {
                "valley_11_01" => "qing_tail_fox",
                "valley_12_06" => "red_mane_wolf",
                "valley_14_03" => "rock_back_badger",
                _ => "",
            };
        if (creatureId.Length == 0) return null;
        return creatureId switch
        {
            "gray_back_boar" => new("boar", "挑战灰背野猪", "一头灰背野猪在盘根间拱食草根，已有通智初期修为。可以查看战前情报后决定是否交手。", 0, 0,
                "灰背野猪已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: creatureId),
            "forest_bei_pack" => new("wolf_bei", "挑战狼狈同行", "林地妖狈与一头凶狼一同活动，均有通智初期修为。狈首天赋会令凶狼的群猎本能更容易累积。可以查看战前情报后决定是否交手。", 0, 0,
                "狼狈同行已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: creatureId),
            "qing_tail_fox" => new("qing_tail_fox_pack", "挑战青尾狐群", "两三只青尾狐在松林边缘活动，皆为通智初期。可以查看战前情报后决定是否交手。", 0, 0,
                "青尾狐群已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: creatureId),
            "red_mane_wolf" => new("red_mane_wolf_pack", "挑战赤鬃狼群", "两三只赤鬃狼沿兽径追踪猎物，皆为通智初期。可以查看战前情报后决定是否交手。", 0, 0,
                "赤鬃狼群已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: creatureId),
            "rock_back_badger" => new("rock_back_badger_pack", "挑战岩背獾群", "两三只岩背獾在碎石坡下掘洞，皆为通智初期。可以查看战前情报后决定是否交手。", 0, 0,
                "岩背獾群已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: creatureId),
            _ => new("wolf", "挑战林地凶狼", "一头林地凶狼在林径间活动，已有通智初期修为。可以查看战前情报后决定是否交手。", 0, 0,
                "林地凶狼已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1, CreatureId: creatureId),
        };
    }

    public static List<EastWoodsActions.ActionDef> AvailableActions(GDictionary instance, string nodeId, GDictionary actions)
    {
        var result = new List<EastWoodsActions.ActionDef>();
        var node = GetNode(instance, nodeId);
        if (node is null) return result;
        var fixedAction = GetResourceAction(node);
        if (IsRoguelikeInstance(instance) && node.ContentType == ResourceNodeType &&
            !node.ResourceNodeId.StartsWith("rogue_material_", StringComparison.Ordinal) && fixedAction is { ItemPath.Length: > 0 })
        {
            var quantity = Math.Max(1, fixedAction.Quantity * 2);
            var itemName = EastWoodsActions.Item(fixedAction.ItemPath)?.entity_name ?? "材料";
            fixedAction = fixedAction with
            {
                Description = $"收取{itemName} ×{quantity}。每次耗时{fixedAction.Hours:0.#}小时；储量有限。",
                Result = $"你收好{itemName} ×{quantity}。",
                Quantity = quantity,
                Stock = Math.Max(fixedAction.Stock * 2, quantity),
            };
        }
        if (IsRoguelikeInstance(instance) && node.ContentType == ResourceNodeType && fixedAction is { ItemPath.Length: > 0 })
        {
            var itemName = EastWoodsActions.Item(fixedAction.ItemPath)?.entity_name ?? "材料";
            fixedAction = fixedAction with
            {
                Stamina = 0,
                Description = $"收取{itemName} ×{fixedAction.Quantity}。每次耗时{fixedAction.Hours:0.#}小时；储量有限。",
            };
        }
        if (fixedAction is { } resourceAction &&
            (resourceAction.Recovery != EastWoodsActions.ResourceRecoveryGroup.None || resourceAction.Id is "rogue_alchemy" or "rogue_forging" or "rogue_meditation" or "rogue_shop" || resourceAction.Id.StartsWith("rogue_cultivation_", StringComparison.Ordinal) ||
             IsRoguelikeInstance(instance) && node.ContentType == ResourceNodeType) &&
            Math.Max(0, resourceAction.Stock - actions.GetValueOrDefault(nodeId + ":" + resourceAction.Id, 0).AsInt32()) >= Math.Max(1, resourceAction.Quantity))
            result.Add(resourceAction);
        else if (GetResourceAction(node) is { Rest: true } restAction &&
                 actions.GetValueOrDefault(nodeId + ":" + restAction.Id, 0).AsInt32() < restAction.Stock)
            result.Add(restAction);
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
            var creatureId = record.GetValueOrDefault("creature_id", "forest_wolf").AsString();
            var source = creatureId == CompactDungeonExpedition.CreatureIdForNode(node.Id) || !IsCompactInstance(instance)
                ? GetBeastSource(node)
                : null;
            if (source is not null)
                result.Add(source with { Id = actionId, EncounterIdentity = record.GetValueOrDefault("encounter_id", "").AsString(), CreatureId = creatureId });
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
        => instance.GetValueOrDefault("entry_open", false).AsBool() && (IsCompactInstance(instance) || currentHour < instance.GetValueOrDefault("expires_hour", 0).AsInt32());

    public static int OpenInstanceCount(GDictionary state, int currentHour)
        => state.GetValueOrDefault("instances", new GArray()).AsGodotArray().Count(value => value.VariantType == Variant.Type.Dictionary && !IsCompactInstance(value.AsGodotDictionary()) && IsEntryOpen(value.AsGodotDictionary(), currentHour));

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
            if (IsCompactInstance(instance) || !IsEntryOpen(instance, currentHour)) continue;
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
    {
        if (!GetLayout(instance).Neighbors.TryGetValue(nodeId, out var neighbors)) return System.Array.Empty<string>();
        if (IsRoguelikeInstance(instance) && !instance.GetValueOrDefault("boss_defeated", false).AsBool())
            return neighbors.Where(id => id != ExitNodeId).ToArray();
        return neighbors;
    }

    public static string GetEntryNodeId(GDictionary instance)
    {
        if (IsCompactInstance(instance)) return CompactDungeonExpedition.EntryNodeId;
        var snapshot = instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary();
        return snapshot.GetValueOrDefault("entry_node_id", LegacyEntryNodeId).AsString();
    }

    public static string GetExitNodeId(GDictionary instance)
    {
        if (IsCompactInstance(instance)) return CompactDungeonExpedition.ExitNodeId;
        var snapshot = instance.GetValueOrDefault("map_snapshot", new GDictionary()).AsGodotDictionary();
        return snapshot.GetValueOrDefault("exit_node_id", LegacyEntryNodeId).AsString();
    }
    public static string GetDungeonId(GDictionary instance) => (IsCompactInstance(instance) ? CompactDungeonExpedition.TemplateId + ":" : DungeonIdPrefix) + instance.GetValueOrDefault("instance_id", "unknown").AsString();
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
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || (!IsCompactInstance(instance) && instance.GetValueOrDefault("template_id", "").AsString() != TemplateId) ||
                instance.GetValueOrDefault("map_snapshot", new GDictionary()).VariantType != Variant.Type.Dictionary) return false;
            if (instance["map_snapshot"].AsGodotDictionary().GetValueOrDefault("template_id", "").AsString() != instance.GetValueOrDefault("template_id", "").AsString()) return false;
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
                (EastWoodsResourceCategory)data.GetValueOrDefault("resource_category", 0).AsInt32(),
                data.GetValueOrDefault("content_type", "").AsString(),
                data.GetValueOrDefault("creature_id", "").AsString());
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
        if (snapshot.GetValueOrDefault("template_id", "").AsString() == CompactDungeonExpedition.TemplateId)
        {
            CompactDungeonExpedition.ValidateLayout(snapshot);
            return;
        }
        if (snapshot.GetValueOrDefault("layout_version", "").AsString() is TowerLayoutVersion or RoguelikeLayoutVersion or PreviousRoguelikeLayoutVersion or OlderRoguelikeLayoutVersion or OldestRoguelikeLayoutVersion or LegacyRoguelikeLayoutVersion)
        {
            ValidateTowerLayout(snapshot);
            return;
        }
        ValidateLegacyGridLayout(snapshot);
    }

    private static void ValidateLegacyGridLayout(GDictionary snapshot)
    {
        var nodes = snapshot.GetValueOrDefault("nodes", new GArray()).AsGodotArray();
        var edges = snapshot.GetValueOrDefault("edges", new GArray()).AsGodotArray();
        if (snapshot.GetValueOrDefault("template_id", "").AsString() != TemplateId ||
            snapshot.GetValueOrDefault("columns", 0).AsInt32() != LegacyMapColumns ||
            snapshot.GetValueOrDefault("rows", 0).AsInt32() != LegacyMapRows || nodes.Count != LegacyMapCellCount ||
            snapshot.GetValueOrDefault("entry_node_id", "").AsString() != LegacyEntryNodeId ||
            snapshot.GetValueOrDefault("exit_node_id", "").AsString() != LegacyEntryNodeId)
            throw new InvalidOperationException("旧雾隐幽谷实例地图的模板尺寸、入口或格数无效。");
        var byId = new System.Collections.Generic.Dictionary<string, Vector2I>(StringComparer.Ordinal);
        var byGrid = new HashSet<Vector2I>();
        foreach (var value in nodes)
        {
            if (value.VariantType != Variant.Type.Dictionary) throw new InvalidOperationException("雾隐幽谷地图节点数据无效。");
            var node = value.AsGodotDictionary();
            var id = node.GetValueOrDefault("id", "").AsString();
            var grid = new Vector2I(node.GetValueOrDefault("x", -1).AsInt32(), node.GetValueOrDefault("y", -1).AsInt32());
            if (id != legacy_node_id(grid.X, grid.Y) || grid.X < 0 || grid.X >= LegacyMapColumns || grid.Y < 0 || grid.Y >= LegacyMapRows ||
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
        if (!byId.ContainsKey(LegacyEntryNodeId) || edges.Count < LegacyMapCellCount - 1)
            throw new InvalidOperationException("雾隐幽谷入口或连通路线缺失。");
        var visited = new HashSet<string>(StringComparer.Ordinal) { LegacyEntryNodeId };
        var queue = new Queue<string>(); queue.Enqueue(LegacyEntryNodeId);
        while (queue.Count > 0)
            foreach (var next in adjacency[queue.Dequeue()])
                if (visited.Add(next)) queue.Enqueue(next);
        if (visited.Count != LegacyMapCellCount) throw new InvalidOperationException("旧雾隐幽谷地图不能从入口连通全部128格。");
    }

    private static void ValidateTowerLayout(GDictionary snapshot)
    {
        var layoutVersion = snapshot.GetValueOrDefault("layout_version", "").AsString();
        var legacyRoguelike = layoutVersion == LegacyRoguelikeLayoutVersion;
        var roguelike = layoutVersion is RoguelikeLayoutVersion or PreviousRoguelikeLayoutVersion or OlderRoguelikeLayoutVersion or OldestRoguelikeLayoutVersion or LegacyRoguelikeLayoutVersion;
        var layerCount = legacyRoguelike || !roguelike ? TowerLayerCount : RoguelikeLayerCount;
        var nodes = snapshot.GetValueOrDefault("nodes", new GArray()).AsGodotArray();
        var edges = snapshot.GetValueOrDefault("edges", new GArray()).AsGodotArray();
        if (snapshot.GetValueOrDefault("template_id", "").AsString() != TemplateId ||
            snapshot.GetValueOrDefault("columns", 0).AsInt32() != MapColumns ||
            snapshot.GetValueOrDefault("rows", 0).AsInt32() != layerCount + 2 ||
            snapshot.GetValueOrDefault("layers", 0).AsInt32() != layerCount ||
            snapshot.GetValueOrDefault("entry_node_id", "").AsString() != EntryNodeId ||
            snapshot.GetValueOrDefault("exit_node_id", "").AsString() != ExitNodeId ||
            nodes.Count < layerCount * MinimumNodesPerLayer + 2 ||
            nodes.Count > layerCount * MaximumNodesPerLayer + 2)
            throw new InvalidOperationException($"{layerCount}层爬塔模板尺寸、出入口或节点数无效。");

        var positions = new System.Collections.Generic.Dictionary<string, Vector2I>(StringComparer.Ordinal);
        var byLayer = Enumerable.Range(1, layerCount).ToDictionary(layer => layer, _ => new HashSet<int>());
        var outgoing = new System.Collections.Generic.Dictionary<string, List<string>>(StringComparer.Ordinal);
        var incoming = new System.Collections.Generic.Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var value in nodes)
        {
            if (value.VariantType != Variant.Type.Dictionary) throw new InvalidOperationException("爬塔节点数据无效。");
            var node = value.AsGodotDictionary();
            var id = node.GetValueOrDefault("id", "").AsString();
            var x = node.GetValueOrDefault("x", -1).AsInt32();
            var y = node.GetValueOrDefault("y", -1).AsInt32();
            var type = node.GetValueOrDefault("content_type", "").AsString();
            if (x < 0 || x >= MapColumns || y < 0 || y > layerCount + 1 ||
                !positions.TryAdd(id, new Vector2I(x, y)) || node.GetValueOrDefault("terrain", -1).AsInt32() is < 0 or > 7)
                throw new InvalidOperationException("爬塔地图含有重复、越界或非法节点。");
            outgoing[id] = [];
            incoming[id] = [];
            if (id == EntryNodeId)
            {
                if (x != 3 || y != layerCount + 1 || type != "entry") throw new InvalidOperationException("爬塔入口坐标或类型无效。");
            }
            else if (id == ExitNodeId)
            {
                if (x != 3 || y != 0 || type != "exit") throw new InvalidOperationException("爬塔出口坐标或类型无效。");
            }
            else
            {
                var layer = layerCount - y + 1;
                var rogueOnlyType = type is "alchemy" or "forging" or "manual" or "cultivation" or "shop" || roguelike && type == BossNodeType;
                if (layer is < 1 || layer > layerCount || id != tower_node_id(layer, x) ||
                    !(type is BattleNodeType or ResourceNodeType or RestNodeType) && !rogueOnlyType || !byLayer[layer].Add(x))
                    throw new InvalidOperationException("爬塔层节点编号、类型或列位置无效。");
                if (type == BattleNodeType && string.IsNullOrWhiteSpace(node.GetValueOrDefault("creature_id", "").AsString()))
                    throw new InvalidOperationException("战斗节点没有固定遭遇生灵。");
                if (type == ResourceNodeType && string.IsNullOrWhiteSpace(node.GetValueOrDefault("resource_id", "").AsString()))
                    throw new InvalidOperationException("资源节点没有预设采集类型。");
                if (rogueOnlyType && !roguelike)
                    throw new InvalidOperationException("普通爬塔不能包含肉鸽专用节点。");
                if (roguelike && type == BossNodeType && (layer != layerCount || string.IsNullOrWhiteSpace(node.GetValueOrDefault("creature_id", "").AsString())))
                    throw new InvalidOperationException($"肉鸽首领必须位于第{layerCount}层且配置有效遭遇。");
            }
        }
        if (!positions.ContainsKey(EntryNodeId) || !positions.ContainsKey(ExitNodeId))
            throw new InvalidOperationException("爬塔出入口缺失。");
        foreach (var layer in byLayer.Keys)
            if (byLayer[layer].Count is < MinimumNodesPerLayer or > MaximumNodesPerLayer)
                throw new InvalidOperationException($"爬塔第{layer}层节点数超出约定范围。");
        if (layoutVersion == RoguelikeLayoutVersion)
        for (var layer = 3; layer <= 27; layer += 3)
        {
            var row = nodes.Select(value => value.AsGodotDictionary())
                .Where(node => layerCount - node.GetValueOrDefault("y", -1).AsInt32() + 1 == layer)
                .ToArray();
            var paths = row.Select(node => node.GetValueOrDefault("feature", "").AsString()).ToHashSet(StringComparer.Ordinal);
            if (row.Length is < MinimumNodesPerLayer or > MaximumNodesPerLayer ||
                row.Any(node => node.GetValueOrDefault("content_type", "").AsString() != "cultivation") ||
                !paths.Contains("cultivation_physique") || !paths.Contains("cultivation_intelligence") || !paths.Contains("cultivation_soul"))
                throw new InvalidOperationException($"肉鸽第{layer}层必须保留三种可选修行路径，确保本局境界成长可达。");
        }
        if (layoutVersion == RoguelikeLayoutVersion)
        {
            foreach (var layer in new[] { 1, 13, 25 })
            {
                var row = nodes.Select(value => value.AsGodotDictionary())
                    .Where(node => layerCount - node.GetValueOrDefault("y", -1).AsInt32() + 1 == layer)
                    .ToArray();
                if (row.Length is < MinimumNodesPerLayer or > MaximumNodesPerLayer ||
                    row.Any(node => node.GetValueOrDefault("content_type", "").AsString() != "manual"))
                    throw new InvalidOperationException($"肉鸽第{layer}层必须保留可达的静室节点。");
            }
            foreach (var layer in RoguelikeShopLayers)
            {
                var row = nodes.Select(value => value.AsGodotDictionary())
                    .Where(node => layerCount - node.GetValueOrDefault("y", -1).AsInt32() + 1 == layer)
                    .ToArray();
                if (row.Count(node => node.GetValueOrDefault("content_type", "").AsString() == "shop") != 1)
                    throw new InvalidOperationException($"肉鸽第{layer}层必须恰有一个商栈节点。");
            }
        }
        if (roguelike && byLayer[layerCount].Any(column =>
                nodes.Any(value => value.AsGodotDictionary().GetValueOrDefault("id", "").AsString() == tower_node_id(layerCount, column) &&
                    value.AsGodotDictionary().GetValueOrDefault("content_type", "").AsString() != BossNodeType)))
            throw new InvalidOperationException("肉鸽终层必须全部由首领节点组成。");

        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in edges)
        {
            if (value.VariantType != Variant.Type.Dictionary) throw new InvalidOperationException("爬塔连接数据无效。");
            var edge = value.AsGodotDictionary();
            var from = edge.GetValueOrDefault("from", "").AsString();
            var to = edge.GetValueOrDefault("to", "").AsString();
            if (!positions.TryGetValue(from, out var a) || !positions.TryGetValue(to, out var b) ||
                a.Y != b.Y + 1 || Math.Abs(a.X - b.X) > 1 || !seenEdges.Add(from + "|" + to))
                throw new InvalidOperationException("爬塔连接必须只通向相邻上层，且横向不超过一列。");
            outgoing[from].Add(to);
            incoming[to].Add(from);
        }
        var reachable = traverse(EntryNodeId, outgoing);
        var canExit = traverse(ExitNodeId, incoming);
        if (reachable.Count != nodes.Count || canExit.Count != nodes.Count)
            throw new InvalidOperationException("爬塔存在入口不可达或没有通往出口的节点。");

        static HashSet<string> traverse(string start, System.Collections.Generic.Dictionary<string, List<string>> graph)
        {
            var result = new HashSet<string>(StringComparer.Ordinal) { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
                foreach (var next in graph[queue.Dequeue()])
                    if (result.Add(next)) queue.Enqueue(next);
            return result;
        }
    }

    private static string tower_node_id(int layer, int column) => "tower_" + layer.ToString("D2") + "_" + column.ToString("D2");
    private static string legacy_node_id(int x, int y) => "valley_" + x.ToString("D2") + "_" + y.ToString("D2");
    private static string edge_key(string from, string to) => string.CompareOrdinal(from, to) <= 0 ? from + "|" + to : to + "|" + from;
}
