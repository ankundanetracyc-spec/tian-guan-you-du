using Godot;
using D = Godot.Collections.Dictionary;

/// <summary>宗内坊市正式界面：真实交易接入纸墨木架样稿。</summary>
public partial class YuxuMarketPanel : Control
{
    public event Action? BackRequested;
    public event Action? Changed;
    public event Action? ClaimRequested;

    private const int GridColumns = 20, GridVisibleRows = 18;
    // At 1920×1080 this resolves to a 36px inventory cell; at 1280×720 it resolves to 24px.
    private const float GridCell = 31f;
    private static readonly Vector2 DesignSize = new(1672, 941);

    private Control _canvas = null!, _market = null!, _stall = null!, _shade = null!, _dialog = null!, _gridContent = null!;
    private TextureRect _pageBackground = null!, _detailIcon = null!, _balanceIcon = null!;
    private ScrollContainer _browseScroll = null!, _inventoryScroll = null!, _listingScroll = null!, _gridScroll = null!;
    private HBoxContainer _browseColumns = null!;
    private VBoxContainer _inventoryList = null!, _listingList = null!;
    private Label _balance = null!, _message = null!, _modeNote = null!, _selectedOfferText = null!, _stallStatus = null!, _pending = null!, _chosenInventory = null!, _confirmText = null!, _gridMeta = null!;
    private Button _marketTab = null!, _stallTab = null!, _buy = null!, _rent = null!, _list = null!, _claim = null!;
    private Button _allSources = null!, _carriedSource = null!, _storedSource = null!;
    private LineEdit _search = null!, _inventorySearch = null!;
    private OptionButton _category = null!;
    private SpinBox _buyCount = null!, _quantity = null!, _unitPrice = null!;
    private Character? _player;
    private NpcWorldManager? _world;
    private Func<bool>? _context;
    private YuxuMarketRules.Offer? _selectedOffer;
    private string _inventoryPath = "", _inventoryContainerId = "", _selectedListingId = "";
    private int _sourceFilter;
    private bool _showStall, _confirming;
    private Action? _confirm;

