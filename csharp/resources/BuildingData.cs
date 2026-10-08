using Godot;

/// <summary>可建造设施、资源点与农田的持久化配置。</summary>
public partial class BuildingData : EntityData
{
    [Export] public bool has_durability = true;
    [Export] public int max_durability = 5;
    [Export] public int current_durability = 5;
    [Export] public bool consume_durability = true;
    [Export] public Vector2I grid_size = Vector2I.One;
    [Export(PropertyHint.MultilineText)] public string summary = "";
    [Export] public string[] build_cost_item_paths = [];
    [Export] public int[] build_cost_counts = [];
    /// <summary>经营账本资源成本；为空时会兼容映射旧的物品路径。</summary>
    [Export] public string[] build_cost_resource_ids = [];
    /// <summary>辖地经营设施标识；建成后由 EstateResourceSystem 提供对应效果。</summary>
    [Export] public string estate_facility_id = "";
    [Export] public string labor_site_id = "";
    [Export] public string labor_resource_id = "";
    [Export] public int labor_yield_min;
    [Export] public int labor_yield_max;
    [Export] public float labor_cycle_hours = 4.0f;
    [Export] public int labor_stamina_cost_per_hour;
    [Export] public string labor_primary_attr = "";
    [Export] public string labor_secondary_attr = "";
    [Export(PropertyHint.Range, "0,1,0.05")] public float labor_primary_weight = 1.0f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float labor_secondary_weight;
    [Export] public string farm_crop_id = "";
    [Export] public float farm_growth_hours;
    [Export(PropertyHint.Range, "0,100,1")] public float soil_fertility;
    [Export(PropertyHint.Range, "0,100,1")] public float soil_spirit;
}
