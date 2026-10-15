using Godot;
using Godot.Collections;
using System.Linq;

public partial class Character
{
    private sealed class CultivationSimulation
    {
        public int WholeStrands;
        public double FractionalProgress;
        public double TotalProgress;
        public double AccumulatedBottleneckHours;
        public int BottleneckLevel;
        public int BottleneckRealmTier;
        public double BottleneckPenalty;
        public double AverageRate;
    }

    private sealed class CultivationBottleneckSnapshot
    {
        public double AccumulatedHours;
        public int Level;
        public int RealmTier;
        public int PreviousRealmTier = -1;
        public double Penalty;

        public void reconcile()
        {
            AccumulatedHours = Math.Clamp(AccumulatedHours, 0d, CultivationBottleneckRules.get_max_accumulated_hours(RealmTier));
            var resolvedLevel = CultivationBottleneckRules.get_level(AccumulatedHours, RealmTier);
            var realmChanged = PreviousRealmTier >= 0 && PreviousRealmTier != RealmTier;
            // A realm transition can change the grace window and stage; until its relief behavior is specified,
            // retain the saved current penalty rather than silently restoring the stage baseline.
            if (Penalty < 0d || (Level != resolvedLevel && !realmChanged))
                Penalty = CultivationBottleneckRules.get_penalty_for_level(resolvedLevel);
            Level = resolvedLevel;
            PreviousRealmTier = RealmTier;
        }

        public void advance(double hours)
        {
            AccumulatedHours = Math.Clamp(AccumulatedHours + hours, 0d,
                CultivationBottleneckRules.get_max_accumulated_hours(RealmTier));
            reconcile();
        }
    }

    /// <summary>Fires after inspiration records or the active bottleneck penalty change.</summary>
    public event Action<Character>? CultivationInspirationChanged;

    private int get_cultivation_bottleneck_realm_tier()
    {
        if (is_yuanxi_realm()) return 0;
        return cultivation_realm switch
        {
            "云息一层" => 1, "云息二层" => 2, "云息三层" => 3,
            "云息四层" => 4, "云息五层" => 5, "云息六层" => 6,
            "云息七层" => 7, "云息八层" => 8, "云息九层" => 9,
            _ => 0
        };
    }

    private CultivationBottleneckSnapshot get_cultivation_bottleneck_snapshot()
    {
        var tier = get_cultivation_bottleneck_realm_tier();
        if (human is null || tier == 0)
            return new CultivationBottleneckSnapshot { AccumulatedHours = 0d, Level = 0, RealmTier = Math.Max(1, tier), Penalty = 0d };

        var snapshot = new CultivationBottleneckSnapshot
        {
            AccumulatedHours = Math.Max(0d, human.cultivation_bottleneck_accumulated_hours),
            Level = human.cultivation_bottleneck_level,
            RealmTier = tier,
            Penalty = human.cultivation_bottleneck_current_penalty,
            // The saved realm tier is the source for restoring a previously relieved penalty.
            PreviousRealmTier = human.cultivation_bottleneck_realm_tier
        };
        snapshot.reconcile();
        write_cultivation_bottleneck_snapshot(snapshot);
        return snapshot;
    }

    private void write_cultivation_bottleneck_snapshot(CultivationBottleneckSnapshot snapshot)
    {
        if (human is null) return;
        human.cultivation_bottleneck_accumulated_hours = Math.Max(0d, snapshot.AccumulatedHours);
        human.cultivation_bottleneck_level = Math.Clamp(snapshot.Level, 0, CultivationBottleneckRules.Stages);
        human.cultivation_bottleneck_realm_tier = snapshot.RealmTier;
        human.cultivation_bottleneck_current_penalty = Math.Clamp(snapshot.Penalty, 0d, .90d);
    }

