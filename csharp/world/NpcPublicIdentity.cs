using System;
using System.Linq;

/// <summary>公开名头的展示口径；不改NPC职业、归属和世界行为数据。</summary>
public static class NpcPublicIdentity
{
    private static readonly string[] OfficialTitles = [
        "弟子", "长老", "执事", "掌门", "宗主", "掌教", "门主", "峰主", "堂主",
        "客卿", "供奉", "护法", "家主", "族长", "少主", "城主", "城守", "真传",
        "杂役", "来使", "主持", "接引", "除名修士"
    ];

    public static string Title(WorldNpcData npc)
    {
        var title = npc.formal_identity.Trim();
        if (title is "散修" or "行旅散修" or "炼气士") return title;
        if (!OfficialTitles.Any(word => title.Contains(word, StringComparison.Ordinal))) return "";
        var faction = InfoPanel.npc_faction_name(npc.faction_id);
        if (string.IsNullOrWhiteSpace(npc.faction_id) || faction == npc.faction_id ||
            title.Contains(faction, StringComparison.Ordinal) || title.Contains("除名", StringComparison.Ordinal)) return title;
        return faction + title;
    }
}
