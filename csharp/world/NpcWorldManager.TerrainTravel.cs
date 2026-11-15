using Godot;

public partial class NpcWorldManager
{
    // Null means a fresh current-world population. Empty string is a real,
    // untagged historical source and must not be replaced after a failed load.
    private string? _npcNavigationSaveProfile;
    private string? _npcTerrainSaveProfile;
    private double? _npcNavigationSaveWorldSize;
    private int? _npcNavigationSaveVersion;

    public string get_npc_navigation_save_profile() => _npcNavigationSaveProfile ?? get_navigation_profile();
    public string get_npc_terrain_save_profile() => _npcTerrainSaveProfile ?? get_terrain_speed_profile();
    public double get_npc_navigation_save_world_size() => _npcNavigationSaveWorldSize ?? current_navigation_world_size();
    public int get_npc_navigation_save_version() => _npcNavigationSaveVersion ?? 2;

    public void reset_npc_navigation_save_profile()
    {
        _npcNavigationSaveProfile = null;
        _npcTerrainSaveProfile = null;
        _npcNavigationSaveWorldSize = null;
        _npcNavigationSaveVersion = null;
    }

    private double current_navigation_world_size() =>
        _tile_world_map?.HasMethod("get_navigation_world_size") == true
            ? _tile_world_map.Call("get_navigation_world_size").AsDouble() : 0.0;

    /// <summary>Compatibility entry point; use one atomic source-aware conversion.</summary>
    public bool migrate_uniform_terrain_travel()
    {
        return migrate_navigation_travel(get_npc_navigation_save_profile(),
            get_npc_navigation_save_world_size(), "", get_npc_navigation_save_version());
    }

    public string get_terrain_speed_profile() =>
        _tile_world_map?.HasMethod("get_terrain_speed_profile") == true
            ? _tile_world_map.Call("get_terrain_speed_profile").AsString() : "";

    public string get_navigation_profile() =>
        _tile_world_map?.HasMethod("get_navigation_profile") == true
            ? _tile_world_map.Call("get_navigation_profile").AsString() : "";

    public bool migrate_navigation_travel(string savedProfile, double sourceWorldSize = 7372.8,
        string savedTerrainProfile = "terrain-speed-v1", int savedVersion = 2)
    {
        _npcNavigationSaveProfile = savedProfile;
        _npcTerrainSaveProfile = savedTerrainProfile;
        _npcNavigationSaveWorldSize = sourceWorldSize;
        _npcNavigationSaveVersion = savedVersion;
        if (savedVersion is not (1 or 2) || _tile_world_map?.HasMethod("upgrade_navigation_travel_progress") != true ||
            !_tile_world_map.HasMethod("can_upgrade_navigation_source") ||
            !_tile_world_map.Call("can_upgrade_navigation_source", savedProfile, sourceWorldSize,
                savedTerrainProfile, savedVersion).AsBool()) return false;
        var migrated = new System.Collections.Generic.List<(WorldNpcData Npc, float Hours, int Days)>();
        foreach (var npc in npcs)
        {
            if (npc is null || !npc.is_traveling || !npc.has_world_grid_position ||
                !string.IsNullOrEmpty(npc.dungeon_id)) continue;
            if (npc.world_travel_next_cell_index < 1 || npc.world_travel_next_cell_index >= npc.world_travel_path.Count) return false;
            var progress = _tile_world_map.Call("upgrade_navigation_travel_progress", npc.current_grid_cell,
                npc.world_travel_path[npc.world_travel_next_cell_index], npc.world_travel_cell_progress_hours,
                npc.dun_speed, savedProfile, sourceWorldSize, savedTerrainProfile, savedVersion).AsDouble();
            if (!double.IsFinite(progress) || progress < 0 || progress > float.MaxValue) return false;
            var remaining = 0.0;
            for (var index = npc.world_travel_next_cell_index; index < npc.world_travel_path.Count; index++)
            {
                var hours = get_npc_cell_travel_hours(index == npc.world_travel_next_cell_index
                    ? npc.current_grid_cell : npc.world_travel_path[index - 1], npc.world_travel_path[index], npc.dun_speed);
                if (!double.IsFinite(hours) || hours <= 0) return false;
                remaining += hours;
            }
            var days = Math.Ceiling(Math.Max(0.0, remaining - progress) / 24.0);
            if (!double.IsFinite(days) || days >= int.MaxValue) return false;
            migrated.Add((npc, (float)progress, Math.Max(1, (int)days)));
        }
        // No NPC is written until every source, route and computed progress is
        // valid. A failure above retains all old values and the saved stamp.
        foreach (var (npc, hours, days) in migrated)
        {
            npc.world_travel_cell_progress_hours = hours;
            npc.travel_days_remaining = days;
        }
        _npcNavigationSaveProfile = get_navigation_profile();
        _npcTerrainSaveProfile = get_terrain_speed_profile();
        _npcNavigationSaveWorldSize = current_navigation_world_size();
        _npcNavigationSaveVersion = 2;
        if (migrated.Count > 0) EmitSignal(SignalName.world_npcs_changed);
        return true;
    }
}
