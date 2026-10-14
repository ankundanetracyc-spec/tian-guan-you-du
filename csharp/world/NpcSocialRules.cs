using Godot;
using System;
using System.Collections.Generic;

/// <summary>首版事实型交谈：不新增随机成功率、关系奖励或隐藏性格展示。</summary>
public static class NpcSocialRules
{
    public sealed record DecisionStep(string Rule, string Text, bool Visible = true)
    {
        // 仅给展示选择相符的措辞；不在界面公开数值，也不参与规则计算。
        public bool? Favorable { get; init; }
    }
    public sealed record Response(string Result, string Speech, IReadOnlyList<string> Factors)
    {
        public IReadOnlyList<DecisionStep> DecisionSteps { get; init; } = [];
        public float StaminaSpent { get; init; }
        public string ActionName { get; init; } = "交谈";
        public bool ExchangesName { get; init; }
        public bool AgreesToSpar { get; init; }
        /// <summary>玩家对 NPC 的关系记事；不包含 NPC 对玩家的内在关系。</summary>
        public string RelationshipFeedback { get; init; } = "";
        // 具体旧事不是新判定，也不形成性格标签或社交加成。
        public NpcPersonalRecallRules.Recall? Recollection { get; init; }
    }
    public static bool KnowsName(WorldNpcData npc) => !string.IsNullOrWhiteSpace(npc.player_social_memory.GetValueOrDefault("player_name", "").AsString());
    public static bool HeardTrueDisciple(WorldNpcData npc) => npc.player_social_memory.GetValueOrDefault("heard_player_rank", "").AsString() == "真传弟子";
    public static void HearSectReputation(WorldNpcData npc, HumanData player, bool proactive)
    {
        // 仅宗内实际同门来访获得公开身份线索；不把区域声望当作私交或全世界通晓。
        if (!proactive || npc.current_node_id != "yuxu_xianzong_core" || player.sect_affiliation_id != "yuxu_xianzong" || npc.faction_id != player.sect_affiliation_id || player.sect_current_rank is not ("真传" or "真传弟子")) return;
        npc.player_social_memory["heard_player_rank"] = "真传弟子";
        npc.player_social_memory["heard_player_name"] = player.entity_name;
        npc.player_social_memory["heard_source"] = "宗内传闻";
    }
    public static string Address(WorldNpcData npc, int playerAge)
    {
        var memory = npc.player_social_memory;
        var name = memory.GetValueOrDefault("player_name", "").AsString();
        var relation = CharacterRelationshipRules.LegacyMarker(npc, CharacterRelationshipRules.PlayerCharacterId);
        if (relation == "enemy") return name.Length > 0 ? name : "你";
        if ((relation == "friend" || CharacterRelationshipRules.Value(npc, CharacterRelationshipRules.PlayerCharacterId, CharacterRelationshipRules.Closeness) >= 80) && name.Length > 0) return name;
        return RealmAddress(npc.cultivation_realm, memory.GetValueOrDefault("player_realm", "").AsString(), npc.current_age, playerAge);
    }
    public static string RealmAddress(string own, string target, int ownAge, int targetAge)
    {
        int Major(string realm) => realm.StartsWith("云息") ? 1 : realm.StartsWith("玄基") ? 2 : realm.StartsWith("金丹") ? 3 : realm == "凡人" ? 0 : -1;
        var self = Major(own); var other = Major(target);
        if (self < 0 || other < 0) return "道友";
        if (self < other) return "前辈";
        if (self > other) return targetAge > ownAge ? "道友" : "小友";
        int Stage(string realm)
        {
            string[] levels = ["一", "二", "三", "四", "五", "六", "七", "八", "九", "十"];
            if (realm.StartsWith("云息"))
                for (int i = 0; i < levels.Length; i++) if (realm == "云息" + levels[i] + "层") return i + 1;
            if (realm.Contains("圆满")) return 4;
            if (realm.Contains("后期")) return 3;
            if (realm.Contains("中期")) return 2;
            if (realm.Contains("初期")) return 1;
            return 0;
        }
        return Stage(own) < Stage(target) ? "前辈" : "道友";
    }
    public static string Opening(WorldNpcData npc, HumanData player, string place, bool proactive)
    {
        var address = Address(npc, player.current_age);
        if (CharacterRelationshipRules.IsHostile(npc, CharacterRelationshipRules.PlayerCharacterId))
            return $"{address}，你找我有何事？";
        if (KnowsName(npc)) return "对方认出你来，向你问了近况。";
        var sameSect = !string.IsNullOrEmpty(player.sect_affiliation_id) && npc.faction_id == player.sect_affiliation_id;
        if (proactive && sameSect && HeardTrueDisciple(npc)) return "对方在宗内停步向你问候，提起听闻你拜入真传。";
        if (proactive && sameSect) return "对方在宗内停下脚步，先向你问了近况。";
        return "你准备上前与对方结识。";
    }
    public static Response Evaluate(WorldNpcData npc, HumanData player, string choice, bool proactive = false)
    {
        var known = KnowsName(npc);
        NpcPersonalityRules.Ensure(npc);
        var factors = new List<string>();
        var steps = new List<DecisionStep>();
        void Trace(string rule, string text, bool visible = true) { steps.Add(new(rule, text, visible)); if (visible) factors.Add(text); }
        Response Finish(string result, string speech, bool exchange = false) =>
            new(result, speech, factors) { ExchangesName = exchange, DecisionSteps = steps.ToArray() };
        if (choice == "leave")
        {
            Trace("player_declined", "你示意另有事务，对方便不再挽留。");
            return Finish("你婉拒了这次来访，双方各自离去。", "你向对方示意，暂不便交谈。") with { ActionName = "告辞" };
        }
        if (choice == "brief")
        {
            return new("你们闲谈片刻，各自散去。", "你们略作寒暄。", factors);
        }
        var hostile = CharacterRelationshipRules.IsHostile(npc, CharacterRelationshipRules.PlayerCharacterId);
        if (hostile)
        {
            Trace("hostile_veto", "提及姓名时，对方只略一拱手，没有继续交谈。");
            return Finish("对方无意继续交谈，双方就此别过。", "对方没有回应你的接近。");
        }
        Trace("player_accepted", "见你应下，对方接着说起了来意。");
        Trace("no_hostile_veto", "双方无既有敌对关系。", false);
        var sameSect = !string.IsNullOrEmpty(player.sect_affiliation_id) && npc.faction_id == player.sect_affiliation_id;
        var heard = HeardTrueDisciple(npc);
        var cautious = NpcPersonalityRules.Is(npc, "risk", "谨慎");
        if (proactive)
        {
            if (NpcPersonalityRules.Is(npc, "sociability", "外向"))
                Trace("outgoing_visit", "对方先向你问候，几句话便接上话头。");
            if (known && NpcPersonalityRules.Is(npc, "attachment", "重情"))
                Trace("attached_visit", "对方提起上次相遇的事，接着问了你的近况。");
            var visitPubliclyKnown = heard && sameSect && (player.sect_current_rank is "真传" or "真传弟子");
            if (NpcPersonalityRules.HasVisitPriority(npc, visitPubliclyKnown))
            {
                if (visitPubliclyKnown && NpcPersonalityRules.Values(npc, "声名"))
                    Trace("renown_visit", "对方提起听闻你拜入真传，随即向你问礼。");
                else if (visitPubliclyKnown && NpcPersonalityRules.Values(npc, "宗门"))
                    Trace("sect_visit", "对方谈起同门近况，接着问了几句你在峰中的日常。");
                else Trace("friend_visit", "对方先问起你近来的修行与起居。");
            }
        }
        if (known)
        {
            Trace("known_name", "对方认出你来，话头自然转到近况上。");
            if (cautious) Trace("known_overcomes_caution", "对方少了几分客套，先问起你近来的情况。");
        }
        else
        {
            // 这些条件直接决定陌生接触是否通过谨慎门槛；展示与分支共用事实。
            if (sameSect) Trace("same_sect", "听你提起同门身份，对方先报了自己的峰头。");
            if (heard) Trace("heard_rank", "听到你的名字，对方提起了宗内流传的入门消息。");
            if (cautious && !sameSect && !heard)
            {
                Trace("no_familiar_basis", "彼此素无往来，你提及姓名后，对方仍未留下自己的姓名。");
                Trace("caution_veto", "谈到姓名时，对方有所保留，只拱手告辞。");
                return Finish("对方婉拒结识，未交换姓名。", "对方只回了礼，没有留下姓名。");
            }
            if (cautious) Trace("basis_overcomes_caution", "听完你的来历，对方也报上了姓名。");
            else Trace("no_caution_veto", "对方坦然报上姓名，愿与你认识一番。");
        }
        return Finish(known ? "你们聊了聊近况。" : "你们互通姓名，闲谈了几句。",
            known ? "你们聊起了各自的近况。" : "你们互通了姓名。", true);
    }
    public static Response Remember(WorldNpcData npc, HumanData player, string choice, string place, int month, Response response, string eventId = "")
    {
        var relationshipEventId = string.IsNullOrWhiteSpace(eventId) ? Guid.NewGuid().ToString("N") : eventId;
        if (choice == "introduce" && response.ExchangesName)
        {
            if (!KnowsName(npc)) npc.player_social_memory["first_place"] = place;
            npc.player_social_memory["player_name"] = player.entity_name;
            NpcPersonalityRules.Observe(npc, "name", "姓名：" + npc.npc_name, "互通姓名", month);
        }
        // 记录实际表现，不把真实六轴或个人所重直接复制给玩家。
        foreach (var step in response.DecisionSteps)
        {
            if (step.Rule == "caution_veto")
                NpcPersonalityRules.Observe(npc, "stranger_contact", "交际印象：对陌生接触有所保留，尚不能确定其一贯性情", "此次结识被婉拒", month);
            if (step.Rule == "outgoing_visit")
                NpcPersonalityRules.Observe(npc, "proactive_contact", "交际经历：曾主动前来与你接触", "亲历来访", month);
        }
        npc.player_social_memory["last_choice"] = choice;
        npc.player_social_memory["last_month"] = month;
        if (choice == "introduce" && response.ExchangesName)
        {
            var recorded = CharacterRelationshipRules.RecordConversation(player, npc, relationshipEventId, month);
            var feedback = recorded
                ? CharacterRelationshipRules.DescribePlayerEvidence(player, npc.npc_id, relationshipEventId,
                    "亲近记事：本月已有一次成功交谈记录，本次未重复增加。")
                : "亲近记事：本月已有一次成功交谈记录，本次未重复增加。";
            response = response with { RelationshipFeedback = feedback };
        }
        else
        {
            var feedback = choice == "introduce"
                ? "亲近：未能互通姓名，本次没有增加亲近。"
                : "关系：本次互动没有形成新的关系记事。";
            response = response with { RelationshipFeedback = feedback };
        }
        var outcome = response.Result + (string.IsNullOrWhiteSpace(response.RelationshipFeedback) ? "" : "\n" + response.RelationshipFeedback);
        npc.record(new NpcActionRecord { month_index = month, date_label = $"{month / 12 + 1}年{month % 12 + 1}月", action_id = "player_social_" + choice, action_name = choice.StartsWith("spar_") ? "切磋邀约" : "人物交谈", node_id = npc.current_node_id, subscene_id = npc.current_subscene_id, target_npc_id = "player", reason = "在场相遇后由双方实际回应。", outcome = outcome,
            player_contact_completed = choice == "introduce" && response.ExchangesName });
        return response;
    }
}
