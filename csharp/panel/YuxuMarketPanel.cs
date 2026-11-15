using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using D = Godot.Collections.Dictionary;

/// <summary>坊市与本人摊位，货物和结算均来自真实库存。</summary>
public partial class YuxuMarketPanel : Control
{
    public event Action? BackRequested;
    public event Action? Changed;
    public event Action? ClaimRequested;
    private static readonly Vector2 DesignSize = new(1672, 941);
    private const int StallsPerPage = 3;
    private readonly Dictionary<(string Seller, string Path), int> _basket = [];
    private readonly Dictionary<(string Seller, string Path), YuxuMarketRules.Offer> _offers = [];
    private readonly Dictionary<(string Seller, string Path), YuxuMarketRules.Offer> _quotes = [];
    private readonly Dictionary<string, int> _sellerScroll = [];
    private Control _canvas = null!, _browse = null!, _stall = null!, _shade = null!, _dialog = null!;
    private HBoxContainer _columns = null!;
    private VBoxContainer _sources = null!, _listings = null!;
    private YuxuMarketDropArea _sourceScroll = null!, _listingScroll = null!;
    private LineEdit _search = null!, _sourceSearch = null!;
    private Button _browseTab = null!, _stallTab = null!, _previous = null!, _next = null!;
    private Button _buy = null!, _clear = null!, _rent = null!, _claim = null!, _all = null!, _carried = null!, _stored = null!;
    private Label _balance = null!, _page = null!, _totals = null!, _status = null!, _lease = null!, _proceeds = null!, _listingVolume = null!, _confirmText = null!;
    private Character? _player;
    private NpcWorldManager? _world;
    private Func<bool>? _context;
    private Action? _confirm;
    private bool _showStall, _confirming, _refreshing;
    private int _pageIndex, _sourceFilter;
    private sealed record InventoryChoice(string Path, int Count, string ContainerId, string SourceName, string InstanceId);
    private sealed record SellerGroup(WorldNpcData Seller, List<YuxuMarketRules.Offer> Offers);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 20;
        AddToGroup("map_overlay_blocker");
        var matte = new ColorRect { Color = new Color("594e47"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(matte);
        matte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = Place(new Control { Name = "MarketCanvas" }, this, 0, 0, DesignSize.X, DesignSize.Y);
        Place(new TextureRect
        {
            Name = "MarketPaperWoodBackground",
            Texture = YuxuLibraryArt.Texture("res://textures/ui/yuxu_market_20261107/market_ledger_blank_20261115_v3.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        Place(Label("坊市", 33, HorizontalAlignment.Center), _canvas, 650, 51, 372, 59);
        var back = Place(Button("返回", 22), _canvas, 0, 0, 114, 46);
        CraftingPaperHeader.PlaceReturn(_canvas, back);
        CraftingPaperHeader.AddReturnArrow(back);
        back.Pressed += HandleBackButton;
        _browseTab = Place(Button("逛坊市", 22), _canvas, 76, 139, 137, 43);
        _stallTab = Place(Button("我的摊位", 22), _canvas, 221, 139, 151, 43);
        _browseTab.Pressed += () => { _showStall = false; Refresh(); };
        _stallTab.Pressed += () => { _showStall = true; Refresh(); };
        _search = Place(new LineEdit { Name = "MarketSearch", PlaceholderText = "搜索物品或者摊位", ClearButtonEnabled = true }, _canvas, 802, 139, 300, 43);
        CraftingPaperStyle.ApplyInput(_search, 22, 12, 4);
        _search.TextChanged += _ => { _pageIndex = 0; RefreshGoods(); };
        _previous = Place(Button("‹", 22), _canvas, 1115, 139, 53, 43);
        _page = Place(Label("", 22, HorizontalAlignment.Center), _canvas, 1171, 139, 65, 43);
        _next = Place(Button("›", 22), _canvas, 1238, 139, 53, 43);
        _previous.Pressed += () => { _pageIndex--; RefreshGoods(); };
        _next.Pressed += () => { _pageIndex++; RefreshGoods(); };
        _balance = Place(Label("", 22, HorizontalAlignment.Right), _canvas, 1320, 139, 262, 43);
        _browse = Place(new Control { Name = "BrowseMarket", MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        _stall = Place(new Control { Name = "MyMarketStall", MouseFilter = MouseFilterEnum.Ignore }, _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        BuildBrowse();
        BuildStall();
        BuildConfirm();
        _status = Place(Label("", 18, HorizontalAlignment.Center), _canvas, 630, 833, 560, 42);
        _status.Hide();
        Resized += Fit;
        Fit();
        Hide();
    }

    private void BuildBrowse()
    {
        _columns = Place(new HBoxContainer { Name = "MarketStallColumns" }, _browse, 76, 222, 1520, 548);
        _columns.AddThemeConstantOverride("separation", 14);
        _totals = Place(Label("共计 0 件　　共计 0 灵石", 22), _browse, 82, 833, 510, 42);
        _clear = Place(Button("取消", 22), _browse, 1242, 826, 142, 56);
        _clear.Pressed += () => { _basket.Clear(); _quotes.Clear(); RefreshGoods(); };
        _buy = Place(Button("确认", 22, CraftingPaperStyle.ButtonKind.Primary), _browse, 1395, 826, 180, 56);
        _buy.Name = "BuyItem";
        _buy.Pressed += BuySelected;
    }

    private void BuildStall()
    {
        Place(Label("可上架物品", 26), _stall, 82, 229, 335, 40);
        _all = Place(Button("全部", 19), _stall, 82, 277, 75, 40);
        _carried = Place(Button("随身", 19), _stall, 163, 277, 75, 40);
        _stored = Place(Button("储物袋", 19), _stall, 244, 277, 96, 40);
        _all.Pressed += () => SetSourceFilter(0);
        _carried.Pressed += () => SetSourceFilter(1);
        _stored.Pressed += () => SetSourceFilter(2);
        _sourceSearch = Place(new LineEdit { Name = "InventorySearch", PlaceholderText = "搜索物品", ClearButtonEnabled = true }, _stall, 351, 277, 175, 40);
        CraftingPaperStyle.ApplyInput(_sourceSearch, 19, 8, 3);
        _sourceSearch.TextChanged += _ => RefreshSources();
        Place(Label("物品", 18), _stall, 85, 327, 260, 32);
        Place(Label("持有", 18, HorizontalAlignment.Center), _stall, 380, 327, 68, 32);
        Place(Label("体积", 18, HorizontalAlignment.Center), _stall, 452, 327, 68, 32);
        Rule(_stall, 82, 361, 444);
        _sourceScroll = Place(new YuxuMarketDropArea { Name = "MarketInventoryScroll", AcceptKind = "listing",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, _stall, 82, 367, 445, 406);
        _sourceScroll.OnDrop = WithdrawFromStall;
        _sources = new VBoxContainer { Name = "MarketInventoryList", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceScroll.AddChild(_sources);
        Rule(_stall, 546, 228, 1, 545);
        Place(Label("已上架货物", 26), _stall, 568, 229, 420, 40);
        _listingVolume = Place(Label("", 19, HorizontalAlignment.Right), _stall, 1260, 232, 310, 36);
        Place(Label("物品", 18), _stall, 576, 283, 650, 35);
        Place(Label("单件体积", 18, HorizontalAlignment.Center), _stall, 1276, 283, 114, 35);
        Place(Label("单价（灵石）", 18, HorizontalAlignment.Center), _stall, 1400, 283, 160, 35);
        Rule(_stall, 568, 322, 1004);
        _listingScroll = Place(new YuxuMarketDropArea { Name = "MarketListingsScroll", AcceptKind = "inventory",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, _stall, 568, 327, 1006, 446);
        _listingScroll.OnDrop = ListInventoryItem;
        _listings = new VBoxContainer { Name = "MarketListings", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _listingScroll.AddChild(_listings);
        _lease = Place(Label("", 22), _stall, 385, 139, 600, 43);
        _proceeds = Place(Label("", 22), _stall, 84, 833, 360, 42);
        _claim = Place(Button("领取所得", 22), _stall, 453, 826, 146, 56);
        _claim.Name = "ClaimMarketProceeds";
        _claim.Pressed += () => ClaimRequested?.Invoke();
        _rent = Place(Button("租用摊位", 22, CraftingPaperStyle.ButtonKind.Primary), _stall, 1385, 826, 190, 56);
        _rent.Name = "RentMarketStall";
        _rent.Pressed += ConfirmRentOrClose;
    }

    private void BuildConfirm()
    {
        _shade = Place(new ColorRect { Color = new Color(0, 0, 0, .44f), MouseFilter = MouseFilterEnum.Stop },
            _canvas, 0, 0, DesignSize.X, DesignSize.Y);
        _shade.Hide();
        var panel = new Panel { Name = "MarketConfirm" };
        panel.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(20));
        _dialog = Place(panel, _canvas, 562, 293, 548, 295);
        _dialog.Hide();
        _confirmText = Place(Label("", 23, HorizontalAlignment.Center), _dialog, 25, 28, 498, 167);
        _confirmText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var cancel = Place(Button("取消", 21), _dialog, 115, 219, 140, 46);
        var accept = Place(Button("确认", 21, CraftingPaperStyle.ButtonKind.Primary), _dialog, 293, 219, 140, 46);
        accept.Name = "ConfirmMarketAction";
        cancel.Pressed += CloseConfirm;
        accept.Pressed += () =>
        {
            var action = _confirm;
            CloseConfirm();
            if (Usable()) action?.Invoke(); else ShowStatus("坊市状态已变化。");
        };
    }

    public void Open(Character player, NpcWorldManager world, Func<bool> context)
    {
        _player = player; _world = world; _context = context;
        _basket.Clear(); _quotes.Clear(); _sellerScroll.Clear(); _showStall = false; _pageIndex = 0; _sourceFilter = 0;
        _search.Text = ""; _sourceSearch.Text = "";
        CloseConfirm(); EnsureStallNumbers(); Refresh(); Show(); MoveToFront(); Fit();
    }
    private bool Usable() => _player is not null && GodotObject.IsInstanceValid(_player) && _context?.Invoke() == true;
    private long Now() => GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0;
    public void ShowStatus(string text) { _status.Text = text; _status.Visible = !string.IsNullOrWhiteSpace(text); }
    private void Run(Func<string> action)
    {
        if (!Usable()) { ShowStatus("坊市状态已变化。"); return; }
        var message = action(); Changed?.Invoke(); Refresh(); ShowStatus(message);
    }
    private void Confirm(string title, string text, Action action)
    { _confirmText.Text = title + "\n\n" + text; _confirm = action; _confirming = true; _shade.Show(); _dialog.Show(); }
    private void CloseConfirm() { _confirming = false; _confirm = null; _shade?.Hide(); _dialog?.Hide(); }

    public void Refresh()
    {
        if (_player?.entity_data is not HumanData human || _world is null) return;
        EnsureStallNumbers();
        _browse.Visible = !_showStall;
        _stall.Visible = _showStall;
        _search.Visible = _previous.Visible = _next.Visible = _page.Visible = !_showStall;
        PaperButtonFeedback.Apply(_browseTab, CraftingPaperStyle.ButtonKind.Regular, 22, !_showStall);
        PaperButtonFeedback.Apply(_stallTab, CraftingPaperStyle.ButtonKind.Regular, 22, _showStall);
        _balance.Text = $"灵石　{_player.get_inventory_total_count(YuxuMarketRules.StonePath)}";
        if (_showStall) RefreshStall(human); else RefreshGoods();
    }

    private List<YuxuMarketRules.Offer> CurrentOffers() => _world is null ? [] :
        YuxuMarketRules.Offers(_world.get_subscene_roster(YuxuMarketRules.NodeId, YuxuMarketRules.PeakId));


    private void EnsureStallNumbers()
    {
        if (_world is null || _player?.entity_data is not HumanData human) return;
        var used = _world.npcs.Where(npc => npc is not null && npc.yuxu_market_stall_number > 0)
            .Select(npc => npc.yuxu_market_stall_number).ToHashSet();
        if (human.yuxu_market_stall_number > 0) used.Add(human.yuxu_market_stall_number);
        int next = 1;
        int Allocate() { while (used.Contains(next)) next++; used.Add(next); return next++; }
        bool changed = false;
        foreach (var seller in CurrentOffers().Select(offer => offer.Seller).Distinct().OrderBy(npc => npc.npc_id, StringComparer.Ordinal))
            if (seller.yuxu_market_stall_number <= 0) { seller.yuxu_market_stall_number = Allocate(); changed = true; }
        if (human.yuxu_market_lease_start_hour >= 0 && human.yuxu_market_stall_number <= 0)
        { human.yuxu_market_stall_number = Allocate(); changed = true; }
        if (changed) Changed?.Invoke();
    }

    private static string StallName(int number)
    {
        if (number <= 0) return "未分配";
        const string stems = "甲乙丙丁戊己庚辛壬癸", branches = "子丑寅卯辰巳午未申酉戌亥";
        int index = number - 1;
        string round = index / 120 == 0 ? "" : ChineseNumber(index / 120 + 1);
        return $"{stems[index % 10]}{branches[(index / 10) % 12]}{round}摊";
    }

    private static string ChineseNumber(int number)
    {
        if (number < 10) return "零一二三四五六七八九"[number].ToString();
        if (number < 100) return (number < 20 ? "" : ChineseNumber(number / 10)) + "十" + (number % 10 == 0 ? "" : ChineseNumber(number % 10));
        return string.Concat(number.ToString().Select(digit => "零一二三四五六七八九"[digit - '0']));
    }

    private void RefreshGoods()
    {
        if (_world is null || _player is null || _columns is null) return;
        var current = CurrentOffers();
        _offers.Clear();
        foreach (var offer in current) _offers[(offer.Seller.npc_id, offer.Path)] = offer;
        var unavailable = _basket.Where(pair =>
            !_quotes.TryGetValue(pair.Key, out var quote) || !_offers.TryGetValue(pair.Key, out var live) ||
            quote.UnitPrice != live.UnitPrice || pair.Value > live.Quantity).Select(pair => pair.Key).ToArray();
        foreach (var key in unavailable) { _basket.Remove(key); _quotes.Remove(key); }
        if (unavailable.Length > 0) ShowStatus("部分摊货或价格已变化，请重新选择。");
        string query = _search.Text.Trim();
        var groups = current.GroupBy(offer => offer.Seller).OrderBy(group => group.Key.yuxu_market_stall_number)
            .Select(group =>
            {
                bool matchesStall = StallName(group.Key.yuxu_market_stall_number).Contains(query, StringComparison.OrdinalIgnoreCase);
                var goods = group.Where(offer => matchesStall ||
                    (YuxuMarketRules.Item(offer.Path)?.entity_name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
                return new SellerGroup(group.Key, goods);
            }).Where(group => group.Offers.Count > 0).ToList();
        int pages = Math.Max(1, (groups.Count + StallsPerPage - 1) / StallsPerPage);
        _pageIndex = Math.Clamp(_pageIndex, 0, pages - 1);
        _previous.Disabled = _pageIndex == 0; _next.Disabled = _pageIndex == pages - 1; _page.Text = $"{_pageIndex + 1} / {pages}";
        foreach (var column in _columns.GetChildren().OfType<Control>())
            if (column.HasMeta("seller") && column.GetNodeOrNull<ScrollContainer>("SellerGoodsScroll") is { } oldScroll)
                _sellerScroll[column.GetMeta("seller").AsString()] = oldScroll.ScrollVertical;
        _refreshing = true;
        try { ClearChildren(_columns); }
        finally { _refreshing = false; }
        if (groups.Count == 0)
        {
            var empty = new Control { CustomMinimumSize = new(1488, 500) };
            _columns.AddChild(empty);
            Place(Label(query.Length == 0 ? "眼下没有可交易的货物" : "没有找到对应物品或摊位", 24, HorizontalAlignment.Center), empty, 150, 170, 1180, 45);
        }
        foreach (var group in groups.Skip(_pageIndex * StallsPerPage).Take(StallsPerPage)) BuildSellerColumn(group);
        RefreshTotals();
    }

    private void BuildSellerColumn(SellerGroup group)
    {
        var column = new Control { Name = "SellerStall", CustomMinimumSize = new(497, 548), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.SetMeta("seller", group.Seller.npc_id);
        _columns.AddChild(column);
        var header = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeStyleboxOverride("panel", Paper("e9dac0", "b49a76"));
        Place(header, column, 3, 0, 490, 49);
        Place(Label(StallName(group.Seller.yuxu_market_stall_number), 23), column, 17, 4, 190, 40);
        Place(Label(YuxuMarketRules.SellerName(group.Seller), 19, HorizontalAlignment.Right), column, 274, 4, 204, 40);
        Place(Label("物品", 20), column, 17, 54, 165, 32);
        Place(Label("已选", 20, HorizontalAlignment.Center), column, 223, 54, 125, 32);
        Place(Label("库存", 20, HorizontalAlignment.Right), column, 350, 54, 58, 32);
        Place(Label("单价", 20, HorizontalAlignment.Right), column, 411, 54, 65, 32);
        Rule(column, 13, 87, 470);
        var scroll = Place(new ScrollContainer { Name = "SellerGoodsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, column, 10, 93, 477, 448);
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 2); scroll.AddChild(rows);
        foreach (var offer in group.Offers) BuildOfferRow(rows, offer);
        if (_sellerScroll.TryGetValue(group.Seller.npc_id, out int oldPosition))
            Callable.From(() => { if (GodotObject.IsInstanceValid(scroll)) scroll.ScrollVertical = oldPosition; }).CallDeferred();
    }

    private void BuildOfferRow(VBoxContainer rows, YuxuMarketRules.Offer offer)
    {
        var item = YuxuMarketRules.Item(offer.Path);
        if (item is null) return;
        var key = (offer.Seller.npc_id, offer.Path);
        int quantity = _basket.GetValueOrDefault(key);
        var row = new Button { Name = "MarketOffer", CustomMinimumSize = new(463, 56), ClipContents = true };
        RowStyle(row, quantity > 0); rows.AddChild(row);
        Place(new TextureRect { Texture = YuxuLibraryArt.Texture(item.icon_path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore }, row, 8, 8, 38, 38);
        Place(Label(item.entity_name, 21), row, 52, 3, 165, 49);
        Place(Label(offer.Quantity.ToString(), 21, HorizontalAlignment.Right), row, 346, 3, 53, 49);
        Place(Label(offer.UnitPrice.ToString(), 21, HorizontalAlignment.Right), row, 410, 3, 51, 49);
        HoverInfoManager.SetProvider(row, () => StoragePanel.ItemHover(item, offer.Quantity, offer.Path, _player));
        var minus = Place(new YuxuMarketQuantityStep { Increase = false }, row, 219, 6.5f, 33, 43);
        var input = Place(new LineEdit { Text = Math.Max(1, quantity).ToString(), Alignment = HorizontalAlignment.Center,
            MaxLength = 9, Name = "SelectedQuantity" }, row, 252, 6.5f, 57, 43);
        CraftingPaperStyle.ApplyInput(input, 21, 4, 2);
        var plus = Place(new YuxuMarketQuantityStep { Increase = true }, row, 310, 6.5f, 33, 43);
        void ShowSelection(bool selected)
        {
            RowStyle(row, selected);
            minus.Visible = input.Visible = plus.Visible = selected;
        }
        void SetValue(int value)
        {
            if (value <= 0)
            {
                _basket.Remove(key);
                _quotes.Remove(key);
                ShowSelection(false);
            }
            else
            {
                int bounded = Math.Clamp(value, 1, offer.Quantity);
                _basket[key] = bounded;
                _quotes[key] = offer;
                if (input.Text != bounded.ToString()) input.Text = bounded.ToString();
                ShowSelection(true);
            }
            RefreshTotals();
        }
        int ReadValue() => long.TryParse(input.Text, out var value)
            ? (int)Math.Clamp(value, 1, offer.Quantity) : _basket.GetValueOrDefault(key, 1);
        row.Pressed += () => { if (!_basket.ContainsKey(key)) SetValue(1); };
        minus.Pressed += () => SetValue(ReadValue() - 1);
        plus.Pressed += () => SetValue(ReadValue() + 1);
        input.TextChanged += text =>
        {
            if (long.TryParse(text, out var value) && value > 0)
            {
                _basket[key] = (int)Math.Clamp(value, 1, offer.Quantity);
                RefreshTotals();
            }
        };
        void CommitInput() { if (!_refreshing && _basket.ContainsKey(key)) SetValue(ReadValue()); }
        input.TextSubmitted += _ => CommitInput();
        input.FocusExited += CommitInput;
        ShowSelection(quantity > 0);
    }

    private void RefreshTotals()
    {
        long quantity = 0, cost = 0;
        foreach (var pair in _basket)
        {
            quantity += pair.Value;
            if (_quotes.TryGetValue(pair.Key, out var offer)) cost += (long)offer.UnitPrice * pair.Value;
        }
        _totals.Text = $"共计 {quantity} 件　　共计 {cost} 灵石";
        _buy.Disabled = _basket.Count == 0; _clear.Disabled = _basket.Count == 0;
    }

    private void BuySelected()
    {
        if (!Usable() || _player is null || _world is null) return;
        var selected = new List<YuxuMarketRules.Purchase>();
        foreach (var (key, count) in _basket)
        {
            if (!_quotes.TryGetValue(key, out var offer) || !_offers.TryGetValue(key, out var current)
                || current.UnitPrice != offer.UnitPrice || _world.get_npc(key.Seller) != offer.Seller)
            { ShowStatus("摊货已变化，请重新选择。"); RefreshGoods(); return; }
            selected.Add(new(offer, count));
        }
        if (selected.Count == 0) return;
        if (!YuxuMarketRules.TryBuyBatch(_player, selected, Now(), out var message))
        { ShowStatus(message); RefreshGoods(); return; }
        _basket.Clear(); _quotes.Clear(); Changed?.Invoke(); Refresh(); ShowStatus(message);
    }


    private void SetSourceFilter(int value) { _sourceFilter = value; RefreshSources(); }

    private List<InventoryChoice> AvailableInventory()
    {
        if (_player is null) return [];
        var bags = _player.get_storage_bags().ToDictionary(bag => bag.InstanceId, bag => bag.DisplayName, StringComparer.Ordinal);
        string search = _sourceSearch.Text.Trim();
        return _player.get_accessible_spatial_inventory()
            .Where(entry => entry.Item is not null && !entry.IsEquipped && _player.can_store_spatial_item(entry.InstanceId)
                && YuxuMarketRules.ReferencePrice(entry.ItemPath) > 0)
            .Where(entry => _sourceFilter == 0 || (_sourceFilter == 1) == (entry.ContainerId.Length == 0))
            .Where(entry => entry.Item!.entity_name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => (entry.ContainerId, entry.ItemPath))
            .Select(group => new InventoryChoice(group.Key.ItemPath, group.Sum(item => item.Count), group.Key.ContainerId,
                group.Key.ContainerId.Length == 0 ? "随身" : bags.GetValueOrDefault(group.Key.ContainerId, "储物袋"), group.First().InstanceId))
            .OrderBy(entry => entry.SourceName, StringComparer.CurrentCulture)
            .ThenBy(entry => YuxuMarketRules.Item(entry.Path)?.entity_name, StringComparer.CurrentCulture).ToList();
    }

    private void RefreshStall(HumanData human)
    {
        bool active = YuxuMarketRules.Active(human, Now());
        long remaining = Math.Max(0, human.yuxu_market_lease_end_hour - Now());
        _lease.Text = active ? $"{StallName(human.yuxu_market_stall_number)}　租期剩余 {Math.Max(1, (remaining + 23) / 24)} 日" : "尚未租用摊位";
        _proceeds.Text = $"待领灵石　{human.yuxu_market_pending_stones}";
        _claim.Disabled = human.yuxu_market_pending_stones <= 0;
        _rent.Text = active ? "结束摆摊" : "租用摊位";
        RefreshSources(); RefreshListings(human, active);
    }

    private void RefreshSources()
    {
        if (_player?.entity_data is not HumanData human || _sources is null) return;
        bool active = YuxuMarketRules.Active(human, Now());
        int scroll = _sourceScroll.ScrollVertical;
        ClearChildren(_sources);
        foreach (var entry in AvailableInventory())
        {
            var item = YuxuMarketRules.Item(entry.Path);
            if (item is null) continue;
            var row = new YuxuMarketDragRow { Name = "MarketInventoryItem", CustomMinimumSize = new(430, 52),
                ItemTexture = YuxuLibraryArt.Texture(item.icon_path), ItemName = item.entity_name, Disabled = !active,
                Payload = new D { ["kind"] = "inventory", ["path"] = entry.Path, ["container"] = entry.ContainerId, ["instance"] = entry.InstanceId } };
            RowStyle(row, false); _sources.AddChild(row);
            Place(new TextureRect { Texture = row.ItemTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore }, row, 8, 6, 36, 36);
            Place(Label(item.entity_name, 19), row, 52, 3, 185, 44);
            Place(Label(entry.SourceName, 15), row, 238, 3, 68, 44);
            Place(Label(entry.Count.ToString(), 19, HorizontalAlignment.Center), row, 298, 3, 68, 44);
            Place(Label(item.get_storage_volume().ToString(), 19, HorizontalAlignment.Center), row, 370, 3, 60, 44);
            HoverInfoManager.SetProvider(row, () => StoragePanel.ItemHover(item, entry.Count, entry.Path, _player));
        }
        if (_sources.GetChildCount() == 0) _sources.AddChild(Label("暂无可上架物品", 19, HorizontalAlignment.Center));
        _sourceScroll.ScrollVertical = scroll;
        PaperButtonFeedback.Apply(_all, CraftingPaperStyle.ButtonKind.Regular, 19, _sourceFilter == 0);
        PaperButtonFeedback.Apply(_carried, CraftingPaperStyle.ButtonKind.Regular, 19, _sourceFilter == 1);
        PaperButtonFeedback.Apply(_stored, CraftingPaperStyle.ButtonKind.Regular, 19, _sourceFilter == 2);
    }

    private void RefreshListings(HumanData human, bool active)
    {
        int scroll = _listingScroll.ScrollVertical;
        ClearChildren(_listings);
        long volume = 0;
        foreach (var listing in human.yuxu_market_listings.Where(entry => entry.GetValueOrDefault("quantity", 0).AsInt32() > 0))
        {
            string id = listing.GetValueOrDefault("id", "").AsString(), path = listing.GetValueOrDefault("item_path", "").AsString();
            int count = listing.GetValueOrDefault("quantity", 0).AsInt32();
            var item = YuxuMarketRules.Item(path);
            if (item is null) continue;
            volume += (long)item.get_storage_volume() * count;
            var row = new Control { Name = "MarketListingControl", CustomMinimumSize = new(986, 54) };
            _listings.AddChild(row);
            var drag = Place(new YuxuMarketDragRow { Name = "MarketPlacedItem", ItemTexture = YuxuLibraryArt.Texture(item.icon_path),
                ItemName = item.entity_name, Payload = new D { ["kind"] = "listing", ["id"] = id } }, row, 0, 0, 818, 52);
            RowStyle(drag, false);
            Place(new TextureRect { Texture = drag.ItemTexture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore }, drag, 8, 6, 38, 38);
            Place(Label(item.entity_name + (count > 1 ? $" ×{count}" : ""), 20), drag, 56, 3, 620, 46);
            Place(Label(item.get_storage_volume().ToString(), 19, HorizontalAlignment.Center), drag, 708, 3, 106, 46);
            HoverInfoManager.SetProvider(drag, () => StoragePanel.ItemHover(item, count, path, _player));
            var price = Place(new LineEdit { Text = listing.GetValueOrDefault("unit_price", 1).AsInt32().ToString(),
                Alignment = HorizontalAlignment.Center, Editable = active, MaxLength = 6, Name = "ListingPrice" }, row, 835, 5, 126, 42);
            CraftingPaperStyle.ApplyInput(price, 20, 7, 2);
            bool committed = false;
            void CommitPrice()
            {
                if (committed || !active) return;
                committed = true;
                int old = listing.GetValueOrDefault("unit_price", 1).AsInt32();
                if (!int.TryParse(price.Text, out var value) || value < 1 || value > 999999)
                { price.Text = old.ToString(); ShowStatus("单价须为 1 至 999999 的整数。"); return; }
                if (value != old) Run(() => { YuxuMarketRules.TryReprice(human, id, value, Now(), out var message); return message; });
            }
            price.TextSubmitted += _ => { CommitPrice(); if (price.IsInsideTree()) price.ReleaseFocus(); };
            price.FocusExited += CommitPrice; price.FocusEntered += () => committed = false;
            Rule(row, 0, 53, 970);
        }
        if (_listings.GetChildCount() == 0) _listings.AddChild(Label("摊上还没有物品", 19, HorizontalAlignment.Center));
        _listingVolume.Text = $"已用体积 {volume}"; _listingScroll.ScrollVertical = scroll;
    }

    private void ListInventoryItem(D payload)
    {
        if (!Usable() || _player is null) return;
        string path = payload.GetValueOrDefault("path", "").AsString(), container = payload.GetValueOrDefault("container", "").AsString();
        string instance = payload.GetValueOrDefault("instance", "").AsString();
        Run(() =>
        {
            string message;
            if (container.Length > 0) YuxuMarketRules.TryListStorageItem(_player, container, instance, path, Now(), out message);
            else YuxuMarketRules.TryList(_player, path, 1, YuxuMarketRules.ReferencePrice(path), Now(), out message);
            return message;
        });
    }

    private void WithdrawFromStall(D payload)
    {
        if (_player is null) return;
        string id = payload.GetValueOrDefault("id", "").AsString();
        Run(() => { YuxuMarketRules.TryWithdrawOne(_player, id, out var message); return message; });
    }

    private void ConfirmRentOrClose()
    {
        if (_player?.entity_data is not HumanData human) return;
        if (YuxuMarketRules.Active(human, Now()))
            Confirm("结束摆摊", "已付租金不退，未售物品与所得仍可领取。", () => Run(() =>
                { YuxuMarketRules.TryClose(human, Now(), out var message); return message; }));
        else Confirm("租用摊位", "租金 1 灵石 / 月，到期不自动续租。", () => Run(() =>
            { YuxuMarketRules.TryRent(_player, Now(), out var message); return message; }));
    }

    private static void ClearChildren(Node parent)
    { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private static T Place<T>(T child, Control parent, float x, float y, float width, float height) where T : Control
    { parent.AddChild(child); child.Position = new(x, y); child.Size = new(width, height); return child; }
    private static Label Label(string value, int size, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = CraftingPaperStyle.Text(value, size);
        label.HorizontalAlignment = alignment; label.VerticalAlignment = VerticalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore; label.ClipText = true; return label;
    }
    private static Button Button(string text, int size, CraftingPaperStyle.ButtonKind kind = CraftingPaperStyle.ButtonKind.Regular)
    {
        var button = new Button { Text = text, MouseDefaultCursorShape = CursorShape.PointingHand };
        PaperButtonFeedback.Apply(button, kind, size); return button;
    }
    private static StyleBoxFlat Paper(string background, string border) => new()
    {
        BgColor = new Color(background), BorderColor = new Color(border),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1
    };
    private static void RowStyle(Button row, bool selected)
    {
        row.AddThemeStyleboxOverride("normal", Paper(selected ? "d7c5aa" : "f1e5d2", "ded0ba"));
        row.AddThemeStyleboxOverride("hover", Paper(selected ? "cfb99a" : "e6d6be", "c4ad8d"));
        row.AddThemeStyleboxOverride("pressed", Paper("d6c1a4", "b9a080"));
        row.AddThemeStyleboxOverride("disabled", Paper("e6ddd0", "d5caba"));
        row.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }
    private static void Rule(Control parent, float x, float y, float width, float height = 1) =>
        Place(new ColorRect { Color = new Color("b7a588"), MouseFilter = MouseFilterEnum.Ignore }, parent, x, y, width, height);
    private void Fit()
    {
        var size = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size;
        float scale = Math.Min(size.X / DesignSize.X, size.Y / DesignSize.Y) * .97f;
        _canvas.Scale = Vector2.One * scale; _canvas.Position = (size - DesignSize * scale) * .5f;
    }
    private void HandleBackButton() { if (_confirming) CloseConfirm(); else BackRequested?.Invoke(); }
    public bool HandleBack() { if (!Visible) return false; HandleBackButton(); return true; }
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || _context?.Invoke() != true || input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        HandleBack(); GetViewport().SetInputAsHandled();
    }
}

