using Godot;
using Godot.Collections;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>Focused checks for inspiration expiry, deduplication, persistence, and bottle relief.</summary>
public partial class CultivationInspirationSmoke : Node
{
    private const long HoursPerYear = 360L * 24L;

    public override void _Ready()
    {
        try
        {
            check_relief_formula();
            check_repeated_refinement_and_clock();
            check_accumulation_cap_and_realm_transition();
            check_source_event_idempotency_and_diminishing_floor();
            check_weighted_rank_selection();
            check_cross_domain_use_mapping();
            check_explicit_action_reservation();
            check_expiry_boundary();
            check_save_and_json_round_trip();
            check_yuanxi_exemption();
            check_room_integration();
            check_pacing_projection();
            GD.Print("CULTIVATION_INSPIRATION_SMOKE_OK");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CULTIVATION_INSPIRATION_SMOKE_FAIL: {error}");
            GetTree().Quit(1);
        }
    }

    private void check_relief_formula()
    {
        foreach (var level in new[] { 1, 4, 8 })
        foreach (var rank in new[] { 1, 2, 3 })
        {
            var penalty = CultivationBottleneckRules.get_penalty_for_level(level);
            var expected = penalty * level / (level + (double)rank);
            var actual = CultivationBottleneckRules.relieve_penalty(penalty, level, rank);
            Check(Math.Abs(actual - expected) < 1e-12, $"P×B/(B+I) at B={level}, I={rank}");
            Check(actual > 0d && actual < penalty, $"one use partially relieves, not clears, B={level}, I={rank}");
            Check(refine_real_character(level, rank, expected), $"character refinement applies B={level}, I={rank}");
        }
    }

    private bool refine_real_character(int level, int rank, double expectedPenalty)
    {
        var clock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var now = clock?.get_total_game_hours() ?? 0;
        var data = create_stage_data(level);
        data.cultivation_inspiration_records.Add(make_record($"formula-{level}-{rank}", rank, now));
        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(data);
        character.Hide();
        var startHours = clock?.get_total_game_hours() ?? 0;
        var succeeded = character.try_refine_cultivation_bottleneck($"formula-{level}-{rank}", out var result);
        var human = character.entity_data as HumanData;
        var summary = character.get_cultivation_bottleneck_summary();
        var ok = succeeded && result.GetValueOrDefault("success", false).AsBool() &&
            Math.Abs(summary.GetValueOrDefault("penalty", 0d).AsDouble() - expectedPenalty) < 1e-12 &&
            human?.cultivation_inspiration_records[0].state_id == CultivationInspirationRecord.StateConsumed &&
            startHours == (clock?.get_total_game_hours() ?? 0);
        character.Free();
        return ok;
    }

    private void check_repeated_refinement_and_clock()
    {
        var clock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var startHours = clock?.get_total_game_hours() ?? 0;
        var data = create_stage_data(4);
        data.cultivation_inspiration_records.Add(make_record("repeat-1", 1, startHours));
        data.cultivation_inspiration_records.Add(make_record("repeat-3", 3, startHours));
        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(data);
        character.Hide();

        var initialPenalty = CultivationBottleneckRules.get_penalty_for_level(4);
        var afterFirst = initialPenalty * 4d / 5d;
        var afterSecond = afterFirst * 4d / 7d;
        Check(character.try_refine_cultivation_bottleneck("repeat-1", out _), "first sequential refinement succeeds");
        Check(Math.Abs(character.get_cultivation_bottleneck_summary().GetValueOrDefault("penalty", 0d).AsDouble() - afterFirst) < 1e-12,
            "first use applies its rank to current penalty");
        Check(character.try_refine_cultivation_bottleneck("repeat-3", out _), "second sequential refinement succeeds");
        Check(Math.Abs(character.get_cultivation_bottleneck_summary().GetValueOrDefault("penalty", 0d).AsDouble() - afterSecond) < 1e-12,
            "second use compounds against remaining penalty");
        Check((clock?.get_total_game_hours() ?? 0) == startHours, "refinement does not convert activity time or advance clock");
        Check((character.entity_data as HumanData)?.cultivation_bottleneck_accumulated_hours == data.cultivation_bottleneck_accumulated_hours,
            "refinement leaves accumulated cultivation hours unchanged");
        Check(!character.try_refine_cultivation_bottleneck("repeat-1", out _), "the consumed record cannot be used twice");
        character.Free();
    }

