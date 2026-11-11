using Godot;
using Godot.Collections;
using System.Linq;

public partial class MainScene
{
    private void on_free_world_distance(double kilometres)
    {
        if (_nodeWorldMap is null || player is null || has_pending_world_encounter()) return;
        if (player.entity_data is HumanData human && _npc_world?.get_active_yuxu_case_transport(human) is { temporary_party_member_active: true }) return;
        var locationId = _nodeWorldMap.Call("get_current_location_runtime_id").AsString();
        var place = _world_node_data?.get_node(locationId);
        var eligible = place is null || place.node_type is not (WorldNodeData.NodeType.City or WorldNodeData.NodeType.Sect or WorldNodeData.NodeType.Village or WorldNodeData.NodeType.Home);
        if (!initialize_road_encounter_clock()) return;
        var candidate = _roadEncounterClock!.Call("consume_edge", kilometres, eligible).AsGodotDictionary();
        if (candidate.Count > 0 && _nodeWorldMap.Call("spawn_visible_encounter", candidate).AsBool())
            _roadEncounterClock.Call("resolve_encounter");
        _worldEncounterState["clock"] = _roadEncounterClock.Call("persistent_state").AsGodotDictionary();
    }

    private void on_visible_world_encounter(Dictionary encounter)
    {
        if (has_pending_world_encounter() || _nodeWorldMap is null) return;
        _worldEncounterState["pending"] = encounter.Duplicate(true);
        on_node_world_changed();
        CallDeferred(nameof(show_world_encounter_event));
    }

    private Array<WorldNpcData> get_free_world_roster()
    {
        var result = new Array<WorldNpcData>();
        if (_nodeWorldMap is null || _npc_world is null) return result;
        var locationId = _nodeWorldMap.Call("get_current_location_runtime_id").AsString();
        var ids = _nodeWorldMap.Call("get_nearby_person_ids").AsStringArray().ToHashSet();
        if (!string.IsNullOrEmpty(locationId))
            foreach (var npc in _npc_world.get_world_grid_roster(_player_world_grid_cell, locationId))
                if (npc.is_alive && !npc.is_traveling) ids.Add(npc.npc_id);
        foreach (var id in ids.Order(StringComparer.Ordinal))
            if (_npc_world.get_npc(id) is { } npc) result.Add(npc);
        foreach (var companion in _npc_world.get_player_travel_party())
            if (result.All(n => n.npc_id != companion.npc_id)) result.Add(companion);
        return result;
    }
}
