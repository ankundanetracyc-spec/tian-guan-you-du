using Godot;

public partial class NpcWorldManager
{
    /// <summary>Preserve physical within-edge positions when uniform saves adopt terrain travel.</summary>
    public bool migrate_uniform_terrain_travel()
    {
        if (_tile_world_map?.HasMethod("upgrade_uniform_travel_progress") != true ||
            string.IsNullOrEmpty(get_terrain_speed_profile())) return false;
        var changed = false;
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_traveling || !npc.has_world_grid_position ||
                !string.IsNullOrEmpty(npc.dungeon_id) || npc.world_travel_next_cell_index < 1 ||
                npc.world_travel_next_cell_index >= npc.world_travel_path.Count) continue;
            var progress = _tile_world_map.Call("upgrade_uniform_travel_progress", npc.current_grid_cell,
                npc.world_travel_path[npc.world_travel_next_cell_index],
                npc.world_travel_cell_progress_hours, npc.dun_speed).AsDouble();
            if (!double.IsFinite(progress) || progress < 0) continue;
            npc.world_travel_cell_progress_hours = (float)progress;
            npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24.0)));
            changed = true;
        }
        if (changed) EmitSignal(SignalName.world_npcs_changed);
        return true;
    }

    public string get_terrain_speed_profile() =>
        _tile_world_map?.HasMethod("get_terrain_speed_profile") == true
            ? _tile_world_map.Call("get_terrain_speed_profile").AsString() : "";
}
