using Godot;
using System.Reflection;

/// <summary>在隔离战场验证自动规划、暂停、手动接管和真实队列结算。</summary>
public partial class AutoBattleSmoke : Node
{
    private const string Attack = "res://resources/activeskills/应敌.tres";
    private const string Heal = "res://resources/entities/items/alchemy/huichun_1.tres";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("AUTO_BATTLE_PASS " + message);
    }
    private async Task Frames(int count = 1)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> condition, string message, int seconds = 35)
    {
        var deadline = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!condition() && Time.GetTicksMsec() < deadline) await Frames();
        Check(condition(), message);
    }
    private async Task Click(Button button)
    {
        var position = GetViewport().GetFinalTransform() * (button.GetGlobalTransformWithCanvas() * (button.Size / 2));
        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
        Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false });
        await Frames();
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var capture = GetViewport().GetTexture().GetImage();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://tmp/auto_battle_20261106"));
        Check(capture.SavePng($"res://tmp/auto_battle_20261106/{name}.png") == Error.Ok, "界面截图 " + name);
    }
    private static int Round(BattleManager manager) => (int)typeof(BattleManager).GetField("_segmentNumber", Private)!.GetValue(manager)!;
    private static BattleEntityData Unit(string id, int faction, bool attack, bool supplies) => new()
    {
        uid = id, entity_name = id, faction = faction, cultivation_realm = "云息三层",
        max_health = 10000, current_health = supplies ? 100 : 10000,
        max_stamina = 6, max_mana = 3, test_mana_cap_override = 3,
        physique = 10, intelligence = 10, soul = 10, precision = 10000, dodge = 0,
        initiative_speed = 10000, combat_skill_slots_configured = true,
        combat_resolved_skills = attack ? [GD.Load<ActiveSkillData>(Attack)] : [],
        pack_state_included = true,
        pack_item_paths = supplies ? [Heal] : [], pack_item_counts = supplies ? [1] : [],
        pack_item_positions = supplies ? [0] : [],
        battle_supplies = supplies ? [new() { item_path = Heal, count = 1 }] : []
    };

    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1672, 941);
            GetWindow().Size = new Vector2I(1672, 941);
            var scene = new Node2D { Name = "AutoBattleIsolatedScene" }; AddChild(scene);
            scene.AddChild(new Node2D { Name = "DeployLayer" });
            var ui = new CanvasLayer { Name = "UILayer" }; scene.AddChild(ui);
            ui.AddChild(GD.Load<PackedScene>("res://tscn/panel/CombatSkillBar.tscn").Instantiate());
            var manager = new BattleManager { Name = "BattleMgr", deploy_layer = "../DeployLayer", isolated_trial_battle = true };
            scene.AddChild(manager);
            var character = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            var playerData = Unit("自动验收主角", 1, true, true);
            var foeAData = Unit("自动验收对手甲", 2, false, true);
            var foeBData = Unit("自动验收对手乙", 2, false, false);
            manager.setup_team_battle([character], [character, character], [playerData], [foeAData, foeBData]);
            await Until(() => manager.can_player_plan(), "默认进入手动规划");
            await Frames(3);
            var player = (Character)manager.get_main_player_unit()!;
            var foes = manager.enemy_units.OfType<Character>().ToArray();
            var overlay = manager.GetChildren().OfType<BattleUnitOverlay>().Single();
            var button = overlay.FindChild("AutoBattleButton", true, false) as Button ?? throw new InvalidOperationException("缺少自动战斗按钮");
            Check(button.Text == "自动：关" && !manager.auto_battle, "自动开关默认关闭");
            var otherButtons = overlay.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b != button).ToArray();
            Check(otherButtons.Where(b => b.Text.StartsWith("倍速") || b.Text is "暂停" or "撤退")
                .All(b => !b.GetGlobalRect().Intersects(button.GetGlobalRect())), "自动按钮不遮挡原有操作");
            await Capture("manual_1672");
            GetWindow().Size = new Vector2I(1280, 720); await Frames(3);
            Check(otherButtons.Where(b => b.Text.StartsWith("倍速") || b.Text is "暂停" or "撤退")
                .All(b => !b.GetGlobalRect().Intersects(button.GetGlobalRect())), "小窗口下自动按钮不遮挡原有操作");
            await Capture("manual_1280");
            GetWindow().Size = new Vector2I(1672, 941); await Frames(3);
            Check(manager.get_battle_unit(foes[0]).skill_queue.Any(a => a.kind == BattleQueuedSkill.ActionKind.Pill), "其他角色仍可使用自己的战备");
            // 消除敌方服丹带来的生命变化，便于单独核对主角的实际伤害。
            foes[0].current_health = foes[0].max_health;
            foes[0].battle_supplies[0].count = 0;

            var loadouts = (System.Collections.Generic.Dictionary<GodotObject, Godot.Collections.Array<ActiveSkillData>>)
                typeof(BattleManager).GetField("_combatSkills", Private)!.GetValue(manager)!;
            manager.on_skill_selected(loadouts[player][0], 0);
            manager.confirm_player_queue();
            Check(manager.is_selecting_queue_targets(), "多个敌人时手动队列等待选目标");
            var manual = manager.get_battle_unit(player).skill_queue[0];
            manager.toggle_battle_pause();
            await Click(button);
            await Frames(5);
            Check(manager.auto_battle && button.Text == "自动：开" && !manager.is_selecting_queue_targets(), "目标选择中开启自动并清理目标选择");
            Check(manager.is_battle_paused() && manager.is_planning_phase() && manager.get_battle_unit(player).skill_queue[0] == manual,
                "暂停时自动不规划、不替换队列");
            var undo = typeof(BattleManager).GetMethod("UndoQueuePlanning", Private)!;
            Check(!(bool)undo.Invoke(manager, null)!, "自动待机时撤销不会改动手动规划记录");
            button.EmitSignal(BaseButton.SignalName.Pressed);
            Check(manager.can_player_plan() && manager.get_battle_unit(player).skill_queue[0] == manual, "暂停时关闭自动可继续编辑原队列");
            Check((bool)undo.Invoke(manager, null)! && manager.get_filled_action_slot_count(player) == 0, "关闭自动后仍可撤销之前的手动行动");
            manager.on_skill_selected(loadouts[player][0], 0);
            manual = manager.get_battle_unit(player).skill_queue[0];
            button.EmitSignal(BaseButton.SignalName.Pressed);
            manager.set_battle_speed(8);
            manager.toggle_battle_pause();
            await Until(() => manager.get_segment_phase() == BattleManager.SegmentPhase.Executing, "恢复后自动确认队列");
            var queue = manager.get_battle_unit(player).skill_queue.ToArray();
            Check(queue.Length == manager.get_action_slot_capacity(player) && queue[0] != manual, "自动替换未确认队列并保留完整行动位");
            var first = queue.First(a => a.kind == BattleQueuedSkill.ActionKind.Skill);
            Check(foes.Contains(first.target) && first.skill.skill_name == "应敌", "自动使用已装配技能并选择合法敌人");
            Check(queue.All(a => a.kind is not (BattleQueuedSkill.ActionKind.Pill or BattleQueuedSkill.ActionKind.Retreat)), "主角自动队列不服丹、不撤退");
            var stamina = player.current_stamina;
            var totalHealth = foes.Sum(f => f.current_health);
            await Until(() => first.completed, "自动技能真实结算完成");
            Check(player.current_stamina == stamina - first.execution_skill.energy_cost, "自动技能按原费用扣除体力");
            Check(foes.Sum(f => f.current_health) < totalHealth, "自动技能对敌方造成实际伤害");
            Check(player.get_inventory_total_count(Heal) == 1, "自动战斗不扣主角丹药");
            await Until(() => Round(manager) >= 2 && manager.get_segment_phase() == BattleManager.SegmentPhase.Executing, "下一回合自动继续规划并执行");
            var locked = manager.get_battle_unit(player).skill_queue.ToArray();
            button.EmitSignal(BaseButton.SignalName.Pressed);
            Check(!manager.auto_battle && locked.SequenceEqual(manager.get_battle_unit(player).skill_queue), "行动中关闭自动保留已锁定队列");
            await Until(() => Round(manager) >= 3 && manager.can_player_plan(), "当前队列结束后恢复手动规划");
            Check(manager.queue_rest_action(), "接管后可手动编排休整");
            manager.confirm_player_queue();
            var manualLocked = manager.get_battle_unit(player).skill_queue.ToArray();
            button.EmitSignal(BaseButton.SignalName.Pressed);
            Check(manager.auto_battle && manualLocked.SequenceEqual(manager.get_battle_unit(player).skill_queue), "行动中开启自动保留已确认的手动队列");
            await Until(() => Round(manager) >= 4 && manager.get_segment_phase() == BattleManager.SegmentPhase.Executing, "行动中开启自动后下一轮生效");
            manager.setup_team_battle([character], [character, character], [playerData], [foeAData, foeBData]);
            Check(!manager.auto_battle && button.Text == "自动：关", "重新开始战斗时默认恢复手动");
            await Frames(3);
            var shortFoe = Unit("自动胜利验收对手", 2, false, false);
            shortFoe.max_health = shortFoe.current_health = 5;
            manager.setup_team_battle([character], [character], [playerData], [shortFoe]);
            await Until(() => manager.can_player_plan(), "新战斗可以手动规划");
            manager.toggle_auto_battle();
            await Until(() => manager.HasMeta("isolated_trial_finished") && manager.GetMeta("isolated_trial_finished").AsBool(), "自动战斗完成至胜负结算");
            Check(!manager.battle_running && ((Creature)manager.enemy_units[0]).current_health == 0, "击败敌人后按正常规则结束战斗");
            scene.QueueFree(); await Frames(3);
            await VerifyFormalBattle(character, playerData, shortFoe);
            GD.Print($"AUTO_BATTLE_SMOKE_OK {_checks}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("AUTO_BATTLE_SMOKE_FAIL " + error); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private async Task VerifyFormalBattle(PackedScene character, BattleEntityData player, BattleEntityData enemy)
    {
        Check(!GetNode<GameSaveManager>("/root/GameSaveManager").has_active_slot, "正式战场测试不打开玩家存档");
        var world = new Node2D { Name = "AutoBattleTestWorld" };
        GetTree().Root.AddChild(world); GetTree().CurrentScene = world;
        var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
        transition.set_battle_return_context(new Godot.Collections.Dictionary { ["is_training"] = true, ["title"] = "自动战斗验收", ["allow_prebattle_cancel"] = true });
        transition.start_team_battle([character], [character], [player], [enemy], "res://tscn/scene/StartMenu.tscn");
        await Until(() => transition.has_open_flow_view(), "正式战前页已打开");
        var flow = transition.GetChildren().OfType<BattleFlowView>().Single();
        flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "开始战斗")
            .EmitSignal(BaseButton.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "进入正式战场");
        var battle = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
        await Until(() => battle.can_player_plan(), "正式战场默认手动规划");
        var button = battle.GetChildren().OfType<BattleUnitOverlay>().Single().FindChild("AutoBattleButton", true, false) as Button
            ?? throw new InvalidOperationException("正式战场缺少自动按钮");
        battle.set_battle_speed(8);
        await Click(button);
        Check(battle.auto_battle, "正式战场可点击开启自动");
        await Until(() => (bool)typeof(BattleTransitionManager).GetField("_awaitingSettlement", Private)!.GetValue(transition)!
            && !transition.is_flow_transition_active() && transition.has_open_flow_view(), "自动战斗进入正式结算");
        var summary = (Godot.Collections.Dictionary)typeof(BattleTransitionManager).GetField("last_summary", Private)!.GetValue(transition)!;
        Check(summary.GetValueOrDefault("player_victory", false).AsBool(), "正式结算记录自动战斗胜利");
    }
}
