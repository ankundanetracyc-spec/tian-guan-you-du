using Godot;
using System;
using System.Collections.Generic;

/// <summary>六轴性情与个人所重；定性首版，不引入未定案的数值权重。</summary>
public static class NpcPersonalityRules
{
    private static readonly (string Key, string Title, string Low, string High)[] Axes = [
        ("sociability", "交往", "内敛", "外向"), ("benevolence", "仁心", "冷酷", "仁厚"),
        ("risk", "行事", "谨慎", "冒进"), ("order", "规矩", "不羁", "守序"),
        ("attachment", "情分", "淡情", "重情"), ("forgiveness", "恩怨", "记怨", "宽和") ];
    private static readonly string[] Priorities = ["修行", "财利", "声名", "宗门", "亲友", "传承"];
    private static uint Stable(string text)
    {
        uint hash = 2166136261;
        foreach (var c in text) { hash ^= c; hash = unchecked(hash * 16777619); }
        return hash;
    }
    public static void Ensure(WorldNpcData npc)
    {
        var seed = string.IsNullOrWhiteSpace(npc.npc_id) ? npc.npc_name + ":" + npc.origin_node_id : npc.npc_id;
        foreach (var axis in Axes)
        {
            if (npc.social_personality.ContainsKey(axis.Key)) continue;
            var legacy = axis.Key switch {
                "sociability" => npc.personality switch { "寡言" => "内敛", "爽直" or "圆融" or "随和" => "外向", _ => "" },
                "risk" => npc.personality is "谨慎" or "多疑" ? "谨慎" : "",
                "benevolence" => npc.personality == "温厚" ? "仁厚" : "",
                "attachment" => npc.personality == "温厚" ? "重情" : "",
                _ => "" };
            var pick = Stable(seed + ":" + axis.Key) % 3;
            npc.social_personality[axis.Key] = legacy.Length > 0 ? legacy : pick == 0 ? axis.Low : pick == 1 ? "均衡" : axis.High;
        }
        // “务实”并非“淡情”或“逐利”：留存旧值，不推导新的道德或利益取向。
        if (npc.social_personality["attachment"].AsString() == "务实")
        { npc.social_personality["legacy_attachment"] = "务实"; npc.social_personality["attachment"] = "均衡"; }
        if (npc.social_personality["order"].AsString() == "随性") npc.social_personality["order"] = "不羁";
        if (npc.social_priorities.Count == 0)
            npc.social_priorities.Add(Priorities[Stable(seed + ":priority") % (uint)Priorities.Length]);
    }
    public static bool Is(WorldNpcData npc, string axis, string value)
    { Ensure(npc); return npc.social_personality.GetValueOrDefault(axis, "均衡").AsString() == value; }
    public static bool Values(WorldNpcData npc, string priority) { Ensure(npc); return npc.social_priorities.Contains(priority); }
    public static bool WillVisit(WorldNpcData npc, bool publiclyKnown)
    {
        Ensure(npc);
        if (CharacterRelationshipRules.AvoidsContact(npc, CharacterRelationshipRules.PlayerCharacterId)) return false;
        return CharacterRelationshipRules.Value(npc, CharacterRelationshipRules.PlayerCharacterId, CharacterRelationshipRules.Closeness) >= 30 ||
            NpcSocialRules.KnowsName(npc) || publiclyKnown || !Is(npc, "sociability", "内敛") ||
            NpcPlayerImpressionRules.WillingContact(npc) ||
            (NpcPlayerImpressionRules.SparPartner(npc) && Values(npc, "修行")) ||
            (NpcPlayerImpressionRules.HerbTradeContact(npc) && NpcPurchaseRules.Offer(npc) is not null);
    }
    public static bool HasVisitPriority(WorldNpcData npc, bool publiclyKnown) =>
        (publiclyKnown && (Values(npc, "声名") || Values(npc, "宗门"))) ||
        (CharacterRelationshipRules.LegacyMarker(npc, CharacterRelationshipRules.PlayerCharacterId) == "friend" && Values(npc, "亲友")) ||
        CharacterRelationshipRules.Value(npc, CharacterRelationshipRules.PlayerCharacterId, CharacterRelationshipRules.Closeness) >= 55;
    public static bool TestInspection => ProjectSettings.GetSetting("gameplay/npc_test_inspection", true).AsBool();
    public static string InspectionText(WorldNpcData npc, bool testing)
    {
        Ensure(npc);
        var lines = new List<string>();
        if (testing)
        {
            lines.Add("测试查阅 · 真实性情（不计入玩家认知）");
            foreach (var axis in Axes) lines.Add(axis.Title + "：" + npc.social_personality[axis.Key].AsString());
            lines.Add("所重：" + string.Join("、", npc.social_priorities));
        }
        else
        {
            lines.Add("交际所得 · 未知信息不展示");
            foreach (var key in npc.player_known_npc_info.Keys)
            {
                var entry = npc.player_known_npc_info[key].AsGodotDictionary();
                lines.Add((entry["value"].AsString() + "（" + entry["source"].AsString() + "）").Replace("神识", "灵识"));
            }
            if (npc.player_known_npc_info.Count == 0) lines.Add("尚无了解。");
        }
        return string.Join("\n", lines);
    }
    public static void Observe(WorldNpcData npc, string key, string value, string source, int month)
    {
        npc.player_known_npc_info[key] = new Godot.Collections.Dictionary {
            ["value"] = value, ["source"] = source, ["month"] = month };
    }
}
