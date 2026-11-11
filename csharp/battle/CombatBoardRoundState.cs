using Godot;

/// <summary>一名参战单位的逐格游标与同招账本。调用方负责真实目标、费用和技能效果。</summary>
public sealed class CombatBoardRoundState
{
    public sealed record Visit(Vector2I Cell, CombatBoardPlacement? Placement, ActiveSkillData? Skill, bool FirstCheck);
    public sealed record VisitFeedback(long Serial, Vector2I Cell, string PlacementId);
    private sealed class SkillLedger
    {
        public int RoundLimit, BattleLimit, Cooldown, RoundUsed, BattleUsed, ReadyRound;
    }

    private readonly Func<string, ActiveSkillData?> _resolve;
    private readonly Dictionary<Vector2I, CombatBoardPlacement> _cells = [];
    private readonly Dictionary<string, SkillLedger> _ledgers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _succeeded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _armed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _scanned = new(StringComparer.Ordinal);
    private readonly HashSet<string> _attempted = new(StringComparer.Ordinal);
    private CombatBoardLayout _layout;
    private int _cursor;
    private int _selectedRow = -1;
    private bool _zhouTianUsed;
    private string _currentNormalCandidate = "";
    private readonly List<VisitFeedback> _visitFeedbackEvents = [];

    public int Round { get; private set; }
    public int SelectedRow => _selectedRow;
    public int NextRow => _selectedRow;
    public bool TraversalComplete => _selectedRow >= 0 && _cursor >= _layout.size.X;
    public CombatBoardLayout Layout => _layout;
    public bool HasUsedZhouTian => _zhouTianUsed;
    public long VisitSerial { get; private set; }
    public Vector2I? LastVisitedCell { get; private set; }
    public string LastVisitedPlacementId { get; private set; } = "";
    public IReadOnlyList<VisitFeedback> VisitFeedbackEvents => _visitFeedbackEvents;

    public CombatBoardRoundState(CombatBoardLayout layout, Func<string, ActiveSkillData?> resolve)
    {
        if (layout.size.X <= 0 || layout.size.Y <= 0) throw new ArgumentException("盘面尺寸无效。", nameof(layout));
        _layout = layout.CopyConfiguration(); _resolve = resolve; RebuildCells();
    }

    private void RebuildCells()
    {
        _cells.Clear();
        foreach (var item in _layout.placements)
        {
            var footprint = CombatBoardGeometry.Footprint(item, _resolve);
            foreach (var cell in CombatBoardGeometry.Cells(item, footprint))
                if (!_cells.TryAdd(cell, item)) throw new ArgumentException("盘面对象重叠。");
            if (item.kind == CombatBoardPlacement.ObjectKind.Skill && _resolve(item.source_key) is { } skill)
                Ledger(skill);
        }
    }

    private SkillLedger Ledger(ActiveSkillData skill)
    {
        var key = skill.get_board_skill_key();
        if (key.Length == 0) throw new ArgumentException("招式缺少稳定身份。");
        if (_ledgers.TryGetValue(key, out var ledger)) return ledger;
        ledger = new()
        {
            RoundLimit = skill.uses_per_round,
            BattleLimit = skill.uses_per_battle > 0 ? skill.uses_per_battle : skill.max_charges,
            Cooldown = skill.cooldown_rounds
        };
        _ledgers.Add(key, ledger); return ledger;
    }

    /// <summary>同一回合重复调用不刷新任何机会、共享次数或待命。</summary>
    public bool BeginRound(int round) => BeginRoundCore(round, 0);

    /// <summary>战斗新制：回合账本先刷新，随后由单位选择或自动分配一行。</summary>
    public bool BeginChoosingRound(int round) => BeginRoundCore(round, -1);

