using Godot;
using Cell = FormationGridModel.Cell;
using Direction = FormationGridModel.Direction;
using PortKind = FormationGridModel.PortKind;

public partial class FormationWorkbench
{
    /// <summary>只由实际点击触发，保持操作状态；信息在右页展示，不弹出悬停框。</summary>
    private void SelectCanvasInspection()
    {
        if (Canvas.SelectedId is not null || Canvas.SelectedWireCell is Cell)
        {
            _inspector.CurrentTab = 0;
            RefreshPaperInspectorTabs();
        }
        RefreshSelection();
    }

    private bool RefreshWireInspection()
    {
        if (Canvas.SelectedWireCell is not Cell cell || !Canvas.Model.Wires.TryGetValue(cell, out var kind)) return false;
        var signal = kind == PortKind.Signal;
        _selectedName.Text = (signal ? "信号线" : "供能线") + $" · ({cell.X + 1},{cell.Y + 1})";
        _intervalRow.Visible = _minimumRow.Visible = _applyParameters.Visible = false;
        HideAdvancedParameters();
        var mask = Canvas.Model.GetConnectionMask(cell);
        var directions = new[] { (Direction.North, "上"), (Direction.East, "右"), (Direction.South, "下"), (Direction.West, "左") }
            .Where(item => (mask & item.Item1) != 0).Select(item => item.Item2).ToArray();
        var lines = new System.Collections.Generic.List<string>
        {
            signal ? "传递事件、开关或数值信号。" : "从阵源向阵器供能。",
            "连接方向：" + (directions.Length == 0 ? "尚未连接" : string.Join("、", directions))
        };
        var routes = FormationGridFlowRoutes.Build(Canvas.Model).Where(route => route.Kind == kind && route.WireCells.Contains(cell)).ToArray();
        string Part(string id) => Canvas.Model.Components.TryGetValue(id, out var part)
            ? (part.Kind == FormationGridModel.Kind.StateInput ? FormationBoardCanvas.StateInputName(part.Settings.StateKey) : FormationBoardCanvas.PartName(part.Kind)) + $"({part.Origin.X + 1},{part.Origin.Y + 1})"
            : "未知部件";
        foreach (var route in routes)
        {
            var description = Part(route.FromComponentId) + " → " + Part(route.ToComponentId);
            if (!lines.Contains(description, StringComparer.Ordinal)) lines.Add(description);
        }
        if (routes.Length == 0) lines.Add("线路尚未连通两端。");
        _parameterHint.Text = string.Join("\n", lines);
        return true;
    }

    private void AppendSelectedPortInfo(FormationGridModel.Component part)
    {
        var ports = part.Ports.Select(port =>
        {
            var facing = port.Facing switch { Direction.North => "上", Direction.East => "右", Direction.South => "下", Direction.West => "左", _ => "" };
            var signal = port.Kind == PortKind.Signal;
            var name = signal ? FormationBoardCanvas.SignalCaption(port.SignalType) + (port.Unit == FormationValueUnit.None ? "" : "·" + FormationBoardCanvas.UnitName(port.Unit)) : "供能";
            var connected = (Canvas.Model.GetConnectionMask(port.Cell) & port.Facing) != 0;
            return $"{facing} · {name}{(port.IsInput ? "入口" : "出口")} · {(connected ? "已接" : "未接")}";
        }).ToArray();
        if (ports.Length == 0) return;
        if (_parameterHint.Text.Length > 0) _parameterHint.Text += "\n\n";
        _parameterHint.Text += "接口\n" + string.Join("\n", ports);
        if (Canvas.GetControlReadout(part.Id) is string value && value.Length > 0)
            _parameterHint.Text += "\n\n运行：" + value;
    }
}
