using System;

/// <summary>当前三种已开放丹种的五档固定药效，首轮平衡参数。</summary>
public static class AlchemyProductCatalog
{
    public static readonly int[] Healing = [12, 18, 29, 43, 62];
    public static readonly int[] ManaPercent = [5, 8, 13, 20, 28];
    public static readonly int[] Calm = [1, 2, 4, 6, 8];
    public static bool Has(string id) => id is "huichun" or "tongmai" or "jingxin";
    /// <summary>已登记定义保留供旧存档读取；当前仅开放回春丹。</summary>
    public static bool IsEnabled(string id) => id == "huichun";
    public static bool IsItemEnabled(ItemData? item) => item is not null &&
        (item is not PillItemData pill || IsEnabled(pill.pill_id));
    public static string Path(string id, int tier, string containerMaterialId = PillContainerCatalog.Porcelain) =>
        containerMaterialId == PillContainerCatalog.Porcelain
            ? $"res://resources/entities/items/alchemy/{id}_{tier}.tres"
            : $"res://resources/entities/items/alchemy/{id}_{tier}_{containerMaterialId}.tres";
    public static string EffectText(string id, int tier)
    {
        tier = Math.Clamp(tier, 0, 4);
        return id switch
        {
            "huichun" => $"恢复{Healing[tier]}点生命值。",
            "tongmai" => $"恢复最大法力的{ManaPercent[tier]}%（向下取整），不超过法力上限；不恢复大世界精力。",
            "jingxin" => $"心境低于0时恢复{Calm[tier]}点，最高恢复至0；不能刷取正向心境。",
            _ => "未知丹药"
        };
    }
}
