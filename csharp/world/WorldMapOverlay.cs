using Godot;
using Dictionary = Godot.Collections.Dictionary;
using GodotArray = Godot.Collections.Array;

/// <summary>
/// 旧世界地图覆盖层的兼容节点。
/// 当前节点地图不再实例化它；保留接口使未迁移主场景可安全调用，避免遗留路径阻断迁移。
/// </summary>
public partial class WorldMapOverlay : Node2D
{
    public override void _Ready() => AddToGroup("world_map_overlay");
    public bool is_overlay_ready() => true;
    public void refresh_overlay() => QueueRedraw();
    public void set_team_icon(Variant cell, int member_count) { }
    public void clear_team_icon() => QueueRedraw();
    public void show_path_preview(GodotArray cells, Variant origin, Variant target, float hours) { }
    public void clear_path_preview() { }
    public void update_dispatch_status(Dictionary route_data) { }
    public void update_stationed_routes(Dictionary routes) { }
    public void set_world_data(Resource world_data) { }
    public void set_team_marker(Vector2I cell, bool visible, int member_count) { }
    public void set_stationed_route(Vector2I coordinate, GodotArray path_out, GodotArray path_back, bool visible, Vector2I current_cell, bool resting) { }
    public void remove_stationed_route(Vector2I coordinate) { }
    public bool is_stationed_route_visible(Vector2I coordinate) => false;
    public void set_path_preview(GodotArray path, Variant target, float total_hours, GodotArray cumulative_hours, Vector2I origin) { }
}
