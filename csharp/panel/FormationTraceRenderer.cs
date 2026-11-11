using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Cell = FormationGridModel.Cell;
using Direction = FormationGridModel.Direction;
using PortKind = FormationGridModel.PortKind;

/// <summary>只绘制阵盘实物脉纹。拓扑、端口与能否连通始终由FormationGridModel决定。</summary>
public static class FormationTraceRenderer
{
    public const string SignalMaterialPath = "res://textures/formations/basic_4x4_ink_v2_20261108/signal_material.png";
    public const string EnergyMaterialPath = "res://textures/formations/basic_4x4_ink_v2_20261108/energy_material.png";
    private static readonly Dictionary<PortKind, Texture2D> Materials = new();
    private static readonly Direction[] Directions = { Direction.North, Direction.East, Direction.South, Direction.West };
    public static bool MaterialsAvailable => Material(PortKind.Signal) is not null && Material(PortKind.Energy) is not null;

    private sealed class Node
    {
        public string Id = "";
        public Vector2 Point;
        public bool Terminal;
        public List<Node> Links = new();
    }

    private sealed class Trace
    {
        public PortKind Kind;
        public Vector2[] Points = Array.Empty<Vector2>();
        public bool SealStart;
        public bool SealEnd;
        public bool MainTrunk;
        public bool Closed;
    }

    /// <summary>在部件图下绘制铜纹刻线；同网先画全体墨边再画色芯，T内不留叠层墨缝。</summary>
    public static void Draw(Control canvas, FormationGridModel model, Rect2 grid, bool editing)
    {
        if (grid.Size.X <= 0 || grid.Size.Y <= 0) return;
        var unit = Mathf.Min(grid.Size.X, grid.Size.Y) / model.BoardSize;
        var traces = BuildTraces(model, grid, unit).OrderBy(trace => trace.MainTrunk).ToArray();
        foreach (var trace in traces)
        {
            var width = Width(trace.Kind, unit);
            canvas.DrawPolyline(trace.Points, new Color("51463d"), width + 3 * unit / 128f, true);
            StrokeCaps(canvas, trace, width + 3 * unit / 128f, new Color("51463d"));
        }
        foreach (var trace in traces)
        {
            var width = Width(trace.Kind, unit);
            var color = new Color(trace.Kind == PortKind.Signal ? "bb6652" : "ceb787");
            canvas.DrawPolyline(trace.Points, color, width, true); StrokeCaps(canvas, trace, width, color);
        }
        foreach (var trace in traces)
        {
            var width = Width(trace.Kind, unit); var offset = new Vector2(-.45f, -.65f) * unit / 128f;
            canvas.DrawPolyline(trace.Points.Select(point => point + offset).ToArray(), new Color(trace.Kind == PortKind.Signal ? "e0a18b55" : "ead9ad70"), Mathf.Max(.55f, width * .17f), true);
            if (editing)
            {
                if (trace.SealStart) DrawBreakpoint(canvas, trace.Points[0], (trace.Points[0] - trace.Points[1]).Normalized(), width, unit, trace.Kind);
                if (trace.SealEnd) DrawBreakpoint(canvas, trace.Points[^1], (trace.Points[^1] - trace.Points[^2]).Normalized(), width, unit, trace.Kind);
            }
        }
    }
    private static void StrokeCaps(Control canvas, Trace trace, float width, Color color)
    {
        if (trace.Closed) return;
        if (trace.SealStart) canvas.DrawCircle(trace.Points[0], width / 2, color, true, -1, true);
        if (trace.SealEnd) canvas.DrawCircle(trace.Points[^1], width / 2, color, true, -1, true);
    }

