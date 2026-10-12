using System;
using Godot;
using System.Collections.Generic;

/// <summary>东林地短测行动目录。内容与表现分离，消耗不随遁速变化。</summary>
public static class EastWoodsActions
{
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
        string Result, string ItemPath = "", int Quantity = 0, int Stock = 0, bool Battle = false);

    public static ActionDef? Content(string node) => node switch
    {
        "outer_03_06" or "outer_06_04" => Content("fern_lowland"),
        "outer_09_07" or "outer_12_03" => Content("dry_pool"),
        "outer_13_06" or "outer_08_08" => Content("root_hollow")! with { Description = "一头林地凶狼伏在林径旁，已有通智初期的修为。初入云息时不宜冒进；可以暂不应战，绕走其他通路。击败它可收取兽骨与兽皮各一份。" },
        "outer_05_06" => Content("north_turn"),
        "outer_12_06" => Content("moss_bridge")! with { Description = "湿土上留着新鲜爪印，朝附近林间延伸。可以辨认痕迹，不必立即追赶。", Result = "附近林径有凶狼活动。留意视野内的动静，再决定应战或绕行。" },
        "fern_lowland" => new("gather_root", "采集", "蕨叶下生着几丛苦苔根。每次可采两份，此处原有四份。采尽后暂不再生，不必在此久候。", 1, 2,
            "你收好苦苔根 ×2。", "res://resources/entities/items/苦苔根.tres", 2, 4),
        "dry_pool" => new("gather_moss", "采集", "浅洼背阴处附着月露苔。每次可采一份，此处原有两份。采尽后暂不再生，不必在此久候。", 1, 2,
            "你收好月露苔 ×1。", "res://resources/entities/items/月露苔.tres", 1, 2),
        "north_turn" => new("old_trap", "调查", "腐叶盖着一处废弃陷坑。可以沿边查看并记下危险，不必跳入坑底。", .5f, 1,
            "你在坑沿留下醒目的枝条记号，确认这里已无人维护，也没有遗落宝物。"),
        "moss_bridge" => new("beast_tracks", "辨迹", "湿土上的爪印折向盘根深处。爪距与拖痕不像寻常野兽留下的。", .5f, 1,
            "你辨出林地凶狼的踪迹：盘根深处可能有妖兽盘踞。抵达后可自行决定应战或绕行。"),
        "root_hollow" => new("wolf", "应战", "一头林地凶狼伏在盘根之后，已有通智初期的修为。初入云息时不宜冒进；可以暂不应战，绕走其他通路。击败它可收取兽骨与兽皮各一份。", 0, 0,
            "此处妖兽已被击败。", Battle: true),
        _ => null
    };

    public static ActionDef? Find(string node, string id) => Content(node) is { } content && content.Id == id ? content : null;
}
