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
        public double RequestedHours;
        public bool AutoRefinementConfigured;
        public bool AutoRefinementTriggered;
        public string AutoRefinementRecordId = "";
        public double AutoRefinementThresholdPenalty;
        public int AutoRefinementQualityRank;
        public long AutoRefinementGameHour = -1;
        public double AutoRefinementOldPenalty;
        public double AutoRefinementNewPenalty;
        public bool AutoRefinementExpiredBeforeThreshold;
    }

    private sealed class CultivationAutoRefinementPlan
    {
        public string RecordId = "";
        public double ThresholdPenalty;
        public int QualityRank;
        public long ExpiresAtGameHour;
    }

    private Dictionary _lastCultivationAutoRefinementResult = new()
    {
        ["attempted"] = false,
        ["consumed"] = false
    };

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
        if (!CultivationBottleneckRules.Enabled) return 0;
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

    public Dictionary get_cultivation_auto_refinement_settings()
    {
        if (!CultivationBottleneckRules.Enabled)
            return new Dictionary { ["enabled"] = false, ["eligible"] = false, ["reason"] = "闭关瓶颈已暂停，无需炼化灵感。" };
        if (human is null)
            return new Dictionary { ["enabled"] = false, ["eligible"] = false, ["reason"] = "角色数据不可用。" };

        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var threshold = get_auto_refinement_threshold_penalty();
        var state = get_cultivation_bottleneck_realm_tier() == 0 ? null : get_cultivation_bottleneck_snapshot();
        var record = CultivationInspirationSystem.find_available_record(human, human.cultivation_auto_refinement_record_id, now);
        var reason = "设置已保存；本次闭关会在瓶颈减速达到所选百分比时炼化。";
        var eligible = human.cultivation_auto_refinement_enabled;
        if (!eligible)
            reason = "自动炼化已关闭。";
        else if (state is null)
        {
            eligible = false;
            reason = "当前境界没有可炼化的云息瓶颈。";
        }
        else if (threshold is < .01d or > .90d)
        {
            eligible = false;
            reason = "瓶颈减速阈值需在 1% 到 90% 之间。";
        }
        else if (record is null)
        {
            eligible = false;
            reason = "所选灵感已失效或已被消耗，请重新选择。";
        }
        else if (record.armed_for_action)
        {
            eligible = false;
            reason = "所选灵感已预留给对应行动；请先取消预留或另选一份。";
        }
        else if (state is not null && state.Penalty >= threshold)
        {
            reason = $"当前瓶颈减速已达 {state.Penalty:P1}；本段开始修炼时会立即炼化。";
        }

        return new Dictionary
        {
            ["enabled"] = human.cultivation_auto_refinement_enabled,
            ["eligible"] = eligible,
            ["record_id"] = human.cultivation_auto_refinement_record_id,
            ["threshold_penalty"] = threshold,
            ["current_penalty"] = state?.Penalty ?? 0d,
            ["quality_rank"] = record?.quality_rank ?? 0,
            ["quality_name"] = record?.get_quality_name() ?? "",
            ["expires_at_game_hour"] = record?.expires_at_game_hour ?? -1,
            ["reason"] = reason
        };
    }

    public bool set_cultivation_auto_refinement_settings(bool enabled, string recordId, double thresholdPenalty, out string reason)
    {
        reason = "";
        if (!CultivationBottleneckRules.Enabled)
        {
            reason = "闭关瓶颈已暂停，无需设置自动炼化。";
            return !enabled;
        }
        if (human is null)
        {
            reason = "角色数据不可用。";
            return false;
        }

        recordId = (recordId ?? "").Trim();
        if (!double.IsFinite(thresholdPenalty) || thresholdPenalty < .01d || thresholdPenalty > .90d)
        {
            if (enabled)
            {
                reason = "瓶颈减速阈值需在 1% 到 90% 之间。";
                return false;
            }
            thresholdPenalty = get_auto_refinement_threshold_penalty();
        }
        if (enabled)
        {
            var state = get_cultivation_bottleneck_realm_tier() == 0 ? null : get_cultivation_bottleneck_snapshot();
            if (state is null)
            {
                reason = "当前境界没有可炼化的云息瓶颈。";
                return false;
            }
            var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
            var record = CultivationInspirationSystem.find_available_record(human, recordId, now);
            if (record is null)
            {
                reason = "所选灵感已失效或不可用。";
                return false;
            }
            if (record.armed_for_action)
            {
                reason = "所选灵感已预留给对应行动，请先取消预留或另选一份。";
                return false;
            }
        }

        human.cultivation_auto_refinement_enabled = enabled;
        human.cultivation_auto_refinement_record_id = recordId;
        human.cultivation_auto_refinement_threshold_penalty = thresholdPenalty;
        CultivationInspirationChanged?.Invoke(this);
        return true;
    }

    public Dictionary get_last_cultivation_auto_refinement_result()
    {
        var result = new Dictionary();
        foreach (Variant key in _lastCultivationAutoRefinementResult.Keys)
            result[key] = _lastCultivationAutoRefinementResult[key];
        return result;
    }

    public Dictionary get_cultivation_auto_refinement_preview(int requestedHours)
    {
        var simulation = simulate_cultivation_hours(Math.Max(0, requestedHours));
        return new Dictionary
        {
            ["configured"] = simulation.AutoRefinementConfigured,
            ["triggered"] = simulation.AutoRefinementTriggered,
            ["threshold_penalty"] = simulation.AutoRefinementThresholdPenalty,
            ["quality_rank"] = simulation.AutoRefinementQualityRank,
            ["old_penalty"] = simulation.AutoRefinementOldPenalty,
            ["new_penalty"] = simulation.AutoRefinementNewPenalty,
            ["game_hour"] = simulation.AutoRefinementGameHour,
            ["expired_before_threshold"] = simulation.AutoRefinementExpiredBeforeThreshold,
            ["result_level"] = simulation.BottleneckLevel
        };
    }

    private CultivationAutoRefinementPlan? get_cultivation_auto_refinement_plan(CultivationBottleneckSnapshot state)
    {
        if (human is null || !human.cultivation_auto_refinement_enabled) return null;
        var threshold = get_auto_refinement_threshold_penalty();
        if (threshold is < .01d or > .90d) return null;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var record = CultivationInspirationSystem.find_available_record(human, human.cultivation_auto_refinement_record_id, now);
        if (record is null || record.armed_for_action) return null;
        return new CultivationAutoRefinementPlan
        {
            RecordId = record.record_id,
            ThresholdPenalty = threshold,
            QualityRank = record.quality_rank,
            ExpiresAtGameHour = record.expires_at_game_hour
        };
    }

    private double get_auto_refinement_threshold_penalty()
    {
        if (human is null) return .10d;
        if (!double.IsFinite(human.cultivation_auto_refinement_threshold_penalty))
            human.cultivation_auto_refinement_threshold_penalty = -1d;
        if (human.cultivation_auto_refinement_threshold_penalty < 0d)
        {
            var legacyLevel = Math.Clamp(human.cultivation_auto_refinement_threshold_level, 1, CultivationBottleneckRules.Stages);
            human.cultivation_auto_refinement_threshold_penalty = CultivationBottleneckRules.get_penalty_for_level(legacyLevel);
        }
        return Math.Clamp(human.cultivation_auto_refinement_threshold_penalty, .01d, .90d);
    }

    private CultivationSimulation simulate_cultivation_hours(double requestedHours, bool includeAutoRefinement = true)
    {
        var result = new CultivationSimulation { RequestedHours = requestedHours };
        if (human is null || requestedHours <= 0d)
            return result;

        var baseRate = Math.Max(0d, get_cultivation_rate());
        var startingToxicity = PillToxicity;
        var activeTier = get_cultivation_bottleneck_realm_tier();
        if (activeTier == 0)
        {
            var unpenalizedProgress = Math.Max(0d, human.cultivation_progress) + baseRate * PillToxicityRules.EffectiveHours(startingToxicity, 0, requestedHours);
            var unpenalizedWhole = (int)Math.Floor(unpenalizedProgress + 1e-8d);
            result.WholeStrands = Math.Max(0, unpenalizedWhole);
            result.FractionalProgress = Math.Clamp(unpenalizedProgress - unpenalizedWhole, 0d, .999999999d);
            result.TotalProgress = unpenalizedProgress;
            result.AverageRate = requestedHours > 0 ? (unpenalizedProgress - Math.Max(0d, human.cultivation_progress)) / requestedHours : baseRate;
            return result;
        }
        var state = get_cultivation_bottleneck_snapshot();
        var autoPlan = includeAutoRefinement ? get_cultivation_auto_refinement_plan(state) : null;
        var startGameHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (autoPlan is not null)
        {
            result.AutoRefinementConfigured = true;
            result.AutoRefinementRecordId = autoPlan.RecordId;
            result.AutoRefinementThresholdPenalty = autoPlan.ThresholdPenalty;
            result.AutoRefinementQualityRank = autoPlan.QualityRank;
        }
        var totalProgress = Math.Max(0d, human.cultivation_progress);
        var remaining = requestedHours;
        var elapsed = 0d;
        var autoRefinementResolved = false;
        double integratedGain = 0d;

        while (remaining > 1e-8d)
        {
            state.reconcile();
            var previousPenalty = state.Penalty;
            if (autoPlan is not null && !autoRefinementResolved && previousPenalty >= autoPlan.ThresholdPenalty)
            {
                autoRefinementResolved = true;
                var triggerHour = startGameHour + (long)Math.Ceiling(elapsed - 1e-9d);
                if (triggerHour < autoPlan.ExpiresAtGameHour)
                {
                    result.AutoRefinementTriggered = true;
                    result.AutoRefinementGameHour = triggerHour;
                    result.AutoRefinementOldPenalty = state.Penalty;
                    result.AutoRefinementNewPenalty = CultivationBottleneckRules.relieve_penalty(
                        state.Penalty, state.RealmTier, autoPlan.QualityRank);
                    state.Penalty = result.AutoRefinementNewPenalty;
                }
                else
                {
                    result.AutoRefinementExpiredBeforeThreshold = true;
                }
            }
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
            var gained = effectiveRate * PillToxicityRules.EffectiveHours(startingToxicity, elapsed, segment);
            integratedGain += gained;
            totalProgress += gained;
            state.advance(segment);
            elapsed += segment;
            if (autoPlan is not null && !autoRefinementResolved && previousPenalty < autoPlan.ThresholdPenalty &&
                state.Penalty >= autoPlan.ThresholdPenalty)
            {
                autoRefinementResolved = true;
                var triggerHour = startGameHour + (long)Math.Ceiling(elapsed - 1e-9d);
                if (triggerHour < autoPlan.ExpiresAtGameHour)
                {
                    result.AutoRefinementTriggered = true;
                    result.AutoRefinementGameHour = triggerHour;
                    result.AutoRefinementOldPenalty = state.Penalty;
                    result.AutoRefinementNewPenalty = CultivationBottleneckRules.relieve_penalty(
                        state.Penalty, state.RealmTier, autoPlan.QualityRank);
                    state.Penalty = result.AutoRefinementNewPenalty;
                }
                else
                {
                    result.AutoRefinementExpiredBeforeThreshold = true;
                }
            }
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
        if (autoPlan is not null && !result.AutoRefinementTriggered &&
            startGameHour + (long)Math.Ceiling(requestedHours - 1e-9d) >= autoPlan.ExpiresAtGameHour)
            result.AutoRefinementExpiredBeforeThreshold = true;
        return result;
    }

    private double get_cultivation_hours_for_strands(int requestedStrands)
    {
        if (human is null || requestedStrands <= 0) return 0;
        var remaining = Math.Max(0, requestedStrands - human.cultivation_progress);
        if (remaining <= 1e-8) return 0;
        var rate = Math.Max(0, get_cultivation_rate());
        if (rate <= 0) return double.PositiveInfinity;
        // 与正式预演共用分段积分，兼容瓶颈、灵感触发及丹毒自然消退。
        double low = 0, high = Math.Max(1, remaining / rate * 20);
        while (simulate_cultivation_hours(high).TotalProgress + 1e-9 < requestedStrands)
        {
            high *= 2;
            if (!double.IsFinite(high)) return double.PositiveInfinity;
        }
        for (int i = 0; i < 60; i++)
        {
            var mid = (low + high) * .5;
            if (simulate_cultivation_hours(mid).TotalProgress >= requestedStrands) high = mid;
            else low = mid;
        }
        return high;
    }

    private void apply_cultivation_simulation(CultivationSimulation simulation)
    {
        if (human is null) return;
        _lastCultivationAutoRefinementResult = new Dictionary
        {
            ["configured"] = simulation.AutoRefinementConfigured,
            ["triggered"] = simulation.AutoRefinementTriggered,
            ["attempted"] = false,
            ["consumed"] = false,
            ["failed"] = false,
            ["expired_before_threshold"] = simulation.AutoRefinementExpiredBeforeThreshold,
            ["record_id"] = simulation.AutoRefinementRecordId,
            ["threshold_penalty"] = simulation.AutoRefinementThresholdPenalty,
            ["quality_rank"] = simulation.AutoRefinementQualityRank,
            ["game_hour"] = simulation.AutoRefinementGameHour,
            ["old_penalty"] = simulation.AutoRefinementOldPenalty,
            ["new_penalty"] = simulation.AutoRefinementNewPenalty
        };

        var autoRefinementConsumed = false;
        if (simulation.AutoRefinementTriggered)
        {
            _lastCultivationAutoRefinementResult["attempted"] = true;
            autoRefinementConsumed = try_consume_auto_refinement_for_simulation(simulation);
            if (!autoRefinementConsumed)
            {
                _lastCultivationAutoRefinementResult["failed"] = true;
                simulation = simulate_cultivation_hours(simulation.RequestedHours, includeAutoRefinement: false);
                _lastCultivationAutoRefinementResult["new_penalty"] = simulation.BottleneckPenalty;
            }
            else
            {
                _lastCultivationAutoRefinementResult["consumed"] = true;
            }
        }

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
        if (previousLevel != state.Level || previousRealmTier != state.RealmTier || Math.Abs(previousPenalty - state.Penalty) > 1e-9d || autoRefinementConsumed)
            CultivationInspirationChanged?.Invoke(this);
    }

    private bool try_consume_auto_refinement_for_simulation(CultivationSimulation simulation)
    {
        if (human is null || !simulation.AutoRefinementConfigured || string.IsNullOrWhiteSpace(simulation.AutoRefinementRecordId) ||
            simulation.AutoRefinementGameHour < 0 || !human.cultivation_auto_refinement_enabled ||
            human.cultivation_auto_refinement_record_id != simulation.AutoRefinementRecordId ||
            Math.Abs(get_auto_refinement_threshold_penalty() - simulation.AutoRefinementThresholdPenalty) > 1e-9d)
            return false;

        var record = CultivationInspirationSystem.find_available_record(
            human, simulation.AutoRefinementRecordId, simulation.AutoRefinementGameHour);
        if (record is null || record.armed_for_action ||
            !CultivationInspirationSystem.try_consume_for_bottleneck(
                human, simulation.AutoRefinementRecordId, simulation.AutoRefinementGameHour, out _))
            return false;

        human.cultivation_auto_refinement_enabled = false;
        human.cultivation_auto_refinement_record_id = "";
        return true;
    }

    public Dictionary get_cultivation_bottleneck_summary()
    {
        if (human is null || get_cultivation_bottleneck_realm_tier() == 0)
            return new Dictionary { ["applicable"] = false, ["level"] = 0, ["accumulated_level"] = 0, ["stages"] = CultivationBottleneckRules.Stages, ["penalty"] = 0d, ["current_penalty"] = 0d, ["gain_multiplier"] = 1d };
        var state = get_cultivation_bottleneck_snapshot();
        var nextThreshold = CultivationBottleneckRules.get_next_level_threshold_hours(state.Level, state.RealmTier);
        return new Dictionary
        {
            ["applicable"] = true,
            ["level"] = state.Level,
            ["accumulated_level"] = state.Level,
            ["stages"] = CultivationBottleneckRules.Stages,
            ["penalty"] = state.Penalty,
            ["current_penalty"] = state.Penalty,
            ["gain_multiplier"] = 1d - state.Penalty,
            ["realm_tier"] = state.RealmTier,
            ["accumulated_cultivation_hours"] = state.AccumulatedHours,
            ["grace_hours"] = CultivationBottleneckRules.get_grace_hours(state.RealmTier),
            ["hours_until_next_level"] = double.IsInfinity(nextThreshold) ? 0d : Math.Max(0d, nextThreshold - state.AccumulatedHours)
        };
    }

    public float get_cultivation_rate_after_bottleneck()
    {
        if (get_cultivation_bottleneck_realm_tier() == 0) return (float)(get_cultivation_rate() * PillAbsorption);
        var state = get_cultivation_bottleneck_snapshot();
        return (float)(Math.Max(0d, get_cultivation_rate()) * PillAbsorption * (1d - Math.Clamp(state.Penalty, 0d, .90d)));
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
        if (!CultivationInspirationSystem.Enabled) return false;
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
        if (!CultivationInspirationSystem.Enabled) return null;
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

    /// <summary>Consumes one inspiration and reduces the current bottleneck percentage according to realm and quality.</summary>
    public bool try_refine_cultivation_bottleneck(string recordId, out Dictionary result)
    {
        result = new Dictionary { ["success"] = false };
        if (human is null || get_cultivation_bottleneck_realm_tier() == 0) return false;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        expire_cultivation_inspiration_records(now);
        var state = get_cultivation_bottleneck_snapshot();
        if (state.Penalty <= 0d) return false;
        var inspiration = CultivationInspirationSystem.find_available_record(human, recordId, now);
        if (inspiration is null || inspiration.armed_for_action) return false;
        var oldPenalty = state.Penalty;
        var newPenalty = CultivationBottleneckRules.relieve_penalty(oldPenalty, state.RealmTier, inspiration.quality_rank);
        if (!CultivationInspirationSystem.try_consume_for_bottleneck(human, recordId, now, out var consumed) || consumed is null)
            return false;
        if (human.cultivation_auto_refinement_record_id == recordId)
        {
            human.cultivation_auto_refinement_enabled = false;
            human.cultivation_auto_refinement_record_id = "";
        }
        state.Penalty = newPenalty;
        write_cultivation_bottleneck_snapshot(state);
        result = new Dictionary
        {
            ["success"] = true,
            ["level"] = state.Level,
            ["accumulated_level"] = state.Level,
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