    /// <summary>在部件图之后画微型接触焊盘。空闲仅画已接接触脚，未接提示仅在编辑态出现。</summary>
    public static void DrawPortSockets(Control canvas, FormationGridModel model, Rect2 grid, bool editing,
        Func<FormationGridModel.Component, bool>? includeComponent = null)
    {
        if (grid.Size.X <= 0 || grid.Size.Y <= 0) return;
        var unit = Mathf.Min(grid.Size.X, grid.Size.Y) / model.BoardSize;
        var drawn = new HashSet<(int, int, PortKind)>();
        foreach (var component in model.Components.Values)
            if (includeComponent?.Invoke(component) != false)
            foreach (var port in component.Ports)
            {
                var axis = Offset(port.Facing);
                var connected = (model.GetConnectionMask(port.Cell) & port.Facing) != 0;
                if (!connected && !editing) continue;
                var key = (port.Cell.X * 2 + 1 + (int)axis.X, port.Cell.Y * 2 + 1 + (int)axis.Y, port.Kind);
                if (!drawn.Add(key)) continue;
                var side = new Vector2(-axis.Y, axis.X);
                var point = Center(port.Cell, grid, unit) + axis * unit * .5f;
                var width = Width(port.Kind, unit);
                var half = width * .58f;
                var contact = point - axis * width * .16f;
                var left = contact - side * half;
                var right = contact + side * half;
                if (connected)
                {
                    canvas.DrawLine(left, right, new Color(.27f, .24f, .16f, .88f), Mathf.Max(1.4f, unit * .011f), true);
                    canvas.DrawLine(left - axis * .35f, right - axis * .35f, new Color(.64f, .55f, .36f, .9f), Mathf.Max(.6f, unit * .0045f), true);
                    var contactLength = Mathf.Max(1.4f, width * .55f);
                    canvas.DrawLine(contact - axis * contactLength, contact + axis * contactLength * .3f, new Color(.72f, .64f, .45f, .82f), Mathf.Max(.65f, width * .16f), true);
                }
                else
                {
                    var color = new Color(.66f, .52f, .32f, .48f);
                    canvas.DrawLine(left, contact - side * width * .18f, color, .75f, true);
                    canvas.DrawLine(contact + side * width * .18f, right, color, .75f, true);
                }
                if (editing)
                {
                    var core = port.Kind == PortKind.Signal ? new Color(.63f, .34f, .29f, .88f) : new Color(.48f, .66f, .67f, .88f);
                    canvas.DrawLine(contact - axis * width * .24f, contact + axis * width * .32f, core, Mathf.Max(.65f, width * .18f), true);
                }
            }
    }

