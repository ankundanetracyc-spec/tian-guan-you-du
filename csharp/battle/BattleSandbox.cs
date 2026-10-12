using Godot;
using Godot.Collections;

public partial class BattleSandbox : Node2D
{
    private static readonly Array<string> TestSkills = ["res://resources/activeskills/灵击.tres", "res://resources/activeskills/符箓/雷击符.tres"];
    public override void _Ready() => CallDeferred(MethodName.start_sandbox);
    private void start_sandbox()
    {
        var manager=GetNodeOrNull<Node>("BattleMgr"); if(manager is null)return;
        var players=new Godot.Collections.Array { make_unit("沈青元",1,0),make_unit("离火散修",1,1)};
        var enemies=new Godot.Collections.Array { make_unit("山魈",2,2),make_unit("黑风盗",2,3)};
        var playerScenes=new Array<PackedScene>{make_character_scene(),make_character_scene()}; var enemyScenes=new Array<PackedScene>{make_character_scene(),make_character_scene()};
        manager.Call("setup_team_battle",playerScenes,enemyScenes,players,enemies);
        // 仅显式启动布局验收时启用，不影响菜单测试或正式战斗。
        if(OS.GetCmdlineUserArgs().Contains("--queue-layout-check"))
            AddChild(new BattleQueueLayoutCheck());
        // 沙盒示例状态便于直接检查帧倒计时、层数角标与悬停说明；正式战斗不预置这些状态。
        var units=manager.Get("all_units").AsGodotArray();
        var inner=GD.Load<StatusEffect>("res://resources/battle/statuses/内伤.tres");
        var outer=GD.Load<StatusEffect>("res://resources/battle/statuses/外伤.tres");
        var agility=GD.Load<StatusEffect>("res://resources/battle/statuses/身法.tres");
        if(units.Count>0&&inner is not null)units[0].AsGodotObject()?.Call("add_status",inner,inner.duration_default,1,"sandbox",inner.max_stacks,"测试状态");
        if(units.Count>1&&outer is not null)units[1].AsGodotObject()?.Call("add_status",outer,outer.duration_default,2,"sandbox",outer.max_stacks,"测试状态");
        if(units.Count>2&&agility is not null)units[2].AsGodotObject()?.Call("add_status",agility,-1,2,"sandbox",agility.max_stacks,"测试状态");
    }
    private PackedScene make_character_scene(){var prototype=new Character();var scene=new PackedScene();scene.Pack(prototype);prototype.QueueFree();return scene;}
    private BattleEntityData make_unit(string name,int faction,int seed)
    {
        return new BattleEntityData {
            uid = $"battle_sandbox_{seed}", entity_name = name, faction = faction,
            cultivation_realm = "云息三层", appearance_seed = 1100 + seed, gender = seed % 2,
            battle_portrait_path = BattlePaperDoll.DefaultPortraitPath,
            physique = 12, intelligence = 13, agility = 16 + seed, soul = 10, comprehension = 12,
            max_health = 150, current_health = 150, max_stamina = 100, current_stamina = 100,
            max_mana = 30, current_mana = 30, max_spirit_perception = 20, used_spirit_perception = 0,
            precision = 80, dodge = 8, critical = 10,
            crit_resist = 5, cards_per_turn = 2, initial_hand_size = 2, active_skill_paths = TestSkills,
            combat_skill_slot_paths = TestSkills,
        };
    }
}