    private CultivationSimulation simulate_cultivation_hours(double requestedHours)
    {
        var result = new CultivationSimulation();
        if (human is null || requestedHours <= 0d)
            return result;

        var baseRate = Math.Max(0d, get_cultivation_rate());
        var activeTier = get_cultivation_bottleneck_realm_tier();
        if (activeTier == 0)
        {
            var unpenalizedProgress = Math.Max(0d, human.cultivation_progress) + baseRate * requestedHours;
            var unpenalizedWhole = (int)Math.Floor(unpenalizedProgress + 1e-8d);
            result.WholeStrands = Math.Max(0, unpenalizedWhole);
            result.FractionalProgress = Math.Clamp(unpenalizedProgress - unpenalizedWhole, 0d, .999999999d);
            result.TotalProgress = unpenalizedProgress;
            result.AverageRate = baseRate;
            return result;
        }
        var state = get_cultivation_bottleneck_snapshot();
        var totalProgress = Math.Max(0d, human.cultivation_progress);
        var remaining = requestedHours;
        double integratedGain = 0d;

        while (remaining > 1e-8d)
        {
            state.reconcile();
            var threshold = CultivationBottleneckRules.get_next_level_threshold_hours(state.Level, state.RealmTier);
            var untilNextLevel = threshold - state.AccumulatedHours;
            var segment = state.Level >= CultivationBottleneckRules.Stages || untilNextLevel <= 1e-8d
                ? remaining
                : Math.Min(remaining, untilNextLevel);
            if (segment <= 1e-8d)
            {
                state.advance(1e-7d);
                remaining = Math.Max(0d, remaining - 1e-7d);
                continue;
            }

            var effectiveRate = baseRate * (1d - Math.Clamp(state.Penalty, 0d, .90d));
            var gained = effectiveRate * segment;
            integratedGain += gained;
            totalProgress += gained;
            state.advance(segment);
            remaining -= segment;
        }

        var whole = (int)Math.Floor(totalProgress + 1e-8d);
        result.WholeStrands = Math.Max(0, whole);
        result.FractionalProgress = Math.Clamp(totalProgress - whole, 0d, .999999999d);
        result.TotalProgress = totalProgress;
        result.AccumulatedBottleneckHours = state.AccumulatedHours;
        result.BottleneckLevel = state.Level;
        result.BottleneckRealmTier = state.RealmTier;
        result.BottleneckPenalty = state.Penalty;
        result.AverageRate = requestedHours > 0d ? integratedGain / requestedHours : baseRate;
        return result;
    }

    private double get_cultivation_hours_for_strands(int requestedStrands)
    {
        if (human is null || requestedStrands <= 0) return 0d;
        var remainingProgress = Math.Max(0d, requestedStrands - human.cultivation_progress);
        if (remainingProgress <= 1e-8d) return 0d;

        var baseRate = Math.Max(0d, get_cultivation_rate());
        if (baseRate <= 0d) return double.PositiveInfinity;
        if (get_cultivation_bottleneck_realm_tier() == 0) return remainingProgress / baseRate;
        var state = get_cultivation_bottleneck_snapshot();
        var elapsed = 0d;
        while (remainingProgress > 1e-8d)
        {
            state.reconcile();
            var rate = baseRate * (1d - Math.Clamp(state.Penalty, 0d, .90d));
            if (rate <= 0d) return double.PositiveInfinity;
            var needed = remainingProgress / rate;
            var threshold = CultivationBottleneckRules.get_next_level_threshold_hours(state.Level, state.RealmTier);
            var untilNextLevel = threshold - state.AccumulatedHours;
            var segment = state.Level >= CultivationBottleneckRules.Stages || untilNextLevel <= 1e-8d
                ? needed
                : Math.Min(needed, untilNextLevel);
            if (segment <= 1e-8d)
            {
                state.advance(1e-7d);
                elapsed += 1e-7d;
                continue;
            }
            remainingProgress -= rate * segment;
            state.advance(segment);
            elapsed += segment;
        }
        return elapsed;
    }

