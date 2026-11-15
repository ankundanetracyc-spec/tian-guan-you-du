using Godot;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;
using Port = FormationGridModel.Port;
using Cell = FormationGridModel.Cell;

public partial class FormationWorkbench
{
    private sealed record DiagramFeedback(string Text, Port[] Ports, Cell[] Cells);
    public string ControlTrialDiagnostic { get; private set; } = "";

    private static string PlayerTrialMessage(string message) => message
        .Replace("重复通知：不重放操作、不扣费、不刷新回合。", "同次操作已处理，不重复发动或耗能。", StringComparison.Ordinal)
        .Replace("实际提交，支付", "发动，消耗", StringComparison.Ordinal)
        .Replace("状态已更新，本次没有新增付款或攻击。", "状态已更新，本次没有额外消耗或攻击。", StringComparison.Ordinal)
        .Replace("缺能：未付款、未占成功额度。", "阵能不足。", StringComparison.Ordinal)
        .Replace("承载不足：未付款、未占成功额度。", "承载不足。", StringComparison.Ordinal)
        .Replace("未实际生效，已退回预留，不占次数。", "未能生效，不消耗阵能或次数。", StringComparison.Ordinal)
        .Replace("本次事件未抵达瞬发出口；已拦的旧事件不会补发。", "这次信号被拦住，没有触发输出。", StringComparison.Ordinal);

    private DiagramFeedback DescribeDiagramProblem()
    {
        var model = Canvas.Model;
        static string At(Cell cell) => $"（{cell.X + 1},{cell.Y + 1}）";
        DiagramFeedback Message(string text, Port[]? ports = null, Cell[]? cells = null) => new(text, ports ?? [], cells ?? []);
        if (!model.Components.Values.Any(p => p.Kind == Kind.Source)) return Message("缺少阵源。");
        if (!model.Components.Values.Any(p => p.Kind is Kind.Sensor or Kind.StateInput)) return Message("缺少输入部件。");
        if (!model.Components.Values.Any(p => FormationGridModel.IsOutput(p.Kind))) return Message("缺少输出部件。");
        foreach (var part in model.Components.Values.OrderBy(p => FormationGridModel.IsOutput(p.Kind) ? 0 : 1).ThenBy(p => p.Origin.Y).ThenBy(p => p.Origin.X))
        foreach (var port in part.Ports.OrderBy(p => p.Kind == PortKind.Signal ? 0 : 1))
        {
            if ((model.GetConnectionMask(port.Cell) & port.Facing) != 0) continue;
            var wire = port.Kind == PortKind.Signal ? "信号线" : "供能线";
            return Message(FormationBoardCanvas.PartName(part.Kind) + "未接" + wire + At(port.Cell) + "。", [port]);
        }
        var ends = model.Wires.Where(p => System.Numerics.BitOperations.PopCount((uint)model.GetConnectionMask(p.Key)) < 2).ToArray();
        if (ends.Length > 0)
        {
            var kind = ends[0].Value;
            return Message((kind == PortKind.Signal ? "信号线" : "供能线") + "断开" + At(ends[0].Key) + "。", cells: ends.Where(p => p.Value == kind).Select(p => p.Key).ToArray());
        }
        foreach (var route in model.GetSignalConnections())
        {
            var from = model.Components[route.FromComponentId].Ports.First(p => !p.IsInput && p.Kind == PortKind.Signal && p.Name == route.FromPortName);
            var to = model.Components[route.ToComponentId].Ports.First(p => p.IsInput && p.Kind == PortKind.Signal && p.Name == route.ToPortName);
            if (from.SignalType != to.SignalType) return Message("信号类型不匹配" + At(to.Cell) + "。", [from, to]);
            if (from.Unit != to.Unit) return Message("数值单位不匹配" + At(to.Cell) + "。", [from, to]);
        }
        var power = model.GetPowerSources();
        foreach (var part in model.Components.Values.Where(p => FormationGridModel.IsOutput(p.Kind)))
            if (!power.ContainsKey(part.Id)) return Message(FormationBoardCanvas.PartName(part.Kind) + "未接到阵源" + At(part.Ports.First(p => p.Kind == PortKind.Energy).Cell) + "。", part.Ports.Where(p => p.Kind == PortKind.Energy).ToArray());
        if (model.GetIssues().Any(issue => issue.Contains("回路", StringComparison.Ordinal)))
            return Message("信号线形成回路。", cells: model.Wires.Where(p => p.Value == PortKind.Signal).Select(p => p.Key).ToArray());
        return Message("阵图连接有误。");
    }
}
