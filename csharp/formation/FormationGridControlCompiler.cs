using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>从真正的格位、具名物理端口与同源供能编译控制图，不生成隐藏连接。</summary>
public static class FormationGridControlCompiler
{
    public sealed record Compilation(FormationControlCircuit Circuit, IReadOnlyList<string> PoweredOutputIds,
        string SourceId, bool SourcePresent, IReadOnlyList<FormationGridModel.SignalConnection> PhysicalConnections);

    public static Compilation Compile(FormationGridModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var issues = model.GetIssues();
        if (issues.Count > 0) throw new ArgumentException(string.Join("；", issues), nameof(model));
        var sources = model.Components.Values.Where(c => c.Kind == FormationGridModel.Kind.Source).ToArray();
        if (sources.Length != 1) throw new ArgumentException("本次试演须有唯一真实阵源，不能合并多个来源。", nameof(model));
        var power = model.GetPowerSources();
        var outputs = model.Components.Values.Where(c => FormationGridModel.IsOutput(c.Kind)).Select(c => c.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (outputs.Length == 0 || outputs.Any(id => !power.TryGetValue(id, out var source) || source != sources[0].Id))
            throw new ArgumentException("每个执行阵器须通过实际供能线连到同一阵源。", nameof(model));
        var physical = model.GetSignalConnections();
        var nodes = model.Components.Values.Where(c => c.Kind is not (FormationGridModel.Kind.Eye or FormationGridModel.Kind.Source)).Select(Node).ToArray();
        var links = physical.Select(p => new FormationControlCircuit.Link(p.FromComponentId, p.FromPortName, p.ToComponentId, p.ToPortName)).ToArray();
        var circuit = new FormationControlCircuit(nodes, links);
        return new Compilation(circuit, Array.AsReadOnly(outputs), sources[0].Id, true, physical);
    }

    public static bool TryCompile(FormationGridModel model, out Compilation? compilation, out IReadOnlyList<string> issues)
    {
        compilation = null;
        try { compilation = Compile(model); issues = Array.Empty<string>(); return true; }
        catch (ArgumentException error) { issues = new[] { error.Message }; return false; }
        catch (InvalidOperationException error) { issues = new[] { error.Message }; return false; }
    }

    private static FormationControlCircuit.Node Node(FormationGridModel.Component component)
    {
        var settings = component.Settings;
        var kind = component.Kind switch
        {
            FormationGridModel.Kind.Sensor => FormationControlCircuit.NodeKind.EventInput,
            FormationGridModel.Kind.Filter => FormationControlCircuit.NodeKind.EventFilter,
            FormationGridModel.Kind.Counter or FormationGridModel.Kind.Rhythm => FormationControlCircuit.NodeKind.EventCounter,
            FormationGridModel.Kind.Split => FormationControlCircuit.NodeKind.Split,
            FormationGridModel.Kind.Merge => FormationControlCircuit.NodeKind.EventMerge,
            FormationGridModel.Kind.Ward => FormationControlCircuit.NodeKind.ShieldOutput,
            FormationGridModel.Kind.StateInput => FormationControlCircuit.NodeKind.StateInput,
            FormationGridModel.Kind.Compare => FormationControlCircuit.NodeKind.Compare,
            FormationGridModel.Kind.Not => FormationControlCircuit.NodeKind.Not,
            FormationGridModel.Kind.Switch => FormationControlCircuit.NodeKind.Switch,
            FormationGridModel.Kind.Fire => FormationControlCircuit.NodeKind.AttackOutput,
            FormationGridModel.Kind.Guard => FormationControlCircuit.NodeKind.GuardOutput,
            FormationGridModel.Kind.And => FormationControlCircuit.NodeKind.And,
            FormationGridModel.Kind.Or => FormationControlCircuit.NodeKind.Or,
            _ => throw new ArgumentException("不支持的控制元件。")
        };
        return new FormationControlCircuit.Node(component.Id, kind, FormationGridModel.Name(component.Kind))
        {
            Key = component.Kind == FormationGridModel.Kind.StateInput ? settings.StateKey : settings.InputEvent,
            SignalType = settings.SignalType, Unit = settings.Unit, Comparison = settings.Comparison,
            Low = settings.Low, High = settings.High, Interval = settings.Interval,
            FirstImmediate = settings.FirstImmediate || component.Kind == FormationGridModel.Kind.Rhythm,
            MinimumAmplitude = settings.MinimumAmplitude, SourceTag = settings.SourceTag, NormallyClosed = settings.NormallyClosed
        };
    }
}
