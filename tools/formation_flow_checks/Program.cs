using System.Text.Json;
using G = FormationGridModel;
using R = FormationGuardAssaultRuntime;
using F = FormationFlowPlayback;
using C = FormationControlCircuit;
using K = FormationGridModel.Kind;
using P = FormationGridModel.PortKind;
using Cell = FormationGridModel.Cell;

internal static class Program
{
    private static int _checks;
    private static readonly List<object> Groups = [];
    private static int Main()
    {
        try
        {
            Run("ordered physical paths and shared power prefix", Routes);
            Run("successful output and causal delivery use its real payment", PaidFlow);
            Run("blocked event and failed quota do not pretend to transmit energy", Blocked);
            Run("known false and numeric zero transmit; unknown does not", ZeroFalseUnknown);
            Run("unknown renewal uses held permission without invented signal", HeldRenewal);
            Run("filters and counters stop the visual path at the real gate", LogicGates);
            Run("merge departs on first arrival rather than waiting as AND", MergeFirstArrival);
            Run("duplicate and repeated plan construction cannot replay transactions", DuplicateAndReplay);
            Run("legacy signal trial never claims real payment", LegacyTrial);
            Report(true, null); Console.WriteLine($"FORMATION_FLOW_CORE_PASS groups={Groups.Count} checks={_checks}"); return 0;
        }
        catch (Exception error) { Report(false, error.ToString()); Console.Error.WriteLine(error); return 1; }
    }
    private static void Run(string name, Action action) { var before = _checks; action(); Groups.Add(new { name, passed = true, checks = _checks - before }); Console.WriteLine("PASS " + name); }
    private static void Check(bool passed, string message) { _checks++; if (!passed) throw new Exception(message); }
    private static R.Settings Fixture() => new(12, 12, 3, 6, 6, 6, 2, 18, 2, 3, 9, 3, 1, 30, 60);
    private sealed class Actor : IFormationGuardActor
    {
        public int Health { get; set; } = 100;
        public int MaxHealth => 100;
        public int Mana => 20;
        public bool IsAlive => Health > 0;
        public FormationShieldLedger Shields { get; } = new();
        public int EnemyHealth { get; private set; } = 100;
        public bool TryAttack(int amount) { if (!IsAlive || EnemyHealth <= 0) return false; EnemyHealth = Math.Max(0, EnemyHealth - amount); return true; }
    }
    private sealed class Session : IDisposable
    {
        public G Model { get; }
        public Actor Actor { get; } = new();
        public FormationEditableTrial Trial { get; }
        public IReadOnlyDictionary<string, R.OutputState> Before { get; private set; } = null!;
        public R.Result Result { get; private set; } = null!;
        public R.Input Input { get; private set; } = null!;
        public Session(G? model = null, R.Settings? fixture = null) { Model = model ?? G.CreateGuardAssaultExample(); Trial = new(Model, Actor, fixture ?? Fixture()); }
        public F.Plan Apply(R.Input input)
        {
            Input = input; Before = Trial.OutputStates; Result = Trial.Apply(input);
            return F.Build(Model, Trial.Compilation, Trial.Runtime.CurrentEvaluation, Before, Result, Trial.OutputStates, input.Id);
        }
        public F.Plan Step(string id, R.ActionKind kind, int value = 0, bool flag = true) => Apply(new(id, kind, value, flag));
        public F.Plan Signal(string id, int amp = 1, string tag = "spell") => Apply(new(id, R.ActionKind.Signal, EventName: "spell_played", SourceTag: tag, Amplitude: amp));
        public void Dispose() => Trial.Dispose();
    }
    private static void Routes()
    {
        var model = G.CreateGuardAssaultExample(); var routes = FormationGridFlowRoutes.Build(model);
        Check(routes.Count == 9 && routes.Count(r => r.Kind == P.Signal) == 7 && routes.Count(r => r.Kind == P.Energy) == 2, "nine real routes, not a decorative all-pairs network");
        var guard = routes.Single(r => r.Kind == P.Energy && r.ToComponentId == "guard");
        var attack = routes.Single(r => r.Kind == P.Energy && r.ToComponentId == "attack");
        Check(guard.WireCells.SequenceEqual(new[] { new Cell(0, 1), new Cell(0, 0), new Cell(1, 0) }) && attack.WireCells.SequenceEqual(new[] { new Cell(0, 1) }), "both power routes share the actual T prefix and preserve source-to-target order");
        foreach (var route in routes)
        {
            Check(route.WireCells.All(c => model.Wires.TryGetValue(c, out var kind) && kind == route.Kind && model.GetComponentAt(c) is null), "route never crosses foreign line or body");
            Check(route.WireCells.Zip(route.WireCells.Skip(1)).All(pair => Math.Abs(pair.First.X - pair.Second.X) + Math.Abs(pair.First.Y - pair.Second.Y) == 1), "wire sequence has no teleport jump");
        }
        Check(model.RemoveWire(new(0, 1)) && FormationGridFlowRoutes.Build(model).All(r => r.Kind != P.Energy), "broken shared stem does not keep either energy route");
        Check(guard.WireCells.Count == 3, "previous route snapshot is not mutated by a later draft edit");
    }
    private static void Causality(F.Plan plan, C circuit)
    {
        foreach (var item in plan.Items)
        {
            var incoming = plan.Items.Where(p => !p.IsEnergy && p.Route.ToComponentId == item.Route.FromComponentId).ToArray();
            if (item.IsEnergy) incoming = plan.Items.Where(p => !p.IsEnergy && p.Route.ToComponentId == item.Route.ToComponentId).ToArray();
            if (incoming.Length == 0) continue;
            var node = item.IsEnergy ? item.Route.ToComponentId : item.Route.FromComponentId;
            var arrival = circuit.Nodes[node].Kind == C.NodeKind.EventMerge ? incoming.Min(p => p.End) : incoming.Max(p => p.End);
            Check(item.Delay + .0001f >= arrival + F.NodePause, "downstream animation waits for the real upstream arrival and processing pause");
        }
    }
    private static void PaidFlow()
    {
        using var s = new Session(); var plan = s.Step("low", R.ActionKind.SetHealth, 20);
        Check(s.Trial.GetSnapshot().EnergySpent == 2 && plan.PaidOutputIds.SequenceEqual(new[] { "guard" }), "guard's actual payment determines the energy target");
        Check(plan.Items.Count(i => i.IsEnergy) == 1 && plan.Items.Single(i => i.IsEnergy).Route.ToComponentId == "guard", "guard startup never lights the attack power branch");
        Check(plan.OccurrenceId == "low" && plan.Items.All(i => i.OccurrenceId == "low"), "all frames share the same real control operation identity");
        Causality(plan, s.Trial.Runtime.Circuit);
        s.Step("high", R.ActionKind.SetHealth, 80); plan = s.Signal("attack");
        Check(s.Actor.EnemyHealth == 91 && s.Trial.GetSnapshot().SourceEnergy == 7 && plan.PaidOutputIds.SequenceEqual(new[] { "attack" }), "new spell uses the already committed attack payment");
        Check(plan.Items.Single(i => i.IsEnergy).Route.ToComponentId == "attack" && plan.Items.Any(i => !i.IsEnergy && i.Route.FromComponentId == "switch" && i.Route.ToComponentId == "attack"), "attack plan includes the actual event route and its own power route");
        Causality(plan, s.Trial.Runtime.Circuit);
        Check(Math.Abs(plan.Duration - plan.Items.Max(i => i.End)) < .0001f && plan.Duration > 0, "playback duration covers the complete causal sequence");
    }
    private static void Blocked()
    {
        using var s = new Session(); s.Step("low", R.ActionKind.SetHealth, 20); var plan = s.Signal("guarded");
        Check(plan.Items.Any(i => !i.IsEnergy && i.Route.FromComponentId == "cast" && i.Route.ToComponentId == "switch") && !plan.Items.Any(i => i.Route.FromComponentId == "switch" && i.Route.ToComponentId == "attack"), "event reaches the actual closed switch and stops there");
        Check(plan.PaidOutputIds.Count == 0 && plan.Items.All(i => !i.IsEnergy), "repeated true guard permission is not another payment");
        s.Step("high", R.ActionKind.SetHealth, 80); s.Signal("attack-1"); plan = s.Signal("quota-denied");
        Check(plan.Items.Any(i => i.Route.FromComponentId == "switch" && i.Route.ToComponentId == "attack") && plan.Items.All(i => !i.IsEnergy) && plan.PaidOutputIds.Count == 0, "valid request still has no gold flow when quota denies payment");
        using var poor = new Session(fixture: Fixture() with { InitialEnergy = 0, RechargePerRound = 0 }); plan = poor.Signal("no-energy");
        Check(poor.Actor.EnemyHealth == 100 && plan.Items.All(i => !i.IsEnergy) && plan.PaidOutputIds.Count == 0, "source shortage cannot pretend energy passed");
    }
    private static void ZeroFalseUnknown()
    {
        using var s = new Session(); var plan = s.Step("zero", R.ActionKind.SetHealth, 0);
        Check(plan.Items.Any(i => i.Route.FromComponentId == "health" && i.Value is { Type: FormationSignalType.Number, Number: 0, Known: true }), "known zero is a numeric signal, not missing input");
        Check(plan.Items.All(i => !i.IsEnergy), "dead-owner permission cannot fabricate paid service");
        using var high = new Session(); plan = high.Step("eighty", R.ActionKind.SetHealth, 80);
        Check(plan.Items.Any(i => i.Route.FromComponentId == "compare" && i.IsLow && i.Value?.State == FormationTruth.False), "false switch state visibly travels as low level");
        plan = high.Step("unknown", R.ActionKind.Observation, flag: false);
        Check(plan.Items.Count == 0, "unknown sample does not create a travelling zero or fake event");
    }
    private static void HeldRenewal()
    {
        using var s = new Session(); s.Step("low", R.ActionKind.SetHealth, 20); s.Step("unknown", R.ActionKind.Observation, flag: false);
        var plan = s.Step("next", R.ActionKind.NextRound);
        Check(s.Trial.GetSnapshot().GuardPayments == 2 && plan.PaidOutputIds.SequenceEqual(new[] { "guard" }), "held unknown permission renews only when real payment committed");
        Check(plan.Items.Count == 1 && plan.Items[0].IsEnergy && plan.Items[0].UsesHeldPermit, "renewal uses retained permission without inventing another red signal");
    }
    private static void LogicGates()
    {
        using var counter = new Session(G.CreateLogicExample(K.Counter)); var plan = counter.Signal("one");
        Check(plan.Items.Any(i => i.Route.ToComponentId == "logic") && !plan.Items.Any(i => i.Route.FromComponentId == "logic") && plan.PaidOutputIds.Count == 0, "not-yet-full counter stops propagation after its input");
        counter.Signal("two"); plan = counter.Signal("three");
        Check(plan.Items.Any(i => i.Route.FromComponentId == "logic") && plan.Items.Count(i => i.IsEnergy) == 1, "Nth event propagates and lights only committed Ward power");
        using var filter = new Session(G.CreateLogicExample(K.Filter)); plan = filter.Signal("too-small", amp: 1);
        Check(plan.Items.Any(i => i.Route.ToComponentId == "filter") && !plan.Items.Any(i => i.Route.FromComponentId == "filter"), "amplitude filter stops at the actual filter");
        plan = filter.Signal("large", amp: 2); Check(plan.Items.Any(i => i.Route.FromComponentId == "filter") && plan.PaidOutputIds.SequenceEqual(new[] { "ward" }), "qualifying original event continues to paid output");
    }
    private static void MergeFirstArrival()
    {
        using var s = new Session(G.CreateLogicExample(K.Split)); var plan = s.Signal("two-paths");
        var incoming = plan.Items.Where(i => !i.IsEnergy && i.Route.FromComponentId == "split" && i.Route.ToComponentId == "merge").ToArray();
        var outgoing = plan.Items.Single(i => !i.IsEnergy && i.Route.FromComponentId == "merge");
        Check(incoming.Length == 2 && incoming.Min(i => i.End) < incoming.Max(i => i.End), "fixture really has two different physical transit lengths");
        Check(Math.Abs(outgoing.Delay - incoming.Min(i => i.End) - F.NodePause) < .0001f, "OR-style merge departs after the first arrival, not both");
        Check(s.Trial.GetSnapshot().EnergySpent == 2 && plan.PaidOutputIds.Count == 1, "two travelling branches still pay once for one real output");
        Causality(plan, s.Trial.Runtime.Circuit);
    }
    private static void DuplicateAndReplay()
    {
        using var s = new Session(); var original = s.Signal("original"); var snapshot = s.Trial.GetSnapshot(); var result = s.Result;
        for (var i = 0; i < 3; i++)
        {
            var replay = F.Build(s.Model, s.Trial.Compilation, s.Trial.Runtime.CurrentEvaluation, s.Before, result, s.Trial.OutputStates, "original");
            Check(replay.Items.Count == original.Items.Count && replay.OccurrenceId == original.OccurrenceId && s.Trial.GetSnapshot() == snapshot, "reconstructing or replaying the immutable plan never calls the world runtime");
        }
        var duplicate = s.Signal("original");
        Check(duplicate.Duplicate && duplicate.Items.Count == 0 && duplicate.PaidOutputIds.Count == 0 && s.Trial.GetSnapshot() == snapshot, "same occurrence creates no automatic signal or power replay");
    }
    private static void LegacyTrial()
    {
        var model = G.CreateStarter(); var simulation = new FormationGridSimulation(model);
        var result = simulation.Send("spell_played", "spell", 1); var plan = F.BuildLegacy(model, result);
        Check(result.OutputNodeIds.Count == 1 && plan.Items.Count == 1 && plan.Items.All(i => !i.IsEnergy) && plan.PaidOutputIds.Count == 0, "legacy request-only trial must not draw paid power");
        Check(F.BuildLegacy(model, simulation.RepeatLast()).Items.Count == 0, "legacy duplicate also leaves the route still");
        Check(F.BuildLegacy(model, simulation.Send("spell_played", "spell", 0)).Items.Count == 0, "legacy zero amplitude is not a visual pulse");
    }
    private static void Report(bool passed, string? error)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "project.godot"))) root = root.Parent;
        if (root is null) throw new Exception("workspace not found");
        var folder = Path.Combine(root.FullName, "docs", "美术规范", "五格复杂阵盘自由构筑_20261109", "分类与传导"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, passed ? "传导纯核心验收.json" : "传导纯核心验收失败.json"), JsonSerializer.Serialize(new { date = "2026-11-09", passed, error, assertion_count = _checks, groups = Groups,
            scope = "Actual runtime results and payment deltas -> physical playback plans; no Godot build or native frame checks.", old_baseline_reused = false }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
