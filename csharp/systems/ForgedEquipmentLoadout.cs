using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>单器试用装配。器物按自带快照复算，始终从未装备的角色基线生成战斗副本。</summary>
public sealed class ForgedEquipmentLoadout
{
    private readonly ProcessForgingTrial.Artifact _item;
    private readonly Dictionary<string, string> _bindings = [];
    private readonly HashSet<string> _swordSkills = [];
    public ProcessForgingTrial.Artifact Item => ProcessForgingTrial.Clone(_item);
    public IReadOnlyDictionary<string, string> Bindings => new Dictionary<string, string>(_bindings);
    public ForgedEquipmentLoadout(ProcessForgingTrial.Artifact item)
    {
        if (string.IsNullOrWhiteSpace(item.InstanceId)) throw new ArgumentException("试用器物缺少实例身份");
        var replay = ProcessForgingTrial.Replay(item);
        if (!replay.Success || replay.Product!.InputHash != item.InputHash) throw new ArgumentException("器物来源快照无法验证");
        _item = replay.Product; _item.InstanceId = item.InstanceId;
    }
    public static bool IsPhysicalAttack(ActiveSkillData skill) => !skill.combat_empty_slot && skill.skill_type == ActiveSkillData.SkillType.ATTACK
        && skill.damage_type == "physical" && (skill.base_damage > 0 || skill.damage_formula.Length > 0 || skill.damage_bonuses.Count > 0);
    public static string BindingScope(ProcessForgingTrial.Effect effect) => effect.AtomId switch
    {
        "defuse_flat" => "能实际应对并施加化解的技能",
        "physical_defense_ignore_ratio" => "带气血关键词且造成单体物理伤害的技能",
        _ => effect.Scope
    };
    public bool SetSwordUse(ActiveSkillData skill, bool enabled)
    {
        string key = ArtifactSkillFactory.Key(skill);
        if (key.Length == 0 || !IsPhysicalAttack(skill) || skill.artifact_instance_id.Length > 0) return false;
        if (enabled) _swordSkills.Add(key); else _swordSkills.Remove(key); return true;
    }
    public bool CanBind(ProcessForgingTrial.Effect effect, ActiveSkillData skill)
    {
        if (_item.Tier != "法器" || effect.CarrierId != "binding" || !_item.Effects.Any(x => x.EffectId == effect.EffectId)) return false;
        return effect.AtomId switch
        {
            "defuse_flat" => CardRules.IsResponse(skill) && skill.status_effects.Any(x => x.GetValueOrDefault("status_id", "").AsString() == "化解"),
            "physical_defense_ignore_ratio" => IsPhysicalAttack(skill) && skill.target_type == 2 && skill.aoe_range == 0 && skill.get_presentation_tags().Contains("气血") && skill.physical_defense_ignore_ratio == 0,
            _ => false
        };
    }
    public bool Bind(string effectId, ActiveSkillData? skill)
    {
        if (skill is null) return _bindings.Remove(effectId);
        var effect = _item.Effects.FirstOrDefault(x => x.EffectId == effectId); string key = ArtifactSkillFactory.Key(skill);
        if (effect is null || key.Length == 0 || !CanBind(effect, skill) || _bindings.Any(x => x.Key != effectId && x.Value == key)) return false;
        _bindings[effectId] = key; return true;
    }
    /// <summary>现行试用入口：整器只选择一个对象，不满足该技能条件的修正不生效。</summary>
    public void BindSingle(ActiveSkillData? skill)
    {
        _bindings.Clear();
        if (skill is null) return;
        foreach (var effect in _item.Effects.Where(effect => CanBind(effect, skill)))
            _bindings[effect.EffectId] = ArtifactSkillFactory.Key(skill);
    }
    public BattleEntityData BuildSnapshot(BattleEntityData unequippedBaseline)
    {
        if (unequippedBaseline.trial_forged_equipment_json.Length > 0 || unequippedBaseline.equipped_artifacts.Count > 0)
            throw new InvalidOperationException("单器试用必须从未装备基线生成，不能叠加第二件器");
        var result = new BattleEntityData();
        CopyStoredProperties(unequippedBaseline, result);
        result.trial_forged_equipment_json = JsonSerializer.Serialize(_item);
        result.combat_resolved_skills = [];
        foreach (var source in unequippedBaseline.combat_resolved_skills)
        {
            var skill = CopySkill(source); string key = ArtifactSkillFactory.Key(source);
            skill.combat_source_key = key;
            if (_swordSkills.Contains(key) && IsPhysicalAttack(skill))
            {
                skill.forged_weapon_instance_id = _item.InstanceId;
                if (!skill.tags.Contains("剑")) skill.tags.Add("剑");
                skill.forged_equipment_description += $"\n用器：{_item.Name}，锋锐{_item.Sharpness}；本次忽略{_item.Sharpness}点物防。";
            }
            foreach (var effect in _item.Effects.Where(x => _bindings.GetValueOrDefault(x.EffectId) == key))
            {
                if (!CanBind(effect, source)) throw new InvalidOperationException("化法绑定条件发生变化，请重新装配");
                if (effect.AtomId == "defuse_flat") skill.artifact_block_bonus += (int)effect.Value;
                else skill.physical_defense_ignore_ratio = (float)effect.Value;
                skill.forged_equipment_description += "\n器物化法：" + effect.Description;
            }
            result.combat_resolved_skills.Add(skill);
        }
        return result;
    }
    private static void CopyStoredProperties(Resource source, Resource target)
    {
        foreach (var info in source.GetPropertyList())
        {
            string name = info["name"].AsString();
            if ((info["usage"].AsInt32() & (int)PropertyUsageFlags.Storage) == 0 || name is "script" or "resource_path" or "resource_name") continue;
            target.Set(name, source.Get(name));
        }
    }
    public static ActiveSkillData CopySkill(ActiveSkillData source)
    {
        var copy = new ActiveSkillData(); CopyStoredProperties(source, copy);
        copy.combat_source_key = ArtifactSkillFactory.Key(source);
        copy.tags = source.tags.Duplicate(); copy.keywords = source.keywords.Duplicate();
        copy.gongfa_rewrite_effect_ids = source.gongfa_rewrite_effect_ids.Duplicate();
        copy.damage_bonuses = source.damage_bonuses.Duplicate();
        copy.special_effects = source.special_effects.Duplicate(true); copy.status_effects = source.status_effects.Duplicate(true);
        copy.non_response_skill = source.non_response_skill is { } normal && normal != source ? CopySkill(normal) : null;
        return copy;
    }
    public static void ApplyBattleBonuses(GodotObject owner, string json)
    {
        int oldDefense = owner.HasMeta("forged_defense_bonus") ? owner.GetMeta("forged_defense_bonus").AsInt32() : 0;
        var item = json.Length == 0 ? null : JsonSerializer.Deserialize<ProcessForgingTrial.Artifact>(json) ?? throw new InvalidOperationException("试器快照为空");
        if (item is not null && (item.SchemaVersion != 3 || item.Category != "剑" || item.InstanceId.Length == 0)) throw new InvalidOperationException("试器快照不合法");
        int defense = (int)(item?.Effects.Where(x => x.CarrierId == "base" && x.AtomId == "physical_defense").Sum(x => x.Value) ?? 0);
        float damage = (float)(item?.Effects.Where(x => x.CarrierId == "base" && x.AtomId == "physical_damage_percent").Sum(x => x.Value) ?? 0) / 100;
        if (owner is Creature creature) creature.physical_defense += defense - oldDefense;
        owner.SetMeta("forged_defense_bonus", defense); owner.SetMeta("forged_physical_bonus", damage);
        owner.SetMeta("forged_instance", item?.InstanceId ?? ""); owner.SetMeta("forged_sharpness", item?.Sharpness ?? 0);
        if (item is not null) owner.SetMeta("forged_sword_json", json);
        else owner.RemoveMeta("forged_sword_json");
    }
    public static int WeaponIgnore(GodotObject? owner, ActiveSkillData skill)
    {
        if (!GodotObject.IsInstanceValid(owner) || !IsPhysicalAttack(skill) || skill.combat_is_gongfa_bonus_damage_segment || skill.combat_is_blood_arm_segment
            || !skill.get_presentation_tags().Contains("剑") || skill.forged_weapon_instance_id.Length == 0
            || ForgedSwordBattleAdapter.Read(owner!) is not { } sword || sword.InstanceId != skill.forged_weapon_instance_id
            || skill.forged_source_instance_id.Length > 0 && skill.forged_source_instance_id != sword.InstanceId
            || skill.artifact_instance_id.Length > 0 && skill.artifact_instance_id != sword.InstanceId) return 0;
        return Math.Max(0, sword.Sharpness);
    }
}