    private bool BeginRoundCore(int round, int initialRow)
    {
        if (round <= Round) return false;
        Round = round; _selectedRow = initialRow; _cursor = 0; _succeeded.Clear(); _armed.Clear();
        _scanned.Clear(); _attempted.Clear(); _zhouTianUsed = false; _currentNormalCandidate = "";
        _visitFeedbackEvents.Clear();LastVisitedCell=null;LastVisitedPlacementId="";
        foreach (var ledger in _ledgers.Values) ledger.RoundUsed = 0;
        return true;
    }

    public bool SelectRow(int row)
    {
        if (Round <= 0 || _selectedRow >= 0 || row < 0 || row >= _layout.size.Y) return false;
        _selectedRow = row; _cursor = 0;
        return true;
    }

    public void SkipSelectedRow() => _cursor = _layout.size.X;

    public bool TryReadNextCell(out Visit visit)
    {
        visit = null!;
        if (Round <= 0 || _selectedRow < 0 || TraversalComplete) return false;
        var cell = new Vector2I(_cursor, _selectedRow);
        _cursor++;
        _cells.TryGetValue(cell, out var item);
        LastVisitedCell=cell;LastVisitedPlacementId=item?.placement_id??"";
        VisitSerial++;
        var skill = item?.kind == CombatBoardPlacement.ObjectKind.Skill ? _resolve(item.source_key) : null;
        var firstCheck = item is not null && _scanned.Add(item.placement_id);
        _currentNormalCandidate = firstCheck ? item!.placement_id : "";
        // 空格仍逐格反馈；多格对象只反馈其起始格，后续占格不再单独播放遍历。
        if (item is null || firstCheck) _visitFeedbackEvents.Add(new(VisitSerial,cell,LastVisitedPlacementId));
        if(_visitFeedbackEvents.Count>64)_visitFeedbackEvents.RemoveAt(0);
        visit = new(cell, item, skill, firstCheck);
        return true;
    }

    public bool HasSucceeded(string placementId) => _succeeded.Contains(placementId);
    public bool IsArmed(string placementId) => _armed.Contains(placementId);
    public bool HasBeenChecked(string placementId) => _scanned.Contains(placementId) || _attempted.Contains(placementId);
    public void MarkNormalCheck(CombatBoardPlacement item) => _attempted.Add(item.placement_id);

    public bool CanUseTalisman(CombatBoardPlacement item) => Round > 0
        && item.kind == CombatBoardPlacement.ObjectKind.Talisman && !item.expended
        && _scanned.Contains(item.placement_id)
        && !_attempted.Contains(item.placement_id);

    /// <summary>复命可以占用尚未扫到的份；按实际盘序选择，未支付时只查询。</summary>
    public CombatBoardPlacement? FindReplayOpportunity(ActiveSkillData copiedSkill) => _layout.placements
        .Where(item => item.kind == CombatBoardPlacement.ObjectKind.Skill
            && !_succeeded.Contains(item.placement_id)
            && !_attempted.Contains(item.placement_id)
            && _resolve(item.source_key) is { } skill
            && skill.get_board_skill_key() == copiedSkill.get_board_skill_key()
            && !CardRules.IsResponse(skill) && CanUseDefinition(skill))
        .OrderBy(item => item.cell.Y).ThenBy(item => item.cell.X).FirstOrDefault();

    /// <summary>复制消耗原技能的一份普通机会及共享账本；不要求该份已扫到。</summary>
    public bool RecordReplayUse(CombatBoardPlacement item)
    {
        if (_resolve(item.source_key) is not { } skill || FindReplayOpportunity(skill)?.placement_id != item.placement_id) return false;
        _succeeded.Add(item.placement_id);
        _attempted.Add(item.placement_id);
        _armed.Remove(item.placement_id);
        RecordDefinitionUse(skill);
        return true;
    }

    public bool CanUseDefinition(ActiveSkillData skill)
    {
        if (Round <= 0) return false;
        var ledger = Ledger(skill);
        return Round >= ledger.ReadyRound
            && (ledger.RoundLimit <= 0 || ledger.RoundUsed < ledger.RoundLimit)
            && (ledger.BattleLimit <= 0 || ledger.BattleUsed < ledger.BattleLimit);
    }

