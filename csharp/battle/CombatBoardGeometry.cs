using Godot;

/// <summary>盘面几何只读取明确的占格与份数，不从图片或旧牌库数量推断规则。</summary>
public static class CombatBoardGeometry
{
    public sealed record Definition(Vector2I Size, int MaxPlacements, string Identity);
    public sealed record Issue(string PlacementId, string Message);

    public static Vector2I Footprint(CombatBoardPlacement item, Func<string, ActiveSkillData?> resolve)
    {
        if (item.kind == CombatBoardPlacement.ObjectKind.Pill)
            return BattleSupplyRules.Load(item.source_key) is null ? Vector2I.Zero : Vector2I.One;
        if (item.kind == CombatBoardPlacement.ObjectKind.Talisman)
            return TalismanCombatCatalog.Load(item.source_key)?.get_inventory_size(item.quarter_turns % 2 != 0) ?? Vector2I.Zero;
        if (item.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
            return Vector2I.One;
        return item.kind == CombatBoardPlacement.ObjectKind.Skill
            ? resolve(item.source_key)?.get_board_footprint(item.quarter_turns) ?? Vector2I.Zero
            : Vector2I.Zero;
    }

    public static IEnumerable<Vector2I> Cells(CombatBoardPlacement item, Vector2I footprint)
    {
        for (var y = 0; y < footprint.Y; y++)
        for (var x = 0; x < footprint.X; x++)
            yield return item.cell + new Vector2I(x, y);
    }

    public static bool Fits(Vector2I boardSize, Vector2I origin, Vector2I footprint, ISet<Vector2I> occupied)
    {
        if (boardSize.X <= 0 || boardSize.Y <= 0 || footprint.X <= 0 || footprint.Y <= 0
            || origin.X < 0 || origin.Y < 0
            || (long)origin.X + footprint.X > boardSize.X || (long)origin.Y + footprint.Y > boardSize.Y) return false;
        for (var y = 0; y < footprint.Y; y++)
        for (var x = 0; x < footprint.X; x++)
            if (occupied.Contains(origin + new Vector2I(x, y))) return false;
        return true;
    }

    public static List<Issue> Validate(CombatBoardLayout layout, Func<string, ActiveSkillData?> resolve,
        Func<string, bool>? skillAllowed = null, Func<string, int>? rewriteLimit = null,
        bool? requiresZhouTianIndicator = null, Func<string, int>? pillCount = null)
    {
        var issues = new List<Issue>();
        if (layout.layout_version is not (1 or CombatBoardLayout.CurrentLayoutVersion) || layout.size.X <= 0 || layout.size.Y <= 0
            || (long)layout.size.X * layout.size.Y > int.MaxValue)
        { issues.Add(new("", "招式盘版本或尺寸无效。")); return issues; }

        var occupied = new HashSet<Vector2I>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var indicators = 0;
        foreach (var item in layout.placements)
        {
            if (item is null) { issues.Add(new("", "盘中存在空对象。")); continue; }
            if (string.IsNullOrWhiteSpace(item.placement_id) || !ids.Add(item.placement_id))
                issues.Add(new(item.placement_id, "摆放编号为空或重复。"));
            var footprint = Footprint(item, resolve);
            if (!Fits(layout.size, item.cell, footprint, occupied)
                || layout.unlocked_cells.Count > 0 && Cells(item, footprint).Any(cell => !layout.unlocked_cells.Contains(cell)))
                issues.Add(new(item.placement_id, "对象未定义占格、旋转无效、重叠或超出盘面。"));
            else foreach (var cell in Cells(item, footprint)) occupied.Add(cell);

            string identity;
            int limit;
            if (item.kind == CombatBoardPlacement.ObjectKind.Skill)
            {
                var skill = resolve(item.source_key);
                if (skill is null || !skill.has_board_definition() || skill.get_board_skill_key().Length == 0
                    || skillAllowed is not null && !skillAllowed(item.source_key))
                { issues.Add(new(item.placement_id, "招式未习得、来源不可用或尚未迁移。")); continue; }
                identity = "skill:" + skill.get_board_skill_key();
                limit = skill.board_max_placements;
            }
            else if (item.kind == CombatBoardPlacement.ObjectKind.Rewrite)
            {
                identity = "rewrite:" + item.source_key;
                limit = rewriteLimit?.Invoke(item.source_key) ?? 0;
                if (item.rewrite_direction is < 0 or > 3)
                    issues.Add(new(item.placement_id, "化法方向无效。"));
            }
            else if (item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
            {
                indicators++;
                identity = "zhou_tian_indicator"; limit = 1;
                if (!item.locked || item.rewrite_direction is < 0 or > 3)
                    issues.Add(new(item.placement_id, "周天指示须保留锁定标记及合法方向。"));
            }
            else if (item.kind == CombatBoardPlacement.ObjectKind.Pill)
            {
                if (BattleSupplyRules.Load(item.source_key) is null)
                { issues.Add(new(item.placement_id, "此丹药尚不能用于战斗。")); continue; }
                identity = (item.expended ? "empty_bottle:" : "pill:") + item.source_key;
                limit = item.expended ? int.MaxValue : pillCount?.Invoke(item.source_key) ?? 0;
            }
            else if (item.kind == CombatBoardPlacement.ObjectKind.Talisman)
            {
                if (TalismanCombatCatalog.Load(item.source_key) is null)
                { issues.Add(new(item.placement_id, "此符箓尚不能用于战斗。")); continue; }
                identity = (item.expended ? "talisman_trace:" : "talisman:") + item.source_key;
                limit = item.expended ? int.MaxValue : pillCount?.Invoke(item.source_key) ?? 0;
            }
            else { issues.Add(new(item.placement_id, "盘中对象类型未开放。")); continue; }

            counts[identity] = counts.GetValueOrDefault(identity) + 1;
            if (limit <= 0 || counts[identity] > limit)
                issues.Add(new(item.placement_id, "来源不可用、份数未定或超过可摆上限。"));
        }
        if (requiresZhouTianIndicator is { } required && indicators != (required ? 1 : 0))
            issues.Add(new("", required ? "玄枢心法需要一个周天指示。" : "当前心法不提供周天指示。"));
        return issues;
    }

    public static CombatBoardPlacement? At(CombatBoardLayout layout, Vector2I cell, Func<string, ActiveSkillData?> resolve) =>
        layout.placements.FirstOrDefault(item => item is not null && Cells(item, Footprint(item, resolve)).Contains(cell));

    public static CombatBoardPlacement? PointedSkill(CombatBoardLayout layout, CombatBoardPlacement pointer,
        Func<string, ActiveSkillData?> resolve)
    {
        var target = At(layout, pointer.cell + pointer.get_rewrite_offset(), resolve);
        return target?.kind == CombatBoardPlacement.ObjectKind.Skill ? target : null;
    }
}
