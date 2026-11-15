using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Shared item hover for a manual; chapter rewards remain on the existing study page.</summary>
public static class ManualItemPresentation
{
    public static bool IsManual(ItemData item) => item.item_category == "秘籍" || item.gongfa is GongfaResource;

    public static HoverInfoManager.HoverInfoContent Hover(ItemData item)
    {
        var book = item.gongfa as GongfaResource;
        var chapters = book?.get_sorted_chapters();
        var first = chapters?.FirstOrDefault();
        var realm = book is null ? "未标注" :
            !string.IsNullOrWhiteSpace(book.main_realm_start) && !string.IsNullOrWhiteSpace(book.main_realm_end)
                ? $"{book.main_realm_start}至{book.main_realm_end}" :
            !string.IsNullOrWhiteSpace(book.required_realm) ? book.required_realm : "未标注";
        var requirements = new List<string>();
        if (book is not null)
        {
            if (book.wuxing == GongfaResource.Wuxing.ALL) requirements.Add("具有灵根");
            else if (book.wuxing != GongfaResource.Wuxing.NONE)
            {
                var roots = book.wuxing.ToString().Split('_').Select(root => root switch
                { "METAL" => "金", "WOOD" => "木", "WATER" => "水", "FIRE" => "火", "EARTH" => "土", _ => "未标注" });
                requirements.Add(string.Join(book.wuxing_requirement_mode == GongfaResource.WuxingRequirementMode.AND ? "、" : "或", roots) + "灵根");
            }
            var attributes = new Dictionary<string, float>();
            void Collect(Godot.Collections.Dictionary values)
            {
                foreach (var key in values.Keys)
                {
                    var name = key.AsString(); var value = values[key].AsSingle();
                    attributes[name] = attributes.TryGetValue(name, out var previous) ? Mathf.Max(previous, value) : value;
                }
            }
            Collect(book.required_attributes);
            if (first is not null) Collect(first.required_attributes);
            foreach (var (name, value) in attributes) requirements.Add($"{AttributeName(name)}≥{value:0.##}");
            if (first is not null)
            {
                foreach (var id in first.required_chapter_ids)
                    requirements.Add("前置：" + (book.get_chapter_by_id(id)?.chapter_name ?? "未标注卷章"));
                if (first.requires_main_slot) requirements.Add("需主修本秘籍");
            }
        }
        var keywords = new List<string>();
        if (book is not null)
            foreach (var heart in new[] { book.get_heart_method_skill(true), book.get_heart_method_skill(false) })
                if (heart is not null) keywords.AddRange(heart.get_presentation_tags());
        var keywordText = keywords.Count > 0 ? string.Join("、", keywords.Distinct()) : "无";
        var introduction = !string.IsNullOrWhiteSpace(book?.description) ? book.description.Trim() : item.description.Trim();
        var icon = !string.IsNullOrWhiteSpace(item.icon_path) && ResourceLoader.Exists(item.icon_path) ? GD.Load<Texture2D>(item.icon_path) : null;
        return ItemLayoutPresentation.AttachItem(HoverInfoManager.Structured(item.entity_name, icon, "",
            new("", $"修习境界：{realm}\n修习要求：{(requirements.Count > 0 ? string.Join("、", requirements.Distinct()) : "无")}\n关键词：{keywordText}"),
            new("", "简介：" + introduction)) with { ShowRules = false, Footprint = new Vector2I(item.get_storage_volume(), 1) }, item);
    }

    private static string AttributeName(string name) => name switch
    {
        "physique" => "体魄", "intelligence" => "灵慧", "soul" => "神魂",
        "body_proficiency" => "体道造诣", "sword_proficiency" => "剑道造诣", "magic_proficiency" => "术法造诣",
        "formation_proficiency" => "阵法造诣", "alchemy_proficiency" => "丹道造诣", "forging_proficiency" => "器道造诣",
        _ => "其他属性"
    };
}
