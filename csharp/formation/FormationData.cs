using Godot;
using Godot.Collections;

/// <summary>可由阵盘部件组装并在战斗或地块上生效的阵法定义。</summary>
public partial class FormationData : Resource
{
    [Export] public string formation_name = "";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public int level = 1;
    [Export] public float max_durability = 1.0f;
    [Export] public Array<string> body_tag_requirements = [];
    [Export] public Array<string> ink_tag_requirements = [];
    [Export] public Array<string> pivot_tag_requirements = [];
    [Export] public Dictionary pivot_bonus_tags = [];
    [Export] public Array<string> fuel_tag_requirements = [];
    [Export] public Dictionary fuel_bonus_tags = [];
    [Export] public float fuel_rate;
    [Export] public float battle_physical_defense_bonus;
    [Export] public float spirit_pressure_bonus;
    [Export] public Dictionary pivot_effect_mults = [];
}
