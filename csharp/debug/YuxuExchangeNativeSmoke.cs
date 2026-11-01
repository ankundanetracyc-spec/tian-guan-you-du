using Godot;

/// <summary>原生善功堂目录/滚动/报价与确认状态专项；不写玩家库存或存档。</summary>
public partial class YuxuExchangeNativeSmoke : Control
{
    private int _checks;
    private void Check(bool success, string reason) { _checks++; if (!success) throw new InvalidOperationException(reason); }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async Task Settle(int count = 6) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(owner)!;
    private async Task Click(Control control)
    {
        var point = control.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        await Settle();
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        HoverInfoManager.HideAll(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://outputs/" + name + ".png");
    }
    private async void Run()
    {
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); GetWindow().Size = new(1920, 1080);
            var items = YuxuExchangeCatalog.LoadItems();
            Check(items.Count > 50, "现行目录未载入");
            Check(items.Count(i => i is PillItemData) == 5, "回春丹五档缺失或恢复暂停丹药");
            Check(items.Any(i => i is PortraitClothingData), "衣着目录缺失");
            Check(items.All(i => i is not PillContainerItemData && YuxuExchangeCatalog.Price(i) >= 0), "模板或无价品进入兑换目录");
            var pill = items.Single(i => i.definition_id.ToString() == "alchemy_huichun_9_1");
            Check(YuxuExchangeCatalog.Price(pill) == 8, "贡献表良效回春价不一致");
            var panel = new YuxuSectExchangePanel(); AddChild(panel); panel.open_panel(100); await Settle();
            Check(panel.IsVisibleInTree(), "目录未显示");
            var columns = panel.FindChild("CategoryColumns", true, false) as HBoxContainer;
            var horizontal = panel.FindChild("CategoryScroll", true, false) as ScrollContainer;
            Check(columns?.GetChildCount() == 9 && horizontal is not null, "九分类缺失");
            Check(Mathf.IsEqualApprox(horizontal!.Size.X, 1440) && columns!.GetChildren().OfType<Control>().All(c => Mathf.IsEqualApprox(c.Size.X, 360)), "未同屏四列");
            Check(panel.FindChildren("ItemIcon", "TextureRect", true, false).Count == 0, "文字行仍有图标");
            Check(panel.FindChildren("ItemScroll", "ScrollContainer", true, false).Count == 9, "列内独立滚动缺失");
            Check(panel.FindChild("PavilionFrame", true, false) is TextureRect { Texture: not null }, "原屋檐木框未载入");
            var next = Field<Button>(panel, "_next"); await Click(next);
            Check(horizontal.ScrollHorizontal > 0, "实际点击右箭头未移动分类");
            var previous = Field<Button>(panel, "_previous"); await Click(previous);
            Check(horizontal.ScrollHorizontal == 0, "左箭头未回首列");
            var row = panel.FindChild("Item_alchemy_huichun_9_1", true, false) as Button;
            Check(row is not null, "丹药原生行缺失"); await Click(row!);
            Check(Field<Label>(panel, "_selectionLabel").Text.Contains("所需贡献 8"), "底部选中和报价不同步");
            await Capture("shop_native_four_columns_20261031");
            var requested = 0; panel.purchase_requested += (path, cost) => { requested++; Check(path == pill.ResourcePath && cost == 8, "购买事件与选中商品不一致"); panel.resolve_purchase(true, 92, "兑换成功"); };
            await Click(Field<Button>(panel, "_exchangeButton"));
            Check(Field<PanelContainer>(panel, "_purchaseDialog").Visible, "兑换按钮未打开原确认流程");
            await Click(Field<Button>(panel, "_purchaseConfirm"));
            Check(requested == 1 && Field<Button>(panel, "_purchaseConfirm").Disabled && Field<Label>(panel, "_balanceLabel").Text.Contains("92"), "兑换结果未阻止重复确认或余额未同步");
            Field<Button>(panel, "_purchaseConfirm").EmitSignal(BaseButton.SignalName.Pressed); Check(requested == 1, "重复信号触发二次购买");
            panel.handle_back(); Check(!Field<PanelContainer>(panel, "_purchaseDialog").Visible && panel.Visible, "返回没有先关闭确认");
            panel.set_contribution_balance(0); await Click(Field<Button>(panel, "_exchangeButton"));
            Check(Field<Button>(panel, "_purchaseConfirm").Disabled, "贡献不足仍可确认"); panel.handle_back();
            var search = Field<LineEdit>(panel, "_search"); search.Text = "回春丹"; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text); await Settle();
            Check(panel.FindChildren("Item_*", "Button", true, false).Count == 5, "搜索没有按全部分类过滤");
            search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text); await Settle();
            GetWindow().Size = new(1280, 720); await Settle();
            Check(panel.FindChild("PavilionCanvas", true, false) is Control canvas && canvas.GetGlobalRect().Size.X <= GetViewport().GetVisibleRect().Size.X + 1, "窄窗口框架溢出");
            await Capture("shop_native_1280_20261031");
            GD.Print($"YUXU_EXCHANGE_NATIVE_OK: {_checks} checks; catalog={items.Count}; four columns, horizontal and independent vertical lists, live prices, confirmation and repeat guard; no persisted purchase");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
