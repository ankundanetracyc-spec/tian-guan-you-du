using Godot;

/// <summary>新增三本的事件结算；只在施展、回落与受击时读取，不逐帧扫描。</summary>
public sealed partial class GongfaCombatRuntime
{
    private const string YuSuiBook = "玉髓回澜养真宝册", YuSuiHeart = "玉髓回澜";
    private const string BaoXuBook = "抱虚养晦通玄录", BaoXuHeart = "虚室生白";
    private const string CloudBook = "舒卷天章", CloudHeart = "流云剑势";
    private readonly System.Collections.Generic.Dictionary<ulong, ExpansionState> expansionStates = [];
    private sealed record CastSnapshot(int Intent, int Shield, bool EmptyMana);
    private sealed class ExpansionState
    {
        public int EmptyManaTriggers;
        public bool ShieldBloodUsed, PendingShieldBlood, EmptyExtraUsed, EmptyGuardUsed, EmptyStrikeShieldUsed;
        public int PendingYuSuiShield;
        public readonly System.Collections.Generic.Dictionary<string, CastSnapshot> Casts = new(StringComparer.Ordinal);
    }
    private ExpansionState Expansion(GodotObject unit)
    {
        if (!expansionStates.TryGetValue(unit.GetInstanceId(), out var state))
            expansionStates[unit.GetInstanceId()] = state = new();
        return state;
    }
    private void ResetExpansion(GodotObject unit) => expansionStates[unit.GetInstanceId()] = new();
    private void BeforeExpansion(GodotObject unit, ActiveSkillData skill)
    {
        Expansion(unit).Casts[ManaSkillKey(skill)] = new(Math.Clamp(Stacks(unit,"剑意"),0,10),
            Math.Max(0,Mathf.RoundToInt(CardRules.Number(unit,"shield"))), CardRules.Number(unit,"current_mana")<=0);
    }
    private CastSnapshot CastAtStart(GodotObject unit, ActiveSkillData skill) =>
        Expansion(unit).Casts.GetValueOrDefault(ManaSkillKey(skill)) ?? new(Math.Clamp(Stacks(unit,"剑意"),0,10),
            Math.Max(0,Mathf.RoundToInt(CardRules.Number(unit,"shield"))),CardRules.Number(unit,"current_mana")<=0);

    private bool ExpansionUtility(GodotObject unit, ActiveSkillData skill)
    {
        if (skill.skill_type==ActiveSkillData.SkillType.POWER && skill.skill_name is YuSuiHeart or BaoXuHeart or CloudHeart)
        {
            if (GongfaAvailability.IsBookNameEnabled(skill.source_gongfa_name))
            { For(unit).Powers.Add(skill.skill_name); AddPowerStatus(unit,skill); }
            return true;
        }
        if (skill.source_gongfa_name==YuSuiBook && skill.skill_name=="涤髓")
        {
            var before=CardRules.Number(unit,"qi_blood");
            Fall(unit,1,true);
            if (before>CardRules.Number(unit,"qi_blood") && HasEffect(skill,"yusui_c1_disui") && CardRules.Number(unit,"shield")>0)
                GrantShield(unit,Mathf.RoundToInt(CardRules.Evaluate("2 + 体魄×0.10 + 体道造诣×0.15",unit)));
            return true;
        }
        if (skill.source_gongfa_name==BaoXuBook && skill.skill_name=="守寂")
        {
            GrantShield(unit,Mathf.RoundToInt(CardRules.Evaluate("4 + 灵慧×0.20 + 神魂×0.15 + 术法造诣×0.20",unit)));
            var state=Expansion(unit);
            if (HasEffect(skill,"baoxu_c2_shouji") && CastAtStart(unit,skill).EmptyMana && !state.EmptyExtraUsed)
            { state.EmptyExtraUsed=true; GrantExpansionQi(unit,1); }
            return true;
        }
        return false;
    }

    /// <summary>只有公共气血回落调用，独立治疗和散血不经过此入口。</summary>
    private void HealBloodFall(Creature unit, float rate)
    {
        var requested=Math.Max(0,Mathf.RoundToInt(unit.max_health*rate/100f));
        var boosted=requested<=0?0:Math.Max(1,Mathf.RoundToInt(requested*(1f+Math.Max(0,unit.healing_received_bonus))));
        var actual=unit.apply_healing(requested);
        if (!HasPower(unit,YuSuiHeart)) return;
        var source=For(unit).PendingCard;
        var ratio=source is not null && source.skill_name=="涤髓" && HasEffect(source,"yusui_c4_disui")?1f:.5f;
        GrantShield(unit,Mathf.FloorToInt(Math.Max(0,boosted-actual)*ratio));
    }

    public void RecordShieldAbsorbed(GodotObject target, GodotObject attacker, int shieldBefore, int absorbed)
    {
        if (absorbed<=0 || shieldBefore<=0 || CardRules.Number(target,"shield")>0
            || CardRules.Number(target,"faction")==CardRules.Number(attacker,"faction")
            || !HasPower(target,YuSuiHeart) || !Main(target,YuSuiBook)) return;
        var state=Expansion(target);
        if (!state.ShieldBloodUsed) state.PendingShieldBlood=true;
    }

