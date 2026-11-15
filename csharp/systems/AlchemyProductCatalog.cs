using System;

/// <summary>现行丹种的五档固定药效；突破丹数值为首版可调值。</summary>
public static class AlchemyProductCatalog
{
    public static readonly int[] Healing = [30, 50, 80, 120, 160];
    public static readonly int[] Shield = [24, 40, 64, 96, 128];
    public static readonly int[] Defense = [1, 2, 3, 4, 5];
    public static readonly int[] Agility = [4, 7, 10, 14, 18];
    public static readonly int[] Regeneration = [20, 32, 50, 72, 96];
    public static readonly int[] WoundRemoval = [1, 1, 2, 3, 4];
    public static readonly int[] WoundHealing = [8, 16, 24, 36, 48];
    public static readonly int[] SpiritProgress = [1, 2, 4, 7, 8];
    public static readonly int[] WorldEnergy = [8, 12, 18, 26, 32];
    public static readonly int[] ManaPercent = [5, 8, 13, 20, 28];
    public static readonly int[] Calm = [1, 2, 4, 6, 8];
    public static readonly int[] MeridianProgress = [1, 2, 3, 4, 5];
    public static readonly int[] YuanxiDurationReductionPercent = [2, 4, 6, 8, 10];
    public static bool Has(string id) => IsEnabled(id) || id is "tongmai" or "jingxin" or "dingmai" or "guiyuan_dingxi";
    public static int Rank(string id) => id == "guiyuan_dingxi" ? 7 : 9;
    /// <summary>暂停丹种的定义保留供旧存档读取，不随首批扩充重新开放。</summary>
    public static bool IsEnabled(string id) => id is "huichun" or "ninggang" or "jinji" or "qingshen" or "xusheng"
        or "jiegu" or "hezhong" or "naling" or "fanyuan" or "dingmai" or "guiyuan_dingxi";
    public static bool IsBattlePill(string id) => id is "huichun" or "ninggang" or "jinji" or "qingshen"
        or "xusheng" or "jiegu" or "hezhong";
    public static bool IsWorldPill(string id) => id is "huichun" or "naling" or "fanyuan";
    public static string Name(string id) => id switch
    {
        "huichun" => "回春丹", "ninggang" => "凝罡丹", "jinji" => "金肌丹", "qingshen" => "轻身丹",
        "xusheng" => "续生丹", "jiegu" => "接骨丹", "hezhong" => "和中丹", "naling" => "纳灵丹", "fanyuan" => "返元丹",
        "tongmai" => "通脉丹", "jingxin" => "静心丹", "dingmai" => "定脉丹", "guiyuan_dingxi" => "归元定息丹", _ => "未知丹药"
    };
    public static int PrimaryAmount(string id, int tier) => (id switch
    {
        "huichun" => Healing, "ninggang" => Shield, "jinji" => Defense, "qingshen" => Agility,
        "xusheng" => Regeneration, "jiegu" or "hezhong" => WoundRemoval,
        "naling" => SpiritProgress, "fanyuan" => WorldEnergy, _ => new int[5]
    })[Math.Clamp(tier, 0, 4)];
    public static bool IsItemEnabled(ItemData? item) => item is not null &&
        (item is not PillItemData pill || IsEnabled(pill.pill_id));
    public static string Path(string id, int tier, string containerMaterialId = PillContainerCatalog.Porcelain) =>
        containerMaterialId == PillContainerCatalog.Porcelain
            ? (id, tier) switch
            {
                ("dingmai", 0) => Character.YunxiThreeToFourPillPath,
                ("guiyuan_dingxi", 0) => Character.YunxiNineToTenPillPath,
                _ => $"res://resources/entities/items/alchemy/{id}_{tier}.tres"
            }
            : $"res://resources/entities/items/alchemy/{id}_{tier}_{containerMaterialId}.tres";
    public static string EffectText(string id, int tier)
    {
        tier = Math.Clamp(tier, 0, 4);
        return id switch
        {
            "huichun" => $"恢复{Healing[tier]}点生命值。",
            "ninggang" => $"获得{Shield[tier]}点护盾。",
            "jinji" => $"物理防御提高{Defense[tier]}点，持续2回合。",
            "qingshen" => $"获得{Agility[tier]}层身法。",
            "xusheng" => $"后续两个自身回合开始时，各恢复{Regeneration[tier]}点生命值。",
            "jiegu" => $"移除{WoundRemoval[tier]}层外伤，并恢复{WoundHealing[tier]}点生命值。",
            "hezhong" => $"移除{WoundRemoval[tier]}层内伤，并恢复{WoundHealing[tier]}点生命值。",
            "naling" => $"补入当前层{SpiritProgress[tier]}缕灵力，不超过本层门槛。",
            "fanyuan" => $"恢复{WorldEnergy[tier]}点大世界精力。",
            "tongmai" => $"恢复最大法力的{ManaPercent[tier]}%（向下取整），不超过法力上限；不恢复大世界精力。",
            "jingxin" => $"心境低于0时恢复{Calm[tier]}点，最高恢复至0；不能刷取正向心境。",
            "dingmai" => $"成功突破至云息四层后，补入{MeridianProgress[tier]}缕吐纳进度；不溢出、不自动晋层。",
            "guiyuan_dingxi" => $"成功突破至云息十层后，归元九息所需时间缩短{YuanxiDurationReductionPercent[tier]}%。",
            _ => "未知丹药"
        };
    }
}