    private sealed record InventoryChoice(string Path, int Count, string ContainerId, string SourceName, string[] InstanceIds);
    private sealed record PlacedListing(string Id, string Path, ItemData Item, int QuantityIndex, int X, int Y, int Width, int Height);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; ZIndex = 20;
        AddToGroup("map_overlay_blocker");
        _canvas = Place(new Control { Name = "MarketCanvas" }, this, 0, 0, DesignSize.X, DesignSize.Y);
        _pageBackground = Place(new TextureRect { Name = "MarketPaperWoodBackground", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        Place(new ColorRect { Color = new Color(0.15f, 0.095f, 0.045f, .035f), MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);

        var title = Label("宗 内 坊 市", 37, HorizontalAlignment.Center);
        Place(title, _canvas, 538, 34, 596, 62);
        _marketTab = Place(Button("逛坊市", 22), _canvas, 94, 151, 137, 46);
        _stallTab = Place(Button("我的摊位", 22), _canvas, 238, 151, 151, 46);
        _marketTab.Pressed += () => { _showStall = false; Refresh(); };
        _stallTab.Pressed += () => { _showStall = true; Refresh(); };
        var stoneItem = GD.Load<ItemData>(YuxuMarketRules.StonePath);
        _balanceIcon = Place(new TextureRect { Texture = stoneItem is null ? null : YuxuLibraryArt.Texture(stoneItem.icon_path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore }, _canvas, 1293, 159, 27, 27);
        _balance = Place(Label("", 21, HorizontalAlignment.Left), _canvas, 1324, 153, 154, 40);
        var back = Place(Button("返回", 22), _canvas, 0, 0, CraftingPaperHeader.ReturnSize.X, CraftingPaperHeader.ReturnSize.Y);
        CraftingPaperHeader.PlaceReturn(_canvas, back); CraftingPaperHeader.AddReturnArrow(back);
        back.Pressed += () => BackRequested?.Invoke();

        _market = Place(new Control { Name = "BrowseMarket" }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        _stall = Place(new Control { Name = "MyMarketStall" }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        BuildMarket(); BuildStall(); BuildConfirm();
        _message = Place(Label("", 18, HorizontalAlignment.Center), _canvas, 440, 894, 792, 27);
        _message.Hide();
        Resized += Fit; Fit(); Hide();
    }

    private void BuildMarket()
    {
        _modeNote = Place(Label("同门互市 · 灵石交易", 18), _canvas, 410, 153, 192, 42);
        _category = Place(new OptionButton { Name = "MarketCategory" }, _canvas, 620, 153, 162, 44);
        StyleOption(_category);
        foreach (var category in new[] { "全部", "材料", "丹药", "装备", "器具", "阵盘", "符箓", "灵膳" }) _category.AddItem(category);
        _category.ItemSelected += _ => RefreshGoods();
        _search = Place(new LineEdit { Name = "MarketSearch", PlaceholderText = "检索坊市货物" }, _canvas, 793, 153, 222, 44);
        CraftingPaperStyle.ApplyInput(_search, 19, 12, 4);
        _search.TextChanged += _ => RefreshGoods();

        _browseScroll = Place(new ScrollContainer { Name = "MarketStallScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled }, _market, 92, 239, 1488, 575);
        _browseColumns = new HBoxContainer { Name = "MarketStallColumns", SizeFlagsVertical = SizeFlags.ExpandFill };
        _browseColumns.AddThemeConstantOverride("separation", 19);
        _browseScroll.AddChild(_browseColumns);

        var selection = new Panel { Name = "MarketPurchaseBar" };
        selection.AddThemeStyleboxOverride("panel", Paper(new Color("f0e4d1"), new Color("977a5d"), 1));
        Place(selection, _market, 94, 836, 1484, 54);
        _detailIcon = Place(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore }, _market, 107, 843, 40, 40);
        _selectedOfferText = Place(Label("选择摊货查看价格", 19), _market, 157, 841, 515, 42);
        Place(Label("数量", 18), _market, 715, 842, 56, 42);
        _buyCount = Place(Spin(1, 1), _market, 774, 842, 104, 42); _buyCount.Name = "BuyQuantity";
        _buyCount.ValueChanged += _ => RefreshBuyCaption();
        _buy = Place(Button("购买", 22, CraftingPaperStyle.ButtonKind.Primary), _market, 1380, 839, 178, 47);
        _buy.Name = "BuyItem"; _buy.Pressed += ConfirmBuy;
    }

    private void BuildStall()
    {
        Place(Label("我的行囊", 26), _stall, 94, 238, 400, 42);
        Place(Label("摊位货格", 26), _stall, 555, 238, 440, 42);
        _gridMeta = Place(Label("20 × 18 格 · 可视区域", 17, HorizontalAlignment.Right), _stall, 850, 239, 320, 40);

        Place(Label("随身与储物空间", 17), _stall, 95, 278, 365, 30);
        _allSources = Place(Button("全部", 17), _stall, 95, 311, 94, 38);
        _carriedSource = Place(Button("随身", 17), _stall, 193, 311, 94, 38);
        _storedSource = Place(Button("储物空间", 17), _stall, 291, 311, 121, 38);
        _allSources.Pressed += () => SetSourceFilter(0); _carriedSource.Pressed += () => SetSourceFilter(1); _storedSource.Pressed += () => SetSourceFilter(2);
        _inventorySearch = Place(new LineEdit { Name = "InventorySearch", PlaceholderText = "检索物品" }, _stall, 95, 354, 365, 42);
        CraftingPaperStyle.ApplyInput(_inventorySearch, 18, 11, 4);
        _inventorySearch.TextChanged += _ => { if (_player?.entity_data is HumanData human) RefreshInventory(human); };
        _inventoryScroll = Place(new ScrollContainer { Name = "MarketInventoryScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, _stall, 95, 402, 365, 252);
        _inventoryList = new VBoxContainer { Name = "MarketInventoryList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inventoryList.AddThemeConstantOverride("separation", 4); _inventoryScroll.AddChild(_inventoryList);
        _chosenInventory = Place(Label("选择一件可上架物品", 18), _stall, 96, 661, 362, 34);
        Place(Label("数量", 16), _stall, 96, 700, 55, 35); _quantity = Place(Spin(1, 1), _stall, 151, 697, 96, 42);
        Place(Label("单价 · 灵石", 16), _stall, 256, 700, 105, 35); _unitPrice = Place(Spin(1, 999999), _stall, 361, 697, 98, 42);
        _list = Place(Button("上架物品", 20, CraftingPaperStyle.ButtonKind.Primary), _stall, 95, 749, 364, 48);
        _list.Name = "ListMarketItem"; _list.Pressed += ConfirmList;
        Place(Label("选中物品后设置数量与每件售价", 15), _stall, 97, 799, 362, 27);

        Place(Label("图标沿用物品格形排列 · 单格 36px", 17), _stall, 555, 275, 620, 29);
        _gridScroll = Place(new ScrollContainer { Name = "MarketStallGridScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled },
            _stall, 555, 307, GridColumns * GridCell + 2, GridVisibleRows * GridCell + 2);
        var gridFrame = new Panel { Name = "MarketStallGridFrame", MouseFilter = MouseFilterEnum.Ignore };
        gridFrame.AddThemeStyleboxOverride("panel", Paper(new Color("8a7357"), new Color("765b40"), 1));
        Place(gridFrame, _stall, 553, 305, GridColumns * GridCell + 6, GridVisibleRows * GridCell + 6);
        _gridScroll.ZIndex = 1;
        _gridContent = new Control { Name = "MarketStallGrid", CustomMinimumSize = new(GridColumns * GridCell, GridVisibleRows * GridCell),
            Size = new(GridColumns * GridCell, GridVisibleRows * GridCell), MouseFilter = MouseFilterEnum.Pass };
        _gridScroll.AddChild(_gridContent);

        var leaseCard = new Panel { Name = "MarketLeaseCard" };
        leaseCard.AddThemeStyleboxOverride("panel", Paper(new Color("#f4ead9e8"), new Color("a18a6e"), 1));
        Place(leaseCard, _stall, 1193, 262, 383, 548);
        Place(Label("租期与所得", 24), _stall, 1215, 276, 335, 39);
        _stallStatus = Place(Label("", 19), _stall, 1215, 317, 335, 64); _stallStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var ruleLine = new ColorRect { Color = new Color(.46f, .35f, .23f, .38f), MouseFilter = MouseFilterEnum.Ignore };
        Place(ruleLine, _stall, 1216, 385, 336, 1);
        Place(Label("租金按游戏月收取，含坊市代管。\n到期停止售卖，未售物品与所得仍可领取。", 16), _stall, 1215, 394, 337, 70);
        _pending = Place(Label("", 19), _stall, 1215, 467, 336, 37);
        _claim = Place(Button("领取所得", 20, CraftingPaperStyle.ButtonKind.Primary), _stall, 1215, 505, 336, 46);
        _claim.Name = "ClaimMarketProceeds"; _claim.Pressed += () => ClaimRequested?.Invoke();
        Place(Label("摊上货物", 19), _stall, 1215, 558, 336, 32);
        _listingScroll = Place(new ScrollContainer { Name = "MarketListingsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, _stall, 1214, 589, 339, 151);
        _listingList = new VBoxContainer { Name = "MarketListings", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _listingList.AddThemeConstantOverride("separation", 4); _listingScroll.AddChild(_listingList);
        _rent = Place(Button("租用摊位", 20, CraftingPaperStyle.ButtonKind.Primary), _stall, 1215, 755, 336, 47);
        _rent.Name = "RentMarketStall"; _rent.Pressed += ConfirmRentOrClose;
    }

    private void BuildConfirm()
    {
        _shade = Place(new ColorRect { Color = new Color(0, 0, 0, .54f), MouseFilter = MouseFilterEnum.Stop }, _canvas, 0, 0, DesignSize.X, DesignSize.Y); _shade.Hide();
        _dialog = Place(new Panel { Name = "MarketConfirm" }, _canvas, 522, 260, 628, 380);
        ((Panel)_dialog).AddThemeStyleboxOverride("panel", Paper(new Color("f5ead8"), new Color("8d7356"), 1)); _dialog.Hide();
        _confirmText = Place(Label("", 23), _dialog, 48, 43, 532, 215); _confirmText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var cancel = Place(Button("返回", 21, CraftingPaperStyle.ButtonKind.Regular), _dialog, 144, 290, 145, 48); cancel.Pressed += CloseConfirm;
        var accept = Place(Button("确认", 21, CraftingPaperStyle.ButtonKind.Primary), _dialog, 337, 290, 145, 48); accept.Name = "ConfirmMarketAction";
        accept.Pressed += () => { var action = _confirm; CloseConfirm(); if (Usable()) action?.Invoke(); else ShowStatus("坊市状态已变化。"); };
    }

    public void Open(Character player, NpcWorldManager world, Func<bool> context)
    {
        _player = player; _world = world; _context = context; _showStall = false; _selectedOffer = null; _inventoryPath = ""; _inventoryContainerId = "";
        _search.Text = ""; _inventorySearch.Text = ""; _category.Select(0); _message.Text = ""; _message.Hide(); CloseConfirm(); Refresh(); Show(); MoveToFront(); Fit();
    }
    private bool Usable() => _player is not null && GodotObject.IsInstanceValid(_player) && _context?.Invoke() == true;
    private long Now() => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
    public void ShowStatus(string text) { _message.Text = text; _message.Visible = !string.IsNullOrWhiteSpace(text); }
    private void Run(Func<string> action) { if (!Usable()) { ShowStatus("坊市状态已变化。"); return; } var text = action(); Changed?.Invoke(); Refresh(); ShowStatus(text); }
    private void Confirm(string title, string text, Action action) { _confirmText.Text = title + "\n\n" + text; _confirm = action; _confirming = true; _shade.Show(); _dialog.Show(); }
    private void CloseConfirm() { _confirming = false; _confirm = null; _shade?.Hide(); _dialog?.Hide(); }

    public void Refresh()
    {
        if (_player?.entity_data is not HumanData human || _world is null) return;
        _market.Visible = !_showStall; _stall.Visible = _showStall;
        _pageBackground.Texture = YuxuLibraryArt.Texture(_showStall
            ? "res://textures/ui/yuxu_market_20261107/market_stall_space_frame_v2.png"
            : "res://textures/ui/yuxu_market_20261107/market_ledger_frame_v1.png");
        _category.Visible = !_showStall; _search.Visible = !_showStall;
        _modeNote.Text = _showStall ? "租金 1 灵石 / 游戏月 · 不自动续租" : "同门互市 · 灵石交易";
        _modeNote.Size = new(_showStall ? 650 : 192, 42);
        StyleButtonState(_marketTab, CraftingPaperStyle.ButtonKind.Regular, 22, !_showStall);
        StyleButtonState(_stallTab, CraftingPaperStyle.ButtonKind.Regular, 22, _showStall);
        _balance.Text = $"{_player.get_inventory_total_count(YuxuMarketRules.StonePath)} 枚";
        RefreshGoods(); RefreshStall(human);
    }

    private void RefreshGoods()
    {
        if (_world is null || _browseColumns is null) return;
        var scrollPosition = _browseScroll.ScrollHorizontal;
        Clear(_browseColumns);
        var text = _search.Text.Trim(); var category = _category.Selected >= 0 ? _category.GetItemText(_category.Selected) : "全部";
        var offers = YuxuMarketRules.Offers(_world.get_subscene_roster(YuxuMarketRules.NodeId, YuxuMarketRules.PeakId))
            .Where(o => YuxuMarketRules.Item(o.Path) is { } item && item.entity_name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && (category == "全部" || item.item_category == category)).ToList();
        var groups = offers.GroupBy(o => o.Seller).OrderBy(g => YuxuMarketRules.SellerName(g.Key), StringComparer.CurrentCulture).ToList();
        if (_selectedOffer is null || !offers.Any(o => o.Seller.npc_id == _selectedOffer.Seller.npc_id && o.Path == _selectedOffer.Path))
            _selectedOffer = offers.FirstOrDefault();
        else _selectedOffer = offers.First(o => o.Seller.npc_id == _selectedOffer.Seller.npc_id && o.Path == _selectedOffer.Path);

        if (groups.Count == 0)
        {
            var empty = Place(new Control { Name = "MarketEmptyState" }, _browseColumns, 0, 0, 500, 320);
            Place(Label("眼下没有可交易的摊货", 25, HorizontalAlignment.Center), empty, 20, 90, 450, 48);
            Place(Label("同门有真实货物上架后，会在这里显示。", 17, HorizontalAlignment.Center), empty, 20, 144, 450, 40);
        }
        foreach (var group in groups)
        {
            var column = new Panel { Name = "SellerStall", CustomMinimumSize = new(345, 550), Size = new(345, 550), MouseFilter = MouseFilterEnum.Ignore };
            column.AddThemeStyleboxOverride("panel", Paper(new Color("#f5ead92c"), new Color("8b7459"), 1));
            _browseColumns.AddChild(column);
            Place(Label(YuxuMarketRules.SellerName(group.Key), 23), column, 17, 14, 215, 38);
            var custody = Label("坊市代管", 15, HorizontalAlignment.Right); custody.AddThemeColorOverride("font_color", new Color("71624e"));
            Place(custody, column, 222, 16, 105, 33);
            Place(Label("货物", 16), column, 20, 58, 155, 29);
            Place(Label("数量", 16, HorizontalAlignment.Right), column, 207, 58, 53, 29);
            Place(Label("单价", 16, HorizontalAlignment.Right), column, 267, 58, 61, 29);
            Place(new ColorRect { Color = new Color(.48f, .36f, .24f, .40f), MouseFilter = MouseFilterEnum.Ignore }, column, 17, 88, 310, 1);
            var scroll = Place(new ScrollContainer { Name = "SellerGoodsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, column, 14, 96, 317, 399);
            var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 5); scroll.AddChild(rows);
            foreach (var offer in group)
            {
                var item = YuxuMarketRules.Item(offer.Path)!;
                bool selected = _selectedOffer?.Seller.npc_id == offer.Seller.npc_id && _selectedOffer?.Path == offer.Path;
                var row = new Button { Name = "MarketOffer", CustomMinimumSize = new(307, 61), Text = "", ClipContents = true };
                StyleButtonState(row, CraftingPaperStyle.ButtonKind.Regular, 17, selected);
                var layout = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
                layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); layout.OffsetLeft = 8; layout.OffsetRight = -8;
                layout.AddThemeConstantOverride("separation", 6); row.AddChild(layout);
                var icon = new TextureRect { Texture = YuxuLibraryArt.Texture(item.icon_path), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new(38, 38), MouseFilter = MouseFilterEnum.Ignore };
                layout.AddChild(icon);
                var rowName = Label(item.entity_name, 16); rowName.CustomMinimumSize = new(105, 0); rowName.SizeFlagsHorizontal = SizeFlags.ExpandFill; layout.AddChild(rowName);
                var rowCount = Label(offer.Quantity.ToString(), 16, HorizontalAlignment.Right); rowCount.CustomMinimumSize = new(42, 0); layout.AddChild(rowCount);
                var rowPrice = Label($"{offer.UnitPrice}", 16, HorizontalAlignment.Right); rowPrice.CustomMinimumSize = new(50, 0); layout.AddChild(rowPrice);
                row.Pressed += () => { _selectedOffer = offer; RefreshGoods(); };
                rows.AddChild(row);
                HoverInfoManager.SetProvider(row, () => StoragePanel.ItemHover(item, offer.Quantity, offer.Path, _player));
            }
            Place(Label($"{group.Count()} 种货物", 15), column, 17, 510, 120, 26);
            Place(Label("同门寄售", 16, HorizontalAlignment.Right), column, 186, 507, 141, 29);
        }
        _browseScroll.ScrollHorizontal = scrollPosition;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var offer = _selectedOffer; var item = offer is null ? null : YuxuMarketRules.Item(offer.Path);
        _detailIcon.Texture = item is null ? null : YuxuLibraryArt.Texture(item.icon_path);
        _selectedOfferText.Text = offer is null || item is null ? "暂无可选摊货" : $"{YuxuMarketRules.SellerName(offer.Seller)} · {item.entity_name}　{offer.UnitPrice} 灵石 / 件";
        _buyCount.MaxValue = Math.Max(1, offer?.Quantity ?? 1);
        if (_buyCount.Value > _buyCount.MaxValue) _buyCount.Value = _buyCount.MaxValue;
        _buy.Disabled = offer is null;
        RefreshBuyCaption();
    }
    private void RefreshBuyCaption() { if (_buy is not null) _buy.Text = _selectedOffer is null ? "购买" : $"购买 · {(long)_selectedOffer.UnitPrice * (int)_buyCount.Value} 灵石"; }
    private void ConfirmBuy()
    {
        if (_selectedOffer is not { } offer || YuxuMarketRules.Item(offer.Path) is not { } item) return;
        int count = (int)_buyCount.Value; long total = (long)count * offer.UnitPrice;
        Confirm("确认购买", $"{item.entity_name} ×{count}\n共需 {total} 灵石，消耗 1 点精力。", () => Run(() =>
        {
            if (_world?.get_npc(offer.Seller.npc_id) != offer.Seller) return "摊主已离开，未能成交。";
            YuxuMarketRules.TryBuy(_player!, offer.Seller, offer.Path, count, offer.UnitPrice, Now(), out var message); return message;
        }));
    }

    private void RefreshStall(HumanData human)
    {
        bool active = YuxuMarketRules.Active(human, Now());
        var remaining = Math.Max(0, human.yuxu_market_lease_end_hour - Now());
        _stallStatus.Text = active
            ? $"已租用　尚余 {Math.Max(1, (remaining + 23) / 24)} 日\n到期：{FormatHour(human.yuxu_market_lease_end_hour)}"
            : "尚未租用　租金：1 灵石 / 月\n到期后不自动续租。";
        _rent.Disabled = false; _rent.Text = active ? "结束摆摊" : "租用摊位";
        StyleButtonState(_rent, CraftingPaperStyle.ButtonKind.Primary, 20, false);
        RefreshInventory(human);
        var selected = GetSelectedInventoryChoice(human);
        _chosenInventory.Text = selected is null ? "选择一件可上架物品" : $"{selected.SourceName} · {YuxuMarketRules.Item(selected.Path)?.entity_name}";
        _quantity.MaxValue = Math.Max(1, selected?.Count ?? 1); if (_quantity.Value > _quantity.MaxValue) _quantity.Value = _quantity.MaxValue;
        _list.Disabled = !active || selected is null;
        RebuildGrid(human);
        RefreshListingControls(human, active);
        _pending.Text = $"待领取售得灵石　{human.yuxu_market_pending_stones} 枚";
        _claim.Disabled = human.yuxu_market_pending_stones <= 0;
    }

    private List<InventoryChoice> AvailableInventory(HumanData human)
    {
        if (_player is null) return [];
        var result = new List<InventoryChoice>();
        var entries = _player.get_accessible_spatial_inventory();
        foreach (var group in entries.Where(e => e.ContainerId.Length == 0 && e.Item is not null && !e.IsEquipped
                && _player.can_store_spatial_item(e.InstanceId) && YuxuMarketRules.ReferencePrice(e.ItemPath) > 0)
            .GroupBy(e => e.ItemPath))
        {
            result.Add(new(group.Key, group.Sum(e => Math.Max(1, e.Count)), "", "随身", group.Select(e => e.InstanceId).ToArray()));
        }
        var bags = _player.get_storage_bags().ToDictionary(b => b.InstanceId, b => b.DisplayName, StringComparer.Ordinal);
        foreach (var group in entries.Where(e => e.ContainerId.Length > 0 && e.Item is not null && !e.IsEquipped
                && _player.can_store_spatial_item(e.InstanceId) && YuxuMarketRules.ReferencePrice(e.ItemPath) > 0)
            .GroupBy(e => (e.ContainerId, e.ItemPath)))
        {
            result.Add(new(group.Key.ItemPath, group.Count(), group.Key.ContainerId, bags.GetValueOrDefault(group.Key.ContainerId, "储物空间"),
                group.Select(e => e.InstanceId).ToArray()));
        }
        return result.Where(x => _sourceFilter == 0 || (_sourceFilter == 1) == (x.ContainerId.Length == 0))
            .Where(x => _inventorySearch.Text.Length == 0 || (YuxuMarketRules.Item(x.Path)?.entity_name.Contains(_inventorySearch.Text.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(x => x.SourceName, StringComparer.CurrentCulture).ThenBy(x => YuxuMarketRules.Item(x.Path)?.entity_name, StringComparer.CurrentCulture).ToList();
    }

    private InventoryChoice? GetSelectedInventoryChoice(HumanData human) => AvailableInventory(human)
        .FirstOrDefault(x => x.Path == _inventoryPath && x.ContainerId == _inventoryContainerId);

    private void RefreshInventory(HumanData human)
    {
        if (_inventoryList is null || _player is null) return;
        Clear(_inventoryList);
        var available = AvailableInventory(human);
        if (available.All(x => x.Path != _inventoryPath || x.ContainerId != _inventoryContainerId))
        {
            _inventoryPath = available.FirstOrDefault()?.Path ?? "";
            _inventoryContainerId = available.FirstOrDefault()?.ContainerId ?? "";
            if (_inventoryPath.Length > 0) _unitPrice.Value = YuxuMarketRules.ReferencePrice(_inventoryPath);
        }
        foreach (var entry in available)
        {
            var item = YuxuMarketRules.Item(entry.Path); if (item is null) continue;
            bool selected = entry.Path == _inventoryPath && entry.ContainerId == _inventoryContainerId;
            var row = new Button { Name = "MarketInventoryItem", Text = "", CustomMinimumSize = new(350, 46), ClipContents = true };
            StyleButtonState(row, CraftingPaperStyle.ButtonKind.Regular, 16, selected);
            var layout = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); layout.OffsetLeft = 8; layout.OffsetRight = -8; layout.AddThemeConstantOverride("separation", 8);
            row.AddChild(layout);
            layout.AddChild(new TextureRect { Texture = YuxuLibraryArt.Texture(item.icon_path), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new(34, 34), MouseFilter = MouseFilterEnum.Ignore });
            var name = Label(item.entity_name, 16); name.SizeFlagsHorizontal = SizeFlags.ExpandFill; layout.AddChild(name);
            var count = Label($"{entry.Count} 件", 15, HorizontalAlignment.Right); count.CustomMinimumSize = new(56, 0); layout.AddChild(count);
            row.Pressed += () => { _inventoryPath = entry.Path; _inventoryContainerId = entry.ContainerId; _quantity.Value = 1; _unitPrice.Value = YuxuMarketRules.ReferencePrice(entry.Path); RefreshStall(human); };
            _inventoryList.AddChild(row);
            HoverInfoManager.SetProvider(row, () => StoragePanel.ItemHover(item, entry.Count, entry.Path, _player));
        }
        if (available.Count == 0) _inventoryList.AddChild(Label("暂无可寄售物品", 18, HorizontalAlignment.Center));
        StyleButtonState(_allSources, CraftingPaperStyle.ButtonKind.Regular, 17, _sourceFilter == 0);
        StyleButtonState(_carriedSource, CraftingPaperStyle.ButtonKind.Regular, 17, _sourceFilter == 1);
        StyleButtonState(_storedSource, CraftingPaperStyle.ButtonKind.Regular, 17, _sourceFilter == 2);
    }

    private void SetSourceFilter(int filter)
    {
        _sourceFilter = filter;
        if (_player?.entity_data is HumanData human) RefreshStall(human);
    }

    private void ConfirmList()
    {
        if (_player?.entity_data is not HumanData human || GetSelectedInventoryChoice(human) is not { } selected) return;
        int quantity = Math.Clamp((int)_quantity.Value, 1, selected.Count); int price = (int)_unitPrice.Value;
        string origin = selected.ContainerId.Length == 0 ? "随身行囊" : selected.SourceName;
        var ids = selected.InstanceIds.Take(quantity).ToArray();
        string message;
        Confirm("确认上架", $"{YuxuMarketRules.Item(selected.Path)?.entity_name} ×{quantity}\n来源：{origin}\n单价：{price} 灵石 / 件", () => Run(() =>
        {
            if (selected.ContainerId.Length == 0)
                YuxuMarketRules.TryList(_player!, selected.Path, quantity, price, Now(), out message);
            else
                YuxuMarketRules.TryListFromStorage(_player!, selected.ContainerId, ids, selected.Path, price, Now(), out message);
            return message;
        }));
    }

    private void RebuildGrid(HumanData human)
    {
        Clear(_gridContent);
        var occupied = new HashSet<Vector2I>(); var placements = new List<PlacedListing>();
        int maxRow = GridVisibleRows;
        foreach (var listing in human.yuxu_market_listings.Where(x => x.GetValueOrDefault("quantity", 0).AsInt32() > 0))
        {
            string path = listing.GetValueOrDefault("item_path", "").AsString();
            var item = YuxuMarketRules.Item(path); if (item is null) continue;
            int quantity = listing.GetValueOrDefault("quantity", 0).AsInt32();
            var size = item.get_inventory_size();
            if (size.X > GridColumns && item.inventory_can_rotate) size = new(size.Y, size.X);
            if (size.X > GridColumns || size.Y <= 0 || size.X <= 0) continue;
            for (int index = 0; index < quantity; index++)
            {
                var position = FindGridPosition(occupied, size.X, size.Y);
                if (position is null) break;
                for (int y = 0; y < size.Y; y++) for (int x = 0; x < size.X; x++) occupied.Add(new(position.Value.X + x, position.Value.Y + y));
                placements.Add(new(listing.GetValueOrDefault("id", "").AsString(), path, item, index, position.Value.X, position.Value.Y, size.X, size.Y));
                maxRow = Math.Max(maxRow, position.Value.Y + size.Y);
            }
        }
        var surfaceSize = new Vector2(GridColumns * GridCell, maxRow * GridCell);
        _gridContent.CustomMinimumSize = surfaceSize; _gridContent.Size = surfaceSize;
        var fill = new ColorRect { Color = new Color("eee1cb"), MouseFilter = MouseFilterEnum.Ignore };
        Place(fill, _gridContent, 0, 0, surfaceSize.X, surfaceSize.Y);
        var line = new Color(.39f, .29f, .19f, .34f);
        for (int x = 0; x <= GridColumns; x++) Place(new ColorRect { Color = line, MouseFilter = MouseFilterEnum.Ignore }, _gridContent, x * GridCell, 0, 1, surfaceSize.Y);
        for (int y = 0; y <= maxRow; y++) Place(new ColorRect { Color = line, MouseFilter = MouseFilterEnum.Ignore }, _gridContent, 0, y * GridCell, surfaceSize.X, 1);
        foreach (var spot in placements)
        {
            var card = new Button { Name = "MarketPlacedItem", Text = "", TooltipText = $"{spot.Item.entity_name} · 点击查看操作",
                CustomMinimumSize = new(spot.Width * GridCell - 4, spot.Height * GridCell - 4), Size = new(spot.Width * GridCell - 4, spot.Height * GridCell - 4) };
            StyleButtonState(card, CraftingPaperStyle.ButtonKind.Regular, 13, spot.Id == _selectedListingId, noMinimum: true);
            card.Position = new Vector2(spot.X * GridCell + 2, spot.Y * GridCell + 2);
            card.AddChild(new TextureRect { Texture = YuxuLibraryArt.Texture(spot.Item.icon_path), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, AnchorLeft = 0, AnchorTop = 0, AnchorRight = 1, AnchorBottom = 1,
                OffsetLeft = 3, OffsetTop = 3, OffsetRight = -3, OffsetBottom = -3, MouseFilter = MouseFilterEnum.Ignore });
            var badge = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            badge.AddThemeStyleboxOverride("panel", Paper(new Color("#f6ead5f0"), new Color("93785a"), 1));
            Place(badge, card, Math.Max(0, card.Size.X - 35), Math.Max(0, card.Size.Y - 20), 33, 18);
            Place(Label("1件", 12, HorizontalAlignment.Center), badge, 1, 0, 31, 18);
            card.Pressed += () => { _selectedListingId = spot.Id; RefreshStall(human); };
            _gridContent.AddChild(card);
            HoverInfoManager.SetProvider(card, () => StoragePanel.ItemHover(spot.Item, 1, spot.Path, _player));
        }
        _gridMeta.Text = $"20 × 18 可视 · 展示 {placements.Count} 件物品";
    }

    private static Vector2I? FindGridPosition(HashSet<Vector2I> occupied, int width, int height)
    {
        int maxY = Math.Max(GridVisibleRows, occupied.Count + height + 1);
        for (int y = 0; y <= maxY; y++)
            for (int x = 0; x <= GridColumns - width; x++)
            {
                bool free = true;
                for (int cy = 0; cy < height && free; cy++) for (int cx = 0; cx < width; cx++)
                    if (occupied.Contains(new(x + cx, y + cy))) { free = false; break; }
                if (free) return new Vector2I(x, y);
            }
        return null;
    }

    private void RefreshListingControls(HumanData human, bool active)
    {
        Clear(_listingList);
        foreach (var listing in human.yuxu_market_listings.Where(x => x.GetValueOrDefault("quantity", 0).AsInt32() > 0))
        {
            string id = listing.GetValueOrDefault("id", "").AsString(), path = listing.GetValueOrDefault("item_path", "").AsString();
            var item = YuxuMarketRules.Item(path); if (item is null) continue;
            var row = new HBoxContainer { Name = "MarketListingControl", CustomMinimumSize = new(328, 43), Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 3);
            var label = Label(item.entity_name, 14); label.CustomMinimumSize = new(89, 38); row.AddChild(label);
            var newPrice = Spin(1, 999999); newPrice.Value = listing.GetValueOrDefault("unit_price", 1).AsInt32(); newPrice.CustomMinimumSize = new(76, 38); row.AddChild(newPrice);
            var reprice = Button("改价", 14, CraftingPaperStyle.ButtonKind.Regular); reprice.CustomMinimumSize = new(66, 39); reprice.Disabled = !active; row.AddChild(reprice);
            reprice.Pressed += () => { int price = (int)newPrice.Value; Confirm("修改售价", $"{item.entity_name} 改为 {price} 灵石 / 件？", () => Run(() => YuxuMarketRules.TryReprice(human, id, price, Now(), out var m) ? m : m)); };
            var withdraw = Button("撤下", 14, CraftingPaperStyle.ButtonKind.Regular); withdraw.CustomMinimumSize = new(66, 39); row.AddChild(withdraw);
            withdraw.Pressed += () => Confirm("撤下物品", $"将剩余 {listing.GetValueOrDefault("quantity", 0).AsInt32()} 件 {item.entity_name} 收回行囊？", () => Run(() => YuxuMarketRules.TryWithdraw(_player!, id, out var m) ? m : m));
            _listingList.AddChild(row);
        }
        if (_listingList.GetChildCount() == 0) _listingList.AddChild(Label("摊上还没有物品。", 16));
    }

    private void ConfirmRentOrClose()
    {
        if (_player?.entity_data is not HumanData human) return;
        if (YuxuMarketRules.Active(human, Now()))
            Confirm("结束摆摊", "已付租金不退，未售物品与所得仍可领取。", () => Run(() => YuxuMarketRules.TryClose(human, Now(), out var m) ? m : m));
        else Confirm("租用摊位", "租金：1 灵石 / 游戏月\n到期停止售卖，不自动续租。", () => Run(() => YuxuMarketRules.TryRent(_player!, Now(), out var m) ? m : m));
    }

    private static string FormatHour(long hour) { var day = hour / 24; return $"{day / 360 + 1}年{day % 360 / 30 + 1}月{day % 30 + 1}日{hour % 24}时"; }
    private static void Clear(Node parent) { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private static T Place<T>(T child, Control parent, float x, float y, float w, float h) where T : Control
    { parent.AddChild(child); child.Position = new(x, y); child.Size = new(w, h); return child; }
    private static Label Label(string text, int size, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore, ClipText = true };
        label.AddThemeFontOverride("font", YuxuLibraryArt.Font()); label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("38433a")); return label;
    }
    private static Button Button(string text, int size, CraftingPaperStyle.ButtonKind kind = CraftingPaperStyle.ButtonKind.Regular)
    { var b = new Button { Text = text, MouseDefaultCursorShape = CursorShape.PointingHand }; CraftingPaperStyle.ApplyButton(b, kind, size); return b; }
    private static void StyleButtonState(Button button, CraftingPaperStyle.ButtonKind kind, int fontSize, bool selected, bool noMinimum = false)
    {
        foreach (var (name, state) in new[] { ("normal", CraftingPaperStyle.ButtonState.Normal), ("hover", CraftingPaperStyle.ButtonState.Hover),
            ("pressed", CraftingPaperStyle.ButtonState.Pressed), ("hover_pressed", CraftingPaperStyle.ButtonState.Hover), ("disabled", CraftingPaperStyle.ButtonState.Disabled) })
            button.AddThemeStyleboxOverride(name, CraftingPaperStyle.ButtonStyle(kind, selected && state != CraftingPaperStyle.ButtonState.Disabled
                ? CraftingPaperStyle.ButtonState.Selected : state));
        button.AddThemeStyleboxOverride("focus", CraftingPaperStyle.ButtonStyle(kind));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, new Color("38433a"));
        button.AddThemeColorOverride("font_disabled_color", CraftingPaperStyle.DisabledInk);
        button.AddThemeFontOverride("font", YuxuLibraryArt.Font()); button.AddThemeFontSizeOverride("font_size", fontSize);
        if (!noMinimum) button.MouseDefaultCursorShape = CursorShape.PointingHand;
    }
    private static SpinBox Spin(int min, int max)
    {
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = min, AllowGreater = false, AllowLesser = false };
        CraftingPaperStyle.ApplyInput(spin.GetLineEdit(), 17, 7, 2);
        spin.GetLineEdit().Alignment = HorizontalAlignment.Center;
        foreach (var state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_focus_color" })
            spin.AddThemeColorOverride(state, new Color("65523f"));
        spin.AddThemeColorOverride("icon_disabled_color", new Color("a59a88"));
        return spin;
    }
    private static void StyleOption(OptionButton option)
    {
        foreach (var (name, state) in new[] { ("normal", CraftingPaperStyle.ButtonState.Normal), ("hover", CraftingPaperStyle.ButtonState.Hover),
            ("pressed", CraftingPaperStyle.ButtonState.Pressed), ("disabled", CraftingPaperStyle.ButtonState.Disabled) })
            option.AddThemeStyleboxOverride(name, CraftingPaperStyle.ButtonStyle(CraftingPaperStyle.ButtonKind.Regular, state));
        option.AddThemeFontOverride("font", YuxuLibraryArt.Font()); option.AddThemeFontSizeOverride("font_size", 17);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) option.AddThemeColorOverride(state, new Color("38433a"));
    }
    private static StyleBoxFlat Paper(Color bg, Color border, int borderWidth = 1) => new()
    {
        BgColor = bg, BorderColor = border, BorderWidthLeft = borderWidth, BorderWidthRight = borderWidth,
        BorderWidthTop = borderWidth, BorderWidthBottom = borderWidth,
        ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 4, ContentMarginBottom = 4
    };
    private void Fit()
    {
        var size = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size;
        var scale = Math.Min(size.X / DesignSize.X, size.Y / DesignSize.Y);
        _canvas.Scale = Vector2.One * scale; _canvas.Position = (size - DesignSize * scale) * .5f;
    }
    public bool HandleBack()
    {
        if (!Visible) return false;
        if (_confirming) CloseConfirm(); else BackRequested?.Invoke();
        return true;
    }
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || _context?.Invoke() != true || input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        HandleBack(); GetViewport().SetInputAsHandled();
    }
}
