using Godot;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;

/// <summary>局外阵图编辑与隔离人物试演；不授予正式器物或变更正式人物。</summary>
public partial class FormationWorkbench : VBoxContainer
{
    public const string DefaultDraftPath = "user://formation_drafts/basic_4x4_v1.json";
    public static string EventDisplay(string eventName) => eventName switch
    {
        "spell_played" => "术法", "card_played" => "出牌", "turn_start" => "行动开始",
        "cycle_completed" => "完成周天", _ => eventName
    };
    private static readonly Kind?[] ExampleKinds = { null, Kind.Filter, Kind.Counter, Kind.Counter, Kind.Split };
    public string DraftPath { get; set; } = DefaultDraftPath;
    public FormationBoardCanvas Canvas { get; private set; } = null!;
    public FormationLogicCircuit.RunResult? LastTrialResult { get; private set; }
    public int TotalControlRequests { get; private set; }
    private Label _status = null!, _counts = null!, _selection = null!, _validation = null!;
    private Label _selectedName = null!, _parameterHint = null!, _trialSummary = null!, _trialPhase = null!;
    private VBoxContainer _intervalRow = null!, _minimumRow = null!;
    private SpinBox _interval = null!, _minimum = null!;
    private OptionButton _examples = null!, _counterMode = null!;
    private Button _applyParameters = null!, _sendTrial = null!, _repeatTrial = null!;
    private RichTextLabel _trialTrace = null!;
    private TabContainer _inspector = null!;
    private FormationGridSimulation? _simulation;
    private FormationGridModel? _compiledModel;
    private string _compiledDraft = "", _simulationIssue = "", _trialNotice = "";
    private readonly System.Collections.Generic.Dictionary<Kind, Button> _parts = [];
    private Control? _draftOverlay;
    private LineEdit? _draftNameInput;
    private Label? _draftDialogHint;
    private ItemList? _draftList;
    private Button? _draftLoadButton;
    private string _armedOverwriteName = "";
    private readonly System.Collections.Generic.List<SavedDraftEntry> _dialogDrafts = [];
    private sealed record SavedDraftEntry(string Name, string Path, DateTime ModifiedUtc, string Description = "")
    {
        public bool IsBuiltIn => Path.StartsWith("builtin:", StringComparison.Ordinal);
    }

    public override void _Ready()
    {
        BuildPaperWorkbench();
        Canvas.Edited += Refresh;
        Canvas.SelectionChanged += _ => SelectCanvasInspection();
        ChooseTool(FormationBoardCanvas.EditTool.Select);
        Refresh("初阶单层布线，不能交叉过线。");
        Canvas.GrabFocus();
    }

