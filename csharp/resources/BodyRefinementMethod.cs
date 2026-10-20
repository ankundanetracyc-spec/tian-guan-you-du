using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>淬体图录中的单本淬体法。时长、基础炼度和部位分配均由法门配置。</summary>
public partial class BodyRefinementMethod : Resource
{
    [Export] public string method_id = "";
    [Export] public string method_name = "未命名淬炼法";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export(PropertyHint.Range, "1,720,1")] public int duration_min_hours = 24;
    [Export(PropertyHint.Range, "1,720,1")] public int duration_max_hours = 24;
    [Export(PropertyHint.Range, "1,100,1")] public int base_yield_min = 8;
    [Export(PropertyHint.Range, "1,100,1")] public int base_yield_max = 12;
    [Export] public Godot.Collections.Array<string> target_parts = [];
    /// <summary>部位到随机分配比例范围，比例使用 0—1。</summary>
    [Export] public GDict allocation_ranges = [];
    /// <summary>部位到有效炼级范围；低于下限为0%，高于上限按一炼25%、二炼5%、三炼及以上1%衰减。</summary>
    [Export] public GDict effective_level_ranges = [];
    [Export] public Godot.Collections.Array<string> required_material_forms = [];
    [Export] public Godot.Collections.Array<string> required_utilities = [];
    [Export(PropertyHint.Range, "0,27,1")] public int required_utility_grade = 1;
    [Export] public bool default_unlocked = true;

    public Vector2 GetAllocationRange(string part)
    {
        var value = allocation_ranges.GetValueOrDefault(part, new Vector2(0f, 0f));
        return value.VariantType == Variant.Type.Vector2 ? value.AsVector2() : new Vector2(0f, 0f);
    }

    public Vector2 GetEffectiveLevelRange(string part)
    {
        var value = effective_level_ranges.GetValueOrDefault(part, new Vector2(0f, 15f));
        return value.VariantType == Variant.Type.Vector2 ? value.AsVector2() : new Vector2(0f, 15f);
    }

    public float GetLevelEfficiency(string part, int level)
    {
        var range = GetEffectiveLevelRange(part);
        var min = Mathf.RoundToInt(range.X); var max = Mathf.RoundToInt(range.Y);
        if (level < min) return 0f;
        if (level <= max) return 1f;
        return level - max switch { 1 => .25f, 2 => .05f, _ => .01f };
    }

    public bool CanUseMaterial(ItemData? item)
    {
        if (item is null || item.item_category != "材料") return false;
        var legacyTexture = (item.craft_data as MaterialCraftData)?.legacy_texture ?? "";
        if (required_material_forms.Count > 0 && !required_material_forms.Contains(legacyTexture)) return false;
        var utilities = ItemAffixCatalog.get_tag_values(item.tags, "功用");
        if (required_utilities.Count > 0 && !required_utilities.Any(required => utilities.Any(actual => BodyRefinementRules.UtilityMatches(actual, required)))) return false;
        return item.grade_value >= required_utility_grade;
    }

    public string GetRequirementText() =>
        $"材料形态：{(required_material_forms.Count == 0 ? "不限" : string.Join(" / ", required_material_forms))}；功用：{(required_utilities.Count == 0 ? "不限" : string.Join(" / ", required_utilities))}；品阶：{ItemAffixCatalog.grade_to_string(required_utility_grade)}以上";

    public string GetAllocationText()
    {
        var parts = new List<string>();
        foreach (var part in target_parts)
        {
            var range = GetAllocationRange(part);
            parts.Add(range.X >= range.Y - .001f ? $"{BodyRefinementRules.GetName(part)} {range.X:P0}" : $"{BodyRefinementRules.GetName(part)} {range.X:P0}～{range.Y:P0}");
        }
        return string.Join("，", parts);
    }

    public string GetEffectiveRangeText()
    {
        var parts = new List<string>();
        foreach (var part in target_parts)
        {
            var range = GetEffectiveLevelRange(part);
            var lower = Mathf.RoundToInt(range.X) <= 0 ? "入炼" : $"{Mathf.RoundToInt(range.X)}炼";
            parts.Add($"{BodyRefinementRules.GetName(part)}{lower}～{Mathf.RoundToInt(range.Y)}炼");
        }
        return string.Join("，", parts);
    }
}
