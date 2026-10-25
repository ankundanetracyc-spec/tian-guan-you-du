using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;
using Godot;
using Godot.Collections;

/// <summary>Legacy inspiration persistence and activity hooks; all gameplay operations are currently suspended.</summary>
public static class CultivationInspirationSystem
{
    /// <summary>暂时撤下灵感玩法；旧记录及来源流水只作存档兼容。</summary>
    public static bool Enabled => false;
    public const string AnyBottleneckUse = "cultivation_bottleneck";

    /// <summary>
    /// Records one completed source event, applying source-specific soft diminishing returns. All reward values are
    /// explicit caller configuration: base/minimum chance, decay factor, rank, and matching action domain.
    /// A source event is recorded even on a miss, so replaying it cannot reroll for a reward.
    /// </summary>
    public static bool try_award_renewable(
        HumanData? human,
        string stableEventId,
        string sourceId,
        string sourceLabel,
        int qualityRank,
        string useDomainId,
        long nowGameHour,
        float baseChance,
        float sameSourceDecayFactor,
        float minimumChance,
        out CultivationInspirationRecord? awarded,
        out string result)
        => try_award_renewable_core(human, stableEventId, sourceId, sourceLabel, qualityRank, 0f, 0f,
            useDomainId, nowGameHour, baseChance, sameSourceDecayFactor, minimumChance, out awarded, out result);

    /// <summary>Weighted rank variant used by configured renewable activity sources.</summary>
    public static bool try_award_renewable(
        HumanData? human,
        string stableEventId,
        string sourceId,
        string sourceLabel,
        string useDomainId,
        long nowGameHour,
        float baseChance,
        float sameSourceDecayFactor,
        float minimumChance,
        float rank2Weight,
        float rank3Weight,
        out CultivationInspirationRecord? awarded,
        out string result)
        => try_award_renewable_core(human, stableEventId, sourceId, sourceLabel, 0, rank2Weight, rank3Weight,
            useDomainId, nowGameHour, baseChance, sameSourceDecayFactor, minimumChance, out awarded, out result);

    private static bool try_award_renewable_core(
        HumanData? human,
        string stableEventId,
        string sourceId,
        string sourceLabel,
        int fixedQualityRank,
        float rank2Weight,
        float rank3Weight,
        string useDomainId,
        long nowGameHour,
        float baseChance,
        float sameSourceDecayFactor,
        float minimumChance,
        out CultivationInspirationRecord? awarded,
        out string result)
    {
        awarded = null;
        result = "invalid_configuration";
        if (!Enabled) { result = "disabled"; return false; }
        if (human is null || string.IsNullOrWhiteSpace(stableEventId) || string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(useDomainId) || (fixedQualityRank is < 0 or > 3) || nowGameHour < 0 ||
            baseChance <= 0f || baseChance > 1f || minimumChance <= 0f || minimumChance > baseChance ||
            sameSourceDecayFactor <= 0f || sameSourceDecayFactor >= 1f ||
            rank2Weight < 0f || rank3Weight < 0f || rank2Weight + rank3Weight > 1f ||
            (fixedQualityRank == 0 && rank2Weight + rank3Weight <= 0f))
            return false;

        ensure_storage(human);
        expire_available_records(human, nowGameHour);
        if (find_source_event(human, stableEventId) is not null)
        {
            result = "duplicate_event";
            return false;
        }

        var repeatCount = 0;
        foreach (var prior in human.cultivation_inspiration_source_events)
            if (prior is not null && prior.source_id == sourceId)
                repeatCount++;

        var evaluatedChance = Math.Max(minimumChance, baseChance * Math.Pow(sameSourceDecayFactor, repeatCount));
        var roll = deterministic_roll(sourceId, stableEventId);
        var granted = roll < evaluatedChance;
        var sourceEvent = new CultivationInspirationSourceEventRecord
        {
            event_id = stableEventId,
            source_id = sourceId,
            attempted_at_game_hour = nowGameHour,
            granted = granted,
            requested_quality_rank = fixedQualityRank,
            evaluated_chance = (float)evaluatedChance,
            deterministic_roll = (float)roll
        };
        human.cultivation_inspiration_source_events.Add(sourceEvent);

        if (!granted)
        {
            result = "no_award";
            return false;
        }

        var qualityRank = fixedQualityRank > 0 ? fixedQualityRank : choose_quality_rank(sourceId, stableEventId, rank2Weight, rank3Weight);
        sourceEvent.requested_quality_rank = qualityRank;

        var durationYears = qualityRank == 3 ? 2d : 1d;
        var inspiration = new CultivationInspirationRecord
        {
            record_id = $"inspiration:{stableEventId}",
            source_event_id = stableEventId,
            source_id = sourceId,
            source_label = sourceLabel ?? "",
            use_domain_id = useDomainId,
            quality_rank = qualityRank,
            acquired_at_game_hour = nowGameHour,
            expires_at_game_hour = nowGameHour + (long)(CultivationBottleneckRules.HoursPerGameYear * durationYears),
            state_id = CultivationInspirationRecord.StateAvailable
        };
        human.cultivation_inspiration_records.Add(inspiration);
        sourceEvent.granted_record_id = inspiration?.record_id ?? "";
        awarded = inspiration;
        result = "awarded";
        return awarded is not null;
    }

