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
            var script = GD.Load<GDScript>("res://gdscripts/world/BaiChuanNodeMapRuntime.gd");
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
            if (_nodeWorldState.Count > 0 && !_nodeWorldMap.Call("restore_node_state", _nodeWorldState).AsBool())
                GD.PushError("百川节点新档状态无效");
            else if (_nodeWorldState.Count == 0)
                _nodeWorldMap.Call("set_initial_runtime_id", _player_world_node_id);
            on_node_world_arrived(_nodeWorldMap.Call("get_current_node").AsString());
            var transport = player?.entity_data is HumanData human ? _npc_world?.get_active_yuxu_case_transport(human) : null;
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
        _player_world_node_id = _nodeWorldMap.Call("get_runtime_id", nodeId).AsString();
        _player_world_grid_cell = _npc_world?.node_world_cell_for_node_id(nodeId) ?? new Vector2I(-1, -1);
        on_node_world_changed();
    }

    private void on_node_world_changed()
    {
        if (_nodeWorldMap is null || !GodotObject.IsInstanceValid(_nodeWorldMap)) return;
        _nodeWorldState = _nodeWorldMap.Call("get_node_state").AsGodotDictionary();
        mark_save_dirty();
    }

    private void on_node_world_travel_finished(string nodeId)
    {
        on_node_world_arrived(nodeId);
        on_tile_world_travel_finished(1);
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
        if (nodeId != _player_world_node_id || !is_world_location_unlocked(nodeId) ||
            _world_node_data?.get_node(nodeId) is not { enterable: true }) return;
        on_node_world_changed();
        _nodeWorldMap?.QueueFree();
        _nodeWorldMap = null;
        on_node_enter_requested(nodeId);
    }
}
