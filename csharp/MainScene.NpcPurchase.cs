using System;
using System.Linq;
using Godot;

public partial class MainScene
{
    private bool can_npc_purchase(WorldNpcData npc) => _npc_world?.HasPendingSettlement == false &&
        !CharacterRelationshipRules.AvoidsContact(npc, CharacterRelationshipRules.PlayerCharacterId) &&
        _npc_world.get_npc(npc.npc_id) == npc && can_contact_social_npc(npc) && NpcPurchaseRules.Offer(npc) is not null;

    private void request_npc_purchase(WorldNpcData npc, bool proactive = false)
    {
        if (player is null || story_dialogue is null || !can_npc_purchase(npc)) return;
        var request = NpcPurchaseRules.Offer(npc)!;
        var requestId = request.request_id;
        var name = NpcPurchaseRules.Item(request.item_path)!.entity_name;
        info?.hide_panel(); npc_interactions?.close();
        var answered = false;
        void Answer(bool sell)
        {
            if (answered) return;
            answered = true;
            if (!sell)
            {
                story_dialogue.show_social_result(npc, new("你没有应下这笔买卖，对方便暂且作罢。", "", [])
                    { ActionName = "结束商议", RelationshipFeedback = "信任：未发生交易，本次没有新增关系记事。" });
                return;
            }
            if (!can_npc_purchase(npc))
            { story_dialogue.show_social_result(npc, new("对方已离开，或已不再需要这份药材。双方未交换财物。", "", [])
                { RelationshipFeedback = "信任：未发生交易，本次没有新增关系记事。" }); return; }
            var month = get_game_month_index(GetNode<GameTimeMgr>("/root/GameTimeManager"));
            if (!player.try_fulfil_npc_purchase(npc, requestId, month, out var failure))
            { story_dialogue.show_social_result(npc, new(failure, "", [])
                { RelationshipFeedback = "信任：未完成交易，本次没有新增关系记事。" }); return; }
            var relationshipFeedback = "信任：这笔成交未进入公平估值范围，关系不变。";
            if (player.entity_data is HumanData playerData)
            {
                var item = NpcPurchaseRules.Item(request.item_path);
                var stones = NpcPurchaseRules.Item(NpcPurchaseRules.StonePath);
                var fair = CharacterRelationshipRules.IsFairTrade(
                    TradeValueRules.Estimate(item, request.quantity),
                    TradeValueRules.Estimate(stones, request.quantity * request.unit_price));
                if (fair)
                {
                    var eventId = "npc-purchase:" + requestId;
                    CharacterRelationshipRules.RecordFairTrade(playerData, npc, eventId, month);
                    relationshipFeedback = CharacterRelationshipRules.DescribePlayerEvidence(playerData, npc.npc_id, eventId,
                        "信任：本次成交关系证据已处理。");
                }
            }
            var purchaseRecord = npc.action_history.LastOrDefault(record => record.action_id == "purchase_completed" && record.reason == requestId);
            if (purchaseRecord is not null)
            {
                purchaseRecord.date_label = $"{month / 12 + 1}年{month % 12 + 1}月";
                purchaseRecord.outcome += "\n" + relationshipFeedback;
            }
            mark_save_dirty();
            NpcSocialRules.DecisionStep[] steps = [
                new("purchase_supplied", "你拿出了所需药材，对方不再另寻卖家。"),
                new("purchase_paid", "双方依照谈妥的价钱交割，药材与灵石各归其主。") ];
            story_dialogue.show_social_result(npc, new($"你交付{name} ×{request.quantity}，收下灵石 {request.quantity * request.unit_price}。", "",
                new[] { steps[0].Text, steps[1].Text })
                { ActionName = "交易", StaminaSpent = WorldStaminaRules.InteractionCost, DecisionSteps = steps, RelationshipFeedback = relationshipFeedback });
        }
        var opening = NpcPlayerImpressionRules.HerbTradeContact(npc)
            ? "对方说道：『前次与你做过药材买卖，这回想再问问。』\n" : "";
        story_dialogue.show_social_choices(player, npc,
            opening + $"对方想补充随身药材，愿以灵石 {request.unit_price} 枚求购{name} ×{request.quantity}。",
            new (string, Action)[] {
                ($"出售 · 获得灵石 {request.quantity * request.unit_price} · 体力 {WorldStaminaRules.InteractionCost:0.#}", () => Answer(true)),
                (proactive ? "婉拒" : "取消", () => Answer(false)) }, () => Answer(false), "求购药材", proactive ? "右键婉拒" : "右键取消");
    }
}
