using Godot;

public partial class BattleManager
{
    private readonly Dictionary<(GodotObject,ActiveSkillData),float> _aiRawDamage = [];
    private float AiRawDamage(GodotObject unit,ActiveSkillData skill)
    {
        var key=(unit,skill);
        if(!_aiRawDamage.TryGetValue(key,out var raw))_aiRawDamage[key]=raw=BattleActionResolver.preview_damage(unit,skill);
        return raw;
    }
    // 有限束搜索比较整段草案。预测全为独立值，不调用支付、加状态、随机或技能执行。
    private sealed class AiDraft
    {
        public int Stamina, Mana, Capacity, Qi, Blood, Shield;
        public bool ConversionReady, CapQiUsed;
        public float Value;
        public ActiveSkillData? Last;
        public List<BattleQueuedSkill> Actions = [];
        public Dictionary<GodotObject, float> Health = [];
        public Dictionary<GodotObject, int> Shields = [];
        public Dictionary<ActiveSkillData, int> Uses = [];
        public HashSet<ActiveSkillData> Converted = [];
        public HashSet<string> Summoned = [];
        public HashSet<string> Statuses = [];
        public AiDraft Copy() => new() { Stamina=Stamina,Mana=Mana,Capacity=Capacity,Qi=Qi,Blood=Blood,Shield=Shield,
            ConversionReady=ConversionReady,CapQiUsed=CapQiUsed,Value=Value,Last=Last,
            Actions=new(Actions),Health=new(Health),Shields=new(Shields),Uses=new(Uses),Converted=new(Converted),Summoned=new(Summoned),Statuses=new(Statuses) };
    }

    private void BuildConstructedAiQueue(GodotObject unit, Dictionary<GodotObject,float> forecast, bool teammate)
    {
        if (!Alive(unit) || !_combatSkills.TryGetValue(unit,out var loadout)) return;
        _aiRawDamage.Clear();
        var battle = get_battle_unit(unit); battle.skill_queue.Clear(); battle.segment_action_index=0;
        var flags=gongfa_combat.GetPlanningFlags(unit);
        var initial=new AiDraft { Stamina=unit.Get("current_stamina").AsInt32(), Mana=unit.Get("current_mana").AsInt32(),
            Capacity=get_effective_mana_capacity(unit), Qi=get_battle_qi(unit),Blood=Mathf.RoundToInt(CardRules.Number(unit,"qi_blood")),
            Shield=unit.Get("shield").AsInt32(),Health=new(forecast),ConversionReady=flags.ConversionReady,CapQiUsed=flags.CapQiUsed,
            Last=gongfa_combat.GetLastNonResponseSkill(unit) };
        foreach(var target in get_target_candidates().Where(Alive)) initial.Shields[target]=Math.Max(0,target.Get("shield").AsInt32());
        foreach(var skill in loadout.Where(skill=>gongfa_combat.IsPermanentlyManaConverted(unit,skill)))initial.Converted.Add(skill);
        var beam=new List<AiDraft>{initial};
        var slots=get_action_slot_capacity(unit);
        for(var index=0;index<slots;index++)
        {
            var next=new List<AiDraft>();
            foreach(var draft in beam)
            {
                var candidates=AiDraftCandidates(unit,loadout,draft,teammate,slots-index).ToList();
                // 高优先级治疗/生存不能被累计一般输出压过。
                var urgent=candidates.Where(candidate=>candidate.Priority>=4).ToList();
                if(urgent.Count>0)candidates=urgent;
                var ranked=candidates.OrderByDescending(candidate=>candidate.Priority*100+candidate.Score).ToList();
                var choices=ranked.GroupBy(candidate=>candidate.Skill).Select(group=>group.First()).Take(5).ToList();
                foreach(var setup in ranked.Where(candidate=>candidate.Skill.skill_name is "催血" or "焚元" or "聚炁").Take(2))
                    if(!choices.Any(candidate=>candidate.Skill==setup.Skill))choices.Add(setup);
                foreach(var candidate in choices)
                {
                    var branch=draft.Copy();
                    if(!AiAdvanceDraft(unit,branch,candidate.Skill,candidate.Target,false))continue;
                    var action=new BattleQueuedSkill(candidate.Skill,candidate.Target);
                    if(candidate.Skill.artifact_instance_id.Length>0)action.artifact_intent=AiDraftExecution(unit,candidate.Skill,draft).artifact_is_summon
                        ?ArtifactBattleRuntime.Intent.Summon:ArtifactBattleRuntime.Intent.Use;
                    branch.Actions.Add(action);
                    branch.Value+=candidate.Priority*100+candidate.Score;
                    next.Add(branch);
                }
                // 休整也参与方案比较；必须至少还有一个位置，且能支撑后续实际可用的技能。
                if(urgent.Count==0&&index+1<slots&&draft.Stamina<unit.Get("max_stamina").AsInt32())
                {
                    var recovered=draft.Copy(); recovered.Stamina=Math.Min(unit.Get("max_stamina").AsInt32(),draft.Stamina+2);
                    if(AiDraftCandidates(unit,loadout,recovered,teammate,slots-index-1).Any(candidate=>candidate.EnergyCost>draft.Stamina))
                    { recovered.Actions.Add(BattleQueuedSkill.Rest()); recovered.Value-=35; next.Add(recovered); }
                }
                if(candidates.Count==0)
                { var empty=draft.Copy();empty.Actions.Add(BattleQueuedSkill.Empty()); next.Add(empty); }
            }
            if(next.Count==0)break;
            var ordered=next.OrderByDescending(draft=>draft.Value).ToList();
            beam=ordered.Take(6).ToList();
            // 保留铺垫与休整分支，避免即时伤害在前几步淘汰尚未兑现的连招。
            foreach(var setup in ordered.Where(draft=>draft.Actions.LastOrDefault() is { } last &&
                (last.kind==BattleQueuedSkill.ActionKind.Rest || last.kind==BattleQueuedSkill.ActionKind.Skill &&
                    last.skill.skill_name is "催血" or "焚元" or "聚炁")).Take(2))
                if(!beam.Contains(setup))beam.Add(setup);
        }
        var chosen=beam.OrderByDescending(draft=>draft.Value).First();
        foreach(var action in chosen.Actions)battle.skill_queue.Add(action);
        PreviewArtifactQueue(unit);
        foreach(var pair in chosen.Health)forecast[pair.Key]=pair.Value;
        _aiRawDamage.Clear();
    }

