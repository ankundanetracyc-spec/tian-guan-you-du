using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Environment = System.Environment;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;
using Tool = FormationBoardCanvas.EditTool;
using PortKind = FormationGridModel.PortKind;

/// <summary>真实五格编辑与当前草图试演专项。所有人物、资料及草图写入均隔离。</summary>
public partial class FormationFreeBuildUiSmoke : Control
{
    private const string EvidenceRoot = "res://docs/美术规范/五格复杂阵盘自由构筑_20261109";
    private readonly List<string> _checks = [];
    private readonly List<object> _shots = [];
    private readonly List<object> _bounds = [];
    private BaigongPanel _archive = null!;
    private Character _owner = null!;
    private FormationPaperWorkspace _workspace = null!;
    private FormationWorkbench Workbench => _workspace.Workbench;
    private FormationBoardCanvas Canvas => Workbench.Canvas;
    private int _width, _height;
    private Vector2 _pointer;
    private bool _leftHeld;
    private string _stage = "初始入口";
    private string _draftPath = "";
    private bool PreviewOnly => OS.GetCmdlineUserArgs().Contains("--formation-freebuild-preview-only");
    private string Evidence => EvidenceRoot + (IsFlowRun ? "/分类与传导" : "") + "/" + _width;

    public override async void _Ready()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--formation-freebuild-ui-smoke")) return;
        _width = OS.GetCmdlineUserArgs().Contains("--formation-freebuild-width=1280") ? 1280 : 1920; _height = _width == 1920 ? 1080 : 720;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = GetWindow().ContentScaleSize = new(_width, _height);
        GetWindow().GuiEmbedSubwindows = true; GetWindow().Position = new(-30000, -30000);
        try
        {
            await Frames(6);
            Check(GetViewport().GetVisibleRect().Size.IsEqualApprox(new(_width, _height)) &&
                (Environment.GetEnvironmentVariable("APPDATA") ?? "").Contains("formation_freebuild_ui", StringComparison.OrdinalIgnoreCase), "视口与独立用户资料均为本轮配置");
            _owner = new Character { Name = "FormationFreeBuildIsolatedOwner" };
            _archive = new BaigongPanel(); AddChild(_archive); _archive.show_for(_owner); await Frames(3);
            await Click(Named<Button>(_archive, "OpenFormationWorkspace")); await Frames(4);
            _workspace = GetTree().Root.GetNodeOrNull<FormationPaperWorkspace>("FormationPaperWorkspace") ?? throw new Exception("百工入口未打开工作台");
            _draftPath = "user://formation_freebuild_ui/" + _width + "/isolated_draft.json"; Workbench.DraftPath = _draftPath;
            Check(Canvas.Model.BoardSize == 4 && !_owner.IsInsideTree(), "真实入口仍保留初阶四格，未载入人物旅程");
            if (IsFlowRun) { await RunClassificationAndFlow(); return; }
            if (!PreviewOnly) await PlaceNewPartAndShortcuts();

            _stage = "真实载入五格守攻";
            await Pick(Find<OptionButton>("LogicExamples"), 5); await MoveOutside();
            Check(Canvas.Model.BoardSize == 5 && Find<OptionButton>("FormationBoardSize").Selected == 1 && Canvas.Model.IsCircuitConnected,
                "实际示例菜单载入合法五格并同步规格");
            Check(Canvas.Model.Complexity == 9 && Canvas.Model.Components.Values.All(p => p.Kind != Kind.Eye) && Canvas.GridVisible && !Canvas.HasPersistentLabels,
                "五格阵图不含固定阵眼，常显格线而无名称状态签");
            foreach (var kind in new[] { Kind.StateInput, Kind.Compare, Kind.Not, Kind.Switch, Kind.Fire, Kind.Guard })
                Check(Find<Button>("Part" + kind).Icon is not null, "高级部件有真实同族贴图 " + kind);
            Check(Workbench.FindChild("PartAnd", true, false) is null && Workbench.FindChild("PartOr", true, false) is null,
                "没有图的与或门未混入部件栏");
            RecordBounds(Find<Control>("FormationDrawingArea")); RecordBounds(Find<Control>("FormationInspectorPaper"));
            await Capture("01_五格完整编辑页.png");

            _stage = "比较器参数页";
            await KeyPress(Key.Escape); await SelectPart(Kind.Compare); await Click(Find<Button>("FormationParametersTab"));
            Check(Find<OptionButton>("ControlCompareMode").Selected == 2 && Find<SpinBox>("ControlCompareLow").Value == 30 && Find<SpinBox>("ControlCompareHigh").Value == 60,
                "右栏读取实际比较器区间和阈值");
            await EnsureVisible(Find<SpinBox>("ControlCompareHigh")); await MoveOutside(); await Capture("02_真实比较器阈值.png");

            _stage = "当前草图低生命实际护罩";
            var blueprint = Canvas.Model.Serialize(); await Click(Find<Button>("OpenGuardTrial"));
            Check(Find<TabContainer>("LogicInspector").CurrentTab == 2 && Workbench.EditableTrial is not null,
                "顶部试演入口进入第三页并编译当前草图");
            var actor = Workbench.ControlTrialOwner!; var enemy = Workbench.ControlTrialEnemy!;
            Check(!actor.IsInsideTree() && !enemy.IsInsideTree() && Workbench.EditableTrial!.Runtime.Circuit.Nodes.Count == 8,
                "试演使用隔离真实人物和当前八个功能节点");
            await SetHealth(20); await Click(Find<Button>("ControlTrialSend"));
            var low = Workbench.EditableTrial!.GetSnapshot();
            Check(low.Health == 20 && low.GuardRunning && low.Shield == 18 && low.EnergySpent == 2 && low.Attacks == 0 && actor.shield == 18,
                "二十生命真实付款开罩，守态事件没有攻击");
            Check(Canvas.Model.Serialize() == blueprint, "真实试演不会改写局外草图");
            await TrialCapture("03_当前草图二十生命开罩.png");

            _stage = "当前草图高生命实际攻击";
            await SetHealth(80); var before = Workbench.EditableTrial!.GetSnapshot(); await Click(Find<Button>("ControlTrialSend"));
            var high = Workbench.EditableTrial!.GetSnapshot();
            Check(!high.GuardRunning && actor.shield == 0 && high.EnemyHealth < before.EnemyHealth && enemy.current_health == high.EnemyHealth && high.SourceEnergy == before.SourceEnergy - 3,
                "八十生命撤本器罩，新事件真实扣敌人生命和阵能");
            await Click(Find<Button>("ControlTrialRepeat"));
            Check(Workbench.LastControlResult?.Duplicate == true && Workbench.EditableTrial.GetSnapshot() == high, "重发实际事件不重复攻击或扣费");
            await TrialCapture("04_当前草图八十生命伤敌.png");
            if (!PreviewOnly) await EditedGraphProof();

            _stage = "退出释放";
            var closingActor = Workbench.ControlTrialOwner; var closingEnemy = Workbench.ControlTrialEnemy;
            await Click(Find<Button>("ReturnFormationWorkspace")); await Frames(3);
            Check(!GodotObject.IsInstanceValid(_workspace) && !GodotObject.IsInstanceValid(closingActor) && !GodotObject.IsInstanceValid(closingEnemy) && _archive.IsVisibleInTree(),
                "返回释放工作台和试演人物并恢复百工");
            WriteReport(true, null); _archive.QueueFree(); _owner.Free(); await Frames(2);
            GD.Print($"FORMATION_FREEBUILD_UI_PASS mode={(PreviewOnly ? "preview" : "full")} width={_width} height={_height} checks={_checks.Count}"); GetTree().Quit();
        }
        catch (Exception error)
        {
            try { await Capture("失败现场.png"); } catch { }
            if (IsFlowRun) WriteFlowReport(false, error.ToString()); else WriteReport(false, error.ToString());
            GD.PushError("FORMATION_FREEBUILD_UI_FAIL " + _stage + " " + error);
            if (GodotObject.IsInstanceValid(_workspace)) _workspace.QueueFree();
            if (GodotObject.IsInstanceValid(_archive)) _archive.QueueFree();
            if (GodotObject.IsInstanceValid(_owner)) _owner.Free();
            await Frames(2); GetTree().Quit(1);
        }
    }

    private async Task PlaceNewPartAndShortcuts()
    {
        _stage = "五格自由放件和Q/E";
        await Pick(Find<OptionButton>("FormationBoardSize"), 1, probeIsolation: true);
        Check(Canvas.Model.BoardSize == 5 && Canvas.Model.GetComponentAt(new Cell(2, 2)) is null, "真实规格菜单扩大旧草图并释放盘心格");
        await Click(Find<Button>("PartStateInput")); await ClickCell(new(4, 4));
        var part = Canvas.Model.Components.Values.Single(p => p.Kind == Kind.StateInput);
        Check(part.Origin == new Cell(4, 4), "真实选择高级件并放入第五行第五列");
        await KeyPress(Key.Escape); await ClickCell(part.Origin); await Click(Find<Button>("FormationParametersTab"));
        await Pick(Find<OptionButton>("ControlStateSource"), 2); await Click(Find<Button>("ApplyPartParameters"));
        Check(Canvas.Model.Components[part.Id].Settings.StateKey == "mana" && Canvas.Model.Components[part.Id].Settings.Unit == FormationValueUnit.Mana,
            "状态来源参数实际写入元件并采用对应单位");
        await ClickCell(part.Origin); await KeyPress(Key.R, 'r');
        Check(Canvas.Model.Components[part.Id].Rotation == 1, "高级件实际R旋转");
        await ClickCell(part.Origin, MouseButton.Right); Check(!Canvas.Model.Components.ContainsKey(part.Id), "高级件右键可取下");
        await KeyPress(Key.Q, 'q'); await ClickCell(new(4, 4));
        Check(Canvas.Tool == Tool.Signal && Canvas.Model.Wires[new(4, 4)] == PortKind.Signal, "Q在第五行真实铺信号线");
        await KeyPress(Key.E, 'e'); await ClickCell(new(3, 4));
        Check(Canvas.Tool == Tool.Energy && Canvas.Model.Wires[new(3, 4)] == PortKind.Energy && Canvas.Model.Wires[new(4, 4)] == PortKind.Signal, "E在第五行铺供能且不混已有信号线");
        await KeyPress(Key.Escape); await ClickCell(new(4, 4), MouseButton.Right); await ClickCell(new(3, 4), MouseButton.Right);
        await Pick(Find<OptionButton>("FormationBoardSize"), 0);
        Check(Canvas.Model.BoardSize == 4 && Canvas.Model.IsCircuitConnected, "清理新物后可缩回四格并保持原基础连通");
    }

    private async Task EditedGraphProof()
    {
        _stage = "真实改阈值并证明行为变化";
        var previousActor = Workbench.ControlTrialOwner; var previousEnemy = Workbench.ControlTrialEnemy;
        await Click(Find<Button>("FormationParametersTab")); await SelectPart(Kind.Compare);
        await Pick(Find<OptionButton>("ControlCompareMode"), 2, probeIsolation: true);
        var unchanged = Canvas.Model.Serialize(); var low = Find<SpinBox>("ControlCompareLow");
        await Click(low.GetLineEdit()); await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e');
        Check(Canvas.Tool == Tool.Select && Canvas.Model.Serialize() == unchanged, "比较阈值数值输入收到Q/E时不切铺线或改图");
        await SetNumber(low, "5"); await SetNumber(Find<SpinBox>("ControlCompareHigh"), "15"); await Click(Find<Button>("ApplyPartParameters"));
        var compare = Canvas.Model.Components.Values.Single(p => p.Kind == Kind.Compare);
        Check(compare.Settings.Low == 5 && compare.Settings.High == 15 && !GodotObject.IsInstanceValid(previousActor) && !GodotObject.IsInstanceValid(previousEnemy),
            "实际应用5/15阈值并释放旧图试演人物");
        await Click(Find<Button>("FormationControlTrialTab")); await SetHealth(20);
        await Click(Find<CheckButton>("ControlTrialSignalDetails")); var tag = Find<LineEdit>("ControlTrialTag");
        await Click(tag); await KeyPress(Key.A, control: true); await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e');
        Check(tag.Text == "qe" && Canvas.Tool == Tool.Select, "展开事件参数后来源框真实键入qe而不串快捷键");
        await TypeText(tag, "术法"); await Click(Find<CheckButton>("ControlTrialSignalDetails"));
        await Click(Find<Button>("ControlTrialSend")); var changed = Workbench.EditableTrial!.GetSnapshot();
        Check(changed.Health == 20 && !changed.GuardRunning && changed.Shield == 0 && changed.Attacks == 1 && changed.EnemyHealth < 100,
            "同为二十生命，仅改草图阈值就从护罩变为真实攻击");
        Check(Workbench.EditableTrial.Runtime.Configuration.LowHealth == 30 && Workbench.EditableTrial.Runtime.Circuit.Nodes[compare.Id].Low == 5,
            "运行遵当前节点阈值，不回套固定试演参数");
        var live = Workbench.EditableTrial; var serialized = Canvas.Model.Serialize();
        await Click(Find<Button>("SaveDraft"));
        Check(System.IO.File.Exists(ProjectSettings.GlobalizePath(_draftPath)) && ReferenceEquals(live, Workbench.EditableTrial) && Workbench.EditableTrial!.GetSnapshot() == changed,
            "保存真实五格草图，不洗当前源余量与执行账本");
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(_draftPath), "{\"version\":2,\"broken\":true}"); await Click(Find<Button>("LoadDraft"));
        Check(Canvas.Model.Serialize() == serialized && ReferenceEquals(live, Workbench.EditableTrial), "坏草图读取不破坏当前图或运行实例");
        await Click(Find<Button>("SaveDraft")); await Click(Find<Button>("LoadDraft"));
        Check(Canvas.Model.Serialize() == serialized && Canvas.Model.BoardSize == 5 && Canvas.Model.Components[compare.Id].Settings.Low == 5,
            "原生读回五格、新参数及实际连接");
        await TrialCapture("05_改阈值后同生命转攻.png");

        _stage = "真实断线后拒绝运行";
        var actor = Workbench.ControlTrialOwner; var enemy = Workbench.ControlTrialEnemy;
        var connection = Workbench.EditableTrial!.Compilation.PhysicalConnections.First(c => c.WireCells.Count > 0);
        var critical = connection.WireCells[0];
        await PickTool("擦除"); await ClickCell(critical); await Click(Find<Button>("FormationControlTrialTab"));
        Check(!Canvas.Model.IsCircuitConnected && Workbench.EditableTrial is null && Workbench.LastControlResult is null && Find<Button>("ControlTrialSend").Disabled,
            "实际擦断关键线后明确拒演，不继续旧图或固定守攻");
        Check(!GodotObject.IsInstanceValid(actor) && !GodotObject.IsInstanceValid(enemy) && Find<Label>("ControlTrialStatus").Text.Contains("无法试演", StringComparison.Ordinal),
            "拒演已释放旧人物，并给出断线原因");
        await MoveOutside(); await Capture("06_断线明确拒绝运行.png");
        await KeyPress(Key.Q, 'q'); await ClickCell(critical); await KeyPress(Key.Escape); await Click(Find<Button>("FormationControlTrialTab"));
        Check(Canvas.Model.IsCircuitConnected && Workbench.EditableTrial is not null, "真实接回线路后可以重新编译当前图");
    }

    private async Task SetHealth(int health) { await SetNumber(Find<SpinBox>("ControlTrialHealth"), health.ToString()); await Click(Find<Button>("ControlTrialSetHealth")); }
    private async Task SelectPart(Kind kind)
    {
        await KeyPress(Key.Escape); var part = Canvas.Model.Components.Values.First(p => p.Kind == kind); await ClickCell(part.Origin);
        Check(Canvas.SelectedId == part.Id, "画布实际命中" + FormationBoardCanvas.PartName(kind));
    }
    private async Task TrialCapture(string file) { await EnsureVisible(Find<Label>("ControlTrialReadout")); await MoveOutside(); await Wait(.18); RecordBounds(Find<Label>("ControlTrialReadout")); await Capture(file); }
    private async Task Pick(OptionButton option, int index, bool probeIsolation = false)
    {
        // Bottom menus can open upward over the original button. Keep the initial
        // press held until the intended row, rather than releasing on that overlay.
        await EnsureVisible(option); var point = option.GetGlobalRect().GetCenter();
        await Move(point); await Mouse(point, MouseButton.Left, true);
        if (probeIsolation)
        {
            var tool = Canvas.Tool; var blueprint = Canvas.Model.Serialize();
            await KeyPress(Key.Q, 'q'); await KeyPress(Key.E, 'e');
            Check(option.GetPopup().Visible && Canvas.Tool == tool && Canvas.Model.Serialize() == blueprint,
                option.Name + "下拉展开时Q/E不切背后工具或改图");
        }
        await PickPopup(option.GetPopup(), index, releaseOnly: true);
    }
    private async Task PickTool(string label)
    {
        var menu = Find<MenuButton>("FormationToolMenu"); await Click(menu); var popup = menu.GetPopup();
        await PickPopup(popup, Enumerable.Range(0, popup.ItemCount).First(i => popup.GetItemText(i).StartsWith(label, StringComparison.Ordinal)));
    }
    private async Task PickPopup(PopupMenu popup, int index, bool releaseOnly = false)
    {
        if (!popup.Visible) throw new Exception("真实菜单未展开");
        for (var y = 4f; y < popup.Size.Y - 4; y += Math.Max(3f, popup.Size.Y / (popup.ItemCount * 4f)))
        {
            var point = new Vector2(popup.Position.X + popup.Size.X * .55f, popup.Position.Y + y); await Move(point);
            if (popup.GetFocusedItem() != index) continue;
            if (!releaseOnly) await Mouse(point, MouseButton.Left, true);
            await Mouse(point, MouseButton.Left, false); await Frames(2);
            if (popup.Visible) throw new Exception("鼠标未选择菜单项 " + index); return;
        }
        throw new Exception("实际鼠标未命中菜单项 " + index);
    }
    private async Task SetNumber(SpinBox number, string value) { await TypeText(number.GetLineEdit(), value); if (Math.Abs(number.Value - double.Parse(value)) > .01) throw new Exception("数字未真实键入 " + number.Name); }
    private async Task TypeText(LineEdit input, string value)
    {
        await Click(input); await KeyPress(Key.A, control: true);
        foreach (var character in value)
        {
            GetViewport().PushInput(new InputEventKey { Pressed = true, Unicode = character }, true);
            GetViewport().PushInput(new InputEventKey { Pressed = false, Unicode = character }, true);
        }
        await KeyPress(Key.Enter); await Frames(2);
    }
    private async Task EnsureVisible(Control control)
    {
        for (var node = control.GetParent(); node is not null; node = node.GetParent())
            if (node is ScrollContainer scroll) { scroll.EnsureControlVisible(control); await Frames(2); }
    }
    private async Task Click(Control control)
    {
        await EnsureVisible(control); var point = control.GetGlobalRect().GetCenter();
        if (!control.IsVisibleInTree() || !GetViewport().GetVisibleRect().HasPoint(point)) throw new Exception("点击目标不可见 " + control.Name);
        await Move(point); await Mouse(point, MouseButton.Left, true); await Mouse(point, MouseButton.Left, false);
    }
    private Vector2 CellPoint(Cell cell) => Canvas.GetGlobalTransformWithCanvas() * Canvas.CellPosition(cell);
    private async Task ClickCell(Cell cell, MouseButton button = MouseButton.Left) { var point = CellPoint(cell); await Move(point); await Mouse(point, button, true); await Mouse(point, button, false); }
    private Task MoveOutside() => Move(Find<Button>("FormationHelp").GetGlobalRect().GetCenter());
    private async Task Move(Vector2 point)
    {
        var relative = point - _pointer; _pointer = point;
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative, Velocity = relative * 60,
            ButtonMask = _leftHeld ? MouseButtonMask.Left : 0 }, true); await Frames(2);
    }
    private async Task Mouse(Vector2 point, MouseButton button, bool down)
    {
        if (button == MouseButton.Left) _leftHeld = down;
        GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = down,
            ButtonMask = down ? button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right : 0 }, true); await Frames(2);
    }
    private async Task KeyPress(Key key, uint unicode = 0, bool control = false)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, CtrlPressed = control, Pressed = true }, true); await Frames(1);
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, CtrlPressed = control, Pressed = false }, true); await Frames(1);
    }
    private T Find<T>(string name) where T : Node => Named<T>(Workbench, name);
    private static T Named<T>(Node node, string name) where T : Node => node.FindChild(name, true, false) as T ?? throw new Exception("缺少原生控件 " + name);
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), Godot.Timer.SignalName.Timeout);
    private void Check(bool passed, string message) { if (!passed) throw new Exception(message); _checks.Add(message); GD.Print("FORMATION_FREEBUILD_UI_CHECK " + message); }
    private void RecordBounds(Control control)
    {
        var rect = control.GetGlobalRect(); Check(GetViewport().GetVisibleRect().Encloses(rect.Grow(-1)), "本档边界完整 " + control.Name);
        _bounds.Add(new { name = control.Name.ToString(), x = rect.Position.X, y = rect.Position.Y, width = rect.Size.X, height = rect.Size.Y });
    }
    private async Task Capture(string file)
    {
        await Frames(2); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence)); using var image = GetViewport().GetTexture().GetImage();
        Check(image.GetWidth() == _width && image.GetHeight() == _height && image.SavePng(Evidence + "/" + file) == Error.Ok, "保存本档真实视口 " + file);
        _shots.Add(new { file, width = _width, height = _height });
    }
    private void WriteReport(bool passed, string? error)
    {
        var folder = ProjectSettings.GlobalizePath(Evidence); System.IO.Directory.CreateDirectory(folder);
        var result = new { date = "2026-11-09", passed, error, stage = _stage, preview_only = PreviewOnly, native_checks = _checks.Count, resolution = new[] { _width, _height },
            appdata = Environment.GetEnvironmentVariable("APPDATA"), draft_path = _draftPath, real_journey_loaded = false, character_save_opened = false,
            items_or_knowledge_granted = false, scope = "Actual five-grid editing and current compiled circuit trial. Earlier fixed graph/UI counts are not reused.",
            checks = _checks, bounds = _bounds, screenshots = _shots, visual_review = "pending per-image inspection" };
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, passed ? "原生验收.json" : "原生验收失败.json"), System.Text.Json.JsonSerializer.Serialize(result,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
