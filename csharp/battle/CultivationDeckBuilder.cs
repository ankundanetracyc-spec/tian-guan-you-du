using Godot;
using Godot.Collections;

/// <summary>从角色修炼结果提取战斗卡牌及其来源信息。</summary>
public partial class CultivationDeckBuilder : RefCounted
{
    public static Array<GodotObject> collect_cards(GodotObject? unit)
    {
        var cards = new Array<GodotObject>();
        if (!GodotObject.IsInstanceValid(unit)) return cards;
        AddBasicDeckCards(unit!, cards);
        foreach (var skill in GetPersonalCards(unit!))
        {
            if (GodotObject.IsInstanceValid(skill)) cards.Add(skill);
        }
        return cards;
    }

    private static Array<GodotObject> GetPersonalCards(GodotObject unit)
    {
        Array<GodotObject> result = [];
        var configured = unit is Character human ? human.is_battle_deck_configured() : unit.HasMeta("battle_deck_configured") && unit.GetMeta("battle_deck_configured").AsBool();
        var selected = GetSelectedSkillPaths(unit);
        void Add(ActiveSkillData? skill)
        {
            if (skill is null || (configured && !selected.Contains(skill.ResourcePath))) return;
            result.Add(skill);
        }
        if (unit is Character character)
        {
            foreach (var skill in character.get_available_battle_skills()) Add(skill);
        }
        else
        {
            var value = unit.Get("active_skills");
            if (value.VariantType == Variant.Type.Array) foreach (var item in value.AsGodotArray()) Add(item.AsGodotObject() as ActiveSkillData);
        }
        if (result.Count > 0 || !unit.HasMeta("active_skill_paths")) return result;
        foreach (var pathValue in unit.GetMeta("active_skill_paths").AsGodotArray()) Add(ResourceLoader.Load<ActiveSkillData>(pathValue.AsString()));
        return result;
    }

    private static Array<string> GetSelectedSkillPaths(GodotObject unit)
    {
        if (unit is Character human && human.entity_data is HumanData data)
        {
            Array<string> result = [];
            foreach (var path in data.loaded_skill_paths)
            {
                var resolved = human.resolve_combat_skill_slot_path(path);
                if (!string.IsNullOrWhiteSpace(resolved) && !result.Contains(resolved)) result.Add(resolved);
            }
            return result;
        }
        if (unit.HasMeta("loaded_skill_paths"))
        {
            Array<string> result = [];
            foreach (var value in unit.GetMeta("loaded_skill_paths").AsGodotArray()) result.Add(value.AsString());
            return result;
        }
        return [];
    }

    private static void AddBasicDeckCards(GodotObject unit, Array<GodotObject> cards)
    {
        if (unit is Beast beast && beast.beast_deck is not null)
        {
            foreach (var skillPath in beast.beast_deck.get_deck_skills())
            {
                var skill = ResourceLoader.Load<ActiveSkillData>(skillPath)?.Duplicate() as ActiveSkillData;
                if (skill is not null) cards.Add(skill);
            }
            return;
        }
        if (unit is Character human) human.refresh_basic_deck_for_bearing();
        var path = GetBasicDeckPath(unit);
        if (string.IsNullOrEmpty(path)) return;
        var deck = ResourceLoader.Load<BasicDeckResource>(path);
        if (deck is null) return;
        var paths = deck.get_deck_skills();
        foreach (var upgradeId in GetSelectedUpgrades(unit))
        {
            var upgrade = deck.get_upgrade_by_id(upgradeId);
            if (upgrade is not null) paths = upgrade.apply_to_paths(paths);
        }
        if (unit is Character character)
        {
            var rewrittenSkills = GongfaSkillRewriteRuntime.ApplyBasicSkillRewriteSkills(character, paths, out var rewrittenPaths);
            for (var i = 0; i < rewrittenSkills.Count; i++)
            {
                var skill = rewrittenSkills[i].Duplicate() as ActiveSkillData;
                if (skill is null) continue;
                skill.SetMeta("basic_deck_source", path);
                skill.SetMeta("battle_card_source", "basic");
                skill.SetMeta("battle_card_path", i < rewrittenPaths.Count ? rewrittenPaths[i] : skill.ResourcePath);
                cards.Add(skill);
            }
            return;
        }
        foreach (var skillPath in paths)
        {
            // 基础牌使用运行时副本保存来源；不能往共享资源写 Meta，
            // 否则同一张秘籍牌会被误判为不可卸下的基础牌。
            var template = ResourceLoader.Load<ActiveSkillData>(skillPath);
            var skill = template?.Duplicate() as ActiveSkillData;
            if (skill is null) continue;
            // 基础技能库是道途底板，永远由此处注入，不属于玩家可调整的功法牌列表。
            skill.SetMeta("basic_deck_source", path);
            skill.SetMeta("battle_card_source", "basic");
            skill.SetMeta("battle_card_path", skillPath);
            cards.Add(skill);
        }
    }

    private static string GetBasicDeckPath(GodotObject unit)
    {
        if (unit is Character human && !string.IsNullOrEmpty(human.basic_deck_path)) return human.basic_deck_path;
        if (unit.HasMeta("basic_deck_path")) return unit.GetMeta("basic_deck_path").AsString();
        return unit.Get("entity_data").AsGodotObject() is HumanData data ? data.basic_deck_path : "";
    }

    private static Array<string> GetSelectedUpgrades(GodotObject unit)
    {
        if (unit is Character human && human.entity_data is HumanData humanData) return humanData.basic_deck_upgrade_ids;
        if (unit.HasMeta("basic_deck_upgrade_ids"))
        {
            Array<string> result = [];
            foreach (var value in unit.GetMeta("basic_deck_upgrade_ids").AsGodotArray()) result.Add(value.AsString());
            return result;
        }
        return unit.Get("entity_data").AsGodotObject() is HumanData data ? data.basic_deck_upgrade_ids : [];
    }

    public static Dictionary get_card_sources(GodotObject? data)
    {
        if (!GodotObject.IsInstanceValid(data)) return [];
        return new Dictionary
        {
            ["main_gongfa"] = data!.Get("main_gongfa_path"),
            ["sub_gongfa"] = data.Get("sub_gongfa_paths"),
            ["equipment"] = data.Get("equipment_paths"),
            ["realm"] = data.Get("cultivation_realm"),
        };
    }
}
