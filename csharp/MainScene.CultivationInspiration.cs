using Godot;

/// <summary>Activity result hooks and save notifications for persistent inspiration.</summary>
public partial class MainScene
{
    private long _worldMapTravelStartedHour = -1;
    private int _worldMapTravelRevealBonus;

    private void on_player_cultivation_inspiration_changed(Character character)
    {
        if (character != player) return;
        mark_save_dirty();
        refresh_player_world_status_ui();
    }

    public int consume_player_inspiration_for_action(string actionDomainId)
    {
        if (player?.try_consume_armed_cultivation_inspiration_for_action(actionDomainId, out var consumed) != true || consumed is null)
            return 0;
        mark_save_dirty();
        return consumed.quality_rank;
    }

    private CultivationInspirationRecord? award_player_renewable_inspiration(string stableEventId, string sourceId)
    {
        if (player is null) return null;
        return player.try_grant_renewable_cultivation_inspiration(stableEventId, sourceId, out var awarded, out _) ? awarded : null;
    }

    private void on_tile_world_travel_started()
    {
        _tileWorldTravelInProgress = true;
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.suspend_world_npcs();
        _worldMapTravelStartedHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        var consumeTravelInspiration = true;
        if (CultivationInspirationSystem.Enabled && player?.entity_data is HumanData human && _npc_world?.get_active_yuxu_case_transport(human) is { temporary_party_member_active: true } task)
        {
            if (task.journey_inspiration_consumed) consumeTravelInspiration = false;
            else
            {
                task.journey_inspiration_consumed = true;
                mark_save_dirty();
            }
        }
        var rank = consumeTravelInspiration ? consume_player_inspiration_for_action(CultivationInspirationTuning.TravelDomain) : 0;
        _worldMapTravelRevealBonus = CultivationInspirationTuning.travel_reveal_bonus_cells(rank);
        if (_nodeWorldMap?.HasMethod("set_travel_reveal_bonus_cells") == true)
            _nodeWorldMap.Call("set_travel_reveal_bonus_cells", _worldMapTravelRevealBonus);
    }

    private void on_tile_world_travel_finished(int completedSteps)
    {
        _tileWorldTravelInProgress = false;
        if (_nodeWorldMap?.HasMethod("set_travel_reveal_bonus_cells") == true)
            _nodeWorldMap.Call("set_travel_reveal_bonus_cells", 0);
        _worldMapTravelRevealBonus = 0;
        if (completedSteps > 0)
        {
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            var hour = time?.get_total_game_hours() ?? 0;
            var node = _nodeWorldMap?.Call("get_current_node").AsString() ?? "";
            award_player_renewable_inspiration($"travel:world:{_worldMapTravelStartedHour}:{node}:{hour}", "journey.step");
        }
        _worldMapTravelStartedHour = -1;
        refresh_node_world_character_list(force: true);
        synchronize_dynamic_dungeon_opportunities();

        if (player?.entity_data is HumanData human && _npc_world?.get_active_yuxu_case_transport(human) is { } task &&
            task.temporary_party_member_active && task.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit &&
            _player_world_node_id == task.journey_destination_node_id)
        {
            _npc_world.mark_yuxu_case_transport_arrived(
                human.sect_active_yuxu_duty_incident_id, task.task_id, human.sect_active_yuxu_duty_instance_id,
                task.journey_destination_node_id);
            mark_save_dirty();
            refresh_yuxu_life_context();
            show_message("案卷押送抵达", "队伍已抵达执中峰所在地点。请进入执中峰，并由指定官员完成交接；抵达本身不算交接。");
        }
    }

    private void award_successful_combat_inspiration(Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        var kind = context.GetValueOrDefault("kind", "").AsString();
        if (!summary.GetValueOrDefault("player_victory", false).AsBool() ||
            summary.GetValueOrDefault("escaped", false).AsBool() ||
            context.GetValueOrDefault("is_training", false).AsBool() ||
            kind is "yuxu_tianwen_practice" or "yuxu_npc_spar" or "yuxu_provisional_exam_trial" ||
            (kind == "east_woods_npc_encounter" && !context.GetValueOrDefault("target_player", false).AsBool()))
            return;
        var battleId = summary.GetValueOrDefault("battle_id", "").AsString();
        if (!string.IsNullOrWhiteSpace(battleId))
            award_player_renewable_inspiration($"combat:{battleId}", "combat.victory");
    }

    private void on_yuxu_official_duty_started(HumanData human, string dutyId, string dutyInstanceId)
    {
        human.sect_active_yuxu_inspiration_bonus = 0;
        if (dutyId == YuxuEastWoodsConflictReviewDutyId || player is null) return;
        if (player.try_consume_armed_cultivation_inspiration_for_action(
                CultivationInspirationTuning.SectDutyDomain, out var consumed) && consumed is not null)
        {
            human.sect_active_yuxu_inspiration_bonus = CultivationInspirationTuning.sect_contribution_bonus(consumed.quality_rank);
            mark_save_dirty();
        }
    }

    private void on_yuxu_official_duty_settled(HumanData human, string stableTransactionId, string dutyTitle)
    {
        if (human.sect_active_yuxu_duty_id == YuxuEastWoodsConflictReviewDutyId || string.IsNullOrWhiteSpace(stableTransactionId)) return;
        award_player_renewable_inspiration($"sect-duty:{stableTransactionId}", "sect.official_duty");
    }

    private void on_yuxu_daily_activity_completed(string stableEventId)
    {
        award_player_renewable_inspiration(stableEventId, "sect.yaopu_watch");
    }
}
