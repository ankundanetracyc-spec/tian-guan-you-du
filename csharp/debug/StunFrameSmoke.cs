using Godot;

/// <summary>僵直帧层数、跨境界削减、刷新及解除边界的独立运行检查。</summary>
public partial class StunFrameSmoke : Node
{
    private int checks;
    private void Check(bool value,string message)
    {
        if(!value)throw new InvalidOperationException(message);
        checks++;
    }
    public override void _Ready()=>CallDeferred(nameof(Run));
    private void Run()
    {
        var manager=new BattleManager();
        var caster=new Creature{entity_data=new LivingData{physique=16,cultivation_realm="云息一层"}};
        var target=new Creature{entity_data=new LivingData{physique=16,cultivation_realm="云息一层"}};
        try
        {
            var skill=GD.Load<ActiveSkillData>("res://resources/activeskills/太和感物养元宝册/化法/叩山.tres")!;
            var status=GD.Load<StatusEffect>("res://resources/battle/statuses/僵直.tres")!;
            var runtime=manager.gongfa_combat;
            Check(status.frame_counted_layers&&status.max_stacks==120,"资源使用帧层数且上限120");
            var sameChance=runtime.PreviewTaiheStunChance(caster,target,skill)!.Value;
            Check(runtime.PreviewTaiheStunFrames(caster,target,skill)==60,"同体魄同境界60帧");
            target.cultivation_realm="玄基一层";
            Check(Mathf.IsEqualApprox(runtime.PreviewTaiheStunChance(caster,target,skill)!.Value,sameChance*.2f),"低一大境界概率乘20%");
            Check(runtime.PreviewTaiheStunFrames(caster,target,skill)==12,"低一大境界时长乘20%");
            target.cultivation_realm="金丹初期";
            Check(Mathf.IsEqualApprox(runtime.PreviewTaiheStunChance(caster,target,skill)!.Value,sameChance*.04f),"低两大境界概率乘4%");
            Check(runtime.PreviewTaiheStunFrames(caster,target,skill)==2,"乘算后帧数向下取整");
            target.cultivation_realm="神胎初期";
            Check(!runtime.RollTaiheStun(caster,target,skill,out var zeroFrames)&&zeroFrames==0,"不足1帧不施加，不补1层");
            target.cultivation_realm="云息一层";
            caster.physique=0;target.physique=1000;
            Check(runtime.PreviewTaiheStunChance(caster,target,skill)==0&&runtime.PreviewTaiheStunFrames(caster,target,skill)==0,"允许零概率和零层数");
            caster.physique=100000;
            Check(runtime.PreviewTaiheStunChance(caster,target,skill)<=.99f&&runtime.PreviewTaiheStunFrames(caster,target,skill)==120,"极端属性受双上限约束");
            caster.physique=target.physique=16;
            target.add_status_combat_time(status,1f);
            Check(target.get_status_stacks("僵直")==60,"1帧1层");
            manager.action_gauges[target]=1000f;
            var deadline=typeof(BattleManager).GetMethod("TimeUntilAction",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            Check(Mathf.IsEqualApprox((float)deadline.Invoke(manager,[target])!,1f),"满行动条的僵直单位有明确解除时刻，可在同一推进步骤行动");
            Check(target.advance_realtime_status_timers(1,60,"僵直")==0&&target.get_status_stacks("僵直")==59,"每战斗帧减1层且冻结进度");
            target.advance_realtime_status_timers(59,60,"僵直");
            Check(target.get_status_stacks("僵直")==0&&target.status_snapshot().Length==0,"归零同帧解除，无浮点残留");
            Check((float)deadline.Invoke(manager,[target])! == 0f&&manager.action_gauges[target].AsSingle()==1000f,"解除后保持原有进度并立刻恢复行动资格");
            target.add_status_combat_time(status,1f);
            Check(Mathf.IsEqualApprox(target.advance_realtime_status_timers(80,60,"僵直"),20),"大步推进仅冻结前60帧，余20帧继续行动");
            target.add_status_combat_time(status,1f);
            target.advance_realtime_status_timers(10*3,60,"僵直");
            Check(target.get_status_stacks("僵直")==30,"三倍速推进30战斗帧消耗30层");
            target.advance_realtime_status_timers(10*3,60,"僵直");
            Check(target.get_status_stacks("僵直")==0,"三倍速同一战斗时钟解除");
            target.add_status_combat_time(status,2f);
            target.add_status_combat_time(status,.2f);
            Check(target.get_status_stacks("僵直")==12,"再次施加刷新为新结果，不累加或取最大");
            target.advance_realtime_status_timers(12,60,"僵直");
            target.add_status_combat_time(status,0);target.add_status_combat_time(status,float.NaN);target.add_status_combat_time(status,float.PositiveInfinity);target.add_status_combat_time(status,.001f);
            Check(target.status_snapshot().Length==0,"零、非有限与不足1帧输入不会生成无限期状态");
            target.add_status(status,stacks:30);
            Check(target.get_status_stacks("僵直")==30,"通用状态入口按传入层数初始化战斗帧");
            target.consume_status_stacks("僵直",10);
            target.advance_realtime_status_timers(1,60,"僵直");
            Check(target.get_status_stacks("僵直")==19,"移除层数同步缩短计时，不在下帧恢复已移除层数");
            target.add_status_combat_time(status,float.MaxValue);
            Check(target.get_status_stacks("僵直")==120,"外部极端时长在转换整数前截断");
            var text=CardPresentation.Face(skill,caster);
            Check(text.Contains("僵直层数公式")&&!text.Contains("C层")&&!text.Contains("秒"),"局内技能显示帧层数公式，不显示秒或占位字母");
            GD.Print($"STUN_FRAME_SMOKE: {checks} checks passed");
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError("STUN_FRAME_SMOKE: "+error);GetTree().Quit(1);}
        finally{caster.Free();target.Free();manager.Free();}
    }
}
