using Godot;
using System.Collections.Generic;
using System.Linq;
using Service = TalismanCraftingService;

/// <summary>已制成纸墨笔的只读展示；不依赖旧描述，也不重算冻结能力。</summary>
public static class TalismanItemPresentation
{
    private const string InvalidBody = "资料已失效，暂不可选用。";

    private static Service.ProductSnapshot? CraftSnapshot(TalismanProductItemData item) =>
        item.Snapshot is { Kind: "paper" or "ink" or "brush" } snapshot && Service.ValidateSnapshot(snapshot)
            ? snapshot : null;

    public static bool IsCraftMaterial(TalismanProductItemData item) => CraftSnapshot(item) is not null;

    public static IReadOnlyList<KeyValuePair<string, string>> Fields(TalismanProductItemData item) =>
        CraftSnapshot(item) is { } snapshot ? Service.PlayerFields(snapshot) : [];

    private static string FieldBody(Service.ProductSnapshot snapshot) =>
        string.Join("\n", Service.PlayerFields(snapshot).Select(field => field.Key + "：" + field.Value));

    public static string Body(TalismanProductItemData item) =>
        CraftSnapshot(item) is { } snapshot ? FieldBody(snapshot) : InvalidBody;

    public static HoverInfoManager.HoverInfoContent Hover(TalismanProductItemData item)
    {
        var snapshot = CraftSnapshot(item);
        string kind = snapshot?.Kind switch { "paper" => "符纸", "ink" => "符墨", "brush" => "符笔", _ => "符材" };
        string name = string.IsNullOrWhiteSpace(item.entity_name) ? kind : item.entity_name;
        string subtitle = "";
        if (snapshot?.Kind is "paper" or "ink")
        {
            var elements = snapshot.ElementShares.Select(share => share.Name).Distinct().ToArray();
            subtitle = "五行：" + (elements.Length == 0 ? "无" : string.Join("、", elements));
        }
        else if (snapshot?.Kind == "brush") subtitle = "画符用具";
        string iconPath = snapshot is null ? item.icon_path : TalismanArtCatalog.IconPath(snapshot);
        if (string.IsNullOrWhiteSpace(iconPath) || !ResourceLoader.Exists(iconPath)) iconPath = item.icon_path;
        var icon = !string.IsNullOrWhiteSpace(iconPath) && ResourceLoader.Exists(iconPath)
            ? ResourceLoader.Load<Texture2D>(iconPath) : null;
        return ItemLayoutPresentation.AttachStorageVolume(HoverInfoManager.Structured(name, icon, subtitle,
            new HoverInfoManager.HoverInfoSection("", snapshot is null ? InvalidBody : FieldBody(snapshot))) with
        {
            CardWidth = 340f, IconSize = 96f, ShowRules = false, SingleColumn = true,
            Footprint = new Vector2I(item.get_storage_volume(), 1), InventoryFootprintBelowTitle = false,
            GridDiagrams = null
        }, item.get_storage_volume());
    }
}
