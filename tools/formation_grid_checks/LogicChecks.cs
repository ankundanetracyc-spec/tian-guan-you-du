using System.Text.Json.Nodes;
using Grid = FormationGridModel;
using Logic = FormationLogicCircuit;
using Cell = FormationGridModel.Cell;
using Kind = FormationGridModel.Kind;
using PortKind = FormationGridModel.PortKind;
using NodeKind = FormationLogicCircuit.NodeKind;

internal static class LogicChecks
{
    public static void Run(Action<string, Action> run, Action<bool, string> check)
    {
        run("input and filter rejection do not advance downstream counts", () => InputsAndFilters(check));
        run("unified counter start modes including interval one", () => Phases(check));
        run("event identity and rejected emissions preserve state", () => Identity(check));
        run("unequal split paths and multiple inputs merge once", () => Branches(check));
        run("invalid logic graphs cannot become runnable", () => InvalidGraphs(check));
        run("settings survive edits and legacy/current persistence", () => SettingsPersistence(check));
        run("invalid configuration and settings records reject atomically", () => InvalidSettings(check));
        run("real rotated ports compile all five examples and parallel paths", () => GridCompilation(check));
        run("grid simulation uses saved parameters and isolated trial state", () => Simulation(check));
        CounterMigrationChecks.Run(run, check);
    }

    private static Logic.Node Part(string id, NodeKind kind) => new(id, kind);
    private static Logic.Edge Link(string from, string to) => new(from, to);
    private static Logic.Signal Signal(string id, int amplitude = 1, string tag = "paid", string eventName = "spell_played") =>
        new(id, eventName, tag, amplitude);

    private static Logic Chain(NodeKind kind, int interval = 3, bool firstImmediate = false) => new(new[]
    {
        Part("input", NodeKind.Input), Part("logic", kind) with { Interval = interval, FirstImmediate = firstImmediate }, Part("output", NodeKind.Output)
    }, new[] { Link("input", "logic"), Link("logic", "output") });

    private static void Throws<T>(Action action, Action<bool, string> check, string message) where T : Exception
    {
        var rejected = false;
        try { action(); }
        catch (T) { rejected = true; }
        check(rejected, message);
    }

    private static void InputsAndFilters(Action<bool, string> check)
    {
        var circuit = new Logic(new[]
        {
            Part("input", NodeKind.Input) with { AcceptedSourceTags = new[] { "paid", "other" } },
            Part("filter", NodeKind.Filter) with { MinimumAmplitude = 3, AcceptedSourceTags = new[] { "paid" } },
            Part("counter", NodeKind.Counter) with { Interval = 2 }, Part("output", NodeKind.Output)
        }, new[] { Link("input", "filter"), Link("filter", "counter"), Link("counter", "output") });
        var rejected = new[]
        {
            Signal("wrong-event", 3, eventName: "card_played"), Signal("unknown-source", 3, "unknown"),
            Signal("wrong-filter-source", 3, "other"), Signal("zero", 0), Signal("below-threshold", 2)
        };
        foreach (var signal in rejected)
        {
            var result = circuit.Emit(signal);
            check(result.OutputNodeIds.Count == 0, "unqualified signal cannot reach output: " + signal.EventId);
            check(circuit.GetCount("counter") == 0, "unqualified signal cannot advance counter: " + signal.EventId);
            check(result.Signal == signal, "filter cannot rewrite event identity, source or amplitude");
        }
        check(!circuit.Emit(Signal("below-again", 2)).Steps.Any(s => s.NodeId == "counter"), "blocked filter has no downstream visit");
        check(circuit.Emit(Signal("at-threshold", 3)).OutputNodeIds.Count == 0 && circuit.GetCount("counter") == 1,
            "threshold is inclusive but first accepted occurrence still waits for counter");
        var passed = circuit.Emit(Signal("large", int.MaxValue));
        check(passed.OutputNodeIds.SequenceEqual(new[] { "output" }) && circuit.GetCount("counter") == 0, "second accepted occurrence releases exactly once");
        check(passed.Signal.Amplitude == int.MaxValue, "large amplitudes are neither clamped nor multiplied");

        var direct = new Logic(new[] { Part("input", NodeKind.Input), Part("output", NodeKind.Output) }, new[] { Link("input", "output") });
        var zero = direct.Emit(Signal("direct-zero", 0));
        check(zero.OutputNodeIds.Count == 1 && zero.Signal.Amplitude == 0, "an occurrence carrying zero remains zero; it is never raised to one");

        var tags = new[] { "original" };
        var frozen = new Logic(new[] { Part("input", NodeKind.Input) with { AcceptedSourceTags = tags }, Part("output", NodeKind.Output) },
            new[] { Link("input", "output") });
        tags[0] = "mutated";
        check(frozen.Emit(Signal("frozen-good", tag: "original")).OutputNodeIds.Count == 1, "caller cannot mutate compiled source whitelist");
        check(frozen.Emit(Signal("frozen-bad", tag: "mutated")).OutputNodeIds.Count == 0, "mutated caller list does not gain authorization");
    }

