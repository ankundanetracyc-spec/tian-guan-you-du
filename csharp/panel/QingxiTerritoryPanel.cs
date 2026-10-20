using Godot;

/// <summary>辖地中枢。地图负责地点，辖地页负责治理、营建与全局信息。</summary>
public partial class QingxiTerritoryPanel : Control
{
    public event Action? Closed;
    public event Action? BuildRequested;
    public event Action? MapRequested;
    public event Action? PersonnelRequested;
    public event Action<string>? Opened;
    public event Action<string, bool>? SituationResolved;

    private QingxiTerritoryState? _state;
    private EstateResourceSystem? _resources;
    private VBoxContainer? _body;
    private readonly Dictionary<string, Button> _tabs = [];
    private string _section = "personnel";
    private bool _building;
    private static readonly Color Ink = new(.13f, .115f, .08f, .985f);
    private static readonly Color Gold = new(.72f, .59f, .32f, .95f);
    private static readonly Color Text = new(.9f, .86f, .73f);

    public override void _Ready()
    {
        AddToGroup("map_overlay_blocker"); MouseFilter = MouseFilterEnum.Stop; ZIndex = 80;
        BuildLayout(); Hide();
    }

    public void bind_state(QingxiTerritoryState state, EstateResourceSystem? resources = null)
    {
        if (_state is not null) _state.TerritoryChanged -= Refresh;
        if (_resources is not null) _resources.ResourcesChanged -= Refresh;
        _state = state; _resources = resources;
        _state.TerritoryChanged += Refresh;
        if (_resources is not null) _resources.ResourcesChanged += Refresh;
        Refresh();
    }

    public void open(string section = "")
    {
        _section = NormalizeSection(section);
        Show(); MoveToFront(); Refresh(); Opened?.Invoke(_section);
    }

    private static string NormalizeSection(string section) => section switch
    {
        "village" or "governance" => "governance",
        "construction" or "build" => "construction",
        "industry" => "industry",
        "defense" or "gate" => "defense",
        "geography" => "geography",
        _ => "personnel"
    };

