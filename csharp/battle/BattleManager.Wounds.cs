using Godot;
using Godot.Collections;

public partial class BattleManager : Node2D
{
    public void add_wound_card(GodotObject target, string wound, int count = 1, string destination = "discard")
    {
        if(_realtimeMode&&target is Creature creature&&(wound is "内伤" or "外伤"))
        {
            if(count<=0)return;
            var effect=GD.Load<StatusEffect>($"res://resources/battle/statuses/{wound}.tres");
            if(effect is not null)
            {
                creature.add_status(effect,effect.duration_default,Mathf.Max(1,count),"wound",4,"伤势");
                creature.current_mana=Math.Min(creature.current_mana,get_effective_mana_capacity(creature));
            }
            return;
        }
        var path=wound=="伤势"?"res://resources/activeskills/业障/伤势.tres":BattleWoundRules.get_wound_path(wound);
        if(string.IsNullOrEmpty(path)||get_deck_manager(target) is not {} deck)return;
        var source=GD.Load<ActiveSkillData>(path);if(source is null)return;
        for(var i=0;i<count;i++)deck.discard(source.Duplicate() as ActiveSkillData);
        RefreshWoundHealth(target);
    }
    public int count_wounds(GodotObject u)
    {
        if(_realtimeMode&&u is Creature creature)return creature.get_status_stacks("内伤")+creature.get_status_stacks("外伤");
        var deck=get_deck_manager(u);return Hand(u).Count(GongfaCombatRuntime.IsWound)+(deck?.draw_pile.Count(GongfaCombatRuntime.IsWound)??0)+(deck?.discard_pile.Count(GongfaCombatRuntime.IsWound)??0);
    }
    public Godot.Collections.Dictionary get_realtime_wound_counts(GodotObject u)
    {
        var counts=new Godot.Collections.Dictionary{{"external",0},{"internal",0}};
        if(u is Creature creature){counts["external"]=creature.get_status_stacks("外伤");counts["internal"]=creature.get_status_stacks("内伤");}
        return counts;
    }
    public int get_effective_mana_capacity(GodotObject u)=>Math.Max(0,u.Get("max_mana").AsInt32()-(u is Creature creature?Math.Min(2,creature.get_status_stacks("内伤")):0));
    public int get_raw_skill_mana_cost(GodotObject u,ActiveSkillData skill)
    {
        var wounds=get_realtime_wound_counts(u);
        var multiplier=BattleWoundRules.get_mana_cost_multiplier(wounds,skill);
        return Mathf.CeilToInt(skill.mana_cost*multiplier);
    }
    public int get_skill_mana_cost(GodotObject u,ActiveSkillData skill)
    {
        var cost=get_raw_skill_mana_cost(u,skill);
        return gongfa_combat.ForceManaCapacityPayment(u,skill,cost)||gongfa_combat.ManaCostTransferredToStamina(u,skill,cost)>0?0:cost;
    }
    public int get_skill_mana_capacity_cost(GodotObject u,ActiveSkillData skill) =>
        gongfa_combat.ForcedManaCapacityCost(u,skill,get_raw_skill_mana_cost(u,skill));
    public int get_skill_energy_cost(GodotObject u,ActiveSkillData skill)
    {
        var cost=gongfa_combat.Cost(u,skill);
        var wounds=get_realtime_wound_counts(u);
        var mana=get_raw_skill_mana_cost(u,skill);
        cost+=gongfa_combat.ManaCostTransferredToStamina(u,skill,mana);
        return skill.skill_type==ActiveSkillData.SkillType.MOVEMENT?Mathf.CeilToInt(cost*BattleWoundRules.get_energy_cost_multiplier(wounds,skill)):cost;
    }
    public bool can_pay_skill_mana_cost(GodotObject unit,ActiveSkillData skill)
    {
        var rawCost=get_raw_skill_mana_cost(unit,skill);
        if(!CanReserveForgedSpirit(unit,skill))return false;
        if(gongfa_combat.ForceManaCapacityPayment(unit,skill,rawCost))
            return gongfa_combat.CanPayForcedManaCapacity(unit,skill,rawCost);
        return can_pay_mana_amount(unit,get_skill_mana_cost(unit,skill));
    }
    public bool can_pay_mana_amount(GodotObject unit,int amount)
    {
        var missing=Math.Max(0,amount-Math.Max(0,unit.Get("current_mana").AsInt32()));
        return gongfa_combat.CanBurnManaCapacityForCost(unit,missing);
    }
    public bool spend_mana_capacity(GodotObject unit,int amount)
    {
        if(amount<=0)return true;
        if(get_effective_mana_capacity(unit)-amount<1)return false;
        unit.Set("max_mana",Math.Max(1,unit.Get("max_mana").AsInt32()-amount));
        var capacity=get_effective_mana_capacity(unit);
        unit.Set("current_mana",Math.Min(Math.Max(0,unit.Get("current_mana").AsInt32()),capacity));
        if(gain_battle_qi(unit,0,out var completedZhouTian)&&completedZhouTian)
            show_status_gain(unit,"周天",1);
        if(unit.HasMethod("update_ui"))unit.Call("update_ui");
        return true;
    }
    private void PaySkillManaCost(GodotObject unit,ActiveSkillData skill,int cost)
    {
        var rawCost=get_raw_skill_mana_cost(unit,skill);
        if(gongfa_combat.ForceManaCapacityPayment(unit,skill,rawCost))
        {
            gongfa_combat.BeginForcedManaCapacityPayment(unit,skill,rawCost);
            for(var i=0;i<rawCost;i++)
            {
                if(!spend_mana_capacity(unit,1))break;
                gongfa_combat.RecordManaCapacityPayment(unit,skill);
                gongfa_combat.ConsumeForcedManaCapacityPoint(unit,skill);
            }
            gongfa_combat.CompleteForcedManaCapacityPayment(unit,skill);
            return;
        }
        for(var i=0;i<cost;i++)
        {
            var current=Math.Max(0,unit.Get("current_mana").AsInt32());
            if(current>0){unit.Set("current_mana",current-1);continue;}
            if(!gongfa_combat.CanBurnManaCapacityForCost(unit,1)||!spend_mana_capacity(unit,1))break;
            gongfa_combat.RecordManaCapacityPayment(unit,skill);
        }
    }
    public float get_realtime_action_speed_multiplier(GodotObject u)=>BattleWoundRules.get_action_speed_multiplier(get_realtime_wound_counts(u));
    public int consume_hand_wounds(GodotObject u,int count)
    {
        if(_realtimeMode&&u is Creature realtimeCreature)
        {
            var inner=realtimeCreature.get_status_stacks("内伤");
            var outer=realtimeCreature.get_status_stacks("外伤");
            var available=inner+outer;
            var consumed=Math.Min(Math.Max(0,count),available);
            var consumedInner=Math.Min(inner,consumed);
            realtimeCreature.consume_status_stacks("内伤",consumedInner);
            realtimeCreature.consume_status_stacks("外伤",consumed-consumedInner);
            var healingPerStack=realtimeCreature.get_combat_tempering_total("wound_consume_heal_percent_per_stack");
            if(healingPerStack>0)realtimeCreature.apply_healing(Mathf.RoundToInt(realtimeCreature.max_health*healingPerStack*consumed/100f));
            return consumed;
        }
        var hand=Hand(u);var selected=hand.Where(GongfaCombatRuntime.IsWound).Take(count).ToArray();
        foreach(var c in selected){hand.Remove(c);get_deck_manager(u)?.exhaust_card(c);}
        _hands[u]=hand;RefreshWoundHealth(u);if(u==active_unit)_handPanel?.Call("populate_hand",hand);
        return selected.Length;
    }
    private void RefreshWoundHealth(GodotObject u)
    {
        if(u is not Creature c)return;
        if(!u.HasMeta("unwounded_health"))u.SetMeta("unwounded_health",c.max_health);
        c.max_health=Math.Max(1,Mathf.RoundToInt(u.GetMeta("unwounded_health").AsInt32()*(1-count_wounds(u)*.01f)));
        c.current_health=Math.Min(c.current_health,c.max_health);
    }
}
