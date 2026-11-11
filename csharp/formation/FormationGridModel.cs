using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

/// <summary>阵盘草图的几何与接线模型；不授予实物，也不执行战斗效果。</summary>
public sealed class FormationGridModel
{
    public const int Size = 4;
    public readonly record struct Cell(int X, int Y);
    // Eye保留旧编号，仅用于识别并迁移旧草图；新草图不再包含阵眼。
    // Rhythm保留旧编号，放置及恢复时统一转为Counter的先发一次模式。
    public enum Kind { Eye, Source, Sensor, Ward, Counter, Filter, Rhythm, Split, Merge, StateInput, Compare, Not, Switch, Fire, Guard, And, Or }
    public enum PortKind { Signal, Energy }
    [Flags]
    public enum Direction { None = 0, North = 1, East = 2, South = 4, West = 8 }
    public readonly record struct Port(Cell Cell, Direction Facing, PortKind Kind, bool IsInput)
    {
        public string Name { get; init; } = "";
        public FormationSignalType SignalType { get; init; } = FormationSignalType.Event;
        public FormationValueUnit Unit { get; init; } = FormationValueUnit.None;
    }
    public sealed record Settings
    {
        public int Interval { get; init; } = 2;
        public bool FirstImmediate { get; init; }
        public int MinimumAmplitude { get; init; } = 1;
        public string SourceTag { get; init; } = "";
        public string InputEvent { get; init; } = "spell_played";
        public FormationSignalType SignalType { get; init; } = FormationSignalType.Event;
        public FormationValueUnit Unit { get; init; } = FormationValueUnit.None;
        public string StateKey { get; init; } = "health";
        public FormationControlCircuit.CompareMode Comparison { get; init; } = FormationControlCircuit.CompareMode.Band;
        public double Low { get; init; } = 30;
        public double High { get; init; } = 60;
        public bool NormallyClosed { get; init; }
    }
    public sealed record SignalConnection(string FromComponentId, string ToComponentId, IReadOnlyList<Cell> WireCells)
    {
        public string FromPortName { get; init; } = "";
        public string ToPortName { get; init; } = "";
        public FormationSignalType SignalType { get; init; } = FormationSignalType.Event;
        public FormationValueUnit Unit { get; init; } = FormationValueUnit.None;
    }

    public sealed record Component(string Id, Kind Kind, Cell Origin, int Rotation)
    {
        public Settings Settings { get; init; } = new();
        public int Width => Rotation % 2 == 0 ? BaseSize(Kind).X : BaseSize(Kind).Y;
        public int Height => Rotation % 2 == 0 ? BaseSize(Kind).Y : BaseSize(Kind).X;
        public int Complexity => ComplexityOf(Kind);
        public IReadOnlyList<Cell> Cells => Enumerable.Range(0, Height)
            .SelectMany(y => Enumerable.Range(0, Width).Select(x => new Cell(Origin.X + x, Origin.Y + y)))
            .ToArray();
        public IReadOnlyList<Port> Ports => PortsOf(this);
    }

    private readonly Dictionary<string, Component> _components = new(StringComparer.Ordinal);
    private readonly Dictionary<Cell, PortKind> _wires = new();
    public IReadOnlyDictionary<string, Component> Components { get; }
    public IReadOnlyDictionary<Cell, PortKind> Wires { get; }
    public string LastError { get; private set; } = "";
    public int BoardSize { get; private set; }
    public int Complexity => _components.Values.Sum(c => c.Complexity);
    public bool IsCircuitConnected => GetIssues().Count == 0;

    public FormationGridModel() : this(Size) { }

    public FormationGridModel(int boardSize)
    {
        if (boardSize is not (4 or 5)) throw new ArgumentOutOfRangeException(nameof(boardSize));
        BoardSize = boardSize;
        Components = new ReadOnlyDictionary<string, Component>(_components);
        Wires = new ReadOnlyDictionary<Cell, PortKind>(_wires);
    }

    public static Settings DefaultSettingsFor(Kind kind) => kind is Kind.StateInput or Kind.Compare
        ? new Settings { SignalType = FormationSignalType.Number, Unit = FormationValueUnit.Percent } : new Settings();

    public bool TryResize(int newSize)
    {
        if (newSize is not (4 or 5)) return Reject("阵盘只支持4×4或5×5。");
        if (newSize == BoardSize) return Accept();
        var candidate = new FormationGridModel(newSize);
        if (!candidate.Commit(_components, _wires)) return Reject(candidate.LastError);
        BoardSize = newSize; _components.Clear(); foreach (var part in candidate._components) _components.Add(part.Key, part.Value);
        return Accept();
    }

    public static FormationGridModel CreateStarter()
    {
        var grid = new FormationGridModel();
        grid.TryPlaceComponent(Kind.Sensor, new Cell(0, 0), id: "sensor");
        grid.TryPlaceComponent(Kind.Ward, new Cell(3, 1), id: "ward");
        grid.TryPlaceComponent(Kind.Source, new Cell(0, 3), id: "source");
        foreach (var cell in new[] { new Cell(1, 0), new Cell(2, 0), new Cell(3, 0) })
            grid.TryPlaceWire(cell, PortKind.Signal);
        foreach (var cell in new[] { new Cell(2, 3), new Cell(3, 3) })
            grid.TryPlaceWire(cell, PortKind.Energy);
        return grid;
    }

    /// <summary>5×5低血护罩范例：生命低于30%时开罩，回升到30%以上即关闭。</summary>
    public static FormationGridModel CreateLowHealthGuardExample()
    {
        var grid = new FormationGridModel(5);
        void Place(Kind kind, int x, int y, int rotation, string id)
        {
            if (!grid.TryPlaceComponent(kind, new Cell(x, y), rotation, id))
                throw new InvalidOperationException(grid.LastError);
        }
        Place(Kind.StateInput, 0, 0, 0, "health");
        Place(Kind.Compare, 1, 0, 0, "threshold");
        Place(Kind.Guard, 0, 2, 0, "guard");
        Place(Kind.Source, 3, 4, 2, "source");
        if (!grid.TryConfigureComponent("threshold", DefaultSettingsFor(Kind.Compare) with
            { Comparison = FormationControlCircuit.CompareMode.AtMost, Low = 30, High = 60 }))
            throw new InvalidOperationException(grid.LastError);

        foreach (var cell in new[] { new Cell(3, 0), new Cell(3, 1), new Cell(2, 1), new Cell(1, 1), new Cell(0, 1) })
            if (!grid.TryPlaceWire(cell, PortKind.Signal)) throw new InvalidOperationException(grid.LastError);
        foreach (var cell in new[] { new Cell(2, 4), new Cell(1, 4), new Cell(0, 4) })
            if (!grid.TryPlaceWire(cell, PortKind.Energy)) throw new InvalidOperationException(grid.LastError);
        return grid;
    }

