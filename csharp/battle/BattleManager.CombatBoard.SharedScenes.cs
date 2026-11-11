using Godot;

public partial class BattleManager
{
    private sealed record BoardSceneAction(GodotObject Owner, CombatBoardPlacement Placement,
        ActiveSkillData? Skill, CombatBoardRoundState State, BoardActionKind Kind, int Column, int OwnerOrder, int Sequence, float Speed);
    private enum BoardActionKind { Skill, Response, Talisman, Pill }
    private enum BoardTargetKind { Enemy, Ally, Any }
    private sealed record BoardTargetPrompt(BoardTargetKind Kind, GodotObject[] Options);
    private sealed class BoardRowTargets
    {
        public readonly HashSet<BoardTargetKind> Locked = [];
        public readonly Dictionary<BoardTargetKind, GodotObject?> Selected = [];
        public void Set(BoardTargetKind kind, GodotObject? target) { Locked.Add(kind); Selected[kind] = target; }
        public GodotObject? Get(BoardTargetKind kind) => Locked.Contains(kind) ? Selected.GetValueOrDefault(kind) : null;
    }

    private const int BoardResolveBase = 60;
    private readonly Dictionary<GodotObject, int> _boardResolveCurrent = [];
    private readonly Dictionary<GodotObject, int> _boardResolveMax = [];
    private readonly Dictionary<GodotObject, int> _boardDazedThroughRound = [];
    private readonly Dictionary<GodotObject, int> _boardPressureDamage = [];
    private GodotObject? _boardPressureOwner;
    private ActiveSkillData? _boardPressureSkill;
    private float _boardPressureCoefficient;
    private bool _boardPressureActive;

    public int get_board_scene_number() => _mainPlayerUnit is not null && _boards.TryGetValue(_mainPlayerUnit, out var board)
        ? board.State.SelectedRow + 1 : 0;
    public int get_board_scene_count(GodotObject? owner = null)
    {
        if (owner is not null && _boards.TryGetValue(owner, out var board)) return board.State.Layout.size.Y;
        return _boards.Values.Select(board => board.State.Layout.size.Y).DefaultIfEmpty(0).Max();
    }
    public int get_board_resolve_current(GodotObject? owner) => owner is not null ? _boardResolveCurrent.GetValueOrDefault(owner) : 0;
    public int get_board_resolve_max(GodotObject? owner) => owner is not null ? _boardResolveMax.GetValueOrDefault(owner) : 0;
    public bool is_board_dazed(GodotObject? owner) => owner is not null && IsBoardDazedInScene(owner, _segmentNumber);

    private void BeginBoardSceneRound()
    {
        foreach (var owner in _boardDazedThroughRound.Where(pair => pair.Value < _segmentNumber).Select(pair => pair.Key).ToArray())
            _boardDazedThroughRound.Remove(owner);
        foreach (var owner in all_units.Where(Alive)) InitializeBoardResolve(owner);
        ClearBoardPressureAction();
    }

    private void InitializeBoardResolve(GodotObject owner)
    {
        var physique = Mathf.Max(0, CardRules.Number(owner, "physique"));
        var intelligence = Mathf.Max(0, CardRules.Number(owner, "intelligence"));
        var soul = Mathf.Max(0, CardRules.Number(owner, "soul"));
        var coordination = Mathf.Min(physique, soul);
        var maximum = Mathf.Max(1, Mathf.RoundToInt(BoardResolveBase + 4 * soul + 2 * intelligence + 2 * coordination));
        _boardResolveMax[owner] = maximum;
        // 定力每回合回满；已经开始的失神时限独立保留至下一回合结束。
        _boardResolveCurrent[owner] = maximum;
    }

    private void CompleteBoardSceneRound()
    {
        foreach (var owner in _boardDazedThroughRound.Where(pair => pair.Value <= _segmentNumber).Select(pair => pair.Key).ToArray())
            _boardDazedThroughRound.Remove(owner);
        ClearBoardPressureAction();
    }

    private void ResetBoardSceneRuntime()
    {
        _boardResolveCurrent.Clear();
        _boardResolveMax.Clear();
        _boardDazedThroughRound.Clear();
        ClearBoardPressureAction();
    }

    private void StartBoardRoundResolution()
    {
        if (!battle_running || !_boardMode || _boardEditing || _boardSelectingRow || _boardTargetPrompts.Count > 0
            || _boardRowRunning || _resolvingUnit is not null || CheckBattleOver()) return;
        _boardRowRunning = true;
        _ = ResolveBoardRound();
    }

