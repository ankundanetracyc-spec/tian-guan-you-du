using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Deterministic pacing projection for the currently configured inspiration loops.
/// It models the existing Yunxi rates/thresholds and bottle rules, not activity durations or random breakthrough rolls.
/// </summary>
public static class CultivationPacingAudit
{
    private const double HoursPerDay = 24d;
    private const double HoursPerYear = CultivationBottleneckRules.HoursPerGameYear;
    private const double HoursPerClosedMonth = 15d * HoursPerDay;
    private const double HoursPerOutdoorMonth = 15d * HoursPerDay;

    private static readonly int[] StrandThresholdByCurrentTier = [6, 18, 36, 60, 96, 144, 144, 198, 258, 324];
    private static readonly int[] BaseBreakthroughDaysByTargetTier = [1, 2, 3, 5, 6, 8, 12, 10, 13, 18];

    public sealed class Result
    {
        public double TotalYears { get; set; }
        public double ToCloudTenYears { get; set; }
        public double CultivationDays { get; set; }
        public double CappedBottleneckDays { get; set; }
        public int CompletedMonths { get; set; }
        public int SourceEventCount { get; set; }
        public int GrantedRecordCount { get; set; }
        public int RefinementUses { get; set; }
        public int ActionUses { get; set; }
        public int ExpiredRecords { get; set; }
        public int EndingAvailableRecords { get; set; }
        public int EndingBottleneckLevel { get; set; }
        public double EndingPenalty { get; set; }
        public Dictionary<string, int> SourceEventCounts { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> GrantedBySource { get; } = new(StringComparer.Ordinal);
        public Dictionary<int, int> GrantedByRank { get; } = new() { [1] = 0, [2] = 0, [3] = 0 };
    }

