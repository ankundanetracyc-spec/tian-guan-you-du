using Godot;
using System;

public partial class ActionBattleSnapshotsSmoke : Node
{
    public override void _Ready()
    {
        try
        {
            var player = new BattleEntityData { uid = "player", current_health = 32, max_health = 40, physique = 8 };
            player.combat_skill_slot_paths.Add("res://resources/activeskills/应敌.tres");
            var enemy = new BattleEntityData { uid = "wolf", current_health = 20, max_health = 20 };
            var battle = new ActionBattleSnapshots(player, enemy);
            battle.Player.combat_skill_slot_paths[0] = "changed";
            if (player.combat_skill_slot_paths[0] == "changed") throw new Exception("快照槽位污染原数据");
            battle.SetHealth(12, 0);
            if (player.current_health != 32 || enemy.current_health != 20) throw new Exception("战斗修改了原生命值");
            var result = (BattleEntityData)battle.PlayerResults()[0].AsGodotObject();
            var defeated = (BattleEntityData)battle.EnemyResults()[0].AsGodotObject();
            if (result.uid != "player" || result.current_health != 12 || result.physique != 8 || defeated.uid != "wolf" || defeated.current_health != 0)
                throw new Exception("结算数据不匹配");
            result.current_health = 1;
            if (battle.Player.current_health != 12) throw new Exception("结算结果与场内快照共用引用");
            var invalidRejected = false;
            try { battle.SetHealth(41, 0); } catch (ArgumentOutOfRangeException) { invalidRejected = true; }
            if (!invalidRejected) throw new Exception("无效生命值未被拒绝");
            GD.Print("动作战斗快照隔离及结果检查通过");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError(ex.ToString());
            GetTree().Quit(1);
        }
    }
}
