using Godot;

/// <summary>纸、墨、符笔及固定符箓的能力快照；原料材性不增加字段。</summary>
public partial class TalismanProductItemData : ItemData
{
    [Export(PropertyHint.MultilineText)] public string snapshot_json = "";
    public TalismanCraftingService.ProductSnapshot? Snapshot => TalismanCraftingService.ReadSnapshot(snapshot_json);

    public override string get_grade_string() => Snapshot is { } value
        ? value.Kind == "talisman" ? "符效" + (value.Tier + 1) + "档" :
            value.SpiritGrade == 0 ? "无品" : MaterialCraftData.utility_grade_name(value.SpiritGrade)
        : "数据缺失";

    public override Godot.Collections.Array<string> validate_definition()
    {
        var errors = base.validate_definition();
        if (Snapshot is not { } value || !TalismanCraftingService.ValidateSnapshot(value)) errors.Add("符材结果数据不完整。");
        return errors;
    }
}
