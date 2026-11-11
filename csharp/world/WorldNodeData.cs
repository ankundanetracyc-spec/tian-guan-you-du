using Godot;
using Godot.Collections;

/// <summary>大世界战略地图上可交互地点的静态配置。</summary>
public partial class WorldNodeData : Resource
{
    public enum NodeType { City, Sect, Resource, Dungeon, War, Village, Home, Wilderness }
    public enum DisplayLevel { World = 1, Region, Local, Ephemeral }

    [Export] public string node_id = "";
    [Export] public string node_name = "";
    [Export] public NodeType node_type = NodeType.Village;

    [Export] public DisplayLevel display_level = DisplayLevel.Local;
    [Export] public bool is_ephemeral_template;
    [Export(PropertyHint.Range, "1,30,1")] public int refresh_days = 3;
    [Export] public Array<string> spawn_territory_ids = [];
    [Export] public Array<string> spawn_terrain_tags = [];
    [Export(PropertyHint.Range, "1,100,1")] public int spawn_weight = 10;

    [Export] public string territory_id = "";
    [Export] public bool requires_unlock;
    [Export] public bool unlocked = true;
    /// <summary>节点抵达后是否有可进入的地点内部；后续可由剧情或势力规则关闭。</summary>
    [Export] public bool enterable = true;
    [Export(PropertyHint.MultilineText)] public string unlock_hint = "";

    [Export] public Vector2 map_position = Vector2.Zero;
    [Export(PropertyHint.Range, "0,8,0.05")] public float min_zoom;
    [Export(PropertyHint.Range, "-1,8,0.05")] public float max_zoom = -1.0f;
    [Export(PropertyHint.Range, "12,240,1")] public float marker_size = 68.0f;

    [Export] public string terrain = "";
    [Export] public int spirit_pressure = 1;
    [Export] public int danger_level = 1;
    [Export] public int population;
    [Export] public string faction_id = "";
    [Export] public WorldConflictRules.Policy hostile_combat_policy = WorldConflictRules.Policy.Inherit;
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public Array<string> connected_nodes = [];
    /// <summary>地点内部空间；人物位置通过 node_id + subscene_id 保存。</summary>
    [Export] public Array<WorldSubsceneData> subscenes = [];
}