    private static List<Trace> BuildTraces(FormationGridModel model, Rect2 grid, float unit)
    {
        var result = new List<Trace>();
        foreach (var kind in new[] { PortKind.Signal, PortKind.Energy })
        {
            var cells = model.Wires.Where(pair => pair.Value == kind).OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X).Select(pair => pair.Key).ToArray();
            var nodes = cells.ToDictionary(cell => cell, cell => new Node { Id = $"{cell.X}:{cell.Y}", Point = Center(cell, grid, unit) });
            var all = nodes.Values.ToList();
            foreach (var cell in cells)
            {
                var node = nodes[cell];
                var mask = model.GetConnectionMask(cell);
                foreach (var direction in Directions)
                {
                    if ((mask & direction) == 0) continue;
                    var axis = Offset(direction);
                    var neighbour = new Cell(cell.X + (int)axis.X, cell.Y + (int)axis.Y);
                    if (nodes.TryGetValue(neighbour, out var other))
                    {
                        if (!node.Links.Contains(other)) node.Links.Add(other);
                        if (!other.Links.Contains(node)) other.Links.Add(node);
                    }
                    else
                    {
                        // 图片留白由Canvas按真实UV补接，主线只到逻辑格边。
                        var terminal = new Node { Id = node.Id + ":" + direction, Point = node.Point + axis * unit * .5f, Terminal = true };
                        node.Links.Add(terminal);
                        terminal.Links.Add(node);
                        all.Add(terminal);
                    }
                }
            }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in all.Where(node => !node.Terminal && node.Links.Count == 0))
                result.Add(new Trace
                {
                    Kind = kind, Points = new[] { node.Point - Vector2.Right * unit * .135f, node.Point + Vector2.Right * unit * .135f }, SealStart = true, SealEnd = true
                });
            // T的对向两臂形成一条贯通主干，第三臂停在主干内部，不增加第四方向。
            foreach (var node in all)
                foreach (var next in node.Links)
                    if (Next(node, next) is null && !visited.Contains(Edge(node, next)))
                        result.Add(Walk(node, next, visited, kind, unit));
            foreach (var node in all)
                foreach (var next in node.Links)
                    if (!visited.Contains(Edge(node, next))) result.Add(Walk(node, next, visited, kind, unit));
            // 相邻器件的两段精确口线在同一格边会合，不添加烘焙焊盘或宽短接块。
        }
        return result;
    }

    private static Trace Walk(Node start, Node next, HashSet<string> visited, PortKind kind, float unit)
    {
        var raw = new List<Vector2> { start.Point };
        var previous = start;
        var current = next;
        var mainTrunk = false;
        visited.Add(Edge(previous, current));
        for (var guard = 0; guard < 128; guard++)
        {
            raw.Add(current.Point);
            var following = Next(current, previous);
            if (following is null || visited.Contains(Edge(current, following))) break;
            if (current.Links.Count == 3) mainTrunk = true;
            visited.Add(Edge(current, following));
            previous = current;
            current = following;
        }
        var closed = current == start;
        return new Trace
        {
            Kind = kind, Points = Smooth(raw, unit * .11f, closed), Closed = closed, MainTrunk = mainTrunk,
            SealStart = !closed && !start.Terminal && start.Links.Count <= 1,
            SealEnd = !closed && !current.Terminal && current.Links.Count <= 1
        };
    }

    private static Node? Next(Node node, Node previous)
    {
        if (node.Links.Count == 2) return node.Links.First(other => other != previous);
        if (node.Links.Count != 3) return null;
        var back = (previous.Point - node.Point).Normalized();
        return node.Links.FirstOrDefault(other => other != previous && (other.Point - node.Point).Normalized().Dot(back) < -.99f);
    }

    private static string Edge(Node first, Node second) => string.CompareOrdinal(first.Id, second.Id) < 0 ? first.Id + "|" + second.Id : second.Id + "|" + first.Id;

    private static Vector2[] Smooth(List<Vector2> raw, float radius, bool closed)
    {
        if (raw.Count < 3) return raw.ToArray();
        if (closed && raw[0].DistanceSquaredTo(raw[^1]) < .001f) raw.RemoveAt(raw.Count - 1);
        var result = new List<Vector2>();
        if (!closed) result.Add(raw[0]);
        var first = closed ? 0 : 1;
        var end = closed ? raw.Count : raw.Count - 1;
        for (var i = first; i < end; i++)
        {
            var previous = raw[(i + raw.Count - 1) % raw.Count];
            var corner = raw[i];
            var next = raw[(i + 1) % raw.Count];
            var before = (corner - previous).Normalized();
            var after = (next - corner).Normalized();
            if (before.Dot(after) > .999f) { result.Add(corner); continue; }
            var reach = Mathf.Min(radius, Mathf.Min(previous.DistanceTo(corner), corner.DistanceTo(next)) * .44f);
            var entry = corner - before * reach;
            var exit = corner + after * reach;
            result.Add(entry);
            for (var step = 1; step <= 16; step++) result.Add(Quadratic(entry, corner, exit, step / 16f));
        }
        if (closed) result.Add(result[0]);
        else result.Add(raw[^1]);
        return result.Where((point, i) => i == 0 || point.DistanceSquaredTo(result[i - 1]) > .0001f).ToArray();
    }

    private static void DrawRibbon(Control canvas, Vector2[] points, float width, Texture2D? texture, Color tint, Vector2 offset)
    {
        if (points.Length < 2) return;
        var positions = new Vector2[points.Length * 2];
        var uvs = new Vector2[positions.Length];
        var period = TexturePeriod(texture, width);
        var length = 0f;
        var closed = points.Length > 3 && points[0].DistanceSquaredTo(points[^1]) < .001f;
        for (var i = 0; i < points.Length; i++)
        {
            if (i > 0) length += points[i].DistanceTo(points[i - 1]);
            var previousIndex = i == 0 && closed ? points.Length - 2 : Math.Max(0, i - 1);
            var nextIndex = i == points.Length - 1 && closed ? 1 : Math.Min(points.Length - 1, i + 1);
            var before = (points[i] - points[previousIndex]).Normalized();
            var after = (points[nextIndex] - points[i]).Normalized();
            if (!closed && i == 0) before = after;
            if (!closed && i == points.Length - 1) after = before;
            var beforeNormal = new Vector2(-before.Y, before.X);
            var afterNormal = new Vector2(-after.Y, after.X);
            var normal = (beforeNormal + afterNormal).Normalized();
            var reach = width * .5f / Mathf.Max(.75f, normal.Dot(afterNormal));
            positions[i * 2] = points[i] + normal * reach + offset;
            positions[i * 2 + 1] = points[i] - normal * reach + offset;
            uvs[i * 2] = new Vector2(length / period, 1);
            uvs[i * 2 + 1] = new Vector2(length / period, 0);
        }
        var colors = new[] { tint };
        for (var i = 0; i < points.Length - 1; i++)
        {
            // 相邻四边形共享完全相同的顶点；弧长UV也共享，转角没有独立贴片或黑楔。
            var at = i * 2;
            canvas.DrawPolygon(
                new[] { positions[at], positions[at + 2], positions[at + 3], positions[at + 1] }, colors,
                new[] { uvs[at], uvs[at + 2], uvs[at + 3], uvs[at + 1] }, texture);
        }
    }

    private static void DrawCaps(Control canvas, Trace trace, float width, Texture2D? texture, Color tint, Vector2 offset)
    {
        if (trace.Points.Length < 2 || trace.Closed) return;
        var period = TexturePeriod(texture, width);
        if (trace.SealStart)
            DrawCap(canvas, trace.Points[0] + offset, (trace.Points[1] - trace.Points[0]).Normalized(), false, width, texture, tint, 0, period);
        if (trace.SealEnd)
        {
            var length = 0f;
            for (var i = 1; i < trace.Points.Length; i++) length += trace.Points[i - 1].DistanceTo(trace.Points[i]);
            DrawCap(canvas, trace.Points[^1] + offset, (trace.Points[^1] - trace.Points[^2]).Normalized(), true, width, texture, tint, length / period, period);
        }
    }

    private static void DrawCap(Control canvas, Vector2 point, Vector2 forward, bool end, float width, Texture2D? texture, Color tint, float u, float period)
    {
        var normal = new Vector2(-forward.Y, forward.X);
        var outward = end ? forward : -forward;
        var positions = new Vector2[13];
        var uvs = new Vector2[13];
        positions[0] = point;
        uvs[0] = new Vector2(u, .5f);
        for (var i = 0; i < 12; i++)
        {
            var angle = -Mathf.Pi * .5f + Mathf.Pi * i / 11f;
            var along = Mathf.Cos(angle) * width * .5f;
            var across = Mathf.Sin(angle) * width * .5f;
            positions[i + 1] = point + outward * along + normal * across;
            uvs[i + 1] = new Vector2(u + (end ? along : -along) / period, .5f + across / width);
        }
        canvas.DrawPolygon(positions, new[] { tint }, uvs, texture);
    }

    private static void DrawBreakpoint(Control canvas, Vector2 point, Vector2 outward, float width, float unit, PortKind kind)
    {
        var side = new Vector2(-outward.Y, outward.X);
        var tip = point + outward * (width * .7f + 2);
        var span = Mathf.Max(2.2f, unit * .018f);
        var color = new Color(.75f, .58f, .34f, .7f);
        if (kind == PortKind.Signal) canvas.DrawPolyline(new[] { tip - outward * span, tip + side * span, tip + outward * span, tip - side * span, tip - outward * span }, color, .85f, true);
        else canvas.DrawArc(tip, span, 0, Mathf.Tau, 20, color, .85f, true);
    }

    private static Texture2D? Material(PortKind kind)
    {
        if (Materials.TryGetValue(kind, out var texture)) return texture;
        var path = kind == PortKind.Signal ? SignalMaterialPath : EnergyMaterialPath;
        if (Godot.FileAccess.FileExists(path + ".import") && ResourceLoader.Exists(path)) texture = GD.Load<Texture2D>(path);
        else if (Godot.FileAccess.FileExists(path))
        {
            using var image = Image.LoadFromFile(path);
            if (image is not null && !image.IsEmpty()) texture = ImageTexture.CreateFromImage(image);
        }
        if (texture is not null) Materials[kind] = texture;
        return texture;
    }

    private static float TexturePeriod(Texture2D? texture, float width) => texture is not null && texture.GetHeight() > 0
        ? width * texture.GetWidth() / texture.GetHeight() : width * 14;
    public static float StrokeWidth(PortKind kind, float unit) => Width(kind, unit);
    private static float Width(PortKind kind, float unit) => kind == PortKind.Signal ? Mathf.Max(1.8f, unit * 6.5f / 128f) : Mathf.Max(2.7f, unit * 10.5f / 128f);
    private static Vector2 Center(Cell cell, Rect2 grid, float unit) => grid.Position + new Vector2((cell.X + .5f) * unit, (cell.Y + .5f) * unit);
    private static Vector2 Quadratic(Vector2 start, Vector2 control, Vector2 end, float t) => start * ((1 - t) * (1 - t)) + control * (2 * (1 - t) * t) + end * (t * t);
    private static Vector2 Offset(Direction direction) => direction switch
    {
        Direction.North => Vector2.Up, Direction.East => Vector2.Right, Direction.South => Vector2.Down, Direction.West => Vector2.Left, _ => Vector2.Zero
    };
}
