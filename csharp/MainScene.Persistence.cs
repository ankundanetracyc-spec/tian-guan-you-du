using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>Save/load integration for the active single-player run.</summary>
public partial class MainScene
{
    public void notify_battle_rewards_changed()
    {
        hud?.refresh_player_status();
        mark_save_dirty();
    }

    private GameSaveManager? _save_manager;
    private PersistentGameSave? _loaded_game_save;
    private YuxuSectTreasuryLedger _yuxu_sect_treasury_ledger = new();
    private Godot.Collections.Dictionary _dynamic_dungeon_state = [];
    private bool _save_is_dirty;
    private double _save_dirty_seconds;
    private bool _returning_from_battle;
    private bool _skipExitSave;

    public bool has_unsaved_progress => _save_is_dirty;

    public bool save_from_gameplay_menu() => save_progress_now(false);

    public bool prepare_return_to_title_from_menu(bool saveFirst)
    {
        if (saveFirst && !save_progress_now(false)) return false;
        _skipExitSave = true;
        return true;
    }

    public void cancel_return_to_title_from_menu() => _skipExitSave = false;

    private void prepare_save_session()
    {
        _save_manager = GetNodeOrNull<GameSaveManager>("/root/GameSaveManager");
        _loaded_game_save = GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.consume_world_progress_snapshot();
        _returning_from_battle = _loaded_game_save is not null;
        _loaded_game_save ??= _save_manager?.consume_pending_load();
        _letterMailbox = _loaded_game_save?.letter_mailbox?.Duplicate(true) as LetterMailbox ?? new LetterMailbox();
        _yuxu_sect_treasury_ledger = new YuxuSectTreasuryLedger();
        if (_loaded_game_save is null)
        {
            GetNodeOrNull<WeatherMgr>("/root/WeatherManager")?.restore_persistent_state("", 0.0f, 0.0f, 0.0f);
            EastWoodsDungeonRules.generate_layout(System.Guid.NewGuid().ToString("N"));
            _east_woods_dungeon_state = new Dictionary { ["layout"] = EastWoodsDungeonRules.get_layout_state() };
            _dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(
                GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
            return;
        }
        _yuxu_sect_treasury_ledger = _loaded_game_save.yuxu_sect_treasury?.Duplicate(true) as YuxuSectTreasuryLedger ?? new YuxuSectTreasuryLedger();
        if (_loaded_game_save.player_data is { } savedPlayer &&
            !string.IsNullOrWhiteSpace(savedPlayer.sect_active_yuxu_duty_id) &&
            string.IsNullOrWhiteSpace(savedPlayer.sect_active_yuxu_duty_instance_id))
        {
            savedPlayer.sect_active_yuxu_duty_instance_id = System.Guid.NewGuid().ToString("N");
            _save_is_dirty = true;
        }
        var battleTransition=GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager");
        if (!_returning_from_battle)
        {
            battleTransition?.restore_reward_ledger(_loaded_game_save.battle_reward_ledger);
            battleTransition?.CallDeferred("resume_pending_loot");
        }

        var npcManager = GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager");
        npcManager?.set_simulation_enabled(false);

        GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.restore_calendar(
            _loaded_game_save.year,
            _loaded_game_save.month,
            _loaded_game_save.day,
            _loaded_game_save.hour,
            _loaded_game_save.pending_hours);
        GetNodeOrNull<WeatherMgr>("/root/WeatherManager")?.restore_persistent_state(
            _loaded_game_save.weather_type,
            _loaded_game_save.weather_intensity,
            _loaded_game_save.weather_wind_speed,
            _loaded_game_save.weather_duration_hours);

        var currentGameHour = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        _dynamic_dungeon_state = _loaded_game_save.dynamic_dungeon_state?.Duplicate(true) as Dictionary
            ?? DynamicDungeonExpedition.CreateEmpty(currentGameHour);
        if (_dynamic_dungeon_state.Count == 0)
            _dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(currentGameHour);

        _player_world_grid_cell = _loaded_game_save.world_cell;
        _player_world_node_id = string.IsNullOrWhiteSpace(_loaded_game_save.current_node_id)
            ? "home_village"
            : _loaded_game_save.current_node_id;
        restore_scene_progress(_loaded_game_save.scene_progress);
        _save_is_dirty = true;
    }

    private void restore_npc_save_state()
    {
        if (_loaded_game_save is null) return;
        _npc_world?.restore_persistent_state(
            _loaded_game_save.world_npcs,
            _loaded_game_save.npc_simulation_enabled,
            _loaded_game_save.npc_last_processed_month,
            _loaded_game_save.east_woods_npc_incidents,
            _loaded_game_save.npc_population_version);
    }

    private void restore_map_save_state()
    {
        if (_loaded_game_save is null || tile_world_map is null) return;
        tile_world_map.Call(
            "restore_persistent_state",
            _loaded_game_save.world_cell,
            _loaded_game_save.explored_map_cells,
            _loaded_game_save.remaining_travel_path);
    }

    private void spawn_or_restore_player()
    {
        if (_loaded_game_save?.player_data is not { } savedPlayer)
        {
            spawn_player();
            return;
        }

        player = new Character();
        player.unique_id = _loaded_game_save.scene_progress.GetValueOrDefault("player_unique_id", "").AsString();
        AddChild(player);
        player.load_from_data(savedPlayer.Duplicate(true) as HumanData ?? savedPlayer);
        player.entity_type = WorldEntity.EntityType.CHARACTER;
        player.faction = (int)WorldEntity.Faction.PLAYER;
    }

    private void show_loaded_game()
    {
        if (_loaded_game_save is null) return;
        refresh_qingxi_quest();
        var departed = has_open_world_access();
        hud?.set_world_map_available(departed);
        set_prologue_skip_available(false);

        if (departed)
        {
            if (_npc_world is not null && node_map?.map_data is { } populationMap && CloudStateNpcCatalog.ensure_population(_npc_world, populationMap))
                mark_save_dirty();
            // A saved random roster is authoritative; never regenerate it on load.
            _cloud_state_initialized = true;
            if (DynamicDungeonExpedition.FindInsideInstance(_dynamic_dungeon_state) is { } activeDynamic)
            {
                estate?.Hide();
                tile_world_map?.Hide();
                node_map?.Hide();
                open_dynamic_dungeon(activeDynamic.GetValueOrDefault("instance_id", "").AsString());
                hud?.Show();
                mark_save_dirty();
                return;
            }
            if (_east_woods_dungeon_state.GetValueOrDefault("inside", false).AsBool())
            {
                estate?.Hide();
                tile_world_map?.Hide();
                node_map?.Hide();
                open_east_woods_dungeon();
                hud?.Show();
                mark_save_dirty();
                if (!_returning_from_battle)
                    CallDeferred(nameof(resume_yuxu_case_transport_after_load));
                return;
            }
            open_world();
            var transportTask = player?.entity_data is HumanData human ? _npc_world?.get_active_yuxu_case_transport(human) : null;
            if (transportTask?.journey_status == YuxuSectCaseTaskRecord.JourneyIntercepted)
            {
                if (!_returning_from_battle)
                    CallDeferred(nameof(resume_yuxu_case_transport_after_load));
            }
            else if (transportTask?.journey_status == YuxuSectCaseTaskRecord.JourneyInTransit)
            {
                tile_world_map?.Call("set_assigned_route_destination", transportTask.journey_destination_node_id);
                if (_loaded_game_save.remaining_travel_path.Count > 1)
                    tile_world_map?.CallDeferred("resume_saved_travel");
                else
                    CallDeferred(nameof(resume_yuxu_case_transport_after_load));
            }
            else if (transportTask?.journey_status == YuxuSectCaseTaskRecord.JourneyExitingDungeon)
                CallDeferred(nameof(resume_yuxu_case_transport_after_load));
            else if (_loaded_game_save.remaining_travel_path.Count > 1)
                tile_world_map?.CallDeferred("resume_saved_travel");
        }
        else
        {
            estate?.Show();
            estate?.MoveToFront();
            estate?.open_location_view("li_residence");
            estate?.set_meditation_available(_static_room_authorized);
            estate?.set_rest_available(_xuanjian_awakened);
            estate?.set_back_hill_trial_available(_back_hill_departure_arranged);
            hud?.Show();
            hud?.MoveToFront();
        }
        mark_save_dirty();
    }

    private void process_save_autosave(double delta)
    {
        if (_npc_world?.HasPendingSettlement == true || action_transition?.IsPlaying == true || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.IsSmoothAdvanceBusy == true) return;
        if (!_save_is_dirty || _save_manager?.has_active_slot != true) return;
        _save_dirty_seconds += delta;
        if (_save_dirty_seconds >= 1.5)
            save_progress_now(false);
    }

    private void mark_save_dirty()
    {
        if (_save_manager?.has_active_slot != true) return;
        if (!_save_is_dirty) _save_dirty_seconds = 0;
        _save_is_dirty = true;
    }

    public void notify_yuanxi_changed(Character character)
    {
        if (character != player) return;
        mark_save_dirty();
        if (!character.is_yunxi_complete()) return;
        RefreshJournalTracker();
        refresh_journal_entries();
    }

    private bool save_progress_now(bool showFeedback)
    {
        if (_npc_world?.HasPendingSettlement == true || action_transition?.IsPlaying == true || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.IsSmoothAdvanceBusy == true) return false;
        if (_save_manager?.has_active_slot != true || player?.entity_data is not HumanData playerData)
            return false;

        var save = capture_battle_world_snapshot();
        if (save is null || !_save_manager.save_snapshot(save)) return false;
        _save_is_dirty = false;
        _save_dirty_seconds = 0;
        if (showFeedback) show_message("存档完成", "当前修行进度已保存。");
        return true;
    }

    /// <summary>In-memory battle handoff, using exactly the same state as a formal progress save.</summary>
    public PersistentGameSave? capture_battle_world_snapshot()
    {
        if (player?.entity_data is not HumanData playerData) return null;
        store_active_dynamic_dungeon_route();

        var save = new PersistentGameSave
        {
            player_data = playerData.Duplicate(true) as HumanData ?? playerData,
            letter_mailbox = _letterMailbox.Duplicate(true) as LetterMailbox ?? _letterMailbox,
            yuxu_sect_treasury = _yuxu_sect_treasury_ledger.Duplicate(true) as YuxuSectTreasuryLedger ?? new YuxuSectTreasuryLedger(),
            current_node_id = _player_world_node_id,
            world_cell = _player_world_grid_cell,
            scene_progress = capture_scene_progress(),
            dynamic_dungeon_state = _dynamic_dungeon_state.Duplicate(true),
        };

        if (GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time)
        {
            save.year = time.year;
            save.month = time.month;
            save.day = time.day;
            save.hour = time.hour;
            save.pending_hours = time.get_pending_hours();
        }

        if (GetNodeOrNull<WeatherMgr>("/root/WeatherManager") is { } weather)
        {
            save.weather_type = weather.weather;
            save.weather_intensity = weather.weather_intensity;
            save.weather_wind_speed = weather.wind_speed;
            save.weather_duration_hours = weather.weather_duration_hours;
        }

        if (tile_world_map is not null)
        {
            var state = tile_world_map.Call("get_persistent_state").AsGodotDictionary();
            if (state.ContainsKey("player_cell")) save.world_cell = state["player_cell"].AsVector2I();
            if (state.ContainsKey("explored_cells")) save.explored_map_cells = state["explored_cells"].AsByteArray();
            if (state.ContainsKey("remaining_travel_path"))
            {
                save.remaining_travel_path.Clear();
                foreach (var cell in state["remaining_travel_path"].AsGodotArray())
                    save.remaining_travel_path.Add(cell.AsVector2I());
            }
        }

        if (_npc_world is not null)
        {
            save.world_npcs = _npc_world.get_persistent_npcs();
            save.east_woods_npc_incidents = _npc_world.get_persistent_east_woods_incidents();
            save.npc_simulation_enabled = _npc_world.simulation_enabled;
            save.npc_last_processed_month = _npc_world.get_last_processed_month();
            save.npc_population_version = _npc_world.population_version;
        }

        return save;
    }

    private Dictionary capture_scene_progress()
    {
        return new Dictionary
        {
            ["qingxi_prologue"] = (int)_qingxi_prologue,
            ["story_free_start"] = _story_free_start,
            ["player_unique_id"] = player?.unique_id ?? "",
            ["social_membership_month"] = _socialMembershipMonth,
            ["social_visit_month"] = _socialVisitMonth,
            ["xuanjian_awakened"] = _xuanjian_awakened,
            ["rested_after_xuanjian"] = _rested_after_xuanjian,
            ["static_room_authorized"] = _static_room_authorized,
            ["first_breakthrough_reported"] = _first_breakthrough_reported,
            ["back_hill_departure_arranged"] = _back_hill_departure_arranged,
            ["rested_for_back_hill"] = _rested_for_back_hill,
            ["first_back_hill_battle_explained"] = _first_back_hill_battle_explained,
            ["qingxi_herbs_gathered"] = _qingxi_herbs_gathered,
            ["back_hill_nest_seen"] = _back_hill_nest_seen,
            ["back_hill_trial_complete"] = _back_hill_trial_complete,
            ["qingxi_arrival_hour"] = _qingxi_arrival_hour,
            ["journal_started_hour"] = _journal_started_hour,
            ["journal_rumor_stage"] = _journal_rumor_stage,
            ["journal_unread"] = _journal_unread,
            ["prologue_goal_recorded"] = _prologue_goal_recorded,
            ["cloud_state_initialized"] = _cloud_state_initialized,
            ["east_woods_dungeon_state"] = _east_woods_dungeon?.get_route_state() ?? _east_woods_dungeon_state.Duplicate(true),
            ["journal_tracking"] = PackJournalTracking(),
            ["active_quest"] = pack_entry(_active_quest),
            ["completed_quests"] = pack_entries(_completed_quests),
            ["earned_rumors"] = pack_entries(_earned_rumors),
        };
    }

    private void restore_scene_progress(Dictionary state)
    {
        _socialMembershipMonth = read_int(state, "social_membership_month", -1);
        _socialVisitMonth = read_int(state, "social_visit_month", -1);
        RestoreJournalTracking(state);
        _east_woods_dungeon_state = state.GetValueOrDefault("east_woods_dungeon_state", new Dictionary()).AsGodotDictionary().Duplicate(true);
        EastWoodsDungeonRules.restore_layout(_east_woods_dungeon_state.GetValueOrDefault("layout", new Dictionary()).AsGodotDictionary());
        _east_woods_dungeon_state["layout"] = EastWoodsDungeonRules.get_layout_state();
        if (state.Count == 0) return;
        var step = read_int(state, "qingxi_prologue", (int)QingxiPrologueStep.Awakened);
        _qingxi_prologue = step is >= 0 and <= (int)QingxiPrologueStep.Departed
            ? (QingxiPrologueStep)step
            : QingxiPrologueStep.Awakened;
        _story_free_start = read_bool(state, "story_free_start");
        _xuanjian_awakened = read_bool(state, "xuanjian_awakened");
        _rested_after_xuanjian = read_bool(state, "rested_after_xuanjian");
        _static_room_authorized = read_bool(state, "static_room_authorized");
        _first_breakthrough_reported = read_bool(state, "first_breakthrough_reported");
        _back_hill_departure_arranged = read_bool(state, "back_hill_departure_arranged");
        _rested_for_back_hill = read_bool(state, "rested_for_back_hill");
        _first_back_hill_battle_explained = read_bool(state, "first_back_hill_battle_explained");
        _qingxi_herbs_gathered = read_bool(state, "qingxi_herbs_gathered");
        _back_hill_nest_seen = read_bool(state, "back_hill_nest_seen");
        _back_hill_trial_complete = read_bool(state, "back_hill_trial_complete");
        _qingxi_arrival_hour = read_int(state, "qingxi_arrival_hour", 0);
        _journal_started_hour = read_int(state, "journal_started_hour", -1);
        _journal_rumor_stage = read_int(state, "journal_rumor_stage", 0);
        _journal_unread = read_int(state, "journal_unread", 0);
        _prologue_goal_recorded = read_bool(state, "prologue_goal_recorded");
        _cloud_state_initialized = read_bool(state, "cloud_state_initialized");
        _beast_hunt_prompt_open = false;
        _beast_hunt_trigger_queued = false;

        _active_quest = unpack_entry(state.GetValueOrDefault("active_quest", new Dictionary()));
        _completed_quests.Clear();
        _completed_quests.AddRange(unpack_entries(state.GetValueOrDefault("completed_quests", new Array<Dictionary>())));
        _earned_rumors.Clear();
        _earned_rumors.AddRange(unpack_entries(state.GetValueOrDefault("earned_rumors", new Array<Dictionary>())));
    }

    private static Dictionary pack_entry(JournalPanel.Entry? entry)
    {
        if (entry is null) return [];
        return new Dictionary
        {
            ["title"] = entry.Title,
            ["body"] = entry.Body,
            ["source"] = entry.Source,
            ["place"] = entry.Place,
            ["state"] = entry.State,
            ["time"] = entry.Time ?? "",
            ["id"] = entry.Id,
            ["accepted_order"] = entry.AcceptedOrder,
        };
    }

    private static Array<Dictionary> pack_entries(IEnumerable<JournalPanel.Entry> entries)
    {
        Array<Dictionary> packed = [];
        foreach (var entry in entries) packed.Add(pack_entry(entry));
        return packed;
    }

    private static JournalPanel.Entry? unpack_entry(Variant value)
    {
        if (value.VariantType != Variant.Type.Dictionary) return null;
        var entry = value.AsGodotDictionary();
        if (!entry.ContainsKey("title")) return null;
        return new JournalPanel.Entry(
            entry["title"].AsString(),
            entry.GetValueOrDefault("body", "").AsString(),
            entry.GetValueOrDefault("source", "").AsString(),
            entry.GetValueOrDefault("place", "").AsString(),
            entry.GetValueOrDefault("state", "").AsString(),
            entry.GetValueOrDefault("time", "").AsString(),
            entry.GetValueOrDefault("id", "").AsString(),
            entry.GetValueOrDefault("accepted_order", 0L).AsInt64());
    }

    private static IEnumerable<JournalPanel.Entry> unpack_entries(Variant value)
    {
        if (value.VariantType != Variant.Type.Array) yield break;
        foreach (var item in value.AsGodotArray())
            if (unpack_entry(item) is { } entry) yield return entry;
    }

    private static int read_int(Dictionary state, string key, int fallback = 0)
        => state.ContainsKey(key) ? state[key].AsInt32() : fallback;

    private static bool read_bool(Dictionary state, string key, bool fallback = false)
        => state.ContainsKey(key) ? state[key].AsBool() : fallback;

    private void on_tile_world_save_state_changed() => mark_save_dirty();
}
