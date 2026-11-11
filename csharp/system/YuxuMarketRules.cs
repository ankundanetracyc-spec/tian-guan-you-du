using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using D = Godot.Collections.Dictionary;

/// <summary>宗内坊市试玩规则；一切交易只消耗既有个人资产。</summary>
public static class YuxuMarketRules
{
    public const string SectId = "yuxu_xianzong";
    public const string NodeId = "yuxu_xianzong_core";
    public const string PeakId = "heguang";
    public const string StonePath = "res://resources/entities/items/灵石.tres";
    public const string PricePath = "res://resources/world/yuxu_market_test_prices_20261107.json";
    public const int Rent = 1;
    public const long LeaseHours = 30L * 24;
    private static D? _prices;

    private static D Prices()
    {
        if (_prices is not null) return _prices;
        var parsed = Json.ParseString(Godot.FileAccess.GetFileAsString(PricePath));
        _prices = parsed.VariantType == Variant.Type.Dictionary ? parsed.AsGodotDictionary() : new D();
        return _prices;
    }
    public static ItemData? Item(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == StonePath || !ResourceLoader.Exists(path) ||
            ResourceLoader.Load<ItemData>(path) is not { } item || item is StorageBagItemData ||
            item.gongfa is not null || item.item_category == "秘籍" ||
            string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) ||
            !GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item)) return null;
        return item;
    }
    public static int ReferencePrice(string path)
    {
        var item = Item(path); if (item is null) return 0;
        var config = Prices();
        var specific = config.GetValueOrDefault("items", new D()).AsGodotDictionary();
        var categories = config.GetValueOrDefault("categories", new D()).AsGodotDictionary();
        var value = specific.ContainsKey(path) ? specific[path].AsInt32()
            : categories.GetValueOrDefault(item.item_category, 0).AsInt32();
        return Math.Max(0, value);
    }
    public static bool IsMember(HumanData? human) => human?.sect_affiliation_id == SectId;
    public static bool Active(HumanData human, long hour) => human.yuxu_market_lease_start_hour >= 0 &&
        human.yuxu_market_lease_start_hour <= hour && hour < human.yuxu_market_lease_end_hour;
    private static bool ActiveForBoundary(HumanData human, long hour) => human.yuxu_market_lease_start_hour >= 0 &&
        human.yuxu_market_lease_start_hour < hour && hour <= human.yuxu_market_lease_end_hour;
    public static bool Available(WorldNpcData? npc) => npc is { is_alive: true, is_traveling: false } &&
        npc.faction_id == SectId && npc.current_node_id == NodeId && npc.current_subscene_id == PeakId &&
        string.IsNullOrEmpty(npc.dungeon_id) && string.IsNullOrEmpty(npc.temporary_party_task_id) &&
        !npc.is_player_travel_companion && npc.current_state != "闭关修行";
    public static string SellerName(WorldNpcData npc) => NpcSocialRules.KnowsName(npc) ? npc.npc_name : "同门";

    public sealed record Offer(WorldNpcData Seller, string Path, int Quantity, int UnitPrice);
    public static List<Offer> Offers(IEnumerable<WorldNpcData> npcs)
    {
        var result = new List<Offer>();
        foreach (var npc in npcs.Where(Available).OrderBy(n => n.npc_id, StringComparer.Ordinal))
        {
            foreach (var key in npc.trade_inventory.Keys)
            {
                var path = key.AsString(); var count = npc.trade_inventory[key].AsInt32(); var price = ReferencePrice(path);
                if (price > 0 && count > 0) result.Add(new(npc, path, count, price));
            }
        }
        return result.OrderBy(o => Item(o.Path)?.entity_name, StringComparer.Ordinal).ThenBy(o => o.Seller.npc_id, StringComparer.Ordinal).ToList();
    }

    public static bool TryRent(Character player, long now, out string message)
    {
        message = "暂时无法租用摊位。";
        if (player.entity_data is not HumanData human || !IsMember(human) || now < 0 || Active(human, now) || now > long.MaxValue - LeaseHours) return false;
        if (!player.try_exchange_inventory(new Dictionary<string, int> { [StonePath] = Rent }, new Dictionary<string, int>(), beforeCommit: () =>
            { human.yuxu_market_lease_start_hour = now; human.yuxu_market_lease_end_hour = now + LeaseHours; return true; }))
        { message = "灵石不足，未能租用摊位。"; return false; }
        message = "已租用摊位，租期一个游戏月。"; return true;
    }

    public static bool TryList(Character player, string path, int quantity, int price, long now, out string message)
    {
        message = "无法上架该物品。";
        if (player.entity_data is not HumanData human || !IsMember(human) || !Active(human, now) ||
            Item(path) is null || quantity <= 0 || price <= 0 || ReferencePrice(path) <= 0) return false;
        var listing = new D { ["id"] = Guid.NewGuid().ToString("N"), ["item_path"] = path,
            ["quantity"] = quantity, ["unit_price"] = price, ["listed_hour"] = now };
        if (!player.try_exchange_inventory(new Dictionary<string, int> { [path] = quantity }, new Dictionary<string, int>(),
            beforeCommit: () => { human.yuxu_market_listings.Add(listing); return true; }))
        { message = "可上架物品不足，或物品正在使用。"; return false; }
        message = "物品已上架。"; return true;
    }

    public static bool TryListFromStorage(Character player, string containerId, IReadOnlyCollection<string> instanceIds,
        string path, int price, long now, out string message)
    {
        message = "无法从储物空间上架该物品。";
        if (player.entity_data is not HumanData human || !IsMember(human) || !Active(human, now) ||
            Item(path) is null || ReferencePrice(path) <= 0 || price <= 0 || instanceIds.Count == 0) return false;
        var entries = player.get_storage_bag_inventory(containerId).Where(e => instanceIds.Contains(e.InstanceId) &&
            e.ItemPath == path && e.Item is not null && player.can_store_spatial_item(e.InstanceId)).ToArray();
        if (entries.Length != instanceIds.Count || entries.Length != instanceIds.Distinct(StringComparer.Ordinal).Count())
        { message = "储物空间中的物品已变化，未能上架。"; return false; }
        var listing = new D { ["id"] = Guid.NewGuid().ToString("N"), ["item_path"] = path,
            ["quantity"] = entries.Length, ["unit_price"] = price, ["listed_hour"] = now };
        if (!player.try_remove_storage_items(containerId, entries.Select(e => e.InstanceId).ToArray(), () =>
            { human.yuxu_market_listings.Add(listing); return true; }))
        { message = "储物空间中的物品已变化，或目前无法安全转入摊位。"; return false; }
        message = "物品已从储物空间上架。"; return true;
    }
    public static bool TryClose(HumanData? human, long now, out string message)
    {
        message = "当前没有租用中的摊位。";
        if (human is null || !IsMember(human) || !Active(human, now)) return false;
        human.yuxu_market_lease_end_hour = now;
        message = "摊位已结束；未售物品与所得仍可领取。"; return true;
    }
    public static bool TryWithdraw(Character player, string listingId, out string message)
    {
        message = "该物品已无法撤下。";
        if (player.entity_data is not HumanData human || !IsMember(human)) return false;
        var listing = human.yuxu_market_listings.FirstOrDefault(x => x.GetValueOrDefault("id", "").AsString() == listingId);
        if (listing is null) return false;
        var path = listing.GetValueOrDefault("item_path", "").AsString(); var count = listing.GetValueOrDefault("quantity", 0).AsInt32();
        if (count <= 0 || Item(path) is null) return false;
        if (!player.try_exchange_inventory(new Dictionary<string, int>(), new Dictionary<string, int> { [path] = count },
            beforeCommit: () => human.yuxu_market_listings.Remove(listing)))
        { message = "行囊容量不足，物品仍由摊位保管。"; return false; }
        message = "物品已收回行囊。"; return true;
    }
    public static bool TryReprice(HumanData? human, string listingId, int price, long now, out string message)
    {
        message = "当前无法修改售价。";
        if (human is null || !IsMember(human) || !Active(human, now) || price <= 0) return false;
        var listing = human.yuxu_market_listings.FirstOrDefault(x => x.GetValueOrDefault("id", "").AsString() == listingId);
        if (listing is null || listing.GetValueOrDefault("quantity", 0).AsInt32() <= 0) return false;
        listing["unit_price"] = price; message = "售价已更新。"; return true;
    }
    public static bool TryClaim(Character player, out string message)
    {
        message = "暂无可领取的灵石。";
        if (player.entity_data is not HumanData human || !IsMember(human) || human.yuxu_market_pending_stones <= 0) return false;
        var amount = human.yuxu_market_pending_stones;
        if (!player.try_exchange_inventory(new Dictionary<string, int>(), new Dictionary<string, int> { [StonePath] = amount },
            beforeCommit: () => { human.yuxu_market_pending_stones = 0; return true; }))
        { message = "行囊容量不足，所得仍由摊位保管。"; return false; }
        message = $"已领取灵石 ×{amount}。"; return true;
    }
    public static bool TryBuy(Character player, WorldNpcData npc, string path, int count, int expectedPrice, long hour, out string message)
    {
        message = "该摊货已无法购买。";
        var price = ReferencePrice(path);
        if (player.entity_data is not HumanData human || !IsMember(human) || !Available(npc) || count <= 0 ||
            price <= 0 || price != expectedPrice ||
            !npc.trade_inventory.TryGetValue(path, out var amount) || amount.AsInt32() < count) return false;
        var costLong = (long)price * count;
        if (costLong > int.MaxValue || (long)npc.get_resource("灵石") + costLong > int.MaxValue) return false;
        if (!player.can_spend_world_stamina(WorldStaminaRules.InteractionCost)) { message = "精力不足，无法完成交易。"; return false; }
        var cost = (int)costLong;
        if (!player.try_exchange_inventory(new Dictionary<string, int> { [StonePath] = cost }, new Dictionary<string, int> { [path] = count },
            beforeCommit: () =>
            {
                if (!player.try_spend_world_stamina(WorldStaminaRules.InteractionCost)) return false;
                int remaining = npc.trade_inventory[path].AsInt32() - count;
                if (remaining == 0) npc.trade_inventory.Remove(path); else npc.trade_inventory[path] = remaining;
                npc.change_resource("灵石", cost);
                human.yuxu_market_receipts.Add(new D { ["id"] = Guid.NewGuid().ToString("N"), ["npc_id"] = npc.npc_id,
                    ["item_path"] = path, ["quantity"] = count, ["unit_price"] = price, ["hour"] = hour, ["kind"] = "buy" });
                npc.record(new NpcActionRecord { month_index = (int)Math.Max(0, hour / 720), action_id = "yuxu_market_sale", action_name = "坊市交易",
                    node_id = npc.current_node_id, subscene_id = npc.current_subscene_id, target_npc_id = "player",
                    reason = "在宗内坊市出售自有货物。", outcome = $"售出{Item(path)!.entity_name} ×{count}，收取灵石 {cost}。" });
                return true;
            })) { message = "灵石或行囊容量不足，未能成交。"; return false; }
        message = $"购得 {Item(path)!.entity_name} ×{count}。"; return true;
    }

    /// <summary>由 NPC 既有工作分支调用；成交取代本月工作，且按真实月界处理租约。</summary>
    public static bool TryAutomaticSale(HumanData? human, WorldNpcData npc, int month)
    {
        long hour = (long)month * 30 * 24;
        if (human is null || !IsMember(human) || month < 0 || !ActiveForBoundary(human, hour) || !Available(npc) ||
            human.yuxu_market_receipts.Any(r => r.GetValueOrDefault("kind", "").AsString() == "sale" &&
                r.GetValueOrDefault("npc_id", "").AsString() == npc.npc_id && r.GetValueOrDefault("month", -1).AsInt32() == month)) return false;
        var request = NpcPurchaseRules.Offer(npc);
        bool generalNeed = npc.short_term_goal.Contains("换取一件有用的物品", StringComparison.Ordinal);
        foreach (var listing in human.yuxu_market_listings)
        {
            var path = listing.GetValueOrDefault("item_path", "").AsString(); var quantity = listing.GetValueOrDefault("quantity", 0).AsInt32();
            var price = listing.GetValueOrDefault("unit_price", 0).AsInt32(); var reference = ReferencePrice(path);
            bool matchesRequest = request is not null && request.item_path == path && price <= request.unit_price;
            bool matchesGeneral = generalNeed && reference > 0 && price <= reference && npc.get_carried_item_count(path) == 0;
            if (listing.GetValueOrDefault("listed_hour", long.MaxValue).AsInt64() >= hour ||
                quantity <= 0 || price <= 0 || Item(path) is null || (!matchesRequest && !matchesGeneral) ||
                npc.get_resource("灵石") < price || (long)human.yuxu_market_pending_stones + price > int.MaxValue ||
                npc.get_carried_item_count(path) == int.MaxValue) continue;
            npc.change_resource("灵石", -price); npc.change_carried_item(path, 1);
            listing["quantity"] = quantity - 1; human.yuxu_market_pending_stones += price;
            if (matchesRequest && request is not null) { request.state = "completed"; request.completed_month = month; }
            human.yuxu_market_receipts.Add(new D { ["id"] = Guid.NewGuid().ToString("N"), ["listing_id"] = listing.GetValueOrDefault("id", ""),
                ["npc_id"] = npc.npc_id, ["item_path"] = path, ["quantity"] = 1, ["unit_price"] = price,
                ["hour"] = hour, ["month"] = month, ["kind"] = "sale" });
            npc.current_state = "坊市交易";
            npc.record(new NpcActionRecord { month_index = month, action_id = "yuxu_market_purchase", action_name = "坊市交易",
                node_id = npc.current_node_id, subscene_id = npc.current_subscene_id, target_npc_id = "player",
                reason = matchesRequest ? "购买本人求购的药材。" : "购买本人需要的物品。",
                outcome = $"购得{Item(path)!.entity_name} ×1，支付灵石 {price}。" });
            return true;
        }
        return false;
    }
}
