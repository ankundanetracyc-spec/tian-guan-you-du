using Godot;
using System.Linq;

/// <summary>Page-independent material information; quantities and prices belong to their page.</summary>
public static class MaterialItemPresentation
{
    public static bool IsMaterial(ItemData item) => item.craft_data is MaterialCraftData || item.item_category == "材料";

    public static HoverInfoManager.HoverInfoContent Hover(ItemData item)
    {
        var material = item.craft_data as MaterialCraftData;
        var grade = material?.material_grade_name() ?? item.get_grade_string();
        if (material is not null && material.utilities.Any(u => u is not null && !string.IsNullOrWhiteSpace(u.utility_id)))
        {
            var grades = material.utilities.Where(u => u is not null && !string.IsNullOrWhiteSpace(u.utility_id))
                .Select(u => Mathf.Clamp(u.grade, 1, 9)).OrderByDescending(g => g).ToArray();
            var terms = grades.Select((g, i) => $"{g}×{(i == 0 ? "2" : i == 1 ? "0.5" : "0.25")}");
            grade += $" 计算公式：材评{string.Join("＋", terms)}＝{material.material_score():0.##}，向上取整并限定在1～27后为第{material.material_grade_value()}小阶";
        }
        string Values(string dimension, string fallback = "未标注")
        {
            var values = StoragePanel.get_display_tag_values(item.tags, dimension, material);
            return values.Count > 0 ? string.Join("、", values.Distinct()) : fallback;
        }
        var element = material is not null && !string.IsNullOrWhiteSpace(material.element) ? material.element : Values("五行");
        var utilities = material is null ? Values("功用", "无") : string.Join("、", material.utilities
            .Where(u => u is not null && !string.IsNullOrWhiteSpace(u.utility_id))
            .Select(u => $"{u.utility_id}·{u.grade_name()}").Distinct());
        if (string.IsNullOrWhiteSpace(utilities)) utilities = "无";
        var icon = !string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path)
            ? ResourceLoader.Load<Texture2D>(item.icon_path) : null;
        return HoverInfoManager.Structured(item.entity_name, icon, "",
            new("", $"品阶：{grade}\n五行：{element}\n质地：{Values("质地")}\n相性：{Values("相性")}\n功用：{utilities}"),
            new("", $"简介：{item.description?.Trim()}")) with { CardWidth = 340f, IconSize = 96f, ShowRules = false };
    }
}
