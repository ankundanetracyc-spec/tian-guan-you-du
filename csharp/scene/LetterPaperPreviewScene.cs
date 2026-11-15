using Godot;

/// <summary>Standalone visual proposal. Its mailbox is entirely in memory; no formal entry or save hook.</summary>
public partial class LetterPaperPreviewScene : Control
{
    public const int SampleHour = (2 * 30 + 11) * 24 + 10;
    private const string EnvelopePath = "res://textures/ui/qingxi/bottom_nav_letters_ink_v1.png";
    private readonly Dictionary<string, Button> _tabs = [];
    private readonly List<LetterData> _visible = [];
    private Control _canvas = null!, _sheet = null!, _empty = null!;
    private ScrollContainer _listScroll = null!, _bodyScroll = null!;
    private VBoxContainer _rows = null!, _inviteBox = null!;
    private PanelContainer _invite = null!;
    private RichTextLabel _body = null!;
    private Label _summary = null!, _matches = null!, _title = null!, _sender = null!, _stamp = null!,
        _inviteName = null!, _inviteState = null!, _place = null!, _replyDate = null!, _eventDate = null!, _emptyTitle = null!, _emptyNote = null!;
    private Button _read = null!, _accept = null!, _decline = null!, _attend = null!, _sort = null!;
    private string _section = "all", _query = "", _selected = "";
    private bool _oldestFirst, _mailboxSubscribed;
    private LetterMailbox _mailbox = new();
    private Tween? _unfold;
    public event Action? Closed;
    public int PreviewHour { get; set; } = SampleHour;
    public bool ExitOnClose { get; set; } = true;
    public LetterMailbox PreviewMailbox => _mailbox;
    public IReadOnlyList<LetterData> VisibleLetters => _visible;
    public string SelectedLetterId => _selected;
    public Control DesignCanvas => _canvas;

