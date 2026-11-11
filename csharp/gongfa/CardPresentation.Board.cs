using Godot;
using System.Text.RegularExpressions;

public static partial class CardPresentation
{
    private static bool UseBoardPresentation(ActiveSkillData skill, GodotObject? owner) => skill.has_board_definition()
        && (owner?.HasMeta("artifact_battle_clone") != true || owner?.HasMeta("combat_board_mode") == true);

    public static string BoardFace(ActiveSkillData skill, GodotObject? owner)
    {
        var original = skill.get_board_skill_key();
        var basis = ResourceLoader.Exists(original) ? ResourceLoader.Load<ActiveSkillData>(original) : skill;
        var text = basis?.description ?? skill.description;
        foreach (var addition in GongfaSkillRewriteRuntime.GetAdditionalEffectDescriptions(skill, excludePrimary: false))
            text += "\n" + addition.Replace("由技能槽10自动施展时", "由周天额外施放时");
        text = text.Replace("原第十槽", "周天指示").Replace("不经行动队列施展时", "由周天额外施放时");
        text = ResolveFormulas(text, owner, skill);
        return AppendStructuredFormulaDetails(text, skill, owner);
    }

    /// <summary>One detailed source for the board hover; the shared reader formats it exactly once.</summary>
    public static string BoardHoverEffect(ActiveSkillData skill, GodotObject? owner)
    {
        var original = skill.get_board_skill_key();
        var basis = ResourceLoader.Exists(original) ? ResourceLoader.Load<ActiveSkillData>(original) : skill;
        var blocks = new List<string> { basis?.description ?? skill.description };
        blocks.AddRange(GongfaSkillRewriteRuntime.GetAdditionalEffectDescriptions(skill, excludePrimary: false));
        var output = new List<string>();
        var known = new Dictionary<string, float>(StringComparer.Ordinal);
        var variable = @"(?<![A-Za-z0-9_])(?<key>[A-Z])(?![A-Za-z0-9_])";
        var assignment = variable + @"\s*[=＝]\s*(?<formula>[^。；\r\n]+)[。；]?";
        var primary = true;
        foreach (var block in blocks.Where(block => !string.IsNullOrWhiteSpace(block)))
        {
            var definitions = new Dictionary<string, (string Value, string Formula)>(StringComparer.Ordinal);
            var formulas = Regex.Matches(block, assignment).Cast<Match>().ToArray();
            var probabilities = formulas.Where(match => match.Groups["formula"].Value.Contains('%')
                || match.Groups["formula"].Value.Contains("个百分点")).Select(match => match.Groups["key"].Value).ToHashSet();
            var values = new Dictionary<string, float>(known, StringComparer.Ordinal);
            foreach (var match in formulas)
            {
                var key = match.Groups["key"].Value;
                var formula = match.Groups["formula"].Value.Trim();
                if (!TryEvaluateFormula(formula, owner, values, out var number))
                {
                    definitions[key] = ("待结算值", FormulaWithValues(formula, owner));
                    continue;
                }
                values[key] = number;
                var displayFormula = FormulaWithValues(formula, owner);
                foreach (var pair in values.Where(pair => pair.Key != key))
                {
                    var value = probabilities.Contains(pair.Key) ? FormatNumber(pair.Value * 100f) + "%" : FormatNumber(pair.Value);
                    var label = skill.skill_name == "云岚三叠" ? pair.Key switch
                    { "C" => "首次追加概率", "D" => "第二次追加概率", "E" => "第三次追加概率", _ => pair.Key } : pair.Key;
                    displayFormula = Regex.Replace(displayFormula, $@"(?<![A-Za-z0-9_]){Regex.Escape(pair.Key)}(?![A-Za-z0-9_])",
                        label + "[" + value + "]");
                }
                if (primary && DealsDamage(skill) && (key == "A" || key == "B" && skill.skill_name == "云岚三叠"))
                {
                    var basisDamage = skill.calculate_damage(owner);
                    var preview = BattleActionResolver.preview_damage(owner, skill);
                    var multiplier = Mathf.IsZeroApprox(basisDamage) ? 1f : preview / basisDamage;
                    if (key == "A")
                    {
                        number = preview;
                        displayFormula = FormulaWithValues(DamageFormula(skill), owner);
                    }
                    else number *= multiplier;
                    if (!Mathf.IsEqualApprox(multiplier, 1f)) displayFormula = $"({displayFormula})×{FormatNumber(multiplier)}";
                    number = Mathf.Max(1, Mathf.RoundToInt(number));
                }
                var result = probabilities.Contains(key) ? FormatNumber(number * 100f) + "%" : FormatNumber(number);
                definitions[key] = (result, displayFormula);
            }
            var body = Regex.Replace(block, assignment, "")
                .Replace("合法目标", "目标").Replace("合法敌人", "敌人").Replace("合法单位", "单位")
                .Replace("由技能槽10自动施展时", "由周天额外施放时")
                .Replace("原第十槽", "周天指示").Replace("不经行动队列施展时", "由周天额外施放时");
            if (DealsDamage(skill))
            {
                body = body.Replace("对一名随机敌人造成", "造成");
            }
            foreach (var part in Regex.Split(body, @"(?<=[。；])|\r?\n"))
            {
                var sentence = part.Trim().TrimEnd('。', '；');
                if (sentence.Length == 0) continue;
                var keys = Regex.Matches(sentence, variable).Cast<Match>().Select(match => match.Groups["key"].Value)
                    .Where(definitions.ContainsKey).Distinct().ToArray();
                sentence = Regex.Replace(sentence, variable, match => definitions.TryGetValue(match.Groups["key"].Value, out var definition)
                    ? definition.Value : match.Value);
                sentence += "。";
                foreach (var key in keys)
                {
                    var label = probabilities.Contains(key) ? key switch
                    { "C" => "首次追加概率", "D" => "第二次追加概率", "E" => "第三次追加概率", _ => "触发概率" }
                        : primary && skill.skill_name == "云岚三叠" ? key == "A" ? "首段伤害" : "每段追加伤害"
                        : sentence.Contains("化解") ? "化解" : sentence.Contains("护盾") ? "护盾"
                        : sentence.Contains("伤害") ? "伤害" : "效果数值";
                    sentence += "计算公式：" + label + "：" + definitions[key].Value + " = " + definitions[key].Formula + "。";
                }
                output.Add(sentence);
            }
            known = values;
            primary = false;
        }
        return string.Join("\n", output);
    }
}
