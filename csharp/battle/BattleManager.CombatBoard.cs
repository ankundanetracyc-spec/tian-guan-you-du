using Godot;
using System.Threading.Tasks;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleManager
{
    private sealed class BoardOwner(CombatBoardCatalog catalog, CombatBoardLayout layout)
    {
        public readonly CombatBoardCatalog Catalog = catalog;
        public readonly CombatBoardRoundState State = new(layout, catalog.FindSkill);
        public readonly Dictionary<string, ActiveSkillData> Resolved = [];
        public BoardSceneAction? PendingAction;
        public ActiveSkillData? Skill(CombatBoardPlacement item)
        {
            if (!Resolved.TryGetValue(item.placement_id, out var skill))
            {
                skill = Catalog.ResolvePlacement(State.Layout, item);
                if (skill is not null) Resolved[item.placement_id] = skill;
            }
            return skill;
        }
    }
    private bool _boardMode, _boardEditing, _boardRowRunning, _boardEditRequested;
    private bool _boardSelectingRow;
    private readonly Dictionary<GodotObject, BoardRowTargets> _boardRoundTargets = [];
    private readonly List<BoardTargetPrompt> _boardTargetPrompts = [];
    private readonly Dictionary<GodotObject, BoardOwner> _boards = [];
    private readonly Dictionary<GodotObject, TaskCompletionSource<bool>> _boardAwaiting = [];
    private BattleBoardHud? _boardHud;
    private static int _activeBattleScenes;
    private string _boardEntryRejectionReason = "";
    public override void _EnterTree() => _activeBattleScenes++;
    public override void _ExitTree()
    {
        battle_running = false; ResetCombatBoards();
        if (--_activeBattleScenes == 0) BattleUiArt.ReleaseGeneratedStyles();
    }
    public bool is_board_battle() => _boardMode;
    public string get_board_entry_rejection_reason() => _boardEntryRejectionReason;
    public bool is_board_editing() => _boardMode && _boardEditing;
    public bool is_board_edit_requested() => _boardEditRequested;
    public bool is_board_row_selection_pending() => _boardSelectingRow || _boardTargetPrompts.Count > 0;
    public int get_board_round() => _segmentNumber;
    public CombatBoardRoundState? get_board_state(GodotObject? owner) => owner is not null && _boards.TryGetValue(owner, out var board) ? board.State : null;
    public CombatBoardCatalog? get_board_catalog(GodotObject? owner) => owner is not null && _boards.TryGetValue(owner, out var board) ? board.Catalog : null;
    public ActiveSkillData? get_board_placement_skill(GodotObject owner, CombatBoardPlacement item) => _boards.TryGetValue(owner, out var board) ? board.Skill(item) : null;

    public bool is_board_zhou_tian_target(GodotObject owner, ActiveSkillData skill)
    {
        if (!_boardMode || !_boards.TryGetValue(owner, out var board)) return false;
        var indicator = board.State.Layout.placements.FirstOrDefault(item => item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator);
        var target = indicator is null ? null : CombatBoardGeometry.PointedSkill(board.State.Layout, indicator, board.Catalog.FindSkill);
        return target is not null && target.source_key == skill.get_board_skill_key() && !CardRules.IsResponse(skill);
    }

    private void ResetCombatBoards()
    {
        _boardMode = _boardEditing = _boardRowRunning = _boardEditRequested = _boardSelectingRow = false;
        ResetBoardSceneRuntime();
        ResetBoardStage();
        foreach (var completion in _boardAwaiting.Values) completion.TrySetResult(false);
        _boardAwaiting.Clear(); _boards.Clear(); _boardRoundTargets.Clear(); _boardTargetPrompts.Clear();
        if (GodotObject.IsInstanceValid(_planHud)) _planHud!.ProcessMode = ProcessModeEnum.Always;
        if (GodotObject.IsInstanceValid(_boardHud))
        { _boardHud!.ProcessMode = ProcessModeEnum.Disabled; _boardHud.Hide(); _boardHud.QueueFree(); }
        _boardHud = null;
    }

    private bool TryInitializeCombatBoards(out string failureReason)
    {
        failureReason = "";
        if (all_units.Count == 0 || all_units.Any(owner => owner is not Character and not Beast))
        {
            failureReason = "战场中存在尚未接入招式盘的单位。";
            return false;
        }
        var fixedBeastBoards = new Dictionary<Beast, (CombatBoardCatalog Catalog, CombatBoardLayout Layout)>();
        foreach (var beast in all_units.OfType<Beast>())
        {
            if (!TryPrepareFixedBeastBoard(beast, out var catalog, out var layout, out failureReason))
            {
                failureReason = $"{beast.entity_name}没有完整的固定招式盘，未进入战斗。{failureReason}";
                return false;
            }
            fixedBeastBoards.Add(beast, (catalog, layout));
        }
        _boardMode = true;
        foreach (var owner in all_units)
        {
            if (owner is Beast beast)
            {
                var fixedBoard = fixedBeastBoards[beast];
                _boards[owner] = new(fixedBoard.Catalog, fixedBoard.Layout);
                owner.SetMeta("combat_board_catalog", fixedBoard.Catalog);
                owner.SetMeta("combat_board_mode", true);
                (owner as Node)?.GetNodeOrNull<CanvasItem>("Bg/Label")?.Hide();
                StoreBoardLayout(owner, fixedBoard.Layout);
                continue;
            }
            var catalog = owner.HasMeta("combat_board_catalog") ? owner.GetMeta("combat_board_catalog").AsGodotObject() as CombatBoardCatalog : null;
            if (catalog is null || catalog.skills.Count == 0)
            {
                catalog = new();
                foreach (var skill in _combatSkills.GetValueOrDefault(owner, []).Concat(Character.PublicBasicDeckResource.get_deck_skills()
                    .Select(path => ResourceLoader.Load<ActiveSkillData>(path)).OfType<ActiveSkillData>()))
                {
                    if (!skill.has_board_definition() || catalog.FindSkill(skill.get_board_skill_key()) is not null) continue;
                    catalog.skills.Add(ResourceLoader.Load<ActiveSkillData>(skill.get_board_skill_key()));
                }
            }
            owner.SetMeta("combat_board_catalog", catalog);
            var layout = owner.HasMeta("combat_board_layout") ? owner.GetMeta("combat_board_layout").AsGodotObject() as CombatBoardLayout : null;
            var indicator = gongfa_combat.HasPower(owner, "玄枢含章");
            var realm = owner.Get("cultivation_realm").AsString();
            layout ??= CombatBoardUnlockRules.CreatePreview(_combatSkills.GetValueOrDefault(owner, [])
                .Select(skill => skill.combat_empty_slot ? "" : skill.get_board_skill_key()), [], catalog.FindSkill, indicator, realm);
            layout = CombatBoardUnlockRules.Normalize(layout, realm, catalog.FindSkill);
            layout = layout.CopyConfiguration();
            if (!indicator)
                layout.placements = new(layout.placements.Where(item => item.kind != CombatBoardPlacement.ObjectKind.ZhouTianIndicator));
            else if (!layout.placements.Any(item => item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator))
            {
                var occupied = layout.placements.SelectMany(item => CombatBoardGeometry.Cells(item, CombatBoardGeometry.Footprint(item, catalog.FindSkill))).ToHashSet();
                var empty = Enumerable.Range(0, layout.size.Y).SelectMany(y => Enumerable.Range(0, layout.size.X).Select(x => new Vector2I(x, y))).Where(cell => !occupied.Contains(cell)).ToArray();
                if (empty.Length > 0) layout.placements.Add(new() { placement_id = "heart:xuan_shu:indicator", source_key = "heart:xuan_shu",
                    kind = CombatBoardPlacement.ObjectKind.ZhouTianIndicator, locked = true, cell = empty[0] });
            }
            // 资格变化时保留原布局供玩家修正；自动单位只移除失效来源，不生成新技能。
            if (owner != player_units.FirstOrDefault() || _spectatorBattle)
            {
                var invalid = CombatBoardGeometry.Validate(layout, catalog.FindSkill, key => catalog.FindSkill(key) is not null,
                    key => catalog.FindRewrite(key)?.max_placements ?? 0, indicator,
                    key => owner is Character actor ? actor.get_inventory_total_count(key) : 0);
                if (invalid.Count > 0)
                    layout = CombatBoardMigration.CreatePreview(_combatSkills.GetValueOrDefault(owner, [])
                        .Select(skill => skill.combat_empty_slot ? "" : skill.get_board_skill_key()), [], catalog.FindSkill, indicator);
                layout.confirmed = true;
            }
            _boards[owner] = new(catalog, layout);
            owner.SetMeta("combat_board_mode", true);
            (owner as Node)?.GetNodeOrNull<CanvasItem>("Bg/Label")?.Hide();
            StoreBoardLayout(owner, layout);
        }
        _mainPlayerUnit = player_units.FirstOrDefault(Alive); active_unit = _mainPlayerUnit;
        RefreshBoardStage();
        _segmentPhase = SegmentPhase.Planning; _boardEditing = !_spectatorBattle;
        _skillBar?.Hide(); _planHud?.Hide(); _statusPanel?.Hide();
        if (_planHud is not null) _planHud.ProcessMode = ProcessModeEnum.Disabled;
        if (!_spectatorBattle && GetParent()?.GetNodeOrNull<CanvasLayer>("UILayer") is { } layer)
        {
            _boardHud = new(); layer.AddChild(_boardHud); _boardHud.Setup(this);
        }
        if (_spectatorBattle) BeginBoardRound();
        return true;
    }

    private bool TryPrepareFixedBeastBoard(Beast beast, out CombatBoardCatalog catalog, out CombatBoardLayout layout, out string reason)
    {
        var sourceLayout = beast.HasMeta("combat_board_layout")
            ? beast.GetMeta("combat_board_layout").AsGodotObject() as CombatBoardLayout
            : beast.get_fixed_combat_board_layout();
        return BeastCombatBoardRules.TryPrepare(sourceLayout, _combatSkills.GetValueOrDefault(beast, []),
            out catalog, out layout, out reason);
    }

    private static void StoreBoardLayout(GodotObject owner, CombatBoardLayout layout)
    {
        owner.SetMeta("combat_board_layout", layout.CopyConfiguration());
        if (owner is Character actor && actor.entity_data is HumanData human)
            human.combat_board_layout = layout.CopyConfiguration();
    }

    public bool commit_player_board(CombatBoardLayout draft, out string reason)
    {
        reason = "";
        if (!_boardEditing || _mainPlayerUnit is null || !_boards.TryGetValue(_mainPlayerUnit, out var board))
        { reason = "请等待共同回合结束后整理。"; return false; }
        var issues = CombatBoardGeometry.Validate(draft, board.Catalog.FindSkill,
            key => board.Catalog.FindSkill(key) is not null, key => board.Catalog.FindRewrite(key)?.max_placements ?? 0,
            gongfa_combat.HasPower(_mainPlayerUnit, "玄枢含章"),
            key => _mainPlayerUnit is Character actor ? actor.get_inventory_total_count(key) : 0);
        if (draft.size != board.State.Layout.size
            || !draft.unlocked_cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X)
                .SequenceEqual(board.State.Layout.unlocked_cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X)))
        { reason = "盘面容量或已解锁格位与当前角色不符。"; return false; }
        if (issues.Count > 0) { reason = issues[0].Message; return false; }
        draft.confirmed = true;
        board.State.ReplaceLayoutAtRoundBoundary(draft); board.Resolved.Clear();
        StoreBoardLayout(_mainPlayerUnit, draft);
        _boardEditing = _boardEditRequested = false;
        _boardHud?.CloseEditor();
        BeginBoardRound(); return true;
    }

    public void request_board_edit()
    {
        if (!_boardMode || !battle_running) return;
        if (_boardEditing) _boardHud?.OpenEditor();
    }

    private void BeginBoardRound()
    {
        if (!battle_running || !_boardMode) return;
        _segmentNumber++; _segmentPhase = SegmentPhase.Executing; _retreatAttemptedThisRound = false;
        BeginBoardSceneRound();
        _queuedHandledTargets.Clear(); _pendingZhouTianAutoCasts.Clear(); _boardRoundTargets.Clear(); _boardTargetPrompts.Clear();
        _boardSelectingRow = false;
        foreach (var owner in all_units.Where(Alive))
        {
            if (owner is Creature creature) creature.battle_round_id = _segmentNumber;
            _boards[owner].PendingAction = null;
            _boards[owner].State.BeginChoosingRound(_segmentNumber);
            gongfa_combat.BeginRound(owner);
            action_gauges[owner] = 0f;
            foreach (var circuit in _formationCircuits.GetValueOrDefault(owner, [])) circuit.begin_owner_turn();
            owner.Set("current_stamina", Math.Min(owner.Get("max_stamina").AsInt32(), owner.Get("current_stamina").AsInt32() + 3));
            InitializeOrClampBattleMana(owner);
            if (owner.HasMethod("process_status_event")) owner.Call("process_status_event", "turn_start", new GDictionary { ["battle_manager"] = this, ["unit"] = owner });
            InitializeOrClampBattleMana(owner);
        }
        foreach (var owner in all_units.Where(Alive))
        {
            if (owner == _mainPlayerUnit && !_spectatorBattle && !IsBoardDazedInScene(owner, _segmentNumber)) continue;
            SelectAutomaticBoardRow(owner);
        }
        if (!_spectatorBattle && GodotObject.IsInstanceValid(_mainPlayerUnit) && Alive(_mainPlayerUnit)
            && !IsBoardDazedInScene(_mainPlayerUnit!, _segmentNumber))
        {
            _boardSelectingRow = true;
            _boardHud?.ShowRowSelection();
        }
        else StartBoardRoundResolution();
    }

    private void ProcessCombatBoards(float frames)
    {
        if (_boardEditing || _boardSelectingRow || _boardRowRunning || _resolvingUnit is not null || CheckBattleOver()) return;
        StartBoardRoundResolution();
    }

    private void AdvanceBoardTime(float frames)
    {
        if (frames <= 0) return;
        _battleVisualElapsedSeconds += frames / SimulationFps; UpdateShieldVisualClocks();
        foreach (var owner in all_units.Where(Alive))
        {
            var actionFrames = owner is Creature creature ? creature.advance_realtime_status_timers(frames, SimulationFps, "僵直") : frames;
            if (_boards[owner].State.TraversalComplete || actionFrames <= 0) continue;
            action_gauges[owner] = Math.Min(ActionThreshold, action_gauges[owner].AsSingle() + Initiative(owner) * actionFrames / SimulationFps);
        }
    }

    private bool CanBoardPay(GodotObject owner, ActiveSkillData skill) => skill.skill_name != "寄念"
        && gongfa_combat.CanPlay(owner, skill) && owner.Get("current_stamina").AsInt32() >= get_skill_energy_cost(owner, skill)
        && can_pay_skill_mana_cost(owner, skill);

    private bool BoardSkillHasEligibleEffectTarget(GodotObject owner, ActiveSkillData skill)
    {
        foreach (var effect in skill.special_effects)
        {
            if (effect.GetValueOrDefault("effect", "").AsString() != "forest_bei_pack_call") continue;
            return PackWolves(owner).Any(wolf => wolf.get_status_stacks("逐猎") < 3);
        }
        return true;
    }

    private async Task<bool> ExecuteBoardPlacement(GodotObject owner, CombatBoardPlacement item, bool zhouTian = false)
    {
        if (!_boards.TryGetValue(owner, out var board) || !board.State.CanUsePlacement(item, zhouTian)
            || board.Skill(item) is not { } skill || !BoardSkillHasEligibleEffectTarget(owner, skill) || !CanBoardPay(owner, skill)
            || skill.skill_name == "复命" && !CanBeginBoardReplay(owner, skill)) return false;
        var target = ResolveBoardActionTarget(owner, skill);
        if (!Alive(target) || !IsValidTarget(skill, owner, target)) return false;
        if (!board.State.RecordSuccessfulCast(item, zhouTian)) return false;
        Pay(owner, skill); gongfa_combat.Before(owner, skill); _queuedHandledTargets.Clear();
        BeginBoardPressureAction(owner, skill, CombatBoardGeometry.Footprint(item, board.Catalog.FindSkill).X * CombatBoardGeometry.Footprint(item, board.Catalog.FindSkill).Y,
            get_skill_energy_cost(owner, skill));
        ShowBoardActionCue(owner, skill);
        var completion = new TaskCompletionSource<bool>(); _boardAwaiting[owner] = completion;
        if (skill.aoe_range > 0 || skill.description.Contains("全体"))
            _executor!.execute_aoe_skill(owner, skill, Vector2I.Zero, Map ?? this, false, !zhouTian, zhouTian);
        else _executor!.execute_skill(owner, skill, target!, false, !zhouTian, zhouTian);
        await completion.Task;
        CompleteBoardPressureAction(owner);
        ReleaseForgedSpirit(owner);
        while (IsInsideTree() && battle_running && (_battlePaused || HasPendingActionAnimations()))
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }

    private bool CompleteBoardCast(GodotObject owner)
    {
        if (!_boardMode) return false;
        if (_boardAwaiting.Remove(owner, out var completion)) completion.TrySetResult(true);
        return true;
    }

    private async Task DrainBoardZhouTian()
    {
        foreach (var owner in all_units.Where(Alive))
        {
            if (!_pendingZhouTianAutoCasts.TryGetValue(owner, out var pending) || pending <= 0) continue;
            if (IsBoardDazedInScene(owner, _segmentNumber))
            {
                // 失神跳过本回合与下一回合全部行动；待施周天也是行动，不跨回合补放。
                _pendingZhouTianAutoCasts.Remove(owner);
                continue;
            }
            _pendingZhouTianAutoCasts.Remove(owner);
            if (!_boards.TryGetValue(owner, out var board)) continue;
            var pointer = board.State.Layout.placements.FirstOrDefault(item => item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator);
            if (pointer is null) continue;
            var target = CombatBoardGeometry.PointedSkill(board.State.Layout, pointer, board.Catalog.FindSkill);
            if (target is null || board.Catalog.FindSkill(target.source_key) is not { } skill || CardRules.IsResponse(skill)) continue;
            _runningZhouTianAutoCasts.Add(owner);
            try { await ExecuteBoardPlacement(owner, target, true); }
            finally { _runningZhouTianAutoCasts.Remove(owner); }
        }
    }

    private bool CanBeginBoardReplay(GodotObject owner, ActiveSkillData fuming)
    {
        var copied = gongfa_combat.GetLastNonResponseSkill(owner);
        if (copied is null || copied.skill_name is "复命" or "寄念" || CardRules.IsResponse(copied)
            || !_boards.TryGetValue(owner, out var board) || board.State.FindReplayOpportunity(copied) is null
            || !gongfa_combat.CanPlay(owner, copied) || !HasLegalSkillTarget(owner, copied)) return false;
        var (energy, mana, capacity) = gongfa_combat.PreviewBoardSequentialCosts(owner, fuming, copied);
        var manaShortfall = Math.Max(0, mana - Math.Max(0, owner.Get("current_mana").AsInt32()));
        return owner.Get("current_stamina").AsInt32() >= energy
            && gongfa_combat.CanBurnManaCapacityForCost(owner, manaShortfall + capacity);
    }

    private bool BeginBoardReplay(GodotObject owner, ActiveSkillData fuming, bool zhouTian)
    {
        var copied = gongfa_combat.GetLastNonResponseSkill(owner);
        gongfa_combat.AfterSkillResolved(owner, fuming);
        var opportunity = copied is not null ? _boards[owner].State.FindReplayOpportunity(copied) : null;
        var target = copied is not null ? ResolveBoardActionTarget(owner, copied) : null;
        if (copied is null || opportunity is null || !Alive(target) || !CanBoardPay(owner, copied))
        { CompleteBoardCast(owner); return false; }
        var replay = (ActiveSkillData)copied.Duplicate(true);
        if (!_boards[owner].State.RecordReplayUse(opportunity)) { CompleteBoardCast(owner); return false; }
        Pay(owner, replay); gongfa_combat.Before(owner, replay, recordAsLastCast: false);
        BeginBoardPressureAction(owner, replay, Mathf.Max(1, replay.board_size.X * replay.board_size.Y), get_skill_energy_cost(owner, replay));
        ShowBoardActionCue(owner, replay);
        if (replay.aoe_range > 0 || replay.description.Contains("全体"))
            _executor!.execute_aoe_skill(owner, replay, Vector2I.Zero, Map ?? this, false, !zhouTian, zhouTian);
        else _executor!.execute_skill(owner, replay, target!, false, !zhouTian, zhouTian);
        return true;
    }

    private bool TryBoardResponse(GodotObject defender, GodotObject attacker, string damageType, ActiveSkillData? incoming)
    {
        if (!Alive(defender) || !Alive(attacker) || _queuedHandledTargets.Contains(defender.GetInstanceId())
            || IsBoardDazedInScene(defender, _segmentNumber)
            || incoming is not null && gongfa_combat.PreventsResponse(attacker, incoming) || !_boards.TryGetValue(defender, out var board)) return false;
        foreach (var placed in board.State.ArmedInOrder())
        {
            var skill = board.Skill(placed);
            if (skill is null || !CardRules.ResponseMatches(skill, damageType) || !CanBoardPay(defender, skill)) continue;
            if (!board.State.RecordSuccessfulCast(placed)) return false;
            _queuedHandledTargets.Add(defender.GetInstanceId());
            Pay(defender, skill); gongfa_combat.Before(defender, skill); gongfa_combat.OnResponseTriggered(defender, skill);
            ShowBoardActionCue(defender, skill);
            if (gongfa_combat.Utility(defender, skill)) _executor?.execute_counter_extras(defender, attacker, skill);
            else _executor?.execute_segment_response(defender, attacker, skill);
            return true;
        }
        return false;
    }

    private void EndBoardRound()
    {
        _boardSelectingRow = false;
        foreach (var board in _boards.Values) board.State.EndCommonRound();
        CompleteBoardSceneRound();
        foreach (var owner in all_units.OfType<Creature>().Where(GodotObject.IsInstanceValid))
            owner.process_status_event("turn_end", new GDictionary { ["battle_manager"] = this, ["unit"] = owner });
        if (CheckBattleOver()) return;
        if (!_spectatorBattle)
        {
            _boardEditing = true; _segmentPhase = SegmentPhase.Planning;
            _boardHud?.BeginEditing();
        }
        else BeginBoardRound();
    }
}