    private void FinishExpansionIncoming(GodotObject unit)
    {
        var state=Expansion(unit);
        if (state.PendingYuSuiShield>0) GrantShield(unit,state.PendingYuSuiShield/2);
        state.PendingYuSuiShield=0;
        if (state.PendingShieldBlood && !state.ShieldBloodUsed && CardRules.Number(unit,"current_health")>0)
        { state.ShieldBloodUsed=true; Gain(unit,1); manager.SyncBloodArmVisual(unit); }
        state.PendingShieldBlood=false;
    }

    private void ExpansionResponse(GodotObject unit, ActiveSkillData skill, int blocked)
    {
        if (blocked<=0) return;
        var state=Expansion(unit);
        if (HasEffect(skill,"yusui_c3_jiazhao"))
            state.PendingYuSuiShield=(int)Math.Min(int.MaxValue,(long)state.PendingYuSuiShield+blocked);
        if (HasEffect(skill,"baoxu_c3_jiazhao") && CardRules.Number(unit,"current_mana")==0 && !state.EmptyGuardUsed)
        { state.EmptyGuardUsed=true; GrantExpansionQi(unit,1); }
    }

    private void AfterExpansionAttack(GodotObject unit, ActiveSkillData skill, bool hit)
    {
        if (!hit) return;
        var start=CastAtStart(unit,skill);
        if (skill.source_gongfa_name==YuSuiBook && skill.skill_name=="鸣泉" && start.Shield>0)
        { Gain(unit,1); manager.SyncBloodArmVisual(unit); }
        var state=Expansion(unit);
        if (HasEffect(skill,"baoxu_c4_sanzhao") && start.EmptyMana && !state.EmptyStrikeShieldUsed)
        {
            state.EmptyStrikeShieldUsed=true;
            GrantShield(unit,Mathf.RoundToInt(CardRules.Evaluate("3 + 灵慧×0.15 + 术法造诣×0.20",unit)));
        }
    }

    private void AfterExpansionSkill(GodotObject unit, ActiveSkillData skill)
    {
        var state=Expansion(unit);
        if (!CardRules.IsResponse(skill) && skill.skill_type!=ActiveSkillData.SkillType.POWER
            && HasPower(unit,BaoXuHeart) && CastAtStart(unit,skill).EmptyMana && state.EmptyManaTriggers<2)
        {
            // 先扣限次再产炁，自动施展或周天不能重入尚未扣除的计数。
            state.EmptyManaTriggers++;
            GrantExpansionQi(unit,Main(unit,BaoXuBook) && state.EmptyManaTriggers==1?2:1);
        }
        state.Casts.Remove(ManaSkillKey(skill));
    }

    private void GrantExpansionQi(GodotObject unit, int amount)
    {
        if (manager.gain_battle_qi(unit,amount,out var completed)) manager.show_status_gain(unit,completed?"周天":"炁",completed?1:amount);
    }

    private float ExpansionDamageMultiplier(GodotObject unit, ActiveSkillData skill)
    {
        var start=CastAtStart(unit,skill);
        if (skill.combat_is_gongfa_bonus_damage_segment) return 1;
        if (HasEffect(skill,"yusui_c2_mingquan") && start.Shield>0) return 1.2f;
        if (HasEffect(skill,"baoxu_c1_sanzhao") && start.EmptyMana) return 1.2f;
        return 1;
    }

    public int CloudFollowUpLimit(GodotObject unit, ActiveSkillData skill, bool firstHit)
    {
        if (!firstHit || skill.damage_type!="physical" || skill.combat_is_gongfa_bonus_damage_segment) return 0;
        if (skill.source_gongfa_name==CloudBook && skill.skill_name=="云岚三叠") return 3;
        var intent=CastAtStart(unit,skill).Intent;
        if (!HasPower(unit,CloudHeart) || !skill.tags.Contains("剑") || intent<(Main(unit,CloudBook)?5:6)) return 0;
        return HasHeartUpgrade(unit,CloudBook,"shujuan_c4") && intent>=10?2:1;
    }

    public float CloudFollowUpChance(GodotObject unit, ActiveSkillData skill, int index)
    {
        if (index<0 || index>=3) return 0;
        if (skill.skill_name!="云岚三叠" || skill.source_gongfa_name!=CloudBook) return 1;
        var first=Mathf.Clamp(.40f+CastAtStart(unit,skill).Intent*.05f+CardRules.Number(unit,"sword_proficiency")*.005f,.40f,.80f);
        return Math.Max(0,first-.20f*index);
    }

    public ActiveSkillData CreateCloudFollowUp(GodotObject unit, ActiveSkillData skill)
    {
        var clone=CloneBonusDamageSegment(skill);
        clone.combat_is_cloud_segment=true;
        var bonus=HasHeartUpgrade(unit,CloudBook,"shujuan_c2") || HasHeartUpgrade(unit,CloudBook,"shujuan_c3")
            || HasHeartUpgrade(unit,CloudBook,"shujuan_c4") || skill.skill_name=="云岚三叠"
            ?Math.Max(0,CastAtStart(unit,skill).Intent-6):0;
        clone.damage_formula=skill.source_gongfa_name==CloudBook && skill.skill_name=="云岚三叠"
            ?$"round(4 + 体魄×0.30 + 剑道造诣×0.50 + {bonus}*4)"
            :$"round(2 + 体魄×0.20 + 剑道造诣×0.30 + {bonus})";
        return clone;
    }
}
