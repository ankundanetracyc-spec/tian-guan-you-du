using System.Text.Json;
using System.Text.Json.Nodes;
using G = FormationGridModel;
using C = FormationControlCircuit;
using R = FormationGuardAssaultRuntime;
using K = FormationGridModel.Kind;
using Cell = FormationGridModel.Cell;
using P = FormationGridModel.PortKind;

internal static class Program
{
    private static int _checks;
    private static readonly List<object> Groups = [];
    private static readonly List<string> CheckMessages = [];
    private static int Main()
    {
        try
        {
            Run("4/5 resize preserves or atomically refuses the actual draft", Resize);
            Run("fifth row/column footprints, named ports and single-layer geometry", Geometry);
            Run("V1 stays readable and V2 roundtrips typed parameters", Persistence);
            Run("bad V2 settings never expose a partially restored draft", InvalidSavedParameters);
            Run("5x5 physical connections compile with no hidden branch or source", PhysicalExample);
            Run("same red ink cannot disguise wrong signal types or units", TypedMistakes);
            Run("broken control/power and wrong-side ports cannot run a fallback", BrokenDrafts);
            Run("all IDs and node thresholds, rather than fixture IDs/settings, govern effects", CurrentGraph);
            Run("legacy Ward remains an event output with its own trial profile", LegacyWard);
            Run("compiled count modes filter and preserve identities", Counters);
            Run("two physical split/merge paths produce one actual request", ParallelPaths);
            Run("multiple fire outputs share a wave peak, energy and event identity", MultipleFire);
            Run("multiple guards own separate sources and segment quotas", MultipleGuard);
            Run("unknown observations only block their dependent branch", DependencyLocalUnknown);
            Run("expired guards leave the shield observation before renewal", ExpiredObservation);
            Run("trial instances clone circuit state and dispose only their own guard", TrialIsolation);
            Report(true, null);
            Console.WriteLine($"FORMATION_FREEBUILD_CORE_PASS groups={Groups.Count} checks={_checks}");
            return 0;
        }
        catch (Exception error)
        {
            Report(false, error.ToString()); Console.Error.WriteLine(error); return 1;
        }
    }
    private static void Run(string name, Action action)
    {
        var before = _checks; action(); Groups.Add(new { name, passed = true, checks = _checks - before }); Console.WriteLine("PASS " + name);
    }
    private static void Check(bool passed, string message)
    {
        _checks++; if (!passed) throw new InvalidOperationException(message); CheckMessages.Add(message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { Check(true, message); return; }
        throw new InvalidOperationException(message + " did not throw " + typeof(T).Name);
    }
    private static void Refused(G grid, Func<bool> change, string message)
    {
        var before = grid.Serialize(); Check(!change() && grid.Serialize() == before && grid.LastError.Length > 0, message);
    }
    private static G Clone(G grid)
    {
        if (!G.TryDeserialize(grid.Serialize(), out var copy) || copy is null) throw new Exception("fixture clone failed"); return copy;
    }
    private static R.Settings Fixture() => new(12, 12, 3, 6, 6, 6, 2, 18, 2, 3, 9, 3, 1, 30, 60);
    private sealed class Actor : IFormationGuardActor
    {
        public int Health { get; set; } = 80;
        public int MaxHealth { get; set; } = 100;
        public int Mana { get; set; } = 20;
        public bool IsAlive => Health > 0;
        public FormationShieldLedger Shields { get; } = new();
        public int EnemyHealth { get; private set; } = 100;
        public bool TryAttack(int amount)
        {
            if (!IsAlive || EnemyHealth <= 0) return false; EnemyHealth = Math.Max(0, EnemyHealth - amount); return true;
        }
    }
    private static R.Input Signal(string id, string name = "spell_played", string tag = "spell", int amplitude = 1) =>
        new(id, R.ActionKind.Signal, EventName: name, SourceTag: tag, Amplitude: amplitude);
    private static R.Result Apply(R runtime, string id, R.ActionKind kind, int value = 0, bool flag = true) => runtime.Apply(new(id, kind, value, flag));

    private static void Resize()
    {
        var grid = G.CreateStarter(); var before = grid.Serialize();
        Check(grid.BoardSize == 4 && grid.BoardComplexityLimit == 4 && grid.FixedEyeCell == new Cell(1, 1), "legacy defaults remain exactly 4x4");
        Check(grid.TryResize(5) && grid.BoardSize == 5 && grid.FixedEyeCell == new Cell(2, 2) && grid.BoardComplexityLimit == 10, "resize moves only the fixed eye and selects the extension limit");
        Check(grid.IsCircuitConnected && grid.Components["sensor"].Origin == new Cell(0, 0) && grid.Wires.Count == 5, "enlargement does not discard the existing circuit");
        Check(grid.TryResize(4) && grid.Serialize() == before, "safe roundtrip returns exact old bytes");
        var occupied = new G(); Check(occupied.TryPlaceComponent(K.Filter, new(2, 2), id: "new-eye-obstacle"), "prepare future eye conflict");
        Refused(occupied, () => occupied.TryResize(5), "new fixed eye must not overwrite a component");
        var wire = new G(); wire.TryPlaceWire(new(2, 2), P.Signal); Refused(wire, () => wire.TryResize(5), "new fixed eye must not overwrite a wire");
        var high = new G(5);
        foreach (var cell in new[] { new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(3, 0), new Cell(0, 1) })
            Check(high.TryPlaceComponent(K.Filter, cell), "prepare wholly in-bounds old-grid overcapacity");
        Refused(high, () => high.TryResize(4), "shrinking also enforces the smaller complexity capacity");
        var example = G.CreateGuardAssaultExample(); Refused(example, () => example.TryResize(4), "complex five-grid draft does not silently lose its outer edge");
        Refused(example, () => example.TryResize(6), "unsupported larger boards are not accidentally enabled");
    }

    private static void Geometry()
    {
        var grid = new G(5);
        Check(grid.TryPlaceComponent(K.Guard, new(4, 3), id: "edge"), "whole vertical guard fits in fifth column");
        Check(grid.GetComponentAt(new(4, 4))?.Id == "edge", "fifth row maps to the same real component");
        Refused(grid, () => grid.TryRotateComponent("edge"), "rotation cannot extend a dual cell beyond column five");
        Refused(grid, () => grid.TryMoveComponent("edge", new(4, 4)), "movement refuses a partially clipped footprint");
        Refused(grid, () => grid.TryPlaceWire(new(5, 4), P.Signal), "sixth-column wire remains invalid");
        Refused(grid, () => grid.RemoveComponent("eye"), "five-grid eye remains immutable");
        foreach (var kind in new[] { K.StateInput, K.Compare, K.Not, K.Switch, K.Fire, K.Guard })
        {
            var cycle = new G(5); Check(cycle.TryPlaceComponent(kind, new(0, 0), id: "part"), "place a typed component " + kind);
            var original = cycle.Serialize(); var names = cycle.Components["part"].Ports.Select(p => p.Name).Order().ToArray();
            for (var i = 0; i < 4; i++)
            {
                Check(cycle.TryRotateComponent("part"), "rotate typed component " + kind);
                var part = cycle.Components["part"];
                Check(part.Ports.All(p => part.Cells.Contains(p.Cell)) && part.Ports.Select(p => p.Name).Order().SequenceEqual(names), "port names and occupied cells stay together " + kind);
            }
            Check(cycle.Serialize() == original, "four typed rotations preserve exact parameters " + kind);
        }
        foreach (var kind in new[] { P.Signal, P.Energy })
        {
            var cross = new G(5);
            foreach (var cell in new[] { new Cell(0, 1), new Cell(1, 1), new Cell(2, 1) }) Check(cross.TryPlaceWire(cell, kind), "prepare single-layer line");
            if (kind == P.Signal) Refused(cross, () => cross.TryPlaceWire(new(1, 0), kind), "signal cannot acquire a free T split");
            else { Check(cross.TryPlaceWire(new(1, 0), kind), "power may use a real T"); Refused(cross, () => cross.TryPlaceWire(new(1, 2), kind), "power still cannot cross four ways"); }
        }
    }

    private static void Persistence()
    {
        var old = G.CreateStarter(); var legacy = JsonNode.Parse(old.Serialize())!.AsObject();
        Check(legacy["version"]!.GetValue<int>() == 1, "pure legacy draft still writes its compatible version");
        foreach (var component in legacy["components"]!.AsArray()) component!.AsObject().Remove("settings");
        Check(G.TryDeserialize(legacy.ToJsonString(), out var restored) && restored!.BoardSize == 4 && restored.IsCircuitConnected, "old records without settings still load");
        var rhythm = JsonNode.Parse(G.CreateLogicExample(K.Counter).Serialize())!.AsObject();
        var count = rhythm["components"]!.AsArray().Single(n => n!["kind"]!.GetValue<string>() == "Counter")!;
        count["kind"] = "Rhythm"; count["settings"]!.AsObject().Remove("first_immediate");
        Check(G.TryDeserialize(rhythm.ToJsonString(), out var alias) && alias!.Components["logic"].Kind == K.Counter && alias.Components["logic"].Settings.FirstImmediate && alias.Components["logic"].Settings.Interval == 3, "V1 rhythm alias preserves N and its phase");
        var five = G.CreateGuardAssaultExample();
        Check(five.TryConfigureComponent("compare", five.Components["compare"].Settings with { Low = 25, High = 65 }), "set saved custom thresholds");
        Check(five.TryConfigureComponent("switch", five.Components["switch"].Settings with { NormallyClosed = true }), "set saved switch polarity");
        var text = five.Serialize(); Check(JsonNode.Parse(text)!["version"]!.GetValue<int>() == 2, "typed five-grid records use V2");
        Check(G.TryDeserialize(text, out var saved) && saved!.Serialize() == text && saved.BoardSize == 5 && saved.FixedEyeCell == new Cell(2, 2), "all typed geometry and parameters roundtrip exactly");
        Check(saved!.Components["compare"].Settings.Low == 25 && saved.Components["switch"].Settings.NormallyClosed && saved.Components["branch"].Settings.SignalType == FormationSignalType.Switch,
            "threshold, polarity and splitter type are stored on their real components");
    }

    private static void InvalidSavedParameters()
    {
        var model = G.CreateGuardAssaultExample(); model.TryConfigureComponent("compare", model.Components["compare"].Settings with { Low = 25, High = 65 });
        var original = model.Serialize();
        string Change(Action<JsonObject> mutate)
        {
            var root = JsonNode.Parse(original)!.AsObject(); mutate(root); return root.ToJsonString();
        }
        static JsonObject Settings(JsonObject root) => root["components"]!.AsArray().Single(n => n!["id"]!.GetValue<string>() == "compare")!["settings"]!.AsObject();
        var invalid = new[] {
            Change(r => r["version"] = 1), Change(r => r["size"] = 6), Change(r => Settings(r)["low"] = "25"),
            Change(r => Settings(r)["low"] = null), Change(r => Settings(r)["high"] = 20),
            Change(r => Settings(r)["high"] = 101), Change(r => Settings(r)["signal_type"] = "Event"),
            Change(r => Settings(r)["unit"] = "None"), Change(r => Settings(r)["unit"] = "2"),
            Change(r => Settings(r)["normally_closed"] = true), Change(r => Settings(r).Remove("high")),
            Change(r => Settings(r)["hidden_port"] = true), original.Replace("\"low\":25", "\"low\":25,\"low\":25", StringComparison.Ordinal)
        };
        foreach (var text in invalid)
        {
            Check(text != original, "malformed fixture is a real mutation");
            Check(!G.TryDeserialize(text, out var candidate) && candidate is null && model.Serialize() == original, "malformed V2 never exposes partial state or changes an existing model");
        }
        Refused(model, () => model.TryConfigureComponent("compare", model.Components["compare"].Settings with { High = double.NaN }), "nonfinite editor parameters refuse atomically");
        Refused(model, () => model.TryConfigureComponent("health", G.DefaultSettingsFor(K.StateInput) with { StateKey = "shield" }), "state source cannot carry the wrong unit");
    }

    private static void PhysicalExample()
    {
        var model = G.CreateGuardAssaultExample(); var compilation = FormationGridControlCompiler.Compile(model);
        Check(model.BoardSize == 5 && model.Components.Values.Sum(c => c.Cells.Count) == 14 && model.Wires.Count == 10 && model.Complexity == 9 && model.BoardComplexityLimit == 10, "real example accounts for every one of its 25 cells");
        Check(model.GetIssues().Count == 0 && compilation.Circuit.Nodes.Count == 8 && compilation.Circuit.Links.Count == 7, "complete physical layout compiles exactly its eight functional nodes and seven links");
        Check(compilation.SourcePresent && compilation.SourceId == "source" && compilation.PoweredOutputIds.Order().SequenceEqual(new[] { "attack", "guard" }), "both real outputs are reached by the one physical source");
        Check(model.GetPowerSources().Values.All(id => id == "source"), "power T does not create another source");
        foreach (var connection in compilation.PhysicalConnections)
        {
            var from = model.Components[connection.FromComponentId].Ports.Single(p => !p.IsInput && p.Name == connection.FromPortName);
            var to = model.Components[connection.ToComponentId].Ports.Single(p => p.IsInput && p.Name == connection.ToPortName);
            Check(!from.IsInput && to.IsInput && from.SignalType == to.SignalType && from.Unit == to.Unit && from.SignalType == connection.SignalType && from.Unit == connection.Unit, "compiled link preserves named physical type and unit");
            Check(connection.WireCells.All(cell => model.Wires.TryGetValue(cell, out var kind) && kind == P.Signal), "compiled path only consumes actually occupied signal cells");
        }
        Check(compilation.Circuit.Nodes["cast"].Key == "spell_played" && compilation.Circuit.Nodes["guard"].Kind == C.NodeKind.GuardOutput && compilation.Circuit.Nodes["attack"].Kind == C.NodeKind.AttackOutput,
            "sample does not rewrite event names or confuse transient and continuous outputs");
    }

    private static void RejectCompilation(G model, string reason)
    {
        Check(!FormationGridControlCompiler.TryCompile(model, out var failed, out var issues) && failed is null && issues.Count > 0, reason);
        var actor = new Actor { Health = 20 };
        Throws<ArgumentException>(() => new FormationEditableTrial(model, actor, Fixture()), "invalid physical draft cannot fall back to another circuit");
        Check(actor.Shields.Total == 0 && actor.EnemyHealth == 100, "rejected draft has no world effect");
    }
    private static void TypedMistakes()
    {
        var number = G.CreateGuardAssaultExample();
        Check(number.TryConfigureComponent("compare", number.Components["compare"].Settings with { Unit = FormationValueUnit.Energy }), "a structurally editable unit mismatch remains a draft");
        Check(number.GetIssues().Any(i => i.Contains("单位", StringComparison.Ordinal)), "unit mismatch is explained"); RejectCompilation(number, "percent does not wire to energy through red ink");
        var kind = G.CreateGuardAssaultExample();
        Check(kind.TryConfigureComponent("branch", new G.Settings { SignalType = FormationSignalType.Event }), "prepare physical event/switch mismatch");
        RejectCompilation(kind, "event split is not silently converted to a switch split");
        var wrongSide = G.CreateGuardAssaultExample(); Check(wrongSide.TryRotateComponent("invert", 2), "swap the actual input and output sides");
        RejectCompilation(wrongSide, "reversed direction does not receive a magical hidden connection");
    }
    private static void BrokenDrafts()
    {
        foreach (var kind in new[] { P.Signal, P.Energy })
        {
            var model = G.CreateGuardAssaultExample(); var cell = model.Wires.First(pair => pair.Value == kind).Key;
            Check(model.RemoveWire(cell), "remove a real critical " + kind + " cell"); RejectCompilation(model, "a missing physical path refuses the whole trial");
        }
        var source = G.CreateGuardAssaultExample(); Check(source.TryRotateComponent("source", 2), "turn source away from its power path");
        RejectCompilation(source, "wrong source side cannot power the old graph");
        var single = new G(5); Check(single.TryPlaceComponent(K.Source, new(0, 0)), "place one true source");
        Refused(single, () => single.TryPlaceComponent(K.Source, new(3, 4)), "multiple sources are not merged");
    }

    private static void CurrentGraph()
    {
        var root = JsonNode.Parse(G.CreateGuardAssaultExample().Serialize())!.AsObject();
        foreach (var node in root["components"]!.AsArray()) if (node!["id"]!.GetValue<string>() != "eye") node["id"] = "custom_" + node["id"]!.GetValue<string>();
        Check(G.TryDeserialize(root.ToJsonString(), out var renamed), "arbitrary stable IDs load");
        var actor = new Actor { Health = 20 }; var deliberatelyDifferent = Fixture() with { LowHealth = 70, HighHealth = 90 };
        using var trial = new FormationEditableTrial(renamed!, actor, deliberatelyDifferent);
        Check(trial.OutputStates["custom_guard"].Running && actor.Shields.Total == 18 && trial.GetSnapshot().EnergySpent == 2, "renamed guard starts and pays by the injected graph");
        trial.Apply(new("up", R.ActionKind.SetHealth, 80));
        trial.Apply(Signal("attack"));
        Check(trial.OutputStates["custom_attack"].Executions == 1 && actor.EnemyHealth == 91 && actor.Shields.Total == 0, "80 follows node High60 rather than fixture High90 or fixed node IDs");
        var changed = G.CreateGuardAssaultExample();
        Check(changed.TryConfigureComponent("compare", changed.Components["compare"].Settings with { Low = 5, High = 15 }), "edit actual comparison thresholds");
        var second = new Actor { Health = 20 }; using var different = new FormationEditableTrial(changed, second, deliberatelyDifferent);
        different.Apply(Signal("same-health"));
        Check(!different.GetSnapshot().GuardRunning && second.Shields.Total == 0 && second.EnemyHealth == 91, "same 20 health produces attack after changing only the real component thresholds");
    }

    private static void LegacyWard()
    {
        var actor = new Actor(); using var trial = new FormationEditableTrial(G.CreateStarter(), actor, Fixture());
        Check(trial.Runtime.Circuit.Nodes["ward"].Kind == C.NodeKind.ShieldOutput, "legacy Ward maps to transient shield, not Guard");
        trial.Apply(new("completed", R.ActionKind.Spell)); trial.Apply(Signal("zero", amplitude: 0));
        Check(actor.Shields.Total == 0 && trial.GetSnapshot().SourceEnergy == 12, "completed and played remain distinct and zero amplitude is not raised to one");
        var input = Signal("played"); trial.Apply(input); var result = trial.Apply(input);
        Check(result.Duplicate && actor.Shields.Total == 2 && trial.GetSnapshot().EnergySpent == 2 && !trial.GetSnapshot().GuardRunning, "actual old Ward pays two for two ordinary shields once, without creating a continuous service");
        trial.Apply(Signal("again")); Check(actor.Shields.Total == 2 && trial.OutputStates["ward"].Executions == 1, "old Ward round quota is not minted by new event IDs");
        trial.Apply(new("next", R.ActionKind.NextRound)); trial.Apply(Signal("next-played"));
        Check(actor.Shields.Total == 4 && trial.OutputStates["ward"].Executions == 2, "new real round permits the next old Ward service");
        var before = trial.GetSnapshot(); Throws<ArgumentException>(() => trial.Apply(input with { Amplitude = 2 }), "same input identity cannot change amplitude");
        Throws<ArgumentException>(() => trial.Apply(Signal("negative", amplitude: -1)), "negative input rejects before commit");
        Check(trial.GetSnapshot() == before, "rejected identity and amplitude leave world and account unchanged");
    }

    private static void Counters()
    {
        foreach (var first in new[] { false, true })
        {
            var model = G.CreateLogicExample(K.Counter, first); model.TryConfigureComponent("sensor", new G.Settings { SourceTag = "spell" });
            var actor = new Actor(); using var trial = new FormationEditableTrial(model, actor, Fixture());
            trial.Apply(Signal("wrong-tag", tag: "water")); trial.Apply(Signal("zero", amplitude: 0));
            Check(trial.Runtime.Circuit.CounterPhases.GetValueOrDefault("logic") == 0, "rejected upstream input never advances the compiled counter");
            for (var i = 1; i <= 6; i++)
            {
                var input = Signal("pulse-" + i); trial.Apply(input);
                Check(trial.Runtime.CurrentEvaluation.PulseRequests.Count == (first ? i % 3 == 1 ? 1 : 0 : i % 3 == 0 ? 1 : 0), "compiled counter preserves its chosen phase " + first + "/" + i);
                Check(trial.Runtime.Circuit.CounterPhases["logic"] == i % 3, "current phase is retained on the real circuit");
                var spent = trial.GetSnapshot().EnergySpent; Check(trial.Apply(input).Duplicate && trial.GetSnapshot().EnergySpent == spent, "duplicate occurrence does not count or spend twice");
                if (i == 3) trial.Apply(new("round", R.ActionKind.NextRound));
            }
            Check(actor.Shields.Total == 4 && trial.OutputStates["ward"].Executions == 2, "actual service follows both periods without resetting counter at round boundary");
        }
    }
    private static void ParallelPaths()
    {
        var model = G.CreateLogicExample(K.Split); var compilation = FormationGridControlCompiler.Compile(model);
        Check(compilation.PhysicalConnections.Count(c => c.FromComponentId == "split" && c.ToComponentId == "merge") == 2, "two real split-to-merge paths remain distinct during compilation");
        var actor = new Actor(); using var trial = new FormationEditableTrial(compilation, actor, Fixture());
        trial.Apply(Signal("both", amplitude: 3)); var value = trial.Runtime.CurrentEvaluation.Values["merge"];
        Check(trial.Runtime.CurrentEvaluation.PulseRequests.Count == 1 && actor.Shields.Total == 2 && trial.GetSnapshot().EnergySpent == 2, "reconverging paths cannot duplicate a fee or effect");
        Check(value.EventId == "both" && value.Amplitude == 3 && value.SourceTag == "spell", "identity, source and amplitude survive the physical branches");
    }

    private static C TwoFire() => new(new C.Node[] {
        new("event", C.NodeKind.EventInput, "输入") { Key = "spell_played" }, new("split", C.NodeKind.Split, "分流") { SignalType = FormationSignalType.Event },
        new("fire-a", C.NodeKind.AttackOutput, "甲"), new("fire-b", C.NodeKind.AttackOutput, "乙") },
        new C.Link[] { new("event", "event", "split", "value"), new("split", "a", "fire-a", "event"), new("split", "b", "fire-b", "event") });
    private static void MultipleFire()
    {
        var actor = new Actor(); using var enough = new R(Fixture(), actor, TwoFire(), new[] { "fire-a", "fire-b" });
        var input = Signal("both"); enough.Apply(input);
        Check(enough.GetSnapshot().Attacks == 2 && enough.GetSnapshot().SourceEnergy == 6 && actor.EnemyHealth == 82 && enough.OutputStates.Values.All(s => s.UsedThisRound == 1), "each real fire output pays and executes once under the shared six peak");
        Check(enough.Apply(input).Duplicate && enough.GetSnapshot().SourceEnergy == 6, "one event cannot replay both outputs");
        var other = new Actor(); using var tight = new R(Fixture() with { SourcePeak = 5 }, other, TwoFire(), new[] { "fire-a", "fire-b" });
        tight.Apply(Signal("limited"));
        Check(tight.GetSnapshot().Attacks == 1 && tight.GetSnapshot().SourceEnergy == 9 && tight.OutputStates.Values.Sum(s => s.UsedThisRound) == 1,
            "serial output iteration cannot reuse one five-peak wave");
        Apply(tight, "budget", R.ActionKind.Energy, 1);
        Check(tight.GetSnapshot().Attacks == 1, "later energy availability never replays the blocked output of the old event");
        tight.Apply(Signal("new-occurrence")); Check(tight.GetSnapshot().Attacks == 2, "failed shared-peak output retained its quota for a genuinely new event");
        using var partial = new R(Fixture(), new Actor(), TwoFire(), new[] { "fire-b" }); partial.Apply(Signal("one-powered"));
        Check(partial.OutputStates["fire-a"].Executions == 0 && partial.OutputStates["fire-b"].Executions == 1, "logic truth cannot bypass an output's actual power reachability");
    }
    private static C TwoGuard() => new(new C.Node[] {
        new("life", C.NodeKind.StateInput, "生命") { Key = "health", Unit = FormationValueUnit.Percent },
        new("band", C.NodeKind.Compare, "低生命") { Unit = FormationValueUnit.Percent, Comparison = C.CompareMode.AtMost, Low = 30 },
        new("split", C.NodeKind.Split, "分流") { SignalType = FormationSignalType.Switch },
        new("guard-a", C.NodeKind.GuardOutput, "甲罩"), new("guard-b", C.NodeKind.GuardOutput, "乙罩") },
        new C.Link[] { new("life", "value", "band", "value"), new("band", "state", "split", "value"), new("split", "a", "guard-a", "permit"), new("split", "b", "guard-b", "permit") });
    private static void MultipleGuard()
    {
        var actor = new Actor { Health = 20 }; using var runtime = new R(Fixture(), actor, TwoGuard(), new[] { "guard-a", "guard-b" });
        Check(runtime.GetSnapshot().SourceEnergy == 8 && actor.Shields.Total == 36 && runtime.OutputStates.Values.Select(s => s.SourceId).Distinct().Count() == 2,
            "two guards pay individually and own independent sources");
        Apply(runtime, "damage", R.ActionKind.Damage, 4);
        Check(runtime.OutputStates.Values.Select(s => s.Remaining).Order().SequenceEqual(new[] { 14, 18 }), "FIFO damage tracks the actual remaining amount of each source");
        Apply(runtime, "ordinary", R.ActionKind.OrdinaryShield, 7); Apply(runtime, "off", R.ActionKind.Enable, flag: false);
        Check(actor.Shields.Total == 7 && runtime.GetSnapshot().SourceEnergy == 8, "stopping removes only both named guards and refunds no energy");
        Apply(runtime, "on", R.ActionKind.Enable);
        Check(actor.Shields.Total == 7 && runtime.OutputStates.Values.All(s => s.PaidThisRound), "same-round re-enable cannot refresh either source quota");
        Apply(runtime, "next", R.ActionKind.NextRound); Check(actor.Shields.Total == 43 && runtime.GetSnapshot().GuardPayments == 4, "real new round separately renews both services");
        var notices = new List<(int, int)>(); actor.Shields.Changed += (old, current) => notices.Add((old, current));
        Apply(runtime, "equal-renew", R.ActionKind.NextRound);
        Check(notices.SequenceEqual(new[] { (43, 43) }) && runtime.GetSnapshot().GuardPayments == 6, "two equal renewals commit one final notification and still pay");
        var low = new Actor { Health = 20 }; using var constrained = new R(Fixture() with { SourcePeak = 3 }, low, TwoGuard(), new[] { "guard-a", "guard-b" });
        Apply(constrained, "refresh", R.ActionKind.Refresh);
        Check(constrained.GetSnapshot().GuardPayments == 1 && low.Shields.Total == 18, "existing maintenance load prevents serial startup of an extra guard");
    }

    private static void DependencyLocalUnknown()
    {
        var graph = new C(new C.Node[] {
            new("state", C.NodeKind.StateInput, "生命") { Key = "health", Unit = FormationValueUnit.Percent },
            new("test", C.NodeKind.Compare, "条件") { Unit = FormationValueUnit.Percent, Comparison = C.CompareMode.AtMost, Low = 30 },
            new("guard", C.NodeKind.GuardOutput, "护罩"), new("event", C.NodeKind.EventInput, "独立事件") { Key = "spell_played" },
            new("fire", C.NodeKind.AttackOutput, "独立火击") }, new C.Link[] {
            new("state", "value", "test", "value"), new("test", "state", "guard", "permit"), new("event", "event", "fire", "event") });
        using var runtime = new R(Fixture(), new Actor(), graph, new[] { "guard", "fire" });
        Apply(runtime, "unknown", R.ActionKind.Observation, flag: false); runtime.Apply(Signal("independent"));
        Check(runtime.CurrentEvaluation.GuardRequests["guard"] == FormationTruth.Unknown && runtime.GetSnapshot().Attacks == 1,
            "unknown life blocks its guard condition but does not invent a life gate on an unrelated fire");
    }
    private static void ExpiredObservation()
    {
        var graph = new C(new C.Node[] {
            new("shield", C.NodeKind.StateInput, "公共盾") { Key = "shield", Unit = FormationValueUnit.Shield },
            new("empty", C.NodeKind.Compare, "缺盾") { Unit = FormationValueUnit.Shield, Comparison = C.CompareMode.AtMost, Low = 0 },
            new("guard", C.NodeKind.GuardOutput, "护罩") },
            new C.Link[] { new("shield", "value", "empty", "value"), new("empty", "state", "guard", "permit") });
        var actor = new Actor(); using var runtime = new R(Fixture(), actor, graph, new[] { "guard" });
        var changes = new List<(int, int)>(); actor.Shields.Changed += (old, current) => changes.Add((old, current));
        Apply(runtime, "next", R.ActionKind.NextRound);
        Check(runtime.CurrentEvaluation.Values["shield"].Number == 0 && actor.Shields.Total == 18 && runtime.GetSnapshot().GuardPayments == 2,
            "new-round condition samples after old segment expiry, before the new service");
        Check(changes.SequenceEqual(new[] { (18, 18) }), "expiry and renewal publish one final equal-total notification");
    }
    private static void TrialIsolation()
    {
        var compilation = FormationGridControlCompiler.Compile(G.CreateLogicExample(K.Counter));
        using var one = new FormationEditableTrial(compilation, new Actor(), Fixture()); one.Apply(Signal("first"));
        using var two = new FormationEditableTrial(compilation, new Actor(), Fixture());
        Check(one.Runtime.Circuit.CounterPhases["logic"] == 1 && two.Runtime.Circuit.CounterPhases.GetValueOrDefault("logic") == 0,
            "compiled definitions do not share counter memory between trial instances");
        Check(!two.Apply(Signal("first")).Duplicate, "each trial owns its own event deduplication history");
        var actor = new Actor { Health = 20 }; actor.Shields.AddOrdinary(4);
        var guard = new FormationEditableTrial(G.CreateGuardAssaultExample(), actor, Fixture());
        actor.Shields.ReplaceSource("other-item", 5); var spent = guard.GetSnapshot().EnergySpent; guard.Dispose(); guard.Dispose();
        Check(actor.Shields.Total == 9 && actor.Shields.GetRemaining("other-item") == 5 && actor.Health == 20 && guard.GetSnapshot().EnergySpent == spent,
            "disposing editable trial removes only its guard, keeps actor, ordinary and other source, and refunds nothing");
        Throws<ObjectDisposedException>(() => guard.Apply(Signal("after-close")), "closed trial cannot perform another operation");
    }

    private static void Report(bool passed, string? error)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "project.godot"))) root = root.Parent;
        if (root is null) throw new Exception("workspace not found");
        var folder = Path.Combine(root.FullName, "docs", "美术规范", "五格复杂阵盘自由构筑_20261109"); Directory.CreateDirectory(folder);
        var result = new { date = "2026-11-09", passed, error, scope = "Pure C# real grid -> typed compiler -> editable runtime, no Godot build or GUI. Fixture values do not define formal balance.",
            groups = Groups, assertion_count = _checks, checks = CheckMessages, game_or_save_loaded = false, historical_assertions_reused = false };
        File.WriteAllText(Path.Combine(folder, passed ? "纯核心验收.json" : "纯核心验收失败.json"), JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
