using System;

public partial class Character
{
    private bool _npcPurchaseCommitting;

    /// <summary>先在轻量行囊副本上试算，全部通过才提交双方资产；只发一次行囊通知。</summary>
    public bool try_fulfil_npc_purchase(WorldNpcData npc, string requestId, int month, out string failure)
    {
        failure = "这笔求购已无法成交。";
        if (_npcPurchaseCommitting || human is null || NpcPurchaseRules.Offer(npc) is not { } request ||
            request.request_id != requestId || month < request.created_month) return false;
        if (!can_spend_world_stamina(WorldStaminaRules.InteractionCost))
        { failure = "你有些疲惫，暂时无法完成交易。"; return false; }
        ensure_inventory_state();
        if (get_inventory_total_count(request.item_path) < request.quantity || is_item_equipped(request.item_path))
        { failure = "行囊中没有可交付的这份药材，未能成交。"; return false; }
        var stonesLong = (long)request.quantity * request.unit_price;
        if (stonesLong > int.MaxValue) { failure = "灵石数量超出当前存档可记录范围，双方暂未交换物品。"; return false; }
        var stones = (int)stonesLong;
        if (!prepare_spatial_exchange(new System.Collections.Generic.Dictionary<string, int> { [request.item_path] = request.quantity },
            new System.Collections.Generic.Dictionary<string, int> { [NpcPurchaseRules.StonePath] = stones },
            out var paths, out var counts, out var positions, out var placements))
        { failure = "行囊装不下灵石，双方暂未交换物品。"; return false; }
        _npcPurchaseCommitting = true;
        try
        {
            if (!try_spend_world_stamina(WorldStaminaRules.InteractionCost)) { failure = "当前精力不足。"; return false; }
            // 同步提交前无 await、无库存信号；通知观察者时双方已是完成态。
            human.inventory_item_paths = new(paths); human.inventory_item_counts = new(counts); human.inventory_item_positions = new(positions); human.inventory_placements = placements;
            npc.change_resource("灵石", -stones);
            npc.change_carried_item(request.item_path, request.quantity);
            request.state = "completed"; request.completed_month = month;
            npc.player_social_memory["last_month"] = month;
            npc.player_social_memory["last_purchase_request"] = requestId;
            npc.record(new NpcActionRecord { month_index = month, action_id = "purchase_completed", action_name = "求购成交",
                node_id = npc.current_node_id, subscene_id = npc.current_subscene_id, target_npc_id = "player",
                reason = requestId, outcome = $"购得{NpcPurchaseRules.Item(request.item_path)!.entity_name} ×{request.quantity}，支付灵石 {stones}。" });
            NpcPersonalityRules.Observe(npc, "purchase_experience", "曾向你购入药材。", "现场求购成交", month);
            notify_inventory_changed();
            failure = "";
            return true;
        }
        finally { _npcPurchaseCommitting = false; }
    }
}
