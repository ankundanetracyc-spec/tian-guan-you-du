using Godot;

/// <summary>云息三至四、九至十层的指定突破丹药条件。</summary>
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

    public bool has_required_breakthrough_pill(string targetRealm)
    {
        var path = get_required_breakthrough_pill_path(targetRealm);
        return string.IsNullOrEmpty(path) || has_inventory_item(path, 1);
    }

    public string get_breakthrough_pill_hint(string targetRealm)
    {
        var path = get_required_breakthrough_pill_path(targetRealm);
        if (string.IsNullOrEmpty(path)) return "";
        var item = ResourceLoader.Exists(path) ? ResourceLoader.Load<ItemData>(path) : null;
        var name = item?.entity_name ?? "对应丹药";
        var toxicity = targetRealm == "云息四层" ? PillToxicityRules.BaseLoads["dingmai"] : PillToxicityRules.BaseLoads["guiyuan_dingxi"];
        return has_inventory_item(path, 1)
            ? $"所需丹药：{name}；成功晋层时消耗，丹毒＋{toxicity}。"
            : $"突破至{targetRealm}需要持有{name}（行囊中尚缺）。";
    }

    /// <summary>为全新白石镇旅程各发一枚关口丹药；检查已有数量以避免重复初始化发放。</summary>
    public bool grant_new_journey_yunxi_breakthrough_pills()
    {
        foreach (var path in new[] { YunxiThreeToFourPillPath, YunxiNineToTenPillPath })
            if (!has_inventory_item(path, 1) && !add_item_to_inventory(path, 1))
                return false;
        return true;
    }

    public bool consume_breakthrough_pill_on_success(string targetRealm)
    {
        var path = get_required_breakthrough_pill_path(targetRealm);
        if (string.IsNullOrEmpty(path)) return true;
        if (!remove_item_from_inventory(path, 1)) return false;
        AddPillToxicity(targetRealm == "云息四层" ? "dingmai" : "guiyuan_dingxi");
        return true;
    }
}
