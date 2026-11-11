using Godot;
using System.Text.Json;

/// <summary>用原生鼠标和按键验证招式盘拖放；不保存方案或正式旅程。</summary>
public partial class CombatBoardInteractionSmoke : Node
{
    private const string Evidence="res://docs/美术规范/招式盘拖放与遍历反馈_20261108/";
    private readonly List<string> _checks=[];
    private readonly List<string> _captures=[];
    private BattleManager _manager=null!;
    private BattleBoardHud _hud=null!;
    private BattleBoardView _board=null!,_mini=null!;
    private CombatBoardCatalog _catalog=null!;
    private Character _owner=null!;
    private Control _hover=null!;
    private Vector2 _pointer;
    private Vector2I _windowSize=new(1920,1080);
    private string _output="";
    private string _painKey="",_normalKey="";
    private string _traversalFrames="";
    private bool _recordTraversal;
    private int _traversalFrame;
    private ulong _nextTraversalFrame;

    public override void _Process(double delta)
    {
        if(!_recordTraversal||_traversalFrames.Length==0||Time.GetTicksMsec()<_nextTraversalFrame)return;
        _nextTraversalFrame=Time.GetTicksMsec()+50;
        using var frame=GetViewport().GetTexture().GetImage();
        frame.SaveJpg(_traversalFrames+$"/{_traversalFrame++:D4}.jpg",.9f);
    }

