using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 仅供交互调试入口使用的试算模型。属性由稳定 NPC ID 临时派生，不写入人物、记忆或存档。
/// 这些权重是可调试的原型，不代表正式 NPC 属性或交际规则。
/// </summary>
public static class NpcSocialPrototypeRules
{
    public readonly record struct TemporaryStats(float Charisma, float Social);

    // 首轮试算权重：属性以 50 为中点；心境为 -50..50；分值只影响本次测试中的姓名交换结果。
    private const float PlayerCharismaWeight = .16f;
    private const float PlayerSocialWeight = .20f;
    private const float NpcCharismaWeight = .10f;
    private const float NpcSocialWeight = .18f;
    private const float VisitPlayerCharismaWeight = .08f;
    private const float VisitPlayerSocialWeight = .10f;
    private const float VisitNpcCharismaWeight = .14f;
    private const float VisitNpcSocialWeight = .24f;
    private const float MoodWeight = .10f;
    private const float ExchangeThreshold = 0f;
    private const float VisitThreshold = 6f;

    public static TemporaryStats GetTemporaryStats(WorldNpcData npc)
    {
        var seed = Stable(string.IsNullOrWhiteSpace(npc.npc_id) ? npc.npc_name : npc.npc_id);
        var charisma = 35 + seed % 51;
        var social = 35 + (Mix(seed ^ 0x9e3779b9u) % 51);
        return new TemporaryStats(charisma, social);
    }

