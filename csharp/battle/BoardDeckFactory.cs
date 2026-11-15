/// <summary>一场战斗内的摆放身份；同一盘的不同份数各有一张牌。</summary>
public readonly record struct BoardCardKey(ulong OwnerInstanceId, string PlacementId);

/// <summary>牌保留原摆放来源；技能是包含该份化法的独立战斗副本。</summary>
public sealed record BoardCard(BoardCardKey Key, string SourceKey, ActiveSkillData Skill);

/// <summary>非招式对象仅被保留，并不表示已经接入手牌或可以在新战斗中使用。</summary>
public sealed record BoardDeckBuildResult(
    IReadOnlyList<BoardCard> Cards,
    IReadOnlyList<CombatBoardPlacement> OtherPlacements,
    IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}

/// <summary>把已通过战备校验的盘面快照转换为战斗牌；不负责抽牌或支付。</summary>
public static class BoardDeckFactory
{
    public static BoardDeckBuildResult Build(ulong ownerInstanceId, CombatBoardLayout layout, CombatBoardCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in layout.placements)
        {
            if (item is null)
            {
                errors.Add("盘中存在空对象。");
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.placement_id) || !ids.Add(item.placement_id))
                errors.Add($"摆放编号为空或重复：{item.placement_id}");
            if (item.kind == CombatBoardPlacement.ObjectKind.Skill
                && (catalog.FindSkill(item.source_key) is not { } source || !source.has_board_definition()))
                errors.Add($"招式来源不可用或缺少盘面定义：{item.source_key}");
            if (item.kind == CombatBoardPlacement.ObjectKind.Rewrite && catalog.FindRewrite(item.source_key) is null)
                errors.Add($"化法来源不可用：{item.source_key}");
            if (!Enum.IsDefined(item.kind))
                errors.Add($"盘中对象类型无效：{item.kind}");
        }
        if (errors.Count != 0)
            return new([], [], errors.ToArray());

        // 快照同时保留化法指向所需的坐标、朝向，以及消耗品的已使用标记。
        var snapshot = layout.CopyConfiguration();
        var cards = new List<BoardCard>();
        var other = new List<CombatBoardPlacement>();
        foreach (var item in snapshot.placements)
        {
            if (item.kind != CombatBoardPlacement.ObjectKind.Skill)
            {
                other.Add(item);
                continue;
            }
            var skill = catalog.ResolvePlacement(snapshot, item);
            if (skill is null)
            {
                errors.Add($"无法解析招式摆放：{item.placement_id}");
                break;
            }
            cards.Add(new(new(ownerInstanceId, item.placement_id), item.source_key, skill));
        }
        return errors.Count == 0 ? new(cards.ToArray(), other.ToArray(), []) : new([], [], errors.ToArray());
    }
}
