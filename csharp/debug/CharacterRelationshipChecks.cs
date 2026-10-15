using Godot;
using System;
using System.Linq;

public static class CharacterRelationshipChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static WorldNpcData Person(string id) => new() { npc_id = id, npc_name = id };

    public static void Run()
    {
        Check(CharacterRelationshipRules.StageIndex(0) == 0 && CharacterRelationshipRules.StageIndex(9) == 0 &&
            CharacterRelationshipRules.StageIndex(10) == 1 && CharacterRelationshipRules.StageIndex(29) == 1 &&
            CharacterRelationshipRules.StageIndex(30) == 2 && CharacterRelationshipRules.StageIndex(54) == 2 &&
            CharacterRelationshipRules.StageIndex(55) == 3 && CharacterRelationshipRules.StageIndex(79) == 3 &&
            CharacterRelationshipRules.StageIndex(80) == 4 && CharacterRelationshipRules.StageIndex(100) == 4,
            "关系阶段边界不符合五档规则");

        var player = new HumanData { mood_value = 7 };
        var npc = Person("relation-npc");
        var chat = new NpcSocialRules.Response("互通姓名，闲谈了几句。", "", []) { ExchangesName = true };
        NpcSocialRules.Remember(npc, player, "introduce", "观复峰", 0, chat, "chat-0");
        NpcSocialRules.Remember(npc, player, "introduce", "观复峰", 0, chat, "chat-0");
        NpcSocialRules.Remember(npc, player, "introduce", "观复峰", 0, chat, "chat-duplicate");
        var playerRelation = CharacterRelationshipRules.Get(player, npc.npc_id)!;
        var npcRelation = CharacterRelationshipRules.Get(npc, CharacterRelationshipRules.PlayerCharacterId)!;
        Check(playerRelation.pending_evidence.Count == 1 && npcRelation.pending_evidence.Count == 1 &&
            playerRelation.closeness == 0 && npcRelation.closeness == 0,
            "普通单次交谈、同一回调重复或同月交谈错误增加亲近");
        Check(CharacterRelationshipRules.SettleMonth(player, 1) == 0 && CharacterRelationshipRules.SettleMonth(npc, 1) == 0 &&
            playerRelation.closeness == 0 && npcRelation.closeness == 0,
            "首次跨月归纳应留下事实但不能把单次交谈视为亲近");
        NpcSocialRules.Remember(npc, player, "introduce", "观复峰", 1, chat, "chat-1");
        Check(CharacterRelationshipRules.SettleMonth(player, 2) == 1 && CharacterRelationshipRules.SettleMonth(npc, 2) == 1 &&
            playerRelation.closeness == 3 && npcRelation.closeness == 3,
            "跨月反复聊成未分别归纳双方亲近");

        var npcOne = Person("relation-pair-one");
        var npcTwo = Person("relation-pair-two");
        CharacterRelationshipRules.RecordConversation(npcOne, npcTwo, "npc-chat-0", 0);
        Check(CharacterRelationshipRules.SettleMonth(npcOne, 1) == 0 && CharacterRelationshipRules.SettleMonth(npcTwo, 1) == 0,
            "NPC 彼此单次交谈提前增加亲近");
        CharacterRelationshipRules.RecordConversation(npcOne, npcTwo, "npc-chat-1", 1);
        CharacterRelationshipRules.SettleMonth(npcOne, 2); CharacterRelationshipRules.SettleMonth(npcTwo, 2);
        Check(CharacterRelationshipRules.Get(npcOne, npcTwo.npc_id)!.closeness == 3 &&
            CharacterRelationshipRules.Get(npcTwo, npcOne.npc_id)!.closeness == 3,
            "NPC 彼此交谈未按两个方向独立归纳");

        var refused = Person("relation-refused");
        NpcSocialRules.Remember(refused, player, "leave", "观复峰", 2,
            new NpcSocialRules.Response("你婉拒了这次来访，双方各自离去。", "", []), "refusal");
        Check(CharacterRelationshipRules.Get(refused, CharacterRelationshipRules.PlayerCharacterId) is null,
            "拒绝邀请错误创建关系变化");

        var fairBuyer = Person("relation-buyer");
        Check(CharacterRelationshipRules.IsFairTrade(100, 100) && CharacterRelationshipRules.IsFairTrade(0, 0) &&
            CharacterRelationshipRules.IsFairTrade(90, 100) && CharacterRelationshipRules.IsFairTrade(110, 100) &&
            !CharacterRelationshipRules.IsFairTrade(89, 100) && !CharacterRelationshipRules.IsFairTrade(111, 100) &&
            !CharacterRelationshipRules.IsFairTrade(3, 0), "公平交易判定边界错误");
        CharacterRelationshipRules.RecordFairTrade(player, fairBuyer, "trade-1", 2);
        CharacterRelationshipRules.RecordFairTrade(player, fairBuyer, "trade-1", 2);
        CharacterRelationshipRules.RecordFairTrade(player, fairBuyer, "trade-2", 2);
        var tradePlayerRelation = CharacterRelationshipRules.Get(player, fairBuyer.npc_id)!;
        var tradeNpcRelation = CharacterRelationshipRules.Get(fairBuyer, CharacterRelationshipRules.PlayerCharacterId)!;
        Check(tradePlayerRelation.trust == 0 && tradeNpcRelation.trust == 0 &&
            CharacterRelationshipRules.SettleMonth(player, 3) == 2 && CharacterRelationshipRules.SettleMonth(fairBuyer, 3) == 2 &&
            tradePlayerRelation.trust == 5 && tradeNpcRelation.trust == 5 &&
            tradePlayerRelation.closeness == 0 && tradePlayerRelation.respect == 0 && tradePlayerRelation.resentment == 0,
            "公平交易没有只增加双方各自的信任，或重复事件未递减／去重");

        var sparPartner = Person("relation-spar");
        CharacterRelationshipRules.RecordCompletedSpar(player, sparPartner, "spar-1", 3);
        CharacterRelationshipRules.RecordCompletedSpar(player, sparPartner, "spar-1", 3);
        var sparPlayerRelation = CharacterRelationshipRules.Get(player, sparPartner.npc_id)!;
        var sparNpcRelation = CharacterRelationshipRules.Get(sparPartner, CharacterRelationshipRules.PlayerCharacterId)!;
        Check(CharacterRelationshipRules.SettleMonth(player, 4) == 1 && CharacterRelationshipRules.SettleMonth(sparPartner, 4) == 1 &&
            sparPlayerRelation.respect == 4 && sparNpcRelation.respect == 4 && sparPlayerRelation.resentment == 0,
            "完成切磋应积累敬重而非怨结，或重复结果重复结算");

        var victim = Person("relation-victim");
        var aggressor = Person("relation-aggressor");
        Check(CharacterRelationshipRules.RecordImmediateGrievance(victim, aggressor.npc_id, "harm-1", 4) &&
            !CharacterRelationshipRules.RecordImmediateGrievance(victim, aggressor.npc_id, "harm-1", 4),
            "明确伤害事件未即时结算或未防重");
        var grievance = CharacterRelationshipRules.Get(victim, aggressor.npc_id)!;
        Check(grievance.resentment == 18 && grievance.closeness == 0 && grievance.trust == 0 && grievance.respect == 0 &&
            CharacterRelationshipRules.Get(aggressor, victim.npc_id) is null,
            "怨结事件被镜像到另一方或合并进其他关系轴");
        CharacterRelationshipRules.RecordImmediateGrievance(victim, aggressor.npc_id, "harm-2", 5);
        Check(grievance.resentment == 30 && CharacterRelationshipRules.AvoidsContact(victim, aggressor.npc_id) &&
            !CharacterRelationshipRules.IsHostile(victim, aggressor.npc_id) &&
            CharacterRelationshipRules.HostilityBias(victim, aggressor.npc_id) == -10,
            "重复伤害递减或怨结行为阈值错误");
        Check(CharacterRelationshipRules.HostilityBias(sparPartner, CharacterRelationshipRules.PlayerCharacterId) == 0,
            "敬重关系错误提高副本敌意");

        var aided = Person("relation-aided");
        CharacterRelationshipRules.RecordImmediateAid(aided, "helper", "aid-1", 5);
        Check(CharacterRelationshipRules.Value(aided, "helper", CharacterRelationshipRules.Trust) == 12 &&
            CharacterRelationshipRules.Get(aided, "helper")!.closeness == 0 &&
            CharacterRelationshipRules.Get(aided, "helper")!.resentment == 0,
            "重大救助没有即时增加单向信任");

        var legacyNpc = Person("relation-legacy");
        legacyNpc.player_relationship = 77;
        legacyNpc.personal_relationships["old-target"] = -45;
        legacyNpc.player_social_memory["relationship"] = "enemy";
        legacyNpc.relation_tags.Add("盟友:old-friend");
        legacyNpc.personal_relationships["old-rival"] = "enemy";
        legacyNpc.personal_relationships["old-ambiguous-score"] = -80;
        legacyNpc.action_history.Add(new NpcActionRecord { action_id = "sect_spar_result", target_npc_id = "player" });
        var nullFieldsNpc = Person("relation-legacy-null-fields");
        nullFieldsNpc.player_social_memory = null!;
        nullFieldsNpc.relation_tags = null!;
        nullFieldsNpc.personal_relationships = null!;
        var oldSave = new PersistentGameSave { format_version = 6, year = 2, month = 3, player_data = new HumanData() };
        oldSave.world_npcs.Add(legacyNpc);
        oldSave.world_npcs.Add(nullFieldsNpc);
        Check(oldSave.migrate_to_current_format() && oldSave.format_version == 7, "v6 到 v7 迁移失败");
        var migratedNpc = oldSave.world_npcs[0];
        Check(CharacterRelationshipRules.LegacyMarker(migratedNpc, CharacterRelationshipRules.PlayerCharacterId) == "enemy" &&
            CharacterRelationshipRules.LegacyMarker(migratedNpc, "old-friend") == "friend" &&
            CharacterRelationshipRules.LegacyMarker(migratedNpc, "old-rival") == "enemy" &&
            CharacterRelationshipRules.LegacyMarker(migratedNpc, "old-ambiguous-score") == "" &&
            CharacterRelationshipRules.IsHostile(migratedNpc, CharacterRelationshipRules.PlayerCharacterId) &&
            CharacterRelationshipRules.Value(migratedNpc, CharacterRelationshipRules.PlayerCharacterId, CharacterRelationshipRules.Closeness) == 0 &&
            CharacterRelationshipRules.Value(migratedNpc, CharacterRelationshipRules.PlayerCharacterId, CharacterRelationshipRules.Resentment) == 0 &&
            migratedNpc.player_relationship == 77 && migratedNpc.action_history.Count == 1,
            "迁移拆分旧分数、丢失明确旧关系或抹掉结构化旧经历");
        Check(oldSave.world_npcs[1].directed_relationships is not null && oldSave.world_npcs[1].player_social_memory is not null &&
            oldSave.world_npcs[1].relation_tags is not null && oldSave.world_npcs[1].personal_relationships is not null,
            "v6 迁移没有补齐缺失的 NPC 关系字段容器");

        CharacterRelationshipRules.Get(player, "relation-deceased");
        Check(CharacterRelationshipRules.RecordDeathFor(player, "relation-deceased", 6, "寿尽") &&
            !CharacterRelationshipRules.RecordDeathFor(player, "relation-deceased", 6, "寿尽") && player.mood_value == 7 &&
            CharacterRelationshipRules.Get(player, "relation-deceased")!.life_events.Count == 1,
            "离世关系接口未留事实记录、重复写入或改变心境");
        var closenessBeforeCalendarPass = playerRelation.closeness;
        Check(CharacterRelationshipRules.SettleMonth(player, 120) == 0 && playerRelation.closeness == closenessBeforeCalendarPass,
            "关系随日历自动衰减或重复归纳");

        const string path = "res://tmp/character_relationship_roundtrip.tres";
        Check(ResourceSaver.Save(player, path) == Error.Ok, "关系数据保存失败");
        var restored = ResourceLoader.Load<HumanData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        var restoredRelation = CharacterRelationshipRules.Get(restored, fairBuyer.npc_id)!;
        Check(restoredRelation.trust == 5 && restoredRelation.category_application_counts.GetValueOrDefault(CharacterRelationshipRules.FairTrade, 0).AsInt32() == 2 &&
            CharacterRelationshipRules.Get(restored, "relation-deceased")!.life_events.Count == 1 &&
            CharacterRelationshipRules.SettleMonth(restored, 120) == 0,
            "关系轴、去重游标、离世事件或月结状态未通过读档往返");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        GD.Print("CHARACTER_RELATIONSHIP PASS: directed axes, monthly evidence, repeat diminishing, immediate harm/aid, legacy v6 migration, save roundtrip, no decay or mood changes");
    }
}
