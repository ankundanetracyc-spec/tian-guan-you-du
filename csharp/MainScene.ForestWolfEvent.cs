using Godot;
using System;
using System.Linq;
using GDictionary = Godot.Collections.Dictionary;

public partial class MainScene
{
    private bool _forestChoiceBusy;
    private void bind_forest_wolf_event(EastWoodsDungeonPanel panel)
    {
        panel.ForestWolfChoiceRequested += on_forest_wolf_choice;
        panel.ForestWolfStateChanged += save_forest_wolf_state;
        var transition = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        Action<GDictionary> cancelled = context =>
        {
            if (!IsInsideTree() || context.GetValueOrDefault("kind", "").AsString() != "east_woods_beast" ||
                !context.ContainsKey("forest_event_key") || player is null) return;
            var record = panel.forest_record(context["forest_event_key"].AsString());
            if (record.Count == 0) return;
            panel.show_forest_event(record, player.get_snapshot_data()); save_forest_wolf_state();
        };
        if (transition is not null)
        {
            transition.BriefingCancelled += cancelled;
            panel.TreeExiting += () => { if (IsInstanceValid(transition)) transition.BriefingCancelled -= cancelled; };
        }
        Action modeChanged = () =>
        {
            if (player is null || !panel.has_open_forest_event) return;
            var record = panel.open_forest_record();
            if (record.Count > 0) panel.show_forest_event(record, player.get_snapshot_data());
        };
        PlayerInfoText.ModeChanged += modeChanged;
        panel.TreeExiting += () => PlayerInfoText.ModeChanged -= modeChanged;
    }
    private void save_forest_wolf_state()
    {
        if (_east_woods_dungeon is null) return;
        _east_woods_dungeon_state = _east_woods_dungeon.get_route_state(); mark_save_dirty();
    }
    private GDictionary ensure_forest_wolf_record(EastWoodsActions.ActionDef action, string node = ForestWolfEvent.NodeId)
    {
        var dungeon = _east_woods_dungeon!;
        var record = dungeon.forest_record(ForestWolfEvent.Key(action, node));
        if (record.Count > 0)
        {
            if (ForestWolfEvent.Wolf(record) is null) throw new InvalidOperationException("此遭遇的凶狼资料缺失，未重新抽取。");
            return record;
        }
        var template = GD.Load<BeastTemplate>("res://resources/beast_templates/林地凶狼.tres");
        var wolf = new Beast
        {
            Name = "ForestEventWolf", unique_id = "temp_enemy_east_woods_" + (action.EncounterIdentity.Length > 0 ? action.EncounterIdentity : node + "_" + action.Id),
            beast_template = template, entity_data = new LivingData(), faction = (int)WorldEntity.Faction.ENEMY, Visible = false,
        };
        AddChild(wolf);
        var snapshot = wolf.get_snapshot_data(); RemoveChild(wolf); wolf.Free();
        snapshot.dun_speed = 100; // The existing East Woods encounter value.
        record = ForestWolfEvent.Create(action, snapshot, node);
        record["lead_in"] = dungeon.previous_forest_contact(node);
        dungeon.store_forest_record(record); save_forest_wolf_state();
        return record;
    }
    private void on_forest_wolf_choice(string node, string key, string choice)
    {
        var dungeon = _east_woods_dungeon;
        if (_forestChoiceBusy || dungeon is null || dungeon.is_dynamic_instance || player is null ||
            !ForestWolfEvent.Supports(node) || node != dungeon.get_current_route_node_id() || GetTree().Paused) return;
        _forestChoiceBusy = true;
        try
        {
            var record = key.Length > 0 ? dungeon.forest_record(key) : new GDictionary();
            if (choice != "open" && (record.Count == 0 || !dungeon.has_open_forest_event ||
                !ForestWolfEvent.Describe(record, player.get_snapshot_data()).Choices.Any(option => option.Id == choice))) return;
            if (choice == "continue")
            {
                if (record.Count == 0) return;
                var stage = record["stage"].AsString();
                if (stage != "retreated" && stage != "drive_success" && !stage.StartsWith("battle_", StringComparison.Ordinal)) return;
                if (record.GetValueOrDefault("outcome", "active").AsString() == "active") record["stage"] = ForestWolfEvent.ResumeStage(record);
                dungeon.store_forest_record(record); dungeon.close_action_details(); save_forest_wolf_state(); return;
            }
            var action = dungeon.available_actions(node).FirstOrDefault(a => a.Battle && (key.Length == 0 || ForestWolfEvent.Key(a, node) == key));
            if (action is null) { dungeon.forest_event_notice("现场已发生变化，这处遭遇已不再活跃。"); return; }
            record = ensure_forest_wolf_record(action, node);
            if (choice == "open")
            {
                if (record["stage"].AsString() == "retreated" || record["stage"].AsString().StartsWith("battle_", StringComparison.Ordinal))
                    record["stage"] = ForestWolfEvent.ResumeStage(record);
                dungeon.show_forest_event(record, player.get_snapshot_data()); save_forest_wolf_state(); return;
            }
            if (choice == "retreat")
            {
                record["stage"] = "retreated";
                dungeon.show_forest_event(record, player.get_snapshot_data()); save_forest_wolf_state(); return;
            }
            if (choice == "fight")
            {
                dungeon.close_action_details(); save_forest_wolf_state();
                start_east_woods_beast(node, action); return;
            }
            if (!ForestWolfEvent.CanAttempt(record, choice)) return;
            var input = player.get_snapshot_data();
            _ = ForestWolfEvent.Chance(record, choice, input); // Validate before charging.
            var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
            if (time is null) { dungeon.forest_event_notice("历法尚未就绪，本次行动没有开始。"); return; }
            if (!player.try_spend_world_stamina(ForestWolfEvent.Stamina))
            { dungeon.forest_event_notice("精力不足。本次行动需要1点精力，未消耗时间。"); return; }
            var remaining = ForestWolfEvent.Hours;
            while (remaining > .0001f)
            {
                var slice = Math.Min(remaining, next_east_woods_npc_step());
                time.advance_time(slice, "east_woods_action"); remaining -= slice;
                var encounter = _npc_world?.advance_east_woods_route(node, node, slice, player.entity_data as HumanData) ?? new GDictionary();
                if (encounter.Count == 0) continue;
                dungeon.close_action_details(); dungeon.interrupt_travel(); save_forest_wolf_state();
                refresh_east_woods_route_roster(node); CallDeferred(nameof(start_east_woods_encounter), encounter); return;
            }
            // Time callbacks may have replaced the panel's dictionaries. Read the latest state.
            record = dungeon.forest_record(ForestWolfEvent.Key(action, node));
            if (!dungeon.available_actions(node).Any(a => a.Battle && ForestWolfEvent.Key(a, node) == ForestWolfEvent.Key(action, node)) ||
                !ForestWolfEvent.CanAttempt(record, choice))
            { dungeon.forest_event_notice("现场已变化，未提交本次判定。"); save_forest_wolf_state(); return; }
            if (!ForestWolfEvent.Resolve(record, choice, input, Math.Min(GD.Randf(), Math.BitDecrement(1d)))) return;
            dungeon.store_forest_record(record);
            if (record.GetValueOrDefault("outcome", "active").AsString() == "driven_off")
                dungeon.mark_forest_wolf_driven_off(record, time.get_total_game_hours());
            dungeon.show_forest_event(record, player.get_snapshot_data());
            save_forest_wolf_state(); refresh_east_woods_route_roster(node);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        { dungeon.forest_event_notice(error.Message); }
        finally { _forestChoiceBusy = false; }
    }
    private bool restore_forest_wolf_battle_result(GDictionary summary, GDictionary context)
    {
        if (!context.ContainsKey("forest_event_key") || player is null) return false;
        var dungeon = get_or_create_east_woods_dungeon();
        var key = context["forest_event_key"].AsString();
        var battleId = summary.GetValueOrDefault("battle_id", "").AsString();
        var previous = dungeon.forest_record(key);
        if (battleId.Length > 0 && previous.GetValueOrDefault("last_battle_id", "").AsString() == battleId) return true;
        dungeon.restore_route_state(context["route_state"].AsGodotDictionary());
        var record = dungeon.forest_record(key);
        if (record.Count == 0) return false;
        var victory = summary.GetValueOrDefault("player_victory", false).AsBool();
        if (victory)
        {
            dungeon.complete_action(record["node"].AsString(), record["action"].AsString());
            record["outcome"] = "defeated";
        }
        if (context.GetValueOrDefault("forest_wolf_result", default(Variant)).AsGodotObject() is BattleEntityData wolfResult)
            record["wolf"] = wolfResult.Duplicate(true);
        record["stage"] = victory ? "battle_win" : summary.GetValueOrDefault("escaped", false).AsBool() ? "battle_escape"
            : summary.GetValueOrDefault("is_draw", false).AsBool() ? "battle_draw" : "battle_loss";
        record["last_battle_id"] = battleId;
        dungeon.store_forest_record(record);
        dungeon.set_player_dun_speed(player.dun_speed);
        dungeon.show_dungeon();
        dungeon.show_forest_event(record, player.get_snapshot_data());
        save_forest_wolf_state(); refresh_east_woods_route_roster(record["node"].AsString());
        return true;
    }
}
