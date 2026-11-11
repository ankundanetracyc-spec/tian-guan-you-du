using Godot;

public partial class FormationWorkbench
{
    private FormationFlowPlayback.Plan? _lastFlowPlan;
    private string _flowBlueprint = "";
    private readonly System.Collections.Generic.List<Button> _flowReplayButtons = [];
    public FormationFlowPlayback.Plan? LastFlowPlan => _lastFlowPlan;

    private void BuildFlowReplay(VBoxContainer box, string name)
    {
        var button = Button("回放传导", () => ReplayLastFlow()); button.Name = name;
        button.Disabled = true; box.AddChild(button); _flowReplayButtons.Add(button);
        var hint = Text("朱砂看信号，赭金看供能；回放不重复发动。", 18, "aeb8aa");
        hint.Name = name + "Hint"; box.AddChild(hint);
    }

    private void RememberFlow(FormationFlowPlayback.Plan plan)
    {
        Canvas.PlayFlow(plan);
        if (!plan.Duplicate) { _lastFlowPlan = plan; _flowBlueprint = Canvas.Model.Serialize(); }
        foreach (var button in _flowReplayButtons) button.Disabled = _lastFlowPlan is null || _lastFlowPlan.Items.Count == 0;
    }

    public bool ReplayLastFlow()
    {
        if (_lastFlowPlan is null || _lastFlowPlan.Items.Count == 0 || _flowBlueprint != Canvas.Model.Serialize()) return false;
        Canvas.PlayFlow(_lastFlowPlan);
        _status.Text = "正在回放上次传导，不重复发动或扣能。"; return true;
    }

    private void ClearFlowPlayback()
    {
        _lastFlowPlan = null; _flowBlueprint = ""; Canvas?.ClearFlow();
        foreach (var button in _flowReplayButtons) button.Disabled = true;
    }

    private void RefreshFlowMatchesDraft()
    {
        if (_lastFlowPlan is not null && _flowBlueprint != Canvas.Model.Serialize()) ClearFlowPlayback();
    }

    private void ApplyControlFact(FormationGuardAssaultRuntime.Input input, string prefix = "")
    {
        if (_controlTrial is null) return;
        var before = _controlTrial.OutputStates;
        LastControlResult = _controlTrial.Apply(input);
        var plan = FormationFlowPlayback.Build(Canvas.Model, _controlTrial.Compilation, _controlTrial.Runtime.CurrentEvaluation,
            before, LastControlResult, _controlTrial.OutputStates, input.Id);
        RememberFlow(plan); ShowControlResult(LastControlResult, prefix);
    }
}