    /// <summary>5×5强术蓄势反击：先筛幅值，再累计三次，经盘面长线触发火击。</summary>
    public static FormationGridModel CreateStrongSpellCounterFireExample()
    {
        var grid = new FormationGridModel(5);
        void Place(Kind kind, int x, int y, int rotation, string id)
        {
            if (!grid.TryPlaceComponent(kind, new Cell(x, y), rotation, id))
                throw new InvalidOperationException(grid.LastError);
        }
        Place(Kind.Source, 3, 0, 2, "source");
        Place(Kind.Fire, 1, 1, 1, "fire");
        Place(Kind.Counter, 3, 1, 3, "counter");
        Place(Kind.Sensor, 0, 4, 0, "spell");
        Place(Kind.Filter, 1, 4, 0, "filter");
        if (!grid.TryConfigureComponent("filter", new Settings { MinimumAmplitude = 3 }) ||
            !grid.TryConfigureComponent("counter", new Settings { Interval = 3 }))
            throw new InvalidOperationException(grid.LastError);

        // 信号从阵盘下缘进入计数器；计数器输出直接接火击。
        foreach (var cell in new[] { new Cell(2, 4), new Cell(3, 4), new Cell(4, 4), new Cell(4, 3), new Cell(3, 3) })
            if (!grid.TryPlaceWire(cell, PortKind.Signal)) throw new InvalidOperationException(grid.LastError);
        // 阵能沿左上侧独立绕行，避免与信号线交叉。
        foreach (var cell in new[] { new Cell(2, 0), new Cell(1, 0), new Cell(0, 0), new Cell(0, 1) })
            if (!grid.TryPlaceWire(cell, PortKind.Energy)) throw new InvalidOperationException(grid.LastError);
        return grid;
    }

    /// <summary>5×5一术双击：分流器把同一术法送往两枚火击，阵源以三岔供能线同时供给。</summary>
    public static FormationGridModel CreateSplitDoubleFireExample()
    {
        var grid = new FormationGridModel(5);
        void Place(Kind kind, int x, int y, int rotation, string id)
        {
            if (!grid.TryPlaceComponent(kind, new Cell(x, y), rotation, id))
                throw new InvalidOperationException(grid.LastError);
        }
        Place(Kind.Source, 3, 0, 2, "source");
        Place(Kind.Fire, 1, 1, 2, "fire_north");
        Place(Kind.Fire, 2, 3, 3, "fire_east");
        Place(Kind.Sensor, 0, 3, 0, "spell");
        Place(Kind.Split, 1, 3, 0, "split");

        // 分流器的两个出口分别直连上下两枚火击，不把三条信号并成一条。
        // 阵能从一格三岔分到两条供能支路；结点度数为3，未做交叉。
        foreach (var cell in new[] { new Cell(2, 0), new Cell(1, 0), new Cell(2, 1), new Cell(3, 1), new Cell(4, 1), new Cell(4, 2), new Cell(4, 3) })
            if (!grid.TryPlaceWire(cell, PortKind.Energy)) throw new InvalidOperationException(grid.LastError);
        return grid;
    }

    /// <summary>5×5真实单层守攻，保留独立取反件；一格空余，不借隐藏线路。</summary>
    public static FormationGridModel CreateGuardAssaultExample()
    {
        var grid = new FormationGridModel(5);
        void Place(Kind kind, int x, int y, int rotation, string id)
        { if (!grid.TryPlaceComponent(kind, new Cell(x, y), rotation, id)) throw new InvalidOperationException(grid.LastError); }
        Place(Kind.StateInput, 3, 4, 0, "health"); Place(Kind.Compare, 4, 2, 3, "compare");
        Place(Kind.Split, 4, 1, 3, "branch"); Place(Kind.Guard, 2, 0, 1, "guard");
        Place(Kind.Sensor, 1, 2, 1, "cast"); Place(Kind.Switch, 1, 3, 1, "switch");
        Place(Kind.Fire, 0, 2, 2, "attack"); Place(Kind.Source, 1, 1, 2, "source"); Place(Kind.Not, 3, 2, 1, "invert");
        if (!grid.TryConfigureComponent("branch", new Settings { SignalType = FormationSignalType.Switch })) throw new InvalidOperationException(grid.LastError);
        foreach (var cell in new[] { new Cell(4, 4), new Cell(4, 0), new Cell(3, 1), new Cell(3, 3), new Cell(2, 3), new Cell(1, 4), new Cell(0, 4) })
            if (!grid.TryPlaceWire(cell, PortKind.Signal)) throw new InvalidOperationException(grid.LastError);
        foreach (var cell in new[] { new Cell(0, 0), new Cell(1, 0), new Cell(0, 1) })
            if (!grid.TryPlaceWire(cell, PortKind.Energy)) throw new InvalidOperationException(grid.LastError);
        return grid;
    }

    public static FormationGridModel CreateLogicExample(Kind kind, bool firstImmediate = false)
    {
        if (kind == Kind.Filter)
        {
            var filterGrid = CreateStarter();
            filterGrid.RemoveWire(new Cell(2, 0));
            filterGrid.TryPlaceComponent(kind, new Cell(2, 0), id: "filter");
            filterGrid.TryConfigureComponent("filter", new Settings { MinimumAmplitude = 2 });
            return filterGrid;
        }
        var grid = new FormationGridModel();
        if (kind is Kind.Counter or Kind.Rhythm)
        {
            grid.TryPlaceComponent(Kind.Counter, new Cell(3, 0), 3, "logic");
            grid.TryConfigureComponent("logic", new Settings { Interval = 3, FirstImmediate = firstImmediate || kind == Kind.Rhythm });
            grid.TryPlaceComponent(Kind.Sensor, new Cell(3, 2), 3, "sensor");
            grid.TryPlaceComponent(Kind.Ward, new Cell(0, 1), 0, "ward");
            grid.TryPlaceComponent(Kind.Source, new Cell(2, 3), 2, "source");
            foreach (var cell in new[] { new Cell(0, 0), new Cell(1, 0), new Cell(2, 0) }) grid.TryPlaceWire(cell, PortKind.Signal);
            foreach (var cell in new[] { new Cell(0, 3), new Cell(1, 3) }) grid.TryPlaceWire(cell, PortKind.Energy);
        }
        else if (kind is Kind.Split or Kind.Merge)
        {
            grid.TryPlaceComponent(Kind.Source, new Cell(0, 1), 1, "source");
            grid.TryPlaceComponent(Kind.Sensor, new Cell(1, 2), 0, "sensor");
            grid.TryPlaceComponent(Kind.Split, new Cell(2, 2), 0, "split");
            grid.TryPlaceComponent(Kind.Merge, new Cell(3, 2), 1, "merge");
            grid.TryPlaceComponent(Kind.Ward, new Cell(1, 3), 1, "ward");
            foreach (var cell in new[] { new Cell(2, 1), new Cell(3, 1), new Cell(3, 3) }) grid.TryPlaceWire(cell, PortKind.Signal);
            grid.TryPlaceWire(new Cell(0, 3), PortKind.Energy);
        }
        else return CreateStarter();
        return grid;
    }

