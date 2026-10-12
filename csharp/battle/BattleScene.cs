using Godot;

/// <summary>消费待处理战斗包，并把双方数据交给场景内的战斗管理器。</summary>
public partial class BattleScene : Node2D
{
    private Node? battle_mgr;

    // 场景的 Ready 传播期间，新加入部署层的角色尚未执行 Ready；
    // 此时写入生命值会因 LivingData 尚未创建而丢失。
    public override void _Ready() => CallDeferred(nameof(InitializeBattle));

    private void InitializeBattle()
    {
        battle_mgr = GetNodeOrNull<Node>("BattleMgr");
        var transitionManager = GetTree().Root.GetNodeOrNull("BattleTransitionManager");
        if (!GodotObject.IsInstanceValid(transitionManager)) { GD.PushError("BattleScene: 找不到 BattleTransitionMgr 单例"); return; }
        if (!GodotObject.IsInstanceValid(battle_mgr)) { GD.PushError("BattleScene: 找不到 BattleMgr 节点"); return; }
        var package = transitionManager!.Call("consume_pending_battle_package").AsGodotArray();
        if (package.Count != 4) { GD.PushError("BattleScene: 未收到完整的战斗包数据"); return; }
        battle_mgr!.Call("set_spectator_battle", transitionManager.Call("is_spectator_battle_active").AsBool());
        battle_mgr!.Call("setup_team_battle", package[0], package[1], package[2], package[3]);
    }
}
