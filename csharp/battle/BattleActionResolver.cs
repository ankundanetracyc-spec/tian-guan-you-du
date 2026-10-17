using Godot;

/// <summary>
/// SkillExecutor 的无场景结算核心。它不持有节点或 UI 状态，因此可由新的
/// BattleManager 直接使用，也能在迁移期间独立验证命中与伤害规则。
/// </summary>
public static class BattleActionResolver
{
    public readonly record struct HitResult(bool Evaded, bool Critical, int DamageBeforeShield, int BlockedDamage);

    public static HitResult resolve_hit(GodotObject attacker, GodotObject defender, ActiveSkillData skill, int block_stacks = 0, float extra_evasion = 0)
    {
        var damageType = skill.damage_type;
        // 神魂伤害直指神魂：不进入闪避与会心判定，仅由双方神魂差参与伤害结算。
        var isSoulDamage = damageType is "soul" or "spiritual";
        if (!isSoulDamage && roll_evasion(attacker, defender, extra_evasion)) return new(true, false, 0, 0);
        var extraCrit=Engine.GetMainLoop() is SceneTree critTree && critTree.GetFirstNodeInGroup("battle_mgr") is BattleManager critManager?critManager.gongfa_combat.SettlingMomentum(attacker,skill):0;
        var critical = !isSoulDamage && roll_crit(attacker, defender,extraCrit);
        var damage = preview_damage(attacker,skill);
        if (critical) damage *= calc_crit_multiplier(attacker, defender);
        if (isSoulDamage)
            damage *= calc_soul_confrontation_multiplier(attacker, defender, skill);

        var finalDamage = Mathf.Max(Mathf.RoundToInt(damage), 1);
        var blocked = damageType == "physical" ? Mathf.Min(Mathf.Max(block_stacks, 0), finalDamage) : 0;
        finalDamage = apply_defense(finalDamage - blocked, damageType, defender, skill.physical_defense_ignore_ratio);
        return new(false, critical, finalDamage, blocked);
    }

    public static float preview_damage(GodotObject? attacker,ActiveSkillData skill)
    {
        var damage=skill.calculate_damage(attacker);
        if(!GodotObject.IsInstanceValid(attacker))return damage;
        var damageType=skill.damage_type;
        if(Engine.GetMainLoop() is SceneTree tree && tree.GetFirstNodeInGroup("battle_mgr") is BattleManager manager)
            damage*=manager.gongfa_combat.DamageMultiplier(attacker,skill);
        if (damageType == "physical" && attacker.HasMethod("get_blood_furnace_physical_damage_multiplier"))
            damage *= 1.0f + attacker.Call("get_blood_furnace_physical_damage_multiplier").AsSingle();
        if (damageType == "physical" && attacker.HasMethod("get_gongfa_physical_damage_multiplier"))
            damage *= 1.0f + attacker.Call("get_gongfa_physical_damage_multiplier").AsSingle();
        if (damageType == "physical" && attacker is Creature creature)
            damage *= 1.0f + creature.get_combat_tempering_physical_damage_multiplier();
        if (damageType == "physical" && attacker.HasMethod("get_species_physical_damage_multiplier"))
            damage *= 1.0f + attacker.Call("get_species_physical_damage_multiplier").AsSingle();
        if(skill.keywords.Contains("重击")&&CardRules.Number(attacker,"punch_frame")>0)damage*=1.5f;
        return damage;
    }

    public static bool roll_evasion(GodotObject attacker, GodotObject defender, float extra_evasion = 0)
    {
        return GD.Randf() >= preview_hit_chance(attacker,defender,extra_evasion);
    }

    public static bool roll_crit(GodotObject attacker, GodotObject defender,float extraChance=0)
    {
        return GD.Randf() < preview_crit_chance(attacker,defender,extraChance);
    }

