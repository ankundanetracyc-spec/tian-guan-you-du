using Godot;

public partial class SkillExecutor
{
    public void execute_segment_response(GodotObject defender, GodotObject attacker, ActiveSkillData skill)
    {
        execute_response(defender, attacker, skill, showTriggerLabel: false);
        execute_counter_extras(defender, attacker, skill);
    }

    public void execute_counter_extras(GodotObject defender, GodotObject attacker, ActiveSkillData skill)
    {
        if (skill.counter_special_effects.Count == 0 && skill.counter_status_effects.Count == 0) return;
        if (skill.Duplicate() is not ActiveSkillData counterEffects) return;
        counterEffects.special_effects = skill.counter_special_effects;
        counterEffects.status_effects = skill.counter_status_effects;
        ApplyStatusEffects(counterEffects, defender, attacker);
        ApplySelfSpecialEffects(counterEffects, defender, attacker);
    }
}