    private static void Phases(Action<bool, string> check)
    {
        foreach (var firstImmediate in new[] { false, true })
        {
            var circuit = Chain(NodeKind.Counter, firstImmediate: firstImmediate);
            var emitted = new List<int>();
            for (var occurrence = 1; occurrence <= 9; occurrence++)
            {
                var result = circuit.Emit(Signal("phase-" + occurrence));
                if (result.OutputNodeIds.Count > 0) emitted.Add(occurrence);
                check(result.OutputNodeIds.Count <= 1, "one occurrence has at most one output request");
            }
            var expected = firstImmediate ? new[] { 1, 4, 7 } : new[] { 3, 6, 9 };
            check(emitted.SequenceEqual(expected), "counter start setting must keep its documented initial phase");
            check(circuit.GetCount("logic") == 0, "complete interval cycles return to initial count/phase");
            var one = Chain(NodeKind.Counter, 1, firstImmediate);
            for (var i = 0; i < 4; i++)
                check(one.Emit(Signal("one-" + i)).OutputNodeIds.Count == 1 && one.GetCount("logic") == 0,
                    "interval one passes every distinct occurrence without accumulating state");
            var boundary = Chain(NodeKind.Counter, 12, firstImmediate);
            var outputs = Enumerable.Range(1, 24).Where(i => boundary.Emit(Signal("boundary-" + i)).OutputNodeIds.Count > 0).ToArray();
            check(outputs.SequenceEqual(firstImmediate ? new[] { 1, 13 } : new[] { 12, 24 }), "largest supported interval preserves phase");
            circuit.Emit(Signal("partial-1"));
            circuit.Emit(Signal("partial-2"));
            check(circuit.GetCount("logic") == 2, "two accepted occurrences retain partial state");
            circuit.Reset();
            var restarted = circuit.Emit(Signal("phase-1"));
            check(!restarted.Duplicate && circuit.GetCount("logic") == 1, "explicit trial reset clears phase and event deduplication");
            check(restarted.OutputNodeIds.Count == (firstImmediate ? 1 : 0), "reset restores the proper initial phase");
        }
    }

    private static void Identity(Action<bool, string> check)
    {
        var circuit = Chain(NodeKind.Counter);
        var first = Signal("same-occurrence", 4);
        circuit.Emit(first);
        var repeated = circuit.Emit(first);
        check(repeated.Duplicate && repeated.Steps.Count == 0 && repeated.OutputNodeIds.Count == 0, "repeated occurrence has no new processing or output");
        check(circuit.GetCount("logic") == 1, "duplicate occurrence does not advance count");
        foreach (var conflicting in new[] { first with { Amplitude = 5 }, first with { EventName = "card_played" }, first with { SourceTag = "other" } })
        {
            Throws<ArgumentException>(() => circuit.Emit(conflicting), check, "same identity cannot be rewritten");
            check(circuit.GetCount("logic") == 1, "conflicting duplicate rejection preserves counter");
            check(circuit.Emit(first).Duplicate, "conflicting duplicate cannot overwrite remembered original");
        }
        var negative = Signal("retry-after-invalid", -1);
        Throws<ArgumentOutOfRangeException>(() => circuit.Emit(negative), check, "negative amplitude is rejected");
        check(circuit.GetCount("logic") == 1, "negative amplitude does not mutate counters");
        var corrected = circuit.Emit(negative with { Amplitude = 1 });
        check(!corrected.Duplicate && circuit.GetCount("logic") == 2, "rejected signal does not reserve its identity");
        check(circuit.Emit(Signal("third-occurrence", 1)).OutputNodeIds.Count == 1, "same event name with new identities counts as new occurrences");
        foreach (var bad in new[]
        {
            Signal(""), Signal("bad\nidentity"), Signal(new string('x', 129)),
            Signal("bad-event", eventName: ""), Signal("bad-source", tag: "bad\nsource")
        })
        {
            Throws<ArgumentException>(() => circuit.Emit(bad), check, "invalid signal metadata is rejected before processing");
            check(circuit.GetCount("logic") == 0, "invalid metadata cannot advance state");
        }
    }

