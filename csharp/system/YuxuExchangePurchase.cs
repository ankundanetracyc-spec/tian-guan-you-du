using Godot;

/// <summary>Shared live redemption: validate the displayed price, plan placement, then charge and commit once.</summary>
public static class YuxuExchangePurchase
{
    public sealed record Result(bool Success, long Balance, string Message);
    public static Result Purchase(Character? player, string itemPath, int requestedCost, string occurredAt)
    {
        if (player?.entity_data is not HumanData human || human.sect_affiliation_id != "yuxu_xianzong")
            return new(false, 0, "只有玉虚仙宗成员可以使用善功堂。");
        long Balance() => Math.Max(0L, human.get_yuxu_contribution_total());
        Result Fail(string reason) => new(false, Balance(), reason);
        var item = !string.IsNullOrWhiteSpace(itemPath) && ResourceLoader.Exists(itemPath) ? GD.Load<ItemData>(itemPath) : null;
        if (item is null || !YuxuExchangeCatalog.IsAvailable(item))
            return Fail("这件物品不在善功堂现行目录中，贡献未扣除。");
        int cost = YuxuExchangeCatalog.Price(item);
        if (cost < 0 || requestedCost != cost)
            return Fail("物品标准价已变化，请重新打开善功堂确认，贡献未扣除。");
        if (human.get_yuxu_contribution_total() < cost)
            return Fail($"贡献不足；需要 {cost} 点，当前为 {Balance()} 点。");

        ArtifactInstanceData? artifact = null;
        if (item is SectArtifactOfferData offer)
        {
            try { artifact = offer.CreateArtifact(); }
            catch (Exception error) { return Fail(error.Message); }
        }
        bool chargeFailed = false;
        bool Charge()
        {
            if (human.sect_affiliation_id != "yuxu_xianzong" || human.get_yuxu_contribution_total() < cost)
            { chargeFailed = true; return false; }
            if (cost == 0) return true;
            var record = new YuxuSectContributionRecord
            {
                record_id = Guid.NewGuid().ToString("N"),
                source_transaction_id = $"yuxu:exchange:{Guid.NewGuid():N}",
                transaction_kind = YuxuSectContributionRecord.KindExchangeSpend,
                source_title = $"善功堂兑换·{item.entity_name}",
                occurred_at = string.IsNullOrWhiteSpace(occurredAt) ? "游戏时间未载" : occurredAt,
                contribution_delta = -cost,
                cost_summary = $"支出 {cost} 点贡献；未消耗灵石。",
                result_summary = $"{item.entity_name} ×1 已放入行囊；累计公事履职贡献不变。"
            };
            chargeFailed = !human.record_yuxu_contribution(record);
            return !chargeFailed;
        }
        bool success = artifact is not null
            ? player.try_add_artifact_to_inventory(artifact, Charge)
            : player.try_exchange_inventory(new Dictionary<string, int>(), new Dictionary<string, int> { [itemPath] = 1 }, beforeCommit: Charge);
        if (!success)
            return Fail(chargeFailed ? "贡献流水未能登记，兑换未提交且未扣除贡献。" : "行囊容量不足，物品未兑换，贡献未扣除。");
        if (artifact is null) player.publish_item_acquired_notice(itemPath);
        var name = item.item_category == "秘籍" ? $"《{item.entity_name}》" : item.entity_name;
        return new(true, Balance(), cost == 0
            ? $"{name} ×1 已放入行囊。本次测试兑换为0贡献，不产生贡献收支流水。"
            : $"{name} ×1 已放入行囊，扣除 {cost} 点贡献。累计公事履职记录不变。");
    }
}
