using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class AlchemyPreviewScene
{
    private PanelContainer _bag=null!;
    private AcceptDialog _roomDetails=null!;
    private PanelContainer _techniques=null!;
    private AcceptDialog _breathing=null!;
    private Button _techniqueButton=null!;
    private Label _selectedMethod=null!;
    private HBoxContainer _shortcutRow=null!;
    private readonly List<AlchemyPreviewModel.Hand> _favorites=[];
    private readonly Dictionary<AlchemyPreviewModel.Hand,Button> _favoriteButtons=[];
    private readonly Dictionary<AlchemyPreviewModel.Hand,Button> _shortcutButtons=[];
    private Texture2D _roomIcons=null!;
    private ConfirmationDialog _doseDialog=null!;
    private SpinBox _doseCount=null!;
    private string _doseName="";
    private AcceptDialog _recipe=null!;
    private RichTextLabel _recipeRules=null!;
    private Control _demoNote=null!;
    private void BuildRoom()
    {
        var bg=new TextureRect{Texture=LoadRoomTexture("res://textures/ui/alchemy_room_20261017/wall_only_front_v8.png"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=MouseFilterEnum.Ignore};
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(bg);
        _roomIcons=LoadRoomTexture("res://textures/ui/alchemy_room_20261017/room_ui_source_v1.png");
        void Place(Control c,float x,float y,float w,float h){AddChild(c);c.AnchorLeft=x;c.AnchorTop=y;c.AnchorRight=x+w;c.AnchorBottom=y+h;}
        var back=RoomButton("〈 返回",()=>GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"));back.AddThemeColorOverride("font_color",new Color("eee6d1"));Place(back,.025f,.025f,.07f,.04f);
        var utilities=new HBoxContainer();utilities.AddThemeConstantOverride("separation",24);Place(utilities,.84f,.025f,.135f,.04f);utilities.AddChild(RoomButton("教程",ShowTutorial));var more=new MenuButton{Text="更多",Flat=true};more.AddThemeColorOverride("font_color",Ink);more.AddThemeFontSizeOverride("font_size",22);utilities.AddChild(more);var popup=more.GetPopup();foreach(var item in new[]{"炉况详情","炉记","自动演示 / 暂停","逐步演示","设置","重新开炉"})popup.AddItem(item);popup.IdPressed+=id=>{switch(id){case 0:_roomDetails.PopupCentered();break;case 1:ShowDetails();break;case 2:StartAutoDemo();break;case 3:NextDemo();break;case 4:_settings.PopupCentered();break;case 5:Reset();break;}};
        _title=Text("丹室",28,Ink);_title.Hide();AddChild(_title);
        _furnace=new AlchemyDragTile{Kind="丹炉",Name="FurnaceDrop",CanInteract=()=>!_demonstrating&&!_model.Done};_furnace.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());Place(_furnace,.26f,.05f,.48f,.95f);
        _furnaceView=new AlchemyFurnaceView();_furnaceView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_furnace.AddChild(_furnaceView);_furnace.Dropped=(name,_)=>ChooseDose(name);
        _furnace.GuiInput+=e=>{if(e is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:true,DoubleClick:true}){_roomDetails.PopupCentered();_furnace.AcceptEvent();}};
        var status=Box(8);_temperature=Text("",22,Gold);status.AddChild(_temperature);_resources=Text("",18,Ink,true);status.AddChild(_resources);status.AddChild(RoomButton("调息 · 副手 / 回气 / 服药",()=>_breathing.PopupCentered()));_spirit=WorldHudStyle.Bar(Jade);_spirit.CustomMinimumSize=new Vector2(0,7);status.AddChild(_spirit);_forecast=Text("",18,Jade,true);status.AddChild(_forecast);
        _yinLabel=Text("",17,Ink,true);status.AddChild(_yinLabel);_yin=new AlchemyPolarityGauge{CustomMinimumSize=new Vector2(0,20)};status.AddChild(_yin);
        var guideBox=Box(4);_demoNote=guideBox;Place(guideBox,.035f,.30f,.23f,.23f);_guide=Text("",21,Ink,true);_guide.MaxLinesVisible=6;guideBox.AddChild(_guide);var demoRow=new HBoxContainer();guideBox.AddChild(demoRow);_demoButton=RoomButton("自动演示",StartAutoDemo);demoRow.AddChild(_demoButton);_next=RoomButton("逐步讲解",NextDemo);demoRow.AddChild(_next);
        var bagButton=ObjectEntry("行囊",0,()=>_bag.Visible=!_bag.Visible);Place(bagButton,.025f,.795f,.115f,.18f);
        var recipeButton=ObjectEntry("丹方",1,ShowRecipe);Place(recipeButton,.86f,.795f,.115f,.18f);
        _bag=new PanelContainer{Visible=false};_bag.AddThemeStyleboxOverride("panel",QuietRoomStyle.Paper(.97f,16));Place(_bag,.03f,.12f,.245f,.50f);var contents=Box(7);_bag.AddChild(contents);var bh=new HBoxContainer();contents.AddChild(bh);var bl=Text("储物袋",24,Ink);bl.SizeFlagsHorizontal=SizeFlags.ExpandFill;bh.AddChild(bl);bh.AddChild(RoomButton("收起",()=>_bag.Hide()));contents.AddChild(Text("拖到炉身安排投药 · 每次一份",16,Dim));
        var scroll=Scroll();scroll.SizeFlagsVertical=SizeFlags.ExpandFill;contents.AddChild(scroll);var grid=new GridContainer{Columns=3};grid.AddThemeConstantOverride("h_separation",8);grid.AddThemeConstantOverride("v_separation",10);scroll.AddChild(grid);
        foreach(var herb in _model.Herbs){var tile=new AlchemyDragTile{HerbName=herb.Name,Icon=GD.Load<Texture2D>(herb.Item.icon_path),CustomMinimumSize=new Vector2(98,90),MouseDefaultCursorShape=CursorShape.Drag,CanInteract=()=>!_demonstrating&&!_model.Done};tile.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());var cell=Box(2);cell.MouseFilter=MouseFilterEnum.Ignore;tile.AddChild(cell);cell.AddChild(Image(tile.Icon,54));var label=Text(herb.Name,17,Ink);label.HorizontalAlignment=HorizontalAlignment.Center;cell.AddChild(label);grid.AddChild(tile);_shelf[herb.Name]=tile;}
        contents.AddChild(Text("待投 · 推进一息入炉，点击可撤回",16,Dim));var pendingScroll=Scroll();pendingScroll.CustomMinimumSize=new Vector2(0,65);contents.AddChild(pendingScroll);_pending=Box(3);pendingScroll.AddChild(_pending);
        BuildTechniques(Place);
        _roomDetails=new AcceptDialog{Title="炉内情况 · 体验演算",Size=new Vector2I(900,840)};AddChild(_roomDetails);var detailScroll=Scroll();detailScroll.CustomMinimumSize=new Vector2(850,740);_roomDetails.AddChild(detailScroll);var details=Box(10);details.SizeFlagsHorizontal=SizeFlags.ExpandFill;detailScroll.AddChild(details);details.AddChild(status);_condition=Text("",20,Ink,true);details.AddChild(_condition);_task=Text("",18,Dim,true);details.AddChild(_task);_functions=Text("",18,Gold,true);details.AddChild(_functions);details.AddChild(RoomButton("查看每项功用",ShowActiveUtilities));var ins=Scroll();ins.CustomMinimumSize=new Vector2(0,100);details.AddChild(ins);_inside=Box(3);ins.AddChild(_inside);var logs=Scroll();logs.CustomMinimumSize=new Vector2(0,200);details.AddChild(logs);_changes=Box(5);logs.AddChild(_changes);_result=Text("",19,Jade,true);details.AddChild(_result);
        _settings=new AcceptDialog{Title="炼丹设置 · 体验参数",Size=new Vector2I(660,560)};AddChild(_settings);var settings=Box(10);_settings.AddChild(settings);
        _recipe=new AcceptDialog{Title="回春丹 · 九品体验丹方",Size=new Vector2I(820,700)};AddChild(_recipe);var recipeBox=Box(10);_recipe.AddChild(recipeBox);_recipeRules=new RichTextLabel{CustomMinimumSize=new Vector2(760,430)};_recipeRules.AddThemeFontSizeOverride("normal_font_size",20);recipeBox.AddChild(_recipeRules);
        _appearance=new OptionButton();_appearance.AddItem("青铜炉 · 外观");_appearance.AddItem("青玉炉 · 原型外观");settings.AddChild(_appearance);_appearance.ItemSelected+=i=>_furnaceView.Appearance=(int)i;
        recipeBox.AddChild(Text("主药（只可一种）",18,Ink));_mainChoice=new OptionButton();foreach(var h in _model.Herbs.Where(h=>h.CanBeMain))_mainChoice.AddItem(h.Name);recipeBox.AddChild(_mainChoice);_mainChoice.ItemSelected+=_=>{_model.MainName=_mainChoice.GetItemText(_mainChoice.Selected);Refresh();};
        recipeBox.AddChild(Text("计划成丹数量",18,Ink));_wanted=new SpinBox{MinValue=1,MaxValue=72,Step=1,Value=3};recipeBox.AddChild(_wanted);_wanted.ValueChanged+=v=>{if(!_updating){_model.Wanted=(int)v;Refresh();}};
        settings.AddChild(Text("丹道造诣",18,Ink));_attainment=new SpinBox{MinValue=0,MaxValue=10000,Step=1,Value=32};settings.AddChild(_attainment);_attainment.ValueChanged+=v=>{if(!_updating){_model.Attainment=(int)v;Refresh();}};_learn=RoomButton("学习精细控火（造诣24）",()=>{_model.Explanation=_model.LearnFine();Refresh();});settings.AddChild(_learn);
        _breathing=new AcceptDialog{Title="调息 · 下一息的安排",Size=new Vector2I(560,310)};AddChild(_breathing);var breath=Box(12);_breathing.AddChild(breath);breath.AddChild(Text("选择副手掌炉后，本人可以同时回气。",19,Ink,true));_operator=new OptionButton();_operator.AddItem("本人掌炉");_operator.AddItem("副手接管");breath.AddChild(_operator);_operator.ItemSelected+=_=>PlanChanged();_recover=new CheckButton{Text="本人回气 · 需副手接管"};breath.AddChild(_recover);_recover.Toggled+=_=>PlanChanged();_potion=new CheckButton{Text="本息服回灵散"};breath.AddChild(_potion);_potion.Toggled+=_=>PlanChanged();breath.AddChild(Text("确认安排后，推进一息时执行并结算消耗。",17,Dim,true));settings.AddChild(RoomButton("十二功用表",ShowUtilities));
        _doseDialog=new ConfirmationDialog{Title="安排投药",Size=new Vector2I(510,235)};AddChild(_doseDialog);var doseBox=Box(10);_doseDialog.AddChild(doseBox);doseBox.AddChild(Text("本次投放份数 · 受造诣和炉容量限制",18,Ink));_doseCount=new SpinBox{MinValue=1,MaxValue=4,Step=1,Value=1};doseBox.AddChild(_doseCount);doseBox.AddChild(Text("确认后列入待投；推进一息真正入炉。",17,Dim));_doseDialog.Confirmed+=()=>{var messages=new List<string>();for(int i=0;i<(int)_doseCount.Value;i++)messages.Add(_model.QueueHerb(_doseName));_model.Explanation=string.Join("\n",messages);Refresh();};
        _timer=new Godot.Timer{WaitTime=3.2};AddChild(_timer);_timer.Timeout+=DemoNext;
        _ghost=new PanelContainer{Visible=false,ZIndex=100,MouseFilter=MouseFilterEnum.Ignore};_ghost.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());_ghost.AddChild(new TextureRect{CustomMinimumSize=new Vector2(70,70),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore});AddChild(_ghost);
    }
    private Button ObjectEntry(string title,int cell,Action action)
    {
        var button=RoomButton("",action);var content=Box(0);content.MouseFilter=MouseFilterEnum.Ignore;button.AddChild(content);content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);var icon=new AtlasTexture{Atlas=_roomIcons,Region=new Rect2(cell*362,0,362,362),FilterClip=true};var art=Image(icon,120);art.SizeFlagsHorizontal=SizeFlags.ExpandFill;art.SizeFlagsVertical=SizeFlags.ExpandFill;content.AddChild(art);var label=Text(title,22,Ink);label.HorizontalAlignment=HorizontalAlignment.Center;content.AddChild(label);button.TooltipText=title;return button;
    }
    private void BuildTechniques(Action<Control,float,float,float,float> place)
    {
        _selectedMethod=Text("",19,Ink);_selectedMethod.HorizontalAlignment=HorizontalAlignment.Center;place(_selectedMethod,.32f,.875f,.40f,.035f);
        _shortcutRow=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};_shortcutRow.AddThemeConstantOverride("separation",8);place(_shortcutRow,.32f,.815f,.40f,.04f);
        var actions=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};actions.AddThemeConstantOverride("separation",24);place(actions,.255f,.915f,.49f,.065f);
        actions.ZIndex=2;
        foreach(var hand in new[]{AlchemyPreviewModel.Hand.Hold,AlchemyPreviewModel.Hand.Raise,AlchemyPreviewModel.Hand.Lower}){var capture=hand;var b=RoomAction(AlchemyPreviewModel.HandNames[(int)hand],()=>SelectHand(capture));b.TooltipText=HandHelp(hand);_hands[hand]=b;actions.AddChild(b);}
        _techniqueButton=RoomAction("技法 ▴",()=>_techniques.Visible=!_techniques.Visible);actions.AddChild(_techniqueButton);_advance=RoomAction("推进一息 →",Advance);_advance.CustomMinimumSize=new Vector2(190,54);actions.AddChild(_advance);
        _techniques=new PanelContainer{Visible=false};_techniques.AddThemeStyleboxOverride("panel",QuietRoomStyle.Paper(.98f,16));place(_techniques,.30f,.55f,.43f,.25f);var menu=Box(8);_techniques.AddChild(menu);var head=new HBoxContainer();menu.AddChild(head);var title=Text("技法 · 选择后推进一息执行",21,Ink);title.SizeFlagsHorizontal=SizeFlags.ExpandFill;head.AddChild(title);head.AddChild(RoomButton("收起",()=>_techniques.Hide()));menu.AddChild(Text("＋常用可设为快捷项，最多三个；未学技法可查看条件。",16,Dim));
        var groups=new HBoxContainer();groups.AddThemeConstantOverride("separation",24);menu.AddChild(groups);
        void Group(string title,params AlchemyPreviewModel.Hand[] hands){var column=Box(6);column.SizeFlagsHorizontal=SizeFlags.ExpandFill;groups.AddChild(column);column.AddChild(Text(title,20,Jade));foreach(var hand in hands){var row=new HBoxContainer();column.AddChild(row);var capture=hand;var b=RoomAction(AlchemyPreviewModel.HandNames[(int)hand],()=>SelectHand(capture));b.SizeFlagsHorizontal=SizeFlags.ExpandFill;b.CustomMinimumSize=new Vector2(100,42);b.TooltipText=HandHelp(hand);row.AddChild(b);_hands[hand]=b;var pin=RoomButton("＋",()=>ToggleFavorite(capture));pin.TooltipText="设为常用技法，最多三个；再次点击移除。";row.AddChild(pin);_favoriteButtons[hand]=pin;}}
        Group("调理药性",AlchemyPreviewModel.Hand.Circulate,AlchemyPreviewModel.Hand.Separate,AlchemyPreviewModel.Hand.Purge);Group("控火",AlchemyPreviewModel.Hand.Fine);Group("成丹",AlchemyPreviewModel.Hand.Gather,AlchemyPreviewModel.Hand.Output);
    }
    private void ToggleFavorite(AlchemyPreviewModel.Hand hand)
    {
        if(_favorites.Contains(hand))_favorites.Remove(hand);else if(_favorites.Count<3)_favorites.Add(hand);else{_model.Explanation="常用技法最多三个，请先移除一个。";Refresh();return;}
        Clear(_shortcutRow);_shortcutButtons.Clear();foreach(var item in _favorites){var capture=item;var b=RoomAction(AlchemyPreviewModel.HandNames[(int)item],()=>SelectHand(capture));b.CustomMinimumSize=new Vector2(110,38);b.TooltipText=HandHelp(item);_shortcutRow.AddChild(b);_shortcutButtons[item]=b;}Refresh();
    }
    private void UpdateRoomState()
    {
        _selectedMethod.Text="本息将执行："+AlchemyPreviewModel.HandNames[(int)_model.Action]+(_model.Pending.Count>0?$" · 待投{_model.Pending.Count}份":"");
        foreach(var p in _hands)p.Value.AddThemeStyleboxOverride("normal",ActionStyle(p.Key==_model.Action));
        foreach(var p in _shortcutButtons){p.Value.Disabled=_hands[p.Key].Disabled;p.Value.AddThemeStyleboxOverride("normal",ActionStyle(p.Key==_model.Action));}
        foreach(var p in _favoriteButtons){p.Value.Text=_favorites.Contains(p.Key)?"－":"＋";p.Value.Disabled=_demonstrating;}
        _techniqueButton.Disabled=_demonstrating||_model.Done;_guide.TooltipText=_model.Explanation;
        _demoNote.Visible=_demonstrating;_selectedMethod.Visible=_model.Pending.Count>0||_model.Action!=AlchemyPreviewModel.Hand.Hold||_demonstrating;
        var next=_model.Preview();_advance.TooltipText=$"执行当前手段并推进一息\n法力消耗 {next.Mana} · 灵识占用 {next.Ratio:P0}\n超过80%不支，超过100%通常炸炉。";if(next.Ratio>.8){_selectedMethod.Show();_selectedMethod.Text+=$" · 灵识{next.Ratio:P0}，{(next.Ratio>1?"超载，通常炸炉":"不支")}";_selectedMethod.AddThemeColorOverride("font_color",Red);}else _selectedMethod.AddThemeColorOverride("font_color",Ink);
    }
    private Texture2D? _quietAction,_chosenAction;
    private StyleBoxTexture ActionStyle(bool selected=false)
    {
        ref Texture2D? texture=ref(selected?ref _chosenAction:ref _quietAction);
        if(texture is null){string color=selected?"#456655":"#657065";string svg=$"<svg xmlns='http://www.w3.org/2000/svg' width='240' height='80' viewBox='0 0 240 80'><path d='M10 18Q44 12 91 16L205 13L229 21L222 58L183 64L42 61L12 65L17 42Z' fill='#eee7d5' opacity='{(selected?".74":".36")}'/><path d='M18 67L64 65L107 66L165 63L220 65L208 68L158 67L90 69L39 68Z' fill='{color}' opacity='{(selected?".85":".40")}'/>{(selected?"<path d='M20 26L26 23L29 29L24 34L19 31Z' fill='#9b5741'/>":"")}</svg>";var image=new Godot.Image();image.LoadSvgFromString(svg);texture=ImageTexture.CreateFromImage(image);}
        return new StyleBoxTexture{Texture=texture,TextureMarginLeft=20,TextureMarginRight=20,TextureMarginTop=20,TextureMarginBottom=20,ContentMarginLeft=18,ContentMarginRight=18,ContentMarginTop=8,ContentMarginBottom=8};
    }
    private Button RoomAction(string title,Action action){var b=RoomButton(title,action);b.CustomMinimumSize=new Vector2(115,54);b.AddThemeStyleboxOverride("normal",ActionStyle());b.AddThemeStyleboxOverride("hover",ActionStyle(true));b.AddThemeStyleboxOverride("pressed",ActionStyle(true));b.AddThemeStyleboxOverride("disabled",ActionStyle());b.AddThemeColorOverride("font_disabled_color",new Color(.39f,.42f,.37f,.65f));return b;}
    public override void _ExitTree(){_quietAction?.Dispose();_chosenAction?.Dispose();_quietAction=_chosenAction=null;}
    private void ChooseDose(string name)
    {
        int maximum=Math.Min(24-_model.InFurnace.Count-_model.Pending.Count,Math.Max(1,_model.OperatorAttainment/8)-_model.Pending.Count);if(maximum<1){QueueIntoFurnace(name);return;}_doseName=name;_doseCount.MaxValue=maximum;_doseCount.Value=1;_doseDialog.Title=$"投入{name} · 本息还可投{maximum}份";_doseDialog.PopupCentered();
    }
    private void ShowRecipe(){_recipeRules.Text="体验丹方参数，正式成品规则尚待接入。\n\n"+_model.Checks()+"\n\n"+string.Join("\n",_model.Pills.Select(p=>$"{p.Index}号：{p.Score} · {AlchemyPreviewModel.Tier(p.Score)}\n{p.Reason}"));_recipe.PopupCentered();}
    public async void CaptureRoomLayout()
    {
        await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("room");_bag.Show();await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("bag");_bag.Hide();_techniques.Show();await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("techniques");_techniques.Hide();
    }
    private async System.Threading.Tasks.Task SaveRoomLayout(string name){if(_captureDirectory.Length==0)return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);DirAccess.MakeDirRecursiveAbsolute(_captureDirectory);GetViewport().GetTexture().GetImage().SavePng(_captureDirectory.PathJoin($"layout-{name}.png"));}
    public async void ShowFirePreview(){_model.QueueHerb("青木芯");_model.Action=AlchemyPreviewModel.Hand.Raise;_model.Advance();_model.Action=AlchemyPreviewModel.Hand.Hold;_model.Advance();Refresh();await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("fire");}
    public override void _UnhandledInput(InputEvent e){if(e.IsActionPressed("ui_cancel")&&(_bag.Visible||_techniques.Visible)){_bag.Hide();_techniques.Hide();GetViewport().SetInputAsHandled();}}
    private static Button RoomButton(string text,Action action)
    {
        var b=Button(text,action);b.AddThemeFontSizeOverride("font_size",22);b.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());b.AddThemeStyleboxOverride("disabled",new StyleBoxEmpty());b.AddThemeStyleboxOverride("hover",new StyleBoxFlat{BgColor=new Color(.38f,.44f,.36f,.09f),BorderWidthBottom=1,BorderColor=Jade});return b;
    }
    internal static Texture2D LoadRoomTexture(string path)=>ResourceLoader.Exists(path)?GD.Load<Texture2D>(path):ImageTexture.CreateFromImage(Godot.Image.LoadFromFile(ProjectSettings.GlobalizePath(path)));
}
