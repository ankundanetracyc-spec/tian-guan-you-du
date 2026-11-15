using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>善功堂目录和批量兑换。</summary>
public partial class YuxuSectExchangePanel : Control
{
    [Signal] public delegate void back_requestedEventHandler();
    [Signal] public delegate void purchase_requestedEventHandler(string item_path, int contribution_cost);
    [Signal] public delegate void batch_purchase_requestedEventHandler(GArray selections);

    private const int PageSize = 3;
    private const string FramePath = "res://textures/ui/shangong_20261115/interior_warm.png";
    private static readonly Vector2 DesignSize = new(1664, 936);
    private static readonly Color Ink = new("30382e");
    private sealed record CatalogEntry(ItemData Item, string Category, int Price);
    private sealed record RowControls(Button Row, Control Quantity, LineEdit Input);
    private readonly List<CatalogEntry> _catalog = [];
    private readonly Dictionary<string, int> _selected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RowControls> _rows = new(StringComparer.Ordinal);
    private Control _canvas = null!, _columns = null!;
    private Texture2D? _backdrop;
    private Font _font = null!;
    private LineEdit _search = null!;
    private Label _balanceLabel = null!, _totalCount = null!, _totalCost = null!, _remaining = null!, _message = null!, _pageLabel = null!;
    private Button _previous = null!, _next = null!, _confirm = null!, _cancel = null!;
    private Character? _hoverActor;
    private long _contributionBalance;
    private int _page;
    private bool _purchasePending;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        AddToGroup("map_overlay_blocker");
        ZIndex = 20;
        _font = GD.Load<FontFile>(CraftingPaperStyle.FontPath);
        _backdrop = YuxuLibraryArt.Texture(FramePath);
        foreach (var item in YuxuExchangeCatalog.LoadItems())
            _catalog.Add(new(item, YuxuExchangeCatalog.Category(item), YuxuExchangeCatalog.Price(item)));
        build_panel();
        Resized += fit_canvas;
        VisibilityChanged += () => { if (!IsVisibleInTree()) HoverInfoManager.HideAll(); };
        fit_canvas();
        Hide();
    }

    public void set_hover_actor(Character? actor) => _hoverActor = actor;

    public void open_panel(long contributionBalance)
    {
        HoverInfoManager.HideAll();
        _selected.Clear();
        _purchasePending = false;
        _message.Text = "";
        _page = 0;
        _search.Text = "";
        set_contribution_balance(contributionBalance);
        rebuild_columns();
        Show();
        MoveToFront();
        fit_canvas();
    }

    public void set_contribution_balance(long contributionBalance)
    {
        _contributionBalance = Math.Max(0L, contributionBalance);
        if (_balanceLabel is not null)
        {
            _balanceLabel.Text = $"可用贡献：{_contributionBalance:N0}";
            refresh_summary();
        }
    }

    public bool handle_back()
    {
        if (_purchasePending) return true;
        HoverInfoManager.HideAll();
        EmitSignal(SignalName.back_requested);
        return true;
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            handle_back();
            GetViewport().SetInputAsHandled();
        }
    }

    private void build_panel()
    {
        var matte = new ColorRect { Color = new("b9a18b"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(matte);
        matte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Name = "PavilionCanvas", Size = DesignSize, MouseFilter = MouseFilterEnum.Pass };
        AddChild(_canvas);
        place(new TextureRect
        {
            Name = "PavilionFrame", Texture = _backdrop, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore
        }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);

        var title = label("善功堂", 32);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        place(title, _canvas, 700, 25, 264, 67);
        var back = new Button { Name = "ExchangeReturn", Text = "返回" };
        PaperButtonFeedback.Apply(back, CraftingPaperStyle.ButtonKind.Regular, 22);
        _canvas.AddChild(back);
        CraftingPaperHeader.PlaceReturn(_canvas, back);
        CraftingPaperHeader.AddReturnArrow(back);
        back.Pressed += () => handle_back();

        _balanceLabel = label("", 23);
        place(_balanceLabel, _canvas, 105, 107, 410, 48);
        _search = new LineEdit { Name = "CatalogSearch", PlaceholderText = "搜索物品", ClearButtonEnabled = true };
        CraftingPaperStyle.ApplyInput(_search, 21);
        var searchFace = new Panel { Name = "SearchFace", MouseFilter = MouseFilterEnum.Ignore };
        if (_search.GetThemeStylebox("normal") is StyleBoxTexture source && source.Duplicate() is StyleBoxTexture warm)
        {
            warm.ModulateColor = new Color(1.08f, 1.02f, .92f);
            searchFace.AddThemeStyleboxOverride("panel", warm);
        }
        place(searchFace, _canvas, 1128, 115, 290, 30);
        var searchInput = new StyleBoxEmpty { ContentMarginLeft = 10, ContentMarginRight = 10 };
        _search.AddThemeStyleboxOverride("normal", searchInput);
        _search.AddThemeStyleboxOverride("focus", searchInput);
        _search.FocusEntered += () => searchFace.Modulate = new Color(.94f, .94f, .94f);
        _search.FocusExited += () => searchFace.Modulate = Colors.White;
        place(_search, _canvas, 1128, 108, 290, 44);
        _search.TextChanged += _ => rebuild_columns();
        _previous = text_button("◀", 21);
        _next = text_button("▶", 21);
        _previous.Name = "PreviousPage";
        _next.Name = "NextPage";
        place(_previous, _canvas, 1434, 108, 42, 44);
        _pageLabel = label("", 20);
        _pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        place(_pageLabel, _canvas, 1476, 108, 62, 44);
        place(_next, _canvas, 1538, 108, 42, 44);
        _previous.Pressed += () => turn_page(-1);
        _next.Pressed += () => turn_page(1);

        _columns = new Control { Name = "CategoryColumns", MouseFilter = MouseFilterEnum.Pass };
        place(_columns, _canvas, 82, 161, 1500, 710);

        _totalCount = label("", 21);
        _totalCost = label("", 21);
        _remaining = label("", 21);
        _message = label("", 19);
        _message.ClipText = true;
        _message.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        place(_totalCount, _canvas, 104, 879, 150, 38);
        place(_totalCost, _canvas, 256, 879, 190, 38);
        place(_remaining, _canvas, 450, 879, 300, 38);
        place(_message, _canvas, 768, 879, 450, 38);
        _cancel = text_button("取消", 22);
        _confirm = text_button("确认", 22);
        place(_confirm, _canvas, 1340, 879, 100, 38);
        place(_cancel, _canvas, 1452, 879, 100, 38);
        _cancel.Pressed += clear_selection;
        _confirm.Pressed += request_purchase;
    }

    private void turn_page(int delta)
    {
        int pageCount = (YuxuExchangeCatalog.Categories.Length + PageSize - 1) / PageSize;
        _page = Math.Clamp(_page + delta, 0, pageCount - 1);
        HoverInfoManager.HideAll();
        rebuild_columns();
    }

    private void rebuild_columns()
    {
        if (_columns is null) return;
        foreach (var child in _columns.GetChildren()) { _columns.RemoveChild(child); child.QueueFree(); }
        _rows.Clear();
        int pageCount = (YuxuExchangeCatalog.Categories.Length + PageSize - 1) / PageSize;
        _previous.Disabled = _page == 0;
        _next.Disabled = _page >= pageCount - 1;
        _pageLabel.Text = $"{_page + 1} / {pageCount}";
        string search = _search.Text.Trim();

        for (int index = 0; index < PageSize; index++)
        {
            int categoryIndex = _page * PageSize + index;
            var column = new Control { Name = "Column_" + index, MouseFilter = MouseFilterEnum.Pass };
            place(column, _columns, index * 500, 0, 500, 710);
            var paper = new TextureRect
            {
                Name = "ColumnPaper", Texture = _backdrop is null ? null : new AtlasTexture
                {
                    Atlas = _backdrop, Region = new Rect2(87, 165, 374, 705), FilterClip = true
                },
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            place(paper, column, 0, 0, 500, 710);
            if (categoryIndex >= YuxuExchangeCatalog.Categories.Length) continue;
            string category = YuxuExchangeCatalog.Categories[categoryIndex];
            var heading = new Panel { Name = "CategoryHeading", MouseFilter = MouseFilterEnum.Ignore };
            heading.AddThemeStyleboxOverride("panel", _backdrop is null ? new StyleBoxEmpty() : new StyleBoxTexture
            {
                Texture = new AtlasTexture { Atlas = _backdrop, Region = new Rect2(87, 108, 1500, 48), FilterClip = true },
                TextureMarginLeft = 24, TextureMarginTop = 10, TextureMarginRight = 24, TextureMarginBottom = 10,
                ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0
            });
            place(heading, column, 13, 8, 474, 46);
            var categoryLabel = label(category, 27);
            categoryLabel.HorizontalAlignment = HorizontalAlignment.Center;
            place(categoryLabel, heading, 0, 0, 474, 46);
            place(label("名称", 19), column, 23, 57, 175, 37);
            var selectedTitle = label("已选", 19); selectedTitle.HorizontalAlignment = HorizontalAlignment.Center;
            place(selectedTitle, column, 219, 57, 104, 37);
            var stockTitle = label("库存", 19); stockTitle.HorizontalAlignment = HorizontalAlignment.Right;
            place(stockTitle, column, 336, 57, 54, 37);
            var priceTitle = label("贡献", 19); priceTitle.HorizontalAlignment = HorizontalAlignment.Right;
            place(priceTitle, column, 402, 57, 66, 37);

            var scroll = new ScrollContainer
            {
                Name = "ItemScroll_" + category, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            place(scroll, column, 15, 94, 470, 572);
            var scrollbar = scroll.GetVScrollBar();
            scrollbar.AddThemeStyleboxOverride("scroll", PaperScrollbarStyle.Create());
            scrollbar.AddThemeStyleboxOverride("scroll_focus", PaperScrollbarStyle.Create());
            scrollbar.AddThemeStyleboxOverride("grabber", PaperScrollbarStyle.Create(grabber: true));
            scrollbar.AddThemeStyleboxOverride("grabber_highlight", PaperScrollbarStyle.Create(grabber: true, highlighted: true));
            scrollbar.AddThemeStyleboxOverride("grabber_pressed", PaperScrollbarStyle.Create(grabber: true, pressed: true));
            scrollbar.ValueChanged += _ => HoverInfoManager.HideAll();
            var list = new VBoxContainer { Name = "Items", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            list.AddThemeConstantOverride("separation", 0);
            scroll.AddChild(list);
            var entries = _catalog.Where(entry => entry.Category == category &&
                (search.Length == 0 || entry.Item.entity_name.Contains(search, StringComparison.CurrentCultureIgnoreCase))).ToList();
            foreach (var entry in entries) list.AddChild(create_item_row(entry));
            if (entries.Count == 0)
            {
                var empty = label(search.Length == 0 ? "暂无可兑换物品" : "没有匹配物品", 18);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.CustomMinimumSize = new(0, 80);
                list.AddChild(empty);
            }
        }
        refresh_summary();
    }

    private Control create_item_row(CatalogEntry entry)
    {
        string path = entry.Item.ResourcePath;
        var holder = new Control { Name = "Entry_" + entry.Item.definition_id, CustomMinimumSize = new(450, 44) };
        var row = new Button { Name = "Item_" + entry.Item.definition_id, FocusMode = FocusModeEnum.None };
        row.AddThemeStyleboxOverride("normal", row_style(_selected.ContainsKey(path)));
        row.AddThemeStyleboxOverride("hover", row_style(true));
        row.AddThemeStyleboxOverride("pressed", row_style(true, true));
        row.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        place(row, holder, 0, 0, 450, 44);
        var name = label(entry.Item.entity_name, 20);
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        place(name, row, 8, 0, 180, 44);
        var stock = label("—", 20); stock.HorizontalAlignment = HorizontalAlignment.Right;
        place(stock, row, 322, 0, 54, 44);
        var price = label(entry.Price.ToString("N0"), 20); price.HorizontalAlignment = HorizontalAlignment.Right;
        place(price, row, 384, 0, 62, 44);
        HoverInfoManager.SetProvider(row, () => StoragePanel.ItemHover(entry.Item, user: _hoverActor));

        var quantity = new Control { MouseFilter = MouseFilterEnum.Pass };
        place(quantity, holder, 193, 0, 126, 44);
        var minus = text_button("−", 22);
        var plus = text_button("+", 22);
        place(minus, quantity, 0, 0, 33, 44);
        var input = new LineEdit { Name = "SelectedQuantity", Alignment = HorizontalAlignment.Center, MaxLength = 3 };
        input.AddThemeFontOverride("font", _font);
        input.AddThemeFontSizeOverride("font_size", 22);
        input.AddThemeConstantOverride("minimum_character_width", 1);
        input.AddThemeColorOverride("font_color", Ink);
        input.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        input.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        place(input, quantity, 33, 0, 60, 44);
        place(plus, quantity, 93, 0, 33, 44);
        _rows[path] = new(row, quantity, input);
        sync_row(path, true);
        row.Pressed += () => set_quantity(entry, _selected.ContainsKey(path) ? 0 : 1, true);
        minus.Pressed += () => set_quantity(entry, _selected.GetValueOrDefault(path) - 1, true);
        plus.Pressed += () => set_quantity(entry, _selected.GetValueOrDefault(path) + 1, true);
        input.TextChanged += value =>
        {
            if (!int.TryParse(value, out int count)) return;
            count = Math.Clamp(count, 0, Character.InventoryCapacity);
            if (count.ToString().Length < value.Length || value != count.ToString() && count == Character.InventoryCapacity)
            {
                input.Text = count.ToString();
                input.CaretColumn = input.Text.Length;
            }
            set_quantity(entry, count, false);
        };
        input.FocusExited += () =>
        {
            if (!int.TryParse(input.Text, out int count)) count = 0;
            set_quantity(entry, Math.Clamp(count, 0, Character.InventoryCapacity), true);
        };
        input.TextSubmitted += _ => input.ReleaseFocus();
        return holder;
    }

    private void set_quantity(CatalogEntry entry, int count, bool syncInput)
    {
        if (_purchasePending) return;
        string path = entry.Item.ResourcePath;
        count = Math.Clamp(count, 0, Character.InventoryCapacity);
        if (count == 0) _selected.Remove(path);
        else _selected[path] = count;
        _message.Text = "";
        sync_row(path, syncInput);
        refresh_summary();
    }

    private void sync_row(string path, bool syncInput)
    {
        if (!_rows.TryGetValue(path, out var controls)) return;
        bool selected = _selected.TryGetValue(path, out int count);
        controls.Row.AddThemeStyleboxOverride("normal", row_style(selected));
        controls.Quantity.Visible = selected;
        if (syncInput) controls.Input.Text = selected ? count.ToString() : "";
    }

    private void clear_selection()
    {
        if (_purchasePending) return;
        _selected.Clear();
        _message.Text = "";
        foreach (var path in _rows.Keys) sync_row(path, true);
        refresh_summary();
    }

    private void refresh_summary()
    {
        if (_confirm is null) return;
        long count = 0, cost = 0;
        foreach (var entry in _catalog)
            if (_selected.TryGetValue(entry.Item.ResourcePath, out int quantity))
            {
                count += quantity;
                cost += (long)quantity * entry.Price;
            }
        _totalCount.Text = $"共计 {count:N0} 件";
        _totalCost.Text = $"共计 {cost:N0} 贡献";
        _remaining.Text = $"预计剩余贡献 {_contributionBalance - cost:N0}";
        _remaining.AddThemeColorOverride("font_color", cost > _contributionBalance ? new Color("945b48") : Ink);
        if (cost > _contributionBalance) _message.Text = "贡献不足";
        else if (_message.Text == "贡献不足") _message.Text = "";
        _confirm.Disabled = _purchasePending || count == 0 || cost > _contributionBalance;
        _cancel.Disabled = _purchasePending || count == 0;
    }

    private void request_purchase()
    {
        if (_purchasePending || _selected.Count == 0) return;
        var selections = new GArray();
        foreach (var entry in _catalog)
            if (_selected.TryGetValue(entry.Item.ResourcePath, out int count))
                selections.Add(new GDictionary { ["item_path"] = entry.Item.ResourcePath, ["count"] = count, ["unit_price"] = entry.Price });
        _purchasePending = true;
        refresh_summary();
        EmitSignal(SignalName.batch_purchase_requested, selections);
    }

    public void resolve_purchase(bool success, long currentBalance, string message)
    {
        _purchasePending = false;
        if (success) _selected.Clear();
        _message.Text = message;
        set_contribution_balance(currentBalance);
        foreach (var path in _rows.Keys) sync_row(path, true);
    }

    private void fit_canvas()
    {
        if (_canvas is null) return;
        Vector2 viewport = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size;
        float scale = Mathf.Min(viewport.X / DesignSize.X, viewport.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = (viewport - DesignSize * scale) * .5f;
    }

    private Label label(string value, int size)
    {
        var result = new Label { Text = value, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        result.AddThemeFontOverride("font", _font);
        result.AddThemeFontSizeOverride("font_size", size);
        result.AddThemeColorOverride("font_color", Ink);
        return result;
    }

    private Button text_button(string value, int size)
    {
        var button = new Button { Text = value, MouseDefaultCursorShape = CursorShape.PointingHand, FocusMode = FocusModeEnum.None };
        button.AddThemeFontOverride("font", _font);
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", new Color("805537"));
        button.AddThemeColorOverride("font_pressed_color", new Color("a35e35"));
        button.AddThemeColorOverride("font_disabled_color", new Color("a7a092"));
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        return button;
    }

    private static StyleBoxFlat row_style(bool selected, bool pressed = false) => new()
    {
        BgColor = selected ? new Color(0.48f, 0.35f, 0.23f, pressed ? .22f : .12f) : Colors.Transparent,
        BorderColor = new Color(.52f, .41f, .29f, .2f), BorderWidthBottom = 1,
        ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0
    };

    private static void place(Control child, Control parent, float x, float y, float width, float height)
    {
        parent.AddChild(child);
        child.Position = new(x, y);
        child.Size = new(width, height);
    }
}


