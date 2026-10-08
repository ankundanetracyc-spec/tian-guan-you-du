using Godot;

/// <summary>世界地图上连接真实地点的路线与航路状态。</summary>
public partial class WorldRouteData : Resource
{
    [Export] public string route_id = "";
    [Export] public string route_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public string from_node_id = "";
    [Export] public string to_node_id = "";
    [Export(PropertyHint.Range, "1,365,1")] public int travel_days = 1;
    [Export] public bool unlocked = true;
}