    public static NpcSocialRules.Response Evaluate(WorldNpcData npc, HumanData player, bool npcInitiated, bool accepted)
    {
        var steps = new List<NpcSocialRules.DecisionStep>();
        var score = 0f;
        void Add(string key, float value, string favorable, string unfavorable)
        {
            if (Math.Abs(value) < .01f) return;
            score += value;
            steps.Add(new NpcSocialRules.DecisionStep(key, value > 0 ? favorable : unfavorable) { Favorable = value > 0 });
        }
        NpcSocialRules.Response Response(string result, string speech, bool exchanged = false)
        {
            var factors = new List<string>(steps.Count);
            foreach (var step in steps) if (step.Visible) factors.Add(step.Text);
            return new NpcSocialRules.Response(result, speech, factors)
            {
                DecisionSteps = steps.ToArray(),
                ActionName = npcInitiated ? "来访" : "结识",
                ExchangesName = exchanged
            };
        }

        if (!accepted)
        {
            steps.Add(new NpcSocialRules.DecisionStep("test_declined", npcInitiated
                ? "你婉言谢绝，对方收住话头，拱手告辞。"
                : "你收回上前搭话的念头，没有惊动对方。", Visible: false));
            return Response(npcInitiated ? "你婉拒了这次来访，双方各自离去。" : "你暂未与对方交谈。", "");
        }

        var hostile = CharacterRelationshipRules.IsHostile(npc, CharacterRelationshipRules.PlayerCharacterId);
        if (hostile)
        {
            steps.Add(new NpcSocialRules.DecisionStep("test_hostile", "旧日过节横在中间，对方只略一拱手，便不再多言。"));
            return Response("旧怨未消，这次交谈没能继续。", "");
        }

        var temporary = GetTemporaryStats(npc);
        var sameFaction = !string.IsNullOrWhiteSpace(player.sect_affiliation_id) && player.sect_affiliation_id == npc.faction_id;
        var trueDisciple = sameFaction && player.sect_affiliation_id == "yuxu_xianzong" && (player.sect_current_rank is "真传" or "真传弟子");
        var knowsName = NpcSocialRules.KnowsName(npc);
        var relationship = CharacterRelationshipRules.LegacyMarker(npc, CharacterRelationshipRules.PlayerCharacterId);
        var close = CharacterRelationshipRules.Value(npc, CharacterRelationshipRules.PlayerCharacterId, CharacterRelationshipRules.Closeness) >= 30;
        var sociability = ReadAxis(npc, "sociability");
        var cautious = ReadAxis(npc, "risk") == "谨慎";
        var deedRenown = RegionalRenownSystem.get_bai_chuan_deed_renown(player);
        var locallyKnownDeeds = deedRenown >= 20 && (sameFaction || RegionalRenownSystem.get_bai_chuan_total(player) >= 100);

        if (!npcInitiated)
        {
            Add("player_charisma", (Math.Clamp(player.charisma, 0f, 100f) - 50f) * PlayerCharismaWeight,
                "你神色从容地开口，对方没有立刻避开目光。", "你开口时略显局促，对方的目光很快移向别处。");
            Add("player_social", (Math.Clamp(player.social, 0f, 100f) - 50f) * PlayerSocialWeight,
                "你接话得体，对方愿意多听几句。", "话头几次没能接稳，对方的回应渐渐短了。");
            Add("player_mood", Math.Clamp(player.mood_value, -50, 50) * MoodWeight,
                "你眉间舒展，话头接得自然。", "你眉间倦色未消，话头几次停顿下来。");
            Add("npc_charisma", (temporary.Charisma - 50f) * NpcCharismaWeight,
                "对方神情温和，与你说话时少了些拘束。", "对方神色疏淡，与你交谈时始终留着距离。");
            Add("npc_social", (temporary.Social - 50f) * NpcSocialWeight,
                "对方很快接住话头，几句寒暄便有了来往。", "对方来意说完便停了下来，没能把话题接开。");
            Add("npc_mood", Math.Clamp(npc.mood_value, -50, 50) * MoodWeight,
                "对方神色和缓，几次主动接过话头。", "对方眉间倦色未散，回应简短，话头几次断开。");
            if (sameFaction) Add("same_faction", 7f, "谈及同门身份，对方少了几分生疏。", "");
            if (trueDisciple) Add("recognized_identity", 7f, "宗内真传的身份已传到对方面前，他的态度郑重了些。", "");
            if (locallyKnownDeeds) Add("public_renown", 4f, "你在百川留下的公开事迹传到此处，对方多看了你一眼。", "");
            if (knowsName) Add("known_name", 9f, "对方认出是你，话头自然转到近况上。", "");
            if (relationship == "friend" || close) Add("friendship", 8f, "你们已有几次往来，对方不再把你当作生客。", "");
            if (sociability == "外向") Add("outgoing", 4f, "对方主动接过话头，交谈没有僵住。", "");
            if (sociability == "内敛") Add("reserved", -4f, "对方话少而谨慎，几次回应后便停了下来。", "");
            if (cautious && !knowsName && !sameFaction && !trueDisciple)
                Add("caution", -8f, "对方几次追问来处，听完后仍没有报上姓名。", "");
            if (sameFaction && HasPriority(npc, "宗门")) Add("sect_priority", 4f, "提到宗门近况，对方追问了几句你在峰中的日常。", "");
            if (locallyKnownDeeds && HasPriority(npc, "声名")) Add("renown_priority", 3f, "你说起百川近来的传闻，对方接连问了几件公开事迹。", "");

            var exchanged = score >= ExchangeThreshold;
            return Response(exchanged ? "你们互通姓名，闲谈了几句。" : "你们略作寒暄，未能结识。",
                exchanged ? "你们互通了姓名。" : "对方没有留下姓名。", exchanged);
        }

        Add("npc_charisma", (temporary.Charisma - 50f) * VisitNpcCharismaWeight,
            "对方开口从容，来意说得清楚。", "对方开口时略显迟疑，几次斟酌才说明来意。");
        Add("npc_social", (temporary.Social - 50f) * VisitNpcSocialWeight,
            "对方善于寒暄，很快把话题引到你身上。", "对方不善铺陈，来意说完便停了下来。");
        Add("npc_mood", Math.Clamp(npc.mood_value, -50, 50) * MoodWeight,
            "对方神色舒展，主动开口说明来意。", "对方眉间倦意未散，话只说了个开头。");
        Add("player_charisma", (Math.Clamp(player.charisma, 0f, 100f) - 50f) * VisitPlayerCharismaWeight,
            "你举止得体，对方愿意把来意说完。", "你神色紧绷，对方说到一半便收住话头。");
        Add("player_social", (Math.Clamp(player.social, 0f, 100f) - 50f) * VisitPlayerSocialWeight,
            "你接住对方的话，交谈很快有了来回。", "你几次没有接上话，对方渐渐止住了话头。");
        Add("player_mood", Math.Clamp(player.mood_value, -50, 50) * MoodWeight,
            "你神色舒展，顺势与对方多谈了几句。", "你眉间倦色未消，回应显得有些勉强。");
        if (sameFaction) Add("same_faction", 7f, "同门身份让这场来访少了些客套。", "");
        if (trueDisciple) Add("recognized_identity", 7f, "对方听闻你是真传弟子，开场便向你问礼。", "");
        if (locallyKnownDeeds) Add("public_renown", 4f, "对方提起你在百川留下的公开事迹，问了其中一两件。", "");
        if (knowsName) Add("known_name", 8f, "对方认得你，交谈很快越过了初见的客套。", "");
        if (relationship == "friend" || close) Add("friendship", 8f, "你们已有交情，对方说起近况时自在许多。", "");
        if (sociability == "外向") Add("visit_initiative", 5f, "对方主动攀谈，几句话便把来意讲开。", "");
        if (sociability == "内敛") Add("visit_initiative", -5f, "对方虽主动来访，说完来意后仍显得拘谨。", "");
        if (sameFaction && HasPriority(npc, "宗门")) Add("sect_priority", 5f, "对方提起峰中事务，接着问了几句你的近况。", "");
        if (trueDisciple && HasPriority(npc, "声名")) Add("renown_priority", 4f, "对方听闻真传名号，来访时先问起你的入门经历。", "");

        var visitContinues = score >= VisitThreshold;
        return Response(visitContinues ? "你们互通姓名，继续交谈。" : "你接待了对方，交谈片刻后各自散去。",
            visitContinues ? "对方报上姓名，交谈继续。" : "对方没有久留。", visitContinues);
    }

    private static string ReadAxis(WorldNpcData npc, string axis)
        => npc.social_personality.GetValueOrDefault(axis, "均衡").AsString();

    private static bool HasPriority(WorldNpcData npc, string priority)
        => npc.social_priorities.Contains(priority);

    private static uint Stable(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in value) { hash ^= c; hash *= 16777619; }
            return hash;
        }
    }

    private static uint Mix(uint value)
    {
        unchecked
        {
            value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15;
            value *= 0x846ca68b; value ^= value >> 16; return value;
        }
    }
}
