using Godot;

public partial class MainScene
{
    private bool sync_east_woods_ecology_state(Godot.Collections.Dictionary routeState, string playerRouteNode)
    {
        var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
        if (time is null) return false;
        if (!EastWoodsEcology.Synchronize(routeState, time.get_total_game_hours(), playerRouteNode)) return false;
        _east_woods_dungeon_state = routeState.Duplicate(true);
        mark_save_dirty();
        return true;
    }

    /// <summary>Catch up a closed route lazily when world time has advanced.</summary>
    private void synchronize_east_woods_ecology()
    {
        var panel = _east_woods_dungeon;
        var routeState = panel?.get_route_state() ?? _east_woods_dungeon_state.Duplicate(true);
        var playerNode = panel?.get_current_route_node_id() ?? routeState.GetValueOrDefault("current", "forest_edge").AsString();
        if (!sync_east_woods_ecology_state(routeState, playerNode)) return;

        if (panel is not null && panel.Visible && !panel.is_route_traveling())
        {
            panel.restore_route_state(routeState);
            _east_woods_dungeon_state = panel.get_route_state();
        }
    }
}
