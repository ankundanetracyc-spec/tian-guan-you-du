using System;

/// <summary>四倾向定性接入，不编造尚未确认的概率或数值权重。</summary>
public static class NpcPersonalityRules
{
    public static void Ensure(WorldNpcData npc)
    {
        // 仅迁移可明确对应的旧描述，其余轴保留中性；不覆盖已有设定。
        void Set(string axis, string value) { if (!npc.social_personality.ContainsKey(axis)) npc.social_personality[axis] = value; }
        Set("sociability", npc.personality switch { "寡言" => "内敛", "爽直" or "圆融" or "随和" => "外向", _ => "均衡" });
        Set("attachment", npc.personality == "温厚" ? "重情" : "均衡");
        Set("order", "均衡");
        Set("risk", npc.personality is "谨慎" or "多疑" ? "谨慎" : "均衡");
    }
    public static bool Is(WorldNpcData npc, string axis, string value)
    { Ensure(npc); return npc.social_personality.GetValueOrDefault(axis, "均衡").AsString() == value; }
    public static bool WillVisit(WorldNpcData npc, bool publiclyKnown)
    {
        Ensure(npc);
        if (npc.player_relationship < 0 || npc.player_social_memory.GetValueOrDefault("relationship", "").AsString() == "enemy") return false;
        return NpcSocialRules.KnowsName(npc) || publiclyKnown || !Is(npc, "sociability", "内敛");
    }
}
