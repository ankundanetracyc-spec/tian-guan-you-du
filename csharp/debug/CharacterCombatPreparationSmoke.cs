using Godot;
using System.Reflection;
using System.Text.Json;

/// <summary>正式人物页、人物存档快照与局内配招的隔离集成验收。</summary>
public partial class CharacterCombatPreparationSmoke : Node
{
    private readonly List<string> _checks=[];
    private readonly List<string> _captures=[];
    private string _output="";
    private Character _actor=null!;
    private InfoPanel _panel=null!;
    private int _width;
    private Vector2 _pointer;
    private const string Pill="res://resources/entities/items/alchemy/huichun_1.tres";
    private void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);_checks.Add(text);GD.Print("PREPARATION_PASS "+text);}
    private async Task Frames(int n=3){for(var i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private static IEnumerable<T> Nodes<T>(Node root)where T:Node
    {if(root is T value)yield return value;foreach(var child in root.GetChildren())foreach(var next in Nodes<T>(child))yield return next;}
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
    private static Button Button(Node node,string name)=>Nodes<Button>(node).Single(b=>b.Name==name);
    private async Task Click(Control target)
    {
        var p=GetViewport().GetFinalTransform()*(target.GetGlobalTransformWithCanvas()*(target.Size/2));
        GetViewport().WarpMouse(GetViewport().GetFinalTransform().AffineInverse()*p);await Frames();
        Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p,Relative=p-_pointer});_pointer=p;await Frames();
        Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});await Frames();
    }
    private Vector2 Point(Control node,Vector2 local)=>GetViewport().GetFinalTransform()*(node.GetGlobalTransformWithCanvas()*local);
    private async Task Move(Vector2 p,bool dragging=false)
    {
        GetViewport().WarpMouse(GetViewport().GetFinalTransform().AffineInverse()*p);await Frames();
        Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p,Relative=p-_pointer,ButtonMask=dragging?MouseButtonMask.Left:0});_pointer=p;await Frames();
    }
    private async Task ClickPoint(Vector2 p)
    {
        await Move(p);Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});await Frames();
    }
    private async Task Drag(Control source,Vector2 target,BattleBoardView board)
    {
        var p=Point(source,new(30,30));await Move(p);
        Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});await Frames(2);await Move(p+new Vector2(22,0),true);
        Check(board.DragActive,"人物组合来源进入原生拖拽");await Move(target,true);
        Input.ParseInputEvent(new InputEventMouseButton{Position=target,GlobalPosition=target,ButtonIndex=MouseButton.Left,Pressed=false});await Frames(6);
        Check(!board.DragActive,"人物组合松手结束拖拽会话");
    }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        HoverInfoManager.DismissVisible();await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();Check(image.SavePng(_output+name+".png")==Error.Ok,"实机截图 "+name);_captures.Add(name+".png");
    }
    private CombatBoardLayout Layout(params (string Key,int X,int Y)[] items)
    {
        var catalog=CombatBoardPreparationService.Catalog(_actor);var result=new CombatBoardLayout();
        foreach(var item in items)result.placements.Add(CombatBoardPreparationService.CreatePlacement(catalog,item.Key,new(item.X,item.Y))!);return result;
    }
    private HumanData Human=>(HumanData)_actor.entity_data!;
    private string Stamp()=>JsonSerializer.Serialize(new{_actor.current_health,_actor.current_stamina,_actor.current_mana,_actor.current_world_stamina,
        items=_actor.pack_items.Select(p=>new{p,count=_actor.get_inventory_total_count(p)}).ToArray(),time=GetNode<GameTimeMgr>("/root/GameTimeManager").get_total_game_hours()});
    private async Task NameDialog(string name,bool cancel=false)
    {
        var dialog=Nodes<CombatBoardNameDialog>(_panel).Single();dialog.NameInput.Text=name;await Frames();
        // 弹出窗口独占输入，触发它的原生按钮信号，排版另验窗口内矩形。
        Button(dialog,cancel?"CancelNameDialog":"ConfirmPresetName").EmitSignal(BaseButton.SignalName.Pressed);await Frames(5);
    }
    public string get_profile_location_for(Character actor)=>"独立验收";
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;CallDeferred(nameof(Run));}
    private async void Run()
    {
        var passed=false;string failure="";
        try
        {
            var user=ProjectSettings.GlobalizePath("user://");var outputRoot=ProjectSettings.GlobalizePath("res://output/");
            Check(System.IO.Path.GetFullPath(user).StartsWith(System.IO.Path.GetFullPath(outputRoot),StringComparison.OrdinalIgnoreCase),"验收使用隔离存档目录");
            _width=OS.GetCmdlineUserArgs().Contains("--wide")?1920:1280;
            _output=$"res://docs/美术规范/人物战斗正式实装_20261109/{_width}/";DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_output));
            GetWindow().Mode=Window.ModeEnum.Windowed;GetWindow().ContentScaleSize=Vector2I.Zero;GetWindow().Size=new(_width,_width*9/16);GetWindow().GrabFocus();
            GetNode<GameTimeMgr>("/root/GameTimeManager").pause_time();
            var books=GongfaAvailability.EnabledBooks().Where(b=>new[]{"玄枢含章经","斫基损契玄经","太和感物养元宝册","舒卷天章"}.Contains(b.gongfa_name)).ToArray();
            var data=NpcCombatBuildPlanner.TestLearning(books);data.data_initialized=true;data.entity_name="苏醒者";data.current_age=20;
            data.main_gongfa_path=books.Single(b=>b.gongfa_name=="斫基损契玄经").ResourcePath;
            data.max_sub_slots=1;data.sub_gongfa_paths=[books.Single(b=>b.gongfa_name=="舒卷天章").ResourcePath];
            data.inventory_item_paths=[Pill];data.inventory_item_counts=[2];data.inventory_item_positions=[0];
            data.combat_board_layout=new();
            _actor=new Character();AddChild(_actor);_actor.Hide();_actor.load_from_data(data);await Frames();
            var catalog=CombatBoardPreparationService.Catalog(_actor);
            var pain=catalog.skills.Single(s=>s.skill_name=="痛击").get_board_skill_key();var normal=catalog.skills.Single(s=>s.skill_name=="应敌").get_board_skill_key();
            var baseline=Layout((pain,0,0),(normal,0,2));var stamp=Stamp();
            Check(CombatBoardPreparationService.Validate(_actor,baseline).Count==0,"正式已学来源可组成合法盘");
            Check(!CombatBoardPreparationService.TrySave(_actor,baseline," ",false,false,out _,out _),"空名称拒绝保存");
            Check(Human.combat_board_presets is null,"取消或无效输入不建立人物库");
            Check(CombatBoardPreparationService.TrySave(_actor,baseline,"养锋连剑",false,false,out var id,out _),"方案按整盘保存到本人数据");
            Check(Human.combat_board_preparation is null,"保存方案不等于应用开局");
            Check(!CombatBoardPreparationService.TrySave(_actor,baseline,"养锋连剑",false,false,out _,out _),"同名保存必须明确覆盖");
            Check(CombatBoardPreparationService.TrySave(_actor,baseline,"养锋连剑",false,true,out var stable,out _)&&id==stable,"覆盖保留稳定编号");
            var named=CombatBoardPreparationService.GetLibrary(_actor).layouts.Single();
            Check(CombatBoardPreparationService.TryApply(_actor,named,out _),"应用命名方案成为默认开局");
            named.placements.Clear();Check(Human.combat_board_preparation!.placements.Count==2,"默认开局与读取副本独立");
            Check(CombatBoardPreparationService.TrySave(_actor,Layout((normal,1,1)),"临敌守御",false,false,out var second,out _),"多套方案数量不设固定上限");
            Check(!CombatBoardPreparationService.TryRename(_actor,second,"养锋连剑",false,out _),"改名拒绝重名");
            Check(CombatBoardPreparationService.TryRename(_actor,id,"养锋连剑·正式",false,out _)&&Human.combat_board_preparation.Title=="养锋连剑·正式","已应用方案改名同步标题且保持布局");
            var combo=CombatBoardCombinations.Normalize(baseline,catalog);
            Check(CombatBoardPreparationService.TrySave(_actor,combo,"攻守组合",true,false,out var comboId,out _),"组合保存相对位置和朝向");
            Check(CombatBoardPreparationService.TryRename(_actor,comboId,"攻守相济",true,out _),"组合可独立改名");
            Check(CombatBoardPreparationService.TrySave(_actor,Layout((Pill,0,0)),"服丹备用",false,false,out _,out _),"有库存的丹药可存整盘");
            var before=CombatBoardPreparationService.Signature(Human.combat_board_preparation);var invalid=Layout((Pill,0,0),(Pill,1,0),(Pill,2,0));
            Check(!CombatBoardPreparationService.TryApply(_actor,invalid,out _)&&CombatBoardPreparationService.Signature(Human.combat_board_preparation)==before,"缺库存原子拒绝且不改默认开局");
            invalid=baseline.CopyConfiguration();invalid.placements[0].source_key="not-learned";
            Check(!CombatBoardPreparationService.TryApply(_actor,invalid,out _),"来源缺失不可应用");
            invalid=baseline.CopyConfiguration();invalid.placements[0].expended=true;
            Check(!CombatBoardPreparationService.TrySave(_actor,invalid,"空瓶",false,false,out _,out _),"空瓶符痕不能保存为补货方案");
            Check(stamp==Stamp(),"保存改名和应用不消耗时间库存或战斗资源");
            await CheckPersistence();
            var blank=new Character();AddChild(blank);blank.load_from_data(new HumanData{data_initialized=true});
            var blankEntries=CombatBoardOwnedSources.Read(blank);Check(blankEntries.Count(e=>e.Category=="招式")<catalog.skills.Count,"未学人物不填充全库招式");
            Check(!blankEntries.Any(e=>e.Category is "丹药" or "符箓" or "法宝"),"空行囊不显示示例物品");blank.QueueFree();
            var owned=CombatBoardOwnedSources.Read(_actor);Check(owned.Single(e=>e.Key==Pill).Detail.Contains("2"),"目录读取实际持有数量");
            _panel=GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>();AddChild(_panel);_panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);_panel.show_entity(_actor);await Frames(6);
            var tabs=Field<List<Button>>(_panel,"_characterTabs");await Click(tabs[2]);await Frames(5);
            var page=Nodes<CharacterCombatBoardPage>(_panel).Single();
            Check(page.IsVisibleInTree(),"正式InfoPanel战斗页替换旧十槽装配");
            Check(!Nodes<BattleBoardView>(page).Any(),"人物首页只显示已学持有清单而不显示当前盘");
            Check(Nodes<Button>(page).Count(b=>b.Name.ToString().StartsWith("OwnedCategory"))==5,"五类目录完整");
            Check(!Nodes<Control>(page).Any(n=>n.Name=="CharacterSkillSlots"),"战斗首页不再展示旧技能槽");
            await Capture("人物战斗_招式清单");await Click(Button(page,"OwnedCategory丹药"));await Capture("人物战斗_持有丹药");
            await Click(Button(page,"OpenCharacterPreparation"));await Frames(5);var editor=Nodes<CharacterCombatBoardEditor>(_panel).Single();
            Check(page.IsVisibleInTree()&&editor.IsVisibleInTree(),"人物原页保留并在暗幕上打开全屏配招");
            Check(editor.Draft.placements.Count==2,"进入配招才显示当前应用配置");
            Check(!Nodes<Button>(editor).Any(b=>b.Text is "旋转 R" or "移出" or "多选"),"不设常驻转向移出工具排");
            var contained=true;
            for(var step=0;step<30;step++)
            {
                Input.ParseInputEvent(new InputEventKey{Keycode=Key.Tab,PhysicalKeycode=Key.Tab,Pressed=true});await Frames(1);
                Input.ParseInputEvent(new InputEventKey{Keycode=Key.Tab,PhysicalKeycode=Key.Tab,Pressed=false});await Frames(1);
                var focus=GetViewport().GuiGetFocusOwner();contained&=focus is not null&&editor.IsAncestorOf(focus);
            }
            Check(contained,"全屏配招的键盘焦点循环不进入暗幕后的原页");
            await Capture("人物配招_来源");
            await Click(Button(editor,"SaveBoardAsPlan"));await Frames();var modal=Nodes<CombatBoardNameDialog>(_panel).Single();
            Check(modal.Visible&&modal.Exclusive,"保存为方案打开独占命名窗口");
            Check(Button(modal,"ConfirmPresetName").Text=="确认覆盖","同名命名窗口明确提示确认覆盖");
            modal.NameInput.Text=" ";Button(modal,"ConfirmPresetName").EmitSignal(BaseButton.SignalName.Pressed);await Frames();
            Check(modal.Visible&&Human.combat_board_presets!.layouts.Count==3,"命名窗口拒绝空名且不部分保存");
            await NameDialog("取消保存",true);Check(Human.combat_board_presets!.layouts.Count==3,"取消命名不增加方案");
            await Click(Button(editor,"SaveBoardAsPlan"));await Frames();modal=Nodes<CombatBoardNameDialog>(_panel).Single();
            var inputRect=modal.NameInput.GetRect();Check(inputRect.Size.Y>=48&&modal.NameInput.GetParent().GetChildren().OfType<Label>().All(l=>l.Position.X+l.Size.X<=modal.NameInput.Position.X),"命名标签和输入框分列对齐");
            var footerButtons=Nodes<Button>(modal).Where(b=>b.Name.ToString() is "CancelNameDialog" or "ConfirmPresetName").ToArray();
            Check(footerButtons.Length==2&&Math.Abs(footerButtons[0].Size.Y-footerButtons[1].Size.Y)<1,"命名确认和返回按钮等高");
            await Capture("人物配招_命名窗口");await NameDialog("养锋连剑·正式");
            Check(Human.combat_board_presets!.layouts.Count==3,"命名窗口确认同名覆盖不增加条目");
            Check(Nodes<Button>(editor).Count(b=>b.Name=="ApplySavedPlan")==3&&Nodes<Button>(editor).Count(b=>b.Name=="RenameSavedPlan")==3,"每个方案独立应用与改名");
            foreach(var row in Nodes<PanelContainer>(editor).Where(n=>n.Name=="SavedPlan"))
            {
                var apply=Button(row,"ApplySavedPlan");var rename=Button(row,"RenameSavedPlan");
                Check(apply.Position.X==rename.Position.X&&apply.Position.Y+apply.Size.Y<=rename.Position.Y&&apply.Size.X==rename.Size.X,"方案应用改名竖向排列且等宽");
                Check(Nodes<Label>(row).Any(label=>label.Text.Length>0&&label.GetThemeFontSize("font_size")==23&&label.Size.Y>=label.GetThemeFontSize("font_size")),"方案名称在容器中保留可见行高");
            }
            await Capture("人物配招_方案列表");
            var firstRow=Nodes<PanelContainer>(editor).First(n=>n.Name=="SavedPlan");await Click(Button(firstRow,"RenameSavedPlan"));
            await NameDialog("修订方案名称足够长时仍在纸面与按钮之间自动换行");await Frames();
            Check(Human.combat_board_preparation!.Title.Contains("修订方案"),"行内改名同步已应用方案而不替换招式");await Capture("人物配招_长名称");
            firstRow=Nodes<PanelContainer>(editor).First(n=>n.Name=="SavedPlan");await Click(Button(firstRow,"ApplySavedPlan"));
            Check(editor.Draft.preset_id==id,"方案行应用更新草稿和默认配置");
            await Click(Button(editor,"PreparationTab组合"));await Capture("人物配招_组合列表");
            var selectedBoard=Nodes<BattleBoardView>(editor).Single(b=>b.Editable);
            await ClickPoint(Point(selectedBoard,new(.5f*selectedBoard.CellSize,.5f*selectedBoard.CellSize)));
            Input.ParseInputEvent(new InputEventKey{Keycode=Key.Shift,PhysicalKeycode=Key.Shift,Pressed=true});await Frames(1);
            await ClickPoint(Point(selectedBoard,new(.5f*selectedBoard.CellSize,2.5f*selectedBoard.CellSize)));
            Input.ParseInputEvent(new InputEventKey{Keycode=Key.Shift,PhysicalKeycode=Key.Shift,Pressed=false});await Frames(2);
            Check(selectedBoard.SelectedPlacements!.Count==2,"真实Shift点击可多选盘中对象");
            await Click(Button(editor,"PreparationLibraryFooter"));await Frames();await NameDialog("两招组合");
            Check(Human.combat_board_presets!.combinations.Count==2,"保存选择的组合写入本人库");
            var unchanged=CombatBoardPreparationService.Signature(editor.Draft);
            Check(!editor.InsertCombination(combo,Vector2I.Zero)&&unchanged==CombatBoardPreparationService.Signature(editor.Draft),"重叠组合整组拒绝，不部分上盘");
            await Click(Button(editor,"PreparationTab来源"));await Click(Button(editor,"PreparationLibraryFooter"));
            Check(editor.Draft.placements.Count==0,"局外清盘仅清草稿不改默认应用");
            await Click(Button(editor,"PreparationTab组合"));var comboSource=Nodes<BattleBoardCombinationSource>(editor).First();
            await Drag(comboSource,Point(selectedBoard,Vector2.One*(selectedBoard.CellSize*.5f)),selectedBoard);
            Check(editor.Draft.placements.Count==2&&editor.Draft.placements.All(p=>!baseline.placements.Any(old=>old.placement_id==p.placement_id)),"人物保存的组合整组拖入且生成独立摆放编号");
            await Capture("人物配招_组合整体拖入");
            var delete=Nodes<Button>(comboSource.GetParent()).Single(b=>b.Text=="删除");await Click(delete);
            Check(Human.combat_board_presets.combinations.Count==1,"组合行内删除仅移除所选记录");
            Check(CombatBoardPreparationService.Signature(Human.combat_board_preparation!)==before,"组合编辑不覆盖已应用整盘");
            await Click(Button(editor,"CloseCharacterPreparation"));await Frames(5);
            Check(!Nodes<CharacterCombatBoardEditor>(_panel).Any()&&page.IsVisibleInTree(),"返回关闭暗幕并保持目录页");
            Check(stamp==Stamp(),"全部人物页交互不消耗物品资源或世界时间");
            _panel.Hide();await CheckBattle();
            passed=true;
        }
        catch(Exception error){failure=error.ToString();GD.PushError("CHARACTER_PREPARATION_FAIL "+error);}
        finally
        {
            if(_output.Length>0)System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(_output+"验收.json"),JsonSerializer.Serialize(new{passed,checks=_checks.Count,items=_checks,captures=_captures,failure},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"CHARACTER_PREPARATION_RESULT passed={passed} checks={_checks.Count}");
            foreach(var child in GetChildren())child.QueueFree();_actor=null!;_panel=null!;await Frames(5);
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Frames(2);GetTree().Quit(passed?0:1);
        }
    }
    private async Task CheckPersistence()
    {
        var detached=DetachedResourceSnapshot.Capture(Human);var restored=(HumanData)detached.Materialize();
        Check(restored.combat_board_presets!.layouts.Count==3&&restored.combat_board_presets.combinations.Count==1,"后台人物快照保留组合与全部方案");
        Check(restored.combat_board_presets.layouts[0].Title=="养锋连剑·正式"&&restored.combat_board_presets.layouts[0].preset_id.Length>0,"导出名称与稳定编号不依赖ResourceName");
        var path="user://preparation-roundtrip.tres";Check(ResourceSaver.Save(restored,path)==Error.Ok,"人物资源实际序列化成功");
        var loaded=ResourceLoader.Load<HumanData>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        Check(loaded.combat_board_preparation?.Title=="养锋连剑·正式"&&loaded.combat_board_presets?.layouts.Count==3,"重新加载人物存档恢复默认盘和命名库");
        loaded.combat_board_presets!.layouts[0].placements.Clear();Check(Human.combat_board_presets!.layouts[0].placements.Count==2,"重载副本不引用世界人物配置");
        var old=(HumanData)DetachedResourceSnapshot.Capture(new HumanData()).Materialize();Check(old.combat_board_preparation is null&&old.combat_board_presets is null,"旧档缺少新字段仍可加载");
        var original=Human.combat_board_presets;var legacyPlan=original!.layouts[0].CopyConfiguration();legacyPlan.preset_name="";legacyPlan.ResourceName="旧全局方案";legacyPlan.preset_id="";
        var legacy=new CombatBoardPresetLibrary{layouts=[legacyPlan]};const string legacyPath="user://combat_board_presets.tres";
        Check(ResourceSaver.Save(legacy,legacyPath)==Error.Ok,"隔离目录准备旧全局方案库");
        var bytes=System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(legacyPath));Human.combat_board_presets=null;
        var compatible=CombatBoardPreparationService.GetLibrary(_actor);
        Check(compatible.layouts.Single().Title=="旧全局方案"&&compatible.layouts[0].preset_id.Length>0&&Human.combat_board_presets is null,"旧全局库兼容读取且不隐式写入人物");
        Check(CombatBoardPreparationService.TrySave(_actor,legacyPlan,"本人新方案",false,false,out _,out _)&&Human.combat_board_presets!.layouts.Count==2,"首次管理复制旧库到本人库再保存");
        Check(bytes.SequenceEqual(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(legacyPath))),"兼容导入不改写旧文件");Human.combat_board_presets=original;
        var saves=GetNode<GameSaveManager>("/root/GameSaveManager");saves.begin_new_game("配招隔离验收");
        var progress=new PersistentGameSave{player_data=restored,dynamic_dungeon_state=DynamicDungeonExpedition.CreateEmpty(0)};Check(saves.save_snapshot(progress),"正式存档管理器写入隔离旅程");
        Check(saves.load_slot(saves.active_slot_id),"正式存档入口可重新载入新字段");
        var disk=ResourceLoader.Load<PersistentGameSave>($"user://game_saves/{saves.active_slot_id}.tres",cacheMode:ResourceLoader.CacheMode.IgnoreDeep);
        Check(disk.player_data!.combat_board_presets!.layouts.Count==3&&disk.player_data.combat_board_preparation!.Title=="养锋连剑·正式","完整旅程序列化保留本人方案与当前应用");
        await Frames();
    }
    private async Task CheckBattle()
    {
        var runtimeBefore=Human.combat_board_layout;var applied=Human.combat_board_preparation!;var defaultSignature=CombatBoardPreparationService.Signature(applied);
        var snapshot=_actor.get_snapshot_data();snapshot.faction=1;snapshot.test_mana_cap_override=3;
        Check(CombatBoardPreparationService.Signature(snapshot.combat_board_layout!)==defaultSignature,"真实开战快照从默认应用盘开始");
        Check(ReferenceEquals(runtimeBefore,Human.combat_board_layout),"准备战斗不改世界运行盘");
        var copy=new BattleEntityData();copy.copy_from(snapshot);copy.combat_board_presets!.layouts[0].Title="替身改名";
        Check(snapshot.combat_board_presets!.layouts[0].Title!=copy.combat_board_presets.layouts[0].Title,"战斗快照复制隔离模板资源");
        var scene=new Node2D{Name="IsolatedPreparationBattle"};AddChild(scene);scene.AddChild(new Node2D{Name="DeployLayer"});
        var layer=new CanvasLayer{Name="UILayer"};scene.AddChild(layer);layer.AddChild(GD.Load<PackedScene>("res://tscn/panel/CombatSkillBar.tscn").Instantiate());
        var manager=new BattleManager{Name="BattleMgr",deploy_layer="../DeployLayer",isolated_trial_battle=true};scene.AddChild(manager);
        var foe=new BattleEntityData();foe.copy_from(snapshot);foe.uid="preparation_foe";foe.faction=2;foe.entity_name="对练者";
        var packed=GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");manager.setup_team_battle([packed],[packed],[snapshot],[foe]);await Frames(8);
        var owner=(Character)manager.get_main_player_unit()!;var hud=Nodes<BattleBoardHud>(layer).Single();
        Check(manager.is_board_editing()&&hud.EditorVisible,"真实战斗管理器打开统一配招页");
        Check(CombatBoardPreparationService.Signature(hud.Draft)==defaultSignature,"局内初始盘与人物当前应用一致");
        Check(CombatBoardPreparationService.GetLibrary(owner).layouts.Count==3,"局内读取出战人物的冻结方案库");
        Check(!CombatBoardPreparationService.TryRename(owner,applied.preset_id,"越界改名",false,out _)&&!CombatBoardPreparationService.TryApply(owner,new(),out _),"战斗替身不能管理默认盘或命名库");
        var planTab=Nodes<Button>(hud).Single(b=>b.Text=="方案");planTab.EmitSignal(BaseButton.SignalName.Pressed);await Frames();
        Check(Nodes<Button>(hud.GetNode<Control>("Editor/SourcePanel/PlanPage")).All(b=>b.Text=="应用"),"局内方案列表只载入，没有保存改名删除");
        await Capture("局内配招_本人方案");
        var second=CombatBoardPreparationService.GetLibrary(owner).layouts[1];Check(hud.TryApplyPlan(second),"局内应用仅替换当前配招草稿");
        Check(CombatBoardPreparationService.Signature(Human.combat_board_preparation!)==defaultSignature,"局内应用不写人物默认开局");
        Check(manager.commit_player_board(hud.Draft,out var reason),"局内方案可提交真实开战 "+reason);await Frames(8);
        Check(!manager.is_board_editing(),"开始战斗后进入真实运行态");
        var result=owner.get_snapshot_data();result.combat_board_preparation=new(){Title="不应回写"};result.combat_board_presets=new();_actor.apply_battle_data(result);
        Check(CombatBoardPreparationService.Signature(Human.combat_board_preparation!)==defaultSignature&&Human.combat_board_presets!.layouts.Count==3,"战斗回传不覆盖局外默认及命名库");
        Check(CombatBoardPreparationService.Signature(_actor.get_snapshot_data().combat_board_layout!)==defaultSignature,"下一场仍以人物应用配置开局");
        scene.QueueFree();await Frames();
    }
}
