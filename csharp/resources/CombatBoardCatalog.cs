using Godot;
using Godot.Collections;

/// <summary>战斗只读的真实已学来源快照；不从本场布局反推学习资格。</summary>
[GlobalClass]
public partial class CombatBoardCatalog : Resource
{
    [Export] public Array<ActiveSkillData> skills = [];
    [Export] public Array<CombatBoardRewriteSource> rewrites = [];

    public ActiveSkillData? FindSkill(string key) => skills.FirstOrDefault(skill => skill.get_board_skill_key() == key);
    public CombatBoardRewriteSource? FindRewrite(string key) => rewrites.FirstOrDefault(source => source.source_key == key);

    public static CombatBoardCatalog FromCharacter(Character actor)
    {
        var result = new CombatBoardCatalog { skills = new(actor.get_available_board_skills()) };
        foreach (var choice in actor.get_skill_rewrite_choices().Where(choice => choice.Available && choice.BoardDefinitionVersion > 0))
        {
            var targets = result.skills.Where(choice.Matches).ToArray();
            // 来源可存在而尚未参悟目标；此时留在已学化法库，放盘后显示未生效。
            var source = new CombatBoardRewriteSource
            {
                source_key = choice.Id, title = choice.Title, source_name = choice.Source,
                description = choice.Description, icon_path = choice.BoardIconPath,
                max_placements = choice.BoardMaxPlacements,
                target_keys = new(targets.Select(skill => skill.get_board_skill_key()))
            };
            foreach (var target in targets)
            {
                var applied = choice.Apply(target);
                foreach (var effect in applied.gongfa_rewrite_effect_ids)
                    if (!source.effect_ids.Contains(effect)) source.effect_ids.Add(effect);
                foreach (var tag in applied.tags)
                    if (!source.tags.Contains(tag)) source.tags.Add(tag);
            }
            result.rewrites.Add(source);
        }
        return result;
    }

    public ActiveSkillData? ResolvePlacement(CombatBoardLayout layout, CombatBoardPlacement placed)
    {
        if (placed.kind != CombatBoardPlacement.ObjectKind.Skill || FindSkill(placed.source_key) is not { } original) return null;
        var skill = (ActiveSkillData)original.Duplicate(true);
        skill.combat_source_key = skill.board_base_skill_path = original.get_board_skill_key();
        foreach (var item in layout.placements.Where(item => item.kind == CombatBoardPlacement.ObjectKind.Rewrite))
        {
            var target = CombatBoardGeometry.PointedSkill(layout, item, FindSkill);
            if (target?.placement_id != placed.placement_id || FindRewrite(item.source_key) is not { } source
                || !source.target_keys.Contains(skill.get_board_skill_key())) continue;
            foreach (var effect in source.effect_ids)
                if (!skill.gongfa_rewrite_effect_ids.Contains(effect)) skill.gongfa_rewrite_effect_ids.Add(effect);
            foreach (var tag in source.tags)
                if (!skill.tags.Contains(tag)) skill.tags.Add(tag);
        }
        return skill;
    }
}
