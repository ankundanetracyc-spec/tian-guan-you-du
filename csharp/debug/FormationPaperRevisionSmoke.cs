using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Environment = System.Environment;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;
using Tool = FormationBoardCanvas.EditTool;

/// <summary>仅验收常显格线、工具菜单与键盘修订；不重跑旧整页验收。</summary>
public partial class FormationPaperRevisionSmoke : Control
{
    private const string PreviousEvidence = "res://docs/美术规范/阵法纸墨木石界面_20261109";
    private const string EvidenceRoot = PreviousEvidence + "/格线与快捷键修订";
    private readonly List<string> _checks = [];
    private readonly List<object> _captures = [];
    private BaigongPanel _archive = null!;
    private Character _owner = null!;
    private FormationPaperWorkspace _workspace = null!;
    private FormationWorkbench Workbench => _workspace.Workbench;
    private FormationBoardCanvas Canvas => Workbench.Canvas;
    private int _width, _height;
    private string _stage = "初始化";
    private string Evidence => EvidenceRoot + "/" + _width;
    private object? _canvasComparison;
    private string _draftPath = "";
    private Vector2 _lastPointer;

    public override async void _Ready()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--formation-paper-revision-smoke")) return;
        _width = OS.GetCmdlineUserArgs().Contains("--formation-paper-width=1280") ? 1280 : 1920;
        _height = _width == 1920 ? 1080 : 720;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Size = GetWindow().ContentScaleSize = new(_width, _height);
        GetWindow().GuiEmbedSubwindows = true;
        GetWindow().Position = new(-30000, -30000);
        try
        {
            await Frames(6);
            Check(GetViewport().GetVisibleRect().Size.IsEqualApprox(new(_width, _height)) &&
                (Environment.GetEnvironmentVariable("APPDATA") ?? "").Contains("formation_paper_revision", StringComparison.OrdinalIgnoreCase),
                "实际分辨率与独立APPDATA匹配");
            _owner = new Character { Name = "FormationRevisionIsolatedOwner" };
            _archive = new BaigongPanel(); AddChild(_archive); _archive.show_for(_owner); await Frames(4);
            await Click(Named<Button>(_archive, "OpenFormationWorkspace")); await Frames(4);
            _workspace = GetTree().Root.GetNodeOrNull<FormationPaperWorkspace>("FormationPaperWorkspace")
                ?? throw new Exception("真实百工入口未打开工作台");
            _draftPath = "user://formation_paper_revision/" + _width + "/isolated_draft.json";
            Workbench.DraftPath = _draftPath;
            Check(!_owner.IsInsideTree() && Workbench.IsVisibleInTree(), "真实入口展开工作台且验收人物未载入世界");

            _stage = "常显格线与空间";
            await MoveOutside(); await Wait(.24);
            Check(Canvas.GridVisible && !Canvas.GuidesVisible && !Canvas.HasPersistentLabels,
                "空闲始终显示格线，动态落点与常驻文字签仍关闭");
            Check(Workbench.FindChild("FormationDrawingTools", true, false) is null,
                "盘下六按钮工具行已移除");
            CompareCanvasHeight();
            var menu = Find<MenuButton>("FormationToolMenu");
            var partsPaper = Find<Control>("FormationPartsPaper");
            Check(partsPaper.GetGlobalRect().Encloses(menu.GetGlobalRect()) && menu.GetGlobalRect().Position.Y < partsPaper.GetGlobalRect().Position.Y + 70,
                "工具菜单收在左侧部件标题区且不占盘下空间");
            var popup = menu.GetPopup();
            var labels = Enumerable.Range(0, popup.ItemCount).Where(i => !popup.IsItemSeparator(i)).Select(popup.GetItemText).ToArray();
            Check(labels.Length == 6 && new[] { "移动", "信号线", "供能线", "擦除", "转向", "清线路" }
                .All(label => labels.Any(text => text.StartsWith(label, StringComparison.Ordinal))) && !labels.Any(text => text.Contains("能源线", StringComparison.Ordinal)),
                "六项工具仍完整，信号线和供能线保持原名");
            Check(!HoverCardVisible(), "常显格线没有使空闲悬停卡或幽灵常亮");
            await Capture("01_常显格线与扩大的盘面.png");

            _stage = "Q与E真实绘线";
            Canvas.SetModel(new FormationGridModel()); Workbench.Refresh("专项准备：空盘可自由使用所有格位。"); await Frames(2);
            await ClickCell(new(3, 2)); await KeyPress(Key.Q, 'q');
            Check(Canvas.Tool == Tool.Signal && Find<Label>("FormationStatus").Text.StartsWith("信号线", StringComparison.Ordinal), "真实Q键选择信号线且状态保持原名");
            await Paint(new(0, 0), new(1, 0), new(2, 0));
            Check(Canvas.Model.Wires.Count == 3 && Canvas.Model.Wires.Values.All(kind => kind == PortKind.Signal),
                "Q后鼠标拖动只铺三格朱砂信号线");
            await MoveOutside(); await Capture("02_Q信号线.png");
            await KeyPress(Key.E, 'e');
            Check(Canvas.Tool == Tool.Energy && Find<Label>("FormationStatus").Text.StartsWith("供能线", StringComparison.Ordinal), "真实E键选择供能线且状态保持原名");
            await Paint(new(0, 3), new(1, 3), new(2, 3));
            Check(Canvas.Model.Wires.Count == 6 && new[] { new Cell(0, 3), new Cell(1, 3), new Cell(2, 3) }
                .All(cell => Canvas.Model.Wires[cell] == PortKind.Energy) && Canvas.Model.Wires[new(1, 0)] == PortKind.Signal,
                "E后鼠标拖动只铺浅金供能线且不改已有信号线");
            await MoveOutside(); await Capture("03_E供能线.png");

            _stage = "退出铺线与原操作";
            await KeyPress(Key.Escape); await MoveOutside();
            Check(Canvas.Tool == Tool.Select && Canvas.GridVisible && !Canvas.GuidesVisible, "Esc回移动，格线保持而动态辅助收起");
            await KeyPress(Key.E, 'e'); await Click(Find<Button>("PartSensor"));
            Check(Canvas.Tool == Tool.Component, "选择部件可以直接离开供能铺线");
            await ClickCell(new(3, 0));
            var sensor = Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Sensor);
            Check(sensor.Origin == new Cell(3, 0) && !Canvas.Model.Wires.ContainsKey(new(3, 0)), "退出铺线后点击实际放件而非误铺线");
            await KeyPress(Key.Escape); await ClickCell(sensor.Origin); await KeyPress(Key.R, 'r');
            Check(Canvas.Model.Components[sensor.Id].Rotation == (sensor.Rotation + 1) % 4, "R仍旋转选中部件及真实端口");
            await ClickCell(sensor.Origin, MouseButton.Right);
            Check(!Canvas.Model.Components.ContainsKey(sensor.Id), "右键仍取下部件");
            await ClickCell(new(1, 3), MouseButton.Right);
            Check(!Canvas.Model.Wires.ContainsKey(new(1, 3)), "右键仍擦除实际线路");