    private IEnumerable<AiCandidate> AiDraftCandidates(GodotObject unit,Godot.Collections.Array<ActiveSkillData> loadout,
        AiDraft draft,bool teammate,int remaining)
    {
        foreach(var skill in loadout)
        {
            if(skill.combat_empty_slot||IsXuanShuSkillSlot10(unit,skill)||skill.skill_type==ActiveSkillData.SkillType.CURSE
                ||!AiDraftReady(unit,skill,draft)||!gongfa_combat.CanPlay(unit,skill))continue;
            if(skill.skill_name=="焚元"&&draft.Capacity<2)continue;
            var execution=AiDraftExecution(unit,skill,draft);
            if(execution.artifact_is_summon&&!can_plan_artifact_summon(unit,skill))continue;
            var costs=AiDraftCosts(unit,execution,draft);
            if(!AiCanPay(unit,draft,costs.Energy,costs.Mana))continue;
            if(execution.artifact_is_summon)
            {
                var items=_artifactItems.GetValueOrDefault(unit,[]);
                var item=items.FirstOrDefault(item=>item.instance_id==skill.artifact_instance_id);
                var pending=items.Where(item=>draft.Summoned.Contains(item.instance_id)).Sum(item=>(long)item.spirit_cost);
                if(item is null||unit.Get("used_spirit_perception").AsInt32()+pending+item.spirit_cost>unit.Get("max_spirit_perception").AsInt32())continue;
                if(remaining>1&&AiTargets(unit,skill).Any(GodotObject.IsInstanceValid))
                    yield return new AiCandidate { Skill=skill,Target=unit,Priority=2,EnergyCost=costs.Energy,ManaCost=costs.Mana,
                        Score=AiRawDamage(unit,skill)*Math.Min(3,remaining-1)*5-costs.Energy*24-costs.Mana*30 };
                continue;
            }
            if(CardRules.IsResponse(skill)&&costs.Mana>draft.Mana)continue;
            if(skill.skill_name=="复命"&&(draft.Last is null||!AiDraftReady(unit,draft.Last,draft)
                ||!AiTargets(unit,draft.Last).Any(target=>GodotObject.IsInstanceValid(target))))continue;
            if(skill.skill_name=="复命"&&draft.Last is { } copy)
            {
                var second=AiDraftCosts(unit,copy,draft);
                if(!AiCanPay(unit,draft,costs.Energy+second.Energy,costs.Mana+second.Mana))continue;
            }
            foreach(var target in AiTargets(unit,skill))
            {
                if(target is not null&&draft.Health.GetValueOrDefault(target,1)<=0)continue;
                var candidate=ScoreAiSkill(unit,skill,target,draft.Health,costs.Energy,costs.Mana,
                    draft.Uses.GetValueOrDefault(skill),draft.Statuses,teammate);
                // 预计的连招伤害、护盾和铺垫取代旧的“当前一招”估分。
                if(!CardRules.IsResponse(skill))
                {
                    var preview=draft.Copy();
                    if(!AiAdvanceDraft(unit,preview,skill,target,false))continue;
                    var damage=draft.Health.Where(pair=>pair.Key.Get("faction").AsInt32()!=unit.Get("faction").AsInt32())
                        .Sum(pair=>Math.Max(0,pair.Value-preview.Health.GetValueOrDefault(pair.Key,pair.Value)));
                    var brokenShield=draft.Shields.Where(pair=>pair.Key.Get("faction").AsInt32()!=unit.Get("faction").AsInt32())
                        .Sum(pair=>Math.Max(0,pair.Value-preview.Shields.GetValueOrDefault(pair.Key,pair.Value)));
                    // 当前真实状态的旧估分不能把尚未显臂的伤害或已破盾后的伤害当成击杀。
                    if(candidate.Priority==4){candidate.Priority=2;candidate.Score-=1800;}
                    if(damage>0||brokenShield>0)
                    {
                        var oldHit=skill.aoe_range>0?1f:target is null?1f:BattleActionResolver.preview_hit_chance(unit,target);
                        candidate.Priority=Math.Max(candidate.Priority,2);candidate.Score+=damage*10+brokenShield*6-candidate.Damage*oldHit*10;
                        if(target is not null&&target is not ArtifactBody&&draft.Health[target]>0&&preview.Health[target]<=0
                            &&BattleActionResolver.preview_hit_chance(unit,target)>=.78f)
                        {candidate.Priority=4;candidate.Score+=1800;}
                    }
                    if(skill.special_effects.Any(effect=>effect.GetValueOrDefault("effect","").AsString()=="shield"))
                    {
                        var need=Math.Max(0,AiPublicThreat(unit,remaining)-draft.Shield);
                        candidate.Priority=need>0?2:0;
                        candidate.Score=need>0?Math.Min(need,preview.Shield-draft.Shield)*9-40:0;
                    }
                    if(skill.skill_name=="催血")
                    {
                        var gain=preview.Blood-draft.Blood;
                        var arms=gongfa_combat.PreviewBloodArmCount(unit,preview.Blood)-gongfa_combat.PreviewBloodArmCount(unit,draft.Blood);
                        var hasMelee=loadout.Any(card=>card.attack_style=="melee"&&card.damage_type=="physical"&&!IsXuanShuSkillSlot10(unit,card));
                        candidate.Priority=gain>0&&remaining>1&&hasMelee&&gongfa_combat.HasPower(unit,"百骸衍枝化生")?2:0;
                        candidate.Score=gain*12+arms*Math.Min(3,remaining-1)*100;
                    }
                    if(skill.skill_name=="焚元")
                    {
                        var gainedMana=preview.Mana-draft.Mana;
                        var canUseConversion=preview.ConversionReady&&loadout.Any(card=>card.mana_cost>0&&!IsXuanShuSkillSlot10(unit,card));
                        candidate.Priority=remaining>1&&(gainedMana>0||preview.Qi>draft.Qi||canUseConversion)?2:0;
                        candidate.Score=gainedMana*80+(canUseConversion?90:0)-60;
                    }
                    if(skill.special_effects.Any(effect=>effect.GetValueOrDefault("effect","").AsString()=="gain_qi")&&skill.skill_name!="焚元")
                    {
                        var useful=draft.Capacity>0&&(draft.Mana<draft.Capacity||gongfa_combat.HasPower(unit,"玄枢含章"));
                        candidate.Priority=useful&&remaining>1?2:0;
                        candidate.Score=damage*10+Math.Max(0,preview.Mana-draft.Mana)*80+Math.Max(0,preview.Qi-draft.Qi)*20;
                    }
                    if(skill.skill_name=="复命") candidate.Score=damage*10-60;
                }
                else
                {
                    // 只依据已经施展过、公开的来袭招式估计应对窗口；不看对方技能栏和隐藏队列。
                    var threat=AiResponseThreat(unit,skill,draft.Actions.Count);
                    var ordinary=CardRules.NormalCast(skill);
                    var value=threat*10+(threat>0&&gongfa_combat.HasPower(unit,"太和感物养元")?80:0);
                    if(ordinary is null){candidate.Priority=value>0?2:0;candidate.Score=value-40-draft.Uses.GetValueOrDefault(skill)*100;}
                    else candidate.Score+=value;
                }
                if(candidate.Priority>0)yield return candidate;
            }
        }
    }

