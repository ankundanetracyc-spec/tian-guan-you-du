using Godot;

/// <summary>只生成可检查的新版配置；完整保留旧槽、重复引用、化法和无法容纳的来源。</summary>
public static class CombatBoardMigration
{
    public static CombatBoardLayout CreatePreview(IEnumerable<string> legacyKeys,
        IEnumerable<KeyValuePair<string, string>> legacyBindings, Func<string, ActiveSkillData?> resolve,
        bool requiresZhouTianIndicator, Vector2I? size = null, IEnumerable<Vector2I>? unlockedCells = null)
    {
        var result = new CombatBoardLayout { size = size ?? new Vector2I(3, 3), migrated_from_legacy = true };
        var availableCells = unlockedCells?.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        if (availableCells is not null) result.unlocked_cells = new(availableCells);
        var keys = legacyKeys.ToList();
        foreach (var key in keys) result.legacy_skill_keys.Add(key ?? "");
        foreach (var pair in legacyBindings) result.legacy_rewrite_bindings[pair.Key] = pair.Value;
        var occupied = new HashSet<Vector2I>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (requiresZhouTianIndicator)
        {
            var indicator = new CombatBoardPlacement
            {
                placement_id = "heart:xuan_shu:indicator", kind = CombatBoardPlacement.ObjectKind.ZhouTianIndicator,
                source_key = "heart:xuan_shu", cell = Vector2I.Zero, locked = true
            };
            result.placements.Add(indicator); occupied.Add(Vector2I.Zero);
        }

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (string.IsNullOrWhiteSpace(key)) continue;
            void Pending(string reason) => result.pending_entries.Add(new()
                { source_key = key, legacy_slot_index = i, reason = reason });
            if (key.StartsWith("artifact:", StringComparison.Ordinal))
            { Pending("器物来源保留，须按手持或御使规则重新配置。"); continue; }
            var skill = resolve(key);
            if (skill is null || !skill.has_board_definition() || skill.get_board_skill_key().Length == 0)
            { Pending("来源缺失、未开放或尚无新版招式定义。"); continue; }
            var identity = skill.get_board_skill_key();
            if (counts.GetValueOrDefault(identity) >= skill.board_max_placements)
            { Pending("超过本招同时可摆份数，原学习记录保留。"); continue; }
            var item = new CombatBoardPlacement
                { placement_id = "legacy:skill:" + i, source_key = identity, kind = CombatBoardPlacement.ObjectKind.Skill };
            if (!TryPlace(result.size, item, skill, occupied, availableCells))
            { Pending("当前盘面放不下，保留为待摆招式。"); continue; }
            counts[identity] = counts.GetValueOrDefault(identity) + 1;
            result.placements.Add(item);
        }
        foreach (var pair in result.legacy_rewrite_bindings)
            result.pending_entries.Add(new()
            {
                source_key = pair.Key, original_target_key = pair.Value,
                reason = "旧化法关系已保留，须按有效来源和实际邻接重新指向。"
            });
        return result;
    }

    private static bool TryPlace(Vector2I boardSize, CombatBoardPlacement item, ActiveSkillData skill,
        ISet<Vector2I> occupied, IReadOnlyCollection<Vector2I>? unlockedCells = null)
    {
        var rotations = skill.board_rotation_allowed ? new[] { 0, 1 } : new[] { 0 };
        IEnumerable<Vector2I> candidates = unlockedCells is null
            ? Enumerable.Range(0, boardSize.Y).SelectMany(y => Enumerable.Range(0, boardSize.X).Select(x => new Vector2I(x, y)))
            : unlockedCells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X);
        foreach (var origin in candidates)
        foreach (var turns in rotations)
        {
            var footprint = skill.get_board_footprint(turns);
            if (!CombatBoardGeometry.Fits(boardSize, origin, footprint, occupied)
                || unlockedCells is not null && Enumerable.Range(0, footprint.Y).SelectMany(y => Enumerable.Range(0, footprint.X)
                    .Select(x => origin + new Vector2I(x, y))).Any(cell => !unlockedCells.Contains(cell))) continue;
            item.cell = origin; item.quarter_turns = turns;
            foreach (var cell in CombatBoardGeometry.Cells(item, footprint)) occupied.Add(cell);
            return true;
        }
        return false;
    }

    public static CombatBoardLayout EnsurePreview(HumanData owner, bool requiresZhouTianIndicator)
    {
        if (owner.combat_board_layout is not null) return owner.combat_board_layout;
        IEnumerable<string> keys = owner.combat_skill_references.Count > 0
            ? owner.combat_skill_references.Select(reference => reference?.Key ?? "")
            : owner.combat_skill_slot_paths;
        owner.combat_board_layout = CreatePreview(keys, owner.skill_rewrite_bindings,
            key => ResourceLoader.Exists(key) ? ResourceLoader.Load<ActiveSkillData>(key) : null,
            requiresZhouTianIndicator, CombatBoardUnlockRules.SizeForRealm(owner.cultivation_realm),
            CombatBoardUnlockRules.CellsForRealm(owner.cultivation_realm));
        return owner.combat_board_layout;
    }
}
