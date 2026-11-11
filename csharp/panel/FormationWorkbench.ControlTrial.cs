using Godot;
using TrialAction = FormationGuardAssaultRuntime.ActionKind;
using TrialInput = FormationGuardAssaultRuntime.Input;

public partial class FormationWorkbench
{
    private FormationEditableTrial? _controlTrial;
    private Character? _controlOwner, _controlEnemy;
    private string _controlBlueprint = "";
    private int _controlSession;
    private long _controlSequence;
    private TrialInput? _lastControlInput;
    private Label _controlStatus = null!, _controlReadout = null!;
    private RichTextLabel _controlTrace = null!;
    private SpinBox _controlHealth = null!, _controlDamage = null!;
    private Button _controlRun = null!;
    private readonly System.Collections.Generic.List<Button> _controlActions = [];
    public FormationEditableTrial? EditableTrial => _controlTrial;
    public Character? ControlTrialOwner => _controlOwner;
    public Character? ControlTrialEnemy => _controlEnemy;
    public FormationGuardAssaultRuntime.Result? LastControlResult { get; private set; }

    private void BuildEditableTrial()
    {
        var box = InspectorPage("阵法运行");
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 8); box.AddChild(heading);
        heading.AddChild(Text("阵法调试", 24, "ded6be"));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _controlRun = Button("运行阵法", () => EnsureEditableTrial(true)); _controlRun.Name = "ControlTrialStart"; heading.AddChild(_controlRun);
        _controlStatus = Text("接通阵图后可开始。", 18, "aeb8aa"); _controlStatus.Name = "ControlTrialStatus"; box.AddChild(_controlStatus);
        _controlReadout = Text("", 19, "ded6be"); _controlReadout.Name = "ControlTrialReadout"; box.AddChild(_controlReadout);
        BuildFlowReplay(box, "ControlTrialReplayFlow");

        box.AddChild(Text("战斗行为", 21, "ded6be"));
        var signals = new HBoxContainer(); signals.AddThemeConstantOverride("separation", 8); box.AddChild(signals);
        AddControlAction(signals, "施展术法", "ControlTrialSend", () => SendControlInput(TrialAction.Signal));
        AddControlAction(signals, "施展强术", "ControlTrialSendStrong", () => SendControlInput(TrialAction.Signal, signalAmplitude: 3));

