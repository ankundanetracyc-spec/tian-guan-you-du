using Godot;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>从卡表生成牌面与机制说明，所有数值使用当前持有者计算。</summary>
public static partial class CardPresentation
{
    private static JsonDocument? _reference;
    private static string[]? _encyclopediaResourceTerms;
    private static JsonElement Reference => (_reference ??= JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://resources/card_reference.json"))).RootElement;
    private static readonly System.Collections.Generic.Dictionary<string,string> Mechanics=new()
    {
        ["应对"]="当前槽遇到匹配来袭时立即触发并支付应对费用；未触发按独立普通效果及费用结算，未配置则免费空放。应对与来袭算同一结算组。",
        ["消耗"]="本即时战斗中，该技能本场最多使用一次。",
        ["固有"]="当前主修或辅修秘籍自动提供心法被动；开战时显示于Buff栏，持续至本场战斗结束。",
        ["保留"]="仅用于凝核牌局；即时队列战斗没有手牌清理阶段。",
        ["虚无"]="仅用于凝核牌局；即时队列战斗没有弃牌堆。",
        ["弃顶"]="弃置时置于弃牌堆顶。",
        ["乘势"]="满足该技能指定的上一项主动行动条件后，触发乘势效果。",
        ["化解"]="详见修行百科的化解词条。",
        ["气血"]="层数降低时，按当前气血回落回复规则结算；回复率及替代效果由公共规则和当前生效效果共同决定。",
        ["散血"]="按技能注明的触发条件，直接打散目标指定层数的气血；不视为气血层数降低。",
        ["打散"]="按技能注明的触发条件，打散目标指定层数的气血；不视为气血层数降低。",
        ["穿盾"]="本次攻击无视目标护盾。",
        ["剑意"]="上限10层。由养锋与格剑获得；尽锋命中后消耗全部剑意，每层提高该次伤害。战斗结束时清除。",
        ["借力"]="每层使下一项主动施放的物理伤害技能增伤5%；该技能结算后清空。挟势不清空，合劲每层总增伤8%。",
        ["护盾"]="受到伤害时优先抵扣；每次受击先于生命值结算。",
        ["护持"]="优先抵扣受到的伤害，按吸收的伤害减少。",
        ["僵直"]="每层对应1战斗帧，推进1战斗帧减少1层，随战斗倍速推进；暂停与动作演出不计时。层数大于0时行动条停止推进并保留进度；再次施加刷新层数，不累加。",
        ["周天"]="炁达到当前法力上限的两倍时完成周天，清空炁并使当前法力回满。"
    };
    public static string Face(ActiveSkillData skill, GodotObject? owner) => Face(skill, owner, null);

    /// <summary>统一格式化技能说明；可传入已经按当前秘籍升级整理过的原文。</summary>
    public static string Face(ActiveSkillData skill, GodotObject? owner, string? descriptionOverride)
    {
        if (descriptionOverride is null && UseBoardPresentation(skill, owner))
            return BoardFace(skill, owner);
        var text=descriptionOverride ?? skill.get_brief_description(owner);
        var preferResourceDescription=descriptionOverride is null && !string.IsNullOrWhiteSpace(skill.description) &&
            (skill.source_gongfa_name is "赤阳参同经" or "玄枢含章经" or "百骸衍枝化生宝册" or "太和感物养元宝册" or "舒卷天章" or "玉髓回澜养真宝册" or "抱虚养晦通玄录" || (skill.skill_name is "架招" or "卸力"));
        var referenceName=ReferenceName(skill);
        if(descriptionOverride is not null)
            text=descriptionOverride;
        else if(preferResourceDescription)
            text=skill.description;
        else if(Reference.GetProperty("cards").TryGetProperty(referenceName,out var card) && skill.skill_name is not ("应敌" or "养息" or "痛击" or "静观"))
            text=skill.skill_type==ActiveSkillData.SkillType.POWER?(string.IsNullOrWhiteSpace(skill.description)?text:skill.description):(card.GetProperty("text").GetString()??"");
        foreach(var mechanic in skill.keywords.Where(k=>k!="固有"&&Mechanics.ContainsKey(k)))
        {
            if(!text.Contains(mechanic) && !(mechanic=="散血" && text.Contains("打散"))) text += $"\n{mechanic}。";
        }
        if(skill.get_presentation_tags().Contains("散血")&&!text.Contains("不经行动队列施展时"))
            text += "\n本技能不经行动队列施展时，命中后打散目标1层气血。";
        var responseBaseOnly = false;
        if(skill.skill_name is "架招" or "卸力")
        {
            if(skill.source_gongfa_name!="赤阳参同经")
            {
                text=$"位于当前槽且遇到敌方物理攻击时，可触发应对。触发应对时，获得 {CardRules.Block(skill,owner)} 层化解。";
                responseBaseOnly = true;
            }
        }
        var rewriteEffects = GongfaSkillRewriteRuntime.GetAdditionalEffectDescriptions(skill, excludePrimary: !responseBaseOnly).ToArray();
        if(rewriteEffects.Length>0)text+="\n"+string.Join("\n",rewriteEffects);
        if(skill.keywords.Contains("乘势"))text=text.Replace("\n乘势。","")+$"\n乘势：上一张为攻击牌时，本次会心率提高 {Mathf.RoundToInt(CardRules.Evaluate(skill.momentum_effect_params,owner)*100)}%。";
        if (CardRules.IsResponse(skill))
        {
            text = Regex.Replace(text, @"未触发时按正常行动时序结算基础效果(?:，不消耗血炉)?。", "");
            text = text.Replace("未触发应对时空放，不消耗体力、法力或使用次数，不产生效果。", "").Trim();
            text += CardRules.NormalCast(skill) is { } normal
                ? $"\n未触发应对时，{CombatEffectText(normal, owner)}"
                : "\n未触发应对时，空放，仅消耗本槽行动时间，不消耗体力、法力或使用次数，不产生效果。";
        }
        text=ResolveFormulas(text,owner,skill);
        text=AppendStructuredFormulaDetails(text,skill,owner);
        if(!string.IsNullOrWhiteSpace(skill.forged_equipment_description))text += "\n" + skill.forged_equipment_description.Trim();
        return text;
    }
    private static string ReferenceName(ActiveSkillData skill) => (skill.source_gongfa_name, skill.skill_name) switch
    {
        ("胎息经", "返照") => "返照（胎息经化法）",
        ("胎息经", "平息") => "平息（胎息经化法）",
        ("胎息经", "抱一") => "抱一（胎息经化法）",
        ("胎息经", "周行") => "周行（胎息经化法）",
        ("胎息经", "催血") => "催血（胎息经化法）",
        ("补虚行血诀", "还阳") => "还阳（补虚行血化法）",
        ("补虚行血诀", "归元") => "归元（补虚行血化法）",
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
        ("赤阳参同经", "朱明") => "朱明（赤阳参同化法）",
        ("赤阳参同经", "撼山") => "撼山（赤阳参同化法）",
        ("内景损益篇", "痛击") => "痛击（内景损益化法）",
        ("灵枢经筋篇", "引筋") => "引筋（灵枢经筋化法）",
        _ => skill.skill_name
    };
    private static string ResolveFormulas(string text, GodotObject? owner, ActiveSkillData skill)
    {
        var values=new System.Collections.Generic.Dictionary<string,float>(StringComparer.Ordinal);
        var pattern=@"(?<label>(?<![A-Za-z0-9_])[A-Z](?![A-Za-z0-9_])|伤害段数|基础伤害|每段伤害|该段伤害|额外伤害|追加伤害|化解层数|伤害)(?:公式)?\s*(?:=|：|:)\s*(?<formula>[^。；，\r\n]+)[。；]?";
        var matches=Regex.Matches(text,pattern);
        if(matches.Count>0)
        {
            var builder=new System.Text.StringBuilder();var cursor=0;
            foreach(Match match in matches)
            {
                builder.Append(text[cursor..match.Index]);
                var label=match.Groups["label"].Value;var formula=match.Groups["formula"].Value.Trim();
                var isLetter=label.Length==1&&char.IsAsciiLetterUpper(label[0]);
                if(formula.Contains("攻击者",StringComparison.Ordinal))
                {
                    if(isLetter)
                    {
                        var unresolved=builder.ToString();
                        unresolved=Regex.Replace(unresolved,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}(?![A-Za-z0-9_])","本次结算值");
                        builder.Clear();builder.Append(unresolved);
                        builder.Append($"计算方式：{formula}（概率在来袭结算时确定）。");
                    }
                    else builder.Append($"{label}：按来袭结算确定。计算方式：{formula}。");
                    cursor=match.Index+match.Length;
                    continue;
                }
                if(isLetter&&(formula.Contains("施加者",StringComparison.Ordinal)||formula.Contains("目标",StringComparison.Ordinal)))
                {
                    var unresolved=builder.ToString();
                    if(label=="C")
                    {
                        unresolved=Regex.Replace(unresolved,$@"获得\s*{Regex.Escape(label)}层僵直","获得僵直（层数按双方属性结算）");
                        unresolved=Regex.Replace(unresolved,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}(?![A-Za-z0-9_])","本次结算值");
                        builder.Clear();builder.Append(unresolved);
                        builder.Append($"僵直层数公式：{Readable(formula)}（1层对应1战斗帧）。");
                    }
                    else
                    {
                        var replacement=label=="B"?"僵直概率":"本次结算值";
                        unresolved=Regex.Replace(unresolved,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}(?![A-Za-z0-9_])",replacement);
                        builder.Clear();builder.Append(unresolved);
                        builder.Append($"僵直概率公式：{Readable(formula)}（按施加者与目标属性结算）。");
                    }
                    cursor=match.Index+match.Length;
                    continue;
                }
                if(isLetter&&formula.Contains("本次气血回落",StringComparison.Ordinal))
                {
                    var pending=Regex.Replace(builder.ToString(),$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}\s*点护盾","护盾（数值在本次气血回落结算后确定）");
                    pending=Regex.Replace(pending,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}(?![A-Za-z0-9_])","本次回落结算值");
                    builder.Clear();builder.Append(pending);
                    builder.Append($"计算方式：{Readable(formula)}（本次气血回落结算后确定）。");
                    cursor=match.Index+match.Length;
                    continue;
                }
                if(!TryEvaluateFormula(formula,owner,values,out var numericValue))
                {
                    if(isLetter&&label!="R")
                    {
                        var unresolved=builder.ToString();
                        unresolved=Regex.Replace(unresolved,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}\s*点护盾","按本次受击结算确定的护盾");
                        unresolved=Regex.Replace(unresolved,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}(?![A-Za-z0-9_])","本次结算值");
                        builder.Clear();builder.Append(unresolved);
                        builder.Append($"计算方式：{Readable(formula)}（数值在对应效果结算时确定）。");
                    }
                    else if(label=="R")
                        builder.Append($"当前气血回落回复率：按本次生效修正结算。计算方式：{Readable(formula)}。");
                    else
                        builder.Append($"{label}：按本次效果结算确定。计算方式：{Readable(formula)}。");
                    cursor=match.Index+match.Length;
                    continue;
                }

                var value=formula.Contains('%')||formula.Contains("个百分点",StringComparison.Ordinal)?FormatNumber(numericValue*100f)+"%":FormatNumber(numericValue);
                if(label=="R")
                {
                    var percent=FormatNumber(numericValue*100f)+"%";
                    var prefix=builder.ToString();
                    prefix=Regex.Replace(prefix,@"(?<![A-Za-z0-9_])R(?![A-Za-z0-9_])",percent);
                    builder.Clear();builder.Append(prefix);
                    values[label]=numericValue;
                    builder.Append($"计算公式：{FormulaWithVariables(formula,owner,values,label)} = {percent}。");
                }
                else if(isLetter)
                {
                    var prefix=builder.ToString();
                    prefix=Regex.Replace(prefix,$@"(?<![A-Za-z0-9_]){Regex.Escape(label)}(?![A-Za-z0-9_])",value);
                    builder.Clear();builder.Append(prefix);
                    values[label]=numericValue;
                    builder.Append($"计算公式：{FormulaWithVariables(formula,owner,values,label)} = {value}。");
                }
                else
                {
                    var effectiveLabel=label=="伤害段数"?"伤害段数":label;
                    var unit=label=="伤害段数"?" 段":label=="化解层数"?" 层":label.Contains("伤害",StringComparison.Ordinal)?" 点":"";
                    if(label=="伤害段数")numericValue=Mathf.Max(1,Mathf.RoundToInt(numericValue));
                    value=FormatNumber(numericValue);
                    builder.Append($"{effectiveLabel}：{value}{unit}。计算公式：{FormulaWithVariables(formula,owner,values,label)} = {value}{unit}。");
                }
                cursor=match.Index+match.Length;
            }
            builder.Append(text[cursor..]);text=builder.ToString();
        }

        text=Regex.Replace(text,@"最大生命\s*[×x*]\s*(?<rate>\d+(?:\.\d+)?)%",m=>
        {
            var rate=float.Parse(m.Groups["rate"].Value,System.Globalization.CultureInfo.InvariantCulture);
            var maxHealth=CardRules.Number(owner,"max_health");
            var result=Mathf.RoundToInt(maxHealth*rate/100f);
            return $"{result} 点生命。计算公式：最大生命[{FormatNumber(maxHealth)}]×{FormatNumber(rate)}% = {result} 点生命";
        });
        return text.Replace("伤害 = ","伤害：").Replace("化解层数 = ","化解层数：");
    }

    private static bool TryEvaluateFormula(string formula,GodotObject? owner,System.Collections.Generic.Dictionary<string,float> variables,out float value)
    {
        if(!GodotObject.IsInstanceValid(owner))
        {
            var identifiers=Regex.Matches(formula,@"[A-Za-z_][A-Za-z_0-9]*").Select(match=>match.Value).Distinct();
            if(identifiers.Any(identifier=>identifier is not ("round" or "floor" or "ceil" or "max" or "min" or "abs" or "base")))
            {value=0;return false;}
            if(Regex.IsMatch(formula,@"[\u4e00-\u9fff]")){value=0;return false;}
        }
        var expanded=formula;
        foreach(var pair in variables.OrderByDescending(pair=>pair.Key.Length))
            expanded=Regex.Replace(expanded,$@"(?<![A-Za-z0-9_]){Regex.Escape(pair.Key)}(?![A-Za-z0-9_])",pair.Value.ToString("0.########",System.Globalization.CultureInfo.InvariantCulture));
        var unresolved=expanded.Replace("个百分点", "");
        foreach(var pair in CardRules.Attributes.OrderByDescending(pair=>pair.Key.Length))unresolved=unresolved.Replace(pair.Key,pair.Value);
        // 未来结算值不是角色属性，不能送进 Expression 当作 self 属性执行。
        if(Regex.IsMatch(unresolved,@"[\u4e00-\u9fff]")){value=0;return false;}
        try { value=CardRules.Evaluate(expanded,owner);return float.IsFinite(value); }
        catch(System.Exception) { value=0;return false; }
    }

    private static string FormulaWithVariables(string formula,GodotObject? owner,System.Collections.Generic.Dictionary<string,float> variables,string currentLabel)
    {
        var display=FormulaWithValues(formula,owner);
        foreach(var pair in variables.Where(pair=>pair.Key!=currentLabel).OrderByDescending(pair=>pair.Key.Length))
            display=Regex.Replace(display,$@"(?<![A-Za-z0-9_]){Regex.Escape(pair.Key)}(?![A-Za-z0-9_])",$"{pair.Key}[{FormatNumber(pair.Value)}]");
        return display;
    }

    private static string AppendStructuredFormulaDetails(string text,ActiveSkillData skill,GodotObject? owner)
    {
        var lines=new List<string>();
        void Add(string label,string formula,string unit,float? knownResult=null)
        {
            if(string.IsNullOrWhiteSpace(formula))return;
            var values=new System.Collections.Generic.Dictionary<string,float>(StringComparer.Ordinal);
            var evaluated=0f;
            if(knownResult is null&&!TryEvaluateFormula(formula,owner,values,out evaluated))
            {
                if(!text.Contains($"{label}：",StringComparison.Ordinal))lines.Add($"{label}：按当前角色属性结算。计算公式：{FormulaWithValues(formula,owner)}。");
                return;
            }
            var result=knownResult??evaluated;
            var value=FormatNumber(result);
            // 原文已紧跟同一结算值的公式时，不再重复列出。
            if(Regex.IsMatch(text,$@"计算公式：[^。\r\n]*=\s*{Regex.Escape(value)}(?:\s*(?:点|层|段))?。"))return;
            lines.Add($"{label}：{value}{unit}。计算公式：{FormulaWithValues(formula,owner)} = {value}{unit}。");
        }
        if(!string.IsNullOrWhiteSpace(skill.stacks_formula))
            Add("状态层数",skill.stacks_formula," 层",GodotObject.IsInstanceValid(owner)?CardRules.Block(skill,owner):null);
        if(!string.IsNullOrWhiteSpace(skill.damage_formula)||!Mathf.IsZeroApprox(skill.base_damage)||skill.damage_bonuses.Count>0)
            Add("基础伤害",DamageFormula(skill)," 点",GodotObject.IsInstanceValid(owner)?Mathf.RoundToInt(skill.calculate_damage(owner)):null);
        foreach(var effect in skill.special_effects)
        {
            var formula=effect.GetValueOrDefault("formula","").AsString();if(string.IsNullOrWhiteSpace(formula))continue;
            var id=effect.GetValueOrDefault("effect","").AsString();
            var label=id switch{"shield"=>"护盾", "restore_stamina"=>"体力回复", "restore_mana"=>"法力回复", _=>"效果数值"};
            float? value=null;
            if(TryEvaluateFormula(formula,owner,new System.Collections.Generic.Dictionary<string,float>(),out var evaluated))value=id is "shield" or "restore_stamina" or "restore_mana"?Mathf.RoundToInt(evaluated):evaluated;
            Add(label,formula," 点",value);
        }
        return lines.Count==0?text:text.TrimEnd()+"\n"+string.Join("\n",lines);
    }

    /// <summary>即时战斗技能栏使用的简明效果文案：伤害目标与类型由运行时配置统一生成。</summary>
    public static string CombatEffectText(ActiveSkillData skill, GodotObject? owner)
    {
        var text = Face(skill, owner);
        text = Regex.Replace(text, @"(?m)^使用次数：.*(?:\r?\n|$)", "").Trim();
        if (!DealsDamage(skill)) return text;

        // 避免旧牌面中目标、伤害类型和公式各写一遍。保留伤害增幅、状态和触发条件，
        // 再由技能数据生成唯一的目标与伤害句；具体公式见数值详情。
        var retained = new List<string>();
        var skipFollowingFormula=false;
        foreach (var line in text.Split('\n'))
        {
            var clauses = Regex.Split(line, @"(?<=[。；])");
            foreach (var clause in clauses)
            {
                if (string.IsNullOrWhiteSpace(clause)) continue;
                if(IsDamageResultClause(clause)||Regex.IsMatch(clause,@"(?:伤害段数|每段伤害|基础伤害|该段伤害|额外伤害)\s*[：:]"))
                {
                    skipFollowingFormula=true;
                    continue;
                }
                if(skipFollowingFormula&&clause.Contains("计算公式",StringComparison.Ordinal))
                {
                    skipFollowingFormula=false;
                    continue;
                }
                skipFollowingFormula=false;
                retained.Add(clause.Trim());
            }
        }

        var summary = StandardDamageSentence(skill, text, owner);
        var extra = string.Join("", retained).Trim(' ', '。', '；', '\r', '\n');
        return string.IsNullOrWhiteSpace(extra) ? summary : $"{summary}\n{extra}";
    }

    /// <summary>返回悬浮信息栏中的精确公式、当前属性代入值及当前自身增伤预估。</summary>
    public static string CombatCalculationDetails(ActiveSkillData skill, GodotObject? owner, BattleManager? manager = null)
    {
        var entries = new List<string>();
        if (DealsDamage(skill))
        {
            var formula = DamageFormula(skill);
            var damageName = DamageTypeText(skill.damage_type);
            var segments = FindAssignedFormulas(RawSkillText(skill))
                .FirstOrDefault(entry => entry.label == "伤害段数");
            var segmentCount = 1;
            if (!string.IsNullOrWhiteSpace(segments.formula))
            {
                segmentCount = Mathf.Max(1, Mathf.RoundToInt(CardRules.Evaluate(segments.formula, owner)));
                entries.Add($"伤害段数：{segmentCount} 段。计算公式：{FormulaWithValues(segments.formula, owner)} = {segmentCount} 段。");
            }

            var perSegmentLabel = segmentCount > 1 ? "每段基础" : "基础";
            var baseDamage=Mathf.RoundToInt(skill.calculate_damage(owner));
            entries.Add($"{perSegmentLabel}{damageName}伤害：{baseDamage} 点。计算公式：{FormulaWithValues(formula, owner)} = {baseDamage} 点。");
            var estimatedLabel = segmentCount > 1 ? "每段当前自身增伤后预计伤害" : "当前自身增伤后预计伤害";
            var preview=Mathf.Max(1,Mathf.RoundToInt(BattleActionResolver.preview_damage(owner,skill)));
            var previewMultiplier=Mathf.IsZeroApprox(skill.calculate_damage(owner))?1f:BattleActionResolver.preview_damage(owner,skill)/skill.calculate_damage(owner);
            entries.Add($"{estimatedLabel}：{preview} 点。计算公式：max(1, round(({FormulaWithValues(formula,owner)})×{FormatNumber(previewMultiplier)})) = {preview} 点。{DamagePreviewNote(skill)}");

            foreach (var entry in FindAssignedFormulas(RawSkillText(skill)))
            {
                if (entry.label is "伤害段数" or "伤害" || string.IsNullOrWhiteSpace(entry.formula)) continue;
                if (NormalizeFormula(entry.formula) == NormalizeFormula(formula)) continue;
                if (!entry.label.Contains("伤害")) continue;
                var baseValue = CardRules.Evaluate(entry.formula, owner);
                var primaryValue = skill.calculate_damage(owner);
                var multiplier = Mathf.IsZeroApprox(primaryValue) ? 1f : BattleActionResolver.preview_damage(owner, skill) / primaryValue;
                var value = Mathf.RoundToInt(baseValue);
                entries.Add($"{entry.label}：{value} 点{damageName}伤害。计算公式：{FormulaWithValues(entry.formula, owner)} = {value} 点；当前自身增伤后预计 {Mathf.RoundToInt(baseValue * multiplier)} 点。");
            }
        }

        if (!string.IsNullOrWhiteSpace(skill.stacks_formula))
            entries.Add($"状态层数：{CardRules.Block(skill, owner)} 层。计算公式：{FormulaWithValues(skill.stacks_formula, owner)} = {CardRules.Block(skill, owner)} 层。");

        foreach (var effect in skill.special_effects)
        {
            var formula = effect.GetValueOrDefault("formula", "").AsString();
            if (string.IsNullOrWhiteSpace(formula)) continue;
            var effectId = effect.GetValueOrDefault("effect", "").AsString();
            var label = effectId switch
            {
                "shield" => "护持",
                "restore_stamina" => "体力回复",
                "restore_mana" => "法力回复",
                _ => "效果数值"
            };
            var amount=effectId=="shield"?Math.Max(0,Mathf.RoundToInt(CardRules.Evaluate(formula,owner))):Mathf.RoundToInt(CardRules.Evaluate(formula,owner));
            entries.Add($"{label}：{amount} 点。计算公式：{FormulaWithValues(formula, owner)} = {amount} 点。");
        }

        foreach (Match match in Regex.Matches(RawSkillText(skill), @"最大生命\s*[×x*]\s*(?<rate>\d+(?:\.\d+)?)%"))
        {
            var rate = float.Parse(match.Groups["rate"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var value = Mathf.RoundToInt(CardRules.Number(owner, "max_health") * rate / 100f);
            var line = $"生命回复：{value} 点。计算公式：最大生命[{FormatNumber(CardRules.Number(owner, "max_health"))}]×{FormatNumber(rate)}% = {value} 点。";
            if (!entries.Contains(line)) entries.Add(line);
        }

        if (skill.skill_name=="复命"&&GodotObject.IsInstanceValid(owner)&&manager is not null)
        {
            var copied=manager.gongfa_combat.GetLastNonResponseSkill(owner!);
            if(copied is not null&&!CardRules.IsResponse(copied)&&copied.skill_name!="复命")
            {
                entries.Add($"本次复制：{copied.skill_name}");
                var copiedDetails=CombatCalculationDetails(copied,owner,manager);
                if(!string.IsNullOrWhiteSpace(copiedDetails))entries.Add($"复制招式数值：\n{copiedDetails}");
            }
            else entries.Add("本次复制：尚无可复制的非应对技能。");
        }

        if (CardRules.IsResponse(skill) && CardRules.NormalCast(skill) is { } normal)
        {
            var normalDetails = CombatCalculationDetails(normal, owner, manager);
            if (!string.IsNullOrWhiteSpace(normalDetails)) entries.Add($"未应对普通分支：\n{normalDetails}");
        }
        return string.Join("\n", entries.Distinct());
    }

    public static string CombatTargetText(ActiveSkillData skill, string effectText)
    {
        if(skill.special_effects.Any(effect=>effect.GetValueOrDefault("effect","").AsString()=="forest_bei_pack_call"))
            return "全体存活的友方林地凶狼（含同物种变种）";
        if(skill.skill_name=="复命")return "从最近一次施展的非应对技能的合法目标中随机选择";
        if (CardRules.IsResponse(skill)) return "触发应对时，自身及匹配的来袭者；未触发应对时，" + (CardRules.NormalCast(skill) is { } normal
            ? CombatTargetText(normal, normal.description) : "无目标（空放）");
        if (skill.aoe_range > 0)
        {
            var isEnemyArea = skill.target_type == 2 || skill.target_type == 5 || skill.description.Contains("敌方") || skill.description.Contains("敌人");
            var faction = isEnemyArea ? "敌方" : skill.target_type == 3 ? "合法目标" : "目标";
            return $"指定1名{faction}；并作用于其周围 {skill.aoe_range} 格";
        }
        if (effectText.Contains("全体敌方") || effectText.Contains("所有敌方")) return "全体敌方";
        return skill.target_type switch
        {
            0 => "自身",
            1 => "自身",
            2 => "指定1名敌方",
            3 => "指定1名合法单位",
            _ => "按技能效果说明"
        };
    }

    public static string CombatUseLimitText(ActiveSkillData skill, GodotObject? owner, BattleManager? manager)
    {
        var limits = new List<string>();
        if (skill.uses_per_round > 0) limits.Add($"每轮最多 {skill.uses_per_round} 次");
        var perBattle = skill.uses_per_battle > 0 ? skill.uses_per_battle : skill.max_charges;
        if (perBattle > 0) limits.Add($"本场最多 {perBattle} 次");
        if (limits.Count == 0) return "不限";

        var current = GodotObject.IsInstanceValid(owner) && manager is not null
            ? manager.get_skill_uses_remaining(owner!, skill)
            : "";
        return string.IsNullOrWhiteSpace(current) ? string.Join("；", limits) : $"{string.Join("；", limits)}；当前剩余 {current}";
    }

    public static string CombatCooldownText(ActiveSkillData skill, GodotObject? owner, BattleManager? manager)
    {
        if (skill.cooldown_rounds <= 0) return "";
        var text = $"冷却：{skill.cooldown_rounds}回合（使用当回合不可再用，之后跳过{skill.cooldown_rounds}个完整回合）";
        if (GodotObject.IsInstanceValid(owner) && manager is not null)
            text += $"；{manager.get_skill_round_cooldown_text(owner!, skill)}";
        return text;
    }

    private static bool DealsDamage(ActiveSkillData skill) =>
        !Mathf.IsZeroApprox(skill.base_damage) || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula);

    private static string StandardDamageSentence(ActiveSkillData skill, string existingText, GodotObject? owner)
    {
        var type = DamageTypeText(skill.damage_type);
        string targetPrefix;
        if (CardRules.IsResponse(skill)) targetPrefix="对触发应对的来袭者";
        else if (existingText.Contains("全体敌方") || existingText.Contains("所有敌方")) targetPrefix="对全体敌方各";
        else if (skill.aoe_range > 0)
        {
            var isEnemyArea = skill.target_type == 2 || skill.target_type == 5 || skill.description.Contains("敌方") || skill.description.Contains("敌人");
            var faction = isEnemyArea ? "敌方单位" : "合法单位";
            var center = isEnemyArea ? "指定1名敌方" : "指定1名合法单位";
            targetPrefix=$"对{center}及其周围 {skill.aoe_range} 格内的{faction}各";
        }
        else targetPrefix=skill.target_type switch
        {
            0 => "对自身", 1 => "对自身", 2 => "对指定1名敌方", 3 => "对指定1名合法单位", _ => ""
        };

        var baseValue=skill.calculate_damage(owner);
        var previewValue=BattleActionResolver.preview_damage(owner,skill);
        var previewDamage=Mathf.Max(1,Mathf.RoundToInt(previewValue));
        var multiplier=Mathf.IsZeroApprox(baseValue)?1f:previewValue/baseValue;
        var formula=$"max(1, round(({FormulaWithValues(DamageFormula(skill),owner)})×{FormatNumber(multiplier)}))";
        var segmentFormula=FindAssignedFormulas(RawSkillText(skill)).FirstOrDefault(entry=>entry.label=="伤害段数");
        var segmentCount=1;
            if(!string.IsNullOrWhiteSpace(segmentFormula.formula)&&TryEvaluateFormula(segmentFormula.formula,owner,new System.Collections.Generic.Dictionary<string,float>(),out var parsedSegments))
            segmentCount=Mathf.Max(1,Mathf.RoundToInt(parsedSegments));

        var damageText=segmentCount>1
            ? $"{targetPrefix}当前预估造成 {segmentCount} 段{type}伤害，每段 {previewDamage} 点。"
            : $"{targetPrefix}当前预估造成 {previewDamage} 点{type}伤害。";
        damageText+=$"计算公式：{formula} = {previewDamage} 点。";
        if(segmentCount>1&&!string.IsNullOrWhiteSpace(segmentFormula.formula))
            damageText+=$"\n伤害段数：{segmentCount} 段。计算公式：{FormulaWithValues(segmentFormula.formula,owner)} = {segmentCount} 段。";
        damageText+=$"\n{DamagePreviewNote(skill)}";
        return damageText;
    }

    private static bool IsDamageResultClause(string text) =>
        Regex.IsMatch(text, @"造成.*(?:物理|法术|神魂)伤害|(?:基础伤害|每段伤害|该段伤害|额外伤害|伤害)\s*(?:公式|=|：|:|为)");

    private static string DamageTypeText(string damageType) => damageType switch
    {
        "magic" => "法术",
        "soul" or "spiritual" => "神魂",
        _ => "物理"
    };

    private static string DamagePreviewNote(ActiveSkillData skill) => skill.damage_type switch
    {
        "soul" or "spiritual" => "只计当前已生效的自身增伤，未计施放时触发的额外效果、神魂对抗倍率与护盾吸收；本类伤害不进行命中和会心判定。",
        "magic" => "只计当前已生效的自身增伤，未计施放时触发的额外效果、命中、会心和护盾吸收；命中、会心与护盾在伤害结算时处理。",
        _ => "只计当前已生效的自身增伤，未计施放时触发的额外效果、命中、会心、化解、物防、百分比物理减伤和护盾吸收；这些在命中时结算。"
    };

    private static string DamageFormula(ActiveSkillData skill) =>
        !string.IsNullOrWhiteSpace(skill.damage_formula)
            ? skill.damage_formula
            : skill.base_damage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + string.Concat(skill.damage_bonuses
                .Where(bonus => bonus is not null && !string.IsNullOrWhiteSpace(bonus.attribute) && !Mathf.IsZeroApprox(bonus.multiplier))
                .Select(bonus => $" + {CardRules.Attributes.FirstOrDefault(pair => pair.Value == bonus.attribute).Key ?? bonus.attribute}×{bonus.multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"));

    private static string RawSkillText(ActiveSkillData skill)
    {
        var lines = new List<string> { skill.description ?? "" };
        try
        {
            if (Reference.GetProperty("cards").TryGetProperty(ReferenceName(skill), out var card) && card.TryGetProperty("text", out var text))
                lines.Add(text.GetString() ?? "");
        }
        catch (System.Exception) { }
        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static List<(string label, string formula)> FindAssignedFormulas(string text)
    {
        var result = new List<(string label, string formula)>();
        var pattern = @"(?<label>伤害段数|基础伤害|每段伤害|该段伤害|额外伤害|追加伤害|化解层数|伤害)\s*(?:公式)?\s*(?:=|：|:)\s*";
        foreach (Match match in Regex.Matches(text, pattern))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOfAny(['。', '；', '\n', '\r'], start);
            if (end < 0) end = text.Length;
            var formula = text[start..end].Trim();
            if (formula.Length > 0 && !result.Any(item => item.label == match.Groups["label"].Value && item.formula == formula))
                result.Add((match.Groups["label"].Value, formula));
        }
        return result;
    }

    private static string FormulaWithValues(string formula, GodotObject? owner)
    {
        var display = DisplayFormula(formula).Replace("*", "×").Replace("/", "÷");
        if(!GodotObject.IsInstanceValid(owner))return display;
        foreach (var pair in CardRules.Attributes.OrderByDescending(pair => pair.Key.Length))
        {
            if (!display.Contains(pair.Key)) continue;
            var value = $"{pair.Key}[{FormatNumber(CardRules.Number(owner, pair.Value))}]";
            display = display.Replace(pair.Key, value);
        }
        return display;
    }

    private static string DisplayFormula(string formula)
    {
        if (!formula.Contains(':')) return Readable(formula);
        var terms = new List<string>();
        foreach (var piece in formula.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = piece.Split(':', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "base") terms.Add(pair[1]);
            else
            {
                var label = CardRules.Attributes.FirstOrDefault(attribute => attribute.Value == pair[0]).Key ?? pair[0];
                terms.Add($"{label}×{pair[1]}");
            }
        }
        return terms.Count == 0 ? Readable(formula) : string.Join(" + ", terms);
    }

    private static string NormalizeFormula(string formula) => Regex.Replace(Readable(formula), @"\s+", "");
    private static string FormatNumber(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // 游戏内不再用下划线标出百科术语；保留入口兼容既有调用，直接展示原文。
    public static string Underline(string text) => text;
    private static string[] Terms(string text)
    {
        var names=Mechanics.Keys.Concat(Reference.GetProperty("terms").EnumerateObject().Select(p=>p.Name))
            .Concat(EncyclopediaResourceTerms())
            .Concat(Reference.GetProperty("cards").EnumerateObject().Where(p=>p.Value.GetProperty("text").GetString()!.Contains("唯一Buff")).Select(p=>p.Name))
            .Concat(new[]{"物理伤害","法术伤害","神魂伤害"}).Distinct().Where(text.Contains).OrderByDescending(s=>s.Length).ThenBy(s=>s,StringComparer.Ordinal).ToArray();
        return names;
    }
    private static string[] EncyclopediaResourceTerms()
    {
        if(_encyclopediaResourceTerms is not null)return _encyclopediaResourceTerms;
        var terms=new HashSet<string>(StringComparer.Ordinal);
        foreach(var path in FindResources("res://resources/battle/statuses"))
            if(ResourceLoader.Load<StatusEffect>(path) is { } status&&!string.IsNullOrWhiteSpace(status.name))terms.Add(status.name);
        foreach(var path in FindResources("res://resources/activeskills"))
        {
            var source=Godot.FileAccess.GetFileAsString(path);
            if(!source.Contains("skill_type = 6",StringComparison.Ordinal)&&!source.Contains("slot_category = \"passive\"",StringComparison.Ordinal)&&!Regex.IsMatch(source,@"(?m)^keywords\s*=.*固有"))continue;
            if(ResourceLoader.Load<ActiveSkillData>(path) is { } skill&&!string.IsNullOrWhiteSpace(skill.skill_name))terms.Add(skill.skill_name);
        }
        _encyclopediaResourceTerms=terms.ToArray();
        return _encyclopediaResourceTerms;
    }
    private static IEnumerable<string> FindResources(string root)
    {
        var files=new List<string>();
        CollectResources(root,files);
        return files;
    }
    private static void CollectResources(string directory,List<string> files)
    {
        var access=DirAccess.Open(directory);
        if(access is null)return;
        foreach(var file in access.GetFiles())
            if(file.EndsWith(".tres",StringComparison.OrdinalIgnoreCase))files.Add($"{directory}/{file}");
        foreach(var child in access.GetDirectories())CollectResources($"{directory}/{child}",files);
    }
    private static string TermPattern(string text) => string.Join("|",Terms(text).Select(Regex.Escape).DefaultIfEmpty("(?!)"));
    public static string Details(ActiveSkillData skill, GodotObject? owner)
    {
        var face=Face(skill,owner);var entries=new List<string>();
        var terms=Regex.Matches(face,TermPattern(face)).Select(m=>m.Value)
            .Concat(skill.get_presentation_tags()).Distinct();
        foreach(var term in terms)
        {
            var explanation=Mechanics.GetValueOrDefault(term,"");
            if (term == "应对" && UseBoardPresentation(skill, owner))
                explanation = "扫到后变暗并待命，持续至共同回合结束。真实来袭时才检查费用与条件；同次攻击只尝试盘序最前的一份合格应对，触发失败不继续找其他份。实际成功才扣费及次数。";
            if(string.IsNullOrEmpty(explanation) && Reference.GetProperty("terms").TryGetProperty(term,out var entry)) explanation=entry.GetString()??"";
            if(string.IsNullOrEmpty(explanation) && Reference.GetProperty("cards").TryGetProperty(term,out var power))explanation=power.GetProperty("text").GetString()??"";
            if(term.EndsWith("伤害"))
            {
                var formula=string.IsNullOrWhiteSpace(skill.damage_formula)
                    ? skill.base_damage.ToString("0.##")+string.Concat(skill.damage_bonuses.Select(b=>$" + {CardRules.Attributes.FirstOrDefault(p=>p.Value==b.attribute).Key??b.attribute}×{b.multiplier:0.##}"))
                    : skill.damage_formula;
                var baseDamage=Mathf.RoundToInt(skill.calculate_damage(owner));
                var finalDamage=Mathf.Max(1,Mathf.RoundToInt(BattleActionResolver.preview_damage(owner,skill)));
                var multiplier=Mathf.IsZeroApprox(skill.calculate_damage(owner))?1f:BattleActionResolver.preview_damage(owner,skill)/skill.calculate_damage(owner);
                explanation=$"基础伤害：{baseDamage} 点。计算公式：{FormulaWithValues(formula,owner)} = {baseDamage} 点。\n计入当前自身增伤后预计 {finalDamage} 点。计算公式：max(1, round(({FormulaWithValues(formula,owner)})×{FormatNumber(multiplier)})) = {finalDamage} 点；命中、会心与目标防御在攻击时结算。";
            }
            if(term=="化解" && !string.IsNullOrWhiteSpace(skill.stacks_formula))
                explanation+=$"\n本次化解层数：{CardRules.Block(skill,owner)} 层。计算公式：{FormulaWithValues(skill.stacks_formula,owner)} = {CardRules.Block(skill,owner)} 层。";
            if(term=="乘势")
            {
                var momentum=Mathf.RoundToInt(CardRules.Evaluate(skill.momentum_effect_params,owner)*100);
                explanation+=$"\n本次技能会心率加成：{momentum}%。计算公式：{FormulaWithValues(skill.momentum_effect_params,owner)}×100% = {momentum}%。";
            }
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
