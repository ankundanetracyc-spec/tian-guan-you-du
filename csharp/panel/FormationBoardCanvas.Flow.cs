using Godot;
using Cell = FormationGridModel.Cell;
using Port = FormationGridModel.Port;
using Direction = FormationGridModel.Direction;
using PortKind = FormationGridModel.PortKind;
using Route = FormationGridFlowRoutes.Route;

public partial class FormationBoardCanvas
{
    private FormationFlowPlayback.Plan? _flowPlan;
    private double _flowTime;
    private bool _flowPlaying;
    private Port[] _problemPorts = [];
    private Cell[] _problemCells = [];
    public FormationFlowPlayback.Plan? FlowPlan => _flowPlan;
    public double FlowPlaybackTime => _flowTime;
    public bool IsFlowPlaying => _flowPlaying;
    public int ProblemMarkerCount => _problemPorts.Length + _problemCells.Length;
    public Vector2[] GetFlowPath(Route route) => FlowPath(route, GridRect.Size.X / _model.BoardSize);

    public void PlayFlow(FormationFlowPlayback.Plan plan)
    {
        _flowPlan = plan; _flowTime = 0; _flowPlaying = plan.Items.Count > 0;
        SetProcess(_flowPlaying); QueueRedraw();
    }

    public void ClearFlow()
    {
        _flowPlan = null; _flowTime = 0; _flowPlaying = false; SetProcess(false); QueueRedraw();
    }

