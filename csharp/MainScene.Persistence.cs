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
    private const int AutosaveIntervalHours = 3 * 30 * 24;
    private int _lastProgressSaveHour;
    private int _autosaveHour;
    private ulong _saveRevision;
    private ulong _autosaveRevision;
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
        _lastProgressSaveHour = _loaded_game_save is { } saved
            ? (saved.year - 1) * 8640 + (saved.month - 1) * 720 + (saved.day - 1) * 24 + saved.hour
            : GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
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

        _nodeWorldState = _loaded_game_save.node_world_state?.Duplicate(true) as Dictionary ?? [];
        _player_world_node_id = string.IsNullOrWhiteSpace(_loaded_game_save.current_node_id)
            ? "home_village"
            : _loaded_game_save.current_node_id;
        restore_scene_progress(_loaded_game_save.scene_progress);
        _save_is_dirty = true;
    }

    private void restore_npc_save_state()
    {
        if (_loaded_game_save is null)
        {
            _npc_world?.reset_npc_navigation_save_profile();
            return;
        }
        _npc_world?.restore_persistent_state(
            _loaded_game_save.world_npcs,
            _loaded_game_save.npc_simulation_enabled,
            _loaded_game_save.npc_last_processed_month,
            _loaded_game_save.east_woods_npc_incidents,
            _loaded_game_save.npc_population_version);
        if (_baishiLecture is { } lecture && _npc_world?.get_npc(lecture.companion.npc_id) is { } savedCompanion)
            lecture.companion = savedCompanion;
        if (_npc_world is not { } npcWorld) return;
        var savedState = _loaded_game_save.node_world_state ?? [];
        var savedNavigation = savedState.GetValueOrDefault("npc_navigation_profile",
            savedState.GetValueOrDefault("navigation_profile", "")).AsString();
        var savedTerrain = savedState.GetValueOrDefault("npc_terrain_speed_profile",
            savedState.GetValueOrDefault("terrain_speed_profile", "")).AsString();
        var savedWorldSize = savedState.GetValueOrDefault("npc_world_size",
            savedState.GetValueOrDefault("world_size", 0.0)).AsDouble();
        var savedVersion = savedState.GetValueOrDefault("npc_source_version",
            savedState.GetValueOrDefault("version", 1)).AsInt32();
        if (!npcWorld.migrate_navigation_travel(savedNavigation, savedWorldSize, savedTerrain, savedVersion))
            GD.PushWarning($"NPC地图迁移未完成，保留原来源与全部进度：{savedNavigation} / {savedWorldSize} / {savedTerrain}");
        _nodeWorldState["npc_navigation_profile"] = npcWorld.get_npc_navigation_save_profile();
        _nodeWorldState["npc_world_size"] = npcWorld.get_npc_navigation_save_world_size();
        _nodeWorldState["npc_terrain_speed_profile"] = npcWorld.get_npc_terrain_save_profile();
        _nodeWorldState["npc_source_version"] = npcWorld.get_npc_navigation_save_version();
    }

    private void restore_node_world_position()
    {
        var id = _nodeWorldState.GetValueOrDefault("current_node", "").AsString();
        if (!string.IsNullOrEmpty(id))
            _player_world_grid_cell = _npc_world?.node_world_cell_for_node_id(id) ?? new Vector2I(-1, -1);
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
        if (_baishiLecture is { departed_qingxi: false }) { show_baishi_opening(); mark_save_dirty(); return; }
        var departed = has_open_world_access();
        hud?.set_world_map_available(departed);

        if (departed)
        {
            if (_npc_world is not null && _world_node_data is { } populationMap && CloudStateNpcCatalog.ensure_population(_npc_world, populationMap))
                mark_save_dirty();
            // A saved random roster is authoritative; never regenerate it on load.
            _cloud_state_initialized = true;
            if (DynamicDungeonExpedition.FindInsideInstance(_dynamic_dungeon_state) is { } activeDynamic)
            {
                _nodeWorldMap?.Hide();

                open_dynamic_dungeon(activeDynamic.GetValueOrDefault("instance_id", "").AsString());
                hud?.Show();
                mark_save_dirty();
                return;
            }
            if (_east_woods_dungeon_state.GetValueOrDefault("inside", false).AsBool())
            {
                _nodeWorldMap?.Hide();

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
                var remaining = _nodeWorldState.GetValueOrDefault("remaining_route", new Godot.Collections.Array()).AsGodotArray();
                if (remaining.Count <= 1)
                    CallDeferred(nameof(resume_yuxu_case_transport_after_load));
            }
            else if (transportTask?.journey_status == YuxuSectCaseTaskRecord.JourneyExitingDungeon)
                CallDeferred(nameof(resume_yuxu_case_transport_after_load));
            CallDeferred(nameof(resume_saved_banquet_if_any));
        }
        else
        {
            // Retired prologue saves keep their character, inventory, clock and valid world location.
            // Reuse normal world restoration; do not replay the opening or grant progress/rewards.
            _story_free_start = true;
            open_world();
        }
        mark_save_dirty();
    }

    private void process_save_autosave(double delta)
    {
        if (_save_manager?.poll_background_save() is { } saved)
        {
            if (saved) _lastProgressSaveHour = _autosaveHour;
            if (saved && _saveRevision == _autosaveRevision) _save_is_dirty = false;
            _save_dirty_seconds = 0;
        }
        if (_save_manager?.IsBackgroundSaveBusy == true) return;
        if (_npc_world?.HasPendingSettlement == true || action_transition?.IsPlaying == true || GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.IsSmoothAdvanceBusy == true) return;
        if (!_save_is_dirty || _save_manager?.has_active_slot != true) return;
        var now = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
        if (now - _lastProgressSaveHour < AutosaveIntervalHours) return;
        _save_dirty_seconds += delta;
        if (_save_dirty_seconds >= 1.5)
        {
            var save = capture_progress_snapshot(false);
            if (save is not null && _save_manager.begin_background_save(save))
            {
                _autosaveRevision = _saveRevision;
                _autosaveHour = now;
            }
            else _save_dirty_seconds = 0;
        }
    }

    private void on_player_inventory_save_changed(Character _) { mark_save_dirty(); sync_player_boats(); }

    private void mark_save_dirty()
    {
        if (_save_manager?.has_active_slot != true) return;
        _saveRevision++;
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

        var save = capture_progress_snapshot(false);
        if (save is null || !_save_manager.save_snapshot(save)) return false;
        _lastProgressSaveHour = (save.year - 1) * 8640 + (save.month - 1) * 720 + (save.day - 1) * 24 + save.hour;
        _save_is_dirty = false;
        _save_dirty_seconds = 0;
        if (showFeedback) show_message("存档完成", "当前修行进度已保存。");
        return true;
    }

    /// <summary>In-memory battle handoff, using exactly the same state as a formal progress save.</summary>
    public PersistentGameSave? capture_battle_world_snapshot() => capture_progress_snapshot(true);

    // The immediate save/capture consumer reads this view before returning to gameplay.
    // Battle handoffs retain their own deep copy because they outlive the current scene.
    private PersistentGameSave? capture_progress_snapshot(bool duplicateResources)
    {
        if (player?.entity_data is not HumanData playerData) return null;
        store_active_dynamic_dungeon_route();

        var save = new PersistentGameSave
        {
            player_data = duplicateResources ? playerData.Duplicate(true) as HumanData ?? playerData : playerData,
            letter_mailbox = duplicateResources ? _letterMailbox.Duplicate(true) as LetterMailbox ?? _letterMailbox : _letterMailbox,
            yuxu_sect_treasury = duplicateResources ? _yuxu_sect_treasury_ledger.Duplicate(true) as YuxuSectTreasuryLedger ?? new YuxuSectTreasuryLedger() : _yuxu_sect_treasury_ledger,
            current_node_id = _player_world_node_id,
            scene_progress = capture_scene_progress(),
            dynamic_dungeon_state = duplicateResources ? _dynamic_dungeon_state.Duplicate(true) : _dynamic_dungeon_state,
            banquet_events = duplicateResources ? duplicate_banquet_events(_banquetEvents) : _banquetEvents,
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

        if (_nodeWorldMap is not null && GodotObject.IsInstanceValid(_nodeWorldMap))
            _nodeWorldState = _nodeWorldMap.Call("get_node_state").AsGodotDictionary();
        save.node_world_state = _nodeWorldState.Duplicate(true) as Dictionary ?? [];

        if (_npc_world is not null)
        {
            save.world_npcs = duplicateResources ? _npc_world.get_persistent_npcs() : _npc_world.npcs;
            save.east_woods_npc_incidents = duplicateResources ? _npc_world.get_persistent_east_woods_incidents() : _npc_world.PersistentIncidentView;
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
            ["story_free_start"] = _story_free_start,
            ["baishi_lecture"] = _baishiLecture is null ? default(Variant) : _baishiLecture.Duplicate(true),
            ["npc_probe_pending"] = _pendingNpcProbe.Duplicate(true),
            ["world_road_encounter"] = _worldEncounterState.Duplicate(true),
            ["player_unique_id"] = player?.unique_id ?? "",
            ["social_membership_month"] = _socialMembershipMonth,
            ["social_visit_month"] = _socialVisitMonth,
            ["social_visit_processed_month"] = _socialProcessedVisitMonth,
            ["social_visited_npc_ids"] = new Array<string>(_socialVisitedNpcIds.OrderBy(id => id, System.StringComparer.Ordinal)),
            ["last_npc_banquet_quarter"] = _lastNpcBanquetQuarter,
            ["journal_unread"] = _journal_unread,
            ["cloud_state_initialized"] = _cloud_state_initialized,
            ["east_woods_dungeon_state"] = _east_woods_dungeon is { is_dynamic_instance: false } ? _east_woods_dungeon.get_route_state() : _east_woods_dungeon_state.Duplicate(true),
            ["journal_tracking"] = PackJournalTracking(),
            ["completed_quests"] = pack_entries(_completed_quests),
            ["earned_rumors"] = pack_entries(_earned_rumors),
        };
    }

    private void restore_scene_progress(Dictionary state)
    {
        _pendingNpcProbe = state.GetValueOrDefault("npc_probe_pending", new Dictionary()).AsGodotDictionary().Duplicate(true);
        _probePresentedEvent = "";
        _probeLocationRestored = false;
        _worldEncounterState = state.GetValueOrDefault("world_road_encounter", new Dictionary()).AsGodotDictionary().Duplicate(true);
        _socialMembershipMonth = read_int(state, "social_membership_month", -1);
        _socialVisitMonth = read_int(state, "social_visit_month", -1);
        _socialProcessedVisitMonth = read_int(state, "social_visit_processed_month", -1);
        _socialVisitedNpcIds.Clear();
        foreach (var npcId in state.GetValueOrDefault("social_visited_npc_ids", new Array<string>()).AsGodotArray<string>())
            if (!string.IsNullOrWhiteSpace(npcId)) _socialVisitedNpcIds.Add(npcId);
        _lastNpcBanquetQuarter = read_int(state, "last_npc_banquet_quarter", -1);
        RestoreJournalTracking(state);
        _east_woods_dungeon_state = state.GetValueOrDefault("east_woods_dungeon_state", new Dictionary()).AsGodotDictionary().Duplicate(true);
        EastWoodsDungeonRules.restore_layout(_east_woods_dungeon_state.GetValueOrDefault("layout", new Dictionary()).AsGodotDictionary());
        _east_woods_dungeon_state["layout"] = EastWoodsDungeonRules.get_layout_state();
        _story_free_start = read_bool(state, "story_free_start");
        _baishiLecture = state.GetValueOrDefault("baishi_lecture", default(Variant)).AsGodotObject() as BaishiLectureProgress;
        // Saves from the removed prologue use the existing free-world path without altering player data.
        if (_baishiLecture is null) _story_free_start = true;
        if (_baishiLecture is { } priorDeparture && (priorDeparture.chose_yuxu || priorDeparture.spirit_boat_in_transit))
        {
            if (string.IsNullOrEmpty(priorDeparture.selected_sect_id)) priorDeparture.selected_sect_id = YuxuSectId;
            priorDeparture.roots_session_finished = true;
        }
        _journal_unread = read_int(state, "journal_unread", 0);
        _cloud_state_initialized = read_bool(state, "cloud_state_initialized");

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