    private async Task ResolveBoardRound()
    {
        var ownerOrder = all_units.Select((owner, index) => (owner, index)).ToDictionary(item => item.owner, item => item.index);
        var sequence = 0;
        try
        {
            foreach (var owner in all_units.Where(Alive))
            {
                if (!_boards.TryGetValue(owner, out var board) || board.State.SelectedRow < 0) continue;
                board.PendingAction = IsBoardDazedInScene(owner, _segmentNumber)
                    ? null : ReadNextBoardAction(owner, board, ownerOrder.GetValueOrDefault(owner), ref sequence);
                if (IsBoardDazedInScene(owner, _segmentNumber)) board.State.SkipSelectedRow();
            }

            while (battle_running && IsInsideTree())
            {
                var action = _boards.Where(pair => pair.Value.PendingAction is not null && Alive(pair.Key))
                    .Select(pair => pair.Value.PendingAction!)
                    .OrderByDescending(item => item.Speed)
                    .ThenBy(item => item.OwnerOrder).ThenBy(item => item.Column).ThenBy(item => item.Sequence)
                    .FirstOrDefault();
                if (action is null) break;
                if (!_boards.TryGetValue(action.Owner, out var ownerBoard)) continue;
                ownerBoard.PendingAction = null;
                if (!Alive(action.Owner)) continue;
                _resolvingUnit = action.Owner;

                if (IsBoardDazedInScene(action.Owner, _segmentNumber))
                {
                    action.State.MarkNormalCheck(action.Placement);
                    action.State.SkipSelectedRow();
                    continue;
                }

                switch (action.Kind)
                {
                    case BoardActionKind.Response:
                        if (action.Skill is { } response && action.State.Arm(action.Placement)) ShowBoardActionCue(action.Owner, response);
                        break;
                    case BoardActionKind.Skill:
                        if (action.Skill is not null) await ExecuteBoardPlacement(action.Owner, action.Placement);
                        action.State.MarkNormalCheck(action.Placement);
                        break;
                    case BoardActionKind.Talisman:
                        await ExecuteBoardTalisman(action.Owner, action.Placement);
                        action.State.MarkNormalCheck(action.Placement);
                        break;
                    case BoardActionKind.Pill:
                        if (action.Owner is Character actor
                            && actor.TryUseBoardPill(action.Placement.source_key, get_effective_mana_capacity(action.Owner), out _))
                        {
                            action.Placement.expended = true;
                            if (BattleSupplyRules.Load(action.Placement.source_key) is { } pill)
                                ShowBoardActionCue(action.Owner, pill.entity_name, pill.icon_path);
                            _executor?.present_recovery(action.Owner);
                            StoreBoardLayout(action.Owner, action.State.Layout);
                            while (battle_running && (_battlePaused || HasPendingActionAnimations()))
                                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        }
                        action.State.MarkNormalCheck(action.Placement);
                        break;
                }

                await DrainBoardZhouTian();
                if (player_units.All(unit => !Alive(unit)) || enemy_units.All(unit => !Alive(unit))) break;

                if (IsBoardDazedInScene(action.Owner, _segmentNumber))
                {
                    action.State.SkipSelectedRow();
                    continue;
                }
                if (Alive(action.Owner))
                    ownerBoard.PendingAction = ReadNextBoardAction(action.Owner, ownerBoard,
                        ownerOrder.GetValueOrDefault(action.Owner), ref sequence);
            }

            foreach (var board in _boards.Values) board.State.EndScene();
        }
        catch (Exception error)
        {
            GD.PushError($"灵台回合执行失败：{error}");
            battle_running = false;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_mainPlayerUnit)) unitOverlay?.ClearTargetFrames();
            foreach (var board in _boards.Values) board.PendingAction = null;
            _resolvingUnit = null;
            _boardRowRunning = false;
            if (battle_running && !CheckBattleOver()) EndBoardRound();
        }
    }

    private BoardSceneAction? ReadNextBoardAction(GodotObject owner, BoardOwner board, int ownerOrder, ref int sequence)
    {
        while (board.State.TryReadNextCell(out var visit))
        {
            if (visit.Placement is null)
            {
                if (Alive(owner)) RefreshStamina(owner);
                continue;
            }
            if (!visit.FirstCheck) continue;
            if (visit.Placement.kind == CombatBoardPlacement.ObjectKind.Pill)
            {
                if (!visit.Placement.expended && owner is Character)
                    return new(owner, visit.Placement, null, board.State, BoardActionKind.Pill,
                        visit.Cell.X, ownerOrder, sequence++, Initiative(owner));
                continue;
            }
            if (visit.Placement.kind == CombatBoardPlacement.ObjectKind.Talisman)
            {
                var talismanSkill = TalismanCombatCatalog.Load(visit.Placement.source_key) is { } product
                    ? TalismanCombatCatalog.Skill(product) : null;
                return new(owner, visit.Placement, talismanSkill, board.State, BoardActionKind.Talisman,
                    visit.Cell.X, ownerOrder, sequence++, talismanSkill is null ? Initiative(owner)
                        : BoardActionSpeed(owner, talismanSkill, 1, TalismanCombatCatalog.EnergyCost));
            }
            if (visit.Placement.kind != CombatBoardPlacement.ObjectKind.Skill || visit.Skill is null) continue;
            var kind = CardRules.IsResponse(visit.Skill) ? BoardActionKind.Response : BoardActionKind.Skill;
            var footprint = CombatBoardGeometry.Footprint(visit.Placement, board.Catalog.FindSkill);
            var rowCellCount = Math.Max(1, CombatBoardGeometry.Cells(visit.Placement, footprint)
                .Count(cell => cell.Y == visit.Cell.Y));
            return new(owner, visit.Placement, visit.Skill, board.State, kind, visit.Cell.X, ownerOrder, sequence++,
                BoardActionSpeed(owner, visit.Skill, rowCellCount, get_skill_energy_cost(owner, visit.Skill)));
        }
        return null;
    }

    private float BoardActionSpeed(GodotObject owner, ActiveSkillData skill, int occupiedCellsInSelectedRow, int energyCost) =>
        Initiative(owner) * skill.get_board_tempo_multiplier(occupiedCellsInSelectedRow, energyCost)
        + skill.get_board_speed_affinity_bonus(owner);

    private GodotObject? ResolveBoardActionTarget(GodotObject owner, ActiveSkillData skill)
    {
        if (skill.target_type == 0 || skill.target_type == 1 && !IsExplicitAllyTarget(skill) || CardRules.IsResponse(skill)) return owner;
        if (IsAutomaticBoardTarget(skill)) return ChooseRandomLegalBoardTarget(owner, skill);
        var kind = ManualBoardTargetKind(skill);
        if (kind is null) return ChooseRandomLegalBoardTarget(owner, skill);
        var candidates = LegalBoardTargets(owner, skill).ToArray();
        if (_boardRoundTargets.TryGetValue(owner, out var selected) && selected.Locked.Contains(kind.Value))
        {
            var target = selected.Get(kind.Value);
            return Alive(target) && IsValidTarget(skill, owner, target) ? target : null;
        }
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool IsAutomaticBoardTarget(ActiveSkillData skill) => skill.aoe_range > 0
        || skill.description.Contains("全体") || skill.description.Contains("随机");

    private static bool IsExplicitAllyTarget(ActiveSkillData skill) => skill.description.Contains("友方")
        || skill.description.Contains("队友") || skill.description.Contains("盟友");

    private static BoardTargetKind? ManualBoardTargetKind(ActiveSkillData skill)
    {
        if (CardRules.IsResponse(skill) || IsAutomaticBoardTarget(skill)) return null;
        return skill.target_type switch
        {
            2 => BoardTargetKind.Enemy,
            1 when IsExplicitAllyTarget(skill) => BoardTargetKind.Ally,
            3 => BoardTargetKind.Any,
            _ => null
        };
    }

    private GodotObject[] LegalBoardTargets(GodotObject owner, ActiveSkillData skill) =>
        get_target_candidates().Where(Alive).Where(target => IsValidTarget(skill, owner, target)).ToArray();

    private GodotObject? ChooseRandomLegalBoardTarget(GodotObject owner, ActiveSkillData skill)
    {
        var candidates = LegalBoardTargets(owner, skill);
        return candidates.Length == 0 ? null : candidates[(int)(GD.Randi() % (uint)candidates.Length)];
    }

    private List<(CombatBoardPlacement Placement, ActiveSkillData Skill, int Column)> BoardRowSkills(GodotObject owner, int row)
    {
        if (!_boards.TryGetValue(owner, out var board)) return [];
        var result = new List<(CombatBoardPlacement, ActiveSkillData, int)>();
        foreach (var placement in board.State.Layout.placements)
        {
            ActiveSkillData? skill = placement.kind switch
            {
                CombatBoardPlacement.ObjectKind.Skill => board.Skill(placement),
                CombatBoardPlacement.ObjectKind.Talisman when TalismanCombatCatalog.Load(placement.source_key) is { } product
                    => TalismanCombatCatalog.Skill(product),
                _ => null
            };
            if (skill is null) continue;
            var footprint = CombatBoardGeometry.Footprint(placement, board.Catalog.FindSkill);
            var columns = CombatBoardGeometry.Cells(placement, footprint).Where(cell => cell.Y == row).Select(cell => cell.X).ToArray();
            if (columns.Length > 0) result.Add((placement, skill, columns.Min()));
        }
        return result.OrderBy(item => item.Item3).ToList();
    }

    private void BuildBoardRowTargets(GodotObject owner, int row, bool allowPlayerChoice)
    {
        var targets = new BoardRowTargets();
        _boardRoundTargets[owner] = targets;
        var skills = BoardRowSkills(owner, row).Select(item => item.Skill).ToList();
        if (skills.Any(skill => skill.skill_name == "复命") && gongfa_combat.GetLastNonResponseSkill(owner) is { } replay)
            skills.Add(replay);
        foreach (var group in skills.Select(skill => (Skill: skill, Kind: ManualBoardTargetKind(skill)))
                     .Where(item => item.Kind.HasValue).GroupBy(item => item.Kind!.Value))
        {
            var kind = group.Key;
            var groupSkills = group.Select(item => item.Skill).ToArray();
            var candidates = LegalBoardTargets(owner, groupSkills[0]).AsEnumerable();
            foreach (var skill in groupSkills.Skip(1))
                candidates = candidates.Intersect(LegalBoardTargets(owner, skill));
            var legal = candidates.ToArray();
            if (legal.Length == 0) { targets.Set(kind, null); continue; }
            if (legal.Length == 1) { targets.Set(kind, legal[0]); continue; }
            if (allowPlayerChoice)
            {
                _boardTargetPrompts.Add(new(kind, legal));
                continue;
            }
            targets.Set(kind, legal[(int)(GD.Randi() % (uint)legal.Length)]);
        }
    }

    private int ChooseAutomaticBoardRow(GodotObject owner)
    {
        if (!_boards.TryGetValue(owner, out var board)) return 0;
        var scored = new List<(int Row, int Score)>();
        for (var row = 0; row < board.State.Layout.size.Y; row++)
        {
            var occupied = new HashSet<Vector2I>();
            foreach (var placement in board.State.Layout.placements)
                foreach (var cell in CombatBoardGeometry.Cells(placement, CombatBoardGeometry.Footprint(placement, board.Catalog.FindSkill)))
                    occupied.Add(cell);
            var empty = Enumerable.Range(0, board.State.Layout.size.X).Count(x => !occupied.Contains(new(x, row))
                && (board.State.Layout.unlocked_cells.Count == 0 || board.State.Layout.unlocked_cells.Contains(new(x, row))));
            var score = empty;
            foreach (var (placement, skill, _) in BoardRowSkills(owner, row))
            {
                if (CardRules.IsResponse(skill))
                {
                    if (board.State.CanUseDefinition(skill)) score += 2;
                    continue;
                }
                if (placement.kind == CombatBoardPlacement.ObjectKind.Talisman)
                {
                    if (owner is not Character actor || actor.get_inventory_total_count(placement.source_key) <= 0
                        || owner.Get("current_stamina").AsInt32() < TalismanCombatCatalog.EnergyCost) continue;
                }
                else if (!CanBoardPay(owner, skill) || !BoardSkillHasEligibleEffectTarget(owner, skill)) continue;
                var targetKind = ManualBoardTargetKind(skill);
                if (targetKind is not null && LegalBoardTargets(owner, skill).Length == 0) continue;
                var target = LegalBoardTargets(owner, skill).FirstOrDefault();
                var damage = target is not null && skill.target_type == 2
                    ? Mathf.Max(0, Mathf.RoundToInt(BattleActionResolver.preview_damage(owner, skill))) : 0;
                score += 10 + damage;
            }
            scored.Add((row, score));
        }
        if (scored.Count == 0) return 0;
        var best = scored.Max(item => item.Score);
        var options = scored.Where(item => item.Score == best).Select(item => item.Row).ToArray();
        return options[(int)(GD.Randi() % (uint)options.Length)];
    }

    private void SelectAutomaticBoardRow(GodotObject owner)
    {
        if (!_boards.TryGetValue(owner, out var board)) return;
        var row = ChooseAutomaticBoardRow(owner);
        if (!board.State.SelectRow(row)) return;
        BuildBoardRowTargets(owner, row, false);
    }

    public bool select_board_row(int row)
    {
        if (!_boardSelectingRow || _mainPlayerUnit is null || !_boards.TryGetValue(_mainPlayerUnit, out var board)
            || !board.State.SelectRow(row)) return false;
        _boardSelectingRow = false;
        BuildBoardRowTargets(_mainPlayerUnit, row, true);
        _boardHud?.RefreshTurnChoicePanel();
        if (_boardTargetPrompts.Count == 0) StartBoardRoundResolution();
        return true;
    }

    public bool select_board_target(GodotObject? target)
    {
        if (_boardTargetPrompts.Count == 0 || !GodotObject.IsInstanceValid(target)
            || _mainPlayerUnit is null || !_boardRoundTargets.TryGetValue(_mainPlayerUnit, out var selection)) return false;
        var prompt = _boardTargetPrompts[0];
        if (!prompt.Options.Contains(target!)) return false;
        selection.Set(prompt.Kind, target);
        _boardTargetPrompts.RemoveAt(0);
        _boardHud?.RefreshTurnChoicePanel();
        if (_boardTargetPrompts.Count == 0) StartBoardRoundResolution();
        return true;
    }

    public int get_board_row_choice_count() => _mainPlayerUnit is not null && _boards.TryGetValue(_mainPlayerUnit, out var board)
        ? board.State.Layout.size.Y : 0;
    public bool is_board_choosing_row() => _boardSelectingRow;
    public string get_board_target_prompt() => _boardTargetPrompts.Count == 0 ? ""
        : _boardTargetPrompts[0].Kind switch
        { BoardTargetKind.Enemy => "选择本行共用的敌方目标", BoardTargetKind.Ally => "选择本行共用的友方目标", _ => "选择本行招式的目标" };
    public Godot.Collections.Array<GodotObject> get_board_target_choices() => _boardTargetPrompts.Count == 0
        ? [] : new(_boardTargetPrompts[0].Options);
    public string get_enemy_board_target_intents()
    {
        var lines = new List<string>();
        foreach (var enemy in enemy_units.Where(Alive))
        {
            if (!_boardRoundTargets.TryGetValue(enemy, out var targets)) continue;
            var selected = targets.Selected.Values.Where(Alive).Distinct().ToArray();
            foreach (var target in selected) lines.Add($"{enemy.Get("entity_name").AsString()} → {target!.Get("entity_name").AsString()}");
        }
        return string.Join("　", lines);
    }

    private bool IsBoardDazedInScene(GodotObject owner, int round) =>
        _boardDazedThroughRound.TryGetValue(owner, out var through) && round <= through;

    private void BeginBoardPressureAction(GodotObject owner, ActiveSkillData skill, int footprintArea, int energyCost)
    {
        _boardPressureDamage.Clear();
        _boardPressureOwner = owner;
        _boardPressureSkill = skill;
        _boardPressureCoefficient = skill.get_composure_pressure_coefficient(footprintArea, energyCost);
        _boardPressureActive = true;
    }

    public void RecordBoardPreShieldDamage(GodotObject attacker, GodotObject target, int damage)
    {
        if (!_boardMode || !_boardPressureActive || attacker != _boardPressureOwner || damage <= 0 || target is ArtifactBody) return;
        _boardPressureDamage[target] = _boardPressureDamage.GetValueOrDefault(target) + damage;
    }

    private void CompleteBoardPressureAction(GodotObject owner)
    {
        if (!_boardPressureActive || owner != _boardPressureOwner) return;
        var coefficient = _boardPressureCoefficient;
        var impacts = _boardPressureDamage.ToArray();
        ClearBoardPressureAction();
        foreach (var (target, damage) in impacts)
        {
            if (!GodotObject.IsInstanceValid(target) || !_boardResolveCurrent.TryGetValue(target, out var current) || current <= 0) continue;
            var pressure = Mathf.RoundToInt(damage * coefficient);
            if (pressure <= 0) continue;
            var actual = Math.Min(current, pressure);
            _boardResolveCurrent[target] = current - actual;
            _executor?.show_board_resolve_loss(target, actual);
            if (_boardResolveCurrent[target] > 0) continue;
            if (IsBoardDazedInScene(target, _segmentNumber)) continue;
            _boardDazedThroughRound[target] = _segmentNumber + 1;
            _executor?.show_board_dazed(target);
        }
    }

    private void ClearBoardPressureAction()
    {
        _boardPressureActive = false;
        _boardPressureOwner = null;
        _boardPressureSkill = null;
        _boardPressureCoefficient = 0;
        _boardPressureDamage.Clear();
    }

    private void ShowBoardActionCue(GodotObject owner, ActiveSkillData skill) =>
        ShowBoardActionCue(owner, skill.skill_name, skill.icon_path);

    private void ShowBoardActionCue(GodotObject owner, string name, string iconPath)
    {
        if (_spectatorBattle || owner is not Node2D node || !GodotObject.IsInstanceValid(node)) return;
        BattleActionCue.AttachTo(node).ShowAction(name, iconPath);
    }
}