    public bool CanUsePlacement(CombatBoardPlacement item, bool zhouTian = false)
    {
        if (item.kind != CombatBoardPlacement.ObjectKind.Skill || _resolve(item.source_key) is not { } skill
            || !CanUseDefinition(skill)) return false;
        if (CardRules.IsResponse(skill))
        {
            return _armed.Contains(item.placement_id) && !_succeeded.Contains(item.placement_id);
        }
        return zhouTian ? !_zhouTianUsed && !CardRules.IsResponse(skill)
            : _scanned.Contains(item.placement_id) && !_succeeded.Contains(item.placement_id)
                && !_attempted.Contains(item.placement_id);
    }

    public bool Arm(CombatBoardPlacement item)
    {
        if (!_scanned.Contains(item.placement_id) || _resolve(item.source_key) is not { } skill
            || !CardRules.IsResponse(skill) || !CanUseDefinition(skill)
            || _succeeded.Contains(item.placement_id) || _attempted.Contains(item.placement_id)) return false;
        if (_armed.Contains(item.placement_id)) return true;
        _armed.Add(item.placement_id);
        return true;
    }

    public IEnumerable<CombatBoardPlacement> ArmedInOrder() => _layout.placements
        .Where(item => _armed.Contains(item.placement_id) && CanUsePlacement(item))
        .OrderBy(item => item.cell.Y).ThenBy(item => item.cell.X);

    /// <summary>仅在真实合法目标与支付已检查后调用，且先于可产生嵌套周天的实际效果。</summary>
    public bool RecordSuccessfulCast(CombatBoardPlacement item, bool zhouTian = false)
    {
        if (!CanUsePlacement(item, zhouTian) || _resolve(item.source_key) is not { } skill) return false;
        if (zhouTian) _zhouTianUsed = true;
        else { _succeeded.Add(item.placement_id); _attempted.Add(item.placement_id); _armed.Remove(item.placement_id); }
        RecordDefinitionUse(skill);
        return true;
    }

    private void RecordDefinitionUse(ActiveSkillData skill)
    {
        var ledger = Ledger(skill);
        ledger.RoundUsed++; ledger.BattleUsed++;
        if (ledger.Cooldown > 0) ledger.ReadyRound = Round + ledger.Cooldown + 1;
    }

    public int RemainingBattleUses(ActiveSkillData skill)
    {
        var ledger = Ledger(skill);
        return ledger.BattleLimit > 0 ? Math.Max(0, ledger.BattleLimit - ledger.BattleUsed) : -1;
    }

    public int RemainingRoundUses(ActiveSkillData skill)
    {
        var ledger = Ledger(skill);
        return ledger.RoundLimit > 0 ? Math.Max(0, ledger.RoundLimit - ledger.RoundUsed) : -1;
    }

    public int CooldownRoundsRemaining(ActiveSkillData skill) => Math.Max(0, Ledger(skill).ReadyRound - Round);

    /// <summary>应对只待命至本回合结束；未触发则无费用、无次数消耗并清除。</summary>
    public void EndScene() => _armed.Clear();

    /// <summary>兼容旧协调器调用；现行生命周期在每回合结束时清理应对。</summary>
    public void EndCommonRound() => EndScene();

    public void ReplaceLayoutAtRoundBoundary(CombatBoardLayout layout)
    {
        if (Round > 0 && !TraversalComplete) throw new InvalidOperationException("尚未完成本轮遍历。");
        if (layout.size.X <= 0 || layout.size.Y <= 0) throw new ArgumentException("盘面尺寸无效。", nameof(layout));
        _layout = layout.CopyConfiguration(); RebuildCells();
        // 共享账本、念所在原技能身份、冷却及本轮标记均不因换盘被清空。
    }
}
