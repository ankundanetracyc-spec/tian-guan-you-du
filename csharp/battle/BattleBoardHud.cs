using Godot;

/// <summary>配招暗幕、真实格形来源、草稿提交与战中小盘。</summary>
public partial class BattleBoardHud : Control
{
    public const int SourceCellSize=54;
    private BattleManager _manager = null!;
    private CombatBoardCatalog _catalog = null!;
    private GodotObject _owner = null!;
    private CombatBoardLayout _draft = null!;
    private CombatBoardLayout _entryLayout = null!;
    private Control _editor = null!;
    private BattleBoardView _board = null!, _mini = null!;
    private VBoxContainer _sources = null!;
    private Label _notice = null!, _round = null!;
    private readonly Label[] _resourceValues = new Label[3];
    private Button _edit = null!, _start = null!, _pause = null!, _speed = null!;
    private LineEdit _search = null!;
    private ColorRect _shade = null!;
    private ScrollContainer _sourceScroll = null!;
    private Control _planPage = null!,_combinationPage=null!;
    private Control _turnChoicePanel = null!, _enemyIntentPanel = null!;
    private Label _turnChoiceTitle = null!, _enemyIntentLabel = null!;
    private Control _miniPrompt = null!;
    private GridContainer _turnChoiceOptions = null!;
    private VBoxContainer _planList = null!;
    private VBoxContainer _combinationList=null!;
    private readonly Dictionary<string,Button> _libraryTabs=[];
    private Control _categories=null!,_clearSource=null!,_resourcePanel=null!;
    private CombatBoardLayout? _pickedCombination;
    private CombatBoardPresetLibrary _planLibrary = new();
    private readonly Dictionary<string,Button> _tabs = [];
    private bool _shadePressed;
    private GridContainer _gauges = null!;
    private readonly List<(GodotObject Owner, Label Label, ProgressBar Bar)> _gaugeViews = [];
    private string _tab = "招式", _selected = "", _pickedSource = "";
    private int _pickedTurns;
    private bool _sourceDragging;
    private string _turnChoiceKey = "";
    private const float MiniCellSize=64f,MiniMaxExtent=240f,MiniMargin=28f,MiniDockDuration=.20f;
    private bool _miniWasVisible,_miniDocking;
    private float _miniDockAge,_miniDockFromCell;
    private Vector2 _miniDockFromPosition;
    public CombatBoardLayout Draft => _draft;
    public bool EditorVisible => _editor.Visible;

