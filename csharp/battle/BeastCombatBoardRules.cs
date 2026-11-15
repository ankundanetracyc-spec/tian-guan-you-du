using Godot;

/// <summary>Validates that every creature entering a board battle has one complete, fixed 3x3 board.</summary>
public static class BeastCombatBoardRules
{
    public static bool TryValidateBattleSnapshots(IEnumerable<BattleEntityData> snapshots, out string reason)
    {
        foreach (var snapshot in snapshots.Where(item => item.entity_type == (int)WorldEntity.EntityType.BEAST))
        {
            var skills = new List<ActiveSkillData>();
            foreach (var path in snapshot.active_skill_paths.Distinct(StringComparer.Ordinal))
            {
                var skill = ResourceLoader.Load<ActiveSkillData>(path);
                if (skill is null)
                {
                    reason = $"{snapshot.entity_name}缺少招式定义，未进入战斗。";
                    return false;
                }
                skills.Add(skill);
            }
            if (!TryPrepare(snapshot.combat_board_layout, skills, out _, out _, out var layoutReason))
            {
                reason = $"{snapshot.entity_name}没有完整的固定招式盘，未进入战斗。{layoutReason}";
                return false;
            }
        }
        reason = "";
        return true;
    }

    public static bool TryPrepare(CombatBoardLayout? sourceLayout, IEnumerable<ActiveSkillData> sourceSkills,
        out CombatBoardCatalog catalog, out CombatBoardLayout layout, out string reason)
    {
        var preparedCatalog = new CombatBoardCatalog();
        catalog = preparedCatalog;
        layout = new();
        if (sourceLayout is null || sourceLayout.size != new Vector2I(3, 3) || !sourceLayout.confirmed)
        {
            reason = "固定盘缺失，或不是已确认的3×3盘面。";
            return false;
        }

        var equipped = sourceSkills.Where(skill => skill is not null && !skill.combat_empty_slot)
            .GroupBy(skill => skill.get_board_skill_key(), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (equipped.Count == 0)
        {
            reason = "没有可用招式定义。";
            return false;
        }

        foreach (var (key, skill) in equipped)
        {
            if (!skill.has_board_definition())
            {
                reason = $"招式「{skill.skill_name}」尚无招式盘定义。";
                return false;
            }
            var original = ResourceLoader.Exists(key) ? ResourceLoader.Load<ActiveSkillData>(key) : null;
            preparedCatalog.skills.Add(original ?? skill);
        }

        if (sourceLayout.placements.Any(item => item.kind != CombatBoardPlacement.ObjectKind.Skill))
        {
            reason = "固定盘含有生灵不支持的盘面对象。";
            return false;
        }
        var placedKeys = sourceLayout.placements.Select(item => item.source_key).ToHashSet(StringComparer.Ordinal);
        var missing = equipped.Keys.Where(key => !placedKeys.Contains(key)).ToArray();
        var extra = placedKeys.Where(key => !equipped.ContainsKey(key)).ToArray();
        if (missing.Length > 0 || extra.Length > 0)
        {
            reason = missing.Length > 0
                ? $"固定盘未摆入招式「{string.Join("、", missing.Select(key => equipped[key].skill_name))}」。"
                : "固定盘含有不属于当前生灵的招式。";
            return false;
        }

        layout = sourceLayout.CopyConfiguration();
        var issues = CombatBoardGeometry.Validate(layout, preparedCatalog.FindSkill, key => preparedCatalog.FindSkill(key) is not null);
        if (issues.Count > 0)
        {
            reason = issues[0].Message;
            return false;
        }
        reason = "";
        return true;
    }
}
