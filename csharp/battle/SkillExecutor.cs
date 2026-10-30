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
    private readonly System.Collections.Generic.Dictionary<Node2D,BattleInkCombatVfx> _primaryAttackVisuals=[];
    private readonly System.Collections.Generic.Dictionary<Node2D,List<BattleInkCombatVfx>> _followUpVisuals=[];
    private bool IsSpectatorBattle => GetManager() is BattleManager manager && manager.is_spectator_battle();
    private async System.Threading.Tasks.Task PresentationWait(float seconds)
    {
        if (IsSpectatorBattle) return;
        if(IsInsideTree())await ToSignal(GetTree().CreateTimer(seconds,processAlways:false),SceneTreeTimer.SignalName.Timeout);
    }

    public async void execute_skill(GodotObject attacker, ActiveSkillData skill, GodotObject target, bool is_counter = false, bool is_queued_skill = false, bool is_slot10_automatic = false, bool finish_as_automatic = false)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { Finish(attacker, is_counter, false,finish_as_automatic); return; }
        if(skill.skill_name=="复命"&&GetManager() is BattleManager replayManager&&replayManager.is_realtime_battle())
        { replayManager.BeginFumingReplay(attacker,skill,finish_as_automatic); return; }
        if (target is ArtifactBody artifactBody)
        {
            var artifactManager = GetManager() as BattleManager;
            if (artifactManager is not null && BattleManager.ArtifactAcceptsSkill(artifactBody, attacker, skill))
            {
                artifactManager.gongfa_combat.PrepareAttack(attacker, skill);
                artifactManager.gongfa_combat.PrepareRedSunAttack(attacker, skill);
                var segmentsOnBody = artifactManager.gongfa_combat.Segments(attacker, skill);
                for (var part = 0; part < segmentsOnBody && artifactBody.current_health > 0; part++)
                    artifactManager.damage_artifact_body(attacker, artifactManager.gongfa_combat.ForDamageSegment(attacker,skill,part), artifactBody);
                artifactManager.gongfa_combat.AfterAttack(attacker, skill, false);
            }
            await SettleAction(attacker, skill);
            Finish(attacker, is_counter, false,finish_as_automatic);
            return;
        }
        show_action_name(attacker, skill.skill_name);
        // 术诀和守御牌不走命中判定，避免无伤害牌被最低 1 点伤害规则误伤自身。
        if (!DealsDamage(skill))
        {
            await PresentationWait(.12f);
            if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target)){Finish(attacker,is_counter,false,finish_as_automatic);return;}
            if (!IsSpectatorBattle) BattleInkCombatVfx.PlayUtility(attacker, target, skill);
            if(GetManager() is BattleManager bodyManager && bodyManager.gongfa_combat.Utility(attacker,skill)){await SettleAction(attacker,skill);Finish(attacker,is_counter,false,finish_as_automatic);return;}
            ApplyStatusEffects(skill, attacker, target);
            ApplySelfSpecialEffects(skill, attacker,target,is_slot10_automatic);
            // 未配置独立状态资源的心法也必须以永久状态呈现，不能只在运行时静默生效。
            if (skill.skill_type == ActiveSkillData.SkillType.POWER && skill.status_effects.Count == 0) AddPowerStatus(attacker, skill);
            await SettleAction(attacker,skill);
            Finish(attacker, is_counter, false,finish_as_automatic);
            return;
        }
        (GetManager() as BattleManager)?.gongfa_combat.PrepareAttack(attacker, skill);
        (GetManager() as BattleManager)?.gongfa_combat.PrepareRedSunAttack(attacker, skill);
        await Lunge(attacker, target, skill);
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { await SettleAction(attacker,skill);Finish(attacker, is_counter, false,finish_as_automatic); return; }
        var killed=false;
        var hitAtLeastOnce=false;
        var firstHit=false;
        var manager=GetManager();
        var body=(manager as BattleManager)?.gongfa_combat;
        var segments=body?.Segments(attacker,skill)??1;
        // 闪避优先于队首应对：先结算整招规避，再预判首段普通闪避，避免成功闪避仍花费应对。
        var evaded=body?.EvadeWithYouhai(target,attacker,skill)??false;
        var firstSegmentEvaded=!evaded&&CanBeEvaded(skill)&&BattleActionResolver.roll_evasion(attacker,target);
        var targetResponded = await CompleteAttackContact(attacker, target, skill, is_counter, allowResponse: !evaded&&!firstSegmentEvaded);
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(target)) { await SettleAction(attacker,skill);Finish(attacker, is_counter, false,finish_as_automatic); return; }
        var blockRemaining=skill.damage_type=="physical"?GetStatusStacks(target,"化解"):0;
        var bloodArmResponseChecked=false;
        var bloodArmResponseTriggered=false;
        if(evaded)
        {
            FloatingText(target,"游骸",Colors.Yellow);
            if (!IsSpectatorBattle) BattleInkCombatVfx.PlayImpact(target, skill, attacker, evaded: true);
        }
        else
        {
            var initialHealth = target.Get("current_health").AsInt32();
            var initialSegmentCausedNoHealthDamage = false;
            for(var i=0;i<segments&&!killed;i++)
            {
                bool? evadedOverride=i==0?firstSegmentEvaded:CanBeEvaded(skill)&&BattleActionResolver.roll_evasion(attacker,target);
                if(evadedOverride!=true&&!bloodArmResponseChecked)
                {
                    bloodArmResponseChecked=true;
                    var extraDefuse=body?.RollBloodArmAutoResponse(target,attacker,skill)??0;
                    if(extraDefuse>0)
                    {
                        AddStatus(target,"化解",extraDefuse,target);
                        blockRemaining+=extraDefuse;
                        if(target.HasMethod("update_ui"))target.Call("update_ui");
                        FloatingText(target,$"化解 +{extraDefuse}",new Color(.73f,.81f,.76f));
                        (manager as BattleManager)?.PlayBloodArmAutoGuard(target,attacker);
                        bloodArmResponseTriggered=true;
                    }
                }
                var bloodFollowUp=body?.IsBloodArmVisualSegment(attacker,skill,i)==true;
                if(i>0)await PresentFollowUpContact(attacker,target,skill,bloodFollowUp);
                if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target))break;
                killed=ApplyToTarget(attacker,skill,target,is_counter,ref blockRemaining,out var hit,i,is_queued_skill,is_slot10_automatic,evadedOverride);
                hitAtLeastOnce|=hit;
                if(i==0)firstHit=hit;
                if(bloodFollowUp)await WaitForBloodArmAction(attacker);
                if(i==0)initialSegmentCausedNoHealthDamage=target.Get("current_health").AsInt32()>=initialHealth;
            }
            var taiheFollowUps=body?.TaiheFollowUpCount(attacker,skill,targetResponded,initialSegmentCausedNoHealthDamage)??0;
            for(var i=0;i<taiheFollowUps&&!killed;i++)
            {
                var followUp=body?.CreateTaiheFollowUpSegment(skill)??skill;
                await PresentFollowUpContact(attacker,target,followUp,false);
                if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target))break;
                var followUpEvaded=CanBeEvaded(skill)&&BattleActionResolver.roll_evasion(attacker,target);
                killed=ApplyToTarget(attacker,skill,target,is_counter,ref blockRemaining,out var hit,segments+i,is_queued_skill,is_slot10_automatic,followUpEvaded,followUp);
                hitAtLeastOnce|=hit;
                if(hit&&target.Get("current_health").AsInt32()>0&&GetManager() is BattleManager taiheManager
                    &&taiheManager.is_realtime_battle()&&body is not null&&body.RollTaiheStun(attacker,target,skill,out var stunFrames))
                    AddStatus(target,"僵直",stunFrames,attacker,stunFrames/StatusEffect.CombatFramesPerSecond);
            }
            var cloudLimit=body?.CloudFollowUpLimit(attacker,skill,firstHit)??0;
            var cloudSegment=cloudLimit>0?body!.CreateCloudFollowUp(attacker,skill):null;
            for(var i=0;i<cloudLimit&&!killed;i++)
            {
                var chance=body!.CloudFollowUpChance(attacker,skill,i);
                if(chance<=0 || chance<1 && GD.Randf()>=chance)break;
                await PresentFollowUpContact(attacker,target,cloudSegment!,false);
                if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target))break;
                var missed=CanBeEvaded(skill)&&BattleActionResolver.roll_evasion(attacker,target);
                killed=ApplyToTarget(attacker,skill,target,is_counter,ref blockRemaining,out var hit,segments+taiheFollowUps+i,is_queued_skill,is_slot10_automatic,missed,cloudSegment);
                hitAtLeastOnce|=hit;
                if(!hit)break;
            }
            if(hitAtLeastOnce&&!is_queued_skill)ApplyDisperseQiBlood(skill,target);
            if(hitAtLeastOnce&&blockRemaining>0)ConsumeStatus(target,"化解",blockRemaining);
        }
        body?.EndIncomingAttack(target);
        if(bloodArmResponseTriggered)body?.CompleteBloodArmAutoResponse(target);
        body?.AfterAttack(attacker,skill,hitAtLeastOnce);
        if(!is_counter&&skill.skill_type==ActiveSkillData.SkillType.ATTACK&&skill.damage_type=="physical")GetManager()?.Call("trigger_species_attack_talent",attacker);
        await SettleAction(attacker,skill);
        Finish(attacker, is_counter, killed,finish_as_automatic,skill);
    }

    public async void execute_aoe_skill(GodotObject attacker, ActiveSkillData skill, Vector2I target_cell, GodotObject battle_map, bool is_counter = false, bool is_queued_skill = false, bool is_slot10_automatic = false, bool finish_as_automatic = false)
    {
        if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(battle_map)) { Finish(attacker, is_counter, false,finish_as_automatic); return; }
        show_action_name(attacker, skill.skill_name);
        var killed = false;
        var seen = new HashSet<ulong>();
        var manager = GetManager() as BattleManager;
        var body=manager?.gongfa_combat;
        var attackPrepared=false;
        var dispersed=false;
        var targets = manager?.get_target_candidates().Where(target => target.Get("faction").AsInt32() != attacker.Get("faction").AsInt32()).ToArray() ?? [];
        foreach (var target in targets)
        {
            if (!GodotObject.IsInstanceValid(target) || target.Get("current_health").AsInt32() <= 0 || !seen.Add(target.GetInstanceId())) continue;
            if (target is ArtifactBody artifactBody)
            {
                manager?.damage_artifact_body(attacker, skill, artifactBody);
                continue;
            }
            if(!attackPrepared){body?.PrepareAttack(attacker,skill);attackPrepared=true;}
            await Lunge(attacker, target, skill);
            if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target)){await SettleAction(attacker,skill);continue;}
            var evadedByYouhai=body?.EvadeWithYouhai(target,attacker,skill)==true;
            var evadedNormally=!evadedByYouhai&&CanBeEvaded(skill)&&BattleActionResolver.roll_evasion(attacker,target);
            await CompleteAttackContact(attacker,target,skill,is_counter,allowResponse:!evadedByYouhai&&!evadedNormally);
            if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target)){await SettleAction(attacker,skill);continue;}
            var blockRemaining=skill.damage_type=="physical"?GetStatusStacks(target!,"化解"):0;
            var bloodArmResponseChecked=false;
            var bloodArmResponseTriggered=false;
            if(evadedByYouhai||evadedNormally)
            {
                FloatingText(target,evadedByYouhai?"游骸":"闪避！",Colors.Yellow);
                if (!IsSpectatorBattle) BattleInkCombatVfx.PlayImpact(target, skill, attacker, evaded: true);
                body?.EndIncomingAttack(target!);
                await SettleAction(attacker,skill);
                continue;
            }
            var segments=body?.Segments(attacker,skill)??1;
            var targetHit=false;
            for(var segment=0;segment<segments&&!killed;segment++)
            {
                bool? evadedOverride=segment==0?false:CanBeEvaded(skill)&&BattleActionResolver.roll_evasion(attacker,target);
                if(evadedOverride!=true&&!bloodArmResponseChecked)
                {
                    bloodArmResponseChecked=true;
                    var extraDefuse=body?.RollBloodArmAutoResponse(target,attacker,skill)??0;
                    if(extraDefuse>0)
                    {
                        AddStatus(target,"化解",extraDefuse,target);
                        blockRemaining+=extraDefuse;
                        if(target.HasMethod("update_ui"))target.Call("update_ui");
                        FloatingText(target,$"化解 +{extraDefuse}",new Color(.73f,.81f,.76f));
                        manager?.PlayBloodArmAutoGuard(target,attacker);
                        bloodArmResponseTriggered=true;
                    }
                }
                var bloodFollowUp=body?.IsBloodArmVisualSegment(attacker,skill,segment)==true;
                if(segment>0)await PresentFollowUpContact(attacker,target,skill,bloodFollowUp);
                if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target))break;
                killed |= ApplyToTarget(attacker,skill,target,is_counter,ref blockRemaining,out var hit,segment,is_queued_skill,is_slot10_automatic,evadedOverride);
                targetHit|=hit;
                if(bloodFollowUp)await WaitForBloodArmAction(attacker);
            }
            if(targetHit&&!dispersed&&!is_queued_skill){ApplyDisperseQiBlood(skill,target);dispersed=true;}
            if(targetHit&&blockRemaining>0)ConsumeStatus(target!,"化解",blockRemaining);
            body?.EndIncomingAttack(target!);
            if(bloodArmResponseTriggered)body?.CompleteBloodArmAutoResponse(target!);
            await SettleAction(attacker,skill);
        }
        if(!is_counter&&skill.skill_type==ActiveSkillData.SkillType.ATTACK&&skill.damage_type=="physical")GetManager()?.Call("trigger_species_attack_talent",attacker);
        Finish(attacker, is_counter, killed,finish_as_automatic);
    }

    private bool ApplyToTarget(GodotObject attacker, ActiveSkillData skill, GodotObject target, bool isCounter, ref int blockRemaining, out bool didHit, int segment = 0, bool isQueuedSkill = true, bool isSlot10Automatic = false, bool? evadedOverride = null, ActiveSkillData? segmentSkillOverride = null)
    {
        didHit=false;
        var body=(GetManager() as BattleManager)?.gongfa_combat;
        var segmentSkill=segmentSkillOverride??body?.ForDamageSegment(attacker,skill,segment)??skill;
        var result = BattleActionResolver.resolve_hit(attacker, target, segmentSkill, blockRemaining, evaded_override: evadedOverride);
        blockRemaining=Mathf.Max(0,blockRemaining-result.BlockedDamage);
        if (result.BlockedDamage > 0) ConsumeStatus(target, "化解", result.BlockedDamage);
        if (result.Evaded)
        {
            if (!IsSpectatorBattle) BattleInkCombatVfx.PlayImpact(target, skill, attacker, evaded: true);
            FloatingText(target, "闪避！", Colors.Yellow);
            ResolveCounter(target, attacker, skill, 0, 0, isCounter);
            return false;
        }
        didHit=true;

        var context = new Dictionary { ["battle_manager"] = GetManager()!, ["attacker"] = attacker, ["target"] = target, ["skill"] = skill, ["damage"] = result.DamageBeforeShield, ["damage_type"] = skill.damage_type, ["is_counter"] = isCounter };
        context = ProcessStatus(attacker, "before_damage", context);
        context = ProcessStatus(target, "before_damage", context);
        var damage = Mathf.Max(context["damage"].AsInt32(), 0);
        var shield = target.Get("shield").AsInt32();
        var ignoreShield=GongfaSkillRewriteRuntime.HasEffect(skill,"xuan_shu_c1_jingzhe");
        var absorbed = ignoreShield?0:Mathf.Min(shield, damage);
        target.Set("shield", shield - absorbed);
        body?.RecordShieldAbsorbed(target,attacker,shield,absorbed);
        // 仅显示本次攻击实际吸收的护盾，清场或其他数值重置不产生扣盾提示。
        if (absorbed > 0) FloatingText(target, $"护盾−{absorbed}", new Color(.49f,.77f,.91f), true);
        damage -= absorbed;
        target.Set("current_health", Mathf.Max(0, target.Get("current_health").AsInt32() - damage));
        body?.OnHealthDamageReceived(target, damage);
        if (!IsSpectatorBattle && absorbed > 0 && target is Node shieldNode)
            shieldNode.GetNodeOrNull<BattleShieldAura>("BattleShieldAura")?.PlayImpact((GetPosition(target) - GetPosition(attacker)).Normalized());
        if (!IsSpectatorBattle && target.HasMethod("update_ui")) target.Call("update_ui");
        if (damage > 0 && target.HasMethod("gain_blood_furnace_from_hit")) target.Call("gain_blood_furnace_from_hit");
        context["damage"] = damage; context["blocked_damage"] = result.BlockedDamage;
        context = ProcessStatus(target, "after_damage", context);
        ProcessStatus(attacker, "after_damage", context);
        ForgedSwordBattleAdapter.AfterHit(attacker, target, segmentSkill, damage);
        ResolveCounter(target, attacker, skill, damage, result.BlockedDamage, isCounter);
        if (result.BlockedDamage > 0)
            FloatingText(target, $"化解 {result.BlockedDamage}", new Color(.73f, .81f, .76f));
        GetManager()?.Call("mark_unit_damaged_this_turn", target);
        if(!segmentSkill.combat_is_gongfa_bonus_damage_segment)
        {
            ApplyStatusEffects(skill, attacker, target);
            ApplySelfSpecialEffects(skill, attacker,target,isSlot10Automatic);
            ApplyWounds(skill, target);
        }
        if (!IsSpectatorBattle && damage > 0) BattleInkSlashEffect.PlayImpactForSkill(target, skill, result.Critical, attacker, segment);
        if (!IsSpectatorBattle && damage <= 0 && (absorbed > 0 || result.BlockedDamage > 0)) BattleInkCombatVfx.PlayDefenseContact(target, attacker);
        var hitDirection = (GetPosition(target) - GetPosition(attacker)).Normalized();
        if (!IsSpectatorBattle && damage > 0 && target is Node targetNode) targetNode.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.play_hit(hitDirection, BattleInkCombatVfx.HitWeight(skill), result.Critical,
            !BattleInkCombatVfx.IsJingzhe(skill) && !BattleInkCombatVfx.UsesForestWolfEffects(attacker, skill)&&!BattleInkCombatVfx.UsesSwordEffects(attacker,skill)&&!BattleInkCombatVfx.UsesForestBeiCry(attacker,skill));
        if (!IsSpectatorBattle && damage > 0 && !(target is Node visualTarget && visualTarget.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll")?.is_defense_presenting() == true) && target.HasMethod("hit_flash")) target.Call("hit_flash", hitDirection);
        show_damage_number(target, damage, skill.damage_type, result.Critical);
        if (target.Get("current_health").AsInt32() > 0) return false;
        if (!IsSpectatorBattle && target is Node deathNode && !deathNode.HasMeta("battle_death_ink_played"))
        {
            deathNode.SetMeta("battle_death_ink_played", true);
            BattleInkSlashEffect.PlayDeath(target);
        }
        if (!IsSpectatorBattle && target.HasMethod("play_death_animation")) target.Call("play_death_animation");
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
        => ApplySelfSpecialEffects(skill, caster, null, false);
    private void ApplySelfSpecialEffects(ActiveSkillData skill, GodotObject caster, GodotObject? attacker)
        => ApplySelfSpecialEffects(skill, caster, attacker, false);
    private void ApplySelfSpecialEffects(ActiveSkillData skill, GodotObject caster, GodotObject? attacker,bool isSlot10Automatic)
    {
        var qiBloodBefore=CardRules.Number(caster,"qi_blood");
        foreach (var effect in skill.special_effects)
        {
            if (!effect.TryGetValue("effect", out var value)) continue;
            var count = Mathf.Max(1, effect.GetValueOrDefault("count", 1).AsInt32());
            switch (value.AsString())
            {
                case "forest_bei_pack_call":
                    if (GetManager() is BattleManager packManager) packManager.grant_pack_hunt_to_wolves(caster);
                    break;
                case "shield":
                    var shieldGain = Math.Max(0, Mathf.RoundToInt(CardRules.Evaluate(effect.GetValueOrDefault("formula", "0").AsString(), caster)));
                    caster.Set("shield", caster.Get("shield").AsInt32() + shieldGain);
                    // Creature.shield 的实际增量入口统一报告飘字，此处不重复显示。
                    if(shieldGain>0&&GetManager() is BattleManager shieldManager)
                    {
                        if(GongfaSkillRewriteRuntime.HasEffect(skill,"xuan_shu_c4_fuming"))
                        {
                            var shared=shieldGain;
                            shieldManager.gongfa_combat.GrantLowestShieldAlly(caster,shared);
                        }
                        if(isSlot10Automatic&&GongfaSkillRewriteRuntime.HasEffect(skill,"xuan_shu_c2_yunpei"))
                        {
                            var shared=Mathf.RoundToInt(shieldGain*.5f);
                            shieldManager.gongfa_combat.GrantLowestShieldAlly(caster,shared);
                        }
                    }
                    break;
                case "gain_qi_blood" when caster.HasMethod("gain_qi_blood"):
                    caster.Call("gain_qi_blood", count);
                    break;
                case "gain_qi" when GetManager() is BattleManager qiManager:
                    if (qiManager.gain_battle_qi(caster, count, out var completedZhouTian))
                        FloatingText(caster, completedZhouTian ? "周天" : $"炁 +{count}", new Color(.58f, .82f, .80f));
                    break;
                case "gain_sword_intent":
                    var swordIntentBefore = GetStatusStacks(caster, "剑意");
                    AddStatus(caster, "剑意", count, caster);
                    var gainedSwordIntent = GetStatusStacks(caster, "剑意") - swordIntentBefore;
                    (GetManager() as BattleManager)?.gongfa_combat.AfterSwordIntentGain(caster,skill,swordIntentBefore);
                    if (gainedSwordIntent > 0)
                    {
                        if (caster.HasMethod("update_ui")) caster.Call("update_ui");
                        FloatingText(caster, $"剑意 +{gainedSwordIntent}", new Color(.78f, .77f, .65f));
                    }
                    break;
                case "consume_sword_intent":
                    var swordIntent = GetStatusStacks(caster, "剑意");
                    if (swordIntent > 0)
                    {
                        ConsumeStatus(caster, "剑意", swordIntent);
                        if (caster.HasMethod("update_ui")) caster.Call("update_ui");
                        FloatingText(caster, $"剑意 −{swordIntent}", new Color(.78f, .77f, .65f));
                    }
                    break;
                case "burn_mana_capacity" when GetManager() is BattleManager manaManager:
                    if(manaManager.spend_mana_capacity(caster,count))FloatingText(caster,$"法力上限−{count}",new Color(.58f,.72f,.82f),true);
                    break;
                case "assign_nian_to_next_skill" when GetManager() is BattleManager nianManager:
                    var queuedOwner = nianManager.get_battle_unit(caster);
                    var currentAction = queuedOwner.current_segment_action;
                    var assignedSkill = currentAction is null ? "" : nianManager.assign_nian_to_next_queued_skill(caster, currentAction, count);
                    if (!string.IsNullOrWhiteSpace(assignedSkill)) FloatingText(caster, $"寄念 · {assignedSkill}", new Color(.76f, .78f, .66f));
                    break;
                case "spend_qi_blood" when caster.HasMethod("spend_qi_blood"):
                    caster.Call("spend_qi_blood", count);
                    break;
                case "restore_health_percent":
                    var heal = Mathf.RoundToInt(caster.Get("max_health").AsInt32() * effect.GetValueOrDefault("percent", .03f).AsSingle());
                    if (caster.HasMethod("apply_healing")) caster.Call("apply_healing", Mathf.Max(1, heal));
                    else caster.Set("current_health", Mathf.Min(caster.Get("max_health").AsInt32(), caster.Get("current_health").AsInt32() + Mathf.Max(1, heal)));
                    if (!IsSpectatorBattle && caster.HasMethod("update_ui")) caster.Call("update_ui");
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
        if(skill.skill_name=="催血"&&GongfaSkillRewriteRuntime.HasEffect(skill,"baihai_c1_cuixue")&&qiBloodBefore<=3&&caster.HasMethod("gain_qi_blood"))
            caster.Call("gain_qi_blood",1);
    }

    private static bool DealsDamage(ActiveSkillData skill) =>
        !Mathf.IsZeroApprox(skill.base_damage) || skill.damage_bonuses.Count > 0 || !string.IsNullOrWhiteSpace(skill.damage_formula);

    private async System.Threading.Tasks.Task Lunge(GodotObject attacker, GodotObject target, ActiveSkillData skill)
    {
        if (IsSpectatorBattle || attacker is not Node2D node || target is not Node2D targetNode) return;
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
        BattleInkCombatVfx.PresentAttackPose(node, direction, skill);
        var visual=BattleInkCombatVfx.PlayAttack(attacker,target,skill);
        if(visual is not null){_incomingAttackVisuals[node]=visual;_primaryAttackVisuals[node]=visual;}
        await PresentationWait(BattleInkCombatVfx.AttackContactSeconds(attacker)-BattleAttackMotion.BeforeContactSeconds);
    }

    private async System.Threading.Tasks.Task<bool> CompleteAttackContact(GodotObject attacker,GodotObject target,ActiveSkillData skill,bool isCounter,bool allowResponse=true)
    {
        if(!GodotObject.IsInstanceValid(attacker)||!GodotObject.IsInstanceValid(target))return false;
        if (GetManager() is BattleManager spectator && spectator.is_spectator_battle())
        {
            return !isCounter&&allowResponse&&await spectator.PresentAutoResponse(target, attacker, skill.damage_type,skill);
        }
        BattleInkCombatVfx? visual=null;
        if(attacker is Node2D node)_incomingAttackVisuals.Remove(node,out visual);
        var mode=GodotObject.IsInstanceValid(visual)?visual!.ProcessMode:ProcessModeEnum.Pausable;
        var responseTriggered=false;
        try
        {
            // Keep the incoming stroke before contact throughout the response insert.
            if(GodotObject.IsInstanceValid(visual))visual!.ProcessMode=ProcessModeEnum.Disabled;
            if(!isCounter&&allowResponse&&GetManager() is BattleManager manager)
                responseTriggered=await manager.PresentAutoResponse(target,attacker,skill.damage_type,skill);
        }
        finally
        {
            if(GodotObject.IsInstanceValid(visual))visual!.ProcessMode=mode;
        }
        await PresentationWait(BattleAttackMotion.BeforeContactSeconds);
        return responseTriggered;
    }
    private static bool CanBeEvaded(ActiveSkillData skill)=>skill.damage_type is not ("soul" or "spiritual");
    private async System.Threading.Tasks.Task WaitForBloodArmAction(GodotObject attacker)
    {
        if(IsSpectatorBattle||attacker is not Node node)return;
        var arms=node.GetNodeOrNull<BattleBloodQiArms>("BloodQiArms");
        while(IsInsideTree()&&GodotObject.IsInstanceValid(attacker)&&GodotObject.IsInstanceValid(arms)&&arms!.IsActionPlaying)
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    private async System.Threading.Tasks.Task PresentFollowUpContact(GodotObject attacker,GodotObject target,ActiveSkillData skill,bool bloodArm)
    {
        if(IsSpectatorBattle||attacker is not Node2D actor||target is not Node2D victim)return;
        await WaitForBloodArmAction(attacker);
        await WaitForFollowUpVisuals(attacker);
        if(!GodotObject.IsInstanceValid(actor)||!GodotObject.IsInstanceValid(victim))return;
        if(bloodArm)
        {
            // 百骸追击由显化血手独立执行：先让本人的原攻击完整收势，随后保持待机。
            await FinishCasterStrikeForBloodArms(actor);
            if(!GodotObject.IsInstanceValid(actor)||!GodotObject.IsInstanceValid(victim))return;
            (GetManager() as BattleManager)?.PlayBloodArmFollowUp(attacker,target);
            await PresentationWait(BattleBloodQiArms.FollowUpContactSeconds);
        }
        else
        {
            // 追加招式沿用当前接触位置，不重新记录站位，整招结束后才统一退回。
            BattleInkCombatVfx.PresentAttackPose(actor,(victim.GlobalPosition-actor.GlobalPosition).Normalized(),skill);
            var visual=BattleInkCombatVfx.PlayAttack(attacker,target,skill);
            if(visual is not null)
            {
                if(!_followUpVisuals.TryGetValue(actor,out var pending))_followUpVisuals[actor]=pending=[];
                pending.Add(visual);
            }
            await PresentationWait(BattleInkCombatVfx.AttackContactSeconds(attacker));
        }
    }
    private async System.Threading.Tasks.Task FinishCasterStrikeForBloodArms(Node2D actor)
    {
        var doll=actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
        doll?.hold_attack_contact(false);
        if(_primaryAttackVisuals.Remove(actor,out var primary))
            while(IsInsideTree()&&GodotObject.IsInstanceValid(actor)&&GodotObject.IsInstanceValid(primary)
                &&primary.IsInsideTree()&&!primary.IsQueuedForDeletion())
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        while(IsInsideTree()&&GodotObject.IsInstanceValid(actor)&&GodotObject.IsInstanceValid(doll)
            &&doll!.is_action_animation_playing())
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(GodotObject.IsInstanceValid(doll))doll!.return_to_blood_arm_idle();
    }
    private async System.Threading.Tasks.Task WaitForFollowUpVisuals(GodotObject attacker)
    {
        if(IsSpectatorBattle||attacker is not Node2D actor||!_followUpVisuals.TryGetValue(actor,out var pending))return;
        while(IsInsideTree()&&GodotObject.IsInstanceValid(attacker))
        {
            pending.RemoveAll(visual=>!GodotObject.IsInstanceValid(visual)||!visual.IsInsideTree()||visual.IsQueuedForDeletion());
            if(pending.Count==0)break;
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        _followUpVisuals.Remove(actor);
    }
    private async System.Threading.Tasks.Task SettleAction(GodotObject attacker,ActiveSkillData skill)
    {
        if (IsSpectatorBattle)
        {
            if (attacker is Node2D spectatorActor) { _attackHomes.Remove(spectatorActor); _incomingAttackVisuals.Remove(spectatorActor); _primaryAttackVisuals.Remove(spectatorActor); }
            return;
        }
        if(!GodotObject.IsInstanceValid(attacker))
        {
            if(attacker is Node2D gone){_attackHomes.Remove(gone);_incomingAttackVisuals.Remove(gone);_primaryAttackVisuals.Remove(gone);}
            return;
        }
        if(attacker is Node2D actor){_incomingAttackVisuals.Remove(actor);_primaryAttackVisuals.Remove(actor);}
        // 追加队列和最后一拳的收势都完成后，才允许开始人物回位。
        await WaitForBloodArmAction(attacker);
        await WaitForFollowUpVisuals(attacker);
        if(!GodotObject.IsInstanceValid(attacker))return;
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
                if(GodotObject.IsInstanceValid(node))node.GlobalPosition=BattleAttackMotion.ReturnPosition(node,contact,home.Position,progress);
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
        if (!IsSpectatorBattle) BattleInkCombatVfx.PlayUtility(defender, defender, skill);
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
        if (IsSpectatorBattle || caster is not Node2D owner || !GodotObject.IsInstanceValid(owner) || string.IsNullOrWhiteSpace(name)) return;
        if (_floatingText?.Instantiate() is not global::FloatingText view) return;
        (GetManager()?.GetParent() ?? GetTree().CurrentScene)?.AddChild(view);
        view.Scale = Vector2.One * BattleUiArt.Scale(GetViewport()) / Mathf.Max(.001f, GetViewport().GetCanvasTransform().Scale.Y);
        view.show_action_name(owner, name);
    }
    public void present_recovery(GodotObject caster)
    {
        if (!IsSpectatorBattle) BattleInkCombatVfx.PlayRecovery(caster);
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
    private static void AddStatus(GodotObject target, string id, int stacks, GodotObject source,float combatDurationSeconds=-1f)
    {
        var status = GD.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres");
        if(status is null||!target.HasMethod("add_status"))return;
        var sourceId=source.GetInstanceId().ToString();var sourceName=source.Get("entity_name").AsString();var count=Mathf.Max(1,stacks);
        if(combatDurationSeconds>0&&status.duration_type==StatusEffect.DurationType.CombatTime&&target.HasMethod("add_status_combat_time"))
            target.Call("add_status_combat_time",status,combatDurationSeconds,count,sourceId,status.max_stacks,sourceName);
        else target.Call("add_status",status,-1,count,sourceId,status.max_stacks,sourceName);
    }
    private static void AddPowerStatus(GodotObject target,ActiveSkillData skill)
    {
        if(target is not Creature creature)return;
        var status=new StatusEffect{effect_id="心法:"+skill.skill_name,name=skill.skill_name,description=CardPresentation.Face(skill,target),max_stacks=1,duration_default=-1};
        creature.add_status(status,-1,1,"power:"+skill.skill_name,1,skill.source_gongfa_name);
    }
    private void ApplyDisperseQiBlood(ActiveSkillData skill,GodotObject target)
    {
        if(target is not Creature creature)return;
        foreach(var effect in skill.special_effects)
        {
            if(effect.GetValueOrDefault("effect","").AsString()!="disperse_qi_blood")continue;
            var before=creature.qi_blood;var scattered=Math.Min(before,Math.Max(1,effect.GetValueOrDefault("count",1).AsInt32()));
            if(scattered<=0)continue;
            creature.qi_blood=before-scattered;
            creature.update_ui();
            FloatingText(target,$"气血−{scattered}",new Color(.80f,.48f,.42f));
        }
    }

    private void Finish(GodotObject attacker, bool isCounter, bool killed,bool finishAsAutomatic=false,ActiveSkillData? resolvedSkill=null)
    {
        var manager = GetManager(); if (manager is null) return;
        if(!isCounter&&GodotObject.IsInstanceValid(attacker)&&manager is BattleManager resolvedManager)
            resolvedManager.gongfa_combat.AfterSkillResolved(attacker,resolvedSkill);
        if (manager is BattleManager realtime && realtime.is_realtime_battle()) { if(finishAsAutomatic)realtime.on_automatic_skill_resolved(attacker);else realtime.on_queued_skill_resolved(attacker); return; }
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
        if (IsSpectatorBattle || !GodotObject.IsInstanceValid(unit)) return;
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
