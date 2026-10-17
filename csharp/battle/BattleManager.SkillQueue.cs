using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    private void AdvanceQueue(GodotObject unit, float frames)
    {
        var queue = get_battle_unit(unit).skill_queue; if (queue.Count == 0) return;
        var card = queue[0];
        if (unit.Get("faction").AsInt32() != (int)WorldEntity.Faction.PLAYER && card.phase == BattleQueuedSkill.Phase.Preparing)
        {
            card.intent_elapsed += frames;
            card.intent_revealed = card.intent_elapsed >= IntentRevealDelay(unit);
        }
        if (card.phase == BattleQueuedSkill.Phase.Waiting)
        {
            if (!CanStartPreparation(unit, card.skill)) return;
            if(!card.wound_timing_adjusted)
            {
                var speed=Mathf.Clamp(get_realtime_action_speed_multiplier(unit),.7f,1f);
                card.preparation_frames=Mathf.CeilToInt(card.preparation_frames/speed);
                card.recovery_frames=Mathf.CeilToInt(card.recovery_frames/speed);
                card.wound_timing_adjusted=true;
            }
            card.phase = BattleQueuedSkill.Phase.Preparing; card.elapsed_frames = 0;
            gongfa_combat.Before(unit, card.skill);
            return;
        }
        if (card.phase == BattleQueuedSkill.Phase.Preparing)
        {
            var before = card.elapsed_frames; card.elapsed_frames = Mathf.Min(card.preparation_frames, card.elapsed_frames + frames);
            PayPreparationMilestones(unit, card, before, card.elapsed_frames);
            if (card.elapsed_frames < card.preparation_frames) return;
            if (CardRules.IsResponse(card.skill)) { card.phase = BattleQueuedSkill.Phase.HoldingResponse; card.elapsed_frames = 0; return; }
            ResolveQueuedCard(unit, card); return;
        }
        if (card.phase == BattleQueuedSkill.Phase.Recovering)
        {
            card.elapsed_frames += frames;
            if (card.elapsed_frames >= card.recovery_frames) queue.RemoveAt(0);
        }
    }

    private int IntentRevealDelay(GodotObject enemy)
    {
        var player = player_units.FirstOrDefault(Alive);
        if (!GodotObject.IsInstanceValid(player)) return IntentRevealFrames;
        return IntentRevealDelay(player!, enemy);
    }

    private static int IntentRevealDelay(GodotObject observer, GodotObject subject)
    {
        var own = CardRules.Number(observer, "soul") + CardRules.Number(observer, "agility");
        var foe = CardRules.Number(subject, "soul") + CardRules.Number(subject, "agility");
        return Mathf.Clamp(Mathf.RoundToInt(IntentRevealFrames * Mathf.Sqrt((foe + 1f) / (own + 1f))), 24, 288);
    }

    /// <summary>
    /// 敌方自己的看破记忆：只在玩家首招开始准备后计时，达到观察者自身的看破阈值
    /// 才把技能和目标写入认知状态。AI 以外的代码不能把玩家队列直接当作已知情报。
    /// </summary>
    private void UpdateEnemyKnowledge(GodotObject enemy, float frames)
    {
        if (!_enemyKnowledge.TryGetValue(enemy, out var knowledge))
            _enemyKnowledge[enemy] = knowledge = new EnemyKnowledgeState();
        var player = player_units.FirstOrDefault(Alive);
        var playerQueue = GodotObject.IsInstanceValid(player) ? get_battle_unit(player!).skill_queue : [];
        var observed = GodotObject.IsInstanceValid(player) && _segmentPhase != SegmentPhase.None
            ? get_battle_unit(player!).current_segment_action : playerQueue.FirstOrDefault();
        if (observed?.kind != BattleQueuedSkill.ActionKind.Skill) observed = null;
        if (observed is null)
        {
            knowledge.ObservedCard = null; knowledge.RevealElapsed = 0; knowledge.Revealed = false;
            knowledge.KnownSkill = null; knowledge.KnownTarget = null;
            return;
        }
        if (!ReferenceEquals(knowledge.ObservedCard, observed))
        {
            knowledge.ObservedCard = observed; knowledge.RevealElapsed = 0; knowledge.Revealed = false;
            knowledge.KnownSkill = null; knowledge.KnownTarget = null;
        }
        if (!knowledge.Revealed && observed.phase == BattleQueuedSkill.Phase.Preparing)
        {
            knowledge.RevealElapsed += frames;
            if (knowledge.RevealElapsed >= IntentRevealDelay(enemy, player!))
            {
                knowledge.Revealed = true;
                knowledge.KnownSkill = observed.skill;
                knowledge.KnownTarget = observed.target;
            }
        }
    }

    private bool CanStartPreparation(GodotObject unit, ActiveSkillData skill) => is_skill_ready(unit, skill) && CanPay(unit, skill)
        && unit.Get("current_stamina").AsInt32() >= get_skill_energy_cost(unit, skill)
        && unit.Get("current_mana").AsInt32() >= get_skill_mana_cost(unit, skill);

    private void StartSkillCooldown(GodotObject unit, ActiveSkillData skill)
    {
        StartRoundSkillCooldown(unit, skill);
        if (_skillCooldowns.TryGetValue(unit, out var cooldowns) && !_realtimeMode && skill.cooldown_frames > 0) cooldowns[skill] = skill.cooldown_frames;
        if (skill.max_charges > 0 && _skillCharges.TryGetValue(unit, out var charges)) charges[skill] = Mathf.Max(0, charges.GetValueOrDefault(skill, skill.max_charges) - 1);
        ConsumeSegmentSkillUses(unit, skill);
    }

    public float get_skill_cooldown(GodotObject unit, ActiveSkillData skill) => _realtimeMode ? 0 : _skillCooldowns.TryGetValue(unit, out var cooldowns) ? cooldowns.GetValueOrDefault(skill, 0) : 0;
    public bool is_skill_ready(GodotObject unit, ActiveSkillData skill) => HasAvailablePlannedSkillUses(unit, skill);

    /// <summary>返回技能在指定角色队列中的位置；-1 表示尚未排队。</summary>
    public int get_skill_queue_index(GodotObject unit, ActiveSkillData skill)
    {
        if (!GodotObject.IsInstanceValid(unit) || skill is null || !battle_units.ContainsKey(unit)) return -1;
        var queue = get_battle_unit(unit).skill_queue;
        return queue.FindIndex(entry => !entry.completed && entry.kind == BattleQueuedSkill.ActionKind.Skill && entry.skill == skill);
    }

    /// <summary>技能栏的二次左键取消入口，队列控件只负责展示。</summary>
    public bool cancel_queued_skill(GodotObject unit, ActiveSkillData skill)
    {
        var index = get_skill_queue_index(unit, skill);
        return index >= 0 && cancel_queued_card(unit, index);
    }
    private void PayPreparationMilestones(GodotObject unit, BattleQueuedSkill card, float before, float now)
    {
        PayMilestones(unit, card.skill, ref card.paid_energy, get_skill_energy_cost(unit, card.skill), card.preparation_frames, before, now, true);
        PayMilestones(unit, card.skill, ref card.paid_mana, get_skill_mana_cost(unit, card.skill), card.preparation_frames, before, now, false);
    }
    private static void PayMilestones(GodotObject unit, ActiveSkillData _, ref int paid, int total, int length, float before, float now, bool energy)
    {
        for (var step = paid + 1; step <= total; step++)
        {
            var milestone = Mathf.CeilToInt(length * step / (float)total);
            if (before < milestone && now >= milestone) { var field = energy ? "current_stamina" : "current_mana"; unit.Set(field, Mathf.Max(0, unit.Get(field).AsInt32() - 1)); paid++; }
        }
    }
    private void ResolveQueuedCard(GodotObject unit, BattleQueuedSkill card)
    {
        card.phase = BattleQueuedSkill.Phase.Resolving;
        StartSkillCooldown(unit, card.skill);
        if (unit == active_unit) _skillBar?.refresh_slots();
        // 即时模式的完整牌面由角色头顶队列持续展示；旧回合模式才使用一次性出牌提示。
        if (!_realtimeMode && unit.Get("faction").AsInt32() != (int)WorldEntity.Faction.PLAYER) unitOverlay?.ShowPlayedCard(unit, card.skill);
        var target = GodotObject.IsInstanceValid(card.target) && Alive(card.target) ? card.target : DefaultTarget(card.skill, unit);
        if (!GodotObject.IsInstanceValid(target)) { on_queued_skill_resolved(unit); return; }
        if (card.skill.aoe_range > 0 && Map is not null) _executor!.execute_aoe_skill(unit, card.skill, Map.Call("get_unit_cell", target).AsVector2I(), Map, false);
        else _executor!.execute_skill(unit, card.skill, target!, false);
    }

    public void on_queued_skill_resolved(GodotObject unit)
    {
        if (_segmentPhase == SegmentPhase.Executing)
        {
            if (_resolvingUnit == unit) CompleteSegmentAction(unit);
            return;
        }
        var queue = get_battle_unit(unit).skill_queue; if (queue.Count == 0) return;
        var card = queue[0]; card.phase = BattleQueuedSkill.Phase.Recovering; card.elapsed_frames = 0;
    }
    public bool cancel_queued_card(GodotObject unit, int index)
    {
        if (_segmentPhase != SegmentPhase.None) return unit == _mainPlayerUnit && cancel_player_action(index);
        var queue = get_battle_unit(unit).skill_queue; if (index < 0 || index >= queue.Count) return false;
        var card = queue[index]; if (card.phase is BattleQueuedSkill.Phase.Resolving or BattleQueuedSkill.Phase.Recovering) return false;
        queue.RemoveAt(index);
        if (unit == active_unit) _skillBar?.refresh_slots();
        return true;
    }
    /// <summary>兼容旧顶部队列读取接口；现行界面改为各角色头顶的独立队列。</summary>
    public Array<Dictionary> get_queue_snapshot()
    {
        var result = new Array<Dictionary>();
        foreach (var unit in all_units.Where(Alive))
        {
            var state = get_battle_unit(unit);
            var queue = state.skill_queue;
            var card = _segmentPhase != SegmentPhase.None ? state.current_segment_action : queue.FirstOrDefault();
            if (card is null) { result.Add(new Dictionary { ["unit"] = unit, ["text"] = "待命", ["progress"] = 0f }); continue; }
            var enemy = unit.Get("faction").AsInt32() != (int)WorldEntity.Faction.PLAYER;
            var visible = !enemy || card.intent_revealed || card.phase is BattleQueuedSkill.Phase.Recovering or BattleQueuedSkill.Phase.HoldingResponse;
            var phase = card.phase switch { BattleQueuedSkill.Phase.Waiting => "待行动", BattleQueuedSkill.Phase.Preparing => "准备", BattleQueuedSkill.Phase.Recovering => "收招", BattleQueuedSkill.Phase.HoldingResponse => "待应", _ => "结算" };
            var name = card.get_display_name();
            var icon = card.kind == BattleQueuedSkill.ActionKind.Skill ? card.skill.icon_path : "";
            result.Add(new Dictionary { ["unit"] = unit, ["text"] = visible ? $"{name} · {phase}" : "?", ["skill_name"] = visible ? name : "", ["icon_path"] = visible ? icon : "", ["progress"] = card.progress(), ["intent_progress"] = IntentRevealProgress(unit, card), ["queue_count"] = queue.Count - (_segmentPhase != SegmentPhase.None ? state.segment_action_index : 0) });
        }
        return result;
    }

    /// <summary>返回指定角色的完整排队牌，供角色头顶的独立队列 UI 使用。</summary>
    public Array<Dictionary> get_queue_snapshot_for_unit(GodotObject unit)
    {
        var result = new Array<Dictionary>();
        if (!GodotObject.IsInstanceValid(unit) || !battle_units.ContainsKey(unit)) return result;
        // 玩家与队友的具体队列不挂在角色头顶；玩家队列固定显示在底部，队友队列由 AI 控制。
        if (unit.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER) return result;
        var state = get_battle_unit(unit);
        var queue = state.skill_queue;
        var start = _segmentPhase != SegmentPhase.None ? state.segment_action_index : 0;
        var enemy = unit.Get("faction").AsInt32() != (int)WorldEntity.Faction.PLAYER;
        // 敌方只公开当前首个行动；后续行动即使已经排队，也不能提前展示。
        var remaining = Math.Max(0, queue.Count - start);
        var visibleCount = enemy ? Math.Min(1, remaining) : remaining;
        for (var i = 0; i < visibleCount; i++)
        {
            var card = queue[start + i];
            var visible = !enemy || card.intent_revealed || card.phase is BattleQueuedSkill.Phase.Recovering or BattleQueuedSkill.Phase.HoldingResponse;
            var phase = card.phase switch
            {
                BattleQueuedSkill.Phase.Waiting => "待备",
                BattleQueuedSkill.Phase.Preparing => "准备",
                BattleQueuedSkill.Phase.Recovering => "收招",
                BattleQueuedSkill.Phase.HoldingResponse => "待应",
                _ => "结算"
            };
            result.Add(new Dictionary
            {
                ["index"] = i,
                ["slot_index"] = start + i,
                ["text"] = visible ? card.get_display_name() : "?",
                ["skill_name"] = visible ? card.get_display_name() : "",
                ["icon_path"] = visible && card.kind == BattleQueuedSkill.ActionKind.Skill ? card.skill.icon_path : "",
                ["phase"] = phase,
                ["progress"] = card.progress(),
                ["intent_progress"] = IntentRevealProgress(unit, card),
                ["front"] = i == 0,
                ["visible"] = visible
            });
        }
        return result;
    }

    /// <summary>敌方首招从开始准备到洞见前的独立看破进度；不会泄露招式准备时长。</summary>
    private float IntentRevealProgress(GodotObject enemy, BattleQueuedSkill card)
    {
        if (enemy.Get("faction").AsInt32() == (int)WorldEntity.Faction.PLAYER || card.intent_revealed) return 1f;
        if (_segmentPhase != SegmentPhase.Executing || card.phase != BattleQueuedSkill.Phase.Waiting) return 0f;
        return Mathf.Clamp(card.intent_elapsed / Mathf.Max(1f, IntentRevealDelay(enemy)), 0f, 1f);
    }

    private void PlanEnemyQueue(GodotObject unit, float frames)
    {
        _enemyPlanTimers[unit] = _enemyPlanTimers.GetValueOrDefault(unit, 0) + frames;
        var queue = get_battle_unit(unit).skill_queue;
        if (!_combatSkills.ContainsKey(unit)) return;
        var knowledge = _enemyKnowledge.GetValueOrDefault(unit);
        // 只有已经看破的玩家首招才能触发应对；这里绝不直接读取玩家隐藏队列。
        if (knowledge is not null && TryArmEnemyResponse(unit, queue, knowledge)) { _enemyPlanTimers[unit] = 0; return; }
        if (_enemyPlanTimers[unit] < 96 || queue.Count >= QueueCapacity) return;
        _enemyPlanTimers[unit] = 0;
        var candidates = _combatSkills[unit].Where(card => !CardRules.IsResponse(card) && card.skill_type != ActiveSkillData.SkillType.CURSE && is_skill_ready(unit, card)).ToList();
        if (candidates.Count == 0) return;
        var skill = ChooseEnemySkill(unit, candidates, knowledge);
        var target = ChooseEnemyTarget(unit, skill);
        if (GodotObject.IsInstanceValid(target)) QueueRealtimeCard(unit, skill, target);
    }

    private bool TryArmEnemyResponse(GodotObject unit, List<BattleQueuedSkill> queue, EnemyKnowledgeState knowledge)
    {
        if (!Alive(unit) || !IsKnownThreat(unit, knowledge)) return false;
        if (queue.Any(entry => CardRules.IsResponse(entry.skill))) return false;
        var damageType = knowledge.KnownSkill!.damage_type;
        var response = _combatSkills[unit]
            .Where(skill => CardRules.IsResponse(skill) && CardRules.ResponseMatches(skill, damageType) && is_skill_ready(unit, skill))
            .OrderBy(skill => skill.preparation_frames)
            .FirstOrDefault();
        if (response is null || !gongfa_combat.CanPlay(unit, response)) return false;

        // 应对只能替换尚未开始准备的队首指令；不打断已经开始的动作。
        if (queue.Count > 0 && queue[0].phase != BattleQueuedSkill.Phase.Waiting) return false;
        if (queue.Count > 0) queue[0] = new BattleQueuedSkill(response, unit);
        else queue.Add(new BattleQueuedSkill(response, unit));
        return true;
    }

    private static bool IsKnownThreat(GodotObject defender, EnemyKnowledgeState knowledge)
    {
        if (!knowledge.Revealed || knowledge.KnownSkill is null || CardRules.IsResponse(knowledge.KnownSkill) || knowledge.KnownSkill.skill_type != ActiveSkillData.SkillType.ATTACK) return false;
        var skill = knowledge.KnownSkill;
        return skill.aoe_range > 0 || skill.description.Contains("全体") || skill.description.Contains("随机") || knowledge.KnownTarget == defender;
    }

    private ActiveSkillData ChooseEnemySkill(GodotObject unit, List<ActiveSkillData> candidates, EnemyKnowledgeState? knowledge)
    {
        var ranked = candidates
            .Select(skill => (skill, score: EnemyCardScore(skill, unit, ChooseEnemyTarget(unit, skill), candidates, knowledge)))
            .OrderByDescending(entry => entry.score)
            .ToList();
        var best = ranked[0].score;
        // 只在分数接近时引入小扰动；明显更优的收割/防守方案仍然稳定优先。
        var pool = ranked.Where(entry => entry.score >= best - 32).ToList();
        return pool[(int)(GD.Randi() % (uint)pool.Count)].skill;
    }

    private GodotObject? ChooseEnemyTarget(GodotObject unit, ActiveSkillData skill)
    {
        if (skill.target_type is 0 or 1) return unit;
        var valid = get_target_candidates().Where(Alive).Where(target => IsValidTarget(skill, unit, target)).ToList();
        if (skill.target_type == 3)
        {
            var hostile = valid.Where(target => target.Get("faction").AsInt32() != unit.Get("faction").AsInt32()).ToList();
            if (hostile.Count > 0) valid = hostile;
        }
        return valid.OrderByDescending(target => EnemyTargetScore(skill, unit, target)).FirstOrDefault();
    }

    private static int EnemyTargetScore(ActiveSkillData skill, GodotObject attacker, GodotObject target)
    {
        var damage = EnemyExpectedDamage(skill, attacker, target);
        var score = damage * 12;
        if (damage >= target.Get("current_health").AsInt32()) score += 4200;
        var healthRatio = target.Get("current_health").AsSingle() / Mathf.Max(1, target.Get("max_health").AsSingle());
        if (damage > 0) score += Mathf.RoundToInt((1f - healthRatio) * 140f);
        return score;
    }

    private void BeginTurn(GodotObject unit)
    {
        gongfa_combat.Begin(unit);
        active_unit = unit; turn_state = unit.Get("faction").AsInt32() == 1 ? TurnState.PLAYER_TURN : TurnState.ENEMY_TURN;
        RefreshStamina(unit);
        RefreshMana(unit);
        foreach (var circuit in _formationCircuits.GetValueOrDefault(unit, [])) circuit.begin_owner_turn();
        if (unit.HasMethod("process_status_event")) unit.Call("process_status_event", "turn_start", new Dictionary { ["battle_manager"] = this, ["unit"] = unit });
        if(!Alive(unit)){_finish_current_action(unit);return;}
        if (turn_state == TurnState.PLAYER_TURN && !auto_battle) ShowPlayerHand(unit);
        else CallDeferred(nameof(RunAutoTurn));
    }

    private void ShowPlayerHand(GodotObject unit)
    {
        var deck = get_deck_manager(unit); if (deck is null || _handPanel is null) { _finish_current_action(unit); return; }
        _handPanel.Call("set_battle_unit", unit);
        var cards = StartHand(unit, deck);
        _hands[unit] = cards; _handPanel.Call("populate_hand", cards); _handPanel.Call("update_deck_info", deck.get_draw_count(), deck.get_discard_count(), deck.get_exhaust_count()); _handPanel.Call("show_hand");
    }

    public async void on_card_selected(ActiveSkillData skill, int _index)
    {
        if (_realtimeMode) { OnRealtimeCardSelected(skill); return; }
        if (turn_state != TurnState.PLAYER_TURN || !GodotObject.IsInstanceValid(active_unit) || !CanPay(active_unit!, skill)) return;
        _pendingInstantSkill=null;
        // 目标框只服务于玩家的当前选牌，不在敌方行动时显示。
        PreviewSkillTargets(skill, active_unit!, RequiresTargetSelection(skill) ? 2.5f : .65f);
        // 敌方/任意指定牌必须拖到场上目标；单击只保留选中态，不能偷偷打向第一个敌人。
        if (!CardRules.IsResponse(skill) && RequiresTargetSelection(skill)) return;
        _pendingInstantSkill=skill;
        // 自身牌没有拖拽阶段，点选后留出一小段明确的目标提示再结算。
        await ToSignal(GetTree().CreateTimer(.35f),SceneTreeTimer.SignalName.Timeout);
        if(_pendingInstantSkill!=skill||turn_state!=TurnState.PLAYER_TURN||active_unit is null)return;
        _pendingInstantSkill=null;
        var target = CardRules.IsResponse(skill)?active_unit:DefaultTarget(skill, active_unit!);
        TryPlaySkill(skill, target);
    }

    public void on_card_release_attempted(GodotObject card, Vector2 drop_viewport_pos)
    {
        if (_realtimeMode)
        {
            if (!GodotObject.IsInstanceValid(active_unit)) return;
            var realtimeSkill = card.Get("skill_data").AsGodotObject() as ActiveSkillData; if (realtimeSkill is null) return;
            var realtimeTarget = RequiresTargetSelection(realtimeSkill) ? pick_card_target(realtimeSkill, drop_viewport_pos) : DefaultTarget(realtimeSkill, active_unit!);
            if (RequiresTargetSelection(realtimeSkill) && !IsValidTarget(realtimeSkill, active_unit!, realtimeTarget)) return;
            var queued=QueueRealtimeCard(active_unit!, realtimeSkill, realtimeTarget);
            if(queued) unitOverlay?.ClearTargetFrames();
            return;
        }
        if ((turn_state != TurnState.PLAYER_TURN && turn_state != TurnState.PROCESSING) || !GodotObject.IsInstanceValid(active_unit) || active_unit!.Get("faction").AsInt32() != 1) return;
        var skill = card.Get("skill_data").AsGodotObject() as ActiveSkillData;
        if(skill is null)return;
        if(turn_state == TurnState.PROCESSING){QueuePlayerCard(skill,drop_viewport_pos);return;}
        if(CardRules.IsResponse(skill)||!RequiresTargetSelection(skill)){TryPlaySkill(skill,active_unit);return;}
        if(Map is null)return;
        var target = pick_card_target(skill, drop_viewport_pos);
        if (!IsValidTarget(skill, active_unit!, target)) return;
        TryPlaySkill(skill, target);
    }

    private void QueuePlayerCard(ActiveSkillData skill,Vector2 viewportPosition)
    {
        if(active_unit is null||!Hand(active_unit).Contains(skill)||_queuedPlayerCards.Any(item=>item.Skill==skill)||!CanPay(active_unit,skill))return;
        GodotObject? target=RequiresTargetSelection(skill)&&Map is not null?pick_card_target(skill,viewportPosition):active_unit;
        if(RequiresTargetSelection(skill)&&!IsValidTarget(skill,active_unit,target))return;
        _queuedPlayerCards.Enqueue(new QueuedPlayerCard{Skill=skill,Target=target});
        _handPanel?.Call("queue_card_for_play",skill,_queuedPlayerCards.Count-1);
    }

    /// <summary>拖拽时供手牌层实时绘制的目标；范围牌按鼠标当前指向的实际范围返回。</summary>
    public Array<GodotObject> get_card_drag_preview_targets(ActiveSkillData skill,Vector2 viewportPosition)
    {
        if(!GodotObject.IsInstanceValid(active_unit))return [];
        var caster=active_unit!;
        var allTargeting=skill.description.Contains("全体")||skill.description.Contains("随机");
        if(allTargeting)return new Array<GodotObject>(all_units.Where(Alive).Where(u=>u.Get("faction").AsInt32()!=caster.Get("faction").AsInt32()));
        if(skill.target_type is 0 or 1 || CardRules.IsResponse(skill))return new Array<GodotObject>{caster};
        var target=pick_card_target(skill,viewportPosition);
        if(skill.aoe_range>0&&IsValidTarget(skill,caster,target)&&Map is not null)
        {
            var cell=Map.Call("get_unit_cell",target!).AsVector2I();var cells=Map.Call("get_aoe_cells",cell,skill.aoe_range).AsGodotArray();
            return new Array<GodotObject>(cells.Select(value=>Map.Call("get_unit_at_cell",value).AsGodotObject()).Where(Alive).Where(unit=>unit!.Get("faction").AsInt32()!=caster.Get("faction").AsInt32()).Select(unit=>unit!));
        }
        return IsValidTarget(skill,caster,target)?new Array<GodotObject>{target!}:[];
    }

    private void PreviewSkillTargets(ActiveSkillData skill,GodotObject caster,float duration,GodotObject? explicitTarget=null)
    {
        if(unitOverlay is null)return;
        var allTargeting = skill.aoe_range>0 || skill.description.Contains("全体") || skill.description.Contains("随机");
        IEnumerable<GodotObject> targets;
        // 进入指定目标态时，先把所有合法候选目标都框出来；最终点击仍由 pick_card_target 决定。
        // 这样每个候选目标旁边都能同时看到各自的命中、会心和状态成功率。
        if(GodotObject.IsInstanceValid(explicitTarget) && IsValidTarget(skill,caster,explicitTarget)) targets=[explicitTarget!];
        else if(skill.target_type is 0 or 1 || CardRules.IsResponse(skill)) targets=[caster];
        else if(allTargeting || RequiresTargetSelection(skill)) targets=get_target_candidates().Where(Alive).Where(u=>IsValidTarget(skill,caster,u));
        else targets=[];
        unitOverlay.ShowTargetFrames(targets,allTargeting?new Color(1f,.48f,.20f):new Color(.45f,1f,.62f),duration,skill,caster);
    }

    public GodotObject? pick_card_target(ActiveSkillData skill, Vector2 viewportPosition)
    {
        if(active_unit is null)return null;
        // 按屏幕上的角色身体选取，避免只能拖到脚底的六边形格子。
        return get_target_candidates().OfType<Node2D>()
            .Where(unit=>IsValidTarget(skill,active_unit,unit))
            .Where(unit=>get_unit_screen_rect(unit).Grow(8).HasPoint(viewportPosition))
            .OrderBy(unit=>get_unit_screen_rect(unit).GetCenter().DistanceSquaredTo(viewportPosition)).FirstOrDefault();
    }

    private void TryPlaySkill(ActiveSkillData skill, GodotObject? target)
    {
        if (_realtimeMode)
        {
            if (active_unit is not null && QueueRealtimeCard(active_unit, skill, target)) unitOverlay?.ClearTargetFrames();
            return;
        }
        if (skill.skill_type == ActiveSkillData.SkillType.CURSE || active_unit is null || !Hand(active_unit).Contains(skill)) return;
        if (!GodotObject.IsInstanceValid(active_unit) || !GodotObject.IsInstanceValid(target) || !CanPay(active_unit!, skill)) return;
        Pay(active_unit!, skill); if(!CardRules.IsResponse(skill))gongfa_combat.Before(active_unit!,skill); turn_state = TurnState.PROCESSING;
        // “打出牌”与“打出术法”是两个事件；后者按现行费用制以实际支付法力为准。
        emit_formation_event(active_unit!, "card_played", "card");
        if (skill.mana_cost > 0) emit_formation_event(active_unit!, "spell_played", "spell");
        var hand = _hands.TryGetValue(active_unit!, out var current) ? current.AsGodotArray().Select(value => value.AsGodotObject()).OfType<ActiveSkillData>().ToArray() : [];
        var nextHand = new Array<ActiveSkillData>(hand); nextHand.Remove(skill); _hands[active_unit!] = nextHand;
        var deck = get_deck_manager(active_unit!);
        // 旧回合模式保留原卡牌结算；实时技能槽模式不会经过此分支。
        if (skill.keywords.Contains("消耗") || skill.skill_type == ActiveSkillData.SkillType.POWER) deck?.exhaust_card(skill); else deck?.discard(skill);
        _handPanel?.Call("confirm_card_played", skill);
        if(CardRules.IsResponse(skill)){_on_player_skill_done(new Dictionary());return;}
        if (skill.aoe_range > 0 && Map is not null)
        {
            var cell = Map.Call("get_unit_cell", target).AsVector2I();
            _executor!.execute_aoe_skill(active_unit!, skill, cell, Map, false);
        }
        else _executor!.execute_skill(active_unit!, skill, target!, false);
    }

    private void OnRealtimeCardSelected(ActiveSkillData skill)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(active_unit) || active_unit != _mainPlayerUnit || !is_skill_ready(active_unit!, skill)) return;
        // 先排招式顺序；确认后才依次选择多目标招式的目标。
        QueueRealtimeCard(active_unit!, skill, RequiresTargetSelection(skill) ? null : DefaultTarget(skill, active_unit!));
    }

    public void on_skill_selected(ActiveSkillData skill, int _index) => OnRealtimeCardSelected(skill);

    public void on_skill_target_selected(ActiveSkillData skill, Vector2 viewportPosition, int _index)
    {
        if (!can_player_plan() || !GodotObject.IsInstanceValid(active_unit) || active_unit != _mainPlayerUnit || !is_skill_ready(active_unit!, skill)) return;
        var target = pick_card_target(skill, viewportPosition);
        if (RequiresTargetSelection(skill) && !IsValidTarget(skill, active_unit!, target)) return;
        _pendingInstantSkill = null; _skillBar?.clear_targeting();
        unitOverlay?.ClearTargetFrames();
        QueueRealtimeCard(active_unit!, skill, target);
    }

    private static bool IsAutomaticScope(ActiveSkillData skill) => skill.description.Contains("全体") || skill.description.Contains("随机");

    private bool QueueRealtimeCard(GodotObject unit, ActiveSkillData skill, GodotObject? target)
    {
        var queue = get_battle_unit(unit).skill_queue;
        var planningPlayer = _segmentPhase == SegmentPhase.None || _segmentPhase == SegmentPhase.Planning && unit == _mainPlayerUnit && can_player_plan();
        if (!Alive(unit) || !planningPlayer || get_filled_action_slot_count(unit) >= get_action_slot_capacity(unit) || !is_skill_ready(unit, skill) || skill.skill_type == ActiveSkillData.SkillType.CURSE || !gongfa_combat.CanPlay(unit, skill)) return false;
        if (_segmentPhase == SegmentPhase.Planning && unit == _mainPlayerUnit)
            return PlacePlayerAction(new BattleQueuedSkill(skill, target));
        queue.Add(new BattleQueuedSkill(skill, target));
        if (unit == active_unit)
        {
            _skillBar?.refresh_slots();
        }
        if (_segmentPhase == SegmentPhase.Planning && unit == _mainPlayerUnit) QueueChanged();
        return true;
    }

    private void RunAutoTurn()
    {
        if (!GodotObject.IsInstanceValid(active_unit)) return;
        var unit = active_unit!; var target = FindTarget(unit);
        var deck = get_deck_manager(unit); if (deck is null || target is null) { _finish_current_action(unit); return; }
        var hand = StartHand(unit, deck); _hands[unit] = hand;
        // 玩家自动战斗仍保留原先的单牌节奏；敌方则会在同一个行动内连续筹划和出牌。
        if (unit.Get("faction").AsInt32() == 1) { PlayOneAutoCard(unit); return; }
        _enemyDecisionStates.Clear();
        PlayNextEnemyCard();
    }

    private void PlayOneAutoCard(GodotObject unit)
    {
        var skill = Hand(unit).FirstOrDefault(card => !CardRules.IsResponse(card) && card.skill_type == ActiveSkillData.SkillType.ATTACK && CanPay(unit, card));
        var target = FindTarget(unit);
        if (skill is null || target is null) { _finish_current_action(unit); return; }
        PlayAutoCard(unit, skill, target);
    }

    /// <summary>敌方会持续出牌，直到没有值得且可支付的牌；不设出牌数量上限。</summary>
    private void PlayNextEnemyCard()
    {
        if (turn_state != TurnState.ENEMY_TURN && turn_state != TurnState.PROCESSING) return;
        if (!GodotObject.IsInstanceValid(active_unit)) return;
        var unit = active_unit!;
        if (!Alive(unit)) { _finish_current_action(unit); return; }
        var candidates = Hand(unit).Where(card => !CardRules.IsResponse(card) && card.skill_type != ActiveSkillData.SkillType.CURSE && CanPay(unit, card)).ToList();
        if (candidates.Count == 0) { _finish_current_action(unit); return; }
        // 只有状态、资源、手牌完全回到同一组合时才停下，防止零费循环；正常连招没有次数限制。
        if(!_enemyDecisionStates.Add(EnemyDecisionState(unit,candidates))){_finish_current_action(unit);return;}
        var skill = candidates.OrderByDescending(card => EnemyCardScore(card, unit, DefaultTarget(card,unit), candidates)).First();
        var target = DefaultTarget(skill, unit);
        if (!GodotObject.IsInstanceValid(target)) { _finish_current_action(unit); return; }
        PlayAutoCard(unit, skill, target!);
    }

    private int EnemyCardScore(ActiveSkillData skill, GodotObject unit, GodotObject? target, List<ActiveSkillData> candidates, EnemyKnowledgeState? knowledge = null)
    {
        var energyCost=gongfa_combat.Cost(unit,skill);
        var remainingEnergy=unit.Get("current_stamina").AsInt32()-energyCost;
        var isSelf=skill.target_type is 0 or 1;
        var score=0;
        var knownThreat=knowledge is not null && IsKnownThreat(unit,knowledge);
        var recovery=skill.special_effects.Any(e=>e.GetValueOrDefault("effect","").AsString() is "restore_health_percent" or "shield" or "evasion" or "gain_qi_blood");

        // 先用必杀牌；范围牌按实际可能击杀人数和总伤害估值。
        var targets=skill.aoe_range>0?all_units.Where(Alive).Where(u=>u.Get("faction").AsInt32()!=unit.Get("faction").AsInt32()).ToArray():GodotObject.IsInstanceValid(target)?[target!]:[];
        var totalDamage=0;var kills=0;
        foreach(var victim in targets){var damage=EnemyExpectedDamage(skill,unit,victim);totalDamage+=damage;if(damage>=victim.Get("current_health").AsInt32())kills++;}
        if(totalDamage>0){score+=totalDamage*12+kills*4200;if(skill.skill_type==ActiveSkillData.SkillType.ATTACK)score+=260;}

        // 心法与增益只在尚未拥有对应状态时优先，不会反复浪费体力。
        if(skill.skill_type==ActiveSkillData.SkillType.POWER)
            score+=HasStatus(unit,"心法:"+skill.skill_name)||HasStatus(unit,skill.skill_name)?-8000:720;
        foreach(var effect in skill.status_effects)
        {
            var id=effect.GetValueOrDefault("status_id","").AsString();if(string.IsNullOrEmpty(id))continue;
            var owner=isSelf?unit:target;
            if(!GodotObject.IsInstanceValid(owner))continue;
            var has=HasStatus(owner!,id);
            score+=has?(effect.GetValueOrDefault("stacks",1).AsInt32()>=3?35:70):(isSelf?280:430);
            if(owner is Creature creature&&creature.status_snapshot().Any(s=>s.effect?.effect_id==id&&s.effect.is_debuff))score-=80;
        }

        // 残血时优先补自身的护持、回复和身法；满血时这些牌的优先级很低。
        var healthRatio=unit.Get("current_health").AsSingle()/Mathf.Max(1,unit.Get("max_health").AsSingle());
        if(isSelf&&totalDamage==0)
        {
            if(recovery)score+=healthRatio<.38f?700:healthRatio<.68f?270:-120;
            if(skill.special_effects.Any(e=>e.GetValueOrDefault("effect","").AsString()=="draw"))score+=150;
        }

        // 看破到玩家首招后才进入战术修正：威胁明确时倾向防守/恢复，
        // 不再盲目继续堆叠攻击；未知阶段不会获得这组信息。
        if(knownThreat)
        {
            if(recovery&&isSelf)score+=460;
            if(skill.skill_type==ActiveSkillData.SkillType.ATTACK)score-=120;
        }

        // 有后续可支付的伤害或控制牌时，保留能接出它的体力；必杀不会受此惩罚。
        var followup=candidates.Where(c=>c!=skill&&gongfa_combat.Cost(unit,c)<=remainingEnergy).ToList();
        if(followup.Count>0)score+=80;
        else if(kills==0&&remainingEnergy==0&&totalDamage<80)score-=90;
        score-=energyCost*18;
        return score;
    }

    private static bool HasStatus(GodotObject unit,string id)=>unit.HasMethod("get_status_stacks")&&unit.Call("get_status_stacks",id).AsInt32()>0;
    private static int EnemyExpectedDamage(ActiveSkillData skill,GodotObject attacker,GodotObject defender)
    {
        if(Mathf.IsZeroApprox(skill.base_damage)&&skill.damage_bonuses.Count==0&&string.IsNullOrWhiteSpace(skill.damage_formula))return 0;
        if (defender is ArtifactBody) return skill.damage_type is "physical" or "magic" or "magical" ? Mathf.Max(0, Mathf.RoundToInt(BattleActionResolver.preview_damage(attacker, skill))) : 0;
        var segments=1;
        if(Engine.GetMainLoop() is SceneTree tree&&tree.GetFirstNodeInGroup("battle_mgr") is BattleManager manager)
            segments=Mathf.Max(1,manager.gongfa_combat.Segments(attacker,skill));
        var blockRemaining=skill.damage_type=="physical"?GetStatus(defender,"化解"):0;
        var shield=Mathf.Max(0,defender.Get("shield").AsInt32());
        var total=0;
        for(var i=0;i<segments;i++)
        {
            var raw=Mathf.Max(1,Mathf.RoundToInt(BattleActionResolver.preview_damage(attacker,skill)));
            if(skill.damage_type=="physical")
            {
                var blocked=Mathf.Min(blockRemaining,raw);
                blockRemaining-=blocked;
                raw=BattleActionResolver.apply_defense(raw-blocked,"physical",defender, skill.physical_defense_ignore_ratio);
            }
            var absorbed=Mathf.Min(shield,raw);
            shield-=absorbed;
            total+=raw-absorbed;
        }
        return total;
    }
    private static int GetStatus(GodotObject unit,string id)=>unit.HasMethod("get_status_stacks")?unit.Call("get_status_stacks",id).AsInt32():0;
    private static string EnemyDecisionState(GodotObject unit,IEnumerable<ActiveSkillData> candidates)
        =>$"{unit.Get("current_stamina").AsInt32()}:{unit.Get("current_mana").AsInt32()}:{string.Join("|",candidates.Select(c=>string.IsNullOrEmpty(c.ResourcePath)?c.skill_name:c.ResourcePath).OrderBy(x=>x))}";

    private void PlayAutoCard(GodotObject unit, ActiveSkillData skill, GodotObject target)
    {
        var deck = get_deck_manager(unit); if (deck is null) { _finish_current_action(unit); return; }
        var hand = Hand(unit); hand.Remove(skill); _hands[unit] = hand;
        Pay(unit, skill); gongfa_combat.Before(unit, skill); turn_state = TurnState.PROCESSING;
        if (skill.keywords.Contains("消耗") || skill.skill_type == ActiveSkillData.SkillType.POWER) deck.exhaust_card(skill); else deck.discard(skill);
        if (unit.Get("faction").AsInt32() != (int)WorldEntity.Faction.PLAYER) unitOverlay?.ShowPlayedCard(unit, skill);
        if (skill.aoe_range > 0 && Map is not null)
            _executor!.execute_aoe_skill(unit, skill, Map.Call("get_unit_cell", target).AsVector2I(), Map, false);
        else _executor!.execute_skill(unit, skill, target, false);
    }
}
