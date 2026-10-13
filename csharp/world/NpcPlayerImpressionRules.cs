using System;
using System.Collections.Generic;

public static class NpcPlayerImpressionRules
{
    public static bool IsEvidence(NpcActionRecord? entry) => entry is not null && entry.month_index >= 0 &&
        entry.target_npc_id == "player" && !entry.is_death_record && (entry.action_id switch {
            "player_social_introduce" => entry.player_contact_completed,
            "sect_spar_result" or "purchase_completed" => true,
            _ => false });

    private static NpcPlayerImpressionData Initialize(WorldNpcData npc)
    {
        var state = new NpcPlayerImpressionData();
        // 旧档首次迁移一次；以后只由 record 入队，不反复扫描人物历史。
        foreach (var record in npc.action_history)
            if (IsEvidence(record)) state.pending.Add(record);
        npc.player_impression = state;
        return state;
    }

    public static void OnRecorded(WorldNpcData npc, NpcActionRecord entry)
    {
        if (!IsEvidence(entry)) return;
        if (npc.player_impression is null) { Initialize(npc); return; }
        npc.player_impression.pending.Add(entry);
    }

    /// <returns>本次实际检查的待处理证据数，便于验证增量处理。</returns>
    public static int SettleMonth(WorldNpcData npc, int month)
    {
        if (month < 0) return 0;
        if (npc.player_impression is null && npc.action_history.Count == 0) return 0;
        var state = npc.player_impression ?? Initialize(npc);
        if (state.pending.Count == 0 || state.last_processed_month >= month) return 0;
        state.last_processed_month = month;
        var inspected = state.pending.Count;
        var owned = new HashSet<NpcActionRecord>(npc.action_history);
        var future = new Godot.Collections.Array<NpcActionRecord>();
        for (var index = state.pending.Count - 1; index >= 0; index--)
        {
            var entry = state.pending[index];
            if (!IsEvidence(entry) || !owned.Contains(entry)) continue;
            if (entry.month_index >= month) { future.Add(entry); continue; } // 本月行为必须等下次过月。
            switch (entry.action_id)
            {
                case "player_social_introduce":
                    if (state.contact_evidence.Count < 2)
                    {
                        var sameMonth = false;
                        foreach (var existing in state.contact_evidence)
                            if (existing.month_index == entry.month_index) { sameMonth = true; break; }
                        if (!sameMonth) state.contact_evidence.Add(entry);
                    }
                    break;
                case "sect_spar_result":
                    if (state.spar_evidence is null || entry.month_index > state.spar_evidence.month_index)
                        state.spar_evidence = entry;
                    break;
                case "purchase_completed":
                    if (state.purchase_evidence is null || entry.month_index > state.purchase_evidence.month_index)
                        state.purchase_evidence = entry;
                    break;
            }
        }
        future.Reverse();
        state.pending = future;
        return inspected;
    }

    private static bool Valid(WorldNpcData npc, NpcActionRecord? entry) => IsEvidence(entry) &&
        npc.player_impression is { } state && entry!.month_index < state.last_processed_month && npc.action_history.Contains(entry);

    public static bool WillingContact(WorldNpcData npc) => npc.player_impression is { } state &&
        state.contact_evidence.Count == 2 && state.contact_evidence[0].month_index != state.contact_evidence[1].month_index &&
        state.contact_evidence[0].action_id == "player_social_introduce" && state.contact_evidence[1].action_id == "player_social_introduce" &&
        Valid(npc, state.contact_evidence[0]) && Valid(npc, state.contact_evidence[1]);
    public static bool SparPartner(WorldNpcData npc) => npc.player_impression?.spar_evidence?.action_id == "sect_spar_result" && Valid(npc, npc.player_impression.spar_evidence);
    public static bool HerbTradeContact(WorldNpcData npc) => npc.player_impression?.purchase_evidence?.action_id == "purchase_completed" && Valid(npc, npc.player_impression.purchase_evidence);

    public static NpcPersonalRecallRules.Recall? Feedback(WorldNpcData npc)
    {
        // 只有明确说出的评价才给玩家看；需求/性情仍决定话题，不公开完整性格。
        if (SparPartner(npc) && NpcPersonalityRules.Values(npc, "修行"))
            return new(npc.player_impression!.spar_evidence!, "对方说道：『与你交过手，往后修行上不妨再相互印证。』", "spar");
        if (HerbTradeContact(npc) && NpcPurchaseRules.Offer(npc) is not null)
            return new(npc.player_impression!.purchase_evidence!, "对方说道：『与你做过药材买卖，这回缺药，正想问问你。』", "trade");
        if (WillingContact(npc))
            return new(npc.player_impression!.contact_evidence[0], NpcPersonalityRules.Is(npc, "sociability", "内敛")
                ? "对方说道：『几番相遇，都与你聊过。今日可有空？』"
                : "对方笑道：『这几回与你颇聊得来，今日再说说近况？』", "contact");
        return null;
    }
}
