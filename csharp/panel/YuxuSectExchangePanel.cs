using Godot;

/// <summary>原生四列善功堂：目录横移、列内纵滚，购买仍由峰内流程原子结算。</summary>
public partial class YuxuSectExchangePanel : Control
{
    [Signal] public delegate void back_requestedEventHandler();
    [Signal] public delegate void purchase_requestedEventHandler(string item_path, int contribution_cost);
    private static readonly Vector2 DesignSize = new(1664, 936);
    private const int ColumnWidth = 360;
    private const string FramePath = "res://textures/ui/yuxu_library_20261107/exchange_frame.png";
    private const string ColumnPath = "res://textures/ui/shangong_columns_20261030/column_mountain_paper_v1.png";
    private sealed record CatalogEntry(ItemData Item, string Category, int ContributionCost);
    private readonly List<CatalogEntry> _catalog = [];
    private readonly Dictionary<string, VBoxContainer> _columnLists = [];
    private readonly Dictionary<string, Button> _rows = [];
    private Control? _canvas;
    private Font? _font;
    private ScrollContainer? _categoryScroll;
    private HBoxContainer? _categoryColumns;
    private LineEdit? _search;
    private Label? _balanceLabel, _selectionLabel, _purchaseTitle, _purchaseName, _purchaseCost, _purchaseBalance, _purchaseNote;
    private Button? _previous, _next, _exchangeButton, _purchaseConfirm, _purchaseCancel;
    private PanelContainer? _purchaseDialog;
    private ColorRect? _modalShade;
    private CatalogEntry? _selectedItem;
    private long _contributionBalance;
    private bool _purchasePending, _purchaseCompleted, _panning;
    private Character? _hoverActor;
    private Vector2 _panStart;
    private int _panScroll;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        AddToGroup("map_overlay_blocker");
        ZIndex = 20;
        _font = GD.Load<FontFile>("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf");
        foreach (var item in YuxuExchangeCatalog.LoadItems()) _catalog.Add(new(item, YuxuExchangeCatalog.Category(item), YuxuExchangeCatalog.Price(item)));
        build_panel();
        Resized += fit_canvas;
        GetWindow().FocusExited += cancel_pan;
        VisibilityChanged += () => { if (!IsVisibleInTree()) { _panning = false; HoverInfoManager.HideAll(); } };
        fit_canvas();
        Hide();
    }

    public override void _ExitTree() { if (GetWindow() is { } window) window.FocusExited -= cancel_pan; }
    private void cancel_pan() => _panning = false;

    public void set_hover_actor(Character? actor) => _hoverActor = actor;

    public void open_panel(long contributionBalance)
    {
        HoverInfoManager.HideAll();
        _selectedItem = null;
        if (_selectionLabel is not null) _selectionLabel.Text = "选择物品查看兑换信息";
        if (_exchangeButton is not null) _exchangeButton.Disabled = true;
        set_contribution_balance(contributionBalance);
        close_purchase_dialog();
        if (_search is not null) _search.Text = "";
        if (_categoryScroll is not null) _categoryScroll.ScrollHorizontal = 0;
        rebuild_rows();
        Show(); MoveToFront(); fit_canvas();
    }

    public void set_contribution_balance(long contributionBalance)
    {
        _contributionBalance = Math.Max(0L, contributionBalance);
        if (_balanceLabel is not null) _balanceLabel.Text = $"可用贡献：{_contributionBalance:N0}";
        refresh_purchase_button();
    }

    public bool handle_back()
    {
        if (_purchaseDialog?.Visible == true) close_purchase_dialog();
        else { HoverInfoManager.HideAll(); EmitSignal(SignalName.back_requested); }
        return true;
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _panning = false; handle_back(); GetViewport().SetInputAsHandled();
        }
        if (_purchaseDialog?.Visible == true) { _panning = false; return; }
        // Handle horizontal wheels before nested item lists consume their vertical wheel input.
        if (_categoryScroll is not null && input is InputEventMouseButton { Pressed: true } mouse
            && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight
            && (mouse.ShiftPressed || mouse.ButtonIndex is MouseButton.WheelLeft or MouseButton.WheelRight)
            && _categoryScroll.GetGlobalRect().HasPoint(mouse.Position))
        {
            _categoryScroll.ScrollHorizontal += mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelLeft ? -100 : 100;
            HoverInfoManager.HideAll(); refresh_arrows(); GetViewport().SetInputAsHandled(); return;
        }
        if (!_panning || _categoryScroll is null || _canvas is null) return;
        if (input is InputEventMouseMotion motion)
        {
            _categoryScroll.ScrollHorizontal = _panScroll - Mathf.RoundToInt((motion.Position.X - _panStart.X) / _canvas.Scale.X);
            HoverInfoManager.HideAll(); refresh_arrows(); GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) _panning = false;
        else if (input is InputEventScreenDrag drag)
        {
            _categoryScroll.ScrollHorizontal = _panScroll - Mathf.RoundToInt((drag.Position.X - _panStart.X) / _canvas.Scale.X);
            HoverInfoManager.HideAll(); refresh_arrows(); GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventScreenTouch { Pressed: false }) _panning = false;
    }

    private void build_panel()
    {
        var matte = new ColorRect { Color = new("d1d5cf"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(matte); matte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Name = "PavilionCanvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_canvas); _canvas.Size = DesignSize;
        place(new TextureRect { Name = "PavilionFrame", Texture = YuxuLibraryArt.Texture(FramePath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, 1664, 936);
        var hallTitle = label("善功堂", 55); hallTitle.HorizontalAlignment = HorizontalAlignment.Center;
        place(hallTitle, _canvas, 585, 78, 495, 80);
        // 纸面状态区继续显示真实余额，原四列布局保持。
        place(paper(new("f5eddf")), _canvas, 126, 204, 385, 31);
        _balanceLabel = label("可用贡献：0", 22); place(_balanceLabel, _canvas, 128, 202, 380, 34);
        _previous = button("‹", 29); place(_previous, _canvas, 1461, 199, 30, 36); _previous.Pressed += () => pan_categories(-1);
        _next = button("›", 29); place(_next, _canvas, 1493, 199, 30, 36); _next.Pressed += () => pan_categories(1);
        _search = new LineEdit { Name = "CatalogSearch", PlaceholderText = "搜索物品名称", ClearButtonEnabled = true };
        _search.AddThemeFontOverride("font", _font); _search.AddThemeFontSizeOverride("font_size", 19);
        _search.AddThemeColorOverride("font_color", new("353c30"));
        _search.AddThemeColorOverride("font_placeholder_color", YuxuLibraryArt.Muted);
        _search.AddThemeStyleboxOverride("normal", flat(new("f5eddf"))); _search.AddThemeStyleboxOverride("focus", flat(new("f5eddf"), new("a99b75")));
        place(_search, _canvas, 1242, 201, 194, 33); _search.TextChanged += _ => rebuild_rows();
        var back = button("返回", 22); PaperButtonFeedback.Apply(back, CraftingPaperStyle.ButtonKind.Regular, 22);
        _canvas.AddChild(back); CraftingPaperHeader.PlaceReturn(_canvas, back); CraftingPaperHeader.AddReturnArrow(back); back.Pressed += () => handle_back();
        _categoryScroll = new ScrollContainer { Name = "CategoryScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, ClipContents = true };
        place(_categoryScroll, _canvas, 110, 246, ColumnWidth * 4, 590);
        _categoryScroll.AddThemeStyleboxOverride("panel", flat(new("f3ecdb")));
        var rail = _categoryScroll.GetHScrollBar(); rail.Name = "CategoryRail";
        rail.AddThemeStyleboxOverride("scroll", rail_style(new("ddd5c3"), false));
        rail.AddThemeStyleboxOverride("grabber", rail_style(new("91a093"), true));
        rail.AddThemeStyleboxOverride("grabber_highlight", rail_style(new("7e9283"), true));
        rail.AddThemeStyleboxOverride("grabber_pressed", rail_style(new("687f70"), true));
        rail.ValueChanged += _ => { HoverInfoManager.HideAll(); refresh_arrows(); };
        rail.Changed += refresh_arrows;
        _categoryScroll.GuiInput += input => on_rail_input(input, _categoryScroll);
        _categoryColumns = new HBoxContainer { Name = "CategoryColumns", SizeFlagsVertical = SizeFlags.ExpandFill };
        _categoryColumns.AddThemeConstantOverride("separation", 0); _categoryScroll.AddChild(_categoryColumns);
        foreach (var category in YuxuExchangeCatalog.Categories) build_column(category);
        place(paper(new("f5eddd")), _canvas, 412, 848, 842, 44);
        _selectionLabel = label("选择物品查看兑换信息", 21); place(_selectionLabel, _canvas, 422, 847, 820, 46);
        _selectionLabel.ClipText = true; _selectionLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _exchangeButton = button("兑换", 26); _exchangeButton.AddThemeStyleboxOverride("normal", flat(new("f3e7ce"))); _exchangeButton.AddThemeStyleboxOverride("disabled", flat(new("f3e7ce")));
        place(_exchangeButton, _canvas, 1329, 850, 154, 43); _exchangeButton.Disabled = true;
        _exchangeButton.Pressed += () => { if (_selectedItem is { } entry) open_purchase_dialog(entry); };
        build_purchase_dialog(); rebuild_rows();
    }

    private void build_column(string category)
    {
        var column = new Control { Name = "Category_" + category, CustomMinimumSize = new(ColumnWidth, 574), MouseFilter = MouseFilterEnum.Pass };
        _categoryColumns!.AddChild(column);
        place(new TextureRect { Texture = GD.Load<Texture2D>(ColumnPath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }, column, 0, 0, 360, 574);
        var heading = new Control { Name = "CategoryHeading", MouseFilter = MouseFilterEnum.Stop }; place(heading, column, 0, 0, 360, 71);
        var title = label(category, 35); title.HorizontalAlignment = HorizontalAlignment.Center; place(title, heading, 0, 0, 360, 71); heading.GuiInput += input => on_rail_input(input, heading);
        var names = label("名称", 19); place(names, column, 22, 71, 205, 44);
        var stock = label("库存", 19); stock.HorizontalAlignment = HorizontalAlignment.Right; place(stock, column, 234, 71, 42, 44);
        var price = label("贡献", 19); price.HorizontalAlignment = HorizontalAlignment.Right; place(price, column, 284, 71, 48, 44);
        var scroll = new ScrollContainer { Name = "ItemScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        place(scroll, column, 14, 115, 332, 426); scroll.GetVScrollBar().ValueChanged += _ => HoverInfoManager.HideAll();
        var list = new VBoxContainer { Name = "Items", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 0); scroll.AddChild(list); _columnLists[category] = list;
    }

    private void on_rail_input(InputEvent input, Control origin)
    {
        if (_categoryScroll is null) return;
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left && mouse.Pressed) { _panning = true; _panStart = origin.GetGlobalTransformWithCanvas() * mouse.Position; _panScroll = _categoryScroll.ScrollHorizontal; }
        }
        else if (input is InputEventScreenTouch { Pressed: true } touch) { _panning = true; _panStart = origin.GetGlobalTransformWithCanvas() * touch.Position; _panScroll = _categoryScroll.ScrollHorizontal; }
    }

    private void pan_categories(int direction) { if (_categoryScroll is not null) _categoryScroll.ScrollHorizontal += direction * ColumnWidth; HoverInfoManager.HideAll(); refresh_arrows(); }
    private void refresh_arrows()
    {
        if (_categoryScroll is null || _previous is null || _next is null) return;
        var bar = _categoryScroll.GetHScrollBar();
        var overflow = bar.MaxValue - bar.Page > bar.MinValue + 1;
        _previous.Visible = _next.Visible = overflow;
        _previous.Disabled = bar.Value <= bar.MinValue + 1; _next.Disabled = bar.Value >= bar.MaxValue - bar.Page - 1;
    }

    private void rebuild_rows()
    {
        foreach (var list in _columnLists.Values) foreach (var child in list.GetChildren()) { list.RemoveChild(child); child.QueueFree(); }
        _rows.Clear(); var search = _search?.Text.Trim() ?? "";
        foreach (var category in YuxuExchangeCatalog.Categories)
        {
            var entries = _catalog.Where(entry => entry.Category == category && (search.Length == 0 || entry.Item.entity_name.Contains(search, StringComparison.CurrentCultureIgnoreCase))).ToList();
            foreach (var entry in entries) _columnLists[category].AddChild(create_item_row(entry));
            if (entries.Count == 0) { var empty = label(search.Length > 0 ? "没有匹配物品" : "暂无已配置兑换品目", 18); empty.HorizontalAlignment = HorizontalAlignment.Center; empty.CustomMinimumSize = new(0, 80); _columnLists[category].AddChild(empty); }
        }
        CallDeferred(MethodName.refresh_arrows);
    }

    private Button create_item_row(CatalogEntry entry)
    {
        var row = button("", 19); row.Name = "Item_" + entry.Item.definition_id; row.CustomMinimumSize = new(0, 42); row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddThemeStyleboxOverride("normal", row_style(false)); row.AddThemeStyleboxOverride("hover", row_style(true));
        var name = label(entry.Item.entity_name, 19); name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; place(name, row, 8, 0, 204, 42);
        var stock = label("—", 19); stock.HorizontalAlignment = HorizontalAlignment.Right; place(stock, row, 220, 0, 42, 42);
        var price = label(entry.ContributionCost.ToString(), 19); price.Name = "ContributionPrice"; price.HorizontalAlignment = HorizontalAlignment.Right; place(price, row, 270, 0, 48, 42);
        HoverInfoManager.SetProvider(row, () => StoragePanel.ItemHover(entry.Item, user: _hoverActor));
        row.Pressed += () => select_entry(entry);
        row.GuiInput += input => { if (input is InputEventMouseButton { Pressed: true, DoubleClick: true, ButtonIndex: MouseButton.Left }) { select_entry(entry); open_purchase_dialog(entry); } };
        _rows[entry.Item.definition_id.ToString()] = row; return row;
    }

    private void select_entry(CatalogEntry entry)
    {
        _selectedItem = entry;
        _selectionLabel!.Text = $"已选：{entry.Item.entity_name}　｜　数量 1　｜　所需贡献 {entry.ContributionCost}";
        _exchangeButton!.Disabled = false;
        foreach (var pair in _rows) pair.Value.AddThemeStyleboxOverride("normal", row_style(pair.Key == entry.Item.definition_id.ToString()));
    }

    private void build_purchase_dialog()
    {
        _modalShade = new ColorRect { Name = "PurchaseShade", Color = new(0.10f, 0.11f, 0.10f, .46f), MouseFilter = MouseFilterEnum.Stop, ZIndex = 40 };
        place(_modalShade, _canvas!, 0, 0, 1664, 936);
        _purchaseDialog = new PanelContainer { Name = "PurchaseConfirmation", ZIndex = 41, MouseFilter = MouseFilterEnum.Stop };
        _purchaseDialog.AddThemeStyleboxOverride("panel", flat(new("f0e6cf"), new("ac9771")));
        place(_purchaseDialog, _canvas!, 517, 278, 630, 380);
        var margin = new MarginContainer(); foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 14); margin.AddChild(content); _purchaseDialog.AddChild(margin);
        _purchaseTitle = label("确认兑换", 27); content.AddChild(_purchaseTitle);
        _purchaseName = label("", 25); content.AddChild(_purchaseName);
        _purchaseCost = label("", 21); content.AddChild(_purchaseCost);
        _purchaseBalance = label("", 19); content.AddChild(_purchaseBalance);
        _purchaseNote = label("兑换一件，成功后放入行囊；履职累计不变。", 18); _purchaseNote.AutowrapMode = TextServer.AutowrapMode.WordSmart; content.AddChild(_purchaseNote);
        var buttons = new HBoxContainer(); buttons.AddThemeConstantOverride("separation", 24); content.AddChild(buttons);
        _purchaseCancel = button("取消", 21); _purchaseCancel.CustomMinimumSize = new(180, 44); buttons.AddChild(_purchaseCancel); _purchaseCancel.Pressed += close_purchase_dialog;
        _purchaseConfirm = button("确认兑换", 21); _purchaseConfirm.CustomMinimumSize = new(200, 44); buttons.AddChild(_purchaseConfirm); _purchaseConfirm.Pressed += request_purchase;
        _modalShade.Hide(); _purchaseDialog.Hide();
    }

    private void open_purchase_dialog(CatalogEntry entry)
    {
        select_entry(entry); HoverInfoManager.HideAll(); _purchasePending = _purchaseCompleted = false;
        _purchaseTitle!.Text = "确认兑换"; _purchaseName!.Text = entry.Item.entity_name;
        _purchaseNote!.Text = "兑换一件，成功后放入行囊；履职累计不变。"; _purchaseCancel!.Text = "取消";
        refresh_purchase_button(); _modalShade!.Show(); _purchaseDialog!.Show();
    }

    private void request_purchase()
    {
        if (_purchasePending || _purchaseCompleted || _selectedItem is not { } entry || !YuxuExchangeCatalog.IsAvailable(entry.Item) || YuxuExchangeCatalog.Price(entry.Item) != entry.ContributionCost || _contributionBalance < entry.ContributionCost) return;
        _purchasePending = true; _purchaseNote!.Text = "正在核验贡献与行囊空间……"; refresh_purchase_button();
        EmitSignal(SignalName.purchase_requested, entry.Item.ResourcePath, entry.ContributionCost);
    }

    public void resolve_purchase(bool success, long currentBalance, string message)
    {
        _purchasePending = false; _purchaseCompleted = success; set_contribution_balance(currentBalance);
        if (_purchaseNote is not null) _purchaseNote.Text = message;
        if (success) { _purchaseTitle!.Text = "兑换完成"; _purchaseCancel!.Text = "完成"; }
        refresh_purchase_button();
    }

    private void close_purchase_dialog()
    {
        _purchaseDialog?.Hide(); _modalShade?.Hide(); _purchasePending = _purchaseCompleted = false;
    }

    private void refresh_purchase_button()
    {
        if (_selectedItem is not { } entry) { if (_purchaseConfirm is not null) _purchaseConfirm.Disabled = true; return; }
        if (_purchaseCost is not null) _purchaseCost.Text = $"消耗 {entry.ContributionCost} 点贡献";
        if (_purchaseBalance is not null) _purchaseBalance.Text = _contributionBalance >= entry.ContributionCost ? $"兑换后剩余 {_contributionBalance - (_purchaseCompleted ? 0 : entry.ContributionCost):N0} 点" : $"贡献不足 · 尚差 {entry.ContributionCost - _contributionBalance:N0} 点";
        if (_purchaseConfirm is not null) _purchaseConfirm.Disabled = _purchasePending || _purchaseCompleted || _contributionBalance < entry.ContributionCost;
    }

    private void fit_canvas()
    {
        _panning = false;
        if (_canvas is null) return; var size = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size;
        var scale = Mathf.Min(size.X / 1664f, size.Y / 936f); _canvas.Scale = Vector2.One * scale; _canvas.Position = (size - DesignSize * scale) * .5f;
    }
    private Label label(string value, int size) { var result = new Label { Text = value, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore }; result.AddThemeFontOverride("font", _font); result.AddThemeFontSizeOverride("font_size", size); result.AddThemeColorOverride("font_color", new("30382e")); return result; }
    private Button button(string value, int size) { var result = new Button { Text = value, MouseDefaultCursorShape = CursorShape.PointingHand }; result.AddThemeFontOverride("font", _font); result.AddThemeFontSizeOverride("font_size", size); result.AddThemeColorOverride("font_color", new("30382e")); result.AddThemeColorOverride("font_disabled_color", new("92957f")); result.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); result.AddThemeStyleboxOverride("hover", flat(new(.72f,.79f,.64f,.25f))); result.AddThemeStyleboxOverride("pressed", flat(new(.62f,.71f,.54f,.35f))); result.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty()); return result; }
    private static StyleBoxFlat row_style(bool selected) => new() { BgColor = selected ? new(.69f,.77f,.60f,.40f) : Colors.Transparent, BorderColor = new(.73f,.66f,.51f,.25f), BorderWidthBottom = 1, ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0 };
    private static StyleBoxFlat rail_style(Color color, bool thumb) => new() { BgColor = color, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6, ContentMarginLeft = thumb ? 24 : 0, ContentMarginRight = thumb ? 24 : 0 };
    private static ColorRect paper(Color color) => new() { Color = color, MouseFilter = MouseFilterEnum.Ignore };
    private static StyleBoxFlat flat(Color color, Color? border = null) => new() { BgColor = color, BorderColor = border ?? Colors.Transparent, BorderWidthLeft = border.HasValue ? 1 : 0, BorderWidthRight = border.HasValue ? 1 : 0, BorderWidthTop = border.HasValue ? 1 : 0, BorderWidthBottom = border.HasValue ? 1 : 0, ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0 };
    private static void place(Control child, Control parent, float x, float y, float width, float height) { parent.AddChild(child); child.Position = new(x,y); child.Size = new(width,height); }
}
