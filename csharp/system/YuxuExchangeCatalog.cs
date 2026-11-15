using Godot;
using System.Text.Json;

/// <summary>善功堂的现行目录与可调整贡献价；界面和实际购买必须读取同一来源。</summary>
public static class YuxuExchangeCatalog
{
    public const string PricePath = "res://docs/plans/物品与装备/善功堂临时贡献价_20261030.json";
    public static readonly string[] Categories = ["材料", "器物", "丹药", "阵盘", "器具", "符箓", "灵膳"];
    private static Dictionary<string, int>? _prices;

    private static Dictionary<string, int> Prices
    {
        get
        {
            if (_prices is not null) return _prices;
            _prices = new(StringComparer.Ordinal);
            try
            {
                using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(PricePath));
                foreach (var entry in document.RootElement.GetProperty("items").EnumerateObject())
                    if (entry.Value.TryGetProperty("contribution", out var price) && price.TryGetInt32(out var value) && value >= 0)
                        _prices[entry.Name] = value;
            }
            catch (Exception error) { GD.PushError("善功堂贡献价读取失败：" + error.Message); }
            return _prices;
        }
    }

    public static int Price(ItemData? item) => item is not null && Prices.TryGetValue(item.definition_id.ToString(), out var value) ? value : -1;
    public static string Category(ItemData item) => item is EquipmentData or SectArtifactOfferData ? "器物" : item.item_category;
    public static bool IsAvailable(ItemData? item)
    {
        if (item is null || Price(item) < 0 || string.IsNullOrWhiteSpace(item.entity_name) || item is PillContainerItemData) return false;
        if (!GongfaAvailability.IsItemEnabled(item) || !AlchemyProductCatalog.IsItemEnabled(item)) return false;
        if (item is PillItemData pill && (pill.pill_id != "huichun" || pill.efficacy_tier is < 0 or > 4)) return false;
        if (item.item_category == "材料" && item.craft_data is not MaterialCraftData) return false;
        if (item.item_category == "秘籍" && item.gongfa is not GongfaResource) return false;
        if (!Categories.Contains(Category(item)) || string.IsNullOrWhiteSpace(item.icon_path) || !ResourceLoader.Exists(item.icon_path)) return false;
        return item.validate_definition().Count == 0;
    }

    public static List<ItemData> LoadItems()
    {
        var result = new List<ItemData>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in new[] { "res://resources/entities/items", "res://resources/entities/equipment" }) Collect(directory, result, seen);
        return result.OrderBy(item => Array.IndexOf(Categories, Category(item))).ThenBy(item => item.entity_name, StringComparer.CurrentCulture).ToList();
    }

    private static void Collect(string path, List<ItemData> result, HashSet<string> seen)
    {
        using var directory = DirAccess.Open(path);
        if (directory is null) return;
        foreach (var file in directory.GetFiles().OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)) continue;
            if (GD.Load<ItemData>(path + "/" + file) is not { } item || !IsAvailable(item) || !seen.Add(item.definition_id.ToString())) continue;
            result.Add(item);
        }
        foreach (var folder in directory.GetDirectories().OrderBy(name => name, StringComparer.Ordinal)) Collect(path + "/" + folder, result, seen);
    }
}
