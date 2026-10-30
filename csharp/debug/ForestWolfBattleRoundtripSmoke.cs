using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using D = Godot.Collections.Dictionary;

/// <summary>Real scene transitions and queued battle retreat. No player save slot is opened.</summary>
public partial class ForestWolfBattleRoundtripSmoke : Node
{
    private int _checks;
    private static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; GD.Print("FOREST_ROUNDTRIP_CHECK: " + message); }
    private async Task Frames(int n = 5) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> done, string message, int frames = 3600)
    { for (var i = 0; i < frames; i++) { if (done()) return; await Frames(1); } throw new Exception(message); }
    private EastWoodsDungeonPanel Panel => ((MainScene)GetTree().CurrentScene).GetNode<EastWoodsDungeonPanel>("SceneLayer/EastWoodsDungeonPanel");
    private void Choose(string name) => Panel.GetNode<ForestWolfEventView>("NodeActionDetails/ForestWolfEvent")
        .FindChildren("Choice_" + name, "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
    private void OpenEvent() => Panel.GetNode<Control>("CurrentNodeActions").FindChildren("Action_wolf", "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = "res://docs/美术规范/林中遇狼实装_20261027";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        GetViewport().GetTexture().GetImage().SavePng(folder + "/" + name + ".png");
    }
    private async Task EnterBattle(BattleTransitionManager transition)
    {
        Choose("fight"); await Frames();
        Check(transition.has_open_flow_view(), "迎战打开实际战前页");
        var flow = transition.GetChildren().OfType<BattleFlowView>().Single();
        flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "开始战斗").EmitSignal(Button.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene?.SceneFilePath == "res://tscn/battle/BattleScene.tscn" && !transition.is_flow_transition_active(), "未进入正式战场");
        Check(GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr").battle_running, "真实战场已运行");
    }
    private async Task ReturnFromSettlement(BattleTransitionManager transition)
    {
        await Until(() => Read<bool>(transition, "_awaitingSettlement") && !transition.is_flow_transition_active() && transition.has_open_flow_view(), "未进入正式结算");
        var flow = transition.GetChildren().OfType<BattleFlowView>().Single();
        flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text is "返回" or "返回并领取").EmitSignal(Button.SignalName.Pressed);
        await Until(() => GetTree().CurrentScene is MainScene && !transition.is_flow_transition_active(), "未返回主场景");
        await Frames(12);
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        string? temp = null;
        try
        {
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(!saves.has_active_slot, "专项不打开正式玩家存档槽");
            EastWoodsDungeonRules.generate_layout("forest-roundtrip");
            var route = new D
            {
                ["layout"] = EastWoodsDungeonRules.get_layout_state(), ["inside"] = true, ["current"] = "root_hollow",
                ["actions"] = new D(), ["visited"] = new Godot.Collections.Array<string> { "forest_edge", "root_hollow" },
            };
            var fixture = new PersistentGameSave
            {
                player_data = new HumanData
                {
                    entity_name = "遇狼场景验收", cultivation_realm = "云息三层", current_world_stamina = 73, max_world_stamina = 100,
                    has_spirit_core = true, data_initialized = true, physique = 10, soul = 10, intelligence = 10,
                    base_physique = 10, base_soul = 10, base_intelligence = 10, current_health = 10000, max_health = 10000,
                },
                current_node_id = "east_woods", world_cell = new Vector2I(34, 166), year = 2, month = 4, day = 8, hour = 9,
                npc_population_version = CloudStateNpcCatalog.PopulationVersion, npc_simulation_enabled = false,
                scene_progress = new D { ["qingxi_prologue"] = 3, ["cloud_state_initialized"] = true, ["xuanjian_awakened"] = true, ["east_woods_dungeon_state"] = route },
            };
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saves, fixture);
            GetTree().CurrentScene = null;
            Check(GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn") == Error.Ok, "读取真实主场景");
            await Until(() => GetTree().CurrentScene is MainScene, "主场景未创建"); await Frames(15);
            OpenEvent(); Choose("observe");
            var record = Panel.forest_record("root_hollow:initial");
            var observation = record["observation"].AsString(); var wolf = ForestWolfEvent.Wolf(record)!;
            var physique = wolf.physique; var soul = wolf.soul;
            var player = ((MainScene)GetTree().CurrentScene).get_player_character()!;
            var uid = player.unique_id; var stamina = player.current_world_stamina;
            var preciseTime = GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours();
            var snapshot = ((MainScene)GetTree().CurrentScene).capture_battle_world_snapshot()!;
            temp = "user://forest_roundtrip_" + Guid.NewGuid().ToString("N") + ".tres";
            Check(ResourceSaver.Save(snapshot, temp) == Error.Ok, "完整进度含事件窗口可保存");
            var loaded = ResourceLoader.Load<PersistentGameSave>(temp, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            typeof(GameSaveManager).GetField("_pendingLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saves, loaded);
            GetTree().ChangeSceneToFile("res://tscn/scene/MainScene.tscn"); await Frames(20);
            Check(Panel.has_open_forest_event && Panel.forest_record("root_hollow:initial")["observation"].AsString() == observation, "正式场景重新读取后恢复观察续页");
            await Capture("06_正式存读档续页");
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            await EnterBattle(transition);
            var manager = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
            var enemy = (Creature)manager.enemy_units.Single();
            Check(enemy.physique == physique && enemy.soul == soul, "正式战斗个体与事件判定一致");
            var attempts = 0;
            while (!Read<bool>(transition, "_awaitingSettlement") && attempts < 12)
            {
                await Until(() => manager.can_queue_retreat() || Read<bool>(transition, "_awaitingSettlement"), "未进入可撤退规划阶段");
                if (Read<bool>(transition, "_awaitingSettlement")) break;
                manager.queue_player_retreat();
                var own = Read<GodotObject>(manager, "_mainPlayerUnit");
                Check(manager.get_battle_unit(own).skill_queue.Any(a => a.kind == BattleQueuedSkill.ActionKind.Retreat), "撤退进入真实行动队列");
                manager.confirm_player_queue(); attempts++;
                await Frames(5);
                await Until(() => manager.can_queue_retreat() || Read<bool>(transition, "_awaitingSettlement"), "撤退行动未完成");
            }
            Check(Read<D>(transition, "last_summary").GetValueOrDefault("escaped", false).AsBool(), "真实排队撤退成功，非注入撤离结果");
            await Capture("07_战斗撤退结算");
            await ReturnFromSettlement(transition);
            record = Panel.forest_record("root_hollow:initial");
            Check(record["stage"].AsString() == "battle_escape" && Panel.has_open_forest_event, "实际战场撤退后返回事件后续页");
            Check(Panel.available_actions("root_hollow").Any(a => a.Battle), "撤退未清除凶狼");
            player = ((MainScene)GetTree().CurrentScene).get_player_character()!;
            Check(player.unique_id == uid && player.current_world_stamina == stamina && Math.Abs(GetNode<GameTimeMgr>("/root/GameTimeManager").get_precise_game_hours() - preciseTime) < .001,
                "场景往返保留角色身份、事件精力及历法");
            Check(record["observation"].AsString() == observation, "战后观察判定未被重置");
            await Capture("08_战斗撤退后续");
            Choose("continue"); OpenEvent();
            await EnterBattle(transition);
            manager = GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
            // The victory transport is tested with a defeated enemy fixture; escape above executes the real queue.
            foreach (var unit in manager.enemy_units) ((Creature)unit).current_health = 0;
            Call(manager, "CheckBattleOver");
            await ReturnFromSettlement(transition);
            record = Panel.forest_record("root_hollow:initial");
            Check(record["stage"].AsString() == "battle_win" && !Panel.available_actions("root_hollow").Any(a => a.Battle), "实际胜利结算返回并清除遭遇");
            Check(!transition.has_pending_battle_return_data(), "正式回传仅消费一次");
            await Capture("09_胜利后续");
            GD.Print($"FOREST_WOLF_BATTLE_ROUNDTRIP_PASS checks={_checks}: native save/reload, actual scene entry, queued retreat, return episode, victory transport with defeated fixture, no player-slot writes");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { if (temp is not null) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(temp)); }
    }
}