    private void check_accumulation_cap_and_realm_transition()
    {
        var clockNow = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var maximum = CultivationBottleneckRules.get_max_accumulated_hours(3);
        var overrun = create_stage_data(8);
        overrun.cultivation_bottleneck_accumulated_hours = maximum + 50000;
        var cappedCharacter = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(cappedCharacter);
        cappedCharacter.load_from_data(overrun);
        cappedCharacter.Hide();
        var capped = cappedCharacter.get_cultivation_bottleneck_summary();
        Check(capped.GetValueOrDefault("level", 0).AsInt32() == 8 &&
            Math.Abs(capped.GetValueOrDefault("accumulated_cultivation_hours", 0d).AsDouble() - maximum) < 1e-9,
            "eighth stage caps saved accumulation at grace plus 48 days");
        cappedCharacter.Free();

        var data = create_stage_data(4);
        data.cultivation_inspiration_records.Add(make_record("realm-transition-relief", 2, clockNow));
        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(data);
        character.Hide();
        Check(character.try_refine_cultivation_bottleneck("realm-transition-relief", out _), "setup relief before realm transition");
        var relievedPenalty = character.get_cultivation_bottleneck_summary().GetValueOrDefault("penalty", 0d).AsDouble();
        character.cultivation_realm = "云息四层";
        var afterRealmChange = character.get_cultivation_bottleneck_summary();
        Check(afterRealmChange.GetValueOrDefault("level", 0).AsInt32() == 2 &&
            Math.Abs(afterRealmChange.GetValueOrDefault("penalty", 0d).AsDouble() - relievedPenalty) < 1e-12,
            "realm progression recalculates stage without silently erasing saved relief");
        character.Free();
    }

    private void check_source_event_idempotency_and_diminishing_floor()
    {
        var human = new HumanData();
        var first = CultivationInspirationSystem.try_award_renewable(human, "stable-event", "repeatable-source", "测试来源", 2, "test-action",
            0, 1f, .5f, .05f, out var awarded, out var firstResult);
        var second = CultivationInspirationSystem.try_award_renewable(human, "stable-event", "repeatable-source", "测试来源", 2, "test-action",
            0, 1f, .5f, .05f, out _, out var secondResult);
        Check(first && awarded is not null && firstResult == "awarded", "configured source event grants one renewable record");
        Check(!second && secondResult == "duplicate_event" && human.cultivation_inspiration_records.Count == 1 && human.cultivation_inspiration_source_events.Count == 1,
            "replaying the stable event ID does not grant twice");

        for (var index = 0; index < 32; index++)
        {
            CultivationInspirationSystem.try_award_renewable(human, $"repeat-event-{index}", "repeatable-source", "测试来源", 1, "test-action",
                index + 1, .8f, .05f, .05f, out _, out _);
        }
        Check(human.cultivation_inspiration_source_events.Count == 33 &&
            human.cultivation_inspiration_source_events.All(record => record.evaluated_chance > 0f && record.evaluated_chance >= .05f),
            "same-source soft diminishing keeps its configured nonzero floor");
    }

    private void check_expiry_boundary()
    {
        var human = new HumanData();
        var record = make_record("expiry-edge", 1, 0);
        human.cultivation_inspiration_records.Add(record);
        Check(CultivationInspirationSystem.get_available_records(human, HoursPerYear - 1).Count == 1,
            "one-year record remains available until the exact expiry boundary");
        Check(CultivationInspirationSystem.get_available_records(human, HoursPerYear).Count == 0 &&
            record.state_id == CultivationInspirationRecord.StateExpired,
            "record expires at its exact saved expiry hour");

        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var characterData = new HumanData { cultivation_realm = "云息三层" };
        var expiring = make_record("character-expiry", 1, now - HoursPerYear);
        expiring.expires_at_game_hour = now;
        characterData.cultivation_inspiration_records.Add(expiring);
        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(characterData);
        character.Hide();
        var notifications = 0;
        character.CultivationInspirationChanged += _ => notifications++;
        var remaining = character.get_cultivation_inspiration_records();
        Check(remaining.Count == 0 && notifications == 1 &&
            (character.entity_data as HumanData)?.cultivation_inspiration_records[0].state_id == CultivationInspirationRecord.StateExpired,
            "expiring a saved record emits the persistence-change notification");
        character.Free();
    }