    private static void Branches(Action<bool, string> check)
    {
        var nodes = new[]
        {
            Part("input", NodeKind.Input), Part("outer-split", NodeKind.Split), Part("inner-split", NodeKind.Split),
            Part("short-filter", NodeKind.Filter), Part("long-filter-a", NodeKind.Filter), Part("long-filter-b", NodeKind.Filter),
            Part("inner-merge", NodeKind.Merge), Part("outer-merge", NodeKind.Merge),
            Part("counter", NodeKind.Counter) with { Interval = 2 }, Part("output", NodeKind.Output)
        };
        var edges = new[]
        {
            Link("input", "outer-split"), Link("outer-split", "outer-merge"), Link("outer-split", "inner-split"),
            Link("inner-split", "short-filter"), Link("inner-split", "long-filter-a"), Link("long-filter-a", "long-filter-b"),
            Link("short-filter", "inner-merge"), Link("long-filter-b", "inner-merge"), Link("inner-merge", "outer-merge"),
            Link("outer-merge", "counter"), Link("counter", "output")
        };
        foreach (var orderedEdges in new[] { edges, Enumerable.Reverse(edges).ToArray() })
        {
            var nested = new Logic(nodes, orderedEdges);
            var first = nested.Emit(Signal("nested-first", 7));
            check(nested.GetCount("counter") == 1 && first.OutputNodeIds.Count == 0, "unequal nested paths count one occurrence, regardless of edge ordering");
            check(first.Steps.Select(s => s.NodeId).Distinct().Count() == first.Steps.Count, "convergence processes each node at most once per occurrence");
            check(first.Signal == Signal("nested-first", 7), "nested fanout does not combine amplitudes or change identity");
            var second = nested.Emit(Signal("nested-second", 7));
            check(second.OutputNodeIds.SequenceEqual(new[] { "output" }) && nested.GetCount("counter") == 0, "second independent occurrence yields only one request");
            check(second.Steps.Count(s => s.NodeId == "outer-merge") == 1 && second.Steps.Count(s => s.NodeId == "inner-merge") == 1,
                "both merge stages forward once");
        }

        var parallel = new Logic(new[] { Part("input", NodeKind.Input), Part("split", NodeKind.Split), Part("merge", NodeKind.Merge), Part("output", NodeKind.Output) },
            new[] { Link("input", "split"), Link("split", "merge"), Link("split", "merge"), Link("merge", "output") });
        var merged = parallel.Emit(Signal("parallel"));
        check(merged.OutputNodeIds.Count == 1 && merged.Steps.Count(s => s.NodeId == "merge") == 1, "two physical paths between the same components remain valid and merge once");

        var twoInputs = new Logic(new[]
        {
            Part("input-a", NodeKind.Input), Part("input-b", NodeKind.Input), Part("merge", NodeKind.Merge),
            Part("counter", NodeKind.Counter) with { Interval = 2 }, Part("output", NodeKind.Output)
        }, new[] { Link("input-a", "merge"), Link("input-b", "merge"), Link("merge", "counter"), Link("counter", "output") });
        check(twoInputs.Emit(Signal("two-inputs-one-occurrence")).OutputNodeIds.Count == 0 && twoInputs.GetCount("counter") == 1,
            "multiple sensors observing one event do not multiply downstream counts");
        check(twoInputs.Emit(Signal("two-inputs-next-occurrence")).OutputNodeIds.Count == 1, "next real occurrence is not lost to merge deduplication");

        var selection = new Logic(new[]
        {
            Part("input", NodeKind.Input), Part("split", NodeKind.Split), Part("strict", NodeKind.Filter) with { MinimumAmplitude = 3 },
            Part("loose", NodeKind.Filter), Part("merge", NodeKind.Merge), Part("output", NodeKind.Output)
        }, new[] { Link("input", "split"), Link("split", "strict"), Link("split", "loose"), Link("strict", "merge"), Link("loose", "merge"), Link("merge", "output") });
        check(selection.Emit(Signal("one-branch-passes", 1)).OutputNodeIds.Count == 1, "merge forwards a surviving branch without waiting for the blocked branch");
        check(selection.Emit(Signal("no-branch-passes", 0)).OutputNodeIds.Count == 0, "merge does not manufacture an event when all branches are blocked");

        var twoOutputs = new Logic(new[] { Part("input", NodeKind.Input), Part("split", NodeKind.Split), Part("left", NodeKind.Output), Part("right", NodeKind.Output) },
            new[] { Link("input", "split"), Link("split", "left"), Link("split", "right") });
        check(twoOutputs.Emit(Signal("two-effects")).OutputNodeIds.Order().SequenceEqual(new[] { "left", "right" }), "fanout can request two distinct outputs without duplicating either");
    }

