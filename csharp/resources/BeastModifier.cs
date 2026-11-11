using Godot;

/// <summary>妖兽的一个独立变种；精英不使用本资源，而是在其后统一追加三项基础战斗属性 +25%。</summary>
[GlobalClass]
public partial class BeastModifier : Resource
{
    [Export] public string modifier_id = "";
    [Export] public string display_name = "";
    [Export] public float physique_multiplier = 1f;
    [Export] public float intelligence_multiplier = 1f;
    [Export] public float soul_multiplier = 1f;
    [Export] public BeastRacialTalent? racial_talent_override;
    [Export] public BasicDeckResource? deck_override;
    [Export] public CombatBoardLayout? combat_board_layout_override;
}
