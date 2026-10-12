using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NpcWorldManager
{
    /// <summary>Initialize only newly created people. Existing saved routes are never resampled.</summary>
    public void seed_initial_world_travel(IEnumerable<WorldNpcData> added)
    {
        if (_map_data is null || _tile_world_map is null) return;
        var destinations = _map_data.get_all_nodes().Where(n => n is not null && n.node_id != "home_village" &&
            n.enterable && (!n.requires_unlock || n.unlocked) && has_world_grid_anchor(n.node_id)).ToArray();
        var month = Math.Max(0, _last_processed_month);
        foreach (var group in added.Where(can_seed_initial_travel).GroupBy(n => n.current_node_id))
        {
            var candidates = group.OrderBy(n => stable_seed(month, n.npc_id, "initial_travel")).ToArray();
            var originCell = get_location_anchor_cell(group.Key);
            var nearby = destinations.Where(n => n.node_id != group.Key)
                .OrderBy(n => originCell.DistanceSquaredTo(get_location_anchor_cell(n.node_id)))
                .ThenBy(n => n.node_id, StringComparer.Ordinal).ToArray();
            // One path search per origin/destination, shared by all travelers from that origin.
            var routes = new List<(WorldNodeData Destination, Godot.Collections.Array<Vector2I> Path)>();
            foreach (var destination in nearby)
            {
                var path = get_npc_grid_path(originCell, get_location_anchor_cell(destination.node_id));
                if (path.Count > 2) routes.Add((destination, path));
                if (routes.Count == 3) break;
            }
            if (routes.Count == 0) continue;
            foreach (var npc in candidates.Take((int)Math.Floor(candidates.Length * .2)))
            {
                var rng = new RandomNumberGenerator { Seed = stable_seed(month, npc.npc_id, "initial_route") };
                var (destination, path) = routes[rng.RandiRange(0, routes.Count - 1)];
                var ordinary = Enumerable.Range(1, path.Count - 2)
                    .Where(i => string.IsNullOrEmpty(get_location_node_id_at_cell(path[i]))).ToArray();
                if (ordinary.Length == 0) continue;
                var at = ordinary[rng.RandiRange(0, ordinary.Length - 1)];
                npc.travel_origin_node_id = group.Key;
                npc.travel_destination_node_id = destination.node_id;
                npc.is_traveling = true;
                npc.current_subscene_id = "";
                npc.current_state = "途中";
                npc.world_travel_path = path.Duplicate();
                npc.current_grid_cell = path[at];
                npc.has_world_grid_position = true;
                npc.world_travel_next_cell_index = at + 1;
                npc.world_travel_cell_progress_hours = (float)(get_npc_cell_travel_hours(path[at + 1], npc.dun_speed) * rng.Randf() * .9);
                npc.travel_days_total = Math.Max(1, Mathf.CeilToInt((float)(path.Skip(1).Sum(cell => get_npc_cell_travel_hours(cell, npc.dun_speed)) / 24)));
                npc.travel_days_remaining = Math.Max(1, Mathf.CeilToInt((float)(get_remaining_world_travel_hours(npc) / 24)));
                record(npc, month, "initial_travel", "行旅初态", "世界人口初始化时已有的外出行程，不推进主角时间。",
                    $"自 {group.Key} 前往 {destination.node_id}，当前已在途中。", 0, "", false,
                    group.Key, destination.node_id, npc.travel_days_total, npc.travel_days_remaining);
            }
        }
        EmitSignal(SignalName.world_npcs_changed);
    }

    private static bool can_seed_initial_travel(WorldNpcData npc) => npc.is_alive && !npc.is_yuxu_lineage_staff &&
        !npc.is_traveling && string.IsNullOrWhiteSpace(npc.dungeon_id) &&
        !new[] { "执事", "长老", "掌门", "护山", "供奉" }.Any(role => npc.formal_identity.Contains(role, StringComparison.Ordinal));
}
