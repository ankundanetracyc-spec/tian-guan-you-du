using Godot;
using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

public static class NpcPlayerImpressionChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static NpcActionRecord Chat(int month) => new() {
        month_index = month, action_id = "player_social_introduce", target_npc_id = "player", player_contact_completed = true };
    private static WorldNpcData Person(string id) => new() {
        npc_id = id, faction_id = "yuxu_xianzong", cultivation_realm = "云息四层" };

    public static void Run()
    {
        var npc = Person("impression_contact");
        for (var index = 0; index < 1000; index++) npc.record(Chat(0));
        Check(!NpcPlayerImpressionRules.WillingContact(npc), "即时登记提前形成印象");
        NpcPlayerImpressionRules.SettleMonth(npc, 1);
        Check(npc.player_impression!.contact_evidence.Count == 1 && !NpcPlayerImpressionRules.WillingContact(npc), "当天重复交谈突破跨月门槛");
        npc.record(Chat(1));
        Check(NpcPlayerImpressionRules.SettleMonth(npc, 1) == 0 && !NpcPlayerImpressionRules.WillingContact(npc), "同月重复结算或本月行为提前生效");
        NpcPlayerImpressionRules.SettleMonth(npc, 2);
        Check(NpcPlayerImpressionRules.WillingContact(npc) && npc.player_relationship == 0 &&
            NpcPlayerImpressionRules.SettleMonth(npc, 3) == 0, "两个月聊成未形成观感，或无新经历仍重新归纳");
        npc.social_personality["sociability"] = "内敛";
        var quiet = NpcPlayerImpressionRules.Feedback(npc)!.Text;
        npc.social_personality["sociability"] = "外向";
        Check(NpcPlayerImpressionRules.Feedback(npc)!.Text != quiet, "不同性情没有产生不同外在评价");
        var other = Person("impression_other");
        Check(!NpcPlayerImpressionRules.WillingContact(other) && other.player_impression is null, "个人观感传播给另一 NPC");

        var human = new HumanData { sect_affiliation_id = "yuxu_xianzong", cultivation_realm = "云息三层" };
        var spar = Person("impression_spar");
        spar.social_personality["sociability"] = "内敛";
        spar.social_personality["risk"] = "谨慎";
        spar.social_priorities.Add("修行");
        spar.record(new NpcActionRecord { month_index = 1, action_id = "sect_spar_result", target_npc_id = "player" });
        NpcPlayerImpressionRules.SettleMonth(spar, 1);
        Check(!NpcSparringRules.WantsToInvite(spar, human), "本月交手提前成为再次邀约依据");
        NpcPlayerImpressionRules.SettleMonth(spar, 2);
        Check(NpcSparringRules.WantsToInvite(spar, human) && NpcPersonalityRules.WillVisit(spar, false), "过月交手经历没有参与再次相邀");
        spar.player_social_memory["player_realm"] = "云息十层";
        Check(!NpcSparringRules.WantsToInvite(spar, human), "旧交手印象绕过已知修为差距");
        spar.player_relationship = -1;
        Check(NpcPersonalityRules.WillVisit(spar, false) && NpcSparringRules.WantsToInvite(spar, human), "旧单一关系分错误继续参与敌意判定");
        CharacterRelationshipRules.GetOrCreate(spar, CharacterRelationshipRules.PlayerCharacterId).resentment = 60;
        Check(!NpcPersonalityRules.WillVisit(spar, false) && !NpcSparringRules.WantsToInvite(spar, human), "怨结未阻止来访与切磋邀约");

        var buyer = Person("impression_buyer");
        buyer.social_personality["sociability"] = "内敛";
        buyer.short_term_goal = "补充药材";
        buyer.resources["灵石"] = 2;
        buyer.purchase_request = new NpcPurchaseRequest { request_id = "impression-demand", item_path = NpcPurchaseRules.HerbPaths[0], quantity = 1, unit_price = 2 };
        buyer.record(new NpcActionRecord { month_index = 1, action_id = "purchase_completed", target_npc_id = "player" });
        NpcPlayerImpressionRules.SettleMonth(buyer, 1);
        Check(!NpcPersonalityRules.WillVisit(buyer, false), "旧买卖尚未过月就绕过陌生来访限制");
        NpcPlayerImpressionRules.SettleMonth(buyer, 2);
        Check(NpcPersonalityRules.WillVisit(buyer, false) && NpcPlayerImpressionRules.Feedback(buyer)?.ImpressionKind == "trade", "实际新需求未接上旧买卖印象");
        buyer.resources["灵石"] = 0;
        Check(!NpcPersonalityRules.WillVisit(buyer, false) && NpcPlayerImpressionRules.Feedback(buyer) is null, "旧买卖印象绕过支付能力");
        buyer.resources["灵石"] = 2;
        buyer.carried_items[NpcPurchaseRules.HerbPaths[0]] = 1;
        Check(!NpcPersonalityRules.WillVisit(buyer, false), "药材已足仍因旧印象主动求购");

        var invalid = Person("impression_invalid");
        invalid.record(new NpcActionRecord { month_index = 0, action_id = "player_social_leave", target_npc_id = "player" });
        invalid.record(new NpcActionRecord { month_index = 0, action_id = "player_social_spar_invitation", target_npc_id = "player" });
        invalid.record(new NpcActionRecord { month_index = 0, action_id = "purchase_completed", target_npc_id = "another" });
        invalid.record(new NpcActionRecord { month_index = 0, action_id = "player_social_introduce", target_npc_id = "player", outcome = "聊得很好" });
        NpcPlayerImpressionRules.SettleMonth(invalid, 1);
        Check(invalid.player_impression!.contact_evidence.Count == 0 && !NpcPlayerImpressionRules.SparPartner(invalid) &&
            !NpcPlayerImpressionRules.HerbTradeContact(invalid) && invalid.player_relationship == 0, "婉拒、邀约或模糊旧记录制造印象／惩罚");

        var pending = Person("impression_save");
        pending.record(Chat(0)); pending.record(Chat(1));
        NpcPlayerImpressionRules.SettleMonth(pending, 1);
        const string path = "res://tmp/npc_player_impression_checks.tres";
        Check(ResourceSaver.Save(pending, path) == Error.Ok, "待结算印象保存失败");
        var restored = ResourceLoader.Load<WorldNpcData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        Check(!NpcPlayerImpressionRules.WillingContact(restored), "读档把未过月证据提前归纳");
        NpcPlayerImpressionRules.SettleMonth(restored, 2);
        Check(NpcPlayerImpressionRules.WillingContact(restored) && restored.player_impression!.pending.Count == 0,
            "待结算证据读档后丢失或与历史脱离");
        var copy = (WorldNpcData)restored.Duplicate(true);
        Check(NpcPlayerImpressionRules.WillingContact(copy), "世界存档深拷贝破坏印象证据引用");
        Check(ResourceSaver.Save(restored, path) == Error.Ok, "已归纳印象保存失败");
        var loaded = ResourceLoader.Load<WorldNpcData>(path, cacheMode: ResourceLoader.CacheMode.Ignore)!;
        Check(NpcPlayerImpressionRules.WillingContact(loaded) && NpcPlayerImpressionRules.SettleMonth(loaded, 2) == 0,
            "已归纳印象读档丢失或同月重复结算");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));

        var manager = new NpcWorldManager();
        var resident = Person("impression_month_entry");
        resident.is_yuxu_lineage_staff = true;
        resident.record(Chat(0)); resident.record(Chat(1)); manager.npcs.Add(resident);
        var steps = (System.Collections.Generic.IEnumerable<int>)typeof(NpcWorldManager)
            .GetMethod("simulate_month_steps", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(manager, new object[] { 2 })!;
        foreach (var _ in steps) { }
        Check(NpcPlayerImpressionRules.WillingContact(resident), "世界月度结算入口未接入印象归纳");
        manager.Free();

        var population = Enumerable.Range(0, 580).Select(index => Person("impression_perf_" + index)).ToArray();
        foreach (var active in population.Take(7)) { active.record(Chat(0)); active.record(Chat(1)); }
        var timer = Stopwatch.StartNew();
        var inspected = 0;
        for (var month = 1; month <= 12; month++) foreach (var person in population)
            inspected += NpcPlayerImpressionRules.SettleMonth(person, month);
        timer.Stop();
        Check(inspected == 21 && population.Skip(7).All(person => person.player_impression is null) &&
            population.Take(7).All(NpcPlayerImpressionRules.WillingContact), "印象归纳扫描无经历人口或重复处理已完成证据");
        GD.Print($"NPC_IMPRESSION performance: 580 NPC x12, 7 changed, {inspected} queued checks, {timer.Elapsed.TotalMilliseconds:0.00}ms (data-only)");
        CharacterRelationshipChecks.Run();
    }
}
