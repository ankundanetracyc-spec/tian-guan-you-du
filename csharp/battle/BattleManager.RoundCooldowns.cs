using Godot;

public partial class BattleManager
{
    // 以原技能实例为键，普通效果分支和实际应对共用一个恢复回合。
    private readonly Dictionary<GodotObject, Dictionary<ActiveSkillData, int>> _skillReadyRounds = [];

    public int get_skill_cooldown_rounds(GodotObject unit, ActiveSkillData skill)
    {
        if (!_realtimeMode || !_skillReadyRounds.TryGetValue(unit, out var rounds)) return 0;
        return Math.Max(0, rounds.GetValueOrDefault(skill, 0) - _segmentNumber);
    }

    public string get_skill_round_cooldown_text(GodotObject unit, ActiveSkillData skill)
    {
        var remaining = get_skill_cooldown_rounds(unit, skill);
        if (remaining == 0) return "可用";
        return $"冷却中，第{_segmentNumber + remaining}回合恢复";
    }

    private void StartRoundSkillCooldown(GodotObject unit, ActiveSkillData skill)
    {
        if (!_realtimeMode || skill.cooldown_rounds <= 0) return;
        if (!_skillReadyRounds.TryGetValue(unit, out var rounds))
            _skillReadyRounds[unit] = rounds = [];
        rounds[skill] = _segmentNumber + skill.cooldown_rounds + 1;
    }
}
