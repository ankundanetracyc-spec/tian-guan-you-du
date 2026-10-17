using Godot;
using Godot.Collections;

public partial class Character
{
    public static string combat_skill_key(ActiveSkillData skill) => ArtifactSkillFactory.Key(skill);

    public IEnumerable<ArtifactInstanceData> equipped_combat_artifacts() => human is null ? [] : human.artifact_inventory
        .Where(item => item is not null && human.equipped_artifact_ids.Contains(item.instance_id)
            && (item.max_durability <= 0 || item.current_durability > 0) && ArtifactSkillFactory.IsConfigured(item));

    public IEnumerable<ActiveSkillData> get_artifact_skills() => equipped_combat_artifacts()
        .Where(item => item.ritual_mode == "外祭").Select(ArtifactSkillFactory.CreateOuter);

    public ActiveSkillData? resolve_combat_skill(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.StartsWith("artifact:", StringComparison.Ordinal))
            return get_artifact_skills().FirstOrDefault(skill => combat_skill_key(skill) == key);
        var resolved = resolve_combat_skill_slot_path(key);
        // 使用已经合并秘籍化法的运行时版本，而不是重新加载其展示资源。
        var original = GongfaSkillRewriteRuntime.ApplyBasicSkillRewriteSkills(this, get_basic_deck_skill_paths_before_gongfa())
            .Concat(get_available_battle_skills()).FirstOrDefault(skill => combat_skill_key(skill) == resolved);
        original ??= ResourceLoader.Exists(resolved) ? ResourceLoader.Load<ActiveSkillData>(resolved) : null;
        if (!is_combat_skill_slot_eligible(original)) return null;
        var skillCopy = (ActiveSkillData)original!.Duplicate(true);
        skillCopy.combat_source_key = resolved;
        foreach (var item in equipped_combat_artifacts().Where(item => item.ritual_mode == "内炼"))
        {
            if (string.IsNullOrEmpty(item.bound_basic_card_path) || resolve_combat_skill_slot_path(item.bound_basic_card_path) != resolved) continue;
            if (!is_valid_artifact_binding(item, item.bound_basic_card_path)) continue;
            return ArtifactSkillFactory.ApplyInner(skillCopy, item);
        }
        return skillCopy;
    }

    public bool is_valid_artifact_binding(ArtifactInstanceData item, string path)
    {
        if (!ArtifactSkillFactory.IsConfigured(item) || !ResourceLoader.Exists(path)) return false;
        var name = ResourceLoader.Load<ActiveSkillData>(path)?.skill_name;
        return item.form!.form_id == "mingfeng" ? name is "应敌" or "痛击" or "发劲" : name == "架招";
    }

    public bool bind_inner_artifact(string instanceId, string path)
    {
        if (HasMeta("artifact_battle_clone")) return false;
        var item = find_artifact(instanceId);
        if (item is null || item.ritual_mode != "内炼" || !is_valid_artifact_binding(item, path)) return false;
        if (equipped_combat_artifacts().Any(other => other.instance_id != instanceId && other.ritual_mode == "内炼"
            && resolve_combat_skill_slot_path(other.bound_basic_card_path) == resolve_combat_skill_slot_path(path))) return false;
        if (resolve_combat_skill(path) is null) return false;
        item.bound_basic_card_path = path;
        notify_inventory_changed();
        return true;
    }

    public void revoke_artifact_skill(string instanceId)
    {
        if (human is null) return;
        for (var i = 0; i < human.combat_skill_references.Count; i++)
            if (human.combat_skill_references[i]?.artifact_instance_id == instanceId) human.combat_skill_references[i] = new();
        for (var i = 0; i < human.combat_skill_slot_paths.Count; i++)
            if (human.combat_skill_slot_paths[i] == "artifact:" + instanceId) human.combat_skill_slot_paths[i] = "";
    }
}
