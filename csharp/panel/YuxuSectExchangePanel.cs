using Godot;

/// <summary>善功堂可交互目录面板。只读取现行、带有效图标的材料与秘籍，使用标准基础价。</summary>
public partial class YuxuSectExchangePanel : Control
{
    [Signal] public delegate void back_requestedEventHandler();
    [Signal] public delegate void purchase_requestedEventHandler(string item_path, int contribution_cost);

    private const string FrameTexturePath = "res://textures/ui/yuxu_shangongtang_frame_v1.png";
    private const string UiFontPath = "res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf";
    private static readonly string[] ItemDirectories =
    [
        "res://resources/entities/items",
        "res://resources/entities/equipment",
    ];

    private sealed record CatalogEntry(ItemData Item, string Category, string FilterKey, string FilterLabel, string Description, int ContributionCost);

    private readonly List<CatalogEntry> _catalog = [];
    private readonly Dictionary<string, Button> _categoryButtons = [];
    private readonly Dictionary<string, Button> _levelButtons = [];
    private VBoxContainer? _levelFilterList;
    private Label? _levelFilterHeader;
    private Font? _font;
    private long _contributionBalance;
    private string _selectedCategory = "全部";
    private string _selectedLevel = "全部";
    private CatalogEntry? _selectedItem;
    private Label? _balanceLabel;
    private GridContainer? _itemGrid;
    private ColorRect? _modalShade;
    private PanelContainer? _purchaseDialog;
    private Label? _purchaseTitle;
    private Label? _purchaseName;
    private Label? _purchaseMeta;
    private TextureRect? _purchaseIcon;
    private Label? _purchaseCost;
    private Label? _purchaseBalance;
    private Label? _purchaseNote;
    private Button? _purchaseConfirm;
    private Button? _purchaseCancel;
    private bool _purchasePending;
    private bool _purchaseCompleted;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _font = GD.Load<FontFile>(UiFontPath);
        load_catalog();
        build_panel();
        VisibilityChanged += () => { if (!IsVisibleInTree()) HoverInfoManager.HideAll(); };
        Visible = false;
    }

    public void open_panel(long contributionBalance)
    {
        HoverInfoManager.HideAll();
        set_contribution_balance(contributionBalance);
        _selectedCategory = "全部";
        _selectedLevel = "全部";
        rebuild_level_filters();
        close_purchase_dialog();
        refresh_filters();
        rebuild_item_grid();
        Show();
        MoveToFront();
    }

    public void set_contribution_balance(long contributionBalance)
    {
        _contributionBalance = Math.Max(0L, contributionBalance);
        if (_balanceLabel is not null)
            _balanceLabel.Text = $"贡献值：{_contributionBalance:N0}";
        if (_purchaseBalance is not null)
            refresh_purchase_summary();
        refresh_purchase_button();
    }

    /// <summary>先关闭购买确认；再次返回时由峰内流程关闭善功堂。</summary>
    public bool handle_back()
    {
        if (_purchaseDialog?.Visible == true)
        {
            close_purchase_dialog();
            return true;
        }

        EmitSignal(SignalName.back_requested);
        return true;
    }

    private void load_catalog()
    {
        _catalog.Clear();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemDirectory in ItemDirectories)
        {
            var directory = DirAccess.Open(itemDirectory);
            if (directory is null)
            {
                GD.PushWarning($"善功堂无法读取物品目录：{itemDirectory}");
                continue;
            }

            var files = directory.GetFiles().Where(file => file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)).ToArray();
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var path = $"{itemDirectory}/{file}";
                var item = GD.Load<ItemData>(path);
                if (!YuxuSectPrototypeBalance.is_exchangeable_item(item) || item is null)
                    continue;

                var itemId = item.definition_id.ToString();
                if (!seenIds.Add(itemId))
                {
                    GD.PushWarning($"善功堂略过重复物品 ID：{itemId}（{path}）");
                    continue;
                }

                var category = item.item_category == "秘籍"
                    ? YuxuSectPrototypeBalance.manual_category(item)
                    : item.item_category;
                var material = item.craft_data as MaterialCraftData;
                var gongfa = item.gongfa as GongfaResource;
                var filterKey = item.item_category == "秘籍"
                    ? get_realm_range(gongfa)
                    : (material?.material_grade_value() ?? item.grade_value).ToString();
                var filterLabel = item.item_category == "秘籍"
                    ? filterKey
                    : material?.material_grade_name() ?? item.get_grade_string();
                var description = item.item_category == "秘籍"
                    ? build_manual_description(gongfa)
                    : build_material_description(item, material);
                var cost = YuxuSectPrototypeBalance.exchange_base_cost(item);
                if (cost <= 0)
                    continue;

                _catalog.Add(new CatalogEntry(item, category, filterKey, filterLabel, description, cost));
            }
        }

        _catalog.Sort((left, right) =>
        {
            var categoryOrder = StringComparer.Ordinal.Compare(left.Category, right.Category);
            return categoryOrder != 0 ? categoryOrder : StringComparer.Ordinal.Compare(left.Item.entity_name, right.Item.entity_name);
        });
    }

    private void build_panel()
    {
        var sourceFrame = GD.Load<Texture2D>(FrameTexturePath);

        var frame = new TextureRect
        {
            Name = "PavilionFrame",
            Texture = sourceFrame,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        add_full_rect(frame);

        var title = make_label("善功堂", 28, new Color("29271f"));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        set_anchors(title, 0.40f, 0.085f, 0.60f, 0.165f);

        var back = make_button("返回", 17);
        set_anchors(back, 0.083f, 0.208f, 0.173f, 0.252f);
        back.Pressed += () => handle_back();

        _balanceLabel = make_label("贡献值：0", 20, new Color("332e24"));
        _balanceLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _balanceLabel.VerticalAlignment = VerticalAlignment.Center;
        set_anchors(_balanceLabel, 0.800f, 0.208f, 0.926f, 0.252f);

        build_filter_rail();
        build_catalog();
        build_purchase_dialog();
        refresh_filters();
        rebuild_item_grid();
    }

    private void build_filter_rail()
    {
        var rail = new VBoxContainer
        {
            Name = "CategoryRail",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        rail.AddThemeConstantOverride("separation", 5);
        set_anchors(rail, 0.065f, 0.255f, 0.19f, 0.90f);

        var categoryHeader = make_label("物品分类", 21, new Color("373327"));
        categoryHeader.HorizontalAlignment = HorizontalAlignment.Center;
        categoryHeader.CustomMinimumSize = new Vector2(0, 32);
        rail.AddChild(categoryHeader);

        var categories = _catalog.Select(entry => entry.Category).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        add_filter_button(rail, _categoryButtons, "全部", "全部", isLevelFilter: false);
        foreach (var category in new[] { "材料", "凡俗武学", "剑修", "云息秘籍", "其他" })
            if (categories.Contains(category))
                add_filter_button(rail, _categoryButtons, category, category, isLevelFilter: false);

        var separator = new HSeparator { CustomMinimumSize = new Vector2(0, 10), MouseFilter = MouseFilterEnum.Ignore };
        rail.AddChild(separator);

        _levelFilterHeader = make_label("品阶 / 修习范围", 18, new Color("373327"));
        _levelFilterHeader.HorizontalAlignment = HorizontalAlignment.Center;
        _levelFilterHeader.CustomMinimumSize = new Vector2(0, 32);
        rail.AddChild(_levelFilterHeader);
        _levelFilterList = new VBoxContainer { Name = "LevelFilters", MouseFilter = MouseFilterEnum.Ignore };
        _levelFilterList.AddThemeConstantOverride("separation", 5);
        rail.AddChild(_levelFilterList);
        rebuild_level_filters();
    }

    private void build_catalog()
    {
        var scroll = new ScrollContainer
        {
            Name = "ExchangeCatalog",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop,
        };
        set_anchors(scroll, 0.205f, 0.255f, 0.932f, 0.90f);
        scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        _itemGrid = new GridContainer
        {
            Name = "ItemGrid",
            Columns = 8,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _itemGrid.AddThemeConstantOverride("h_separation", 8);
        _itemGrid.AddThemeConstantOverride("v_separation", 10);
        scroll.AddChild(_itemGrid);
    }

    private void build_purchase_dialog()
    {
        _modalShade = new ColorRect
        {
            Name = "PurchaseShade",
            Color = new Color(0.10f, 0.11f, 0.10f, 0.46f),
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 40,
        };
        add_full_rect(_modalShade);
        _modalShade.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                close_purchase_dialog();
                _modalShade.AcceptEvent();
            }
        };

        _purchaseDialog = new PanelContainer
        {
            Name = "PurchaseConfirmation",
            CustomMinimumSize = new Vector2(610, 360),
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -305,
            OffsetTop = -180,
            OffsetRight = 305,
            OffsetBottom = 180,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 41,
        };
        _purchaseDialog.AddThemeStyleboxOverride("panel", paper_style(0.99f, 0.94f));

        var dialogMargin = new MarginContainer();
        dialogMargin.AddThemeConstantOverride("margin_left", 30);
        dialogMargin.AddThemeConstantOverride("margin_right", 30);
        dialogMargin.AddThemeConstantOverride("margin_top", 18);
        dialogMargin.AddThemeConstantOverride("margin_bottom", 18);
        var dialogContent = new VBoxContainer();
        dialogContent.AddThemeConstantOverride("separation", 8);
        dialogMargin.AddChild(dialogContent);
        _purchaseTitle = make_label("确认兑换", 26, new Color("302b22"));
        _purchaseTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _purchaseTitle.CustomMinimumSize = new Vector2(0, 36);

        var titleRule = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 1),
            Color = new Color("887b61", 0.55f),
            MouseFilter = MouseFilterEnum.Ignore,
        };

        var itemSummary = new HBoxContainer { Name = "PurchaseItemSummary" };
        itemSummary.AddThemeConstantOverride("separation", 18);
        itemSummary.CustomMinimumSize = new Vector2(0, 78);
        _purchaseIcon = new TextureRect
        {
            Name = "SelectedItemIcon",
            CustomMinimumSize = new Vector2(72, 78),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var itemText = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        itemText.AddThemeConstantOverride("separation", 5);
        _purchaseName = make_label("", 23, new Color("302b22"));
        _purchaseName.Name = "SelectedItemName";
        _purchaseName.HorizontalAlignment = HorizontalAlignment.Left;
        _purchaseMeta = make_label("", 16, new Color("76674d"));
        _purchaseMeta.HorizontalAlignment = HorizontalAlignment.Left;
        itemText.AddChild(_purchaseName);
        itemText.AddChild(_purchaseMeta);
        itemSummary.AddChild(_purchaseIcon);
        itemSummary.AddChild(itemText);

        var summaryRule = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 1),
            Color = new Color("887b61", 0.38f),
            MouseFilter = MouseFilterEnum.Ignore,
        };

        var priceSummary = new PanelContainer { Name = "ContributionSummary", CustomMinimumSize = new Vector2(0, 52) };
        priceSummary.AddThemeStyleboxOverride("panel", exchange_summary_style());
        var priceMargin = new MarginContainer();
        priceMargin.AddThemeConstantOverride("margin_left", 12);
        priceMargin.AddThemeConstantOverride("margin_right", 12);
        priceMargin.AddThemeConstantOverride("margin_top", 5);
        priceMargin.AddThemeConstantOverride("margin_bottom", 5);
        var priceRow = new HBoxContainer();
        priceRow.AddThemeConstantOverride("separation", 12);
        _purchaseCost = make_label("消耗 0 点贡献", 20, new Color("655437"));
        _purchaseCost.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _purchaseBalance = make_label("兑换后剩余 0 点", 17, new Color("514b3d"));
        _purchaseBalance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        priceRow.AddChild(_purchaseCost);
        var priceDivider = new ColorRect
        {
            CustomMinimumSize = new Vector2(1, 28),
            Color = new Color("887b61", 0.40f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        priceRow.AddChild(priceDivider);
        priceRow.AddChild(_purchaseBalance);
        priceMargin.AddChild(priceRow);
        priceSummary.AddChild(priceMargin);

        _purchaseNote = make_label("兑换一件，成功后放入行囊；履职累计不变。", 15, new Color("76674d"));
        _purchaseNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _purchaseNote.CustomMinimumSize = new Vector2(0, 26);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 18);
        _purchaseCancel = make_button("取消", 17);
        _purchaseCancel.CustomMinimumSize = new Vector2(138, 44);
        _purchaseCancel.Pressed += close_purchase_dialog;
        _purchaseConfirm = make_button("确认兑换", 17);
        _purchaseConfirm.CustomMinimumSize = new Vector2(160, 44);
        _purchaseConfirm.Pressed += request_purchase;
        buttons.AddChild(_purchaseCancel);
        buttons.AddChild(_purchaseConfirm);

        dialogContent.AddChild(_purchaseTitle);
        dialogContent.AddChild(titleRule);
        dialogContent.AddChild(itemSummary);
        dialogContent.AddChild(summaryRule);
        dialogContent.AddChild(priceSummary);
        dialogContent.AddChild(_purchaseNote);
        dialogContent.AddChild(buttons);
        _purchaseDialog.AddChild(dialogMargin);
        AddChild(_purchaseDialog);
        _modalShade.Hide();
        _purchaseDialog.Hide();
    }

    private void add_filter_button(VBoxContainer parent, Dictionary<string, Button> store, string filterValue, string text, bool isLevelFilter)
    {
        var fontSize = text.Length > 12 ? 12 : text.Length > 7 ? 14 : 17;
        var button = make_button(text, fontSize);
        button.CustomMinimumSize = new Vector2(0, text.Length > 12 ? 38 : text.Length > 7 ? 36 : 34);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Pressed += () =>
        {
            if (isLevelFilter)
                _selectedLevel = filterValue;
            else
            {
                _selectedCategory = filterValue;
                _selectedLevel = "全部";
                rebuild_level_filters();
            }
            refresh_filters();
            rebuild_item_grid();
        };
        parent.AddChild(button);
        store[filterValue] = button;
    }

    private void rebuild_level_filters()
    {
        if (_levelFilterList is null) return;
        foreach (var child in _levelFilterList.GetChildren())
        {
            _levelFilterList.RemoveChild(child);
            child.QueueFree();
        }
        _levelButtons.Clear();

        if (_levelFilterHeader is not null)
            _levelFilterHeader.Text = _selectedCategory switch
            {
                "材料" => "材料品阶",
                "全部" => "品阶 / 修习范围",
                _ => "修习范围"
            };

        add_filter_button(_levelFilterList, _levelButtons, "全部", "全部", isLevelFilter: true);
        var entries = _catalog.Where(entry => _selectedCategory == "全部" || entry.Category == _selectedCategory);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = _selectedCategory == "全部" ? $"{entry.Category}::{entry.FilterKey}" : entry.FilterKey;
            var label = _selectedCategory == "全部" ? $"{short_category_label(entry.Category)} · {entry.FilterLabel}" : entry.FilterLabel;
            options.TryAdd(key, label);
        }
        foreach (var option in options.OrderBy(pair => pair.Value, StringComparer.Ordinal))
            add_filter_button(_levelFilterList, _levelButtons, option.Key, option.Value, isLevelFilter: true);

        if (!_levelButtons.ContainsKey(_selectedLevel))
            _selectedLevel = "全部";
        refresh_filters();
    }

    private void refresh_filters()
    {
        foreach (var pair in _categoryButtons)
            apply_filter_style(pair.Value, pair.Key == _selectedCategory);
        foreach (var pair in _levelButtons)
            apply_filter_style(pair.Value, pair.Key == _selectedLevel);
    }

    private void rebuild_item_grid()
    {
        if (_itemGrid is null)
            return;

        foreach (var child in _itemGrid.GetChildren())
        {
            _itemGrid.RemoveChild(child);
            child.QueueFree();
        }

        var visibleItems = _catalog.Where(entry =>
        {
            if (_selectedCategory != "全部" && entry.Category != _selectedCategory) return false;
            if (_selectedLevel == "全部") return true;
            var levelKey = _selectedCategory == "全部" ? $"{entry.Category}::{entry.FilterKey}" : entry.FilterKey;
            return levelKey == _selectedLevel;
        });

        foreach (var entry in visibleItems)
            _itemGrid.AddChild(create_item_card(entry));
    }

    private Control create_item_card(CatalogEntry entry)
    {
        var cell = new VBoxContainer
        {
            Name = $"ItemCell_{entry.Item.definition_id}",
            CustomMinimumSize = new Vector2(144, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        cell.AddThemeConstantOverride("separation", 4);

        var card = new Button
        {
            Name = $"Item_{entry.Item.definition_id}",
            CustomMinimumSize = new Vector2(144, 132),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None,
        };
        card.AddThemeStyleboxOverride("normal", card_style(false, false));
        card.AddThemeStyleboxOverride("hover", card_style(true, false));
        card.AddThemeStyleboxOverride("pressed", card_style(true, true));
        card.Pressed += () => open_purchase_dialog(entry);

        var iconCenter = new CenterContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        iconCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        iconCenter.OffsetLeft = 5;
        iconCenter.OffsetTop = 5;
        iconCenter.OffsetRight = -5;
        iconCenter.OffsetBottom = -5;

        var iconPath = entry.Item.icon_path;
        var icon = !string.IsNullOrWhiteSpace(iconPath) && ResourceLoader.Exists(iconPath)
            ? ResourceLoader.Load<Texture2D>(iconPath)
            : null;
        if (icon is not null)
        {
            var texture = new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(58, 70),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            iconCenter.AddChild(texture);
        }
        HoverInfoManager.SetInfo(card, new HoverInfoManager.HoverInfoContent(
            entry.Item.entity_name,
            entry.Description,
            icon,
            format_exchange_cost(entry.ContributionCost)));
        card.AddChild(iconCenter);

        var name = make_label(entry.Item.entity_name, 19, new Color("302b22"));
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.CustomMinimumSize = new Vector2(0, 48);
        name.MouseFilter = MouseFilterEnum.Ignore;

        cell.AddChild(card);
        cell.AddChild(name);
        return cell;
    }

    private void open_purchase_dialog(CatalogEntry entry)
    {
        _selectedItem = entry;
        _purchaseCompleted = false;
        HoverInfoManager.HideAll();
        if (_purchaseName is not null) _purchaseName.Text = entry.Item.entity_name;
        if (_purchaseMeta is not null)
            _purchaseMeta.Text = string.IsNullOrWhiteSpace(entry.FilterLabel) ? entry.Category : $"{entry.Category} · {entry.FilterLabel}";
        if (_purchaseIcon is not null)
        {
            var iconPath = entry.Item.icon_path;
            _purchaseIcon.Texture = !string.IsNullOrWhiteSpace(iconPath) && ResourceLoader.Exists(iconPath)
                ? ResourceLoader.Load<Texture2D>(iconPath)
                : null;
        }
        if (_purchaseTitle is not null) _purchaseTitle.Text = "确认兑换";
        if (_purchaseNote is not null) _purchaseNote.Text = "兑换一件，成功后放入行囊；履职累计不变。";
        if (_purchaseCancel is not null) _purchaseCancel.Text = "取消";
        _purchasePending = false;
        set_contribution_balance(_contributionBalance);
        _modalShade?.Show();
        _purchaseDialog?.Show();
        _purchaseDialog?.MoveToFront();
    }

    private void request_purchase()
    {
        if (_purchasePending || _selectedItem is null || _selectedItem.ContributionCost <= 0 ||
            _contributionBalance < _selectedItem.ContributionCost)
            return;

        _purchasePending = true;
        if (_purchaseNote is not null) _purchaseNote.Text = "正在核验贡献与行囊空间……";
        refresh_purchase_button();
        EmitSignal(SignalName.purchase_requested, _selectedItem.Item.ResourcePath, _selectedItem.ContributionCost);
    }

    public void resolve_purchase(bool success, long currentBalance, string message)
    {
        set_contribution_balance(currentBalance);
        _purchasePending = false;
        if (success)
        {
            _purchaseCompleted = true;
            if (_purchaseTitle is not null) _purchaseTitle.Text = "兑换完成";
            if (_purchaseNote is not null) _purchaseNote.Text = message;
            if (_purchaseCancel is not null) _purchaseCancel.Text = "完成";
            if (_purchaseConfirm is not null) _purchaseConfirm.Disabled = true;
            refresh_purchase_summary();
            return;
        }

        if (_purchaseNote is not null) _purchaseNote.Text = message;
        refresh_purchase_button();
    }

    private void close_purchase_dialog()
    {
        _purchaseDialog?.Hide();
        _modalShade?.Hide();
        _selectedItem = null;
        _purchasePending = false;
        _purchaseCompleted = false;
        if (_purchaseCancel is not null) _purchaseCancel.Text = "取消";
    }

    private void refresh_purchase_summary()
    {
        if (_selectedItem is null) return;
        var cost = _selectedItem.ContributionCost;
        if (_purchaseCost is not null)
            _purchaseCost.Text = $"消耗 {cost:N0} 点贡献";

        if (_purchaseBalance is null) return;
        if (_purchaseCompleted)
        {
            _purchaseBalance.Text = $"兑换后剩余 {_contributionBalance:N0} 点";
            _purchaseBalance.AddThemeColorOverride("font_color", new Color("514b3d"));
        }
        else if (_contributionBalance >= cost)
        {
            _purchaseBalance.Text = $"兑换后剩余 {(_contributionBalance - cost):N0} 点";
            _purchaseBalance.AddThemeColorOverride("font_color", new Color("514b3d"));
        }
        else
        {
            _purchaseBalance.Text = $"贡献不足 · 尚差 {(cost - _contributionBalance):N0} 点";
            _purchaseBalance.AddThemeColorOverride("font_color", new Color("8a4d3d"));
        }
    }

    private static string format_exchange_cost(int cost)
        => cost > 0 ? $"所需贡献：{cost}（标准价）" : "所需贡献：暂未定";

    private void refresh_purchase_button()
    {
        refresh_purchase_summary();
        if (_purchaseConfirm is null) return;
        var cost = _selectedItem?.ContributionCost ?? 0;
        _purchaseConfirm.Disabled = _purchasePending || cost <= 0 || _contributionBalance < cost;
    }

    private void add_full_rect(Control control)
    {
        control.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(control);
    }

    private void set_anchors(Control control, float left, float top, float right, float bottom)
    {
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
        AddChild(control);
    }

    private Label make_label(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        if (_font is not null)
            label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private Button make_button(string text, int size)
    {
        var button = new Button
        {
            Text = text,
            Alignment = HorizontalAlignment.Center,
            FocusMode = FocusModeEnum.None,
        };
        if (_font is not null)
            button.AddThemeFontOverride("font", _font);
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", new Color("342f25"));
        button.AddThemeColorOverride("font_hover_color", new Color("241f18"));
        button.AddThemeColorOverride("font_disabled_color", new Color("8a806e"));
        button.AddThemeStyleboxOverride("normal", filter_style(false));
        button.AddThemeStyleboxOverride("hover", filter_style(true));
        button.AddThemeStyleboxOverride("pressed", filter_style(true));
        button.AddThemeStyleboxOverride("disabled", filter_style(false, true));
        return button;
    }

    private static void apply_filter_style(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride("normal", filter_style(selected));
        button.AddThemeStyleboxOverride("hover", filter_style(true));
        button.AddThemeStyleboxOverride("pressed", filter_style(true));
    }

    private static StyleBoxFlat filter_style(bool selected, bool disabled = false) => new()
    {
        BgColor = disabled ? new Color(0.72f, 0.68f, 0.58f, 0.24f) : selected ? new Color(0.61f, 0.66f, 0.59f, 0.54f) : new Color(0.91f, 0.88f, 0.80f, 0.18f),
        BorderColor = selected ? new Color(0.31f, 0.39f, 0.34f, 0.64f) : new Color(0.40f, 0.36f, 0.29f, 0.28f),
        BorderWidthLeft = selected ? 2 : 1,
        BorderWidthTop = selected ? 2 : 1,
        BorderWidthRight = selected ? 2 : 1,
        BorderWidthBottom = selected ? 2 : 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 6,
        ContentMarginRight = 6,
        ContentMarginTop = 4,
        ContentMarginBottom = 4,
    };

    private static StyleBoxFlat card_style(bool hover, bool selected) => new()
    {
        BgColor = selected ? new Color(0.78f, 0.72f, 0.57f, 0.28f) : hover ? new Color(0.78f, 0.82f, 0.75f, 0.36f) : new Color(0.93f, 0.89f, 0.79f, 0.14f),
        BorderColor = selected ? new Color(0.54f, 0.39f, 0.20f, 0.72f) : hover ? new Color(0.40f, 0.47f, 0.40f, 0.54f) : new Color(0.39f, 0.36f, 0.29f, 0.26f),
        BorderWidthLeft = selected ? 2 : 1,
        BorderWidthTop = selected ? 2 : 1,
        BorderWidthRight = selected ? 2 : 1,
        BorderWidthBottom = selected ? 2 : 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 4,
        ContentMarginRight = 4,
        ContentMarginTop = 4,
        ContentMarginBottom = 4,
    };

    private static StyleBoxFlat paper_style(float backgroundAlpha, float borderAlpha) => new()
    {
        BgColor = new Color(0.94f, 0.91f, 0.83f, backgroundAlpha),
        BorderColor = new Color(0.39f, 0.36f, 0.29f, borderAlpha),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
    };

    private static StyleBoxFlat exchange_summary_style() => new()
    {
        BgColor = new Color(0.79f, 0.77f, 0.68f, 0.34f),
        BorderColor = new Color(0.43f, 0.39f, 0.31f, 0.42f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
    };

    private static string get_realm_range(GongfaResource? gongfa)
    {
        if (gongfa is null || string.IsNullOrWhiteSpace(gongfa.main_realm_start) || string.IsNullOrWhiteSpace(gongfa.main_realm_end))
            return "境界未标注";
        return $"{gongfa.main_realm_start}—{gongfa.main_realm_end}";
    }

    private static string build_manual_description(GongfaResource? gongfa)
    {
        var description = string.IsNullOrWhiteSpace(gongfa?.description) ? "尚无简介。" : gongfa.description.Trim();
        return $"修习范围：{get_realm_range(gongfa)}\n{description}";
    }

    private static string build_material_description(ItemData item, MaterialCraftData? material)
    {
        var gradeName = material?.material_grade_name() ?? item.get_grade_string();
        var tags = item.tags.Select(format_tag).ToArray();
        var utilities = material is null
            ? "未标注"
            : string.Join("、", material.utilities
                .Where(utility => utility is not null && !string.IsNullOrWhiteSpace(utility.utility_id))
                .Select(utility => $"{utility.utility_id}{utility.grade_name()}"));
        if (string.IsNullOrWhiteSpace(utilities)) utilities = "未标注";
        var subcategory = string.IsNullOrWhiteSpace(item.item_subcategory) ? "材料" : item.item_subcategory;
        return $"材料分类：{subcategory}\n品阶：{gradeName}\n材性：{string.Join(" · ", tags)}\n功用强度：{utilities}";
    }

    private static string format_tag(string tag)
    {
        var parsed = ItemAffixCatalog.parse_tag(tag);
        if (parsed.Count == 0) return tag;
        return $"{parsed["dimension"].AsString()}·{parsed["value"].AsString()}";
    }

    private static string short_category_label(string category) => category switch
    {
        "材料" => "材",
        "凡俗武学" => "武",
        "剑修" => "剑",
        "云息秘籍" => "秘",
        _ => category
    };
}
