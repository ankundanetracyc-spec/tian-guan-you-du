using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    public override void _Input(InputEvent @event)
    {
        if(@event is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Space}&&_realtimeMode&&battle_running)
        {
            toggle_battle_pause();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_realtimeMode && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse && _pendingInstantSkill is not null && GodotObject.IsInstanceValid(active_unit))
        {
            var skill = _pendingInstantSkill;
            var target = pick_card_target(skill, mouse.Position);
            if (_selectingQueueTargets)
            {
                if (AcceptQueueTarget(target)) GetViewport().SetInputAsHandled();
                return;
            }
            if (IsValidTarget(skill, active_unit!, target))
            {
                _pendingInstantSkill = null; _skillBar?.clear_targeting();
                unitOverlay?.ClearTargetFrames();
                QueueRealtimeCard(active_unit!, skill, target);
                GetViewport().SetInputAsHandled(); return;
            }
        }
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) { if (!UndoQueuePlanning()) finish_battle(); GetViewport().SetInputAsHandled(); }
    }
    private bool CheckBattleOver()
    {
        // 空队伍是入口或资源配置错误，不是胜负结果；保留场景并输出错误，避免画面闪回导致问题被掩盖。
        if (player_units.Count == 0 || enemy_units.Count == 0)
        {
            GD.PushError($"BattleManager: 战斗队伍为空（我方 {player_units.Count}，敌方 {enemy_units.Count}）");
            return false;
        }
        if (player_units.All(unit => !Alive(unit)) || enemy_units.All(unit => !Alive(unit))) { finish_battle(); return true; }
        return false;
    }
    /// <summary>回传双方的最终快照；Esc 作为测试期的安全退出，正式结束也会走同一出口。</summary>
    private void finish_battle()
    {
        if (_returning_to_world)return; SetBattlePaused(false); _returning_to_world=true; battle_running=false; _segmentPhase=SegmentPhase.None; _skillBar?.Hide(); _skillBar?.clear_targeting(); _planHud?.Hide(); _handPanel?.Call("hide_hand");
        var attackers=new Godot.Collections.Array(); var defenders=new Godot.Collections.Array();
        foreach(var unit in player_units.Concat(enemy_units))
            if(GodotObject.IsInstanceValid(unit) && unit.HasMeta("unwounded_health"))
            {unit.Set("max_health",unit.GetMeta("unwounded_health"));unit.RemoveMeta("unwounded_health");}
        RestoreBattleResources();
        foreach(var unit in player_units)if(GodotObject.IsInstanceValid(unit))attackers.Add(unit.Call("get_snapshot_data"));
        foreach(var unit in enemy_units)if(GodotObject.IsInstanceValid(unit))defenders.Add(unit.Call("get_snapshot_data"));
        GetTree().Root.GetNodeOrNull<BattleTransitionManager>("BattleTransitionManager")?.end_team_battle(attackers,defenders);
    }
    private GodotObject? FindTarget(GodotObject unit) => (unit.Get("faction").AsInt32() == 1 ? enemy_units : player_units).FirstOrDefault(Alive);
    private static bool RequiresTargetSelection(ActiveSkillData skill) => skill.target_type is 2 or 3 || skill.can_target_empty || skill.aoe_range > 0;
    private GodotObject? DefaultTarget(ActiveSkillData skill, GodotObject caster) => skill.target_type == 0 || skill.target_type == 1 ? caster : FindTarget(caster);
    private static bool IsValidTarget(ActiveSkillData skill, GodotObject caster, GodotObject? target)
    {
        if (!GodotObject.IsInstanceValid(target) || !Alive(target)) return false;
        var sameFaction = target!.Get("faction").AsInt32() == caster.Get("faction").AsInt32();
        return skill.target_type switch { 2 => !sameFaction, 3 => true, 1 => sameFaction, _ => false };
    }
    private static bool Alive(GodotObject? unit) => GodotObject.IsInstanceValid(unit) && unit!.Get("current_health").AsInt32() > 0;
    /// <summary>战斗最终行动速度：常驻属性与状态先作固定加减，手中每张伤势再按比例减速（最多 30%）。</summary>
    private float Initiative(GodotObject unit)
    {
        var fixedSpeed = CardRules.Number(unit, "initiative_speed");
        Godot.Collections.Array woundCards = [];
        foreach (var card in Hand(unit)) woundCards.Add(card);
        var woundMultiplier = BattleWoundRules.get_action_speed_multiplier(BattleWoundRules.count_wounds_in_skills(woundCards));
        var speciesMultiplier=unit.HasMethod("get_species_action_speed_multiplier")?unit.Call("get_species_action_speed_multiplier").AsSingle():1f;
        return Mathf.Max(20f, fixedSpeed * speciesMultiplier * woundMultiplier);
    }
    /// <summary>林地凶狼的群猎本能：主动攻击后，只给同阵营的其他同种凶狼叠加逐猎。</summary>
    public void trigger_species_attack_talent(GodotObject attacker)
    {
        if(attacker is not Beast { species_id: "forest_wolf" })return;
        var status=GD.Load<StatusEffect>("res://resources/battle/statuses/逐猎.tres");if(status is null)return;
        foreach(var ally in all_units.Where(Alive).Where(unit=>unit!=attacker&&unit.Get("faction").AsInt32()==attacker.Get("faction").AsInt32()).OfType<Beast>().Where(wolf=>wolf.species_id=="forest_wolf"))
            ally.add_status(status,-1,1,attacker.GetInstanceId().ToString(),3,attacker.Get("entity_name").AsString());
    }
    public bool CanPay(GodotObject unit, ActiveSkillData skill)
    {
        // 即时队列允许资源尚不足的牌先排队；资源门槛在其真正开始准备时检查。
        return body_cards.CanPlay(unit,skill) && (_realtimeMode || (unit.Get("current_stamina").AsInt32() >= body_cards.Cost(unit,skill) && unit.Get("current_mana").AsInt32() >= skill.mana_cost));
    }
    private void Pay(GodotObject unit, ActiveSkillData skill)
    {
        unit.Set("current_stamina", Mathf.Max(0, unit.Get("current_stamina").AsInt32() - get_skill_energy_cost(unit,skill)));
        unit.Set("current_mana", Mathf.Max(0, unit.Get("current_mana").AsInt32() - get_skill_mana_cost(unit,skill)));
    }
    private static void RefreshStamina(GodotObject unit)
    {
        var maximum = Mathf.Max(0, unit.Get("max_stamina").AsInt32());
        unit.Set("current_stamina", Mathf.Min(maximum, unit.Get("current_stamina").AsInt32() + 1));
    }
    private static void RefreshMana(GodotObject unit) => unit.Set("current_mana", Mathf.Max(0, unit.Get("max_mana").AsInt32()));
    private int FactionCount(int faction) => all_units.Count(unit => unit.Get("faction").AsInt32() == faction);
    private static void AddPaperDoll(Node unit, BattleEntityData snapshot)
    {
        if (unit is not Node2D node) return;
        node.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.QueueFree();
        var doll = new BattlePaperDoll();
        node.AddChild(doll);
        doll.Configure(snapshot);
        BattleShieldAura.AttachTo(node);
    }
    // 不设前后排：双方各占固定横向位置，单位只围绕中位交错向上、向下展开。
    private static Vector2I FormationCell(int faction, int index)
    {
        var verticalOffsets=new[]{0,-1,1,-2,2,-3,3,-4,4,-5,5};
        var lane=verticalOffsets[Mathf.Min(index,verticalOffsets.Length-1)];
        var player=faction==1;
        // q 决定双方间距；r 配合 q/2 让两个阵营的第一个单位同高。
        // 我方固定在屏幕左侧朝右，敌方固定在屏幕右侧朝左。
        var q=player?-2:2;
        var r=(player?1:-1)+lane;
        return new Vector2I(q,r);
    }
    private void RestoreBattleResources()
    {
        foreach (var pair in _resourceSnapshots.ToArray())
            if (GodotObject.IsInstanceValid(pair.Key))
            {
                pair.Key.Set("current_stamina", pair.Value.CurrentStamina);
                pair.Key.Set("max_stamina", pair.Value.MaxStamina);
                pair.Key.Set("current_mana", pair.Value.CurrentMana);
                pair.Key.Set("max_mana", pair.Value.MaxMana);
            }
        _resourceSnapshots.Clear();
    }
    private void ResetBattle()
    {
        SetBattlePaused(false);
        _battleVisualElapsedSeconds=0;
        _segmentPhase=SegmentPhase.None; _planningElapsed=0; _playerQueueConfirmed=false; _mainPlayerUnit=null; _resolvingUnit=null; _segmentNumber=0;
        _selectingQueueTargets=false; _targetQueueIndex=-1; _targetSelectionHistory.Clear(); _layoutViewportSize=Vector2.Zero;
        RestoreBattleResources();
        body_cards.Reset();
        _chengjiResponses.Clear();
        _queuedPlayerCards.Clear();
        _livingUnits.Clear();
        _energyRegen.Clear();
        _manaRegen.Clear();
        _drawElapsedFrames.Clear();
        _enemyPlanTimers.Clear();
        _enemyKnowledge.Clear();
        _combatSkills.Clear();
        _skillCooldowns.Clear();
        _skillCharges.Clear();
        _pendingTargetSkills.Clear();
        _returning_to_world = false;
        _skillBar?.Hide();
        _planHud?.Hide();
        foreach (var value in _decks.Values)
            if (value.AsGodotObject() is Node node) node.QueueFree();
        _decks.Clear();
        _hands.Clear();
        _firstHands.Clear();
        _statusPanel?.clear_all();
        player_units.Clear();
        enemy_units.Clear();
        all_units.Clear();
        action_gauges.Clear();
        battle_units.Clear();
        Map?.Call("clear_occupancy");
    }
}
