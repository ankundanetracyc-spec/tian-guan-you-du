using Godot;

/// <summary>李家宅院的地点页。静室在此安排与使用，辖地只负责营建它。</summary>
public partial class FamilyResidencePanel : Control
{
    public event Action? Closed;
    public event Action? BuildRequested;
    public event Action? ExpansionRequested;

    private Func<Godot.Collections.Array>? _familyProvider;
    private EstateResourceSystem? _resources;
    private VBoxContainer? _rooms;
    private VBoxContainer? _detail;
    private Label? _subtitle;
    private int _selectedRoom;
    private int _consumptionPeriod = 24;
    private Texture2D? _iconSheet;
    private readonly string[] _tabs = ["正院", "书斋", "静室", "后宅"];

    public override void _Ready()
    {
        AddToGroup("map_overlay_blocker"); MouseFilter = MouseFilterEnum.Stop; ZIndex = 100;
        BuildLayout(); Hide();
    }

    public void bind(Func<Godot.Collections.Array> familyProvider, EstateResourceSystem? resources)
    {
        _familyProvider = familyProvider; _resources = resources;
        // 资源账本变化不直接重建静室页，避免打断成员选择、资源下拉等正在进行的交互。
    }

    public void open(string tab = "meditation") { Show(); MoveToFront(); Refresh(); }
    public void refresh() { Refresh(); }
    // 不在 _Process 中重建界面：重建会关闭 OptionButton 等临时交互。
    // 仅在打开页面、切换静室、确认安排或资源账本发生实际变化时刷新。