    private void Check(bool value,string label)
    {
        if(!value)throw new InvalidOperationException(label);
        _checks.Add(label);GD.Print("BOARD_INTERACTION_PASS "+label);
    }
    private async Task Frames(int count=1)
    { for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Until(Func<bool> condition,string label,int seconds=15)
    {
        var until=Time.GetTicksMsec()+(ulong)seconds*1000;
        while(!condition()&&Time.GetTicksMsec()<until)await Frames();Check(condition(),label);
    }
    private static IEnumerable<T> Nodes<T>(Node root) where T:Node
    {
        if(root is T candidate)yield return candidate;
        foreach(var child in root.GetChildren(includeInternal:true))foreach(var next in Nodes<T>(child))yield return next;
    }
    private static Button FindButton(Node root,string text)=>Nodes<Button>(root).First(button=>button.Text==text);
    private Vector2 Screen(Control control,Vector2 local)=>GetViewport().GetFinalTransform()*(control.GetGlobalTransformWithCanvas()*local);
    private Vector2 OnBoard(float x,float y)=>Screen(_board,new(x*_board.CellSize,y*_board.CellSize));
    private Vector2 Outside=>Screen(_hud,new(150,650));
    private void Motion(Vector2 position,bool dragging=false)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position=position,GlobalPosition=position,Relative=position-_pointer,
            ButtonMask=dragging?MouseButtonMask.Left:0 });_pointer=position;
        Input.FlushBufferedEvents();
    }
    private void MouseButton(MouseButton button,bool pressed)
    {
        Input.ParseInputEvent(new InputEventMouseButton { Position=_pointer,GlobalPosition=_pointer,ButtonIndex=button,Pressed=pressed });
    }
    private async Task Move(Vector2 position,bool dragging=false)
    {
        // 真窗口的原生命中仍读取系统指针；等移动消息落定后再发送本次事件。
        if(DisplayServer.GetName()!="headless")
        {
            GetViewport().WarpMouse(GetViewport().GetFinalTransform().AffineInverse()*position);
            await Frames(3);
        }
        Motion(position,dragging);await Frames(3);
    }
    private async Task Click(Vector2 position)
    { await Move(position);MouseButton(Godot.MouseButton.Left,true);await Frames();MouseButton(Godot.MouseButton.Left,false);await Frames(3); }
    private async Task BeginDrag(Vector2 start,string name)
    {
        await Move(start);MouseButton(Godot.MouseButton.Left,true);await Frames(2);
        await Move(start+new Vector2(18,0),true);
        await Until(()=>_board.DragActive&&GetViewport().GuiIsDragging(),name+"开始原生拖拽",5);
    }
    private async Task EndDrag(Vector2 position)
    {
        await Move(position,true);
        MouseButton(Godot.MouseButton.Left,false);
        await Until(()=>!_board.DragActive&&!GetViewport().GuiIsDragging(),"释放后拖拽会话结束",5);await Frames(2);
    }
    private async Task RotateKey()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode=Key.R,PhysicalKeycode=Key.R,Pressed=true });await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode=Key.R,PhysicalKeycode=Key.R,Pressed=false });await Frames(2);
    }
    private async Task RotateMouse()
    {
        MouseButton(Godot.MouseButton.Right,true);await Frames(2);MouseButton(Godot.MouseButton.Right,false);await Frames(2);
    }
    private async Task<BattleBoardSource> Source(string key,string category="招式")
    {
        _hud.SelectCategory(category);await Frames(3);
        var source=Nodes<BattleBoardSource>(_hud).Single(item=>item.SourceKey==key);
        _hud.GetNode<ScrollContainer>("Editor/SourcePanel/SourceScroll").EnsureControlVisible(source);await Frames(4);return source;
    }
    private Vector2 SourcePoint(BattleBoardSource source)
    {
        var sample=source.GetChildren().OfType<BattleBoardView>().Single();
        return Screen(source,sample.Position+Vector2.One*(sample.CellSize*.5f));
    }
    private (int Stamina,int Mana,int Occupancy,int Pills) Resources()=>
        (_owner.current_stamina,_owner.current_mana,_owner.used_spirit_perception,
        _owner.get_inventory_total_count("res://resources/entities/items/alchemy/huichun_1.tres"));

    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(_output+name+".png")==Error.Ok,"原生画面 "+name);_captures.Add(name+".png");
    }
    private async Task<Image?> BoardRegion(Rect2 local)
    {
        if(DisplayServer.GetName()=="headless")return null;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var capture=GetViewport().GetTexture().GetImage();
        var rect=_board.GetGlobalTransformWithCanvas()*local;
        var scale=capture.GetSize()/GetViewport().GetVisibleRect().Size;
        var pixels=new Rect2I((Vector2I)(rect.Position*scale),(Vector2I)(rect.Size*scale)).Grow(-5);
        var image=capture.GetRegion(pixels);image.Convert(Image.Format.Rgba8);return image;
    }
    private static int ChangedPixels(Image before,Image after)
    {
        if(before.GetSize()!=after.GetSize())return 0;
        var a=before.GetData();var b=after.GetData();var changed=0;
        for(var index=0;index<a.Length;index+=4)
            if(Math.Abs(a[index]-b[index])+Math.Abs(a[index+1]-b[index+1])+Math.Abs(a[index+2]-b[index+2])>36)changed++;
        return changed;
    }

    public override void _Ready() { ProcessMode=ProcessModeEnum.Always;CallDeferred(nameof(Run)); }
    private async Task Setup()
    {
        var argument=OS.GetCmdlineUserArgs().FirstOrDefault(value=>value.StartsWith("--interaction-size=",StringComparison.Ordinal));
        if(argument is not null)
        {
            var pieces=argument.Split('=')[1].Split('x');_windowSize=new(int.Parse(pieces[0]),int.Parse(pieces[1]));
        }
        _output=Evidence+$"{_windowSize.X}x{_windowSize.Y}/";
        var outputArgument=OS.GetCmdlineUserArgs().FirstOrDefault(value=>value.StartsWith("--interaction-output=",StringComparison.Ordinal));
        if(outputArgument is not null)_output=outputArgument["--interaction-output=".Length..].TrimEnd('/')+"/";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_output));
        var framesArgument=OS.GetCmdlineUserArgs().FirstOrDefault(value=>value.StartsWith("--traversal-frames=",StringComparison.Ordinal));
        if(framesArgument is not null)
        {
            _traversalFrames=framesArgument["--traversal-frames=".Length..].TrimEnd('/');
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_traversalFrames));
        }
        GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().ContentScaleSize=new(1920,1080);GetWindow().Size=_windowSize;
        var entry=GD.Load<PackedScene>(CombatBoardTestEntry.ScenePath).Instantiate<CombatBoardTestEntry>();
        GetTree().Root.AddChild(entry);GetTree().CurrentScene=entry;await Frames(4);
        GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().Size=_windowSize;await Frames(3);entry.BeginTrial();
        var transition=GetNode<BattleTransitionManager>("/root/BattleTransitionManager");
        await Until(()=>transition.has_open_flow_view(),"独立试演沿真实战前入口打开");
        FindButton(transition,"开始战斗").EmitSignal(BaseButton.SignalName.Pressed);
        await Until(()=>GetTree().CurrentScene is BattleScene&&!transition.is_flow_transition_active(),"真实人类战斗场景就绪");
        _manager=GetTree().CurrentScene.GetNode<BattleManager>("BattleMgr");
        _hud=Nodes<BattleBoardHud>(GetTree().CurrentScene).Single();_board=_hud.GetNode<BattleBoardView>("Editor/Board");
        _mini=_hud.GetNode<BattleBoardView>("MiniBoard");_owner=(Character)_manager.get_main_player_unit()!;
        _catalog=_manager.get_board_catalog(_owner)!;
        _hover=GetNode<Control>("/root/HoverInfoManager/SharedHoverInfoCard");
        _painKey=_catalog.skills.Single(skill=>skill.skill_name=="痛击").get_board_skill_key();
        _normalKey=_catalog.skills.Single(skill=>skill.skill_name=="应敌").get_board_skill_key();
        if(DisplayServer.GetName()!="headless")GetWindow().GrabFocus();
        await Frames(5);
        Check(_manager.is_board_editing()&&_hud.EditorVisible,"编辑停在合法阶段");
        Check(_catalog.FindSkill(_painKey)?.board_size==new Vector2I(2,1)&&_catalog.FindSkill(_painKey)?.board_rotation_allowed==true,"使用正式可旋转双格痛击");
    }
    private async Task CheckNavigation()
    {
        Check(!Nodes<Button>(_hud).Any(button=>button.Text is "旋转 R" or "移出" or "卸下" or "补药"),"移除三个常驻旋转移出补药按钮");
        Check(_hud.GetNodeOrNull<Button>("ReturnToTrial") is null,"顶栏没有重复的旧返回按钮");
        var toggle=_hud.GetNode<Button>("BoardModeToggle");var position=toggle.Position;
        Check(toggle.Text.StartsWith("返回",StringComparison.Ordinal),"编辑时共享导航显示返回");
        var count=_hud.Draft.placements.Count;await Click(Screen(toggle,toggle.Size/2));
        Check(!_hud.EditorVisible&&_manager.is_board_editing()&&_hud.Draft.placements.Count==count,"返回只隐藏配招并保留草稿");
        Check(toggle.Text=="配招"&&toggle.Position==position,"战场查看时同位置显示配招");
        await Click(Screen(toggle,toggle.Size/2));Check(_hud.EditorVisible&&toggle.Position==position,"同一位置重新打开配招");
        var panel=_hud.GetNode<Control>("Editor/SourcePanel");
        Check(panel.Size.X<476,"右侧来源栏收窄");
        Check(Nodes<Button>(panel.GetNode("Categories")).All(button=>button.CustomMinimumSize.Y<=34),"分类按钮采用紧凑高度");
    }
    private async Task PrepareLayout()
    {
        _hud.Draft.placements=new(_hud.Draft.placements.Where(item=>item.locked).Select(item=>item.CopyConfiguration()));
        Check(_hud.TryPlaceSource(_painKey,new(0,0)),"准备横双格原招式");
        Check(_hud.TryPlaceSource(_normalKey,new(2,0)),"准备碰撞对象");
        Check(_hud.Draft.placements.Count(item=>item.locked)==1,"保留心法必需的周天指示");
        _board.Selected="";_board.QueueRedraw();HoverInfoManager.HideAll();await Frames(3);
        await Capture("01_整份占形边框与紧凑来源");
    }
    private async Task CheckUnifiedEditor()
    {
        var panel=_hud.GetNode<Control>("Editor/SourcePanel");var tabs=panel.GetNode<HBoxContainer>("LibraryTabs");
        Check(tabs.GetChildren().OfType<Button>().Select(button=>button.Text).SequenceEqual(new[]{"来源","组合","方案"}),"局内与人物配招共用来源组合方案三栏结构");
        Check(!Nodes<Button>(panel).Any(button=>button.Text.Contains("保存")||button.Text.Contains("删除")||button.Text.Contains("新建")),"局内没有模板保存新建删除操作");
        Check(_board.CellSize<=120&&_board.Size.X<=360&&_board.Size.X>=300,"局内编辑盘保持紧凑并保留实际占形");
        Check(_hud.GetNodeOrNull<BattleBoardHandPreview>("Editor/HandPreview")is not null,"外围手持采用只读同族示意");
        Check(_hud.GetNode<ColorRect>("Editor/Backdrop").Color.A>=.75f,"局内保留战场背景并使用相同深暗幕");
        Check(!_hud.GetChildren().OfType<Button>().Any(button=>button.IsVisibleInTree()&&(button.Text.Contains("倍速")||button.Text=="暂停")),"配招层撤去战场倍速暂停控件");
        var resources=_hud.GetNode<Control>("Resources");var start=_hud.GetNode<Button>("Editor/BeginCombat");
        Check(resources.GetGlobalRect().Position.Y>=start.GetGlobalRect().End.Y+40&&Mathf.Abs(resources.GetGlobalRect().GetCenter().X-_board.GetGlobalRect().GetCenter().X)<1,"真实资源栏位于盘下并与开始按钮留有间距");
        var ordinary=_catalog.skills.First(skill=>skill.skill_name=="架招").get_board_skill_key();
        var combo=new CombatBoardLayout { ResourceName="双格连招",size=new(3,1),placements=new()
        {
            new(){placement_id="template-pain",source_key=_painKey,cell=Vector2I.Zero},
            new(){placement_id="template-normal",source_key=_normalKey,cell=new(2,0)}
        }};
        var library=new CombatBoardPresetLibrary();library.combinations.Add(combo);library.layouts.Add(_hud.Draft.CopyConfiguration());library.layouts[0].Title="已保存整盘";
        // 正式人物库随出战快照冻结；本回归仅在隔离替身上准备相同夹具。
        ((HumanData)_owner.entity_data!).combat_board_presets=library.CopyConfiguration();
        const string libraryPath="user://combat_board_presets.tres";
        Check(ResourceSaver.Save(library,libraryPath)==Error.Ok,"仅在隔离用户目录准备真实来源模板");
        var bytes=System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(libraryPath));
        FindButton(_hud,"组合").EmitSignal(BaseButton.SignalName.Pressed);await Frames(4);
        Check(panel.GetNode<Control>("CombinationPage").Visible&&!panel.GetNode<Control>("Categories").Visible,"组合页在同一纸卷中切换且隐藏来源分类");
        var source=Nodes<BattleBoardCombinationSource>(panel).Single();
        Check(source.Layout.placements.Count==2&&source.Layout.ResourceName=="双格连招","组合读取已保存相对布局而不注入样稿假数据");
        var locked=_hud.Draft.placements.Single(item=>item.locked).CopyConfiguration();_hud.Draft.placements=new(){locked};_board.QueueRedraw();
        var originalResources=Resources();
        await BeginDrag(Screen(source,new(30,30)),"组合整体");await Move(OnBoard(.5f,.5f),true);
        Check(_board.PreviewValid&&_board.PreviewCell==Vector2I.Zero,"组合拖入以完整多物占形统一校验");
        await RotateMouse();await Move(OnBoard(.5f,.5f),true);
        if(_board.DragTurns!=1||!_board.PreviewValid)GD.Print($"COMBO_DRAG_DIAGNOSTIC turns={_board.DragTurns} cell={_board.PreviewCell} valid={_board.PreviewValid} active={_board.DragActive} pointer={_pointer}");
        Check(_board.DragTurns==1&&_board.PreviewValid,"局内组合支持拖动中右键整体转向");
        await Capture("11_组合整组转向预览");await EndDrag(OnBoard(.5f,.5f));
        Check(_hud.Draft.placements.Count==3&&_hud.Draft.placements.Single(p=>p.source_key==_painKey).quarter_turns==1,"合法松手一次提交整组并保留各自朝向");
        Check(_hud.Draft.placements.Select(p=>p.placement_id).Distinct().Count()==3&&!_hud.Draft.placements.Any(p=>p.placement_id.StartsWith("template-",StringComparison.Ordinal)),"组合新增使用独立身份且不复用模板编号");
        var before=JsonSerializer.Serialize(_hud.Draft.placements.Select(p=>new{p.placement_id,p.source_key,p.cell,p.quarter_turns}));
        Check(!_hud.TryInsertCombination(combo,new(2,2))&&before==JsonSerializer.Serialize(_hud.Draft.placements.Select(p=>new{p.placement_id,p.source_key,p.cell,p.quarter_turns})),"越界组合整组拒绝没有部分插入");
        var unavailable=new CombatBoardLayout { placements=new(){new(){placement_id="missing",source_key="unavailable-source"}} };
        Check(!_hud.TryInsertCombination(unavailable,Vector2I.Zero),"当前角色缺少全部组合来源时也不崩溃或写入");
        _hud.Draft.placements=new(){locked.CopyConfiguration()};Check(_hud.MovePlacement(locked.placement_id,new(2,2)),"检查组合时保留且移动必需指示物");
        var bottles=new CombatBoardLayout { placements=new(Enumerable.Range(0,3).Select(x=>new CombatBoardPlacement
        { placement_id="pill-"+x,source_key="res://resources/entities/items/alchemy/huichun_1.tres",kind=CombatBoardPlacement.ObjectKind.Pill,cell=new(x,0) })) };
        Check(!_hud.TryInsertCombination(bottles,Vector2I.Zero)&&_hud.Draft.placements.Count==1,"缺少真实库存时整组丹药不写入");
        Check(_hud.TryPlaceSource(ordinary,new(1,0)),"准备位于组合空隙内的已有招式");
        var gap=new CombatBoardLayout { placements=new()
        {
            new(){placement_id="left",source_key=_normalKey,cell=Vector2I.Zero},
            new(){placement_id="right",source_key=_normalKey,cell=new(2,0)}
        }};
        Check(_hud.TryInsertCombination(gap,Vector2I.Zero)&&_hud.Draft.placements.Count==4,"组合外接框中的空隙不预留不覆盖已有招式");
        Check(Resources()==originalResources&&_manager.get_board_round()==0,"组合载入不改变资源库存或回合");
        FindButton(_hud,"方案").EmitSignal(BaseButton.SignalName.Pressed);await Frames(3);
        Check(panel.GetNode<Control>("PlanPage").Visible&&Nodes<Button>(panel.GetNode("PlanPage")).Count()==2,"方案页展示入场配置与实际已保存整盘");
        await Capture("12_统一方案载入页");
        var plan=Nodes<Button>(panel.GetNode("PlanPage")).First();plan.EmitSignal(BaseButton.SignalName.Pressed);await Frames(3);
        Check(!panel.GetNode<Control>("PlanPage").Visible&&_hud.Draft.placements.Any(p=>p.locked),"载入整盘回来源并保留必需指示物");
        Check(bytes.SequenceEqual(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(libraryPath))),"局内仅载入且不改写组合方案库");
    }
    private async Task CheckSourceDrag()
    {
        var source=await Source(_painKey);await Move(SourcePoint(source));
        await ToSignal(GetTree().CreateTimer(.22),SceneTreeTimer.SignalName.Timeout);await Frames(3);
        Check(_hover.Visible,"来源悬停可正常显示");
        Check(Nodes<Control>(_hover).All(control=>control.MouseFilter==Control.MouseFilterEnum.Ignore),"悬停及其所有子控件不拦截鼠标");
        var oldIds=_hud.Draft.placements.Select(item=>item.placement_id).ToHashSet();
        await BeginDrag(SourcePoint(source),"来源招式");
        Check(_board.HiddenPlacementId.Length==0,"来源拖动不隐藏盘内其他同名招式");
        Check(!_hover.Visible&&HoverInfoManager.IsDragInProgress(),"原生拖起立即收起信息栏");
        await Move(OnBoard(.5f,1.5f),true);
        Check(_board.PreviewCell==new Vector2I(0,1)&&_board.PreviewValid,"来源进入盘内显示合法落点预览");
        var turn=_board.DragTurns;await RotateKey();
        Check(_board.DragActive&&_board.DragTurns!=turn,"拖拽中按R旋转候选");
        await Move(OnBoard(.5f,1.5f),true);
        Check(_board.PreviewCell==new Vector2I(0,1)&&_board.PreviewValid,"旋转后按完整新占形检查落点");
        await ToSignal(GetTree().CreateTimer(.16),SceneTreeTimer.SignalName.Timeout);await Frames(2);
        Check(!_hover.Visible,"拖拽停留时不重开信息栏");
        Check(!Nodes<BattleBoardTooltip>(GetTree().Root).Any(control=>control.IsVisibleInTree()),"来源拖动不生成旧原生空白提示框");
        await Capture("02_来源转向与合法落点预览");await EndDrag(OnBoard(.5f,1.5f));
        var added=_hud.Draft.placements.Single(item=>!oldIds.Contains(item.placement_id));
        Check(added.source_key==_painKey&&added.cell==new Vector2I(0,1)&&added.quarter_turns%2==1,"释放实际提交转向后的竖双格");
        Check(_hud.EditorVisible&&!HoverInfoManager.IsDragInProgress(),"释放后编辑页保持且全局拖拽抑制结束");
    }
    private async Task CheckBoardTransactions()
    {
        var original=_hud.Draft.placements.Single(item=>item.source_key==_painKey&&item.cell==Vector2I.Zero);
        var originalId=original.placement_id;
        HoverInfoManager.HideAll();using var before=await BoardRegion(new Rect2(Vector2.Zero,new Vector2(_board.CellSize*2,_board.CellSize)));
        await BeginDrag(OnBoard(1.5f,.5f),"原招式第二占格");
        await Move(OnBoard(2.5f,1.5f),true);
        Check(_board.HiddenPlacementId==originalId&&_hud.Draft.placements.Any(item=>item.placement_id==originalId&&item.cell==Vector2I.Zero),"拖起原物隐去显示但事务提交前保留原布局");
        Check(_board.PreviewCell==new Vector2I(1,1)&&_board.PreviewValid,"第二格抓取保留相对锚点不跳左上角");
        using var after=await BoardRegion(new Rect2(Vector2.Zero,new Vector2(_board.CellSize*2,_board.CellSize)));
        if(before is not null&&after is not null)Check(ChangedPixels(before,after)>250,"原生图像确认原位内部图标已隐去");
        await Capture("03_拖动物整体隐藏原位置");
        var turn=_board.DragTurns;await RotateMouse();
        Check(_board.DragActive&&_board.DragTurns!=turn&&_hud.EditorVisible,"拖拽中右键旋转且不触发返回");
        await Move(OnBoard(1.5f,2.5f),true);
        Check(_board.PreviewCell==new Vector2I(1,1)&&_board.PreviewValid,"右键旋转后鼠标仍对应抓取位置");
        await Capture("04_右键转向后的落点");
        Check(!_hover.Visible&&!Nodes<BattleBoardTooltip>(GetTree().Root).Any(control=>control.IsVisibleInTree()),"盘内拖动与右键转向均没有信息栏或空白旧框");
        await EndDrag(OnBoard(1.5f,2.5f));
        original=_hud.Draft.placements.Single(item=>item.placement_id==originalId);
        Check(original.cell==new Vector2I(1,1)&&original.quarter_turns%2==1,"移动提交保留原身份与新方向");
        var oldCell=original.cell;var oldTurn=original.quarter_turns;
        await BeginDrag(OnBoard(1.5f,1.5f),"盘内非法落点");await Move(OnBoard(2.5f,.5f),true);
        Check(_board.PreviewCell.HasValue&&!_board.PreviewValid,"盘内冲突落点显示非法预览");
        await Capture("05_非法落点预览");await EndDrag(OnBoard(2.5f,.5f));
        original=_hud.Draft.placements.Single(item=>item.placement_id==originalId);
        Check(original.cell==oldCell&&original.quarter_turns==oldTurn,"盘内无效释放完整还原而不卸下");
        var locked=_hud.Draft.placements.Single(item=>item.locked);var lockedCell=locked.cell;var lockedDirection=locked.rewrite_direction;
        await BeginDrag(OnBoard(locked.cell.X+.5f,locked.cell.Y+.5f),"锁定周天指示");await RotateKey();await Move(Outside,true);
        Check(!_board.PreviewCell.HasValue,"盘外预览不伪装成可落点");
        await EndDrag(Outside);locked=_hud.Draft.placements.Single(item=>item.locked);
        Check(locked.cell==lockedCell&&locked.rewrite_direction==lockedDirection,"锁定对象拖出连同候选转向回原位");
        var removable=_hud.Draft.placements.Single(item=>item.source_key==_painKey&&item.placement_id!=originalId);
        await BeginDrag(OnBoard(removable.cell.X+.5f,removable.cell.Y+.5f),"普通对象拖出");await EndDrag(Outside);
        Check(!_hud.Draft.placements.Any(item=>item.placement_id==removable.placement_id)&&_hud.Draft.placements.Any(item=>item.placement_id==originalId),"拖出只卸下目标份不影响同名另一份");
        Check(_hud.EditorVisible,"拖出取消装配不会误关暗幕");
        var count=_hud.Draft.placements.Count;var source=await Source(_painKey);
        await BeginDrag(SourcePoint(source),"来源拖出取消");await EndDrag(Outside);
        Check(_hud.Draft.placements.Count==count,"来源盘外释放只取消不新增");
        await Click(OnBoard(.5f,.5f));
        Check(_hud.Draft.placements.Count==count,"来源取消后没有残留点击待摆状态");
        await Capture("06_拖出卸下后的有效布局");
    }
    private async Task CheckTraversal()
    {
        var toggle=_hud.GetNode<Button>("BoardModeToggle");
        var painId=_hud.Draft.placements.Single(item=>item.source_key==_painKey).placement_id;
        var editorPosition=_board.Position;var editorCell=_board.CellSize;
        _recordTraversal=true;
        Check(_manager.commit_player_board(_hud.Draft,out var reason),"提交真实战斗 "+reason);
        _manager.toggle_battle_pause();await Frames(2);
        Check(!_hud.EditorVisible&&_mini.Visible&&toggle.Disabled,"执行时小盘可见且配招导航禁用");
        Check(_mini.Position.IsEqualApprox(editorPosition)&&Mathf.IsEqualApprox(_mini.CellSize,editorCell),"战中小盘从中央配置盘的原位置和大小开始缩移");
        Check(ReferenceEquals(_mini.Layout,_manager.get_board_state(_owner)!.Layout),"战中小盘显示实际已提交布局");
        _hud.OpenEditor();Check(!_hud.EditorVisible,"普通暂停不能开放编辑");
        var pausedPosition=_mini.Position;var pausedCell=_mini.CellSize;
        await Frames(4);
        Check(_mini.Position.IsEqualApprox(pausedPosition)&&Mathf.IsEqualApprox(_mini.CellSize,pausedCell),"暂停冻结盘面缩移");
        _manager.toggle_battle_pause();await Frames(2);
        Check(_mini.CellSize<editorCell&&_mini.Position.X<editorPosition.X&&_mini.Position.Y>editorPosition.Y,"开战后盘面一边缩小一边向左下移动");
        await Until(()=>_mini.CellSize<=64.01f,"缩移在展示时间内完成",2);
        var miniBounds=new Rect2(_mini.Position,_mini.Size);
        Check(_mini.Position.X<48&&miniBounds.End.Y>BattleUiArt.Canvas.Y-48&&miniBounds.End.Y<BattleUiArt.Canvas.Y,"战中盘停靠左下并保留完整边缘");
        Check(_mini.Size.X<_board.Size.X&&_mini.Size.Y<_board.Size.Y&&!miniBounds.Intersects(_hud.GetNode<Control>("Resources").GetRect()),"战中盘缩小且不遮挡底部资源栏");
        await Capture("07a_战中左下小盘");
        _manager.cycle_battle_speed();_manager.cycle_battle_speed();
        var sawObject=false;var sawEmpty=false;var sawElastic=false;var exclusive=true;var sampledObject=false;var sampledEmpty=false;
        var until=Time.GetTicksMsec()+90000;
        while(!_manager.is_board_editing()&&_manager.battle_running&&Time.GetTicksMsec()<until)
        {
            await Frames();
            var item=_mini.FeedbackPlacementId;var cell=_mini.FeedbackCell;
            exclusive&=item.Length==0||!cell.HasValue;
            if(item.Length>0)
            {
                sawObject=true;sawElastic|=_mini.FeedbackScale>1.005f;
                if(!sampledObject&&_mini.FeedbackScale>1.025f)
                {
                    sampledObject=true;_manager.toggle_battle_pause();
                    var frozenItem=_mini.FeedbackPlacementId;var frozenScale=_mini.FeedbackScale;
                    var frozenSerial=_manager.get_board_state(_owner)!.VisitSerial;var frozenResources=Resources();
                    await Frames(4);
                    Check(_mini.FeedbackPlacementId==frozenItem&&Mathf.IsEqualApprox(_mini.FeedbackScale,frozenScale),"暂停冻结当前整物回弹");
                    Check(_manager.get_board_state(_owner)!.VisitSerial==frozenSerial&&Resources()==frozenResources,"暂停展示不额外遍历或支付资源");
                    await Capture("07_遍历整份对象弹性反馈");_manager.toggle_battle_pause();
                }
            }
            if(cell.HasValue)
            {
                sawEmpty=true;
                if(!sampledEmpty) { sampledEmpty=true;await Capture("08_仅空格的遍历反馈"); }
            }
        }
        _recordTraversal=false;
        Check(_manager.battle_running&&_manager.is_board_editing()&&_hud.EditorVisible,"全员完成后开放共同回合整理");
        Check(sawObject&&sawEmpty,"真实扫描分别产生对象与空格反馈");
        Check(exclusive,"有物格只反馈整物而不同时亮格");
        Check(sawElastic,"读取整份对象时出现轻微放大回弹");
        var state=_manager.get_board_state(_owner)!;
        var expectedFeedbacks=(int)(state.Layout.size.X*state.Layout.size.Y)
            -state.Layout.placements.Sum(item=>Math.Max(0,CombatBoardGeometry.Cells(item,CombatBoardGeometry.Footprint(item,_catalog.FindSkill)).Count()-1));
        Check(state.VisitFeedbackEvents.Count==expectedFeedbacks&&state.VisitSerial>=9,"空格和招式起始格有遍历反馈，延伸占格不单独反馈");
        Check(state.HasSucceeded(painId),"演出反馈保留真实招式结算");
        Check(_manager.get_board_round()==1&&_owner.current_stamina<=_owner.max_stamina,"显示反馈不新增回合或突破回体上限");
        await Capture("09_回合末整理");
    }
    private async Task CheckPillReplacement()
    {
        const string key="res://resources/entities/items/alchemy/huichun_1.tres";
        Check(_hud.TryPlaceSource(key,new(0,1)),"准备实存丹药的盘内占形");
        var bottle=_hud.Draft.placements.Single(item=>item.kind==CombatBoardPlacement.ObjectKind.Pill);
        // 只构造隔离空瓶状态；实际服药与消耗由既有人类效果专项覆盖。
        bottle.expended=true;var id=bottle.placement_id;_board.QueueRedraw();
        var count=_owner.get_inventory_total_count(key);var source=await Source(key,"丹药");
        await BeginDrag(SourcePoint(source),"同种丹药补空瓶");await Move(OnBoard(.5f,1.5f),true);
        Check(_board.PreviewValid,"来源拖到同占形空瓶显示可补药预览");
        await Capture("10_拖放同种丹药补空瓶");await EndDrag(OnBoard(.5f,1.5f));
        bottle=_hud.Draft.placements.Single(item=>item.kind==CombatBoardPlacement.ObjectKind.Pill);
        Check(bottle.placement_id==id&&!bottle.expended,"直接拖药补入保留原空瓶身份");
        Check(_owner.get_inventory_total_count(key)==count,"补入只预留实际服药前不扣库存");
        await BeginDrag(OnBoard(.5f,1.5f),"移出补入丹药");await EndDrag(Outside);
        Check(!_hud.Draft.placements.Any(item=>item.kind==CombatBoardPlacement.ObjectKind.Pill)&&_owner.get_inventory_total_count(key)==count,"拖出未服丹药释放预留不生成或扣除物品");
        _hud.SelectCategory("招式");await Frames(2);
    }
    private async void Run()
    {
        try
        {
            var save=GetNode<GameSaveManager>("/root/GameSaveManager");var originalSlot=save.active_slot_id;
            await Setup();await CheckNavigation();
            if(OS.GetCmdlineUserArgs().Contains("--unified-editor-checks"))await CheckUnifiedEditor();
            await PrepareLayout();var before=Resources();
            await CheckSourceDrag();await CheckBoardTransactions();await CheckPillReplacement();
            Check(Resources()==before&&_manager.get_board_round()==0,"配招拖放不改变资源库存或推进回合");
            var layoutOnly=OS.GetCmdlineUserArgs().Contains("--layout-only");
            if(!layoutOnly)await CheckTraversal();
            Check(save.active_slot_id==originalSlot,"隔离验收不切换正式旅程");
            HoverInfoManager.HideAll();_manager.battle_running=false;
            GetNode<BattleTransitionManager>("/root/BattleTransitionManager").abandon_battle_for_title();
            GetTree().CurrentScene.QueueFree();await Frames(5);
            var report=new { passed=true,checks=_checks.Count,layout_only=layoutOnly,size=new[]{_windowSize.X,_windowSize.Y},engine=Engine.GetVersionInfo()["string"].AsString(),renderer=DisplayServer.GetName(),captures=_captures,items=_checks };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(_output+"验收.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
            GD.Print("COMBAT_BOARD_INTERACTION_OK checks="+_checks.Count);
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Frames(2);GetTree().Quit();
        }
        catch(Exception error)
        {
            GD.PushError("COMBAT_BOARD_INTERACTION_FAILED "+error);GetTree().Paused=false;GetTree().Quit(1);
        }
    }
}
