using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using D = Godot.Collections.Dictionary;

public static partial class YuxuMarketRules
{
    public sealed record Purchase(Offer Offer, int Quantity);

    public static bool TryBuyBatch(Character player, IReadOnlyList<Purchase> purchases, long hour, out string message)
    {
        message = "摊货已变化，请重新选择。";
        if (player.entity_data is not HumanData human || !IsMember(human) || purchases.Count == 0) return false;
        if (purchases.GroupBy(p => (p.Offer.Seller.npc_id, p.Offer.Path)).Any(group => group.Count() != 1)) return false;

        long total = 0;
        var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
        var sellerTotals = new Dictionary<WorldNpcData, long>();
        foreach (var purchase in purchases)
        {
            var offer = purchase.Offer;
            int count = purchase.Quantity;
            int price = ReferencePrice(offer.Path);
            if (count <= 0 || !Available(offer.Seller) || price <= 0 || price != offer.UnitPrice ||
                !offer.Seller.trade_inventory.TryGetValue(offer.Path, out var available) || available.AsInt32() < count) return false;
            long itemCount = (long)incoming.GetValueOrDefault(offer.Path) + count;
            if (itemCount > int.MaxValue) return false;
            incoming[offer.Path] = (int)itemCount;
            long cost = (long)price * count;
            total += cost;
            if (total > int.MaxValue) return false;
            sellerTotals[offer.Seller] = sellerTotals.GetValueOrDefault(offer.Seller) + cost;
        }
        if (sellerTotals.Any(pair => (long)pair.Key.get_resource("灵石") + pair.Value > int.MaxValue)) return false;
        int spend = (int)total;
        if (!player.try_exchange_inventory(new Dictionary<string, int> { [StonePath] = spend }, incoming, beforeCommit: () =>
            {
                foreach (var purchase in purchases)
                {
                    var offer = purchase.Offer;
                    if (!Available(offer.Seller) || ReferencePrice(offer.Path) != offer.UnitPrice ||
                        !offer.Seller.trade_inventory.TryGetValue(offer.Path, out var available) || available.AsInt32() < purchase.Quantity)
                        return false;
                }
                if (sellerTotals.Any(pair => (long)pair.Key.get_resource("灵石") + pair.Value > int.MaxValue)) return false;
                foreach (var purchase in purchases)
                {
                    var offer = purchase.Offer;
                    int count = purchase.Quantity;
                    int cost = offer.UnitPrice * count;
                    int remaining = offer.Seller.trade_inventory[offer.Path].AsInt32() - count;
                    if (remaining == 0) offer.Seller.trade_inventory.Remove(offer.Path);
                    else offer.Seller.trade_inventory[offer.Path] = remaining;
                    offer.Seller.change_resource("灵石", cost);
                    human.yuxu_market_receipts.Add(new D { ["id"] = Guid.NewGuid().ToString("N"), ["npc_id"] = offer.Seller.npc_id,
                        ["item_path"] = offer.Path, ["quantity"] = count, ["unit_price"] = offer.UnitPrice, ["hour"] = hour, ["kind"] = "buy" });
                    offer.Seller.record(new NpcActionRecord { month_index = (int)Math.Max(0, hour / 720), action_id = "yuxu_market_sale",
                        action_name = "坊市交易", node_id = offer.Seller.current_node_id, subscene_id = offer.Seller.current_subscene_id,
                        target_npc_id = "player", reason = "在宗内坊市出售自有货物。",
                        outcome = $"售出{Item(offer.Path)!.entity_name} ×{count}，收取灵石 {cost}。" });
                }
                return true;
            }))
        {
            message = "灵石或行囊容量不足，未能成交。";
            return false;
        }
        message = $"已购得 {purchases.Sum(item => item.Quantity)} 件物品。";
        return true;
    }

    public static bool TryListStorageItem(Character player, string containerId, string instanceId, string path, long now, out string message)
    {
        message = "无法从储物袋上架该物品。";
        int price = ReferencePrice(path);
        if (player.entity_data is not HumanData human || !IsMember(human) || !Active(human, now) || price <= 0) return false;
        var listing = new D { ["id"] = Guid.NewGuid().ToString("N"), ["item_path"] = path,
            ["quantity"] = 1, ["unit_price"] = price, ["listed_hour"] = now };
        if (!player.try_remove_one_market_storage_item(containerId, instanceId, path, () =>
            { human.yuxu_market_listings.Add(listing); return true; })) return false;
        message = "物品已上架。";
        return true;
    }

    public static bool TryWithdrawOne(Character player, string listingId, out string message)
    {
        message = "该物品已无法撤下。";
        if (player.entity_data is not HumanData human || !IsMember(human)) return false;
        var listing = human.yuxu_market_listings.FirstOrDefault(entry => entry.GetValueOrDefault("id", "").AsString() == listingId);
        if (listing is null) return false;
        string path = listing.GetValueOrDefault("item_path", "").AsString();
        int count = listing.GetValueOrDefault("quantity", 0).AsInt32();
        if (count <= 0 || Item(path) is null) return false;
        if (!player.try_exchange_inventory(new Dictionary<string, int>(), new Dictionary<string, int> { [path] = 1 },
            beforeCommit: () =>
            {
                if (!human.yuxu_market_listings.Contains(listing) || listing.GetValueOrDefault("quantity", 0).AsInt32() != count) return false;
                if (count == 1) human.yuxu_market_listings.Remove(listing); else listing["quantity"] = count - 1;
                return true;
            }))
        {
            message = "行囊容量不足，物品仍由摊位保管。";
            return false;
        }
        message = "物品已收回行囊。";
        return true;
    }
}
