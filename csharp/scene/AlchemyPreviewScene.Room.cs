using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class AlchemyPreviewScene
{
    private AlchemyHerbClothPanel _herbCloth=null!;
    private PanelContainer _roomDetails=null!;
    private Button _infoToggle=null!;
    private PanelContainer _techniques=null!;
    private AcceptDialog _breathing=null!;
    private Button _techniqueButton=null!;
    private Label _selectedMethod=null!,_networkResources=null!;
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
    private OptionButton _pillChoice=null!,_mainChoice=null!,_formulaChoice=null!;
    private Control _demoNote=null!;
    private OptionButton _learnChoice=null!;
    private void BuildRoom()
    {
        _roomIcons=LoadRoomTexture("res://textures/ui/alchemy_room_20261017/room_ui_source_v1.png");
        void Place(Control c,float x,float y,float w,float h){AddChild(c);c.AnchorLeft=x;c.AnchorTop=y;c.AnchorRight=x+w;c.AnchorBottom=y+h;}
        var back=RoomButton("〈 返回",()=>GetTree().ChangeSceneToFile("res://tscn/scene/StartMenu.tscn"));back.AddThemeColorOverride("font_color",new Color("eee6d1"));Place(back,.025f,.025f,.07f,.04f);
        var utilities=new HBoxContainer();utilities.AddThemeConstantOverride("separation",24);Place(utilities,.80f,.025f,.18f,.04f);var tutorialEntry=RoomButton("教程",ShowTutorial);tutorialEntry.AddThemeColorOverride("font_color",new Color("eee9d7"));utilities.AddChild(tutorialEntry);var demoEntry=RoomButton("演示 / 暂停",StartSimpleDemo);demoEntry.AddThemeColorOverride("font_color",new Color("eee9d7"));utilities.AddChild(demoEntry);var more=new MenuButton{Text="更多",Flat=true};more.AddThemeColorOverride("font_color",new Color("eee9d7"));more.AddThemeFontSizeOverride("font_size",22);utilities.AddChild(more);var popup=more.GetPopup();foreach(var item in new[]{"炉况详情","炉记","自动演示 / 暂停","逐步演示","设置","重新开炉","整理节点布局"})popup.AddItem(item);popup.IdPressed+=id=>{switch(id){case 0:ToggleFurnaceInfo();break;case 1:ShowDetails();break;case 2:StartAutoDemo();break;case 3:NextDemo();break;case 4:_settings.PopupCentered();break;case 5:Reset();break;case 6:_relationGraph.ResetLayout();break;}};
        _title=Text("丹室",28,Ink);_title.Hide();AddChild(_title);
        var factory=RoomAction("第一炉教程",()=>StartSimpleCourse());Place(factory,.12f,.025f,.13f,.045f);
        _furnace=new AlchemyDragTile{Kind="丹炉",Name="FurnaceDrop",CanInteract=()=>_model.CanPrepare&&!_demonstrating};_furnace.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());Place(_furnace,.26f,.05f,.48f,.95f);
        _furnaceView=new AlchemyFurnaceView();_furnaceView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);_furnace.AddChild(_furnaceView);
        _relationGraph=new AlchemyFurnaceRelationGraph{FurnaceMouthGlobal=()=>_furnaceView.MouthGlobalPosition};Place(_relationGraph,.015f,.085f,.97f,.80f);_relationGraph.Hide();
        _networkResources=Text("",20,new Color("e9eddb"));_networkResources.HorizontalAlignment=HorizontalAlignment.Center;Place(_networkResources,.29f,.025f,.49f,.045f);
        _relationGraph.GraphChanged+=Refresh;
        _relationGraph.BasicAction=BasicFurnaceAction;
        _relationGraph.CanDropMaterial=(position,data)=>_furnace._CanDropData(position,data);
        _relationGraph.DropMaterial=(position,data)=>_furnace._DropData(position,data);
        _furnace.Dropped=(name,_)=>ChooseDose(name);
        _furnace.GuiInput+=e=>
        {
            if(!_relationGraph.Visible&&e is InputEventMouseButton{ButtonIndex:MouseButton.Left,Pressed:true,DoubleClick:false}){_relationGraph.SetOpen(true);_furnace.AcceptEvent();}
        };
        var status=Box(7);_forecast=Text("",18,Jade,true);status.AddChild(_forecast);_resources=Text("",17,Ink,true);status.AddChild(_resources);
        _yinLabel=Text("",17,Ink,true);status.AddChild(_yinLabel);_yin=new AlchemyPolarityGauge{CustomMinimumSize=new Vector2(0,20)};status.AddChild(_yin);
        // 演示说明只进入右侧炉记，画面左侧不再覆盖文字。
        _guide=Text("",18,Ink,true);_demoNote=new Control();_demoNote.Hide();AddChild(_demoNote);
        _demoButton=RoomButton("自动演示",StartAutoDemo);_next=RoomButton("逐步讲解",NextDemo);
        _herbCloth=new AlchemyHerbClothPanel{Visible=false};
        Place(_herbCloth,.025f,.12f,.245f,.675f);
        var clothInset=new MarginContainer{MouseFilter=MouseFilterEnum.Pass};
        clothInset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        clothInset.AddThemeConstantOverride("margin_left",22);clothInset.AddThemeConstantOverride("margin_right",24);
        clothInset.AddThemeConstantOverride("margin_top",25);clothInset.AddThemeConstantOverride("margin_bottom",20);
        _herbCloth.ContentRoot.AddChild(clothInset);
        var contents=Box(8);clothInset.AddChild(contents);
        var clothHeader=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};clothHeader.AddThemeConstantOverride("separation",8);contents.AddChild(clothHeader);
        var clothTitle=Text("百草卷",24,Ink);clothTitle.SizeFlagsHorizontal=SizeFlags.ExpandFill;clothHeader.AddChild(clothTitle);
        var closeCloth=RoomButton("卷起",()=>_herbCloth.SetOpen(false));closeCloth.AddThemeFontSizeOverride("font_size",17);clothHeader.AddChild(closeCloth);
        var clothHint=Text("布面拣药 · 拖入炉口",16,Dim);contents.AddChild(clothHint);
        var materialRow=new HBoxContainer();materialRow.AddThemeConstantOverride("separation",4);materialRow.SizeFlagsVertical=SizeFlags.ExpandFill;contents.AddChild(materialRow);
        var scroll=Scroll();scroll.SizeFlagsHorizontal=SizeFlags.ExpandFill;scroll.SizeFlagsVertical=SizeFlags.ExpandFill;materialRow.AddChild(scroll);
        var nativeScroll=scroll.GetVScrollBar();nativeScroll.Modulate=new Color(1,1,1,0);
        var grid=new GridContainer{Columns=2};grid.AddThemeConstantOverride("h_separation",7);grid.AddThemeConstantOverride("v_separation",8);scroll.AddChild(grid);
        foreach(var herb in _model.Herbs){var tile=new AlchemyDragTile{HerbName=herb.Name,Icon=GD.Load<Texture2D>(herb.Item.icon_path),CustomMinimumSize=new Vector2(132,108),MouseDefaultCursorShape=CursorShape.Drag,CanInteract=()=>_model.CanPrepare&&!_demonstrating};tile.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());var cell=Box(1);cell.MouseFilter=MouseFilterEnum.Ignore;tile.AddChild(cell);cell.AddChild(Image(tile.Icon,62));var label=Text(herb.Name,17,Ink);label.HorizontalAlignment=HorizontalAlignment.Center;cell.AddChild(label);grid.AddChild(tile);_shelf[herb.Name]=tile;}
        var scrollMark=new AlchemyClothScrollMarker{Scroll=scroll,CustomMinimumSize=new Vector2(12,0),SizeFlagsVertical=SizeFlags.ExpandFill};materialRow.AddChild(scrollMark);
        var pendingHead=new HBoxContainer();pendingHead.AddThemeConstantOverride("separation",8);contents.AddChild(pendingHead);
        pendingHead.AddChild(Text("本次投料",16,Ink));var pendingRule=Text("·",16,Dim);pendingRule.SizeFlagsHorizontal=SizeFlags.ExpandFill;pendingHead.AddChild(pendingRule);
        var pendingScroll=Scroll();pendingScroll.CustomMinimumSize=new Vector2(0,64);contents.AddChild(pendingScroll);_pending=Box(3);pendingScroll.AddChild(_pending);
        // 先放铺开的药布，再放卷轴入口；卷起时图标像药布的收纳轴，始终在最上层可点击。
        var clothIcon=GD.Load<Texture2D>("res://textures/ui/alchemy_room_20261017/herb_cloth_roll_icon_v1.png");
        var clothButton=ObjectEntry("百草卷",0,()=>_herbCloth.Toggle(),clothIcon);Place(clothButton,.025f,.795f,.115f,.18f);
        var recipeButton=ObjectEntry("丹方",1,ShowRecipe);Place(recipeButton,.86f,.795f,.115f,.18f);
        BuildTechniques(Place);
        BuildFurnaceSidebar(status);
        _runButton=RoomAction("开炉炼丹",StartAutomaticAlchemy);Place(_runButton,.425f,.905f,.15f,.075f);_runButton.ZIndex=45;
        _settings=new AcceptDialog{Title="炼丹设置 · 体验参数",Size=new Vector2I(660,560)};AddChild(_settings);var settings=Box(10);_settings.AddChild(settings);
        _recipe=new AcceptDialog{Title="丹方册",Size=new Vector2I(940,760)};AddChild(_recipe);var recipeBox=Box(8);_recipe.AddChild(recipeBox);
        var chooser=new VBoxContainer();chooser.AddThemeConstantOverride("separation",5);recipeBox.AddChild(chooser);
        var pillRow=new HBoxContainer();pillRow.AddThemeConstantOverride("separation",10);chooser.AddChild(pillRow);
        pillRow.AddChild(Text("丹药",18,Dim));_pillChoice=new OptionButton{CustomMinimumSize=new Vector2(230,42)};foreach(var pill in AlchemyRecipeCatalog.Pills)_pillChoice.AddItem(pill.Name);pillRow.AddChild(_pillChoice);_pillChoice.ItemSelected+=OnPillChoice;
        pillRow.AddChild(Text("本炉主材",18,Dim));_mainChoice=new OptionButton{CustomMinimumSize=new Vector2(230,42)};pillRow.AddChild(_mainChoice);_mainChoice.ItemSelected+=OnMainChoice;
        var formulaRow=new HBoxContainer();formulaRow.AddThemeConstantOverride("separation",10);chooser.AddChild(formulaRow);
        formulaRow.AddChild(Text("参考丹方",18,Dim));_formulaChoice=new OptionButton{SizeFlagsHorizontal=SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(320,42)};formulaRow.AddChild(_formulaChoice);_formulaChoice.ItemSelected+=OnFormulaChoice;
        var recipeScroll=Scroll();recipeScroll.SizeFlagsVertical=SizeFlags.ExpandFill;recipeBox.AddChild(recipeScroll);_recipeRules=new RichTextLabel{CustomMinimumSize=new Vector2(840,420),SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};_recipeRules.AddThemeFontSizeOverride("normal_font_size",19);recipeScroll.AddChild(_recipeRules);_recipeDetailsButton=RoomButton("查看具体判定",ToggleRecipeDetails);recipeBox.AddChild(_recipeDetailsButton);
        _appearance=new OptionButton();_appearance.AddItem("青铜炉 · 外观");_appearance.AddItem("青玉炉 · 原型外观");settings.AddChild(_appearance);_appearance.ItemSelected+=i=>_furnaceView.Appearance=(int)i;
        _wanted=new SpinBox{MinValue=1,MaxValue=72,Step=1,Value=3,Visible=false};AddChild(_wanted);_wanted.ValueChanged+=v=>{if(!_updating){_model.Wanted=(int)v;Refresh();}};
        settings.AddChild(Text("丹道造诣 · 当前测试角色",18,Ink));_attainment=new SpinBox{MinValue=0,MaxValue=1000000,Step=1,Value=36};settings.AddChild(_attainment);_attainment.ValueChanged+=v=>{if(!_updating){_model.Attainment=(int)v;Refresh();}};
        _learnChoice=new OptionButton();foreach(var hand in new[]{AlchemyPreviewModel.Hand.Fine,AlchemyPreviewModel.Hand.SlowFeed,AlchemyPreviewModel.Hand.PreciseSeparate,AlchemyPreviewModel.Hand.Quench})_learnChoice.AddItem(AlchemyPreviewModel.HandNames[(int)hand],(int)hand);AddChild(_learnChoice);_learnChoice.Hide();_learnChoice.ItemSelected+=_=>Refresh();_learn=RoomButton("学习所选技术",()=>{_model.Explanation=_model.Learn((AlchemyPreviewModel.Hand)_learnChoice.GetSelectedId());Refresh();});AddChild(_learn);_learn.Hide();
        var equipment=new HBoxContainer();settings.AddChild(equipment);var furnaceKind=new OptionButton();furnaceKind.AddItem("厚壁炉 · 容量12份");furnaceKind.AddItem("细控炉 · 容量8份，精度支持+12%");equipment.AddChild(furnaceKind);furnaceKind.ItemSelected+=i=>{_model.FurnaceIndex=(int)i;Refresh();};var fireKind=new OptionButton();fireKind.AddItem("平火 · 供热20/档");fireKind.AddItem("柔火 · 供热17/档，耗法力×0.77");equipment.AddChild(fireKind);fireKind.ItemSelected+=i=>{_model.FireIndex=(int)i;Refresh();};
        _breathing=new AcceptDialog{Title="调息 · 下一息的安排",Size=new Vector2I(560,310)};AddChild(_breathing);var breath=Box(12);_breathing.AddChild(breath);breath.AddChild(Text("选择副手掌炉后，本人可以同时回气。",19,Ink,true));_operator=new OptionButton();_operator.AddItem("本人掌炉");_operator.AddItem("副手接管");breath.AddChild(_operator);_operator.ItemSelected+=_=>PlanChanged();_recover=new CheckButton{Text="本人回气 · 需副手接管"};breath.AddChild(_recover);_recover.Toggled+=_=>PlanChanged();_potion=new CheckButton{Text="本息服回灵散"};breath.AddChild(_potion);_potion.Toggled+=_=>PlanChanged();breath.AddChild(Text("安排随下一炉令执行；可点守炉让副手掌炉一息。",17,Dim,true));
        _doseDialog=new ConfirmationDialog{Title="放入布置区",Size=new Vector2I(510,235)};AddChild(_doseDialog);var doseBox=Box(10);_doseDialog.AddChild(doseBox);doseBox.AddChild(Text("药材份数 · 受丹炉总容量限制",18,Ink));_doseCount=new SpinBox{MinValue=.1,MaxValue=4,Step=.1,Value=1};doseBox.AddChild(_doseCount);doseBox.AddChild(Text("投料立即显示为节点；开炉后自动炼化。",17,Dim));_doseDialog.Confirmed+=()=>{_model.Explanation=_model.PlaceHerb(_doseName,_doseCount.Value);_herbCloth.SetOpen(false);Refresh();};
        _timer=new Godot.Timer{WaitTime=3.2};AddChild(_timer);_timer.Timeout+=DemoNext;
        _ghost=new PanelContainer{Visible=false,ZIndex=100,MouseFilter=MouseFilterEnum.Ignore};_ghost.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());_ghost.AddChild(new TextureRect{CustomMinimumSize=new Vector2(70,70),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore});AddChild(_ghost);
    }
    private Button ObjectEntry(string title,int cell,Action action,Texture2D? customIcon=null)
    {
        var button=RoomButton("",action);var content=Box(0);content.MouseFilter=MouseFilterEnum.Ignore;button.AddChild(content);content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);Texture2D icon=customIcon??new AtlasTexture{Atlas=_roomIcons,Region=new Rect2(cell*362,0,362,362),FilterClip=true};var art=Image(icon,120);art.SizeFlagsHorizontal=SizeFlags.ExpandFill;art.SizeFlagsVertical=SizeFlags.ExpandFill;content.AddChild(art);var label=Text(title,22,Ink);label.HorizontalAlignment=HorizontalAlignment.Center;content.AddChild(label);return button;
    }
    private static StyleBox MainHerbClothMark() => new StyleBoxFlat
    {
        BgColor = new Color(.37f, .45f, .34f, .12f),
        BorderWidthBottom = 1,
        BorderColor = new Color(.30f, .39f, .30f, .32f),
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3
    };
    private void BuildTechniques(Action<Control,float,float,float,float> place)
    {
        _selectedMethod=Text("",19,Ink);_selectedMethod.HorizontalAlignment=HorizontalAlignment.Center;place(_selectedMethod,.32f,.875f,.40f,.035f);
        _shortcutRow=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};_shortcutRow.AddThemeConstantOverride("separation",8);place(_shortcutRow,.32f,.815f,.40f,.04f);
        var actions=GetNode<HBoxContainer>("Actions");actions.Hide();
        foreach(var hand in new[]{AlchemyPreviewModel.Hand.Hold,AlchemyPreviewModel.Hand.Raise,AlchemyPreviewModel.Hand.Lower})
        {
            var capture=hand;var b=actions.GetNode<Button>(hand.ToString());
            b.Text=AlchemyPreviewModel.HandNames[(int)hand];b.Pressed+=()=>SelectHand(capture);_hands[hand]=b;
            b.AddThemeStyleboxOverride("normal",ActionStyle());b.AddThemeStyleboxOverride("hover",ActionStyle(true));b.AddThemeStyleboxOverride("pressed",ActionStyle(true));
        }
        _techniqueButton=actions.GetNode<Button>("Techniques");_techniqueButton.Hide();
        _shortcutRow.Hide();
        _techniques=new PanelContainer{Visible=false};AddChild(_techniques);
    }

    private void ToggleFavorite(AlchemyPreviewModel.Hand hand)
    {
        if(_favorites.Contains(hand))_favorites.Remove(hand);else if(_favorites.Count<3)_favorites.Add(hand);else{_model.Explanation="常用技法最多三个，请先移除一个。";Refresh();return;}
        Clear(_shortcutRow);_shortcutButtons.Clear();foreach(var item in _favorites){var capture=item;var b=RoomAction(AlchemyPreviewModel.HandNames[(int)item],()=>SelectHand(capture));b.CustomMinimumSize=new Vector2(110,38);_shortcutRow.AddChild(b);_shortcutButtons[item]=b;}Refresh();
    }
    private void UpdateRoomState()
    {
        _selectedMethod.Text=_model.Done?"本炉已结束 · 可重新开炉":$"第{_model.Breath}息 · 炉令："+AlchemyPreviewModel.HandNames[(int)_model.Action]+(_model.Pending.Count>0?$" · 待投{_model.Pending.Count}份":"");
        foreach(var p in _hands)p.Value.AddThemeStyleboxOverride("normal",ActionStyle(p.Key==_model.Action));
        foreach(var p in _shortcutButtons){p.Value.Disabled=_hands[p.Key].Disabled;p.Value.AddThemeStyleboxOverride("normal",ActionStyle(p.Key==_model.Action));}
        foreach(var p in _favoriteButtons){p.Value.Text=_favorites.Contains(p.Key)?"－":"＋";p.Value.Disabled=_demonstrating;}
        _techniqueButton.Disabled=_demonstrating||_model.Done;_guide.TooltipText=_model.Explanation;
        _demoNote.Hide();_selectedMethod.Show();
        int spiritCap=_model.Helper?AlchemyProcessConfig.HelperSpirit:AlchemyProcessConfig.MainSpirit;
        int mana=_model.Helper?_model.HelperMana:_model.Mana;
        _networkResources.Text=_model.AutoRunStarted
            ?$"并行 {_model.ActiveAutomaticLines}/{spiritCap}　·　轮候 {_model.QueuedAutomaticLines}线　·　法力 {mana}（实耗 {_model.AutomaticManaSpent}）"
            :$"灵识并行上限 {spiritCap}　·　已布线 {_model.SignedLinks.Count}　·　法力 {mana}　·　自动控火";
        _runButton.Disabled=_demonstrating||_autoRunInProgress;
        _runButton.Text=_model.Done?"重新开炉":_model.AutoRunActive?$"运行中 · 第{_model.AutomaticBreathsElapsed}息":"开炉炼丹";
        _infoToggle.Visible=!_roomDetails.Visible;_infoToggle.Text=_roomDetails.Visible?"炉内信息  ›":"‹  炉内信息";

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
    private void ToggleFurnaceInfoIfOpen(){if(_roomDetails.Visible)ToggleFurnaceInfo();}
    private void ChooseDose(string name)
    {
        double maximum=_model.FurnaceCapacity-_model.FurnaceMass;
        if(maximum<.1){_model.Explanation="丹炉容量已满；请先移除节点或重新开炉。";Refresh();return;}
        _doseName=name;_doseCount.MaxValue=Math.Floor(maximum*10)/10;_doseCount.Value=Math.Min(1,maximum);_doseDialog.Title=$"放入{name} · 还可放{maximum:0.#}份";_doseDialog.PopupCentered();
    }
    private void SyncRecipeChoices()
    {
        int pillIndex=Array.FindIndex(AlchemyRecipeCatalog.Pills,p=>p.Id==_model.SelectedPillId);
        _pillChoice.Select(Math.Max(0,pillIndex));_mainChoice.Clear();
        foreach(var name in _model.CurrentPill.MainCandidates)_mainChoice.AddItem(name);
        _mainChoice.Select(Math.Max(0,Array.IndexOf(_model.CurrentPill.MainCandidates,_model.SelectedMainHerbName)));
        _formulaChoice.Clear();
        foreach(var formula in _model.CurrentPill.Formulas)_formulaChoice.AddItem(formula.Name);
        int formulaIndex=Array.FindIndex(_model.CurrentPill.Formulas,f=>f.Id==_model.SelectedFormulaId);
        _formulaChoice.Select(Math.Max(0,formulaIndex));
        _pillChoice.Disabled=_mainChoice.Disabled=_formulaChoice.Disabled=!_model.CanChangeRecipe;
        _recipe.Title=$"丹药与参考丹方 · {_model.CurrentPill.Name}";
    }
    private void OnPillChoice(long index)
    {
        if(_updating)return;
        var definition=AlchemyRecipeCatalog.Pills[(int)index];
        if(!_model.SelectPill(definition.Id)){SyncRecipeChoices();return;}
        _recipeExpanded=false;_recipeDetailsButton.Text="查看具体判定";SyncRecipeChoices();_recipeRules.Text=RecipeSummary();Refresh();
    }
    private void OnFormulaChoice(long index)
    {
        if(_updating)return;
        var formula=_model.CurrentPill.Formulas[(int)index];
        if(!_model.SelectFormula(formula.Id)){SyncRecipeChoices();return;}
        _recipeExpanded=false;_recipeDetailsButton.Text="查看具体判定";SyncRecipeChoices();_recipeRules.Text=RecipeSummary();Refresh();
    }
    private void OnMainChoice(long index)
    {
        if(_updating)return;
        string selected=_model.CurrentPill.MainCandidates[(int)index];
        if(!_model.SelectMainHerb(selected)){SyncRecipeChoices();return;}
        _recipeExpanded=false;_recipeDetailsButton.Text="查看具体判定";SyncRecipeChoices();_recipeRules.Text=RecipeSummary();Refresh();
    }
    private void ShowRecipe(){_recipeExpanded=false;_recipeDetailsButton.Text="查看具体判定";SyncRecipeChoices();_recipeRules.Text=RecipeSummary();_recipe.PopupCentered();}
    private void ToggleRecipeDetails()
    {
        _recipeExpanded=!_recipeExpanded;
        _recipeDetailsButton.Text=_recipeExpanded?"收起具体判定":"查看具体判定";
        _recipeRules.Text=_recipeExpanded?RecipeSummary()+"\n\n"+_model.Checks():RecipeSummary();
    }
    private string RecipeSummary()
    {
        var pill=_model.CurrentPill;var formula=_model.CurrentFormula;
        string current=_model.Done
            ?"\n\n本炉结果："+_model.Result+"\n"+string.Join("\n",_model.Pills.Select(p=>$"{p.Index}号 · {p.Score}分 · {AlchemyPreviewModel.Tier(p.Score)} · 成品丹效：{p.QualityText}"))
            :$"\n\n本炉计划：{_model.Wanted}颗；丹效低于20不成丹。";
        var components=string.Join("\n",formula.Ingredients.Select(i=>
        {
            var herb=_model.Herbs.First(h=>h.Name==i.HerbName);
            string utility=string.Join("、",herb.Craft.utilities.Select(u=>$"{u.utility_id}{MaterialCraftData.utility_grade_name(u.grade)}"));
            return $"{(i.HerbName==formula.MainHerb?"方中主材":"建议辅材")}　{i.HerbName}　建议{i.Recommended}份　材料功用：{(utility.Length==0?"无":utility)}";
        }));
        var order=string.Join("\n",formula.FeedOrder.Select((name,index)=>
        {
            var ingredient=formula.Ingredients.FirstOrDefault(i=>i.HerbName==name);
            return $"{index+1}　{name}：{(ingredient?.Timing??"按当前炉温、阴阳偏向及已炼出的药性决定是否投入")}";
        }));
        string mainSummary=string.Join("、",pill.MainCandidates);
        double currentPolarity=_model.Available<.001?0:_model.Polarity;
        double currentTolerance=pill.PolarityTolerance(60);
        string mainMatch=_model.SelectedMainHerbName==formula.MainHerb?"本炉主材与参考方一致":"本炉主材与参考方不同：当前是自由改方试炼";

        string lockText=_model.CanChangeRecipe?"尚未投料，可调整丹种、主材和参考方。":"本炉已有实际操作，丹种、主材和参考方已锁定；重新开炉后可另选。";
        string operations=string.Join("\n",formula.Operations.Select(note=>"· "+note));
        return $"{pill.Name} · 丹阶{pill.Rank}品\n丹药用途：{pill.Purpose}\n\n【成丹要求】\n主材从候选中选一味：{mainSummary}；当前选择{_model.SelectedMainHerbName}。主材须含材料原表中的{pill.CoreUtility}功用，并实际炼出足够药性。主药每100份理论最多4颗。\n辅材不限于某张丹方；从材料架自由选择、替换、增减。材料的功用、质地、五行和阴阳按原表参与炉内过程。\n阴阳目标：{pill.PolarityTarget:+0.00;-0.00;0.00}，丹效20时容许偏差±{pill.PolarityToleranceAt20:F2}，丹效100时收紧至±{pill.PolarityToleranceAt100:F2}；当前炉内{currentPolarity:+0.00;-0.00;0.00}，丹效60时允许偏差±{currentTolerance:F2}。实际混匀、主药发挥、成形、药渣和损伤也须满足随丹效提高而收紧的条件。\n\n【参考丹方：{formula.Name}】\n{formula.Comparison}\n{mainMatch}\n建议搭配（仅作起点，不是成丹门槛；每份为100炉内质量单位）：\n{components}\n\n建议投料次序（可自由调整，不是固定阶段）：\n{order}\n\n操作手法参考：\n{operations}\n\n所有炼法使用同一成丹要求；实际组合能否成丹，只看本炉主材、药性发挥、炉况和最终成品，不检查是否凑齐参考方。丹效分档：20–50薄效 · 51–75良效 · 76–90优效 · 91–100极效；同丹效档使用固定效果，具体效果尚未配置\n\n{lockText}"+current;
    }
    public async void CaptureRoomLayout()
    {
        await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("room");
        var first=_model.InFurnace.FirstOrDefault();
        if(first is not null)
        {
            var position=_relationGraph.NodeLocalPosition(first.Id);
            void Click(){_relationGraph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=position});_relationGraph._GuiInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=position});}
            Click();await SaveRoomLayout("node-pinned");Click();_relationGraph._GuiInput(new InputEventMouseMotion{Position=Vector2.Zero});
        }
        ToggleFurnaceInfo();await SaveRoomLayout("sidebar");ToggleFurnaceInfo();_herbCloth.SetOpen(true);await ToSignal(GetTree().CreateTimer(.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("herb-cloth");_herbCloth.SetOpen(false);
    }
    private async System.Threading.Tasks.Task SaveRoomLayout(string name){if(_captureDirectory.Length==0)return;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);DirAccess.MakeDirRecursiveAbsolute(_captureDirectory);GetViewport().GetTexture().GetImage().SavePng(_captureDirectory.PathJoin($"layout-{name}.png"));}
    public async void ShowFirePreview(){_model.QueueHerb("青木芯");_model.Action=AlchemyPreviewModel.Hand.Raise;_model.Advance();_model.Action=AlchemyPreviewModel.Hand.Hold;_model.Advance();Refresh();await ToSignal(GetTree().CreateTimer(1.5),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("fire");int breath=_model.Breath;await ToSignal(GetTree().CreateTimer(.7),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("fire-later");GD.Print("ALCHEMY_IDLE_FIRE_BREATH_UNCHANGED "+(breath==_model.Breath));}
    public async void CaptureDepthLayers(){await ToSignal(GetTree().CreateTimer(2),SceneTreeTimer.SignalName.Timeout);_furnaceView.PreviewLayer(1);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("rear-half");_furnaceView.PreviewLayer(2);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("front-half");_furnaceView.PreviewLayer(0);await ToSignal(GetTree().CreateTimer(.4),SceneTreeTimer.SignalName.Timeout);await SaveRoomLayout("assembled");}
    public override void _UnhandledInput(InputEvent e){if(e.IsActionPressed("ui_cancel")&&(_herbCloth.Visible||_techniques.Visible||_roomDetails.Visible)){_herbCloth.SetOpen(false);_techniques.Hide();ToggleFurnaceInfoIfOpen();GetViewport().SetInputAsHandled();}}
    private static Button RoomButton(string text,Action action)
    {
        var b=Button(text,action);b.AddThemeFontSizeOverride("font_size",22);b.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());b.AddThemeStyleboxOverride("disabled",new StyleBoxEmpty());b.AddThemeStyleboxOverride("hover",new StyleBoxFlat{BgColor=new Color(.38f,.44f,.36f,.09f),BorderWidthBottom=1,BorderColor=Jade});return b;
    }
    internal static Texture2D LoadRoomTexture(string path)=>ResourceLoader.Exists(path)?GD.Load<Texture2D>(path):ImageTexture.CreateFromImage(Godot.Image.LoadFromFile(ProjectSettings.GlobalizePath(path)));
}
