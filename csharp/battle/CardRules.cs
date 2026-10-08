using Godot;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>牌面与战斗共用的属性读取、公式与机制规则。</summary>
public static class CardRules
{
    public static readonly System.Collections.Generic.Dictionary<string, string> Attributes = new()
    {
        ["体魄"]="physique", ["敏捷"]="agility", ["灵慧"]="intelligence",
        ["神魂"]="soul", ["体道造诣"]="body_proficiency", ["术法造诣"]="magic_proficiency",
        ["剑道造诣"]="sword_proficiency", ["最大生命"]="max_health", ["借力层数"]="borrowed_force",
        ["实际消耗血炉值"]="spent_furnace", ["本场实际消耗游骸层数"]="spent_youhai"
    };
    public static float Number(GodotObject? owner, string key)
    {
        if (!GodotObject.IsInstanceValid(owner)) return 0;
        if (key == "borrowed_force" && owner is Creature creature) return creature.get_status_stacks("借力");
        if (key == "spent_furnace") return Number(owner, "blood_furnace_value");
        if (owner!.HasMeta(key)) return owner.GetMeta(key).AsSingle();
        if (owner.GetPropertyList().Any(p => p["name"].AsString() == key))
        {
            var v=owner.Get(key); if(v.VariantType is Variant.Type.Int or Variant.Type.Float) return v.AsSingle()+(owner is Creature c?c.status_bonus(key):0);
        }
        if(owner is WorldEntity entity && entity.entity_data is { } data) return Number(data,key)+(owner is Creature c?c.status_bonus(key):0);
        return 0;
    }
    public static float Evaluate(string formula, GodotObject? owner)
    {
        if(string.IsNullOrWhiteSpace(formula)) return 0;
        var expression=formula;
        if(formula.Contains(':'))
        {
            float total=0;
            foreach(var part in formula.Split(';',StringSplitOptions.RemoveEmptyEntries))
            {
                var pair=part.Split(':',2);
                if(pair.Length==2 && float.TryParse(pair[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var factor))
                    total+=factor*(pair[0]=="base"?1:Number(owner,pair[0]));
            }
            return total;
        }
        foreach(var pair in Attributes.OrderByDescending(p=>p.Key.Length)) expression=expression.Replace(pair.Key,pair.Value);
        expression=expression.Replace("×","*").Replace("÷","/");
        expression=Regex.Replace(expression,@"\b[a-zA-Z_][a-zA-Z_0-9]*\b", m =>
            m.Value is "round" or "floor" or "ceil" or "max" or "min" or "abs" ? m.Value : Number(owner,m.Value).ToString(CultureInfo.InvariantCulture));
        using var evaluator=new Expression();
        if(evaluator.Parse(expression)!=Error.Ok) throw new InvalidOperationException($"无法解析卡牌公式：{formula}");
        var result=evaluator.Execute();
        if(evaluator.HasExecuteFailed()) throw new InvalidOperationException($"无法结算卡牌公式：{formula}");
        return result.AsSingle();
    }
    public static int Block(ActiveSkillData skill, GodotObject? owner, int fallback=1) =>
        string.IsNullOrWhiteSpace(skill.stacks_formula)?Math.Max(1,fallback):Math.Max(1,Mathf.FloorToInt(Evaluate(skill.stacks_formula,owner)));
    public static bool IsResponse(ActiveSkillData skill)=>skill.keywords.Contains("应对");
    public static bool ResponseMatches(ActiveSkillData skill, string damageType) => IsResponse(skill) &&
        (skill.counter_condition is "" or "on_attacked" || skill.counter_condition=="on_physical_attacked" && damageType=="physical");
}