    public static void ensure_storage(HumanData human)
    {
        human.cultivation_inspiration_records ??= [];
        human.cultivation_inspiration_source_events ??= [];
    }

    public static int expire_available_records(HumanData? human, long nowGameHour)
    {
        if (!Enabled) return 0;
        if (human is null) return 0;
        ensure_storage(human);
        var expired = 0;
        foreach (var record in human.cultivation_inspiration_records)
            if (record is not null && record.mark_expired_if_due(nowGameHour))
                expired++;
        return expired;
    }

    public static Array<CultivationInspirationRecord> get_available_records(HumanData? human, long nowGameHour)
    {
        var result = new Array<CultivationInspirationRecord>();
        if (!Enabled) return result;
        if (human is null) return result;
        expire_available_records(human, nowGameHour);
        foreach (var record in human.cultivation_inspiration_records)
            if (record is not null && record.is_available_at(nowGameHour))
                result.Add(record);
        return result;
    }

    public static CultivationInspirationRecord? find_available_record(HumanData? human, string recordId, long nowGameHour)
    {
        if (!Enabled) return null;
        if (human is null || string.IsNullOrWhiteSpace(recordId)) return null;
        expire_available_records(human, nowGameHour);
        foreach (var record in human.cultivation_inspiration_records)
            if (record is not null && record.record_id == recordId && record.is_available_at(nowGameHour))
                return record;
        return null;
    }

    /// <summary>Call at the actual start of the configured action. Failure after this point does not refund it.</summary>
    public static bool try_consume_for_action_at_start(
        HumanData? human, string recordId, string actionDomainId, long nowGameHour,
        out CultivationInspirationRecord? consumed)
    {
        consumed = null;
        var record = find_available_record(human, recordId, nowGameHour);
        if (record is null || string.IsNullOrWhiteSpace(actionDomainId) || record.use_domain_id != actionDomainId)
            return false;
        if (!record.mark_consumed($"action:{actionDomainId}", nowGameHour)) return false;
        consumed = record;
        return true;
    }

    public static bool try_consume_for_bottleneck(HumanData? human, string recordId, long nowGameHour,
        out CultivationInspirationRecord? consumed)
    {
        if (!Enabled || !CultivationBottleneckRules.Enabled)
        {
            consumed = null;
            return false;
        }
        consumed = find_available_record(human, recordId, nowGameHour);
        if (consumed is null || !consumed.mark_consumed(AnyBottleneckUse, nowGameHour))
        {
            consumed = null;
            return false;
        }
        return true;
    }

    private static CultivationInspirationSourceEventRecord? find_source_event(HumanData human, string eventId)
    {
        foreach (var prior in human.cultivation_inspiration_source_events)
            if (prior is not null && prior.event_id == eventId)
                return prior;
        return null;
    }

    private static double deterministic_roll(string sourceId, string eventId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceId}\n{eventId}"));
        var raw = BinaryPrimitives.ReadUInt64LittleEndian(hash);
        return raw / 18446744073709551616d;
    }

    private static int choose_quality_rank(string sourceId, string eventId, float rank2Weight, float rank3Weight)
    {
        var roll = deterministic_roll(sourceId + ":rank", eventId);
        if (roll < rank3Weight) return 3;
        if (roll < rank3Weight + rank2Weight) return 2;
        return 1;
    }
}