    private static void InvalidGraphs(Action<bool, string> check)
    {
        var validNodes = new[] { Part("input", NodeKind.Input), Part("output", NodeKind.Output) };
        var validEdges = new[] { Link("input", "output") };
        void Reject(IEnumerable<Logic.Node> nodes, IEnumerable<Logic.Edge> edges, string reason)
        {
            check(Logic.Validate(nodes, edges).Count > 0, "validator explains " + reason);
            Throws<ArgumentException>(() => _ = new Logic(nodes, edges), check, "constructor rejects " + reason);
        }
        Reject(Array.Empty<Logic.Node>(), Array.Empty<Logic.Edge>(), "empty circuit");
        Reject(new[] { Part("input", NodeKind.Input), Part("input", NodeKind.Output) }, validEdges, "duplicate node identity");
        Reject(new[] { Part("", NodeKind.Input), Part("output", NodeKind.Output) }, validEdges, "missing node identity");
        Reject(new[] { Part("input", (NodeKind)99), Part("output", NodeKind.Output) }, validEdges, "undefined node kind");
        Reject(validNodes, new[] { Link("input", "missing") }, "missing edge endpoint");
        Reject(validNodes, new[] { Link("input", "output"), Link("output", "input") }, "input/output feedback");
        Reject(new[] { Part("input", NodeKind.Input), Part("a", NodeKind.Output), Part("b", NodeKind.Output) },
            new[] { Link("input", "a"), Link("input", "b") }, "implicit fanout without splitter");
        Reject(new[] { Part("input", NodeKind.Input), Part("split", NodeKind.Split), Part("output", NodeKind.Output) },
            new[] { Link("input", "split"), Link("split", "output") }, "splitter missing its second route");
        Reject(new[] { Part("input", NodeKind.Input), Part("merge", NodeKind.Merge), Part("output", NodeKind.Output) },
            new[] { Link("input", "merge"), Link("merge", "output") }, "merge missing its second input");
        var loopNodes = validNodes.Concat(new[] { Part("loop-a", NodeKind.Filter), Part("loop-b", NodeKind.Filter) });
        Reject(loopNodes, validEdges.Concat(new[] { Link("loop-a", "loop-b"), Link("loop-b", "loop-a") }), "closed loop with otherwise valid degrees");
        foreach (var node in new[]
        {
            Part("bad", NodeKind.Counter) with { Interval = 0 }, Part("bad", NodeKind.Divider) with { Interval = 13 },
            Part("bad", NodeKind.Filter) with { MinimumAmplitude = 0 }, Part("bad", NodeKind.Filter) with { MinimumAmplitude = 10 }
        })
            Reject(new[] { validNodes[0], node, validNodes[1] }, new[] { Link("input", "bad"), Link("bad", "output") }, "invalid node parameter");
        Reject(new[] { validNodes[0] with { EventName = "" }, validNodes[1] }, validEdges, "invalid input event");
        Reject(new[] { validNodes[0] with { FirstImmediate = true }, validNodes[1] }, validEdges, "count mode on non-counter node");
        Reject(new[] { validNodes[0] with { AcceptedSourceTags = new[] { "bad\ntag" } }, validNodes[1] }, validEdges, "invalid source whitelist");
    }

