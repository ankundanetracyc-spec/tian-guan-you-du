using Godot;

/// <summary>纸墨信匣：稳定三栏、独立滚动、真实信件快照及限时邀约。</summary>
public partial class LetterPanel : Control
{
    public event Action? Closed;
    public event Action<LetterData>? AttendanceRequested;
    public Func<string, bool>? ActivityAvailable;
    private static readonly Vector2 DesignSize = new(1672, 941);
    private static readonly Color Ink = WorldHudStyle.Ink, Muted = WorldHudStyle.SecondaryInk,
        PaperText = new("eee8d7"), Accent = new("52766a"), Red = new("985344");
    private const string FontPath = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";
    private LetterMailbox? _mailbox;
    private Control _canvas = null!, _empty = null!;
    private VBoxContainer _rows = null!, _reading = null!;
    private ScrollContainer _listScroll = null!, _bodyScroll = null!;
    private Label _summary = null!, _date = null!, _matches = null!, _title = null!, _meta = null!,
        _inviteTitle = null!, _inviteState = null!, _invitePlace = null!, _inviteReply = null!, _inviteTime = null!;
    private RichTextLabel _body = null!;
    private PanelContainer _invite = null!;
    private Button _read = null!, _accept = null!, _decline = null!, _attend = null!;
    private readonly Dictionary<string, Button> _tabs = [];
    private string _section = "all", _query = "", _selected = "";
    private bool _oldestFirst;
    private int _lastHour = -1;
    public int? PreviewHour { get; set; }
    private int Now => PreviewHour ?? GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; ZIndex = 250;
        Theme = new Theme { DefaultFont = GD.Load<FontFile>(FontPath), DefaultFontSize = 21 };
        Build(); Resized += Fit; Fit(); Hide();
    }
    public override void _ExitTree() { if (_mailbox is not null) _mailbox.Changed -= Refresh; }
    public override void _Process(double delta)
    {
        if (!Visible || _lastHour == Now) return;
        _lastHour = Now; _date.Text = LetterMailbox.Date(Now); Refresh();
    }
    public void Bind(LetterMailbox mailbox)
    {
        if (_mailbox is not null) _mailbox.Changed -= Refresh;
        _mailbox = mailbox; mailbox.Changed += Refresh; _selected = "";
        if (IsNodeReady()) Refresh();
    }
    public void Open(string selectedId = "")
    {
        Show(); MoveToFront(); _lastHour = Now; _date.Text = LetterMailbox.Date(Now);
        if (!string.IsNullOrWhiteSpace(selectedId)) SelectLetter(selectedId);
        else Refresh();
    }
    public void Close() { Hide(); Closed?.Invoke(); }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (Visible && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { Close(); GetViewport().SetInputAsHandled(); }
    }
    public void SelectLetter(string id)
    {
        if (_mailbox?.Find(id) is not { } letter) return;
        _selected = id; _bodyScroll.ScrollVertical = 0;
        if (!letter.is_read) _mailbox.SetRead(id, true); else Refresh();
    }
    private void Fit()
    {
        if (_canvas is null) return;
        var scale = Mathf.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * scale; _canvas.Position = (Size - DesignSize * scale) / 2;
    }
    private static void Rect(Control control, float x, float y, float w, float h)
    { control.Position = new Vector2(x, y); control.Size = new Vector2(w, h); }
    private static Label Text(string value, int size = 21, bool secondary = false, bool paragraph = false)
    {
        var label = WorldHudStyle.Text(value, size, secondary);
        label.AutowrapMode = paragraph ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        return label;
    }
    private static Label Clipped(string value, int size = 21, bool secondary = false)
    {
        var label = Text(value, size, secondary); label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; return label;
    }
    private static StyleBoxFlat Edge(bool selected = false, int padding = 8) => new()
    {
        BgColor = Colors.Transparent, DrawCenter = false,
        BorderColor = selected ? Accent : new Color("ad9875", .65f),
        BorderWidthLeft = selected ? 2 : 1, BorderWidthRight = selected ? 2 : 1,
        BorderWidthTop = selected ? 2 : 1, BorderWidthBottom = selected ? 2 : 1,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding
    };
    private static Button Button(string caption, int font = 20)
    {
        var button = new Button { Text = caption, CustomMinimumSize = new Vector2(0, 42) };
        WorldHudStyle.StyleSmallButton(button, font);
        button.AddThemeColorOverride("font_hover_pressed_color", Ink); button.AddThemeColorOverride("font_disabled_color", new Color("89806b"));
        button.AddThemeStyleboxOverride("normal", Edge()); button.AddThemeStyleboxOverride("disabled", Edge());
        foreach (var state in new[] { "hover", "pressed", "hover_pressed", "focus" })
        { var edge = Edge(true); edge.BorderColor = new Color("c5ad77"); button.AddThemeStyleboxOverride(state, edge); }
        button.MouseEntered += () => { if (!button.Disabled) button.Modulate = new Color(1.07f, 1.05f, 1.01f); };
        button.MouseExited += () => button.Modulate = Colors.White; return button;
    }
    private static VBoxContainer Box(Control parent, int gap = 10)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", gap); parent.AddChild(box); return box;
    }
    private static ScrollContainer Scroll(Control parent)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        parent.AddChild(scroll); return scroll;
    }
    private VBoxContainer Paper(float x, float y, float width, float height, int padding)
    {
        var panel = new PanelContainer(); _canvas.AddChild(panel); Rect(panel, x, y, width, height);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        panel.AddChild(new InventoryPaperFrame());
        var margin = new MarginContainer(); panel.AddChild(margin);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, padding);
        return Box(margin, 12);
    }
    private static TextureRect Envelope(int size) => new()
    {
        Texture = GD.Load<Texture2D>("res://textures/ui/qingxi/bottom_nav_letters_ink_v1.png"),
        CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
    };
    private void Build()
    {
        _canvas = new Control { Name = "LettersCanvas", Size = DesignSize }; AddChild(_canvas);
        var background = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/ui/inventory_20261012/approved_shell_v1.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(background); Rect(background, 0, 0, 1672, 941);
        var back = Button("←", 30); _canvas.AddChild(back); Rect(back, 28, 18, 52, 48); back.Pressed += Close;
        var material = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://textures/ui/gongfa_back_button_v1.png") };
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) back.AddThemeStyleboxOverride(state, material);
        var title = Text("信件", 32); _canvas.AddChild(title); Rect(title, 92, 15, 200, 54);
        _date = Text("", 21, true); _date.HorizontalAlignment = HorizontalAlignment.Right; _canvas.AddChild(_date); Rect(_date, 970, 22, 420, 40);
        var railTitle = Text("尺素往来", 28); railTitle.AddThemeColorOverride("font_color", PaperText); railTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _canvas.AddChild(railTitle); Rect(railTitle, 30, 104, 214, 44);
        var icon = Envelope(104); icon.Modulate = new Color(1, 1, 1, .78f); _canvas.AddChild(icon); Rect(icon, 85, 166, 104, 88);
        _summary = Text("", 20); _summary.AddThemeColorOverride("font_color", PaperText); _summary.HorizontalAlignment = HorizontalAlignment.Center;
        _canvas.AddChild(_summary); Rect(_summary, 30, 257, 214, 64);
        var index = 0;
        foreach (var (id, caption) in new[] { ("all", "全部"), ("unread", "未读"), ("invites", "邀约") })
        {
            var tab = Button(caption, 25); _canvas.AddChild(tab); Rect(tab, 34, 329 + index++ * 64, 208, 52); _tabs[id] = tab;
            tab.Pressed += () => { _section = id; _selected = ""; _listScroll.ScrollVertical = 0; Refresh(); };
        }
        var list = Paper(282, 105, 410, 795, 14);
        var search = new LineEdit { PlaceholderText = "搜索署名、标题、信中内容", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(0, 44) };
        search.AddThemeFontSizeOverride("font_size", 19); search.AddThemeColorOverride("font_color", Ink);
        search.AddThemeColorOverride("font_placeholder_color", Muted); search.AddThemeColorOverride("caret_color", Ink);
        search.AddThemeStyleboxOverride("normal", Edge()); search.AddThemeStyleboxOverride("focus", Edge(true)); list.AddChild(search);
        search.TextChanged += value => { _query = value.Trim(); _listScroll.ScrollVertical = 0; RefreshList(); };
        var toolbar = new HBoxContainer(); list.AddChild(toolbar);
        _matches = Text("", 18, true); _matches.SizeFlagsHorizontal = SizeFlags.ExpandFill; toolbar.AddChild(_matches);
        var sort = Button("新信在前 ▾", 18); sort.CustomMinimumSize = new Vector2(126, 38); toolbar.AddChild(sort);
        sort.Pressed += () => { _oldestFirst = !_oldestFirst; sort.Text = _oldestFirst ? "旧信在前 ▴" : "新信在前 ▾"; RefreshList(); };
        list.AddChild(WorldHudStyle.Rule()); _listScroll = Scroll(list); _rows = Box(_listScroll, 8);
        _reading = Paper(710, 105, 922, 795, 24);
        _title = Clipped("", 31); _reading.AddChild(_title); _meta = Text("", 19, true, true); _reading.AddChild(_meta);
        _reading.AddChild(WorldHudStyle.Rule()); _bodyScroll = Scroll(_reading);
        _body = new RichTextLabel { FitContent = true, ScrollActive = false, BbcodeEnabled = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeFontOverride("normal_font", GD.Load<FontFile>(FontPath)); _body.AddThemeFontSizeOverride("normal_font_size", 22);
        _body.AddThemeColorOverride("default_color", Ink); _body.AddThemeConstantOverride("line_separation", 5); _bodyScroll.AddChild(_body);
        BuildInvitation();
        _reading.AddChild(WorldHudStyle.Rule());
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End }; footer.AddThemeConstantOverride("separation", 12); _reading.AddChild(footer);
        _read = Button("标为未读"); footer.AddChild(_read);
        _read.Pressed += () => { if (_mailbox?.Find(_selected) is { } letter) _mailbox.SetRead(_selected, !letter.is_read); };
        _empty = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore }; _canvas.AddChild(_empty); Rect(_empty, 734, 129, 874, 747);
        var empty = new VBoxContainer { CustomMinimumSize = new Vector2(600, 0) }; empty.AddThemeConstantOverride("separation", 18); _empty.AddChild(empty);
        var emptyIcon = Envelope(128); emptyIcon.Modulate = new Color(1, 1, 1, .35f); empty.AddChild(emptyIcon);
        var emptyTitle = Text("", 29); emptyTitle.Name = "EmptyTitle"; emptyTitle.HorizontalAlignment = HorizontalAlignment.Center; empty.AddChild(emptyTitle);
        var emptyBody = Text("", 20, true, true); emptyBody.Name = "EmptyBody"; emptyBody.HorizontalAlignment = HorizontalAlignment.Center; empty.AddChild(emptyBody);
    }
    private void BuildInvitation()
    {
        _invite = new PanelContainer(); _invite.AddThemeStyleboxOverride("panel", Edge(true, 14)); _reading.AddChild(_invite);
        var box = Box(_invite, 7); var heading = new HBoxContainer(); box.AddChild(heading);
        _inviteTitle = Clipped("", 24); _inviteTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(_inviteTitle);
        _inviteState = Text("", 18); heading.AddChild(_inviteState);
        _invitePlace = Text("", 20); box.AddChild(_invitePlace);
        _inviteReply = Text("", 18, true); box.AddChild(_inviteReply);
        _inviteTime = Text("", 18, true, true); box.AddChild(_inviteTime);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); box.AddChild(actions);
        _accept = Button("应邀"); _decline = Button("婉拒"); actions.AddChild(_accept); actions.AddChild(_decline);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); _attend = Button("赴约"); actions.AddChild(_attend);
        _accept.Pressed += () => _mailbox?.Reply(_selected, true, Now);
        _decline.Pressed += () => _mailbox?.Reply(_selected, false, Now);
        _attend.Pressed += () =>
        {
            if (_mailbox?.Find(_selected) is { invitation: { } invite } letter && invite.CanAttend(Now)
                && ActivityAvailable?.Invoke(invite.activity_key) == true) AttendanceRequested?.Invoke(letter);
            Refresh();
        };
    }
    private bool Matches(LetterData letter)
    {
        var category = _section switch
        {
            "unread" => !letter.is_read, "invites" => letter.invitation is not null, _ => true
        };
        var haystack = $"{letter.SenderLabel} {letter.subject} {letter.body} {letter.invitation?.activity_name} {letter.invitation?.place_name}";
        return category && _query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
    private void Refresh()
    {
        if (!IsNodeReady()) return;
        var count = _mailbox?.letters.Count ?? 0; _summary.Text = $"收存 {count} 封\n未读 {_mailbox?.UnreadCount ?? 0} 封";
        foreach (var entry in _tabs)
        {
            var selected = entry.Key == _section;
            StyleBox material = selected ? new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://textures/ui/infopanel_nav_plaque_clean_v2.png") } : new StyleBoxEmpty();
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" }) entry.Value.AddThemeStyleboxOverride(state, material);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                entry.Value.AddThemeColorOverride(state, selected ? Ink : PaperText);
        }
        RefreshList(); RefreshLetter();
    }
    private void RefreshList()
    {
        var scroll = _listScroll.ScrollVertical;
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var shown = (_mailbox?.letters.Where(letter => letter is not null && Matches(letter)) ?? []).ToList();
        shown = _oldestFirst ? shown.OrderBy(letter => letter.sent_at_hour).ToList() : shown.OrderByDescending(letter => letter.sent_at_hour).ToList();
        _matches.Text = $"{shown.Count} 封信笺";
        foreach (var letter in shown)
        {
            var row = Button(""); row.CustomMinimumSize = new Vector2(0, 116); _rows.AddChild(row);
            var edge = Edge(letter.letter_id == _selected);
            if (!letter.is_read && letter.letter_id != _selected) { edge.BorderColor = Red; edge.BorderWidthLeft = 3; }
            row.AddThemeStyleboxOverride("normal", edge);
            var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; content.AddThemeConstantOverride("separation", 4);
            row.AddChild(content); content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); content.OffsetLeft = 12; content.OffsetRight = -12; content.OffsetTop = 9; content.OffsetBottom = -9;
            var heading = new HBoxContainer(); content.AddChild(heading);
            var sender = Clipped(letter.SenderLabel, 18, true); sender.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(sender);
            var stamp = letter.sent_at_hour / 24; heading.AddChild(Text($"{GameTimeMgr.ZhaomingStartYear + stamp / 360}/{stamp / 30 % 12 + 1}/{stamp % 30 + 1}", 15, true));
            content.AddChild(Clipped(letter.subject, 23));
            content.AddChild(Clipped(letter.invitation is { } invite ? $"{letter.KindLabel} · {invite.State(Now)}" : letter.Preview, 17, true));
            row.Pressed += () => SelectLetter(letter.letter_id);
        }
        if (shown.Count == 0)
        {
            var hint = Text(_query.Length > 0 ? "没有匹配的信件\n可调整检索内容。" : "此处暂无信笺", 21, true, true);
            hint.HorizontalAlignment = HorizontalAlignment.Center; _rows.AddChild(hint);
        }
        _listScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scroll);
    }
    private void RefreshLetter()
    {
        var letter = _mailbox?.Find(_selected); _reading.Visible = letter is not null; _empty.Visible = letter is null;
        if (letter is null)
        {
            _empty.FindChild("EmptyTitle", true, false).Set("text", (_mailbox?.letters.Count ?? 0) == 0 ? "暂无来信" : "尺素未展");
            _empty.FindChild("EmptyBody", true, false).Set("text", (_mailbox?.letters.Count ?? 0) == 0
                ? "闭关时留下的书信与邀约，将收存于此。" : "选择左侧的一封信笺，展开阅读。信中有约，亦可查看时日。");
            return;
        }
        _title.Text = letter.subject;
        _meta.Text = $"{(letter.sender_was_known || letter.sender_npc_id.Length == 0 ? "寄信人" : "落款")}：{letter.SenderLabel}\n{LetterMailbox.Date(letter.sent_at_hour)}{(letter.delivery_place.Length > 0 ? "　·　" + letter.delivery_place : "")}";
        _body.Text = letter.body; _read.Text = letter.is_read ? "标为未读" : "标为已读";
        _invite.Visible = letter.invitation is not null;
        if (letter.invitation is not { } invitation) return;
        _inviteTitle.Text = invitation.activity_name; _inviteState.Text = invitation.State(Now);
        _inviteState.AddThemeColorOverride("font_color", invitation.response == "pending" && !invitation.CanReply(Now) ? Red : Accent);
        _invitePlace.Text = "相约地点　" + invitation.place_name;
        _inviteReply.Text = "应答截止　" + LetterMailbox.Date(invitation.reply_by_hour);
        _inviteTime.Text = "举办时段　" + LetterMailbox.Date(invitation.starts_at_hour) + " 至 " + LetterMailbox.Date(invitation.ends_at_hour);
        _accept.Visible = _decline.Visible = invitation.CanReply(Now);
        var available = ActivityAvailable?.Invoke(invitation.activity_key) == true;
        _attend.Text = available ? "赴约" : "赴约 · 待开放";
        _attend.Disabled = !available || !invitation.CanAttend(Now);
        _attend.Visible = invitation.CanReply(Now) || invitation.CanAttend(Now);
    }
}