    private void BuildLayout()
    {
        var backdrop = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/residence/meditation_room_bg.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);
        var veil = new ColorRect { Color = new Color(.015f, .012f, .01f, .62f), MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(veil);

        var frame = new MarginContainer(); frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); frame.AddThemeConstantOverride("margin_left", 34); frame.AddThemeConstantOverride("margin_right", 34); frame.AddThemeConstantOverride("margin_top", 22); frame.AddThemeConstantOverride("margin_bottom", 24); AddChild(frame);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); frame.AddChild(root);
        var header = new HBoxContainer(); root.AddChild(header);
        var title = Label("李家宅院  ›  静室", 30, new Color(.98f, .86f, .58f)); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(title);
        var close = new Button { Text = "返回地图", CustomMinimumSize = new Vector2(106, 42) }; ApplyButtonStyle(close); close.Pressed += () => { Hide(); Closed?.Invoke(); }; header.AddChild(close);
        _subtitle = Label("一室只供一人使用；修炼不离日程，闭关则暂离家中诸务。", 15, new Color(.84f, .81f, .71f)); root.AddChild(_subtitle);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 18); root.AddChild(body);
        body.AddChild(BuildNavigation());
        var center = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.6f }; center.AddThemeStyleboxOverride("panel", Style(new Color(.12f, .095f, .065f, .86f), new Color(.65f, .5f, .27f, .85f))); body.AddChild(center);
        var centerMargin = new MarginContainer(); centerMargin.AddThemeConstantOverride("margin_left", 18); centerMargin.AddThemeConstantOverride("margin_right", 18); centerMargin.AddThemeConstantOverride("margin_top", 16); centerMargin.AddThemeConstantOverride("margin_bottom", 16); center.AddChild(centerMargin);
        _rooms = new VBoxContainer(); _rooms.AddThemeConstantOverride("separation", 12); centerMargin.AddChild(_rooms);
        var right = new PanelContainer { CustomMinimumSize = new Vector2(366, 0) }; right.AddThemeStyleboxOverride("panel", Style(new Color(.08f, .07f, .055f, .93f), new Color(.68f, .53f, .29f, .9f))); body.AddChild(right);
        var rightMargin = new MarginContainer(); rightMargin.AddThemeConstantOverride("margin_left", 20); rightMargin.AddThemeConstantOverride("margin_right", 20); rightMargin.AddThemeConstantOverride("margin_top", 18); rightMargin.AddThemeConstantOverride("margin_bottom", 18); right.AddChild(rightMargin);
        _detail = new VBoxContainer(); _detail.AddThemeConstantOverride("separation", 10); rightMargin.AddChild(_detail);
    }

    private Control BuildNavigation()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(160, 0) }; panel.AddThemeStyleboxOverride("panel", Style(new Color(.055f, .05f, .04f, .9f), new Color(.47f, .36f, .19f, .85f)));
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 12); panel.AddChild(list); _iconSheet = GD.Load<Texture2D>("res://textures/ui/residence/residence_nav_icons.png");
        for (var i = 0; i < _tabs.Length; i++)
        {
            var item = new Button { Text = _tabs[i], CustomMinimumSize = new Vector2(0, 88), TooltipText = _tabs[i] == "静室" ? "查看静室、修炼与闭关安排" : "此处内容将在后续开放", Disabled = _tabs[i] != "静室" };
            ApplyButtonStyle(item, _tabs[i] == "静室"); item.AddThemeFontSizeOverride("font_size", 18); item.Icon = IconAt(i); item.ExpandIcon = true; list.AddChild(item);
        }
        return panel;
    }

    private Texture2D? IconAt(int index)
    {
        if (_iconSheet is null) return null;
        var x = index % 2 * 768; var y = index / 2 * 512;
        return new AtlasTexture { Atlas = _iconSheet, Region = new Rect2(x, y, 768, 512) };
    }

    private void Refresh()
    {
        if (!Visible || _rooms is null) return;
        foreach (var child in _rooms.GetChildren()) child.QueueFree();
        var count = _resources?.get_facility_count("meditation_room") ?? 0;
        if (count == 0)
        {
            _rooms.AddChild(Label("宅院尚无静室", 23, new Color(.97f, .85f, .6f)));
            _rooms.AddChild(Label("静室须先在“辖地 · 营建”中兴建。建成后，此处可安排族人修炼、闭关与提供资源。", 16, new Color(.85f, .81f, .7f)));
            var build = new Button { Text = "前往辖地营建", CustomMinimumSize = new Vector2(0, 46) }; ApplyButtonStyle(build, true); build.Pressed += () => BuildRequested?.Invoke(); _rooms.AddChild(build);
            _selectedRoom = -1; RefreshDetailOnly(); return;
        }
        _selectedRoom = Mathf.Clamp(_selectedRoom, 0, count - 1);
        _rooms.AddChild(Label($"静室 {count} 间　｜　选择一间查看或安排成员", 18, new Color(.9f, .85f, .72f)));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _rooms.AddChild(scroll);
        var cards = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill }; cards.AddThemeConstantOverride("h_separation", 16); cards.AddThemeConstantOverride("v_separation", 16); scroll.AddChild(cards);
        for (var i = 0; i < count; i++) cards.AddChild(RoomCard(i));
        cards.AddChild(ExpansionCard());
        RefreshDetailOnly();
    }

    private Control RoomCard(int index)
    {
        var occupant = Occupant(index); var active = index == _selectedRoom;
        var card = new Control { CustomMinimumSize = new Vector2(240, 360), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var image = new TextureRect { Texture = GD.Load<Texture2D>(occupant is null ? "res://textures/ui/residence/room_empty.png" : "res://textures/ui/residence/room_occupied.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); card.AddChild(image);
        var shade = new ColorRect { Color = new Color(.03f, .025f, .018f, active ? .08f : .28f), MouseFilter = MouseFilterEnum.Ignore }; shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); card.AddChild(shade);
        var button = new Button { Flat = true, TooltipText = "查看此间静室", MouseFilter = MouseFilterEnum.Stop };
        button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); button.Pressed += () => { _selectedRoom = index; Refresh(); }; card.AddChild(button);
        var name = occupant?.entity_name ?? "空置";
        var status = occupant is null ? "可安排一名族人" : occupant.is_in_breakthrough_retreat() ? "闭关突破中 · 暂离诸务" : "日常修炼中 · 仍按日程行止";
        var title = Label($"静室 {index + 1}", 22, new Color(.94f, .88f, .73f)); title.HorizontalAlignment = HorizontalAlignment.Center; title.MouseFilter = MouseFilterEnum.Ignore; title.SetAnchorsPreset(LayoutPreset.TopWide); title.Position = new Vector2(12, 16); title.Size = new Vector2(-24, 38); card.AddChild(title);
        var plaque = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore }; plaque.SetAnchorsPreset(LayoutPreset.BottomWide); plaque.OffsetLeft = 16; plaque.OffsetRight = -16; plaque.OffsetTop = -96; plaque.OffsetBottom = -20; plaque.AddThemeStyleboxOverride("panel", Style(new Color(.08f, .065f, .045f, .86f), active ? new Color(.75f, .61f, .36f, .95f) : new Color(.38f, .31f, .21f, .9f))); card.AddChild(plaque);
        var copy = new VBoxContainer(); copy.AddThemeConstantOverride("separation", 2); plaque.AddChild(copy); var who = Label(name, 18, new Color(.94f, .89f, .77f)); who.HorizontalAlignment = HorizontalAlignment.Center; copy.AddChild(who); var state = Label(status, 13, occupant is null ? new Color(.8f, .78f, .69f) : new Color(.72f, .84f, .74f)); state.HorizontalAlignment = HorizontalAlignment.Center; copy.AddChild(state);
        return card;
    }

    private Control ExpansionCard()
    {
        var canBuild = _resources is not null && _resources.can_afford(EstateResourceSystem.Stone, 6);
        var card = new Button { Text = canBuild ? "+\n扩建静室\n石材 6" : "+\n扩建静室\n建材不足（石材 6）", CustomMinimumSize = new Vector2(240, 360), SizeFlagsHorizontal = SizeFlags.ExpandFill, Disabled = !canBuild };
        ApplyButtonStyle(card, canBuild); card.AddThemeFontSizeOverride("font_size", 18); card.Pressed += () => ExpansionRequested?.Invoke(); return card;
    }

    private void RefreshDetailOnly()
    {
        if (!Visible || _detail is null) return;
        foreach (var child in _detail.GetChildren()) child.QueueFree();
        var count = _resources?.get_facility_count("meditation_room") ?? 0;
        if (count == 0) { _detail.AddChild(Label("静室未建", 24, new Color(.97f, .85f, .59f))); _detail.AddChild(Label("营建完成后，才可在此安排成员与资源。", 15, new Color(.84f, .8f, .69f))); return; }
        var occupant = Occupant(_selectedRoom);
        _detail.AddChild(Label($"静室 {_selectedRoom + 1}", 25, new Color(.98f, .86f, .58f)));
        if (occupant is null) { BuildAssignment(); return; }
        BuildOccupantDetail(occupant);
    }

    private void BuildAssignment()
    {
        _detail!.AddChild(Label("当前空置", 17, new Color(.8f, .85f, .73f)));
        _detail.AddChild(Label("安排成员后，他会在每日修炼时段使用此室；其余时间仍按日程休憩、劳作或研读。", 15, new Color(.83f, .8f, .7f)));
        var candidates = Family().Where(c => c.meditation_room_index < 0 && !c.is_in_breakthrough_retreat()).ToList();
        if (candidates.Count == 0) { _detail.AddChild(Label("暂无可安排的族人。", 15, new Color(.85f, .75f, .62f))); return; }
        var picker = new OptionButton(); foreach (var member in candidates) picker.AddItem($"{member.entity_name} · {member.cultivation_realm}"); _detail.AddChild(picker);
        var assign = new Button { Text = "安排入室修炼", CustomMinimumSize = new Vector2(0, 44) }; ApplyButtonStyle(assign, true); assign.Pressed += () => { candidates[picker.Selected].assign_meditation_room(_selectedRoom); Refresh(); }; _detail.AddChild(assign);
    }

    private void BuildOccupantDetail(Character member)
    {
        var retreat = member.get_breakthrough_retreat_summary(); var retreating = retreat.GetValueOrDefault("active", false).AsBool();
        _detail!.AddChild(Label($"{member.entity_name} · {member.cultivation_realm}", 18, new Color(.93f, .86f, .68f)));
        _detail.AddChild(Label(retreating ? $"闭关突破中　余 {retreat.GetValueOrDefault("remaining_hours", 0).AsInt32() / 24f:0.0} 日" : "日常修炼中　仍遵循个人日程", 15, retreating ? new Color(.96f, .74f, .45f) : new Color(.74f, .88f, .76f)));
        _detail.AddChild(new HSeparator());
        _detail.AddChild(Label("实际修炼速度", 15, new Color(.84f, .78f, .64f)));
        var rate = member.get_cultivation_rate();
        _detail.AddChild(Label(rate > 0 ? $"{rate:0.####} 缕 / 时" : "尚未设定主修功法", rate > 0 ? 27 : 18, rate > 0 ? new Color(.56f, .86f, .71f) : new Color(.9f, .72f, .48f)));
        var environment=member.get_cultivation_environment_breakdown(member.get_current_lingqi_pressure());
        _detail.AddChild(Label($"本地灵力密度　{member.ambient_lingqi_pressure:0}\n提供资源后密度　{member.get_current_lingqi_pressure():0}\n当前需求密度　{member.get_realm_required_lingqi_pressure_for_ui():0}\n环境适配　　　 {environment.adaptation:P0}\n富余转化　　　 {environment.surplus:P0}\n环境效能　　　×{environment.factor:0.00}", 15, new Color(.86f, .83f, .74f)));
        _detail.AddChild(new HSeparator());
        _detail.AddChild(Label("提供资源", 17, new Color(.97f, .84f, .56f)));
        var supplied = member.meditation_spirit_stone_supply && member.has_spirit_stones_for_meditation() && rate > 0;
        _detail.AddChild(Label(supplied ? "灵石　辖地账本资源　·　提供中" : "灵石　辖地账本资源　·　未提供", 15, supplied ? new Color(.65f, .87f, .72f) : new Color(.84f, .8f, .69f)));
        var dayHours = Mathf.Max(0, member.daily_cultivation_hours);
        var units = new HBoxContainer(); units.AddThemeConstantOverride("separation", 6); _detail.AddChild(units);
        AddConsumptionUnit(units, "每时", 1); AddConsumptionUnit(units, "每日", 24); AddConsumptionUnit(units, "每月", 720);
        var activeHours = _consumptionPeriod == 1 ? 1f : _consumptionPeriod == 24 ? dayHours : dayHours * 30f;
        var unitName = _consumptionPeriod == 1 ? "每时" : _consumptionPeriod == 24 ? "每日" : "每月";
        var expected = member.get_spirit_stone_consumption_estimate(activeHours);
        _detail.AddChild(Label($"预计{unitName}消耗　{expected:0.####} 灵石\n（每日按日程修炼 {dayHours} 时计算）", 16, new Color(.82f, .78f, .68f)));
        var supply = new Button { Text = supplied ? "停止提供灵石" : "提供灵石", CustomMinimumSize = new Vector2(0, 40), Disabled = rate <= 0 || (!supplied && (_resources?.get_amount(EstateResourceSystem.SpiritStones) ?? 0) <= 0) }; ApplyButtonStyle(supply, supplied); supply.Pressed += () => { member.set_meditation_spirit_stone_enabled(!supplied); Refresh(); }; _detail.AddChild(supply);
        var material = new Button { Text = "添加仓棚物品（即将开放）", Disabled = true, CustomMinimumSize = new Vector2(0, 36) }; ApplyButtonStyle(material); _detail.AddChild(material);
        _detail.AddChild(new HSeparator());
        if (!retreating && member.is_breakthrough_ready())
        {
            var start = new Button { Text = $"闭关突破（约 {member.get_breakthrough_retreat_hours(true) / 24f:0.0} 日）", CustomMinimumSize = new Vector2(0, 44) }; ApplyButtonStyle(start, true); start.Pressed += () => { member.start_breakthrough_retreat(true); Refresh(); }; _detail.AddChild(start);
        }
        if (!retreating) { var leave = new Button { Text = "结束静室安排", CustomMinimumSize = new Vector2(0, 36) }; ApplyButtonStyle(leave); leave.Pressed += () => { member.leave_meditation_room(); Refresh(); }; _detail.AddChild(leave); }
    }

    private Character? Occupant(int index) => Family().FirstOrDefault(member => member.meditation_room_index == index);
    private void AddConsumptionUnit(Container parent, string text, int period)
    {
        var button = new Button { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 32) }; ApplyButtonStyle(button, _consumptionPeriod == period); button.Pressed += () => { _consumptionPeriod = period; RefreshDetailOnly(); }; parent.AddChild(button);
    }
    private List<Character> Family()
    {
        List<Character> result = [];
        var source = _familyProvider?.Invoke(); if (source is null) return result;
        foreach (Variant value in source) if (value.AsGodotObject() is Character member && GodotObject.IsInstanceValid(member)) result.Add(member);
        return result;
    }
    private static Label Label(string text, int size, Color color) { var result = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; result.AddThemeFontSizeOverride("font_size", size); result.AddThemeColorOverride("font_color", color); return result; }
    private static StyleBoxFlat Style(Color bg, Color border) => new() { BgColor = bg, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 };
    private static void ApplyButtonStyle(Button button, bool selected = false) { button.AddThemeStyleboxOverride("normal", Style(selected ? new Color(.28f, .22f, .12f, .96f) : new Color(.12f, .1f, .075f, .94f), selected ? new Color(.9f, .67f, .3f, .95f) : new Color(.48f, .38f, .23f, .9f))); button.AddThemeStyleboxOverride("hover", Style(new Color(.34f, .27f, .15f, .98f), new Color(.98f, .78f, .4f, 1f))); button.AddThemeColorOverride("font_color", new Color(.96f, .87f, .65f)); }
}
