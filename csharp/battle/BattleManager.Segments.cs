using Godot;
using Godot.Collections;

public partial class BattleManager
{
    private readonly System.Collections.Generic.Dictionary<GodotObject, System.Collections.Generic.Dictionary<ActiveSkillData, int>> _skillBattleCharges = [];
    private readonly HashSet<ulong> _queuedHandledTargets = [];

    public GodotObject? get_main_player_unit() => _mainPlayerUnit;
    public float get_planning_seconds_remaining() => Mathf.Max(0, planning_timeout_seconds - _planningElapsed);

    private void BeginPlanningSegment()
    {
        if (!battle_running || !_realtimeMode) return;
        _segmentNumber++;
        _segmentPhase = SegmentPhase.Planning;
        _planningElapsed = 0;
        _playerQueueConfirmed = false;
        _selectingQueueTargets = false;
        _targetQueueIndex = -1;
        _targetSelectionHistory.Clear();
        _resolvingUnit = null;
        _queuedHandledTargets.Clear();
        _pendingInstantSkill = null;
        if (_segmentNumber == 1) InitializeBattleSkillUses();

        foreach (var unit in all_units)
        {
            var state = get_battle_unit(unit);
            state.skill_queue.Clear();
            action_gauges[unit] = 0f;
            if (!Alive(unit)) continue;

            if (unit is Creature roundCreature && GodotObject.IsInstanceValid(roundCreature)) roundCreature.battle_round_id = _segmentNumber;
            body_cards.BeginRound(unit);
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
        if (!can_player_plan() || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return false;
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        if (queue.Count >= get_action_slot_capacity(_mainPlayerUnit)) return false;
        queue.Add(actionKind == 0 ? BattleQueuedSkill.Rest() : BattleQueuedSkill.Breathe());
        QueueChanged();
        return true;
    }

    public bool cancel_player_action(int index)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return false;
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        if (index < 0 || index >= queue.Count) return false;
        queue.RemoveAt(index);
        _skillBar?.refresh_slots();
        QueueChanged();
        return true;
    }

    public bool reorder_player_action(int from, int to)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(_mainPlayerUnit)) return false;
        var queue = get_battle_unit(_mainPlayerUnit!).skill_queue;
        if (from < 0 || from >= queue.Count || to < 0 || to >= queue.Count || from == to) return false;
        var action = queue[from];
        queue.RemoveAt(from);
        queue.Insert(to, action);
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
        if (!string.IsNullOrWhiteSpace(realm) && action_slots_by_realm.TryGetValue(realm, out var configured))
            return Mathf.Clamp(configured.AsInt32(), 5, 10);
        return get_action_slot_capacity();
    }

    private void InitializeBattleSkillUses()
    {
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
        if (skill.uses_per_round > 0 && _skillRoundCharges.TryGetValue(unit, out var round) && round.GetValueOrDefault(skill, skill.uses_per_round) <= 0) return false;
        if (skill.uses_per_battle > 0 && _skillBattleCharges.TryGetValue(unit, out var battle) && battle.GetValueOrDefault(skill, skill.uses_per_battle) <= 0) return false;
        return true;
    }

    private bool HasAvailablePlannedSkillUses(GodotObject unit, ActiveSkillData skill)
    {
        if (!is_skill_ready_for_legacy_limits(unit, skill)) return false;
        var queued = get_battle_unit(unit).skill_queue.Count(action => action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill);
        if (skill.uses_per_round > 0 && _skillRoundCharges.TryGetValue(unit, out var round) && round.GetValueOrDefault(skill, skill.uses_per_round) <= queued) return false;
        if (skill.uses_per_battle > 0 && _skillBattleCharges.TryGetValue(unit, out var battle) && battle.GetValueOrDefault(skill, skill.uses_per_battle) <= queued) return false;
        return true;
    }

    private bool is_skill_ready_for_legacy_limits(GodotObject unit, ActiveSkillData skill)
    {
        if (!_combatSkills.TryGetValue(unit, out var loadout) || !loadout.Contains(skill)) return false;
        if (!_realtimeMode && get_skill_cooldown(unit, skill) > 0.01f) return false;
        if (skill.max_charges > 0 && _skillCharges.TryGetValue(unit, out var charges))
        {
            var remaining = charges.GetValueOrDefault(skill, skill.max_charges);
            var queued = get_battle_unit(unit).skill_queue.Count(action => action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill);
            if (remaining <= queued) return false;
        }
        return true;
    }

    public string get_skill_uses_remaining(GodotObject unit, ActiveSkillData skill)
    {
        var queued = get_battle_unit(unit).skill_queue.Count(action => action.kind == BattleQueuedSkill.ActionKind.Skill && action.skill == skill);
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
            var queue = get_battle_unit(unit).skill_queue;
            if (queue.Count == 0) { order++; continue; }
            var speed = Mathf.Max(1, Initiative(unit));
            var gauge = action_gauges.ContainsKey(unit) ? action_gauges[unit].AsSingle() : 0f;
            var resolving = unit == _resolvingUnit && _segmentPhase == SegmentPhase.Executing;
            var firstIndex = resolving ? 1 : 0;
            if (resolving)
                pending.Add((-1, order++, unit.Get("entity_name").AsString()));
            for (var i = firstIndex; i < queue.Count; i++)
            {
                var firstDue = resolving ? 0 : Mathf.Max(0, ActionThreshold - gauge) / speed;
                var time = i * ActionThreshold / speed + firstDue;
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
        if (GodotObject.IsInstanceValid(_resolvingUnit))
        {
            AdvanceCombatTime(wallFrames);
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
                .Where(unit => Alive(unit) && get_battle_unit(unit).skill_queue.Count > 0)
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

            var queue = get_battle_unit(due!).skill_queue;
            if (queue.Count == 0 || !Alive(due)) continue;
            var action = queue[0];
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
            if (!battle_units.ContainsKey(unit) || get_battle_unit(unit).skill_queue.Count == 0) continue;
            var currentGauge = action_gauges.ContainsKey(unit) ? action_gauges[unit].AsSingle() : 0f;
            action_gauges[unit] = Mathf.Min(ActionThreshold, currentGauge + Initiative(unit) * frames / SimulationFps);
            if (unit.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER) continue;
            var head = get_battle_unit(unit).skill_queue[0];
            head.intent_elapsed += frames;
            head.intent_revealed = head.intent_elapsed >= IntentRevealDelay(unit);
        }
        _statusPanel?.refresh_all();
    }

    private void ResolveSegmentAction(GodotObject unit, BattleQueuedSkill action)
    {
        var queue = get_battle_unit(unit).skill_queue;
        if (action.kind == BattleQueuedSkill.ActionKind.Rest)
        {
            var staminaMax = Math.Max(0, unit.Get("max_stamina").AsInt32());
            unit.Set("current_stamina", Math.Min(staminaMax, unit.Get("current_stamina").AsInt32() + 2));
            CompleteSegmentAction(unit);
            return;
        }
        if (action.kind == BattleQueuedSkill.ActionKind.Breathe)
        {
            var manaMax = get_effective_mana_capacity(unit);
            unit.Set("current_mana", Math.Min(manaMax, unit.Get("current_mana").AsInt32() + 1));
            CompleteSegmentAction(unit);
            return;
        }

        var skill = action.skill;
        Pay(unit, skill);
        body_cards.Before(unit, skill);
        StartSkillCooldown(unit, skill);
        action.phase = BattleQueuedSkill.Phase.Resolving;
        _resolvingUnit = unit;
        _queuedHandledTargets.Clear();
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

    private bool CanResolveSegmentAction(GodotObject unit, BattleQueuedSkill action)
    {
        if (action.kind != BattleQueuedSkill.ActionKind.Skill) return true;
        var skill = action.skill;
        if (!body_cards.CanPlay(unit, skill)) return false;
        if (skill.max_charges > 0 && _skillCharges.TryGetValue(unit, out var charges) && charges.GetValueOrDefault(skill, skill.max_charges) <= 0) return false;
        if (!HasAvailableSegmentSkillUses(unit, skill)) return false;
        return unit.Get("current_stamina").AsInt32() >= get_skill_energy_cost(unit, skill)
            && unit.Get("current_mana").AsInt32() >= get_skill_mana_cost(unit, skill);
    }

    private void SkipSegmentAction(GodotObject unit)
    {
        var queue = get_battle_unit(unit).skill_queue;
        if (queue.Count > 0) queue.RemoveAt(0);
        action_gauges[unit] = 0f;
        if (unit == _mainPlayerUnit) _planHud?.RefreshPlayerQueue();
        _skillBar?.refresh_slots();
    }

    private void CompleteSegmentAction(GodotObject unit)
    {
        var queue = get_battle_unit(unit).skill_queue;
        if (queue.Count > 0) queue.RemoveAt(0);
        action_gauges[unit] = 0f;
        if (_resolvingUnit == unit) _resolvingUnit = null;
        if (unit == _mainPlayerUnit) _planHud?.RefreshPlayerQueue();
        _skillBar?.refresh_slots();
        if (CheckBattleOver()) return;
    }

    private void PruneDeadQueues()
    {
        foreach (var unit in all_units.Where(unit => !Alive(unit)))
        {
            if (battle_units.ContainsKey(unit)) get_battle_unit(unit).skill_queue.Clear();
            action_gauges[unit] = 0f;
        }
    }

    private void FinishActionSegment()
    {
        if (_segmentPhase != SegmentPhase.Executing || GodotObject.IsInstanceValid(_resolvingUnit)) return;
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
