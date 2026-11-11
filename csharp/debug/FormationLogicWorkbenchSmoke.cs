using Godot;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;

/// <summary>隔离原生信号试运行验收。只创建设计台，不载入人物存档。</summary>
public partial class FormationLogicWorkbenchSmoke : Control
{
    private static string Evidence => OS.GetCmdlineUserArgs().Contains("--formation-counter-merge-smoke")
        ? "res://docs/美术规范/计数器合并原生回归_20261109"
        : "res://docs/美术规范/阵盘基础逻辑实装_20261109";
    private FormationWorkbench _workbench = null!;
    private readonly System.Collections.Generic.List<string> _checks = [];

    public override async void _Ready()
    {
        GetWindow().Mode = Window.ModeEnum.Windowed;
        GetWindow().Position = new Vector2I(-30000, -30000);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("242a2e"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 30); margin.AddThemeConstantOverride("margin_right", 30);
        margin.AddThemeConstantOverride("margin_top", 22); margin.AddThemeConstantOverride("margin_bottom", 22); AddChild(margin);
        _workbench = new FormationWorkbench { Name = "LogicWorkbench", DraftPath = "user://formation_logic_ui/isolated_draft.json" };
        margin.AddChild(_workbench);
        if (!OS.GetCmdlineUserArgs().Contains("--formation-logic-smoke")) return;
        try
        {
            await Resize(1920, 1080);
            SelectExample(2); await Frames(4);
            Check(_workbench.Canvas.Model.IsCircuitConnected, "计数器示例真实接通");
            await SelectPart(Kind.Counter);
            Find<SpinBox>("PartInterval").Value = 2;
            await Click("ApplyPartParameters");
            var counter = _workbench.Canvas.Model.Components.Values.First(part => part.Kind == Kind.Counter);
            Check(counter.Settings.Interval == 2 && !counter.Settings.FirstImmediate && counter.Settings.MinimumAmplitude == 1 && counter.Settings.SourceTag == "", "数满再发参数实际应用且不串用筛选参数");
            await Capture("01_计数器参数_1920.png");
            await InspectorTab(1);
            await Click("SendTrial");
            Check(_workbench.LastTrialResult?.OutputNodeIds.Count == 0 && Find<Label>("TrialPhases").Text.Contains("1/2"), "第一次信号只记数，原生相位显示1/2");
            Check(Find<SpinBox>("PartInterval").Value == 2 && Find<Label>("SelectionInfo").Text.Contains("N=2") && Find<Label>("TrialPhases").Text.Contains("1/2"), "参数面板、底部选中说明与实际相位的N一致");
            var beforeSave = _workbench.LastTrialResult;
            await Click("SaveDraft");
            Check(ReferenceEquals(beforeSave, _workbench.LastTrialResult) && Find<Label>("TrialPhases").Text.Contains("1/2"), "实际保存草图不清试运行相位");
            var beforeRejected = _workbench.Canvas.Model.Serialize();
            await Click("RotatePart");
            Check(beforeRejected == _workbench.Canvas.Model.Serialize() && ReferenceEquals(beforeSave, _workbench.LastTrialResult), "越界旋转被拒绝且计数保留");
            await Click("RepeatTrial");
            Check(_workbench.LastTrialResult?.Duplicate == true && _workbench.TotalControlRequests == 0 && Find<Label>("TrialPhases").Text.Contains("1/2"), "重发上次不重复计数");
            await Click("SendTrial");
            Check(_workbench.LastTrialResult?.OutputNodeIds.Count == 1 && _workbench.TotalControlRequests == 1, "第二个新信号产生一次控制请求");
            await Capture("02_计数试运行_1920.png");
            await InspectorTab(0); Find<SpinBox>("PartInterval").Value = 3; await Click("ApplyPartParameters");
            Check(_workbench.LastTrialResult is null && _workbench.TotalControlRequests == 0, "成功修改参数清试运行");

            await Resize(1280, 900);
            SelectExample(1); await Frames(3); await SelectPart(Kind.Filter); await InspectorTab(0);
            Find<SpinBox>("FilterMinimum").Value = 3; Find<LineEdit>("PartSourceTag").Text = "spell";
            await Click("ApplyPartParameters"); await Click("SaveDraft");
            Find<SpinBox>("FilterMinimum").Value = 2; await Click("ApplyPartParameters"); await Click("LoadDraft");
            var filter = _workbench.Canvas.Model.Components.Values.First(part => part.Kind == Kind.Filter);
            Check(filter.Settings.MinimumAmplitude == 3 && filter.Settings.SourceTag == "spell", "原生保存读取保留筛选参数");
            await SelectPart(Kind.Filter); await InspectorTab(1);
            Find<SpinBox>("TrialAmplitude").Value = 0; Find<LineEdit>("TrialSourceTag").Text = "spell";
            await Click("SendTrial");
            Check(_workbench.LastTrialResult?.Signal.Amplitude == 0 && _workbench.LastTrialResult.OutputNodeIds.Count == 0, "零幅值原样到达并被筛选拦下");
            await Capture("03_筛选与零幅值_1280.png");
            Find<SpinBox>("TrialAmplitude").Value = 3; await Click("SendTrial");
            Check(_workbench.LastTrialResult?.OutputNodeIds.Count == 1, "达到筛选阈值后放行");
            Find<LineEdit>("TrialSourceTag").Text = "other"; await Click("SendTrial");
            Check(_workbench.LastTrialResult?.OutputNodeIds.Count == 0, "来源不符的真实试运行被拦下");
            await Click("ClearTrial"); Check(_workbench.LastTrialResult is null && _workbench.TotalControlRequests == 0, "清试运行只清试运行状态");

            SelectExample(3); await Frames(3); await SelectPart(Kind.Counter); await InspectorTab(1);
            var immediate = _workbench.Canvas.Model.Components.Values.First(part => part.Kind == Kind.Counter);
            Check(immediate.Settings.FirstImmediate && !_workbench.Canvas.Model.Components.Values.Any(part => part.Kind == Kind.Rhythm) && Find<OptionButton>("CounterMode").Selected == 1, "先发一次示例使用同一种计数器并显示正确模式");
            await Click("SendTrial"); Check(_workbench.LastTrialResult?.OutputNodeIds.Count == 1, "先发一次从第一个新信号开始放行");
            await Click("RepeatTrial"); Check(_workbench.LastTrialResult?.Duplicate == true && _workbench.TotalControlRequests == 1, "先发一次重发不改变相位或增加请求");
            await Click("SendTrial"); Check(_workbench.LastTrialResult?.OutputNodeIds.Count == 0 && Find<Label>("TrialPhases").Text.Contains("2/3"), "第二个新信号被计数模式拦下且显示相位");
            await Capture("04_先发一次与重发_1280.png");

            await Resize(1920, 1080);
            SelectExample(4); await Frames(3); await InspectorTab(1);
            Check(_workbench.Canvas.Model.GetSignalConnections().Count(link => link.FromComponentId == "split" && link.ToComponentId == "merge") == 2, "分合保留两条不同物理路线");
            await Click("SendTrial");
            Check(_workbench.LastTrialResult?.Steps.Count(step => step.NodeId == "merge") == 1 && _workbench.LastTrialResult.OutputNodeIds.Count == 1, "两路汇回仅处理一次并抵达一次护持");
            await Capture("05_分流合流去重_1920.png");
            Check(GetViewport().GetVisibleRect().Encloses(_workbench.GetGlobalRect()), "1920设计台全部位于窗口内");

            await Resize(1280, 900);
            _workbench.Hide();
            var archive = new BaigongPanel(); AddChild(archive); var owner = new Character(); archive.show_for(owner); await Frames(6);
            var embedded = archive.FindChild("FormationWorkbench", true, false) as FormationWorkbench;
            Check(embedded is not null && GetViewport().GetVisibleRect().Encloses(embedded.GetGlobalRect()), "1280百工内嵌设计台完整可见");
            await Capture("06_百工入口_1280.png");
            archive.QueueFree(); owner.Free();
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var record = new { date = "2026-11-09", status = "passed", input = "native Viewport.PushInput mouse hit testing for buttons and canvas; native OptionButton selection", isolated_profile = true, character_save_opened = false, checks = _checks };
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(Evidence + "/原生验收.json"), System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            GD.Print($"FORMATION_LOGIC_UI_PASS checks={_checks.Count}"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError("FORMATION_LOGIC_UI_FAIL " + exception); GetTree().Quit(1); }
    }
    private T Find<T>(string name) where T : Node => _workbench.FindChild(name, true, false) as T ?? throw new Exception("缺少原生控件 " + name);
    private void SelectExample(int index)
    {
        var options = Find<OptionButton>("LogicExamples"); options.Select(index); options.EmitSignal(OptionButton.SignalName.ItemSelected, index);
    }
    private async System.Threading.Tasks.Task InspectorTab(int index)
    {
        var container = Find<TabContainer>("LogicInspector"); var bar = container.GetTabBar();
        await ClickPoint(bar.GlobalPosition + bar.GetTabRect(index).GetCenter());
        Check(container.CurrentTab == index, "实际点击右侧页签" + index);
    }
    private async System.Threading.Tasks.Task SelectPart(Kind kind)
    {
        var part = _workbench.Canvas.Model.Components.Values.First(item => item.Kind == kind);
        await ClickPoint(_workbench.Canvas.GlobalPosition + _workbench.Canvas.CellPosition(part.Origin));
        Check(_workbench.Canvas.SelectedId == part.Id, "实际点击选中" + FormationBoardCanvas.PartName(kind));
    }
    private async System.Threading.Tasks.Task Click(string name)
    {
        var control = Find<Control>(name); Check(control.IsVisibleInTree(), "按钮可见 " + name);
        await ClickPoint(control.GetGlobalRect().GetCenter());
    }
    private async System.Threading.Tasks.Task ClickPoint(Vector2 position)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true); await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true); await Frames(1);
        GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true); await Frames(2);
    }
    private async System.Threading.Tasks.Task Resize(int width, int height)
    {
        GetWindow().Size = new Vector2I(width, height); GetWindow().ContentScaleSize = new Vector2I(width, height); await Frames(5);
    }
    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message); _checks.Add(message); GD.Print("FORMATION_LOGIC_UI_CHECK " + message);
    }
    private async System.Threading.Tasks.Task Capture(string name)
    {
        await Frames(3); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
        using var image = GetViewport().GetTexture().GetImage();
        Check(image is not null && !image.IsEmpty() && image.SavePng(Evidence + "/" + name) == Error.Ok, "原生截图 " + name);
    }
}
