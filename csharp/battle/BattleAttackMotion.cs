using Godot;

/// <summary>Combat staging only; never changes cells, ranges, targets or skill data.</summary>
public static class BattleAttackMotion
{
    public const float ApproachSeconds = .18f;
    public const float ReturnSeconds = .22f;
    public const float BeforeContactSeconds = .04f;

    // Brisk staging with intentionally sliding steps; foot pace is independent.
    public static float TravelSeconds(Node2D actor, Vector2 from, Vector2 to, bool retreat = false)
    {
        var doll = actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        if (doll?.uses_queue_avatar() != true) return retreat ? ReturnSeconds : ApproachSeconds;
        var speed = doll.body_display_height_local() * Mathf.Abs(actor.GlobalScale.X) * (700f / 160f);
        return Mathf.Clamp(from.DistanceTo(to) / Mathf.Max(1f, speed), .35f, .70f);
    }

    public static void ApplySmallAvatarTravel(Node2D actor, Vector2 from, Vector2 to, float progress)
    {
        if (!GodotObject.IsInstanceValid(actor)) return;
        actor.GlobalPosition = from.Lerp(to, Mathf.Clamp(progress, 0f, 1f));
        actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.set_queue_travel_progress(progress);
    }

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

    public static Vector2 ReturnPosition(Node2D actor, Vector2 contact, Vector2 home, float progress) =>
        actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is BattlePaperDoll doll && (doll.uses_static_beast_poses() || doll.uses_queue_avatar())
            ? contact.Lerp(home, progress) : ReturnPosition(contact, home, progress);
}
