using System.Text.Json.Nodes;
using Grid = FormationGridModel;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;
using Direction = FormationGridModel.Direction;

internal static class Program
{
    private static int _assertions;
    private static int _groups;

    private static int Main()
    {
        try
        {
            Run("starter and immutable eye", Starter);
            Run("footprints and rotated ports", Rotations);
            Run("atomic footprint and bounds rejection", AtomicPlacement);
            Run("port side and network type isolation", TypeAndSide);
            Run("wire and component induced branches", Branches);
            Run("deletion and movement rebuild connections", Recompute);
            Run("adjacent ports connect directly", DirectPorts);
            Run("complexity and disconnected draft issues", DraftIssues);
            Run("strict persistence schema and roundtrip", Persistence);
            Run("deterministic edit/restore property checks", EditSequences);
            LogicChecks.Run(Run, Check);
            Console.WriteLine($"PASS: {_groups} groups, {_assertions} assertions.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(string name, Action action)
    {
        action();
        _groups++;
        Console.WriteLine("PASS " + name);
    }

    private static void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Refused(Grid grid, Func<bool> mutation, string message)
    {
        var before = grid.Serialize();
        Check(!mutation(), message + " should be refused");
        Check(before == grid.Serialize(), message + " should be atomic");
        Check(grid.LastError.Length > 0, message + " should explain refusal");
    }

    private static Grid.Port Geometry(Grid.Port port) => new(port.Cell, port.Facing, port.Kind, port.IsInput);

    private static void Starter()
    {
        var grid = Grid.CreateStarter();
        Check(grid.Components.Count == 3 && grid.Components.Values.Sum(c => c.Cells.Count) == 5, "starter has five occupied component cells");
        Check(grid.Wires.Count == 5, "starter has five wire cells");
        Check(grid.Complexity == 2, "starter complexity is two");
        Check(grid.IsCircuitConnected, "starter must be connected: " + string.Join(";", grid.GetIssues()));
        Check(grid.GetConnectionMask(new Cell(1, 0)) == (Direction.East | Direction.West), "straight signal mask");
        Check(grid.GetConnectionMask(new Cell(3, 0)) == (Direction.West | Direction.South), "signal elbow mask");
        Check(grid.GetConnectionMask(new Cell(3, 3)) == (Direction.West | Direction.North), "energy elbow mask");
        Check(grid.GetComponentAt(new Cell(1, 1)) is null, "盤心初始可用");
        Check(grid.TryPlaceComponent(Kind.Filter, new Cell(1, 1), id: "center"), "盤心可放一般部件");
        Check(grid.RemoveComponent("center"), "盤心部件可移除");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Eye, new Cell(0, 1)), "cancelled eye cannot be placed");
    }

    private static void Rotations()
    {
        foreach (var kind in Enum.GetValues<Kind>())
            foreach (var rotation in Enumerable.Range(0, 4))
            {
                var component = new Grid.Component("part", kind, new Cell(0, 0), rotation);
                Check(component.Cells.Count == component.Width * component.Height, "rotated footprint area " + kind);
                Check(component.Cells.Distinct().Count() == component.Cells.Count, "no duplicate occupied cells");
                foreach (var port in component.Ports)
                    Check(component.Cells.Contains(port.Cell), "rotated port remains on own footprint " + kind);
            }
        var ward = new Grid.Component("w", Kind.Ward, new Cell(0, 0), 1);
        Check(ward.Width == 2 && ward.Height == 1, "quarter turn swaps ward footprint");
        Check(Geometry(ward.Ports[0]) == new Grid.Port(new Cell(1, 0), Direction.East, PortKind.Signal, true), "ward signal port rotates position and side");
        Check(Geometry(ward.Ports[1]) == new Grid.Port(new Cell(0, 0), Direction.West, PortKind.Energy, true), "ward energy port rotates position and side");
        var counter = new Grid.Component("c", Kind.Counter, new Cell(0, 0), 1);
        Check(Geometry(counter.Ports[0]) == new Grid.Port(new Cell(0, 0), Direction.North, PortKind.Signal, true), "counter input rotates north");
        Check(Geometry(counter.Ports[1]) == new Grid.Port(new Cell(0, 1), Direction.East, PortKind.Signal, false), "counter output rotates east on lower cell");
        var source = new Grid.Component("s", Kind.Source, new Cell(0, 0), 2);
        Check(Geometry(source.Ports.Single()) == new Grid.Port(new Cell(0, 0), Direction.West, PortKind.Energy, false), "source half turn moves output to opposite end");
        var grid = new Grid();
        Check(grid.TryPlaceComponent(Kind.Counter, new Cell(2, 1), id: "counter"), "place counter for rotation cycle");
        var original = grid.Serialize();
        for (var i = 0; i < 4; i++) Check(grid.TryRotateComponent("counter"), "rotation cycle remains in bounds");
        Check(grid.Serialize() == original, "four rotations return canonical geometry");
        Check(grid.TryRotateComponent("counter", -1) && grid.Components["counter"].Rotation == 3, "negative rotation supported");
    }

    private static void AtomicPlacement()
    {
        var grid = Grid.CreateStarter();
        Refused(grid, () => grid.TryPlaceWire(new Cell(-1, 0), PortKind.Signal), "negative wire coordinate");
        Refused(grid, () => grid.TryPlaceWire(new Cell(4, 0), PortKind.Energy), "wire outside right edge");
        Check(grid.TryPlaceWire(new Cell(1, 1), PortKind.Signal), "signal wire may use the former center cell");
        Refused(grid, () => grid.TryPlaceWire(new Cell(1, 0), PortKind.Energy), "wire over other wire type");
        Refused(grid, () => grid.TryPlaceWire(new Cell(0, 1), (PortKind)19), "undefined wire kind");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Counter, new Cell(3, 3)), "partial multi-cell footprint outside");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Filter, new Cell(1, 0)), "module over wire");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Filter, new Cell(1, 1)), "module over wire");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Source, new Cell(0, 1)), "second source");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Filter, new Cell(0, 1), id: "ward"), "duplicate module identity");
        Refused(grid, () => grid.TryPlaceComponent((Kind)80, new Cell(0, 1)), "undefined module kind");
        Refused(grid, () => grid.TryPlaceComponent(Kind.Filter, new Cell(0, 1), 9), "invalid initial rotation");
        Refused(grid, () => grid.TryMoveComponent("ward", new Cell(2, 3)), "move partially outside");
        Refused(grid, () => grid.TryMoveComponent("ward", new Cell(2, 0)), "move over existing wire");
        Refused(grid, () => grid.TryMoveComponent("ward", new Cell(0, 0)), "move over existing module");
        Refused(grid, () => grid.TryRotateComponent("ward"), "rotate over edge");
        Refused(grid, () => grid.TryMoveComponent("missing", new Cell(0, 1)), "move missing module");
        Check(grid.TryPlaceWire(new Cell(1, 0), PortKind.Signal) && grid.LastError == "", "idempotent draw clears error");
    }

    private static void TypeAndSide()
    {
        var grid = new Grid();
        Check(grid.TryPlaceWire(new Cell(0, 0), PortKind.Signal), "signal draft");
        Check(grid.TryPlaceWire(new Cell(1, 0), PortKind.Energy), "adjacent different type draft");
        Check(grid.GetConnectionMask(new Cell(0, 0)) == Direction.None, "signal ignores energy neighbour");
        Check(grid.GetConnectionMask(new Cell(1, 0)) == Direction.None, "energy ignores signal neighbour");
        var ports = new Grid();
        Check(ports.TryPlaceComponent(Kind.Sensor, new Cell(2, 2), id: "sensor"), "sensor placement");
        Check(ports.TryPlaceWire(new Cell(2, 1), PortKind.Signal), "wire on sensor wrong side allowed as draft");
        Check(ports.GetConnectionMask(new Cell(2, 1)) == Direction.None, "wrong side does not connect");
        Check(ports.TryPlaceWire(new Cell(3, 2), PortKind.Signal), "wire on sensor output side");
        Check(ports.GetConnectionMask(new Cell(3, 2)) == Direction.West, "correct side connects");
        Check(ports.GetConnectionMask(new Cell(2, 2)) == Direction.East, "module reports actually connected port");
        Check(ports.TryRotateComponent("sensor"), "rotate sensor toward south");
        Check(ports.GetConnectionMask(new Cell(3, 2)) == Direction.None, "rotation disconnects stale old side");
        var energy = new Grid();
        Check(energy.TryPlaceComponent(Kind.Sensor, new Cell(2, 2), id: "s"), "sensor on isolated board");
        Check(energy.TryPlaceWire(new Cell(3, 2), PortKind.Energy), "wrong network at matching facing");
        Check(energy.GetConnectionMask(new Cell(3, 2)) == Direction.None, "matching face does not override network type");
    }

    private static void Branches()
    {
        var signal = new Grid();
        foreach (var cell in new[] { new Cell(2, 2), new Cell(2, 1), new Cell(2, 3) })
            Check(signal.TryPlaceWire(cell, PortKind.Signal), "straight signal can be drawn");
        Refused(signal, () => signal.TryPlaceWire(new Cell(1, 2), PortKind.Signal), "implicit signal T");
        Refused(signal, () => signal.TryPlaceComponent(Kind.Sensor, new Cell(1, 2), id: "s"), "module output causing implicit signal T");
        var moduleFirst = new Grid();
        Check(moduleFirst.TryPlaceComponent(Kind.Sensor, new Cell(1, 2), id: "s"), "module before wire");
        Check(moduleFirst.TryPlaceWire(new Cell(2, 2), PortKind.Signal), "wire attaches module");
        Check(moduleFirst.TryPlaceWire(new Cell(2, 1), PortKind.Signal), "second connected direction");
        Refused(moduleFirst, () => moduleFirst.TryPlaceWire(new Cell(2, 3), PortKind.Signal), "wire third arm includes module port");
        var moving = new Grid();
        foreach (var cell in new[] { new Cell(2, 2), new Cell(2, 1), new Cell(2, 3) }) moving.TryPlaceWire(cell, PortKind.Signal);
        Check(moving.TryPlaceComponent(Kind.Sensor, new Cell(0, 2), id: "s"), "sensor before hidden branch move");
        Refused(moving, () => moving.TryMoveComponent("s", new Cell(1, 2)), "move causing hidden signal branch");
        var energy = new Grid();
        foreach (var cell in new[] { new Cell(2, 2), new Cell(2, 1), new Cell(1, 2), new Cell(3, 2) })
            Check(energy.TryPlaceWire(cell, PortKind.Energy), "energy permits T draft");
        Check(energy.GetConnectionMask(new Cell(2, 2)) == (Direction.North | Direction.East | Direction.West), "energy T mask");
        Refused(energy, () => energy.TryPlaceWire(new Cell(2, 3), PortKind.Energy), "four-way crossing");
        Check(energy.TryPlaceWire(new Cell(2, 3), PortKind.Signal), "other kind does not create an energy fourth arm");
        Check(energy.GetConnectionMask(new Cell(2, 2)) == (Direction.North | Direction.East | Direction.West), "unlike neighbour remains disconnected");
    }

    private static void Recompute()
    {
        var grid = Grid.CreateStarter();
        Check(grid.RemoveWire(new Cell(2, 0)), "delete middle signal");
        Check(grid.GetConnectionMask(new Cell(1, 0)) == Direction.West, "left wire drops deleted connection");
        Check(grid.GetConnectionMask(new Cell(3, 0)) == Direction.South, "right wire drops deleted connection");
        Check(!grid.IsCircuitConnected && grid.GetIssues().Any(s => s.Contains("断点")), "deleted wire makes actual circuit invalid");
        Check(grid.TryPlaceWire(new Cell(2, 0), PortKind.Signal) && grid.IsCircuitConnected, "redraw reconnects");
        Check(grid.RemoveWire(new Cell(2, 3)), "delete energy bridge");
        Check(!grid.IsCircuitConnected && grid.GetIssues().Any(s => s.Contains("阵源")), "disconnected energy cannot count as complete circuit");
        Check(grid.TryPlaceWire(new Cell(2, 3), PortKind.Energy) && grid.IsCircuitConnected, "restore energy bridge");
        Check(grid.RemoveComponent("source"), "delete source");
        Check(grid.GetConnectionMask(new Cell(2, 3)) == Direction.East, "source removal clears old port connection");
        Check(!grid.IsCircuitConnected, "source removal invalidates power");
        Check(grid.TryPlaceComponent(Kind.Source, new Cell(0, 3), id: "source") && grid.IsCircuitConnected, "restoring source reconnects without cached stale state");
    }

    private static void DirectPorts()
    {
        var grid = new Grid();
        Check(grid.TryPlaceComponent(Kind.Sensor, new Cell(0, 2), id: "sensor"), "direct sensor");
        Check(grid.TryPlaceComponent(Kind.Ward, new Cell(1, 2), 3, "ward"), "direct rotated ward");
        Check(grid.TryPlaceComponent(Kind.Source, new Cell(1, 0), id: "source"), "direct layout source");
        foreach (var cell in new[] { new Cell(3, 0), new Cell(3, 1), new Cell(3, 2) }) Check(grid.TryPlaceWire(cell, PortKind.Energy), "direct layout power wire");
        Check(grid.GetConnectionMask(new Cell(0, 2)) == Direction.East, "adjacent module output connects");
        Check(grid.GetConnectionMask(new Cell(1, 2)) == Direction.West, "adjacent module input connects");
        Check(grid.IsCircuitConnected, "direct signal plus wired energy is complete: " + string.Join(";", grid.GetIssues()));
        var power = new Grid();
        Check(power.TryPlaceComponent(Kind.Source, new Cell(0, 2), id: "source"), "direct energy source");
        Check(power.TryPlaceComponent(Kind.Ward, new Cell(2, 2), 1, "ward"), "direct energy rotated ward");
        Check(power.GetConnectionMask(new Cell(1, 2)) == Direction.East, "direct energy output side");
        Check(power.GetConnectionMask(new Cell(2, 2)) == Direction.West, "direct energy input side");
        var outputConflict = new Grid();
        Check(outputConflict.TryPlaceComponent(Kind.Sensor, new Cell(2, 2), 0, "a"), "left opposing sensor");
        Check(outputConflict.TryPlaceComponent(Kind.Sensor, new Cell(3, 2), 2, "b"), "right opposing sensor");
        Check(outputConflict.GetConnectionMask(new Cell(2, 2)) == Direction.None, "two outputs do not directly connect");
    }

    private static void DraftIssues()
    {
        var grid = new Grid();
        Check(!grid.IsCircuitConnected, "empty board is not a complete circuit");
        Check(grid.TryPlaceWire(new Cell(0, 0), PortKind.Signal), "suspended draft wire is legal editing");
        Check(grid.GetIssues().Any(s => s.Contains("断点")), "suspended draft reports breakpoint");
        Check(grid.TryPlaceComponent(Kind.Sensor, new Cell(2, 0), id: "sensor"), "sensor placement");
        Check(grid.TryPlaceComponent(Kind.Counter, new Cell(2, 1), id: "counter"), "counter placement");
        Check(grid.TryPlaceComponent(Kind.Ward, new Cell(0, 2), id: "ward"), "ward placement");
        Check(grid.TryPlaceComponent(Kind.Filter, new Cell(2, 3), id: "filter"), "draft places components without a complexity cap");
        Check(!grid.GetIssues().Any(s => s.Contains("复杂度")), "complexity is not a draft validation rule");
        Check(!grid.IsCircuitConnected, "incomplete wiring remains invalid");
    }

    private static void Persistence()
    {
        var starter = Grid.CreateStarter();
        var original = starter.Serialize();
        Check(Grid.TryDeserialize(original, out var restored) && restored is not null, "starter roundtrip loads");
        Check(restored!.Serialize() == original && restored.IsCircuitConnected, "starter roundtrip preserves identity and circuit");
        var draft = new Grid();
        draft.TryPlaceWire(new Cell(0, 0), PortKind.Signal);
        Check(Grid.TryDeserialize(draft.Serialize(), out var draftRestored) && draftRestored!.Serialize() == draft.Serialize(), "valid incomplete draft roundtrips");
        string Change(Action<JsonObject> mutation)
        {
            var root = JsonNode.Parse(original)!.AsObject();
            mutation(root);
            return root.ToJsonString();
        }
        var invalid = new List<string?>
        {
            null, "", " ", "null", "[]", "{}", "not json", original[..^1],
            Change(r => r["version"] = 3), Change(r => r["size"] = 5),
            Change(r => r["extra"] = true), Change(r => r["wires"] = null),
            Change(r => r["components"] = new JsonArray()),
            Change(r => r["components"]![0]!["x"] = 0),
            Change(r => r["components"]![0]!["rotation"] = 1),
            Change(r => r["components"]![1]!["kind"] = "Invalid"),
            Change(r => r["components"]![1]!["kind"] = "2"),
            Change(r => r["components"]![1]!["rotation"] = -1),
            Change(r => r["components"]![1]!["rotation"] = 4),
            Change(r => r["components"]![1]!["x"] = 4),
            Change(r => r["components"]![1]!["x"] = "0"),
            Change(r => r["components"]![1]!["x"] = 0.5),
            Change(r => r["components"]![1]!["id"] = "eye"),
            Change(r => { r["components"]![1]!["x"] = 1; r["components"]![1]!["y"] = 1; }),
            Change(r => r["wires"]![0]!["kind"] = "Unknown"),
            Change(r => r["wires"]![0]!["x"] = -1),
            Change(r => { r["wires"]![0]!["x"] = 1; r["wires"]![0]!["y"] = 1; }),
            Change(r => r["wires"]!.AsArray().Add(r["wires"]![0]!.DeepClone())),
            Change(r => r["components"]!.AsArray().Add(r["components"]![1]!.DeepClone())),
            original.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal)
        };
        foreach (var json in invalid)
        {
            Grid? result = starter;
            Check(!Grid.TryDeserialize(json, out result), "invalid JSON/schema/geometry must reject: " + json);
            Check(result is null, "invalid persistence must not expose partial restoration");
            Check(starter.Serialize() == original, "deserialization does not mutate another existing grid");
        }
    }

    private static void EditSequences()
    {
        var random = new Random(1108);
        var grid = new Grid();
        for (var i = 0; i < 350; i++)
        {
            var before = grid.Serialize();
            var cell = new Cell(random.Next(-1, 5), random.Next(-1, 5));
            var existing = grid.Components.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var id = existing[random.Next(existing.Length)];
            var success = random.Next(6) switch
            {
                0 => grid.TryPlaceWire(cell, (PortKind)random.Next(2)),
                1 => grid.RemoveWire(cell),
                2 => grid.TryPlaceComponent((Kind)random.Next(1, 9), cell, random.Next(4), "part_" + i),
                3 => grid.TryMoveComponent(id, cell),
                4 => grid.TryRotateComponent(id, random.Next(-4, 5)),
                _ => grid.RemoveComponent(id)
            };
            if (!success) Check(grid.Serialize() == before && grid.LastError.Length > 0, "random failed edit remains atomic");
            else Check(grid.LastError == "", "random accepted edit clears error");
            var serialized = grid.Serialize();
            Check(Grid.TryDeserialize(serialized, out var restored) && restored is not null, "every accepted structural state can be restored");
            Check(restored!.Serialize() == serialized, "deterministic state roundtrip");
            Check(grid.Components.Values.All(component => component.Kind != Kind.Eye), "new random edits never create an eye");
            for (var y = 0; y < Grid.Size; y++)
                for (var x = 0; x < Grid.Size; x++)
                    Check(grid.GetConnectionMask(new Cell(x, y)) == restored.GetConnectionMask(new Cell(x, y)), "roundtrip preserves every adaptive connection mask");
            Check(grid.GetIssues().SequenceEqual(restored.GetIssues()), "roundtrip preserves validity results");
        }
    }
}
