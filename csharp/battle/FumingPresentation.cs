using Godot;

/// <summary>Display identity only. The original skill remains the queue/cost/cooldown identity.</summary>
public static class FumingPresentation
{
    public static ActiveSkillData Resolve(GodotObject? owner, ActiveSkillData skill, BattleManager? manager)
    {
        if (skill.skill_name != "复命" || owner is null || manager is null) return skill;
        var copied = manager.gongfa_combat.GetLastNonResponseSkill(owner);
        return copied is not null && copied.skill_name != "复命" && !CardRules.IsResponse(copied) ? copied : skill;
    }
    public static string Name(GodotObject? owner, ActiveSkillData skill, BattleManager? manager)
    {
        var display = Resolve(owner, skill, manager);
        return display == skill ? skill.skill_name : display.skill_name + "（复命）";
    }
    public static string QueueName(GodotObject? owner, BattleQueuedSkill? action, BattleManager manager)
        => action is null ? "空位" : action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill.skill_name == "复命"
            ? Name(owner, action.skill, manager) : action.get_display_name();
}
