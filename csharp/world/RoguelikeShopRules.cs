using Godot;
using System;
using System.Collections.Generic;
using D = Godot.Collections.Dictionary;

/// <summary>Independent, finite spirit-stone economy for the roguelike run.</summary>
public static class RoguelikeShopRules
{
    public const string StonePath = "res://resources/entities/items/灵石.tres";
    public const string QingheOfferPath = "res://resources/entities/equipment/清和剑.tres";
    public const int QingheSwordPrice = 36;
    private static readonly int[] PillPricesByTier = [6, 9, 13, 17, 22];

    public sealed record Offer(string ItemPath, int Quantity, int UnitPrice, ItemData Item);

    public static IReadOnlyList<(string ItemPath, int Quantity)> OffersForLayer(int layer)
    {
        var result = new List<(string, int)>();
        void AddPill(string id, int tier, int quantity = 1) => result.Add((AlchemyProductCatalog.Path(id, tier), quantity));
        switch (layer)
        {
            case 4:
                AddPill("huichun", 0, 2); AddPill("fanyuan", 0);
                break;
            case 10:
                AddPill("ninggang", 0); AddPill("qingshen", 0); AddPill("jiegu", 0);
                break;
            case 16:
                AddPill("huichun", 1, 2); AddPill("fanyuan", 1); AddPill("hezhong", 1);
                break;
            case 22:
                AddPill("huichun", 2, 2); AddPill("ninggang", 2); result.Add((QingheOfferPath, 1));
                break;
            case 28:
                AddPill("huichun", 3); AddPill("fanyuan", 2); AddPill("qingshen", 3);
                break;
        }
        return result;
    }

    public static int BuybackBudgetForLayer(int layer) => layer switch
    {
        4 => 12,
        10 => 16,
        16 => 20,
        22 => 24,
        28 => 30,
        _ => 0,
    };

    public static bool IsSellableMaterial(ItemData? item, string path)
        => item is not null && path != StonePath && item.item_category == "材料" &&
           (item.craft_data is MaterialCraftData || item.entity_name is "兽骨" or "兽皮");

    public static int SellUnitPrice(ItemData item) => item.entity_name switch
    {
        "雾铃花" => 3,
        "玄铁" or "沉云玉屑" or "清音玉片" => 4,
        "赤纹燧石" or "地脉焰炭" or "铜髓矿" or "风窍晶屑" or "兽骨" or "兽皮" => 3,
        _ => 2,
    };

    public static int BuyUnitPrice(ItemData item) => item switch
    {
        PillItemData pill when pill.efficacy_tier is >= 0 and <= 4 && AlchemyProductCatalog.IsEnabled(pill.pill_id)
            => PillPricesByTier[pill.efficacy_tier],
        SectArtifactOfferData offer when offer.ResourcePath == QingheOfferPath => QingheSwordPrice,
        _ => 0,
    };

    public static List<Offer> GetOffers(D instance, string nodeId)
    {
        var layer = DynamicDungeonExpedition.RoguelikeLayerForNodeId(nodeId);
        var state = EnsureShopState(instance, nodeId, layer);
        var stock = state.GetValueOrDefault("stock", new D()).AsGodotDictionary();
        var result = new List<Offer>();
        foreach (var (path, initialQuantity) in OffersForLayer(layer))
        {
            var item = ResourceLoader.Load<ItemData>(path);
            var quantity = stock.GetValueOrDefault(path, initialQuantity).AsInt32();
            var price = item is null ? 0 : BuyUnitPrice(item);
            if (item is not null && quantity > 0 && price > 0) result.Add(new(path, quantity, price, item));
        }
        return result;
    }

    public static int GetBuybackRemaining(D instance, string nodeId)
    {
        var layer = DynamicDungeonExpedition.RoguelikeLayerForNodeId(nodeId);
        return EnsureShopState(instance, nodeId, layer).GetValueOrDefault("buyback_remaining", BuybackBudgetForLayer(layer)).AsInt32();
    }

