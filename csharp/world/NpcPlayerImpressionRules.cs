using System;
using System.Collections.Generic;
using System.Linq;

public static class NpcPlayerImpressionRules
{
    public sealed record PlayerImpressionView(
        string Summary,
        IReadOnlyList<string> Clues,
        string RecentSource,
        string RecentDate,
        string RecentClue);

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

    /// <summary>
    /// Returns only experiences this NPC actually recorded with the player. It is a qualitative
    /// reading aid for the relationship page, not a relationship score or a personality verdict.
    /// </summary>
    public static PlayerImpressionView DescribeForPlayer(WorldNpcData npc)
    {
        var observations = new List<(NpcActionRecord Record, string Source, string Clue, int Index)>();
        for (var index = 0; index < npc.action_history.Count; index++)
        {
            var record = npc.action_history[index];
            if (record is null || record.month_index < 0 || record.target_npc_id != "player" || record.is_death_record) continue;

            switch (record.action_id)
            {
                case "player_social_introduce":
                case "player_social_brief":
                    if (!record.player_contact_completed) continue;
                    observations.Add((record, "当面交谈", "你们有过一次聊成的当面交谈。", index));
                    break;
                case "sect_spar_result":
                    observations.Add((record, "宗内切磋", "你们完成过一次点到为止的切磋。", index));
                    break;
                case "purchase_completed":
                    observations.Add((record, "药材求购", "对方曾向你完成药材求购。", index));
                    break;
                case "player_trade_completed":
                    observations.Add((record, "物品交易", "你们曾完成一次物品交易。", index));
                    break;
                case "banquet_response":
                    if (!IsValidBanquetObservation(record)) continue;
                    observations.Add((record, "宗门宴席", $"对方在席间听到了你的{record.reason}回应：“{record.outcome}”", index));
                    break;
            }
        }

        // Old callbacks or duplicate saves must not make one banquet response appear as several
        // independent observations. The event id is assigned only to a response actually heard.
        observations = observations
            .GroupBy(item => item.Record.action_id == "banquet_response"
                ? "banquet:" + item.Record.dungeon_incident_id
                : item.Record.action_id + ":" + item.Index.ToString(), StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.Index).Last())
            .OrderBy(item => item.Index)
            .ToList();

        var contactReady = WillingContact(npc);
        var sparReady = SparPartner(npc);
        var herbTradeReady = HerbTradeContact(npc);
        var summaries = new List<string>();
        if (contactReady) summaries.Add("愿意继续与你往来");
        if (sparReady) summaries.Add("记得与你有过修行切磋");
        if (herbTradeReady) summaries.Add("记得与你有过药材交易");
        var summary = summaries.Count > 0
            ? string.Join("；", summaries)
            : "尚未形成明确印象";

        // Show a concise set of actual sources rather than guessing at unrecorded traits.
        var clues = new List<string>();
        if (contactReady) clues.Add("不同月份的成功交谈已让对方愿意继续往来。");
        if (sparReady) clues.Add("对方记得与你完成过切磋；这只说明有过修行交流经历。");
        if (herbTradeReady) clues.Add("对方记得曾向你完成药材求购。");
        foreach (var observation in observations.TakeLast(4))
        {
            if (observation.Record.action_id == "banquet_response")
            {
                if (!clues.Any(clue => clue == observation.Clue)) clues.Add(observation.Clue);
                continue;
            }
            if (observation.Record.action_id is "player_social_introduce" or "player_social_brief")
            {
                if (!contactReady && !clues.Any(clue => clue.StartsWith("你们有过一次聊成", StringComparison.Ordinal)))
                    clues.Add(observation.Clue + (observations.Count(item => item.Record.action_id is "player_social_introduce" or "player_social_brief") > 1
                        ? " 其他交谈仍在积累，暂不概括为稳定倾向。"
                        : " 目前只有这次交谈记录。"));
                continue;
            }
            if ((observation.Record.action_id is "purchase_completed" or "player_trade_completed") &&
                !clues.Any(clue => clue.Contains("交易", StringComparison.Ordinal) || clue.Contains("求购", StringComparison.Ordinal)))
                clues.Add(observation.Clue);
            else if (observation.Record.action_id == "sect_spar_result" && !sparReady &&
                !clues.Any(clue => clue.Contains("切磋", StringComparison.Ordinal)))
                clues.Add(observation.Clue);
        }

        if (observations.Count == 0)
            return new(summary, [], "", "", "");

        var recent = observations[^1];
        var recentDate = !string.IsNullOrWhiteSpace(recent.Record.date_label)
            ? recent.Record.date_label
            : $"第{recent.Record.month_index / 12 + 1}年第{recent.Record.month_index % 12 + 1}月";
        var recentClue = recent.Record.action_id == "banquet_response"
            ? $"席间回应（{recent.Record.reason}）：“{recent.Record.outcome}”"
            : recent.Clue;
        return new(summary, clues, recent.Source, recentDate, recentClue);
    }

    private static bool IsValidBanquetObservation(NpcActionRecord record) =>
        record.action_id == "banquet_response" && record.target_npc_id == "player" && record.month_index >= 0 &&
        record.action_name == "宗门宴席回应" && !string.IsNullOrWhiteSpace(record.dungeon_incident_id) &&
        (record.reason is "谦让" or "坦率" or "自信" or "转问") && !string.IsNullOrWhiteSpace(record.outcome);

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
