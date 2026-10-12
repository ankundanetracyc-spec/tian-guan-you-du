using Godot;

public partial class BattleManager
{
    private bool _retreatAttemptedThisRound;
    private string _retreatNotice="";
    private double _retreatNoticeUntil;
    public string get_retreat_notice()=>Time.GetTicksMsec()<_retreatNoticeUntil?_retreatNotice:"";
    public bool get_retreat_chance(out float chance)
    {
        chance=0;
        if(!Alive(_mainPlayerUnit))return false;
        var enemies=enemy_units.Where(Alive).ToList();
        if(enemies.Count==0 || enemies.Any(e=>!e.HasMeta("battle_dun_speed")||e.GetMeta("battle_dun_speed").AsSingle()<=0))return false;
        var own=_mainPlayerUnit!.GetMeta("battle_dun_speed",0f).AsSingle();
        return GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.try_get_retreat_chance(own,enemies.Max(e=>e.GetMeta("battle_dun_speed").AsSingle()),out chance)==true;
    }
    public bool can_queue_retreat()=>can_player_plan() && !_retreatAttemptedThisRound && get_retreat_chance(out _) &&
        get_filled_action_slot_count(_mainPlayerUnit)<get_action_slot_capacity(_mainPlayerUnit) &&
        !get_battle_unit(_mainPlayerUnit!).skill_queue.Any(a=>a.kind==BattleQueuedSkill.ActionKind.Retreat);
    public void queue_player_retreat()
    {
        if(!can_queue_retreat())return;
        PlacePlayerAction(BattleQueuedSkill.Retreat());
    }
    private void ResolveRetreat(GodotObject unit)
    {
        var success=false;
        if(!_retreatAttemptedThisRound && unit==_mainPlayerUnit && get_retreat_chance(out var chance))success=GD.Randf()<chance;
        _retreatAttemptedThisRound=true;
        if(success){finish_battle(true);return;}
        _retreatNotice="撤退失败，继续战斗";_retreatNoticeUntil=Time.GetTicksMsec()+3000;
        CompleteSegmentAction(unit);
    }
}