    private void apply_cultivation_simulation(CultivationSimulation simulation)
    {
        if (human is null) return;
        var previousLevel = human.cultivation_bottleneck_level;
        var previousRealmTier = human.cultivation_bottleneck_realm_tier;
        var previousPenalty = human.cultivation_bottleneck_current_penalty;
        human.cultivation_progress = (float)simulation.FractionalProgress;
        if (simulation.BottleneckRealmTier <= 0) return;
        var state = new CultivationBottleneckSnapshot
        {
            AccumulatedHours = simulation.AccumulatedBottleneckHours,
            Level = simulation.BottleneckLevel,
            RealmTier = simulation.BottleneckRealmTier,
            Penalty = simulation.BottleneckPenalty,
            PreviousRealmTier = simulation.BottleneckRealmTier
        };
        write_cultivation_bottleneck_snapshot(state);
        if (previousLevel != state.Level || previousRealmTier != state.RealmTier || Math.Abs(previousPenalty - state.Penalty) > 1e-9d)
            CultivationInspirationChanged?.Invoke(this);
    }

    public Dictionary get_cultivation_bottleneck_summary()
    {
        if (human is null || get_cultivation_bottleneck_realm_tier() == 0)
            return new Dictionary { ["applicable"] = false, ["level"] = 0, ["stages"] = CultivationBottleneckRules.Stages, ["penalty"] = 0d, ["gain_multiplier"] = 1d };
        var state = get_cultivation_bottleneck_snapshot();
        var nextThreshold = CultivationBottleneckRules.get_next_level_threshold_hours(state.Level, state.RealmTier);
        return new Dictionary
        {
            ["applicable"] = true,
            ["level"] = state.Level,
            ["stages"] = CultivationBottleneckRules.Stages,
            ["penalty"] = state.Penalty,
            ["gain_multiplier"] = 1d - state.Penalty,
            ["accumulated_cultivation_hours"] = state.AccumulatedHours,
            ["grace_hours"] = CultivationBottleneckRules.get_grace_hours(state.RealmTier),
            ["hours_until_next_level"] = double.IsInfinity(nextThreshold) ? 0d : Math.Max(0d, nextThreshold - state.AccumulatedHours)
        };
    }

    public float get_cultivation_rate_after_bottleneck()
    {
        if (get_cultivation_bottleneck_realm_tier() == 0) return get_cultivation_rate();
        var state = get_cultivation_bottleneck_snapshot();
        return (float)(Math.Max(0d, get_cultivation_rate()) * (1d - Math.Clamp(state.Penalty, 0d, .90d)));
    }

