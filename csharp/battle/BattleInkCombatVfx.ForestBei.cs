using Godot;

public partial class BattleInkCombatVfx
{
    private static Shader? _forestBeiShader;
    public static bool IsForestBeiPackCall(ActiveSkillData skill) => skill.special_effects.Any(effect => effect.GetValueOrDefault("effect", "").AsString() == "forest_bei_pack_call");
    public static bool UsesForestBeiCry(GodotObject? source, ActiveSkillData skill) => skill.skill_name == "狈啸" && source is Node2D actor && actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.uses_forest_bei_portrait() == true;

    private static BattleInkCombatVfx? SpawnBei(Vector2 at, Vector2 size, Vector2 direction, int phase, ActiveSkillData? skill = null)
    {
        _forestBeiShader ??= GD.Load<Shader>("res://shaders/battle_forest_bei.gdshader");
        if (_forestBeiShader is null) return null;
        var effect = Spawn(at, size, direction.Angle(), Kind.SoulNeedle, phase, .52f, skill, false, _forestBeiShader);
        if (effect is not null) { effect.Name = phase == 0 ? "ForestBeiCry" : phase == 1 ? "ForestBeiContact" : "ForestBeiCall"; effect._material!.SetShaderParameter("canvas_aspect", size.X / size.Y); }
        return effect;
    }
    private static BattleInkCombatVfx? PlayForestBeiCry(Node2D source, Node2D target, ActiveSkillData skill)
    {
        var doll = source.GetNode<BattlePaperDoll>("BattlePaperDoll");
        var start = doll.beast_voice_anchor();
        var end = Anchor(target, true).Lerp(Anchor(target), .35f);
        var delta = end - start;
        var distance = delta.Length();
        var height = Mathf.Clamp(doll.body_display_height_local() * Mathf.Abs(source.GlobalScale.Y) * .7f, 80, 180);
        var width = distance + height;
        var effect = SpawnBei((start + end) * .5f, new Vector2(width, height), delta.LengthSquared() > .01f ? delta.Normalized() : Facing(source), 0, skill);
        if (effect is not null) { var contact = AttackContactSeconds(source); effect._material!.SetShaderParameter("travel_span", distance / width); effect._material.SetShaderParameter("contact_progress", contact / effect._duration); effect._holdAt = contact; effect._hold = .035f + ContactHoldSeconds(HitWeight(skill)); }
        return effect;
    }
    private static void PlayForestBeiContact(Node2D source, Node2D target, ActiveSkillData skill, bool critical)
    {
        var end = Anchor(target, true).Lerp(Anchor(target), .35f);
        var effect = SpawnBei(end, Vector2.One * (critical ? 170 : 145), (end - Anchor(source)).Normalized(), 1, skill);
        if (effect is not null) { effect._holdAt = .035f; effect._hold = ContactHoldSeconds(HitWeight(skill), critical); }
    }
    private static void PlayForestBeiCall(Node2D source)
    {
        var doll = source.GetNode<BattlePaperDoll>("BattlePaperDoll");
        doll.play_beast_command(Facing(source));
        SpawnBei(doll.beast_voice_anchor(), Vector2.One * 145, Facing(source), 2);
    }
    public static void PlayForestBeiBlessing(Node2D wolf) => SpawnBei(Anchor(wolf), Vector2.One * 105, Vector2.Right, 2);
}