    private void check_weighted_rank_selection()
    {
        Check(CultivationInspirationTuning.try_get_source_rule("field.gathering", out var rule),
            "gathering inspiration source has prototype rank weights");
        var human = new HumanData();
        var counts = new System.Collections.Generic.Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0 };
        const int eventCount = 96;
        for (var index = 0; index < eventCount; index++)
        {
            var eventId = $"rank-weight-event-{index}";
            var granted = CultivationInspirationSystem.try_award_renewable(
                human, eventId, rule.SourceId, rule.Label, rule.UseDomainId,
                index, 1f, .82f, 1f, rule.Rank2Weight, rule.Rank3Weight,
                out var record, out _);
            var expectedRank = expected_quality_rank(rule.SourceId, eventId, rule.Rank2Weight, rule.Rank3Weight);
            Check(granted && record is not null && record.quality_rank == expectedRank,
                $"weighted rank is deterministic for event {index}");
            counts[record!.quality_rank]++;
        }
        Check(counts.Values.Sum() == eventCount && counts[1] > counts[2] && counts[2] > counts[3] && counts[3] > 0,
            $"rank distribution follows configured 见几/明辨/通变 weights ({counts[1]}/{counts[2]}/{counts[3]})");
    }

    private void check_explicit_action_reservation()
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var data = create_stage_data(4);
        var first = make_record("reservation-combat-1", 1, now);
        first.use_domain_id = CultivationInspirationTuning.CombatDomain;
        var replacement = make_record("reservation-combat-2", 2, now);
        replacement.use_domain_id = CultivationInspirationTuning.CombatDomain;
        var otherDomain = make_record("reservation-travel", 3, now);
        otherDomain.use_domain_id = CultivationInspirationTuning.TravelDomain;
        data.cultivation_inspiration_records.Add(first);
        data.cultivation_inspiration_records.Add(replacement);
        data.cultivation_inspiration_records.Add(otherDomain);

        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(data);
        character.Hide();
        var storedRecords = (character.entity_data as HumanData)?.cultivation_inspiration_records;
        var storedFirst = storedRecords?.FirstOrDefault(record => record is not null && record.record_id == first.record_id);
        var storedReplacement = storedRecords?.FirstOrDefault(record => record is not null && record.record_id == replacement.record_id);
        var storedOtherDomain = storedRecords?.FirstOrDefault(record => record is not null && record.record_id == otherDomain.record_id);
        Check(storedFirst is not null && storedReplacement is not null && storedOtherDomain is not null,
            "loaded inspiration records are found in the character's persisted snapshot");
        Check(character.set_cultivation_inspiration_action_reservation(first.record_id, true),
            "player can explicitly reserve an insight for its matching action");
        Check(character.set_cultivation_inspiration_action_reservation(replacement.record_id, true) &&
            !storedFirst!.armed_for_action && storedReplacement!.armed_for_action,
            "reserving a same-domain record replaces that reservation");
        Check(!character.try_refine_cultivation_bottleneck(replacement.record_id, out _) &&
            storedReplacement!.state_id == CultivationInspirationRecord.StateAvailable,
            "a reserved action insight cannot be refined for the bottleneck");
        Check(character.set_cultivation_inspiration_action_reservation(replacement.record_id, false) &&
            !storedReplacement!.armed_for_action && storedReplacement.state_id == CultivationInspirationRecord.StateAvailable,
            "canceling an action reservation preserves the insight");
        Check(character.set_cultivation_inspiration_action_reservation(first.record_id, true) &&
            character.set_cultivation_inspiration_action_reservation(otherDomain.record_id, true),
            "separate action domains keep independent explicit reservations");
        Check(!character.try_consume_armed_cultivation_inspiration_for_action(
                CultivationInspirationTuning.StudyDomain, out _) &&
            storedFirst!.state_id == CultivationInspirationRecord.StateAvailable && storedFirst.armed_for_action,
            "a mismatched action cannot consume a reserved insight");
        Check(character.try_consume_armed_cultivation_inspiration_for_action(
                CultivationInspirationTuning.CombatDomain, out var consumed) && consumed == storedFirst &&
            storedFirst!.state_id == CultivationInspirationRecord.StateConsumed && !storedFirst.armed_for_action,
            "the matching action consumes exactly the explicitly reserved record at start");
        Check(storedOtherDomain!.armed_for_action &&
            storedOtherDomain.state_id == CultivationInspirationRecord.StateAvailable,
            "consuming one action domain leaves another domain reservation untouched");
        character.Free();
    }

    private void check_cross_domain_use_mapping()
    {
        Check(CultivationInspirationTuning.try_get_source_rule("combat.victory", out var combat) &&
            CultivationInspirationTuning.try_get_source_rule("field.gathering", out var gathering) &&
            CultivationInspirationTuning.try_get_source_rule("journey.step", out var travel) &&
            CultivationInspirationTuning.try_get_source_rule("study.progress", out var study) &&
            CultivationInspirationTuning.try_get_source_rule("sect.yaopu_watch", out var yaopu) &&
            combat.UseDomainId == CultivationInspirationTuning.StudyDomain &&
            gathering.UseDomainId == CultivationInspirationTuning.TravelDomain &&
            travel.UseDomainId == CultivationInspirationTuning.GatheringDomain &&
            study.UseDomainId == CultivationInspirationTuning.CombatDomain &&
            yaopu.UseDomainId == CultivationInspirationTuning.GatheringDomain,
            "combat, field, travel, study, and Yaopu experiences can strengthen a different matching action");
    }

    private void check_save_and_json_round_trip()
    {
        var human = create_stage_data(4);
        var record = make_record("saved-inspiration", 3, 24);
        record.source_event_id = "saved-event";
        record.source_id = "journey-test";
        record.source_label = "旅途中的观势";
        record.use_domain_id = "route_observation";
        record.armed_for_action = true;
        human.cultivation_inspiration_records.Add(record);
        human.cultivation_inspiration_source_events.Add(new CultivationInspirationSourceEventRecord
        {
            event_id = "saved-event", source_id = "journey-test", attempted_at_game_hour = 24,
            granted = true, granted_record_id = record.record_id, requested_quality_rank = 3,
            evaluated_chance = .4f, deterministic_roll = .2f
        });

        var path = $"user://cultivation_inspiration_smoke_{Guid.NewGuid():N}.tres";
        Check(ResourceSaver.Save(human, path) == Error.Ok, "inspiration state writes to a resource save");
        var restored = ResourceLoader.Load<HumanData>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
        Check(restored is not null && restored.cultivation_inspiration_records.Count == 1 &&
            restored.cultivation_inspiration_records[0].record_id == "saved-inspiration" &&
            restored.cultivation_inspiration_records[0].source_event_id == "saved-event" &&
            restored.cultivation_inspiration_records[0].source_label == "旅途中的观势" &&
            restored.cultivation_inspiration_records[0].quality_rank == 3 &&
            restored.cultivation_inspiration_records[0].state_id == CultivationInspirationRecord.StateAvailable &&
            restored.cultivation_inspiration_records[0].armed_for_action &&
            restored.cultivation_inspiration_records[0].expires_at_game_hour == record.expires_at_game_hour &&
            restored.cultivation_inspiration_source_events.Count == 1 &&
            restored.cultivation_bottleneck_current_penalty == human.cultivation_bottleneck_current_penalty,
            "records, source ledger, expiry, and bottleneck survive save/load");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));

        var json = Json.Stringify(new Dictionary
        {
            ["record_id"] = record.record_id,
            ["source_event_id"] = record.source_event_id,
            ["source_id"] = record.source_id,
            ["source_label"] = record.source_label,
            ["use_domain_id"] = record.use_domain_id,
            ["quality_rank"] = record.quality_rank,
            ["acquired_at_game_hour"] = record.acquired_at_game_hour,
            ["expires_at_game_hour"] = record.expires_at_game_hour,
            ["state_id"] = record.state_id,
            ["armed_for_action"] = record.armed_for_action,
            ["consumed_for"] = record.consumed_for,
            ["consumed_at_game_hour"] = record.consumed_at_game_hour
        });
        var parsed = Json.ParseString(json).AsGodotDictionary();
        Check(parsed.GetValueOrDefault("record_id", "").AsString() == record.record_id &&
            parsed.GetValueOrDefault("source_label", "").AsString() == record.source_label &&
            parsed.GetValueOrDefault("use_domain_id", "").AsString() == record.use_domain_id &&
            parsed.GetValueOrDefault("quality_rank", 0).AsInt32() == record.quality_rank &&
            parsed.GetValueOrDefault("expires_at_game_hour", 0L).AsInt64() == record.expires_at_game_hour &&
            parsed.GetValueOrDefault("state_id", "").AsString() == record.state_id &&
            parsed.GetValueOrDefault("armed_for_action", false).AsBool(),
            "all player-facing record fields survive JSON round-trip");
    }

    private void check_yuanxi_exemption()
    {
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var data = create_stage_data(8);
        data.cultivation_realm = "云息十层";
        data.cultivation_inspiration_records.Add(make_record("yuanxi-record", 3, now));
        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(data);
        character.Hide();
        Check(!character.get_cultivation_bottleneck_summary().GetValueOrDefault("applicable", true).AsBool(),
            "Yuanxi stage has no applicable bottleneck");
        Check(!character.try_refine_cultivation_bottleneck("yuanxi-record", out _) &&
            (character.entity_data as HumanData)?.cultivation_inspiration_records[0].state_id == CultivationInspirationRecord.StateAvailable,
            "Yuanxi does not consume an inspiration for bottleneck relief");
        character.Free();
    }

    private void check_room_integration()
    {
        var clock = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        var startHours = clock?.get_total_game_hours() ?? 0;
        var data = create_stage_data(4);
        data.cultivation_inspiration_records.Add(make_record("room-refinement", 2, startHours));
        var character = new Character { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(character);
        character.load_from_data(data);
        character.Hide();
        var room = new CultivationUI();
        AddChild(room);
        room.show_entity(character);
        FindButton(room, "灵感·瓶颈").EmitSignal(BaseButton.SignalName.Pressed);
        Check(room.GetNode<Control>("QuietRoom/RoomPages/InspirationPage").IsVisibleInTree(), "静室灵感页签打开对应页面");
        var recordButton = Nodes<Button>(room).FirstOrDefault(button => button.Text.StartsWith("灵感·明辨", StringComparison.Ordinal));
        Check(recordButton is not null && recordButton.Text.Contains("来源：测试经历", StringComparison.Ordinal) &&
            recordButton.Text.Contains("用途方向：test-action", StringComparison.Ordinal) && recordButton.Text.Contains("有效至：", StringComparison.Ordinal),
            "记录行显示品级、来源、用途方向与期限");
        var beforePenalty = character.get_cultivation_bottleneck_summary().GetValueOrDefault("penalty", 0d).AsDouble();
        FindButton(room, "炼化选中灵感").EmitSignal(BaseButton.SignalName.Pressed);
        var afterPenalty = character.get_cultivation_bottleneck_summary().GetValueOrDefault("penalty", 0d).AsDouble();
        Check(afterPenalty < beforePenalty &&
            (character.entity_data as HumanData)?.cultivation_inspiration_records[0].state_id == CultivationInspirationRecord.StateConsumed,
            "静室按钮恰好消费选中记录并更新瓶颈减速");
        Check((clock?.get_total_game_hours() ?? 0) == startHours, "打开与使用灵感页不推进时间");
        room.Free();
        character.Free();
    }

    private void check_pacing_projection()
    {
        var noInspiration = CultivationPacingAudit.Simulate(false, 0d);
        var quarterForActions = CultivationPacingAudit.Simulate(true, .25d);
        var allForRefinement = CultivationPacingAudit.Simulate(true, 0d);
        Check(noInspiration.GrantedRecordCount == 0 && noInspiration.RefinementUses == 0 && noInspiration.ActionUses == 0,
            "pacing baseline models the same activity schedule without insight rewards");
        Check(quarterForActions.TotalYears is >= 12d and <= 15d &&
            quarterForActions.ActionUses > 0 && quarterForActions.RefinementUses > 0,
            $"configured 25% matching-action use reaches Cloud Breath ten and Yuanxi nine within 12–15 game years (actual={quarterForActions.TotalYears:F4}y, cloud10={quarterForActions.ToCloudTenYears:F4}y, records={quarterForActions.GrantedRecordCount}, refine={quarterForActions.RefinementUses}, actions={quarterForActions.ActionUses})");
        Check(quarterForActions.GrantedByRank.Values.Sum() == quarterForActions.GrantedRecordCount &&
            quarterForActions.GrantedRecordCount == quarterForActions.RefinementUses + quarterForActions.ActionUses +
                quarterForActions.ExpiredRecords + quarterForActions.EndingAvailableRecords,
            "pacing accounting tracks generated records as refinement, action use, expiry, or still available");

        static string sourceCounts(CultivationPacingAudit.Result result)
            => string.Join(",", result.SourceEventCounts.Select(pair => $"{pair.Key}={pair.Value}"));
        static string rankCounts(CultivationPacingAudit.Result result)
            => string.Join(",", result.GrantedByRank.Select(pair => $"{pair.Key}:{pair.Value}"));
        GD.Print($"CULTIVATION_PACING no-insight total={noInspiration.TotalYears:F4}y cloud10={noInspiration.ToCloudTenYears:F4}y cultivation={noInspiration.CultivationDays:F1}d bottle={noInspiration.CappedBottleneckDays:F1}d months={noInspiration.CompletedMonths} events=[{sourceCounts(noInspiration)}]");
        GD.Print($"CULTIVATION_PACING action25 total={quarterForActions.TotalYears:F4}y cloud10={quarterForActions.ToCloudTenYears:F4}y cultivation={quarterForActions.CultivationDays:F1}d bottle={quarterForActions.CappedBottleneckDays:F1}d months={quarterForActions.CompletedMonths} records={quarterForActions.GrantedRecordCount} ranks=[{rankCounts(quarterForActions)}] refine={quarterForActions.RefinementUses} actions={quarterForActions.ActionUses} expired={quarterForActions.ExpiredRecords} available={quarterForActions.EndingAvailableRecords} endBottle={quarterForActions.EndingBottleneckLevel}/8 penalty={quarterForActions.EndingPenalty:P2} sources=[{string.Join(",", quarterForActions.GrantedBySource.Select(pair => $"{pair.Key}={pair.Value}"))}] events=[{sourceCounts(quarterForActions)}]");
        GD.Print($"CULTIVATION_PACING refinement-only total={allForRefinement.TotalYears:F4}y cloud10={allForRefinement.ToCloudTenYears:F4}y cultivation={allForRefinement.CultivationDays:F1}d bottle={allForRefinement.CappedBottleneckDays:F1}d months={allForRefinement.CompletedMonths} records={allForRefinement.GrantedRecordCount} ranks=[{rankCounts(allForRefinement)}] refine={allForRefinement.RefinementUses} actions={allForRefinement.ActionUses} expired={allForRefinement.ExpiredRecords} available={allForRefinement.EndingAvailableRecords}");
    }

    private static HumanData create_stage_data(int level)
    {
        var tier = 3;
        var accumulated = CultivationBottleneckRules.get_grace_hours(tier) + level * CultivationBottleneckRules.get_stage_interval_hours();
        return new HumanData
        {
            entity_name = "灵感验收临时人物",
            cultivation_realm = "云息三层",
            cultivation_bottleneck_accumulated_hours = accumulated,
            cultivation_bottleneck_level = level,
            cultivation_bottleneck_realm_tier = tier,
            cultivation_bottleneck_current_penalty = CultivationBottleneckRules.get_penalty_for_level(level)
        };
    }

    private static CultivationInspirationRecord make_record(string id, int rank, long now) => new()
    {
        record_id = id,
        source_event_id = "event:" + id,
        source_id = "source:" + id,
        source_label = "测试经历",
        use_domain_id = "test-action",
        quality_rank = rank,
        acquired_at_game_hour = now,
        expires_at_game_hour = now + HoursPerYear * (rank == 3 ? 2 : 1),
        state_id = CultivationInspirationRecord.StateAvailable
    };

    private static int expected_quality_rank(string sourceId, string eventId, float rank2Weight, float rank3Weight)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceId}:rank\n{eventId}"));
        var raw = BinaryPrimitives.ReadUInt64LittleEndian(hash);
        var roll = raw / 18446744073709551616d;
        if (roll < rank3Weight) return 3;
        if (roll < rank3Weight + rank2Weight) return 2;
        return 1;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("INSPIRATION_CHECK_OK " + message);
    }

    private static IEnumerable<T> Nodes<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var nested in Nodes<T>(child)) yield return nested;
        }
    }

    private static Button FindButton(Node node, string text)
        => Nodes<Button>(node).FirstOrDefault(button => button.Text == text)
            ?? throw new InvalidOperationException("missing button: " + text);
}
