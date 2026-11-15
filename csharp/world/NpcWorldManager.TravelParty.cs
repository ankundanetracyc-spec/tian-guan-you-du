using Godot;
using Godot.Collections;
using System;

public partial class NpcWorldManager
{
    private Func<int, bool>? _travelPartyCapacityGuard;

    public void set_travel_party_capacity_guard(Func<int, bool> guard) => _travelPartyCapacityGuard = guard;
    public void clear_travel_party_capacity_guard(Func<int, bool> guard)
    {
        if (_travelPartyCapacityGuard == guard) _travelPartyCapacityGuard = null;
    }

    /// <summary>
    /// Scripted callers establish consent and co-location before joining. The NPC
    /// remains a real saved world character, but no independent route is advanced.
    /// </summary>
    public bool join_player_travel_party(WorldNpcData npc)
    {
        if (npc is null || !npc.is_alive || string.IsNullOrWhiteSpace(npc.npc_id) ||
            npc.is_traveling || !string.IsNullOrEmpty(npc.dungeon_id) ||
            !string.IsNullOrEmpty(npc.temporary_party_task_id)) return false;
        var existing = get_npc(npc.npc_id);
        if (existing is not null && existing != npc) return false;
        if (npc.is_player_travel_companion) return existing == npc;
        if (_travelPartyCapacityGuard?.Invoke(get_player_travel_party().Count + 2) == false) return false;

        npc.is_player_travel_companion = true;
        npc.current_node_id = "";
        npc.current_subscene_id = "";
        npc.current_state = "同行";
        npc.has_world_grid_position = false;
        npc.current_grid_cell = new Vector2I(-1, -1);
        npc.world_travel_path.Clear();
        if (existing is null) npcs.Add(npc);
        invalidate_world_grid_roster_cache();
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public Array<WorldNpcData> get_player_travel_party()
    {
        var result = new Array<WorldNpcData>();
        foreach (var npc in npcs)
            if (npc is { is_alive: true, is_player_travel_companion: true }) result.Add(npc);
        return result;
    }

    /// <summary>The caller supplies the actual place where the companion leaves.</summary>
    public bool leave_player_travel_party(string npcId, string nodeId, string subsceneId, string nextState)
    {
        var npc = get_npc(npcId);
        var node = _map_data?.get_node(nodeId);
        if (npc is not { is_player_travel_companion: true } || node is null ||
            (!string.IsNullOrEmpty(subsceneId) && !node.subscenes.Any(s => s is not null && s.subscene_id == subsceneId))) return false;
        npc.is_player_travel_companion = false;
        npc.current_node_id = nodeId;
        npc.current_subscene_id = subsceneId;
        npc.current_state = nextState;
        initialize_world_grid_position(npc);
        invalidate_world_grid_roster_cache();
        EmitSignal(SignalName.world_npcs_changed);
        return true;
    }
}
