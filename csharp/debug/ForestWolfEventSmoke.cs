using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Globalization;
using D = Godot.Collections.Dictionary;
using A = Godot.Collections.Array;

public partial class ForestWolfEventSmoke : Node
{
    private int _checks;
    private EastWoodsActionHarness _main = null!;
    private EastWoodsDungeonPanel _panel = null!;
    private Character _player = null!;
    private static Type DeclaringType(object target) => target is MainScene ? typeof(MainScene) : target.GetType();
    private static object? Call(object target, string method, params object[] args)
        => DeclaringType(target).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static void Set(object target, string field, object value)
        => DeclaringType(target).GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
    private static T Read<T>(object target, string field)
        => (T)DeclaringType(target).GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target)!;
    private void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); _checks++; GD.Print("FOREST_EVENT_CHECK: " + message); }
    private async Task Frames(int n = 4) { for (var i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private ForestWolfEventView View => _panel.GetNode<ForestWolfEventView>("NodeActionDetails/ForestWolfEvent");
    private void Select(string id) => View.FindChildren("Choice_" + id, "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
    private void Open() => _panel.GetNode<Control>("CurrentNodeActions").FindChildren("Action_wolf", "Button", true, false).OfType<Button>().Single().EmitSignal(Button.SignalName.Pressed);
    private D Record => _panel.forest_record("root_hollow:initial");
    private void Reset(D baseline)
    {
        _panel.close_action_details(); _panel.restore_route_state(baseline.Duplicate(true)); _panel.show_dungeon();
        _player.current_world_stamina = 100; Open();
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = "res://docs/美术规范/林中遇狼实装_20261027";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        GetViewport().GetTexture().GetImage().SavePng(folder + "/" + name + ".png");
    }
    private void CheckHintSources(D original, BattleEntityData input)
    {
        var previousMode = PlayerInfoText.Mode;
        try
        {
            foreach (var node in new[] { "root_hollow", "outer_08_08", "outer_13_06" })
            foreach (var observation in new[] { "", "success", "failure" })
            {
                var record = original.Duplicate(true);
                record["node"] = node; record["observation"] = observation;
                record["stage"] = observation.Length == 0 ? "opening" : "observe_" + observation;
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
                var fromSimple = ForestWolfEvent.Describe(record, input);
                PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
                var fromDetailed = ForestWolfEvent.Describe(record, input);
                Check(fromSimple.Choices.Select(choice => (choice.Id, choice.Hint))
                    .SequenceEqual(fromDetailed.Choices.Select(choice => (choice.Id, choice.Hint))),
                    node + "生成完整提示不依赖显示档位：" + observation);
                foreach (var choice in fromSimple.Choices.Where(choice => choice.Id is "observe" or "drive"))
                {
                    var simple = PlayerInfoText.Summary(choice.Hint);
                    var detailed = PlayerInfoText.Detailed(choice.Hint);
                    Check(simple.Contains("耗时0.5小时 · 精力1") && !simple.Contains("计算来源") && !simple.Contains("成功率"),
                        node + choice.Id + "简略保留代价风险且不显示判定来源");
                    Check(PlayerInfoText.CalculationSources(choice.Hint).Count == 3
                        && detailed.Contains("人物评分：") && detailed.Contains("凶狼评分：")
                        && detailed.Contains("神魂[" + input.soul.ToString("0.##", CultureInfo.InvariantCulture) + "]")
                        && detailed.Split("计算来源", StringSplitOptions.None).Length == 2,
                        node + choice.Id + "详细恢复三项真实代入且只有一个末尾来源块");
                    var percent = Math.Round(ForestWolfEvent.Chance(record, choice.Id, input) * 100,
                        MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
                    Check(detailed.Contains("成功率：" + percent + "% = min(95%,max(5%,")
                        && PlayerInfoText.Detailed(detailed) == detailed,
                        node + choice.Id + "显示成功率沿真实判定且重复格式化不变");
                    if (choice.Id == "drive" && observation.Length > 0)
                        Check(detailed.Contains(observation == "success" ? "×100%＋15%))" : "×100%－10%))"),
                            node + "观察修正在最终上下限内计算");
                }
            }
        }
        finally { PlayerInfoText.SetMode(previousMode, persist: false); }
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        string? temp = null;
        try
        {
            GetNode<NpcWorldManager>("/root/NpcWorldManager").set_simulation_enabled(false);
            GetNode<NpcWorldManager>("/root/NpcWorldManager").npcs.Clear();
            EastWoodsDungeonRules.generate_layout("forest-event-runtime");
            _main = new EastWoodsActionHarness(); AddChild(_main); _main.AddChild(new CanvasLayer { Name = "SceneLayer" });
            _player = new Character { entity_data = new HumanData { entity_name = "遇狼验收", current_world_stamina = 100, max_world_stamina = 100 } };
            _main.AddChild(_player); Set(_main, "player", _player);
            _player.physique = _player.soul = _player.intelligence = 10;
            _panel = (EastWoodsDungeonPanel)Call(_main, "get_or_create_east_woods_dungeon")!;
            var state = _panel.get_route_state(); state["current"] = "root_path"; _panel.restore_route_state(state); _panel.show_dungeon(); await Frames();
            Check(_panel.begin_assigned_route("root_hollow"), "真实行走可接近首个遭遇");
            for (var i = 0; i < 180 && !_panel.has_open_forest_event; i++) await Frames(1);
            Check(_panel.has_open_forest_event && _panel.get_current_route_node_id() == "root_hollow", "落步后自动打开插画事件并停止路线");
            Check(View.FindChildren("Choice_*", "Button", true, false).Count == 4, "首次页面显示四项情境应对");
            Check(ForestWolfEvent.Wolf(Record) is { dun_speed: 100 }, "首次遭遇固定真实狼快照");
            var wolf = ForestWolfEvent.Wolf(Record)!;
            var baseline = _panel.get_route_state().Duplicate(true);
            CheckHintSources(Record, _player.get_snapshot_data());
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager");
            var hour = time.get_precise_game_hours(); var stamina = _player.current_world_stamina;
            await Capture("01_首次遭遇_1080");
            await Frames();
            Check(!Read<ScrollContainer>(View, "_storyScroll").GetVScrollBar().Visible, "1080画布短旁白完整显示");
            Select("retreat");
            Check(Record["stage"].AsString() == "retreated" && time.get_precise_game_hours() == hour && _player.current_world_stamina == stamina, "战前退开只展示收束页，不判定或扣费");
            Call(_main, "on_forest_wolf_choice", "root_hollow", "root_hollow:initial", "observe");
            Check(Record["stage"].AsString() == "retreated" && _player.current_world_stamina == stamina, "上一页迟到的选择不能跨页扣费");
            Select("continue");
            Check(!_panel.has_open_forest_event && _panel.get_current_route_node_id() == "root_hollow" && _panel.available_actions("root_hollow").Any(a => a.Battle), "退开留在原格且狼仍在");
            Open(); Check(ForestWolfEvent.Wolf(Record)!.physique == wolf.physique, "重开不重抽个体");
            Select("observe");
            Check(_player.current_world_stamina == stamina - 1 && Math.Abs(time.get_precise_game_hours() - hour - .5) < .001, "真实观察扣1精力并推进半小时");
            Check(Record["stage"].AsString().StartsWith("observe_"), "观察后进入成功或失败续页");
            Check((Record["observe_sample"].AsDouble() < Record["observe_probability"].AsDouble()) == (Record["observation"].AsString() == "success"), "实际随机样本和属性概率决定结果");
            var sample = Record["observe_sample"].AsDouble(); var costAfter = _player.current_world_stamina;
            Call(_main, "on_forest_wolf_choice", "root_hollow", "root_hollow:initial", "observe");
            Check(_player.current_world_stamina == costAfter && Record["observe_sample"].AsDouble() == sample, "重复观察不扣费不重抽");
            await Capture("02_观察后续_1080");
            _panel.close_action_details(); Open();
            Check(Record["observe_sample"].AsDouble() == sample, "关闭重开保留结果");
            var save = new PersistentGameSave { scene_progress = new D { ["east_woods_dungeon_state"] = _panel.get_route_state() } };
            var detached = (PersistentGameSave)DetachedResourceSnapshot.Capture(save).Materialize();
            var detachedRecord = ForestWolfEvent.Get(detached.scene_progress["east_woods_dungeon_state"].AsGodotDictionary()["actions"].AsGodotDictionary(), "root_hollow:initial");
            Check(ForestWolfEvent.Wolf(detachedRecord)!.physique == wolf.physique && detachedRecord["observation"].AsString() == Record["observation"].AsString(), "异步存档快照链保留狼与判定记录");
            temp = "user://forest_event_" + Guid.NewGuid().ToString("N") + ".tres";
            Check(ResourceSaver.Save(detached, temp) == Error.Ok, "事件和嵌套狼快照可写入正式Resource格式");
            var loaded = ResourceLoader.Load<PersistentGameSave>(temp, cacheMode: ResourceLoader.CacheMode.Ignore)!;
            var saved = loaded.scene_progress["east_woods_dungeon_state"].AsGodotDictionary();
            _panel.close_action_details(); _panel.restore_route_state(saved); _panel.show_dungeon(); Open();
            Check(Math.Abs(Record["observe_sample"].AsDouble() - sample) < .000001 && Math.Abs(ForestWolfEvent.Wolf(Record)!.physique - wolf.physique) < .00001,
                $"存读档保留判定与个体快照：样本{sample:R}→{Record["observe_sample"].AsDouble():R}，体魄{wolf.physique}→{ForestWolfEvent.Wolf(Record)!.physique}");
            Reset(baseline); _player.current_world_stamina = 0; hour = time.get_precise_game_hours(); Select("observe");
            Check(Record["observation"].AsString() == "" && time.get_precise_game_hours() == hour, "精力不足无判定无扣时");
            Reset(baseline);
            var input = _player.get_snapshot_data();
            foreach (var draw in new[] { 0d, .999999d })
            {
                var fresh = ForestWolfEvent.Get(baseline["actions"].AsGodotDictionary(), "root_hollow:initial");
                var record = fresh.Duplicate(true); Check(ForestWolfEvent.Resolve(record, "observe", input, draw), "观察成功失败边界均可结算");
                var unobserved = ForestWolfEvent.Chance(fresh, "drive", input);
                var changed = ForestWolfEvent.Chance(record, "drive", input);
                Check(Math.Abs(changed - unobserved - (draw == 0 ? .15 : -.10)) < .00001, "观察结果只改变后续驱赶概率");
                _panel.show_forest_event(record, input);
                Check(!View.FindChildren("Choice_observe", "Button", true, false).Any(), "续页移除已用观察机会");
            }
            var low = input.Duplicate(true) as BattleEntityData; low!.physique = low.soul = low.intelligence = 0;
            Check(ForestWolfEvent.Chance(Record, "observe", low) == .05, "零属性仍能尝试，无硬性门槛");
            bool sawSuccess = false, sawFailure = false;
            for (ulong seed = 1; seed <= 30 && !(sawSuccess && sawFailure); seed++)
            {
                Reset(baseline); hour = time.get_precise_game_hours(); GD.Seed(seed); Select("drive");
                Check(_player.current_world_stamina == 99 && Math.Abs(time.get_precise_game_hours() - hour - .5) < .001, "真实驱赶无论结果均结算一次费用");
                if (Record["drive"].AsString() == "success")
                {
                    sawSuccess = true;
                    Check(!_panel.available_actions("root_hollow").Any(a => a.Battle), "驱赶成功从活跃遭遇移除");
                    var actions = _panel.get_route_state()["actions"].AsGodotDictionary();
                    Check(actions.GetValueOrDefault("root_hollow:wolf", 0).AsInt32() == 0 && Record["outcome"].AsString() == "driven_off", "驱离未冒充战斗胜利");
                    Check(_player.get_inventory_total_count("res://resources/entities/items/兽骨.tres") == 0, "驱赶不发击杀奖励");
                    var route = _panel.get_route_state();
                    EastWoodsEcology.Synchronize(route, 0, "root_hollow"); EastWoodsEcology.Synchronize(route, 14 * 24, "root_hollow");
                    Check(EastWoodsEcology.AvailableRegeneratedBeasts("root_hollow", route["actions"].AsGodotDictionary()).Count == 0, "站在原格不刷狼");
                    EastWoodsEcology.Synchronize(route, 14 * 24, "forest_edge");
                    Check(EastWoodsEcology.AvailableRegeneratedBeasts("root_hollow", route["actions"].AsGodotDictionary()).Count == 1, "驱离后生态按原周期补入新个体");
                    await Capture("03_驱离结局_1080");
                }
                else
                {
                    sawFailure = true;
                    Check(View.FindChildren("Choice_*", "Button", true, false).Count == 2 && _panel.available_actions("root_hollow").Any(a => a.Battle), "驱赶失败只余迎战或退开，狼仍活跃");
                    await Capture("04_驱赶失败_1080");
                }
            }
            Check(sawSuccess && sawFailure, "真实控制器覆盖驱赶两种结果");
            Reset(baseline); Select("observe"); var recordBeforeBattle = Record.Duplicate(true);
            Select("fight"); await Frames();
            var transition = GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            var pending = Read<A>(transition, "pending_defender_data");
            Check(transition.has_open_flow_view() && pending.Count == 1 && pending[0].AsGodotObject() is BattleEntityData frozen && frozen.physique == wolf.physique && frozen.soul == wolf.soul, "迎战进入正式战前且使用同一只狼");
            var flow = transition.GetChildren().OfType<BattleFlowView>().Single();
            flow.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "返回").EmitSignal(Button.SignalName.Pressed); await Frames();
            Check(!GetTree().Paused && _panel.has_open_forest_event && Record["observation"].AsString() == recordBeforeBattle["observation"].AsString(), "战前取消恢复原事件阶段，不重置判定");
            var context = new D { ["kind"] = "east_woods_beast", ["forest_event_key"] = "root_hollow:initial", ["route_state"] = _panel.get_route_state(), ["node"] = "root_hollow", ["action"] = "wolf" };
            _main.handle_persistent_battle_return(new(), new(), new D { ["escaped"] = true, ["battle_id"] = "escape-smoke" }, context);
            Check(Record["stage"].AsString() == "battle_escape" && _panel.available_actions("root_hollow").Any(a => a.Battle), "正式撤离回调进入对应续页，不清狼");
            context["route_state"] = _panel.get_route_state();
            _main.handle_persistent_battle_return(new(), new(), new D { ["player_victory"] = true, ["battle_id"] = "victory-smoke" }, context);
            Check(Record["stage"].AsString() == "battle_win" && !_panel.available_actions("root_hollow").Any(a => a.Battle), "胜利回调清狼并进入结局");
            _main.handle_persistent_battle_return(new(), new(), new D { ["player_victory"] = true, ["battle_id"] = "victory-smoke" }, context);
            Check(_player.get_inventory_total_count("res://resources/entities/items/兽骨.tres") == 0, "重复结果回调不在事件窗复制奖励");
            Reset(baseline); _panel.close_action_details();
            var previousWolf = ForestWolfEvent.Wolf(Record)!;
            foreach (var node in new[] { "outer_08_08", "outer_13_06" })
            {
                var route = _panel.get_route_state(); route["current"] = node;
                _panel.restore_route_state(route); _panel.show_dungeon(); await Frames(); Open(); await Frames();
                var key = node + ":initial";
                var episode = _panel.forest_record(key);
                var page = ForestWolfEvent.Describe(episode, _player.get_snapshot_data());
                Check(episode["node"].AsString() == node && page.Title == (node == "outer_08_08" ? "林径尾随" : "草间对峙"), "新增遭遇打开独立主题页：" + node);
                Check(View.FindChildren("Choice_observe", "Button", true, false).OfType<Button>().Single().Text == "›  " + (node == "outer_08_08" ? "辨听脚步" : "观察草动") + "（神魂、灵慧）" &&
                    View.FindChildren("Choice_drive", "Button", true, false).OfType<Button>().Single().Text == "›  " + (node == "outer_08_08" ? "反身喝止" : "踏步逼退") + "（体魄、神魂）",
                    "原选项名称后显示判定属性，多个依据使用顿号");
                Check(ForestWolfEvent.Wolf(episode)!.uid != previousWolf.uid, "不同事件使用不同的真实狼个体");
                if (node == "outer_13_06") Check(episode["lead_in"].AsString().Length > 0, "前一处真实结局衔接后续遭遇");
                await Capture(node == "outer_08_08" ? "11_林径尾随_属性提示" : "12_草间对峙_属性提示");
                var frame = View.GetNode<Control>("EventPaper").GetGlobalRect();
                var startedHour = time.get_precise_game_hours(); var startedStamina = _player.current_world_stamina;
                Select("observe"); await Frames(); episode = _panel.forest_record(key);
                Check(episode["observation"].AsString().Length > 0 && time.get_precise_game_hours() == startedHour + .5 &&
                    _player.current_world_stamina == startedStamina - 1, "新事件实际观察只扣一次费用");
                Check(View.GetNode<Control>("EventPaper").GetGlobalRect().IsEqualApprox(frame), "观察续页保持同一窗口尺寸和位置");
                Select("fight"); await Frames();
                Check(transition.has_open_flow_view(), "新事件迎战接入正式战前");
                transition.GetChildren().OfType<BattleFlowView>().Single().FindChildren("*", "Button", true, false)
                    .OfType<Button>().Single(b => b.Text == "返回").EmitSignal(Button.SignalName.Pressed); await Frames();
                Check(_panel.has_open_forest_event && _panel.forest_record(key)["observation"].AsString().Length > 0,
                    "新事件取消战前保留判定与现场");
                Select("retreat"); await Frames();
                Check(View.GetNode<Control>("EventPaper").GetGlobalRect().IsEqualApprox(frame), "退开续页保持同一窗口尺寸和位置");
                Select("continue"); Open(); Select("drive"); await Frames(); episode = _panel.forest_record(key);
                var cleared = episode["outcome"].AsString() == "driven_off";
                Check(cleared == !_panel.available_actions(node).Any(a => a.Battle), "新事件驱赶与该节点生态一致");
                if (!cleared)
                {
                    var resultContext = new D { ["kind"] = "east_woods_beast", ["node"] = node, ["action"] = "wolf",
                        ["forest_event_key"] = key, ["route_state"] = _panel.get_route_state() };
                    _main.handle_persistent_battle_return(new(), new(), new D { ["escaped"] = true, ["battle_id"] = node + "-escape" }, resultContext);
                    Check(_panel.forest_record(key)["stage"].AsString() == "battle_escape" && _panel.available_actions(node).Any(a => a.Battle),
                        "新节点撤离回传保留狼并恢复后续页");
                    resultContext["route_state"] = _panel.get_route_state();
                    _main.handle_persistent_battle_return(new(), new(), new D { ["player_victory"] = true, ["battle_id"] = node + "-win" }, resultContext);
                    Check(!_panel.available_actions(node).Any(a => a.Battle), "新节点胜利回传清除正确节点");
                }
                Check(_panel.forest_record("root_hollow:initial")["outcome"].AsString() == "active", "其他节点结算不清除盘根凶狼");
                var captured = (PersistentGameSave)DetachedResourceSnapshot.Capture(new PersistentGameSave { scene_progress = new D {
                    ["east_woods_dungeon_state"] = _panel.get_route_state() } }).Materialize();
                var loadedEpisode = ForestWolfEvent.Get(captured.scene_progress["east_woods_dungeon_state"].AsGodotDictionary()["actions"].AsGodotDictionary(), key);
                Check(loadedEpisode["observation"].AsString() == episode["observation"].AsString() && loadedEpisode["node"].AsString() == node,
                    "新增事件记录可通过正式异步存档快照链");
                _panel.close_action_details();
            }
            Reset(baseline); GetWindow().ContentScaleSize = new Vector2I(1280, 720); GetWindow().Size = new Vector2I(1280, 720); await Frames(8);
            var paper = View.GetNode<Control>("EventPaper");
            Check(paper.GetGlobalRect().End.X <= _panel.Size.X && paper.GetGlobalRect().End.Y <= _panel.Size.Y, "720画布事件窗不溢出");
            Check(!Read<ScrollContainer>(View, "_storyScroll").GetVScrollBar().Visible, "720画布短旁白完整显示");
            await Capture("05_四选项_720");
            var longPage = new ForestWolfEvent.Page("林间抉择", string.Join("\n\n", Enumerable.Repeat("树影深处传来隐约的动静，你停下脚步，仔细判断眼前的情形与接下来的选择。", 15)),
                Enumerable.Range(0, 6).Select(i => new ForestWolfEvent.Choice("example_" + i, "沿着林间足迹观察周围环境，并选择接下来应当采取的行动")).ToArray());
            View.Display(longPage); await Frames(8);
            Check(Read<ScrollContainer>(View, "_storyScroll").GetVScrollBar().Visible && Read<ScrollContainer>(View, "_choiceScroll").GetVScrollBar().Visible,
                "长旁白和六项长选项分别滚动，窗口不随内容增高");
            await Capture("10_长文与六选项布局测试_720");
            GD.Print($"FOREST_WOLF_EVENT_PASS checks={_checks}: actual arrival, costs, outcomes, fixed wolf, save, ecology, briefing/cancel and result callbacks; full combat uses separate roundtrip smoke");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { if (temp is not null) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(temp)); }
    }
}