    public void Setup(BattleManager manager)
    {
        _manager = manager; _owner = manager.get_main_player_unit()!;
        _catalog = manager.get_board_catalog(_owner)!;
        _draft = manager.get_board_state(_owner)!.Layout.CopyConfiguration();
        _entryLayout=_draft.CopyConfiguration();_entryLayout.Title="入场配置";
        Name = "BattleBoardHud"; ProcessMode = ProcessModeEnum.Always; MouseFilter = MouseFilterEnum.Ignore; ZIndex = 80;
        Theme = new Theme(); Theme.SetStylebox("panel","TooltipPanel",new StyleBoxEmpty());
        Size = BattleUiArt.Canvas;
        _round = Text("", new(550, 20), new(550, 30), 21); AddChild(_round);
        _round.AddThemeColorOverride("font_color",BattleBoardSkin.PaperText);
        _gauges = new() { Position = new(552, 56), Size = new(530, 0), Columns = Math.Min(3, manager.all_units.Count), MouseFilter = MouseFilterEnum.Ignore };
        _gauges.AddThemeConstantOverride("h_separation", 12); AddChild(_gauges);
        foreach (var actor in manager.all_units)
        {
            var block = new VBoxContainer { CustomMinimumSize = new(166, 36), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            var name = new Label { MouseFilter = MouseFilterEnum.Ignore }; name.AddThemeFontSizeOverride("font_size", 14); block.AddChild(name);
            var progress = new ProgressBar { MinValue = 0, MaxValue = 1000, ShowPercentage = false, CustomMinimumSize = new(166, 5), MouseFilter = MouseFilterEnum.Ignore };
            progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("252e2d") });
            progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("b4a078") });
            block.AddChild(progress); _gauges.AddChild(block); _gaugeViews.Add((actor, name, progress));
        }
        var headerButtonSize = new Vector2(122, 48);
        _edit = Button("返回 ▸", new(1526, 21), headerButtonSize);_edit.AddThemeFontSizeOverride("font_size",23);_edit.Name="BoardModeToggle";
        _edit.Pressed += () => { if(_editor.Visible)CloseEditor();else manager.request_board_edit(); };AddChild(_edit);
        _speed = Button("倍速 ×1", new(1246, 21), headerButtonSize);_speed.AddThemeFontSizeOverride("font_size",23); _speed.Pressed += () => manager.cycle_battle_speed(); AddChild(_speed);
        _pause = Button("暂停", new(1386, 21), headerButtonSize);_pause.AddThemeFontSizeOverride("font_size",23); _pause.Pressed += manager.toggle_battle_pause; AddChild(_pause);
        var resourcePanel = Panel(new(626, 803), new(420, 118),"resource_frame");_resourcePanel=resourcePanel; resourcePanel.Name="Resources"; AddChild(resourcePanel);
        for(var i=0;i<3;i++)
        {
            var glyph=new TextureRect { ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture=BattleBoardSkin.Part(new[]{"stamina_glyph","mana_glyph","spirit_glyph"}[i]),Position=new(29,19+i*28), Size=new(20,20),MouseFilter=MouseFilterEnum.Ignore };
            resourcePanel.AddChild(glyph);
            var caption=Text(new[]{"体力","法力","灵识占用"}[i],new(62,16+i*28),new(118,26),19);
            caption.VerticalAlignment=VerticalAlignment.Center;resourcePanel.AddChild(caption);
            var value=Text("",new(184,16+i*28),new(206,26),19);
            value.Name=new[]{"StaminaValue","ManaValue","SpiritValue"}[i];
            value.HorizontalAlignment=HorizontalAlignment.Right;value.VerticalAlignment=VerticalAlignment.Center;
            resourcePanel.AddChild(value);_resourceValues[i]=value;
        }
        _mini = new() { Name="MiniBoard",Visible=false,CellSize=MiniCellSize,Catalog=_catalog,CombatOwner=_owner,Manager=manager,Editable=false,MouseFilter=MouseFilterEnum.Stop };
        _mini.RowPressed += row => { if(_manager.is_board_choosing_row())_manager.select_board_row(row); };
        AddChild(_mini);
        _miniPrompt=Panel(Vector2.Zero,new(270,38),"tooltip_panel");_miniPrompt.Name="MiniBoardRowPrompt";_miniPrompt.Visible=false;_miniPrompt.ZIndex=30;
        var miniPromptText=Text("点击一行，选择本回合出手",new(12,6),new(246,26),17);
        miniPromptText.AddThemeColorOverride("font_color",BattleBoardSkin.PaperText);_miniPrompt.AddChild(miniPromptText);AddChild(_miniPrompt);
        _turnChoicePanel = Panel(new(1096, 114), new(522, 204)); _turnChoicePanel.Name="TurnChoicePanel"; _turnChoicePanel.Visible=false; _turnChoicePanel.ZIndex=20; AddChild(_turnChoicePanel);
        _turnChoiceTitle = Text("", new(20, 13), new(482, 42), 23); _turnChoicePanel.AddChild(_turnChoiceTitle);
        _turnChoiceOptions = new() { Name="TurnChoiceOptions", Position=new(18, 62), Size=new(486, 128), Columns=3, MouseFilter=MouseFilterEnum.Stop };
        _turnChoiceOptions.AddThemeConstantOverride("h_separation", 8); _turnChoiceOptions.AddThemeConstantOverride("v_separation", 8); _turnChoicePanel.AddChild(_turnChoiceOptions);
        _enemyIntentPanel = Panel(new(1096, 72), new(522, 38)); _enemyIntentPanel.Name="EnemyTargetIntent"; _enemyIntentPanel.Visible=false; _enemyIntentPanel.ZIndex=20; AddChild(_enemyIntentPanel);
        _enemyIntentLabel = Text("", new(12, 5), new(498, 28), 17); _enemyIntentLabel.AutowrapMode=TextServer.AutowrapMode.WordSmart; _enemyIntentPanel.AddChild(_enemyIntentLabel);
        _editor = new Control { Name = "Editor", Size = BattleUiArt.Canvas, MouseFilter = MouseFilterEnum.Ignore }; AddChild(_editor);
        _shade = new ColorRect { Name="Backdrop",Color = new Color(.03f,.055f,.043f,.78f), Size = BattleUiArt.Canvas, MouseFilter = MouseFilterEnum.Stop };
        _shade.GuiInput += e =>
        {
            if(e is not InputEventMouseButton { ButtonIndex:MouseButton.Left } click)return;
            if(click.Pressed)_shadePressed=!GetViewport().GuiIsDragging()&&_board?.DragActive!=true;
            else { if(_shadePressed && !_sourceDragging && !GetViewport().GuiIsDragging()&&_board?.DragActive!=true)CloseEditor(); _shadePressed=false; }
            _shade.AcceptEvent();
        };
        _editor.AddChild(_shade);
        var cellSize = Math.Min(120f, 360f / Math.Max(_draft.size.X, _draft.size.Y));
        _board = new() { Name = "Board", Catalog = _catalog, Layout = _draft, CombatOwner = _owner, Manager = manager, CellSize = cellSize, Editable = true,
            Position = new(712 - _draft.size.X * cellSize / 2, 452 - _draft.size.Y * cellSize / 2), Size = (Vector2)_draft.size * cellSize };
        _board.SelectedChanged += id => { _selected = id;_search?.ReleaseFocus(); };
        _board.CellPressed += cell => { if(_pickedCombination is not null)TryInsertCombination(_pickedCombination,cell);else if (_pickedSource.Length > 0) TryPlaceSource(_pickedSource, cell, _pickedTurns); };
        _board.DropRequested += (key, cell) => { if (key.StartsWith("source:")) TryPlaceSource(key[7..], cell, _pickedTurns); else MovePlacement(key, cell); };
        _board.PlacementAllowed=CanPlaceDragged;
        _board.PlacementCommitted=CommitDragged;
        _board.PlacementRemoved=RemoveDragged;
        _board.CombinationAllowed=CanInsertCombination;
        _board.CombinationCommitted=CommitCombination;
        _board.DragEnded=()=> { _pickedSource="";_pickedCombination=null;_pickedTurns=0;_shadePressed=false;_board.Selected=_selected;_board.QueueRedraw(); };
        _editor.AddChild(_board);
        _editor.AddChild(new BattleBoardHandPreview { Name="HandPreview",CellSize=cellSize,
            Position=_board.Position-new Vector2(cellSize*1.3f,0),Size=new(cellSize*2.3f,cellSize*1.45f) });
        var bottom = _board.Position.Y+_draft.size.Y*cellSize;
        _start = Button("开始战斗", new(597, bottom + 30), new(230, 48),true);_start.AddThemeFontSizeOverride("font_size",26); _start.Name="BeginCombat";_start.Pressed += Commit; _editor.AddChild(_start);
        _notice = Text("", new(410, bottom + 90), new(604, 50), 18);_notice.HorizontalAlignment=HorizontalAlignment.Center; _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart; _editor.AddChild(_notice);
        _notice.AddThemeColorOverride("font_color",BattleBoardSkin.PaperText);
        var panel = Panel(new(1219, 86), new(429, 835)); panel.Name="SourcePanel"; _editor.AddChild(panel);
        var libraryTabs=new HBoxContainer { Name="LibraryTabs",Position=new(23,23),Size=new(383,45) };libraryTabs.AddThemeConstantOverride("separation",9);panel.AddChild(libraryTabs);
        foreach(var name in new[]{"来源","组合","方案"})
        {
            var tab=Button(name,Vector2.Zero,new(121,45));tab.ToggleMode=true;tab.AddThemeFontSizeOverride("font_size",24);libraryTabs.AddChild(tab);_libraryTabs[name]=tab;
            tab.Pressed+=()=>{if(name=="来源")ShowSources();else if(name=="组合")ShowCombinations();else ShowPlans();};
        }
        _libraryTabs["来源"].ButtonPressed=true;
        var tabs = new HBoxContainer { Name="Categories",Position = new(23, 80), Size = new(383, 35) };_categories=tabs; tabs.AddThemeConstantOverride("separation",5); panel.AddChild(tabs);
        foreach (var tab in new[] { "招式", "化法", "丹药", "符箓", "法宝" })
        {
            var button = Button(tab, Vector2.Zero, new(72, 34));button.AddThemeFontSizeOverride("font_size",21); button.ToggleMode=true;tabs.AddChild(button);_tabs[tab]=button;
            button.Pressed += () => SelectCategory(tab);
        }
        _tabs[_tab].ButtonPressed=true;
        _search = new() { Name="Search",Position = new(23, 127), Size = new(383, 42), PlaceholderText = "搜索名称…" }; BattleBoardSkin.Skin(_search);_search.AddThemeFontSizeOverride("font_size",20); panel.AddChild(_search);
        _search.TextChanged += _ => RebuildSources();
        _sourceScroll = new ScrollContainer { Name="SourceScroll", Position = new(23, 183), Size = new(383, 579), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; panel.AddChild(_sourceScroll);
        _sources = new() { Name="Sources",CustomMinimumSize = new(363, 0),SizeFlagsHorizontal=SizeFlags.ExpandFill }; _sources.AddThemeConstantOverride("separation", 0); _sourceScroll.AddChild(_sources);
        var clear = Button("清空盘面", new(23, 778), new(383, 36));_clearSource=clear;clear.AddThemeFontSizeOverride("font_size",22); clear.Pressed += () => { _draft.placements = new(_draft.placements.Where(item => item.locked)); Changed(); }; panel.AddChild(clear);
        BuildPlanPage(panel);
        BuildCombinationPage(panel);
        RebuildSources();
        if (_draft.migrated_from_legacy && !_draft.confirmed && _draft.pending_entries.Count > 0)
            _notice.Text = $"旧配置有{_draft.pending_entries.Count}项未摆入，请检查后开始；已学招式与原物品保留。";
        MoveChild(resourcePanel, GetChildCount()-1);
        MoveChild(_edit,GetChildCount()-1);
        BattleUiArt.Fit(this);
    }

    public override void _Process(double delta)
    {
        if (_manager is null || _owner is null || !GodotObject.IsInstanceValid(_owner)
            || _manager.get_board_state(_owner) is null) return;
        BattleUiArt.Fit(this);
        _shade.Position=-Position/Scale;
        _shade.Size=GetViewportRect().Size/Scale;
        if (!_manager.battle_running) { Hide(); return; }
        RefreshModeButton();
        RefreshTurnChoicePanel();
        var enemyIntent = _manager.get_enemy_board_target_intents();
        _enemyIntentLabel.Text = enemyIntent.Length == 0 ? "" : "敌方目标：" + enemyIntent;
        _enemyIntentPanel.Visible = !_editor.Visible && enemyIntent.Length > 0;
        _pause.Text = _manager.is_battle_paused() ? "继续" : "暂停";
        _pause.Visible=_speed.Visible=!_editor.Visible;
        _speed.Text = $"倍速 ×{_manager.get_battle_speed_scale():0}";
        _resourceValues[0].Text=$"{_owner.Get("current_stamina").AsInt32()} / {_owner.Get("max_stamina").AsInt32()}";
        _resourceValues[1].Text=$"{_owner.Get("current_mana").AsInt32()} / {_manager.get_effective_mana_capacity(_owner)}";
        _resourceValues[2].Text=$"{_owner.Get("used_spirit_perception").AsInt32()} / {_owner.Get("max_spirit_perception").AsInt32()}";
        var state = _manager.get_board_state(_owner)!;
        _round.Visible = !_editor.Visible;
        _gauges.Visible = !_editor.Visible && !_manager.is_board_editing();
        foreach (var gauge in _gaugeViews)
        {
            if (!GodotObject.IsInstanceValid(gauge.Owner)) continue;
            var currentResolve = _manager.get_board_resolve_current(gauge.Owner);
            var maxResolve = Math.Max(1, _manager.get_board_resolve_max(gauge.Owner));
            gauge.Label.Text = gauge.Owner.Get("entity_name").AsString() + $" · 定力 {currentResolve}/{maxResolve}"
                + (_manager.is_board_dazed(gauge.Owner) ? " · 失神" : "");
            gauge.Bar.MaxValue = maxResolve;
            gauge.Bar.Value = currentResolve;
        }
        _round.Text = _manager.is_board_editing() ? "配招后开始战斗" : _manager.is_board_choosing_row()
            ? $"第{_manager.get_board_round()}回合 · 选择出手行"
            : _manager.get_board_target_prompt().Length > 0 ? $"第{_manager.get_board_round()}回合 · 选择目标"
            : $"第{_manager.get_board_round()}回合 · " + (state.SelectedRow >= 0 ? $"第{state.SelectedRow + 1}行交锋" : "交锋准备中");
        UpdateCombatMini(state,delta);
        _resourcePanel.Position=_editor.Visible?new(502,803):new(626,803);
        _sourceDragging = GetViewport().GuiIsDragging()||_board.DragActive;
        _notice.Visible=!_sourceDragging;
        _board.Editable=_manager.is_board_editing()&&_editor.Visible;
    }
    private void UpdateCombatMini(CombatBoardRoundState state,double delta)
    {
        var visible=!_manager.is_board_editing()&&!_editor.Visible;
        _mini.Layout=state.Layout;_mini.Runtime=state;_mini.Visible=visible;
        var choosingRow=visible&&_manager.is_board_choosing_row();
        _mini.SetRowSelectionEnabled(choosingRow);_miniPrompt.Visible=choosingRow;
        var cell=Math.Min(MiniCellSize,MiniMaxExtent/Math.Max(state.Layout.size.X,state.Layout.size.Y));
        var destination=new Vector2(MiniMargin,BattleUiArt.Canvas.Y-MiniMargin-state.Layout.size.Y*cell);
        if(visible&&!_miniWasVisible)
        {
            // 与中央配置使用同一布局、格形及材质，开战时连续缩移到左下。
            _miniDocking=true;_miniDockAge=0;
            _miniDockFromPosition=_board.Position;_miniDockFromCell=_board.CellSize;
        }
        if(!visible)_miniDocking=false;
        if(_miniDocking)
        {
            if(!_manager.is_battle_paused())_miniDockAge+=(float)delta;
            var progress=Mathf.Clamp(_miniDockAge/MiniDockDuration,0,1);
            var eased=1-Mathf.Pow(1-progress,3);
            _mini.Position=_miniDockFromPosition.Lerp(destination,eased);
            _mini.CellSize=Mathf.Lerp(_miniDockFromCell,cell,eased);
            if(progress>=1)_miniDocking=false;
        }
        else {_mini.Position=destination;_mini.CellSize=cell;}
        _miniPrompt.Position=_mini.Position+new Vector2(0,-42);
        _mini.Size=(Vector2)state.Layout.size*_mini.CellSize;_mini.QueueRedraw();_miniWasVisible=visible;
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!_editor.Visible || !e.IsPressed() || e.IsEcho() || GetViewport().GuiGetFocusOwner() is LineEdit) return;
        if (e is InputEventKey { Keycode: Key.R }) { RotateSelected(); GetViewport().SetInputAsHandled(); }
        if (e is InputEventKey { Keycode: Key.Delete }) { RemoveSelected(); GetViewport().SetInputAsHandled(); }
        if (e.IsActionPressed("ui_cancel")) { CloseEditor(); GetViewport().SetInputAsHandled(); }
    }
    private void RefreshModeButton() { _edit.Text=_editor.Visible?"返回 ▸":"配招";_edit.Disabled=!_manager.is_board_editing(); }
    public void OpenEditor() { if (_manager.is_board_editing()) { _editor.Show();RefreshModeButton(); } }
    public void CloseEditor() { if(_board?.DragActive==true)return;_pickedSource="";_pickedCombination=null;_pickedTurns=0;_editor.Hide();RefreshModeButton(); }
    public void BeginEditing()
    {
        _draft = _manager.get_board_state(_owner)!.Layout.CopyConfiguration(); _board.Layout = _draft;
        _selected = _pickedSource = "";_pickedCombination=null; _notice.Text = ""; _start.Text = "继续战斗"; _editor.Show(); Changed();
        RefreshModeButton();
    }
    public void ShowRowSelection() => RefreshTurnChoicePanel();
    public void RefreshTurnChoicePanel()
    {
        if (_manager is null || _turnChoicePanel is null) return;
        var choosingRow = _manager.is_board_choosing_row();
        var choices = _manager.get_board_target_choices();
        var targetPrompt = _manager.get_board_target_prompt();
        var visible = !choosingRow && targetPrompt.Length > 0;
        _turnChoicePanel.Visible = visible;
        if (!visible) { _turnChoiceKey = ""; return; }
        var choiceKey = "target:" + targetPrompt + ":" + string.Join("|", choices.Select(target => target.GetInstanceId().ToString()));
        if (choiceKey == _turnChoiceKey) return;
        _turnChoiceKey = choiceKey;
        _turnChoiceTitle.Text = targetPrompt;
        foreach (var child in _turnChoiceOptions.GetChildren()) { _turnChoiceOptions.RemoveChild(child); child.QueueFree(); }
        foreach (var target in choices)
        {
            var chosenTarget = target;
            var button = Button(target.Get("entity_name").AsString(), Vector2.Zero, new(150, 42));
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Pressed += () => _manager.select_board_target(chosenTarget);
            _turnChoiceOptions.AddChild(button);
        }
    }
    private void Commit()
    {
        if (!_manager.commit_player_board(_draft, out var reason)) _notice.Text = reason;
    }
    private void Changed()
    {
        _board.Layout = _draft; _board.Selected = _selected; _board.QueueRedraw(); RebuildSources();
    }
    public bool TryPlaceSource(string key, Vector2I cell, int turns = 0)
    {
        if (!_manager.is_board_editing()) return false;
        CombatBoardPlacement item;
        if (_catalog.FindSkill(key) is { } skill)
        {
            if (_draft.placements.Count(item => item.kind == CombatBoardPlacement.ObjectKind.Skill && item.source_key == key) >= skill.board_max_placements)
            { _notice.Text = $"{skill.skill_name}已达可摆上限。"; return false; }
            item = new() { placement_id = Guid.NewGuid().ToString("N"), source_key = key, cell = cell, quarter_turns = skill.board_rotation_allowed ? turns % 2 : 0 };
        }
        else if (_catalog.FindRewrite(key) is { } rewrite)
        {
            if (_draft.placements.Any(item => item.source_key == key)) { _notice.Text = "同一项化法只能摆一块。"; return false; }
            item = new() { placement_id = Guid.NewGuid().ToString("N"), kind = CombatBoardPlacement.ObjectKind.Rewrite, source_key = key, cell = cell, rewrite_direction = turns % 4 };
        }
        else if (BattleSupplyRules.Load(key) is { } pill && _owner is Character actor)
        {
            var reserved = _draft.placements.Count(item => item.kind == CombatBoardPlacement.ObjectKind.Pill && !item.expended && item.source_key == key);
            if (reserved >= actor.get_inventory_total_count(key)) { _notice.Text = "这颗丹药已放入盘中，或当前行囊数量不足。"; return false; }
            item = new() { placement_id = Guid.NewGuid().ToString("N"), kind = CombatBoardPlacement.ObjectKind.Pill, source_key = key,
                cell = cell, quarter_turns = pill.inventory_can_rotate ? turns % 2 : 0 };
        }
        else if (TalismanCombatCatalog.Load(key) is { } talisman && _owner is Character talismanOwner)
        {
            var reserved = _draft.placements.Count(item => item.kind == CombatBoardPlacement.ObjectKind.Talisman && !item.expended && item.source_key == key);
            if (reserved >= talismanOwner.get_inventory_total_count(key)) { _notice.Text = "行囊中没有可用的同种符箓。"; return false; }
            item = new() { placement_id = Guid.NewGuid().ToString("N"), kind = CombatBoardPlacement.ObjectKind.Talisman,
                source_key = key, cell = cell, quarter_turns = talisman.inventory_can_rotate ? turns % 2 : 0 };
        }
        else if (key == "heart:xuan_shu" && !_draft.placements.Any(item => item.kind == CombatBoardPlacement.ObjectKind.ZhouTianIndicator))
            item = new() { placement_id = "heart:xuan_shu:indicator", source_key = key, cell = cell, kind = CombatBoardPlacement.ObjectKind.ZhouTianIndicator, locked = true, rewrite_direction = turns % 4 };
        else return false;
        if (!Fits(item)) { _notice.Text = "此处放不下，请调整位置或朝向。"; return false; }
        _draft.placements.Add(item); _selected = item.placement_id; _pickedSource = ""; _notice.Text = ""; Changed(); return true;
    }
    public bool MovePlacement(string id, Vector2I cell)
    {
        if(!_manager.is_board_editing())return false;
        var item = _draft.placements.FirstOrDefault(item => item.placement_id == id); if (item is null) return false;
        var previous = item.cell; item.cell = cell;
        if (!Fits(item)) { item.cell = previous; _notice.Text = "此处放不下。"; return false; }
        _selected = id; Changed(); return true;
    }
    private bool Fits(CombatBoardPlacement candidate,CombatBoardPlacement? replacedBottle=null)
    {
        var occupied = _draft.placements.Where(item => item.placement_id != candidate.placement_id&&item!=replacedBottle)
            .SelectMany(item => CombatBoardGeometry.Cells(item, CombatBoardGeometry.Footprint(item, _catalog.FindSkill))).ToHashSet();
        var cells=CombatBoardGeometry.Cells(candidate,CombatBoardGeometry.Footprint(candidate,_catalog.FindSkill)).ToArray();
        return CombatBoardGeometry.Fits(_draft.size, candidate.cell, CombatBoardGeometry.Footprint(candidate, _catalog.FindSkill), occupied)
            && (_draft.unlocked_cells.Count==0||cells.All(cell=>_draft.unlocked_cells.Contains(cell)));
    }
    private CombatBoardPlacement? RefillTarget(CombatBoardPlacement candidate)
    {
        if(candidate.kind is not (CombatBoardPlacement.ObjectKind.Pill or CombatBoardPlacement.ObjectKind.Talisman)||candidate.expended
            ||_draft.placements.Any(item=>item.placement_id==candidate.placement_id))return null;
        var shape=CombatBoardGeometry.Footprint(candidate,_catalog.FindSkill);
        return _draft.placements.FirstOrDefault(item=>item.kind==candidate.kind&&item.expended
            &&item.source_key==candidate.source_key&&item.cell==candidate.cell
            &&CombatBoardGeometry.Footprint(item,_catalog.FindSkill)==shape);
    }
    private bool CanPlaceDragged(CombatBoardPlacement candidate)
    {
        if(!_manager.is_board_editing()||!_editor.Visible||string.IsNullOrWhiteSpace(candidate.placement_id))return false;
        var previous=_draft.placements.FirstOrDefault(item=>item.placement_id==candidate.placement_id);
        if(previous is not null&&(previous.kind!=candidate.kind||previous.source_key!=candidate.source_key
            ||previous.locked!=candidate.locked||previous.expended!=candidate.expended))return false;
        var others=_draft.placements.Where(item=>item.placement_id!=candidate.placement_id);
        switch(candidate.kind)
        {
            case CombatBoardPlacement.ObjectKind.Skill:
                var skill=_catalog.FindSkill(candidate.source_key);
                if(skill is null||!skill.has_board_definition()||others.Count(item=>item.kind==candidate.kind&&item.source_key==candidate.source_key)>=skill.board_max_placements)return false;
                break;
            case CombatBoardPlacement.ObjectKind.Rewrite:
                var rewrite=_catalog.FindRewrite(candidate.source_key);
                if(rewrite is null||candidate.rewrite_direction is <0 or >3
                    ||others.Count(item=>item.kind==candidate.kind&&item.source_key==candidate.source_key)>=rewrite.max_placements)return false;
                break;
            case CombatBoardPlacement.ObjectKind.ZhouTianIndicator:
                if(candidate.source_key!="heart:xuan_shu"||!candidate.locked||candidate.rewrite_direction is <0 or >3
                    ||!_manager.gongfa_combat.HasPower(_owner,"玄枢含章")||others.Any(item=>item.kind==candidate.kind))return false;
                break;
            case CombatBoardPlacement.ObjectKind.Pill:
                if(BattleSupplyRules.Load(candidate.source_key)is null||_owner is not Character actor)return false;
                if(candidate.expended&&previous?.expended!=true)return false;
                if(!candidate.expended&&others.Count(item=>item.kind==candidate.kind&&item.source_key==candidate.source_key&&!item.expended)
                    >=actor.get_inventory_total_count(candidate.source_key))return false;
                break;
            case CombatBoardPlacement.ObjectKind.Talisman:
                if(TalismanCombatCatalog.Load(candidate.source_key)is null||_owner is not Character talismanOwner)return false;
                if(candidate.expended&&previous?.expended!=true)return false;
                if(!candidate.expended&&others.Count(item=>item.kind==candidate.kind&&item.source_key==candidate.source_key&&!item.expended)
                    >=talismanOwner.get_inventory_total_count(candidate.source_key))return false;
                break;
            default:return false;
        }
        return Fits(candidate,RefillTarget(candidate));
    }
    private bool CommitDragged(CombatBoardPlacement candidate)
    {
        if(!CanPlaceDragged(candidate)){_notice.Text="此处放不下，已保留原配置。";return false;}
        var refill=RefillTarget(candidate);
        var copy=candidate.CopyConfiguration();
        if(refill is not null)copy.placement_id=refill.placement_id;
        var next=_draft.CopyConfiguration();
        var index=next.placements.ToList().FindIndex(item=>item.placement_id==copy.placement_id);
        if(index<0)next.placements.Add(copy);else next.placements[index]=copy;
        var issues=CombatBoardGeometry.Validate(next,_catalog.FindSkill,key=>_catalog.FindSkill(key)is not null,
            key=>_catalog.FindRewrite(key)?.max_placements??0,_manager.gongfa_combat.HasPower(_owner,"玄枢含章"),
            key=>_owner is Character actor?actor.get_inventory_total_count(key):0);
        if(issues.Count>0){_notice.Text=issues[0].Message;return false;}
        _draft=next;_selected=copy.placement_id;_pickedSource="";_pickedTurns=0;
        _notice.Text=refill is null?"":copy.kind==CombatBoardPlacement.ObjectKind.Talisman?"已补入一张，施符时扣除库存。":"已补入一颗，服用时扣除库存。";Changed();return true;
    }
    private void RemoveDragged(string id)
    {
        if(!_manager.is_board_editing())return;
        var item=_draft.placements.FirstOrDefault(item=>item.placement_id==id);
        if(item is null)return;
        if(item.locked){_notice.Text="周天指示可以移动、转向，不能移出。";return;}
        _draft.placements.Remove(item);_selected=_pickedSource="";_pickedTurns=0;_notice.Text="";Changed();
    }
    public void RotateSelected()
    {
        if(!_manager.is_board_editing())return;
        if(_pickedCombination is not null){_pickedCombination=BattleBoardView.RotateCombination(_pickedCombination,_catalog);_notice.Text="组合已转向。";return;}
        if (_pickedSource.Length > 0) { _pickedTurns = (_pickedTurns + 1) % 4; _notice.Text = "已旋转待摆对象。"; return; }
        var item = _draft.placements.FirstOrDefault(item => item.placement_id == _selected); if (item is null) return;
        if (item.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator) item.rewrite_direction = (item.rewrite_direction + 1) % 4;
        else
        {
            if (item.kind == CombatBoardPlacement.ObjectKind.Skill && _catalog.FindSkill(item.source_key)?.board_rotation_allowed != true) return;
            if (item.kind == CombatBoardPlacement.ObjectKind.Pill && BattleSupplyRules.Load(item.source_key)?.inventory_can_rotate != true) return;
            if (item.kind == CombatBoardPlacement.ObjectKind.Talisman && TalismanCombatCatalog.Load(item.source_key)?.inventory_can_rotate != true) return;
            var previous = item.quarter_turns; item.quarter_turns = (previous + 1) % 2;
            if (!Fits(item)) { item.quarter_turns = previous; _notice.Text = "旋转后放不下。"; return; }
        }
        Changed();
    }
    public void RemoveSelected()
    {
        if(!_manager.is_board_editing())return;
        var item = _draft.placements.FirstOrDefault(item => item.placement_id == _selected); if (item is null) return;
        if (item.locked) { _notice.Text = "周天指示可以移动、转向，不能移出。"; return; }
        _draft.placements.Remove(item); _selected = ""; Changed();
    }

    public void RefillSelected()
    {
        if(!_manager.is_board_editing())return;
        var item = _draft.placements.FirstOrDefault(item => item.placement_id == _selected);
        if (item is null || item.kind is not (CombatBoardPlacement.ObjectKind.Pill or CombatBoardPlacement.ObjectKind.Talisman) || !item.expended || _owner is not Character actor) return;
        var reserved = _draft.placements.Count(other => other.kind == item.kind && !other.expended && other.source_key == item.source_key);
        if (actor.get_inventory_total_count(item.source_key) <= reserved) { _notice.Text = item.kind == CombatBoardPlacement.ObjectKind.Talisman ? "行囊中没有可补入的同种符箓。" : "行囊中没有可补入的同种丹药。"; return; }
        item.expended = false; _notice.Text = item.kind == CombatBoardPlacement.ObjectKind.Talisman ? "已补入一张，施符时扣除库存。" : "已补入一颗，服用时扣除库存。"; Changed();
    }

    private void RebuildSources()
    {
        foreach (var child in _sources.GetChildren()) { _sources.RemoveChild(child); child.QueueFree(); }
        var search = _search.Text.Trim();
        if (_tab == "招式")
            foreach (var skill in _catalog.skills.Where(skill => skill.skill_name.Contains(search)))
                AddSource(skill.get_board_skill_key(), skill.skill_name, skill.board_size, skill.icon_path, "");
        else if (_tab == "化法")
        {
            foreach (var rewrite in _catalog.rewrites.Where(rewrite => (rewrite.title + rewrite.source_name).Contains(search)))
                AddSource(rewrite.source_key, rewrite.get_board_display_name(), Vector2I.One, rewrite.icon_path, rewrite.description + "\n强化共边相邻、箭头指向的一份适用招式。");
            if (_manager.gongfa_combat.HasPower(_owner, "玄枢含章"))
                AddSource("heart:xuan_shu", "周天指示", Vector2I.One, "res://textures/ui/combat/board_skill_icons/zhoutian_indicator.png", CombatBoardPresentation.IndicatorText);
        }
        else if (_tab == "丹药" && _owner is Character actor)
        {
            foreach (var path in actor.pack_items.Distinct())
            {
                var pill = BattleSupplyRules.Load(path);
                if (pill is null || !pill.entity_name.Contains(search) || actor.get_inventory_total_count(path) <= 0) continue;
                var info = AlchemyPillVisual.Hover(pill, user: actor);
                AddSource(path, pill.entity_name, pill.get_inventory_size(), pill.icon_path,
                    info.Body + $"\n\n行囊现有{actor.get_inventory_total_count(path)}颗；一块一颗，服用后保留空瓶占格。", info);
            }
        }
        else if (_tab == "符箓" && _owner is Character talismanOwner)
        {
            foreach (var path in talismanOwner.pack_items.Distinct())
            {
                var talisman = TalismanCombatCatalog.Load(path);
                if (talisman is null || talismanOwner.get_inventory_total_count(path) <= 0) continue;
                var name = talisman.entity_name + " · " + TalismanCombatCatalog.TierName(talisman.Snapshot!.Tier);
                if (!name.Contains(search)) continue;
                AddSource(path, name, talisman.get_inventory_size(), talisman.icon_path, "", TalismanCombatCatalog.Hover(talisman));
            }
        }
        if(_sources.GetChildCount()==0)
        {
            var empty=new Label { Text=search.Length>0?"没有匹配的条目":$"当前没有可用的{_tab}",CustomMinimumSize=new(336,64),VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore };
            empty.AddThemeFontSizeOverride("font_size",20);empty.AddThemeColorOverride("font_color",BattleBoardSkin.MutedInk);_sources.AddChild(empty);
        }
    }
    public void SelectCategory(string category)
    {
        if(!_tabs.ContainsKey(category))return;
        _tab=category;
        foreach(var (name,button) in _tabs)button.ButtonPressed=name==category;
        ShowSources();RebuildSources();
    }
    private void AddSource(string key, string name, Vector2I shape, string icon, string tip,
        HoverInfoManager.HoverInfoContent? hover = null)
    {
        var item = new BattleBoardSource { SourceKey = key, Board=_board,CustomMinimumSize = new(363, Math.Max(75, shape.Y * SourceCellSize + 19)),
            TooltipText = "" };
        var sample = new CombatBoardLayout { size = shape };
        var kind = key == "heart:xuan_shu" ? CombatBoardPlacement.ObjectKind.ZhouTianIndicator : _catalog.FindRewrite(key) is not null ? CombatBoardPlacement.ObjectKind.Rewrite
            : BattleSupplyRules.Load(key) is not null ? CombatBoardPlacement.ObjectKind.Pill
            : TalismanCombatCatalog.Load(key) is not null ? CombatBoardPlacement.ObjectKind.Talisman : CombatBoardPlacement.ObjectKind.Skill;
        sample.placements.Add(new() { placement_id = "sample", source_key = key, kind = kind });
        var drawing = new BattleBoardView { Layout = sample, Catalog = _catalog, CellSize = SourceCellSize, MouseFilter = MouseFilterEnum.Ignore, Position = new(3, 10), Size = (Vector2)shape * SourceCellSize };
        item.AddChild(drawing);
        var label = Text(name, new(181, 10), new(174, item.CustomMinimumSize.Y - 20), 23);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;label.VerticalAlignment=VerticalAlignment.Center;item.AddChild(label);
        item.Picked += () => { _search.ReleaseFocus();_pickedCombination=null;_pickedSource = key; _pickedTurns = 0; _selected = ""; _notice.Text = ""; };
        _sources.AddChild(item);
        if (kind == CombatBoardPlacement.ObjectKind.Skill && _catalog.FindSkill(key) is { } sourceSkill)
            HoverInfoManager.SetProvider(item, () => CombatBoardPresentation.SkillHover(sourceSkill, _owner, _manager));
        else if (hover is { } info) HoverInfoManager.SetInfo(item, kind == CombatBoardPlacement.ObjectKind.Talisman ? info : info with { Body = tip, Sections = null });
        else HoverInfoManager.SetProvider(item, () => CombatBoardPresentation.PlacementHover(sample, _catalog,
            sample.placements[0], _owner, manager: _manager));
    }
    private void BuildPlanPage(Control panel)
    {
        _planPage=new Control { Name="PlanPage",Position=new(23,80),Size=new(383,734),MouseFilter=MouseFilterEnum.Stop,Visible=false };panel.AddChild(_planPage);
        _planPage.AddChild(Text("我的方案",new(0,4),new(383,40),29));
        var scroll=new ScrollContainer { Name="PlanScroll",Position=new(0,60),Size=new(383,614),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };_planPage.AddChild(scroll);
        _planList=new VBoxContainer { CustomMinimumSize=new(363,0),SizeFlagsHorizontal=SizeFlags.ExpandFill };_planList.AddThemeConstantOverride("separation",12);scroll.AddChild(_planList);
        _planPage.AddChild(Text("载入整盘 · 保存管理在人物页",new(0,692),new(383,30),17));
    }
    private void BuildCombinationPage(Control panel)
    {
        _combinationPage=new Control { Name="CombinationPage",Position=new(23,80),Size=new(383,734),MouseFilter=MouseFilterEnum.Stop,Visible=false };panel.AddChild(_combinationPage);
        _combinationPage.AddChild(Text("我的组合",new(0,4),new(383,40),29));
        _combinationPage.AddChild(Text("拖入整组搭配，保留相对位置与朝向。",new(0,51),new(383,54),18));
        var scroll=new ScrollContainer { Name="CombinationScroll",Position=new(0,118),Size=new(383,556),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled };_combinationPage.AddChild(scroll);
        _combinationList=new VBoxContainer { CustomMinimumSize=new(363,0),SizeFlagsHorizontal=SizeFlags.ExpandFill };_combinationList.AddThemeConstantOverride("separation",12);scroll.AddChild(_combinationList);
        _combinationPage.AddChild(Text("保存、改名与删除在人物页",new(0,692),new(383,30),17));
    }
    private void SetLibraryTab(string name)
    {
        foreach(var (key,button) in _libraryTabs)button.ButtonPressed=key==name;
        var sources=name=="来源";_categories.Visible=sources;_search.Visible=sources;_sourceScroll.Visible=sources;_clearSource.Visible=sources;
        _planPage.Visible=name=="方案";_combinationPage.Visible=name=="组合";
        _pickedCombination=null;_pickedSource="";_pickedTurns=0;
    }
    private void ShowSources() => SetLibraryTab("来源");
    private void ReadLibrary()
    {
        try
        {
            _planLibrary=_owner is Character actor?CombatBoardPreparationService.GetLibrary(actor):new();
        }
        catch { _planLibrary=new();_notice.Text="方案暂时无法读取，原文件已保留。"; }
    }
    private void ShowPlans()
    {
        ReadLibrary();
        foreach(var child in _planList.GetChildren()){_planList.RemoveChild(child);child.QueueFree();}
        foreach (var layout in new[]{_entryLayout}.Concat(_planLibrary.layouts))
        {
            var row=new PanelContainer { CustomMinimumSize=new(363,130),SizeFlagsHorizontal=SizeFlags.ExpandFill };
            row.AddThemeStyleboxOverride("panel",CraftingPaperStyle.Paper(padding:12,brightness:.96f));_planList.AddChild(row);
            var columns=new HBoxContainer();columns.AddThemeConstantOverride("separation",12);row.AddChild(columns);
            columns.AddChild(CombatBoardUi.Mini(layout,_catalog,72));
            var words=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ShrinkCenter };columns.AddChild(words);
            var title=CombatBoardUi.Text(layout.Title,23,wrap:true);CombatBoardUi.LimitLines(title,3);words.AddChild(title);
            HoverInfoManager.SetInfo(row,new(layout.Title,$"{layout.placements.Count}项 · 载入整盘。"));
            words.AddChild(CombatBoardUi.Text($"{layout.placements.Count}项 · 载入整盘",17,true,true));
            var button=CombatBoardUi.Button("应用",()=>TryApplyPlan(layout),font:19,battle:true);
            button.Name="ApplyBattlePlan";button.CustomMinimumSize=new(70,38);button.SizeFlagsVertical=SizeFlags.ShrinkCenter;columns.AddChild(button);
        }
        if(_planLibrary.layouts.Count==0)
        {
            var empty=Text("暂无自定义方案",Vector2.Zero,new(363,54),18);_planList.AddChild(empty);
        }
        SetLibraryTab("方案");
    }
    private void ShowCombinations()
    {
        ReadLibrary();
        foreach(var child in _combinationList.GetChildren()){_combinationList.RemoveChild(child);child.QueueFree();}
        foreach(var saved in _planLibrary.combinations.Where(layout=>layout.placements.Count>0))
        {
            var layout=CombatBoardCombinations.Normalize(saved,_catalog);var source=new BattleBoardCombinationSource { Board=_board,Layout=layout,
                CustomMinimumSize=new(363,Math.Max(118,layout.size.Y*34+24)),MouseDefaultCursorShape=CursorShape.Drag };
            source.AddChild(new BattleBoardView { Layout=layout,Catalog=_catalog,CellSize=Math.Min(34,142f/Math.Max(1,layout.size.X)),
                OccupiedCellsOnly=true,Position=new(5,12),MouseFilter=MouseFilterEnum.Ignore });
            var title=Text(layout.Title,new(170,16),new(185,source.CustomMinimumSize.Y-24),23);title.AutowrapMode=TextServer.AutowrapMode.WordSmart;source.AddChild(title);
            source.Picked=()=>{_pickedCombination=layout.CopyConfiguration();_pickedSource="";_pickedTurns=0;_selected="";_notice.Text="点击落点摆入整组，R转向。";};
            _combinationList.AddChild(source);
        }
        if(_combinationList.GetChildCount()==0)_combinationList.AddChild(Text("尚无已保存的组合",Vector2.Zero,new(363,64),20));
        SetLibraryTab("组合");
    }
    public bool TryInsertCombination(CombatBoardLayout saved,Vector2I cell)
    {
        if(saved.layout_version!=CombatBoardLayout.CurrentLayoutVersion){_notice.Text="此组合的版本暂不适用。";return false;}
        var layout=CombatBoardCombinations.Normalize(saved,_catalog);var placements=layout.placements.Select(item=>
        {var copy=item.CopyConfiguration();copy.placement_id=Guid.NewGuid().ToString("N");copy.cell+=cell;return copy;}).ToArray();
        return CommitCombination(placements);
    }
    private CombatBoardLayout CombinedDraft(IReadOnlyList<CombatBoardPlacement> placements)
    {
        var next=_draft.CopyConfiguration();foreach(var item in placements)next.placements.Add(item.CopyConfiguration());return next;
    }
    private List<CombatBoardGeometry.Issue> CombinationIssues(CombatBoardLayout next) => CombatBoardGeometry.Validate(next,_catalog.FindSkill,
        key=>_catalog.FindSkill(key)is not null,key=>_catalog.FindRewrite(key)?.max_placements??0,_manager.gongfa_combat.HasPower(_owner,"玄枢含章"),
        key=>_owner is Character actor?actor.get_inventory_total_count(key):0);
    private bool CanInsertCombination(IReadOnlyList<CombatBoardPlacement> placements)=>_manager.is_board_editing()&&placements.Count>0&&CombinationIssues(CombinedDraft(placements)).Count==0;
    private bool CommitCombination(IReadOnlyList<CombatBoardPlacement> placements)
    {
        if(!_manager.is_board_editing()||placements.Count==0)return false;
        var next=CombinedDraft(placements);var issues=CombinationIssues(next);
        if(issues.Count>0){_notice.Text=issues[0].Message+"；盘面保持。";return false;}
        _draft=next;_selected=placements[0].placement_id;_pickedCombination=null;_pickedSource="";_pickedTurns=0;_notice.Text="";Changed();return true;
    }
    public bool TryApplyPlan(CombatBoardLayout layout)
    {
        if(!_manager.is_board_editing())return false;
        var normalized=CombatBoardUnlockRules.Normalize(layout,_owner.Get("cultivation_realm").AsString(),_catalog.FindSkill);
        var issues=CombatBoardGeometry.Validate(normalized,_catalog.FindSkill,key=>_catalog.FindSkill(key)is not null,key=>_catalog.FindRewrite(key)?.max_placements??0,
            _manager.gongfa_combat.HasPower(_owner,"玄枢含章"),key=>_owner is Character actor?actor.get_inventory_total_count(key):0);
        if(normalized.size!=_draft.size||!normalized.unlocked_cells.OrderBy(cell=>cell.Y).ThenBy(cell=>cell.X)
            .SequenceEqual(_draft.unlocked_cells.OrderBy(cell=>cell.Y).ThenBy(cell=>cell.X))||issues.Count>0)
        {_notice.Text="当前角色无法使用此方案，请检查来源、数量或盘面。";return false;}
        _draft=normalized;_selected=_pickedSource="";_notice.Text="";Changed();ShowSources();return true;
    }
    private static Label Text(string text, Vector2 position, Vector2 size, int font) {
        var label = new Label { Text = text, Position = position, Size = size, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", font); label.AddThemeColorOverride("font_color", BattleBoardSkin.Ink);label.Size=size;return label;
    }
    private static Control Panel(Vector2 position, Vector2 size,string part="source_panel") {
        var panel = new Control { Position = position, Size = size, MouseFilter = MouseFilterEnum.Stop };
        panel.AddChild(new BattleBoardSkinPanel { Part=part,Size=size });return panel;
    }
    private static Button Button(string text, Vector2 position, Vector2 size,bool primary=false) {
        var button = new Button { Text = text, Position = position, CustomMinimumSize = size, Size = size, FocusMode = FocusModeEnum.None };
        BattleBoardSkin.Skin(button,primary); return button;
    }
}
