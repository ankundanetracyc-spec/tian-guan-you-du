using Godot;
using GArray = Godot.Collections.Array;

/// <summary>善功堂多件兑换。价格、容量与贡献一次性核验并提交。</summary>
public static class YuxuExchangeBatchPurchase
{
    public static YuxuExchangePurchase.Result Purchase(Character? player, GArray selections, string occurredAt)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != "yuxu_xianzong")
            return new(false, 0, "只有玉虚仙宗成员可以使用善功堂。");
        long Balance() => Math.Max(0L, human.get_yuxu_contribution_total());
        YuxuExchangePurchase.Result Fail(string text) => new(false, Balance(), text);
        if (selections.Count == 0) return Fail("请选择物品。");

        var selected = new List<(ItemData Item, int Count, int UnitPrice)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        long cost = 0, quantity = 0;
        foreach (var value in selections)
        {
            if (value.VariantType != Variant.Type.Dictionary) return Fail("兑换内容已变化，请重新选择。");
            var row = value.AsGodotDictionary();
            string path = row.GetValueOrDefault("item_path", "").AsString();
            int count = row.GetValueOrDefault("count", 0).AsInt32();
            int quotedPrice = row.GetValueOrDefault("unit_price", -1).AsInt32();
            if (count <= 0 || count > Character.InventoryCapacity || !seen.Add(path) ||
                !ResourceLoader.Exists(path) || GD.Load<ItemData>(path) is not { } item ||
                !YuxuExchangeCatalog.IsAvailable(item))
                return Fail("兑换内容已变化，请重新选择。");
            int price = YuxuExchangeCatalog.Price(item);
            if (quotedPrice != price) return Fail("物品贡献价已变化，请重新选择。");
            cost += (long)price * count;
            quantity += count;

            if (cost > int.MaxValue || quantity > Character.InventoryCapacity)
                return Fail("所选物品超过行囊容量。");
            selected.Add((item, count, price));
        }
        if (cost > Balance()) return Fail($"贡献不足，还差 {cost - Balance():N0} 点。");

        var ordinary = new Dictionary<string, int>(StringComparer.Ordinal);
        var artifacts = new List<ArtifactInstanceData>();
        try
        {
            foreach (var (item, count, _) in selected)
            {
                if (item is SectArtifactOfferData offer)
                    for (int index = 0; index < count; index++) artifacts.Add(offer.CreateArtifact());
                else ordinary[item.ResourcePath] = count;
            }
        }
        catch (Exception error) { return Fail(error.Message); }

        bool chargeFailed = false;
        bool Charge()
        {
            if (human.sect_affiliation_id != "yuxu_xianzong" || human.get_yuxu_contribution_total() < cost)
            { chargeFailed = true; return false; }
            if (cost == 0) return true;
            string id = Guid.NewGuid().ToString("N");
            var record = new YuxuSectContributionRecord
            {
                record_id = id, source_transaction_id = $"yuxu:exchange:{id}",
                transaction_kind = YuxuSectContributionRecord.KindExchangeSpend,
                source_title = "善功堂兑换",
                occurred_at = string.IsNullOrWhiteSpace(occurredAt) ? "游戏时间未载" : occurredAt,
                contribution_delta = -(int)cost,
                cost_summary = $"支出 {cost} 点贡献。",
                result_summary = string.Join("、", selected.Select(row => $"{row.Item.entity_name} ×{row.Count}")) + " 已放入行囊。"
            };
            chargeFailed = !human.record_yuxu_contribution(record);
            return !chargeFailed;
        }

        if (!player.try_add_exchange_batch(ordinary, artifacts, Charge))
            return Fail(chargeFailed ? "贡献流水未能登记，兑换未提交。" : "行囊容量不足，兑换未提交。");
        return new(true, Balance(), $"已兑换 {quantity} 件物品，消耗 {cost:N0} 点贡献。");
    }
}


