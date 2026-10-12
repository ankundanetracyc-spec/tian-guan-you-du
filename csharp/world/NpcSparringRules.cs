using System.Collections.Generic;

/// <summary>宗内友好对练；判断只使用对方确实掌握的玩家信息。</summary>
public static class NpcSparringRules
{
    public static bool Supports(WorldNpcData npc, HumanData player) =>
        player.sect_affiliation_id == "yuxu_xianzong" && npc.faction_id == player.sect_affiliation_id &&
        player.cultivation_realm.StartsWith("云息") && npc.cultivation_realm.StartsWith("云息");
    public static bool WantsToInvite(WorldNpcData npc, HumanData player) => Supports(npc, player) &&
        NpcSocialRules.KnowsName(npc) && NpcPersonalityRules.Values(npc, "修行") && Evaluate(npc, player).AgreesToSpar;
    public static NpcSocialRules.Response Evaluate(WorldNpcData npc, HumanData player)
    {
        var steps = new List<NpcSocialRules.DecisionStep>();
        var lines = new List<string>();
        void Note(string rule, string text) { steps.Add(new(rule, text)); lines.Add(text); }
        NpcSocialRules.Response Finish(string text, bool agrees = false) => new(text, "", lines) {
            DecisionSteps = steps.ToArray(), ActionName = "商议切磋", AgreesToSpar = agrees };
        if (!Supports(npc, player)) return Finish("目前只开放玉虚同门之间的云息对练。");
        if (npc.player_relationship < 0 || npc.player_social_memory.GetValueOrDefault("relationship", "").AsString() == "enemy")
        { Note("spar_hostile", "说起对练，对方提起旧怨，并不愿与你点到为止。"); return Finish("对方婉拒了此次切磋。"); }
        var known = NpcSocialRules.KnowsName(npc);
        var cautious = NpcPersonalityRules.Is(npc, "risk", "谨慎");
        var rememberedRealm = npc.player_social_memory.GetValueOrDefault("player_realm", "").AsString();
        if (cautious && !known && string.IsNullOrWhiteSpace(rememberedRealm))
        { Note("spar_unknown", "对方尚不了解你，听到切磋之邀，仍有些放心不下。"); return Finish("对方表示不妨先认识一番，暂未应下切磋。"); }
        if (cautious && !string.IsNullOrWhiteSpace(rememberedRealm) &&
            NpcSocialRules.RealmAddress(npc.cultivation_realm, rememberedRealm, npc.current_age, player.current_age) == "前辈")
        { Note("spar_known_gap", "想起此前得知的修为差距，对方摇了摇头，表示还需再练些时日。"); return Finish("对方暂不愿与你切磋。"); }
        if (!known && NpcPersonalityRules.Is(npc, "sociability", "内敛") && !NpcPersonalityRules.Values(npc, "修行"))
        { Note("spar_reserved", "彼此尚不熟悉，对方没有接下邀约，只客气地推到了改日。"); return Finish("对方婉拒了此次切磋。"); }
        if (known) Note("spar_familiar", "与你已有往来，对方愿意试一试彼此的身手。");
        if (NpcPersonalityRules.Values(npc, "修行")) Note("spar_cultivation", "说到相互印证所学，对方来了兴致。");
        Note("spar_terms", "双方约定点到为止，胜负之外不作争执。");
        return Finish("对方应下切磋，双方准备对练。", true);
    }
}
