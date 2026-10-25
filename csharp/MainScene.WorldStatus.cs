using Godot;

/// <summary>主角大世界状态的存档入口与游戏时辰到期处理。</summary>
public partial class MainScene
{
    public bool add_player_world_status(WorldStatusRecord record)
    {
        if (player?.entity_data is not HumanData data ||
            !WorldStatusRules.add(data.world_status_records, record, current_world_status_hour()))
            return false;
        mark_save_dirty();
        refresh_player_world_status_ui();
        return true;
    }

    public bool remove_player_world_status(string instanceId)
    {
        if (player?.entity_data is not HumanData data ||
            !WorldStatusRules.remove(data.world_status_records, instanceId))
            return false;
        mark_save_dirty();
        refresh_player_world_status_ui();
        return true;
    }

    private void apply_player_severe_injury(Godot.Collections.Dictionary summary, Godot.Collections.Dictionary context)
    {
        if (player?.entity_data is not HumanData data ||
            !summary.GetValueOrDefault("player_defeated", false).AsBool() ||
            summary.GetValueOrDefault("escaped", false).AsBool() ||
            context.GetValueOrDefault("is_life_and_death", false).AsBool() ||
            context.GetValueOrDefault("is_training", false).AsBool() ||
            context.GetValueOrDefault("kind", "").AsString() == "yuxu_tianwen_practice")
            return;

        var requiredHours = WorldStatusRules.severe_injury_recovery_hours_for_realm(player.cultivation_realm);
        if (requiredHours <= 0)
        {
            GD.PushWarning($"尚未配置境界“{player.cultivation_realm}”的重伤休养时长；本次不擅自推定。");
            return;
        }

        var sourceName = context.GetValueOrDefault("title", "").AsString();
        if (string.IsNullOrWhiteSpace(sourceName))
            sourceName = context.GetValueOrDefault("kind", "").AsString() switch
            {
                "east_woods_npc_encounter" => "东林地沿途冲突",
                "east_woods_beast" => "东林地妖兽遭遇",
                _ => "正式战斗"
            };
        var existing = data.world_status_records.FirstOrDefault(record =>
            record is not null && record.status_id == WorldStatusRules.SevereInjuryId);
        if (existing is null)
        {
            existing = new WorldStatusRecord
            {
                status_id = WorldStatusRules.SevereInjuryId,
                display_name = "重伤",
                description = "战斗中生命上限暂降 15%；需通过实际休息行动调养恢复。",
                stack_count = 1,
                source_id = summary.GetValueOrDefault("battle_id", "").AsString(),
                source_name = sourceName,
                applied_at_total_game_hours = current_world_status_hour(),
                expires_at_total_game_hours = WorldStatusRules.PermanentExpiry,
                recovery_hours_required = requiredHours,
                recovery_hours_progress = 0,
                recovery_realm = player.cultivation_realm
            };
            if (!WorldStatusRules.add(data.world_status_records, existing, current_world_status_hour())) return;
        }
        else
        {
            // 重伤不叠层；再次战败以新伤为准，重置休养进度并记录最新缘起。
            existing.display_name = "重伤";
            existing.description = "战斗中生命上限暂降 15%；需通过实际休息行动调养恢复。";
            existing.stack_count = 1;
            existing.source_id = summary.GetValueOrDefault("battle_id", "").AsString();
            existing.source_name = sourceName;
            existing.applied_at_total_game_hours = current_world_status_hour();
            existing.expires_at_total_game_hours = WorldStatusRules.PermanentExpiry;
            existing.recovery_hours_required = requiredHours;
            existing.recovery_hours_progress = 0;
            existing.recovery_realm = player.cultivation_realm;
        }
        mark_save_dirty();
        refresh_player_world_status_ui();
    }

    private void advance_player_world_status_rest(int restHours)
    {
        if (player?.entity_data is not HumanData data) return;
        var changed = player.RestPillToxicity(restHours);
        if (WorldStatusRules.advance_rest_hours(data.world_status_records, restHours) == 0 && !changed) return;
        mark_save_dirty();
        refresh_player_world_status_ui();
    }

    private void refresh_player_world_status_ui()
    {
        if (player is null) return;
        var hour = current_world_status_hour();
        hud?.refresh_player_status();
        hud?.refresh_player_world_status_details(player, hour);
        info?.refresh_player_world_status_details(player, hour);
    }

    public float get_player_world_battle_health_cap_ratio()
        => player?.entity_data is HumanData data && WorldStatusRules.active_records(data.world_status_records, current_world_status_hour())
            .Any(record => record.status_id == WorldStatusRules.SevereInjuryId)
                ? WorldStatusRules.SevereInjuryBattleHealthCapRatio : 1f;

    private long current_world_status_hour()
        => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;

    private void on_world_status_time_changed()
    {
        if (player?.entity_data is not HumanData data) return;
        var changed = player.SettlePillToxicity();
        if (WorldStatusRules.expire_due(data.world_status_records, current_world_status_hour()) == 0 && !changed) return;
        mark_save_dirty();
        refresh_player_world_status_ui();
    }

    private void expire_player_world_statuses()
    {
        if (player?.entity_data is not HumanData data) return;
        var changed = player.SettlePillToxicity();
        if (WorldStatusRules.expire_due(data.world_status_records, current_world_status_hour()) > 0 || changed)
        {
            mark_save_dirty();
            refresh_player_world_status_ui();
        }
    }

    public void notify_pill_toxicity_changed(Character character)
    {
        mark_save_dirty();
        if (character == player) refresh_player_world_status_ui();
    }
}
