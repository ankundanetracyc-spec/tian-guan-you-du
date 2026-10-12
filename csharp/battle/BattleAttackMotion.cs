using Godot;

/// <summary>Combat staging only; never changes cells, ranges, targets or skill data.</summary>
public static class BattleAttackMotion
{
    public const float ApproachSeconds = .18f;
    public const float ReturnSeconds = .22f;
    public const float BeforeContactSeconds = .04f;

    public static bool UsesApproach(ActiveSkillData skill) => BattleInkCombatVfx.Profile(skill) is
        BattleInkCombatVfx.Kind.Strike or BattleInkCombatVfx.Kind.Release or BattleInkCombatVfx.Kind.Heavy
        or BattleInkCombatVfx.Kind.Flow or BattleInkCombatVfx.Kind.Bite or BattleInkCombatVfx.Kind.Pounce
        or BattleInkCombatVfx.Kind.Charge or BattleInkCombatVfx.Kind.Thrust;

    public static Vector2 ContactPosition(Node2D attacker, Node2D target, Vector2 home)
    {
        var delta = target.GlobalPosition - home;
        var side = Mathf.Abs(delta.X) > 1f ? Mathf.Sign(delta.X)
            : attacker is Creature creature && creature.faction == (int)WorldEntity.Faction.ENEMY ? -1f : 1f;
        var spacing = (attacker.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.get_contact_radius() ?? 65f)
            + (target.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.get_contact_radius() ?? 65f) + 12f;
        // Already-close actors need no backwards step before the strike.
        if (Mathf.Abs(delta.X) > 1f) spacing = Mathf.Min(spacing, Mathf.Abs(delta.X));
        return target.GlobalPosition - new Vector2(side * spacing, 0);
    }

    public static Vector2 ReturnPosition(Vector2 contact, Vector2 home, float progress) =>
        contact.Lerp(home, progress) + Vector2.Up * Mathf.Sin(progress * Mathf.Pi)
        * Mathf.Min(32f, contact.DistanceTo(home) * .08f);
}
