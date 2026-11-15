using Godot;

public partial class BattleInkCombatVfx
{
    private static Shader? _forestWolfShader;

    private static bool IsForestWolfAttack(Node2D actor, Kind kind) => kind is Kind.Bite or Kind.Pounce
        && actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.uses_forest_wolf_portrait() == true;

    public static bool UsesForestWolfEffects(GodotObject? actor, ActiveSkillData skill) =>
        actor is Node2D wolf && IsForestWolfAttack(wolf, Profile(skill));

    private static float ForestWolfEffectHeight(Node2D actor)
    {
        var doll = actor.GetNode<BattlePaperDoll>("BattlePaperDoll");
        return Mathf.Clamp(doll.body_display_height_local() * Mathf.Abs(actor.GlobalScale.Y) * .72f, 64, 165);
    }

    private static BattleInkCombatVfx? SpawnForestWolf(Vector2 at, Vector2 size, Vector2 direction,
        Kind kind, int phase, float duration, ActiveSkillData skill, bool critical = false)
    {
        _forestWolfShader ??= GD.Load<Shader>("res://shaders/battle_forest_wolf.gdshader");
        if (_forestWolfShader is null) return null;
        var effect = Spawn(at, size, direction.Angle(), kind, phase, duration, skill, critical, _forestWolfShader);
        if (effect is not null)
        {
            effect.Name = phase == 0 ? "ForestWolfIncoming" : "ForestWolfContact";
            effect._material!.SetShaderParameter("canvas_aspect", size.X / size.Y);
        }
        return effect;
    }

    private static BattleInkCombatVfx? PlayForestWolfAttack(Node2D actor, Node2D target, ActiveSkillData skill, Kind kind)
    {
        var start = actor.GetNode<BattlePaperDoll>("BattlePaperDoll").forest_wolf_mouth_anchor();
        var end = Anchor(target);
        var delta = end - start;
        var distance = delta.Length();
        var direction = distance > .1f ? delta / distance : Facing(actor);
        var height = ForestWolfEffectHeight(actor);
        var width = distance + height * .9f;
        var effect = SpawnForestWolf((start + end) * .5f, new Vector2(width, height), direction, kind, 0, .48f, skill);
        if (effect is null) return null;
        var contact = AttackContactSeconds(actor);
        effect._material!.SetShaderParameter("travel_span", distance / width);
        effect._material.SetShaderParameter("contact_progress", contact / effect._duration);
        effect._holdAt = contact;
        effect._hold = .035f + ContactHoldSeconds(HitWeight(skill));
        return effect;
    }

    private static void PlayForestWolfContact(Node2D actor, Node2D target, ActiveSkillData skill, Kind kind, bool critical, int segment)
    {
        var end = Anchor(target);
        var delta = end - actor.GetNode<BattlePaperDoll>("BattlePaperDoll").forest_wolf_mouth_anchor();
        var direction = delta.LengthSquared() > .01f ? delta.Normalized() : Facing(actor);
        var height = ForestWolfEffectHeight(actor) * (critical ? 1.13f : 1);
        var size = new Vector2(height * 1.25f, height * (kind == Kind.Pounce ? 1.18f : 1));
        var effect = SpawnForestWolf(end, size, direction, kind, 1, .52f, skill, critical);
        if (effect is null) return;
        effect._delay = Mathf.Clamp(segment, 0, 6) * .055f;
        effect._holdAt = .035f;
        effect._hold = ContactHoldSeconds(HitWeight(skill), critical);
    }
}
