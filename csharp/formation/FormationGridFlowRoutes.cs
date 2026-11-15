using System;
using System.Collections.Generic;
using System.Linq;
using Cell = FormationGridModel.Cell;
using Direction = FormationGridModel.Direction;
using PortKind = FormationGridModel.PortKind;

/// <summary>只读提取端口之间真实存在的有向路线，不执行信号、能量或战斗结算。</summary>
public static class FormationGridFlowRoutes
{
    public sealed record Route(string FromComponentId, string FromPortName, string ToComponentId,
        string ToPortName, PortKind Kind, IReadOnlyList<Cell> WireCells);

    /// <summary>仅返回可达的合法端到端段；整张草图能否执行仍须由编译器核定。</summary>
    public static IReadOnlyList<Route> Build(FormationGridModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var result = new List<Route>();
        foreach (var connection in model.GetSignalConnections())
        {
            if (!model.Components.TryGetValue(connection.FromComponentId, out var from) || !model.Components.TryGetValue(connection.ToComponentId, out var to)) continue;
            var outputs = from.Ports.Where(p => p.Kind == PortKind.Signal && !p.IsInput && p.Name == connection.FromPortName).ToArray();
            var inputs = to.Ports.Where(p => p.Kind == PortKind.Signal && p.IsInput && p.Name == connection.ToPortName).ToArray();
            if (outputs.Length != 1 || inputs.Length != 1 || outputs[0].SignalType != inputs[0].SignalType || outputs[0].Unit != inputs[0].Unit) continue;
            if (TryPath(model, outputs[0], inputs[0], out var cells))
                result.Add(new Route(from.Id, outputs[0].Name, to.Id, inputs[0].Name, PortKind.Signal, cells));
        }

        var sources = model.Components.Values.Where(c => c.Kind == FormationGridModel.Kind.Source).ToArray();
        if (sources.Length == 1)
        {
            var source = sources[0]; var supply = source.Ports.Where(p => p.Kind == PortKind.Energy && !p.IsInput).ToArray();
            var powered = model.GetPowerSources();
            if (supply.Length == 1)
                foreach (var target in model.Components.Values.Where(c => FormationGridModel.IsOutput(c.Kind)))
                {
                    if (!powered.TryGetValue(target.Id, out var sourceId) || sourceId != source.Id) continue;
                    foreach (var port in target.Ports.Where(p => p.Kind == PortKind.Energy && p.IsInput))
                        if (TryPath(model, supply[0], port, out var cells))
                            result.Add(new Route(source.Id, supply[0].Name, target.Id, port.Name, PortKind.Energy, cells));
                }
        }
        return result.OrderBy(r => r.Kind).ThenBy(r => r.FromComponentId, StringComparer.Ordinal)
            .ThenBy(r => r.FromPortName, StringComparer.Ordinal).ThenBy(r => r.ToComponentId, StringComparer.Ordinal)
            .ThenBy(r => r.ToPortName, StringComparer.Ordinal).ToList().AsReadOnly();
    }

    private static bool TryPath(FormationGridModel model, FormationGridModel.Port from, FormationGridModel.Port to, out IReadOnlyList<Cell> path)
    {
        path = Array.Empty<Cell>();
        if (from.IsInput || !to.IsInput || from.Kind != to.Kind) return false;
        var start = Step(from.Cell, from.Facing); var end = Step(to.Cell, to.Facing);
        if (start == to.Cell && end == from.Cell) return true;
        if (!Wire(start) || !Wire(end)) return false;

        var previous = new Dictionary<Cell, Cell?> { [start] = null }; var queue = new Queue<Cell>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell == end)
            {
                var ordered = new List<Cell>(); Cell? cursor = cell;
                while (cursor.HasValue) { ordered.Add(cursor.Value); cursor = previous[cursor.Value]; }
                ordered.Reverse(); path = ordered.AsReadOnly(); return true;
            }
            foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                var next = Step(cell, direction);
                if (Wire(next) && previous.TryAdd(next, cell)) queue.Enqueue(next);
            }
        }
        return false;

        bool Wire(Cell cell) => cell.X >= 0 && cell.X < model.BoardSize && cell.Y >= 0 && cell.Y < model.BoardSize &&
            model.Wires.TryGetValue(cell, out var kind) && kind == from.Kind && model.GetComponentAt(cell) is null;
    }

    private static Cell Step(Cell cell, Direction direction) => direction switch
    {
        Direction.North => cell with { Y = cell.Y - 1 }, Direction.East => cell with { X = cell.X + 1 },
        Direction.South => cell with { Y = cell.Y + 1 }, Direction.West => cell with { X = cell.X - 1 }, _ => cell
    };
}
