using Godot;

/// <summary>人物局外配招。草稿、命名模板、默认开局三份数据分别提交。</summary>
public partial class CharacterCombatBoardEditor : Control
{
    private Character _actor=null!;
    private CombatBoardCatalog _catalog=null!;
    private CombatBoardLayout _draft=null!;
    private BattleBoardView _board=null!;
    private ColorRect _shade=null!;
    private Label _current=null!,_notice=null!,_listTitle=null!;
    private VBoxContainer _list=null!;
    private ScrollContainer _scroll=null!;
    private LineEdit _search=null!;
    private HBoxContainer _categories=null!;
    private Button _sourceFooter=null!;
    private readonly Dictionary<string,Button> _tabs=[],_libraryTabs=[];
    private readonly HashSet<string> _selection=[];
    private string _category="招式",_page="来源",_picked="";
    private int _turns;
    private CombatBoardLayout? _pickedCombination;
    private Action _persist=null!;
    private Action<CombatBoardLayout> _close=null!;
    private CombatBoardNameDialog? _dialog;
    private bool _closed;
    public CombatBoardLayout Draft=>_draft;
    public void Setup(Character actor,CombatBoardLayout? retained,Action persist,Action<CombatBoardLayout> close)
    {
        Name="CharacterCombatBoardEditor";_actor=actor;_catalog=CombatBoardPreparationService.Catalog(actor);
        _draft=retained?.CopyConfiguration()??CombatBoardPreparationService.GetDraft(actor);_persist=persist;_close=close;
        Size=BattleUiArt.Canvas;MouseFilter=MouseFilterEnum.Ignore;ProcessMode=ProcessModeEnum.Always;
        Theme=CraftingPaperStyle.MakeTheme();
        _shade=new ColorRect{Name="CharacterPreparationShade",Color=new(0.025f,.04f,.035f,.81f),MouseFilter=MouseFilterEnum.Stop};AddChild(_shade);
        _current=LightText("",new(290,115),new(820,82),30);CombatBoardUi.LimitLines(_current,2);_current.HorizontalAlignment=HorizontalAlignment.Center;AddChild(_current);
        var back=PlaceButton("返回",new(1534,18),new(114,46),Close);back.Name="CloseCharacterPreparation";
        CraftingPaperHeader.AddReturnArrow(back);
        var cell=Math.Min(120f,360f/Math.Max(_draft.size.X,_draft.size.Y));
        _board=new BattleBoardView{Name="CharacterPreparationBoard",Layout=_draft,Catalog=_catalog,CombatOwner=actor,
            CellSize=cell,Editable=true,Position=new(712-_draft.size.X*cell/2,452-_draft.size.Y*cell/2),Size=(Vector2)_draft.size*cell};
        _board.SelectedPlacements=_selection;AddChild(_board);
        _board.SelectedChanged+=Select;
        _board.CellPressed+=cell=>{if(_pickedCombination is not null)InsertCombination(_pickedCombination,cell);else if(_picked.Length>0)PlaceSource(_picked,cell);};
        _board.PlacementAllowed=p=>Issues(WithPlacement(p)).Count==0;
        _board.PlacementCommitted=CommitPlacement;
        _board.PlacementRemoved=id=>Remove([id]);
        _board.CombinationAllowed=items=>Issues(WithCombination(items)).Count==0;
        _board.CombinationCommitted=CommitCombination;
        _board.DragEnded=()=>{_picked="";_pickedCombination=null;_turns=0;Changed();};
        AddChild(new BattleBoardHandPreview{Name="CharacterPreparationHand",CellSize=cell,
            Position=_board.Position-new Vector2(cell*1.3f,0),Size=new(cell*2.3f,cell*1.5f)});
        var bottom=_board.Position.Y+_board.Size.Y;
        var save=PlaceButton("保存为方案",new(502,bottom+38),new(210,48),()=>Save(false));save.Name="SaveBoardAsPlan";
        var apply=PlaceButton("应用",new(730,bottom+38),new(192,48),()=>{if(Apply(_draft))Close();},true);apply.Name="ApplyCharacterPreparation";
        AddChild(LightText("拖入配招 · R / 右键转向 · 拖出移除\nShift / Ctrl 多选后可保存组合",new(355,bottom+113),new(714,65),19));
        _notice=LightText("",new(325,bottom+185),new(774,74),20);_notice.AutowrapMode=TextServer.AutowrapMode.WordSmart;AddChild(_notice);
        var panel=new Control{Name="CharacterPreparationLibrary",Position=new(1219,86),Size=new(429,835),MouseFilter=MouseFilterEnum.Stop};AddChild(panel);
        panel.AddChild(new BattleBoardSkinPanel{Part="source_panel",Size=panel.Size,MouseFilter=MouseFilterEnum.Ignore});
        var nav=new HBoxContainer{Position=new(23,23),Size=new(383,45)};nav.AddThemeConstantOverride("separation",9);panel.AddChild(nav);
        foreach(var name in new[]{"来源","组合","方案"})
        {
            var button=CombatBoardUi.Button(name,()=>ShowPage(name),font:24,battle:true);button.Name="PreparationTab"+name;
            button.CustomMinimumSize=new(121,45);button.ToggleMode=true;nav.AddChild(button);_libraryTabs[name]=button;
        }
        _categories=new HBoxContainer{Position=new(23,80),Size=new(383,36)};_categories.AddThemeConstantOverride("separation",5);panel.AddChild(_categories);
        foreach(var name in new[]{"招式","化法","丹药","符箓","法宝"})
        {
            var button=CombatBoardUi.Button(name,()=>{_category=name;ShowPage("来源");},font:21,battle:true);
            button.Name="PreparationCategory"+name;button.ToggleMode=true;button.CustomMinimumSize=new(72,35);_categories.AddChild(button);_tabs[name]=button;
        }
        _search=new LineEdit{Name="PreparationSearch",Position=new(23,127),Size=new(383,42),PlaceholderText="搜索名称…"};BattleBoardSkin.Skin(_search);panel.AddChild(_search);
        _search.TextChanged+=_=>{if(_page=="来源")RenderSources();};
        _listTitle=CombatBoardUi.Text("",27);_listTitle.Position=new(23,80);_listTitle.Size=new(383,52);panel.AddChild(_listTitle);
        _scroll=new ScrollContainer{Name="PreparationLibraryScroll",Position=new(23,183),Size=new(383,579),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};panel.AddChild(_scroll);
        _list=new VBoxContainer{Name="PreparationLibraryList",SizeFlagsHorizontal=SizeFlags.ExpandFill};_list.AddThemeConstantOverride("separation",12);_scroll.AddChild(_list);
        _sourceFooter=CombatBoardUi.Button("",()=>{if(_page=="组合")Save(true);else{_draft.placements=new(_draft.placements.Where(IsRequired));_selection.Clear();Changed();}},font:21,battle:true);
        _sourceFooter.Name="PreparationLibraryFooter";_sourceFooter.Position=new(23,778);_sourceFooter.Size=new(383,36);_sourceFooter.CustomMinimumSize=new(383,36);panel.AddChild(_sourceFooter);
        ShowPage("来源");Changed();BattleUiArt.Fit(this);_search.GrabFocus();
    }
    public override void _Process(double delta)
    {
        if(_closed||_shade is null)return;
        if(!GodotObject.IsInstanceValid(_actor)||!IsVisibleInTree()){Close();return;}
        BattleUiArt.Fit(this);_shade.Position=-Position/Scale;_shade.Size=GetViewportRect().Size/Scale;
    }
    public override void _Input(InputEvent e)
    {
        if(_closed||GodotObject.IsInstanceValid(_dialog)||!e.IsPressed()||e.IsEcho())return;
        if(e is InputEventKey {Keycode:Key.Tab} key)
        {
            var controls=FocusControls(this).ToArray();var current=GetViewport().GuiGetFocusOwner();var index=Array.IndexOf(controls,current);
            if(controls.Length>0)
            {
                var next=controls[(index+(key.ShiftPressed?-1:1)+controls.Length)%controls.Length];next.GrabFocus();
                for(var parent=next.GetParent();parent!=this&&parent is not null;parent=parent.GetParent())
                    if(parent is ScrollContainer scroll)scroll.EnsureControlVisible(next);
            }
            GetViewport().SetInputAsHandled();return;
        }
        if(e is InputEventKey {Keycode:Key.Escape})
        {
            if(!_board.DragActive)Close();else return;
            GetViewport().SetInputAsHandled();
        }
    }
    private static IEnumerable<Control> FocusControls(Node root)
    {
        foreach(var child in root.GetChildren())
        {
            if(child is Window)continue;
            if(child is Control control&&control.IsVisibleInTree()&&control.FocusMode!=FocusModeEnum.None&&!(control is BaseButton {Disabled:true}))yield return control;
            foreach(var nested in FocusControls(child))yield return nested;
        }
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if(_closed||GodotObject.IsInstanceValid(_dialog)||!e.IsPressed()||e.IsEcho()||GetViewport().GuiGetFocusOwner() is LineEdit||_board.DragActive)return;
        if(e is InputEventKey {Keycode:Key.R}){Rotate();GetViewport().SetInputAsHandled();}
        if(e is InputEventKey {Keycode:Key.Delete}){Remove(_selection.ToArray());GetViewport().SetInputAsHandled();}
    }
    private Button PlaceButton(string text,Vector2 pos,Vector2 size,Action action,bool primary=false)
    {var b=CombatBoardUi.Button(text,action,primary,24,true);b.Position=pos;b.Size=size;b.CustomMinimumSize=size;AddChild(b);return b;}
    private static Label LightText(string text,Vector2 pos,Vector2 size,int font)
    {var t=CombatBoardUi.Text(text,font,wrap:true);t.Position=pos;t.Size=size;t.HorizontalAlignment=HorizontalAlignment.Center;t.AddThemeColorOverride("font_color",BattleBoardSkin.PaperText);return t;}
    private List<string> Issues(CombatBoardLayout layout)=>CombatBoardPreparationService.Validate(_actor,layout);
    private CombatBoardLayout WithPlacement(CombatBoardPlacement p)
    {
        var next=_draft.CopyConfiguration();var old=next.placements.FirstOrDefault(item=>item.placement_id==p.placement_id);
        if(old is not null)next.placements.Remove(old);next.placements.Add(p.CopyConfiguration());return next;
    }
    private CombatBoardLayout WithCombination(IReadOnlyList<CombatBoardPlacement> items)
    {var next=_draft.CopyConfiguration();foreach(var item in items)next.placements.Add(item.CopyConfiguration());return next;}
    private bool CommitPlacement(CombatBoardPlacement item)
    {
        var next=WithPlacement(item);var issues=Issues(next);if(issues.Count>0){_notice.Text=issues[0];return false;}
        _draft=next;_selection.Clear();_selection.Add(item.placement_id);_picked="";Changed();return true;
    }
    public bool PlaceSource(string key,Vector2I cell)
    {var item=CombatBoardPreparationService.CreatePlacement(_catalog,key,cell,_turns);return item is not null&&CommitPlacement(item);}
    public bool InsertCombination(CombatBoardLayout saved,Vector2I cell)
    {
        var normalized=CombatBoardCombinations.Normalize(saved,_catalog);
        var items=normalized.placements.Select(p=>{var copy=p.CopyConfiguration();copy.placement_id=Guid.NewGuid().ToString("N");copy.cell+=cell;return copy;}).ToArray();
        return CommitCombination(items);
    }
    private bool CommitCombination(IReadOnlyList<CombatBoardPlacement> items)
    {
        var next=WithCombination(items);var issues=Issues(next);if(issues.Count>0){_notice.Text=issues[0]+"；盘面保持。";return false;}
        _draft=next;_selection.Clear();foreach(var item in items)_selection.Add(item.placement_id);
        _pickedCombination=null;_picked="";Changed();return true;
    }
    private void Remove(IReadOnlyCollection<string> ids)
    {
        _draft.placements=new(_draft.placements.Where(p=>!ids.Contains(p.placement_id)||IsRequired(p)));_selection.Clear();Changed();
    }
    private bool IsRequired(CombatBoardPlacement item)=>item.locked&&(item.kind!=CombatBoardPlacement.ObjectKind.ZhouTianIndicator||CombatBoardPreparationService.RequiresIndicator(_actor));
    private void Select(string id)
    {
        _search.ReleaseFocus();
        if(Input.IsKeyPressed(Key.Shift)||Input.IsKeyPressed(Key.Ctrl))
        {if(!_selection.Add(id))_selection.Remove(id);}
        else{_selection.Clear();if(id.Length>0)_selection.Add(id);}
        _selection.Remove("");_board.Selected="";_board.QueueRedraw();UpdateSelectionFooter();
    }
    private void Rotate()
    {
        if(_pickedCombination is not null){_pickedCombination=CombatBoardCombinations.Rotate(_pickedCombination,_catalog);return;}
        if(_picked.Length>0){_turns=(_turns+1)%4;return;}
        if(_selection.Count!=1)return;
        var old=_draft.placements.FirstOrDefault(p=>_selection.Contains(p.placement_id));if(old is null)return;
        var item=CombatBoardPreparationService.CreatePlacement(_catalog,old.source_key,old.cell,
            old.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator?old.rewrite_direction+1:old.quarter_turns+1);
        if(item is null)return;item.placement_id=old.placement_id;CommitPlacement(item);
    }
    private void Changed()
    {
        _board.Layout=_draft;_board.Selected="";_board.QueueRedraw();
        var applied=(_actor.entity_data as HumanData)?.combat_board_preparation;
        var isApplied=applied is not null&&CombatBoardPreparationService.Signature(applied)==CombatBoardPreparationService.Signature(_draft);
        _current.Text="当前应用："+(applied?.Title is {Length:>0}title?title:"尚未应用")+(isApplied?"":"\n正在配置");
        var issues=Issues(_draft);_notice.Text=issues.Count>0?issues[0]:"";UpdateSelectionFooter();
    }
    private void UpdateSelectionFooter()
    {
        if(_sourceFooter is null)return;_sourceFooter.Text=_page=="组合"?$"保存当前选择（{_selection.Count}项）":"清空盘面";
        _sourceFooter.Disabled=_page=="组合"&&_selection.Count==0;
    }
    public void ShowPage(string page)
    {
        _page=page;_picked="";_pickedCombination=null;_turns=0;
        foreach(var (name,b) in _libraryTabs)b.ButtonPressed=name==page;
        foreach(var (name,b) in _tabs)b.ButtonPressed=name==_category;
        var source=page=="来源";_categories.Visible=_search.Visible=source;_listTitle.Visible=!source;
        _sourceFooter.Visible=page!="方案";_scroll.Position=new(23,source?183:143);_scroll.Size=new(383,page=="方案"?670:619);
        _scroll.ScrollVertical=0;UpdateSelectionFooter();
        if(source)RenderSources();else RenderLibrary();
    }
    private void RenderSources()
    {
        CombatBoardUi.Clear(_list);var query=_search.Text.Trim();
        var entries=CombatBoardOwnedSources.Read(_actor);
        if(CombatBoardPreparationService.RequiresIndicator(_actor))entries.Add(new("heart:xuan_shu","化法","周天指示","玄枢含章","", "",Vector2I.One,true,
            new("周天指示","玄枢含章的必需指示，不能移出。")));
        foreach(var entry in entries.Where(e=>e.Category==_category&&e.Configurable)
            .Where(e=>e.Matches(query)))
        {
            var placement=CombatBoardPreparationService.CreatePlacement(_catalog,entry.Key,Vector2I.Zero);if(placement is null)continue;
            var shape=CombatBoardGeometry.Footprint(placement,_catalog.FindSkill);var sample=new CombatBoardLayout{size=shape};sample.placements.Add(placement);
            var cell=Math.Min(54f,150f/Math.Max(1,shape.X));
            var source=new BattleBoardSource{Name="PreparationSource",SourceKey=entry.Key,Board=_board,
                CustomMinimumSize=new(0,Math.Max(98,shape.Y*cell+20)),SizeFlagsHorizontal=SizeFlags.ExpandFill,MouseDefaultCursorShape=CursorShape.Drag};
            source.SetMeta("source_key",entry.Key);source.AddChild(new BattleBoardView{Layout=sample,Catalog=_catalog,CombatOwner=_actor,
                CellSize=cell,Position=new(5,10),Size=(Vector2)shape*cell,MouseFilter=MouseFilterEnum.Ignore});
            var title=CombatBoardUi.Text(entry.Name+(entry.Category is "丹药" or "符箓"?"\n"+entry.Detail:""),23,wrap:true);title.Position=new(174,10);title.Size=new(180,source.CustomMinimumSize.Y-20);title.VerticalAlignment=VerticalAlignment.Center;source.AddChild(title);
            CombatBoardUi.LimitLines(title,2);
            source.Picked=()=>{_picked=entry.Key;_pickedCombination=null;_turns=0;_selection.Clear();_search.ReleaseFocus();_notice.Text="点击空位摆入，R转向。";_board.QueueRedraw();};
            HoverInfoManager.SetInfo(source,entry.Hover);_list.AddChild(source);
        }
        if(_list.GetChildCount()==0)AddEmpty($"暂无可配招的{_category}");
    }
    private void RenderLibrary()
    {
        CombatBoardUi.Clear(_list);var library=CombatBoardPreparationService.GetLibrary(_actor);var combo=_page=="组合";
        var items=combo?library.combinations:library.layouts;_listTitle.Text=$"我的{_page} · {items.Count}{(combo?"组":"套")}";
        foreach(var saved in items)
        {
            var layout=saved.CopyConfiguration();
            if(combo)
            {
                var normalized=CombatBoardCombinations.Normalize(layout,_catalog);
                var row=new VBoxContainer{Name="SavedCombination",SizeFlagsHorizontal=SizeFlags.ExpandFill};row.SetMeta("preset_id",layout.preset_id);row.AddThemeConstantOverride("separation",8);_list.AddChild(row);
                var source=new BattleBoardCombinationSource{Board=_board,Layout=normalized,CustomMinimumSize=new(0,112),SizeFlagsHorizontal=SizeFlags.ExpandFill,MouseDefaultCursorShape=CursorShape.Drag};row.AddChild(source);
                var mini=CombatBoardUi.Mini(normalized,_catalog,86);mini.Position=new(5,10);source.AddChild(mini);
                var title=CombatBoardUi.Text(layout.Title,24,wrap:true);CombatBoardUi.LimitLines(title,2);title.Position=new(108,12);title.Size=new(243,86);source.AddChild(title);
                HoverInfoManager.SetInfo(source,new(layout.Title,$"{layout.placements.Count}项 · 拖入整组搭配。"));
                source.Picked=()=>{_pickedCombination=normalized.CopyConfiguration();_picked="";_selection.Clear();_board.QueueRedraw();_notice.Text="点击落点摆入整组，R转向。";};
                var controls=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.End};controls.AddThemeConstantOverride("separation",10);row.AddChild(controls);
                controls.AddChild(CombatBoardUi.Button("改名",()=>Rename(layout,true),font:19,battle:true));
                controls.AddChild(CombatBoardUi.Button("删除",()=>{if(CombatBoardPreparationService.TryDeleteCombination(_actor,layout.preset_id,out var reason)){_persist();RenderLibrary();}else _notice.Text=reason;},font:19,battle:true));
            }
            else
            {
                var row=new PanelContainer{Name="SavedPlan",SizeFlagsHorizontal=SizeFlags.ExpandFill,CustomMinimumSize=new(0,132)};
                row.SetMeta("preset_id",layout.preset_id);
                row.AddThemeStyleboxOverride("panel",CraftingPaperStyle.Paper(padding:12,brightness:.96f));_list.AddChild(row);
                var columns=new HBoxContainer();columns.AddThemeConstantOverride("separation",12);row.AddChild(columns);
                columns.AddChild(CombatBoardUi.Mini(layout,_catalog,72));
                var words=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ShrinkCenter};words.AddThemeConstantOverride("separation",8);columns.AddChild(words);
                var title=CombatBoardUi.Text(layout.Title,23,wrap:true);CombatBoardUi.LimitLines(title,3);words.AddChild(title);
                var issues=Issues(layout);var applied=(_actor.entity_data as HumanData)?.combat_board_preparation;
                var current=applied is not null&&applied.preset_id==layout.preset_id&&CombatBoardPreparationService.Signature(applied)==CombatBoardPreparationService.Signature(layout);
                var status=issues.Count>0?issues[0]:current?"当前应用":applied?.preset_id==layout.preset_id?"已保存 · 尚未应用":$"{layout.placements.Count}项 · 可以应用";
                words.AddChild(CombatBoardUi.Text(status,17,true,true));
                HoverInfoManager.SetInfo(row,new(layout.Title,status));
                var actions=new VBoxContainer{SizeFlagsVertical=SizeFlags.ShrinkCenter};actions.AddThemeConstantOverride("separation",8);columns.AddChild(actions);
                var apply=CombatBoardUi.Button("应用",()=>{if(Apply(layout)){_draft=layout.CopyConfiguration();Changed();RenderLibrary();}},font:19,battle:true);
                apply.Name="ApplySavedPlan";apply.CustomMinimumSize=new(70,38);apply.Disabled=issues.Count>0;actions.AddChild(apply);
                var rename=CombatBoardUi.Button("改名",()=>Rename(layout,false),font:19,battle:true);rename.Name="RenameSavedPlan";rename.CustomMinimumSize=new(70,38);actions.AddChild(rename);
            }
        }
        if(items.Count==0)AddEmpty(combo?"选择盘中招式，保存常用搭配。":"在盘下选择“保存为方案”。");
    }
    private void AddEmpty(string text)=>_list.AddChild(CombatBoardUi.Text(text,21,true,true));
    private void Save(bool combination)
    {
        var layout=_draft.CopyConfiguration();
        if(combination)
        {
            layout.placements=new(layout.placements.Where(p=>_selection.Contains(p.placement_id)));
            if(layout.placements.Count==0){_notice.Text="请先选择盘中的招式。";return;}layout=CombatBoardCombinations.Normalize(layout,_catalog);
        }
        var issues=CombatBoardPreparationService.Validate(_actor,layout,combination);if(issues.Count>0){_notice.Text=issues[0];return;}
        var caption=combination?"组合":"方案";_dialog=new();AddChild(_dialog);
        _dialog.Open("保存为"+caption,caption+"名称",combination?"常用组合":_draft.Title.Length>0?_draft.Title:"日常配招",false,(name,overwrite)=>
        {
            if(!CombatBoardPreparationService.TrySave(_actor,layout,name,combination,overwrite,out var id,out var reason))return reason;
            if(!combination){_draft.preset_id=id;_draft.Title=name;}
            _persist();ShowPage(caption);Changed();ScrollToSaved(id,caption);return null;
        },name=>(combination?CombatBoardPreparationService.GetLibrary(_actor).combinations:CombatBoardPreparationService.GetLibrary(_actor).layouts).Any(p=>p.Title==name));
    }
    private async void ScrollToSaved(string id,string page)
    {
        var tree=GetTree();await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        if(!GodotObject.IsInstanceValid(this)||!IsInsideTree()||_closed||_page!=page)return;
        var row=_list.GetChildren().OfType<Control>().FirstOrDefault(node=>node.GetMeta("preset_id","").AsString()==id);
        if(row is not null)_scroll.EnsureControlVisible(row);
    }
    private void Rename(CombatBoardLayout layout,bool combination)
    {
        _dialog=new();AddChild(_dialog);_dialog.Open(combination?"组合改名":"方案改名",combination?"组合名称":"方案名称",layout.Title,true,(name,_)=>
        {
            if(!CombatBoardPreparationService.TryRename(_actor,layout.preset_id,name,combination,out var reason))return reason;
            if(!combination&&_draft.preset_id==layout.preset_id)_draft.Title=name;
            _persist();Changed();RenderLibrary();return null;
        });
    }
    private bool Apply(CombatBoardLayout layout)
    {
        if(!CombatBoardPreparationService.TryApply(_actor,layout,out var reason)){_notice.Text=reason;return false;}
        _persist();Changed();return true;
    }
    public void Close()
    {
        if(_closed)return;_closed=true;HoverInfoManager.HideFor(this);_close(_draft.CopyConfiguration());
    }
}
