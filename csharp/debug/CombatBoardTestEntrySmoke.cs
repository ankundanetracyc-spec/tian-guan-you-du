using Godot;
using System.Text.Json;

/// <summary>从真实菜单进入新版战斗，检查开战配招、连续选行、单位定力条及返回；不保存玩家方案。</summary>
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
    private async Task CaptureImmediate(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(Evidence+name+".png")==Error.Ok,"接触瞬间原生画面 "+name);
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
            var entryButton=menu.GetNode<Button>("StartMenuMenu/CombatBoardTest");
            Check(entryButton.IsVisibleInTree()&&entryButton.Text=="新版战斗试演","首页直接显示新版战斗入口");
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
            await Until(()=>Nodes<BattleBoardHud>(scene).Any(),"新版招式盘创建完成");
            var hud=Nodes<BattleBoardHud>(scene).Single();
            await Until(()=>manager.is_board_battle()&&manager.is_board_editing()&&hud.EditorVisible,"初始停在新版配招而非旧队列");
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
            var invalid=hud.Draft.CopyConfiguration();invalid.placements[^1].placement_id=invalid.placements[0].placement_id;
            Check(!hud.TryApplyPlan(invalid)&&hud.Draft.placements.Select(p=>p.placement_id).SequenceEqual(ids),"重复编号方案整盘不覆盖");
            Check(hud.TryApplyPlan(hud.Draft.CopyConfiguration())&&!hud.GetNode<Control>("Editor/SourcePanel/PlanPage").Visible,"合法方案一键覆盖并回来源");
            Find(hud,"方案").EmitSignal(BaseButton.SignalName.Pressed);await Frames(2);
            Nodes<Button>(hud.GetNode<Control>("Editor/SourcePanel/PlanPage")).First(button=>button.Name=="ApplyBattlePlan").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!hud.GetNode<Control>("Editor/SourcePanel/PlanPage").Visible&&hud.Draft.placements.Select(p=>p.placement_id).SequenceEqual(ids),"入场配置一键恢复且不恢复资源或创建新物品");
            hud.SelectCategory("化法");Check(Nodes<BattleBoardSource>(hud).Count()>0,"已选主辅修提供真实定向化法");
            await Capture("05_化法来源_1920");hud.SelectCategory("丹药");Check(Nodes<BattleBoardSource>(hud).Count()==1,"丹药来源取本人的真实实存");hud.SelectCategory("招式");
            var player=manager.get_main_player_unit()!;
            var movementDemoSkill=manager.get_board_placement_skill(player,hud.Draft.placements.Single(item=>item.placement_id==ids[0]));
            Check(movementDemoSkill is { attack_style:"ranged" },"走位演示使用真实远程招式");
            var resource=hud.GetNode<Control>("Resources");
            Check(!resource.GetGlobalRect().Intersects(board.GetGlobalRect()),"编辑时资源栏避开放大的主盘");
            hud.CloseEditor();await Frames(2);
            Check(Mathf.IsEqualApprox(resource.Position.X+resource.Size.X/2,BattleUiArt.Canvas.X/2),"查看战场时资源栏恢复全屏中轴");
            hud.OpenEditor();await Frames(2);
            Check(resource.GetNode<Label>("StaminaValue").Text==player.Get("current_stamina").AsInt32()+" / "+player.Get("max_stamina").AsInt32(),"资源数字读取当前人物");
            Check(new[]{"StaminaValue","ManaValue","SpiritValue"}.Select(name=>resource.GetNode<Label>(name)).All(label=>label.HorizontalAlignment==HorizontalAlignment.Right&&label.Position.X+label.Size.X==390),"三行资源数值使用统一右边界");
            foreach(var unit in manager.all_units){unit.Set("dodge",0);unit.Set("physical_defense",0);unit.Set("shield",0);}
            Check(manager.all_units.All(unit=>unit.Get("dodge").AsInt32()==0),"试演固定命中以验收真实受击反馈");
            foreach(var enemy in manager.enemy_units)
            {
                var enemyState=manager.get_board_state(enemy)!;
                foreach(var placement in enemyState.Layout.placements.ToArray())
                    if(manager.get_board_placement_skill(enemy,placement) is { } enemySkill&&CardRules.IsResponse(enemySkill))
                        enemyState.Layout.placements.Remove(placement);
            }
            Check(manager.enemy_units.All(enemy=>manager.get_board_state(enemy)!.Layout.placements.All(placement=>
            {
                var skill=manager.get_board_placement_skill(enemy,placement);
                return skill is null||!CardRules.IsResponse(skill);
            })),"隔离击退对练排除应对干扰");
            Check(manager.commit_player_board(hud.Draft,out var reason),"开始按钮提交合法草稿 "+reason);
            var committedMovementSkill=manager.get_board_placement_skill(player,manager.get_board_state(player)!.Layout.placements.Single(item=>item.placement_id==ids[0]))!;
            committedMovementSkill.preferred_range=committedMovementSkill.range_min;
            await Frames(18);
            Check(!hud.EditorVisible&&hud.GetNode<BattleBoardView>("MiniBoard").Visible,"开战收起编辑与右栏并显示左下小盘");
            var small=hud.GetNode<BattleBoardView>("MiniBoard");
            Check(small.Position.X<100&&small.Position.Y>650,"小盘固定左下");
            Check(!hud.MovePlacement(ids[0],Vector2I.Zero),"运行中不能修改配置");
            var resolveBars=Nodes<BattleHealthBar>(GetTree().Root).Where(bar=>bar.ShowResolve).ToArray();
            var resolveLabels=Nodes<Label>(GetTree().Root).Where(label=>label.Name=="ResolveValue"&&label.Visible).ToArray();
            Check(resolveBars.Length>0&&resolveLabels.Length>0&&resolveBars.All(bar=>bar.MaxResolve>0),"角色头顶显示可读定力数值与独立定力条");
            manager.toggle_battle_pause();hud.OpenEditor();
            Check(manager.is_battle_paused()&&!hud.EditorVisible,"普通暂停不开放配招");manager.toggle_battle_pause();
            manager.cycle_battle_speed();manager.cycle_battle_speed();await Capture("06_自动战斗_1920");
            manager.set_battle_speed(1f);
            var map=scene.GetNode<BattleMap>("BattleMap");
            Check(map.try_get_unit_cell(player,out var movementStartCell),"远程走位演示读取真实起始格");
            Check(manager.select_board_row(0),"玩家选行后进入交锋");await Frames(3);
            Check(Nodes<Label>(hud.GetNode<Control>("PlayerActionColumn")).Any(label=>label.Text.Contains("出手值 ",StringComparison.Ordinal)),"行动纵列显示计算后的出手值");
            await Capture("07_交锋行动纵列_1920");
            await Until(()=>map.try_get_unit_cell(player,out var movedCell)&&movedCell!=movementStartCell,"远程招式按理想距离先走一格",seconds:8);
            await Until(()=>Nodes<BattleBoardMovementEffect>(GetTree().Root).Any(effect=>effect.Name=="BoardStepTrace"),"走位显示逐格墨痕");
            await Capture("12_远程走位_1920");
            await Until(()=>Nodes<BattleContactFocus>(GetTree().Root).Any(focus=>focus.ImpactStopActive),"真实接触触发短暂停帧");
            var contactFocus=Nodes<BattleContactFocus>(GetTree().Root).First(focus=>focus.ImpactStopActive);
            Check(contactFocus.SlowMotionActive&&Engine.TimeScale<.5f,"真实命中触发聚焦慢放");
            Check(contactFocus.StageDimStrength>=.7f,"聚焦时战场背景明显压暗");
            await CaptureImmediate("11_真实接触停帧_1920");
            await Until(()=>Mathf.IsEqualApprox(Engine.TimeScale,1f),"慢放结束后恢复原战斗速度",5);
            await Until(()=>Nodes<BattleContactFocus>(GetTree().Root).Any(focus=>focus.FrameZoom>1.08f),"交锋命中触发舞台局部放大");
            await Capture("10_交锋局部放大_1920");
            await Until(()=>manager.get_board_state(player)!.HasSucceeded(ids[0]),"测试使用真实招式执行与本轮账本");
            Check(manager.all_units.OfType<Node>().All(unit=>unit.GetNodeOrNull<Node>("BattleActionCue") is null),"角色旁不再浮出重复的斜置招式牌");
            Check(player.Get("current_stamina").AsInt32()<=player.Get("max_stamina").AsInt32(),"空格与轮初回体不突破上限");
            await Until(()=>manager.get_board_round()==2&&manager.is_board_choosing_row(),"全员走完后直接进入第二回合选行");
            Check(!hud.EditorVisible&&manager.get_board_round()==2,"回合边界不再打开灵台整理页");
            Check(manager.get_board_state(player)!.Layout.placements.Select(item=>item.placement_id).SequenceEqual(ids),"整场战斗沿用开战时锁定的灵台布局");
            await Capture("08_第二回合直接选行_1920");
            var painPlacement=manager.get_board_state(player)!.Layout.placements.Single(item=>manager.get_board_placement_skill(player,item)?.skill_name=="痛击");
            var pain=manager.get_board_placement_skill(player,painPlacement)!;
            Check(pain.knockback_distance==2&&Mathf.IsEqualApprox(pain.knockback_force,20f),"痛击读取正式的体魄缩放击退参数");
            var target=manager.enemy_units.FirstOrDefault(unit=>unit.Get("current_health").AsInt32()>0);
            Check(target is Node2D,"击退演示读取存活目标");
            var attackerCell=default(Vector2I);var targetCell=default(Vector2I);
            Check(map.try_get_unit_cell(player,out attackerCell)&&map.try_get_unit_cell(target!,out targetCell),"击退演示读取真实双方格位");
            var targetPhysique=Mathf.Max(0,CardRules.Number(target!,"physique"));
            var expectedDistance=Mathf.Clamp(Mathf.FloorToInt(pain.knockback_distance*pain.knockback_force/(pain.knockback_force+targetPhysique)+.5f),0,pain.knockback_distance);
            var expectedPath=map.get_knockback_path(attackerCell,target!,expectedDistance);
            Check(expectedPath.Count>0,"痛击参数按目标体魄算出可落地击退格");
            var startPosition=((Node2D)target!).GlobalPosition;
            BattleContactFocus.Play((Node2D)player,(Node2D)target!,1f);
            BattleContactFocus.HoldImpactFrame((Node2D)target!);
            var knockback=manager.ApplyBoardKnockback(player,target!,pain);
            await Until(()=>Nodes<BattleBoardMovementEffect>(GetTree().Root).Any(effect=>effect.Name=="BoardKnockbackTrace"),"痛击的正式参数驱动击退墨痕");
            var focus=Nodes<BattleContactFocus>(GetTree().Root).First(candidate=>candidate.SlowMotionActive);
            Check(focus.StageDimStrength>=.7f&&focus.FrameZoom>1.08f,"击退接触触发聚焦放大、慢放与背景压暗");
            await CaptureImmediate("13_正式痛击击退_1920");
            await knockback;
            Check(map.try_get_unit_cell(target!,out var afterKnockbackCell)&&afterKnockbackCell==expectedPath[^1],"目标实际移至体魄缩放后的击退落点");
            Check(((Node2D)target!).GlobalPosition.DistanceTo(startPosition)>1f,"战场刷新未覆盖击退位移动画");
            await Until(()=>Mathf.IsEqualApprox(Engine.TimeScale,1f),"击退演出结束后恢复战斗速度",5);
            var navigation=hud.GetNode<Button>("BoardModeToggle");
            Check(!navigation.Visible,"开战后移除灵台编辑入口");
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

