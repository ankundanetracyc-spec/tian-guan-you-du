using Godot;

/// <summary>同丹种、丹阶与丹效档的物品共用静态定义，不保存每颗评分或炉次。</summary>
public partial class PillItemData : ItemData
{
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
        if (!AlchemyProductCatalog.Has(pill_id) || efficacy_tier is < 0 or > 4 || pill_rank != AlchemyProductCatalog.Rank(pill_id))
            errors.Add("丹药标识、丹阶或丹效档无效");
        if (PillContainerCatalog.LoadContainerDefinition(container_material_id) is null)
            errors.Add("丹药没有对应的有效容器材质定义");
        return errors;
    }
}
