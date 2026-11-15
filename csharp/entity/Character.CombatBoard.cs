using Godot;

public partial class Character
{
    /// <summary>从已学技能回到原定义；盘面化法不再复用旧槽位绑定生成的效果副本。</summary>
    public List<ActiveSkillData> get_available_board_skills()
    {
        var result = new Dictionary<string, ActiveSkillData>(StringComparer.Ordinal);
        var candidates = get_available_battle_skills().AsEnumerable()
            .Concat(get_basic_deck_skill_paths_before_gongfa()
                .Select(path => ResourceLoader.Exists(path) ? ResourceLoader.Load<ActiveSkillData>(path) : null)
                .OfType<ActiveSkillData>());
        foreach (var candidate in candidates)
        {
            if (!candidate.has_board_definition()) continue;
            var key = candidate.get_board_skill_key();
            if (key.Length == 0 || !ResourceLoader.Exists(key)) continue;
            var original = ResourceLoader.Load<ActiveSkillData>(key);
            if (original is not null && original.has_board_definition() && GongfaAvailability.IsSkillEnabled(original))
                result.TryAdd(key, original);
        }
        return result.Values.ToList();
    }

    public CombatBoardLayout get_combat_board_preview()
    {
        if (human is null) return new CombatBoardLayout();
        var hasHeart = get_combat_passive_paths().Any(path =>
            ResourceLoader.Exists(path) && ResourceLoader.Load<ActiveSkillData>(path)?.skill_name == "玄枢含章");
        return CombatBoardMigration.EnsurePreview(human, hasHeart);
    }
}
