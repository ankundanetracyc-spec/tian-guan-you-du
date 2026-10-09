using Godot;

/// <summary>Bridges fixed-grid travel back to the established stable location IDs and interiors.</summary>
public partial class MainScene
{
    private Vector2I _player_world_grid_cell = new(38, 166);
    private bool _tile_world_npc_signal_connected;
    private bool _tile_world_map_bridge_configured;

    public void configure_tile_world_map_bridge()
    {
        if (tile_world_map is null)
            return;
        if (_tile_world_map_bridge_configured)
            return;

        if (tile_world_map.HasSignal("player_cell_changed"))
            tile_world_map.Connect("player_cell_changed", Callable.From<Vector2I>(on_tile_world_player_cell_changed));
        if (tile_world_map.HasSignal("world_state_changed"))
            tile_world_map.Connect("world_state_changed", Callable.From(on_tile_world_save_state_changed));
        if (tile_world_map.HasSignal("node_enter_requested"))
            tile_world_map.Connect("node_enter_requested", Callable.From<string>(on_tile_world_node_enter_requested));
        tile_world_map.Connect("visibility_changed", Callable.From(on_tile_world_map_visibility_changed));
        _npc_world?.set_tile_world_map(tile_world_map);
        _tile_world_map_bridge_configured = true;
        connect_tile_world_npc_population();
        sync_tile_world_map_state();
    }

    private void on_tile_world_map_visibility_changed()
    {
        if (tile_world_map?.Visible == true)
            sync_tile_world_map_state();
    }

    private void sync_tile_world_map_state()
    {
        if (tile_world_map is null)
            return;
        connect_tile_world_npc_population();
        if (tile_world_map.Visible)
            tile_world_map.Call("prepare_world_markers");
        tile_world_map.Call("set_dun_speed", player?.dun_speed ?? 100.0f);
        tile_world_map.Call("set_can_fly", player?.can_fly_world_map ?? false);
        tile_world_map.Call("set_player_cell", _player_world_grid_cell);
        refresh_tile_world_population_counts();
    }

    private void connect_tile_world_npc_population()
    {
        if (_tile_world_npc_signal_connected || _npc_world is null)
            return;

        _npc_world.Connect("world_npcs_changed", Callable.From(refresh_tile_world_population_counts));
        _tile_world_npc_signal_connected = true;
    }

    private void refresh_tile_world_population_counts()
    {
        if (tile_world_map is null)
            return;

        if (node_map?.map_data is { } mapData)
        {
            var counts = new Godot.Collections.Dictionary();
            foreach (var node in mapData.get_all_nodes())
                counts[node.node_id] = _npc_world?.get_world_location_population_count(node.node_id) ?? 0;
            tile_world_map.Call("set_population_counts", counts);
        }
        tile_world_map.Call("set_grid_population_counts", _npc_world?.get_ordinary_grid_population_counts() ?? new Godot.Collections.Dictionary());

        refresh_tile_world_character_list();
    }

    private void on_tile_world_player_cell_changed(Vector2I cell)
    {
        _player_world_grid_cell = cell;
        mark_save_dirty();
        if (tile_world_map is null)
            return;

        var locationNodeId = tile_world_map.Call("get_location_node_id_at_cell", cell).AsString();
        if (locationNodeId != _player_world_node_id)
        {
            if (!string.IsNullOrEmpty(_player_world_node_id))
                node_map?.set_player_node_count(_player_world_node_id, 0);
            _player_world_node_id = locationNodeId;

            if (!string.IsNullOrEmpty(locationNodeId) && node_map?.map_data?.get_node(locationNodeId) is not null)
            {
                node_map.set_player_location(locationNodeId);
                node_map.set_player_node_count(locationNodeId, player is null ? 0 : 1);
            }
        }

        // 普通格也保留名单外框与明确空状态；每次逐格抵达都按该格刷新，不再留着上一个地点的人物。
        refresh_tile_world_character_list(cell, locationNodeId);
    }

    private void refresh_tile_world_character_list(Vector2I? knownCell = null, string? knownLocationNodeId = null)
    {
        if (tile_world_map?.Visible != true)
            return;

        var cell = knownCell ?? tile_world_map.Call("get_player_cell").AsVector2I();
        var locationNodeId = knownLocationNodeId ?? tile_world_map.Call("get_location_node_id_at_cell", cell).AsString();
        var contextId = string.IsNullOrEmpty(locationNodeId)
            ? $"world-cell:{cell.X},{cell.Y}"
            : $"world:{locationNodeId}";
        var roster = _npc_world?.get_world_grid_roster(cell, locationNodeId) ?? new Godot.Collections.Array<WorldNpcData>();
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.show_world_npcs(roster, contextId);
    }

    private void on_tile_world_node_enter_requested(string nodeId)
    {
        if (node_map?.map_data?.get_node(nodeId) is not { } node ||
            nodeId != _player_world_node_id || !node_map.is_node_unlocked(nodeId) || !node.enterable)
            return;

        on_node_enter_requested(nodeId);
        tile_world_map?.Hide();
    }
}
