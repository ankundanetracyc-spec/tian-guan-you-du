using Godot;

public partial class BattleInkCombatVfx
{
    // Only existing outward projectile categories. Draw-in, thunder and melee keep their poses.
    public static bool UsesThrowCast(ActiveSkillData skill) => DealsDamage(skill)
        && Profile(skill) is Kind.Spirit or Kind.SpiritBurst or Kind.SoulNeedle;
}