    private static T Put<T>(Control parent, T child, float x, float y, float width, float height) where T : Control
    { parent.AddChild(child); child.Position = new(x, y); child.Size = new(width, height); return child; }
    private static Label Text(string value, int size = 22, bool muted = false)
    {
        var label = CraftingPaperStyle.Text(value, size, muted);
        label.VerticalAlignment = VerticalAlignment.Center; return label;
    }
    private static Label Ellipsis(string value, int size = 22, bool muted = false)
    {
        var label = Text(value, size, muted); label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; return label;
    }
    private static Button ActionButton(string name, string caption, Action action, bool selected = false)
    {
        var button = new Button { Name = name, Text = caption };
        PaperButtonFeedback.Apply(button, fontSize: 22, selected: selected);
        button.Pressed += action; return button;
    }
    private static TextureRect Envelope(int size) => new()
    {
        Texture = GD.Load<Texture2D>(EnvelopePath), CustomMinimumSize = new(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };
    private static AtlasTexture Atlas(string file, Rect2 region) => new()
    { Atlas = GD.Load<Texture2D>(CraftingPaperStyle.Assets + file), Region = region, FilterClip = true };
    private static Panel Paper(Control parent, string name, Rect2 bounds, bool framed = false)
    {
        var panel = new Panel { Name = name, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(0, framed, .97f));
        return Put(parent, panel, bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y);
    }
    private static HSeparator Rule(Control parent, float x, float y, float width)
        => Put(parent, CraftingPaperStyle.Divider(), x, y, width, 2);
    private static void IgnoreChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        { if (child is Control control) control.MouseFilter = MouseFilterEnum.Ignore; IgnoreChildren(child); }
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = CraftingPaperStyle.MakeTheme(); MouseFilter = MouseFilterEnum.Stop;
        _canvas = QuietRoomStyle.Canvas(this); _canvas.Name = "LetterPaperCanvas";
        BuildHeader(); BuildMailbox(); BuildReading();
        Bind(CreateSampleMailbox());
        if (OS.GetCmdlineUserArgs().Contains("--letters-empty")) Bind(new LetterMailbox());
        Closed += () => { if (ExitOnClose) GetTree().Quit(); else Hide(); };
    }
    public override void _ExitTree()
    {
        if (_mailboxSubscribed) { _mailbox.Changed -= Refresh; _mailboxSubscribed = false; }
        _unfold?.Kill();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { Closed?.Invoke(); GetViewport().SetInputAsHandled(); }
    }
    private void BuildHeader()
    {
        var wood = new Panel { Name = "LetterDesk", MouseFilter = MouseFilterEnum.Ignore };
        wood.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        { Texture = Atlas("frame_atlas.png", new Rect2(110, 285, 850, 140)), ModulateColor = new Color(.93f, .95f, .96f) });
        Put(_canvas, wood, 0, 0, 1672, 941);
        var plaque = new PanelContainer(); var plaqueSkin = CraftingPaperStyle.Title();
        plaqueSkin.ContentMarginTop = plaqueSkin.ContentMarginBottom = 2;
        plaque.AddThemeStyleboxOverride("panel", plaqueSkin);
        Put(_canvas, plaque, 24, 18, 230, 46);
        var title = Text("信 件", 22); title.HorizontalAlignment = HorizontalAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center;
        plaque.AddChild(title);
        _summary = Text("", 22); _summary.AddThemeColorOverride("font_color", new Color("ded7c9"));
        _summary.VerticalAlignment = VerticalAlignment.Center; Put(_canvas, _summary, 286, 18, 630, 46);
        var date = Text(LetterMailbox.Date(PreviewHour), 22); date.Name = "LetterDate";
        date.AddThemeColorOverride("font_color", new Color("e7dfcf"));
        date.VerticalAlignment = VerticalAlignment.Center; date.HorizontalAlignment = HorizontalAlignment.Right;
        Put(_canvas, date, 1040, 18, 466, 46);
        var back = ActionButton("LetterReturn", "返回", () => Closed?.Invoke());
        Put(_canvas, back, 1534, 18, 114, 46);
        Put(back, new QuietRoomTriangle { Right = true, MouseFilter = MouseFilterEnum.Ignore }, 91, 20, 5, 7);
    }
    private void BuildMailbox()
    {
        var carrier = new Panel { Name = "MailboxCloth", MouseFilter = MouseFilterEnum.Ignore };
        carrier.AddThemeStyleboxOverride("panel", new StyleBoxTexture
        {
            Texture = Atlas("button_navigation.png", new Rect2(100, 150, 80, 400)),
            TextureMarginLeft = 5, TextureMarginRight = 5, TextureMarginTop = 8, TextureMarginBottom = 8,
            ModulateColor = new Color(.84f, .94f, 1.02f)
        });
        Put(_canvas, carrier, 24, 104, 524, 810);
        Paper(_canvas, "MailboxPaper", new Rect2(47, 119, 485, 778), framed: true);
        var binding = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        binding.AddThemeStyleboxOverride("panel", CraftingPaperStyle.BindingStyle());
        Put(_canvas, binding, 30, 131, 19, 754);
        Put(_canvas, Envelope(52), 67, 138, 52, 52);
        var heading = Text("信 匣", 28); heading.VerticalAlignment = VerticalAlignment.Center;
        Put(_canvas, heading, 133, 141, 164, 46);
        _matches = Text("", 19, true); _matches.HorizontalAlignment = HorizontalAlignment.Right; _matches.VerticalAlignment = VerticalAlignment.Center;
        Put(_canvas, _matches, 319, 141, 188, 46);
        var index = 0;
        foreach (var (key, caption) in new[] { ("all", "全部"), ("unread", "未读"), ("invites", "邀约") })
        {
            var tab = ActionButton("LetterTab_" + key, caption, () =>
            { _section = key; _selected = ""; _listScroll.ScrollVertical = 0; Refresh(); });
            tab.ToggleMode = true; Put(_canvas, tab, 67 + index++ * 150, 201, 138, 44); _tabs[key] = tab;
        }
        var search = new LineEdit { Name = "LetterQuery", PlaceholderText = "按署名、标题、正文检索", ClearButtonEnabled = true };
        CraftingPaperStyle.ApplyInput(search, 19);
        Put(_canvas, search, 67, 261, 282, 44);
        search.TextChanged += value => { _query = value.Trim(); _listScroll.ScrollVertical = 0; RefreshList(); };
        _sort = ActionButton("LetterSort", "新信在前 ▾", () =>
        { _oldestFirst = !_oldestFirst; _sort.Text = _oldestFirst ? "旧信在前 ▴" : "新信在前 ▾"; RefreshList(); });
        _sort.AddThemeFontSizeOverride("font_size", 18); Put(_canvas, _sort, 361, 261, 146, 44);
        Rule(_canvas, 67, 323, 440);
        _listScroll = Put(_canvas, new ScrollContainer
        {
            Name = "LetterListScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, ClipContents = true
        }, 66, 342, 443, 533);
        _rows = new VBoxContainer { Name = "LetterRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 12); _listScroll.AddChild(_rows);
    }
    private void BuildReading()
    {
        _sheet = Paper(_canvas, "LetterReadingSheet", new Rect2(578, 104, 1070, 797), framed: true);
        _sheet.MouseFilter = MouseFilterEnum.Pass;
        _title = Ellipsis("", 32); _title.Name = "LetterSubject"; Put(_sheet, _title, 38, 34, 993, 48);
        _sender = Text("", 20, true); Put(_sheet, _sender, 39, 88, 540, 36);
        _stamp = Text("", 20, true); _stamp.HorizontalAlignment = HorizontalAlignment.Right;
        Put(_sheet, _stamp, 560, 88, 470, 36);
        Rule(_sheet, 39, 136, 990);
        _bodyScroll = Put(_sheet, new ScrollContainer
        {
            Name = "LetterBodyScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, ClipContents = true
        }, 40, 156, 990, 537);
        _body = new RichTextLabel
        {
            Name = "LetterBody", FitContent = true, ScrollActive = false, BbcodeEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass
        };
        _body.AddThemeFontOverride("normal_font", GD.Load<FontFile>(CraftingPaperStyle.FontPath));
        _body.AddThemeFontSizeOverride("normal_font_size", 23); _body.AddThemeConstantOverride("line_separation", 5);
        _body.AddThemeColorOverride("default_color", CraftingPaperStyle.Ink); _bodyScroll.AddChild(_body);
        _invite = new PanelContainer { Name = "LetterInvitation" };
        _invite.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(18, brightness: .96f));
        Put(_sheet, _invite, 36, 548, 998, 156);
        _inviteBox = new VBoxContainer(); _inviteBox.AddThemeConstantOverride("separation", 8); _invite.AddChild(_inviteBox);
        var header = new HBoxContainer(); _inviteBox.AddChild(header);
        _inviteName = Ellipsis("", 25); _inviteName.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(_inviteName);
        _inviteState = Text("", 20); header.AddChild(_inviteState);
        _place = Text("", 20); _inviteBox.AddChild(_place);
        var dates = new HBoxContainer(); dates.AddThemeConstantOverride("separation", 28); _inviteBox.AddChild(dates);
        _replyDate = Text("", 18, true); _replyDate.SizeFlagsHorizontal = SizeFlags.ExpandFill; dates.AddChild(_replyDate);
        _eventDate = Text("", 18, true); _eventDate.SizeFlagsHorizontal = SizeFlags.ExpandFill; dates.AddChild(_eventDate);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12);
        Put(_sheet, actions, 39, 738, 730, 46);
        _accept = ActionButton("LetterAccept", "应邀", () => _mailbox.Reply(_selected, true, PreviewHour));
        _decline = ActionButton("LetterDecline", "婉拒", () => _mailbox.Reply(_selected, false, PreviewHour));
        _accept.CustomMinimumSize = _decline.CustomMinimumSize = new Vector2(126, 46);
        actions.AddChild(_accept); actions.AddChild(_decline);
        _attend = ActionButton("LetterAttend", "赴约 · 待开放", () => { });
        _attend.Disabled = true; _attend.CustomMinimumSize = new(204, 46); actions.AddChild(_attend);
        Rule(_sheet, 39, 723, 990);
        _read = ActionButton("LetterReadState", "标为未读", () =>
        { if (_mailbox.Find(_selected) is { } letter) _mailbox.SetRead(_selected, !letter.is_read); });
        Put(_sheet, _read, 838, 738, 192, 46);
        _empty = new Control { Name = "LetterEmpty", MouseFilter = MouseFilterEnum.Ignore };
        Put(_canvas, _empty, 578, 104, 1070, 797);
        Paper(_empty, "EmptyPaper", new Rect2(0, 0, 1070, 797), framed: true);
        var icon = Envelope(136); icon.Modulate = new Color(1, 1, 1, .65f); Put(_empty, icon, 467, 225, 136, 136);
        _emptyTitle = Text("", 30); _emptyTitle.HorizontalAlignment = HorizontalAlignment.Center; Put(_empty, _emptyTitle, 100, 388, 870, 48);
        _emptyNote = Text("", 21, true); _emptyNote.HorizontalAlignment = HorizontalAlignment.Center; Put(_empty, _emptyNote, 60, 451, 950, 70);
    }
    public void Bind(LetterMailbox mailbox)
    {
        if (_mailboxSubscribed) _mailbox.Changed -= Refresh;
        _mailbox = mailbox; _mailbox.Changed += Refresh; _mailboxSubscribed = true;
        _selected = ""; _section = "all"; _query = ""; _oldestFirst = false;
        if (_rows is not null)
        {
            _canvas.GetNode<LineEdit>("LetterQuery").Text = ""; _sort.Text = "新信在前 ▾";
            _listScroll.ScrollVertical = 0; Refresh();
        }
    }
    public void SelectLetter(string id)
    {
        if (_mailbox.Find(id) is null) return;
        _selected = id; _bodyScroll.ScrollVertical = 0;
        var letter = _mailbox.Find(id)!; bool alreadyRead = letter.is_read;
        _mailbox.SetRead(id, true); if (alreadyRead) Refresh();
        _unfold?.Kill(); _sheet.Modulate = new Color(1, 1, 1, .6f);
        _unfold = CreateTween(); _unfold.TweenProperty(_sheet, "modulate:a", 1f, .16);
    }
    private bool Matches(LetterData letter)
    {
        bool section = _section switch { "unread" => !letter.is_read, "invites" => letter.invitation is not null, _ => true };
        string text = $"{letter.SenderLabel} {letter.subject} {letter.body} {letter.invitation?.activity_name} {letter.invitation?.place_name}";
        return section && _query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
    private void Refresh()
    {
        if (_summary is null) return;
        _summary.Text = $"收存 {_mailbox.letters.Count} 封　·　未读 {_mailbox.UnreadCount} 封";
        foreach (var (key, button) in _tabs)
        {
            button.SetPressedNoSignal(_section == key);
            PaperButtonFeedback.Apply(button, selected: _section == key);
        }
        RefreshList(); RefreshReading();
    }
    private void RefreshList()
    {
        if (_rows is null) return;
        int scroll = _listScroll.ScrollVertical;
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        _visible.Clear();
        var entries = _mailbox.letters.Where(Matches);
        _visible.AddRange(_oldestFirst ? entries.OrderBy(letter => letter.sent_at_hour) : entries.OrderByDescending(letter => letter.sent_at_hour));
        _matches.Text = $"{_visible.Count} 封信笺";
        foreach (var letter in _visible)
        {
            var button = ActionButton("LetterRow_" + letter.source_event_id, "", () => SelectLetter(letter.letter_id), _selected == letter.letter_id);
            button.CustomMinimumSize = new(0, 121); button.ClipContents = true; button.SetMeta("letter_id", letter.letter_id); _rows.AddChild(button);
            var inner = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            inner.AddThemeConstantOverride("separation", 7); button.AddChild(inner); inner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            inner.OffsetLeft = 18; inner.OffsetRight = -18; inner.OffsetTop = 13; inner.OffsetBottom = -10;
            var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 10); inner.AddChild(heading);
            var sender = Ellipsis(letter.SenderLabel, 18, true); sender.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(sender);
            if (!letter.is_read) { var unread = Text("未读", 18); unread.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger); heading.AddChild(unread); }
            int day = letter.sent_at_hour / 24;
            heading.AddChild(Text($"{GameTimeMgr.ZhaomingStartYear + day / 360}/{day / 30 % 12 + 1}/{day % 30 + 1}", 18, true));
            inner.AddChild(Ellipsis(letter.subject, 24));
            inner.AddChild(Ellipsis(letter.invitation is { } invitation ? invitation.activity_name + " · " + invitation.State(PreviewHour) : letter.Preview, 17, true));
            IgnoreChildren(button);
        }
        if (_visible.Count == 0)
        {
            var hint = Text(_query.Length == 0 ? "此处暂无信笺" : "未寻得相符信笺", 22, true);
            hint.HorizontalAlignment = HorizontalAlignment.Center; hint.CustomMinimumSize = new(0, 100); _rows.AddChild(hint);
        }
        _listScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scroll);
    }
    private void RefreshReading()
    {
        var letter = _mailbox.Find(_selected); _sheet.Visible = letter is not null; _empty.Visible = letter is null;
        if (letter is null)
        {
            _emptyTitle.Text = _mailbox.letters.Count == 0 ? "暂无来信" : "尺素未展";
            _emptyNote.Text = _mailbox.letters.Count == 0 ? "闭关时留下的书信与邀约，将收存于此。" : "选择左侧的一封信笺，展开阅读。";
            return;
        }
        _title.Text = letter.subject;
        _sender.Text = (letter.sender_was_known || letter.sender_npc_id.Length == 0 ? "寄信人　" : "落款　") + letter.SenderLabel;
        _stamp.Text = LetterMailbox.Date(letter.sent_at_hour);
        _body.Text = letter.body; _read.Text = letter.is_read ? "标为未读" : "标为已读";
        _invite.Visible = letter.invitation is not null;
        _accept.Visible = _decline.Visible = _attend.Visible = false;
        _bodyScroll.Size = new Vector2(990, letter.invitation is null ? 537 : 367);
        if (letter.invitation is not { } invitation) return;
        _inviteName.Text = invitation.activity_name; _inviteState.Text = invitation.State(PreviewHour);
        _inviteState.AddThemeColorOverride("font_color", invitation.response == "pending" && !invitation.CanReply(PreviewHour) ? CraftingPaperStyle.Danger : CraftingPaperStyle.Accent);
        _place.Text = "相约地点　" + invitation.place_name;
        _replyDate.Text = "应答截止　" + invitation.ReplyByDisplay;
        _eventDate.Text = "举办日期　" + invitation.ScheduleDisplay;
        _accept.Visible = _decline.Visible = invitation.CanReply(PreviewHour);
        _attend.Visible = invitation.CanReply(PreviewHour) || invitation.CanAttend(PreviewHour);
    }
    public static LetterMailbox CreateSampleMailbox()
    {
        const int now = SampleHour; var mailbox = new LetterMailbox();
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_banquet", sender_npc_id = "preview_shen", sender_name = "沈听澜", sender_was_known = true,
            subject = "出关后，可来小聚", sent_at_hour = now - 48, delivery_place = "和光峰 · 静室",
            body = "道友：\n\n今日来访，见你仍在闭关，便把邀约留在门前。\n\n我拟在和光峰设一席清茶，请几位同门坐坐。来者有旧识，也有初来峰中的道友，正好相互见礼，聊聊近况。\n\n若你在约期前出关，且有闲暇，盼能同席。倘仍在静修，也不必为此分心。\n\n沈听澜 敬上",
            invitation = new LetterInvitationData { activity_key = "banquet", activity_name = "同门茶叙", location_id = "yuxu_xianzong_core", subscene_id = "heguang_peak", place_name = "玉虚仙宗 · 和光峰", reply_by_hour = now + 72, starts_at_hour = now + 96, ends_at_hour = now + 102 }
        });
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_expedition", sender_npc_id = "preview_chen", sender_name = "陈砚", sender_was_known = false,
            subject = "东林同行之约", sent_at_hour = now - 200, delivery_place = "静室门前",
            body = "道友：\n\n我名陈砚，近日欲约几位同门一同外出，故来问询。见道友闭门静修，便留下此信。\n\n若能在所约时日前出关，可再商议同行之事；若已过约期，便留待下回相逢。\n\n陈砚 敬上",
            invitation = new LetterInvitationData { activity_key = "party_dungeon", activity_name = "结伴外出", location_id = "east_woods", place_name = "东林地 · 林缘", reply_by_hour = now - 40, starts_at_hour = now - 16, ends_at_hour = now - 10 }
        });
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_greeting", sender_npc_id = "preview_gu", sender_name = "顾远", sender_was_known = true,
            subject = "静室门前留书", sent_at_hour = now - 72,
            body = "道友：\n\n今日前来问候，见静室仍闭，便未叩扰。留此短笺，愿道友清修顺遂。\n\n待你出关，若有闲暇，我们再叙。\n\n顾远 留书"
        });
        mailbox.Deliver(new LetterData
        {
            source_event_id = "preview_introduction", sender_npc_id = "preview_chen", sender_name = "陈砚", sender_was_known = false,
            subject = "留笺见礼", sent_at_hour = now - 24,
            body = "道友：\n\n我名陈砚，与道友同在本宗修行。今日登门，见静室闭门，便留此笺，不扰清修。\n\n若出关后有意往来，日后宗内相逢，再当面见礼。\n\n陈砚 敬上"
        });
        return mailbox;
    }
}
