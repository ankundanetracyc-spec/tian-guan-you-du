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
        ["应对"]="位于队首且遇到匹配来袭时立即触发；未触发则按正常行动时序结算基础效果。应对与对应来袭行动算同一结算组。",
        ["消耗"]="本即时战斗中，该技能本场最多使用一次。",
        ["固有"]="当前主修或辅修秘籍自动提供心法被动；开战时显示于Buff栏，持续至本场战斗结束。",
        ["保留"]="仅用于凝核牌局；即时队列战斗没有手牌清理阶段。",
        ["虚无"]="仅用于凝核牌局；即时队列战斗没有弃牌堆。",
        ["弃顶"]="弃置时置于弃牌堆顶。",
        ["乘势"]="满足该技能指定的上一项主动行动条件后，触发乘势效果。",
        ["重击"]="拥有拳架时，消耗一层拳架，使本次技能伤害提高50%。",
        ["化解"]="每层抵消本次攻击1点物理伤害；该次攻击结算后全部消散。",
        ["气血"]="每主动下降1层，按公共规则回复最大生命的3%；主动技能与状态效果均可触发回落。",
        ["借力"]="每层使下一项主动施放的物理伤害技能增伤5%；该技能结算后清空。挟势不清空，合劲每层总增伤8%。",
        ["护持"]="优先抵扣受到的伤害，按吸收的伤害减少。"
    };
    public static string Face(ActiveSkillData skill, GodotObject? owner)
    {
        var text=skill.get_brief_description(owner);
        var preferResourceDescription=!string.IsNullOrWhiteSpace(skill.description) &&
            (skill.source_gongfa_name=="赤阳参同经" || (skill.skill_name is "架招" or "卸力"));
        var referenceName=ReferenceName(skill);
        if(preferResourceDescription)
            text=skill.description;
        else if(Reference.GetProperty("cards").TryGetProperty(referenceName,out var card) && skill.skill_name is not ("应敌" or "养息" or "痛击" or "静观"))
            text=skill.skill_type==ActiveSkillData.SkillType.POWER?(string.IsNullOrWhiteSpace(skill.description)?text:skill.description):ResolveFormulas(card.GetProperty("text").GetString()??"",owner,skill);
        foreach(var mechanic in skill.keywords.Where(k=>k!="固有"&&Mechanics.ContainsKey(k)))
            if(!text.Contains(mechanic)) text += $"\n{mechanic}。";
        var responseBaseOnly = false;
        if(skill.skill_name is "架招" or "卸力")
        {
            if(skill.source_gongfa_name!="赤阳参同经")
            {
                text=$"应对：位于队首且遇到敌方物理攻击时立即触发，获得 {CardRules.Block(skill,owner)} 层化解；该次攻击结算后消散。未触发时按正常行动时序结算基础效果。";
                responseBaseOnly = true;
            }
        }
        var rewriteEffects = GongfaSkillRewriteRuntime.GetAdditionalEffectDescriptions(skill, excludePrimary: !responseBaseOnly).ToArray();
        if(rewriteEffects.Length>0)text+="\n"+string.Join("\n",rewriteEffects);
        if(skill.keywords.Contains("乘势"))text=text.Replace("\n乘势。","")+$"\n乘势：上一张为攻击牌时，本次会心率提高 {Mathf.RoundToInt(CardRules.Evaluate(skill.momentum_effect_params,owner)*100)}%。";
        return text;
    }
    private static string ReferenceName(ActiveSkillData skill) => (skill.source_gongfa_name, skill.skill_name) switch
    {
        ("黄庭养形篇", "返照") => "返照（黄庭养形化法）",
        ("黄庭养形篇", "抱一") => "抱一（黄庭养形化法）",
        ("黄庭养形篇", "周行") => "周行（黄庭养形化法）",
        ("黄庭养形篇", "催血") => "催血（黄庭养形化法）",
        ("补虚行血诀", "还阳") => "还阳（补虚行血化法）",
        ("补虚行血诀", "卸力") => "卸力（补虚行血化法）",
        ("内景损益篇", "淬厉") => "淬厉（内景损益化法）",
        ("内景损益篇", "发劲") => "发劲（内景损益化法）",
        ("内景损益篇", "撼山") => "撼山（内景损益化法）",
        ("灵枢经筋篇", "去故") => "去故（灵枢经筋化法）",
        ("灵枢经筋篇", "催血") => "催血（灵枢经筋化法）",
        ("灵枢经筋篇", "发劲") => "发劲（灵枢经筋化法）",
        ("阴符应物经", "承机") => "承机（阴符应物化法）",
        ("阴符应物经", "挟势") => "挟势（阴符应物化法）",
        ("阴符应物经", "合劲") => "合劲（阴符应物化法）",
        ("内景损益篇", "卸力") => "卸力（留痕化法）",
        ("灵枢经筋篇", "平息") => "平息（灵枢经筋化法）",
        ("阴符应物经", "卸力") => "卸力（阴符应物化法）",
        ("乘物游骸真经", "平息") => "平息（乘物游骸化法）",
        ("乘物游骸真经", "发劲") => "发劲（乘物游骸化法）",
        ("乘物游骸真经", "催血") => "催血（乘物游骸化法）",
        ("乘物游骸真经", "撼山") => "撼山（乘物游骸化法）",
        ("补虚行血诀", "平息") => "平息（补虚行血化法）",
        ("补虚行血诀", "催血") => "催血（补虚行血化法）",
        ("赤阳参同经", "平息") => "平息（赤阳参同化法）",
        ("赤阳参同经", "卸力") => "卸力（赤阳参同化法）",
        ("赤阳参同经", "发劲") => "发劲（赤阳参同化法）",
        ("赤阳参同经", "撼山") => "撼山（赤阳参同化法）",
        _ => skill.skill_name
    };
    private static string ResolveFormulas(string text, GodotObject? owner, ActiveSkillData skill)
    {
        // 技能词典中的公式只在详情中展示，技能说明替换为最终整数。
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
                explanation+=$"\n本次化解层数 = {Readable(skill.stacks_formula)} = {CardRules.Block(skill,owner)}。";
            if(term=="气血" && skill.source_gongfa_name!="赤阳参同经") explanation+=$"\n每层回落回复 = round(最大生命×3%) = {Mathf.RoundToInt(CardRules.Number(owner,"max_health")*.03f)}。";
            if(term=="乘势")explanation+=$"\n本次技能会心率加成 = {Readable(skill.momentum_effect_params)} = {Mathf.RoundToInt(CardRules.Evaluate(skill.momentum_effect_params,owner)*100)}%。";
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
