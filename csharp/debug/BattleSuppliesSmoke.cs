using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

/// <summary>真实战前页→战备选择→队列→服丹→库存回传的专项检查。</summary>
public partial class BattleSuppliesSmoke : Node
{
    private int _checks;
    private const string Heal = "res://resources/entities/items/alchemy/huichun_1.tres";
    private const string Mana = "res://resources/entities/items/alchemy/tongmai_0.tres";
    private const string Folder = "res://docs/美术规范/局内战备按钮与展开面板_20261025/";
    private void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        _checks++; GD.Print("SUPPLY_CHECK: " + description);
    }
    private Button Named(string name) => GetTree().Root.FindChildren(name, "Button", true, false).OfType<Button>().First();
    private IEnumerable<Button> Buttons(string text) => GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.Text == text);
    private async System.Threading.Tasks.Task Frames(int count = 2)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async System.Threading.Tasks.Task Until(Func<bool> condition, string description, int frames = 2400)
    {
        for (var i = 0; i < frames; i++) { if (condition()) return; await Frames(1); }
        throw new Exception("等待超时：" + description);
    }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(Folder + name + ".png");
    }
    private Character CreateOwner()
    {
        var actor = new Character { entity_data = new HumanData() }; AddChild(actor);
        var human = (HumanData)actor.entity_data!;
        human.inventory_item_paths = [Heal, Mana]; human.inventory_item_counts = [8, 3]; human.inventory_item_positions = [4, 9];
        human.battle_supplies = [];
        actor.max_health = 100; actor.current_health = 40; actor.max_mana = 40; actor.current_mana = 0;
        return actor;
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            await Frames();
            var original = CreateOwner();
            var snapshot = original.get_snapshot_data();
            Check(snapshot.pack_state_included && snapshot.pack_item_positions.SequenceEqual(new[] { 4, 9 }), "快照携带真实库存及原格位");
            Check(BattleSupplyRules.Set(snapshot, 0, Heal, 99) && snapshot.battle_supplies[0].count == 5, "单战备位最多5颗");
            Check(BattleSupplyRules.Set(snapshot, 1, Heal, 5) && snapshot.battle_supplies[1].count == 3, "重复丹药位不能超额预留本人库存");
            Check(!BattleSupplyRules.Set(snapshot, 2, Heal, 5), "库存全部被预留后不能凭空再装一堆");
            Check(!BattleSupplyRules.Set(snapshot, 2, "res://resources/entities/items/alchemy/jingxin_0.tres", 1), "世界静心丹不进入战斗战备");
            var copy = (BattleEntityData)snapshot.Duplicate(true); copy.battle_supplies[0].count--;
            Check(snapshot.battle_supplies[0].count == 5, "战备深复制不共享可变数量");
            snapshot.battle_supplies = [];
            snapshot.uid = "supply-smoke-player"; snapshot.entity_name = "战备试炼修士"; snapshot.faction = 1;
            snapshot.cultivation_realm = "云息三层"; snapshot.test_mana_cap_override = 40;
            snapshot.combat_skill_slots_configured = true; snapshot.combat_skill_slot_paths = []; snapshot.active_skill_paths = [];
            var enemy = new BattleEntityData { uid = "supply-smoke-enemy", entity_name = "战备试炼对手", faction = 2,
                current_health = 1000, max_health = 1000, physique = 10, intelligence = 10, soul = 10,
                cultivation_realm = "云息三层", initiative_speed = 1, test_mana_cap_override = 40,
                pack_state_included = true, combat_skill_slots_configured = true };
            var world = new Node2D { Name = "SupplySmokeWorld" }; GetTree().Root.AddChild(world); GetTree().CurrentScene = world;
            var transition = GetTree().Root.GetNode<BattleTransitionManager>("BattleTransitionManager");
            transition.set_battle_return_context(new Godot.Collections.Dictionary { ["is_training"] = true, ["title"] = "战备专项演练", ["allow_prebattle_cancel"] = true });
            var characterScene = GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            transition.start_team_battle(new Godot.Collections.Array<PackedScene> { characterScene }, new Godot.Collections.Array<PackedScene> { characterScene }, new GArray { snapshot }, new GArray { enemy }, "res://tscn/scene/StartMenu.tscn");
            await Frames(); Buttons("配置战备").Single().EmitSignal(Button.SignalName.Pressed); await Frames();
            GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text.StartsWith("回春丹 · 良效")).EmitSignal(Button.SignalName.Pressed); await Frames();
            Buttons("选择此位").ElementAt(1).EmitSignal(Button.SignalName.Pressed); await Frames();
            GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text.StartsWith("回春丹 · 良效")).EmitSignal(Button.SignalName.Pressed); await Frames();
            Buttons("选择此位").ElementAt(2).EmitSignal(Button.SignalName.Pressed); await Frames();
            GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text.StartsWith("通脉丹 · 薄效")).EmitSignal(Button.SignalName.Pressed); await Frames();
            await Capture("实装_战前配置");
            Buttons("完成战备配置").Single().EmitSignal(Button.SignalName.Pressed); await Frames();
            Buttons("开始战斗").Single().EmitSignal(Button.SignalName.Pressed);
            BattleManager? manager = null;
            await Until(() => { manager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager; return manager?.battle_running == true && manager.can_player_plan(); }, "进入真实战斗规划");
            await Until(() => Named("BattleSuppliesToggle").IsVisibleInTree(), "入场过渡完成并显示战备入口");
            var battle = manager!;
            var player = (Character)battle.get_main_player_unit()!;
            player.current_health = 40; player.max_health = 100; player.current_mana = 0; player.max_mana = 40;
            Check(player.battle_supplies.Count == 3 && player.battle_supplies.Select(s => s.count).SequenceEqual(new[] { 5, 3, 3 }), "战前UI配置完整进入本人战斗快照");
            Check(player.get_inventory_total_count(Heal) == 8 && original.get_inventory_total_count(Heal) == 8, "战前配置与入场均未扣除丹药");
            Named("BattleSuppliesToggle").EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(Named("BattleSupplySlot1").IsVisibleInTree(), "点击正式战备入口向上展开");
            var bar = GetTree().CurrentScene.GetNode<Control>("UILayer/CombatSkillBar/BarArtwork");
            Check(Named("BattleSuppliesToggle").GetGlobalRect().End.X < bar.GetGlobalRect().Position.X, "战备入口不与技能栏重叠");
            var hud = (BattleSupplyHud)GetTree().CurrentScene.GetNode("BattleSuppliesLayer");
            Check(hud.Layer == 20 && GetTree().Root.GetNode<HoverInfoManager>("HoverInfoManager").Layer == 160, "战备及悬停层级高于常规HUD");
            for (var i = 0; i < 5; i++) Check(battle.queue_pill_action(0), "库存范围内逐颗排入独立行动位");
            Check(!battle.queue_pill_action(0), "不允许超过战备堆／行动位容量排队");
            Check(player.get_inventory_total_count(Heal) == 8, "仅排队不扣库存");
            Check(battle.cancel_player_action(1) && battle.get_available_supply_count(player, 0) == 1, "取消预留立即返还可排次数");
            Check(battle.reorder_player_action(0, 1) && battle.get_battle_unit(player).skill_queue[0].kind == BattleQueuedSkill.ActionKind.Empty, "服丹拖动保留原空位");
            for (var i = 0; i < 5; i++) battle.cancel_player_action(i);
            Check(battle.queue_pill_action(0) && battle.queue_pill_action(0) && battle.queue_pill_action(2) && battle.queue_pill_action(0), "服丹与通脉按第一个空位编排");
            var actions = battle.get_battle_unit(player).skill_queue.ToArray();
            Check(actions.Take(4).All(a => a.kind == BattleQueuedSkill.ActionKind.Pill && a.target == player), "服丹自动以本人为目标");
            var slot = Named("BattleSupplySlot1");
            var mouse = new InputEventMouseMotion { Position = slot.GetGlobalRect().GetCenter() }; mouse.GlobalPosition = mouse.Position;
            Input.ParseInputEvent(mouse); slot.EmitSignal(Control.SignalName.MouseEntered);
            await ToSignal(GetTree().CreateTimer(.2, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            await Capture("实装_排入队列");
            Check(GetTree().Root.GetNode<HoverInfoManager>("HoverInfoManager").GetNode<Control>("SharedHoverInfoCard").Visible, "正式悬停说明可见");
            player.initiative_speed = 10000;
            foreach (var unit in battle.enemy_units) unit.Set("initiative_speed", 1);
            battle.confirm_player_queue();
            await Until(() => actions[2].completed, "实际队列结算回血与回蓝");
            Check(player.current_health == 76 && player.current_mana == 2, "既有良效回血18与薄效回蓝实际生效");
            Check(player.get_inventory_total_count(Heal) == 6 && player.get_inventory_total_count(Mana) == 2, "仅成功结算扣除2颗回春与1颗通脉");
            Check(player.battle_supplies[0].count == 3 && player.battle_supplies[2].count == 2, "战备堆与真实行囊数量同步");
            Check(Math.Abs(player.PillToxicity - 35) < .001, "实际服丹按丹种及档位累积丹毒");
            Check(original.get_inventory_total_count(Heal) == 8, "战斗克隆不直接修改世界角色");
            player.current_health = player.max_health;
            await Until(() => actions[3].completed, "满生命服丹跳过");
            Check(actions[3].skipped && player.get_inventory_total_count(Heal) == 6 && Math.Abs(player.PillToxicity - 35) < .001, "无收益跳过不扣药不加毒");
            Check(!hud.Expanded && !Named("BattleSuppliesToggle").IsVisibleInTree(), "确认行动后自动关闭战备入口");
            await Capture("实装_执行后");
            var result = player.get_snapshot_data(); original.apply_battle_data(result);
            Check(original.get_inventory_total_count(Heal) == 6 && original.get_inventory_total_count(Mana) == 2, "战末库存可回传真实角色");
            Check(((HumanData)original.entity_data!).inventory_item_positions.SequenceEqual(new[] { 4, 9 }), "结算保留行囊原格位");
            Check(Math.Abs(original.PillToxicity - 35) < .001, "丹毒随战末角色状态回传");
            var resourcePath = "user://battle_supply_smoke_" + Guid.NewGuid().ToString("N") + ".tres";
            Check(ResourceSaver.Save(original.entity_data, resourcePath) == Error.Ok, "战备与库存可保存");
            var loaded = GD.Load<HumanData>(resourcePath)!;
            Check(loaded.battle_supplies[0].count == 3 && loaded.inventory_item_counts[0] == 6, "保存后数量仍一致");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(resourcePath));
            GD.Print($"BATTLE_SUPPLIES_OK: {_checks} checks"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("BATTLE_SUPPLIES_FAILED: " + error); GetTree().Quit(1); }
    }
}
