using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Character
{
    /// <summary>将旧档的自动化法版本恢复为原招式身份；不会因此确认任何化法。</summary>
    public string get_base_combat_skill_path(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("artifact:", StringComparison.Ordinal)) return path;
        foreach (var book in GongfaAvailability.EnabledBooks())
        foreach (var chapter in book.get_sorted_chapters())
        foreach (var rewrite in chapter.get_deck_rewrites(true).Concat(chapter.get_deck_rewrites(false)).Distinct())
        {
            if (rewrite.result_skill?.ResourcePath != path && rewrite.main_result_skill?.ResourcePath != path) continue;
            if (rewrite.source_skill is ActiveSkillData source && source.ResourcePath.Length > 0) return source.ResourcePath;
            var originals = book.granted_active_skills.OfType<ActiveSkillData>()
                .Concat(book.get_sorted_chapters().SelectMany(c => c.get_learned_skills(true)).OfType<ActiveSkillData>())
                .Concat(get_basic_deck_skill_paths_before_gongfa().Select(p => ResourceLoader.Load<ActiveSkillData>(p)).OfType<ActiveSkillData>());
            var original = originals.FirstOrDefault(s => s.skill_name == rewrite.source_skill_name &&
                s.ResourcePath != rewrite.result_skill?.ResourcePath && s.ResourcePath != rewrite.main_result_skill?.ResourcePath);
            if (original is not null) return original.ResourcePath;
        }
        return path;
    }

    public List<SkillRewriteChoice> get_skill_rewrite_choices()
    {
        var choices = new List<SkillRewriteChoice>();
        if (human is null) return choices;
        foreach (var book in get_known_gongfas().Where(GongfaAvailability.IsEnabled))
        {
            var isMain = book == main_gongfa;
            var equipped = isMain || sub_gongfa_list.Contains(book);
            foreach (var chapter in book.get_sorted_chapters())
            {
                if (!is_gongfa_chapter_learned(book, chapter.get_id())) continue;
                var available = equipped && (isMain || get_sub_gongfa_chapter_route(book, chapter.get_id()) == "right");
                var configs = chapter.get_deck_rewrites(isMain);
                for (var i = 0; i < configs.Count; i++)
                {
                    var rewrite = configs[i];
                    if (!rewrite.enabled || rewrite.get_result_skill(isMain) is not ActiveSkillData replacement) continue;
                    // 来源身份使用原配置，不随主辅修结果版本变化。
                    var id = $"gongfa:{book.ResourcePath}|{chapter.get_id()}|{i}";
                    var ownPaths = book.granted_active_skills.OfType<ActiveSkillData>()
                        .Concat(book.get_sorted_chapters().SelectMany(c => c.get_learned_skills(isMain)).OfType<ActiveSkillData>())
                        .Select(s => s.ResourcePath).ToHashSet(StringComparer.Ordinal);
                    // 精确指向催血、养锋等节点招式的化法，同样只能绑定已参悟的原技能。
                    var basicPaths = get_basic_deck_skill_paths_before_gongfa().Concat(get_unlocked_skill_node_paths()).ToHashSet(StringComparer.Ordinal);
                    choices.Add(new SkillRewriteChoice
                    {
                        Id = id, Source = $"{book.gongfa_name} · {chapter.chapter_name}",
                        Title = $"{replacement.skill_name}化法",
                        Condition = rewrite.source_skill_name,
                        Description = GongfaSkillRewriteRuntime.GetRewriteAddedEffectDescriptions(replacement), Available = available,
                        UnavailableReason = !equipped ? "需装备此秘籍为主修或辅修" : "辅修本卷需选择化法分支",
                        Matches = skill => rewrite.matches(skill) &&
                            (rewrite.target_scope == GongfaDeckRewriteConfig.RewriteTargetScope.BASIC_SKILL ? basicPaths : ownPaths).Contains(combat_skill_key(skill)),
                        Apply = skill => GongfaSkillRewriteRuntime.CopyWithMergedEffects(replacement, skill)
                    });
                }
            }
        }

        foreach (var item in human.artifact_inventory.Where(item => item is not null))
        {
            var equipped = human.equipped_artifact_ids.Contains(item.instance_id) &&
                (item.max_durability <= 0 || item.current_durability > 0);
            if (ForgedArtifactInventory.IsForged(item))
            {
                if (!ForgedArtifactInventory.TryRead(item, out var snapshot)) continue;
                var effects = snapshot.Effects.Where(e => e.CarrierId == "binding").ToList();
                if (effects.Count == 0) continue;
                ForgedEquipmentLoadout loadout;
                try { loadout = new ForgedEquipmentLoadout(snapshot); }
                catch (ArgumentException) { continue; }
                choices.Add(new SkillRewriteChoice
                {
                    Id = $"forged:{item.instance_id}", Source = item.artifact_name, Title = "器物化法",
                    Condition = string.Join("；", effects.Select(ForgedEquipmentLoadout.BindingScope).Distinct()),
                    Description = string.Join("\n", effects.Select(e => e.Description)),
                    Available = equipped, UnavailableReason = "需装备且器物仍可用",
                    Matches = skill => effects.All(effect => loadout.CanBind(effect, skill)),
                    Apply = skill =>
                    {
                        var copy = ForgedEquipmentLoadout.CopySkill(skill);
                        foreach (var effect in effects)
                        {
                            if (effect.AtomId == "defuse_flat") copy.artifact_block_bonus += (int)effect.Value;
                            else if (effect.AtomId == "physical_defense_ignore_ratio") copy.physical_defense_ignore_ratio = (float)effect.Value;
                            copy.forged_equipment_description += "\n器物化法：" + effect.Description;
                        }
                        return copy;
                    }
                });
            }
            else if (item.ritual_mode == "内炼" && ArtifactSkillFactory.IsConfigured(item))
            {
                choices.Add(new SkillRewriteChoice
                {
                    Id = $"artifact:{item.instance_id}", Source = item.artifact_name, Title = "器物化法",
                    Condition = item.form!.form_id == "mingfeng" ? "应敌、痛击、发劲" : "架招",
                    Description = item.form.form_id == "mingfeng"
                        ? $"本次伤害忽略{item.combat_profile!.physical_defense_ignore_ratio:P0}物防。"
                        : $"实际架招时，化解增加{item.combat_profile!.inner_block_bonus}层。",
                    Available = equipped, UnavailableReason = "需装备且器物仍可用", LegacyArtifact = item,
                    Matches = skill => is_valid_artifact_binding(item, combat_skill_key(skill)),
                    Apply = skill => ArtifactSkillFactory.ApplyInner(skill, item)
                });
            }
        }
        return choices;
    }

    public string get_skill_rewrite_binding(SkillRewriteChoice choice)
    {
        if (human is null) return "";
        return get_base_combat_skill_path(choice.LegacyArtifact?.bound_basic_card_path ??
            (human.skill_rewrite_bindings.TryGetValue(choice.Id, out var key) ? key : ""));
    }

    public List<(int Slot, string Key, ActiveSkillData Skill)> get_skill_rewrite_candidates(string sourceId)
    {
        var choice = get_skill_rewrite_choices().FirstOrDefault(c => c.Id == sourceId);
        return choice is null ? [] : get_skill_rewrite_candidates(choice);
    }

    /// <summary>复用已汇总的来源项检查候选，避免界面逐条化法重复构造整份来源列表。</summary>
    public List<(int Slot, string Key, ActiveSkillData Skill)> get_skill_rewrite_candidates(SkillRewriteChoice choice)
    {
        var candidates = new List<(int, string, ActiveSkillData)>();
        if (choice.Available != true) return candidates;
        var slots = get_combat_skill_slot_paths();
        for (var i = 0; i < slots.Count; i++)
        {
            var key = get_base_combat_skill_path(slots[i]);
            if (key.Length == 0) continue;
            var skill = key.StartsWith("artifact:", StringComparison.Ordinal)
                ? get_artifact_skills().FirstOrDefault(s => combat_skill_key(s) == key)
                : ResourceLoader.Load<ActiveSkillData>(key);
            if (is_combat_skill_slot_eligible(skill) && choice.Matches(skill!)) candidates.Add((i, key, skill!));
        }
        return candidates;
    }

    public bool set_skill_rewrite_binding(string sourceId, string skillKey, out string reason)
    {
        reason = "化法来源已不可用。";
        if (human is null || HasMeta("artifact_battle_clone")) return false;
        var choice = get_skill_rewrite_choices().FirstOrDefault(c => c.Id == sourceId);
        if (choice is null) return false;
        var key = get_base_combat_skill_path(skillKey);
        if (key.Length > 0)
        {
            if (!get_skill_rewrite_candidates(sourceId).Any(c => c.Key == key))
            { reason = "请选择已装配且符合化法条件的技能。"; return false; }
            if (choice.LegacyArtifact is not null && get_skill_rewrite_choices().Any(c => c.Id != sourceId &&
                c.LegacyArtifact is not null && c.Available && get_skill_rewrite_binding(c) == key))
            { reason = "此技能已绑定另一件内炼器物。"; return false; }
        }
        if (choice.LegacyArtifact is not null) choice.LegacyArtifact.bound_basic_card_path = key;
        else if (key.Length == 0) human.skill_rewrite_bindings.Remove(sourceId);
        else human.skill_rewrite_bindings[sourceId] = key;
        reason = key.Length == 0 ? "已解除绑定。" : "化法已绑定所选技能。";
        notify_inventory_changed();
        return true;
    }

    private ActiveSkillData apply_selected_skill_rewrites(ActiveSkillData original, string key)
    {
        if (human is null) return original;
        // 不调用槽位读取器，避免“读取槽位→解析技能→读取槽位”的递归。
        if (!human.combat_skill_references.Any(slot => slot is not null && get_base_combat_skill_path(slot.Key) == key)) return original;
        var result = original;
        foreach (var choice in get_skill_rewrite_choices())
        {
            if (!choice.Available || get_skill_rewrite_binding(choice) != key || !choice.Matches(original)) continue;
            result = choice.Apply(result);
            result.combat_source_key = key;
        }
        return result;
    }
}
