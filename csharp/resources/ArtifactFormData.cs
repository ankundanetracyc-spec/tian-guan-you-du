using Godot;
using Godot.Collections;

/// <summary>法宝器形定义的最小数据底座；具体器形与炼制服务可在此基础上继续扩展。</summary>
public partial class ArtifactFormData : Resource
{
    [Export] public string form_id = "";
    [Export] public string form_name = "";
    [Export] public string icon_path = "";
    [Export] public Array<string> allowed_textures = [];
    [Export] public Array<string> accepted_utilities = [];
    [Export] public int inner_spirit_base;
    [Export] public int outer_spirit_base;
    [Export] public int outer_energy_cost;
    [Export] public int outer_mana_cost;
    [Export] public int outer_body_base;
    [Export] public int durability_base;
    [Export] public string inner_trigger_id = "";
    [Export] public string outer_trigger_id = "";
    [Export] public int response_limit = 1;
}
