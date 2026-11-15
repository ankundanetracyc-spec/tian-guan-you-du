using Godot;
using Godot.Collections;

public partial class MainScene
{
    private Control? _nodeWorldMap;
    private Dictionary _nodeWorldState = [];
    private string _playerTravelPartySignature = "\0";

    private void show_node_world_map()
    {
        synchronize_dynamic_dungeon_opportunities();
        var beginBaishiJourney = false;
        if (_nodeWorldMap is null || !GodotObject.IsInstanceValid(_nodeWorldMap))
        {
            var script = GD.Load<GDScript>("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd");
            if (script is null) { GD.PushError("百川节点地图脚本加载失败"); return; }
            _nodeWorldMap = new Control { Name = "BaiChuanNodeWorld" };
            _playerTravelPartySignature = "\0";
            _nodeWorldMap.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _nodeWorldMap.SetScript(script);
            GetNode<CanvasLayer>("MapLayer").AddChild(_nodeWorldMap);
            _nodeWorldMap.Connect("node_enter_requested", Callable.From<string>(on_node_world_enter));
            _nodeWorldMap.Connect("node_travel_finished", Callable.From<string>(on_node_world_travel_finished));
            _nodeWorldMap.Connect("node_travel_started", Callable.From(on_tile_world_travel_started));
            _nodeWorldMap.Connect("node_step_committed", Callable.From<string>(on_node_world_step_committed));
            _nodeWorldMap.Connect("world_state_changed", Callable.From(on_node_world_changed));
            _nodeWorldMap.Connect("return_requested", Callable.From(close_world_map));
            _nodeWorldMap.Connect("distance_traveled", Callable.From<double>(on_free_world_distance));
            _nodeWorldMap.Connect("visible_encounter_requested", Callable.From<Dictionary>(on_visible_world_encounter));
            _nodeWorldMap.Connect("player_boat_requested", Callable.From<string>(on_player_boat_requested));
            _nodeWorldMap.Call("configure_player_boat_callbacks",
                Callable.From<string, double, bool>(prepare_player_boat_hours),
                Callable.From<string, double, bool>(consume_player_boat_hours),
                Callable.From<string, bool>(leave_player_boat),
                Callable.From<string, double, bool>(validate_player_boat));
            _nodeWorldMap.Connect("location_context_changed", Callable.From<string>(on_node_world_arrived));
            _nodeWorldMap.Connect("nearby_people_changed", Callable.From(() => refresh_node_world_character_list(force: true)));
            _nodeWorldMap.Connect("world_npc_selected", Callable.From<string>(id =>
            {
                if (_npc_world?.get_npc(id) is { } npc)
                {
                    if (npc.is_player_travel_companion && _nodeWorldMap?.Call("is_traveling").AsBool() == true)
                        _nodeWorldMap.Call("stop_free_travel");
                    open_world_npc_actions(npc);
                }
            }));
            sync_dynamic_dungeon_map_markers();
            var transport = player?.entity_data is HumanData human ? _npc_world?.get_active_yuxu_case_transport(human) : null;
            var restoredState = _nodeWorldState.Duplicate(true);
            upgrade_saved_baishi_escort_speed(restoredState);
            upgrade_saved_matching_player_boat_speed(restoredState);
            if (restoredState.GetValueOrDefault("version", 1).AsInt32() < 2 && transport?.temporary_party_member_active == true)
                restoredState["legacy_assigned_route"] = true;
            if (_nodeWorldState.Count > 0 && !_nodeWorldMap.Call("restore_node_state", restoredState).AsBool())
            {
                GD.PushError("百川行旅位置与当前地图数据不一致，保留原存档状态");
                _nodeWorldMap.QueueFree();
                _nodeWorldMap = null;
                show_message("地图位置暂不可恢复", "已保存的位置与当前地图不一致，本次没有替换原来的行旅位置。请保留当前存档以便核对地图版本。");
                return;
            }
            else if (_nodeWorldState.Count == 0)
            {
                _nodeWorldMap.Call("set_initial_runtime_id", _player_world_node_id);
                if (_baishiLecture is { traveling_to_baishi: true })
                {
                    if (!_nodeWorldMap.Call("set_initial_position_near_location", BaishiNodeId).AsBool())
                    {
                        _nodeWorldMap.QueueFree();
                        _nodeWorldMap = null;
                        show_message("暂无法出发", "白石附近的出发位置暂不可用，同行状态仍然保留。");
                        return;
                    }
                    beginBaishiJourney = true;
                }
            }
            on_node_world_arrived(_nodeWorldMap.Call("get_current_node").AsString());
            if (transport?.journey_status != YuxuSectCaseTaskRecord.JourneyIntercepted && !has_pending_world_encounter())
                _nodeWorldMap.CallDeferred("resume_saved_travel");
        }
        sync_dynamic_dungeon_map_markers();
        _nodeWorldMap.Call("set_dun_speed", player?.dun_speed ?? 100.0f);
        _nodeWorldMap.Call("set_encounter_blocked", has_pending_world_encounter());
        connect_road_encounter_briefing();
        _nodeWorldMap.Show();
        _nodeWorldMap.MoveToFront();
        sync_player_boats();
        sync_player_travel_party_map();
        initialize_node_world_roster();
        if (beginBaishiJourney && !_nodeWorldMap.Call("travel_to_runtime_location", BaishiNodeId).AsBool())
            show_message("前往白石", "暂时无法规划路线，请在地图上选择白石城后再出发。");
        if (has_pending_world_encounter() && !_returning_from_battle)
            CallDeferred(nameof(show_world_encounter_event));
    }

