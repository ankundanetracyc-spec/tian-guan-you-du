using Godot;

public partial class BattleInkCombatVfx
{
    private const string JingzheSequencePath = "res://textures/vfx/jingzhe_lightning_sequence_20261020_v2/lightning_effects.tres";
    private static SpriteFrames? _lightningFrames;
    public static bool IsJingzhe(ActiveSkillData skill) => skill.skill_name == "惊蛰" && skill.source_gongfa_name == "玄枢含章经";

    public static void PresentAttackPose(Node2D actor, Vector2 direction, ActiveSkillData skill)
    {
        if (actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll") is not { } doll) return;
        if (IsJingzhe(skill)) doll.play_cast(direction);
        else if (UsesThrowCast(skill)) doll.play_throw(direction);
        else doll.play_attack(direction, UsesHandChop(skill));
    }

    private static SpriteFrames LightningFrames() => _lightningFrames ??= GD.Load<SpriteFrames>(JingzheSequencePath);

    private static BattleInkCombatVfx? PlayJingzhe(Node2D caster, Node2D target, BattlePaperDoll targetDoll)
    {
        var contact = AttackContactSeconds(caster);
        if (SideDoll(caster) is { } source)
        {
            var charge = SpawnSequence(source, false, "charge", source.side_ink_cast_finger() + new Vector2(0, -18), new Vector2(360, 360), 30, LightningFrames(), new Vector2(512, 512));
            if (charge is not null) { charge._delay = .055f; charge._cancelOnDeath = true; }
        }
        var nativeScale = targetDoll.body_display_height_local() / BattlePaperDoll.SideInkReferenceHeight;
        var upperEdge = target.GetViewportRect().Size.Y * .12f;
        for (Node? parent = target.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is Control stage && stage.Name == "Stage") { upperEdge = stage.GetGlobalRect().Position.Y + 118; break; }
        var ground = target.GetGlobalTransformWithCanvas().Origin;
        var pixelsPerNative = Mathf.Max(.001f, nativeScale * Mathf.Abs(target.GetGlobalTransformWithCanvas().Scale.Y));
        var availableHeight = (ground.Y - upperEdge) / pixelsPerNative;
        var height = Mathf.Clamp((availableHeight - 130) / .84f, 1500, 3400);
        var bolt = SpawnSequence(targetDoll, false, "bolt", new Vector2(0, -height * .385f - 130), new Vector2(1450, height), 9f / contact, LightningFrames(), new Vector2(768, 1024));
        if (bolt is null) return null;
        bolt._worldSequenceOwner = target;
        bolt._worldSequenceScale = Vector2.One * nativeScale;
        bolt._holdAt = contact;
        bolt._hold = .035f + ContactHoldSeconds(1);
        bolt._followUntil = contact + bolt._hold;
        bolt.UpdateSequenceTransform();
        return bolt;
    }

    private static void PlayJingzheContact(Node2D target, BattlePaperDoll doll)
    {
        var impact = SpawnSequence(doll, false, "impact", new Vector2(0, -160), new Vector2(1100, 760), 30, LightningFrames());
        if (impact is null) return;
        impact._worldSequenceOwner = target;
        impact._worldSequenceScale = Vector2.One * doll.body_display_height_local() / BattlePaperDoll.SideInkReferenceHeight;
        impact._followUntil = 0;
        impact._holdAt = 1f / 30;
        impact._hold = ContactHoldSeconds(1);
        impact.UpdateSequenceTransform();
    }
}
