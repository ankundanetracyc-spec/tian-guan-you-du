using System;
using Godot;
using System.Collections.Generic;

/// <summary>东林地短测行动目录。内容与表现分离，消耗不随遁速变化。</summary>
public static class EastWoodsActions
{
    public enum ResourceRecoveryGroup { None, Common, SlowGrowth, Mineral }

    // Keep shared resource wrappers alive while preview and settlement use the same definition.
    private static readonly Dictionary<string, ItemData> Items = new(StringComparer.Ordinal);
    public static ItemData? Item(string path)
    {
        if (Items.TryGetValue(path, out var item)) return item;
        item = ResourceLoader.Load<ItemData>(path);
        if (item is not null) Items[path] = item;
        return item;
    }
    public sealed record ActionDef(string Id, string Name, string Description, float Hours, float Stamina,
        string Result, string ItemPath = "", int Quantity = 0, int Stock = 0, bool Battle = false,
        ResourceRecoveryGroup Recovery = ResourceRecoveryGroup.None, bool OrdinaryBeastSource = false,
        int OrdinaryBeastCapacity = 0, string EncounterIdentity = "", string CreatureId = "", bool Rest = false);

    public static string SlowGrowthPointNodeId()
        => ResourceNodeId("mistbell_slow");

    public static string MineralPointNodeId()
        => ResourceNodeId("spirit_stone_vein");

    private static string ResourceNodeId(string profileId)
        => EastWoodsDungeonRules.get_nodes().FirstOrDefault(node => node.ResourceNodeId == profileId)?.Id ?? "";

    public static ActionDef? Content(string node)
    {
        var mapNode = EastWoodsDungeonRules.get_node(node);
        if (mapNode is not null)
        {
            if (EastWoodsResourceNodes.ForNode(mapNode) is { } resource) return resource.Action;
            if (EastWoodsResourceNodes.HasResourceTag(mapNode)) return null;
        }

        return node switch
        {
        "outer_13_06" or "outer_08_08" => Content("root_hollow")! with { Description = "一头林地凶狼伏在林径旁，已有通智初期的修为。初入云息时不宜冒进；可以暂不应战，绕走其他通路。击败它可收取兽骨与兽皮各一份。", OrdinaryBeastSource = true, OrdinaryBeastCapacity = 1 },
        "outer_05_06" => Content("north_turn"),
        "outer_12_06" => Content("moss_bridge")! with { Description = "湿土上留着新鲜爪印，朝附近林间延伸。可以辨认痕迹，不必立即追赶。", Result = "附近林径有凶狼活动。留意视野内的动静，再决定应战或绕行。" },
        "north_turn" => new("old_trap", "调查", "腐叶盖着一处废弃陷坑。可以沿边查看并记下危险，不必跳入坑底。", .5f, 1,
            "你在坑沿留下醒目的枝条记号，确认这里已无人维护，也没有遗落宝物。"),
        "moss_bridge" => new("beast_tracks", "辨迹", "湿土上的爪印折向盘根深处。爪距与拖痕不像寻常野兽留下的。", .5f, 1,
            "你辨出林地凶狼的踪迹：盘根深处可能有妖兽盘踞。抵达后可自行决定应战或绕行。"),
        "root_hollow" => new("wolf", "应战", "一头林地凶狼伏在盘根之后，已有通智初期的修为。初入云息时不宜冒进；可以暂不应战，绕走其他通路。击败它可收取兽骨与兽皮各一份。", 0, 0,
            "此处妖兽已被击败。", Battle: true, OrdinaryBeastSource: true, OrdinaryBeastCapacity: 1),
        _ => null
        };
    }

    public static ActionDef? Find(string node, string id) => Content(node) is { } content && content.Id == id ? content : null;

    public static List<(string Node, ActionDef Action)> ResourcePoints()
    {
        var result = new List<(string Node, ActionDef Action)>();
        foreach (var node in EastWoodsDungeonRules.get_nodes())
            if (Content(node.Id) is { Recovery: not ResourceRecoveryGroup.None } action)
                result.Add((node.Id, action));
        return result;
    }

    public static List<(string Node, ActionDef Action)> OrdinaryBeastPoints()
    {
        var result = new List<(string Node, ActionDef Action)>();
        foreach (var node in EastWoodsDungeonRules.get_nodes())
            if (Content(node.Id) is { OrdinaryBeastSource: true, OrdinaryBeastCapacity: > 0 } action)
                result.Add((node.Id, action));
        return result;
    }

    public static ActionDef? RegeneratedBeast(string node, string actionId, string encounterIdentity)
    {
        if (Content(node) is not { OrdinaryBeastSource: true, Battle: true } action) return null;
        return action with
        {
            Id = actionId,
            Name = "应战",
            EncounterIdentity = encounterIdentity,
            Description = "林地凶狼在此处重新活动。它仍是原有凶狼配置；可自行决定应战或绕行。击败可依现有战斗结算收取兽骨与兽皮。"
        };
    }
}
