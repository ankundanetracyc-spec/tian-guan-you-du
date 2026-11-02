using Godot;
using Godot.Collections;

/// <summary>云息三至四、九至十层的指定突破丹药条件与成功后结算。</summary>
public partial class Character
{
    public const string YunxiThreeToFourPillPath = "res://resources/entities/items/定脉丹.tres";
    public const string YunxiNineToTenPillPath = "res://resources/entities/items/归元定息丹.tres";

    public static string get_required_breakthrough_pill_path(string targetRealm) => targetRealm switch
    {
        "云息四层" => YunxiThreeToFourPillPath,
        "云息十层" => YunxiNineToTenPillPath,
        _ => ""
    };

    private static string get_breakthrough_pill_id(string targetRealm) => targetRealm switch
    {
        "云息四层" => "dingmai",
        "云息十层" => "guiyuan_dingxi",
        _ => ""
    };

    private static string get_breakthrough_pill_path_for_tier(string targetRealm, int tier)
    {
        if (tier == 0) return get_required_breakthrough_pill_path(targetRealm);
        var id = get_breakthrough_pill_id(targetRealm);
        return string.IsNullOrEmpty(id) ? "" : AlchemyProductCatalog.Path(id, tier);
    }

    /// <summary>返回本人确实持有的可选档位，按薄效至满效排列。</summary>
    public Array<Dictionary> get_breakthrough_pill_choices(string targetRealm)
    {
        var choices = new Array<Dictionary>();
        var id = get_breakthrough_pill_id(targetRealm);
        if (string.IsNullOrEmpty(id)) return choices;
        for (var tier = 0; tier < 5; tier++)
        {
            var path = get_breakthrough_pill_path_for_tier(targetRealm, tier);
            if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path)) continue;
            var count = get_inventory_total_count(path);
            if (count <= 0 || ResourceLoader.Load<PillItemData>(path) is not { } pill ||
                pill.pill_id != id || pill.efficacy_tier != tier) continue;
            var effectValue = id == "dingmai"
                ? AlchemyProductCatalog.MeridianProgress[tier]
                : AlchemyProductCatalog.YuanxiDurationReductionPercent[tier];
            choices.Add(new Dictionary
            {
                ["path"] = path,
                ["pill_id"] = id,
                ["tier"] = tier,
                ["name"] = pill.entity_name,
                ["count"] = count,
                ["effect"] = pill.EffectText,
                ["effect_value"] = effectValue,
                ["toxicity"] = PillToxicityRules.Load(id, tier)
            });
        }
        return choices;
    }

    public bool has_required_breakthrough_pill(string targetRealm)
    {
        var requiredPath = get_required_breakthrough_pill_path(targetRealm);
        return string.IsNullOrEmpty(requiredPath) || get_breakthrough_pill_choices(targetRealm).Count > 0;
    }

    public string get_breakthrough_pill_hint(string targetRealm)
    {
        var path = get_required_breakthrough_pill_path(targetRealm);
        if (string.IsNullOrEmpty(path)) return "";
        var name = targetRealm == "云息四层" ? "定脉丹" : "归元定息丹";
        return has_required_breakthrough_pill(targetRealm)
            ? $"突破至{targetRealm}须选择一枚{name}；仅成功晋层后消耗，并结算所选丹效。"
            : $"突破至{targetRealm}需要持有{name}任一丹效档（行囊中尚缺）。";
    }

    /// <summary>为全新白石镇旅程各发一枚薄效关口丹；避免重复初始化发放。</summary>
    public bool grant_new_journey_yunxi_breakthrough_pills()
    {
        foreach (var path in new[] { YunxiThreeToFourPillPath, YunxiNineToTenPillPath })
            if (!has_inventory_item(path, 1) && !add_item_to_inventory(path, 1))
                return false;
        return true;
    }

    /// <summary>兼容更新前已开始的闭关：沿用旧档的门票、基值丹毒与无后续丹效。</summary>
    public bool consume_breakthrough_pill_on_success(string targetRealm)
    {
        var path = get_required_breakthrough_pill_path(targetRealm);
        if (string.IsNullOrEmpty(path)) return true;
        if (!remove_item_from_inventory(path, 1)) return false;
        AddPillToxicity(targetRealm == "云息四层" ? "dingmai" : "guiyuan_dingxi");
        return true;
    }

    /// <summary>扣除本次预选丹，按服用前丹毒结算丹效，再加入所选档位的丹毒。</summary>
    public bool consume_selected_breakthrough_pill_on_success(string targetRealm, string path, string pillId, int tier, out double toxicityBefore)
    {
        toxicityBefore = 0;
        var expectedId = get_breakthrough_pill_id(targetRealm);
        if (string.IsNullOrEmpty(expectedId) || pillId != expectedId || tier is < 0 or > 4 ||
            path != get_breakthrough_pill_path_for_tier(targetRealm, tier) || !has_inventory_item(path, 1) ||
            !ResourceLoader.Exists(path) || ResourceLoader.Load<PillItemData>(path) is not { } pill ||
            pill.pill_id != pillId || pill.efficacy_tier != tier)
            return false;

        toxicityBefore = PillToxicity;
        bool removed;
        _consumingBreakthroughReservation = true;
        try { removed = remove_item_from_inventory(path, 1); }
        finally { _consumingBreakthroughReservation = false; }
        if (!removed) return false;
        AddPillToxicity(pillId, tier);
        return true;
    }
}
