using System;
using System.Collections.Generic;
using System.Linq;
using Kind = FormationGridModel.Kind;

/// <summary>按草图真实端口编译的信号试运行，不发放器物、不支付费用、不产生战斗效果。</summary>
public sealed class FormationGridSimulation
{
    private readonly FormationLogicCircuit _logic;
    private readonly IReadOnlyDictionary<string, FormationLogicCircuit.Node> _nodes;
    private FormationLogicCircuit.Signal? _last;
    private long _sequence;

    public FormationGridSimulation(FormationGridModel model)
    {
        var issues = model.GetIssues();
        if (issues.Count > 0) throw new ArgumentException(string.Join("；", issues), nameof(model));
        var nodes = model.Components.Values.Where(c => c.Kind is not (Kind.Eye or Kind.Source)).Select(c =>
            new FormationLogicCircuit.Node(c.Id, c.Kind switch
            {
                Kind.Sensor => FormationLogicCircuit.NodeKind.Input,
                Kind.Filter => FormationLogicCircuit.NodeKind.Filter,
                Kind.Counter or Kind.Rhythm => FormationLogicCircuit.NodeKind.Counter,
                Kind.Split => FormationLogicCircuit.NodeKind.Split,
                Kind.Merge => FormationLogicCircuit.NodeKind.Merge,
                Kind.Ward => FormationLogicCircuit.NodeKind.Output,
                _ => throw new ArgumentException("未知信号元件。")
            })
            {
                Interval = c.Settings.Interval, MinimumAmplitude = c.Settings.MinimumAmplitude,
                FirstImmediate = c.Settings.FirstImmediate || c.Kind == Kind.Rhythm,
                EventName = c.Settings.InputEvent,
                AcceptedSourceTags = string.IsNullOrEmpty(c.Settings.SourceTag) ? Array.Empty<string>() : new[] { c.Settings.SourceTag }
            });
        _logic = new FormationLogicCircuit(nodes, model.GetSignalConnections()
            .Select(link => new FormationLogicCircuit.Edge(link.FromComponentId, link.ToComponentId)));
        _nodes = _logic.Nodes;
    }

    public FormationLogicCircuit.RunResult Send(string eventName, string sourceTag, int amplitude)
    {
        var signal = new FormationLogicCircuit.Signal("trial_" + ++_sequence, eventName, sourceTag, amplitude);
        var result = _logic.Emit(signal); _last = signal;
        return result;
    }
    public FormationLogicCircuit.RunResult RepeatLast() => _last is not null ? _logic.Emit(_last)
        : throw new InvalidOperationException("先发送一次信号。");
    public void Reset() { _logic.Reset(); _last = null; _sequence = 0; }
    public string DescribeState(string nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return "";
        return node.Kind switch
        {
            FormationLogicCircuit.NodeKind.Counter => $"计数进度{_logic.GetCount(nodeId)}/{node.Interval}（{(node.FirstImmediate ? "先发一次" : "数满再发")}）",
            _ => ""
        };
    }
}
