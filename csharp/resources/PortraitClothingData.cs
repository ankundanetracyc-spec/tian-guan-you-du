using Godot;
using Godot.Collections;

/// <summary>Cosmetic clothing item that selects one portrait clothing layer without changing armor stats.</summary>
public partial class PortraitClothingData : ItemData
{
    [Export(PropertyHint.Enum, "male,female,both")] public string portrait_gender = "both";
    [Export(PropertyHint.Range, "0,6,1")] public int appearance_style_index;

    public bool can_be_worn_by(HumanData? human)
    {
        if (human is null) return false;
        return portrait_gender == "both" || portrait_gender == (human.gender == 1 ? "female" : "male");
    }

    public override Array<string> validate_definition()
    {
        Array<string> errors = base.validate_definition();
        if (portrait_gender is not ("male" or "female" or "both"))
            errors.Add("立绘衣物的性别范围必须为 male、female 或 both");
        if (appearance_style_index < 0 || appearance_style_index > 6)
            errors.Add("立绘衣物款式编号必须位于0至6");
        if (item_category != "装备")
            errors.Add("立绘衣物必须使用装备分类");
        return errors;
    }
}
