using Godot;
using Godot.Collections;

/// <summary>阵盘、阵眼、阵源和灵络的硬件上限。材料词缀只能通过这些字段影响阵法性能。</summary>
public partial class FormationHardwareData : Resource
{
    [Export(PropertyHint.Range, "1,64,1")] public int component_slots = 4;
    [Export(PropertyHint.Range, "1,8,1")] public int pulse_layers = 1;
    [Export(PropertyHint.Range, "1,128,1")] public int eye_complexity = 4;
    [Export(PropertyHint.Range, "0,99,1")] public int source_capacity = 6;
    [Export(PropertyHint.Range, "0,99,1")] public int source_recharge_per_turn = 1;
    [Export(PropertyHint.Range, "0,99,1")] public int source_peak_output = 3;
    [Export(PropertyHint.Range, "0,99,1")] public int spirit_link_peak_output = 3;
    [Export(PropertyHint.Range, "0,99,1")] public int board_peak_load = 3;
    [Export] public Array<string> board_material_tags = [];
    [Export] public Array<string> ink_material_tags = [];
    [Export] public Array<string> eye_material_tags = [];
    [Export] public Array<string> source_material_tags = [];

    private bool has(string tag) => board_material_tags.Contains(tag) || ink_material_tags.Contains(tag) || eye_material_tags.Contains(tag) || source_material_tags.Contains(tag);
    public int effective_component_slots() => component_slots;
    public int effective_eye_complexity() => eye_complexity + (has("功用[明心]") || has("功用[凝神]") ? 1 : 0);
    public int effective_source_capacity() => source_capacity + (has("功用[蕴灵]") ? 1 : 0);
    public int effective_source_recharge() => source_recharge_per_turn + (has("功用[聚灵]") ? 1 : 0);
    public int effective_source_peak() => source_peak_output + (has("功用[纳气]") ? 1 : 0);
    public int effective_link_peak() => spirit_link_peak_output + (has("功用[通脉]") ? 1 : 0);
    public int effective_board_load() => board_peak_load + (has("功用[坚韧]") || has("功用[固本]") ? 1 : 0);

    public void apply_material_tags(Array<string> tags)
    {
        if (tags.Contains("功用[通脉]")) spirit_link_peak_output += 1;
        if (tags.Contains("功用[蕴灵]")) source_capacity += 1;
        if (tags.Contains("功用[聚灵]")) source_recharge_per_turn += 1;
        if (tags.Contains("功用[纳气]")) source_peak_output += 1;
        if (tags.Contains("功用[坚韧]") || tags.Contains("功用[固本]")) board_peak_load += 1;
        if (tags.Contains("功用[明心]") || tags.Contains("功用[凝神]")) eye_complexity += 1;
    }
}