    public static bool TrySell(Character player, D instance, string nodeId, string itemPath, int quantity, out string message)
    {
        message = "材料未能售出。";
        var layer = DynamicDungeonExpedition.RoguelikeLayerForNodeId(nodeId);
        if (!DynamicDungeonExpedition.RoguelikeShopLayers.Contains(layer) || quantity <= 0 || !ResourceLoader.Exists(itemPath) ||
            ResourceLoader.Load<ItemData>(itemPath) is not { } item || !IsSellableMaterial(item, itemPath))
        { message = "商栈只收本局持有的材料。"; return false; }
        var unit = SellUnitPrice(item);
        var total = (long)unit * quantity;
        var state = EnsureShopState(instance, nodeId, layer);
        var budget = state.GetValueOrDefault("buyback_remaining", BuybackBudgetForLayer(layer)).AsInt32();
        if (player.get_inventory_total_count(itemPath) < quantity)
        { message = "材料数量已变化，请重新选择。"; return false; }
        if (total <= 0 || total > budget || total > int.MaxValue)
        { message = $"本栈最多还收购价值 {budget} 枚灵石的材料。"; return false; }
        if (!player.try_exchange_inventory(new Dictionary<string, int> { [itemPath] = quantity },
                new Dictionary<string, int> { [StonePath] = (int)total }))
        { message = "行囊容量或材料状态有变，交易未提交。"; return false; }
        state["buyback_remaining"] = budget - (int)total;
        message = $"售出{item.entity_name} ×{quantity}，获得灵石 ×{total}。";
        return true;
    }

    public static bool TryBuy(Character player, D instance, string nodeId, string itemPath, int quantity, out string message)
    {
        message = "购买未能完成。";
        var layer = DynamicDungeonExpedition.RoguelikeLayerForNodeId(nodeId);
        if (!DynamicDungeonExpedition.RoguelikeShopLayers.Contains(layer) || quantity <= 0 ||
            !OffersForLayer(layer).Any(offer => offer.ItemPath == itemPath)) return false;
        var state = EnsureShopState(instance, nodeId, layer);
        var stock = state.GetValueOrDefault("stock", new D()).AsGodotDictionary();
        var item = ResourceLoader.Load<ItemData>(itemPath);
        var price = item is null ? 0 : BuyUnitPrice(item);
        if (item is null || price <= 0)
        { message = "此商品不在本栈货单中。"; return false; }
        if (item is SectArtifactOfferData && quantity != 1)
        { message = "法器每栈限购一件。"; return false; }
        var remaining = stock.GetValueOrDefault(itemPath, 0).AsInt32();
        if (remaining < quantity) { message = "本栈库存不足。"; return false; }
        var total = (long)price * quantity;
        if (total > int.MaxValue || player.get_inventory_total_count(StonePath) < total)
        { message = $"灵石不足；本次需要 {total} 枚。"; return false; }

        var outgoing = new Dictionary<string, int> { [StonePath] = (int)total };
        bool success;
        if (item is SectArtifactOfferData artifactOffer)
        {
            ArtifactInstanceData artifact;
            try { artifact = artifactOffer.CreateArtifact(); }
            catch (Exception error) { message = "法器暂不可取：" + error.Message; return false; }
            success = player.try_exchange_inventory_for_artifact(outgoing, artifact);
        }
        else
        {
            if (item is not PillItemData pill || !AlchemyProductCatalog.IsEnabled(pill.pill_id))
            { message = "本栈只售现有丹药和法器。"; return false; }
            success = player.try_exchange_inventory(outgoing, new Dictionary<string, int> { [itemPath] = quantity });
        }
        if (!success) { message = "灵石或行囊容量不足，交易未提交。"; return false; }
        stock[itemPath] = remaining - quantity;
        state["stock"] = stock;
        var product = item is SectArtifactOfferData ? "法器" : "丹药";
        message = $"购得{item.entity_name} ×{quantity}，支付灵石 ×{total}。" + (product == "法器" ? "法器实例已放入本局行囊。" : "");
        return true;
    }

    private static D EnsureShopState(D instance, string nodeId, int layer)
    {
        var shops = instance.GetValueOrDefault("roguelike_shop_state", new D()).AsGodotDictionary();
        if (!shops.ContainsKey(nodeId) || shops[nodeId].VariantType != Variant.Type.Dictionary)
        {
            var stock = new D();
            foreach (var (path, quantity) in OffersForLayer(layer)) stock[path] = quantity;
            shops[nodeId] = new D { ["stock"] = stock, ["buyback_remaining"] = BuybackBudgetForLayer(layer) };
            instance["roguelike_shop_state"] = shops;
        }
        else instance["roguelike_shop_state"] = shops;
        return shops[nodeId].AsGodotDictionary();
    }
}
