using Godot;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>从卡表生成牌面与机制说明，所有数值使用当前持有者计算。</summary>
public static class CardPresentation
{
    private static JsonDocument? _reference;
    private static JsonElement Reference => (_reference ??= JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://resources/card_reference.json"))).RootElement;
    private static readonly System.Collections.Generic.Dictionary<string,string> Mechanics=new()
    {
        ["应对"]="准备完成后进入持招并停在队列最前方；满足牌面条件时优先结算。玩家可在结算前点击队列牌取消，已支付资源不返还。",
        ["消耗"]="本即时战斗中，该技能本场最多使用一次。",
        ["固有"]="主修或辅修槽位中的秘籍自动提供心法被动；心法随战斗快照在开战时生效，不设心法槽位，也不占技能槽。",
        ["保留"]="仅用于凝核牌局；即时队列战斗没有手牌清理阶段。",
        ["虚无"]="仅用于凝核牌局；即时队列战斗没有弃牌堆。",
        ["弃顶"]="弃置时置于弃牌堆顶。",
        ["乘势"]="满足本牌指定的上一张出牌条件后，触发乘势效果。",
        ["重击"]="拥有拳架时，消耗一层拳架，使本次伤害提高50%。",
        ["化解"]="每层抵消本次攻击1点物理伤害；该次攻击结算后全部消散。",
        ["气血"]="每下降1层，回复最大生命的3%；即时战斗中由明确的卡牌或状态事件触发回落。",
        ["借力"]="每层使下一张主动打出的物理伤害牌增伤5%；该牌结算后清空。挟势不清空，合劲每层总增伤8%。",
        ["护持"]="优先抵扣受到的伤害，按吸收的伤害减少。"
    };
    public static string Face(ActiveSkillData skill, GodotObject? owner)
    {
        var text=skill.get_brief_description(owner);
        if(Reference.GetProperty("cards").TryGetProperty(skill.skill_name,out var card) && skill.skill_name is not ("应敌" or "养息" or "痛击" or "静观"))
            text=skill.skill_type==ActiveSkillData.SkillType.POWER?(string.IsNullOrWhiteSpace(skill.description)?text:skill.description):ResolveFormulas(card.GetProperty("text").GetString()??"",owner,skill);
        foreach(var mechanic in skill.keywords.Where(k=>Mechanics.ContainsKey(k)))
            if(!text.Contains(mechanic)) text += $"\n{mechanic}。";
        if(skill.skill_name is "架招" or "卸力")text=$"应对：准备完成后持招，获得 {CardRules.Block(skill,owner)} 层化解。";
        if(skill.keywords.Contains("乘势"))text=text.Replace("\n乘势。","")+$"\n乘势：上一张为攻击牌时，本次会心率提高 {Mathf.RoundToInt(CardRules.Evaluate(skill.momentum_effect_params,owner)*100)}%。";
        return text;
    }
    private static string ResolveFormulas(string text, GodotObject? owner, ActiveSkillData skill)
    {
        // 卡表公式只在右侧解释中展示，牌面替换为最终整数。
        text=Regex.Replace(text,@"伤害段数 = 1 \+ floor\([^)]*\)",m=>$"伤害段数为 {1+(int)CardRules.Number(owner,"spent_youhai")/2}");
        for(var start=0;start<text.Length;)
        {
            var match=Regex.Match(text[start..],@"\b(?:round|max|floor)\(");
            if(!match.Success) break;
            var from=start+match.Index;var end=from+match.Length;var depth=1;
            while(end<text.Length && depth>0){if(text[end]=='(')depth++;if(text[end]==')')depth--;end++;}
            var formula=text[from..end];
            if(formula.StartsWith("round(") && !string.IsNullOrEmpty(skill.damage_formula))formula=skill.damage_formula;
            var value=Mathf.RoundToInt(formula==skill.damage_formula?BattleActionResolver.preview_damage(owner,skill):CardRules.Evaluate(formula,owner)).ToString();
            text=text[..from]+value+text[end..];start=from+value.Length;
        }
        text=Regex.Replace(text,@"最大生命×(\d+(?:\.\d+)?)%",m=>
            $"{Mathf.RoundToInt(CardRules.Number(owner,"max_health")*float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)/100)}点生命");
        return text.Replace("伤害 = ","伤害为 ").Replace("化解层数 = ","化解层数为 ");
    }
    public static string Underline(string text) => Regex.Replace(text,TermPattern(text), m=>string.Concat(m.Value.Select(c=>$"{c}\u0332")));
    private static string[] Terms(string text)
    {
        var names=Mechanics.Keys.Concat(Reference.GetProperty("terms").EnumerateObject().Select(p=>p.Name))
            .Concat(Reference.GetProperty("cards").EnumerateObject().Where(p=>p.Value.GetProperty("text").GetString()!.Contains("唯一Buff")).Select(p=>p.Name))
            .Concat(new[]{"物理伤害","法术伤害","神魂伤害"}).Distinct().Where(text.Contains).OrderByDescending(s=>s.Length).ToArray();
        return names;
    }
    private static string TermPattern(string text) => string.Join("|",Terms(text).Select(Regex.Escape).DefaultIfEmpty("(?!)"));
    public static string Details(ActiveSkillData skill, GodotObject? owner)
    {
        var face=Face(skill,owner);var entries=new List<string>();
        foreach(var term in Regex.Matches(face,TermPattern(face)).Select(m=>m.Value).Distinct())
        {
            var explanation=Mechanics.GetValueOrDefault(term,"");
            if(string.IsNullOrEmpty(explanation) && Reference.GetProperty("terms").TryGetProperty(term,out var entry)) explanation=entry.GetString()??"";
            if(string.IsNullOrEmpty(explanation) && Reference.GetProperty("cards").TryGetProperty(term,out var power))explanation=power.GetProperty("text").GetString()??"";
            if(term.EndsWith("伤害"))
            {
                var formula=string.IsNullOrWhiteSpace(skill.damage_formula)
                    ? skill.base_damage.ToString("0.##")+string.Concat(skill.damage_bonuses.Select(b=>$" + {CardRules.Attributes.FirstOrDefault(p=>p.Value==b.attribute).Key??b.attribute}×{b.multiplier:0.##}"))
                    : skill.damage_formula;
                explanation=$"基础伤害 = {Readable(formula)} = {Mathf.RoundToInt(skill.calculate_damage(owner))}。\n计入当前自身增伤后 = {Mathf.RoundToInt(BattleActionResolver.preview_damage(owner,skill))}；命中、会心与目标防御在攻击时结算。";
            }
            if(term=="化解" && !string.IsNullOrWhiteSpace(skill.stacks_formula))
                explanation+=$"\n本牌层数 = {Readable(skill.stacks_formula)} = {CardRules.Block(skill,owner)}。";
            if(term=="气血") explanation+=$"\n每层回落回复 = round(最大生命×3%) = {Mathf.RoundToInt(CardRules.Number(owner,"max_health")*.03f)}。";
            if(term=="乘势")explanation+=$"\n本牌会心率加成 = {Readable(skill.momentum_effect_params)} = {Mathf.RoundToInt(CardRules.Evaluate(skill.momentum_effect_params,owner)*100)}%。";
            if(!string.IsNullOrEmpty(explanation))entries.Add($"{term}\n{explanation}");
        }
        return string.Join("\n\n",entries);
    }
    private static string Readable(string formula)
    {
        foreach(var pair in CardRules.Attributes.OrderByDescending(p=>p.Value.Length))formula=formula.Replace(pair.Value,pair.Key);
        return formula;
    }
}
