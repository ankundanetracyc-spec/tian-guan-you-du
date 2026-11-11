using Godot;
using Godot.Collections;

/// <summary>Optional, authored spatial information. Missing old data stays missing; this does not enable an item in battle.</summary>
[GlobalClass]
public partial class ItemLayoutPreviewData : Resource
{
    [Export] public int definition_version = 1;
    [Export] public string definition_key = "";
    [Export] public Array<Vector2I> board_cells = [];
    [Export] public bool board_rotation_allowed;
    [Export] public Array<ItemLayoutRangeData> ranges = [];

    public Array<string> validate_definition()
    {
        Array<string> errors = [];
        if (definition_version != 1 || string.IsNullOrWhiteSpace(definition_key))
            errors.Add("占格展示缺少有效定义或版本");
        if (board_cells.Distinct().Count() != board_cells.Count)
            errors.Add("战斗占格包含重复坐标");
        foreach (var range in ranges)
        {
            if (range is null) { errors.Add("范围展示来源为空"); continue; }
            if (string.IsNullOrWhiteSpace(range.title) || string.IsNullOrWhiteSpace(range.source_label)
                || range.direction is < 0 or > 3 || range.offsets.Count == 0
                || range.offsets.Distinct().Count() != range.offsets.Count)
                errors.Add("范围展示的标题、方向或坐标未完整定义");
            if (range.offsets.Contains(Vector2I.Zero)) errors.Add("范围受益格不能冒充来源锚点");
        }
        return errors;
    }
}
