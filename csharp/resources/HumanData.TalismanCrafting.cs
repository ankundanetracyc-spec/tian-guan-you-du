using Godot;

public partial class HumanData
{
    [Export] public Godot.Collections.Array<string> talisman_method_ids = [];
    [Export] public Godot.Collections.Array<string> talisman_recipe_ids = [];
    [Export] public Godot.Collections.Array<string> talisman_records = [];
    // 结果同时随人物保存；产品文件缺失时可从这份冻结能力恢复，不能重新用当前造诣推算。
    [Export] public Godot.Collections.Dictionary<string, string> talisman_product_definitions = new();
}
