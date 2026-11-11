using Godot;
using System.Text.Json;

/// <summary>Real pointer, quantity and disposable-actor trade checks. No formal world or save is loaded.</summary>
public partial class TradePaperUiSmoke : Node
{
    private const string Output = "res://docs/美术规范/普通交易纸墨木石样稿_20261109/";
    private readonly List<object> _checks = [];
    private readonly List<string> _captures = [];
    private TradePaperPreviewScene _preview = null!;
    private int _width;
    private void Check(bool passed, string name)
    { _checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException(name); GD.Print("TRADE_PAPER_OK " + name); }
    private async Task Frames(int count = 3)
    { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private T Find<T>(string name) where T : Node => (T)_preview.FindChild(name, true, false);
    private async Task Mouse(Control control, bool pressed)
    {
        var position = control.GetGlobalTransformWithCanvas() * (control.Size / 2);
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = pressed }, true);
        await Frames(2);
    }
    private async Task Click(Control control) { await Mouse(control, true); await Mouse(control, false); await Frames(2); }
    private async Task Capture(string state, bool keepPointer = false)
    {
        if (!keepPointer) GetViewport().PushInput(new InputEventMouseMotion { Position = new(4, 4), GlobalPosition = new(4, 4) }, true);
        HoverInfoManager.HideAll(); await Frames(4); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var picture = GetViewport().GetTexture().GetImage();
        Check(picture.GetWidth() == _width && picture.GetHeight() == _width * 9 / 16, "原生完整图尺寸 " + state);
        string name = $"普通交易_{state}_{_width}.png";
        Check(picture.SavePng(Output + name) == Error.Ok, "原生完整图保存 " + state); _captures.Add(name);
    }
    private static IEnumerable<T> All<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        { if (child is T found) yield return found; foreach (var nested in All<T>(child)) yield return nested; }
    }
    private async Task EnterQuantity(QuietRoomNumber number, string text)
    {
        var input = (LineEdit)number.FindChild("NumberInput", true, false);
        await Click(input); input.SelectAll();
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Backspace, Pressed = true }, true);
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Backspace, Pressed = false }, true);
        foreach (char digit in text)
        {
            GetViewport().PushInput(new InputEventKey { Unicode = digit, Pressed = true }, true);
            GetViewport().PushInput(new InputEventKey { Unicode = digit, Pressed = false }, true);
        }
        await Frames(3);
        Check(input.Text == text, "输入尚未提交时不被逐帧同步覆盖");
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }, true);
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }, true); await Frames(4);
    }
    public override async void _Ready()
    {
        bool passed = false; string failure = "";
        try
        {
            _width = OS.GetCmdlineUserArgs().Contains("--wide") ? 1920 : 1280;
            string project = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
            string user = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            Check(user.StartsWith(System.IO.Path.Combine(project, "output") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "用户目录隔离在output内");
            Check(GetViewport() is SubViewport, "实际完整图使用固定GPU子视口");
            var saves = GetNode<GameSaveManager>("/root/GameSaveManager"); Check(!saves.has_active_slot, "未打开正式旅程或存档槽");
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager"); int hour = clock.get_total_game_hours();
            _preview = GD.Load<PackedScene>("res://tscn/scene/TradePaperPreview.tscn").Instantiate<TradePaperPreviewScene>();
            _preview.ExitOnClose = false; AddChild(_preview); await Frames(12); var panel = _preview.Panel;
            var columns = Find<HBoxContainer>("TradeColumns");
            Check(columns.GetChildCount() == 4 && columns.GetChildren().OfType<PanelContainer>().All(column => column.GetThemeStylebox("panel") is StyleBoxTexture), "保留四列文字目录并采用批准纸面");
            Check(panel.CommitButton.Disabled, "没有所选物品时不能成交");
            Check(!All<TextureRect>(Find<GridContainer>("PlayerTradeList")).Any() && !All<TextureRect>(Find<GridContainer>("NpcTradeList")).Any(), "文字目录不增加物品图标");
            var back = Find<Button>("TradeReturn"); var date = Find<Label>("TradeDate");
            var canvasInverse = panel.DesignCanvas.GetGlobalTransformWithCanvas().AffineInverse();
            var title = Find<HBoxContainer>("TradePaperHeader").GetChildren().OfType<Label>().First();
            var titlePosition = canvasInverse * title.GetGlobalTransformWithCanvas().Origin;
            var backPosition = canvasInverse * back.GetGlobalTransformWithCanvas().Origin;
            Check(Math.Abs(titlePosition.X - 36) < .1f, "交易标题距页眉纸面左边保留12设计单位内边距");
            Check(Math.Abs(backPosition.X - 1534) < .1f && Math.Abs(backPosition.Y - 18) < .1f && back.Size.X == 114, "页眉加左内边距不移动右上返回坐标");
            Check(back.Size.Y == 46 && date.Size.Y == 46 && Math.Abs(back.GlobalPosition.Y - date.GlobalPosition.Y) < .1f && back.GetThemeFontSize("font_size") == 22 && date.GetThemeFontSize("font_size") == 22, "日期返回22字46高同一行");
            Check(!date.Text.Contains(":") && !date.Text.Contains("时") && !date.Text.Contains("昼"), "日期不显示时辰钟点");
            Check(All<QuietRoomNumber>(panel).Count() == 10 && All<SpinBox>(panel).All(picker => !picker.Visible), "原数量模型由十个纸面输入槽承接");
            await Capture("初开目录");
            var source = panel.SourceRow(true, "灵石");
            await Click(source);
            Check(panel.Quantity(true, "灵石").Value == 1, "来源实际点击添加一件");
            var number = panel.NumberControl(true, "灵石");
            var plus = (QuietRoomTriangle)number.FindChild("NumberPlus", true, false); await Click(plus);
            Check(panel.Quantity(true, "灵石").Value == 2 && plus.GetThemeStylebox("normal") is StyleBoxEmpty, "裸三角实际加一且不带按钮素材");
            var offer = Find<VBoxContainer>("OutgoingTradeList").GetChildren().OfType<Button>().Single(); await Click(offer);
            Check(panel.Quantity(true, "灵石").Value == 1 && ((LineEdit)number.FindChild("NumberInput", true, false)).Text == "1", "核对清单减一同步回数量槽");
            await EnterQuantity(number, "6"); Check(panel.Quantity(true, "灵石").Value == 6, "整数输入回原数量事件");
            await EnterQuantity(number, "999");
            Check(panel.Quantity(true, "灵石").Value == 12 && plus.Disabled, "超量输入按真实库存上限收敛");
            await EnterQuantity(number, "4"); await Capture("数量编辑");
            panel.Quantity(true, "兽皮").Value = 1;
            panel.Quantity(false, "兽骨").Value = 1; panel.Quantity(false, "雾铃花").Value = 1; await Frames(6);
            Check(Find<VBoxContainer>("OutgoingTradeList").GetChildren().OfType<Button>().Count() == 2 && Find<VBoxContainer>("IncomingTradeList").GetChildren().OfType<Button>().Count() == 2, "双方所选物品进入各自核对列");
            Check(source.GetThemeStylebox("normal") is StyleBoxTexture && source.SelfModulate != Colors.White &&
                !All<ColorRect>(source).Any(rect => rect.Visible), "来源整纸选中且文字后没有额外色块");
            await Capture("交换清单");
            await Mouse(source, true);
            Check(source.GetThemeStylebox("pressed") is StyleBoxTexture pressed && pressed.ExpandMarginTop == -1, "按住来源行产生整纸下沉反馈");
            await Capture("按钮按下", true); await Mouse(source, false);
            _preview.ClearSelection(); panel.Quantity(false, "灵石").Value = 8; await Frames(5);
            Check(panel.CommitButton.Disabled && Find<PanelContainer>("TradePaperNotice").Visible, "不可成交沿原态度判断并常显提示");
            await Capture("不可成交");
            _preview.ClearSelection(); panel.Quantity(true, "灵石").Value = 2; panel.Quantity(false, "灵石").Value = 2; await Frames(5);
            Check(!panel.CommitButton.Disabled, "相同物品等量交换由原估值接受");
            var stamina = _preview.Actor.current_world_stamina; int count = _preview.CompletedCount;
            await Click(panel.CommitButton);
            Check(_preview.CompletedCount == count + 1 && _preview.Actor.current_world_stamina == stamina - WorldStaminaRules.InteractionCost, "只在隔离夹具完成一次原交易并收原精力");
            Check(_preview.Actor.get_inventory_total_count(TradePaperPreviewScene.Items + "灵石.tres") == 12 && _preview.Merchant.trade_inventory[TradePaperPreviewScene.Items + "灵石.tres"].AsInt32() == 8, "同物等量互换保持双方真实数量");
            await Capture("交易完成");
            _preview.ResetFixture(true); await Frames(8);
            Check(!Find<GridContainer>("PlayerTradeList").GetChildren().OfType<Button>().Any() && !Find<GridContainer>("NpcTradeList").GetChildren().OfType<Button>().Any() && panel.CommitButton.Disabled, "空目录不补假商品且不可成交");
            await Capture("空目录");
            Check(clock.get_total_game_hours() == hour && !saves.has_active_slot, "样稿不推进时间或打开正式存档");
            await Click(back); Check(!_preview.Visible, "右上返回实际退出独立样稿"); passed = true;
        }
        catch (Exception error) { failure = error.ToString(); GD.PushError(failure); }
        finally
        {
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            var report = new { status = passed ? "passed" : "failed", width = _width, checks = _checks, captures = _captures, error = failure, scope = "独立普通交易；原业务仅绑定临时人物库存；未接正式旅程" };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Output + $"验收记录_{_width}.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Frames(2);
            GD.Print($"TRADE_PAPER_{(passed ? "PASS" : "FAIL")} width={_width} checks={_checks.Count} captures={_captures.Count}"); GetTree().Quit(passed ? 0 : 1);
        }
    }
}
