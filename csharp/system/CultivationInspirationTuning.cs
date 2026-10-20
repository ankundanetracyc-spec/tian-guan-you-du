using System;
using System.Collections.Generic;

/// <summary>Single tuning point for renewable inspiration sources and prototype action effects.</summary>
public static class CultivationInspirationTuning
{
    /// <summary>
    /// Pacing calibration for the configured renewable activity sources. This multiplier applies to both the initial
    /// and floor probabilities; the deterministic half-cultivation/half-field projection verifies the chosen value.
    /// </summary>
    public const float PrototypeSourceChanceMultiplier = 3.30f;

    public const string CombatDomain = "combat";
    public const string GatheringDomain = "gathering";
    public const string TravelDomain = "travel";
    public const string StudyDomain = "study";
    public const string SectDutyDomain = "sect_duty";

    public readonly record struct SourceRule(
        string SourceId,
        string Label,
        string UseDomainId,
        float BaseChance,
        float SameSourceDecayFactor,
        float MinimumChance,
        float Rank2Weight,
        float Rank3Weight);

    private static readonly Dictionary<string, SourceRule> SourceRules = new()
    {
        // Chances diminish by completed same-source results, including misses, and remain at the configured floor.
        ["combat.victory"] = new("combat.victory", "战斗胜局", StudyDomain, .45f, .80f, .12f, .28f, .08f),
        ["field.gathering"] = new("field.gathering", "野外采集", TravelDomain, .28f, .82f, .08f, .16f, .04f),
        ["journey.step"] = new("journey.step", "旅途行进", GatheringDomain, .22f, .90f, .08f, .16f, .04f),
        ["study.progress"] = new("study.progress", "静室研修", CombatDomain, .38f, .86f, .12f, .24f, .06f),
        ["sect.official_duty"] = new("sect.official_duty", "宗门公事", SectDutyDomain, .48f, .82f, .15f, .28f, .08f),
        ["sect.yaopu_watch"] = new("sect.yaopu_watch", "瑶圃日常值守", GatheringDomain, .34f, .86f, .10f, .20f, .05f),
    };

    public static bool try_get_source_rule(string sourceId, out SourceRule rule)
    {
        if (!SourceRules.TryGetValue(sourceId, out var configured))
        {
            rule = default;
            return false;
        }
        rule = configured with
        {
            BaseChance = Math.Min(1f, configured.BaseChance * PrototypeSourceChanceMultiplier),
            MinimumChance = Math.Min(1f, configured.MinimumChance * PrototypeSourceChanceMultiplier)
        };
        return true;
    }

    // Prototype values: kept here so pacing and action effects can be adjusted without changing activity logic.
    public static int combat_starting_shield(int rank) => rank switch { 1 => 2, 2 => 3, 3 => 4, _ => 0 };
    public static int gathering_extra_yield(int rank) => rank switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
    public static int travel_reveal_bonus_cells(int rank) => rank switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };
    public static float study_progress_multiplier(int rank) => rank switch { 1 => 1.10f, 2 => 1.20f, 3 => 1.35f, _ => 1f };
    public static int sect_contribution_bonus(int rank) => rank switch { 1 => 1, 2 => 2, 3 => 3, _ => 0 };

    public static string action_domain_label(string domainId) => domainId switch
    {
        CombatDomain => "战斗",
        GatheringDomain => "采集",
        TravelDomain => "旅途",
        StudyDomain => "研修",
        SectDutyDomain => "宗门公事",
        _ => "其他用途"
    };

    public static string source_label(string sourceId)
    {
        if (SourceRules.TryGetValue(sourceId, out var rule)) return rule.Label;
        return "其他经历";
    }

    public static string source_display_label(string sourceId, string sourceLabel)
    {
        var label = sourceLabel?.Trim() ?? "";
        var containsInternalEnglish = label.Any(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        return string.IsNullOrWhiteSpace(label) || label == sourceId || SourceRules.ContainsKey(label) || containsInternalEnglish
            ? source_label(sourceId)
            : label;
    }

    /// <summary>Player-facing instructions for where the reserved inspiration takes effect.</summary>
    public static string action_effect_text(string domainId, int rank)
    {
        var quality = rank switch { 1 => "见几", 2 => "明辨", 3 => "通变", _ => "灵感" };
        return domainId switch
        {
            CombatDomain => $"去开始下一场战斗；入场时消耗，开场护盾 +{combat_starting_shield(rank)}。",
            GatheringDomain => $"去东林地或副本开始采集；行动开始时消耗，额外获得 {gathering_extra_yield(rank)} 份资源。剩余储量与行囊容量都要足够。",
            TravelDomain => $"回到大世界开始一段行旅；出发时消耗，额外揭示 {travel_reveal_bonus_cells(rank)} 格地图。",
            StudyDomain => $"在静室开始功法研读；本次耗时不变，研读进度 ×{study_progress_multiplier(rank):0.00}。",
            SectDutyDomain => $"开始一项可强化的玉虚宗门公事；结算贡献额外 +{sect_contribution_bonus(rank)}。执中峰纠纷复核不适用。",
            _ => $"这份{quality}灵感会在对应活动开始时生效。"
        };
    }
}
