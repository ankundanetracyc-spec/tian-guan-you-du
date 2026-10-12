using Godot;

/// <summary>
/// 首轮宗门玩法验证参数。全部是可调整的测试值，不代表最终经济或晋升定案。
/// </summary>
public static class YuxuSectPrototypeBalance
{
    public const int OfficialStudyDutyReward = 5;
    public const int OfficialConflictVerificationReward = OfficialStudyDutyReward;
    public const int OfficialYaopuDutyReward = OfficialStudyDutyReward;
    public const int OfficialBeastDropSubmissionReward = OfficialStudyDutyReward;
    public const int OfficialBitterMossRootSubmissionReward = 5;
    public const int BitterMossRootSubmissionCount = 2;
    public const int OfficialYaopuCultivationReward = 10;
    public const int YaopuWatchHours = 4;
    public const float YaopuWatchStaminaCost = 2.0f;
    public const int YaopuCultivationStartHours = 1;
    public const float YaopuCultivationStartStaminaCost = 1.0f;
    public const int YaopuCultivationHours = 24;
    public const int YaopuCultivationYieldCount = 4;
    public const int YaopuCultivationSectDeliveryCount = 3;
    public const int EastWoodsPatrolRequiredNodeMoves = 4;
    public const int MaterialExchangeBaseCost = 5;
    public const int MaterialExchangeCostPerGrade = 5;

    public sealed record PromotionRule(string CurrentRank, string TargetRank, string RequiredRealm, long RequiredOfficialService);

    public static int monthly_stipend(string rank) => rank switch
    {
        "杂役" or "杂役弟子" => 1,
        "外门" or "外门弟子" => 3,
        "内门" or "内门弟子" => 8,
        "真传" or "真传弟子" => 20,
        _ => 0
    };

    public static int manual_exchange_cost(string category) => category switch
    {
        "凡俗武学" => 5,
        "剑修" => 10,
        "云息秘籍" => 15,
        _ => 0
    };

    public static string manual_category(ItemData? item)
    {
        if (item?.gongfa is not GongfaResource gongfa)
            return "其他";
        if (gongfa.school_tags.Contains("cloud_breath"))
            return "云息秘籍";
        if (gongfa.school_tags.Contains("sword"))
            return "剑修";
        if (gongfa.school_tags.Contains("fist") || gongfa.school_tags.Contains("movement"))
            return "凡俗武学";
        return "其他";
    }

    public static int manual_exchange_cost(ItemData? item)
        => item is null || item.item_category != "秘籍" ? 0 : manual_exchange_cost(manual_category(item));

    /// <summary>材料按材性推导品阶计价：无品 5 点，每升一整品阶增加 5 点。</summary>
    public static int material_exchange_cost(ItemData? item)
    {
        if (item is null || item.item_category != "材料") return 0;
        var grade = item.craft_data is MaterialCraftData material
            ? material.material_grade_value()
            : item.grade_value;
        grade = Math.Clamp(grade, 0, 27);
        var gradeSteps = (grade + 2) / 3;
        return MaterialExchangeBaseCost + gradeSteps * MaterialExchangeCostPerGrade;
    }

    /// <summary>当前目录商品的标准基础价；未来的局势波动只应调整该基础价的现价报价。</summary>
    public static int exchange_base_cost(ItemData? item) => item?.item_category switch
    {
        "秘籍" => manual_exchange_cost(item),
        "材料" => material_exchange_cost(item),
        _ => 0
    };

    /// <summary>守住目录边界：只兑换有有效图标的现行材料/秘籍，不恢复已废弃占位物。</summary>
    public static bool is_exchangeable_item(ItemData? item)
    {
        if (item is null || item.item_category is not ("材料" or "秘籍")) return false;
        if (string.IsNullOrWhiteSpace(item.entity_name) || string.IsNullOrWhiteSpace(item.definition_id.ToString())) return false;
        if (item.entity_name.Trim() is "木材" or "爆炎符" or "豆麦" or "黄粟" or "回春丹" or "魂玉" or "雷击符" or "灵米粥" or "肉干" or "山麻" or "山薯" or "石材" or "铁矿石" or "妖丹" or "野菜" or "阵台" or "止血草" or "凝血草") return false;
        if (item.definition_id.ToString() is "mat_wood" or "talisman_flame_burst" or "food_doumai" or "crop_huangsu" or "pill_rejuvenation" or "mat_soul_jade" or "talisman_lightning_strike" or "meal_spirit_rice_porridge" or "food_jerky" or "mat_hemp" or "food_shanyu" or "mat_stone" or "mat_iron_ore" or "mat_demon_core" or "food_wild_vegetable" or "tool_array_platform" or "herb_zhixuecao") return false;
        if (item.item_category == "材料" && item.craft_data is not MaterialCraftData) return false;
        if (item.item_category == "秘籍" && item.gongfa is not GongfaResource) return false;
        if (string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path) || GD.Load<Texture2D>(item.icon_path) is null)
            return false;
        if (item.validate_definition().Count > 0 || exchange_base_cost(item) <= 0) return false;
        return true;
    }

    public static PromotionRule? promotion_rule(string rank) => rank switch
    {
        "杂役" or "杂役弟子" => new PromotionRule("杂役", "外门弟子", "云息三层", 10),
        "外门" or "外门弟子" => new PromotionRule("外门弟子", "内门弟子", "云息七层", 20),
        _ => null
    };
}
