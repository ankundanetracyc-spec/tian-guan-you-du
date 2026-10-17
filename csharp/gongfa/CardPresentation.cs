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
        ["应对"]="当前槽遇到匹配来袭时立即触发并支付应对费用；未触发按独立普通效果及费用结算，未配置则免费空放。应对与来袭算同一结算组。",
        ["消耗"]="本即时战斗中，该技能本场最多使用一次。",
        ["固有"]="当前主修或辅修秘籍自动提供心法被动；开战时显示于Buff栏，持续至本场战斗结束。",
        ["保留"]="仅用于凝核牌局；即时队列战斗没有手牌清理阶段。",
        ["虚无"]="仅用于凝核牌局；即时队列战斗没有弃牌堆。",
        ["弃顶"]="弃置时置于弃牌堆顶。",
        ["乘势"]="满足该技能指定的上一项主动行动条件后，触发乘势效果。",
        ["重击"]="拥有拳架时，消耗一层拳架，使本次技能伤害提高50%。",
        ["化解"]="每层抵消本次攻击1点物理伤害；该次攻击结算后全部消散。",
        ["气血"]="层数降低时，按当前气血回落回复规则结算；回复率及替代效果由公共规则和当前生效效果共同决定。",
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
                text=$"应对触发：位于当前槽且遇到敌方物理攻击时立即触发，获得 {CardRules.Block(skill,owner)} 层化解；该次攻击结算后消散。";
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
                ? $"\n未触发应对：{CombatEffectText(normal, owner)}"
                : "\n未触发应对：空放，仅消耗本槽行动时间，不消耗体力、法力或使用次数，不产生效果。";
        }
        return text;
    }
    private static string ReferenceName(ActiveSkillData skill) => (skill.source_gongfa_name, skill.skill_name) switch
    {
        ("黄庭养形篇", "返照") => "返照（黄庭养形化法）",
        ("黄庭养形篇", "平息") => "平息（黄庭养形化法）",
        ("黄庭养形篇", "抱一") => "抱一（黄庭养形化法）",
        ("黄庭养形篇", "周行") => "周行（黄庭养形化法）",
        ("黄庭养形篇", "催血") => "催血（黄庭养形化法）",
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
        // 技能词典中的公式只在详情中展示，技能说明替换为最终整数。
        text=Regex.Replace(text,@"伤害段数\s*=\s*(?<formula>[^。；\r\n]+)",m=>
            $"伤害段数为 {Mathf.Max(1,Mathf.RoundToInt(CardRules.Evaluate(m.Groups["formula"].Value,owner)))}");
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

    /// <summary>即时战斗技能栏使用的简明效果文案：伤害目标与类型由运行时配置统一生成。</summary>
    public static string CombatEffectText(ActiveSkillData skill, GodotObject? owner)
    {
        var text = Face(skill, owner);
        text = Regex.Replace(text, @"(?m)^使用次数：.*(?:\r?\n|$)", "").Trim();
        if (!DealsDamage(skill)) return text;

        // 避免旧牌面中目标、伤害类型和公式各写一遍。保留伤害增幅、状态和触发条件，
        // 再由技能数据生成唯一的目标与伤害句；具体公式见数值详情。
        var retained = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var clauses = Regex.Split(line, @"(?<=[。；])");
            foreach (var clause in clauses)
            {
                if (string.IsNullOrWhiteSpace(clause) || IsDamageResultClause(clause)) continue;
                retained.Add(clause.Trim());
            }
        }

        var summary = StandardDamageSentence(skill, text);
        var extra = string.Join("", retained).Trim(' ', '。', '；', '\r', '\n');
        return string.IsNullOrWhiteSpace(extra) ? summary : $"{summary}\n{extra}";
    }

    /// <summary>返回悬浮信息栏中的精确公式、当前属性代入值及当前自身增伤预估。</summary>
    public static string CombatCalculationDetails(ActiveSkillData skill, GodotObject? owner)
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
                entries.Add($"伤害段数：{segmentCount}（{FormulaWithValues(segments.formula, owner)}）");
            }

            var perSegmentLabel = segmentCount > 1 ? "每段基础" : "基础";
            entries.Add($"{perSegmentLabel}{damageName}伤害：{Mathf.RoundToInt(skill.calculate_damage(owner))} 点（{FormulaWithValues(formula, owner)}）");
            var estimatedLabel = segmentCount > 1 ? "每段当前自身增伤后预计伤害" : "当前自身增伤后预计伤害";
            entries.Add($"{estimatedLabel}：{Mathf.RoundToInt(BattleActionResolver.preview_damage(owner, skill))} 点。{DamagePreviewNote(skill)}");

            foreach (var entry in FindAssignedFormulas(RawSkillText(skill)))
            {
                if (entry.label is "伤害段数" or "伤害" || string.IsNullOrWhiteSpace(entry.formula)) continue;
                if (NormalizeFormula(entry.formula) == NormalizeFormula(formula)) continue;
                if (!entry.label.Contains("伤害")) continue;
                var baseValue = CardRules.Evaluate(entry.formula, owner);
                var primaryValue = skill.calculate_damage(owner);
                var multiplier = Mathf.IsZeroApprox(primaryValue) ? 1f : BattleActionResolver.preview_damage(owner, skill) / primaryValue;
                var value = Mathf.RoundToInt(baseValue);
                entries.Add($"{entry.label}：{value} 点{damageName}伤害（{FormulaWithValues(entry.formula, owner)}）；当前自身增伤后预计 {Mathf.RoundToInt(baseValue * multiplier)} 点。");
            }
        }

        if (!string.IsNullOrWhiteSpace(skill.stacks_formula))
            entries.Add($"状态层数：{CardRules.Block(skill, owner)} 层（{FormulaWithValues(skill.stacks_formula, owner)}）");

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
            entries.Add($"{label}：{CardRules.Evaluate(formula, owner):0.##} 点（{FormulaWithValues(formula, owner)}）");
        }

        foreach (Match match in Regex.Matches(RawSkillText(skill), @"最大生命\s*[×x*]\s*(?<rate>\d+(?:\.\d+)?)%"))
        {
            var rate = float.Parse(match.Groups["rate"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var value = Mathf.RoundToInt(CardRules.Number(owner, "max_health") * rate / 100f);
            var line = $"生命回复：{value} 点（最大生命[{FormatNumber(CardRules.Number(owner, "max_health"))}]×{FormatNumber(rate)}%）";
            if (!entries.Contains(line)) entries.Add(line);
        }

        if (CardRules.IsResponse(skill) && CardRules.NormalCast(skill) is { } normal)
        {
            var normalDetails = CombatCalculationDetails(normal, owner);
            if (!string.IsNullOrWhiteSpace(normalDetails)) entries.Add($"未应对普通分支：\n{normalDetails}");
        }
        return string.Join("\n", entries.Distinct());
    }

    public static string CombatTargetText(ActiveSkillData skill, string effectText)
    {
        if (CardRules.IsResponse(skill)) return "应对：自身及匹配的来袭者；未应对：" + (CardRules.NormalCast(skill) is { } normal
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
        var text = $"回合冷却：{skill.cooldown_rounds}回合（使用当回合不可再用，之后跳过{skill.cooldown_rounds}个完整回合）";
        if (GodotObject.IsInstanceValid(owner) && manager is not null)
            text += $"；{manager.get_skill_round_cooldown_text(owner!, skill)}";
        return text;
    }

    private static bool DealsDamage(ActiveSkillData skill) =>
        !Mathf.IsZeroApprox(skill.base_damage) || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula);

    private static string StandardDamageSentence(ActiveSkillData skill, string existingText)
    {
        var type = DamageTypeText(skill.damage_type);
        var damageVerb = $"造成{type}伤害。";
        if (CardRules.IsResponse(skill)) return $"对触发应对的来袭者{damageVerb}";
        if (existingText.Contains("全体敌方") || existingText.Contains("所有敌方")) return $"对全体敌方各{damageVerb}";
        if (skill.aoe_range > 0)
        {
            var isEnemyArea = skill.target_type == 2 || skill.target_type == 5 || skill.description.Contains("敌方") || skill.description.Contains("敌人");
            var faction = isEnemyArea ? "敌方单位" : "合法单位";
            var center = isEnemyArea ? "指定1名敌方" : "指定1名合法单位";
            return $"对{center}及其周围 {skill.aoe_range} 格内的{faction}各{damageVerb}";
        }
        return skill.target_type switch
        {
            0 => $"对自身{damageVerb}",
            1 => $"对自身{damageVerb}",
            2 => $"对指定1名敌方{damageVerb}",
            3 => $"对指定1名合法单位{damageVerb}",
            _ => $"造成{type}伤害。"
        };
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
