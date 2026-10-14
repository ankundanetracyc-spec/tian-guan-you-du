using Godot.Collections;
using System;
using System.Globalization;

/// <summary>Formats derived cultivation state alongside persisted world-status records without duplicating saved data.</summary>
public static class CharacterStatusPresentationRules
{
    public static bool has_active_bottleneck(Dictionary? summary)
    {
        if (summary is null || !summary.GetValueOrDefault("applicable", false).AsBool()) return false;
        var level = summary.GetValueOrDefault("level", 0).AsInt32();
        var penalty = summary.GetValueOrDefault("penalty", 0d).AsDouble();
        return level > 0 || penalty > 1e-9d;
    }

    public static string format_bottleneck_hud_line(Dictionary? summary)
    {
        if (!has_active_bottleneck(summary)) return "";
        var state = summary!;
        var level = state.GetValueOrDefault("level", 0).AsInt32();
        var stages = Math.Max(1, state.GetValueOrDefault("stages", CultivationBottleneckRules.Stages).AsInt32());
        var efficiency = format_percent(state.GetValueOrDefault("gain_multiplier", 1d).AsDouble());
        return level > 0
            ? $"修行状态　瓶颈 {level}/{stages} 阶 · 吐纳效率 {efficiency}%"
            : $"修行状态　瓶颈缓解中 · 吐纳效率 {efficiency}%";
    }

    public static string format_bottleneck_details(Dictionary? summary)
    {
        if (!has_active_bottleneck(summary)) return "";
        var state = summary!;
        var level = state.GetValueOrDefault("level", 0).AsInt32();
        var stages = Math.Max(1, state.GetValueOrDefault("stages", CultivationBottleneckRules.Stages).AsInt32());
        var penalty = format_percent(state.GetValueOrDefault("penalty", 0d).AsDouble());
        var efficiency = format_percent(state.GetValueOrDefault("gain_multiplier", 1d).AsDouble());
        var heading = level > 0 ? $"闭关瓶颈 · 第 {level}/{stages} 阶" : "闭关瓶颈 · 缓解中";
        var lines = new System.Collections.Generic.List<string>
        {
            heading,
            $"当前吐纳效率 {efficiency}%（减速 {penalty}%）；仅影响云息吐纳，不影响归元九息。"
        };
        if (level >= stages)
            lines.Add("已至当前最高阶。");
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