    public Component? GetComponentAt(Cell cell) => _components.Values.FirstOrDefault(c => c.Cells.Contains(cell));

    public bool TryPlaceWire(Cell cell, PortKind kind)
    {
        if (!Enum.IsDefined(kind)) return Reject("线路类型无效。");
        if (!Inside(cell)) return Reject("线路超出阵盘。");
        if (GetComponentAt(cell) is not null) return Reject("这里已有部件，线路不能穿过部件。");
        if (_wires.TryGetValue(cell, out var existing))
            return existing == kind ? Accept() : Reject("线路相叠会干扰传导，初阶请绕开已有线路。");
        var candidate = new Dictionary<Cell, PortKind>(_wires) { [cell] = kind };
        return Commit(_components, candidate);
    }

    public bool RemoveWire(Cell cell)
    {
        if (!_wires.ContainsKey(cell)) return Reject("这里没有线路。");
        var candidate = new Dictionary<Cell, PortKind>(_wires);
        candidate.Remove(cell);
        return Commit(_components, candidate);
    }

    /// <summary>线路模式只能从同类线路或该类真实接口起笔；具体接口须由画布按位置命中。</summary>
    public bool IsWireAnchor(Cell cell, PortKind kind) => Enum.IsDefined(kind) && Inside(cell) &&
        (_wires.TryGetValue(cell, out var existing) && existing == kind ||
         _components.Values.Any(component => component.Ports.Any(port => port.Cell == cell && port.Kind == kind)));

    /// <summary>检验真实接口属于当前盘且与当前铺线类型一致；允许从输入或输出起笔。</summary>
    public bool CanStartWireFromPort(Port port, PortKind kind) => Enum.IsDefined(kind) && port.Kind == kind &&
        _components.Values.Any(component => component.Ports.Contains(port));

    /// <summary>删除目标线及本次操作造成失去源头连接的同类线。此前独立草图和仍连源分支保留。</summary>
    public bool RemoveWireAndDisconnected(Cell cell, out IReadOnlyList<Cell> removed)
    {
        removed = Array.Empty<Cell>();
        if (!_wires.TryGetValue(cell, out var kind)) return Reject("这里没有线路。");
        var before = SourceConnectedWireCells(kind, _wires);
        var beforeAnyPort = PortConnectedWireCells(kind, _wires, _components);
        var selectedRegion = WireRegion(cell, kind, _wires);
        var candidate = new Dictionary<Cell, PortKind>(_wires);
        candidate.Remove(cell);
        var after = SourceConnectedWireCells(kind, candidate);
        var afterAnyPort = PortConnectedWireCells(kind, candidate, _components);
        var toRemove = before.Where(c => c != cell && !after.Contains(c))
            .Concat(beforeAnyPort.Where(c => c != cell && !afterAnyPort.Contains(c)))
            .Concat(!beforeAnyPort.Contains(cell) ? selectedRegion.Where(c => c != cell) : Array.Empty<Cell>()).Distinct().ToArray();
        foreach (var disconnected in toRemove) candidate.Remove(disconnected);
        foreach (var incomplete in FindIncompleteWireCells(_components, candidate)) candidate.Remove(incomplete);
        var removedCells = _wires.Keys.Except(candidate.Keys).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
        if (!Commit(_components, candidate)) return false;
        removed = Array.AsReadOnly(removedCells);
        return true;
    }

    /// <summary>落笔后清除没有接成两端的悬空线路，同时保留已经连通的完整网络和支路。</summary>
    public bool RemoveIncompleteWireRegions(out IReadOnlyList<Cell> removed)
    {
        var candidate = new Dictionary<Cell, PortKind>(_wires);
        var cells = FindIncompleteWireCells(_components, candidate);
        removed = Array.AsReadOnly(cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray());
        if (cells.Count == 0) return false;
        foreach (var cell in cells) candidate.Remove(cell);
        return Commit(_components, candidate);
    }

    private static HashSet<Cell> FindIncompleteWireCells(IReadOnlyDictionary<string, Component> components, IReadOnlyDictionary<Cell, PortKind> wires)
    {
        var remove = new HashSet<Cell>();
        foreach (var kind in new[] { PortKind.Signal, PortKind.Energy })
        {
            var visited = new HashSet<Cell>();
            foreach (var seed in wires.Where(pair => pair.Value == kind).Select(pair => pair.Key))
            {
                if (!visited.Add(seed)) continue;
                var region = WireRegion(seed, kind, wires);
                visited.UnionWith(region);
                var active = new HashSet<Cell>(region);

                // 反复剥掉没有第二个连接点的线头。这样接在完整主线上的悬空支线也会一起消失。
                while (active.Count > 0)
                {
                    var leaves = active.Where(cell =>
                    {
                        var wireNeighbours = Directions.Count(direction => active.Contains(Step(cell, direction)));
                        var portContacts = components.Values.SelectMany(component => component.Ports)
                            .Count(port => port.Kind == kind && Step(port.Cell, port.Facing) == cell);
                        return wireNeighbours + portContacts < 2;
                    }).ToArray();
                    if (leaves.Length == 0) break;
                    foreach (var leaf in leaves) { active.Remove(leaf); remove.Add(leaf); }
                }

                if (active.Count == 0) continue;
                var attached = components.Values.SelectMany(component => component.Ports
                    .Where(port => port.Kind == kind && active.Contains(Step(port.Cell, port.Facing)))
                    .Select(port => (component.Kind, port.IsInput)));
                var complete = kind == PortKind.Energy
                    ? attached.Any(port => port.Kind == Kind.Source && !port.IsInput) && attached.Any(port => port.Kind != Kind.Source && port.IsInput)
                    : attached.Any(port => !port.IsInput) && attached.Any(port => port.IsInput);
                if (!complete) remove.UnionWith(active);
            }
        }
        return remove;
    }

