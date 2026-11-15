using Godot;
using System.Reflection;

public partial class HumanCombatBoardFormalSmoke : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); _checks++; GD.Print("BOARD_FORMAL_PASS " + message); }
    private async Task Frames(int count = 1) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> test, string message, int seconds = 40)
    {
        var deadline = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!test() && Time.GetTicksMsec() < deadline) await Frames(); Check(test(), message);
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(4); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng("res://docs/美术规范/人类招式图标新版_20261107/" + name + ".png") == Error.Ok, "正式场景截图 " + name);
    }
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        try
        {
            GetWindow().ContentScaleSize = new(1672, 941); GetWindow().Size = new(1672, 941);
            Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "未打开正式旅程");
            var world = GD.Load<PackedScene>("res://tscn/debug/HumanCombatBoardReturnWorld.tscn").Instantiate<HumanCombatBoardReturnWorld>();
            GetTree().Root.AddChild(world); GetTree().CurrentScene = world; await Frames(4);
            var original = world.Player.get_snapshot_data(); original.initiative_speed = 10000;
            var enemy = new BattleEntityData { uid = "board_return_foe", entity_name = "人类对练者", faction = 2,
                current_health = 1, max_health = 1, physique = 5, intelligence = 5, soul = 5, precision = 0, dodge = 0,
                crit_resist = 10000, initiative_speed = 50, combat_skill_slots_configured = true, combat_resolved_skills = [] };
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            transition.set_battle_return_context(new Godot.Collections.Dictionary { ["is_training"] = false, ["title"] = "人类招式盘验收", ["allow_prebattle_cancel"] = true });
            var scene = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            transition.start_team_battle([scene], [scene], [original], [enemy], "res://tscn/debug/HumanCombatBoardReturnWorld.tscn");
            await Until(() => transition.has_open_flow_view(), "正式战前页打开");
            transition.GetChildren().OfType<BattleFlowView>().Single().FindChildren("*", "Button", true, false)
                .OfType<Button>().Single(button => button.Text == "开始战斗").EmitSignal(BaseButton.SignalName.Pressed);
            await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "进入正式战斗场景");
            var manager = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
            Check(manager.is_board_battle() && manager.is_board_editing(), "正式人类对战接入招式盘");
            var hud = GetTree().CurrentScene.GetNode<CanvasLayer>("UILayer").GetChildren().OfType<BattleBoardHud>().Single();
            Check(hud.Draft.migrated_from_legacy && hud.Draft.legacy_skill_keys.SequenceEqual(original.combat_skill_slot_paths), "旧十槽含空位完整进入迁移记录");
            hud.Draft.placements.Clear();
            Check(hud.TryPlaceSource("res://resources/entities/items/alchemy/huichun_1.tres", new(0, 0)), "从真实行囊摆入一颗回春丹");
            Check(hud.TryPlaceSource("res://resources/activeskills/应敌.tres", new(1, 0)), "从真实已学池摆入招式");
            await Capture("正式场景_配招_1672");
            GetWindow().Size = new(1280, 720); await Capture("正式场景_配招_1280"); GetWindow().Size = new(1672, 941);
            hud.CloseEditor(); await Capture("正式场景_查看战场"); hud.OpenEditor();
            Check(manager.commit_player_board(hud.Draft, out _), "正式开战提交草稿");
            await Capture("正式场景_扫盘战斗");
            await Until(() => !manager.battle_running && transition.has_open_flow_view() && !transition.is_flow_transition_active(), "真实服丹及最后一击进入正式结算");
            var returned = manager.player_units[0] as Character;
            Check(returned!.get_inventory_total_count("res://resources/entities/items/alchemy/huichun_1.tres") == 1, "正式战斗只消耗一颗");
            transition.GetChildren().OfType<BattleFlowView>().Single().FindChildren("*", "Button", true, false)
                .OfType<Button>().Single(button => button.Text is "返回" or "返回并领取").EmitSignal(BaseButton.SignalName.Pressed);
            await Until(() => GetTree().CurrentScene is HumanCombatBoardReturnWorld returnedWorld
                && ((HumanData)returnedWorld.Player.entity_data!).combat_board_layout?.confirmed == true && !transition.is_flow_transition_active(), "正式返回场景恢复已确认布局");
            world = (HumanCombatBoardReturnWorld)GetTree().CurrentScene;
            var human = (HumanData)world.Player.entity_data!;
            Check(world.Player.get_inventory_total_count("res://resources/entities/items/alchemy/huichun_1.tres") == 1 && human.combat_board_layout!.placements.Any(item => item.expended), "真实返回保留库存与空瓶占位");
            Check(human.combat_skill_slot_paths.SequenceEqual(original.combat_skill_slot_paths), "新布局未覆盖旧十槽备份");
            world.GetNode<CombatTrigger>("CombatTrigger").check_and_restore_battle_results();
            Check(world.Player.get_inventory_total_count("res://resources/entities/items/alchemy/huichun_1.tres") == 1, "重复恢复不重复消费");
            var save = new PersistentGameSave { player_data = human };
            Check(ResourceSaver.Save(save, "res://tmp/combat_board_20261107/formal_return_save.tres") == Error.Ok, "隔离存档保存");
            var loaded = ResourceLoader.Load<PersistentGameSave>("res://tmp/combat_board_20261107/formal_return_save.tres", cacheMode: ResourceLoader.CacheMode.Ignore)!;
            Check(loaded.player_data!.combat_board_layout!.confirmed && loaded.player_data.combat_board_layout.placements.Any(item => item.expended), "重载保留确认布局与已服用标记");
            Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "完整往返未使用正式旅程");
            GD.Print($"HUMAN_COMBAT_BOARD_FORMAL_OK checks={_checks}"); CallDeferred(nameof(Shutdown));
        }
        catch (Exception error) { GD.PushError("HUMAN_COMBAT_BOARD_FORMAL_FAILED " + error); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private async void Shutdown()
    {
        await Frames(3);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        await Frames(2); GetTree().Quit();
    }
}
