using Godot;

public partial class BattleManager
{
    public void SyncBloodArmVisual(GodotObject unit)
    {
        if(_spectatorBattle||!_realtimeMode||unit is not Creature creature||unit is not Node2D subject
            ||!gongfa_combat.HasPower(unit,"百骸衍枝化生"))return;
        var arms=gongfa_combat.BloodArmCount(unit);
        var view=BattleBloodQiArms.ForSubject(subject);
        view.SetQiVisualLevel(creature.qi_blood,gongfa_combat.BloodArmFirstThreshold(unit));
        view.SetArmCount(arms);
    }

    public void PlayBloodArmFollowUp(GodotObject attacker,GodotObject target)
    {
        if(_spectatorBattle||attacker is not Node2D subject||target is not Node2D targetNode)return;
        subject.GetNodeOrNull<BattleBloodQiArms>("BloodQiArms")?.QueueFollowUp(targetNode);
    }

    public void PlayBloodArmAutoGuard(GodotObject defender,GodotObject attacker)
    {
        if(_spectatorBattle||defender is not Node2D subject||attacker is not Node2D targetNode)return;
        subject.GetNodeOrNull<BattleBloodQiArms>("BloodQiArms")?.PlayAutoGuard(targetNode);
    }
}