    private static HashSet<Cell> WireRegion(Cell start, PortKind kind, IReadOnlyDictionary<Cell, PortKind> wires)
    {
        var result = new HashSet<Cell>(); var queue = new Queue<Cell>();
        if (wires.TryGetValue(start, out var wire) && wire == kind && result.Add(start)) queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = Step(cell, direction);
                if (wires.TryGetValue(next, out var value) && value == kind && result.Add(next)) queue.Enqueue(next);
            }
        }
        return result;
    }

    private HashSet<Cell> PortConnectedWireCells(PortKind kind, IReadOnlyDictionary<Cell, PortKind> wires, IReadOnlyDictionary<string, Component> components)
    {
        var result = new HashSet<Cell>(); var queue = new Queue<Cell>();
        foreach (var component in components.Values)
            foreach (var port in component.Ports.Where(p => p.Kind == kind))
            {
                var cell = Step(port.Cell, port.Facing);
                if (wires.TryGetValue(cell, out var wire) && wire == kind && result.Add(cell)) queue.Enqueue(cell);
            }
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = Step(cell, direction);
                if (wires.TryGetValue(next, out var wire) && wire == kind && result.Add(next)) queue.Enqueue(next);
            }
        }
        return result;
    }

    private bool CommitAfterComponentChange(Dictionary<string, Component> parts)
    {
        // 移动/转向不能借自动清线把原有线路覆盖掉。
        if (parts.Values.SelectMany(part => part.Cells).Any(cell => _wires.ContainsKey(cell)))
            return Reject("这里已有线路，请先移除，不能用部件覆盖。");
        var wires = new Dictionary<Cell, PortKind>(_wires);
        foreach (var kind in new[] { PortKind.Signal, PortKind.Energy })
        {
            var before = PortConnectedWireCells(kind, _wires, _components);
            var after = PortConnectedWireCells(kind, _wires, parts);
            var sourceBefore = SourceConnectedWireCells(kind, _wires, _components);
            var sourceAfter = SourceConnectedWireCells(kind, _wires, parts);
            foreach (var orphan in before.Where(cell => !after.Contains(cell)).Concat(sourceBefore.Where(cell => !sourceAfter.Contains(cell)))) wires.Remove(orphan);
        }
        foreach (var incomplete in FindIncompleteWireCells(parts, wires)) wires.Remove(incomplete);
        return Commit(parts, wires);
    }

    private HashSet<Cell> SourceConnectedWireCells(PortKind kind, IReadOnlyDictionary<Cell, PortKind> wires, IReadOnlyDictionary<string, Component>? components = null)
    {
        var result = new HashSet<Cell>(); var queue = new Queue<Cell>();
        foreach (var component in (components ?? _components).Values)
            foreach (var port in component.Ports)
            {
                if (port.IsInput || port.Kind != kind || kind == PortKind.Energy && component.Kind != Kind.Source) continue;
                var cell = Step(port.Cell, port.Facing);
                if (wires.TryGetValue(cell, out var wire) && wire == kind && result.Add(cell)) queue.Enqueue(cell);
            }
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var neighbour = Step(cell, direction);
                if (wires.TryGetValue(neighbour, out var wire) && wire == kind && result.Add(neighbour)) queue.Enqueue(neighbour);
            }
        }
        return result;
    }

    public bool TryPlaceComponent(Kind kind, Cell origin, int rotation = 0, string? id = null)
    {
        if (!Enum.IsDefined(kind)) return Reject("部件类型无效。");
        var firstImmediate = kind == Kind.Rhythm;
        if (firstImmediate) kind = Kind.Counter;
        if (kind == Kind.Eye) return Reject("阵眼已取消，请直接使用盘面格位。");
        if (rotation is < 0 or > 3) return Reject("部件转向无效。");
        id ??= kind.ToString().ToLowerInvariant() + "_" + Guid.NewGuid().ToString("N");
        if (!ValidId(id) || _components.ContainsKey(id)) return Reject("部件标识无效或重复。");
        if (kind == Kind.Source && _components.Values.Any(c => c.Kind == Kind.Source))
            return Reject("基础阵盘只能放置一件阵源。");
        var candidate = new Dictionary<string, Component>(_components, StringComparer.Ordinal)
        {
            [id] = new Component(id, kind, origin, rotation) { Settings = DefaultSettingsFor(kind) with { FirstImmediate = firstImmediate } }
        };
        return CommitAfterComponentChange(candidate);
    }

    public bool TryMoveComponent(string id, Cell origin)
    {
        if (id is null || !_components.TryGetValue(id, out var component)) return Reject("没有找到这件部件。");
        var candidate = new Dictionary<string, Component>(_components, StringComparer.Ordinal)
        {
            [id] = component with { Origin = origin }
        };
        return CommitAfterComponentChange(candidate);
    }

    /// <param name="delta">顺时针四分之一圈数；负数为逆时针。</param>
    public bool TryRotateComponent(string id, int delta = 1)
    {
        if (id is null || !_components.TryGetValue(id, out var component)) return Reject("没有找到这件部件。");
        var rotation = ((component.Rotation + delta % 4) % 4 + 4) % 4;
        var candidate = new Dictionary<string, Component>(_components, StringComparer.Ordinal)
        {
            [id] = component with { Rotation = rotation }
        };
        return CommitAfterComponentChange(candidate);
    }

    public bool RemoveComponent(string id)
    {
        if (id is null || !_components.TryGetValue(id, out var component)) return Reject("没有找到这件部件。");
        var candidate = new Dictionary<string, Component>(_components, StringComparer.Ordinal);
        candidate.Remove(id);
        return CommitAfterComponentChange(candidate);
    }

    public bool TryConfigureComponent(string id, Settings settings)
    {
        if (id is null || !_components.TryGetValue(id, out var component)) return Reject("没有找到这件部件。");
        if (!ValidSettings(component.Kind, settings)) return Reject("参数无效，已有设置保留。");
        var candidate = new Dictionary<string, Component>(_components, StringComparer.Ordinal)
        {
            [id] = component with { Settings = settings }
        };
        return CommitAfterComponentChange(candidate);
    }

    /// <summary>每条物理信号网络单独保留；同一分流和合流之间的两路不能提前合成一条边。</summary>
    public IReadOnlyList<SignalConnection> GetSignalConnections()
    {
        var graph = BuildNetworkGraph();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<SignalConnection>();
        foreach (var root in graph.Values.Where(n => n.Kind == PortKind.Signal))
        {
            if (!visited.Add(root.Id)) continue;
            var region = new List<NetworkNode>();
            var queue = new Queue<NetworkNode>(); queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue(); region.Add(node);
                foreach (var neighbour in node.Neighbours) if (visited.Add(neighbour)) queue.Enqueue(graph[neighbour]);
            }
            var inputs = region.Where(n => n.Port is { IsInput: true }).ToArray();
            var outputs = region.Where(n => n.Port is { IsInput: false }).ToArray();
            if (inputs.Length == 1 && outputs.Length == 1)
                result.Add(new SignalConnection(outputs[0].Component!.Id, inputs[0].Component!.Id,
                    Array.AsReadOnly(region.Where(n => !n.Port.HasValue).Select(n => n.Cell).ToArray()))
                { FromPortName = outputs[0].Port!.Value.Name, ToPortName = inputs[0].Port!.Value.Name,
                  SignalType = outputs[0].Port!.Value.SignalType, Unit = outputs[0].Port!.Value.Unit });
        }
        return result.AsReadOnly();
    }

    /// <summary>线路格返回自动连接掩码；部件格仅返回该格已连通的真实端口。</summary>
    public Direction GetConnectionMask(Cell cell) => ConnectionMask(cell, _components, _wires);

    public IReadOnlyList<string> GetIssues()
    {
        var issues = new HashSet<string>(StringComparer.Ordinal);
        if (!_components.Values.Any(c => c.Kind == Kind.Source)) issues.Add("缺少阵源。");
        if (!_components.Values.Any(c => c.Kind is Kind.Sensor or Kind.StateInput)) issues.Add("缺少感应器。");
        if (!_components.Values.Any(c => IsOutput(c.Kind))) issues.Add("缺少执行阵器。");
        var graph = BuildNetworkGraph();
        foreach (var (cell, kind) in _wires.OrderBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X))
        {
            var degree = BitCount(GetConnectionMask(cell));
            if (degree < 2) issues.Add($"{LineName(kind)}断点{CellName(cell)}。");
        }
        foreach (var node in graph.Values.Where(n => n.Port.HasValue))
            if (node.Neighbours.Count == 0)
            {
                var port = node.Port!.Value;
                issues.Add($"{Name(node.Component!.Kind)}的{LineName(port.Kind)}{(port.IsInput ? "入口" : "出口")}未连接{CellName(port.Cell)}。");
            }

        var signalEdges = _components.Keys.ToDictionary(id => id, _ => new HashSet<string>(), StringComparer.Ordinal);
        var powered = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in graph.Values)
        {
            if (!visited.Add(root.Id)) continue;
            var nodes = new List<NetworkNode>();
            var queue = new Queue<NetworkNode>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                nodes.Add(node);
                foreach (var neighbour in node.Neighbours)
                    if (visited.Add(neighbour)) queue.Enqueue(graph[neighbour]);
            }
            if (nodes.Count == 1 && root.Port.HasValue) continue;
            var inputs = nodes.Where(n => n.Port is { IsInput: true }).ToArray();
            var outputs = nodes.Where(n => n.Port is { IsInput: false }).ToArray();
            if (root.Kind == PortKind.Signal)
            {
                if (inputs.Length != 1 || outputs.Length != 1)
                    issues.Add($"信号线路{CellName(root.Cell)}须连接一个出口和一个入口，分合流请使用部件。");
                else
                {
                    var output = outputs[0].Port!.Value; var input = inputs[0].Port!.Value;
                    if (output.SignalType != input.SignalType || output.Unit != input.Unit) issues.Add($"信号线路{CellName(root.Cell)}的类型或数值单位不匹配。");
                    signalEdges[outputs[0].Component!.Id].Add(inputs[0].Component!.Id);
                }
            }
            else
            {
                if (outputs.Length != 1 || outputs[0].Component?.Kind != Kind.Source || inputs.Length == 0)
                    issues.Add($"供能线路{CellName(root.Cell)}须连接唯一阵源和执行阵器。");
                else foreach (var input in inputs) powered.Add(input.Component!.Id);
            }
        }
        var fromSensors = Reachable(_components.Values.Where(c => c.Kind is Kind.Sensor or Kind.StateInput).Select(c => c.Id), signalEdges);
        var reversed = _components.Keys.ToDictionary(id => id, _ => new HashSet<string>(), StringComparer.Ordinal);
        foreach (var (from, targets) in signalEdges)
            foreach (var target in targets) reversed[target].Add(from);
        var toWards = Reachable(_components.Values.Where(c => IsOutput(c.Kind)).Select(c => c.Id), reversed);
        foreach (var component in _components.Values.Where(c => c.Ports.Any(p => p.Kind == PortKind.Signal)))
        {
            if (!fromSensors.Contains(component.Id)) issues.Add($"{Name(component.Kind)}未连到有效感应来源。");
            if (!toWards.Contains(component.Id)) issues.Add($"{Name(component.Kind)}未连到执行阵器。");
            if (IsOutput(component.Kind) && !powered.Contains(component.Id)) issues.Add(Name(component.Kind) + "没有连通阵源。");
        }
        if (HasCycle(signalEdges)) issues.Add("信号线路存在回路，基础阵盘不支持反馈。");
        return issues.OrderBy(issue => issue, StringComparer.Ordinal).ToArray();
    }

    public static bool IsOutput(Kind kind) => kind is Kind.Ward or Kind.Fire or Kind.Guard;

    public IReadOnlyDictionary<string, string> GetPowerSources()
    {
        var graph = BuildNetworkGraph(); var visited = new HashSet<string>(StringComparer.Ordinal); var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in graph.Values.Where(n => n.Kind == PortKind.Energy))
        {
            if (!visited.Add(root.Id)) continue; var nodes = new List<NetworkNode>(); var queue = new Queue<NetworkNode>(); queue.Enqueue(root);
            while (queue.Count > 0) { var n = queue.Dequeue(); nodes.Add(n); foreach (var other in n.Neighbours) if (visited.Add(other)) queue.Enqueue(graph[other]); }
            var sources = nodes.Where(n => n.Port is { IsInput: false } && n.Component?.Kind == Kind.Source).ToArray();
            if (sources.Length != 1) continue;
            foreach (var output in nodes.Where(n => n.Port is { IsInput: true } && n.Component is not null && IsOutput(n.Component.Kind))) result[output.Component!.Id] = sources[0].Component!.Id;
        }
        return new ReadOnlyDictionary<string, string>(result);
    }

    public string Serialize() => JsonSerializer.Serialize(new
    {
        version = BoardSize == 4 && _components.Values.All(c => (int)c.Kind <= (int)Kind.Merge && c.Settings.SignalType == FormationSignalType.Event && c.Settings.Unit == FormationValueUnit.None) ? 1 : 2,
        size = BoardSize,
        components = _components.Values.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => new
        {
            id = c.Id, kind = c.Kind.ToString(), x = c.Origin.X, y = c.Origin.Y, rotation = c.Rotation,
            settings = SerializedSettings(c)
        }),
        wires = _wires.OrderBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X).Select(kv => new
        {
            x = kv.Key.X, y = kv.Key.Y, kind = kv.Value.ToString()
        })
    }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

    private static object? SerializedSettings(Component c)
    {
        if (c.Settings == DefaultSettingsFor(c.Kind)) return null;
        var s = c.Settings;
        var result = new Dictionary<string, object>
        { ["interval"] = s.Interval, ["minimum_amplitude"] = s.MinimumAmplitude, ["source_tag"] = s.SourceTag, ["input_event"] = s.InputEvent, ["first_immediate"] = s.FirstImmediate };
        if ((int)c.Kind >= (int)Kind.StateInput || s.SignalType != FormationSignalType.Event || s.Unit != FormationValueUnit.None)
        {
            result["signal_type"] = s.SignalType.ToString(); result["unit"] = s.Unit.ToString(); result["state_key"] = s.StateKey;
            result["comparison"] = s.Comparison.ToString(); result["low"] = s.Low; result["high"] = s.High; result["normally_closed"] = s.NormallyClosed;
        }
        return result;
    }

    /// <summary>只恢复结构合法的草图；悬空端点可保留，通过GetIssues检查完整性。</summary>
    public static bool TryDeserialize(string? json, out FormationGridModel? model)
    {
        model = null;
        if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) return false;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            if (!HasFields(root, "version", "size", "components", "wires") ||
                !Int(root, "version", out var version) || version is not (1 or 2) ||
                !Int(root, "size", out var size) || size is not (4 or 5) || version == 1 && size != 4) return false;
            var components = root.GetProperty("components");
            var wires = root.GetProperty("wires");
            if (components.ValueKind != JsonValueKind.Array || wires.ValueKind != JsonValueKind.Array ||
                components.GetArrayLength() > size * size || wires.GetArrayLength() > size * size) return false;
            var candidate = new FormationGridModel(size);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in components.EnumerateArray())
            {
                var hasSettings = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("settings", out _);
                if (!(hasSettings ? HasFields(element, "id", "kind", "x", "y", "rotation", "settings") : HasFields(element, "id", "kind", "x", "y", "rotation")) ||
                    !Text(element, "id", out var id) || !ValidId(id) || !ids.Add(id) ||
                    !Text(element, "kind", out var kindText) || !TryEnum(kindText, out Kind kind) || version == 1 && (int)kind > (int)Kind.Merge ||
                    !Int(element, "x", out var x) || !Int(element, "y", out var y) ||
                    !Int(element, "rotation", out var rotation) || rotation is < 0 or > 3) return false;
                if (kind == Kind.Eye)
                {
                    // 旧版把阵眼作为固定部件写入草图。验证旧位置后丢弃，释放该格。
                    var oldEyeCell = size == 5 ? new Cell(2, 2) : new Cell(1, 1);
                    if (id != "eye" || new Cell(x, y) != oldEyeCell || rotation != 0) return false;
                    continue;
                }
                else if (!candidate.TryPlaceComponent(kind, new Cell(x, y), rotation, id)) return false;
                if (hasSettings)
                {
                    if (!TryReadSettings(element.GetProperty("settings"), kind, version, out var restored) || !candidate.TryConfigureComponent(id, restored!)) return false;
                }
            }
            var wireCells = new HashSet<Cell>();
            foreach (var element in wires.EnumerateArray())
            {
                if (!HasFields(element, "x", "y", "kind") ||
                    !Int(element, "x", out var x) || !Int(element, "y", out var y) ||
                    !Text(element, "kind", out var kindText) || !TryEnum(kindText, out PortKind kind)) return false;
                var cell = new Cell(x, y);
                if (!wireCells.Add(cell) || !candidate.TryPlaceWire(cell, kind)) return false;
            }
            model = candidate;
            return true;
        }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public static string Name(Kind kind) => kind switch
    {
        Kind.Eye => "旧版部件", Kind.Source => "阵源", Kind.Sensor => "感应器", Kind.Ward => "护持",
        Kind.Counter or Kind.Rhythm => "计数器", Kind.Filter => "筛选器", Kind.Split => "分流器", Kind.Merge => "合流器",
        Kind.StateInput => "状态输入", Kind.Compare => "比较器", Kind.Not => "取反", Kind.Switch => "开关", Kind.Fire => "火击", Kind.Guard => "护罩", Kind.And => "与门", Kind.Or => "或门", _ => "未知部件"
    };

    private bool Commit(IReadOnlyDictionary<string, Component> components, IReadOnlyDictionary<Cell, PortKind> wires)
    {
        var occupied = new HashSet<Cell>();
        foreach (var component in components.Values)
            foreach (var cell in component.Cells)
            {
                if (!Inside(cell)) return Reject("部件超出阵盘，整件都要放在有效格内。");
                if (!occupied.Add(cell)) return Reject("部件占格重叠。");
                if (wires.ContainsKey(cell)) return Reject("这里已有线路，请先移除，不能用部件覆盖。");
            }
        foreach (var (cell, kind) in wires)
        {
            var degree = BitCount(ConnectionMask(cell, components, wires));
            if (degree == 4) return Reject("四向交叉会干扰传导，基础单层请绕线。");
            if (kind == PortKind.Signal && degree > 2) return Reject("信号线不能分成三岔，请使用分流器或合流器。");
            if (kind == PortKind.Energy && degree > 3) return Reject("供能线最多连接三方向。");
        }
        if (!ReferenceEquals(components, _components))
        {
            _components.Clear();
            foreach (var pair in components) _components.Add(pair.Key, pair.Value);
        }
        if (!ReferenceEquals(wires, _wires))
        {
            _wires.Clear();
            foreach (var pair in wires) _wires.Add(pair.Key, pair.Value);
        }
        return Accept();
    }

    private static Direction ConnectionMask(Cell cell, IReadOnlyDictionary<string, Component> components, IReadOnlyDictionary<Cell, PortKind> wires)
    {
        var ports = components.Values.SelectMany(c => c.Ports).ToArray();
        var mask = Direction.None;
        if (wires.TryGetValue(cell, out var kind))
        {
            foreach (var direction in Directions)
            {
                var neighbour = Step(cell, direction);
                if (wires.TryGetValue(neighbour, out var other) && other == kind ||
                    ports.Any(p => p.Cell == neighbour && p.Kind == kind && p.Facing == Opposite(direction))) mask |= direction;
            }
        }
        else
            foreach (var port in ports.Where(p => p.Cell == cell))
            {
                var neighbour = Step(cell, port.Facing);
                if (wires.TryGetValue(neighbour, out var other) && other == port.Kind ||
                    ports.Any(p => p.Cell == neighbour && p.Kind == port.Kind && p.Facing == Opposite(port.Facing) && p.IsInput != port.IsInput))
                    mask |= port.Facing;
            }
        return mask;
    }

    private sealed class NetworkNode
    {
        public string Id { get; init; } = "";
        public Cell Cell { get; init; }
        public PortKind Kind { get; init; }
        public Port? Port { get; init; }
        public Component? Component { get; init; }
        public HashSet<string> Neighbours { get; } = new(StringComparer.Ordinal);
    }

    private Dictionary<string, NetworkNode> BuildNetworkGraph()
    {
        var graph = new Dictionary<string, NetworkNode>(StringComparer.Ordinal);
        var wireIds = new Dictionary<Cell, string>();
        foreach (var (cell, kind) in _wires.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            var id = $"w:{cell.X}:{cell.Y}";
            graph.Add(id, new NetworkNode { Id = id, Cell = cell, Kind = kind });
            wireIds.Add(cell, id);
        }
        var portNodes = new List<NetworkNode>();
        foreach (var component in _components.Values.OrderBy(component => component.Id, StringComparer.Ordinal))
            for (var i = 0; i < component.Ports.Count; i++)
            {
                var port = component.Ports[i];
                var id = $"p:{component.Id}:{i}";
                var node = new NetworkNode { Id = id, Cell = port.Cell, Kind = port.Kind, Port = port, Component = component };
                graph.Add(id, node);
                portNodes.Add(node);
            }
        void Connect(NetworkNode a, NetworkNode b) { a.Neighbours.Add(b.Id); b.Neighbours.Add(a.Id); }
        foreach (var (cell, id) in wireIds)
            foreach (var direction in Directions)
            {
                var neighbour = Step(cell, direction);
                if (wireIds.TryGetValue(neighbour, out var other) && graph[other].Kind == graph[id].Kind) Connect(graph[id], graph[other]);
            }
        foreach (var node in portNodes)
        {
            var port = node.Port!.Value;
            var neighbour = Step(port.Cell, port.Facing);
            if (wireIds.TryGetValue(neighbour, out var wire) && graph[wire].Kind == port.Kind) Connect(node, graph[wire]);
            foreach (var other in portNodes)
                if (other != node && other.Cell == neighbour && other.Kind == port.Kind &&
                    other.Port!.Value.Facing == Opposite(port.Facing) && other.Port.Value.IsInput != port.IsInput) Connect(node, other);
        }
        return graph;
    }

    private static HashSet<string> Reachable(IEnumerable<string> starts, Dictionary<string, HashSet<string>> edges)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(starts);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            foreach (var next in edges[id]) queue.Enqueue(next);
        }
        return visited;
    }

    private static bool HasCycle(Dictionary<string, HashSet<string>> edges)
    {
        var marks = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (marks.TryGetValue(id, out var mark)) return mark == 1;
            marks[id] = 1;
            foreach (var next in edges[id]) if (Visit(next)) return true;
            marks[id] = 2;
            return false;
        }
        return edges.Keys.Any(Visit);
    }

    private static IReadOnlyList<Port> PortsOf(Component component)
    {
        var ports = new List<Port>();
        void Add(int x, int y, Direction facing, PortKind kind, bool input, string name,
            FormationSignalType type = FormationSignalType.Event, FormationValueUnit unit = FormationValueUnit.None) =>
            ports.Add(new Port(new Cell(x, y), facing, kind, input) { Name = name, SignalType = type, Unit = unit });
        switch (component.Kind)
        {
            case Kind.Source: Add(1, 0, Direction.East, PortKind.Energy, false, "energy"); break;
            case Kind.Sensor: Add(0, 0, Direction.East, PortKind.Signal, false, "event"); break;
            case Kind.Ward:
            case Kind.Fire:
            case Kind.Guard:
                Add(0, 0, Direction.North, PortKind.Signal, true, component.Kind == Kind.Guard ? "permit" : "event", component.Kind == Kind.Guard ? FormationSignalType.Switch : FormationSignalType.Event);
                Add(0, 1, Direction.South, PortKind.Energy, true, "energy"); break;
            case Kind.Counter:
            case Kind.Rhythm:
                Add(0, 0, Direction.West, PortKind.Signal, true, "event");
                Add(1, 0, Direction.North, PortKind.Signal, false, "event"); break;
            case Kind.Filter:
                Add(0, 0, Direction.West, PortKind.Signal, true, "event");
                Add(0, 0, Direction.East, PortKind.Signal, false, "event"); break;
            case Kind.Split:
                Add(0, 0, Direction.West, PortKind.Signal, true, "value", component.Settings.SignalType, component.Settings.Unit);
                Add(0, 0, Direction.North, PortKind.Signal, false, "b", component.Settings.SignalType, component.Settings.Unit);
                Add(0, 0, Direction.East, PortKind.Signal, false, "a", component.Settings.SignalType, component.Settings.Unit); break;
            case Kind.Merge:
                Add(0, 0, Direction.West, PortKind.Signal, true, "a");
                Add(0, 0, Direction.South, PortKind.Signal, true, "b");
                Add(0, 0, Direction.East, PortKind.Signal, false, "event"); break;
            case Kind.StateInput: Add(0, 0, Direction.East, PortKind.Signal, false, "value", component.Settings.SignalType, component.Settings.Unit); break;
            case Kind.Compare:
                Add(0, 0, Direction.West, PortKind.Signal, true, "value", FormationSignalType.Number, component.Settings.Unit);
                Add(1, 0, Direction.East, PortKind.Signal, false, "state", FormationSignalType.Switch); break;
            case Kind.Not:
                Add(0, 0, Direction.West, PortKind.Signal, true, "state", FormationSignalType.Switch);
                Add(0, 0, Direction.East, PortKind.Signal, false, "state", FormationSignalType.Switch); break;
            case Kind.Switch:
                Add(0, 0, Direction.West, PortKind.Signal, true, "event");
                Add(0, 0, Direction.North, PortKind.Signal, true, "permit", FormationSignalType.Switch);
                Add(0, 0, Direction.East, PortKind.Signal, false, "event"); break;
            case Kind.And:
            case Kind.Or:
                Add(0, 0, Direction.West, PortKind.Signal, true, "a", FormationSignalType.Switch);
                Add(0, 0, Direction.North, PortKind.Signal, true, "b", FormationSignalType.Switch);
                Add(0, 0, Direction.East, PortKind.Signal, false, "state", FormationSignalType.Switch); break;
        }
        var baseSize = BaseSize(component.Kind);
        return ports.Select(port =>
        {
            var cell = port.Cell;
            var facing = port.Facing;
            var width = baseSize.X;
            var height = baseSize.Y;
            for (var i = 0; i < component.Rotation; i++)
            {
                cell = new Cell(height - 1 - cell.Y, cell.X);
                facing = RotateClockwise(facing);
                (width, height) = (height, width);
            }
            return port with { Cell = new Cell(component.Origin.X + cell.X, component.Origin.Y + cell.Y), Facing = facing };
        }).ToArray();
    }

    private static Cell BaseSize(Kind kind) => kind switch
    {
        Kind.Source or Kind.Counter or Kind.Rhythm or Kind.Compare => new Cell(2, 1),
        Kind.Ward or Kind.Fire or Kind.Guard => new Cell(1, 2), _ => new Cell(1, 1)
    };
    private static int ComplexityOf(Kind kind) => kind switch
    {
        Kind.Eye or Kind.Source => 0, Kind.Counter or Kind.Rhythm or Kind.Compare => 2, _ => 1
    };
    private static readonly Direction[] Directions = { Direction.North, Direction.East, Direction.South, Direction.West };
    private static Cell Step(Cell cell, Direction direction) => direction switch
    {
        Direction.North => cell with { Y = cell.Y - 1 }, Direction.East => cell with { X = cell.X + 1 },
        Direction.South => cell with { Y = cell.Y + 1 }, Direction.West => cell with { X = cell.X - 1 }, _ => cell
    };
    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South, Direction.East => Direction.West,
        Direction.South => Direction.North, Direction.West => Direction.East, _ => Direction.None
    };
    private static Direction RotateClockwise(Direction direction) => direction switch
    {
        Direction.North => Direction.East, Direction.East => Direction.South,
        Direction.South => Direction.West, Direction.West => Direction.North, _ => Direction.None
    };
    private bool Inside(Cell cell) => cell.X >= 0 && cell.X < BoardSize && cell.Y >= 0 && cell.Y < BoardSize;
    private static int BitCount(Direction mask) => System.Numerics.BitOperations.PopCount((uint)mask);
    private static string CellName(Cell cell) => $"（{cell.X + 1},{cell.Y + 1}）";
    private static string LineName(PortKind kind) => kind == PortKind.Signal ? "信号线" : "供能线";
    private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && !id.Any(char.IsControl);
    private static bool ValidSettings(Kind kind, Settings? settings)
    {
        if (settings is null || settings.Interval is < 1 or > 12 || settings.MinimumAmplitude is < 1 or > 9 ||
            settings.SourceTag is null || settings.SourceTag.Length > 64 || settings.SourceTag.Any(char.IsControl) ||
            settings.SourceTag != settings.SourceTag.Trim() || settings.InputEvent is null) return false;
        if (kind is not (Kind.Counter or Kind.Rhythm) && settings.Interval != 2) return false;
        if (kind is not (Kind.Counter or Kind.Rhythm) && settings.FirstImmediate) return false;
        if (kind != Kind.Filter && settings.MinimumAmplitude != 1) return false;
        if (kind is not (Kind.Sensor or Kind.Filter) && settings.SourceTag.Length > 0) return false;
        if (!Enum.IsDefined(settings.SignalType) || !Enum.IsDefined(settings.Unit) || !Enum.IsDefined(settings.Comparison) ||
            !double.IsFinite(settings.Low) || !double.IsFinite(settings.High) || settings.Low < 0 || settings.High < 0) return false;
        if (kind != Kind.Switch && settings.NormallyClosed) return false;
        if (kind != Kind.StateInput && settings.StateKey != "health") return false;
        if (kind != Kind.Compare && (settings.Comparison != FormationControlCircuit.CompareMode.Band || settings.Low != 30 || settings.High != 60)) return false;
        if (kind == Kind.StateInput)
        {
            var expectedUnit = settings.StateKey switch { "health" => FormationValueUnit.Percent, "shield" => FormationValueUnit.Shield, "mana" => FormationValueUnit.Mana, "energy" => FormationValueUnit.Energy, _ => FormationValueUnit.None };
            if (settings.SignalType != FormationSignalType.Number || expectedUnit == FormationValueUnit.None || settings.Unit != expectedUnit) return false;
        }
        else if (kind == Kind.Compare)
        {
            if (settings.SignalType != FormationSignalType.Number || settings.Unit == FormationValueUnit.None ||
                settings.Comparison == FormationControlCircuit.CompareMode.Band && settings.Low >= settings.High ||
                settings.Unit == FormationValueUnit.Percent && (settings.Low > 100 || settings.High > 100)) return false;
        }
        else if (kind == Kind.Split)
        {
            if ((settings.SignalType == FormationSignalType.Number) != (settings.Unit != FormationValueUnit.None)) return false;
        }
        else if (settings.SignalType != FormationSignalType.Event || settings.Unit != FormationValueUnit.None) return false;
        if (kind != Kind.Sensor) return settings.InputEvent == "spell_played";
        return settings.InputEvent is "spell_played" or "spell_completed" or "card_played" or "turn_start" or "cycle_completed";
    }
    private static bool TryReadSettings(JsonElement element, Kind kind, int version, out Settings? settings)
    {
        settings = null; if (element.ValueKind != JsonValueKind.Object) return false;
        var required = new[] { "interval", "minimum_amplitude", "source_tag", "input_event" };
        var advanced = new[] { "signal_type", "unit", "state_key", "comparison", "low", "high", "normally_closed" };
        var allowed = new HashSet<string>(required.Concat(new[] { "first_immediate" }).Concat(version == 2 ? advanced : Array.Empty<string>()), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) return false;
        if (required.Any(name => !seen.Contains(name)) || !Int(element, "interval", out var interval) || !Int(element, "minimum_amplitude", out var minimum) || !Text(element, "source_tag", out var tag) || !Text(element, "input_event", out var evt)) return false;
        var immediate = kind == Kind.Rhythm;
        if (seen.Contains("first_immediate") && !Bool(element, "first_immediate", out immediate)) return false;
        var value = DefaultSettingsFor(kind) with { Interval = interval, MinimumAmplitude = minimum, SourceTag = tag, InputEvent = evt, FirstImmediate = immediate || kind == Kind.Rhythm };
        if (advanced.Any(seen.Contains))
        {
            if (advanced.Any(name => !seen.Contains(name)) || !Text(element, "signal_type", out var typeText) || !TryEnum(typeText, out FormationSignalType type) ||
                !Text(element, "unit", out var unitText) || !TryEnum(unitText, out FormationValueUnit unit) || !Text(element, "state_key", out var key) ||
                !Text(element, "comparison", out var modeText) || !TryEnum(modeText, out FormationControlCircuit.CompareMode mode) ||
                !element.GetProperty("low").TryGetDouble(out var low) || !element.GetProperty("high").TryGetDouble(out var high) || !Bool(element, "normally_closed", out var closed)) return false;
            value = value with { SignalType = type, Unit = unit, StateKey = key, Comparison = mode, Low = low, High = high, NormallyClosed = closed };
        }
        if (!ValidSettings(kind, value)) return false; settings = value; return true;
    }
    private bool Reject(string message) { LastError = message; return false; }
    private bool Accept() { LastError = ""; return true; }
    private static bool HasFields(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var expected = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) if (!expected.Remove(property.Name)) return false;
        return expected.Count == 0;
    }
    private static bool Int(JsonElement element, string name, out int value)
    {
        var property = element.GetProperty(name);
        value = 0;
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }
    private static bool Text(JsonElement element, string name, out string value)
    {
        var property = element.GetProperty(name);
        value = "";
        if (property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString() ?? "";
        return true;
    }
    private static bool Bool(JsonElement element, string name, out bool value)
    {
        var property = element.GetProperty(name);
        value = property.ValueKind == JsonValueKind.True;
        return property.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }
    private static bool TryEnum<T>(string text, out T value) where T : struct, Enum
    {
        value = default;
        return Enum.GetNames<T>().Contains(text, StringComparer.Ordinal) && Enum.TryParse(text, out value);
    }
}
