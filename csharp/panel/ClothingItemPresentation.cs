using Godot;
using System.Linq;

/// <summary>Shared cosmetic clothing information, independent of inventory and page actions.</summary>
public static class ClothingItemPresentation
{
    public static HoverInfoManager.HoverInfoContent Hover(PortraitClothingData item)
    {
        var audience = item.portrait_gender switch { "male" => "男修", "female" => "女修", "both" => "通用", _ => "未标注" };
        var introduction = string.Join("。", item.description.Split('。').Select(s => s.Trim())
            .Where(s => s.Length > 0 && s is not ("男修立绘衣着" or "女修立绘衣着") &&
                !s.StartsWith("持有后可") && !s.StartsWith("只改变立绘")))
            .Replace("立绘衣着，男女款随角色性别呈现", "衣着");
        if (introduction.Length > 0) introduction += "。";
        var icon = !string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path) ? GD.Load<Texture2D>(item.icon_path) : null;
        return HoverInfoManager.Structured(item.entity_name, icon, "", new("", "适用人物：" + audience), new("", "简介：" + introduction))
            with { CardWidth = 340f, IconSize = 96f, ShowRules = false, Footprint = item.get_inventory_size() };
    }
}
