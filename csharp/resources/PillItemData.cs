using Godot;

/// <summary>同丹种、丹阶与丹效档的物品共用静态定义，不保存每颗评分或炉次。</summary>
public partial class PillItemData : ItemData
{
    public const string CommonIconPath = "res://textures/items/inventory_density_20261029/pill_2x2.png";
    public const int MaxStackSize = 50;
    public const int BottleCapacity = MaxStackSize;

    [Export] public string pill_id = "";
    [Export] public int pill_rank = 9;
    [Export(PropertyHint.Range, "0,4,1")] public int efficacy_tier;
    [Export] public string container_material_id = PillContainerCatalog.Porcelain;
    public string EffectText => AlchemyProductCatalog.EffectText(pill_id, efficacy_tier);
    public override string get_grade_string() => AlchemyProductCatalog.Rank(pill_id) switch
    {
        7 => "七品",
        _ => "九品"
    };
    public override Godot.Collections.Array<string> validate_definition()
    {
        var errors = base.validate_definition();
        if (!can_stack || max_stack != MaxStackSize || get_storage_volume() != 1)
            errors.Add($"丹药每颗占1容量，同种丹药最多堆叠{MaxStackSize}颗");
        if (icon_path != CommonIconPath)
            errors.Add("丹药必须共用统一丹药图标");
        if (inventory_width != 2 || inventory_height != 2)
            errors.Add("统一丹丸图标按2×2展示");
        if (!AlchemyProductCatalog.Has(pill_id) || efficacy_tier is < 0 or > 4 || pill_rank != AlchemyProductCatalog.Rank(pill_id))
            errors.Add("丹药标识、丹阶或丹效档无效");
        if (PillContainerCatalog.LoadContainerDefinition(container_material_id) is null)
            errors.Add("丹药没有对应的有效容器材质定义");
        if (AlchemyProductCatalog.IsEnabled(pill_id))
        {
            int expected = pill_id == "huichun" ? 2 : AlchemyProductCatalog.IsBattlePill(pill_id) ? 1 : 0;
            if (usage_type != expected) errors.Add("丹药用途与正式开放范围不一致");
        }
        return errors;
    }
}
