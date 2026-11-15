using Godot;
using System.Linq;

/// <summary>Connects the current node to the shared world-character roster.</summary>
public partial class MainScene
{
    private bool is_world_location_unlocked(string nodeId) =>
        _world_node_data?.get_node(nodeId) is { } node && (!node.requires_unlock || node.unlocked);

    private void hide_world_character_list() =>
        (GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList)?.hide_world_npcs();

    // This field stores an opaque node identifier for existing task and roster interfaces.
    private Vector2I _player_world_grid_cell = new(-1, -1);
    private bool _nodeWorldNpcSignalConnected;
    private bool _tileWorldTravelInProgress;
    private bool _nodeRosterContextInitialized;
    private string _lastNodeRosterContext = "";
    private bool _nodeWorldPopulationRefreshQueued;
    private Godot.Timer? _nodeWorldPopulationRefreshTimer;

    private void initialize_node_world_roster()
    {
        if (!_nodeWorldNpcSignalConnected && _npc_world is not null)
        {
            _npc_world.Connect("world_npcs_changed", Callable.From(on_node_world_population_changed));
            _nodeWorldNpcSignalConnected = true;
        }
        refresh_node_world_population_counts();
        refresh_node_world_character_list(force: true);
    }

    private void on_node_world_population_changed()
    {
        // Model changes are immediate; population labels share the map's 4 Hz
        // presentation cadence instead of rebuilding for every elapsed hour.
        if (_nodeWorldMap?.Visible != true) return;
        sync_player_travel_party_map();
        if (_nodeWorldPopulationRefreshQueued) return;
        _nodeWorldPopulationRefreshQueued = true;
        if (_nodeWorldPopulationRefreshTimer is null)
        {
            _nodeWorldPopulationRefreshTimer = new Godot.Timer
            {
                Name = "NodeWorldPopulationRefresh", WaitTime = .25, OneShot = true,
            };
            AddChild(_nodeWorldPopulationRefreshTimer);
            _nodeWorldPopulationRefreshTimer.Timeout += flush_node_world_population_refresh;
        }
        _nodeWorldPopulationRefreshTimer.Start();
    }

    private void flush_node_world_population_refresh()
    {
        _nodeWorldPopulationRefreshQueued = false;
        refresh_node_world_population_counts();
        refresh_node_world_character_list(force: true);
    }

    private void refresh_node_world_population_counts()
    {
        if (_nodeWorldMap?.Visible != true || _npc_world is null) return;
        _nodeWorldMap.Call("set_population_counts", _npc_world.get_node_world_population_counts());
    }

    private void refresh_node_world_character_list(bool force = false)
    {
        if (_nodeWorldMap?.Visible != true || _npc_world is null) return;
        if (GetTree().GetFirstNodeInGroup("cell_character_list") is not CellCharacterList rosterPanel) return;
        if (_nodeWorldMap.HasMethod("get_nearby_person_ids"))
        {
            if (_tileWorldTravelInProgress) return;
            var people = get_free_world_roster();
            var context = "free-world:" + _nodeWorldMap.Call("get_current_location_runtime_id").AsString() + ":" + string.Join("|", people.Select(n => n.npc_id));
            if (!force && _nodeRosterContextInitialized && _lastNodeRosterContext == context) return;
            _nodeRosterContextInitialized = true;
            _lastNodeRosterContext = context;
            rosterPanel.show_world_npcs(people, context);
            return;
        }
        var id = _nodeWorldMap.Call("get_current_node").AsString();
        if (string.IsNullOrEmpty(id)) return;
        var cell = _npc_world.node_world_cell_for_node_id(id);
        var locationId = _nodeWorldMap.Call("get_runtime_id", id).AsString();
        var contextId = string.IsNullOrEmpty(locationId) ? $"world-cell:{id}" : $"world:{locationId}";
        if (!force && _nodeRosterContextInitialized && _lastNodeRosterContext == contextId) return;
        _nodeRosterContextInitialized = true;
        _lastNodeRosterContext = contextId;
        rosterPanel.show_world_npcs(_npc_world.get_world_grid_roster(cell, locationId), contextId);
    }
}
