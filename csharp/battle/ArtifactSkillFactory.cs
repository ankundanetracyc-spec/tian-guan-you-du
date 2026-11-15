using Godot;

public static class ArtifactSkillFactory
{
    public static string Key(ActiveSkillData skill) => string.IsNullOrEmpty(skill.combat_source_key) ? skill.ResourcePath : skill.combat_source_key;
    public static bool IsConfigured(ArtifactInstanceData item)
    {
        if (ForgedArtifactInventory.IsForged(item)) return ForgedSwordBattleAdapter.IsSword(item);
        var p = item.combat_profile;
        if (p is null || item.form is null || p.physical_defense_ignore_ratio < 0 || p.physical_defense_ignore_ratio > 1
            || p.inner_block_bonus < 0 || p.uses_per_round < 0 || p.uses_per_battle < 0 || p.cooldown_rounds < 0) return false;
        var attack = item.utility_id is "破" or "破障" && item.form.form_id == "mingfeng";
        var guard = item.utility_id is "固" or "固结" && item.form.form_id == "shoufeng";
        if (!attack && !guard) return false;
        if (item.ritual_mode == "内炼") return attack ? p.physical_defense_ignore_ratio > 0 : p.inner_block_bonus > 0;
        if (item.ritual_mode != "外祭" || item.outer_body <= 0 || item.spirit_cost < 0
            || item.deploy_energy_cost is < 0 or > 3 || item.deploy_mana_cost < 0
            || p.use_energy_cost is < 0 or > 3 || p.use_mana_cost < 0) return false;
        if (p.use_energy_cost == 0 && p.use_mana_cost == 0 && p.uses_per_round == 0 && p.uses_per_battle == 0 && p.cooldown_rounds == 0) return false;
        return attack ? p.use_base_damage > 0 : p.use_shield > 0;
    }

    public static ActiveSkillData CreateOuter(ArtifactInstanceData item)
    {
        var p = item.combat_profile!;
        var attack = item.utility_id is "破" or "破障";
        var skill = new ActiveSkillData
        {
            skill_name = "御·" + item.artifact_name, icon_path = item.form?.icon_path ?? "",
            combat_source_key = "artifact:" + item.instance_id, artifact_instance_id = item.instance_id,
            energy_cost = p.use_energy_cost, mana_cost = p.use_mana_cost,
            skill_type = attack ? ActiveSkillData.SkillType.ATTACK : ActiveSkillData.SkillType.BUFF,
            target_type = attack ? 2 : 0, base_damage = attack ? p.use_base_damage : 0,
            damage_type = "physical", physical_defense_ignore_ratio = p.physical_defense_ignore_ratio,
            uses_per_round = p.uses_per_round, uses_per_battle = p.uses_per_battle, cooldown_rounds = p.cooldown_rounds,
            description = $"召唤：体力{item.deploy_energy_cost}、法力{item.deploy_mana_cost}，器体{item.outer_body}，在场占灵识{item.spirit_cost}。\n御使：体力{p.use_energy_cost}、法力{p.use_mana_cost}。" +
                (attack ? $"对单个敌方造成基础物理伤害{p.use_base_damage}，忽略{p.physical_defense_ignore_ratio:P0}物防。" : $"自身护盾增加{p.use_shield}。"),
            tags = attack ? ["增伤"] : ["防御"]
        };
        if (!attack) skill.special_effects.Add(new Godot.Collections.Dictionary { ["effect"] = "shield", ["formula"] = p.use_shield.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        skill.artifact_summon_skill = new ActiveSkillData
        {
            skill_name = skill.skill_name + "〔召唤〕", combat_source_key = skill.combat_source_key,
            artifact_instance_id = item.instance_id, artifact_is_summon = true,
            skill_type = ActiveSkillData.SkillType.SPECIAL, target_type = 0,
            energy_cost = item.deploy_energy_cost, mana_cost = item.deploy_mana_cost, description = skill.description
        };
        return skill;
    }

    public static ActiveSkillData ApplyInner(ActiveSkillData source, ArtifactInstanceData item)
    {
        var result = (ActiveSkillData)source.Duplicate(true);
        result.combat_source_key = Key(source);
        result.physical_defense_ignore_ratio = item.combat_profile!.physical_defense_ignore_ratio;
        result.artifact_block_bonus = item.combat_profile.inner_block_bonus;
        result.description = source.description + (result.artifact_block_bonus > 0
            ? $"\n法器化法：实际架招时化解增加{result.artifact_block_bonus}。"
            : $"\n法器化法：本次伤害忽略{result.physical_defense_ignore_ratio:P0}物防。");
        return result;
    }

    public static ActiveSkillData EmptySlot() => new() { skill_name = "", skill_type = ActiveSkillData.SkillType.CURSE, combat_empty_slot = true };
}