    private bool AiDraftReady(GodotObject unit,ActiveSkillData skill,AiDraft draft)
    {
        if(!is_skill_ready(unit,skill))return false;
        var used=draft.Uses.GetValueOrDefault(skill);
        if(skill.cooldown_rounds>0&&used>0)return false;
        var perRound=_skillRoundCharges.TryGetValue(unit,out var round)?round.GetValueOrDefault(skill,skill.uses_per_round):skill.uses_per_round;
        var battleLimit=skill.uses_per_battle>0?skill.uses_per_battle:skill.max_charges;
        var perBattle=skill.uses_per_battle>0&&_skillBattleCharges.TryGetValue(unit,out var battle)?battle.GetValueOrDefault(skill,battleLimit)
            :_skillCharges.TryGetValue(unit,out var old)?old.GetValueOrDefault(skill,battleLimit):battleLimit;
        return (skill.uses_per_round<=0||used<perRound)&&(battleLimit<=0||used<perBattle);
    }

    private (int Energy,int Mana) AiDraftCosts(GodotObject unit,ActiveSkillData skill,AiDraft draft)
    {
        (int Energy,int Mana) Branch(ActiveSkillData branch)
        {
            var wounds=get_realtime_wound_counts(unit);
            var energy=gongfa_combat.Cost(unit,branch);
            var mana=Mathf.CeilToInt(branch.mana_cost*BattleWoundRules.get_mana_cost_multiplier(wounds,branch));
            if(draft.ConversionReady||draft.Converted.Contains(skill)){energy+=mana;mana=0;}
            if(branch.skill_type==ActiveSkillData.SkillType.MOVEMENT)
                energy=Mathf.CeilToInt(energy*BattleWoundRules.get_energy_cost_multiplier(wounds,branch));
            return(energy,mana);
        }
        var cost=Branch(skill);
        if(CardRules.IsResponse(skill)&&CardRules.NormalCast(skill) is { } normal)
        {var second=Branch(normal);return(Math.Max(cost.Energy,second.Energy),Math.Max(cost.Mana,second.Mana));}
        return cost;
    }
    private bool AiCanPay(GodotObject unit,AiDraft draft,int energy,int mana) => energy<=draft.Stamina &&
        (mana<=draft.Mana||gongfa_combat.HasPower(unit,"斫基损契")&&mana-draft.Mana<=draft.Capacity-1);
    private ActiveSkillData AiDraftExecution(GodotObject unit,ActiveSkillData skill,AiDraft draft) =>
        skill.artifact_instance_id.Length>0&&draft.Summoned.Contains(skill.artifact_instance_id)?skill:get_next_action_skill(unit,skill);

