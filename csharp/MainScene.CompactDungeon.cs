using Godot;

public partial class MainScene
{
    private void open_compact_fern_path()
    {
        var dungeon = _east_woods_dungeon;
        // The junction is at the real East Woods entrance, never a shortcut out
        // of its depths. Both maps keep their own route and resource histories.
        if (dungeon is null || dungeon.is_dynamic_instance || !dungeon.can_leave() ||
            dungeon.get_current_route_node_id() != "forest_edge") return;
        _east_woods_dungeon_state = dungeon.get_route_state();
        _east_woods_dungeon_state["inside"] = false;
        if (_dynamic_dungeon_state.Count == 0)
            _dynamic_dungeon_state = DynamicDungeonExpedition.CreateEmpty(current_game_hour());
        var instance = DynamicDungeonExpedition.EnsureCompactInstance(_dynamic_dungeon_state, _player_world_grid_cell, current_game_hour());
        var route = DynamicDungeonExpedition.GetRoute(instance);
        if (!instance.GetValueOrDefault("inside", false).AsBool())
            route["current"] = DynamicDungeonExpedition.GetEntryNodeId(instance);
        DynamicDungeonExpedition.StoreRoute(instance, route);
        _npc_world?.set_simulation_enabled(false);
        open_dynamic_dungeon(instance.GetValueOrDefault("instance_id", "").AsString());
        mark_save_dirty();
    }
}