    public static float preview_hit_chance(GodotObject attacker,GodotObject defender,float extraEvasion=0)
    {
        if (defender is ArtifactBody) return 1f;
        var precision=Mathf.Max(GetNumber(attacker,"precision"),.01f);var dodge=Mathf.Max(GetNumber(defender,"dodge"),0);
        if(defender is Creature creature)
            dodge=Mathf.Floor(dodge*(1f+Mathf.Min(creature.get_status_stacks("身法"),20)*.01f));
        if(Engine.GetMainLoop() is SceneTree tree&&tree.GetFirstNodeInGroup("battle_mgr") is BattleManager manager)
            dodge*=BattleWoundRules.get_dodge_multiplier(manager.get_realtime_wound_counts(defender));
        return Mathf.Clamp(precision*3f/(precision*3f+dodge)-Mathf.Max(extraEvasion,0),.01f,1);
    }
    public static float preview_crit_chance(GodotObject attacker,GodotObject defender,float extraChance=0)
    {
        if (defender is ArtifactBody) return 0f;
        var critical=Mathf.Max(GetNumber(attacker,"critical"),.01f);var resist=Mathf.Max(GetNumber(defender,"crit_resist"),0);
        if(Engine.GetMainLoop() is SceneTree tree&&tree.GetFirstNodeInGroup("battle_mgr") is BattleManager manager)
            resist*=BattleWoundRules.get_crit_resist_multiplier(manager.get_realtime_wound_counts(defender));
        return Mathf.Clamp(critical/(critical+resist*1.5f)+extraChance,.01f,1);
    }

    public static float calc_crit_multiplier(GodotObject attacker, GodotObject defender) =>
        Mathf.Max(GetNumber(attacker, "crit_damage_mult"), 1.5f) + GetNumber(attacker, "soul") / Mathf.Max(GetNumber(defender, "physique") + GetNumber(defender, "soul"), 1) * 2f;

    public static int apply_defense(int damage, string damage_type, GodotObject defender, float ignoreRatio = 0)
    {
        if (damage_type is "soul" or "spiritual") return Mathf.Max(damage, 1);
        if (damage_type != "physical") return Mathf.Max(damage, 1);
        // 化解可以将一次物理伤害完全抵消；只有仍有正伤害时才继续计算物防与百分比减免。
        if (damage <= 0) return 0;
        var afterDefense = Mathf.Max(damage - Mathf.RoundToInt(GetNumber(defender, "physical_defense") * (1f - Mathf.Clamp(ignoreRatio, 0, 1))), Mathf.CeilToInt(damage * .01f));
        var baseReduction = Mathf.Clamp(GetNumber(defender, "physical_damage_reduction"), 0f, .90f);
        var woundReduction = 0f;
        if (defender is Creature creature && creature.get_status_stacks("心法:内景损益") > 0)
        {
            var wounds = creature.get_status_stacks("内伤") + creature.get_status_stacks("外伤");
            var perWoundReduction = Mathf.Max(0f, creature.get_combat_tempering_total("wound_physical_reduction_pp_per_stack"));
            woundReduction = Mathf.Clamp(wounds * perWoundReduction / 100f, 0f, .90f);
        }
        // 独立百分比减伤层按剩余伤害乘算；百分比减伤合计封顶90%，保留至少10%的结算伤害。
        var combinedReduction = Mathf.Min(1f - (1f - baseReduction) * (1f - woundReduction), .90f);
        return Mathf.Max(Mathf.RoundToInt(afterDefense * (1f - combinedReduction)), 1);
    }

    public static float calc_soul_confrontation_multiplier(GodotObject attacker, GodotObject defender, ActiveSkillData skill)
    {
        var attackSoul = Mathf.Max(GetNumber(attacker, "soul"), 0);
        var defendSoul = Mathf.Max(GetNumber(defender, "soul"), 0);
        var multiplier = 1f + (attackSoul - defendSoul) / Mathf.Max(attackSoul + defendSoul + Mathf.Max(skill.soul_confrontation_buffer, 0), .001f) * skill.soul_confrontation_weight;
        return Mathf.Clamp(multiplier, skill.soul_confrontation_min, skill.soul_confrontation_max);
    }

    public static float evaluate_formula(string formula, GodotObject caster, GodotObject target, float fallback = 1)
    {
        if (string.IsNullOrWhiteSpace(formula) || formula == "1.0") return 1;
        var expression = formula;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(formula, "(caster|target)\\.([a-zA-Z_]+)"))
        {
            var owner = match.Groups[1].Value == "caster" ? caster : target;
            expression = expression.Replace(match.Value, GetNumber(owner, match.Groups[2].Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        var evaluator = new Expression();
        if (evaluator.Parse(expression) != Error.Ok) return fallback;
        var result = evaluator.Execute();
        return result.VariantType is Variant.Type.Int or Variant.Type.Float ? Mathf.Clamp(result.AsSingle(), .01f, .99f) : fallback;
    }

    private static float GetNumber(GodotObject unit, StringName property) => CardRules.Number(unit,property.ToString());
}
