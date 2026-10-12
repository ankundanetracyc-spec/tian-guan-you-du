using Godot;
using System.Reflection;

/// <summary>临时 UI 验收场景，不读写正式存档或推进旅程。</summary>
public partial class JournalUiSmoke : Control
{
    private JournalPanel _journal = null!;
    private MainHUD _hud = null!;
    private string _folder = "res://docs/美术规范/见闻与任务界面概念_20261011/实机验收";
    private static IEnumerable<T> Nodes<T>(Node root) where T : Node
    { foreach (var child in root.GetChildren()) { if (child is T result) yield return result; foreach (var nested in Nodes<T>(child)) yield return nested; } }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); GD.Print("JOURNAL_CHECK_OK " + message); }
    private async System.Threading.Tasks.Task Settle() { for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Settle();
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(_folder + "/" + name + ".png"));
    }
    public override async void _Ready()
    {
        try
        {
            var compact = OS.GetCmdlineUserArgs().Contains("--compact");
            GetWindow().ContentScaleSize = compact ? new Vector2I(1280, 720) : new Vector2I(1600, 900);
            if (compact) _folder += "/1280x720";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_folder));
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var bg = new TextureRect { Texture = GD.Load<Texture2D>("res://textures/village_overview/qingxi_territory_overview_v3.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
            AddChild(bg); bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _hud = GD.Load<PackedScene>("res://tscn/panel/MainHUD.tscn").Instantiate<MainHUD>(); AddChild(_hud);
            _journal = new JournalPanel(); AddChild(_journal);
            var tasks = new[] {
                new JournalPanel.Entry("玄鉴初读", "进入静室，研读完玄鉴传入功法的第一卷。", "阶段任务", "青溪里 · 静室", "进行中", Id: "qingxi.first_volume", AcceptedOrder: 2),
                new JournalPanel.Entry("初醒青溪", "确认身处何地并踏入云息，取得离村资格。", "阶段目标", "青溪里", "进行中", Id: "phase.qingxi", AcceptedOrder: 1),
                new JournalPanel.Entry("问道", "与李守拙说话，问起偏院旧屋。", "阶段任务", "青溪里", "已完成", Id: "qingxi.ask_for_room") };
            _journal.TrackedTaskId = tasks[0].Id;
            _journal.set_entries(JournalPanel.Section.Tasks, tasks);
            _journal.set_entries(JournalPanel.Section.Rumors, [new("后山夜声", "近来后山夜里常有短促兽鸣，入夜后忽远忽近。猎户说这不是寻常时节该有的动静。\n\n村中人近来很少独自走山路。", "村中闲谈", "青溪里", "待查")]);
            var requested = "";
            _hud.TaskRequested += id => { requested = id; _journal.show_panel(JournalPanel.Section.Tasks, id); };
            _hud.set_task_entries(tasks, true); _hud.set_task_tracker_horizontal_layout(); await Capture("01_两项任务栏");
            var tracker = _hud.GetNode<Control>("TaskTracker");
            Check(Math.Abs(tracker.Size.X - 300) < 2 && tracker.Size.Y <= 220, "任务栏宽300加20收起签，高度不超过220");
            Check(Math.Abs(tracker.Position.Y - GetViewportRect().Size.Y * .25f) < 2, "任务栏25%顶锚");
            Check(Nodes<Button>(tracker).Count() == 2, "任务栏恰好两项");
            var firstLabels = Nodes<Label>(tracker.FindChild("TaskEntry0", true, false)).ToList();
            Check(firstLabels[0].GetGlobalRect().End.Y <= firstLabels[1].GetGlobalRect().Position.Y, "标题与目标不重叠");
            Check(!Nodes<Label>(tracker).Any(l => l.Text.Contains("当前追踪") || l.Text.Contains("最近接到") || l.Text.Contains("全部任务")), "无冗余任务栏标签");
            var toggle = _hud.GetNode<Button>("TaskTrackerToggle"); toggle.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(tracker.Position.X >= GetViewportRect().Size.X, "收起后只剩窄签");
            toggle.EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(Math.Abs(toggle.Size.Y - tracker.Size.Y) < 2, "收起签与任务栏等高");
            var first = tracker.FindChild("TaskEntry0", true, false) as Button;
            first!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            Check(requested == "", "单击不打开任务");
            first.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, DoubleClick = true });
            await Capture("02_任务页");
            Check(requested == tasks[0].Id && _journal.Visible && Nodes<Label>(_journal).Any(l => l.Text == tasks[0].Title), "双击按稳定标识定位任务");
            var time = GetNode<GameTimeMgr>("/root/GameTimeManager"); var hours = time.get_total_game_hours();
            string? changed = "unmodified";
            _journal.TrackingRequested += id => { changed = id; _journal.TrackedTaskId = id ?? ""; };
            Nodes<Button>(_journal).First(b => b.Text == "取消追踪").EmitSignal(BaseButton.SignalName.Pressed);
            Check(changed is null, "取消追踪事件");
            Nodes<Button>(_journal).First(b => b.Text == "追踪任务").EmitSignal(BaseButton.SignalName.Pressed);
            Check(changed == tasks[0].Id, "追踪事件使用稳定标识");
            Nodes<Button>(_journal).First(b => b.Text == "已完成").EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(Nodes<Label>(_journal).Any(l => l.IsVisibleInTree() && l.Text == "问道") && !Nodes<Button>(_journal).Any(b => b.IsVisibleInTree() && b.Text == "取消追踪"), "已完成筛选且不可追踪");
            Nodes<Button>(_journal).First(b => b.Text == "传闻").EmitSignal(BaseButton.SignalName.Pressed); await Capture("03_见闻阅读");
            var selectedTab = Nodes<Button>(_journal).First(b => b.Text == "传闻");
            Check(selectedTab.GetThemeStylebox("hover") is StyleBoxTexture && selectedTab.GetThemeStylebox("pressed") == selectedTab.GetThemeStylebox("normal"), "选中导航悬停及按下保留纸签");
            var inactiveTab = Nodes<Button>(_journal).First(b => b.Text == "任务");
            Check(inactiveTab.GetThemeStylebox("hover") is StyleBoxEmpty && inactiveTab.GetThemeColor("font_hover_color") == inactiveTab.GetThemeColor("font_color"), "未选中导航无黑底且文字保持可读");
            var caption = Nodes<Label>(_journal).First(l => l.Text == "全部记录");
            Check(caption.GetLineCount() == 1 && caption.Size.X >= caption.GetMinimumSize().X, "全部记录四字单行");
            Input.WarpMouse(selectedTab.GetGlobalRect().GetCenter()); await Capture("05_导航悬停");
            Check(time.get_total_game_hours() == hours, "切页不推进时间");
            Check(_journal.GetNode<TextureRect>("SharedCharacterBackground").Texture.ResourcePath == "res://textures/ui/infopanel_fullscreen_bg_v1.png", "直接复用角色页背景");
            _journal.set_entries(JournalPanel.Section.Events, []); _journal.show_panel(JournalPanel.Section.Events); await Settle();
            Check(Nodes<Label>(_journal).Any(l => l.IsVisibleInTree() && l.Text == "此处暂未收录新的内容。"), "空栏目显示");
            Nodes<Button>(_journal).First(b => b.Text == "地志").EmitSignal(BaseButton.SignalName.Pressed); await Settle();
            Check(Nodes<Label>(_journal).Any(l => l.IsVisibleInTree() && l.Text == "地志 · 待续"), "预留入口保留");
            var longTask = tasks[0] with { Title = "这是用于验证任务名称较长时独立换行且不会挤入目标文字的测试标题", Body = string.Concat(Enumerable.Repeat("长正文应当在自己的区域滚动，不挤占标题与返回入口。", 80)) };
            _journal.set_entries(JournalPanel.Section.Tasks, [longTask]); _journal.show_panel(JournalPanel.Section.Tasks); await Capture("04_长文字");
            var scrolls = Nodes<ScrollContainer>(_journal).ToList();
            Check(scrolls.Any(s => s.GetVScrollBar().MaxValue > s.Size.Y + 100), "长正文可滚动");
            Check(Nodes<Button>(_journal).First(b => b.Text == "返回").GetGlobalRect().End.X <= GetViewportRect().Size.X, "返回入口保持可达");
            _journal._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.Escape }); Check(!_journal.Visible, "Esc返回");
            var source = new MainScene(); var restored = new MainScene();
            object? Call(MainScene node, string method, params object[] args) => typeof(MainScene).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(node, args);
            Call(source, "IdentifyJournalTask", tasks[0]);
            typeof(MainScene).GetField("_trackedJournalTask", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(source, tasks[0].Id);
            var state = new Godot.Collections.Dictionary { ["journal_tracking"] = (Godot.Collections.Dictionary)Call(source, "PackJournalTracking")! };
            Call(restored, "RestoreJournalTracking", state);
            var packed = (Godot.Collections.Dictionary)Call(restored, "PackJournalTracking")!;
            Check(packed["selected"].AsString() == tasks[0].Id && packed["orders"].AsGodotDictionary().ContainsKey(tasks[0].Id), "追踪及接取顺序往返保存");
            Call(restored, "RestoreJournalTracking", new Godot.Collections.Dictionary());
            Check(((Godot.Collections.Dictionary)Call(restored, "PackJournalTracking")!)["selected"].AsString() == "", "旧存档缺字段兼容");
            _hud.set_task_entries(tasks.Take(1), true); await Settle();
            Check(Nodes<Button>(tracker).Count() == 1 && tracker.Size.Y < 130, "单项不留第二个空槽");
            _hud.set_task_entries([], false); await Settle(); Check(!tracker.Visible && !toggle.Visible, "空任务栏与收起签同步隐藏");
            source.Free(); restored.Free();
            GD.Print("JOURNAL_UI_SMOKE_OK"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
