using Godot;

/// <summary>全屏见闻簿；共用人物页美术，栏目、列表与正文各自保持稳定位置。</summary>
public partial class JournalPanel : Control
{
    public enum Section { Rumors, Journal, People, Events, Tasks }
    public sealed record Entry(string Title, string Body, string Source, string Place, string State, string? Time = null, string Id = "", long AcceptedOrder = 0);
    public event Action<string?>? TrackingRequested;
    public string TrackedTaskId { get; set; } = "";
    private readonly Dictionary<Section, List<Entry>> _entries = new();
    private readonly Dictionary<Section, Button> _tabs = new();
    private readonly List<(Entry entry, Button button)> _rows = [];
    private VBoxContainer _entryList = null!;
    private HBoxContainer _filters = null!;
    private Label _detailTitle = null!, _detailMeta = null!, _detailBody = null!, _time = null!, _goal = null!;
    private VBoxContainer _goalBox = null!;
    private Button _track = null!;
    private ScrollContainer _detailScroll = null!;
    private Control _listRegion = null!;
    private Section _current = Section.Rumors;
    private Entry? _selected;
    private bool _completed;
    private static readonly Color Ink = new("211c14"), Muted = new("62543b"), Accent = new("49665b");
    private const float TextScale = .8f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; ZIndex = 250;
        Theme = new Theme { DefaultFont = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf"), DefaultFontSize = 16 };
        Build(); Hide();
    }
    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager") is { } time)
            _time.Text = $"{time.year}年{time.month}月{time.day}日";
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { hide_panel(); GetViewport().SetInputAsHandled(); }
    }
    public void set_entries(Section section, IEnumerable<Entry> entries)
    {
        _entries[section] = entries.ToList();
        if (Visible && _current == section) Populate(_selected?.Id, _selected?.Title);
    }
    public void show_panel(Section section = Section.Rumors, string? selectedTitle = null)
    {
        _current = section;
        var chosen = _entries.GetValueOrDefault(section, []).FirstOrDefault(e => e.Id == selectedTitle || e.Title == selectedTitle);
        _completed = section == Section.Tasks && chosen?.State == "已完成";
        Show(); MoveToFront(); Populate(chosen?.Id, selectedTitle);
    }
    public void hide_panel() => Hide();

    private void Build()
    {
        var background = new TextureRect { Name = "SharedCharacterBackground", Texture = GD.Load<Texture2D>("res://textures/ui/infopanel_fullscreen_bg_v1.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var header = new HBoxContainer(); AddChild(header);
        header.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide); header.OffsetLeft = 28; header.OffsetRight = -28; header.OffsetTop = 14; header.OffsetBottom = 70;
        var heading = Text("见闻", 32); heading.AutowrapMode = TextServer.AutowrapMode.Off; heading.AddThemeColorOverride("font_color", new Color("eee6d2")); header.AddChild(heading);
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _time = Text("", 21); _time.AutowrapMode = TextServer.AutowrapMode.Off; header.AddChild(_time);
        var back = Button("返回", 21); back.CustomMinimumSize = new Vector2(110, 44); back.Pressed += hide_panel; header.AddChild(back);
        var rail = new PanelContainer(); AddChild(rail); PlaceIn(rail, .016f, .088f, .166f, .974f);
        rail.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.045f, .055f, .05f, .78f), BorderColor = new Color(.72f, .64f, .45f, .38f), BorderWidthRight = 1, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 18 });
        var navScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; rail.AddChild(navScroll);
        var nav = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; nav.AddThemeConstantOverride("separation", 12); navScroll.AddChild(nav);
        foreach (var item in new[] { ("传闻", Section.Rumors), ("行记", Section.Journal), ("人物", Section.People), ("异动", Section.Events), ("任务", Section.Tasks) })
        {
            var tab = Button(item.Item1, 26); tab.CustomMinimumSize = new Vector2(0, 58); nav.AddChild(tab); _tabs[item.Item2] = tab;
            tab.Pressed += () => { _current = item.Item2; _completed = false; Populate(); };
        }
        nav.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });
        var reserved = Text("待续", 16); reserved.AddThemeColorOverride("font_color", new Color("c8bea5")); nav.AddChild(reserved);
        foreach (var name in new[] { "地志", "势力", "妖录", "灵物", "旧闻" })
        {
            var tab = Button(name, 17); tab.AddThemeColorOverride("font_color", new Color("c8bea5"));
            tab.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); nav.AddChild(tab); tab.Pressed += () => ShowReserved(name);
        }
        var paper = new PanelContainer(); AddChild(paper); PlaceIn(paper, .177f, .088f, .984f, .974f);
        paper.AddThemeStyleboxOverride("panel", Surface());
        // The column anchors share one paper rather than separate floating cards.
        var content = new Control(); paper.AddChild(content);
        _listRegion = new MarginContainer(); content.AddChild(_listRegion); PlaceIn(_listRegion, 0, 0, .335f, 1);
        var listBox = new VBoxContainer(); _listRegion.AddChild(listBox);
        _filters = new HBoxContainer(); listBox.AddChild(_filters);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; listBox.AddChild(scroll);
        _entryList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _entryList.AddThemeConstantOverride("separation", 8); scroll.AddChild(_entryList);
        var divider = new ColorRect { Color = new Color(.29f, .24f, .16f, .38f), MouseFilter = MouseFilterEnum.Ignore }; content.AddChild(divider); PlaceIn(divider, .352f, 0, .352f, 1); divider.OffsetRight = 1;
        var detail = new VBoxContainer(); content.AddChild(detail); PlaceIn(detail, .377f, 0, 1, 1); detail.AddThemeConstantOverride("separation", 15);
        _detailTitle = Text("", 30); detail.AddChild(_detailTitle);
        _detailMeta = Text("", 18, true); detail.AddChild(_detailMeta);
        detail.AddChild(Rule());
        _detailScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; detail.AddChild(_detailScroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 18); _detailScroll.AddChild(body);
        _goalBox = new VBoxContainer(); body.AddChild(_goalBox); _goalBox.AddChild(Text("当前目标", 22));
        var goalPaper = new PanelContainer(); goalPaper.AddThemeStyleboxOverride("panel", RowStyle(true)); _goalBox.AddChild(goalPaper); _goal = Text("", 22); goalPaper.AddChild(_goal);
        _detailBody = Text("", 21); _detailBody.AddThemeConstantOverride("line_spacing", 8); body.AddChild(_detailBody);
        _track = Button("追踪任务", 20); _track.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; _track.CustomMinimumSize = new Vector2(150, 44); detail.AddChild(_track);
        _track.Pressed += () => { if (_selected is null) return; TrackingRequested?.Invoke(_selected.Id == TrackedTaskId ? null : _selected.Id); Populate(_selected.Id, _selected.Title); };
    }
    private void Populate(string? id = null, string? title = null)
    {
        foreach (var node in _entryList.GetChildren()) { _entryList.RemoveChild(node); node.QueueFree(); }
        foreach (var node in _filters.GetChildren()) { _filters.RemoveChild(node); node.QueueFree(); }
        _rows.Clear(); _listRegion.Show();
        foreach (var pair in _tabs)
        {
            var selected = pair.Key == _current;
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                pair.Value.AddThemeColorOverride(state, selected ? Ink : new Color("ede7d6"));
            StyleBox background = selected ? new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_plaque_clean_v2.png"), TextureMarginLeft = 32, TextureMarginRight = 32, ContentMarginLeft = 12, ContentMarginRight = 12 } : new StyleBoxEmpty();
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
                pair.Value.AddThemeStyleboxOverride(state, background);
        }
        if (_current == Section.Tasks)
        {
            foreach (var completed in new[] { false, true })
            {
                var filter = Button(completed ? "已完成" : "进行中", 21); filter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                if (completed == _completed) filter.AddThemeStyleboxOverride("normal", RowStyle(true));
                _filters.AddChild(filter); filter.Pressed += () => { _completed = completed; Populate(); };
            }
        }
        else
        {
            var caption = Text("全部记录", 20, true);
            caption.AutowrapMode = TextServer.AutowrapMode.Off;
            caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _filters.AddChild(caption);
        }
        var entries = _entries.GetValueOrDefault(_current, []).Where(e => _current != Section.Tasks || (e.State == "已完成") == _completed).ToList();
        foreach (var entry in entries)
        {
            var button = Button("", 20); button.Name = "Entry" + _rows.Count; button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore }; button.AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 12);
            var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; margin.AddChild(box);
            box.AddChild(Text(entry.Title, 24));
            box.AddChild(Text(string.Join(" · ", new[] { entry.Place, entry.State }.Where(s => !string.IsNullOrWhiteSpace(s))), 17, true));
            margin.MinimumSizeChanged += () => button.CustomMinimumSize = new Vector2(0, margin.GetCombinedMinimumSize().Y);
            button.Pressed += () => ShowEntry(entry); _entryList.AddChild(button); _rows.Add((entry, button));
        }
        if (entries.Count > 0) ShowEntry(entries.FirstOrDefault(e => (!string.IsNullOrEmpty(id) && e.Id == id) || e.Title == title) ?? entries[0]);
        else { _selected = null; _detailTitle.Text = "暂无" + (_current == Section.Tasks ? (_completed ? "已完成任务" : "进行中任务") : "记录"); _detailMeta.Text = "见闻会随你的经历逐渐补全。"; _detailBody.Text = "此处暂未收录新的内容。"; _goalBox.Hide(); _track.Hide(); _detailBody.Show(); }
    }
    private void ShowEntry(Entry entry)
    {
        _selected = entry; _detailTitle.Text = entry.Title;
        _detailMeta.Text = $"来源　{entry.Source}\n地点　{entry.Place}\n进展　{entry.State}" + (string.IsNullOrWhiteSpace(entry.Time) ? "" : $"\n记录　{entry.Time}");
        _detailBody.Text = entry.Body; _detailScroll.ScrollVertical = 0;
        _goalBox.Visible = _current == Section.Tasks; _goal.Text = entry.Body;
        // The data currently has one goal text; do not fabricate a second instruction paragraph.
        _detailBody.Visible = _current != Section.Tasks;
        _track.Visible = _current == Section.Tasks && entry.State != "已完成" && !string.IsNullOrEmpty(entry.Id);
        _track.Text = entry.Id == TrackedTaskId ? "取消追踪" : "追踪任务";
        foreach (var row in _rows) row.button.AddThemeStyleboxOverride("normal", RowStyle(row.entry == entry));
    }
    private void ShowReserved(string title)
    {
        _selected = null; _detailTitle.Text = title + " · 待续"; _detailMeta.Text = "此栏入口已留出。";
        _detailBody.Text = "随着游历展开，这里会逐步收录相关见闻。"; _detailBody.Show(); _goalBox.Hide(); _track.Hide(); _listRegion.Hide();
    }
    private static void PlaceIn(Control node, float left, float top, float right, float bottom)
    { node.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); node.AnchorLeft = left; node.AnchorTop = top; node.AnchorRight = right; node.AnchorBottom = bottom; }
    private static Label Text(string text, int size, bool muted = false)
    { var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore }; label.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(size * TextScale)); label.AddThemeColorOverride("font_color", muted ? Muted : Ink); return label; }
    private static Button Button(string text, int size)
    {
        var button = new Button { Text = text, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(size * TextScale));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, Ink);
        button.AddThemeStyleboxOverride("normal", RowStyle(false)); button.AddThemeStyleboxOverride("hover", RowStyle(true)); button.AddThemeStyleboxOverride("pressed", RowStyle(true));
        button.AddThemeStyleboxOverride("focus", new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = Accent, BorderWidthBottom = 2 }); return button;
    }
    private static StyleBoxFlat Surface() => new() { BgColor = new Color(.91f, .875f, .78f, .89f), BorderColor = new Color(.27f, .23f, .17f, .68f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7, ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 16, ContentMarginBottom = 16 };
    private static StyleBoxFlat RowStyle(bool selected) => new() { BgColor = selected ? new Color(.36f, .43f, .36f, .14f) : Colors.Transparent, BorderColor = selected ? Accent : new Color(.29f, .24f, .16f, .24f), BorderWidthLeft = selected ? 3 : 0, BorderWidthBottom = 1, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10 };
    private static ColorRect Rule() => new() { Color = new Color(.29f, .24f, .16f, .38f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore };
}