    private bool AiAdvanceDraft(GodotObject unit,AiDraft draft,ActiveSkillData skill,GodotObject? target,bool automatic)
    {
        if(skill.skill_name=="焚元"&&draft.Capacity<2)return false;
        var execution=AiDraftExecution(unit,skill,draft);
        var costs=AiDraftCosts(unit,execution,draft);
        if(!AiCanPay(unit,draft,costs.Energy,costs.Mana)||!AiDraftReady(unit,skill,draft))return false;
        var manaBefore=draft.Mana;
        draft.Stamina-=costs.Energy;
        if(execution.artifact_is_summon)
        {
            // 只预估祭出费用与占用，不能把尚未御使的伤害算到祭出动作里。
            draft.Mana=Math.Max(0,draft.Mana-costs.Mana);draft.Capacity-=Math.Max(0,costs.Mana-manaBefore);
            draft.Summoned.Add(skill.artifact_instance_id);
            if(execution.mana_cost>0&&draft.ConversionReady)draft.ConversionReady=false;
            AiCheckZhouTian(unit,draft,automatic);
            return true;
        }
        // 应对费用预留，但不把触发后的状态、治疗、返还视为确定已获得。
        if(CardRules.IsResponse(skill))
        {
            draft.Mana=Math.Max(0,draft.Mana-costs.Mana);
            draft.Capacity-=Math.Max(0,costs.Mana-manaBefore);
            draft.Uses[skill]=draft.Uses.GetValueOrDefault(skill)+1;
            if(skill.mana_cost>0&&draft.ConversionReady&&!draft.Converted.Contains(skill))draft.ConversionReady=false;
            // 两个分支结果不确定，均不提前兑现状态/回复；费用采用逐资源较大值。
            return true;
        }
        var burned=Math.Max(0,costs.Mana-draft.Mana);
        draft.Mana=Math.Max(0,draft.Mana-costs.Mana);draft.Capacity-=burned;
        if(costs.Mana==0&&skill.mana_cost>0&&draft.ConversionReady&&!draft.Converted.Contains(skill))draft.ConversionReady=false;
        draft.Uses[skill]=draft.Uses.GetValueOrDefault(skill)+1;
        if(burned>0&&unit is Character character&&character.main_gongfa?.gongfa_name=="斫基损契玄经")draft.Converted.Add(skill);
        AiCheckZhouTian(unit,draft,automatic);
        if(skill.skill_name=="复命")
        {
            if(draft.Last is not { } copy)return false;
            if(!AiAdvanceRandomDraft(unit,draft,copy))return false;
        }
        else { AiDraftEffects(unit,draft,skill,target,automatic); draft.Last=skill; }
        if(GongfaSkillRewriteRuntime.HasEffect(skill,"zhuo_ji_c2_jianzhu"))draft.ConversionReady=true;
        if(burned>0&&!draft.CapQiUsed&&AiHeartUpgrade(unit,"斫基损契玄经|zhuo_ji_c3"))
        {draft.CapQiUsed=true;draft.Qi++;AiCheckZhouTian(unit,draft,automatic);}
        return true;
    }

