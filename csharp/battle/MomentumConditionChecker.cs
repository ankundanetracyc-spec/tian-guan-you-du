using Godot;
using Godot.Collections;

/// <summary>判断卡牌“乘势”触发条件的无状态服务。</summary>
public partial class MomentumConditionChecker : RefCounted
{
    public static bool check(string condition, Dictionary ctx)
    {
        if (string.IsNullOrEmpty(condition) || condition == "none") return true;
        var lastSkillType = ctx.TryGetValue("last_skill_type", out var value) ? value.AsInt32() : -1;
        return condition switch
        {
            "after_move" => lastSkillType == 1,
            "after_attack" => lastSkillType == 0,
            _ => false,
        };
    }
}
