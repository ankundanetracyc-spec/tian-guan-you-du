using Godot;
using Godot.Collections;

/// <summary>C# implementation of the SkillExecutor execution chain for BattleManager.</summary>
public partial class SkillExecutor : Node
{
    private static readonly Color Physical = new(1f, .28f, .24f), PhysicalCrit = new(.76f, .34f, 1f),
        Spiritual = new(.30f, .68f, 1f), SpiritualCrit = new(.76f, .34f, 1f), Dot = Colors.White;
    private readonly PackedScene? _floatingText = GD.Load<PackedScene>("res://tscn/battle/FloatingText.tscn");

    public async void execute_skill(GodotObject attacker, ActiveSkillData skill, GodotObject target, bool is_counter = false)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { Finish(attacker, is_counter, false); return; }
        // 术诀和守御牌不走命中判定，避免无伤害牌被最低 1 点伤害规则误伤自身。
        if (!DealsDamage(skill))
        {
            if(GetManager() is BattleManager bodyManager && bodyManager.body_cards.Utility(attacker,skill)){Finish(attacker,is_counter,false);return;}
            ApplyStatusEffects(skill, attacker, target);
            ApplySelfSpecialEffects(skill, attacker);
            // 未配置独立状态资源的心法也必须以永久状态呈现，不能只在运行时静默生效。
            if (skill.skill_type == ActiveSkillData.SkillType.POWER && skill.status_effects.Count == 0) AddPowerStatus(attacker, skill);
            Finish(attacker, is_counter, false);
            return;
        }
        await Lunge(attacker, target, skill);
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { Finish(attacker, is_counter, false); return; }
        var killed=false;
        var manager=GetManager();
        var body=(manager as BattleManager)?.body_cards;
        // 即时战斗先结算已持招的应对，再检查游骸对整项物理技能的闪避。
        manager?.Call("try_auto_response", target, attacker, skill.damage_type);
        var segments=body?.Segments(attacker,skill)??1;
        var evaded=body?.EvadeWithYouhai(target,attacker,skill)??false;
        if(evaded)FloatingText(target,"游骸",Colors.Yellow);
        else for(var i=0;i<segments&&!killed;i++)killed=ApplyToTarget(attacker,skill,target,is_counter,false);
        var followup=body?.FurnaceFollowup(attacker,skill);
        body?.AfterAttack(attacker,skill);
        if(!is_counter&&skill.skill_type==ActiveSkillData.SkillType.ATTACK&&skill.damage_type=="physical")GetManager()?.Call("trigger_species_attack_talent",attacker);
        if(followup is not null&&!killed)killed=ApplyToTarget(attacker,followup,target,true);
        Finish(attacker, is_counter, killed);
    }

    public async void execute_aoe_skill(GodotObject attacker, ActiveSkillData skill, Vector2I target_cell, GodotObject battle_map, bool is_counter = false)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(battle_map)) { Finish(attacker, is_counter, false); return; }
        var killed = false;
        var cells = battle_map.Call("get_aoe_cells", target_cell, skill.aoe_range).AsGodotArray();
        var seen = new HashSet<ulong>();
        foreach (var value in cells)
        {
            var target = battle_map.Call("get_unit_at_cell", value).AsGodotObject();
            if (!GodotObject.IsInstanceValid(target) || target!.Get("current_health").AsInt32() <= 0 || target.Get("faction").AsInt32() == attacker.Get("faction").AsInt32() || !seen.Add(target.GetInstanceId())) continue;
            await Lunge(attacker, target, skill);
            if(!GodotObject.IsInstanceValid(target))continue;
            var manager=GetManager();
            var body=(manager as BattleManager)?.body_cards;
            if(!is_counter)manager?.Call("try_auto_response",target,attacker,skill.damage_type);
            if(body?.EvadeWithYouhai(target,attacker,skill)==true)
            {
                FloatingText(target,"游骸",Colors.Yellow);
                continue;
            }
            killed |= ApplyToTarget(attacker, skill, target, is_counter, false);
        }
        if(!is_counter&&skill.skill_type==ActiveSkillData.SkillType.ATTACK&&skill.damage_type=="physical")GetManager()?.Call("trigger_species_attack_talent",attacker);
        Finish(attacker, is_counter, killed);
    }

    private bool ApplyToTarget(GodotObject attacker, ActiveSkillData skill, GodotObject target, bool isCounter, bool allowResponse = true)
    {
        if (!isCounter && allowResponse) GetManager()?.Call("try_auto_response", target, attacker, skill.damage_type);
        var block = GetStatusStacks(target, "化解");
        var result = BattleActionResolver.resolve_hit(attacker, target, skill, block);
        if (block > 0) ConsumeStatus(target, "化解", block);
        if (result.Evaded)
        {
            BattleInkSlashEffect.PlayImpact(target, false, true);
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
            FloatingText(target, $"应对 {result.BlockedDamage}", new Color(1f, .82f, .30f));
        GetManager()?.Call("mark_unit_damaged_this_turn", target);
        ApplyStatusEffects(skill, attacker, target);
        ApplySelfSpecialEffects(skill, attacker);
        ApplyWounds(skill, target);
        BattleInkSlashEffect.PlayImpactForSkill(target, skill, result.Critical, attacker);
        var hitDirection = (GetPosition(target) - GetPosition(attacker)).Normalized();
        if (target is Node targetNode) targetNode.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(hitDirection);
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
            if(id=="化解")stacks=CardRules.Block(skill,caster,stacks);
            target.Call("add_status", status, -1, Mathf.Max(1, stacks), caster.GetInstanceId().ToString(), status.max_stacks, caster.Get("entity_name").AsString());
            FloatingText(target, id, Colors.Orange);
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
                case "apply_sword_mark_to_attacker" when GodotObject.IsInstanceValid(attacker):
                    AddStatus(attacker!, "剑痕", count, caster);
                    break;
            }
        }
    }

    private static bool DealsDamage(ActiveSkillData skill) =>
        !Mathf.IsZeroApprox(skill.base_damage) || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula);

    private async System.Threading.Tasks.Task Lunge(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if (attacker is not Node2D node || target is not Node2D targetNode) return;
        var home = node.GlobalPosition; var direction = (targetNode.GlobalPosition - home).Normalized(); var tween = node.CreateTween();
        node.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_attack(direction);
        BattleInkSlashEffect.PlayAttack(attacker, target, skill);
        tween.TweenProperty(node, "global_position", home + direction * 30, .06).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(node, "global_position", home, .12).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
    public void execute_response(GodotObject defender, GodotObject attacker, ActiveSkillData skill)
    {
        ApplyStatusEffects(skill, defender, defender);
        ApplySelfSpecialEffects(skill, defender, attacker);
        FloatingText(defender, $"应对：{skill.skill_name}", new Color(1f, .78f, .3f));
    }
    public void show_damage_number(GodotObject target, int amount, string damageType, bool critical)
    {
        var color = damageType == "dot" ? Dot : damageType is "soul" or "magic" or "spiritual" ? critical ? SpiritualCrit : Spiritual : critical ? PhysicalCrit : Physical;
        FloatingText(target, amount.ToString(), color);
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
    private int floatingLane;
    private void FloatingText(GodotObject unit,string text,Color color)
    {
        if(_floatingText?.Instantiate() is not Node2D view)return;
        GetTree().CurrentScene?.AddChild(view);
        var lane=floatingLane++%3;var side=unit.Get("faction").AsInt32()==1?-1:1;
        view.GlobalPosition=GetPosition(unit)+new Vector2(side*(112+lane*24),-280-lane*38);
        view.Call("show_text",text,color);
    }
    private static Vector2 GetPosition(GodotObject unit) => unit is Node2D node ? node.GlobalPosition : Vector2.Zero;
}
