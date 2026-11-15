using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Run-local market: finite pill/artifact stock and a per-shop material buyback cap.</summary>
public partial class RoguelikeShopPanel : Control
{
    private Character _player = null!;
    private Dictionary _instance = [];
    private string _nodeId = "";
    private int _layer;
    private Action? _changed;
    private VBoxContainer _content = null!;
    private Label _balance = null!, _buyback = null!;
    private Label _noticeLabel = null!;
    private bool _showBuying = true;
    private string _notice = "";

    public static RoguelikeShopPanel ShowFor(Node owner, Character player, Dictionary instance,
        string nodeId, int layer, Action changed)
    {
        var panel = new RoguelikeShopPanel
        {
            Name = "RoguelikeShopPanel",
            _player = player,
            _instance = instance,
            _nodeId = nodeId,
            _layer = layer,
            _changed = changed,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 240,
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        owner.AddChild(panel);
        panel.Build();
        return panel;
    }

    private void Build()
    {
        var veil = new ColorRect { Color = new Color(.02f, .025f, .02f, .72f), MouseFilter = MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);

        var paper = new PanelContainer { Name = "RoguelikeMarketPaper", CustomMinimumSize = new Vector2(960, 680), MouseFilter = MouseFilterEnum.Stop };
        paper.SetAnchorsPreset(LayoutPreset.Center);
        paper.OffsetLeft = -480; paper.OffsetTop = -340; paper.OffsetRight = 480; paper.OffsetBottom = 340;
        paper.AddThemeStyleboxOverride("panel", PaperStyle());
        AddChild(paper);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        paper.AddChild(root);

        var header = new HBoxContainer(); root.AddChild(header);
        var title = new Label { Text = $"秘境商栈 · 第{_layer}层", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 27); title.AddThemeColorOverride("font_color", Ink()); header.AddChild(title);
        var close = Button("离开商栈", 16); close.Pressed += QueueFree; header.AddChild(close);

        var summary = new HBoxContainer(); summary.AddThemeConstantOverride("separation", 22); root.AddChild(summary);
        _balance = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        _balance.AddThemeFontSizeOverride("font_size", 17); _balance.AddThemeColorOverride("font_color", Ink()); summary.AddChild(_balance);
        _buyback = new Label { VerticalAlignment = VerticalAlignment.Center };
        _buyback.Name = "BuybackBudgetLabel"; _buyback.AddThemeFontSizeOverride("font_size", 16); _buyback.AddThemeColorOverride("font_color", QuietInk()); summary.AddChild(_buyback);

        var intro = new Label
        {
            Text = "本局灵石只在秘境内流通。每栈货单固定；可出售材料换取灵石，丹药与法器售罄后不补。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 32),
        };
        intro.AddThemeColorOverride("font_color", QuietInk()); root.AddChild(intro);

        var tabs = new HBoxContainer(); tabs.AddThemeConstantOverride("separation", 8); root.AddChild(tabs);
        var buyTab = Button("购入丹药与法器", 16); buyTab.SizeFlagsHorizontal = SizeFlags.ExpandFill; tabs.AddChild(buyTab);
        var sellTab = Button("出售材料换灵石", 16); sellTab.SizeFlagsHorizontal = SizeFlags.ExpandFill; tabs.AddChild(sellTab);
        buyTab.Pressed += () => { _showBuying = true; Refresh(); };
        sellTab.Pressed += () => { _showBuying = false; Refresh(); };

        _noticeLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 25) };
        _noticeLabel.AddThemeFontSizeOverride("font_size", 15); _noticeLabel.AddThemeColorOverride("font_color", new Color("72553a")); root.AddChild(_noticeLabel);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 9); scroll.AddChild(_content);
        Refresh();
    }

    private void Refresh()
    {
        if (_content is null) return;
        _balance.Text = $"持有灵石：{_player.get_inventory_total_count(RoguelikeShopRules.StonePath)} 枚";
        _buyback.Text = $"本栈材料收购余额：{RoguelikeShopRules.GetBuybackRemaining(_instance, _nodeId)} 灵石";
        _noticeLabel.Text = _notice;
        foreach (var child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
        if (_showBuying) BuildBuyList(); else BuildSellList();
    }

    private void BuildBuyList()
    {
        var offers = RoguelikeShopRules.GetOffers(_instance, _nodeId);
        if (offers.Count == 0)
        {
            AddEmptyMessage("本栈货物已经售罄。");
            return;
        }
        foreach (var offer in offers)
        {
            var isArtifact = offer.Item is SectArtifactOfferData;
            var paperRow = MakeRow(out var row);
            var effect = offer.Item is PillItemData pill ? pill.EffectText : offer.Item.description;
            var description = new Label
            {
                Text = $"{(isArtifact ? "法器" : "丹药")}　{offer.Item.entity_name}\n{effect}\n单价 {offer.UnitPrice} 灵石　·　库存 {offer.Quantity}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 68),
            };
            description.AddThemeColorOverride("font_color", Ink()); description.AddThemeFontSizeOverride("font_size", 16); row.AddChild(description);
            var affordable = Math.Max(0, _player.get_inventory_total_count(RoguelikeShopRules.StonePath) / offer.UnitPrice);
            var maxQuantity = isArtifact ? 1 : Math.Min(offer.Quantity, affordable);
            var quantity = new SpinBox { MinValue = 1, MaxValue = Math.Max(1, maxQuantity), Value = 1, Step = 1, CustomMinimumSize = new Vector2(90, 40) };
            row.AddChild(quantity);
            var buy = Button(isArtifact ? "购入法器" : "购入丹药", 15);
            buy.Disabled = affordable < 1;
            buy.Pressed += () => Buy(offer.ItemPath, (int)Math.Round(quantity.Value));
            row.AddChild(buy);
            _content.AddChild(paperRow);
        }
    }

    private void BuildSellList()
    {
        var budget = RoguelikeShopRules.GetBuybackRemaining(_instance, _nodeId);
        var materials = _player.pack_items
            .Where(path => ResourceLoader.Load<ItemData>(path) is { } item && RoguelikeShopRules.IsSellableMaterial(item, path))
            .Distinct(StringComparer.Ordinal)
            .Select(path => (Path: path, Item: ResourceLoader.Load<ItemData>(path)!, Count: _player.get_inventory_total_count(path)))
            .Where(entry => entry.Count > 0 && RoguelikeShopRules.SellUnitPrice(entry.Item) > 0)
            .OrderBy(entry => entry.Item.entity_name, StringComparer.Ordinal)
            .ToArray();
        if (budget <= 0)
        {
            AddEmptyMessage("本栈收购额度已用完；可前往后续商栈交易。");
            return;
        }
        if (materials.Length == 0)
        {
            AddEmptyMessage("行囊里没有可出售的材料。");
            return;
        }
        foreach (var entry in materials)
        {
            var price = RoguelikeShopRules.SellUnitPrice(entry.Item);
            var maxQuantity = Math.Min(entry.Count, budget / price);
            var paperRow = MakeRow(out var row);
            var description = new Label
            {
                Text = $"{entry.Item.entity_name}　×{entry.Count}\n收购价 {price} 灵石 / 份",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 52),
            };
            description.AddThemeColorOverride("font_color", Ink()); description.AddThemeFontSizeOverride("font_size", 16); row.AddChild(description);
            var quantity = new SpinBox { MinValue = 1, MaxValue = Math.Max(1, maxQuantity), Value = 1, Step = 1, CustomMinimumSize = new Vector2(90, 40), Editable = maxQuantity >= 1 };
            row.AddChild(quantity);
            var sell = Button("出售", 15); sell.Disabled = maxQuantity < 1;
            sell.Pressed += () => Sell(entry.Path, (int)Math.Round(quantity.Value));
            row.AddChild(sell);
            _content.AddChild(paperRow);
        }
    }

    private void Buy(string itemPath, int quantity)
    {
        if (RoguelikeShopRules.TryBuy(_player, _instance, _nodeId, itemPath, quantity, out var message)) _changed?.Invoke();
        _notice = message;
        Refresh();
    }

    private void Sell(string itemPath, int quantity)
    {
        if (RoguelikeShopRules.TrySell(_player, _instance, _nodeId, itemPath, quantity, out var message)) _changed?.Invoke();
        _notice = message;
        Refresh();
    }

    private void AddEmptyMessage(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 58), VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", QuietInk()); label.AddThemeFontSizeOverride("font_size", 18); _content.AddChild(label);
    }

    private static PanelContainer MakeRow(out HBoxContainer row)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", SectionStyle());
        row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);
        return panel;
    }

    private static Button Button(string text, int size)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(142, 40), FocusMode = FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", size); button.AddThemeColorOverride("font_color", Ink());
        button.AddThemeStyleboxOverride("normal", SectionStyle());
        button.AddThemeStyleboxOverride("hover", SectionStyle(new Color(.86f, .79f, .64f, .94f)));
        button.AddThemeStyleboxOverride("pressed", SectionStyle(new Color(.72f, .65f, .52f, .98f)));
        return button;
    }

    private static StyleBoxFlat PaperStyle()
    {
        var style = new StyleBoxFlat { BgColor = new Color("e5d8b7"), BorderColor = new Color("715e43"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 };
        style.SetBorderWidthAll(2); style.ContentMarginLeft = 22; style.ContentMarginRight = 22; style.ContentMarginTop = 18; style.ContentMarginBottom = 18; return style;
    }

    private static StyleBoxFlat SectionStyle(Color? color = null)
    {
        var style = new StyleBoxFlat { BgColor = color ?? new Color(.91f, .86f, .73f, .72f), BorderColor = new Color("9d8865"), CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7 };
        style.SetBorderWidthAll(1); style.ContentMarginLeft = 10; style.ContentMarginRight = 10; style.ContentMarginTop = 7; style.ContentMarginBottom = 7; return style;
    }

    private static Color Ink() => new("3a3023");
    private static Color QuietInk() => new("5e523f");
}
