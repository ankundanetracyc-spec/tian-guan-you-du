using Godot;
using Godot.Collections;

public partial class NpcWorldManager
{
    /// <summary>Legacy 200km saves retain their within-edge position on the expanded world.</summary>
    public void migrate_legacy_free_world_travel()
    {
        if (_tile_world_map?.HasMethod("get_legacy_progress_scale") != true) return;
        foreach (var npc in npcs)
        {
            if (!npc.is_traveling || npc.world_travel_next_cell_index >= npc.world_travel_path.Count ||
                npc.world_travel_cell_progress_hours <= 0) continue;
            var factor = _tile_world_map.Call("get_legacy_progress_scale", npc.current_grid_cell,
                npc.world_travel_path[npc.world_travel_next_cell_index], npc.dun_speed).AsDouble();
            if (double.IsFinite(factor) && factor > 0) npc.world_travel_cell_progress_hours *= (float)factor;
            npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24.0)));
        }
        EmitSignal(SignalName.world_npcs_changed);
    }

    /// <summary>Actual map travelers; fractional projection never mutates NPC history.</summary>
    public Array<Dictionary> get_free_world_map_people(Vector2 center, float radius)
    {
        var result = new Array<Dictionary>();
        if (_tile_world_map?.HasMethod("project_map_position") != true) return result;
        var fractional = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_pending_hours() ?? 0f;
        var radiusSquared = radius * radius;
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_alive || !npc.has_world_grid_position ||
                !string.IsNullOrEmpty(npc.dungeon_id) || !string.IsNullOrEmpty(npc.temporary_party_task_id)) continue;
            if (!npc.is_traveling && (!string.IsNullOrEmpty(npc.current_subscene_id) ||
                _map_data?.get_node(npc.current_node_id) is { node_type: WorldNodeData.NodeType.City or WorldNodeData.NodeType.Sect or WorldNodeData.NodeType.Village or WorldNodeData.NodeType.Home })) continue;
            var position = npc.is_traveling
                ? _tile_world_map.Call("project_map_position", npc.current_grid_cell, npc.world_travel_path,
                    npc.world_travel_next_cell_index, npc.world_travel_cell_progress_hours, fractional, npc.dun_speed).AsVector2()
                : _tile_world_map.Call("get_map_position", npc.current_grid_cell).AsVector2();
            if (position.X < 0 || position.DistanceSquaredTo(center) > radiusSquared) continue;
            result.Add(new Dictionary
            {
                ["id"] = npc.npc_id, ["name"] = npc.npc_name, ["realm"] = npc.cultivation_realm,
                ["position"] = position, ["traveling"] = npc.is_traveling,
            });
        }
        return result;
    }
}
