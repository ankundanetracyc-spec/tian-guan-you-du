using Godot;

/// <summary>
/// Central tuning point for the Yunxi bottleneck. The eight penalty anchors drive accumulation only;
/// inspiration relief operates on the current percentage. These defaults remain replaceable after pacing calibration.
/// </summary>
public static class CultivationBottleneckRules
{
    public const int Stages = 8;
    public const double HoursPerGameDay = 24d;
    public const double HoursPerGameYear = 360d * HoursPerGameDay;
    public const double OldStageIntervalDays = 6d;
    public const double OldMaximumPostGraceDays = Stages * OldStageIntervalDays;

    private static readonly double[] OldPenaltyByLevel = [0d, .02d, .05d, .10d, .17d, .26d, .40d, .61d, .90d];

    public static double get_grace_days(int yunxiTier) => yunxiTier switch
    {
        >= 1 and <= 3 => 12d,
        >= 4 and <= 6 => 24d,
        >= 7 and <= 9 => 36d,
        _ => 36d
    };

    public static double get_grace_hours(int yunxiTier) => get_grace_days(yunxiTier) * HoursPerGameDay;
    public static double get_stage_interval_hours() => OldStageIntervalDays * HoursPerGameDay;
    public static double get_max_accumulated_hours(int yunxiTier)
        => get_grace_hours(yunxiTier) + OldMaximumPostGraceDays * HoursPerGameDay;
    public static double get_penalty_for_level(int level) => OldPenaltyByLevel[Math.Clamp(level, 0, Stages)];

    public static int get_level(double accumulatedCultivationHours, int yunxiTier)
    {
        var cappedHours = Math.Min(Math.Max(0d, accumulatedCultivationHours), get_max_accumulated_hours(yunxiTier));
        var beyondGrace = Math.Max(0d, cappedHours - get_grace_hours(yunxiTier));
        return Math.Clamp((int)Math.Floor((beyondGrace + 1e-9d) / get_stage_interval_hours()), 0, Stages);
    }

    public static double get_next_level_threshold_hours(int currentLevel, int yunxiTier)
        => currentLevel >= Stages
            ? double.PositiveInfinity
            : get_grace_hours(yunxiTier) + (currentLevel + 1) * get_stage_interval_hours();

    /// <summary>Applies realm-scaled inspiration relief to the current bottleneck percentage.</summary>
    public static double relieve_penalty(double currentPenalty, int cultivationRealmTier, int inspirationRank)
    {
        if (cultivationRealmTier is < 1 or > 9 || inspirationRank is < 1 or > 3 || currentPenalty <= 0d)
            return Math.Max(0d, currentPenalty);
        return currentPenalty * cultivationRealmTier / (cultivationRealmTier + (double)inspirationRank);
    }
}
