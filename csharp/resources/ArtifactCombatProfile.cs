using Godot;

/// <summary>明确配置的法器效果；不从材料或功用品阶推导。测试参数仅在开发样本中给出。</summary>
public partial class ArtifactCombatProfile : Resource
{
    [Export] public float physical_defense_ignore_ratio;
    [Export] public int inner_block_bonus;
    [Export] public int use_energy_cost;
    [Export] public int use_mana_cost;
    [Export] public float use_base_damage;
    [Export] public int use_shield;
    [Export] public int uses_per_round;
    [Export] public int uses_per_battle;
    [Export] public int cooldown_rounds;
}