    private static void SettingsPersistence(Action<bool, string> check)
    {
        var legacy = JsonNode.Parse(Grid.CreateStarter().Serialize())!.AsObject();
        foreach (var component in legacy["components"]!.AsArray()) component!.AsObject().Remove("settings");
        check(Grid.TryDeserialize(legacy.ToJsonString(), out var old) && old is not null && old.IsCircuitConnected, "version one without settings still loads");
        check(old!.Components.Values.All(c => c.Settings == new Grid.Settings()), "legacy records receive the documented defaults");

        var filter = Grid.CreateLogicExample(Kind.Filter);
        var filterSettings = new Grid.Settings { MinimumAmplitude = 4, SourceTag = "water" };
        var sensorSettings = new Grid.Settings { InputEvent = "card_played", SourceTag = "water" };
        check(filter.TryConfigureComponent("filter", filterSettings) && filter.TryConfigureComponent("sensor", sensorSettings), "configure filter and sensor independently");
        var serialized = filter.Serialize();
        check(Grid.TryDeserialize(serialized, out var restored) && restored!.Serialize() == serialized, "nondefault parameters roundtrip canonically");
        check(restored!.Components["filter"].Settings == filterSettings && restored.Components["sensor"].Settings == sensorSettings, "all four saved settings retain exact values");
        check(restored.IsCircuitConnected, "settings do not change physical ports or circuit validity");

        var moved = new Grid();
        check(moved.TryPlaceComponent(Kind.Counter, new Cell(2, 1), id: "counter"), "place configurable counter on editable draft");
        var countSettings = new Grid.Settings { Interval = 4 };
        check(moved.TryConfigureComponent("counter", countSettings), "configure count interval");
        check(moved.TryRotateComponent("counter") && moved.TryMoveComponent("counter", new Cell(0, 2)), "rotate and move configured component");
        check(moved.Components["counter"].Settings == countSettings, "geometry edits preserve saved logical parameters");
        check(Grid.TryDeserialize(moved.Serialize(), out var draft) && draft!.Components["counter"].Settings == countSettings, "incomplete configured drafts roundtrip too");
    }

    private static void InvalidSettings(Action<bool, string> check)
    {
        var grid = Grid.CreateLogicExample(Kind.Filter);
        void Refuse(string id, Grid.Settings settings)
        {
            var before = grid.Serialize();
            check(!grid.TryConfigureComponent(id, settings), "bad settings must be refused for " + id);
            check(grid.Serialize() == before && grid.LastError.Length > 0, "bad settings preserve the entire existing draft");
        }
        Refuse("filter", null!);
        Refuse("missing", new Grid.Settings());
        foreach (var settings in new[]
        {
            new Grid.Settings { MinimumAmplitude = 0 }, new Grid.Settings { MinimumAmplitude = 10 },
            new Grid.Settings { SourceTag = null! }, new Grid.Settings { SourceTag = " leading" },
            new Grid.Settings { SourceTag = "trailing " }, new Grid.Settings { SourceTag = "bad\ntag" },
            new Grid.Settings { SourceTag = new string('x', 65) }, new Grid.Settings { Interval = 3 },
            new Grid.Settings { FirstImmediate = true },
            new Grid.Settings { InputEvent = "card_played" }
        }) Refuse("filter", settings);
        Refuse("sensor", new Grid.Settings { InputEvent = "unapproved_event" });
        Refuse("source", new Grid.Settings { SourceTag = "free" });
        Refuse("eye", new Grid.Settings { Interval = 3 });
        var counter = Grid.CreateLogicExample(Kind.Counter);
        var originalCounter = counter.Serialize();
        foreach (var interval in new[] { 0, 13 })
            check(!counter.TryConfigureComponent("logic", new Grid.Settings { Interval = interval }) && counter.Serialize() == originalCounter,
                "invalid interval leaves configured counter untouched");

        var original = grid.Serialize();
        JsonObject Find(JsonObject root) => root["components"]!.AsArray().Select(n => n!.AsObject()).Single(n => n["id"]!.GetValue<string>() == "filter");
        string Change(Action<JsonObject> mutation)
        {
            var root = JsonNode.Parse(original)!.AsObject(); mutation(Find(root)); return root.ToJsonString();
        }
        var invalid = new[]
        {
            Change(c => c["settings"] = null), Change(c => c["settings"] = new JsonArray()),
            Change(c => c["settings"]!["extra"] = true), Change(c => c["settings"]!.AsObject().Remove("source_tag")),
            Change(c => c["settings"]!["interval"] = 0), Change(c => c["settings"]!["interval"] = "2"),
            Change(c => c["settings"]!["minimum_amplitude"] = 1.5), Change(c => c["settings"]!["minimum_amplitude"] = 10),
            Change(c => c["settings"]!["source_tag"] = 1), Change(c => c["settings"]!["source_tag"] = "bad\ntag"),
            Change(c => c["settings"]!["input_event"] = "card_played"),
            original.Replace("\"minimum_amplitude\":2", "\"minimum_amplitude\":2,\"minimum_amplitude\":2", StringComparison.Ordinal)
        };
        foreach (var json in invalid)
        {
            Grid? candidate = grid;
            check(!Grid.TryDeserialize(json, out candidate) && candidate is null, "bad settings JSON cannot expose partial restored model");
            check(grid.Serialize() == original, "bad saved settings do not touch an existing model");
        }
    }

