using Godot;
using System.Text.Json;

/// <summary>Shared readonly hover input passthrough and native/custom drag suppression.</summary>
public partial class HoverDragPassthroughSmoke : Node
{
    private int _checks;
    private Control _stage = null!;
    private HoverInfoManager _reader = null!;
    private PanelContainer _card = null!;
    private Button _source = null!;

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++; GD.Print("HOVER_DRAG_PASS " + message);
    }
    private async Task Frames(int count = 3)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Delay(double seconds = .17)
    { await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout); await Frames(); }
    private void Move(Vector2 position) => GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
    private void Mouse(Vector2 position, MouseButton button)
    {
        Move(position);
        foreach (var pressed in new[] { true, false })
            GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = pressed }, true);
    }
    private void PressKey(Key code) => GetViewport().PushInput(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true }, true);
    private static IEnumerable<Control> Controls(Node root)
    {
        if (root is Control control) yield return control;
        foreach (var child in root.GetChildren(true)) foreach (var descendant in Controls(child)) yield return descendant;
    }
    private HoverInfoManager.HoverInfoContent Content(string title = "信息来源") => new(title, "只读说明正文。",
        RulesText: "独立规则页继续允许滚动和关闭。", SingleColumn: true);
    private async Task ShowSource(string title = "信息来源")
    {
        Move(_source.GetGlobalRect().GetCenter());
        HoverInfoManager.SetInfo(_source, Content(title), true);
        await Frames(5);
        Check(_card.Visible, "来源正常显示只读悬停");
    }
    public override void _Ready() { ProcessMode = ProcessModeEnum.Always; CallDeferred(nameof(Run)); }
    private async void Run()
    {
        try
        {
            GetWindow().ContentScaleSize = new(1280, 720);
            _reader = GetNode<HoverInfoManager>("/root/HoverInfoManager");
            _card = _reader.GetNode<PanelContainer>("SharedHoverInfoCard");
            _stage = new Control { Size = new(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore }; AddChild(_stage);
            _source = new Button { Text = "来源", Position = new(80, 110), Size = new(160, 52) }; _stage.AddChild(_source);
            var underlay = new Button { Text = "底层操作", Position = new(500, 130), Size = new(160, 90) }; _stage.AddChild(underlay);
            var clicks = 0; underlay.Pressed += () => clicks++;
            await Frames(5); await ShowSource();
            Check(Controls(_card).All(control => control.MouseFilter == Control.MouseFilterEnum.Ignore), "悬停全部可见与内部子控件均忽略鼠标");
            Check(Controls(_card).All(control => control.FocusMode == Control.FocusModeEnum.None), "只读内容不取得控件焦点");
            var hint = Controls(_card).OfType<Button>().Single(button => button.Text.Contains("[F1]"));
            Check(hint.MouseFilter == Control.MouseFilterEnum.Ignore, "规则详解提示不再挡住鼠标");
            var dynamic = new Control { MouseFilter = Control.MouseFilterEnum.Stop, FocusMode = Control.FocusModeEnum.All }; _card.AddChild(dynamic);
            Check(dynamic.MouseFilter == Control.MouseFilterEnum.Ignore && dynamic.FocusMode == Control.FocusModeEnum.None, "动态添加的悬停内容立即保持穿透");
            dynamic.QueueFree(); await Frames();

            _card.Position = underlay.Position - new Vector2(10, 10);
            var point = underlay.GetGlobalRect().GetCenter(); Move(point);
            Check(_card.Visible && _card.GetGlobalRect().HasPoint(point), "实际点击前卡片仍覆盖底层按钮");
            Mouse(point, MouseButton.Left); await Frames(2);
            Check(clicks == 1, "真实鼠标点击只触发底层按钮一次");
            await Delay(); Check(!_card.Visible, "移入卡片不延长显示而在离开来源后收起");

            var scroll = new ScrollContainer { Position = new(500, 350), Size = new(220, 160), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                MouseForcePassScrollEvents = false }; _stage.AddChild(scroll);
            var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(rows);
            for (var i = 0; i < 12; i++) rows.AddChild(new Button { Text = "条目" + i, CustomMinimumSize = new(180, 44) });
            await Frames(4); await ShowSource();
            _card.Position = scroll.Position - new Vector2(10, 10); point = scroll.GetGlobalRect().GetCenter();
            Move(point); Check(_card.Visible && _card.GetGlobalRect().HasPoint(point), "滚轮前悬停仍覆盖来源列表");
            Mouse(point, MouseButton.WheelDown); await Frames(2);
            Check(scroll.ScrollVertical > 0, "滚轮自然传给底下真实列表");
            Check(Controls(_card).OfType<ScrollContainer>().All(container => container.ScrollVertical == 0), "悬停不代收滚轮或滚动正文");

            await ShowSource();
            var firstOwner = new Node(); var secondOwner = new Node(); _stage.AddChild(firstOwner); _stage.AddChild(secondOwner);
            HoverInfoManager.SetDragInProgress(firstOwner, true);
            Check(HoverInfoManager.IsDragInProgress() && !_card.Visible, "自定义拖拽开始同步隐藏当前悬停");
            HoverInfoManager.SetInfo(_source, Content("拖拽更新"), true);
            Check(!_card.Visible, "即时显示请求也不能穿过拖拽抑制");
            _source.EmitSignal(Control.SignalName.MouseEntered); await Delay();
            Check(!_card.Visible, "拖拽期间重新进入来源与延时均不重开");
            var providerCalls = 0;
            HoverInfoManager.SetProvider(_source, () => { providerCalls++; return Content("释放后更新"); });
            Check(providerCalls == 0 && !_card.Visible, "拖拽期间记录动态来源但不运行悬停展示");
            PressKey(Key.F1); Check(!_reader.GetNode<Control>("RulesReader").Visible, "拖拽时不因F1误开规则页");
            HoverInfoManager.SetDragInProgress(secondOwner, true); HoverInfoManager.SetDragInProgress(firstOwner, false);
            Check(HoverInfoManager.IsDragInProgress() && !_card.Visible, "一个拖拽结束不释放另一所有者的抑制");
            secondOwner.QueueFree(); await Frames(3);
            Check(!HoverInfoManager.IsDragInProgress(), "拖拽所有者离树自动释放悬停抑制");
            Move(_source.GetGlobalRect().GetCenter()); _source.EmitSignal(Control.SignalName.MouseEntered); await Delay();
            Check(_card.Visible && providerCalls > 0, "拖拽结束后重新从真实来源读取最新内容");

            _source.ForceDrag("hover-native-drag", new Label { Text = "原生拖拽预览" });
            Check(GetViewport().GuiIsDragging(), "实际原生拖拽已建立");
            Check(!_card.Visible && HoverInfoManager.IsDragInProgress(), "原生拖拽无需额外调用也同步隐藏悬停");
            HoverInfoManager.SetInfo(_source, Content("原生拖拽期间"), true); await Delay();
            Check(!_card.Visible, "原生拖拽跨帧与重复更新均不重开");
            GetViewport().GuiCancelDrag(); await Frames();
            Check(!GetViewport().GuiIsDragging() && !HoverInfoManager.IsDragInProgress(), "取消原生拖拽恢复正常悬停资格");
            await ShowSource();
            PressKey(Key.F1); await Frames();
            Check(_reader.GetNode<Control>("RulesReader").Visible && !_card.Visible, "非拖拽状态F1仍打开独立规则阅读页");
            PressKey(Key.Escape); await Frames(); Check(!_reader.GetNode<Control>("RulesReader").Visible, "独立规则阅读页仍可关闭");
            HoverInfoManager.HideAll();
            var report = new { passed = true, checks = _checks, scope = "readonly hover transparency and native/custom drag suppression" };
            var output = ProjectSettings.GlobalizePath("res://docs/美术规范/招式盘拖放与遍历反馈_20261108");
            System.IO.Directory.CreateDirectory(output);
            System.IO.File.WriteAllText(System.IO.Path.Combine(output, "全局悬停交互验收.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("HOVER_DRAG_PASSTHROUGH_OK checks=" + _checks); CallDeferred(nameof(Shutdown));
        }
        catch (Exception error) { GD.PushError("HOVER_DRAG_PASSTHROUGH_FAILED " + error); GetTree().Paused = false; GetTree().Quit(1); }
    }
    private async void Shutdown()
    {
        HoverInfoManager.HideAll(); _stage.QueueFree(); await Frames(4);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Frames(2); GetTree().Quit();
    }
}
