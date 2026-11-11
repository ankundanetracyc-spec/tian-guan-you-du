using Godot;
using System;
using System.Globalization;

/// <summary>Pure display arithmetic over recorded coefficients and an already calculated character.</summary>
public static class SwordControlPreview
{
    public sealed record Context(double Soul, double Sword, int SpiritCapacity, int UsedSpirit);
    public sealed record Evaluation(int SpiritRequired, int SpiritAvailable,
        decimal RawPhysicalDamage, decimal StabilityMaximum);

    public static Context? FromCharacter(Character? actor)
    {
        if (actor is null || !GodotObject.IsInstanceValid(actor) || actor.entity_data is not HumanData)
            return null;
        var context = new Context(CardRules.Number(actor, "soul"), CardRules.Number(actor, "sword_proficiency"),
            actor.max_spirit_perception, actor.get_used_artifact_spirit());
        return IsContextValid(context) ? context : null;
    }

    public static Evaluation? Evaluate(SwordControlPreviewData? data, Context? context)
    {
        if (data is null || !data.IsValid || context is null || !IsContextValid(context)) return null;
        try
        {
            var soul = (decimal)context.Soul;
            var sword = (decimal)context.Sword;
            var damage = (decimal)data.attack_base + soul * (decimal)data.attack_soul
                + sword * (decimal)data.attack_sword;
            var stability = (decimal)data.stable_base + soul * (decimal)data.stable_soul
                + sword * (decimal)data.stable_sword + data.spirit_required * (decimal)data.stable_occupation;
            // Current archived v2: damage uses positive half-up; stability uses floor.
            return new Evaluation(data.spirit_required, context.SpiritCapacity - context.UsedSpirit,
                decimal.Round(damage, 0, MidpointRounding.AwayFromZero), decimal.Floor(stability));
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>Detailed source text; PlayerInfoText derives both display modes from these same lines.</summary>
    public static string Describe(SwordControlPreviewData? data, Context? context)
    {
        if (data is null || !data.IsValid) return "御使参数：未记录";
        if (context is null)
        {
            return $"灵识占用：{data.spirit_required}\n"
                + $"预估物理伤害：待选择角色。计算公式：{N(data.attack_base)}＋神魂×{N(data.attack_soul)}＋剑道造诣×{N(data.attack_sword)}。\n"
                + $"稳定：待选择角色。计算公式：{N(data.stable_base)}＋神魂×{N(data.stable_soul)}＋剑道造诣×{N(data.stable_sword)}＋本器占用[{data.spirit_required}]×{N(data.stable_occupation)}。";
        }

        var result = Evaluate(data, context);
        if (result is null) return "御使参数：未记录";
        return $"灵识占用：{result.SpiritRequired}\n"
            + $"预估物理伤害{N(result.RawPhysicalDamage)}。计算公式：{N(data.attack_base)}＋神魂[{N(context.Soul)}]×{N(data.attack_soul)}＋剑道造诣[{N(context.Sword)}]×{N(data.attack_sword)}≈{N(result.RawPhysicalDamage)}。\n"
            + $"稳定{N(result.StabilityMaximum)}。计算公式：{N(data.stable_base)}＋神魂[{N(context.Soul)}]×{N(data.stable_soul)}＋剑道造诣[{N(context.Sword)}]×{N(data.stable_sword)}＋本器占用[{result.SpiritRequired}]×{N(data.stable_occupation)}≈{N(result.StabilityMaximum)}。";
    }

    /// <summary>Apply the existing current-self damage preview after the item's rounded base output.</summary>
    public static string DescribeForCharacter(SwordControlPreviewData? data, Character? actor)
    {
        var context = FromCharacter(actor);
        var original = Describe(data, context);
        var result = Evaluate(data, context);
        if (data is null || context is null || actor is null || result is null) return original;

        using var skill = new ActiveSkillData
        {
            skill_name = data.skill_name ?? "",
            base_damage = (float)result.RawPhysicalDamage,
            damage_type = "physical",
            tags = ["剑"],
        };
        var currentDamage = BattleActionResolver.preview_damage(actor, skill);
        if (!float.IsFinite(currentDamage) || currentDamage < 0 || currentDamage >= int.MaxValue)
            return "御使参数：未记录";
        var estimated = Mathf.Max(1, Mathf.RoundToInt(currentDamage));
        var baseDamage = skill.calculate_damage(actor);
        var multiplier = Mathf.IsZeroApprox(baseDamage) ? 1f : currentDamage / baseDamage;
        var itemExpression = $"{N(data.attack_base)}＋神魂[{N(context.Soul)}]×{N(data.attack_soul)}＋剑道造诣[{N(context.Sword)}]×{N(data.attack_sword)}";
        string expression;
        if (result.RawPhysicalDamage == 0)
            expression = $"取较大值（1，{itemExpression}）≈{estimated}";
        else if (Mathf.IsEqualApprox(multiplier, 1f) && estimated == result.RawPhysicalDamage)
            expression = $"{itemExpression}≈{estimated}";
        else
            expression = $"({itemExpression}≈基础物伤{N(result.RawPhysicalDamage)})×自身当前倍率[{multiplier.ToString("0.##", CultureInfo.InvariantCulture)}]≈{estimated}";
        var lines = original.Split('\n');
        lines[1] = $"预估物理伤害{estimated}。计算公式：{expression}。";
        return string.Join("\n", lines);
    }

    /// <summary>Describe exactly the one recorded skill, including unknown costs or target.</summary>
    public static string DescribeSkillForCharacter(SwordControlPreviewData? data, Character? actor)
    {
        if (data is null) return DescribeForCharacter(null, actor);
        var name = string.IsNullOrWhiteSpace(data.skill_name) ? "技能：未定" : data.skill_name.Trim();
        var energy = data.energy_cost >= 0 ? data.energy_cost.ToString(CultureInfo.InvariantCulture) : "未定";
        var mana = data.mana_cost >= 0 ? data.mana_cost.ToString(CultureInfo.InvariantCulture) : "未定";
        var target = string.IsNullOrWhiteSpace(data.target_text) ? "未定" : data.target_text.Trim();
        var timing = string.IsNullOrWhiteSpace(data.timing_text) ? "" : "时机：" + data.timing_text.Trim() + "\n";
        return $"{name}\n费用：体力{energy}、法力{mana}\n目标：{target}\n"
            + timing + DescribeForCharacter(data, actor);
    }

    private static bool IsContextValid(Context context) =>
        double.IsFinite(context.Soul) && context.Soul >= 0 && context.Soul <= (double)decimal.MaxValue
        && double.IsFinite(context.Sword) && context.Sword >= 0 && context.Sword <= (double)decimal.MaxValue
        && context.SpiritCapacity >= 0 && context.UsedSpirit >= 0;

    private static string N(double value) => N((decimal)value);
    // Display precision only; Evaluate continues to use every recorded coefficient digit.
    private static string N(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