            _stage = "工具菜单实际操作";
            Workbench.LoadExample(0); await MoveOutside();
            await Click(menu); await Frames(2);
            Check(popup.Visible, "标题工具按钮实际打开菜单");
            await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e');
            Check(popup.Visible && Canvas.Tool == Tool.Select, "菜单展开时Q和E不串到背后画布");
            await Capture("04_收起的六项工具菜单.png");
            await ClosePopup(popup);
            foreach (var item in new[] { ("移动", Tool.Select), ("信号线", Tool.Signal), ("供能线", Tool.Energy), ("擦除", Tool.Erase) })
            {
                await ChooseMenu(item.Item1);
                Check(Canvas.Tool == item.Item2, "菜单原生激活" + item.Item1 + "生效");
            }
            await ChooseMenu("移动");
            var menuSensor = Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Sensor);
            await ClickCell(menuSensor.Origin); await ChooseMenu("转向");
            Check(Canvas.Model.Components[menuSensor.Id].Rotation == (menuSensor.Rotation + 1) % 4, "菜单转向仍旋转实际选中件");
            var componentCount = Canvas.Model.Components.Count;
            await ChooseMenu("清线路");
            Check(Canvas.Model.Wires.Count == 0 && Canvas.Model.Components.Count == componentCount, "菜单清线路只清线路并保留部件");

            _stage = "输入和弹页不串键";
            Workbench.LoadExample(1); await Frames(2);
            var filter = Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Filter);
            await ClickCell(filter.Origin);
            await Click(Find<Button>("FormationParametersTab"));
            var unchanged = Canvas.Model.Serialize();
            var tag = Find<LineEdit>("PartSourceTag");
            await Click(tag); await KeyPress(Key.A, control: true); await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e');
            Check(tag.Text == "qe" && Canvas.Tool == Tool.Select && Canvas.Model.Serialize() == unchanged,
                "来源输入框真实键入qe，不切工具或改草图");
            var number = Find<SpinBox>("FilterMinimum").GetLineEdit();
            await Click(number); await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e');
            Check(GetViewport().GuiGetFocusOwner() == number && Canvas.Tool == Tool.Select && Canvas.Model.Serialize() == unchanged,
                "数值输入框收到q和e时不串铺线快捷键");
            await Click(Find<Button>("FormationHelp"));
            var help = Named<Control>(_workspace, "FormationHelpOverlay");
            await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e'); await KeyPress(Key.R, 'r');
            Check(help.IsVisibleInTree() && Canvas.Tool == Tool.Select && Canvas.Model.Serialize() == unchanged,
                "说明弹页中Q、E和R不改变背后工具或部件");
            await Click(Named<Button>(help, "CloseFormationHelp")); await Frames(2);
            await Click(Find<Button>("OpenGuardTrial")); await Frames(3);
            var guard = GetTree().Root.GetNodeOrNull<FormationGuardPanel>("FormationGuardPanel") ?? throw new Exception("未打开守攻页");
            var actor = guard.TrialOwner; var enemy = guard.Enemy;
            await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e'); await KeyPress(Key.R, 'r');
            Check(guard.IsVisibleInTree() && Canvas.Tool == Tool.Select && Canvas.Model.Serialize() == unchanged,
                "守攻页中Q、E和R不串到背后工作台");
            await Click(Named<Button>(guard, "GuardReturn")); await Frames(3);
            Check(!GodotObject.IsInstanceValid(guard) && !GodotObject.IsInstanceValid(actor) && !GodotObject.IsInstanceValid(enemy),
                "守攻返回释放其页面和试演人物");
            await Click(Find<Button>("ReturnFormationWorkspace")); await Frames(3);
            Check(!GodotObject.IsInstanceValid(_workspace) && _archive.IsVisibleInTree(), "返回释放工作台并恢复百工入口");
            WriteReport(true, null);
            _archive.QueueFree(); _owner.Free(); await Frames(2);
            GD.Print($"FORMATION_PAPER_REVISION_PASS width={_width} height={_height} checks={_checks.Count}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            try { await Capture("失败现场.png"); } catch { }
            WriteReport(false, exception.ToString());
            GD.PushError("FORMATION_PAPER_REVISION_FAIL " + _stage + " " + exception);
            if (GodotObject.IsInstanceValid(_workspace)) _workspace.QueueFree();
            if (GodotObject.IsInstanceValid(_archive)) _archive.QueueFree();
            if (GodotObject.IsInstanceValid(_owner)) _owner.Free();
            await Frames(2);
            GetTree().Quit(1);
        }
    }

    private void CompareCanvasHeight()
    {
        using var previous = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(PreviousEvidence + "/" + _width + "/原生验收.json"));
        var oldBounds = previous.RootElement.GetProperty("bounds").EnumerateArray().ToArray();
        var oldCenter = oldBounds.First(item => item.GetProperty("name").GetString() == "FormationDrawingArea");
        var oldTools = oldBounds.First(item => item.GetProperty("name").GetString() == "FormationDrawingTools");
        var scale = Named<Control>(_workspace, "FormationDesign").Scale.Y;
        var oldCanvasHeight = oldTools.GetProperty("y").GetSingle() - oldCenter.GetProperty("y").GetSingle() - 8 * scale;
        var current = Canvas.GetGlobalRect();
        Check(current.Size.Y >= oldCanvasHeight + 40 * scale && GetViewport().GetVisibleRect().Encloses(current),
            "取消工具行后画布真实高度比旧版增加至少40设计单位且完整在视口内");
        _canvasComparison = new { old_canvas_height = oldCanvasHeight, method = "旧工具行顶部减旧中央顶部再减原8单位间距", current_x = current.Position.X, current_y = current.Position.Y, current_width = current.Size.X, current_height = current.Size.Y, gain = current.Size.Y - oldCanvasHeight, scale };
    }

    private async Task ChooseMenu(string label)
    {
        var menu = Find<MenuButton>("FormationToolMenu"); var popup = menu.GetPopup();
        await Click(menu); await Frames(1);
        if (!popup.Visible) throw new Exception("菜单未显示：" + label);
        var index = Enumerable.Range(0, popup.ItemCount).First(i => popup.GetItemText(i).StartsWith(label, StringComparison.Ordinal));
        if (popup.IsItemDisabled(index)) throw new Exception("菜单项被禁用：" + label);
        // Default ui_accept/ui_cancel bindings are deliberately empty in this
        // project. Locate the actual row through its mouse hit test instead.
        for (var y = 4f; y < popup.Size.Y - 4; y += Math.Max(3f, popup.Size.Y / (popup.ItemCount * 4f)))
        {
            var point = new Vector2(popup.Position.X + popup.Size.X * .55f, popup.Position.Y + y);
            await Move(point);
            if (popup.GetFocusedItem() != index) continue;
            await Mouse(point, MouseButton.Left, true); await Mouse(point, MouseButton.Left, false); await Frames(2);
            if (popup.Visible) throw new Exception("鼠标未激活真实菜单项：" + label);
            return;
        }
        throw new Exception("真实鼠标未命中工具菜单项：" + label);
    }
    private async Task ClosePopup(PopupMenu popup)
    {
        await Click(Find<Label>("WorkbenchTitle")); await Frames(1);
        if (popup.Visible) throw new Exception("菜单外实际点击未关闭工具菜单");
    }
    private async Task Paint(params Cell[] cells)
    {
        await Move(CellPoint(cells[0])); await Mouse(CellPoint(cells[0]), MouseButton.Left, true);
        foreach (var cell in cells.Skip(1)) await Move(CellPoint(cell), true);
        await Mouse(CellPoint(cells[^1]), MouseButton.Left, false);
    }
    private async Task ClickCell(Cell cell, MouseButton button = MouseButton.Left)
    {
        var point = CellPoint(cell); await Move(point); await Mouse(point, button, true); await Mouse(point, button, false);
    }
    private async Task Click(Control control)
    {
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) { scroll.EnsureControlVisible(control); await Frames(2); }
        if (!control.IsVisibleInTree() || !GetViewport().GetVisibleRect().HasPoint(control.GetGlobalRect().GetCenter())) throw new Exception("点击目标不可见：" + control.Name);
        var point = control.GetGlobalRect().GetCenter(); await Move(point); await Mouse(point, MouseButton.Left, true); await Mouse(point, MouseButton.Left, false);
    }
    private Vector2 CellPoint(Cell cell) => Canvas.GetGlobalTransformWithCanvas() * Canvas.CellPosition(cell);
    private Task MoveOutside() => Move(Find<Label>("WorkbenchTitle").GetGlobalRect().GetCenter());
    private async Task Move(Vector2 point, bool held = false)
    {
        var relative = point - _lastPointer; _lastPointer = point;
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative, Velocity = relative * 60,
            ButtonMask = held ? MouseButtonMask.Left : 0 }, true); await Frames(2);
    }
    private async Task Mouse(Vector2 point, MouseButton button, bool pressed)
    {
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = pressed,
            ButtonMask = pressed ? button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right : 0 }, true); await Frames(2);
    }
    private async Task KeyPress(Key key, uint unicode = 0, bool control = false)
    {
        var target = GetViewport();
        target.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, Pressed = true, CtrlPressed = control }, true); await Frames(1);
        target.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, Pressed = false, CtrlPressed = control }, true); await Frames(1);
    }
    private bool HoverCardVisible() => GetTree().Root.GetNodeOrNull<HoverInfoManager>("HoverInfoManager")?.GetNodeOrNull<PanelContainer>("SharedHoverInfoCard")?.Visible == true;
    private T Find<T>(string name) where T : Node => Named<T>(Workbench, name);
    private static T Named<T>(Node parent, string name) where T : Node => parent.FindChild(name, true, false) as T ?? throw new Exception("缺少原生控件 " + name);
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), Godot.Timer.SignalName.Timeout);
    private void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
        _checks.Add(message); GD.Print("FORMATION_PAPER_REVISION_CHECK " + message);
    }
    private async Task Capture(string filename)
    {
        await Frames(2); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
        using var image = GetViewport().GetTexture().GetImage();
        Check(image is not null && !image.IsEmpty() && image.GetWidth() == _width && image.GetHeight() == _height && image.SavePng(Evidence + "/" + filename) == Error.Ok,
            "保存原生本档截图 " + filename);
        _captures.Add(new { file = filename, width = _width, height = _height, view = "actual Godot Viewport; no repaint" });
    }
    private void WriteReport(bool passed, string? error)
    {
        var directory = ProjectSettings.GlobalizePath(Evidence); System.IO.Directory.CreateDirectory(directory);
        var report = new { date = "2026-11-09", passed, stage = _stage, error, resolution = new[] { _width, _height },
            scope = "Only persistent grid, relocated tool menu, Q/E/Esc and their input isolation. Previous 590 checks and evidence are not overwritten or counted.",
            native_check_count = _checks.Count, input = "Root Viewport.PushInput mouse and physical keycode plus Unicode; embedded PopupMenu rows are located by real hover hit tests and activated by mouse clicks, never IdPressed emission. Project ui_accept/ui_cancel bindings remain unchanged.",
            appdata = Environment.GetEnvironmentVariable("APPDATA"), draft_path = _draftPath, real_journey_loaded = false, character_save_opened = false,
            artifacts_or_knowledge_granted = false, canvas_comparison = _canvasComparison, visual_review = "pending screenshot inspection", checks = _checks, screenshots = _captures };
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, passed ? "原生专项.json" : "原生专项失败.json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