    /// <param name="generateInspiration">False models no insight awards; activities still occur.</param>
    /// <param name="actionUseShare">Fraction of awarded records the player reserves for a matching action.</param>
    public static Result Simulate(bool generateInspiration, double actionUseShare)
    {
        actionUseShare = Math.Clamp(actionUseShare, 0d, 1d);
        var result = new Result();
        var human = new HumanData();
        var actionAssigned = new HashSet<string>(StringComparer.Ordinal);
        var refineAssigned = new HashSet<string>(StringComparer.Ordinal);
        var activityMonths = build_monthly_activity_profile();

        var tier = 0; // ordinary entry to Cloud Breath one starts from the unbottlenecked initial state.
        var strands = 0d;
        var bottleneckHours = 0d;
        var bottleneckLevel = 0;
        var bottleneckRealmTier = 0;
        var penalty = 0d;
        var elapsedHours = 0d;
        var cultivationHours = 0d;
        var refinementUses = 0;
        var actionUses = 0;
        var monthIndex = 0;
        var enteredTiers = new HashSet<int>();

        foreach (var source in new[]
        {
            "combat.victory", "field.gathering", "journey.step", "study.progress",
            "sect.official_duty", "sect.yaopu_watch"
        })
        {
            result.SourceEventCounts[source] = 0;
            result.GrantedBySource[source] = 0;
        }

        // Keep the no-insight comparison case running long enough to finish even after severe late-stage slowdown.
        while (tier < 10 && monthIndex < 2400)
        {
            var month = monthIndex % 12;
            var year = monthIndex / 12 + 1;
            monthIndex++;

            var closedRemaining = HoursPerClosedMonth;
            while (closedRemaining > 1e-8d && tier < 10)
            {
                expire_records(elapsedHours, human);
                enter_reached_tiers(ref tier, strands, ref elapsedHours, enteredTiers);
                if (tier >= 10) break;

                update_bottleneck_state(tier, bottleneckHours, ref bottleneckLevel, ref bottleneckRealmTier, ref penalty);
                if (tier is >= 1 and <= 9 && bottleneckLevel > 0 && penalty > 0d)
                    refine_available_records(elapsedHours, tier, bottleneckLevel, ref penalty, human, refineAssigned, ref refinementUses);

                var multiplier = tier == 0 ? 1d : Character.get_realm_cultivation_multiplier_for_tier(tier);
                var rate = .005d * multiplier; // three roots, compatible generic main practice, cultivation environment factor 1.
                if (tier is >= 1 and <= 9) rate *= 1d - penalty;
                if (rate <= 0d) throw new InvalidOperationException("pacing projection has no positive cultivation rate");

                var segmentHours = closedRemaining;
                if (tier is >= 1 and <= 9)
                {
                    var nextBottleBoundary = CultivationBottleneckRules.get_next_level_threshold_hours(bottleneckLevel, tier);
                    var untilBottleBoundary = nextBottleBoundary - bottleneckHours;
                    if (bottleneckLevel < CultivationBottleneckRules.Stages && untilBottleBoundary > 1e-8d)
                        segmentHours = Math.Min(segmentHours, untilBottleBoundary);
                }

                var strandsToNextTier = StrandThresholdByCurrentTier[tier] - strands;
                if (strandsToNextTier > 1e-8d)
                    segmentHours = Math.Min(segmentHours, strandsToNextTier / rate);
                if (segmentHours <= 1e-8d)
                {
                    enter_reached_tiers(ref tier, strands, ref elapsedHours, enteredTiers);
                    update_bottleneck_state(tier, bottleneckHours, ref bottleneckLevel, ref bottleneckRealmTier, ref penalty);
                    continue;
                }

                strands += rate * segmentHours;
                cultivationHours += segmentHours;
                elapsedHours += segmentHours;
                closedRemaining -= segmentHours;
                if (tier is >= 1 and <= 9)
                {
                    bottleneckHours = Math.Min(
                        bottleneckHours + segmentHours,
                        CultivationBottleneckRules.get_max_accumulated_hours(tier));
                }
            }

            // Once Cloud Breath ten is reached, the remainder of this closed period can be spent beginning Yuanxi.
            // It has no bottle penalty, and the full 396-day Yuanxi duration is added below.
            if (tier >= 10 && closedRemaining > 0d)
                elapsedHours += closedRemaining;

            var outdoorStart = elapsedHours;
            var eventsThisMonth = activityMonths[month];
            for (var index = 0; index < eventsThisMonth.Count; index++)
            {
                var sourceId = eventsThisMonth[index];
                var eventHour = outdoorStart + (long)((index + 1) * HoursPerOutdoorMonth / (eventsThisMonth.Count + 1));
                var eventGameHour = (long)Math.Floor(eventHour);
                expire_records(eventHour, human);

                if (CultivationInspirationTuning.try_get_source_rule(sourceId, out var actionRule))
                {
                    var matchingAssigned = human.cultivation_inspiration_records
                        .Where(record => record is not null && actionAssigned.Contains(record.record_id) &&
                            record.use_domain_id == actionRule.UseDomainId && record.is_available_at(eventGameHour))
                        .OrderBy(record => record.expires_at_game_hour)
                        .FirstOrDefault();
                    if (matchingAssigned is not null &&
                        CultivationInspirationSystem.try_consume_for_action_at_start(
                            human, matchingAssigned.record_id, actionRule.UseDomainId, eventGameHour, out _))
                    {
                        actionAssigned.Remove(matchingAssigned.record_id);
                        actionUses++;
                    }
                }

                result.SourceEventCounts[sourceId]++;
                if (!generateInspiration || !CultivationInspirationTuning.try_get_source_rule(sourceId, out var rule))
                    continue;

                var stableEventId = $"pacing:{sourceId}:{year}:{month + 1}:{index + 1}";
                var granted = CultivationInspirationSystem.try_award_renewable(
                    human, stableEventId, rule.SourceId, rule.Label, rule.UseDomainId,
                    eventGameHour, rule.BaseChance, rule.SameSourceDecayFactor, rule.MinimumChance,
                    rule.Rank2Weight, rule.Rank3Weight, out var record, out _);
                if (!granted || record is null) continue;

                result.GrantedBySource[sourceId]++;
                result.GrantedByRank[record.quality_rank]++;
                if (deterministic_roll("use:" + stableEventId) < actionUseShare)
                    actionAssigned.Add(record.record_id);
                else
                {
                    refineAssigned.Add(record.record_id);
                    if (tier is >= 1 and <= 9 && bottleneckLevel > 0 && penalty > 0d)
                        refine_available_records(eventHour, tier, bottleneckLevel, ref penalty, human, refineAssigned, ref refinementUses);
                }
            }

            elapsedHours = outdoorStart + HoursPerOutdoorMonth;
        }

        if (tier < 10)
            throw new InvalidOperationException($"pacing projection did not reach Cloud Breath ten within its guard limit (inspiration={generateInspiration}, actionShare={actionUseShare:P0}, month={monthIndex}, tier={tier}, strands={strands:F3})");

        var cloudTenReachedAt = elapsedHours;
        elapsedHours += 396d * HoursPerDay;
        expire_records(elapsedHours, human);

        result.TotalYears = elapsedHours / HoursPerYear;
        result.ToCloudTenYears = cloudTenReachedAt / HoursPerYear;
        result.CultivationDays = cultivationHours / HoursPerDay;
        result.CappedBottleneckDays = bottleneckHours / HoursPerDay;
        result.CompletedMonths = monthIndex;
        result.SourceEventCount = result.SourceEventCounts.Values.Sum();
        result.GrantedRecordCount = result.GrantedByRank.Values.Sum();
        result.RefinementUses = refinementUses;
        result.ActionUses = actionUses;
        result.ExpiredRecords = human.cultivation_inspiration_records.Count(record =>
            record is not null && record.state_id == CultivationInspirationRecord.StateExpired);
        result.EndingAvailableRecords = human.cultivation_inspiration_records.Count(record =>
            record is not null && record.is_available_at((long)Math.Floor(elapsedHours)));
        result.EndingBottleneckLevel = bottleneckLevel;
        result.EndingPenalty = penalty;
        return result;
    }