    private void AiDraftEffects(GodotObject unit,AiDraft draft,ActiveSkillData skill,GodotObject? target,bool automatic)
    {
        var bloodBefore=draft.Blood;
        if(target is not null&&AiRawDamage(unit,skill)>0)
        {
            var victims=skill.aoe_range>0?get_target_candidates().Where(Alive).Where(victim=>victim.Get("faction").AsInt32()!=unit.Get("faction").AsInt32()):new[]{target};
            foreach(var victim in victims)
            {
                var raw=AiRawDamage(unit,skill);
                var segments=new List<float>{raw};
                var arms=skill.attack_style=="melee"&&skill.damage_type=="physical"?gongfa_combat.PreviewBloodArmCount(unit,draft.Blood):0;
                if(arms>0&&skill.skill_name=="血臂撼岳"&&GongfaSkillRewriteRuntime.HasEffect(skill,"baihai_c2_cuishan"))segments.Add(raw);
                var weaponSegments=segments.Count;
                for(var arm=0;arm<arms;arm++)segments.Add(gongfa_combat.PreviewBloodArmDamage(unit,draft.Blood));
                var shield=draft.Shields.GetValueOrDefault(victim);float damage=0;
                var block=skill.damage_type=="physical"?GetStatus(victim,"化解"):0;
                var ignoresShield=GongfaSkillRewriteRuntime.HasEffect(skill,"xuan_shu_c1_jingzhe");
                for(var segmentIndex=0;segmentIndex<segments.Count;segmentIndex++)
                {
                    var segment=segments[segmentIndex];
                    var amount=Math.Max(0,Mathf.RoundToInt(segment));var blocked=Math.Min(block,amount);block-=blocked;amount-=blocked;
                    amount=BattleActionResolver.apply_defense(amount,skill.damage_type,victim,skill.physical_defense_ignore_ratio,segmentIndex<weaponSegments?ForgedEquipmentLoadout.WeaponIgnore(unit,skill):0);
                    var absorbed=ignoresShield?0:Math.Min(shield,amount);shield-=absorbed;damage+=amount-absorbed;
                }
                var hit=BattleActionResolver.preview_hit_chance(unit,victim);
                draft.Health[victim]=Math.Max(0,draft.Health.GetValueOrDefault(victim)-damage*hit);
                var previousShield=draft.Shields.GetValueOrDefault(victim);
                draft.Shields[victim]=Math.Max(0,Mathf.RoundToInt(previousShield+(shield-previousShield)*hit));
            }
        }
        foreach(var effect in skill.special_effects)
        {
            var kind=effect.GetValueOrDefault("effect","").AsString();var count=effect.GetValueOrDefault("count",1).AsInt32();
            switch(kind)
            {
                case "shield":draft.Shield+=Math.Max(0,Mathf.RoundToInt(CardRules.Evaluate(effect.GetValueOrDefault("formula","0").AsString(),unit)));break;
                case "gain_energy": case "restore_stamina":draft.Stamina=Math.Min(unit.Get("max_stamina").AsInt32(),draft.Stamina+count);break;
                case "restore_mana":draft.Mana=Math.Min(draft.Capacity,draft.Mana+count);break;
                case "burn_mana_capacity":draft.Capacity=Math.Max(1,draft.Capacity-count);draft.Mana=Math.Min(draft.Mana,draft.Capacity);AiCheckZhouTian(unit,draft,automatic);break;
                case "gain_qi":draft.Qi+=count;AiCheckZhouTian(unit,draft,automatic);break;
                case "gain_qi_blood":draft.Blood=Math.Min(Mathf.RoundToInt(CardRules.Number(unit,"max_qi_blood")),draft.Blood+count);break;
                case "restore_health_percent":draft.Health[unit]=Math.Min(unit.Get("max_health").AsInt32(),draft.Health[unit]+unit.Get("max_health").AsInt32()*effect.GetValueOrDefault("percent",.03f).AsSingle());break;
            }
        }
        if(skill.skill_name=="催血")
        {
            // 专属化法资源省略基础效果数组，运行时仍保留基础两层。
            if(!skill.special_effects.Any(effect=>effect.GetValueOrDefault("effect","").AsString()=="gain_qi_blood"))draft.Blood+=2;
            if(bloodBefore<=3&&GongfaSkillRewriteRuntime.HasEffect(skill,"baihai_c1_cuixue"))draft.Blood++;
            draft.Blood=Math.Min(Mathf.RoundToInt(CardRules.Number(unit,"max_qi_blood")),draft.Blood);
        }
        foreach(var effect in skill.status_effects)
        {
            var id=effect.GetValueOrDefault("status_id","").AsString();var owner=skill.target_type==0?unit:target;
            if(owner is not null&&id.Length>0)draft.Statuses.Add(StatusForecastKey(owner,id));
        }
    }