    // Only the old, identified Baishi escort is revised; generic saved flights
    // and player-owned boats keep their own recorded speed.
    private void upgrade_saved_baishi_escort_speed(Dictionary state)
    {
        if (_baishiLecture is not { spirit_boat_in_transit: true } escort ||
            string.IsNullOrEmpty(escort.spirit_boat_driver_id) ||
            state.GetValueOrDefault("movement_mode", "").AsString() != "flight" ||
            state.GetValueOrDefault("flight_driver_id", "").AsString() != escort.spirit_boat_driver_id) return;
        var oldSpeed = state.GetValueOrDefault("flight_speed", 0.0).AsDouble();
        if (!double.IsFinite(oldSpeed) || (Math.Abs(oldSpeed - 2400.0) > 0.001 && Math.Abs(oldSpeed - 12000.0) > 0.001)) return;
        if (GD.Load<ItemTravelData>(BaishiEscortBoatPath) is { } boat && boat.try_get_dun_speed(out var speed))
            state["flight_speed"] = speed;
    }

    // Some older player fixtures reference this same external boat profile.
    // Keep their in-flight save readable when that exact profile is revised.
    private void upgrade_saved_matching_player_boat_speed(Dictionary state)
    {
        if (state.GetValueOrDefault("movement_mode", "").AsString() != "player_flight") return;
        var oldSpeed = state.GetValueOrDefault("flight_speed", 0.0).AsDouble();
        if (!double.IsFinite(oldSpeed) || (Math.Abs(oldSpeed - 2400.0) > 0.001 && Math.Abs(oldSpeed - 12000.0) > 0.001)) return;
        var id = state.GetValueOrDefault("player_boat_id", "").AsString();
        if (player?.get_owned_spirit_boat(id) is { travel_in_use: true, travel_data: { } travel } &&
            travel.ResourcePath == BaishiEscortBoatPath && travel.try_get_dun_speed(out var speed))
            state["flight_speed"] = speed;
    }

    private void sync_player_boats()
    {
        if (_nodeWorldMap?.HasMethod("set_owned_player_boats") != true || player is null) return;
        var boats = new Godot.Collections.Array();
        foreach (var boat in player.get_artifact_items())
            if (Character.is_usable_spirit_boat(boat))
                boats.Add(new Dictionary { ["id"] = boat.instance_id, ["name"] = boat.artifact_name, ["charge"] = boat.travel_charge_hours });
        _nodeWorldMap.Call("set_owned_player_boats", boats);
    }

    private bool validate_player_boat(string id, double speed) =>
        player?.get_owned_spirit_boat(id) is { travel_in_use: true, travel_data: { } data } &&
        data.try_get_dun_speed(out var actual) && Math.Abs(actual - speed) <= Math.Max(1e-6, actual * 1e-6);

    private bool prepare_player_boat_hours(string id, double hours) => player?.prepare_spirit_boat_hours(id, hours) == true;

    private bool consume_player_boat_hours(string id, double hours)
    {
        if (player?.consume_spirit_boat_hours(id, hours) != true) return false;
        mark_save_dirty();
        return true;
    }

    private bool leave_player_boat(string id) => player?.leave_spirit_boat(id) == true;

    private bool can_fit_player_travel_party(int occupants)
    {
        if (_nodeWorldMap is null || !GodotObject.IsInstanceValid(_nodeWorldMap)) return true;
        var mode = _nodeWorldMap.Get("movement_mode").AsString();
        ItemTravelData? data = mode switch
        {
            "player_flight" => player?.get_owned_spirit_boat(_nodeWorldMap.Get("player_boat_id").AsString())?.travel_data,
            "flight" when _baishiLecture is { spirit_boat_in_transit: true } => GD.Load<ItemTravelData>(BaishiEscortBoatPath),
            _ => null
        };
        if (mode != "player_flight" && (mode != "flight" || _baishiLecture is not { spirit_boat_in_transit: true })) return true;
        return data?.try_get_passenger_capacity(out var capacity) == true && occupants <= capacity;
    }

