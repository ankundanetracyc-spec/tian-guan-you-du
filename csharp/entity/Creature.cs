using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

/// <summary>人类与妖兽的共享战斗实体，属性唯一存储于 LivingData。</summary>
public partial class Creature : WorldEntity
{
    // 敏捷主导身法反应，体魄、灵慧与神魂提供较温和的补正。
    public const float InitiativeBase = 80f, InitiativePhysiqueWeight = .5f, InitiativeIntelligenceWeight = .75f, InitiativeAgilityWeight = 1.5f, InitiativeSoulWeight = .75f;
    protected LivingData? data => entity_data as LivingData;
    public int battle_round_id;
    public GArray statuses=[]; public int shield,qi_blood,max_qi_blood,blood_furnace_value,blood_furnace_value_cap=100,punch_frame; public Godot.Collections.Dictionary status_stack_cap_bonuses=[];
    /// <summary>战斗内按心法归属汇总的淬炼属性；战斗结束随单位实例销毁。</summary>
    public Godot.Collections.Dictionary combat_tempering_by_gongfa=[];
    public float base_physique{get=>data?.base_physique??0;set{if(data is not null)data.base_physique=value;}} public float base_intelligence{get=>data?.base_intelligence??0;set{if(data is not null)data.base_intelligence=value;}} public float base_agility{get=>data?.base_agility??0;set{if(data is not null)data.base_agility=value;}} public float base_soul{get=>data?.base_soul??0;set{if(data is not null)data.base_soul=value;}} public float base_fortune{get=>data?.base_fortune??0;set{if(data is not null)data.base_fortune=value;}} public float base_comprehension{get=>data?.base_comprehension??0;set{if(data is not null)data.base_comprehension=value;}}
    public float physique{get=>data?.physique??0;set{if(data is not null)data.physique=value;}} public float intelligence{get=>data?.intelligence??0;set{if(data is not null)data.intelligence=value;}} public float agility{get=>data?.agility??0;set{if(data is not null)data.agility=value;}} public float soul{get=>data?.soul??0;set{if(data is not null)data.soul=value;}} public float fortune{get=>data?.fortune??0;set{if(data is not null)data.fortune=value;}} public float comprehension{get=>data?.comprehension??0;set{if(data is not null)data.comprehension=value;}}
    public int spirit_strands{get=>data?.spirit_strands??0;set{if(data is not null)data.spirit_strands=Math.Max(0,value);}} public int current_age{get=>data?.current_age??0;set{if(data is not null)data.current_age=value;}} public int max_age{get=>data?.max_age??0;set{if(data is not null)data.max_age=value;}} public string cultivation_realm{get=>data?.cultivation_realm??"凡人";set{if(data is not null)data.cultivation_realm=value;}}
    public int current_health{get=>data?.current_health??0;set{if(data is not null)data.current_health=value;}} public int max_health{get=>data?.max_health??0;set{if(data is not null)data.max_health=value;}} public int current_stamina{get=>data?.current_stamina??0;set{if(data is not null)data.current_stamina=value;}} public int max_stamina{get=>data?.max_stamina??0;set{if(data is not null)data.max_stamina=value;}} public int current_mana{get=>data?.current_mana??0;set{if(data is not null)data.current_mana=value;}} public int max_mana{get=>data?.max_mana??0;set{if(data is not null)data.max_mana=value;}} public int current_mind{get=>data?.current_mind??0;set{if(data is not null)data.current_mind=value;}} public int max_mind{get=>data?.max_mind??0;set{if(data is not null)data.max_mind=value;}} public int used_spirit_perception{get=>data?.used_spirit_perception??0;set{if(data is not null)data.used_spirit_perception=value;}} public int max_spirit_perception{get=>data?.max_spirit_perception??0;set{if(data is not null)data.max_spirit_perception=value;}}
    public int physical_defense{get=>data?.physical_defense??0;set{if(data is not null)data.physical_defense=value;}} public float physical_damage_reduction{get=>data?.physical_damage_reduction??0;set{if(data is not null)data.physical_damage_reduction=value;}} public float healing_received_bonus{get=>data?.healing_received_bonus??0;set{if(data is not null)data.healing_received_bonus=value;}} public int body_qi_blood_bonus{get=>data?.body_qi_blood_bonus??0;set{if(data is not null)data.body_qi_blood_bonus=value;}} public int precision{get=>data?.precision??0;set{if(data is not null)data.precision=value;}} public int dodge{get=>data?.dodge??0;set{if(data is not null)data.dodge=value;}} public int critical{get=>data?.critical??0;set{if(data is not null)data.critical=value;}} public int crit_resist{get=>data?.crit_resist??0;set{if(data is not null)data.crit_resist=value;}} public float crit_damage_mult{get=>data?.crit_damage_mult??1.5f;set{if(data is not null)data.crit_damage_mult=value;}} public float initiative_speed{get=>data?.initiative_speed??100;set{if(data is not null)data.initiative_speed=value;}}
    public int cards_per_turn{get=>data?.cards_per_turn??0;set{if(data is not null)data.cards_per_turn=value;}} public int initial_hand_size{get=>data?.initial_hand_size??0;set{if(data is not null)data.initial_hand_size=value;}} public Godot.Collections.Array<Resource> active_skills{get=>data?.active_skills??[];set{if(data is not null)data.active_skills=value;}}
    public override void _Ready(){entity_data ??= new LivingData();base._Ready();}
    public void initialize_body_combat_state(bool enabled,int cap=3)
    {
        qi_blood=0;
        max_qi_blood=enabled?Math.Max(0,cap+body_qi_blood_bonus+Mathf.RoundToInt(get_combat_tempering_total("qi_blood_cap"))):0;
        blood_furnace_value=0;
        blood_furnace_value_cap=Math.Max(0,100+Mathf.RoundToInt(get_combat_tempering_total("blood_furnace_cap")));
    }
    /// <summary>战斗开场应用淬炼上限；保留受伤生命的绝对缺口，不把最大生命视作治疗。</summary>
    public void apply_combat_tempering_stats()
    {
        var flatHealth=get_combat_tempering_total("max_health_flat");
        var healthPercent=get_combat_tempering_total("max_health_percent");
        if(!Mathf.IsZeroApprox(flatHealth)||!Mathf.IsZeroApprox(healthPercent))
        {
            var oldMax=max_health;
            var oldMissing=Math.Max(0,oldMax-current_health);
            max_health=Math.Max(1,Mathf.RoundToInt(oldMax*(1f+healthPercent/100f)+flatHealth));
            current_health=Math.Max(0,max_health-oldMissing);
        }
        var dodgePercent=get_combat_tempering_total("dodge_percent");
        if(!Mathf.IsZeroApprox(dodgePercent))dodge=Mathf.Max(0,Mathf.RoundToInt(dodge*(1f+dodgePercent/100f)));
    }
    public Godot.Collections.Dictionary get_combat_tempering_for_gongfa(string gongfaName)
    {
        if(!combat_tempering_by_gongfa.TryGetValue(gongfaName,out var raw)||raw.VariantType!=Variant.Type.Dictionary)return new Godot.Collections.Dictionary();
        return raw.AsGodotDictionary();
    }
    public float get_combat_tempering_total(string key)
    {
        var total=0f;
        foreach(Variant bookName in combat_tempering_by_gongfa.Keys)
        {
            var values=get_combat_tempering_for_gongfa(bookName.AsString());
            total+=values.GetValueOrDefault(key,0f).AsSingle();
        }
        return total;
    }
    public float get_combat_tempering_physical_damage_multiplier()=>get_combat_tempering_total("physical_damage_percent")/100f;
    public string get_combat_tempering_summary(string gongfaName)
    {
        var values=get_combat_tempering_for_gongfa(gongfaName);
        if(values.Count==0)return "";
        var lines=new List<string>();
        foreach(var key in new[]{"max_health_flat","max_health_percent","qi_blood_cap","physical_damage_percent","dodge_percent","blood_furnace_cap","fall_heal_rate_pp","wound_physical_reduction_pp_per_stack","wound_consume_heal_percent_per_stack","defuse_gain_layers","borrowed_force_damage_pp_per_stack"})
        {
            var value=values.GetValueOrDefault(key,0f).AsSingle();if(Mathf.IsZeroApprox(value))continue;
            var line=key switch
            {
                "max_health_flat"=>$"最大生命 +{value:0.#}",
                "max_health_percent"=>$"最大生命 +{value:0.#}%",
                "qi_blood_cap"=>$"气血上限 +{value:0.#}",
                "physical_damage_percent"=>$"物理伤害 +{value:0.#}%",
                "dodge_percent"=>$"身法 +{value:0.#}%",
                "blood_furnace_cap"=>$"血炉上限 +{value:0.#}",
                "fall_heal_rate_pp"=>$"气血回落回复率 +{value:0.#}个百分点",
                "wound_physical_reduction_pp_per_stack"=>$"每层伤势使所受物理伤害减免额外 +{value:0.#} 个百分点（最多8层，最高 +{value*8:0.#}%；独立减伤层与其他百分比减伤乘算，百分比减伤上限90%）",
                "wound_consume_heal_percent_per_stack"=>$"主动消耗每层伤势回复最大生命 +{value:0.#}%",
                "defuse_gain_layers"=>$"获得化解时额外 +{value:0.#}层",
                "borrowed_force_damage_pp_per_stack"=>$"每层借力额外提高物理伤害 +{value:0.#}个百分点",
                _=>""
            };
            if(line.Length>0)lines.Add(line);
        }
        return string.Join("\n",lines);
    }
    public int gain_qi_blood(int amount){var gain=Math.Min(Math.Max(amount,0),max_qi_blood-qi_blood);qi_blood+=gain;return gain;} public int spend_qi_blood(int amount){var spend=Math.Min(Math.Max(amount,0),qi_blood);qi_blood-=spend;return spend;} public float get_blood_furnace_physical_damage_multiplier()=>max_qi_blood<=0?0:blood_furnace_value/2*.01f;
    /// <summary>统一受疗入口；心部位的受疗加成只放大实际回复量，不突破生命上限。</summary>
    public int apply_healing(int amount)
    {
        if (amount <= 0) return 0;
        var boosted = Mathf.Max(1, Mathf.RoundToInt(amount * (1f + Mathf.Max(0f, healing_received_bonus))));
        var actual = Math.Min(Math.Max(0, max_health - current_health), boosted);
        current_health += actual;
        return actual;
    }
    public void add_status(StatusEffect effect,int duration=-1,int stacks=1,string sourceId="",int stackCap=0,string sourceName="")
    {
        if(effect is null)return;
        if(effect.effect_id=="化解"&&stacks>0)stacks+=Mathf.RoundToInt(get_combat_tempering_total("defuse_gain_layers"));
        var existing=status_instances().FirstOrDefault(s=>s.effect?.effect_id==effect.effect_id&&(!effect.stack_by_source||s.source_id==sourceId));
        if(existing is null){existing=new StatusInstance{effect=effect,remaining_duration=duration<0?effect.duration_default:duration,stacks=0,source_id=sourceId,source_name=sourceName};statuses.Add(existing);}
        var effectiveDuration=duration<0?effect.duration_default:duration;
        switch(effect.duration_type)
        {
            case StatusEffect.DurationType.Turns:
                if(effectiveDuration>0){existing.remaining_duration=Math.Max(existing.remaining_duration,effectiveDuration);existing.applied_round_id=battle_round_id;}
                break;
            case StatusEffect.DurationType.Frames:
                if(effectiveDuration>0)existing.remaining_frames=Mathf.Max(existing.remaining_frames,effectiveDuration);
                else existing.remaining_frames=-1;
                break;
            case StatusEffect.DurationType.Hits:
                existing.remaining_hits=Math.Max(existing.remaining_hits,effectiveDuration>0?effectiveDuration:1);
                break;
            case StatusEffect.DurationType.Trigger:
                existing.remaining_frames=-1;
                break;
        }
        existing.stacks=Mathf.Min(existing.stacks+Mathf.Max(1,stacks),stackCap>0?stackCap:Mathf.Max(1,effect.max_stacks));
    }
    public int get_status_stacks(string id)=>status_instances().Where(s=>s.effect?.effect_id==id).Sum(s=>s.stacks);
    public int get_status_stacks_from_source(string id,string sourceId)=>status_instances().Where(s=>s.effect?.effect_id==id&&s.source_id==sourceId).Sum(s=>s.stacks);
    public void consume_status_stacks(string id,int amount){for(var i=statuses.Count-1;i>=0&&amount>0;i--){if(statuses[i].AsGodotObject() is not StatusInstance s||s.effect?.effect_id!=id)continue;var used=Math.Min(amount,s.stacks);s.stacks-=used;amount-=used;if(s.stacks<=0)statuses.RemoveAt(i);}}
    public Dictionary process_status_event(string eventName,Dictionary context)
    {
        if(eventName=="turn_end"){
            foreach(var instance in status_instances().ToArray())
            {
                if(instance.effect?.duration_type!=StatusEffect.DurationType.Turns||instance.remaining_duration<=0)continue;
                if(battle_round_id>0&&instance.applied_round_id==battle_round_id)continue;
                if(--instance.remaining_duration==0)statuses.Remove(instance);
            }
            return context;
        }
        if(eventName=="after_damage"&&context.GetValueOrDefault("target",default).AsGodotObject()==this){
            foreach(var instance in status_instances().ToArray())if(instance.effect?.duration_type==StatusEffect.DurationType.Hits&&--instance.remaining_hits<=0)statuses.Remove(instance);
            return context;
        }
        if(eventName!="turn_start")return context;
        foreach(var instance in status_instances().ToArray())
        {
            var damage=instance.get_damage_per_tick();if(damage>0){current_health=Mathf.Max(0,current_health-damage);if(context.TryGetValue("battle_manager",out var manager))manager.AsGodotObject()?.Call("show_damage_number",this,damage,"dot",false);}
            var heal=instance.get_heal_per_tick();if(heal>0)apply_healing(heal);
            shield+=Mathf.Max(0,instance.get_shield_per_tick());current_stamina=Mathf.Min(max_stamina,current_stamina+Mathf.Max(0,instance.get_stamina_per_tick()));

        }
        return context;
    }
    public StatusInstance[] status_snapshot()=>status_instances().ToArray();
    public float status_bonus(string key)=>status_instances().Sum(s=>s.effect!.stat_modifiers.GetValueOrDefault(key,0).AsSingle()*s.stacks);
    public void tick_status_frames(float frames){if(frames<=0)return;foreach(var s in status_instances().ToArray())if(s.effect?.duration_type==StatusEffect.DurationType.Frames&&s.remaining_frames>0){s.remaining_frames-=frames;if(s.remaining_frames<=0)statuses.Remove(s);}}
    private IEnumerable<StatusInstance> status_instances(){foreach(var value in statuses)if(value.AsGodotObject() is StatusInstance instance)yield return instance;}
    /// <summary>基础属性最终值 = 基础值 × (1 + 境界累计倍率 + 其他倍率) + 固定值。
    /// 云息每层分别提供倍率；四层、七层为小分界，单层收益略高。悟性与福缘不吃境界倍率。</summary>
    public static float calculate_base_initiative_speed(float physique,float intelligence,float agility,float soul)=>InitiativeBase+physique*InitiativePhysiqueWeight+intelligence*InitiativeIntelligenceWeight+agility*InitiativeAgilityWeight+soul*InitiativeSoulWeight;
    public float get_base_initiative_speed()=>calculate_base_initiative_speed(physique,intelligence,agility,soul);
    /// <summary>即时战斗资源恢复倍率；高属性继续有效，但采用对数递减，避免高境界失控。</summary>
    public static float calculate_combat_regen_factor(float attributes)=>Mathf.Min(1.75f,1f+.08f*Mathf.Log(1f+Mathf.Max(0,attributes)/10f));
    public float get_energy_recovery_interval()=>360f/calculate_combat_regen_factor(physique+agility);
    public float get_mana_recovery_interval()=>480f/calculate_combat_regen_factor(intelligence+soul);
    /// <summary>人物的正式派生公式。精力费用是独立战斗机制，不由四维推导。</summary>
    public void recalculate_attributes(){var realmMultiplier=get_realm_attribute_multiplier();physique=base_physique*realmMultiplier;intelligence=base_intelligence*realmMultiplier;agility=base_agility*realmMultiplier;soul=base_soul*realmMultiplier;fortune=base_fortune;comprehension=base_comprehension;max_health=Mathf.RoundToInt(60+physique*6+base_physique*2+agility*3+soul*3+intelligence*1.5f);max_mind=Mathf.RoundToInt(30+soul*4+intelligence*1.5f+physique*.5f);max_mana=0;initiative_speed=get_base_initiative_speed();current_health=Math.Min(current_health,max_health);current_mind=Math.Min(current_mind,max_mind);precision=Mathf.RoundToInt(intelligence*4+agility*3+soul+physique*.5f);dodge=Mathf.RoundToInt(agility*4+soul*2+intelligence+physique*.5f);critical=Mathf.RoundToInt(physique*.5f+agility*.8f+soul*1.2f+intelligence*.3f);crit_resist=Mathf.RoundToInt(physique*3+intelligence*2+agility*2+soul*1.5f);}
    public float get_realm_attribute_bonus()=>get_realm_attribute_multiplier()-1f;
    /// <summary>仅返回当前小境界本次提供的倍率，供界面按“本层 +X%”而非累计值展示。</summary>
    public float get_current_realm_attribute_increment()
    {
        const string prefix="云息";
        if(!cultivation_realm.StartsWith(prefix)||!cultivation_realm.EndsWith("层"))return 0f;
        if(!int.TryParse(cultivation_realm[prefix.Length..^1],out var tier))return 0f;
        return tier is 4 or 7 ? .15f : tier is >= 1 and <= 13 ? .10f : 0f;
    }
    private float get_realm_attribute_multiplier()
    {
        const string prefix="云息";
        if(!cultivation_realm.StartsWith(prefix)||!cultivation_realm.EndsWith("层"))return 1f;
        if(!int.TryParse(cultivation_realm[prefix.Length..^1],out var tier))return 1f;
        var total=0f;
        for(var layer=1;layer<=Mathf.Clamp(tier,1,13);layer++) total+=layer is 4 or 7?.15f:.10f;
        return 1f+total;
    }
    public override string get_detail_text()=>$"{entity_name}\n境界: {cultivation_realm}\n生命: {current_health}/{max_health}\n精力: {current_stamina}/{max_stamina}\n法力: {current_mana}/{max_mana}\n";
    public override void apply_battle_data(BattleEntityData snapshot){base.apply_battle_data(snapshot);current_age=snapshot.current_age;max_age=snapshot.max_age;cultivation_realm=snapshot.cultivation_realm;physique=snapshot.physique;intelligence=snapshot.intelligence;agility=snapshot.agility;soul=snapshot.soul;current_health=snapshot.current_health;max_health=snapshot.max_health;current_stamina=snapshot.current_stamina;max_stamina=snapshot.max_stamina;current_mana=snapshot.current_mana;max_mana=snapshot.max_mana;physical_defense=snapshot.physical_defense;physical_damage_reduction=snapshot.physical_damage_reduction;healing_received_bonus=snapshot.healing_received_bonus;body_qi_blood_bonus=snapshot.body_qi_blood_bonus;precision=snapshot.precision;dodge=snapshot.dodge;critical=snapshot.critical;crit_resist=snapshot.crit_resist;crit_damage_mult=snapshot.crit_damage_mult;initiative_speed=snapshot.initiative_speed;combat_tempering_by_gongfa=snapshot.combat_tempering_by_gongfa.Duplicate(true);}
    public override BattleEntityData get_snapshot_data(){var snapshot=base.get_snapshot_data();snapshot.current_age=current_age;snapshot.max_age=max_age;snapshot.cultivation_realm=cultivation_realm;snapshot.physique=physique;snapshot.intelligence=intelligence;snapshot.agility=agility;snapshot.soul=soul;snapshot.fortune=Mathf.RoundToInt(fortune);snapshot.comprehension=Mathf.RoundToInt(comprehension);snapshot.used_spirit_perception=used_spirit_perception;snapshot.max_spirit_perception=max_spirit_perception;snapshot.current_health=current_health;snapshot.max_health=max_health;snapshot.current_stamina=current_stamina;snapshot.max_stamina=max_stamina;snapshot.current_mana=current_mana;snapshot.max_mana=max_mana;snapshot.physical_defense=physical_defense;snapshot.physical_damage_reduction=physical_damage_reduction;snapshot.healing_received_bonus=healing_received_bonus;snapshot.body_qi_blood_bonus=body_qi_blood_bonus;snapshot.precision=precision;snapshot.dodge=dodge;snapshot.critical=critical;snapshot.crit_resist=crit_resist;snapshot.crit_damage_mult=crit_damage_mult;snapshot.initiative_speed=initiative_speed;snapshot.combat_tempering_by_gongfa=combat_tempering_by_gongfa.Duplicate(true);return snapshot;}
}
