using Godot;

/// <summary>玄览峰实物拓印：稳定架位、薄书脊、筛选压暗及实际人物的逐项修习条件。</summary>
public partial class YuxuManualLibraryPanel : Control
{
    public event Action? BackRequested;
    public event Action<string>? CopyRequested;
    private static readonly Vector2 DesignSize = new(1664, 936);
    private static readonly Vector2[] Slots = [new(235, 272), new(663, 272), new(554, 663), new(381, 467), new(858, 467)];
    private readonly List<YuxuManualCopy.Entry> _catalog = [];
    private readonly Dictionary<string, YuxuManualSpine> _spines = [];
    private readonly Dictionary<string, Button> _keywords = [];
    private Control _canvas = null!;
    private VBoxContainer _body = null!;
    private HBoxContainer _tags = null!;
    private Label _title = null!, _price = null!, _status = null!;
    private Button _copy = null!, _summary = null!, _chapters = null!;
    private LineEdit _search = null!;
    private CheckButton _studyOnly = null!;
    private Character? _reader;
    private YuxuManualCopy.Entry? _selected;
    private string _keyword = "全部";
    private bool _chapterView, _pending;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        AddToGroup("map_overlay_blocker"); ZIndex = 20;
        _canvas = Place(new Control { Name = "LibraryCanvas" }, this, 0, 0, 1664, 936);
        Place(new TextureRect { Name = "LibraryFrame", Texture = YuxuLibraryArt.Texture("res://textures/ui/yuxu_library_20261107/library_frame.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, 1664, 936);
        Place(Label("玄览峰 · 藏经阁", 40, HorizontalAlignment.Center), _canvas, 550, 24, 550, 62);
        var back = Place(Button("返回", 22, false), _canvas, 0, 0, CraftingPaperHeader.ReturnSize.X, CraftingPaperHeader.ReturnSize.Y);
        PaperButtonFeedback.Apply(back, CraftingPaperStyle.ButtonKind.Regular, 22);
        CraftingPaperHeader.PlaceReturn(_canvas, back); CraftingPaperHeader.AddReturnArrow(back); back.Pressed += () => BackRequested?.Invoke();
        foreach (var (text, x, selected, enabled) in new[] { ("第一层 · 云息", 98, true, true), ("第二层 · 玄基", 319, false, false), ("第三层 · 金丹", 540, false, false) })
        {
            var floor = Place(Button(text, 24, false), _canvas, x, 130, 205, 44); floor.Disabled = !enabled;
            floor.AddThemeColorOverride("font_color", selected ? YuxuLibraryArt.Ink : YuxuLibraryArt.Muted);
            floor.AddThemeColorOverride("font_disabled_color", YuxuLibraryArt.Muted);
        }
        Place(Label("⌕", 28), _canvas, 800, 133, 28, 40);
        _search = Place(new LineEdit { Name = "ManualSearch", PlaceholderText = "查找秘籍" }, _canvas, 830, 133, 210, 40);
        _search.AddThemeFontOverride("font", YuxuLibraryArt.Font()); _search.AddThemeFontSizeOverride("font_size", 23);
        _search.AddThemeColorOverride("font_color", YuxuLibraryArt.Ink); _search.AddThemeColorOverride("font_placeholder_color", YuxuLibraryArt.Muted);
        _search.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); _search.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _search.TextChanged += _ => RefreshMatches();
        Place(Label("关键词", 23), _canvas, 106, 195, 90, 38);
        _catalog.AddRange(YuxuManualCopy.Catalog());
        float keywordX = 205;
        foreach (var keyword in new[] { "全部" }.Concat(_catalog.SelectMany(e => Tags(e.Book)).Distinct()))
        {
            var b = Place(Button(keyword, 23), _canvas, keywordX, 194, 87, 39); _keywords[keyword] = b; keywordX += 95;
            b.Pressed += () => { _keyword = keyword; RefreshMatches(); };
        }
        _studyOnly = Place(new CheckButton { Text = "可修习", Name = "StudyOnly", MouseDefaultCursorShape = CursorShape.PointingHand }, _canvas, 929, 194, 126, 39);
        YuxuLibraryArt.ButtonStyle(_studyOnly, 22, false); _studyOnly.Toggled += _ => RefreshMatches();
        var manual = YuxuLibraryArt.Texture("res://textures/items/inventory_density_20261029/manual_generic_2x3.png");
        for (var i = 0; i < _catalog.Count; i++)
        {
            var entry = _catalog[i]; var spine = new YuxuManualSpine { Name = "Manual_" + i };
            _canvas.AddChild(spine); spine.Position = Slots[i % Slots.Length]; spine.Configure(entry.Book.gongfa_name, manual);
            spine.SetMeta("book_path", entry.Book.ResourcePath); _spines[entry.Book.ResourcePath] = spine;
            spine.Pressed += () => Select(entry);
        }
        _title = Place(Label("", 30, HorizontalAlignment.Center), _canvas, 1132, 193, 460, 63);
        _tags = Place(new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin }, _canvas, 1151, 261, 422, 43);
        _tags.AddThemeConstantOverride("separation", 12);
        _summary = Place(Button("简介", 24), _canvas, 1142, 318, 125, 39);
        _chapters = Place(Button("卷章", 24), _canvas, 1267, 318, 125, 39);
        _summary.Pressed += () => { _chapterView = false; RefreshDetail(); };
        _chapters.Pressed += () => { _chapterView = true; RefreshDetail(); };
        var scroll = Place(new ScrollContainer { Name = "ManualDetailScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, _canvas, 1154, 369, 420, 365);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _body.AddThemeConstantOverride("separation", 10); scroll.AddChild(_body);
        _status = Place(Label("", 19), _canvas, 1154, 735, 418, 38); _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.AddThemeColorOverride("font_color", YuxuLibraryArt.Red);
        _price = Place(Label("首本免费", 28), _canvas, 1167, 796, 180, 63);
        _copy = Place(Button("拓印", 34), _canvas, 1379, 789, 182, 63); _copy.Name = "CopyManual";
        _copy.Pressed += () => { if (!_pending && _selected is not null) CopyRequested?.Invoke(_selected.Book.ResourcePath); };
        Resized += FitCanvas; FitCanvas(); Hide();
    }

    public void Open(Character reader)
    {
        _reader = reader; _pending = false; _keyword = "全部"; _search.Text = ""; _studyOnly.ButtonPressed = false;
        _selected ??= _catalog.FirstOrDefault(e => e.Book.gongfa_name == "太和感物养元宝册") ?? _catalog.FirstOrDefault();
        _status.Text = ""; Refresh(); Show(); MoveToFront(); FitCanvas();
    }
    public void Refresh() { RefreshMatches(); RefreshDetail(); }
    public void SetPending(bool pending) { _pending = pending; RefreshDetail(); }
    public void ShowStatus(string text) => _status.Text = text;
    private void Select(YuxuManualCopy.Entry entry) { _selected = entry; _status.Text = ""; RefreshDetail(); }
    private static IEnumerable<string> Tags(GongfaResource book) => new[] { book.get_heart_method_skill(true), book.get_heart_method_skill(false) }
        .Where(s => s is not null).SelectMany(s => s!.get_presentation_tags()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct();
    private void RefreshMatches()
    {
        foreach (var (keyword, button) in _keywords) button.AddThemeStyleboxOverride("normal", YuxuLibraryArt.Paper(keyword == _keyword));
        foreach (var entry in _catalog)
        {
            var first = entry.Book.get_sorted_chapters().FirstOrDefault();
            var study = _reader is not null && first is not null && (_reader.is_gongfa_chapter_learned(entry.Book, first.get_id()) || _reader.can_learn_gongfa_chapter(entry.Book, first));
            _spines[entry.Book.ResourcePath].SetMatched((_keyword == "全部" || Tags(entry.Book).Contains(_keyword)) &&
                entry.Book.gongfa_name.Contains(_search.Text.Trim(), StringComparison.OrdinalIgnoreCase) && (!_studyOnly.ButtonPressed || study));
        }
    }
    private void RefreshDetail()
    {
        if (_reader is null || _selected is null) return;
        foreach (var (path, spine) in _spines) spine.SetSelected(path == _selected.Book.ResourcePath);
        var book = _selected.Book; _title.Text = book.gongfa_name;
        Clear(_tags); foreach (var tag in Tags(book)) { var label = Label(tag, 22, HorizontalAlignment.Center); label.CustomMinimumSize = new(76, 39); _tags.AddChild(label); }
        _summary.AddThemeStyleboxOverride("normal", YuxuLibraryArt.Paper(!_chapterView));
        _chapters.AddThemeStyleboxOverride("normal", YuxuLibraryArt.Paper(_chapterView));
        Clear(_body);
        if (_chapterView)
        {
            foreach (var chapter in book.get_sorted_chapters())
            {
                Section($"第{chapter.order}卷 · {chapter.chapter_name}");
                Requirements(book, chapter, true);
            }
        }
        else
        {
            Section("修习境界");
            Paragraph(string.IsNullOrEmpty(book.main_realm_start) ? "依各卷要求" : book.main_realm_start + (book.main_realm_end.Length > 0 ? " 至 " + book.main_realm_end : ""));
            Section("修习要求（入门）"); Requirements(book, book.get_sorted_chapters().FirstOrDefault(), false);
            Section("简介"); Paragraph(book.description);
        }
        if (_reader.entity_data is HumanData human) _price.Text = YuxuManualCopy.Cost(human) == 0 ? "首本免费" : $"{YuxuManualCopy.Cost(human)} 贡献";
        _copy.Disabled = _pending;
    }
    private void Section(string text) { var label = Label(text, 22); label.CustomMinimumSize = new(0, 33); _body.AddChild(label); }
    private void Paragraph(string text)
    {
        var label = Label(text, 22); label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = SizeFlags.ExpandFill; _body.AddChild(label);
    }
    private void Requirements(GongfaResource book, GongfaChapterConfig? chapter, bool includeRealm)
    {
        foreach (var condition in YuxuManualRequirements.For(_reader!, book, chapter, includeRealm))
        {
            var row = new HBoxContainer { CustomMinimumSize = new(0, 33) }; row.AddThemeConstantOverride("separation", 7); _body.AddChild(row);
            var mark = Label(condition.Met ? "✓" : "✕", 21); mark.AddThemeFontOverride("font", new SystemFont { FontNames = ["Segoe UI Symbol"] });
            mark.CustomMinimumSize = new(23, 30); mark.AddThemeColorOverride("font_color", condition.Met ? YuxuLibraryArt.Green : YuxuLibraryArt.Red); row.AddChild(mark);
            var text = Label(condition.Text, 22); text.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(text);
            if (condition.Current.Length > 0) { var current = Label(condition.Current, 19); current.AddThemeColorOverride("font_color", condition.Met ? YuxuLibraryArt.Muted : YuxuLibraryArt.Red); row.AddChild(current); }
        }
    }
    private static void Clear(Node parent) { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private static Label Label(string text, int size, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", YuxuLibraryArt.Font()); label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", YuxuLibraryArt.Ink); return label;
    }
    private static Button Button(string text, int size, bool paper = true)
    {
        var button = new Button { Text = text, MouseDefaultCursorShape = CursorShape.PointingHand }; YuxuLibraryArt.ButtonStyle(button, size, paper); return button;
    }
    private static T Place<T>(T child, Control parent, float x, float y, float width, float height) where T : Control
    { parent.AddChild(child); child.Position = new(x, y); child.Size = new(width, height); return child; }
    private void FitCanvas()
    {
        var size = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size; var scale = Math.Min(size.X / DesignSize.X, size.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * scale; _canvas.Position = (size - DesignSize * scale) * .5f;
    }
    public override void _Input(InputEvent input)
    {
        if (IsVisibleInTree() && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { BackRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
    }
}
