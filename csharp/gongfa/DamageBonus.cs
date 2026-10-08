using Godot;

/// <summary>将指定角色属性按倍率折算为技能伤害的配置。</summary>
public partial class DamageBonus : Resource
{
    [Export] public string attribute = "";
    [Export] public float multiplier;
}