    private void on_player_boat_requested(string id)
    {
        if (_nodeWorldMap is null || player?.get_owned_spirit_boat(id) is not { travel_data: { } data } ||
            !data.try_get_dun_speed(out var speed)) return;
        if (!data.try_get_passenger_capacity(out var capacity))
        {
            show_message("暂无法驾舟", "这艘灵舟的可承载人数尚未确定。"); return;
        }
        var occupants = 1 + (_npc_world?.get_player_travel_party().Count ?? 0);
        if (occupants > capacity)
        {
            show_message("随行人数过多", $"这艘灵舟最多可载{capacity}人，人数包括驾舟者。"); return;
        }
        if (!player.board_spirit_boat(id, occupants)) return;
        if (_nodeWorldMap.Call("board_player_spirit_boat", id, speed).AsBool()) return;
        player.leave_spirit_boat(id);
        show_message("暂无法驾舟", "当前状态不能启用灵舟，请稍后再试。");
    }

    private void sync_player_travel_party_map()
    {
        if (_nodeWorldMap?.HasMethod("set_player_travel_party") != true || _npc_world is null) return;
        var members = new Godot.Collections.Array();
        var identity = new System.Text.StringBuilder();
        foreach (var npc in _npc_world.get_player_travel_party())
        {
            members.Add(new Dictionary { ["id"] = npc.npc_id, ["name"] = npc.npc_name });
            identity.Append(npc.npc_id).Append('\0').Append(npc.npc_name).Append('\0');
        }
        var signature = identity.ToString();
        if (_playerTravelPartySignature == signature) return;
        _playerTravelPartySignature = signature;
        _nodeWorldMap.Call("set_player_travel_party", members);
    }

    private void on_node_world_arrived(string nodeId)
    {
        if (_nodeWorldMap is null) return;
        _player_world_node_id = _nodeWorldMap.HasMethod("get_current_location_runtime_id")
            ? _nodeWorldMap.Call("get_current_location_runtime_id").AsString()
            : _nodeWorldMap.Call("get_runtime_id", nodeId).AsString();
        _player_world_grid_cell = _npc_world?.node_world_cell_for_node_id(nodeId) ?? new Vector2I(-1, -1);
        on_node_world_changed();
    }

    private void on_node_world_changed()
    {
        if (_nodeWorldMap is null || !GodotObject.IsInstanceValid(_nodeWorldMap)) return;
        _nodeWorldState = _nodeWorldMap.Call("get_node_state").AsGodotDictionary();
        if (_nodeWorldMap.HasMethod("get_world_position"))
            _player_world_grid_cell = _npc_world?.node_world_cell_for_node_id(_nodeWorldState.GetValueOrDefault("current_node", "").AsString()) ?? new Vector2I(-1, -1);
        mark_save_dirty();
    }

    private void on_node_world_travel_finished(string nodeId)
    {
        on_node_world_arrived(nodeId);
        complete_baishi_boat_flight();
        on_tile_world_travel_finished(_nodeWorldMap?.HasMethod("get_last_completed_steps") == true
            ? _nodeWorldMap.Call("get_last_completed_steps").AsInt32() : 1);
    }

    private void on_node_world_step_committed(string nodeId)
    {
        if (_nodeWorldMap is null) return;
        _player_world_node_id = _nodeWorldMap.Call("get_runtime_id", nodeId).AsString();
        _player_world_grid_cell = _npc_world?.node_world_cell_for_node_id(nodeId) ?? new Vector2I(-1, -1);
        on_node_world_transport_step(nodeId);
        if (_nodeWorldMap.Call("is_traveling").AsBool()) check_world_road_encounter(nodeId);
    }

    private void on_node_world_enter(string nodeId)
    {
        var locationId = _nodeWorldMap?.HasMethod("get_current_location_runtime_id") == true
            ? _nodeWorldMap.Call("get_current_location_runtime_id").AsString() : _player_world_node_id;
        if (nodeId != locationId || !is_world_location_unlocked(nodeId) ||
            _world_node_data?.get_node(nodeId) is not { enterable: true }) return;
        if (nodeId == BaishiNodeId && !arrive_baishi_with_companion()) return;
        _player_world_node_id = nodeId;
        on_node_world_changed();
        _nodeWorldMap?.QueueFree();
        _nodeWorldMap = null;
        on_node_enter_requested(nodeId);
    }
}
