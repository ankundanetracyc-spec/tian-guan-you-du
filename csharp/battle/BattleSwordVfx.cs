using Godot;

/// <summary>Routes sword attacks to the cleave and on-target wound VFX.</summary>
public static class BattleSwordVfx
{
    public static void PlayAttack(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if(BattleInkCombatVfx.UsesSwordEffects(attacker,skill))BattleInkCombatVfx.PlayAttack(attacker,target,skill);
        else if (IsSwordSkill(skill)) BattleInkSlashEffect.PlaySwordCleave(attacker, target);
        else BattleInkSlashEffect.PlayAttack(attacker, target, skill);
    }

    public static void PlayImpact(GodotObject target, ActiveSkillData skill, bool critical, GodotObject? attacker = null)
    {
        if(BattleInkCombatVfx.UsesSwordEffects(attacker,skill))BattleInkCombatVfx.PlayImpact(target,skill,attacker,critical);
        else if (IsSwordSkill(skill)) BattleInkSlashEffect.PlaySwordImpact(target, attacker, critical);
        else BattleInkSlashEffect.PlayImpactForSkill(target, skill, critical);
    }

    public static bool IsSwordSkill(ActiveSkillData skill)
    {
        if (skill.required_weapon_type == (int)Equipment.WeaponType.SWORD) return true;
        return skill.source_gongfa_name.Contains("剑")
            || skill.skill_name.Contains("剑")
            || skill.skill_name.Contains("斩");
    }
}

public partial class BattleInkSlashEffect
{
    public static void PlaySwordCleave(GodotObject attacker, GodotObject target)
    {
        if (attacker is not Node2D attackerNode || target is not Node2D targetNode) return;
        var scene = CurrentScene();
        if (scene is null) return;

        var attackerAnchor = CombatAnchor(attackerNode);
        var targetAnchor = CombatAnchor(targetNode);
        var offset = targetAnchor - attackerAnchor;
        // Sword cleaves are designed as screen-horizontal sweeps; only their facing changes.
        var direction = offset.X < 0f ? Vector2.Left : Vector2.Right;
        var effect = CreateEffect(scene);
        if (effect is null) return;

        var travel = offset.Length();
        effect.GlobalPosition = (attackerAnchor + targetAnchor) * 0.5f;
        effect._mode = EffectMode.SwordCleave;
        effect._direction = direction;
        effect._normal = new Vector2(-direction.Y, direction.X);
        effect._length = travel;
        effect._duration = SwordCleaveDuration;
        _swordSequence++;
        effect._seed = (attacker.GetInstanceId() ^ target.GetInstanceId() ^ ((ulong)_swordSequence * 131UL)) % 997UL;
        effect._inkColor = new Color(0.012f, 0.012f, 0.012f, 0.98f);
        effect._elapsed = 0f;
        effect.ConfigureShaderVisual();
        effect.QueueRedraw();
    }

    public static void PlaySwordImpact(GodotObject target, GodotObject? attacker = null, bool critical = false)
    {
        if (target is not Node2D targetNode) return;
        var scene = CurrentScene();
        if (scene is null) return;

        var direction = attacker is Node2D attackerNode
            ? (CombatAnchor(targetNode) - CombatAnchor(attackerNode)).Normalized()
            : Vector2.Right;
        if (direction.LengthSquared() < 0.01f) direction = Vector2.Right;
        var effect = CreateEffect(scene);
        if (effect is null) return;

        effect.GlobalPosition = CombatAnchor(targetNode);
        effect._mode = EffectMode.SwordImpact;
        effect._direction = direction;
        effect._normal = new Vector2(-direction.Y, direction.X);
        effect._duration = SwordImpactDuration;
        _swordSequence++;
        effect._seed = (target.GetInstanceId() ^ ((ulong)_swordSequence * 97UL)) % 997UL;
        effect._inkColor = new Color(0.012f, 0.012f, 0.012f, 0.98f);
        effect._critical = critical;
        effect._elapsed = 0f;
        effect.ConfigureShaderVisual();
        effect.QueueRedraw();
    }

}
