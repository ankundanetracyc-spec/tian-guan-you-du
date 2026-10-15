using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class EastWoodsActionsSmoke : Node
{
    private EastWoodsActionHarness _main = null!;
    private EastWoodsDungeonPanel _panel = null!;
    private Character _player = null!;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object? Call(object target, string name, params object[] args) => typeof(MainScene).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private static void Field(object target, string name, object value) => typeof(MainScene).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    private static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private async Task Frames() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void At(string node)
    {
        _panel.close_action_details(); var state = _panel.get_route_state(); state["current"] = node;
        _panel.restore_route_state(state); _panel.show_dungeon();
    }
    private Button Confirm() => _panel.GetNode<PanelContainer>("NodeActionDetails/CenteredCard/PaperCard").FindChild("Confirm", true, false) as Button ?? throw new Exception("Missing confirm");
    private Button Action(string id) => _panel.GetNode<PanelContainer>("CurrentNodeActions").FindChild("Action_" + id, true, false) as Button ?? throw new Exception("Missing action " + id);
    private void Execute(string id) { _panel.close_action_details(); Action(id).EmitSignal(BaseButton.SignalName.Pressed); Confirm().EmitSignal(BaseButton.SignalName.Pressed); }
    private async Task Capture(string name)
    {
        await Frames();
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        var folder = "res://docs/美术规范/东林地节点地图_20261011/实机验收/节点行动";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(folder + "/" + name + ".png");
    }
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--seeded")) EastWoodsDungeonRules.generate_layout("actions-in-seeded-layout");
            var compact = OS.GetCmdlineUserArgs().Contains("--compact");
            GetWindow().ContentScaleSize = compact ? new Vector2I(1280,720) : new Vector2I(1920,1080);
            _main = new EastWoodsActionHarness(); AddChild(_main);
            _main.AddChild(new CanvasLayer { Name = "SceneLayer" });
            _player = new Character { entity_data = new HumanData { entity_name = "行动测试", current_world_stamina = 100, max_world_stamina = 100 } };
            _main.AddChild(_player); Field(_main, "player", _player);
            _panel = (EastWoodsDungeonPanel)Call(_main, "get_or_create_east_woods_dungeon")!;
            At("fern_lowland"); await Frames();
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); var before = time.get_precise_game_hours();
            Check(_panel.GetNode<PanelContainer>("CurrentNodeActions").FindChild("Action_explore", true, false) is null, "废弃探查按钮仍在");
            Action("gather_root").EmitSignal(BaseButton.SignalName.Pressed); await Capture("01_采集详情_" + (compact ? "720" : "1080"));
            _panel._UnhandledKeyInput(new InputEventKey { Keycode = Key.W, Pressed = true });
            Check(_panel.get_current_route_node_id() == "fern_lowland", "详情没有阻止移动");
            _panel.return_one_level();
            Check(_panel.Visible && !_panel.GetNode<Control>("NodeActionDetails").Visible, "返回未只关闭详情");
            Check(time.get_precise_game_hours() == before && _player.current_world_stamina == 100, "取消扣费");
            Check(_panel.can_execute_action("fern_lowland", "gather_root") && !_panel.can_execute_action("fern_lowland", "explore"), "采集仍要求探查");
            _panel.close_action_details(); Action("gather_root").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("02_采集详情_" + (compact ? "720" : "1080")); _panel.close_action_details();
            Execute("gather_root"); Execute("gather_root");
            Check(_player.get_inventory_total_count("res://resources/entities/items/苦苔根.tres") == 4 && _player.current_world_stamina == 96, "采集物品或费用错误");
            Check(_panel.action_remaining("fern_lowland", EastWoodsActions.Content("fern_lowland")!) == 0, "资源未耗尽");
            before = time.get_precise_game_hours(); Call(_main,"on_east_woods_action","fern_lowland","gather_root");
            Check(time.get_precise_game_hours() == before && _player.current_world_stamina == 96, "耗尽后仍结算");
            var saved = _panel.get_route_state(); _panel.restore_route_state(Json.ParseString(Json.Stringify(saved)).AsGodotDictionary());
            Check(_panel.is_explored("fern_lowland") && !_panel.can_execute_action("fern_lowland","gather_root"), "资源存档恢复错误");
            At("north_turn"); Execute("old_trap"); await Capture("03_事件结果_" + (compact ? "720" : "1080"));
            _panel.close_action_details(); Check(!_panel.can_execute_action("north_turn","old_trap"), "事件可重复刷取");
            At("dry_pool"); _player.current_world_stamina = 0; before = time.get_precise_game_hours(); Execute("gather_moss");
            Check(_panel.action_remaining("dry_pool", EastWoodsActions.Content("dry_pool")!) == 2 && time.get_precise_game_hours() == before, "体力不足仍完成");
            _player.current_world_stamina = 100; _panel.close_action_details();
            // Filling a real inventory tests the non-mutating capacity check.
            var human = (HumanData)_player.entity_data!;
            var oldPaths = human.inventory_item_paths.Duplicate(); var oldCounts = human.inventory_item_counts.Duplicate(); var oldPositions = human.inventory_item_positions.Duplicate();
            human.inventory_item_paths.Clear(); human.inventory_item_counts.Clear(); human.inventory_item_positions.Clear();
            for (var i=0;i<Character.InventorySlotCapacity;i++) { human.inventory_item_paths.Add("res://resources/entities/items/兽骨.tres"); human.inventory_item_counts.Add(999); human.inventory_item_positions.Add(i); }
            before = time.get_precise_game_hours(); var stamina = _player.current_world_stamina; Execute("gather_moss");
            Check(time.get_precise_game_hours() == before && _player.current_world_stamina == stamina && _panel.action_remaining("dry_pool",EastWoodsActions.Content("dry_pool")!)==2,"行囊满仍扣费");
            human.inventory_item_paths=oldPaths; human.inventory_item_counts=oldCounts; human.inventory_item_positions=oldPositions;
            At("outer_03_06"); Execute("gather_root");
            Check(_panel.action_remaining("outer_03_06",EastWoodsActions.Content("outer_03_06")!)==2 && _panel.action_remaining("outer_06_04",EastWoodsActions.Content("outer_06_04")!)==4 && _panel.action_remaining("fern_lowland",EastWoodsActions.Content("fern_lowland")!)==0,"新旧资源点储量互相覆盖");
            var expandedState=Json.ParseString(Json.Stringify(_panel.get_route_state())).AsGodotDictionary();
            _panel.restore_route_state(expandedState);
            Check(_panel.action_remaining("outer_03_06",EastWoodsActions.Content("outer_03_06")!)==2,"新增资源余量读档丢失");
            foreach(var node in EastWoodsDungeonRules.get_nodes())
                if(EastWoodsActions.Content(node.Id) is {ItemPath.Length:>0} content)
                    Check(EastWoodsActions.Item(content.ItemPath) is {} item && ResourceLoader.Exists(item.icon_path),"扩图使用不存在物品或图标");
            var slowPoint = EastWoodsActions.ResourcePoints().Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.SlowGrowth);
            var mineralPoint = EastWoodsActions.ResourcePoints().Single(point => point.Action.Recovery == EastWoodsActions.ResourceRecoveryGroup.Mineral);
            Check(EastWoodsDungeonRules.get_node(slowPoint.Node) is { ResourceCategory: EastWoodsResourceCategory.Plant, ResourceNodeId: "mistbell_slow" } &&
                  EastWoodsDungeonRules.get_node(mineralPoint.Node) is { ResourceCategory: EastWoodsResourceCategory.Mineral, ResourceNodeId: "spirit_stone_vein" } &&
                  _panel.find_action(slowPoint.Node, mineralPoint.Action.Id) is null &&
                  _panel.find_action(mineralPoint.Node, slowPoint.Action.Id) is null,
                "资源节点类别未隔离植物与矿产采集行动。");
            before = time.get_precise_game_hours(); stamina = _player.current_world_stamina;
            At(slowPoint.Node); Execute(slowPoint.Action.Id);
            Check(_player.get_inventory_total_count(slowPoint.Action.ItemPath) == 1 &&
                  time.get_precise_game_hours() == before + 1 && _player.current_world_stamina == stamina - 2 &&
                  _panel.action_remaining(slowPoint.Node, slowPoint.Action) == 1,
                "30日慢生点未按现有采集行动结算物品、时间、体力与储量。");
            before = time.get_precise_game_hours(); stamina = _player.current_world_stamina;
            At(mineralPoint.Node); Execute(mineralPoint.Action.Id);
            Check(_player.get_inventory_total_count(mineralPoint.Action.ItemPath) == 1 &&
                  time.get_precise_game_hours() == before + 1 && _player.current_world_stamina == stamina - 2 &&
                  _panel.action_remaining(mineralPoint.Node, mineralPoint.Action) == 1,
                "90日矿产点未按现有采集行动结算物品、时间、体力与储量。");
            var routeState = _panel.get_route_state();
            var savedHour = time.get_total_game_hours();
            EastWoodsEcology.Synchronize(routeState, savedHour, _panel.get_current_route_node_id());
            routeState = Json.ParseString(Json.Stringify(routeState)).AsGodotDictionary();
            var savedActions = routeState["actions"].AsGodotDictionary();
            var savedEcology = savedActions["__east_woods_ecology"].AsGodotDictionary();
            var savedCursors = savedEcology["resource_cursors"].AsGodotDictionary();
            var slowKey = EastWoodsEcology.ActionProgressKey(slowPoint.Node, slowPoint.Action.Id);
            var mineralKey = EastWoodsEcology.ActionProgressKey(mineralPoint.Node, mineralPoint.Action.Id);
            var slowCursor = savedCursors.GetValueOrDefault(slowKey, -1).AsInt32();
            var mineralCursor = savedCursors.GetValueOrDefault(mineralKey, -1).AsInt32();
            var slowPeriod = EastWoodsEcology.RecoveryRate(slowPoint.Action.Recovery).PeriodHours;
            var mineralPeriod = EastWoodsEcology.RecoveryRate(mineralPoint.Action.Recovery).PeriodHours;
            Check(savedActions[slowKey].AsInt32() == 1 && savedActions[mineralKey].AsInt32() == 1 &&
                  savedCursors.ContainsKey(slowKey) && savedCursors.ContainsKey(mineralKey) &&
                  slowCursor >= 0 && slowCursor <= savedHour && savedHour - slowCursor < slowPeriod &&
                  mineralCursor >= 0 && mineralCursor <= savedHour && savedHour - mineralCursor < mineralPeriod,
                "资源储量未保存，或恢复游标超出当前时间和周期余数范围。");
            _panel.restore_route_state(routeState);
            Check(_panel.action_remaining(slowPoint.Node, slowPoint.Action) == 1 &&
                  _panel.action_remaining(mineralPoint.Node, mineralPoint.Action) == 1 &&
                  EastWoodsDungeonRules.get_node(slowPoint.Node)?.ResourceCategory == EastWoodsResourceCategory.Plant &&
                  EastWoodsDungeonRules.get_node(mineralPoint.Node)?.ResourceCategory == EastWoodsResourceCategory.Mineral,
                "存档恢复后资源储量或类别丢失。");
            var npcManager = GetNode<NpcWorldManager>("/root/NpcWorldManager"); npcManager.npcs.Clear();
            var npc = new WorldNpcData { npc_id="action_smoke", npc_name="测试同修", is_alive=true, dungeon_id=EastWoodsDungeonRules.DungeonId, dungeon_node_id="forest_edge", dun_speed=100 };
            npcManager.npcs.Add(npc); Field(_main,"_npc_world",npcManager);
            // Use null player data to measure the timeline independently of encounter probability.
            npcManager.advance_east_woods_route("quiet_grove","quiet_grove",.5f,null);
            Check(npc.dungeon_step_index==0 && Math.Abs(npc.dungeon_travel_progress-.5f)<.001,"NPC半格进度错误");
            npcManager.advance_east_woods_route("quiet_grove","quiet_grove",.5f,null);
            Check(npc.dungeon_step_index==1,"NPC遁速100一小时未行动一次");
            npc.dun_speed=200; npcManager.advance_east_woods_route("quiet_grove","quiet_grove",1,null);
            Check(npc.dungeon_step_index==3,"NPC遁速200一小时未行动两次");
            npc.dun_speed=1000; npcManager.advance_east_woods_route("quiet_grove","quiet_grove",1,null);
            Check(npc.dungeon_step_index==13,"高速NPC仍被旧八步上限截断");
            npc.dun_speed=200;
            // Real action handler must also advance NPCs while the player stays put.
            At("outer_06_04"); var steps=npc.dungeon_step_index; Execute("gather_root");
            Check(npc.dungeon_step_index==steps+2 && _panel.get_current_route_node_id()=="outer_06_04","驻足采集未同步NPC");
            npcManager.npcs.Clear();
            At("root_hollow"); _panel.close_action_details();
            Action("wolf").EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("05_遇怪详情_"+(compact?"720":"1080"));
            var card = _panel.GetNode<PanelContainer>("NodeActionDetails/CenteredCard/PaperCard");
            Check(card.GetGlobalRect().End.Y <= _panel.Size.Y && card.GetGlobalRect().End.X <= _panel.Size.X,"遇怪详情溢出屏幕");
            Confirm().EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            var transition=GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            var pending = Read<Godot.Collections.Array>(transition,"pending_defender_data");
            Check(pending.Count==1 && pending[0].AsGodotObject() is BattleEntityData { dun_speed:100 },"妖兽正式快照未接入");
            Check(Read<Godot.Collections.Dictionary>(transition,"return_context")["loot_rewards"].AsGodotArray().Count==2,"遭遇奖励未配置");
            var flow=transition.GetChildren().OfType<BattleFlowView>().Single();
            var back=flow.FindChildren("*","Button",true,false).OfType<Button>().Single(button=>button.Text=="返回");
            back.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Check(!GetTree().Paused && Read<Godot.Collections.Array>(transition,"pending_defender_data").Count==0 && _panel.can_execute_action("root_hollow","wolf"),"战前取消未恢复");
            var context = new Godot.Collections.Dictionary { ["kind"]="east_woods_beast", ["node"]="root_hollow", ["action"]="wolf", ["route_state"]=_panel.get_route_state() };
            _main.handle_persistent_battle_return(new(),new(),new Godot.Collections.Dictionary { ["player_victory"]=false },context);
            _panel.close_action_details(); Check(_panel.can_execute_action("root_hollow","wolf"),"战败错误清怪");
            _main.handle_persistent_battle_return(new(),new(),new Godot.Collections.Dictionary { ["player_victory"]=true },context);
            _panel.close_action_details(); Check(!_panel.can_execute_action("root_hollow","wolf"),"胜利未清怪");
            Check(_player.get_inventory_total_count("res://resources/entities/items/兽骨.tres")==0,"回调重复发放战斗奖励");
            await Capture("04_行动地图_"+(compact?"720":"1080"));
            // Real navigation callbacks must not provide an anywhere-exit shortcut.
            At("quiet_grove"); before = time.get_precise_game_hours();
            _panel.close(); Check(_panel.Visible, "深处可直接退出");
            _panel.return_one_level();
            Check(_panel.Visible && _panel.GetNode<Control>("NodeActionDetails").Visible, "Esc直接离场");
            await Capture("06_副本暂歇_"+(compact?"720":"1080"));
            _panel._UnhandledKeyInput(new InputEventKey { Keycode=Key.W, Pressed=true });
            Check(_panel.get_current_route_node_id()=="quiet_grove", "暂停时仍可移动");
            _panel.return_one_level();
            Call(_main,"open_world");
            Check(_panel.Visible && _panel.get_current_route_node_id()=="quiet_grove" && _panel.GetNode<PanelContainer>("RoutePreview").Visible, "世界入口绕过出口或直接移动");
            Check(time.get_precise_game_hours()==before, "暂停或定位出口消耗时间");
            saved=Json.ParseString(Json.Stringify(_panel.get_route_state())).AsGodotDictionary();
            Check(saved["inside"].AsBool(), "副本内存档丢失入场标记");
            Field(_main,"_east_woods_dungeon_state",saved);
            Call(_main,"open_east_woods_dungeon");
            Check(_panel.get_current_route_node_id()=="quiet_grove", "恢复副本内进度错误回入口");
            At("forest_edge"); _panel.GetNode<PanelContainer>("CurrentNodeActions").FindChild("LeaveDungeon",true,false).EmitSignal(BaseButton.SignalName.Pressed);
            await Capture("07_出口确认_"+(compact?"720":"1080"));
            _panel.return_one_level(); Check(_panel.Visible,"取消出口仍离场");
            _panel.GetNode<PanelContainer>("CurrentNodeActions").FindChild("LeaveDungeon",true,false).EmitSignal(BaseButton.SignalName.Pressed);
            Confirm().EmitSignal(BaseButton.SignalName.Pressed);
            Check(!_panel.Visible && !_panel.get_route_state()["inside"].AsBool(),"出口确认未离场");
            Call(_main,"open_east_woods_dungeon");
            Check(_panel.Visible && _panel.get_current_route_node_id()=="forest_edge" && _panel.is_explored("fern_lowland") && _panel.action_remaining("fern_lowland",EastWoodsActions.Content("fern_lowland")!)==0 && !_panel.can_execute_action("root_hollow","wolf"), "往返重置进度或没有从入口出发");
            At("root_hollow"); Check(_panel.action_remaining("root_hollow",EastWoodsActions.Content("root_hollow")!)==0,"往返复活已击败妖兽");
            GD.Print("EAST_WOODS_EXIT PASS: guarded close, Esc modal, world shortcut, zero-cost preview, inside save restore, exit cancel/confirm, reentry preserves actions");
            npcManager.npcs.Clear();
            var walker = new WorldNpcData { npc_id="motion_one",npc_name="行旅甲",is_alive=true,dungeon_id=EastWoodsDungeonRules.DungeonId,dungeon_node_id="forest_edge",dun_speed=100 };
            var swift = new WorldNpcData { npc_id="motion_two",npc_name="行旅乙",is_alive=true,dungeon_id=EastWoodsDungeonRules.DungeonId,dungeon_node_id="forest_edge",dun_speed=200 };
            npcManager.npcs.Add(walker); npcManager.npcs.Add(swift);
            At("forest_edge");
            Control[] MovingNpcs() => _panel.GetNode<Control>("MapViewport/OrthogonalNodeMap").GetChildren().OfType<Control>().Where(p=>p.HasMeta("npc_id")).ToArray();
            before=time.get_precise_game_hours();
            _panel.set_player_dun_speed(400); // Slow visual playback for reliable frame-by-frame GPU inspection.
            npcManager.advance_east_woods_route("quiet_grove","quiet_grove",1,null);
            Check(walker.dungeon_step_index==1 && swift.dungeon_step_index==2 && MovingNpcs().Length==2,"多人遁速移动或过渡缺失");
            var sawRock=false;
            for(var sample=0;sample<20;sample++)
            {
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(MovingNpcs().Any(p=>Math.Abs(p.Rotation)>.001)) { sawRock=true; break; }
            }
            Check(sawRock,"NPC没有摇摆");
            await Capture("08_NPC并行移动_"+(compact?"720":"1080"));
            await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);
            Check(MovingNpcs().Length==0 && time.get_precise_game_hours()==before,"动画残影或额外推进时间");
            _panel.set_player_dun_speed(100);
            swift.dun_speed=1000;
            var edgeCount=0;
            void CountEdge(WorldNpcData n,string a,string b) { if(n.npc_id==swift.npc_id) { edgeCount++; Check(EastWoodsDungeonRules.get_neighbors(a).Contains(b),"NPC动画收到斜向捷径"); } }
            npcManager.DungeonNodeStepped+=CountEdge;
            npcManager.advance_east_woods_route("quiet_grove","quiet_grove",1,null);
            npcManager.DungeonNodeStepped-=CountEdge;
            Check(edgeCount==10,"高速NPC丢失真实路径边");
            _panel.interrupt_travel(); await Frames();
            Check(MovingNpcs().Length==0 && swift.dungeon_step_index==12,"中断后残影或回滚NPC位置");
            npcManager.advance_east_woods_route("quiet_grove","quiet_grove",1,null);
            walker.is_alive=false; npcManager.EmitSignal(NpcWorldManager.SignalName.world_npcs_changed); await Frames();
            Check(!MovingNpcs().Any(p=>p.GetMeta("npc_id").AsString()==walker.npc_id),"死亡NPC残留动画");
            _panel.Hide(); await Frames(); Check(MovingNpcs().Length==0,"隐藏副本残留动画");
            npcManager.advance_east_woods_route("quiet_grove","quiet_grove",1,null);
            _panel.show_dungeon(); await Frames(); Check(MovingNpcs().Length==0,"重进重播离线动画");
            swift.dungeon_node_id="forest_edge"; swift.dungeon_travel_progress=0; swift.dun_speed=200;
            var actionFixture = _panel.get_route_state();
            actionFixture["actions"].AsGodotDictionary().Remove("fern_lowland:gather_root");
            _panel.restore_route_state(actionFixture);
            At("fern_lowland"); Execute("gather_root");
            Check(MovingNpcs().Length==1,"驻足行动结束错误清除NPC过渡");
            _panel.interrupt_travel(); await Frames();
            GD.Print("EAST_WOODS_NPC_MOTION PASS: concurrent gait, speed/path edges, no extra clock, cancellation, death, hidden scene and reentry");
            _main.QueueFree(); await Frames(); GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print("EAST_WOODS_ACTIONS PASS: cancel, modal, direct gather/stock, save, event, exhaustion, full bag, NPC timeline, real beast package/briefing/cancel, victory/loss return");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