    private static void GridCompilation(Action<bool, string> check)
    {
        foreach (var kind in new[] { Kind.Filter, Kind.Counter, Kind.Rhythm, Kind.Split, Kind.Merge })
        {
            var grid = Grid.CreateLogicExample(kind);
            check(grid.IsCircuitConnected, kind + " example must be a legal complete 4x4 circuit: " + string.Join(";", grid.GetIssues()));
            check(grid.Complexity == (kind == Kind.Filter ? 3 : 4), kind + " example retains its diagnostic complexity score without a cap");
            var links = grid.GetSignalConnections();
            check(links.All(link => link.FromComponentId != "source" && link.ToComponentId != "source"), "energy networks cannot become logical edges");
            check(links.SelectMany(link => link.WireCells).All(cell => grid.Wires[cell] == PortKind.Signal), "compiled routes include only real signal wire cells");
            check(Grid.TryDeserialize(grid.Serialize(), out var restored) && restored!.GetSignalConnections().Count == links.Count, "route count survives persistence");
            _ = new FormationGridSimulation(grid);
        }
        var pair = Grid.CreateLogicExample(Kind.Split);
        var connections = pair.GetSignalConnections();
        check(pair.Components.Values.Sum(c => c.Cells.Count) == 8 && pair.Wires.Count == 4, "paired example has eight component cells and four wires");
        check(connections.Count == 4, "compiled pair retains four physical links, including both split-to-merge paths");
        var routes = connections.Where(link => link.FromComponentId == "split" && link.ToComponentId == "merge").ToArray();
        check(routes.Length == 2 && routes.Select(link => link.WireCells.Count).Order().SequenceEqual(new[] { 0, 2 }), "direct and two-wire parallel paths are not collapsed");
        check(routes.Single(link => link.WireCells.Count == 2).WireCells.ToHashSet().SetEquals(new[] { new Cell(2, 1), new Cell(3, 1) }), "long path follows the actual rotated merge north port");
        var result = new FormationGridSimulation(pair).Send("spell_played", "paid", 5);
        check(result.OutputNodeIds.SequenceEqual(new[] { "ward" }) && result.Signal.Amplitude == 5, "real two-route grid requests ward once without increasing amplitude");
        check(result.Steps.Count(s => s.NodeId == "merge") == 1, "runtime deduplication happens at merge, not by deleting a physical route");

        check(pair.RemoveWire(new Cell(2, 1)), "remove just the longer branch wire");
        check(!pair.IsCircuitConnected, "one surviving route does not make a two-port component fully connected");
        Throws<ArgumentException>(() => _ = new FormationGridSimulation(pair), check, "unconnected branch prevents simulation compilation");
        check(pair.TryPlaceWire(new Cell(2, 1), PortKind.Signal) && pair.IsCircuitConnected, "restoring the branch recompiles connectivity");
        check(pair.RemoveWire(new Cell(0, 3)), "remove source-to-ward energy link");
        check(pair.GetSignalConnections().Count == 4 && !pair.IsCircuitConnected, "valid signal geometry cannot replace missing energy connection");
        Throws<ArgumentException>(() => _ = new FormationGridSimulation(pair), check, "unpowered ward cannot compile as complete trial circuit");

        var rotated = Grid.CreateLogicExample(Kind.Counter);
        check(rotated.Components["logic"].Rotation == 3 && rotated.Components["sensor"].Rotation == 3 && rotated.Components["source"].Rotation == 2,
            "counter example compiles genuinely rotated components");
        check(rotated.GetSignalConnections().Any(link => link.FromComponentId == "sensor" && link.ToComponentId == "logic" && link.WireCells.Count == 0),
            "opposed sensor/counter ports connect directly after rotation");
        check(rotated.GetSignalConnections().Single(link => link.FromComponentId == "logic").WireCells.ToHashSet().SetEquals(new[] { new Cell(0, 0), new Cell(1, 0), new Cell(2, 0) }),
            "counter-to-ward route uses all three true top-row wires");
        check(rotated.TryRotateComponent("sensor"), "rotate sensor away from input while keeping footprint valid");
        check(!rotated.GetSignalConnections().Any(link => link.FromComponentId == "sensor"), "wrong-facing adjacent port cannot compile a hidden connection");
        Throws<ArgumentException>(() => _ = new FormationGridSimulation(rotated), check, "wrong-facing input prevents trial compilation");
    }

