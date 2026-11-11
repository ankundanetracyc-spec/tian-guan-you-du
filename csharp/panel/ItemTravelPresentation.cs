using Godot;
using System.Collections.Generic;

/// <summary>Append only configured item-owned travel data to the shared item reader.</summary>
public static class ItemTravelPresentation
{
    public static HoverInfoManager.HoverInfoContent Append(HoverInfoManager.HoverInfoContent content, ItemTravelData? travel, double? chargeHours = null)
    {
        if (travel is null) return content;
        var speed = travel.try_get_dun_speed(out var value) ? value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "未定";
        var capacity = travel.try_get_passenger_capacity(out var people) ? $"{people}人" : "未定";
        var capacityLine = travel.visual_id == "spirit_boat" || travel.passenger_capacity != 0 ? $"\n可承载人数：{capacity}" : "";
        var cost = travel.describe_consumption();
        var charge = chargeHours is { } hours && double.IsFinite(hours) && hours >= 0 ? $"\n剩余续航：{hours:0.##}小时" : "";
        var section = new HoverInfoManager.HoverInfoSection("行旅", $"遁速：{speed}{capacityLine}\n消耗：{cost}{charge}");
        var sections = content.Sections is null ? new List<HoverInfoManager.HoverInfoSection>() : new List<HoverInfoManager.HoverInfoSection>(content.Sections);
        if (content.Sections is null && !string.IsNullOrWhiteSpace(content.Body)) sections.Add(new("", content.Body));
        sections.Add(section);
        var body = string.IsNullOrWhiteSpace(content.Body) ? $"{section.Title}\n{section.Body}" : $"{content.Body}\n\n{section.Title}\n{section.Body}";
        return content with { Body = body, Sections = sections };
    }
}
