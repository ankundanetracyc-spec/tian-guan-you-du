using Godot;

public partial class BattleManager
{
    private static bool AiMainBook(GodotObject unit,string book)=>unit is Character c?c.main_gongfa?.gongfa_name==book:
        unit is WorldEntity entity&&entity.entity_data is HumanData data&&data.main_gongfa_path==$"res://resources/gongfa/{book}.tres";

    private void AiSevenBookUtility(GodotObject unit,AiDraft draft,ActiveSkillData skill,bool automatic)
    {
        if(skill.source_gongfa_name=="玉髓回澜养真宝册"&&skill.skill_name=="涤髓"&&draft.Blood>0)
        {
            draft.Blood--;
            var max=CardRules.Number(unit,"max_health");
            var rate=5f+(unit is Creature c?c.get_combat_tempering_total("fall_heal_rate_pp"):0);
            var healing=Math.Max(1,Mathf.RoundToInt(Mathf.RoundToInt(max*rate/100)*(1+Math.Max(0,CardRules.Number(unit,"healing_received_bonus")))));
            var actual=Math.Min(Math.Max(0,max-draft.Health.GetValueOrDefault(unit)),healing);
            draft.Health[unit]=Math.Min(max,draft.Health.GetValueOrDefault(unit)+actual);
            if(gongfa_combat.HasPower(unit,"玉髓回澜"))
                draft.Shield+=Mathf.FloorToInt((healing-actual)*(GongfaSkillRewriteRuntime.HasEffect(skill,"yusui_c4_disui")?1f:.5f));
            if(draft.Shield>0&&GongfaSkillRewriteRuntime.HasEffect(skill,"yusui_c1_disui"))
                draft.Shield+=Mathf.RoundToInt(CardRules.Evaluate("2 + 体魄×0.10 + 体道造诣×0.15",unit));
        }
        if(skill.source_gongfa_name=="抱虚养晦通玄录"&&skill.skill_name=="守寂")
        {
            draft.Shield+=Mathf.RoundToInt(CardRules.Evaluate("4 + 灵慧×0.20 + 神魂×0.15 + 术法造诣×0.20",unit));
            if(draft.Mana==0&&!draft.EmptyGuardQiUsed&&GongfaSkillRewriteRuntime.HasEffect(skill,"baoxu_c2_shouji"))
            {draft.EmptyGuardQiUsed=true;draft.Qi++;AiCheckZhouTian(unit,draft,automatic);}
        }
    }

    private void AiSevenBookAfterSkill(GodotObject unit,AiDraft draft,ActiveSkillData skill,bool empty,bool automatic)
    {
        if(!empty||draft.EmptyManaTriggers>=2||CardRules.IsResponse(skill)||!gongfa_combat.HasPower(unit,"虚室生白"))return;
        draft.EmptyManaTriggers++;
        draft.Qi+=draft.EmptyManaTriggers==1&&AiMainBook(unit,"抱虚养晦通玄录")?2:1;
        AiCheckZhouTian(unit,draft,automatic);
    }

    private void ScoreSevenBookSetup(GodotObject unit,ActiveSkillData skill,AiDraft draft,AiCandidate candidate,int remaining)
    {
        if(CardRules.IsResponse(skill))return;
        if(skill.skill_name is not ("涤髓" or "守寂" or "养锋")&&!gongfa_combat.HasPower(unit,"虚室生白"))return;
        var preview=draft.Copy();
        if(!AiAdvanceDraft(unit,preview,skill,candidate.Target,false))return;
        if(skill.skill_name=="涤髓")
        {
            var value=(preview.Health.GetValueOrDefault(unit)-draft.Health.GetValueOrDefault(unit))*4+(preview.Shield-draft.Shield)*2-35;
            var lostArms=gongfa_combat.PreviewBloodArmCount(unit,draft.Blood)-gongfa_combat.PreviewBloodArmCount(unit,preview.Blood);
            value-=lostArms*Math.Min(3,remaining)*25;
            if(value>0){candidate.Priority=Math.Max(candidate.Priority,2);candidate.Score=Math.Max(candidate.Score,value);}
        }
        if(skill.skill_name=="养锋"&&remaining>1)
        {
            var gain=preview.SwordIntent-draft.SwordIntent;
            var threshold=AiMainBook(unit,"舒卷天章")?5:6;
            var crosses=gongfa_combat.HasPower(unit,"流云剑势")&&draft.SwordIntent<threshold&&preview.SwordIntent>=threshold;
            if(gain>0){candidate.Priority=Math.Max(candidate.Priority,2);candidate.Score=Math.Max(candidate.Score,gain*20+(crosses?100:0));}
        }
        var gainedMana=preview.Mana-draft.Mana;var gainedQi=preview.Qi-draft.Qi;
        if(remaining>1&&(gainedMana>0||gainedQi>0)&&gongfa_combat.HasPower(unit,"虚室生白"))
        {candidate.Priority=Math.Max(candidate.Priority,2);candidate.Score=Math.Max(candidate.Score,gainedMana*70+Math.Max(0,gainedQi)*25-15);}
    }
}
