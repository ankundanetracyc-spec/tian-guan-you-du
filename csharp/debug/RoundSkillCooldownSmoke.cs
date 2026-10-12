using Godot;
using System.Reflection;

/// <summary>验证实际战斗管理器的回合冷却、队列预留、限次及空放边界。</summary>
public partial class RoundSkillCooldownSmoke : Node
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private int checks;
    private void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        checks++;
    }
    private static void Set(BattleManager m, string field, object value) => typeof(BattleManager).GetField(field, Private)!.SetValue(m, value);
    private static object? Invoke(BattleManager m, string method, params object[] args) => typeof(BattleManager).GetMethod(method, Private)!.Invoke(m, args);

    public override void _Ready()
    {
        using var manager = new BattleManager();
        using var unit = new Creature { entity_data = new HumanData() };
        using var other = new Creature { entity_data = new HumanData() };
        using var state = new BattleUnit();
        using var otherState = new BattleUnit();
        using var skill = new ActiveSkillData { skill_name = "冷却回归招式", cooldown_rounds = 1, uses_per_round = 2, uses_per_battle = 2 };
        using var plain = new ActiveSkillData { skill_name = "不限冷却招式" };
        using var response = new ActiveSkillData { skill_name = "未触发应对", cooldown_rounds = 1, slot_category = "response", keywords = ["应对"] };
        try
        {
            Set(manager, "_segmentNumber", 2);
            Set(manager, "_segmentPhase", BattleManager.SegmentPhase.Executing);
            var loadouts = (Dictionary<GodotObject, Godot.Collections.Array<ActiveSkillData>>)typeof(BattleManager).GetField("_combatSkills", Private)!.GetValue(manager)!;
            loadouts[unit] = [skill, plain, response];
            loadouts[other] = [skill];
            manager.all_units.Add(unit); manager.all_units.Add(other);
            manager.battle_units[unit] = state;
            manager.battle_units[other] = otherState;
            Invoke(manager, "InitializeBattleSkillUses");
            Invoke(manager, "ResetRoundSkillUses", unit);
            Check(manager.is_skill_ready(unit, skill), "新战斗技能可用");
            Check(manager.get_skill_cooldown_rounds(unit, skill) == 0, "空放或失败前不生成冷却");
            state.skill_queue.Add(new BattleQueuedSkill(skill, other));
            Check(!manager.is_skill_ready(unit, skill), "规划时同回合不能重复加入有冷却的技能");
            state.skill_queue.Clear();
            Invoke(manager, "StartSkillCooldown", unit, skill);
            Check(!(bool)Invoke(manager, "HasAvailableSegmentSkillUses", unit, skill)!, "使用当回合不能再次执行");
            Check(manager.get_skill_round_cooldown_text(unit, skill).Contains("第4回合"), "使用回合展示第4回合恢复");
            Check(manager.is_skill_ready(other, skill), "相同资源在其他角色上独立冷却");
            Check(CardPresentation.CombatCooldownText(skill, unit, manager).Contains("第4回合"), "信息栏展示正确恢复回合");
            Set(manager, "_segmentNumber", 3); Invoke(manager, "ResetRoundSkillUses", unit);
            Check(!manager.is_skill_ready(unit, skill), "跳过下一完整回合，重置轮次次数也不解锁冷却");
            Set(manager, "_segmentNumber", 4); Invoke(manager, "ResetRoundSkillUses", unit);
            Check(manager.is_skill_ready(unit, skill), "第4回合恢复使用");
            Check(manager.get_skill_uses_remaining(unit, skill).Contains("场 1"), "冷却期间场次不补回");
            Invoke(manager, "StartSkillCooldown", unit, skill);
            Set(manager, "_segmentNumber", 6); Invoke(manager, "ResetRoundSkillUses", unit);
            Check(!manager.is_skill_ready(unit, skill), "冷却结束但本场次数耗尽仍不可用");
            Check(manager.get_skill_cooldown_rounds(unit, skill) == 0, "恢复后冷却状态清零");
            state.skill_queue.Add(new BattleQueuedSkill(plain, other));
            Check(manager.is_skill_ready(unit, plain), "未配置冷却和限次仍可重复规划");
            state.skill_queue.Clear();
            Check(CardRules.NormalCast(response) is null, "无独立普通分支的应对按空放处理");
            Check((bool)Invoke(manager, "CanResolveSegmentAction", unit, new BattleQueuedSkill(response, other), false)!, "未触发纯应对允许空放");
            Check(manager.get_skill_cooldown_rounds(unit, response) == 0, "纯应对空放不占冷却");
            Invoke(manager, "InitializeBattleSkillUses");
            Check(manager.is_skill_ready(unit, skill), "下一场重置冷却与场次");
            skill.cooldown_rounds = 2;
            Set(manager, "_segmentNumber", 2); Invoke(manager, "StartSkillCooldown", unit, skill);
            Set(manager, "_segmentNumber", 4);
            Check(manager.get_skill_cooldown_rounds(unit, skill) > 0, "冷却2回合跳过第3和第4回合");
            Set(manager, "_segmentNumber", 5);
            Check(manager.get_skill_cooldown_rounds(unit, skill) == 0, "冷却2回合第5回合恢复");
            GD.Print($"ROUND_SKILL_COOLDOWN_SMOKE: {checks} checks passed");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
        finally { manager.Free(); unit.Free(); other.Free(); }
    }
}
