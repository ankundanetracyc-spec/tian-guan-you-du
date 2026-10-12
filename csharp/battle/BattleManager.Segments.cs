using Godot;
using Godot.Collections;

public partial class BattleManager
{
    private readonly System.Collections.Generic.Dictionary<GodotObject, System.Collections.Generic.Dictionary<ActiveSkillData, int>> _skillBattleCharges = [];
    private readonly HashSet<ulong> _queuedHandledTargets = [];
    private readonly HashSet<GodotObject> _pendingActionCompletions = [];
    private readonly List<BattleQueuedSkill> _playerPlanningHistory = [];

    public GodotObject? get_main_player_unit() => _mainPlayerUnit;
    public int get_current_action_slot(GodotObject? unit) => GodotObject.IsInstanceValid(unit) && battle_units.ContainsKey(unit!) ? get_battle_unit(unit!).segment_action_index : -1;
    public int get_filled_action_slot_count(GodotObject? unit) => GodotObject.IsInstanceValid(unit) && battle_units.ContainsKey(unit!)
        ? get_battle_unit(unit!).skill_queue.Count(action => action.kind != BattleQueuedSkill.ActionKind.Empty) : 0;

    private void EnsureSegmentSlots(GodotObject unit)
    {
        var state = get_battle_unit(unit);
        var capacity = get_action_slot_capacity(unit);
        while (state.skill_queue.Count < capacity) state.skill_queue.Add(BattleQueuedSkill.Empty());
    }