    private static List<string>[] build_monthly_activity_profile()
    {
        var months = Enumerable.Range(0, 12).Select(_ => new List<string>()).ToArray();
        for (var month = 0; month < 12; month++)
        {
            // Twelve gathering, travel, and Yaopu completions per year.
            months[month].Add("field.gathering");
            months[month].Add("journey.step");
            months[month].Add("sect.yaopu_watch");
            if (month % 3 == 0)
            {
                months[month].Add("combat.victory");
                months[month].Add("sect.official_duty");
            }
            if (month % 2 == 0) months[month].Add("study.progress");
        }
        return months;
    }

    private static void enter_reached_tiers(ref int tier, double strands, ref double elapsedHours, HashSet<int> enteredTiers)
    {
        while (tier < 10 && strands + 1e-9d >= StrandThresholdByCurrentTier[tier])
        {
            tier++;
            if (enteredTiers.Add(tier))
                elapsedHours += BaseBreakthroughDaysByTargetTier[tier - 1] * HoursPerDay;
        }
    }

    private static void update_bottleneck_state(
        int tier, double accumulatedHours, ref int level, ref int previousRealmTier, ref double penalty)
    {
        if (tier is < 1 or > 9) return;
        accumulatedHours = Math.Clamp(accumulatedHours, 0d, CultivationBottleneckRules.get_max_accumulated_hours(tier));
        var resolvedLevel = CultivationBottleneckRules.get_level(accumulatedHours, tier);
        var realmChanged = previousRealmTier >= 0 && previousRealmTier != tier;
        if (resolvedLevel != level && !realmChanged)
            penalty = CultivationBottleneckRules.get_penalty_for_level(resolvedLevel);
        level = resolvedLevel;
        previousRealmTier = tier;
    }

    private static void refine_available_records(
        double nowHours, int tier, int level, ref double penalty, HumanData human,
        HashSet<string> refineAssigned, ref int refinementUses)
    {
        var nowGameHour = (long)Math.Floor(nowHours);
        while (level is >= 1 and <= CultivationBottleneckRules.Stages && penalty > 0d)
        {
            var record = human.cultivation_inspiration_records
                .Where(candidate => candidate is not null && refineAssigned.Contains(candidate.record_id) &&
                    candidate.is_available_at(nowGameHour))
                .OrderByDescending(candidate => candidate.quality_rank)
                .ThenBy(candidate => candidate.expires_at_game_hour)
                .FirstOrDefault();
            if (record is null || !CultivationInspirationSystem.try_consume_for_bottleneck(
                    human, record.record_id, nowGameHour, out var consumed) || consumed is null)
                break;
            refineAssigned.Remove(consumed.record_id);
            penalty = CultivationBottleneckRules.relieve_penalty(penalty, level, consumed.quality_rank);
            refinementUses++;
        }
    }

    private static void expire_records(double nowHours, HumanData human)
        => CultivationInspirationSystem.expire_available_records(human, (long)Math.Floor(nowHours));

    private static double deterministic_roll(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var raw = BinaryPrimitives.ReadUInt64LittleEndian(hash);
        return raw / 18446744073709551616d;
    }
}
