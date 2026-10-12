using Godot;
using System;
using System.Collections.Generic;

/// <summary>首轮药材需求：仅月结算生成；现场报价不全表扫描、不读玩家行囊。</summary>
public static class NpcPurchaseRules
{
    public const string StonePath = "res://resources/entities/items/灵石.tres";
    public const int TestUnitPrice = 2;
    public static readonly string[] HerbPaths = ["res://resources/entities/items/雾铃花.tres", "res://resources/entities/items/苦苔根.tres"];
    private static readonly Dictionary<string, ItemData> Items = new(StringComparer.Ordinal);
    public static ItemData? Item(string path)
    {
        if (Items.TryGetValue(path, out var cached)) return cached;
        if (!ResourceLoader.Exists(path) || ResourceLoader.Load<ItemData>(path) is not { } item ||
            string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path)) return null;
        Items[path] = item;
        return item;
    }
    public static bool Eligible(WorldNpcData npc) => npc.is_alive && !npc.is_yuxu_lineage_staff &&
        npc.faction_id == "yuxu_xianzong" && npc.short_term_goal.Contains("药材", StringComparison.Ordinal);

    public static void SettleMonth(WorldNpcData npc, int month)
    {
        if (month < 0 || npc.purchase_last_processed_month >= month) return;
        npc.purchase_last_processed_month = month;
        if (npc.purchase_request is { state: "completed" } completed && completed.completed_month >= month) return;
        if (npc.purchase_request is { state: "open" } current)
        {
            if (!Eligible(npc) || npc.get_carried_item_count(current.item_path) >= current.quantity)
            {
                current.state = "cancelled";
                current.reason = "原有需求已不再成立。";
            }
            else return; // 旧需求保留原 ID、报价，不因月份或界面刷新重抽。
        }
        if (!Eligible(npc) || npc.get_resource("灵石") < TestUnitPrice || Item(StonePath) is null) return;
        // 稳定选择自身尚缺的药材，不消费世界随机序列、不检查玩家库存。
        var start = 0u;
        foreach (var c in npc.npc_id) start = unchecked(start * 31 + c);
        for (var i = 0; i < HerbPaths.Length; i++)
        {
            var path = HerbPaths[(int)((start + (uint)i) % (uint)HerbPaths.Length)];
            if (npc.get_carried_item_count(path) >= 1 || Item(path) is null) continue;
            npc.purchase_request = new NpcPurchaseRequest {
                request_id = $"npc-purchase:{npc.npc_id}:{month}", item_path = path,
                quantity = 1, unit_price = TestUnitPrice, created_month = month,
                reason = "补充随身药材。" };
            npc.record(new NpcActionRecord { month_index = month, action_id = "purchase_need",
                action_name = "求购药材", node_id = npc.current_node_id, subscene_id = npc.current_subscene_id,
                reason = npc.short_term_goal, outcome = $"需补充{Item(path)!.entity_name}一份，愿付灵石两枚。" });
            return;
        }
    }
    public static NpcPurchaseRequest? Offer(WorldNpcData npc)
    {
        if (!Eligible(npc) || npc.purchase_request is not { state: "open", quantity: 1, unit_price: TestUnitPrice } request ||
            request.request_id.Length == 0 || Array.IndexOf(HerbPaths, request.item_path) < 0 ||
            npc.get_carried_item_count(request.item_path) >= request.quantity || npc.get_resource("灵石") < TestUnitPrice ||
            Item(request.item_path) is null || Item(StonePath) is null) return null;
        return request;
    }
}
