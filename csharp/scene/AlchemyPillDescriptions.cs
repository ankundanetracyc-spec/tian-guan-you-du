using System.Collections.Generic;

/// <summary>丹方册的只读图文，以成品模板 ID 关联，不参与炼丹结算。</summary>
public static class AlchemyPillDescriptions
{
    public sealed record Entry(string Verse, string Description, string Guidance, string TexturePath);

    public static readonly IReadOnlyDictionary<string, Entry> Entries = new Dictionary<string, Entry>
    {
        ["huichun"] = new("春未老，一点青意，入丹来。",
            "采草木生发之意，凝为一丸，服之可温养生机。药性平和，为丹道入门常习之丹。",
            "以蕴「生」性之材为「君」，立回春之本。",
            "res://textures/items/item_alchemy_huichun_v1.png"),
        ["tongmai"] = new("一缕清流，过重关。",
            "药意如清流行于经脉，疏其壅滞，畅其气机。",
            "以「通」性之材为「君」，方入通脉丹之列。",
            "res://textures/items/item_alchemy_tongmai_v1.png"),
        ["jingxin"] = new("尘声渐远，心湖自平。",
            "收浮躁，敛纷扰，取一分静水涵月之意，凝于丹中。",
            "「君」药蕴「镇」性，乃得静心之意。",
            "res://textures/items/item_alchemy_jingxin_v1.png")
    };

    public static string DisplayName(AlchemyRecipeCatalog.PillType pill)
    {
        // 名称和丹阶均取自模板；当前模板名以“九品”等丹阶前缀开头。
        string rank = pill.Rank is >= 1 and <= 9 ? "一二三四五六七八九"[pill.Rank - 1] + "品" : pill.Rank + "品";
        string name = pill.Name.StartsWith(rank, System.StringComparison.Ordinal) ? pill.Name[rank.Length..] : pill.Name;
        return name + " · " + rank;
    }
}