    public Array<CultivationInspirationRecord> get_cultivation_inspiration_records()
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var records = CultivationInspirationSystem.get_available_records(human, now);
        if (human is not null) CultivationInspirationSystem.ensure_storage(human);
        return records;
    }

    public bool try_grant_cultivation_inspiration(
        string stableEventId, string sourceId, string sourceLabel, int qualityRank, string useDomainId,
        float baseChance, float sameSourceDecayFactor, float minimumChance, out CultivationInspirationRecord? awarded, out string result)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var didAward = CultivationInspirationSystem.try_award_renewable(human, stableEventId, sourceId, sourceLabel,
            qualityRank, useDomainId, now, baseChance, sameSourceDecayFactor, minimumChance, out awarded, out result);
        if (didAward || result == "no_award") CultivationInspirationChanged?.Invoke(this);
        return didAward;
    }

    public bool try_grant_renewable_cultivation_inspiration(
        string stableEventId, string sourceId, out CultivationInspirationRecord? awarded, out string result)
    {
        awarded = null;
        result = "unknown_source";
        if (!CultivationInspirationTuning.try_get_source_rule(sourceId, out var rule)) return false;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var didAward = CultivationInspirationSystem.try_award_renewable(human, stableEventId,
            rule.SourceId, rule.Label, rule.UseDomainId, now, rule.BaseChance, rule.SameSourceDecayFactor,
            rule.MinimumChance, rule.Rank2Weight, rule.Rank3Weight, out awarded, out result);
        if (didAward || result == "no_award") CultivationInspirationChanged?.Invoke(this);
        return didAward;
    }

    /// <summary>Player explicitly reserves this record for its matching action domain.</summary>
    public bool set_cultivation_inspiration_action_reservation(string recordId, bool armed)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var record = CultivationInspirationSystem.find_available_record(human, recordId, now);
        if (record is null || string.IsNullOrWhiteSpace(record.use_domain_id)) return false;
        if (armed)
        {
            foreach (var other in human!.cultivation_inspiration_records)
                if (other is not null && other.record_id != recordId && other.use_domain_id == record.use_domain_id)
                    other.armed_for_action = false;
        }
        record.armed_for_action = armed;
        CultivationInspirationChanged?.Invoke(this);
        return true;
    }

    public CultivationInspirationRecord? get_armed_cultivation_inspiration_for_action(string actionDomainId)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        if (human is null || string.IsNullOrWhiteSpace(actionDomainId)) return null;
        return human.cultivation_inspiration_records.FirstOrDefault(record => record is not null &&
            record.armed_for_action && record.use_domain_id == actionDomainId && record.is_available_at(now));
    }

    /// <summary>Consumes only the explicitly armed matching record when the action actually begins.</summary>
    public bool try_consume_armed_cultivation_inspiration_for_action(
        string actionDomainId, out CultivationInspirationRecord? consumed)
    {
        consumed = get_armed_cultivation_inspiration_for_action(actionDomainId);
        if (consumed is null || !try_consume_cultivation_inspiration_for_action_at_start(consumed.record_id, actionDomainId, out var actuallyConsumed))
        {
            consumed = null;
            return false;
        }
        actuallyConsumed!.armed_for_action = false;
        consumed = actuallyConsumed;
        CultivationInspirationChanged?.Invoke(this);
        return true;
    }

    private float consume_study_action_inspiration()
    {
        return try_consume_armed_cultivation_inspiration_for_action(
            CultivationInspirationTuning.StudyDomain, out var consumed) && consumed is not null
                ? CultivationInspirationTuning.study_progress_multiplier(consumed.quality_rank)
                : 1f;
    }

    private void award_study_action_inspiration(SecretBook book, string chapterId, long completedAtGameHour)
    {
        var source = book.gongfa?.ResourcePath ?? book.craft_data?.ResourcePath ?? "study";
        var safeChapterId = string.IsNullOrWhiteSpace(chapterId) ? "progress" : chapterId;
        try_grant_renewable_cultivation_inspiration(
            $"study:{source}:{safeChapterId}:{completedAtGameHour}", "study.progress", out _, out _);
    }

    public bool try_consume_cultivation_inspiration_for_action_at_start(
        string recordId, string actionDomainId, out CultivationInspirationRecord? consumed)
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var didConsume = CultivationInspirationSystem.try_consume_for_action_at_start(human, recordId, actionDomainId, now, out consumed);
        if (didConsume) CultivationInspirationChanged?.Invoke(this);
        return didConsume;
    }

    /// <summary>Consumes one inspiration and partially lowers the current Yunxi cultivation penalty.</summary>
    public bool try_refine_cultivation_bottleneck(string recordId, out Dictionary result)
    {
        result = new Dictionary { ["success"] = false };
        if (human is null || get_cultivation_bottleneck_realm_tier() == 0) return false;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var state = get_cultivation_bottleneck_snapshot();
        if (state.Level is < 1 or > CultivationBottleneckRules.Stages || state.Penalty <= 0d) return false;
        var inspiration = CultivationInspirationSystem.find_available_record(human, recordId, now);
        if (inspiration is null || inspiration.armed_for_action) return false;
        var oldPenalty = state.Penalty;
        var newPenalty = CultivationBottleneckRules.relieve_penalty(oldPenalty, state.Level, inspiration.quality_rank);
        if (!CultivationInspirationSystem.try_consume_for_bottleneck(human, recordId, now, out var consumed) || consumed is null)
            return false;
        state.Penalty = newPenalty;
        write_cultivation_bottleneck_snapshot(state);
        result = new Dictionary
        {
            ["success"] = true,
            ["level"] = state.Level,
            ["quality_rank"] = consumed.quality_rank,
            ["old_penalty"] = oldPenalty,
            ["new_penalty"] = newPenalty,
            ["gain_multiplier"] = 1d - newPenalty,
            ["consumed_record_id"] = consumed.record_id
        };
        CultivationInspirationChanged?.Invoke(this);
        return true;
    }

    private void expire_cultivation_inspiration_records(long nowGameHour)
    {
        if (human is not null && CultivationInspirationSystem.expire_available_records(human, nowGameHour) > 0)
            CultivationInspirationChanged?.Invoke(this);
    }
}
