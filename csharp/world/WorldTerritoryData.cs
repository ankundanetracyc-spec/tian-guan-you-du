using Godot;

/// <summary>大世界中可交互区域的静态配置；旧 TerritoryLevel.State 值保留用于已序列化资源兼容。</summary>
public partial class WorldTerritoryData : Resource
{
    public enum TerritoryLevel { State, MajorFaction, MinorFaction }

    [Export] public string territory_id = "";
    [Export] public string territory_name = "";
    [Export] public string region_subtitle = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public string main_faction = "";
    [Export(PropertyHint.MultilineText)] public string faction_summary = "";
    [Export] public TerritoryLevel territory_level = TerritoryLevel.State;
    [Export] public string parent_territory_id = "";
    [Export] public string controlling_faction_id = "";
    [Export] public string controlling_faction_name = "";
    [Export] public string governance_type = "";
    [Export] public bool is_direct_domain;
    [Export] public bool requires_unlock;
    [Export] public bool unlocked = true;
    [Export] public Color mask_color = new(0, 0, 0, 0);
    [Export] public Vector2[] polygon = [];
    [Export] public Color fill_color = new(0.45f, 0.35f, 0.24f, 0.16f);
    [Export] public Color outline_color = new(0.72f, 0.61f, 0.42f, 0.82f);
    [Export(PropertyHint.Range, "0,8,0.05")] public float min_zoom;
    [Export(PropertyHint.Range, "-1,8,0.05")] public float max_zoom = 0.82f;
}
