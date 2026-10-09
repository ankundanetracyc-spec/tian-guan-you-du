using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    public BattleDeckManager? get_deck_manager(GodotObject unit) => _decks.TryGetValue(unit, out var deck) ? deck.AsGodotObject() as BattleDeckManager : null;
    /// <summary>将由当前卡牌结算获得的手牌立刻加入本次行动的手牌，并同步界面。</summary>
    public void draw_cards_to_hand(GodotObject unit, int count)
    {
        if (_realtimeMode) return; // 即时战斗没有牌堆；旧卡牌效果由后续技能化迁移单独处理。
        var deck = get_deck_manager(unit); if (deck is null || count <= 0) return;
        if (!_hands.TryGetValue(unit, out var current)) return;
        var drawn = deck.draw(count);
        var hand = current.AsGodotArray().Select(value => value.AsGodotObject()).OfType<ActiveSkillData>().ToList();
        hand.AddRange(drawn);
        _hands[unit] = new Array<ActiveSkillData>(hand);
        if (unit == active_unit)
        {
            _handPanel?.Call("populate_hand", _hands[unit]);
            _handPanel?.Call("update_deck_info", deck.get_draw_count(), deck.get_discard_count(), deck.get_exhaust_count());
        }
    }
    public BattleUnit get_battle_unit(GodotObject unit) => battle_units.TryGetValue(unit, out var state) ? state.AsGodotObject() as BattleUnit ?? new BattleUnit() : new BattleUnit();
    public void _on_player_skill_done(Dictionary _context)
    {
        if (turn_state != TurnState.PROCESSING || !GodotObject.IsInstanceValid(active_unit)) return;
        if(_queuedPlayerCards.Count>0){PlayQueuedPlayerCard();return;}
        turn_state = TurnState.PLAYER_TURN;
    }

    private void PlayQueuedPlayerCard()
    {
        if(!GodotObject.IsInstanceValid(active_unit)||_queuedPlayerCards.Count==0){turn_state=TurnState.PLAYER_TURN;return;}
        var queued=_queuedPlayerCards.Dequeue();
        if(!Hand(active_unit!).Contains(queued.Skill)||!CanPay(active_unit!,queued.Skill))
        {
            _handPanel?.Call("cancel_queued_card",queued.Skill);
            if(_queuedPlayerCards.Count>0){PlayQueuedPlayerCard();return;}
            turn_state=TurnState.PLAYER_TURN;return;
        }
        var target=GodotObject.IsInstanceValid(queued.Target)&&Alive(queued.Target)?queued.Target:DefaultTarget(queued.Skill,active_unit!);
        if(!GodotObject.IsInstanceValid(target)){_handPanel?.Call("cancel_queued_card",queued.Skill);turn_state=TurnState.PLAYER_TURN;return;}
        TryPlaySkill(queued.Skill,target);
    }
    public async void _on_enemy_skill_done(GodotObject unit, bool _targetKilled)
    {
        if (turn_state != TurnState.PROCESSING || active_unit != unit) return;
        // 异步结算结束后再取下一张，确保命中、应对和死亡动画完整落地。
        turn_state = TurnState.ENEMY_TURN;
        // 每张敌牌之间保留读牌节奏，头顶牌面和伤害反馈不会连成一团。
        await ToSignal(GetTree().CreateTimer(.85f), SceneTreeTimer.SignalName.Timeout);
        if (turn_state == TurnState.ENEMY_TURN && active_unit == unit) PlayNextEnemyCard();
    }
    public void _finish_current_action(GodotObject unit)
    {
        if(unit is Creature creature)creature.process_status_event("turn_end",new Dictionary{["battle_manager"]=this,["unit"]=unit});
        if(!_realtimeMode)body_cards.Fall(unit,1,false);
        unit.Set("current_mana", 0); action_gauges[unit] = 0f; active_unit = null; turn_state = TurnState.IDLE;
    }
    private Array<ActiveSkillData> Hand(GodotObject unit) => _hands.TryGetValue(unit,out var value)
        ? new(value.AsGodotArray().Select(v=>v.AsGodotObject()).OfType<ActiveSkillData>()) : [];
    public Array<ActiveSkillData> hand_cards(GodotObject unit)=>Hand(unit);
    private Array<ActiveSkillData> StartHand(GodotObject unit, BattleDeckManager deck)
    {
        var cards=deck.discard_hand_at_turn_start(Hand(unit));
        if(!_firstHands.ContainsKey(unit)){cards.AddRange(deck.draw_initial_hand());_firstHands[unit]=true;}
        else cards.AddRange(deck.draw_for_turn());
        return cards;
    }
    public void mark_unit_damaged_this_turn(GodotObject unit) { }
    public void resolve_pending_counter_followups(GodotObject target, Dictionary context)
    {
        body_cards.ResponseResolved(target,context.GetValueOrDefault("blocked_damage",0).AsInt32(),context.GetValueOrDefault("damage",0).AsInt32(),_chengjiResponses.Remove(target.GetInstanceId()));
    }
    /// <summary>攻击命中判定前自动触发一张可用的“应对”牌。</summary>
    public bool try_auto_response(GodotObject defender, GodotObject attacker, string damageType = "physical")
    {
        if (_realtimeMode)
        {
            if (_segmentPhase == SegmentPhase.Executing)
            {
                if (!Alive(defender) || !Alive(attacker) || !_queuedHandledTargets.Add(defender.GetInstanceId())) return false;
                var segmentQueue = get_battle_unit(defender).skill_queue;
                if (segmentQueue.Count == 0 || segmentQueue[0].kind != BattleQueuedSkill.ActionKind.Skill ||
                    !CardRules.ResponseMatches(segmentQueue[0].skill, damageType))
                {
                    _queuedHandledTargets.Remove(defender.GetInstanceId());
                    return false;
                }

                var queued = segmentQueue[0];
                if (!CanResolveSegmentAction(defender, queued))
                {

                    SkipSegmentAction(defender);
                    return false;
                }

                Pay(defender, queued.skill);
                body_cards.Before(defender, queued.skill);
                body_cards.OnResponseTriggered(defender, queued.skill);
                StartSkillCooldown(defender, queued.skill);
                segmentQueue.RemoveAt(0);
                action_gauges[defender] = 0f;
                if (defender == _mainPlayerUnit) _planHud?.RefreshPlayerQueue();
                if (body_cards.Utility(defender, queued.skill)) _executor?.execute_counter_extras(defender, attacker, queued.skill);
                else _executor?.execute_segment_response(defender, attacker, queued.skill);

                _skillBar?.refresh_slots();
                return true;
            }

            var queue = get_battle_unit(defender).skill_queue;
            if (queue.Count == 0 || queue[0].phase != BattleQueuedSkill.Phase.HoldingResponse) return false;
            var armed = queue[0]; if (!CardRules.ResponseMatches(armed.skill, damageType)) return false;
            if (!_realtimeMode) unitOverlay?.ShowResponse(defender, armed.skill);
            armed.phase = BattleQueuedSkill.Phase.Resolving;
            body_cards.Before(defender,armed.skill);
            body_cards.OnResponseTriggered(defender,armed.skill);
            StartSkillCooldown(defender, armed.skill);
            _executor?.execute_response(defender, attacker, armed.skill);
            if (defender == active_unit) _skillBar?.refresh_slots();
            armed.phase = BattleQueuedSkill.Phase.Recovering; armed.elapsed_frames = 0;
            return true;
        }
        if (!Alive(defender) || !Alive(attacker) || _executor is null) return false;
        var deck = get_deck_manager(defender); if (deck is null) return false;
        var hand=Hand(defender);
        var response = hand.FirstOrDefault(card => CardRules.ResponseMatches(card,damageType) && CanPay(defender, card));
        if (response is null) return false;
        hand.Remove(response); _hands[defender]=hand; Pay(defender, response);
        if(response.keywords.Contains("消耗"))deck.exhaust_card(response);else deck.discard(response);
        if(response.skill_name=="承机")_chengjiResponses.Add(defender.GetInstanceId());
        // 观察态下仅移走触发的应对牌，重建整手牌会造成所有卡牌抽动。
        if(_handPanel is SkillHandPanel panel && panel.current_unit==defender)panel.confirm_auto_response(response);
        unitOverlay?.ShowResponse(defender,response);
        _executor.execute_response(defender, attacker, response);
        return true;
    }
    public void show_damage_number(GodotObject target, int amount, string damageType = "physical", bool critical = false)
    {
        if (_executor is not null && amount > 0) _executor.show_damage_number(target, amount, damageType, critical);
    }
    /// <summary>供周天、受击等战斗子系统接入。阵脉是瞬时事件，不会在此保存或扣除。</summary>
}
