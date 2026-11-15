using System.Text.Json;
using Kind = FormationGridModel.Kind;
var model = FormationGridModel.CreateGuardAssaultExample();
var issues = model.GetIssues();
if (issues.Count != 0) throw new Exception(string.Join(";", issues));
var compiled = FormationGridControlCompiler.Compile(model);
if (compiled.Circuit.Nodes.Count != 8 || compiled.Circuit.Links.Count != 7 || compiled.PoweredOutputIds.Count != 2 || model.Complexity != 9) throw new Exception("5x5 proof counts");
if (!FormationGridModel.TryDeserialize(model.Serialize(), out var restored) || restored!.GetIssues().Count != 0 || restored.Serialize() != model.Serialize()) throw new Exception("5x5 roundtrip");
var observations = new Dictionary<string, FormationControlValue> { ["health"] = FormationControlValue.Numeric(20, FormationValueUnit.Percent) };
var low = compiled.Circuit.Evaluate(observations, new("first", "spell_played"));
if (low.GuardRequests["guard"] != FormationTruth.True || low.AttackRequests.Count != 0) throw new Exception("physical guard logic");
observations["health"] = FormationControlValue.Numeric(80, FormationValueUnit.Percent);
var high = compiled.Circuit.Evaluate(observations, new("second", "spell_played"));
if (high.GuardRequests["guard"] != FormationTruth.False || high.AttackRequests.Single() != "attack") throw new Exception("physical attack logic");
var initial = model.Serialize(); if (model.TryResize(4) || model.Serialize() != initial) throw new Exception("atomic shrink");
foreach (var legacy in new[] { FormationGridModel.CreateStarter(), FormationGridModel.CreateLogicExample(Kind.Counter), FormationGridModel.CreateLogicExample(Kind.Split) })
{
    if (legacy.GetIssues().Count > 0 || !FormationGridModel.TryDeserialize(legacy.Serialize(), out _)) throw new Exception("legacy compatibility");
    _ = FormationGridControlCompiler.Compile(legacy);
}
var beforeRoutes = model.Serialize(); var beforeError = model.LastError;
var routes = FormationGridFlowRoutes.Build(model); var routeChecks = 0;
void RouteIs(string from, string fromPort, string to, string toPort, FormationGridModel.PortKind kind, params FormationGridModel.Cell[] cells)
{
    var route = routes.Single(r => r.FromComponentId == from && r.FromPortName == fromPort && r.ToComponentId == to && r.ToPortName == toPort && r.Kind == kind);
    if (!route.WireCells.SequenceEqual(cells)) throw new Exception("route order: " + from + "→" + to); routeChecks++;
}
if (routes.Count != 9) throw new Exception("physical route count");
RouteIs("health", "value", "compare", "value", FormationGridModel.PortKind.Signal, new FormationGridModel.Cell(4, 4));
RouteIs("compare", "state", "branch", "value", FormationGridModel.PortKind.Signal);
RouteIs("branch", "a", "guard", "permit", FormationGridModel.PortKind.Signal, new FormationGridModel.Cell(4, 0));
RouteIs("branch", "b", "invert", "state", FormationGridModel.PortKind.Signal, new FormationGridModel.Cell(3, 1));
RouteIs("invert", "state", "switch", "permit", FormationGridModel.PortKind.Signal, new(3, 3), new(2, 3));
RouteIs("cast", "event", "switch", "event", FormationGridModel.PortKind.Signal);
RouteIs("switch", "event", "attack", "event", FormationGridModel.PortKind.Signal, new(1, 4), new(0, 4));
RouteIs("source", "energy", "guard", "energy", FormationGridModel.PortKind.Energy, new(0, 1), new(0, 0), new(1, 0));
RouteIs("source", "energy", "attack", "energy", FormationGridModel.PortKind.Energy, new FormationGridModel.Cell(0, 1));
if (model.Serialize() != beforeRoutes || model.LastError != beforeError) throw new Exception("read-only extraction"); routeChecks++;
var splitRoutes = FormationGridFlowRoutes.Build(FormationGridModel.CreateLogicExample(Kind.Split)).Where(r => r.FromComponentId == "split" && r.ToComponentId == "merge").ToArray();
if (splitRoutes.Length != 2 || splitRoutes.Select(r => r.FromPortName).Distinct().Count() != 2 || !splitRoutes.Any(r => r.WireCells.Count == 0) || !splitRoutes.Any(r => r.WireCells.Count == 2)) throw new Exception("split routes merged"); routeChecks++;
FormationGridModel Copy() => FormationGridModel.TryDeserialize(model.Serialize(), out var copy) ? copy! : throw new Exception("copy");
var broken = Copy(); broken.RemoveWire(new(0, 1));
if (FormationGridFlowRoutes.Build(broken).Any(r => r.Kind == FormationGridModel.PortKind.Energy)) throw new Exception("broken power invented"); routeChecks++;
broken.TryPlaceWire(new(0, 1), FormationGridModel.PortKind.Signal);
if (FormationGridFlowRoutes.Build(broken).Any(r => r.Kind == FormationGridModel.PortKind.Energy)) throw new Exception("power crossed red wire"); routeChecks++;
var wrongUnit = Copy(); wrongUnit.TryConfigureComponent("compare", FormationGridModel.DefaultSettingsFor(Kind.Compare) with { Unit = FormationValueUnit.Mana });
if (FormationGridFlowRoutes.Build(wrongUnit).Any(r => r.FromComponentId == "health" && r.ToComponentId == "compare")) throw new Exception("wrong unit route emitted"); routeChecks++;
var directPower = new FormationGridModel(); directPower.TryPlaceComponent(Kind.Source, new(0, 2), id: "power"); directPower.TryPlaceComponent(Kind.Ward, new(2, 2), 1, "receiver");
if (FormationGridFlowRoutes.Build(directPower).Single(r => r.Kind == FormationGridModel.PortKind.Energy).WireCells.Count != 0) throw new Exception("direct power fabricated wire cells"); routeChecks++;
var proof = new { size = model.BoardSize, complexity = model.Complexity, limit = model.BoardComplexityLimit,
    occupied = model.Components.Values.Sum(c => c.Cells.Count), wire_cells = model.Wires.Count, empty = 25 - model.Components.Values.Sum(c => c.Cells.Count) - model.Wires.Count,
    issues, links = compiled.Circuit.Links, sources = model.GetPowerSources(), diagram = model.Serialize() };
var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.godot/test-results/formation_guard_search/model_proof.json"));
Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("FORMATION_GRID_5X5_PROOF_PASS size=5 nodes=8 links=7 powered=2 complexity=9/10 empty=1");
Console.WriteLine($"FORMATION_GRID_FLOW_ROUTES_PASS checks={routeChecks} routes=9 direct_signal=2 shared_energy_prefix=true");