        box.AddChild(Text("状态调整", 21, "ded6be"));
        var hp = new HBoxContainer(); hp.AddThemeConstantOverride("separation", 8); box.AddChild(hp);
        hp.AddChild(Text("生命", 18, "bfc8b7"));
        _controlHealth = Number("ControlTrialHealth", 0, 100, 100); _controlHealth.CustomMinimumSize = new Vector2(100, 0); hp.AddChild(_controlHealth);
        AddControlAction(hp, "设置", "ControlTrialSetHealth", () => SendControlInput(TrialAction.SetHealth, (int)_controlHealth.Value));
        var impact = new HBoxContainer(); impact.AddThemeConstantOverride("separation", 8); box.AddChild(impact);
        impact.AddChild(Text("数值", 18, "bfc8b7"));
        _controlDamage = Number("ControlTrialDamage", 0, 10000, 10); _controlDamage.CustomMinimumSize = new Vector2(100, 0); impact.AddChild(_controlDamage);
        AddControlAction(impact, "受击", "ControlTrialHit", () => SendControlInput(TrialAction.Damage, (int)_controlDamage.Value));
        AddControlAction(impact, "治疗", "ControlTrialHeal", () => SendControlInput(TrialAction.Heal, (int)_controlDamage.Value));
        AddControlAction(box, "推进一回合", "ControlTrialNextRound", () => SendControlInput(TrialAction.NextRound));
        _controlTrace = new RichTextLabel { Name = "ControlTrialTrace", BbcodeEnabled = false, FitContent = false, ScrollActive = true, SelectionEnabled = true, CustomMinimumSize = new Vector2(0, 180) };
        _controlTrace.AddThemeFontSizeOverride("normal_font_size", 18); _controlTrace.AddThemeColorOverride("default_color", CraftingPaperStyle.Ink); box.AddChild(_controlTrace);
        foreach (var action in _controlActions) action.Disabled = true;
    }

    private void AddControlAction(Node parent, string caption, string name, Action action)
    {
        var button = Button(caption, action); button.Name = name; button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _controlActions.Add(button); parent.AddChild(button);
    }

    private void RefreshEditableTrial()
    {
        if (_controlStatus is null) return;
        if (_controlBlueprint != Canvas.Model.Serialize())
        {
            ReleaseEditableTrial();
            foreach (var action in _controlActions) action.Disabled = true;
            _controlStatus.Text = "阵图已更新，请重新运行。";
            _controlReadout.Text = ""; _controlTrace.Text = "";
        }
    }

    public bool EnsureEditableTrial(bool restart = false)
    {
        var blueprint = Canvas.Model.Serialize();
        if (!restart && _controlTrial is not null && _controlBlueprint == blueprint) return true;
        ClearFlowPlayback();
        ReleaseEditableTrial();
        try
        {
            var issues = Canvas.Model.GetIssues();
            if (issues.Count > 0) { RefuseControlTrial(string.Join("；", issues.Take(3))); return false; }
            var compilation = FormationGridControlCompiler.Compile(Canvas.Model);
            _controlOwner = TrialCharacter(100, 20); _controlEnemy = TrialCharacter(100, 0);
            var actor = new FormationGuardActor(_controlOwner, _controlEnemy);
            // 明确的隔离试演配置；不写入正式图谱、成器数据或人物。
            var settings = new FormationGuardAssaultRuntime.Settings(12, 12, 3, 6, 6, 6, 2, 18, 2, 3, 9, 3, 1, 30, 60);
            _controlTrial = new FormationEditableTrial(compilation, actor, settings);
            ControlTrialDiagnostic = "";
            _controlBlueprint = blueprint; _controlSequence = 0; _controlSession++; _lastControlInput = null;
            _controlHealth.Value = 100;
            _controlRun.Text = "重新运行";
            foreach (var button in _controlActions) button.Disabled = false;
            ApplyControlFact(new TrialInput($"editor_{_controlSession}_initial", TrialAction.Refresh), "阵法已启动。"); return true;
        }
        catch (ArgumentException error) { RefuseControlTrial(error.Message); return false; }
        catch (InvalidOperationException error) { RefuseControlTrial(error.Message); return false; }
    }

    private void RefuseControlTrial(string message)
    {
        ReleaseEditableTrial(); ControlTrialDiagnostic = message;
        var feedback = DescribeDiagramProblem();
        _controlStatus.Text = "无法运行：" + feedback.Text;
        Canvas.SetProblemMarkers(feedback.Ports, feedback.Cells);
        _controlStatus.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger);
        _controlReadout.Text = ""; _controlTrace.Text = "";
        foreach (var button in _controlActions) button.Disabled = true;
    }

    private void SendControlInput(TrialAction action, int value = 0, int signalAmplitude = 1)
    {
        if (!EnsureEditableTrial() || _controlTrial is null) return;
        var input = new TrialInput($"editor_{_controlSession}_{++_controlSequence}", action, value,
            EventName: "spell_played", SourceTag: "spell", Amplitude: action == TrialAction.Signal ? signalAmplitude : 1);
        _lastControlInput = input; ApplyControlFact(input);
    }

    private void RepeatControlInput()
    {
        if (!EnsureEditableTrial() || _controlTrial is null || _lastControlInput is null) return;
        ApplyControlFact(_lastControlInput);
    }

    private void ShowControlResult(FormationGuardAssaultRuntime.Result result, string prefix = "")
    {
        var s = result.Snapshot; _controlStatus.AddThemeColorOverride("font_color", CraftingPaperStyle.Ink);
        _controlStatus.Text = (prefix.Length == 0 ? "" : prefix + "\n") + string.Join("\n", result.Messages.Select(PlayerTrialMessage));
        _controlReadout.Text = $"生命{s.Health}/{s.MaxHealth}　法力{s.Mana}\n护盾{s.Shield}　其中本阵{s.GuardRemaining}\n阵能{s.SourceEnergy}/12　敌方生命{s.EnemyHealth}\n共同回合{s.Round}　耗能{s.EnergySpent}";
        var lines = new System.Collections.Generic.List<string>();
        foreach (var (id, state) in _controlTrial!.OutputStates)
        {
            var name = Canvas.Model.Components.TryGetValue(id, out var part) ? FormationBoardCanvas.PartName(part.Kind) + $"({part.Origin.X + 1},{part.Origin.Y + 1})" : id;
            var status = state.Kind == FormationControlCircuit.NodeKind.GuardOutput ? $"{(state.Running ? "防护中" : "未防护")}，本器余量{state.Remaining}" : $"本回合执行{state.UsedThisRound}次";
            lines.Add($"{name}：{(state.Powered ? "已供能" : "未供能")}，{status}，累计付款{state.Payments}次、执行{state.Executions}次");
        }
        if (result.Duplicate) lines.Add("同次重发，没有重复扣能或执行。");
        else foreach (var step in result.Steps) lines.Add((Canvas.Model.Components.TryGetValue(step.NodeId, out var part) ? FormationBoardCanvas.PartName(part.Kind) : step.Label) + "本次控制：" + step.Value);
        _controlTrace.Text = string.Join("\n", lines);
        var values = _controlTrial.Runtime.CurrentEvaluation.Values.ToDictionary(pair => pair.Key,
            pair => result.Duplicate && pair.Value.Type == FormationSignalType.Event ? "同次重发，不重复执行" : (pair.Value.Type == FormationSignalType.Number ? "本次采样 " : "本次控制 ") + pair.Value.Display, StringComparer.Ordinal);
        foreach (var (id, state) in _controlTrial.OutputStates)
            values[id] = state.Kind == FormationControlCircuit.NodeKind.GuardOutput ? $"{(state.Running ? "防护中" : "未防护")}，本器余量{state.Remaining}，累计付款{state.Payments}次" : $"本回合执行{state.UsedThisRound}次，累计执行{state.Executions}次";
        foreach (var part in Canvas.Model.Components.Values)
        {
            if (part.Kind == FormationGridModel.Kind.Source) values[part.Id] = $"阵能{s.SourceEnergy}/12";
            if (_controlTrial.Runtime.Circuit.CounterPhases.TryGetValue(part.Id, out var phase)) values[part.Id] = $"计次{phase}/{part.Settings.Interval}";
        }
        Canvas.SetControlReadouts(values);
    }

    private static Character TrialCharacter(int health, int mana) => new() { entity_data = new HumanData { max_health = health, current_health = health, max_mana = mana, current_mana = mana } };
    private void ReleaseEditableTrial()
    {
        _controlTrial?.Dispose(); _controlTrial = null; LastControlResult = null; _lastControlInput = null; _controlBlueprint = "";
        foreach (var actor in new[] { _controlOwner, _controlEnemy })
        {
            if (!GodotObject.IsInstanceValid(actor)) continue;
            var data = actor!.entity_data; actor.Free(); if (GodotObject.IsInstanceValid(data)) data.Dispose();
        }
        _controlOwner = _controlEnemy = null;
        if (_controlRun is not null && GodotObject.IsInstanceValid(_controlRun)) _controlRun.Text = "运行阵法";
        Canvas?.SetControlReadouts(null);
    }
}
