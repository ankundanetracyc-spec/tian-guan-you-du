using Godot;
using Godot.Collections;

public partial class Character
{
    public static string combat_skill_key(ActiveSkillData skill) => ArtifactSkillFactory.Key(skill);

    public IEnumerable<ArtifactInstanceData> equipped_combat_artifacts() => human is null ? [] : human.artifact_inventory
        .Where(item => item is not null && human.equipped_artifact_ids.Contains(item.instance_id)
            && (item.max_durability <= 0 || item.current_durability > 0) && ArtifactSkillFactory.IsConfigured(item))
        .Select(item => ForgedArtifactInventory.IsForged(item) ? ForgedSwordBattleAdapter.Prepare(item) : item);

    public IEnumerable<ActiveSkillData> get_artifact_skills() => equipped_combat_artifacts().SelectMany(item =>
        ForgedArtifactInventory.IsForged(item) ? ForgedSwordBattleAdapter.Skills(item) :
        item.ritual_mode == "外祭" ? new[] { ArtifactSkillFactory.CreateOuter(item) } : Enumerable.Empty<ActiveSkillData>());

    public ActiveSkillData? resolve_combat_skill(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.StartsWith("artifact:", StringComparison.Ordinal))
        {
            var artifactSkill = get_artifact_skills().FirstOrDefault(skill => combat_skill_key(skill) == key);
            return artifactSkill is null ? null : apply_selected_skill_rewrites(artifactSkill, key);
        }
        var resolved = resolve_combat_skill_slot_path(key);
        // 先取得原始招式，再按明确绑定生成化法副本。
        var original = GongfaSkillRewriteRuntime.ApplyBasicSkillRewriteSkills(this, get_basic_deck_skill_paths_before_gongfa())
            .Concat(get_available_battle_skills()).FirstOrDefault(skill => combat_skill_key(skill) == resolved);
        original ??= ResourceLoader.Exists(resolved) ? ResourceLoader.Load<ActiveSkillData>(resolved) : null;
        if (!is_combat_skill_slot_eligible(original)) return null;
        var skillCopy = (ActiveSkillData)original!.Duplicate(true);
        skillCopy.combat_source_key = resolved;
        return apply_selected_skill_rewrites(skillCopy, resolved);
    }

    public bool is_valid_artifact_binding(ArtifactInstanceData item, string path)
    {
        if (!ArtifactSkillFactory.IsConfigured(item) || path.StartsWith("artifact:", StringComparison.Ordinal) || !ResourceLoader.Exists(path)) return false;
        var name = ResourceLoader.Load<ActiveSkillData>(path)?.skill_name;
        return item.form!.form_id == "mingfeng" ? name is "应敌" or "痛击" or "发劲" : name == "架招";
    }

    public bool bind_inner_artifact(string instanceId, string path)
    {
        return set_skill_rewrite_binding("artifact:" + instanceId, path, out _);
    }

    public void revoke_artifact_skill(string instanceId)
    {
        if (human is null) return;
        for (var i = 0; i < human.combat_skill_references.Count; i++)
            if (human.combat_skill_references[i]?.artifact_instance_id is { } source && (source == instanceId || source == instanceId + "#direct")) human.combat_skill_references[i] = new();
        for (var i = 0; i < human.combat_skill_slot_paths.Count; i++)
            if (human.combat_skill_slot_paths[i] == "artifact:" + instanceId || human.combat_skill_slot_paths[i] == "artifact:" + instanceId + "#direct") human.combat_skill_slot_paths[i] = "";
    }
}