    private static void Simulation(Action<bool, string> check)
    {
        foreach (var firstImmediate in new[] { false, true })
        {
            var model = Grid.CreateLogicExample(Kind.Counter, firstImmediate);
            var before = model.Serialize();
            var simulation = new FormationGridSimulation(model);
            Throws<InvalidOperationException>(() => simulation.RepeatLast(), check, "repeat without an occurrence is refused");
            check(simulation.DescribeState("logic").Length > 0 && simulation.DescribeState("missing") == "", "trial exposes state only for known stateful components");
            check(simulation.Send("card_played", "paid", 1).OutputNodeIds.Count == 0, "wrong event cannot enter configured sensor");
            var observed = new List<int>();
            for (var i = 1; i <= 6; i++)
            {
                var result = simulation.Send("spell_played", "paid", 1);
                if (result.OutputNodeIds.Count > 0) observed.Add(i);
                var state = simulation.DescribeState("logic");
                check(simulation.RepeatLast().Duplicate && simulation.DescribeState("logic") == state, "trial repeated occurrence preserves count/phase");
            }
            check(observed.SequenceEqual(firstImmediate ? new[] { 1, 4 } : new[] { 3, 6 }), "saved counter mode determines actual output phase");
            check(model.Serialize() == before, "trial state and sent occurrences never rewrite saved draft parameters");
            simulation.Reset();
            Throws<InvalidOperationException>(() => simulation.RepeatLast(), check, "reset removes last occurrence too");
            check(simulation.Send("spell_played", "paid", 1).OutputNodeIds.Count == (firstImmediate ? 1 : 0), "reset restores compiled initial phase");
        }
        var filter = Grid.CreateLogicExample(Kind.Filter);
        check(filter.TryConfigureComponent("filter", new Grid.Settings { MinimumAmplitude = 3, SourceTag = "water" }) &&
            filter.TryConfigureComponent("sensor", new Grid.Settings { InputEvent = "card_played" }), "configure physical sensor and filter for an alternate legal event");
        check(Grid.TryDeserialize(filter.Serialize(), out var restored), "load configured filter draft");
        var filtered = new FormationGridSimulation(restored!);
        check(filtered.Send("spell_played", "water", 3).OutputNodeIds.Count == 0, "saved sensor event is enforced by compiler");
        check(filtered.Send("card_played", "fire", 3).OutputNodeIds.Count == 0, "saved source restriction is enforced by compiler");
        check(filtered.Send("card_played", "water", 2).OutputNodeIds.Count == 0, "saved amplitude threshold blocks weak occurrence");
        check(filtered.Send("card_played", "water", 3).OutputNodeIds.SequenceEqual(new[] { "ward" }), "saved inclusive threshold permits qualified occurrence");

        var editable = Grid.CreateLogicExample(Kind.Counter);
        var snapshot = new FormationGridSimulation(editable);
        check(editable.TryConfigureComponent("logic", new Grid.Settings { Interval = 1 }), "edit interval after compiling old trial");
        check(snapshot.Send("spell_played", "paid", 1).OutputNodeIds.Count == 0, "compiled trial retains its original parameters");
        check(new FormationGridSimulation(editable).Send("spell_played", "paid", 1).OutputNodeIds.Count == 1, "newly compiled trial reads the updated settings");
    }
}
