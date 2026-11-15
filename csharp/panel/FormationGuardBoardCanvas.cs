using Godot;
using System.Text.Json;

/// <summary>铜纹素材试排：只呈现实际控制图与结果，不定义正式阵盘等级或构筑规则。</summary>
public partial class FormationGuardBoardCanvas : Control
{
    public const string ArtDirectory = "res://docs/美术规范/守攻阵铜纹素材_20261109/";
    private const string BasicDirectory = "res://docs/美术规范/铜纹阵盘基础元件_20261108/";
    private const string LogicDirectory = "res://docs/美术规范/铜纹阵盘逻辑元件_20261108/";
    private const float Cell = 128;
    private static readonly Rect2 Board = new(-230, -230, 1484, 1484);
    private sealed record Socket(string Key, string Side, bool Energy, Vector2 Boundary, Vector2 Core, Vector2 Mouth, float Width, float Bridge = 0, Vector2? BridgeFrom = null, Vector2? BridgeTo = null, float? BridgeWidth = null);
    private sealed class Piece(string id, string label, string path, Vector2 origin, Vector2 shape, Vector2 tag)
    {
        public string Id = id, Label = label, Path = path;
        public Vector2 Origin = origin, Shape = shape, Tag = tag, Offset;
        public int Rotation = 0;
        public readonly System.Collections.Generic.Dictionary<string, Socket> Ports = [];
        public Rect2 Bounds => new(Origin * Cell, new Vector2(Rotation % 2 == 0 ? Shape.X : Shape.Y, Rotation % 2 == 0 ? Shape.Y : Shape.X) * Cell);
    }
    private sealed record Route(string From, string FromPort, string To, string ToPort, bool Energy, Vector2[] Bends);
    private readonly System.Collections.Generic.Dictionary<string, Piece> _pieces = [];
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> _textures = [];
    private readonly System.Collections.Generic.Dictionary<string, ControlStep> _values = [];
    private readonly System.Collections.Generic.Dictionary<string, Control> _hoverAnchors = [];
    private readonly System.Collections.Generic.List<Route> _routes = [];
    private FormationGuardAssaultRuntime? _runtime;
    private FormationGuardAssaultRuntime.Result? _result;
    private int _previousAttacks;
    private bool _attackNow;
    private float _scale;
    private Vector2 _origin;
    public Rect2? FocusRegion { get; set; }
    // Older isolated renderers still set this property; permanent paper tags have been removed.
    public bool ShowLabels { get => false; set { } }
    public bool HasPersistentLabels => false;
    public bool HasAllAssets => Texture(ArtDirectory + "board_base.png") is not null && _pieces.Values.All(p => Texture(p.Path) is not null);
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass; TextureFilter = TextureFilterEnum.Linear; ClipContents = true;
        BuildLayout(); LoadMetadata(BasicDirectory + "components.json"); LoadMetadata(LogicDirectory + "components.json"); LoadMetadata(ArtDirectory + "components.json");
        foreach (var piece in _pieces.Values)
        {
            var anchor = new Control { Name = "GuardPartHover_" + piece.Id, MouseFilter = MouseFilterEnum.Pass, FocusMode = FocusModeEnum.None };
            AddChild(anchor); _hoverAnchors[piece.Id] = anchor; BindHover(piece, anchor);
        }
        MouseExited += () => { TooltipText = ""; QueueRedraw(); };
    }
    private void BuildLayout()
    {
        Piece Add(string id, string label, string image, Vector2 origin, Vector2 shape, Vector2 tag, string directory = ArtDirectory)
        { var piece = new Piece(id, label, directory + image + ".png", origin, shape, tag); _pieces[id] = piece; return piece; }
        void Port(Piece p, string key, string side, bool energy, Vector2 core, Vector2 mouth, float width, float bridge = 0)
        {
            var boundary = side switch { "north" => new Vector2(p.Shape.X / 2, 0), "south" => new Vector2(p.Shape.X / 2, p.Shape.Y), "west" => new Vector2(0, p.Shape.Y / 2), _ => new Vector2(p.Shape.X, p.Shape.Y / 2) };
            p.Ports[key] = new Socket(key, side, energy, boundary * Cell, core, mouth, width, bridge);
        }
        var state = Add("health", "生命输入", "state_1x1", new(1, 1), Vector2.One, new(1.2f, 2.05f)); state.Offset = new(0, .61f);
        Port(state, "out", "east", false, new(.87161f, .49522f), new(.92026f, .49522f), 4.29f, 2.34f);
        var compare = Add("compare", "比较器", "compare_2x1", new(3, 1), new(2, 1), new(4, 2.05f)); compare.Offset = new(0, .36f);
        Port(compare, "in", "west", false, new(.09329f, .49718f), new(.03890f, .49718f), 6.64f, 6.07f);
        Port(compare, "out", "east", false, new(.90699f, .49718f), new(.96054f, .49718f), 6.64f, 5.93f);
        var branch = Add("branch", "分流", "split_1x1", new(6, 1), Vector2.One, new(6.15f, 2.05f), LogicDirectory); branch.Offset = new(0, -9.3f);
        Port(branch, "in", "west", false, new(.1651f, .5726f), new(.1156f, .5726f), 5.3f, 2);
        Port(branch, "guard", "east", false, new(.8393f, .5722f), new(.8852f, .5722f), 5.3f, 2);
        Port(branch, "invert", "north", false, new(.5060f, .2177f), new(.5060f, .1659f), 5.3f, 2);
        var guard = Add("guard", "护罩", "ward_1x2", new(7, 2), new(1, 2), new(6.1f, 2.7f), BasicDirectory); guard.Offset = new(-.0768f, 0);
        Port(guard, "in", "north", false, new(.5006f, .1088f), new(.5006f, .0880f), 7.5f);
        Port(guard, "energy", "south", true, new(.5006f, .8825f), new(.5006f, .9115f), 9.5f);
        var invert = Add("invert", "取反", "invert_1x1", new(1, 3), Vector2.One, new(1.3f, 4.05f)); invert.Offset = new(0, -.9f);
        Port(invert, "in", "west", false, new(.1754f, .5076f), new(.1324f, .5076f), 5.5f, 2);
        Port(invert, "out", "east", false, new(.8214f, .5060f), new(.8620f, .5060f), 5.5f, 2);
        var cast = Add("cast", "施法感应", "sensor_1x1", new(1, 5), Vector2.One, new(1.3f, 4.72f), BasicDirectory); cast.Offset = new(0, -.7168f);
        Port(cast, "out", "east", false, new(.7974f, .5056f), new(.8533f, .5056f), 4.3f, 3);
        var gate = Add("switch", "开关", "gate_1x1", new(4, 5), Vector2.One, new(5, 6.03f)); gate.Offset = new(0, -13);
        Port(gate, "event", "west", false, new(.12839f, .60167f), new(.10128f, .60167f), 5.92f, 2);
        Port(gate, "permit", "north", false, new(.50120f, .2210f), new(.50120f, .17384f), 5.92f, 2);
        Port(gate, "out", "east", false, new(.86962f, .60167f), new(.89872f, .60167f), 5.72f, 2);
        var fire = Add("attack", "火击", "fire_1x2", new(6, 4), new(1, 2), new(6.5f, 7.05f));
        Port(fire, "in", "north", false, new(.5017f, .1215f), new(.5017f, .0874f), 6, 2);
        Port(fire, "energy", "south", true, new(.4994f, .8658f), new(.4994f, .8985f), 7.2f, 2);
        var source = Add("source", "阵源", "source_2x1", new(2, 6), new(2, 1), new(3, 7.05f), BasicDirectory); source.Offset = new(0, .4352f);
        Port(source, "energy", "east", true, new(.9543f, .4966f), new(.9679f, .4966f), 5.3f);
        _routes.AddRange(new Route[]
        {
            new("health", "out", "compare", "in", false, []), new("compare", "out", "branch", "in", false, []),
            new("branch", "guard", "guard", "in", false, [new(7.5f, 1.5f)]),
            new("branch", "invert", "invert", "in", false, [new(6.5f, .5f), new(.5f, .5f), new(.5f, 3.5f)]),
            new("invert", "out", "switch", "permit", false, [new(4.5f, 3.5f)]),
            new("cast", "out", "switch", "event", false, []),
            new("switch", "out", "attack", "in", false, [new(5.5f, 5.5f), new(5.5f, 3.5f), new(6.5f, 3.5f)]),
            new("source", "energy", "guard", "energy", true, [new(6.5f, 6.5f), new(7.5f, 6.5f)]),
            new("source", "energy", "attack", "energy", true, [new(6.5f, 6.5f)])
        });
    }
    private void LoadMetadata(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) return;
        using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path));
        if (!document.RootElement.TryGetProperty("components", out var components)) return;
        static Vector2 Pair(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle());
        foreach (var item in components.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? "";
            id = id switch { "state" => "health", "gate" => "switch", "fire" => "attack", "sensor" => "cast", "ward" => "guard", "split" => "branch", _ => id };
            if (!_pieces.TryGetValue(id, out var piece)) continue;
            if (item.TryGetProperty("art_offset_128", out var offset)) piece.Offset = Pair(offset);
            if (!item.TryGetProperty("ports", out var ports)) continue;
            foreach (var port in ports.EnumerateArray())
            {
                var side = port.GetProperty("side").GetString() ?? "";
                var energy = port.TryGetProperty("kind", out var kind) && kind.GetString() == "energy";
                var current = piece.Ports.Values.FirstOrDefault(p => p.Side == side && p.Energy == energy); if (current is null) continue;
                var core = port.TryGetProperty("slot_center_uv", out var slot) ? Pair(slot) : current.Core;
                var mouth = port.TryGetProperty("mouth_uv", out var opening) ? Pair(opening) : port.TryGetProperty("color_mouth_uv", out var colorOpening) ? Pair(colorOpening) : current.Mouth;
                var width = port.TryGetProperty("slot_inner_width_at_128", out var a) ? a.GetSingle() : port.TryGetProperty("slot_inner_width", out var b) ? b.GetSingle() : current.Width;
                var bridge = port.TryGetProperty("overlay_bridge", out var bridgeValue) ? bridgeValue.GetSingle() : current.Bridge;
                Vector2? bridgeFrom = null, bridgeTo = null; float? bridgeWidth = null;
                if (port.TryGetProperty("overlay_bridge_segment_128", out var segment)) { bridgeFrom = Pair(segment.GetProperty("from")); bridgeTo = Pair(segment.GetProperty("to")); bridgeWidth = segment.GetProperty("width").GetSingle(); }
                piece.Ports[current.Key] = current with { Core = core, Mouth = mouth, Width = energy && id == "attack" ? Mathf.Min(7.2f, width) : width, Bridge = bridge, BridgeFrom = bridgeFrom, BridgeTo = bridgeTo, BridgeWidth = bridgeWidth };
            }
        }
    }
    private Texture2D? Texture(string path)
    {
        if (_textures.TryGetValue(path, out var texture)) return texture;
        texture = FormationBoardCanvas.LoadArt(path); if (texture is not null) _textures[path] = texture; return texture;
    }
    public void SetState(FormationGuardAssaultRuntime runtime, FormationGuardAssaultRuntime.Result result)
    {
        if (!ReferenceEquals(_runtime, runtime)) _previousAttacks = 0;
        _runtime = runtime; _result = result; _attackNow = !result.Duplicate && result.Snapshot.Attacks > _previousAttacks; _previousAttacks = result.Snapshot.Attacks;
        if (!result.Duplicate) { _values.Clear(); foreach (var step in result.Steps) _values[step.NodeId] = step; }
        foreach (var (id, anchor) in _hoverAnchors) BindHover(_pieces[id], anchor);
        QueueRedraw();
    }
    private Vector2 Point(Piece p, Vector2 local, bool offset = false) => p.Bounds.GetCenter() + (local + (offset ? p.Offset : Vector2.Zero) - p.Shape * Cell / 2).Rotated(p.Rotation * Mathf.Pi / 2);
    private Vector2 Boundary(Piece p, Socket socket) => Point(p, socket.Boundary);
    private Vector2 Core(Piece p, Socket socket) => Point(p, socket.Core * p.Shape * Cell, true);
    private Vector2 Mouth(Piece p, Socket socket) => Point(p, socket.Mouth * p.Shape * Cell, true);
    private Vector2[] Path(Route route) => new[] { Boundary(_pieces[route.From], _pieces[route.From].Ports[route.FromPort]) }.Concat(route.Bends.Select(p => p * Cell)).Append(Boundary(_pieces[route.To], _pieces[route.To].Ports[route.ToPort])).ToArray();
    private bool Exists(Route route) => route.Energy || _runtime?.Circuit.Links.Any(link => link.From == route.From && link.To == route.To) == true;
    public override void _Draw()
    {
        if (_runtime is null || _result is null || _pieces.Count == 0) return;
        var area = FocusRegion ?? Board; _scale = Mathf.Min(Size.X / area.Size.X, Size.Y / area.Size.Y); _origin = Size / 2 - area.GetCenter() * _scale;
        DrawSetTransform(_origin, 0, Vector2.One * _scale);
        if (Texture(ArtDirectory + "board_base.png") is Texture2D board) DrawTextureRect(board, Board, false);
        foreach (var route in _routes.Where(r => Exists(r) && !r.Energy)) DrawTrace(Path(route), false, Active(route));
        var energyPaths = EnergyPaths();
        for (var pass = 1; pass <= 3; pass++) foreach (var path in energyPaths) DrawTrace(path, true, false, pass);
        foreach (var piece in _pieces.Values) foreach (var socket in piece.Ports.Values) DrawSocket(piece, socket, false);
        foreach (var piece in _pieces.Values)
        {
            if (Texture(piece.Path) is not Texture2D art) continue;
            DrawSetTransform(_origin + piece.Bounds.GetCenter() * _scale, piece.Rotation * Mathf.Pi / 2, Vector2.One * _scale);
            DrawTextureRect(art, new Rect2(-piece.Shape * Cell / 2 + piece.Offset, piece.Shape * Cell), false);
        }
        DrawSetTransform(_origin, 0, Vector2.One * _scale);
        foreach (var piece in _pieces.Values) foreach (var socket in piece.Ports.Values) DrawSocket(piece, socket, true);
        DrawSetTransform(Vector2.Zero);
        foreach (var (id, anchor) in _hoverAnchors)
        {
            var bounds = _pieces[id].Bounds; anchor.Position = _origin + bounds.Position * _scale; anchor.Size = bounds.Size * _scale;
        }
    }
    private bool Active(Route route)
    {
        if (_result is null) return false;
        if (route.Energy) return route.To == "guard" ? _result.Snapshot.GuardRunning : _attackNow;
        var isEvent = route.From is "cast" or "switch"; return _values.GetValueOrDefault(route.From)?.Passed == true && (!isEvent || !_result.Duplicate);
    }
    private bool Connected(Piece p, Socket s) => _routes.Any(r => Exists(r) && (r.From == p.Id && r.FromPort == s.Key || r.To == p.Id && r.ToPort == s.Key));
    private void DrawSocket(Piece piece, Socket port, bool overlay)
    {
        if (!Connected(piece, port)) return;
        var edge = Boundary(piece, port); var mouth = Mouth(piece, port); var core = Core(piece, port);
        var color = new Color(port.Energy ? "ceb787" : "bb6652"); var width = port.Energy ? 10.5f : 6.5f;
        if (overlay)
        {
            if (port.BridgeFrom is Vector2 from && port.BridgeTo is Vector2 to) { var a = Point(piece, from); var b = Point(piece, to); DrawLine(a, b, color, port.BridgeWidth ?? port.Width, true); Brush(a, b, port.Energy, port.BridgeWidth ?? port.Width); return; }
            if (port.Bridge <= 0) return;
            var length = Mathf.Min(port.Bridge, mouth.DistanceTo(core));
            var end = mouth + (core - mouth).Normalized() * length;
            DrawLine(mouth, end, color, Mathf.Max(1, port.Width - .3f), true); Brush(mouth, end, port.Energy, port.Width); return;
        }
        Taper(edge, mouth, width + 3, port.Width + 1.2f, new Color("51463d")); Taper(mouth, core, port.Width + 1.2f, port.Width + 1.2f, new Color("51463d"));
        Taper(edge, mouth, width, port.Width, color); Taper(mouth, core, port.Width, port.Width, color);
        Brush(edge, mouth, port.Energy, Mathf.Min(width, port.Width)); Brush(mouth, core, port.Energy, port.Width);
    }
    private void Brush(Vector2 a, Vector2 b, bool energy, float width) => DrawLine(a + new Vector2(-.45f, -.65f), b + new Vector2(-.45f, -.65f), new Color(energy ? "ead9ad70" : "e0a18b55"), Mathf.Max(.65f, width * .17f), true);
    private void Taper(Vector2 a, Vector2 b, float start, float end, Color color)
    {
        if (a.DistanceSquaredTo(b) < .001f) return; var normal = (b - a).Normalized().Orthogonal();
        DrawColoredPolygon([a + normal * start / 2, b + normal * end / 2, b - normal * end / 2, a - normal * start / 2], color);
    }
    private Vector2[][] EnergyPaths()
    {
        var segments = _routes.Where(r => r.Energy && Exists(r)).SelectMany(r => Path(r).Zip(Path(r).Skip(1), (a, b) => (a, b))).ToArray();
        var vertices = segments.SelectMany(s => new[] { s.a, s.b }).Distinct().ToArray();
        var graph = new System.Collections.Generic.Dictionary<Vector2, System.Collections.Generic.HashSet<Vector2>>();
        foreach (var (a, b) in segments)
        {
            var cuts = vertices.Where(p => Mathf.Abs((b - a).Cross(p - a)) < .01f && p.X >= Mathf.Min(a.X, b.X) && p.X <= Mathf.Max(a.X, b.X) && p.Y >= Mathf.Min(a.Y, b.Y) && p.Y <= Mathf.Max(a.Y, b.Y)).OrderBy(p => p.DistanceSquaredTo(a)).ToArray();
            for (var i = 1; i < cuts.Length; i++)
            {
                if (!graph.ContainsKey(cuts[i - 1])) graph[cuts[i - 1]] = []; if (!graph.ContainsKey(cuts[i])) graph[cuts[i]] = [];
                graph[cuts[i - 1]].Add(cuts[i]); graph[cuts[i]].Add(cuts[i - 1]);
            }
        }
        var used = new System.Collections.Generic.HashSet<(Vector2, Vector2)>(); var paths = new System.Collections.Generic.List<Vector2[]>();
        foreach (var start in graph.Keys.Where(p => graph[p].Count != 2)) foreach (var next in graph[start])
        {
            if (used.Contains((start, next))) continue;
            var path = new System.Collections.Generic.List<Vector2> { start }; var a = start; var b = next;
            while (true)
            {
                used.Add((a, b)); used.Add((b, a)); path.Add(b); if (graph[b].Count != 2) break;
                var c = graph[b].First(p => p != a); a = b; b = c;
            }
            paths.Add(path.ToArray());
        }
        return paths.ToArray();
    }
    private void DrawTrace(Vector2[] points, bool energy, bool active, int pass = 0)
    {
        var rounded = new System.Collections.Generic.List<Vector2> { points[0] };
        for (var i = 1; i < points.Length - 1; i++)
        {
            var a = points[i - 1]; var b = points[i]; var c = points[i + 1]; var before = (b - a).Normalized(); var after = (c - b).Normalized();
            if (Mathf.Abs(before.Cross(after)) < .01f) { rounded.Add(b); continue; }
            var radius = Mathf.Min(14, Mathf.Min(a.DistanceTo(b), b.DistanceTo(c)) / 3); var start = b - before * radius; var end = b + after * radius;
            rounded.Add(start); for (var step = 1; step <= 6; step++) { var t = step / 6f; rounded.Add((1 - t) * (1 - t) * start + 2 * (1 - t) * t * b + t * t * end); }
        }
        rounded.Add(points[^1]); var path = rounded.ToArray(); var width = energy ? 10.5f : 6.5f;
        if (pass is 0 or 1) DrawPolyline(path, new Color("51463d"), width + 3, true);
        if (pass is 0 or 2) DrawPolyline(path, new Color(energy ? "ceb787" : "bb6652"), width, true);
        if (pass is 0 or 3) DrawPolyline(path.Select(p => p + new Vector2(-.45f, -.65f)).ToArray(), new Color(energy ? "ead9ad70" : active ? "e0a18b55" : "e0a18b35"), Mathf.Max(1, width * .17f), true);
    }
    private string Value(Piece p)
    {
        if (_result is null) return ""; var s = _result.Snapshot;
        return p.Id switch
        {
            "health" => s.ObservationKnown ? $"{s.Health * 100 / Math.Max(1, s.MaxHealth)}%" : "未知",
            "compare" => s.ModeText, "branch" or "invert" => _values.GetValueOrDefault(p.Id)?.Value ?? "待采样",
            "guard" => s.GuardRunning ? $"运行{s.GuardRemaining}" : "停止", "attack" => $"{s.Attacks}次",
            "source" => $"{s.SourceEnergy}/{_runtime!.Configuration.SourceCapacity}",
            "cast" => _result.Duplicate ? "同次重发" : _values.GetValueOrDefault(p.Id)?.Passed == true ? "有事件" : "待事件",
            "switch" => _result.Duplicate ? "同次重发" : _values.GetValueOrDefault("cast")?.Passed != true ? "待事件" : _values.GetValueOrDefault(p.Id)?.Passed == true ? "放行" : "拦截", _ => ""
        };
    }
    private void BindHover(Piece piece, Control anchor) => HoverInfoManager.SetProvider(anchor, () =>
        new HoverInfoManager.HoverInfoContent(piece.Label, HoverText(piece), Texture(piece.Path), ShowRules: false, LiveUpdate: true, SingleColumn: true));
    private string HoverText(Piece piece)
    {
        if (_result is null || HoverInfoManager.IsDragInProgress() || GetViewport().GuiIsDragging()) return "";
        var value = _values.GetValueOrDefault(piece.Id)?.Value; var text = "当前状态：" + Value(piece);
        if (value is not null) text += "\n本次控制值：" + value;
        if (piece.Id == "guard") text += "\n本器防护：" + _result.Snapshot.GuardRemaining;
        if (piece.Id == "switch") text += "\n攻击许可：" + FormationGuardPanel.Truth(_result.Snapshot.AttackAllowed);
        return text;
    }
    public System.Collections.Generic.IReadOnlyList<string> GetLayoutIssues()
    {
        var issues = new System.Collections.Generic.List<string>();
        foreach (var link in _runtime?.Circuit.Links ?? []) if (!_routes.Any(r => !r.Energy && r.From == link.From && r.To == link.To)) issues.Add("尚未试排真实连接：" + link.From + "→" + link.To);
        var routes = _routes.Where(Exists).ToArray();
        for (var i = 0; i < routes.Length; i++)
        {
            var path = Path(routes[i]);
            for (var j = 1; j < path.Length; j++)
            {
                var a = path[j - 1]; var b = path[j];
                if (Mathf.Abs(a.X - b.X) > .01f && Mathf.Abs(a.Y - b.Y) > .01f) issues.Add("主线路不是正交路径");
                foreach (var piece in _pieces.Values.Where(p => p.Id != routes[i].From && p.Id != routes[i].To))
                    if (IntersectsInterior(a, b, piece.Bounds.Grow(-.1f))) issues.Add("线路穿过" + piece.Label);
                for (var k = i + 1; k < routes.Length; k++)
                {
                    var other = Path(routes[k]); for (var m = 1; m < other.Length; m++)
                        if (!(routes[i].Energy && routes[k].Energy) && SegmentsMeet(a, b, other[m - 1], other[m])) issues.Add("不允许的线交叉：" + routes[i].From + "→" + routes[i].To + " / " + routes[k].From + "→" + routes[k].To);
                }
            }
        }
        return issues.Distinct().ToArray();
    }
    private static bool IntersectsInterior(Vector2 a, Vector2 b, Rect2 r) => Mathf.Abs(a.X - b.X) < .01f
        ? a.X > r.Position.X && a.X < r.End.X && Mathf.Max(a.Y, b.Y) > r.Position.Y && Mathf.Min(a.Y, b.Y) < r.End.Y
        : a.Y > r.Position.Y && a.Y < r.End.Y && Mathf.Max(a.X, b.X) > r.Position.X && Mathf.Min(a.X, b.X) < r.End.X;
    private static bool SegmentsMeet(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static bool Between(float p, float x, float y) => p >= Mathf.Min(x, y) - .01f && p <= Mathf.Max(x, y) + .01f;
        var av = Mathf.Abs(a.X - b.X) < .01f; var cv = Mathf.Abs(c.X - d.X) < .01f;
        if (av && cv) return Mathf.Abs(a.X - c.X) < .01f && Mathf.Max(Mathf.Min(a.Y, b.Y), Mathf.Min(c.Y, d.Y)) <= Mathf.Min(Mathf.Max(a.Y, b.Y), Mathf.Max(c.Y, d.Y)) + .01f;
        if (!av && !cv) return Mathf.Abs(a.Y - c.Y) < .01f && Mathf.Max(Mathf.Min(a.X, b.X), Mathf.Min(c.X, d.X)) <= Mathf.Min(Mathf.Max(a.X, b.X), Mathf.Max(c.X, d.X)) + .01f;
        return av ? Between(a.X, c.X, d.X) && Between(c.Y, a.Y, b.Y) : Between(c.X, a.X, b.X) && Between(a.Y, c.Y, d.Y);
    }
    public object DescribeLayout() => new
    {
        status = "素材试排，正式足迹与盘级未定", logical_working_area = new[] { 8, 8 }, units_per_cell = Cell,
        pieces = _pieces.Values.Select(p => new { id = p.Id, image = p.Path, origin = new[] { p.Origin.X, p.Origin.Y }, image_rectangle = new[] { p.Shape.X, p.Shape.Y }, rotation = p.Rotation, art_offset_128 = new[] { p.Offset.X, p.Offset.Y }, ports = p.Ports.Values.Select(s => new { id = s.Key, side = s.Side, energy = s.Energy, logical_boundary = new[] { Boundary(p, s).X, Boundary(p, s).Y }, slot = new[] { Core(p, s).X, Core(p, s).Y }, mouth = new[] { Mouth(p, s).X, Mouth(p, s).Y } }) }),
        routes = _routes.Where(Exists).Select(r => new { from = r.From, to = r.To, energy = r.Energy, points = Path(r).Select(p => new[] { p.X, p.Y }) }),
        issues = GetLayoutIssues()
    };
}
