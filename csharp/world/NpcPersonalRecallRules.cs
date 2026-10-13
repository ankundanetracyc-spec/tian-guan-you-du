using System;

/// <summary>只引用当前 NPC 自己的既有记录，不归纳人格、不改变社交结果。</summary>
public static class NpcPersonalRecallRules
{
    public sealed record Recall(NpcActionRecord Evidence, string Text, string ImpressionKind = "");

    public static NpcSocialRules.Response Attach(WorldNpcData npc, NpcSocialRules.Response response, int month)
    {
        // 拒绝、敌对阻断和取消不借旧事强行继续交谈。
        if (!response.ExchangesName) return response;
        var impression = npc.player_impression?.last_processed_month <= month ? NpcPlayerImpressionRules.Feedback(npc) : null;
        return response with { Recollection = impression ?? Select(npc, month) };
    }

    public static Recall? Select(WorldNpcData npc, int month)
    {
        NpcActionRecord? latest = null;
        for (var index = npc.action_history.Count - 1; index >= 0; index--)
        {
            var entry = npc.action_history[index];
            if (!Eligible(entry) || entry.month_index > month || (latest is not null && entry.month_index <= latest.month_index)) continue;
            latest = entry;
        }
        if (latest is null) return null;
        uint hash = 2166136261;
        foreach (var c in npc.npc_id + ":" + latest.action_id + ":" + latest.month_index)
            hash = unchecked((hash ^ c) * 16777619);
        var text = latest.action_id switch
        {
            "sect_spar_result" => new[] {
                "对方提起旧事：『上回与你交手，不知近来所学可有进境？』",
                "对方说道：『还记得上回那场切磋，近来修行如何？』",
                "对方问道：『上回对练之后，你可又有所得？』",
                "对方提起上回的切磋：『改日有空，再论所学。』" },
            "purchase_completed" => new[] {
                "对方提起旧事：『上回那份药材，是从你这里购得的。』",
                "对方说道：『前次与你做过药材买卖，今日又碰见了。』",
                "对方提起那笔买卖：『还记得上回向你购药的事。』",
                "对方说道：『上回买药时与你打过交道，别来可好？』" },
            _ => new[] {
                "对方说道：『前次你肯停步聊上几句，今日又遇见了。』",
                "对方提起旧事：『上回与你聊过，不知近来如何？』",
                "对方说道：『还记得前次交谈，近来可好？』",
                "对方问道：『自上回聊过之后，你近来过得如何？』" }
        };
        return new(latest, text[hash % (uint)text.Length]);
    }

    public static bool IsValid(WorldNpcData npc, Recall recall) =>
        npc.action_history.Contains(recall.Evidence) && Eligible(recall.Evidence) && (recall.ImpressionKind switch {
            "contact" => NpcPlayerImpressionRules.WillingContact(npc),
            "spar" => NpcPlayerImpressionRules.SparPartner(npc),
            "trade" => NpcPlayerImpressionRules.HerbTradeContact(npc) && NpcPurchaseRules.Offer(npc) is not null,
            "" => true,
            _ => false });

    private static bool Eligible(NpcActionRecord? entry) => entry is not null && entry.month_index >= 0 &&
        entry.target_npc_id == "player" && !entry.is_death_record && (entry.action_id switch
        {
            "player_social_introduce" or "player_social_brief" => entry.player_contact_completed,
            "sect_spar_result" or "purchase_completed" => true,
            _ => false
        });
}
