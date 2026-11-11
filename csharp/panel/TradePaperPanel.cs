using Godot;

/// <summary>正式世界人物交易的纸墨界面；估值与成交仍复用 TradePanel 原流程。</summary>
public partial class TradePaperPanel : TradePanel
{
    private const string LastValue = "trade_paper_last_value";
    private Control _canvas = null!;
    private GridContainer _playerRows = null!, _npcRows = null!;
    private VBoxContainer _outgoingRows = null!, _incomingRows = null!;
    private Label _counterparty = null!;
    private Label _dateLabel = null!;
    private Label _tradeTermsLabel = null!;
    private Label _attitudeLabel = null!;
    private WorldNpcData? _previewNpc;
    public Control DesignCanvas => _canvas;
    public Button CommitButton { get; private set; } = null!;
    public override void _Ready()
    {
        base._Ready(); Theme = CraftingPaperStyle.MakeTheme();
        var backdrop = GetChildren().OfType<TextureRect>().Single();
        backdrop.Texture = new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>(CraftingPaperStyle.Assets + "frame_atlas.png"),
            Region = new Rect2(110, 285, 850, 140), FilterClip = true
        };
        _canvas = GetNode<Control>("MaterialCanvas");
        var margin = _canvas.GetChildren().OfType<MarginContainer>().Single();
        margin.AddThemeConstantOverride("margin_left", 24); margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 18);
        var root = margin.GetChildren().OfType<VBoxContainer>().Single();
        var header = root.GetChildren().OfType<HBoxContainer>().First(); header.Name = "TradePaperHeader";
        header.CustomMinimumSize = new(0, 46); header.AddThemeConstantOverride("separation", 22);
        int headerIndex = header.GetIndex(); root.RemoveChild(header);
        var headerInset = new MarginContainer { Name = "TradePaperHeaderInset" };
        headerInset.AddThemeConstantOverride("margin_left", 12);
        headerInset.AddThemeConstantOverride("margin_right", 136);
        root.AddChild(headerInset); root.MoveChild(headerInset, headerIndex); headerInset.AddChild(header);
        header.GetChildren().OfType<Button>().Single().Hide();
        foreach (var label in header.GetChildren().OfType<Label>())
        {
            label.AddThemeFontSizeOverride("font_size", 22); label.VerticalAlignment = VerticalAlignment.Center;
            label.CustomMinimumSize = new(0, 46); label.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        }
        _counterparty = header.GetChildren().OfType<Label>().Last();
        var date = CraftingPaperStyle.Text(CurrentDateText(), 22);
        date.Name = "TradeDate"; date.VerticalAlignment = VerticalAlignment.Center; date.CustomMinimumSize = new(380, 46);
        date.HorizontalAlignment = HorizontalAlignment.Right; header.AddChild(date); _dateLabel = date;
        var back = new Button { Name = "TradeReturn", Text = "返回", CustomMinimumSize = new(114, 46) };
        PaperButtonFeedback.Apply(back); _canvas.AddChild(back); CraftingPaperHeader.PlaceReturn(_canvas, back); back.Pressed += close;
        CraftingPaperHeader.AddReturnArrow(back);
        var headerPaper = new Panel { Name = "TradeHeaderPaper", MouseFilter = MouseFilterEnum.Ignore, Position = new(24, 18), Size = new(1624, 46) };
        headerPaper.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(0, true, .97f));
        _canvas.AddChild(headerPaper); _canvas.MoveChild(headerPaper, 0);
        var columns = (HBoxContainer)FindChild("TradeColumns", true, false);
        int index = 0;
        foreach (var column in columns.GetChildren().OfType<PanelContainer>())
        {
            column.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(18, index is 0 or 3, .97f));
            column.Name = "TradePaperColumn_" + index++;
        }
        _playerRows = (GridContainer)FindChild("PlayerTradeList", true, false);
        _npcRows = (GridContainer)FindChild("NpcTradeList", true, false);
        _outgoingRows = (VBoxContainer)FindChild("OutgoingTradeList", true, false);
        _incomingRows = (VBoxContainer)FindChild("IncomingTradeList", true, false);
        CommitButton = (Button)FindChild("CommitTrade", true, false);
        PaperButtonFeedback.Apply(CommitButton, CraftingPaperStyle.ButtonKind.Primary, 22);
        CommitButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var footer = CommitButton.GetParent<HBoxContainer>();
        var summary = footer.GetChildren().OfType<VBoxContainer>().Single();
        _tradeTermsLabel = summary.GetChildren().OfType<Label>().Last();
        var attitude = footer.GetChildren().OfType<Label>().Single();
        _attitudeLabel = attitude;
        footer.RemoveChild(attitude);
        attitude.Name = "TradeAttitude";
        attitude.CustomMinimumSize = new(280, 58);
        attitude.HorizontalAlignment = HorizontalAlignment.Center;
        attitude.VerticalAlignment = VerticalAlignment.Center;
        attitude.AddThemeFontSizeOverride("font_size", 30);
        attitude.MouseFilter = MouseFilterEnum.Ignore;
        int footerIndex = footer.GetIndex(); root.RemoveChild(footer);
        var footerPaper = new PanelContainer { Name = "TradeFooterPaper" };
        footerPaper.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(12, true, .97f));
        root.AddChild(footerPaper); root.MoveChild(footerPaper, footerIndex);
        var footerContent = new Control { Name = "TradeFooterContent", CustomMinimumSize = new(0, 82) };
        footerPaper.AddChild(footerContent);
        footer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); footerContent.AddChild(footer);
        var attitudeCenter = new CenterContainer { Name = "TradeAttitudeCenter", MouseFilter = MouseFilterEnum.Ignore };
        attitudeCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        footerContent.AddChild(attitudeCenter); attitudeCenter.AddChild(attitude);
        var notice = root.GetChildren().OfType<PanelContainer>().First(panel => panel != footerPaper);
        notice.Name = "TradePaperNotice";
        notice.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(12, false, .97f));
        foreach (var label in notice.GetChildren().OfType<Label>()) label.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
    }
    public new void show_for(Character player, WorldNpcData npc)
    { _previewNpc = npc; base.show_for(player, npc); SyncPaper(); }
    public void BindPreview(Character player, WorldNpcData npc) => show_for(player, npc);
    private string CurrentDateText() => LetterMailbox.Date(GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.get_total_game_hours() ?? 0);
    private static IEnumerable<T> All<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        { if (child is T match) yield return match; foreach (var nested in All<T>(child)) yield return nested; }
    }
    public Button SourceRow(bool playerSide, string itemName) => (playerSide ? _playerRows : _npcRows).GetChildren().OfType<Button>()
        .First(row => row.FindChild("ItemName", true, false) is Label { } label && label.Text == itemName);
    public SpinBox Quantity(bool playerSide, string itemName) => All<SpinBox>(SourceRow(playerSide, itemName)).Single();
    public QuietRoomNumber NumberControl(bool playerSide, string itemName) => All<QuietRoomNumber>(SourceRow(playerSide, itemName)).Single();
    public void ClearQuantities()
    { foreach (var picker in All<SpinBox>(_playerRows).Concat(All<SpinBox>(_npcRows))) picker.Value = 0; }
    private static void SkinRow(Button row, bool selected)
    {
        PaperButtonFeedback.Apply(row, selected: selected);
        // Selected rows use the shared warm-paper darkening; don't add a green tint.
        row.SelfModulate = Colors.White;
        foreach (var label in All<Label>(row))
        {
            label.AddThemeFontSizeOverride("font_size", 22);
            label.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        }
    }
    public void SyncPaper()
    {
        if (_playerRows is null) return;
        SimplifyTradeTerms();
        AddAttitudeSubject();
        var date = CurrentDateText();
        if (_dateLabel.Text != date) _dateLabel.Text = date;
        if (_previewNpc is { } npc)
        {
            var identity = NpcPublicIdentity.Title(npc);
            _counterparty.Text = string.Join(" · ", new[] { npc.npc_name, identity, npc.cultivation_realm }.Where(text => text.Length > 0));
        }
        foreach (var source in new[] { _playerRows, _npcRows })
        foreach (var row in source.GetChildren().OfType<Button>())
        {
            var picker = All<SpinBox>(row).Single();
            if (picker.GetParent() is HBoxContainer holder)
            {
                int childIndex = picker.GetIndex(); holder.RemoveChild(picker);
                var number = new QuietRoomNumber(picker) { Name = "TradePaperQuantity", CustomMinimumSize = new(92, 46), SizeFlagsVertical = SizeFlags.ShrinkCenter };
                holder.AddChild(number); holder.MoveChild(number, childIndex); number.SetMeta(LastValue, picker.Value);
                ((LineEdit)number.FindChild("NumberInput", true, false)).AddThemeFontSizeOverride("font_size", 22);
            }
            var control = (QuietRoomNumber)picker.GetParent();
            if (!Mathf.IsEqualApprox(control.GetMeta(LastValue, picker.Value).AsDouble(), picker.Value))
            { control.SetMeta(LastValue, picker.Value); control.RefreshNumber(); }
            if (row.GetThemeStylebox("normal") is not StyleBoxTexture) SkinRow(row, picker.Value > 0);
        }
        foreach (var offers in new[] { _outgoingRows, _incomingRows })
        foreach (var row in offers.GetChildren().OfType<Button>())
            if (row.GetThemeStylebox("normal") is not StyleBoxTexture) SkinRow(row, true);
    }
    private void SimplifyTradeTerms()
    {
        const string acceptedPrefix = "双方按当前物品价值判断，";
        const string declinedPrefix = "物品价值差距较大，";
        if (_tradeTermsLabel.Text.StartsWith(acceptedPrefix, StringComparison.Ordinal))
            _tradeTermsLabel.Text = _tradeTermsLabel.Text[acceptedPrefix.Length..];
        else if (_tradeTermsLabel.Text.StartsWith(declinedPrefix, StringComparison.Ordinal))
            _tradeTermsLabel.Text = _tradeTermsLabel.Text[declinedPrefix.Length..];
    }
    private void AddAttitudeSubject()
    {
        const string subject = "对方：";
        if (!_attitudeLabel.Text.StartsWith(subject, StringComparison.Ordinal))
            _attitudeLabel.Text = subject + _attitudeLabel.Text;
    }
    public override void _Process(double delta) { if (Visible) SyncPaper(); }
}
