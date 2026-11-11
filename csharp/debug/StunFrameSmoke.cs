using Godot;

/// <summary>僵直帧层数状态的刷新、计时与解除边界检查。</summary>
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
        var target=new Creature{entity_data=new LivingData{physique=16,cultivation_realm="云息一层"}};
        try
        {
            var status=GD.Load<StatusEffect>("res://resources/battle/statuses/僵直.tres")!;
            Check(status.frame_counted_layers&&status.max_stacks==120,"资源使用帧层数且上限120");
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
            GD.Print($"STUN_FRAME_SMOKE: {checks} checks passed");
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError("STUN_FRAME_SMOKE: "+error);GetTree().Quit(1);}
        finally{target.Free();manager.Free();}
    }
}
