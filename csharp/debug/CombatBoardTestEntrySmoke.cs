using Godot;
using System.Text.Json;

/// <summary>从真实菜单进入新版战斗、配招、共同回合及返回；不保存玩家方案。</summary>
public partial class CombatBoardTestEntrySmoke : Node
{
    private readonly List<string> _checks=[];
    private string Evidence="res://docs/美术规范/招式盘拖放与遍历反馈_20261108/入口复验/";
    private void Check(bool value,string name) { if(!value)throw new InvalidOperationException(name);_checks.Add(name);GD.Print("BOARD_ENTRY_PASS "+name); }
    private async Task Frames(int count=1) { for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> condition,string name,int seconds=45)
    {
        var deadline=Time.GetTicksMsec()+(ulong)seconds*1000;
        while(!condition()&&Time.GetTicksMsec()<deadline)await Frames();Check(condition(),name);
    }
    private static IEnumerable<T> Nodes<T>(Node root) where T:Node
    {
        if(root is T item)yield return item;
        foreach(var child in root.GetChildren())foreach(var item2 in Nodes<T>(child))yield return item2;
    }
    private static Button Find(Node root,string text)=>Nodes<Button>(root).First(b=>b.Text==text||Nodes<Label>(b).Any(label=>label.Text==text));
    private async Task Click(Vector2 at)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position=at,GlobalPosition=at });await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position=at,GlobalPosition=at,ButtonIndex=MouseButton.Left,Pressed=true });await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position=at,GlobalPosition=at,ButtonIndex=MouseButton.Left,Pressed=false });await Frames(2);
    }
    private Vector2 Screen(Control node,Vector2 local)=>GetViewport().GetFinalTransform()*(node.GetGlobalTransformWithCanvas()*local);
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await Frames(5);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(Evidence+name+".png")==Error.Ok,"原生画面 "+name);
    }
    private async Task CaptureResources(Control resource)
    {
        if(DisplayServer.GetName()=="headless")return;
        await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        var scale=image.GetSize()/(Vector2)GetViewport().GetVisibleRect().Size;
        var rect=resource.GetGlobalRect();
        using var detail=image.GetRegion(new Rect2I((Vector2I)(rect.Position*scale),(Vector2I)(rect.Size*scale)));
        Check(detail.SavePng(Evidence+"08_资源栏_对齐修正.png")==Error.Ok,"原生资源栏局部截图");
    }
    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        var output=OS.GetCmdlineUserArgs().FirstOrDefault(value=>value.StartsWith("--entry-output=",StringComparison.Ordinal));
        if(output is not null)Evidence=output["--entry-output=".Length..].TrimEnd('/')+"/";
        CallDeferred(nameof(Run));
    }
    private async void Run()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var save=GetNode<GameSaveManager>("/root/GameSaveManager");var slot=save.active_slot_id;
            GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().ContentScaleSize=new(1920,1080);GetWindow().Size=new(1920,1080);
            var menu=GD.Load<PackedScene>("res://tscn/scene/StartMenu.tscn").Instantiate<StartMenu>();GetTree().Root.AddChild(menu);GetTree().CurrentScene=menu;await Frames(4);
            menu.GetNode<Button>("StartMenuMenu/DemoToggle").EmitSignal(BaseButton.SignalName.Pressed);await Frames(2);
            var entryButton=Find(menu,"新版战斗 · 招式盘试演");Check(entryButton.IsVisibleInTree(),"首页试演菜单有独立新版入口");
            entryButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Until(()=>GetTree().CurrentScene is CombatBoardTestEntry,"进入独立心法配置页");
            var entry=(CombatBoardTestEntry)GetTree().CurrentScene;
            GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Size=new(1920,1080);await Frames(4);
            entry.GetNode<OptionButton>("EnemyCount").Select(1);
            var preview=entry.CreateSide("来源检查",1,9,"","");
            Check(preview.combat_board_catalog?.skills.Count==20,"试演使用完整真实二十招已学池");
            Check(preview.pack_item_paths.Select((path,index)=>path=="res://resources/entities/items/alchemy/huichun_1.tres"?preview.pack_item_counts[index]:0).Sum()==2,"丹药只来自隔离试演的两颗实存");
            await Capture("01_试演配置_1920");
            entry.BeginTrial();
            var transition=GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
            await Until(()=>transition.has_open_flow_view(),"沿正式入口打开战前页");
            Find(transition,"开始战斗").EmitSignal(BaseButton.SignalName.Pressed);
            await Until(()=>GetTree().CurrentScene is BattleScene&&!transition.is_flow_transition_active(),"进入真实战斗场景");
            var scene=GetTree().CurrentScene;var manager=scene.GetNode<BattleManager>("BattleMgr");
            var hud=Nodes<BattleBoardHud>(scene).Single();await Frames(5);
            if(DisplayServer.GetName()!="headless")await ToSignal(GetTree().CreateTimer(1.6),SceneTreeTimer.SignalName.Timeout);
            Check(manager.is_board_battle()&&manager.is_board_editing()&&hud.EditorVisible,"初始停在新版配招而非旧队列");
            Check(manager.enemy_units.Count==2,"敌人数配置进入真实战斗包");
            Check(Nodes<BattleBoardSource>(hud).Count()==20,"右栏实际列出二十招");
            Check(Nodes<BattleBoardSkinPanel>(hud).Any(p=>p.Part=="source_panel")&&Nodes<BattleBoardSkinPanel>(hud).Any(p=>p.Part=="resource_frame"),"使用新来源与资源透明底板");
            Check(BattleBoardSkin.Part("cell_face").GetSize()==new Vector2(246,246),"新格面使用高密度源图区域");
            Check(hud.GetNode<Control>("Resources").GetChildren().OfType<TextureRect>().All(icon=>icon.Size==new Vector2(20,20)),"资源图符固定显示尺寸不被源纹理撑大");
            var board=hud.GetNode<BattleBoardView>("Editor/Board");
            Check(Nodes<BattleBoardSource>(hud).All(source=>source.GetChildren().OfType<BattleBoardView>().Single().CellSize==BattleBoardHud.SourceCellSize),"来源各项统一格距并保留实际占格");
            Check(board._GetTooltip(Vector2.Zero)=="","盘面关闭旧原生提示以免与共用悬停重叠");
            await Capture("02_配招暗幕_1920");
            await CaptureResources(hud.GetNode<Control>("Resources"));
            GetWindow().Size=new(1280,720);await Capture("03_配招暗幕_1280");GetWindow().Size=new(1920,1080);await Frames(3);
            var search=hud.GetNode<LineEdit>("Editor/SourcePanel/Search");search.Text="架招";search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);await Frames(3);
            Check(Nodes<BattleBoardSource>(hud).Count()==1,"搜索只筛选来源而不改盘");
            var ids=hud.Draft.placements.Select(p=>p.placement_id).ToArray();
            await Click(Screen(hud,new(130,650)));
            Check(!hud.EditorVisible&&manager.is_board_editing(),"裸露暗幕关闭且不提前开战");
            await Capture("04_查看战场_1920");
            hud.OpenEditor();await Frames(2);
            Check(hud.Draft.placements.Select(p=>p.placement_id).SequenceEqual(ids)&&search.Text=="架招","重开保留草稿身份与搜索");
            search.Text="";search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);await Frames(3);
            var source=Nodes<BattleBoardSource>(hud).First();
            search.GrabFocus();
            var from=Screen(source,new(25,25));var outside=Screen(hud,new(130,650));
            Input.ParseInputEvent(new InputEventMouseButton { Position=from,GlobalPosition=from,ButtonIndex=MouseButton.Left,Pressed=true });await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { Position=outside,GlobalPosition=outside,ButtonIndex=MouseButton.Left,Pressed=false });await Frames(2);
            Check(hud.EditorVisible,"从控件按下后松到暗幕不误关闭");
            Check(GetViewport().GuiGetFocusOwner()!=search,"选中招式后解除搜索焦点以允许R转向");
            Find(hud,"方案").EmitSignal(BaseButton.SignalName.Pressed);await Frames(2);
            Check(hud.GetNode<Control>("Editor/SourcePanel/PlanPage").Visible&&!Nodes<AcceptDialog>(hud).Any(),"方案在右栏子页打开且不弹系统窗口");
            await Capture("09_方案子页_1920");
            hud.CloseEditor();hud.OpenEditor();Check(hud.GetNode<Control>("Editor/SourcePanel/PlanPage").Visible,"临时查看保留方案子页");
            var invalid=hud.Draft.CopyConfiguration();invalid.size=new(1,1);
            Check(!hud.TryApplyPlan(invalid)&&hud.Draft.placements.Select(p=>p.placement_id).SequenceEqual(ids),"不合法方案整盘不覆盖");
            Check(hud.TryApplyPlan(hud.Draft.CopyConfiguration())&&!hud.GetNode<Control>("Editor/SourcePanel/PlanPage").Visible,"合法方案一键覆盖并回来源");
            Find(hud,"方案").EmitSignal(BaseButton.SignalName.Pressed);Find(hud,"入场配置").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!hud.GetNode<Control>("Editor/SourcePanel/PlanPage").Visible&&hud.Draft.placements.Select(p=>p.placement_id).SequenceEqual(ids),"入场配置一键恢复且不恢复资源或创建新物品");
            hud.SelectCategory("化法");Check(Nodes<BattleBoardSource>(hud).Count()>0,"已选主辅修提供真实定向化法");
            await Capture("05_化法来源_1920");hud.SelectCategory("丹药");Check(Nodes<BattleBoardSource>(hud).Count()==1,"丹药来源取本人的真实实存");hud.SelectCategory("招式");
            var player=manager.get_main_player_unit()!;
            var resource=hud.GetNode<Control>("Resources");
            Check(!resource.GetGlobalRect().Intersects(board.GetGlobalRect()),"编辑时资源栏避开放大的主盘");
            hud.CloseEditor();await Frames(2);
            Check(Mathf.IsEqualApprox(resource.Position.X+resource.Size.X/2,BattleUiArt.Canvas.X/2),"查看战场时资源栏恢复全屏中轴");
            hud.OpenEditor();await Frames(2);
            Check(resource.GetNode<Label>("StaminaValue").Text==player.Get("current_stamina").AsInt32()+" / "+player.Get("max_stamina").AsInt32(),"资源数字读取当前人物");
            Check(new[]{"StaminaValue","ManaValue","SpiritValue"}.Select(name=>resource.GetNode<Label>(name)).All(label=>label.HorizontalAlignment==HorizontalAlignment.Right&&label.Position.X+label.Size.X==390),"三行资源数值使用统一右边界");
            Check(manager.commit_player_board(hud.Draft,out var reason),"开始按钮提交合法草稿 "+reason);
            await Frames(3);
            Check(!hud.EditorVisible&&hud.GetNode<BattleBoardView>("MiniBoard").Visible,"开战收起编辑与右栏并显示左下小盘");
            var small=hud.GetNode<BattleBoardView>("MiniBoard");
            Check(small.Position.X<100&&small.Position.Y>650,"小盘固定左下");
            Check(!hud.MovePlacement(ids[0],Vector2I.Zero),"运行中不能修改配置");
            manager.toggle_battle_pause();hud.OpenEditor();
            Check(manager.is_battle_paused()&&!hud.EditorVisible,"普通暂停不开放配招");manager.toggle_battle_pause();
            manager.cycle_battle_speed();manager.cycle_battle_speed();await Capture("06_自动战斗_1920");
            await Until(()=>manager.is_board_editing()&&!manager.is_board_edit_requested(),"全员遍历结束自动进入共同回合整理");
            Check(hud.EditorVisible&&manager.get_board_round()==1,"整理重新打开同一配招页");
            Check(manager.all_units.Where(u=>u.Get("current_health").AsInt32()>0).All(u=>manager.get_board_state(u)?.TraversalComplete==true),"整理时所有存活人物都完成遍历");
            Check(manager.get_board_state(player)!.HasSucceeded(ids[0]),"测试使用真实招式执行与本轮账本");
            Check(player.Get("current_stamina").AsInt32()<=player.Get("max_stamina").AsInt32(),"空格与轮初回体不突破上限");
            await Capture("07_回合末整理_1920");
            Check(manager.commit_player_board(hud.Draft,out _),"整理后可继续第二回合");
            Check(manager.get_board_round()==2,"继续只推进一个共同回合");
            var navigation=hud.GetNode<Button>("BoardModeToggle");
            Check(navigation.Text=="配招"&&navigation.Disabled,"执行期间共享配招入口不可改盘");
            manager.battle_running=false;transition.abandon_battle_for_title();GetTree().ChangeSceneToFile(CombatBoardTestEntry.ScenePath);
            await Until(()=>GetTree().CurrentScene is CombatBoardTestEntry,"试演收尾回到独立配置");
            Check(save.active_slot_id==slot&&!transition.has_pending_results(),"往返不切换旅程且不遗留待恢复战斗结果");
            var report=new { passed=true,checks=_checks.Count,engine=Engine.GetVersionInfo()["string"].AsString(),renderer=DisplayServer.GetName(),items=_checks };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Evidence+(DisplayServer.GetName()=="headless"?"验收_无窗口.json":"验收_原生画面.json")),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
            GD.Print("COMBAT_BOARD_ENTRY_OK checks="+_checks.Count);
            await Frames(5);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Frames(2);GetTree().Quit();
        }
        catch(Exception error) { GD.PushError("COMBAT_BOARD_ENTRY_FAILED "+error);GetTree().Paused=false;GetTree().Quit(1); }
    }
}