    private VBoxContainer InspectorPage(string name)
    {
        var scroll = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _inspector.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, side is "top" or "bottom" ? 14 : 0);
        scroll.AddChild(margin);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12); margin.AddChild(box); return box;
    }
    private void BuildParameters()
    {
        var box = InspectorPage("参数");
        _selectedName = Text("点选盘内部件", 24, "ded6be"); _selectedName.Name = "FormationSelectedName"; box.AddChild(_selectedName);
        _parameterHint = Text("", 18, "b9c4b4"); _parameterHint.Name = "FormationParameterHint"; box.AddChild(_parameterHint);
        _intervalRow = Field(box, "间隔 N（1至12）");
        _counterMode = new OptionButton { Name = "CounterMode" }; _counterMode.AddItem("数满再发"); _counterMode.AddItem("先发一次");
        StylePaperOption(_counterMode); _intervalRow.AddChild(_counterMode);
        _interval = Number("PartInterval", 1, 12, 2); _intervalRow.AddChild(_interval);
        _minimumRow = Field(box, "最小幅值（1至9）");
        _minimum = Number("FilterMinimum", 1, 9, 1); _minimumRow.AddChild(_minimum);
        BuildAdvancedParameters(box);
        _applyParameters = Button("应用参数", ApplySelectedParameters); _applyParameters.Name = "ApplyPartParameters"; box.AddChild(_applyParameters);
        CraftingPaperStyle.ApplyButton(_applyParameters, CraftingPaperStyle.ButtonKind.Primary, 21);
    }
    private void BuildTrial()
    {
        var box = InspectorPage("信号试运行");
        BuildFlowReplay(box, "SignalTrialReplayFlow");
        box.AddChild(Text("只模拟一次成功施展术法。信号来源与强度由术法事实自动带出。", 18, "b9c4b4"));
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 8); box.AddChild(actions);
        _sendTrial = Button("施展一次术法", () => SendTrialSignal()); _sendTrial.Name = "SendTrial"; actions.AddChild(_sendTrial);
        _repeatTrial = Button("重发上次", () => RepeatTrialSignal()); _repeatTrial.Name = "RepeatTrial"; actions.AddChild(_repeatTrial);
        _sendTrial.SizeFlagsHorizontal = _repeatTrial.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var clear = Button("清试运行", ClearTrialSignal); clear.Name = "ClearTrial"; box.AddChild(clear);
        _trialSummary = Text("", 18, "d9c6a1"); _trialSummary.Name = "TrialSummary"; box.AddChild(_trialSummary);
        _trialPhase = Text("", 19, "c8d9bf"); _trialPhase.Name = "TrialPhases"; box.AddChild(_trialPhase);
        _trialTrace = new RichTextLabel
        {
            Name = "TrialTrace", BbcodeEnabled = false, FitContent = false, ScrollActive = true,
            SelectionEnabled = true, CustomMinimumSize = new Vector2(0, 155),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _trialTrace.AddThemeFontSizeOverride("normal_font_size", 18);
        _trialTrace.AddThemeColorOverride("default_color", CraftingPaperStyle.Ink); box.AddChild(_trialTrace);
    }
    private static VBoxContainer Field(Node parent, string label)
    {
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 3);
        parent.AddChild(box); box.AddChild(Text(label, 18, "bfc8b7")); return box;
    }
    private static SpinBox Number(string name, double minimum, double maximum, double initial)
    {
        var spin = new SpinBox { Name = name, MinValue = minimum, MaxValue = maximum, Step = 1, Value = initial, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        spin.CustomMinimumSize = new Vector2(0, CraftingPaperStyle.InputHeight);
        spin.AddThemeFontSizeOverride("font_size", 20);
        CraftingPaperStyle.ApplyInput(spin.GetLineEdit(), 20); return spin;
    }
    private void AddPart(VBoxContainer palette, Kind kind, string? stateKey = null, string? label = null)
    {
        var shape = FormationBoardCanvas.PartShape(kind);
        var button = new FormationPartDragButton(kind, stateKey) { Text = (label ?? FormationBoardCanvas.PartName(kind)) + $" {shape.X}×{shape.Y}", CustomMinimumSize = new Vector2(0, 44), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        CraftingPaperStyle.ApplyButton(button, fontSize: 20);
        button.Name = "Part" + kind; button.ToggleMode = false; button.Alignment = HorizontalAlignment.Left;
        button.CustomMinimumSize = new Vector2(0, 55);
        button.Icon = FormationBoardCanvas.LoadArt(FormationBoardCanvas.ArtPath(kind));
        button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width", 34);
        button.AddThemeConstantOverride("h_separation", 10);
        if (!_parts.ContainsKey(kind)) _parts[kind] = button;
        palette.AddChild(button);
    }
    private void ChooseTool(FormationBoardCanvas.EditTool tool, Kind kind = Kind.Sensor)
    {
        Canvas.SetTool(tool, kind);
        if (tool == FormationBoardCanvas.EditTool.Component && _inspector is not null)
        {
            _inspector.CurrentTab = 0; RefreshPaperInspectorTabs();
            _selectedName.Text = FormationBoardCanvas.PartName(kind) + $" · {FormationBoardCanvas.PartShape(kind).X}×{FormationBoardCanvas.PartShape(kind).Y}";
            _parameterHint.Text = kind == Kind.Sensor ? "感应战斗中真实发生的术法，不设置事件名或信号强度。" : "放入阵盘后连接接口。";
            _intervalRow.Visible = _minimumRow.Visible = _applyParameters.Visible = false; HideAdvancedParameters();
        }
        foreach (var (name, button) in _parts) SetPaperSelected(button, tool == FormationBoardCanvas.EditTool.Component && name == kind);
        if (_status is not null)
            _status.Text = ActiveToolName + (tool == FormationBoardCanvas.EditTool.Component ? " · 放下部件，R转向。" : tool switch
            {
                FormationBoardCanvas.EditTool.Connect => " · 从亮起接口或线路开始拖动。",
                FormationBoardCanvas.EditTool.Signal => " · Q信号线；仅连接信号接口与信号线。E切换供能线。",
                FormationBoardCanvas.EditTool.Energy => " · E供能线；仅连接供能接口与供能线。Q切换信号线。",
                _ => " · 右键取下部件或擦除线路。"
            });
    }
    private void ClearWires()
    {
        foreach (var cell in Canvas.Model.Wires.Keys.ToArray()) Canvas.Model.RemoveWire(cell);
        Canvas.QueueRedraw(); Refresh("已清除线路，部件保留。");
    }
    public void LoadExample(int index)
    {
        if (index < 0 || index > ExampleKinds.Length) return;
        _examples.Select(index);
        ClearFlowPlayback(); ReleaseEditableTrial();
        Canvas.SetModel(index == ExampleKinds.Length ? FormationGridModel.CreateGuardAssaultExample() : index == 3 ? FormationGridModel.CreateLogicExample(Kind.Counter, firstImmediate: true) :
            ExampleKinds[index] is Kind kind ? FormationGridModel.CreateLogicExample(kind) : FormationGridModel.CreateStarter());
        SyncBoardSize();
        ChooseTool(FormationBoardCanvas.EditTool.Select);
        Refresh("已载入" + _examples.GetItemText(index).Replace("示例：", "") + "示例；试运行重新开始。");
    }
    private void RefreshSelection(bool synchronizeCaption = false)
    {
        if (RefreshWireInspection()) return;
        if (Canvas.SelectedId is not string id || !Canvas.Model.Components.TryGetValue(id, out var part))
        {
            _selectedName.Text = "点选盘内部件"; _parameterHint.Text = "在盘面点选现有部件，再编辑对应参数。";
            _intervalRow.Visible = _minimumRow.Visible = _applyParameters.Visible = false;
            HideAdvancedParameters();
            return;
        }
        var displayName = part.Kind == Kind.StateInput ? FormationBoardCanvas.StateInputName(part.Settings.StateKey) : FormationBoardCanvas.PartName(part.Kind);
        _selectedName.Text = displayName + $" · {part.Width}×{part.Height}";
        if (synchronizeCaption) _selection.Text = Canvas.DescribeSelected();
        _intervalRow.Visible = part.Kind is Kind.Counter or Kind.Rhythm;
        _minimumRow.Visible = part.Kind == Kind.Filter;
        _applyParameters.Visible = part.Kind is Kind.Filter or Kind.Counter or Kind.Rhythm or Kind.Compare or Kind.Switch or Kind.Split;
        _interval.Value = part.Settings.Interval; _minimum.Value = part.Settings.MinimumAmplitude;
        _counterMode.Select(part.Settings.FirstImmediate ? 1 : 0);
        RefreshAdvancedParameters(part);
        _parameterHint.Text = part.Kind switch
        {
            Kind.Counter or Kind.Rhythm => part.Settings.FirstImmediate ? "先放第1次，此后每隔N次再放行。" : "数满N次再放行，然后重新计数。",
            Kind.Filter => "按术法实际带出的信号强度筛选；玩家不伪造来源。",
            Kind.Sensor => "感应战斗中成功施展的术法；不配置事件名、来源或幅值。",
            Kind.StateInput => $"固定感应{FormationBoardCanvas.StateName(part.Settings.StateKey)}，读取当前真实状态。",
            Kind.Split => "固定一入两出，信号身份与幅值保持。",
            Kind.Merge => "固定两入一出；任一路都可通过，同次信号只过一次。",
            Kind.Source => "为所有输出共用。试演容量12，每共同回合补3；阵源、线路和阵盘峰值均为6。",
            Kind.Ward => "事件触发护持。试演每次耗2阵能、提供2护盾、负荷2，每共同回合限1次。",
            _ => ""
        };
        if (part.Kind is Kind.StateInput or Kind.Compare or Kind.Not or Kind.Switch or Kind.Guard or Kind.Fire or Kind.Split) _parameterHint.Text = AdvancedHint(part);
        AppendSelectedPortInfo(part);
    }
    public void ApplySelectedParameters()
    {
        if (Canvas.SelectedId is not string id || !Canvas.Model.Components.TryGetValue(id, out var part)) return;
        var settings = TryAdvancedSettings(part, out var advancedSettings) ? advancedSettings : part.Kind switch
        {
            Kind.Counter or Kind.Rhythm => new FormationGridModel.Settings { Interval = (int)_interval.Value, FirstImmediate = _counterMode.Selected == 1 },
            Kind.Filter => part.Settings with { MinimumAmplitude = (int)_minimum.Value, SourceTag = "" },
            _ => new FormationGridModel.Settings()
        };
        var accepted = Canvas.Model.TryConfigureComponent(id, settings);
        Canvas.QueueRedraw(); Refresh(accepted ? "参数已应用。" : Canvas.Model.LastError);
    }
    private void EnsureTrialMatchesDraft()
    {
        var serialized = Canvas.Model.Serialize();
        if (ReferenceEquals(_compiledModel, Canvas.Model) && _compiledDraft == serialized) return;
        _compiledModel = Canvas.Model; _compiledDraft = serialized; _simulation = null;
        LastTrialResult = null; TotalControlRequests = 0; _simulationIssue = ""; _trialNotice = "草图已更新，试运行从头开始。";
        if (HasStateControl) { _simulationIssue = "此图包含状态控制，请切到“试演”运行当前阵图。"; return; }
        var issues = Canvas.Model.GetIssues();
        if (issues.Count > 0) { _simulationIssue = DescribeDiagramProblem().Text; return; }
        try { _simulation = new FormationGridSimulation(Canvas.Model); }
        catch (ArgumentException) { _simulationIssue = "信号线还没接好，请检查接口是否相配。"; }
    }
    public bool SendTrialSignal() => RunTrial(false);
    public bool RepeatTrialSignal() => RunTrial(true);
    private bool RunTrial(bool repeat)
    {
        EnsureTrialMatchesDraft();
        if (_simulation is null) { _trialNotice = "先完整接通草图，再送入信号。"; RefreshTrialView(); return false; }
        try
        {
            LastTrialResult = repeat ? _simulation.RepeatLast() : _simulation.Send("spell_played", "spell", 1);
            TotalControlRequests += LastTrialResult.OutputNodeIds.Count;
            RememberFlow(FormationFlowPlayback.BuildLegacy(Canvas.Model, LastTrialResult));
            _trialNotice = repeat ? "重发的是同一次发生，不新增计数或输出。" : "已送入一次新信号。";
            RefreshTrialView(); return true;
        }
        catch (ArgumentException exception) { _trialNotice = exception.Message; RefreshTrialView(); return false; }
        catch (InvalidOperationException exception) { _trialNotice = exception.Message; RefreshTrialView(); return false; }
    }
    public void ClearTrialSignal()
    {
        EnsureTrialMatchesDraft(); _simulation?.Reset(); LastTrialResult = null; TotalControlRequests = 0;
        ClearFlowPlayback();
        _trialNotice = "已清除试运行，草图保持。"; RefreshTrialView();
    }
    private void RefreshTrialView()
    {
        _sendTrial.Disabled = _simulation is null; _repeatTrial.Disabled = _simulation is null || LastTrialResult is null;
        _trialSummary.Text = _simulation is null ? "未能试运行 · " + _simulationIssue :
            $"本次护持请求{LastTrialResult?.OutputNodeIds.Count ?? 0}　累计{TotalControlRequests}";
        var phases = _simulation is null ? Array.Empty<string>() : Canvas.Model.Components.Values
            .Select(part => (part, state: _simulation.DescribeState(part.Id))).Where(item => item.state.Length > 0)
            .Select(item => FormationBoardCanvas.PartName(item.part.Kind) + "：" + item.state).ToArray();
        _trialPhase.Text = string.Join("\n", phases); _trialPhase.Visible = phases.Length > 0;
        var text = new System.Text.StringBuilder();
        text.AppendLine(_trialNotice);
        if (LastTrialResult is { } run)
        {
            var occurrence = run.Signal.EventId.StartsWith("trial_", StringComparison.Ordinal) && long.TryParse(run.Signal.EventId[6..], out var number) ? $"第{number}次信号" : "本次信号";
            text.AppendLine($"{occurrence} · {EventDisplay(run.Signal.EventName)}");
            text.AppendLine("本次输入来自实际战斗行为。");
            if (run.Duplicate) text.AppendLine("同次消息已处理，全图没有再次计数或输出。");
            foreach (var step in run.Steps)
            {
                var name = Canvas.Model.Components.TryGetValue(step.NodeId, out var part) ? FormationBoardCanvas.PartName(part.Kind) : step.NodeId;
                text.AppendLine(name + "：" + step.Message);
            }
        }
        _trialTrace.Text = text.ToString();
    }
    public void Refresh(string message)
    {
        var model = Canvas.Model;
        var componentCells = model.Components.Values.Sum(component => component.Width * component.Height);
        var signals = model.Wires.Count(wire => wire.Value == PortKind.Signal);
        var energy = model.Wires.Count(wire => wire.Value == PortKind.Energy);
        _counts.Text = $"{model.BoardSize}×{model.BoardSize}　部件{componentCells}格　信号{signals}格　供能{energy}格　空{model.BoardSize * model.BoardSize - componentCells - signals - energy}格";
        var issues = model.GetIssues();
        var feedback = issues.Count == 0 ? null : DescribeDiagramProblem();
        _validation.Text = feedback is null ? "线路已接通" : "未接通 · " + feedback.Text;
        Canvas.SetProblemMarkers(feedback?.Ports, feedback?.Cells);
        _validation.Modulate = Colors.White;
        _validation.AddThemeColorOverride("font_color", issues.Count == 0 ? CraftingPaperStyle.Accent : CraftingPaperStyle.Danger);
        _status.Text = message.Contains("无法", StringComparison.Ordinal) || message.Contains("未能", StringComparison.Ordinal) || message.Contains("已保存", StringComparison.Ordinal) || message.Contains("已读取", StringComparison.Ordinal) ? message : "";
        RefreshFlowMatchesDraft();
        EnsureTrialMatchesDraft(); RefreshSelection(true); RefreshTrialView();
        SyncBoardSize(); RefreshEditableTrial();
    }
    public bool SaveDraft() => SaveDraftFile(ProjectSettings.GlobalizePath(DraftPath), null);

    public bool SaveDraft(string name)
    {
        name = name?.Trim() ?? "";
        if (!ValidDraftName(name)) { Refresh("名字需为1至32个字，不能含控制字符。"); return false; }
        return SaveDraftFile(NamedDraftPath(name), name);
    }

    private bool SaveDraftFile(string path, string? name)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(path);
            if (directory is not null) System.IO.Directory.CreateDirectory(directory);
            var temporary = path + ".writing";
            System.IO.File.WriteAllText(temporary, Canvas.Model.Serialize());
            System.IO.File.Move(temporary, path, true);
            if (name is not null)
            {
                var namePath = path + ".name"; var nameTemporary = namePath + ".writing";
                System.IO.File.WriteAllText(nameTemporary, name);
                System.IO.File.Move(nameTemporary, namePath, true);
            }
            Refresh(name is null ? "草图已保存。" : $"已保存“{name}”草图。"); return true;
        }
        catch (Exception) { Refresh("草图未能保存，已有记录保留。"); return false; }
    }

    public void OpenSaveDraftDialog()
    {
        if (GodotObject.IsInstanceValid(_draftOverlay)) return;
        var root = CreateDraftOverlay("保存阵图草图", new Vector2(660, 350)); _draftOverlay = root;
        var body = root.FindChild("FormationDraftDialogBody", true, false) as VBoxContainer ?? throw new InvalidOperationException("草图弹窗未建立内容区。");
        body.AddChild(Text("给这张阵图起个名字（最多32个字）", 19, "bfc8b7"));
        _draftNameInput = new LineEdit { Name = "FormationDraftName", PlaceholderText = "例如：护持阵", MaxLength = 32, ClearButtonEnabled = true };
        CraftingPaperStyle.ApplyInput(_draftNameInput, 21); body.AddChild(_draftNameInput);
        body.AddChild(Text("同名草图会先提示确认，再覆盖原记录。", 17, "aeb8aa"));
        _draftDialogHint = Text("草图只保存阵盘摆放与连线。", 18, "b8c6b3"); _draftDialogHint.Name = "FormationDraftDialogHint"; body.AddChild(_draftDialogHint);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); body.AddChild(actions);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var cancel = Button("取消", CloseDraftDialog); actions.AddChild(cancel);
        var save = Button("保存", SaveNamedDraftFromDialog); CraftingPaperStyle.ApplyButton(save, CraftingPaperStyle.ButtonKind.Primary, 21); actions.AddChild(save);
        _draftNameInput.TextSubmitted += _ => SaveNamedDraftFromDialog();
        _draftNameInput.GrabFocus();
    }

    private void SaveNamedDraftFromDialog()
    {
        if (_draftNameInput is null) return;
        var name = _draftNameInput.Text.Trim();
        if (!ValidDraftName(name)) { if (_draftDialogHint is not null) _draftDialogHint.Text = "请输入1至32个字，且不要使用控制字符。"; return; }
        var path = NamedDraftPath(name);
        if (System.IO.File.Exists(path) && !string.Equals(_armedOverwriteName, name, StringComparison.Ordinal))
        {
            _armedOverwriteName = name;
            if (_draftDialogHint is not null) _draftDialogHint.Text = "已有同名草图；再点一次“保存”即可覆盖。";
            return;
        }
        _armedOverwriteName = "";
        if (SaveDraft(name)) CloseDraftDialog();
    }

    public void OpenLoadDraftDialog()
    {
        if (GodotObject.IsInstanceValid(_draftOverlay)) return;
        var root = CreateDraftOverlay("读取阵图草图", new Vector2(760, 570)); _draftOverlay = root;
        var body = root.FindChild("FormationDraftDialogBody", true, false) as VBoxContainer ?? throw new InvalidOperationException("草图弹窗未建立内容区。");
        body.AddChild(Text("内置草图可以直接载入；你保存的草图也会列在这里。", 19, "bfc8b7"));
        _draftList = new ItemList { Name = "FormationSavedDraftList", SelectMode = ItemList.SelectModeEnum.Single, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 300) };
        _draftList.AddThemeFontSizeOverride("font_size", 19); _draftList.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        body.AddChild(_draftList);
        var entries = GetSavedDrafts(); _dialogDrafts.Clear(); _dialogDrafts.AddRange(entries);
        foreach (var entry in entries)
            _draftList.AddItem(entry.IsBuiltIn
                ? $"内置 · {entry.Name}　{entry.Description}"
                : $"{entry.Name}　·　{entry.ModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 12); body.AddChild(actions);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var cancel = Button("返回", CloseDraftDialog); actions.AddChild(cancel);
        _draftLoadButton = Button("读取", LoadSelectedDraft); _draftLoadButton.Disabled = entries.Count == 0;
        CraftingPaperStyle.ApplyButton(_draftLoadButton, CraftingPaperStyle.ButtonKind.Primary, 21); actions.AddChild(_draftLoadButton);
        _draftList.ItemSelected += _ => { if (_draftLoadButton is not null) _draftLoadButton.Disabled = false; };
        if (entries.Count > 0) _draftList.Select(0);
    }

    private void LoadSelectedDraft()
    {
        if (_draftList is null) return;
        var selected = _draftList.GetSelectedItems();
        if (selected.Length == 0) return;
        var index = (int)selected[0];
        if (index < 0 || index >= _dialogDrafts.Count) return;
        var entry = _dialogDrafts[index];
        var loaded = entry.IsBuiltIn ? LoadBuiltInDraft(entry.Path["builtin:".Length..]) : LoadDraftFile(entry.Path, entry.Name);
        if (loaded) CloseDraftDialog();
    }

    private bool LoadBuiltInDraft(string id)
    {
        FormationGridModel model;
        try
        {
            model = id switch
            {
                "low_health_guard" => FormationGridModel.CreateLowHealthGuardExample(),
                "strong_spell_counter_fire" => FormationGridModel.CreateStrongSpellCounterFireExample(),
                "split_double_fire" => FormationGridModel.CreateSplitDoubleFireExample(),
                "strong_spell_filter" => FormationGridModel.CreateLogicExample(Kind.Filter),
                "three_spell_counter" => FormationGridModel.CreateLogicExample(Kind.Counter),
                "first_then_three" => FormationGridModel.CreateLogicExample(Kind.Counter, firstImmediate: true),
                _ => throw new ArgumentOutOfRangeException(nameof(id))
            };
        }
        catch (Exception)
        {
            Refresh("这张内置草图暂时无法载入。");
            return false;
        }
        ClearFlowPlayback(); ReleaseEditableTrial();
        Canvas.SetModel(model); ChooseTool(FormationBoardCanvas.EditTool.Select);
        Refresh("已载入内置草图“" + DraftDisplayName(id) + "”。");
        return true;
    }

    private static string DraftDisplayName(string id) => id switch
    {
        "low_health_guard" => "低血护罩",
        "strong_spell_counter_fire" => "强术蓄势反击",
        "split_double_fire" => "一术双击",
        "strong_spell_filter" => "强术筛选",
        "three_spell_counter" => "三术蓄势",
        "first_then_three" => "先手节律",
        _ => "内置阵图"
    };

    private Control CreateDraftOverlay(string title, Vector2 panelSize)
    {
        var host = GetParent();
        while (host is Container && host.GetParent() is Control ancestor) host = ancestor;
        var overlay = new Control { Name = "FormationDraftDialogOverlay", MouseFilter = MouseFilterEnum.Stop, ZIndex = 40 };
        host.AddChild(overlay); overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .43f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); overlay.AddChild(shade);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); overlay.AddChild(center);
        var paper = new PanelContainer { CustomMinimumSize = panelSize };
        paper.AddThemeStyleboxOverride("panel", CraftingPaperHeader.Paper(26, true, 1f)); center.AddChild(paper);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24); margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20); margin.AddThemeConstantOverride("margin_bottom", 20); paper.AddChild(margin);
        var body = new VBoxContainer { Name = "FormationDraftDialogBody", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14); margin.AddChild(body);
        var heading = new HBoxContainer(); body.AddChild(heading);
        heading.AddChild(Text(title, 25, "ded6be")); heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = Button("返回", CloseDraftDialog); heading.AddChild(close);
        body.AddChild(CraftingPaperStyle.Divider());
        return overlay;
    }

    private void CloseDraftDialog()
    {
        if (GodotObject.IsInstanceValid(_draftOverlay)) _draftOverlay!.QueueFree();
        _draftOverlay = null; _draftNameInput = null; _draftDialogHint = null; _draftList = null; _draftLoadButton = null;
        _dialogDrafts.Clear(); _armedOverwriteName = "";
    }

    private static bool ValidDraftName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 32 &&
        name == name.Trim() && !name.Any(char.IsControl) && name is not "." and not "..";

    private static string NamedDraftPath(string name)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(name);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        return System.IO.Path.Combine(ProjectSettings.GlobalizePath("user://formation_drafts"), "draft_" + hash + ".json");
    }

    private static System.Collections.Generic.List<SavedDraftEntry> GetSavedDrafts()
    {
        var builtIns = new System.Collections.Generic.List<SavedDraftEntry>
        {
            new("强术蓄势反击（5×5）", "builtin:strong_spell_counter_fire", DateTime.MinValue, "幅值≥3才计次；每3次触发火击"),
            new("一术双击（5×5）", "builtin:split_double_fire", DateTime.MinValue, "分两路触发火击，共用一处阵源"),
            new("低血护罩（5×5）", "builtin:low_health_guard", DateTime.MinValue, "生命≤30%时开启护罩"),
            new("强术筛选（4×4）", "builtin:strong_spell_filter", DateTime.MinValue, "只让幅值≥2的术法通过"),
            new("三术蓄势（4×4）", "builtin:three_spell_counter", DateTime.MinValue, "每累计3次术法放行1次"),
            new("先手节律（4×4）", "builtin:first_then_three", DateTime.MinValue, "首次立即放行，此后每3次一次")
        };
        var directory = ProjectSettings.GlobalizePath("user://formation_drafts");
        if (!System.IO.Directory.Exists(directory)) return builtIns;
        try
        {
            var saved = System.IO.Directory.GetFiles(directory, "*.json")
                .Select(path =>
                {
                    var namePath = path + ".name";
                    var name = System.IO.File.Exists(namePath) ? System.IO.File.ReadAllText(namePath).Trim() : System.IO.Path.GetFileNameWithoutExtension(path);
                    if (name == "basic_4x4_v1") name = "基础阵图（旧记录）";
                    if (!ValidDraftName(name)) name = "未命名阵图";
                    return new SavedDraftEntry(name, path, System.IO.File.GetLastWriteTimeUtc(path));
                })
                .OrderByDescending(entry => entry.ModifiedUtc).ToList();
            builtIns.AddRange(saved);
            return builtIns;
        }
        catch (System.IO.IOException) { return builtIns; }
        catch (UnauthorizedAccessException) { return builtIns; }
    }

    public bool LoadDraft(string name)
    {
        name = name?.Trim() ?? "";
        if (!ValidDraftName(name)) { Refresh("草图名字无效。"); return false; }
        return LoadDraftFile(NamedDraftPath(name), name);
    }

    private bool LoadDraftFile(string path, string name)
    {
        try
        {
            if (!System.IO.File.Exists(path)) { Refresh("找不到这张草图。"); return false; }
            var info = new System.IO.FileInfo(path);
            if (info.Length > 65536 || !FormationGridModel.TryDeserialize(System.IO.File.ReadAllText(path), out var model) || model is null)
            { Refresh("草图无法读取，当前盘面和已有记录保留。"); return false; }
            Canvas.SetModel(model); Refresh($"已读取“{name}”草图。"); return true;
        }
        catch (Exception) { Refresh("草图无法读取，当前盘面和已有记录保留。"); return false; }
    }
    public void OpenGuardTrial()
    {
        _inspector.CurrentTab = 1; RefreshPaperInspectorTabs(); EnsureEditableTrial();
    }
    public bool LoadDraft()
    {
        var path = ProjectSettings.GlobalizePath(DraftPath);
        if (!System.IO.File.Exists(path)) { Refresh("尚无保存的草图。"); return false; }
        return LoadDraftFile(path, System.IO.Path.GetFileNameWithoutExtension(path));
    }
    private static Label Text(string text, int size, string color)
    {
        var label = CraftingPaperStyle.Text(text, size, color is "aeb8aa" or "b8bcae" or "b9c4b4" or "bfc8b7" or "c0c4b7");
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart; return label;
    }
    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        CraftingPaperStyle.ApplyButton(button, fontSize: 20); button.Pressed += action; return button;
    }
}