    private bool PlacePlayerAction(BattleQueuedSkill action)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return false;
        EnsureSegmentSlots(_mainPlayerUnit!);
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        var index = queue.FindIndex(slot => slot.kind == BattleQueuedSkill.ActionKind.Empty);
        if (index < 0) return false;
        queue[index] = action;
        _playerPlanningHistory.Add(action);
        QueueChanged();
        return true;
    }

    private void AdvanceSegmentSlot(GodotObject unit, bool skipped = false)
    {
        var state = get_battle_unit(unit);
        if (state.current_segment_action is { } action)
        {
            action.completed = true; action.skipped = skipped;
            state.segment_action_index++;
        }
        action_gauges[unit] = 0f;
        if (unit == _mainPlayerUnit) _planHud?.RefreshPlayerQueue();
        _skillBar?.refresh_slots();
    }
    public float get_planning_seconds_remaining() => Mathf.Max(0, planning_timeout_seconds - _planningElapsed);

    private void BeginPlanningSegment()
    {
        if (!battle_running || !_realtimeMode) return;
        _segmentNumber++;
        _retreatAttemptedThisRound=false;
        _segmentPhase = SegmentPhase.Planning;
        _planningElapsed = 0;
        _playerQueueConfirmed = false;
        _selectingQueueTargets = false;
        _targetQueueIndex = -1;
        _targetSelectionHistory.Clear();
        _playerPlanningHistory.Clear();
        _resolvingUnit = null;
        _queuedHandledTargets.Clear();
        _pendingInstantSkill = null;
        if (_segmentNumber == 1) InitializeBattleSkillUses();

        foreach (var unit in all_units)
        {
            var state = get_battle_unit(unit);
            state.skill_queue.Clear();
            state.segment_action_index = 0;
            action_gauges[unit] = 0f;
            if (!Alive(unit)) continue;
            EnsureSegmentSlots(unit);

            if (unit is Creature roundCreature && GodotObject.IsInstanceValid(roundCreature)) roundCreature.battle_round_id = _segmentNumber;
            gongfa_combat.BeginRound(unit);
            ResetRoundSkillUses(unit);
            foreach (var circuit in _formationCircuits.GetValueOrDefault(unit, [])) circuit.begin_owner_turn();
            var staminaMax = Mathf.Max(0, unit.Get("max_stamina").AsInt32());
            unit.Set("current_stamina", Mathf.Min(staminaMax, unit.Get("current_stamina").AsInt32() + 3));
            unit.Set("current_mana", get_effective_mana_capacity(unit));
            if (unit.HasMethod("process_status_event"))
                unit.Call("process_status_event", "turn_start", new Dictionary { ["battle_manager"] = this, ["unit"] = unit });
        }

        _mainPlayerUnit = player_units.FirstOrDefault(Alive);
        active_unit = _mainPlayerUnit;
        turn_state = TurnState.IDLE;
        PlanEnemyTeams();
        PlanAllyTeam();
        if (_spectatorBattle)
        {
            if (GodotObject.IsInstanceValid(_mainPlayerUnit))
            {
                var forecast = CreateHealthForecast();
                BuildAiQueue(_mainPlayerUnit!, forecast, false);
                EnsureSegmentSlots(_mainPlayerUnit!);
                ApplyQueueForecast(_mainPlayerUnit!, forecast);
            }
            _playerQueueConfirmed = true;
            _segmentPhase = SegmentPhase.Executing;
            _skillBar?.Hide();
            _planHud?.Hide();
            _statusPanel?.Hide();
            return;
        }
        if (GodotObject.IsInstanceValid(_mainPlayerUnit))
        {
            _skillBar?.set_battle_unit(_mainPlayerUnit!);
            _skillBar?.set_loadout(_combatSkills.GetValueOrDefault(_mainPlayerUnit!, []));
            _skillBar?.Show();
            _skillBar?.set_actions_visible(true);
        }
        _planHud?.Show();
        _planHud?.RefreshAll();
        _statusPanel?.refresh_all();
    }

    private void AdvancePlanning(float frames)
    {
        if (_segmentPhase != SegmentPhase.Planning) return;
        // 单机规划无倒计时；只由玩家确认后推进。
    }

    public void confirm_player_queue()
    {
        if (!can_player_plan()) return;
        BeginQueueTargetSelection();
    }

    public bool queue_system_action(int actionKind)
    {
        return PlacePlayerAction(actionKind == 0 ? BattleQueuedSkill.Rest() : BattleQueuedSkill.Breathe());
    }

    public bool cancel_player_action(int index)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return false;
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        if (index < 0 || index >= queue.Count || queue[index].kind == BattleQueuedSkill.ActionKind.Empty) return false;
        _playerPlanningHistory.Remove(queue[index]);
        queue[index] = BattleQueuedSkill.Empty();
        _skillBar?.refresh_slots();
        QueueChanged();
        return true;
    }

    public bool reorder_player_action(int from, int to)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return false;
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        if (from < 0 || from >= queue.Count || to < 0 || to >= queue.Count || from == to) return false;
        if (queue[from].kind == BattleQueuedSkill.ActionKind.Empty) return false;
        (queue[from], queue[to]) = (queue[to], queue[from]);
        QueueChanged();
        return true;
    }

    private void QueueChanged()
    {
        PlanAllyTeam();
        _planHud?.RefreshAll();
        _skillBar?.refresh_slots();
    }

    public int get_action_slot_capacity(GodotObject? unit)
    {
        if (GodotObject.IsInstanceValid(unit) && unit!.HasMeta("battle_action_slots"))
            return Mathf.Clamp(unit.GetMeta("battle_action_slots").AsInt32(), 5, 10);
        var realm = unit is Creature creature ? creature.cultivation_realm : "";
        return get_action_slot_capacity_for_realm(realm);
    }
    public int get_action_slot_capacity_for_realm(string realm)
    {
        if (!string.IsNullOrWhiteSpace(realm) && action_slots_by_realm.TryGetValue(realm, out var configured))
            return Mathf.Clamp(configured.AsInt32(), 5, 10);
        return get_action_slot_capacity();
    }

    private void InitializeBattleSkillUses()
    {
        _skillReadyRounds.Clear();
        _skillBattleCharges.Clear();
        foreach (var unit in all_units)
        {
            var charges = new System.Collections.Generic.Dictionary<ActiveSkillData, int>();
            if (_combatSkills.TryGetValue(unit, out var skills))
                foreach (var skill in skills)
                    if (skill.uses_per_battle > 0) charges[skill] = skill.uses_per_battle;
            _skillBattleCharges[unit] = charges;
        }
    }

    private void ResetRoundSkillUses(GodotObject unit)
    {
        var charges = new System.Collections.Generic.Dictionary<ActiveSkillData, int>();
        if (_combatSkills.TryGetValue(unit, out var skills))
            foreach (var skill in skills)
                if (skill.uses_per_round > 0) charges[skill] = skill.uses_per_round;
        _skillRoundCharges[unit] = charges;
    }

    private bool HasAvailableSegmentSkillUses(GodotObject unit, ActiveSkillData skill)
    {
        if (get_skill_cooldown_rounds(unit, skill) > 0) return false;
        if (skill.uses_per_round > 0 && _skillRoundCharges.TryGetValue(unit, out var round) && round.GetValueOrDefault(skill, skill.uses_per_round) <= 0) return false;
        if (skill.uses_per_battle > 0 && _skillBattleCharges.TryGetValue(unit, out var battle) && battle.GetValueOrDefault(skill, skill.uses_per_battle) <= 0) return false;
        return true;
    }

    private bool HasAvailablePlannedSkillUses(GodotObject unit, ActiveSkillData skill)
    {
        if (!is_skill_ready_for_legacy_limits(unit, skill)) return false;
        var queued = get_battle_unit(unit).skill_queue.Count(action => !action.completed && action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill);
        if (skill.cooldown_rounds > 0 && queued > 0) return false;
        if (skill.uses_per_round > 0 && _skillRoundCharges.TryGetValue(unit, out var round) && round.GetValueOrDefault(skill, skill.uses_per_round) <= queued) return false;
        if (skill.uses_per_battle > 0 && _skillBattleCharges.TryGetValue(unit, out var battle) && battle.GetValueOrDefault(skill, skill.uses_per_battle) <= queued) return false;
        return true;
    }

    private bool is_skill_ready_for_legacy_limits(GodotObject unit, ActiveSkillData skill)
    {
        if (!_combatSkills.TryGetValue(unit, out var loadout) || !loadout.Contains(skill)) return false;
        if (get_skill_cooldown_rounds(unit, skill) > 0) return false;
        if (!_realtimeMode && get_skill_cooldown(unit, skill) > 0.01f) return false;
        if (skill.max_charges > 0 && _skillCharges.TryGetValue(unit, out var charges))
        {
            var remaining = charges.GetValueOrDefault(skill, skill.max_charges);
            var queued = get_battle_unit(unit).skill_queue.Count(action => !action.completed && action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill);
            if (remaining <= queued) return false;
        }
        return true;
    }

    public string get_skill_uses_remaining(GodotObject unit, ActiveSkillData skill)
    {
        var queued = get_battle_unit(unit).skill_queue.Count(action => !action.completed && action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill);
        var parts = new List<string>();
        if (skill.uses_per_round > 0)
        {
            var remaining = _skillRoundCharges.TryGetValue(unit, out var round) ? round.GetValueOrDefault(skill, skill.uses_per_round) : skill.uses_per_round;
            parts.Add($"轮 {Mathf.Max(0, remaining - queued)}");
        }
        var battleLimit = skill.uses_per_battle > 0 ? skill.uses_per_battle : skill.max_charges;
        if (battleLimit > 0)
        {
            var remaining = skill.uses_per_battle > 0 && _skillBattleCharges.TryGetValue(unit, out var battle)
                ? battle.GetValueOrDefault(skill, battleLimit)
                : _skillCharges.TryGetValue(unit, out var oldCharges) ? oldCharges.GetValueOrDefault(skill, battleLimit) : battleLimit;
            parts.Add($"场 {Mathf.Max(0, remaining - queued)}");
        }
        return string.Join(" · ", parts);
    }

    private void ConsumeSegmentSkillUses(GodotObject unit, ActiveSkillData skill)
    {
        if (_segmentPhase != SegmentPhase.Executing) return;
        if (skill.uses_per_round > 0 && _skillRoundCharges.TryGetValue(unit, out var round))
            round[skill] = Mathf.Max(0, round.GetValueOrDefault(skill, skill.uses_per_round) - 1);
        if (skill.uses_per_battle > 0 && _skillBattleCharges.TryGetValue(unit, out var battle))
            battle[skill] = Mathf.Max(0, battle.GetValueOrDefault(skill, skill.uses_per_battle) - 1);
    }

    public Array<string> get_timeline_names()
    {
        var pending = new List<(float time, int order, string name)>();
        var order = 0;
        foreach (var unit in all_units.Where(Alive))
        {
            if (!battle_units.ContainsKey(unit)) continue;
            var state = get_battle_unit(unit);
            var queue = state.skill_queue;
            if (!state.has_pending_segment_action) { order++; continue; }
            var speed = Mathf.Max(1, Initiative(unit));
            var gauge = action_gauges.ContainsKey(unit) ? action_gauges[unit].AsSingle() : 0f;
            var resolving = unit == _resolvingUnit && _segmentPhase == SegmentPhase.Executing;
            var firstIndex = state.segment_action_index + (resolving ? 1 : 0);
            if (resolving)
                pending.Add((-1, order++, unit.Get("entity_name").AsString()));
            for (var i = firstIndex; i < queue.Count; i++)
            {
                var firstDue = resolving ? 0 : Mathf.Max(0, ActionThreshold - gauge) / speed;
                var time = (i - state.segment_action_index) * ActionThreshold / speed + firstDue;
                pending.Add((time, order++, unit.Get("entity_name").AsString()));
            }
        }
        var names = new Array<string>();
        foreach (var entry in pending.OrderBy(item => item.time).ThenBy(item => item.order).Take(12))
            names.Add(string.IsNullOrWhiteSpace(entry.name) ? "无名" : entry.name);
        return names;
    }

    private void ProcessSegmentExecution(float wallFrames)
    {
        if (_segmentPhase != SegmentPhase.Executing) return;
        if (_resolvingUnit is not null)
        {
            // 演出是结算时刻的展开，不让动画长度额外推进状态、意图或行动进度。
            return;
        }

        PruneDeadQueues();
        if (CheckBattleOver()) return;

        var remainingFrames = Mathf.Max(0, wallFrames);
        // 行动进度按战斗时间逐帧推进；进度条走满后才结算当前队首。
        // 若费用／次数不足，跳过该项并在同一模拟步内继续检查后续队列。
        for (var guard = 0; guard < 32; guard++)
        {
            var due = all_units
                .Where(unit => Alive(unit) && get_battle_unit(unit).has_pending_segment_action)
                .OrderBy(unit => TimeUntilAction(unit))
                .FirstOrDefault();
            if (!GodotObject.IsInstanceValid(due))
            {
                FinishActionSegment();
                return;
            }

            var framesUntilAction = TimeUntilAction(due!) * SimulationFps;
            if (framesUntilAction > remainingFrames + 0.0001f)
            {
                AdvanceCombatTime(remainingFrames);
                return;
            }
            if (framesUntilAction > 0f)
            {
                AdvanceCombatTime(framesUntilAction);
                remainingFrames = Mathf.Max(0, remainingFrames - framesUntilAction);
            }
            if (CheckBattleOver()) return;

            var action = get_battle_unit(due!).current_segment_action;
            if (action is null || !Alive(due)) continue;
            action_gauges[due!] = ActionThreshold;
            if (!CanResolveSegmentAction(due!, action))
            {
                SkipSegmentAction(due!);
                continue;
            }
            ResolveSegmentAction(due!, action);
            return;
        }
    }

    private float TimeUntilAction(GodotObject unit)
    {
        var gauge = action_gauges.ContainsKey(unit) ? action_gauges[unit].AsSingle() : 0f;
        return Mathf.Max(0, ActionThreshold - gauge) / Mathf.Max(1, Initiative(unit));
    }

    private void AdvanceCombatTime(float frames)
    {
        if (frames <= 0) return;
        _battleVisualElapsedSeconds += frames / SimulationFps;
        UpdateShieldVisualClocks();
        foreach (var unit in all_units)
        {
            if (!Alive(unit)) continue;
            if (unit is Creature creature && GodotObject.IsInstanceValid(creature)) creature.tick_status_frames(frames);
            if (!battle_units.ContainsKey(unit) || !get_battle_unit(unit).has_pending_segment_action) continue;
            var currentGauge = action_gauges.ContainsKey(unit) ? action_gauges[unit].AsSingle() : 0f;
            action_gauges[unit] = Mathf.Min(ActionThreshold, currentGauge + Initiative(unit) * frames / SimulationFps);
            if (unit.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER) continue;
            var head = get_battle_unit(unit).current_segment_action!;
            head.intent_elapsed += frames;
            head.intent_revealed = head.intent_elapsed >= IntentRevealDelay(unit);
        }
        _statusPanel?.refresh_all();
    }

    private void ResolveSegmentAction(GodotObject unit, BattleQueuedSkill action)
    {
        action.phase = BattleQueuedSkill.Phase.Resolving;
        _resolvingUnit = unit;
        if (action.kind == BattleQueuedSkill.ActionKind.Empty)
        {
            // 空槽已走满一次行动条，只消耗该行动位；不当作休整或吐纳。
            _planHud?.PresentAction(unit);
            CompleteSystemActionPresentation(unit);
            return;
        }
        if (action.kind == BattleQueuedSkill.ActionKind.Rest)
        {
            _executor?.show_action_name(unit, "休整");
            var staminaMax = Math.Max(0, unit.Get("max_stamina").AsInt32());
            unit.Set("current_stamina", Math.Min(staminaMax, unit.Get("current_stamina").AsInt32() + 2));
            _planHud?.PresentAction(unit);
            CompleteSystemActionPresentation(unit);
            return;
        }
        if (action.kind == BattleQueuedSkill.ActionKind.Breathe)
        {
            _executor?.show_action_name(unit, "吐纳");
            var manaMax = get_effective_mana_capacity(unit);
            unit.Set("current_mana", Math.Min(manaMax, unit.Get("current_mana").AsInt32() + 1));
            _planHud?.PresentAction(unit);
            CompleteSystemActionPresentation(unit);
            return;
        }

        if (action.kind == BattleQueuedSkill.ActionKind.Retreat) { _executor?.show_action_name(unit, "撤退"); ResolveRetreat(unit); return; }
        var skill = CardRules.NormalCast(action.skill);
        if (skill is null)
        {
            // 未应对空放只耗当前行动位；不支付、计次、触发心法或生成防御状态。
            _planHud?.PresentAction(unit);
            CompleteSystemActionPresentation(unit);
            return;
        }
        Pay(unit, skill);
        gongfa_combat.Before(unit, skill);
        StartSkillCooldown(unit, action.skill);
        action.phase = BattleQueuedSkill.Phase.Resolving;
        _resolvingUnit = unit;
        _queuedHandledTargets.Clear();
        _planHud?.PresentAction(unit);
        var target = GodotObject.IsInstanceValid(action.target) && Alive(action.target)
            ? action.target
            : DefaultTarget(skill, unit);
        if (!GodotObject.IsInstanceValid(target))
        {
            CompleteSegmentAction(unit);
            return;
        }
        if (skill.aoe_range > 0 && Map is not null)
            _executor!.execute_aoe_skill(unit, skill, Map.Call("get_unit_cell", target).AsVector2I(), Map, false);
        else
            _executor!.execute_skill(unit, skill, target!, false);
    }

    private async void CompleteSystemActionPresentation(GodotObject unit)
    {
        if (_spectatorBattle)
        {
            if (battle_running && GodotObject.IsInstanceValid(unit) && _resolvingUnit == unit) CompleteSegmentAction(unit);
            return;
        }
        // 回复也有独立的一拍，避免与相邻技能在同一画面闪过。
        await ToSignal(GetTree().CreateTimer(.34,processAlways:false),SceneTreeTimer.SignalName.Timeout);
        if(battle_running&&GodotObject.IsInstanceValid(unit)&&_resolvingUnit==unit)CompleteSegmentAction(unit);
    }

    private bool CanResolveSegmentAction(GodotObject unit, BattleQueuedSkill action, bool responseTriggered = false)
    {
        if (action.kind != BattleQueuedSkill.ActionKind.Skill) return true;
        var skill = responseTriggered ? action.skill : CardRules.NormalCast(action.skill);
        if (skill is null) return true;
        if (!gongfa_combat.CanPlay(unit, skill)) return false;
        if (action.skill.max_charges > 0 && _skillCharges.TryGetValue(unit, out var charges) && charges.GetValueOrDefault(action.skill, action.skill.max_charges) <= 0) return false;
        if (!HasAvailableSegmentSkillUses(unit, action.skill)) return false;
        return unit.Get("current_stamina").AsInt32() >= get_skill_energy_cost(unit, skill)
            && unit.Get("current_mana").AsInt32() >= get_skill_mana_cost(unit, skill);
    }

    private void SkipSegmentAction(GodotObject unit)
    {
        AdvanceSegmentSlot(unit, skipped: true);
    }

    private bool HasPendingActionAnimations()
    {
        if(_planHud?.is_action_animation_playing()==true)return true;
        foreach(var actor in all_units.OfType<Node>().Where(GodotObject.IsInstanceValid))
            if(actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.is_action_animation_playing()==true)return true;
        return GetTree().GetNodesInGroup("battle_action_visuals")
            .Any(node=>GodotObject.IsInstanceValid(node)&&node.IsInsideTree()&&!node.IsQueuedForDeletion());
    }

    private async void CompleteSegmentAction(GodotObject unit)
    {
        if(!_pendingActionCompletions.Add(unit))return;
        try
        {
            // 至少跨过一帧，让本次命中产生的受击、飘字与死亡节点全部注册。
            // 不以固定秒数代替完成信号，也不等待待机／常驻护盾循环。
            do
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(!IsInsideTree()||!battle_running||_resolvingUnit!=unit)return;
            }while(_battlePaused||HasPendingActionAnimations());

            _resolvingUnit=null;
            if(GodotObject.IsInstanceValid(unit)&&battle_units.ContainsKey(unit))
            {
                AdvanceSegmentSlot(unit);
            }
            if(GodotObject.IsInstanceValid(unit))action_gauges[unit]=0f;
            CheckBattleOver();
        }
        finally{_pendingActionCompletions.Remove(unit);}
    }

    private void PruneDeadQueues()
    {
        foreach (var unit in all_units.Where(unit => !Alive(unit)))
        {
            if (battle_units.ContainsKey(unit))
            {
                var state = get_battle_unit(unit);
                foreach (var action in state.skill_queue.Skip(state.segment_action_index)) { action.completed = true; action.skipped = true; }
                state.segment_action_index = state.skill_queue.Count;
            }
            action_gauges[unit] = 0f;
        }
    }

    private void FinishActionSegment()
    {
        if (_segmentPhase != SegmentPhase.Executing || _resolvingUnit is not null) return;
        foreach (var unit in all_units)
        {
            if (unit is Creature creature && GodotObject.IsInstanceValid(creature))
                creature.process_status_event("turn_end", new Dictionary { ["battle_manager"] = this, ["unit"] = unit });
            unit.Set("current_mana", 0);
            action_gauges[unit] = 0f;
        }
        if (CheckBattleOver()) return;
        _segmentPhase = SegmentPhase.None;
        BeginPlanningSegment();
    }
}
