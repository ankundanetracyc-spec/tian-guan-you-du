using Godot;
using Godot.Collections;

/// <summary>C# implementation of the SkillExecutor execution chain for BattleManager.</summary>
public partial class SkillExecutor : Node
{
    private static readonly Color Physical = Colors.White, Spiritual = new(.76f, .34f, 1f),
        Magic = new(.30f, .68f, 1f), Critical = new(.96f, .22f, .19f), Recovery = new(.38f, .87f, .46f);
    private readonly PackedScene? _floatingText = GD.Load<PackedScene>("res://tscn/battle/FloatingText.tscn");
    private readonly System.Collections.Generic.Dictionary<Node2D,(Vector2 Position,int ZIndex)> _attackHomes=[];
    private readonly System.Collections.Generic.Dictionary<Node2D,BattleInkCombatVfx> _incomingAttackVisuals=[];
    private async System.Threading.Tasks.Task PresentationWait(float seconds)
    {
        if(IsInsideTree())await ToSignal(GetTree().CreateTimer(seconds,processAlways:false),SceneTreeTimer.SignalName.Timeout);
    }

    public async void execute_skill(GodotObject attacker, ActiveSkillData skill, GodotObject target, bool is_counter = false)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { Finish(attacker, is_counter, false); return; }
        show_action_name(attacker, skill.skill_name);
        // 术诀和守御牌不走命中判定，避免无伤害牌被最低 1 点伤害规则误伤自身。
        if (!DealsDamage(skill))
        {
            await PresentationWait(.12f);
            if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target)){Finish(attacker,is_counter,false);return;}
            BattleInkCombatVfx.PlayUtility(attacker, target, skill);
            if(GetManager() is BattleManager bodyManager && bodyManager.gongfa_combat.Utility(attacker,skill)){await SettleAction(attacker,skill);Finish(attacker,is_counter,false);return;}
            ApplyStatusEffects(skill, attacker, target);
            ApplySelfSpecialEffects(skill, attacker);
            // 未配置独立状态资源的心法也必须以永久状态呈现，不能只在运行时静默生效。
            if (skill.skill_type == ActiveSkillData.SkillType.POWER && skill.status_effects.Count == 0) AddPowerStatus(attacker, skill);
            await SettleAction(attacker,skill);
            Finish(attacker, is_counter, false);
            return;
        }
        (GetManager() as BattleManager)?.gongfa_combat.PrepareAttack(attacker, skill);
        (GetManager() as BattleManager)?.gongfa_combat.PrepareRedSunAttack(attacker, skill);
        await Lunge(attacker, target, skill);
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { await SettleAction(attacker,skill);Finish(attacker, is_counter, false); return; }
        var killed=false;
        var manager=GetManager();
        var body=(manager as BattleManager)?.gongfa_combat;
        // 等真实队首应对的定格与特写结束，再继续本次来袭伤害。
        await CompleteAttackContact(attacker, target, skill, is_counter);
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { await SettleAction(attacker,skill);Finish(attacker, is_counter, false); return; }
        var segments=body?.Segments(attacker,skill)??1;
        var evaded=body?.EvadeWithYouhai(target,attacker,skill)??false;
        var blockRemaining=skill.damage_type=="physical"?GetStatusStacks(target,"化解"):0;
        if(evaded)
        {
            if(blockRemaining>0)ConsumeStatus(target,"化解",blockRemaining);
            FloatingText(target,"游骸",Colors.Yellow);
            BattleInkCombatVfx.PlayImpact(target, skill, attacker, evaded: true);
        }
        else
        {
            for(var i=0;i<segments&&!killed;i++)killed=ApplyToTarget(attacker,skill,target,is_counter,ref blockRemaining,i);
            if(blockRemaining>0)ConsumeStatus(target,"化解",blockRemaining);
        }
        body?.EndIncomingAttack(target);
        body?.AfterAttack(attacker,skill);
        if(!is_counter&&skill.skill_type==ActiveSkillData.SkillType.ATTACK&&skill.damage_type=="physical")GetManager()?.Call("trigger_species_attack_talent",attacker);
        await SettleAction(attacker,skill);
        Finish(attacker, is_counter, killed);
    }

    public async void execute_aoe_skill(GodotObject attacker, ActiveSkillData skill, Vector2I target_cell, GodotObject battle_map, bool is_counter = false)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(battle_map)) { Finish(attacker, is_counter, false); return; }
        show_action_name(attacker, skill.skill_name);
        var killed = false;
        var cells = battle_map.Call("get_aoe_cells", target_cell, skill.aoe_range).AsGodotArray();
        var seen = new HashSet<ulong>();
        var body=(GetManager() as BattleManager)?.gongfa_combat;
        var attackPrepared=false;
        foreach (var value in cells)
        {
            var target = battle_map.Call("get_unit_at_cell", value).AsGodotObject();
            if (!GodotObject.IsInstanceValid(target) || target!.Get("current_health").AsInt32() <= 0 || target.Get("faction").AsInt32() == attacker.Get("faction").AsInt32() || !seen.Add(target.GetInstanceId())) continue;
            if(!attackPrepared){body?.PrepareAttack(attacker,skill);attackPrepared=true;}
            await Lunge(attacker, target, skill);
            if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target)){await SettleAction(attacker,skill);continue;}
            await CompleteAttackContact(attacker, target, skill, is_counter);
            if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target)){await SettleAction(attacker,skill);continue;}
            var blockRemaining=skill.damage_type=="physical"?GetStatusStacks(target!,"化解"):0;
            if(body?.EvadeWithYouhai(target,attacker,skill)==true)
            {
                if(blockRemaining>0)ConsumeStatus(target!,"化解",blockRemaining);
                FloatingText(target,"游骸",Colors.Yellow);
                BattleInkCombatVfx.PlayImpact(target, skill, attacker, evaded: true);
                body.EndIncomingAttack(target!);
                await SettleAction(attacker,skill);
                continue;
            }
            killed |= ApplyToTarget(attacker, skill, target, is_counter,ref blockRemaining);
            if(blockRemaining>0)ConsumeStatus(target!,"化解",blockRemaining);
            body?.EndIncomingAttack(target!);
            await SettleAction(attacker,skill);
        }
        if(!is_counter&&skill.skill_type==ActiveSkillData.SkillType.ATTACK&&skill.damage_type=="physical")GetManager()?.Call("trigger_species_attack_talent",attacker);
        Finish(attacker, is_counter, killed);
    }

    private bool ApplyToTarget(GodotObject attacker, ActiveSkillData skill, GodotObject target, bool isCounter, ref int blockRemaining, int segment = 0)
    {
        var body=(GetManager() as BattleManager)?.gongfa_combat;
        var segmentSkill=body?.ForDamageSegment(skill,segment)??skill;
        var result = BattleActionResolver.resolve_hit(attacker, target, segmentSkill, blockRemaining);
        blockRemaining=Mathf.Max(0,blockRemaining-result.BlockedDamage);
        if (result.BlockedDamage > 0) ConsumeStatus(target, "化解", result.BlockedDamage);
        if (result.Evaded)
        {
            BattleInkCombatVfx.PlayImpact(target, skill, attacker, evaded: true);
            FloatingText(target, "闪避！", Colors.Yellow);
            ResolveCounter(target, attacker, skill, 0, 0, isCounter);
            return false;
        }

        var context = new Dictionary { ["battle_manager"] = GetManager()!, ["attacker"] = attacker, ["target"] = target, ["skill"] = skill, ["damage"] = result.DamageBeforeShield, ["damage_type"] = skill.damage_type, ["is_counter"] = isCounter };
        context = ProcessStatus(attacker, "before_damage", context);
        context = ProcessStatus(target, "before_damage", context);
        var damage = Mathf.Max(context["damage"].AsInt32(), 0);
        if (skill.keywords.Contains("重击") && attacker.Get("punch_frame").AsInt32() > 0)
        {
            attacker.Set("punch_frame", attacker.Get("punch_frame").AsInt32() - 1);
        }
        var shield = target.Get("shield").AsInt32();
        var absorbed = Mathf.Min(shield, damage);
        target.Set("shield", shield - absorbed);
        damage -= absorbed;
        target.Set("current_health", Mathf.Max(0, target.Get("current_health").AsInt32() - damage));
        body?.OnHealthDamageReceived(target, damage);
        if (damage > 0 && attacker.Get("faction").AsInt32() != target.Get("faction").AsInt32() && GetManager() is BattleManager woundManager && woundManager.is_realtime_battle()) woundManager.add_wound_card(target, "外伤", 1);
        if (absorbed > 0 && target is Node shieldNode)
            shieldNode.GetNodeOrNull<BattleShieldAura>("BattleShieldAura")?.PlayImpact((GetPosition(target) - GetPosition(attacker)).Normalized());
        if (target.HasMethod("update_ui")) target.Call("update_ui");
        if (damage > 0 && target.HasMethod("gain_blood_furnace_from_hit")) target.Call("gain_blood_furnace_from_hit");
        context["damage"] = damage; context["blocked_damage"] = result.BlockedDamage;
        context = ProcessStatus(target, "after_damage", context);
        ProcessStatus(attacker, "after_damage", context);
        ResolveCounter(target, attacker, skill, damage, result.BlockedDamage, isCounter);
        if (result.BlockedDamage > 0)
            FloatingText(target, $"化解 {result.BlockedDamage}", new Color(.73f, .81f, .76f));
        GetManager()?.Call("mark_unit_damaged_this_turn", target);
        ApplyStatusEffects(skill, attacker, target);
        ApplySelfSpecialEffects(skill, attacker);
        ApplyWounds(skill, target);
        if (damage > 0) BattleInkSlashEffect.PlayImpactForSkill(target, skill, result.Critical, attacker, segment);
        var hitDirection = (GetPosition(target) - GetPosition(attacker)).Normalized();
        if (damage > 0 && target is Node targetNode) targetNode.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(hitDirection, BattleInkCombatVfx.HitWeight(skill), result.Critical);
        if (target.HasMethod("hit_flash")) target.Call("hit_flash", hitDirection);
        show_damage_number(target, damage, skill.damage_type, result.Critical);
        if (target.Get("current_health").AsInt32() > 0) return false;
        if (target is Node deathNode && !deathNode.HasMeta("battle_death_ink_played"))
        {
            deathNode.SetMeta("battle_death_ink_played", true);
            BattleInkSlashEffect.PlayDeath(target);
        }
        if (target.HasMethod("play_death_animation")) target.Call("play_death_animation");
        return true;
    }

    private void ApplyStatusEffects(ActiveSkillData skill, GodotObject caster, GodotObject target)
    {
        foreach (var effect in skill.status_effects)
        {
            var id = effect.TryGetValue("status_id", out var idValue) ? idValue.AsString() : "";
            if (string.IsNullOrEmpty(id)) continue;
            var formula = effect.TryGetValue("hit_formula", out var formulaValue) ? formulaValue.AsString() : "1.0";
            if (GD.Randf() >= BattleActionResolver.evaluate_formula(formula, caster, target)) { FloatingText(target, "抵抗", Colors.Gray); continue; }
            var status = GD.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
            if (status is null || !target.HasMethod("add_status")) continue;
            var stacks = effect.TryGetValue("stacks", out var stackValue) ? stackValue.AsInt32() : 1;
            if(id=="化解")stacks=CardRules.Block(skill,caster,stacks)+((GetManager() as BattleManager)?.gongfa_combat.AdditionalResponseBlock(caster,skill)??0);
            target.Call("add_status", status, -1, Mathf.Max(1, stacks), caster.GetInstanceId().ToString(), status.max_stacks, caster.Get("entity_name").AsString());
        }
    }

    private void ApplyWounds(ActiveSkillData skill, GodotObject target)
    {
        var manager = GetManager(); if (manager is null) return;
        foreach (var effect in skill.special_effects)
            if (effect.TryGetValue("effect", out var type) && type.AsString() == "add_wound")
                manager.Call("add_wound_card", target, effect.GetValueOrDefault("wound", "").AsString(), effect.GetValueOrDefault("count", 1).AsInt32(), effect.GetValueOrDefault("destination", "discard").AsString());
    }

    private void ApplySelfSpecialEffects(ActiveSkillData skill, GodotObject caster)
        => ApplySelfSpecialEffects(skill, caster, null);
    private void ApplySelfSpecialEffects(ActiveSkillData skill, GodotObject caster, GodotObject? attacker)
    {
        foreach (var effect in skill.special_effects)
        {
            if (!effect.TryGetValue("effect", out var value)) continue;
            var count = Mathf.Max(1, effect.GetValueOrDefault("count", 1).AsInt32());
            switch (value.AsString())
            {
                case "gain_qi_blood" when caster.HasMethod("gain_qi_blood"):
                    caster.Call("gain_qi_blood", count);
                    break;
                case "spend_qi_blood" when caster.HasMethod("spend_qi_blood"):
                    caster.Call("spend_qi_blood", count);
                    break;
                case "restore_health_percent":
                    var heal = Mathf.RoundToInt(caster.Get("max_health").AsInt32() * effect.GetValueOrDefault("percent", .03f).AsSingle());
                    if (caster.HasMethod("apply_healing")) caster.Call("apply_healing", Mathf.Max(1, heal));
                    else caster.Set("current_health", Mathf.Min(caster.Get("max_health").AsInt32(), caster.Get("current_health").AsInt32() + Mathf.Max(1, heal)));
                    if (caster.HasMethod("update_ui")) caster.Call("update_ui");
                    break;
                case "gain_blood_furnace":
                    var cap = caster.Get("blood_furnace_value_cap").AsInt32();
                    caster.Set("blood_furnace_value", Mathf.Min(cap, caster.Get("blood_furnace_value").AsInt32() + count));
                    break;
                case "spend_blood_furnace":
                    if(skill.skill_name!="偃月")caster.Set("blood_furnace_value", Mathf.Max(0, caster.Get("blood_furnace_value").AsInt32() - count));
                    break;
                case "redsun_spend_furnace_for_block" when caster is Creature furnaceUser && skill.source_gongfa_name=="赤阳参同经":
                    var furnaceLimit=effect.GetValueOrDefault("max",20).AsInt32();
                    var furnaceSpend=Math.Min(Math.Max(0,furnaceLimit),Math.Max(0,furnaceUser.blood_furnace_value));
                    furnaceSpend-=furnaceSpend%2;
                    if(furnaceSpend>0)
                    {
                        furnaceUser.blood_furnace_value-=furnaceSpend;
                        AddStatus(caster,"化解",furnaceSpend/2,caster);
                    }
                    break;
                case "gain_punch_frame":
                    caster.Set("punch_frame", Mathf.Min(1, caster.Get("punch_frame").AsInt32() + count));
                    break;
                case "gain_energy":
                    var maximum = Mathf.Max(0, caster.Get("max_stamina").AsInt32());
                    caster.Set("current_stamina", Mathf.Min(maximum, caster.Get("current_stamina").AsInt32() + count));
                    break;
                case "draw":
                    GetManager()?.Call("draw_cards_to_hand", caster, count);
                    break;
                case "counter_block":
                    if(!skill.status_effects.Any(e=>e.GetValueOrDefault("status_id", "").AsString()=="化解"))
                        AddStatus(caster, "化解", CardRules.Block(skill,caster,count), caster);
                    break;
                case "evasion":
                    AddStatus(caster,"身法",Mathf.RoundToInt(effect.GetValueOrDefault("power",.05f).AsSingle()/.05f),caster);
                    break;
            }
        }
    }

    private static bool DealsDamage(ActiveSkillData skill) =>
        !Mathf.IsZeroApprox(skill.base_damage) || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula);

    private async System.Threading.Tasks.Task Lunge(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if (attacker is not Node2D node || target is not Node2D targetNode) return;
        var home = node.GlobalPosition;
        // Read the action, approach if needed, then start the common attack/contact flow.
        await PresentationWait(.14f);
        if(!GodotObject.IsInstanceValid(node)||!GodotObject.IsInstanceValid(targetNode))return;
        if(BattleAttackMotion.UsesApproach(skill))
        {
            _attackHomes[node]=(home,node.ZIndex);
            node.ZIndex=Math.Max(node.ZIndex,targetNode.ZIndex)+1;
            var contact=BattleAttackMotion.ContactPosition(node,targetNode,home);
            node.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.face_direction(targetNode.GlobalPosition-home);
            var tween=node.CreateTween().SetPauseMode(Tween.TweenPauseMode.Stop);
            tween.TweenProperty(node,"global_position",contact,BattleAttackMotion.ApproachSeconds)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            await ToSignal(tween,Tween.SignalName.Finished);
            if(!GodotObject.IsInstanceValid(node)||!GodotObject.IsInstanceValid(targetNode))return;
        }
        var direction=(targetNode.GlobalPosition-node.GlobalPosition).Normalized();
        node.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_attack(direction);
        var visual=BattleInkCombatVfx.PlayAttack(attacker,target,skill);
        if(visual is not null)_incomingAttackVisuals[node]=visual;
        await PresentationWait(BattleInkCombatVfx.ContactSeconds-BattleAttackMotion.BeforeContactSeconds);
    }

    private async System.Threading.Tasks.Task CompleteAttackContact(GodotObject attacker,GodotObject target,ActiveSkillData skill,bool isCounter)
    {
        if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target))return;
        BattleInkCombatVfx? visual=null;
        if(attacker is Node2D node)_incomingAttackVisuals.Remove(node,out visual);
        var mode=GodotObject.IsInstanceValid(visual)?visual!.ProcessMode:ProcessModeEnum.Pausable;
        try
        {
            // Keep the incoming stroke before contact throughout the response insert.
            if(GodotObject.IsInstanceValid(visual))visual!.ProcessMode=ProcessModeEnum.Disabled;
            if(!isCounter&&GetManager() is BattleManager manager)
                await manager.PresentAutoResponse(target,attacker,skill.damage_type);
        }
        finally
        {
            if(GodotObject.IsInstanceValid(visual))visual!.ProcessMode=mode;
        }
        await PresentationWait(BattleAttackMotion.BeforeContactSeconds);
    }
    private async System.Threading.Tasks.Task SettleAction(GodotObject attacker,ActiveSkillData skill)
    {
        if(!GodotObject.IsInstanceValid(attacker))
        {
            if(attacker is Node2D gone){_attackHomes.Remove(gone);_incomingAttackVisuals.Remove(gone);}
            return;
        }
        if(attacker is Node2D actor)_incomingAttackVisuals.Remove(actor);
        // 命中／护持效果先留住一个短拍，再退回站位；下一行动不会抢掉这一落点。
        var doll=(attacker as Node)?.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        if(DealsDamage(skill)&&GodotObject.IsInstanceValid(doll))doll!.hold_attack_contact(true);
        await PresentationWait(DealsDamage(skill)?BattleInkCombatVfx.ContactHoldSeconds(BattleInkCombatVfx.HitWeight(skill))+.055f:.14f);
        if(GodotObject.IsInstanceValid(doll))doll!.hold_attack_contact(false);
        if(attacker is Node2D node&&GodotObject.IsInstanceValid(node)&&_attackHomes.Remove(node,out var home))
        {
            var contact=node.GlobalPosition;
            if(contact.DistanceTo(home.Position)>1f&&GodotObject.IsInstanceValid(doll))doll!.play_retreat(contact-home.Position);
            var tween=node.CreateTween().SetPauseMode(Tween.TweenPauseMode.Stop);
            tween.TweenMethod(Callable.From<float>(progress=>
            {
                if(GodotObject.IsInstanceValid(node))node.GlobalPosition=BattleAttackMotion.ReturnPosition(contact,home.Position,progress);
            }),0f,1f,BattleAttackMotion.ReturnSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            await ToSignal(tween,Tween.SignalName.Finished);
            if(GodotObject.IsInstanceValid(node)){node.GlobalPosition=home.Position;node.ZIndex=home.ZIndex;}
        }
        else await PresentationWait(BattleAttackMotion.ReturnSeconds);
        await PresentationWait(.065f);
    }
    public void execute_response(GodotObject defender, GodotObject attacker, ActiveSkillData skill, bool showTriggerLabel = true)
    {
        show_action_name(defender, skill.skill_name);
        BattleInkCombatVfx.PlayUtility(defender, defender, skill);
        ApplyStatusEffects(skill, defender, defender);
        ApplySelfSpecialEffects(skill, defender, attacker);
        if (showTriggerLabel) FloatingText(defender, $"应对：{skill.skill_name}", new Color(.73f, .81f, .76f));
    }
    public void show_damage_number(GodotObject target, int amount, string damageType, bool critical)
    {
        var color = critical ? Critical : damageType is "soul" or "spiritual" ? Spiritual : damageType == "magic" ? Magic : Physical;
        FloatingText(target, critical ? $"{amount}！" : amount.ToString(), color);
    }
    public void show_action_name(GodotObject caster, string name)
    {
        if (caster is not Node2D owner || !GodotObject.IsInstanceValid(owner) || string.IsNullOrWhiteSpace(name)) return;
        if (_floatingText?.Instantiate() is not global::FloatingText view) return;
        (GetManager()?.GetParent() ?? GetTree().CurrentScene)?.AddChild(view);
        view.Scale = Vector2.One * BattleUiArt.Scale(GetViewport()) / Mathf.Max(.001f, GetViewport().GetCanvasTransform().Scale.Y);
        view.show_action_name(owner, name);
    }
    public void show_recovery_number(GodotObject target, int amount, string resource = "生命")
    {
        if (amount <= 0) return;
        FloatingText(target, resource == "生命" ? $"+{amount}" : $"{resource}+{amount}", Recovery, resource != "生命");
    }
    public void show_status_gain(GodotObject target, string name, int amount, bool debuff = false)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(name)) return;
        FloatingText(target, $"{name}+{amount}", debuff ? new Color(.90f,.57f,.43f) : new Color(.74f,.85f,.64f), true);
    }
    private static void AddStatus(GodotObject target, string id, int stacks, GodotObject source)
    {
        var status = GD.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
        if (status is not null && target.HasMethod("add_status")) target.Call("add_status", status, -1, Mathf.Max(1, stacks), source.GetInstanceId().ToString(), status.max_stacks, source.Get("entity_name").AsString());
    }
    private static void AddPowerStatus(GodotObject target,ActiveSkillData skill)
    {
        if(target is not Creature creature)return;
        var status=new StatusEffect{effect_id="心法:"+skill.skill_name,name=skill.skill_name,description=skill.description,max_stacks=1,duration_default=-1};
        creature.add_status(status,-1,1,"power:"+skill.skill_name,1,skill.source_gongfa_name);
    }
    private void Finish(GodotObject attacker, bool isCounter, bool killed)
    {
        var manager = GetManager(); if (manager is null) return;
        if (manager is BattleManager realtime && realtime.is_realtime_battle()) { realtime.on_queued_skill_resolved(attacker); return; }
        if (!isCounter && attacker.Get("faction").AsInt32() == 1 && manager is BattleManager { auto_battle:false })
            manager.Call("_on_player_skill_done", new Dictionary { ["target_killed"] = killed });
        else if (!isCounter && attacker.Get("faction").AsInt32() != 1)
            manager.Call("_on_enemy_skill_done", attacker, killed);
        else manager.Call("_finish_current_action", attacker);
    }
    private Node? GetManager() => GetTree()?.GetFirstNodeInGroup("battle_mgr");
    private static int GetStatusStacks(GodotObject unit, string id) => unit.HasMethod("get_status_stacks") ? unit.Call("get_status_stacks", id).AsInt32() : 0;
    private static void ConsumeStatus(GodotObject unit, string id, int stacks) { if (unit.HasMethod("consume_status_stacks")) unit.Call("consume_status_stacks", id, stacks); }
    private static Dictionary ProcessStatus(GodotObject unit, string eventName, Dictionary context) => unit.HasMethod("process_status_event") ? unit.Call("process_status_event", eventName, context).AsGodotDictionary() : context;
    private void ResolveCounter(GodotObject target, GodotObject attacker, ActiveSkillData skill, int damage, int blocked, bool isCounter) => GetManager()?.Call("resolve_pending_counter_followups", target, new Dictionary { ["attacker"] = attacker, ["target"] = target, ["skill"] = skill, ["damage"] = damage, ["blocked_damage"] = blocked, ["damage_type"] = skill.damage_type, ["is_counter"] = isCounter });
    private readonly System.Collections.Generic.Dictionary<ulong, (ulong Time, int Lane)> _floatingLanes = [];
    private void FloatingText(GodotObject unit,string text,Color color,bool compact = false)
    {
        if (!GodotObject.IsInstanceValid(unit)) return;
        if(_floatingText?.Instantiate() is not Node2D view)return;
        (GetManager()?.GetParent() ?? GetTree().CurrentScene)?.AddChild(view);
        var id = unit.GetInstanceId();
        var now = Time.GetTicksMsec();
        var previous = _floatingLanes.GetValueOrDefault(id);
        var lane = now - previous.Time < 1700 ? previous.Lane % 5 : 0;
        _floatingLanes[id] = (now, lane + 1);
        var scale = BattleUiArt.Scale(GetViewport()) / Mathf.Max(.001f, GetViewport().GetCanvasTransform().Scale.Y);
        var anchor = (unit as Node)?.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.combat_anchor() ?? GetPosition(unit) + new Vector2(0,-120);
        view.Scale = Vector2.One * scale;
        view.GlobalPosition = anchor + new Vector2((lane % 2 == 0 ? -1 : 1) * 18, -lane * 58) * scale;
        view.Call("show_text",text,color,compact ? 32 : 52);
    }
    private static Vector2 GetPosition(GodotObject unit) => unit is Node2D node ? node.GlobalPosition : Vector2.Zero;
}
