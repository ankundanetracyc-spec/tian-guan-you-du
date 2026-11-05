using Godot;
using Godot.Collections;

public partial class MainScene
{
    private Control? _nodeWorldMap;
    private Dictionary _nodeWorldState = [];

    private void show_node_world_map()
    {
        if (_nodeWorldMap is null || !GodotObject.IsInstanceValid(_nodeWorldMap))
        {
            var script = GD.Load<GDScript>("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd");
            if (script is null) { GD.PushError("百川节点地图脚本加载失败"); return; }
            _nodeWorldMap = new Control { Name = "BaiChuanNodeWorld" };
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
            _nodeWorldMap.Connect("location_context_changed", Callable.From<string>(on_node_world_arrived));
            _nodeWorldMap.Connect("nearby_people_changed", Callable.From(() => refresh_node_world_character_list(force: true)));
            _nodeWorldMap.Connect("world_npc_selected", Callable.From<string>(id =>
            {
                if (_npc_world?.get_npc(id) is { } npc) open_world_npc_actions(npc);
            }));
            var transport = player?.entity_data is HumanData human ? _npc_world?.get_active_yuxu_case_transport(human) : null;
            var restoredState = _nodeWorldState.Duplicate(true);
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
                _nodeWorldMap.Call("set_initial_runtime_id", _player_world_node_id);
            on_node_world_arrived(_nodeWorldMap.Call("get_current_node").AsString());
            if (transport?.journey_status != YuxuSectCaseTaskRecord.JourneyIntercepted && !has_pending_world_encounter())
                _nodeWorldMap.CallDeferred("resume_saved_travel");
        }
        _nodeWorldMap.Call("set_dun_speed", player?.dun_speed ?? 100.0f);
        _nodeWorldMap.Call("set_encounter_blocked", has_pending_world_encounter());
        connect_road_encounter_briefing();
        _nodeWorldMap.Show();
        _nodeWorldMap.MoveToFront();
        initialize_node_world_roster();
        if (has_pending_world_encounter() && !_returning_from_battle)
            CallDeferred(nameof(show_world_encounter_event));
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
        _player_world_node_id = nodeId;
        on_node_world_changed();
        _nodeWorldMap?.QueueFree();
        _nodeWorldMap = null;
        on_node_enter_requested(nodeId);
    }
}