    public void SetProblemMarkers(System.Collections.Generic.IEnumerable<Port>? ports, System.Collections.Generic.IEnumerable<Cell>? cells)
    {
        _problemPorts = ports?.ToArray() ?? []; _problemCells = cells?.ToArray() ?? []; QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_flowPlaying || _flowPlan is null) return;
        _flowTime += delta;
        if (_flowTime >= _flowPlan.Duration + .28) { _flowPlaying = false; SetProcess(false); }
        QueueRedraw();
    }

    private void DrawFlowAndProblems()
    {
        var unit = GridRect.Size.X / _model.BoardSize;
        if (_flowPlaying && _flowPlan is not null)
        foreach (var item in _flowPlan.Items)
        {
            var elapsed = _flowTime - item.Delay;
            if (elapsed < 0 || elapsed > item.Duration + .22) continue;
            var points = FlowPath(item.Route, unit);
            if (points.Length < 2) continue;
            var width = FormationTraceRenderer.StrokeWidth(item.Route.Kind, unit);
            var color = item.IsEnergy ? new Color("fff0bd") : item.IsLow ? new Color("ad9a83") : new Color("ffca9a");
            var length = 0f; for (var i = 1; i < points.Length; i++) length += points[i - 1].DistanceTo(points[i]);
            var distance = length * Mathf.Clamp((float)(elapsed / item.Duration), 0, 1);
            var tail = Mathf.Max(unit * .55f, width * 5);
            if (elapsed <= item.Duration)
            {
                var ribbon = PathSection(points, Mathf.Max(0, distance - tail), distance);
                if (ribbon.Length >= 2) DrawPolyline(ribbon, new Color(color, item.IsLow ? .58f : .88f), Mathf.Max(1.4f, width * .72f), true);
                var head = PointOnPath(points, distance);
                if (item.IsLow) DrawArc(head, Mathf.Max(2.5f, width * .8f), 0, Mathf.Tau, 18, color, 1.2f, true);
                else
                {
                    DrawCircle(head, Mathf.Max(3.7f, width * .95f), new Color(color, .18f), true, -1, true);
                    DrawCircle(head, Mathf.Max(2.2f, width * .6f), color, true, -1, true);
                }
            }
            else
            {
                var fade = 1 - (float)((elapsed - item.Duration) / .22);
                DrawArc(points[^1], width + unit * .035f * (1 - fade), 0, Mathf.Tau, 18, new Color(color, fade * .7f), 1.2f, true);
            }
        }
        var danger = new Color("b66345");
        foreach (var port in _problemPorts)
        {
            var part = _model.GetComponentAt(port.Cell); if (part is null) continue;
            var position = DisplayPortPoint(part, port, CellPosition(port.Cell) + FlowAxis(port.Facing) * unit / 2);
            DrawArc(position, Mathf.Max(5, unit * .09f), 0, Mathf.Tau, 24, danger, 1.5f, true);
        }
        foreach (var cell in _problemCells) DrawArc(CellPosition(cell), Mathf.Max(5, unit * .09f), 0, Mathf.Tau, 24, danger, 1.5f, true);
    }

    private void DrawCoordinateNumbers(Rect2 grid, float unit)
    {
        var font = GD.Load<FontFile>(CraftingPaperStyle.FontPath); const int size = 17; var color = new Color("d4c6a9");
        for (var index = 0; index < _model.BoardSize; index++)
        {
            var text = (index + 1).ToString(); var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
            DrawString(font, grid.Position + new Vector2((index + .5f) * unit - width / 2, -9), text, HorizontalAlignment.Left, -1, size, color);
            DrawString(font, grid.Position + new Vector2(-13 - width, (index + .5f) * unit + size * .33f), text, HorizontalAlignment.Left, -1, size, color);
        }
    }

    private Vector2[] FlowPath(Route route, float unit)
    {
        if (!_model.Components.TryGetValue(route.FromComponentId, out var from) || !_model.Components.TryGetValue(route.ToComponentId, out var to)) return [];
        var source = from.Ports.FirstOrDefault(p => !p.IsInput && p.Kind == route.Kind && p.Name == route.FromPortName);
        var target = to.Ports.FirstOrDefault(p => p.IsInput && p.Kind == route.Kind && p.Name == route.ToPortName);
        if (source.Name is null || target.Name is null) return [];
        var first = FlowPortPath(from, source); var last = FlowPortPath(to, target);
        var middle = new System.Collections.Generic.List<Vector2> { first[^1] };
        middle.AddRange(route.WireCells.Select(CellPosition)); middle.Add(last[^1]);
        var result = first.Take(first.Length - 1).Concat(SmoothFlowPath(middle, route, unit)).Concat(last.AsEnumerable().Reverse().Skip(1)).ToArray();
        return result.Where((point, i) => i == 0 || point.DistanceSquaredTo(result[i - 1]) > .0001f).ToArray();
    }

    private Vector2[] FlowPortPath(FormationGridModel.Component part, Port port)
    {
        var boundary = CellPosition(port.Cell) + FlowAxis(port.Facing) * GridRect.Size.X / _model.BoardSize / 2;
        if (!_logicArt.TryGetValue(part.Kind, out var art)) return [boundary];
        var match = art.Ports.FirstOrDefault(p => p.Kind == port.Kind && p.Input == port.IsInput && RotatedSide(p.Side, part.Rotation) == port.Facing);
        if (match is null) return [boundary];
        var shape = PartShape(part.Kind);
        return [ArtPoint(part, match.CoreUv * new Vector2(shape.X, shape.Y) * 128, true),
            ArtPoint(part, match.MouthUv * new Vector2(shape.X, shape.Y) * 128, true), ArtPoint(part, match.Boundary, false)];
    }

    private Vector2[] SmoothFlowPath(System.Collections.Generic.List<Vector2> raw, Route route, float unit)
    {
        var result = new System.Collections.Generic.List<Vector2> { raw[0] };
        for (var i = 1; i < raw.Count - 1; i++)
        {
            var corner = raw[i]; var before = (corner - raw[i - 1]).Normalized(); var after = (raw[i + 1] - corner).Normalized();
            if (before.Dot(after) > .999f || System.Numerics.BitOperations.PopCount((uint)_model.GetConnectionMask(route.WireCells[i - 1])) >= 3) { result.Add(corner); continue; }
            var reach = Mathf.Min(unit * .11f, Mathf.Min(raw[i - 1].DistanceTo(corner), corner.DistanceTo(raw[i + 1])) * .44f);
            var entry = corner - before * reach; var exit = corner + after * reach; result.Add(entry);
            for (var step = 1; step <= 16; step++) { var t = step / 16f; result.Add(entry * ((1 - t) * (1 - t)) + corner * (2 * (1 - t) * t) + exit * (t * t)); }
        }
        result.Add(raw[^1]); return result.ToArray();
    }

    private static Vector2 PointOnPath(Vector2[] points, float distance)
    {
        for (var i = 1; i < points.Length; i++) { var span = points[i - 1].DistanceTo(points[i]); if (distance <= span) return points[i - 1].Lerp(points[i], span <= 0 ? 0 : distance / span); distance -= span; }
        return points[^1];
    }
    private static Vector2[] PathSection(Vector2[] points, float from, float to)
    {
        var result = new System.Collections.Generic.List<Vector2> { PointOnPath(points, from) }; var distance = 0f;
        for (var i = 1; i < points.Length; i++) { distance += points[i - 1].DistanceTo(points[i]); if (distance > from && distance < to) result.Add(points[i]); }
        result.Add(PointOnPath(points, to)); return result.ToArray();
    }
    private static Vector2 FlowAxis(Direction facing) => facing switch { Direction.North => Vector2.Up, Direction.East => Vector2.Right, Direction.South => Vector2.Down, Direction.West => Vector2.Left, _ => Vector2.Zero };
}