    private void AiCheckZhouTian(GodotObject unit,AiDraft draft,bool automatic)
    {
        if(draft.Capacity<=0||draft.Qi<2*draft.Capacity)return;
        draft.Qi=0;draft.Mana=draft.Capacity;
        if(automatic||!gongfa_combat.HasPower(unit,"玄枢含章")||!_combatSkills.TryGetValue(unit,out var skills))return;
        var skill=skills.FirstOrDefault(card=>IsXuanShuSkillSlot10(unit,card));
        if(skill is null||CardRules.IsResponse(skill)||skill.skill_name=="复命")return;
        AiAdvanceRandomDraft(unit,draft,skill);
    }
    private bool AiAdvanceRandomDraft(GodotObject unit,AiDraft draft,ActiveSkillData skill)
    {
        // 周天和复命的随机目标只能按所有合法结果平均估价，不替 AI 选中最优目标。
        var outcomes=new List<AiDraft>();
        foreach(var target in AiTargets(unit,skill).Where(GodotObject.IsInstanceValid)
            .Where(target=>draft.Health.GetValueOrDefault(target!,1)>0))
        {
            var outcome=draft.Copy();
            if(AiAdvanceDraft(unit,outcome,skill,target,true))outcomes.Add(outcome);
        }
        if(outcomes.Count==0)return false;
        var first=outcomes[0];
        draft.Stamina=first.Stamina;draft.Mana=first.Mana;draft.Capacity=first.Capacity;draft.Qi=first.Qi;
        draft.Blood=first.Blood;draft.Shield=first.Shield;draft.ConversionReady=first.ConversionReady;draft.CapQiUsed=first.CapQiUsed;
        draft.Last=first.Last;draft.Uses=first.Uses;draft.Converted=first.Converted;draft.Summoned=first.Summoned;
        foreach(var target in draft.Health.Keys.ToArray())draft.Health[target]=outcomes.Average(outcome=>outcome.Health[target]);
        foreach(var target in draft.Shields.Keys.ToArray())draft.Shields[target]=Mathf.RoundToInt((float)outcomes.Average(outcome=>outcome.Shields[target]));
        // 随机分配的状态不保证落在某人身上，只保留各结果都成立的状态。
        draft.Statuses=new HashSet<string>(outcomes.Select(outcome=>outcome.Statuses).Aggregate((left,right)=>new HashSet<string>(left.Intersect(right))));
        return true;
    }
    private static bool AiHeartUpgrade(GodotObject unit,string key) => unit.GetMeta("combat_heart_method_upgrades",new Godot.Collections.Dictionary()).AsGodotDictionary().ContainsKey(key);
    private float AiPublicThreat(GodotObject unit,int remaining) => get_target_candidates().Where(Alive)
        .Where(other=>other.Get("faction").AsInt32()!=unit.Get("faction").AsInt32())
        .Sum(other=>gongfa_combat.GetLastNonResponseSkill(other) is { } last&&last.calculate_damage(other)>0
            ?Math.Min(remaining,2)*EnemyExpectedDamage(last,other,unit):0);
    private float AiResponseThreat(GodotObject unit,ActiveSkillData response,int index)
    {
        var threat=0f;
        foreach(var enemy in get_target_candidates().Where(Alive).Where(other=>other.Get("faction").AsInt32()!=unit.Get("faction").AsInt32()))
        {
            var last=gongfa_combat.GetLastNonResponseSkill(enemy);
            if(last is null||last.calculate_damage(enemy)<=0||!CardRules.ResponseMatches(response,last.damage_type))continue;
            var ownTime=(index+1)/Math.Max(1,Initiative(unit));var enemyTime=1f/Math.Max(1,Initiative(enemy));
            var previous=index/Math.Max(1,Initiative(unit));
            if(MathF.Floor(ownTime/enemyTime)<=MathF.Floor(previous/enemyTime))continue;
            var chance=BattleActionResolver.preview_hit_chance(enemy,unit);
            threat+=EnemyExpectedDamage(last,enemy,unit)*chance/Math.Max(1,player_units.Count(Alive)+enemy_units.Count(Alive)-1);
        }
        return threat;
    }
}
