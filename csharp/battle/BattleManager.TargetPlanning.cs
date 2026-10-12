using Godot;

public partial class BattleManager
{
    private bool _selectingQueueTargets;
    private int _targetQueueIndex = -1;
    private readonly List<int> _targetSelectionHistory = [];
    public bool is_selecting_queue_targets() => _selectingQueueTargets;
    public int get_target_queue_index() => _targetQueueIndex;

    private void BeginQueueTargetSelection()
    {
        _selectingQueueTargets = true;
        _targetQueueIndex = 0;
        _targetSelectionHistory.Clear();
        _skillBar?.set_actions_visible(false,true);
        ContinueQueueTargetSelection();
    }
    private void ContinueQueueTargetSelection()
    {
        if (!GodotObject.IsInstanceValid(_mainPlayerUnit)) return;
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        unitOverlay?.ClearTargetFrames();
        _pendingInstantSkill = null;
        while (_targetQueueIndex < queue.Count)
        {
            var action = queue[_targetQueueIndex];
            if (action.kind != BattleQueuedSkill.ActionKind.Skill) { _targetQueueIndex++; continue; }
            var skill = CardRules.NormalCast(action.skill);
            if (skill is null) { action.target = _mainPlayerUnit; _targetQueueIndex++; continue; }
            if (!RequiresTargetSelection(skill) || IsAutomaticScope(skill))
            {
                action.target = DefaultTarget(skill, _mainPlayerUnit!); _targetQueueIndex++; continue;
            }
            var targets = all_units.Where(Alive).Where(unit => IsValidTarget(skill, _mainPlayerUnit!, unit)).ToList();
            if (targets.Count <= 1)
            {
                action.target = targets.FirstOrDefault(); _targetQueueIndex++; continue;
            }
            _pendingInstantSkill = skill;
            unitOverlay?.ShowTargetFrames(targets, Colors.White, float.PositiveInfinity, null, _mainPlayerUnit);
            _planHud?.RefreshAll();
            return;
        }
        StartConfirmedQueue();
    }
    private bool AcceptQueueTarget(GodotObject? target)
    {
        if (!_selectingQueueTargets || _pendingInstantSkill is null || _mainPlayerUnit is null || !IsValidTarget(_pendingInstantSkill, _mainPlayerUnit, target)) return false;
        get_battle_unit(_mainPlayerUnit).skill_queue[_targetQueueIndex].target = target;
        _targetSelectionHistory.Add(_targetQueueIndex);
        _targetQueueIndex++;
        ContinueQueueTargetSelection();
        return true;
    }
    private void StartConfirmedQueue()
    {
        _selectingQueueTargets = false;
        _targetQueueIndex = -1;
        _playerQueueConfirmed = true;
        _pendingInstantSkill = null;
        unitOverlay?.ClearTargetFrames();
        PlanAllyTeam();
        _segmentPhase = SegmentPhase.Executing;
        _skillBar?.set_actions_visible(false);
        _planHud?.RefreshAll();
    }
    private bool UndoQueuePlanning()
    {
        if (_segmentPhase != SegmentPhase.Planning || _mainPlayerUnit is null) return false;
        var queue = get_battle_unit(_mainPlayerUnit).skill_queue;
        if (_selectingQueueTargets)
        {
            _pendingInstantSkill = null; unitOverlay?.ClearTargetFrames();
            if (_targetSelectionHistory.Count == 0)
            {
                _selectingQueueTargets = false; _targetQueueIndex = -1; _skillBar?.set_actions_visible(true); _planHud?.RefreshAll();
            }
            else
            {
                _targetQueueIndex = _targetSelectionHistory[^1];
                _targetSelectionHistory.RemoveAt(_targetSelectionHistory.Count - 1);
                for (var i = _targetQueueIndex; i < queue.Count; i++) queue[i].target = null;
                ContinueQueueTargetSelection();
            }
        }
        else
        {
            while (_playerPlanningHistory.Count > 0)
            {
                var last = _playerPlanningHistory[^1];
                _playerPlanningHistory.RemoveAt(_playerPlanningHistory.Count - 1);
                var index = queue.IndexOf(last);
                if (index >= 0) { cancel_player_action(index); break; }
            }
        }
        return true;
    }
    // 预算扣除所有已排费用，允许赤字；不写回角色，也不用于实际支付。
    public Vector2I get_player_queue_resource_budget()
    {
        if (!GodotObject.IsInstanceValid(_mainPlayerUnit) || !battle_units.ContainsKey(_mainPlayerUnit!)) return Vector2I.Zero;
        var unit = _mainPlayerUnit!;
        var energy = unit.Get("current_stamina").AsInt32();
        var mana = unit.Get("current_mana").AsInt32();
        foreach (var action in get_battle_unit(unit).skill_queue)
        {
            if (action.kind == BattleQueuedSkill.ActionKind.Rest)
                energy = Math.Min(unit.Get("max_stamina").AsInt32(), energy + 2);
            else if (action.kind == BattleQueuedSkill.ActionKind.Breathe)
                mana = Math.Min(get_effective_mana_capacity(unit), mana + 1);
            else if (action.kind == BattleQueuedSkill.ActionKind.Skill)
            {
                energy -= get_skill_energy_cost(unit, action.skill);
                mana -= get_skill_mana_cost(unit, action.skill);
                ApplyPlannedResourceReturn(unit, action.skill, ref energy);
            }
        }
        return new Vector2I(energy, mana);
    }

    private static void ApplyPlannedResourceReturn(GodotObject unit, ActiveSkillData skill, ref int energy)
    {
        // 只计入现有执行器明确定义的无条件返还；不预支应对、受击或心法触发收益。
        if (CardRules.IsResponse(skill)) return;
        foreach (var effect in skill.special_effects)
            if (effect.GetValueOrDefault("effect", "").AsString() == "gain_energy")
                energy = Math.Min(unit.Get("max_stamina").AsInt32(), energy + Math.Max(1, effect.GetValueOrDefault("count", 1).AsInt32()));
    }

    public List<string> get_player_queue_resource_warnings()
    {
        var result = new List<string>();
        if (!GodotObject.IsInstanceValid(_mainPlayerUnit) || !battle_units.ContainsKey(_mainPlayerUnit!)) return result;
        var unit = _mainPlayerUnit!;
        var energy = unit.Get("current_stamina").AsInt32(); var mana = unit.Get("current_mana").AsInt32();
        foreach (var action in get_battle_unit(unit).skill_queue)
        {
            var warning = "";
            if (action.kind == BattleQueuedSkill.ActionKind.Rest) energy = Math.Min(unit.Get("max_stamina").AsInt32(), energy + 2);
            else if (action.kind == BattleQueuedSkill.ActionKind.Breathe) mana = Math.Min(get_effective_mana_capacity(unit), mana + 1);
            else if (action.kind == BattleQueuedSkill.ActionKind.Skill)
            {
                var e = get_skill_energy_cost(unit, action.skill); var m = get_skill_mana_cost(unit, action.skill);
                warning = energy < e ? "精力不足" : mana < m ? "法力不足" : "";
                // 不足的行动会跳过，不扣费，继续预测后续行动。
                if (warning == "") { energy -= e; mana -= m; ApplyPlannedResourceReturn(unit, action.skill, ref energy); }
            }
            result.Add(warning);
        }
        return result;
    }
}
