using Godot.Collections;
using System;
using System.Globalization;

/// <summary>Formats derived cultivation state alongside persisted world-status records without duplicating saved data.</summary>
public static class CharacterStatusPresentationRules
{
    public static bool has_active_bottleneck(Dictionary? summary)
    {
        if (summary is null || !summary.GetValueOrDefault("applicable", false).AsBool()) return false;
        var penalty = summary.GetValueOrDefault("penalty", 0d).AsDouble();
        return penalty > 1e-9d;
    }

    public static string format_bottleneck_hud_line(Dictionary? summary)
    {
        if (!has_active_bottleneck(summary)) return "";
        var state = summary!;
        var efficiency = format_percent(state.GetValueOrDefault("gain_multiplier", 1d).AsDouble());
        var penalty = format_percent(state.GetValueOrDefault("current_penalty", state.GetValueOrDefault("penalty", 0d)).AsDouble());
        return $"修行状态　瓶颈减速 {penalty}% · 吐纳效率 {efficiency}%";
    }

    public static string format_bottleneck_details(Dictionary? summary)
    {
        if (!has_active_bottleneck(summary)) return "";
        var state = summary!;
        var penalty = format_percent(state.GetValueOrDefault("current_penalty", state.GetValueOrDefault("penalty", 0d)).AsDouble());
        var efficiency = format_percent(state.GetValueOrDefault("gain_multiplier", 1d).AsDouble());
        var lines = new System.Collections.Generic.List<string>
        {
            $"云息吐纳瓶颈：效率损失 {penalty}%，当前吐纳效率 {efficiency}%。",
            "灵感炼化按当前境界和灵感品级降低瓶颈百分比；瓶颈随继续吐纳重新累积。归元九息不受影响。"
        };
        return string.Join("\n", lines);
    }

    public static string format_details(Array<WorldStatusRecord>? records, Dictionary? bottleneckSummary, long currentGameHour)
    {
        var worldStatus = WorldStatusRules.format_details(records, currentGameHour);
        var bottleneck = format_bottleneck_details(bottleneckSummary);
        if (string.IsNullOrWhiteSpace(bottleneck)) return worldStatus;
        if (worldStatus == "暂无特殊状态。") return bottleneck;
        return $"{bottleneck}\n\n{worldStatus}";
    }

    public static string format_percent(double value)
        => (Math.Clamp(value, 0d, 1d) * 100d).ToString("0.#", CultureInfo.InvariantCulture);
}
