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
        var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        await Settle();
    }
    private async Task Wheel(Control control, MouseButton direction, bool shift = false)
    {
        var point = control.GetGlobalTransformWithCanvas() * (control.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = direction, Pressed = true, ShiftPressed = shift, Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = direction, Pressed = false, ShiftPressed = shift, Position = point, GlobalPosition = point }, true);
        await Settle();
    }
    private async Task Drag(Control control, Vector2 delta, Vector2? localStart = null)
    {
        var point = control.GetGlobalTransformWithCanvas() * (localStart ?? control.Size * .5f);
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
        for (var step = 1; step <= 4; step++)
        {
            var moved = point + delta * step / 4;
            GetViewport().PushInput(new InputEventMouseMotion { Position = moved, GlobalPosition = moved, Relative = delta / 4, ButtonMask = MouseButtonMask.Left }, true);
        }
        point += delta;
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
            Check(!YuxuExchangeCatalog.Categories.Contains("装备") && items.All(i => i is not PortraitClothingData && YuxuExchangeCatalog.Category(i) != "装备"), "装备分类或衣着仍在善功堂");
            Check(items.All(i => i is not PillContainerItemData && YuxuExchangeCatalog.Price(i) >= 0), "模板或无价品进入兑换目录");
            var pill = items.Single(i => i.definition_id.ToString() == "alchemy_huichun_9_1");
            Check(YuxuExchangeCatalog.Price(pill) == 8, "贡献表良效回春价不一致");
            var panel = new YuxuSectExchangePanel(); AddChild(panel); panel.open_panel(100); await Settle();
            Check(panel.IsVisibleInTree(), "目录未显示");
            var columns = panel.FindChild("CategoryColumns", true, false) as HBoxContainer;
            var horizontal = panel.FindChild("CategoryScroll", true, false) as ScrollContainer;
            Check(columns?.GetChildCount() == YuxuExchangeCatalog.Categories.Length && columns.GetChildCount() == 7 && horizontal is not null && !YuxuExchangeCatalog.Categories.Contains("秘籍") && items.All(i => i.gongfa is null), "分类不一致或秘籍仍在善功堂");
            Check(Mathf.IsEqualApprox(horizontal!.Size.X, 1440) && columns!.GetChildren().OfType<Control>().All(c => Mathf.IsEqualApprox(c.Size.X, 360)), "未同屏四列");
            Check(panel.FindChildren("ItemIcon", "TextureRect", true, false).Count == 0, "文字行仍有图标");
            Check(panel.FindChildren("ItemScroll", "ScrollContainer", true, false).Count == YuxuExchangeCatalog.Categories.Length, "列内独立滚动缺失");
            Check(panel.FindChild("PavilionFrame", true, false) is TextureRect { Texture: not null }, "原屋檐木框未载入");
            var rail = horizontal.GetHScrollBar();
            var previous = Field<Button>(panel, "_previous");
            var next = Field<Button>(panel, "_next");
            Check(rail.IsVisibleInTree() && rail.MaxValue > rail.Page && previous.Disabled && !next.Disabled, "超过四类未显示有效滑轨或首端箭头状态有误");
            var extraColumns = columns!.GetChildren().OfType<Control>().Skip(4).ToArray();
            foreach (var column in extraColumns) columns.RemoveChild(column);
            await Settle();
            Check(!rail.Visible && !previous.Visible && !next.Visible && horizontal.ScrollHorizontal == 0, "四类仍有多余滑轨或可滚动留白");
            columns.AddChild(extraColumns[0]); await Settle();
            Check(rail.IsVisibleInTree() && next.Visible && !next.Disabled && Mathf.IsEqualApprox((float)(rail.MaxValue - rail.Page), 360), "第五类没有自动启用横向滑轨");
            foreach (var column in extraColumns.Skip(1)) columns.AddChild(column);
            await Settle();
            var heading = columns.GetChild<Control>(1).GetNode<Control>("CategoryHeading");
            await Drag(heading, new(-240, 0));
            Check(horizontal.ScrollHorizontal > 0, "鼠标拖动列头未横移");
            horizontal.ScrollHorizontal = 0; await Settle();
            var thumbCenter = new Vector2(rail.Size.X * (float)(rail.Page / (rail.MaxValue - rail.MinValue)) * .5f, rail.Size.Y * .5f);
            await Drag(rail, new(180, 0), thumbCenter);
            Check(horizontal.ScrollHorizontal > 0, "拖动滑轨滑块未横移");
            horizontal.ScrollHorizontal = 0; await Settle();
            var materialScroll = columns.GetChild<Control>(0).GetNode<ScrollContainer>("ItemScroll");
            var firstMaterial = materialScroll.GetNode<VBoxContainer>("Items").GetChild<Control>(0);
            await Wheel(firstMaterial, MouseButton.WheelDown, true);
            Check(horizontal.ScrollHorizontal > 0 && materialScroll.ScrollVertical == 0, "物品行上Shift滚轮未横移或串动纵滚");
            horizontal.ScrollHorizontal = 0; await Settle();
            await Wheel(firstMaterial, MouseButton.WheelDown);
            Check(materialScroll.ScrollVertical > 0 && horizontal.ScrollHorizontal == 0, "普通滚轮没有独立滚动材料列");
            materialScroll.ScrollVertical = 0; await Settle();
            await Wheel(firstMaterial, MouseButton.WheelRight);
            Check(horizontal.ScrollHorizontal > 0, "物品行上横向滚轮未移动分类");
            horizontal.ScrollHorizontal = 0; await Settle();
            await Capture("shop_native_rail_before_arrow_20261107");
            var arrowPresses = 0; next.Pressed += () => arrowPresses++;
            await Click(next);
            Check(horizontal.ScrollHorizontal > 0, $"实际点击右箭头未移动分类: presses={arrowPresses}, visible={next.IsVisibleInTree()}, disabled={next.Disabled}, rect={next.GetGlobalRect()}, pan={Field<bool>(panel, "_panning")}, hover={GetViewport().GuiGetHoveredControl()?.GetPath()}");
            await Click(previous);
            Check(horizontal.ScrollHorizontal == 0, "左箭头未回首列");
            for (var step = 0; step < YuxuExchangeCatalog.Categories.Length; step++) await Click(next);
            var lastColumn = columns.GetChild<Control>(columns.GetChildCount() - 1);
            Check(next.Disabled && !previous.Disabled && Mathf.IsEqualApprox(horizontal.ScrollHorizontal, (float)(rail.MaxValue - rail.Page)) && horizontal.GetGlobalRect().Grow(1).Encloses(lastColumn.GetGlobalRect()), "末分类不能完整到达或末端箭头状态不正确");
            await Capture("shop_native_rail_end_20261107");
            horizontal.ScrollHorizontal = 0; await Settle();
            var row = panel.FindChild("Item_alchemy_huichun_9_1", true, false) as Button;
            Check(row is not null, "丹药原生行缺失"); await Click(row!);
            Check(Field<Label>(panel, "_selectionLabel").Text.Contains("所需贡献 8"), "底部选中和报价不同步");
            await Capture("shop_native_four_columns_20261107");
            var requested = 0; panel.purchase_requested += (path, cost) => { requested++; Check(path == pill.ResourcePath && cost == 8, "购买事件与选中商品不一致"); panel.resolve_purchase(true, 92, "兑换成功"); };
            await Click(Field<Button>(panel, "_exchangeButton"));
            Check(Field<PanelContainer>(panel, "_purchaseDialog").Visible, "兑换按钮未打开原确认流程");
            await Wheel(firstMaterial, MouseButton.WheelDown, true);
            Check(horizontal.ScrollHorizontal == 0, "确认弹窗显示时底层分类仍可横移");
            await Click(Field<Button>(panel, "_purchaseConfirm"));
            Check(requested == 1 && Field<Button>(panel, "_purchaseConfirm").Disabled && Field<Label>(panel, "_balanceLabel").Text.Contains("92"), "兑换结果未阻止重复确认或余额未同步");
            Field<Button>(panel, "_purchaseConfirm").EmitSignal(BaseButton.SignalName.Pressed); Check(requested == 1, "重复信号触发二次购买");
            panel.handle_back(); Check(!Field<PanelContainer>(panel, "_purchaseDialog").Visible && panel.Visible, "返回没有先关闭确认");
            panel.set_contribution_balance(0); await Click(Field<Button>(panel, "_exchangeButton"));
            Check(Field<Button>(panel, "_purchaseConfirm").Disabled, "贡献不足仍可确认"); panel.handle_back();
            horizontal.ScrollHorizontal = 360; await Settle();
            var search = Field<LineEdit>(panel, "_search"); search.Text = "回春丹"; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text); await Settle();
            Check(panel.FindChildren("Item_*", "Button", true, false).Count == 5, "搜索没有按全部分类过滤");
            Check(horizontal.ScrollHorizontal == 360, "搜索改变分类横向位置");
            search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text); await Settle();
            GetWindow().Size = new(1280, 720); await Settle();
            Check(panel.FindChild("PavilionCanvas", true, false) is Control canvas && canvas.GetGlobalRect().Size.X <= GetViewport().GetVisibleRect().Size.X + 1, "窄窗口框架溢出");
            horizontal.ScrollHorizontal = 0; await Settle();
            await Drag(heading, new(-160, 0));
            Check(horizontal.ScrollHorizontal > 0, "1280窗口下列头拖动失效");
            horizontal.ScrollHorizontal = 0; await Settle();
            await Capture("shop_native_1280_20261107");
            GD.Print($"YUXU_EXCHANGE_NATIVE_OK: {_checks} checks; catalog={items.Count}; no equipment, automatic four/five category rail, thumb and heading drag, nested wheels, end bounds, prices, confirmation and repeat guard; no persisted purchase");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
