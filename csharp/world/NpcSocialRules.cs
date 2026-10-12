using Godot;
using System;
using System.Collections.Generic;

/// <summary>首版事实型交谈：不新增随机成功率、关系奖励或隐藏性格展示。</summary>
public static class NpcSocialRules
{
    public sealed record Response(string Result, string Speech, IReadOnlyList<string> Factors)
    {
        public float StaminaSpent { get; init; }
        public string ActionName { get; init; } = "交谈";
        public bool ExchangesName { get; init; }
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
        var relation = memory.GetValueOrDefault("relationship", "").AsString();
        if (relation == "enemy") return name.Length > 0 ? name : "你";
        if (relation == "friend" && name.Length > 0) return name;
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
        if (npc.player_social_memory.GetValueOrDefault("relationship", "").AsString() == "enemy")
            return $"{address}，你找我有何事？";
        if (KnowsName(npc)) return $"{npc.npc_name}在此与你相遇，想与你闲谈近况。";
        var sameSect = !string.IsNullOrEmpty(player.sect_affiliation_id) && npc.faction_id == player.sect_affiliation_id;
        if (proactive && sameSect && HeardTrueDisciple(npc)) return $"{npc.npc_name}听闻你拜入真传，趁宗内相遇，希望与你结识。";
        if (proactive && sameSect) return $"{npc.npc_name}在宗内与你相遇，希望与你结识。";
        return $"你准备与{npc.npc_name}结识。";
    }
    public static Response Evaluate(WorldNpcData npc, HumanData player, string choice)
    {
        var known = KnowsName(npc);
        NpcPersonalityRules.Ensure(npc);
        var factors = new List<string>();
        if (choice == "leave")
        {
            return new("你婉拒了这次来访，双方各自离去。", "你向对方示意，暂不便交谈。", factors);
        }
        if (choice == "brief")
        {
            return new($"你与{npc.npc_name}闲谈片刻，各自散去。", "你们略作寒暄。", factors);
        }
        var hostile = npc.player_social_memory.GetValueOrDefault("relationship", "").AsString() == "enemy" || npc.player_relationship < 0;
        if (hostile)
        {
            factors.Add("对方似乎仍对你心存芥蒂……");
            return new($"{npc.npc_name}无意继续交谈，双方就此别过。", "对方没有回应你的接近。", factors);
        }
        var sameSect = !string.IsNullOrEmpty(player.sect_affiliation_id) && npc.faction_id == player.sect_affiliation_id;
        if (known) factors.Add("对方认出了你，停下脚步与你叙旧……");
        else if (HeardTrueDisciple(npc)) factors.Add("对方似乎早已听过你的名号……");
        else factors.Add("对方注意到你的接近……");
        if (!known && !sameSect && !HeardTrueDisciple(npc) && NpcPersonalityRules.Is(npc, "risk", "谨慎"))
        {
            factors.Add("对方与你保持着距离，似乎有所顾虑……");
            return new($"{npc.npc_name}婉拒了结识，未交换姓名。", "对方不愿贸然与陌生人深交。", factors);
        }
        factors.Add(known ? "你们谈起了近况……" : "对方愿意与你互通姓名……");
        return new(known ? $"你与{npc.npc_name}聊了聊近况。" : $"你与{npc.npc_name}互通姓名，闲谈了几句。",
            known ? "你们聊起了各自的近况。" : "你们互通了姓名。", factors) { ExchangesName = true };
    }
    public static void Remember(WorldNpcData npc, HumanData player, string choice, string place, int month, Response response)
    {
        if (choice == "introduce" && response.ExchangesName)
        {
            if (!KnowsName(npc)) npc.player_social_memory["first_place"] = place;
            npc.player_social_memory["player_name"] = player.entity_name;
        }
        npc.player_social_memory["last_choice"] = choice;
        npc.player_social_memory["last_month"] = month;
        npc.record(new NpcActionRecord { month_index = month, action_id = "player_social_" + choice, action_name = "人物交谈", node_id = npc.current_node_id, subscene_id = npc.current_subscene_id, target_npc_id = "player", reason = "在场相遇后由双方实际回应。", outcome = response.Result });
    }
}
