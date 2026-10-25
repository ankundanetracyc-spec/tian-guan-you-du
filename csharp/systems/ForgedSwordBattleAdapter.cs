using Godot;
using System.Globalization;

/// <summary>按成器快照能力构建运行态；无商品名、材料名或器形白名单。</summary>
public static class ForgedSwordBattleAdapter
{
    public static ProcessForgingTrial.Artifact? Read(GodotObject owner)
    {
        if (owner is Character c && c.get_equipped_forged_sword() is { } item && ForgedArtifactInventory.TryRead(item, out var s)) return s;
        return owner.HasMeta("forged_sword_json") ? System.Text.Json.JsonSerializer.Deserialize<ProcessForgingTrial.Artifact>(owner.GetMeta("forged_sword_json").AsString()) : null;
    }
    public static float KeywordMultiplier(GodotObject owner, ActiveSkillData skill) => skill.get_presentation_tags().Contains("剑") && Read(owner) is { } s
        ? 1 + (float)s.Effects.Where(e => e.CarrierId == "keyword" && e.AtomId == "physical_damage_percent").Sum(e => e.Value) / 100 : 1;
    public static void AfterHit(GodotObject owner, GodotObject target, ActiveSkillData skill, int damage)
    {
        if (damage <= 0 || target is ArtifactBody || skill.damage_type != "physical" || !skill.get_presentation_tags().Contains("剑") ||
            skill.combat_is_gongfa_bonus_damage_segment || skill.combat_is_blood_arm_segment || owner.GetMeta("forged_triggered", false).AsBool() || Read(owner) is not { } s) return;
        var shield = (int)s.Effects.Where(e => e.CarrierId == "trigger" && e.AtomId == "shield_flat").Sum(e => e.Value);
        if (shield <= 0) return;
        owner.SetMeta("forged_triggered", true); owner.Set("shield", owner.Get("shield").AsInt32() + shield);
    }
    public static bool IsSword(ArtifactInstanceData item) => ForgedArtifactInventory.TryRead(item, out var s) && s.Category == "剑";
    public static ArtifactInstanceData Prepare(ArtifactInstanceData item)
    {
        var copy = (ArtifactInstanceData)item.Duplicate(true);
        if (!ForgedArtifactInventory.TryRead(item, out var s)) return copy;
        copy.ritual_mode = s.BodyMaximum > 0 && s.Effects.Any(e => e.CarrierId == "summon" && e.Value > 0) ? "外祭" : "";
        copy.outer_body = s.BodyMaximum; copy.spirit_cost = s.SpiritRequired;
        copy.deploy_energy_cost = 1; copy.deploy_mana_cost = 0;
        return copy;
    }
    public static IEnumerable<ActiveSkillData> Skills(ArtifactInstanceData item)
    {
        if (!ForgedArtifactInventory.TryRead(item, out var s)) yield break;
        foreach (var carrier in new[] { "summon", "direct" })
        {
            var effects = s.Effects.Where(e => e.CarrierId == carrier && e.Value > 0).ToList();
            if (effects.Count == 0 || carrier == "summon" && s.BodyMaximum <= 0) continue;
            var damage = effects.Where(e => e.AtomId == "physical_damage_base").Sum(e => e.Value);
            var shield = effects.Where(e => e.AtomId == "shield_flat").Sum(e => e.Value);
            if (damage <= 0 && shield <= 0) continue;
            var key = "artifact:" + item.instance_id + (carrier == "direct" ? "#direct" : "");
            var skill = new ActiveSkillData { skill_name = (carrier == "summon" ? "御使·" : "借器·") + s.Name,
                combat_source_key = key, icon_path = ForgedArtifactPresentation.IconPath(s),
                skill_type = damage > 0 ? ActiveSkillData.SkillType.ATTACK : ActiveSkillData.SkillType.BUFF,
                target_type = damage > 0 ? 2 : 0, damage_type = "physical", base_damage = (float)damage,
                energy_cost = 1, mana_cost = 1, forged_source_instance_id = item.instance_id, forged_spirit_cost = 1,
                forged_flat_ignore = carrier == "summon" && damage > 0 ? s.Sharpness : 0,
                description = $"体力1，法力1；施法期间临时占用灵识1，结束释放。物理伤害{damage:0.##}；自身护盾+{shield:0.##}。" };
            skill.tags.Add("剑");
            if (shield > 0) skill.special_effects.Add(new Godot.Collections.Dictionary { ["effect"] = "shield", ["formula"] = shield.ToString(CultureInfo.InvariantCulture) });
            if (carrier == "summon")
            {
                skill.artifact_instance_id = item.instance_id;
                skill.artifact_summon_skill = new ActiveSkillData { skill_name = "御用·" + s.Name, combat_source_key = key,
                    icon_path = skill.icon_path, artifact_instance_id = item.instance_id, artifact_is_summon = true,
                    skill_type = ActiveSkillData.SkillType.SPECIAL, target_type = 0, energy_cost = 1,
                    description = $"体力1，法力0。召出器体{s.BodyMaximum}，在场占用灵识{s.SpiritRequired}；同槽随后用于御使。" };
            }
            yield return skill;
        }
    }
}