    private void BuildLayout()
    {
        var backdrop = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/territory/territory_ink_backdrop_v1.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, .9f) };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);
        var shade = new ColorRect { Color = new Color(.04f, .035f, .025f, .42f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); shade.GuiInput += input => { if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close(); }; AddChild(shade);
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); panel.OffsetLeft = 18; panel.OffsetTop = 16; panel.OffsetRight = -18; panel.OffsetBottom = -16;
        panel.AddThemeStyleboxOverride("panel", MakeStyle(Ink, Gold)); AddChild(panel);
        var parchment = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/territory/territory_parchment_card_v1.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, .28f) };
        panel.AddChild(parchment);
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 42); margin.AddThemeConstantOverride("margin_right", 42); margin.AddThemeConstantOverride("margin_top", 26); margin.AddThemeConstantOverride("margin_bottom", 28); panel.AddChild(margin);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 18); margin.AddChild(content);

        var header = new HBoxContainer(); content.AddChild(header);
        var title = new Label { Text = "青溪里 · 辖地", SizeFlagsHorizontal = SizeFlags.ExpandFill }; title.AddThemeFontSizeOverride("font_size", 38); title.Modulate = new Color(.98f, .91f, .7f); header.AddChild(title);
        var map = MakeButton("查看地点", 108); map.Pressed += () => { Hide(); MapRequested?.Invoke(); }; header.AddChild(map);
        var close = MakeButton("返回", 82); close.Pressed += Close; header.AddChild(close);

        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 8); content.AddChild(tabs);
        foreach (var pair in new[] { ("personnel", "人事"), ("governance", "治事"), ("construction", "营建"), ("industry", "产业"), ("defense", "防务"), ("geography", "地志") })
        {
            var tab = MakeButton(pair.Item2, 0); tab.SizeFlagsHorizontal = SizeFlags.ExpandFill; tab.CustomMinimumSize = new Vector2(0, 54); tab.AddThemeFontSizeOverride("font_size", 21);
            var id = pair.Item1; tab.Pressed += () => { _section = id; Refresh(); };
            _tabs[id] = tab; tabs.AddChild(tab);
        }
        content.AddChild(new HSeparator());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        content.AddChild(scroll); _body = new VBoxContainer(); _body.AddThemeConstantOverride("separation", 12); _body.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(_body);
    }

    private void Refresh()
    {
        if (_state is null || _body is null || _building) return;
        _building = true;
        foreach (var button in _tabs) StyleTab(button.Value, button.Key == _section);
        foreach (var child in _body.GetChildren()) child.QueueFree();
        switch (_section)
        {
            case "personnel": BuildPersonnelEntry(); break;
            case "governance": BuildGovernance(); break;
            case "construction": BuildConstruction(); break;
            case "industry": BuildIndustry(); break;
            case "defense": BuildDefense(); break;
            case "geography": BuildGeography(); break;
            default: BuildPersonnelEntry(); break;
        }
        _building = false;
    }


    private void BuildGovernance()
    {
        AddTitle("治事", "任命常驻主事，编制民力去向；日常事务会随时间自行运转。 ");
        var offices = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill }; offices.AddThemeConstantOverride("h_separation", 16); _body!.AddChild(offices);
        foreach (var office in _state!.offices) AddOfficeCard(offices, office);

        var split = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; split.AddThemeConstantOverride("separation", 18); _body.AddChild(split);
        var staffing = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; staffing.AddThemeConstantOverride("separation", 8); split.AddChild(staffing);
        staffing.AddChild(Label("民力编制", 25, new Color(.96f, .84f, .53f)));
        staffing.AddChild(Label($"可用民力总数：{_state.get_available_labor()} / {_state.total_labor}　　已编 {_state.get_assigned_labor()}　　闲置 {Mathf.Max(0, _state.get_idle_labor())}", 17, new Color(.35f, .55f, .4f)));
        AddLaborLine(staffing, QingxiTerritoryState.Labor.Farming, "耕作", "从事田畴耕作，提升粮食产量");
        AddLaborLine(staffing, QingxiTerritoryState.Labor.Work, "作业", "从事手工作业，产出各类物资");
        AddLaborLine(staffing, QingxiTerritoryState.Labor.Transport, "运输", "负责物资运输，保障流通效率");
        AddLaborLine(staffing, QingxiTerritoryState.Labor.Construction, "营建", "参与营造修缮，加快建设进度");
        AddLaborLine(staffing, QingxiTerritoryState.Labor.Defense, "守备", "维护辖地治安，防范外敌侵扰");
        AddLaborLine(staffing, QingxiTerritoryState.Labor.External, "外务", "处理对外事务，提升辖地声望");

        var tips = new VBoxContainer { CustomMinimumSize = new Vector2(270, 0) }; tips.AddThemeConstantOverride("separation", 12); split.AddChild(tips);
        tips.AddChild(Label("运行提示", 24, new Color(.96f, .84f, .53f)));
        AddTip(tips, "职务空缺", _state.offices.Any(item => !item.covered) ? "尚有常驻职位无人主持，可前往人事页选择候选人。" : "常驻职责均已有人照应。");
        AddTip(tips, "储备底线", $"粮食低于 {_state.food_reserve_target} 时，自治体系会优先回补。当前粮 {_resources?.get_amount(EstateResourceSystem.Food) ?? 0}。");
        AddTip(tips, "守备底线", $"最低守备 {_state.minimum_guard} 人，当前编入 {_state.get_labor(QingxiTerritoryState.Labor.Defense)} 人。");
        var rules = MakeOrnateButton("调整治理章程"); rules.Pressed += OpenGovernanceRules; tips.AddChild(rules);
    }

    private void AddOfficeCard(Container parent, QingxiTerritoryState.Office office)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 230), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", MakeStyle(new Color(.92f, .88f, .75f, .96f), new Color(.43f, .34f, .19f, .95f)));
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 22); margin.AddThemeConstantOverride("margin_right", 22); margin.AddThemeConstantOverride("margin_top", 18); margin.AddThemeConstantOverride("margin_bottom", 16); card.AddChild(margin);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 7); margin.AddChild(box);
        box.AddChild(Label(office.title, 27, new Color(.18f, .14f, .09f)));
        box.AddChild(new HSeparator());
        box.AddChild(Label($"主事　{(string.IsNullOrWhiteSpace(office.lead) ? "待任" : office.lead)}\n副手　{(string.IsNullOrWhiteSpace(office.deputy) ? "待任" : office.deputy)}", 18, new Color(.22f, .18f, .12f)));
        box.AddChild(Label(office.note, 14, new Color(.34f, .31f, .25f)));
        var assign = MakeOrnateButton("调整派任"); assign.Pressed += () => PersonnelRequested?.Invoke(); box.AddChild(assign);
        parent.AddChild(card);
    }

    private void AddLaborLine(Container parent, QingxiTerritoryState.Labor labor, string title, string note)
    {
        var row = new PanelContainer { CustomMinimumSize = new Vector2(0, 50), SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddThemeStyleboxOverride("panel", MakeStyle(new Color(.94f, .91f, .82f, .82f), new Color(.52f, .44f, .29f, .52f))); parent.AddChild(row);
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 12); row.AddChild(line);
        var name = Label(title, 19, new Color(.19f, .15f, .09f)); name.CustomMinimumSize = new Vector2(96, 0); line.AddChild(name);
        var description = Label(note, 14, new Color(.37f, .33f, .25f)); description.SizeFlagsHorizontal = SizeFlags.ExpandFill; line.AddChild(description);
        var down = MakeButton("−", 38); down.Pressed += () => _state!.set_labor(labor, Mathf.Max(0, _state.get_labor(labor) - 1)); line.AddChild(down);
        var count = Label(_state!.get_labor(labor).ToString(), 20, new Color(.18f, .15f, .1f)); count.HorizontalAlignment = HorizontalAlignment.Center; count.CustomMinimumSize = new Vector2(42, 0); line.AddChild(count);
        var up = MakeButton("+", 38); up.Pressed += () => _state!.set_labor(labor, _state.get_labor(labor) + 1); line.AddChild(up);
    }

    private static void AddTip(Container parent, string title, string note)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 78) }; panel.AddThemeStyleboxOverride("panel", MakeStyle(new Color(.91f, .88f, .79f, .82f), new Color(.48f, .4f, .25f, .58f))); parent.AddChild(panel);
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 13); margin.AddThemeConstantOverride("margin_right", 13); margin.AddThemeConstantOverride("margin_top", 9); margin.AddThemeConstantOverride("margin_bottom", 9); panel.AddChild(margin);
        var box = new VBoxContainer(); margin.AddChild(box); box.AddChild(Label(title, 17, new Color(.2f, .16f, .1f))); box.AddChild(Label(note, 13, new Color(.35f, .3f, .22f)));
    }

    private void OpenGovernanceRules()
    {
        var dialog = new AcceptDialog { Title = "治理章程", DialogText = "储粮底线、最低守备、培养名额与外务授权会影响自动运转。完整章程编辑将放入治事页下方的专用面板。" };
        AddChild(dialog); dialog.PopupCentered();
    }

    private void BuildConstruction()
    {
        AddTitle("营建", "营建改变的是辖地可承受的长期规模；地点本身仍从地点图进入。 ");
        var rooms = _resources?.get_facility_count("meditation_room") ?? 0;
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 14); _body!.AddChild(grid);
        AddInfoCard(grid, "现有设施", $"静室 {rooms} 间\n农田、仓棚、工坊与哨塔均由地点图呈现。");
        AddInfoCard(grid, "营建原则", "材料达到要求才可开工。仓储上限、生产空间与防务节点将各自独立成长。");
        var build = MakeButton("查看营建项目", 0); build.SizeFlagsHorizontal = SizeFlags.ExpandFill; build.CustomMinimumSize = new Vector2(0, 48); build.Pressed += () => BuildRequested?.Invoke(); _body!.AddChild(build);
    }

    private void BuildIndustry()
    {
        AddTitle("产业", "产业的任务是持续供养辖地，而非要求你按固定旬期逐项操作。 ");
        AddLabel(ResourceText(), 18, new Color(.75f, .89f, .77f));
        var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 12); _body!.AddChild(grid);
        AddInfoCard(grid, "田畴", $"耕作民力：{_state!.get_labor(QingxiTerritoryState.Labor.Farming)}\n粮食低于底线时，自治体系会优先回补。 ");
        AddInfoCard(grid, "仓储", "粮、建材与药材分别受其对应库房上限约束；超出容量的产出无法入库。 ");
        AddInfoCard(grid, "作业", $"作业民力：{_state.get_labor(QingxiTerritoryState.Labor.Work)}\n后续配方与工坊能力将在此汇总。 ");
    }

    private void BuildDefense()
    {
        AddTitle("防务", "村口是预警与出行的地点；这里管理的是覆盖整个辖地的长期守备。 ");
        AddLabel($"当前治安 {_state!.security}/100　　最低守备 {_state.minimum_guard}　　现有守备 {_state.get_labor(QingxiTerritoryState.Labor.Defense)}", 19, new Color(.95f, .81f, .58f));
        AddLabel("遇到兽患、失窃或外来威胁时，常驻守备会先按章程应对；越过其权限的风险才会形成待决案卷。 ", 16, Text);
    }

    private void BuildGeography()
    {
        AddTitle("地志", "青溪里坐落于凡俗边地，灵力环境平稳而稀薄。 ");
        AddInfoCard(_body!, "本地灵力密度", "100\n此地灵力平稳而稀薄；族人以此为基础吐纳。静室安排与资源供给归李家宅院管理。 ");
        AddTitle("修炼环境", "环境先满足境界所需密度，再将富余部分转化为效率。 ");
        AddLabel("适配 = min(1，环境密度 ÷ 需求密度)\n富余 = min(4，1 + max(0，环境密度 − 需求密度) ÷ 需求密度 × 0.75)\n环境效能 = min(4，适配 × 富余)", 16, Text);
        AddLabel("云息境界的需求密度由 100 逐层升至 200。聚灵阵将在后续作为静室内的布置，不会抬高全辖地环境。 ", 16, new Color(.75f, .89f, .77f));
    }

    private void BuildPersonnelEntry()
    {
        AddTitle("人事", "家族、外姓家仆与庄户的招募、属性、去向与家计均由人事页统一管理。 ");
        AddLabel("常驻职位只能从人事名册中选择；外姓人亦会进入角色名册，可查看与族人相同的基础属性。", 16, Text);
        var open = MakeButton("进入人事名册", 0); open.SizeFlagsHorizontal = SizeFlags.ExpandFill; open.CustomMinimumSize = new Vector2(0, 56); open.Pressed += () => PersonnelRequested?.Invoke(); _body!.AddChild(open);
    }

    private void AddSituationSummary(bool showActions)
    {
        if (_state!.situations.Count == 0) { AddLabel("暂无待决局势。寻常事务正由常驻人手按授权处理。", 16, Text); return; }
        foreach (var situation in _state.situations)
        {
            var frame = new PanelContainer(); frame.AddThemeStyleboxOverride("panel", MakeStyle(new Color(.11f, .1f, .075f, .9f), new Color(.42f, .34f, .19f, .8f))); var pad = new MarginContainer(); pad.AddThemeConstantOverride("margin_left", 14); pad.AddThemeConstantOverride("margin_right", 14); pad.AddThemeConstantOverride("margin_top", 10); pad.AddThemeConstantOverride("margin_bottom", 10); frame.AddChild(pad); var card = new VBoxContainer(); card.AddThemeConstantOverride("separation", 5); pad.AddChild(card); _body!.AddChild(frame);
            card.AddChild(Label(situation.title, 18, new Color(.97f, .84f, .57f))); card.AddChild(Label(situation.detail, 15, Text));
            if (showActions) { var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 10); var primary = MakeButton(situation.primary, 0); primary.SizeFlagsHorizontal = SizeFlags.ExpandFill; primary.Pressed += () => SituationResolved?.Invoke(situation.id, true); actions.AddChild(primary); var secondary = MakeButton(situation.secondary, 0); secondary.SizeFlagsHorizontal = SizeFlags.ExpandFill; secondary.Pressed += () => SituationResolved?.Invoke(situation.id, false); actions.AddChild(secondary); card.AddChild(actions); }
        }
    }

    private void SwitchTo(string section) { _section = section; Refresh(); }
    private string ResourceText() => _resources is null ? "家业账本尚未建立。" : $"家业账本　粮 {_resources.get_amount(EstateResourceSystem.Food)}　石 {_resources.get_amount(EstateResourceSystem.Stone)}　药 {_resources.get_amount(EstateResourceSystem.Herbs)}　矿 {_resources.get_amount(EstateResourceSystem.Ore)}　灵石 {_resources.get_amount(EstateResourceSystem.SpiritStones)}";
    private void AddTitle(string title, string note) { _body!.AddChild(Label(title, 23, new Color(.96f, .84f, .53f))); if (!string.IsNullOrWhiteSpace(note)) AddLabel(note, 16, Text); }
    private void AddLabel(string text, int size, Color color) => _body!.AddChild(Label(text, size, color));
    private static Label Label(string text, int size, Color color) { var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; label.AddThemeFontSizeOverride("font_size", size); label.Modulate = color; return label; }
    private void AddCard(Container parent, string title, string detail, Action action) { var button = MakeButton($"{title}\n\n{detail}", 0); button.SizeFlagsHorizontal = SizeFlags.ExpandFill; button.CustomMinimumSize = new Vector2(0, 230); button.AddThemeFontSizeOverride("font_size", 24); button.Pressed += action; parent.AddChild(button); }
    private void AddInfoCard(Container parent, string title, string detail) { var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 150) }; panel.AddThemeStyleboxOverride("panel", MakeStyle(new Color(.105f, .095f, .07f, .92f), new Color(.42f, .34f, .19f, .8f))); var texture = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/territory/territory_parchment_card_v1.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, .2f) }; panel.AddChild(texture); var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 22); margin.AddThemeConstantOverride("margin_right", 22); margin.AddThemeConstantOverride("margin_top", 18); margin.AddThemeConstantOverride("margin_bottom", 18); panel.AddChild(margin); var box = new VBoxContainer(); margin.AddChild(box); box.AddChild(Label(title, 22, new Color(.96f, .84f, .53f))); box.AddChild(Label(detail, 18, Text)); parent.AddChild(panel); }
    private void AddNumberSetting(Container parent, string title, int min, int max, int current, Action<int> changed) { var box = new VBoxContainer(); box.AddChild(new Label { Text = title }); var value = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Rounded = true, Value = current, CustomMinimumSize = new Vector2(150, 36) }; value.ValueChanged += next => changed(Mathf.RoundToInt((float)next)); box.AddChild(value); parent.AddChild(box); }
    private static Button MakeButton(string text, float width) { var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 38), AutowrapMode = TextServer.AutowrapMode.WordSmart }; button.AddThemeStyleboxOverride("normal", MakeStyle(new Color(.17f, .145f, .095f, .96f), new Color(.55f, .43f, .22f, .9f))); button.AddThemeStyleboxOverride("hover", MakeStyle(new Color(.25f, .2f, .12f, .98f), new Color(.84f, .66f, .31f, 1f))); return button; }
    private static Button MakeOrnateButton(string text)
    {
        var button = new Button { CustomMinimumSize = new Vector2(240, 64), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var texture = GD.Load<Texture2D>("res://textures/ui/territory/territory_ornate_button_v1.png");
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        var art = new TextureRect { Texture = new AtlasTexture { Atlas = texture, Region = new Rect2(0, 90, 2172, 530) }, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); button.AddChild(art);
        var caption = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        caption.AddThemeFontSizeOverride("font_size", 18); caption.AddThemeColorOverride("font_color", new Color(.96f, .88f, .66f)); caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); button.AddChild(caption);
        button.MouseEntered += () => art.Modulate = new Color(1.15f, 1.15f, 1.1f); button.MouseExited += () => art.Modulate = Colors.White;
        button.FocusEntered += () => art.Modulate = new Color(1.15f, 1.15f, 1.1f); button.FocusExited += () => art.Modulate = Colors.White;
        return button;
    }
    private static void StyleTab(Button button, bool active) { button.AddThemeStyleboxOverride("normal", MakeStyle(active ? new Color(.32f, .25f, .13f, 1f) : new Color(.13f, .115f, .08f, .96f), active ? new Color(.9f, .7f, .32f, 1f) : new Color(.45f, .35f, .18f, .85f))); }
    private void Close() { Hide(); Closed?.Invoke(); }
    private static StyleBoxFlat MakeStyle(Color background, Color border) => new() { BgColor = background, BorderColor = border, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5 };
}
